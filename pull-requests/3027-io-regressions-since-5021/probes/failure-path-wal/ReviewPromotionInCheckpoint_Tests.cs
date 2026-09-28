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
    /// Review probe: the MVCC promotion runs inside a retiring checkpoint (PrepareRetirement), whose
    /// thread holds the WAL writer, index write and commit locks. A torn promotion header now runs
    /// CompleteStop (engine close) on that thread while it holds those locks. It must not hang,
    /// and the header must be recoverable from the journal.
    /// </summary>
    public class ReviewPromotionInCheckpoint_Tests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Torn_mvcc_promotion_inside_a_retiring_checkpoint_stops_cleanly(bool ioFailure)
        {
            using var data = new TornHeaderData { IoFailure = ioFailure };
            using var log = new MemoryStream();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, CompactStorage = CompactStorageMode.Legacy })))
            {
                db.GetCollection("rows").Insert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, 0)));
            }
            data.ToArray()[HeaderPage.P_FILE_VERSION].Should().BeLessThan(HeaderPage.MVCC_FILE_VERSION);

            Exception checkpointFailure = null;
            var settings = new EngineSettings { DataStream = data, LogStream = log };
            var engine = new LiteEngine(settings);
            var db2 = new LiteDatabase(engine, disposeOnClose: false);
            db2.CheckpointSize = 0;
            for (var value = 1; value <= 5; value++) Update(db2, value);
            var reader = engine.Query("rows", new Query());
            reader.Read().Should().BeTrue();
            var worker = new Thread(() =>
            {
                for (var value = 6; value <= 9; value++) Update(db2, value);
                data.Armed = true;
                try { db2.Checkpoint(); }
                catch (Exception ex) { checkpointFailure = ex; }
            });
            worker.Start();
            worker.Join(TimeSpan.FromSeconds(60)).Should().BeTrue("the failed promotion must not deadlock the checkpoint");
            data.Torn.Should().BeTrue("the promotion tore the data header");
            checkpointFailure.Should().NotBeNull();
            checkpointFailure.Should().BeOfType<IOException>();
            checkpointFailure.StackTrace.Should().Contain("WriteFileVersion");
            Action later = () => db2.GetCollection("rows").Count();
            later.Should().Throw<Exception>().Where(e => e.Message.Contains("Engine closed"));
            try { reader.Dispose(); } catch { }
            try { db2.Dispose(); } catch { }
            try { engine.Dispose(); } catch { }

            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(data.ToArray()), LogStream = new MemoryStream(log.ToArray())
            }));
            recovered.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Should().HaveCount(64).And.OnlyContain(v => v == 9);
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, value)));

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
