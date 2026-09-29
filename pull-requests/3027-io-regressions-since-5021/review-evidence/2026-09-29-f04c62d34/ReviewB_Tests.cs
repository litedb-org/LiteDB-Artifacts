#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Tests.Issues;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>Independent review repros (reviewer B): sticky failures, overwrite barrier, WAL limit.</summary>
    [Trait("Category", "IoSafety")]
    [Collection(NativeFileSyncCollection.Name)]
    public class ReviewB_Tests
    {
        private const long Limit = 64 * 1024;

        /// <summary>
        /// Decision D / note 12: opted out, on a log that cannot sync, the WAL limit holds since no
        /// checkpoint can drain the WAL. An engine that has not itself seen a log barrier fail (a reopen,
        /// every operation of a shared connection) assumes the log syncs, so the limit never holds and
        /// $database.walKept says false.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Wal_limit_on_an_unsyncable_log_holds_for_a_fresh_engine(bool shared)
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            using (var setup = new LiteDatabase(file.Filename)) setup.GetCollection("rows").EnsureIndex("value");
            using var power = new FilePowerLossModel(file.Filename) { LogFails = true };
            var connection = $"Filename={file.Filename};durable commits=false;wal limit=64KB";
            try
            {
                var rows = 0;
                // The engine that finds out: the limit holds there (as OverwriteBarrier_Tests shows).
                using (var engine = new LiteEngine(new EngineSettings { Filename = file.Filename, DurableCommits = false, WalLimit = Limit }))
                using (var db = new LiteDatabase(engine, disposeOnClose: false))
                {
                    db.CheckpointSize = 0;
                    db.GetCollection("rows").Insert(Row(++rows));
                    engine.Checkpoint().Should().Be(0);
                    while (Info(db)["logFileSize"].AsInt64 <= Limit) db.GetCollection("rows").Insert(Row(++rows));
                    Action refused = () => db.GetCollection("rows").Insert(Row(rows + 1));
                    refused.Should().Throw<IOException>().WithMessage("*passed the WAL limit*");
                }
                new FileInfo(logName).Length.Should().BeGreaterThan(Limit, "the WAL was kept");

                var files = (SyncPowerLossModel.ReadShared(file.Filename), SyncPowerLossModel.ReadShared(logName));
                using (var db = new LiteDatabase(connection + (shared ? ";connection=shared" : "")))
                {
                    if (shared) db.Checkpoint(); // a checkpoint in this connection finds out again
                    Action insert = () => db.GetCollection("rows").Insert(Row(rows + 1));
                    insert.Should().Throw<IOException>().WithMessage("*passed the WAL limit*", "the log still cannot sync");
                    SyncPowerLossModel.ReadShared(logName).Should().Equal(files.Item2);
                    Info(db)["walKept"].AsBoolean.Should().BeTrue("no checkpoint can drain the WAL while the log cannot sync");
                }
            }
            finally { File.Delete(logName); }
        }

        /// <summary>
        /// Decision 6 / note 13: once a data sync failed with an I/O error the failure is recorded and no
        /// write, and no sync on the handle that failed, may follow in that engine. A write whose source
        /// is $database (SELECT .. INTO) records the failure through walKept's data sync (StopLater), then
        /// commits, and the automatic checkpoint after that commit retries the data sync (fsyncgate: the
        /// retry answers success) and backfills the data file and empties the WAL.
        /// </summary>
        [Fact]
        public void Write_that_records_a_failed_data_sync_runs_no_checkpoint_after_it()
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 16).Select(Row));
            var dataPath = Path.GetFullPath(file.Filename);
            var dataErrno = 0;
            var failedAt = 0;
            var dataSyncs = 0;
            NativeFileSync.SimulateErrno = path =>
            {
                if (!string.Equals(Path.GetFullPath(path), dataPath, StringComparison.OrdinalIgnoreCase)) return 0;
                var n = Interlocked.Increment(ref dataSyncs);
                var errno = dataErrno;
                if (errno == 5) { failedAt = n; dataErrno = 0; } // one EIO; a retry on the handle then "succeeds"
                return errno;
            };
            try
            {
                using var db = new LiteDatabase(file.Filename);
                db.CheckpointSize = 1; // every commit starts an automatic checkpoint
                dataErrno = 22; // cannot sync: the WAL is kept, walKept retries the data sync
                db.GetCollection("rows").Update(Row(1));
                new FileInfo(logName).Length.Should().BeGreaterThan(0);
                dataErrno = 5;
                var dataBefore = SyncPowerLossModel.ReadShared(file.Filename);

                db.Execute("SELECT $ INTO dbinfo FROM $database");

                failedAt.Should().BeGreaterThan(0, "walKept's data sync failed with EIO and was recorded");
                var observed = $"data syncs after the failed one: {dataSyncs - failedAt}; data file changed: " +
                    $"{!SyncPowerLossModel.ReadShared(file.Filename).SequenceEqual(dataBefore)}; log length now: " +
                    $"{(File.Exists(logName) ? new FileInfo(logName).Length : 0)}";
                dataSyncs.Should().Be(failedAt, "no sync may be retried on the handle whose sync failed ({0})", observed);
                SyncPowerLossModel.ReadShared(file.Filename).Should().Equal(dataBefore, "no checkpoint may write after the recorded failure");
            }
            finally
            {
                NativeFileSync.SimulateErrno = null;
                File.Delete(logName);
            }
        }

        /// <summary>
        /// Decision 2/6: after a write failure the engine reopens read-only "from the files as they are",
        /// and reads keep working. For :memory: and :temp: the "files" are streams the engine's own factory
        /// owns: the failure's teardown disposes them and the reopen starts from new, empty streams, so
        /// every acknowledged row silently disappears (or the reopen fails).
        /// </summary>
        [Theory]
        [InlineData(":memory:")]
        [InlineData(":temp:")]
        public void Volatile_database_keeps_its_rows_readable_after_a_write_failure(string filename)
        {
            using var engine = new LiteEngine(new EngineSettings { Filename = filename });
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            db.CheckpointSize = 0;
            var rows = db.GetCollection("rows");
            rows.Insert(Enumerable.Range(1, 10).Select(Row));
            var failure = new IOException("injected WAL write failure (disk full)");
            var armed = true;
            engine.SimulateDiskWriteFail = _ =>
            {
                if (!armed) return;
                armed = false;
                throw failure;
            };
            Action write = () => rows.Insert(Row(11));
            write.Should().Throw<IOException>();

            string observed;
            try { observed = string.Join(",", rows.FindAll().Select(x => x["_id"].AsInt32)); }
            catch (Exception ex) { observed = ex.GetType().Name + ": " + ex.Message; }
            observed.Should().Be(string.Join(",", Enumerable.Range(1, 10)), "reads keep working after a write failure");
        }

        /// <summary>
        /// Decision 2 / note 6: a failed write of the sort spill file (ENOSPC on the temp file of a large
        /// ORDER BY) is neither a failed read nor a damaged file, yet Handle stops the engine without a
        /// record (the IOException names no file), so it stays closed and every later read throws.
        /// </summary>
        [Fact]
        public void Failed_sort_spill_keeps_reads_working()
        {
            using var file = new TempFile();
            using var db = new LiteDatabase(file.Filename);
            var rows = db.GetCollection("rows");
            rows.Insert(Enumerable.Range(1, 6000).Select(id => new BsonDocument { ["_id"] = id, ["k"] = new string((char)('a' + id % 26), 200) + id }));
            EngineState.ObserveSortSpill = _ => throw new IOException("No space left on device (sort temp file)");
            try
            {
                Action sorted = () => rows.Query().OrderBy("k").ToList();
                sorted.Should().Throw<IOException>();
            }
            finally { EngineState.ObserveSortSpill = null; }

            string observed;
            try { observed = rows.FindById(1)?["_id"].ToString() ?? "null"; }
            catch (Exception ex) { observed = ex.GetType().Name + ": " + ex.Message; }
            observed.Should().Be("1", "reads keep working after a failed temp-file write");
        }

        private static BsonDocument Row(int id) => new BsonDocument
        {
            ["_id"] = id, ["value"] = id % 5, ["payload"] = new string((char)('a' + id % 26), 1500) + id
        };

        private static BsonDocument Info(LiteDatabase db) => db.GetCollection("$database").FindAll().Single();
    }
}
#endif
