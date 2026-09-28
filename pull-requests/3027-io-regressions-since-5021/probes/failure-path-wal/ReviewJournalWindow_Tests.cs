#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>
    /// Review probe for the WalWrite journal guard (a failed append never truncates an outstanding
    /// header journal): a reclaiming checkpoint tears the data header while rotating the WAL salt,
    /// with the header journal outstanding, and a committer appends in the window between the
    /// checkpoint releasing its locks and stopping the engine (forced through the coordination
    /// signal raised in that window). The journal must survive so the next open repairs the header.
    /// </summary>
    public class ReviewJournalWindow_Tests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Append_in_the_window_after_a_torn_checkpoint_header_keeps_the_journal(bool ioFailure)
        {
            using var data = new TornHeaderData { IoFailure = ioFailure };
            using var log = new MemoryStream();
            var signals = new WindowSignals { Ready = () => data.Torn };
            var settings = new EngineSettings { DataStream = data, LogStream = log, CoordinationSignals = signals };
            settings.CheckpointStage = stage => { if (stage == "before-reclaim") data.Armed = true; };
            var acknowledged = false;
            Exception lateFailure = null;
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 20).Select(id => new BsonDocument { ["_id"] = id, ["p"] = new string('x', 300) }));
                Exception checkpointFailure = null;
                var worker = new Thread(() =>
                {
                    signals.OnWindow = () =>
                    {
                        var committer = new Thread(() =>
                        {
                            try
                            {
                                db.GetCollection("late").Insert(new BsonDocument { ["_id"] = 1 });
                                acknowledged = true;
                            }
                            catch (Exception ex) { lateFailure = ex; }
                        });
                        committer.Start();
                        committer.Join(TimeSpan.FromSeconds(30)).Should().BeTrue();
                    };
                    try { db.Checkpoint(); }
                    catch (Exception ex) { checkpointFailure = ex; }
                });
                worker.Start();
                worker.Join(TimeSpan.FromSeconds(60)).Should().BeTrue();
                data.Torn.Should().BeTrue("the salt rotation tore the data header");
                checkpointFailure.Should().NotBeNull();
                signals.WindowRan.Should().BeTrue();
            }

            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(data.ToArray()), LogStream = new MemoryStream(log.ToArray())
            }));
            recovered.GetCollection("rows").Count().Should().Be(20, "the header is restored from its journal (late: acknowledged={0}, failure={1})", acknowledged, lateFailure?.Message);
            recovered.GetCollection("late").Count().Should().Be(acknowledged ? 1 : 0);
        }

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

        private sealed class TornHeaderData : MemoryStream
        {
            internal volatile bool Armed;
            internal bool Torn, IoFailure;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed && Position == 0 && count == Constants.PAGE_SIZE)
                {
                    Armed = false;
                    base.Write(buffer, offset, 40);
                    Torn = true;
                    throw IoFailure ? new IOException("injected torn header write") : new UnauthorizedAccessException("injected torn header write");
                }
                base.Write(buffer, offset, count);
            }
        }
    }
}
#endif
