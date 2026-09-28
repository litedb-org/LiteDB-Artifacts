#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using LiteDB.Tests.Issues;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    [Collection(NativeFileSyncCollection.Name)]
    public class ReReview_Tests
    {
        private const int Rows = 64;

        /// <summary>
        /// A checkpoint on storage where neither file syncs journals the header (OS cache only), then its
        /// header write is torn and the writer dies. Once the storage syncs again, the next open recovers
        /// the header from the journal. The data proof before that open's first log sync now makes the
        /// torn header durable before the journal: a power loss in between leaves a torn header and no
        /// journal, although every commit acknowledged as durable (value 5) was in the data file.
        /// </summary>
        [Fact]
        public void Open_recovering_a_torn_header_never_makes_it_durable_before_its_journal()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
            using var power = new FilePowerLossModel(file.Filename);
            using (var a = new LiteDatabase(file.Filename))
            {
                a.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++)
                {
                    Update(a, value);
                    DurableLogFlush(a).Should().BeTrue();
                }
            }

            power.DataFails = power.LogFails = true;
            var settings = new EngineSettings { Filename = file.Filename };
            settings.CheckpointStage = stage => { if (stage == "data-page") throw new IOException("the writer dies mid-checkpoint"); };
            using (var b = new LiteDatabase(new LiteEngine(settings)))
            {
                b.CheckpointSize = 0;
                b.GetCollection("other").Insert(new BsonDocument { ["_id"] = 1, ["text"] = new string('x', 5000) });
                Action checkpoint = () => b.Checkpoint();
                checkpoint.Should().Throw<Exception>();
            }
            // The header write of that checkpoint, torn in the OS cache.
            using (var data = new FileStream(file.Filename, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                data.Position = 200;
                data.Write(Enumerable.Repeat((byte)0xA5, 3000).ToArray(), 0, 3000);
            }

            power.DataFails = power.LogFails = false; // the storage syncs again
            (byte[] Data, byte[] Log) image = default;
            var hook = NativeFileSync.SimulateErrno;
            var log = Path.GetFullPath(FileHelper.GetLogFile(file.Filename));
            NativeFileSync.SimulateErrno = path =>
            {
                if (image.Data == null && string.Equals(Path.GetFullPath(path), log, StringComparison.OrdinalIgnoreCase)) image = power.Capture();
                return hook(path);
            };
            try
            {
                using var c = new LiteDatabase(file.Filename);
                Values(c).Should().Equal(new[] { 5 }, "the open itself recovers");
            }
            finally { NativeFileSync.SimulateErrno = hook; }

            image.Data.Should().NotBeNull("the open synced the log");
            FilePowerLossModel.Open(image, Values).Should().Equal(new[] { 5 }, "value 5 was acknowledged durable");
            File.Delete(FileHelper.GetLogFile(file.Filename));
        }


        /// <summary>
        /// FreshEngineLogSync_Tests with a password. The encrypted WAL's stream syncs itself when an
        /// engine opens it (AesStream constructor, EncryptedLogPreamble), not through SyncLogBarrier.
        /// </summary>
        [Theory]
        [InlineData(false)] // the open only reads
        [InlineData(true)]  // the open repairs a torn tail
        public void Encrypted_fresh_engine_open_keeps_commits_acknowledged_durable(bool tornTail)
        {
            using var file = new TempFile();
            var cs = $"Filename={file.Filename};Password=secret";
            using (var setup = new LiteDatabase(cs))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
            using var power = new FilePowerLossModel(file.Filename);
            using (var a = new LiteDatabase(cs))
            {
                a.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++)
                {
                    Update(a, value);
                    DurableLogFlush(a).Should().BeTrue();
                }
            }
            using (var b = new LiteDatabase(cs))
            {
                // Both writers exist (their AES streams synced when created); then the storage stops syncing.
                b.GetCollection("other").Insert(new BsonDocument { ["_id"] = 0 });
                power.DataFails = power.LogFails = true;
                b.Checkpoint();
                b.GetCollection("other").Insert(new BsonDocument { ["_id"] = 1 });
                DurableLogFlush(b).Should().BeFalse();
            }
            new FileInfo(FileHelper.GetLogFile(file.Filename)).Length.Should().BeGreaterThan(0);
            if (tornTail)
                using (var log = new FileStream(FileHelper.GetLogFile(file.Filename), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    log.Write(Enumerable.Repeat((byte)0x5A, 100).ToArray(), 0, 100);

            power.DataFails = power.LogFails = false;
            var syncsBefore = power.DataSyncs;
            (byte[] Data, byte[] Log) image;
            using (var c = new LiteDatabase(cs))
            {
                Values(c).Should().Equal(new[] { 5 });
                image = power.Capture();
            }
            Console.WriteLine($"MEASURE encrypted open tornTail={tornTail}: data syncs during open={power.DataSyncs - syncsBefore}");
            OpenEncrypted(image, Values).Should().Equal(new[] { 5 }, "value 5 was acknowledged durable");
            File.Delete(FileHelper.GetLogFile(file.Filename));
        }

        private static T OpenEncrypted<T>((byte[] Data, byte[] Log) captured, Func<LiteDatabase, T> read)
        {
            using var image = new TempFile();
            File.WriteAllBytes(image.Filename, captured.Data);
            File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), captured.Log);
            var hook = NativeFileSync.SimulateErrno;
            NativeFileSync.SimulateErrno = null;
            try
            {
                using var db = new LiteDatabase($"Filename={image.Filename};Password=secret");
                return read(db);
            }
            finally
            {
                NativeFileSync.SimulateErrno = hook;
                File.Delete(FileHelper.GetLogFile(image.Filename));
            }
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, value)));

        private static int[] Values(LiteDatabase db) =>
            db.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().ToArray();

        private static bool DurableLogFlush(LiteDatabase db) =>
            db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;
    }
}
#endif
