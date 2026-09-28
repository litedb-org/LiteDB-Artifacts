#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>
    /// Review probe: a retiring checkpoint appends retirement records to the raw WAL outside
    /// WriteLogDisk. A torn (or even cleanly failed) record write leaves the reserved slot invalid
    /// and _logLength advanced; the checkpoint stops the engine only after it released the WAL
    /// writer, commit and index locks. A commit that lands in that window is appended behind the
    /// invalid slot, acknowledged, and silently discarded by the next open's recovery.
    /// The interleaving is forced through the coordination signal raised after the locks are released.
    /// </summary>
    public class ReviewTornRetirement_Tests
    {
        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public void Commit_in_the_window_after_a_torn_retirement_record_survives_recovery(bool torn, bool ioFailure)
        {
            using var data = new MemoryStream();
            using var log = new TearingLog { Torn = torn, IoFailure = ioFailure };
            var signals = new WindowSignals();
            signals.Ready = () => log.Fired;
            var settings = new EngineSettings { DataStream = data, LogStream = log, CoordinationSignals = signals };
            settings.CheckpointStage = stage => { if (stage == "retirement-before-record-write") log.Armed = true; };
            var acknowledged = false;
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, 0)));
                for (var value = 1; value <= 5; value++) Update(db, value);
                using (var reader = engine.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Exception checkpointFailure = null;
                    var worker = new Thread(() =>
                    {
                        for (var value = 6; value <= 9; value++) Update(db, value);
                        signals.OnWindow = () =>
                        {
                            // Runs on the checkpoint thread after it released every lock, before it stops the engine.
                            var committer = new Thread(() =>
                            {
                                try
                                {
                                    db.GetCollection("late").Insert(new BsonDocument { ["_id"] = 1, ["v"] = "acknowledged" });
                                    acknowledged = true;
                                }
                                catch { }
                            });
                            committer.Start();
                            committer.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();
                        };
                        try { db.Checkpoint(); }
                        catch (Exception ex) { checkpointFailure = ex; }
                    });
                    worker.Start();
                    worker.Join(TimeSpan.FromSeconds(60)).Should().BeTrue();
                    log.Fired.Should().BeTrue("the checkpoint reached its retirement record write");
                    checkpointFailure.Should().NotBeNull();
                    signals.WindowRan.Should().BeTrue();
                }
            }

            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(data.ToArray()), LogStream = new MemoryStream(log.ToArray())
            }));
            recovered.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Should().OnlyContain(v => v == 9);
            recovered.GetCollection("late").Count().Should().Be(acknowledged ? 1 : 0,
                "a commit acknowledged in the window must survive recovery (acknowledged={0})", acknowledged);
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, value)));

        private sealed class WindowSignals : ICoordinationSignals
        {
            internal Action OnWindow;
            internal Func<bool> Ready = () => true;
            internal bool WindowRan;
            public void StructuralBegin() { }
            public void StructuralEnd(int version)
            {
                if (OnWindow == null || !Ready()) return;
                var action = Interlocked.Exchange(ref OnWindow, null);
                if (action == null) return;
                WindowRan = true;
                action();
            }
            public void SlotReused() { }
            public void Committed(int version) { }
        }

        private sealed class TearingLog : MemoryStream
        {
            internal volatile bool Armed;
            internal bool Torn, IoFailure, Fired;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed && count == WalChecksum.FrameSize)
                {
                    Armed = false;
                    Fired = true;
                    if (Torn) base.Write(buffer, offset, count / 2);
                    throw IoFailure ? new IOException("injected retirement record failure") : new UnauthorizedAccessException("injected retirement record failure");
                }
                base.Write(buffer, offset, count);
            }
        }
    }
}
#endif
