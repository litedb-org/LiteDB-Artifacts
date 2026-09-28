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
    /// Review probe for 5136e938e: every log sync must wait for a data sync after "cannot sync".
    /// An encrypted log stream syncs its file whenever it is created (AesStream's constructor calls
    /// FlushToDisk), outside SyncLogBarrier. A new log stream created while the data file cannot sync
    /// (the pool grows under concurrent readers) then makes the emptied WAL durable ahead of the
    /// backfill, as in the reviewer's P1.
    /// </summary>
    public class ReviewEncryptedLogSync_Tests
    {
        private const int Rows = 64;

        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public void New_log_stream_while_the_data_file_cannot_sync(string password)
        {
            using var file = new TempFile();
            var cs = password == null ? file.Filename : $"Filename={file.Filename};Password={password}";
            using (var setup = new LiteDatabase(cs))
            {
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
                setup.GetCollection("rows").EnsureIndex("value");
            }
            using var power = new SyncPowerLossModel(file.Filename);
            var settings = power.Settings();
            settings.Password = password;
            var engine = new LiteEngine(settings);
            var db = new LiteDatabase(engine, disposeOnClose: false);
            try
            {
                db.CheckpointSize = 0;
                // Grow the data stream pool while the WAL is empty and the data file syncs.
                ConcurrentReaders(engine).Should().Be(0);
                for (var value = 1; value <= 5; value++)
                {
                    Update(db, value);
                    DurableLogFlush(db).Should().BeTrue();
                }
                power.DataFails = power.LogFails = true;
                db.Checkpoint();
                power.LogFails = false;
                Update(db, 6);
                DurableLogFlush(db).Should().BeFalse("the data file cannot sync");

                // Concurrent readers of WAL pages: the log stream pool creates new streams.
                ConcurrentReaders(engine).Should().Be(0);

                var (data, log) = power.Capture();
                using var image = new TempFile();
                File.WriteAllBytes(image.Filename, data);
                File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), log);
                try
                {
                    var ics = password == null ? image.Filename : $"Filename={image.Filename};Password={password}";
                    using var recovered = new LiteDatabase(ics);
                    var values = recovered.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().ToArray();
                    values.Should().Equal(new[] { 5 }, "commits acknowledged durable before the storage stopped syncing survive");
                }
                finally { File.Delete(FileHelper.GetLogFile(image.Filename)); }
            }
            finally
            {
                power.DataFails = power.LogFails = false;
                try { db.Dispose(); } catch { }
                try { engine.Dispose(); } catch { }
            }
        }

        /// <summary>The encrypted open over a data file that cannot sync is refused; did it sync the WAL first?</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public void Refused_reopen_while_the_data_file_cannot_sync(string password)
        {
            using var file = new TempFile();
            var cs = password == null ? file.Filename : $"Filename={file.Filename};Password={password}";
            using (var setup = new LiteDatabase(cs))
            {
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
            }
            using var power = new SyncPowerLossModel(file.Filename);
            var settings = power.Settings();
            settings.Password = password;
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(db, value);
                power.DataFails = power.LogFails = true;
                db.Checkpoint();
                power.LogFails = false;
            }
            Exception reopenFailure = null;
            try
            {
                var again = power.Settings();
                again.Password = password;
                using var engine = new LiteEngine(again);
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.GetCollection("rows").Count();
            }
            catch (Exception ex) { reopenFailure = ex; }
            Console.WriteLine($"PROBE reopen password={password ?? "none"} failure={reopenFailure?.GetType().Name}: {reopenFailure?.Message}");

            var (data, log) = power.Capture();
            using var image = new TempFile();
            File.WriteAllBytes(image.Filename, data);
            File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), log);
            try
            {
                var ics = password == null ? image.Filename : $"Filename={image.Filename};Password={password}";
                using var recovered = new LiteDatabase(ics);
                recovered.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().Should().Equal(new[] { 5 });
            }
            finally { File.Delete(FileHelper.GetLogFile(image.Filename)); }
        }

        /// <summary>
        /// Reordered writeback: the OS writes the WAL's cached content back, the data file's not.
        /// Image = data as of its last sync, WAL as it is now.
        /// </summary>
        [Theory]
        [InlineData(false)] // only the data file cannot sync
        [InlineData(true)]  // neither syncs
        public void Writeback_of_the_wal_ahead_of_the_backfill(bool neither)
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
            using var power = new SyncPowerLossModel(file.Filename);
            using (var engine = new LiteEngine(power.Settings()))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(db, value);
                power.DataFails = true;
                power.LogFails = neither;
                db.Checkpoint();
                var data = power.Capture().Data;
                var log = SyncPowerLossModel.ReadShared(FileHelper.GetLogFile(file.Filename));
                using var image = new TempFile();
                File.WriteAllBytes(image.Filename, data);
                File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), log);
                try
                {
                    using var recovered = new LiteDatabase(image.Filename);
                    var values = recovered.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().ToArray();
                    Console.WriteLine($"PROBE writeback neither={neither} walBytes={log.Length} values={string.Join(",", values)}");
                    values.Should().Equal(new[] { 5 });
                }
                finally { File.Delete(FileHelper.GetLogFile(image.Filename)); }
                power.DataFails = power.LogFails = false;
            }
        }

        private static int ConcurrentReaders(LiteEngine engine)
        {
            using var barrier = new Barrier(6);
            var failures = 0;
            var threads = Enumerable.Range(0, 6).Select(_ => new Thread(() =>
            {
                try
                {
                    using var reader = engine.Query("rows", new Query());
                    reader.Read();
                    barrier.SignalAndWait(TimeSpan.FromSeconds(20));
                }
                catch (Exception ex) { Interlocked.Increment(ref failures); Console.WriteLine("PROBE reader failure: " + ex.GetType().Name + ": " + ex.Message); }
            })).ToArray();
            foreach (var t in threads) t.Start();
            foreach (var t in threads) t.Join();
            return failures;
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, value)));

        private static bool DurableLogFlush(LiteDatabase db) =>
            db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;
    }
}
#endif
