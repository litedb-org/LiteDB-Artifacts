#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>
    /// Review probe for 506678daf: the kept WAL (KeepsSyncedWal) is decided per engine. An engine
    /// that did not itself sync the WAL empties a WAL whose frames an earlier engine synced, once the
    /// data file cannot sync and its first data proof fails (the data header changed in the OS cache).
    /// Image: data file as of its last sync, WAL as the OS wrote it back.
    /// </summary>
    public class ReviewKeptWalAcrossEngines_Tests
    {
        private const int Rows = 64;

        [Theory]
        [InlineData("reopen")]
        [InlineData("shared")]
        public void Wal_kept_by_one_engine_is_emptied_by_the_next(string mode)
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
            using var power = new SyncPowerLossModel(file.Filename);
            var logName = FileHelper.GetLogFile(file.Filename);

            if (mode == "reopen")
            {
                using (var engine = new LiteEngine(power.Settings()))
                using (var db = new LiteDatabase(engine, disposeOnClose: false))
                {
                    db.CheckpointSize = 0;
                    Commit(db);
                    DurableLogFlush(db).Should().BeTrue("acknowledged durable");
                    power.DataFails = true;
                    db.Checkpoint();
                    OpenImage(power.Capture(), 1, 40);
                }
                new FileInfo(logName).Length.Should().BeGreaterThan(0, "the first engine kept its synced WAL");
                using (var engine = new LiteEngine(power.Settings()))
                using (var db = new LiteDatabase(engine, disposeOnClose: false))
                {
                    db.CheckpointSize = 0;
                    db.Checkpoint();
                }
            }
            else
            {
                using var shared = new SharedEngine(power.Settings());
                using var db = new LiteDatabase(shared, disposeOnClose: false);
                // The data file stops syncing first: the commit's data proof matches the durable
                // header, so the commit's log sync still runs and the commit is durable.
                power.DataFails = true;
                Commit(db);
                OpenImage((power.Capture().Data, power.Capture().Log), 1, 40);
                db.Checkpoint(); // an operation that may have synced the WAL: it keeps it
                db.Checkpoint(); // a fresh operation's engine
                db.Checkpoint();
            }

            var wal = SyncPowerLossModel.ReadShared(logName);
            Console.WriteLine($"PROBE {mode}: wal bytes now={wal.Length}");
            OpenImage((power.Capture().Data, wal), 1, 40);
        }

        private static void OpenImage((byte[] Data, byte[] Log) image, int value, int extra)
        {
            using var copy = new TempFile();
            File.WriteAllBytes(copy.Filename, image.Data);
            File.WriteAllBytes(FileHelper.GetLogFile(copy.Filename), image.Log);
            try
            {
                using var recovered = new LiteDatabase(copy.Filename);
                recovered.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().Should().Equal(new[] { value },
                    "the commit acknowledged durable survives");
                recovered.GetCollection("extra").Count().Should().Be(extra);
            }
            finally { File.Delete(FileHelper.GetLogFile(copy.Filename)); }
        }

        /// <summary>Updates every row and inserts new pages (the header's page count changes).</summary>
        private static void Commit(LiteDatabase db)
        {
            db.BeginTrans();
            db.GetCollection("rows").Upsert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 1)));
            db.GetCollection("extra").Insert(Enumerable.Range(1, 40).Select(id => new BsonDocument { ["_id"] = id, ["p"] = new string('e', 3000) }));
            db.Commit();
        }

        private static bool DurableLogFlush(LiteDatabase db) =>
            db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;
    }
}
#endif
