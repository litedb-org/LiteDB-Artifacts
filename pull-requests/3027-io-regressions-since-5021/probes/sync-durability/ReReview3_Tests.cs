#if DEBUG || TESTING
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using LiteDB.Tests.Issues;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Regressions
{
    [Collection(NativeFileSyncCollection.Name)]
    public class ReReview3_Tests
    {
        private const int Rows = 64;
        private readonly ITestOutputHelper _output;

        public ReReview3_Tests(ITestOutputHelper output) => _output = output;

        /// <summary>
        /// FreshEngineLogSync_Tests.Open_repairing_a_torn_header_makes_its_journal_durable_first with a
        /// password. The data writer's AesStream syncs the data file when RecoverHeaderJournal creates it,
        /// before SyncLogBarrierUnproven syncs the journal.
        /// </summary>
        [Fact]
        public void Encrypted_open_repairing_a_torn_header_makes_its_journal_durable_first()
        {
            using var file = new TempFile();
            var cs = $"Filename={file.Filename};Password=secret";
            using (var setup = new LiteDatabase(cs))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
            using var power = new FilePowerLossModel(file.Filename);
            using (var db = new LiteDatabase(cs))
            {
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++)
                    db.GetCollection("rows").Upsert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, value)));
            }

            var settings = new EngineSettings { Filename = file.Filename, Password = "secret" };
            settings.CheckpointStage = stage => { if (stage == "data-page") throw new IOException("the writer dies mid-checkpoint"); };
            using (var db = new LiteDatabase(new LiteEngine(settings)))
            {
                db.CheckpointSize = 0;
                db.GetCollection("other").Insert(new BsonDocument { ["_id"] = 1, ["text"] = new string('x', 5000) });
                power.DataFails = power.LogFails = true; // both writers exist; now the storage stops syncing
                Action checkpoint = () => db.Checkpoint();
                checkpoint.Should().Throw<IOException>();
            }
            // That checkpoint's header write, torn in the OS cache (physical page 1 is page 0's ciphertext).
            using (var data = new FileStream(file.Filename, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            {
                data.Position = Constants.PAGE_SIZE + 208;
                data.Write(Enumerable.Repeat((byte)0xA5, 3008).ToArray(), 0, 3008);
            }

            power.DataFails = power.LogFails = false;
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
                using var reopened = new LiteDatabase(cs);
                Values(reopened).Should().Equal(new[] { 5 }, "the open restores the header from its journal");
            }
            finally { NativeFileSync.SimulateErrno = hook; }

            image.Data.Should().NotBeNull("the open synced the log");
            OpenImage(image, "secret", Values).Should().Equal(new[] { 5 }, "value 5 was acknowledged durable");
            File.Delete(FileHelper.GetLogFile(file.Filename));
        }

        /// <summary>
        /// A legacy data file restored beside a converted WAL whose first slot a retiring checkpoint
        /// cleared: RejectConvertedWal reads only frame 0.
        /// </summary>
        [Fact]
        public void Legacy_header_beside_a_converted_wal_whose_first_slot_was_retired()
        {
            var legacy = Fixture("plain.db");
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            try
            {
                File.WriteAllBytes(file.Filename, legacy);
                byte[] wal;
                using (var engine = new SharedEngine(new EngineSettings { Filename = file.Filename }))
                using (var db = new LiteDatabase(engine, disposeOnClose: false))
                {
                    db.CheckpointSize = 0;
                    var big = db.GetCollection("big");
                    void Touch(int value) => big.Upsert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, value)));
                    for (var value = 1; value <= 5; value++) Touch(value);
                    using (var reader = engine.Query("big", new Query()))
                    {
                        reader.Read().Should().BeTrue();
                        var t = new System.Threading.Thread(() => { for (var value = 6; value <= 9; value++) Touch(value); db.Checkpoint(); });
                        t.Start(); t.Join();
                        wal = SyncPowerLossModel.ReadShared(logName);
                    }
                }
                var frame0Blank = wal.Take(WalChecksum.FrameSize).All(b => b == 0);
                _output.WriteLine($"WAL {wal.Length} bytes, frame 0 blank: {frame0Blank}, frame 1 is a frame: {WalChecksum.IsFrame(wal.Skip(WalChecksum.FrameSize).Take(WalChecksum.FrameSize).ToArray(), Constants.PAGE_SIZE)}");
                // What ReclaimLogPages leaves in a retired first slot.
                Array.Clear(wal, 0, WalChecksum.FrameSize);

                File.WriteAllBytes(file.Filename, legacy);
                File.WriteAllBytes(logName, wal);
                Exception failure = null;
                long count = -1;
                try
                {
                    using var db = new LiteDatabase($"Filename={file.Filename};readonly=true;legacy index scan=true");
                    count = db.GetCollection("rows").Count();
                }
                catch (Exception ex) { failure = ex; }
                _output.WriteLine($"read-only open: {(failure == null ? "opened, rows=" + count : failure.GetType().Name + ": " + failure.Message)}");
                var dataAfter = new FileInfo(file.Filename).Length;
                failure = null;
                try
                {
                    using var db = new LiteDatabase(file.Filename);
                    count = db.GetCollection("rows").Count();
                }
                catch (Exception ex) { failure = ex; }
                _output.WriteLine($"writable open: {(failure == null ? "opened, rows=" + count : failure.GetType().Name + ": " + failure.Message)}; data {legacy.Length} -> {new FileInfo(file.Filename).Length} bytes");
                failure.Should().NotBeNull("a converted WAL beside a legacy header must be refused");
                new FileInfo(file.Filename).Length.Should().Be(legacy.Length, "the refused open changes neither file");
            }
            finally { File.Delete(logName); }
        }

        /// <summary>Real 5.0.21 crash images (generated with LiteDB 5.0.21) open under the legacy page bound.</summary>
        [Theory]
        [InlineData("initial", "docs", 200)]
        [InlineData("bigwal", "docs", 400)]
        [InlineData("churn", "b", 50)]
        [InlineData("rollback", "docs", 21)]
        [InlineData("grown", "docs", 300)]
        [InlineData("enc-initial", "docs", 200)]
        [InlineData("enc-bigwal", "docs", 400)]
        [InlineData("enc-churn", "b", 50)]
        [InlineData("enc-rollback", "docs", 21)]
        [InlineData("enc-grown", "docs", 300)]
        public void Legacy_5_0_21_crash_images_open(string name, string collection, int expected)
        {
            const string images = "$SCRATCH/img5021b";
            var password = name.StartsWith("enc-") ? ";Password=secret" : "";
            foreach (var readOnly in new[] { true, false })
            {
                using var file = new TempFile();
                File.Copy(Path.Combine(images, name + "-crash.db"), file.Filename);
                File.Copy(Path.Combine(images, name + "-crash-log.db"), FileHelper.GetLogFile(file.Filename));
                try
                {
                    using var db = new LiteDatabase($"Filename={file.Filename}{password}" + (readOnly ? ";readonly=true;legacy index scan=true" : ""));
                    db.GetCollection(collection).Count().Should().Be(expected, $"{name} readOnly={readOnly}");
                }
                finally { File.Delete(FileHelper.GetLogFile(file.Filename)); }
            }
        }

        private static int[] Values(LiteDatabase db) =>
            db.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().ToArray();

        private static T OpenImage<T>((byte[] Data, byte[] Log) captured, string password, Func<LiteDatabase, T> read)
        {
            using var image = new TempFile();
            File.WriteAllBytes(image.Filename, captured.Data);
            File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), captured.Log);
            var hook = NativeFileSync.SimulateErrno;
            NativeFileSync.SimulateErrno = null;
            try
            {
                using var db = new LiteDatabase($"Filename={image.Filename};Password={password}");
                return read(db);
            }
            finally
            {
                NativeFileSync.SimulateErrno = hook;
                File.Delete(FileHelper.GetLogFile(image.Filename));
            }
        }

        private static byte[] Fixture(string name)
        {
            using var resource = typeof(ReReview3_Tests).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.IndexMigration_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
#endif
