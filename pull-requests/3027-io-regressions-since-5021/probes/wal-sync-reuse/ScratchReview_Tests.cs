#if DEBUG || TESTING
using System;
using System.IO;
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
    public class ScratchReview_Tests
    {
        private readonly ITestOutputHelper _output;
        public ScratchReview_Tests(ITestOutputHelper output) { _output = output; }

        /// <summary>
        /// A: the log stops syncing during the retiring checkpoint (after ProveRetirementSyncs), at the
        /// witness records' sync. The data file keeps syncing, so the root is published durably while
        /// the witness records it names are not.
        /// </summary>
        [Theory]
        [InlineData("retirement-before-record-write")]
        [InlineData("retirement-records-flushed")]
        public void Scratch_log_stops_syncing_at_retirement_records(string stage)
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename);
            try
            {
                using var engine = new SharedEngine(new EngineSettings
                {
                    Filename = file.Filename,
                    CheckpointStage = s => { if (s == stage) power.LogFails = true; }
                });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                var durable = 0;
                for (var value = 1; value <= 5; value++)
                {
                    Update(db, value);
                    if (DurableLogFlush(db)) durable = value;
                }
                using (var reader = engine.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Worker(() =>
                    {
                        for (var value = 6; value <= 9; value++)
                        {
                            Update(db, value);
                            if (DurableLogFlush(db)) durable = value;
                        }
                        db.Checkpoint();
                    });
                }
                power.LogFails.Should().BeTrue();
                durable.Should().Be(9);
                BitConverter.ToInt64(SyncPowerLossModel.ReadShared(file.Filename), WalRetirement.RootPosition)
                    .Should().BeGreaterThan(0, "root published");
                DurableLogFlush(db).Should().BeFalse();
                power.AfterPowerLoss(64).Should().Be(durable);
            }
            finally { EngineState.SimulateProcessCrash = null; }
        }

        /// <summary>
        /// B: a caller DataStream (a FileStream on storage that answers "cannot sync") without a LogStream:
        /// the WAL is an in-memory MemoryStream, whose Flush "syncs".
        /// </summary>
        [Fact]
        public void Scratch_datastream_with_memory_wal_on_unsyncable_data_file()
        {
            using var file = new TempFile();
            var name = Path.GetFullPath(file.Filename);
            NativeFileSync.SimulateErrno = path => Path.GetFullPath(path) == name ? 22 : 0;
            try
            {
                using var stream = new FileStream(file.Filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                var checkpoints = 0;
                var validations = 0;
                using var db = new LiteDatabase(new LiteEngine(new EngineSettings
                {
                    DataStream = stream,
                    CheckpointStage = s => { if (s == "data-flushed") checkpoints++; if (s == "before-index-lock") validations++; }
                }));
                db.CheckpointSize = 10;
                var col = db.GetCollection("docs");
                var sw = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < 400; i++)
                {
                    col.Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('x', 3000) });
                    if (i % 100 == 99)
                        _output.WriteLine($"i={i} logFileSize={db.GetCollection("$database").FindAll().Single()["logFileSize"]} checkpoints={checkpoints} elapsed={sw.ElapsedMilliseconds}ms durable={DurableLogFlush(db)}");
                }
                var size = db.GetCollection("$database").FindAll().Single()["logFileSize"].AsInt32;
                size.Should().BeLessThan(10 * 8192 * 4, "the in-memory WAL should be emptied by full checkpoints");
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        /// <summary>
        /// C: both files answer "cannot sync" at a full checkpoint (the WAL is emptied, as before);
        /// then the log syncs again while the data file still cannot. An independent connection's
        /// commit reports durable.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Scratch_neither_syncs_then_log_recovers(bool secondConnection)
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename);
            using var first = new SharedEngine(new EngineSettings { Filename = file.Filename });
            using var firstDb = new LiteDatabase(first, disposeOnClose: false);
            firstDb.CheckpointSize = 0;
            for (var value = 1; value <= 5; value++)
            {
                Update(firstDb, value);
                DurableLogFlush(firstDb).Should().BeTrue();
            }
            power.DataFails = power.LogFails = true;
            firstDb.Checkpoint();
            power.LogFails = false; // the log syncs again; the data file still cannot
            using var second = secondConnection ? new SharedEngine(new EngineSettings { Filename = file.Filename }) : null;
            using var writer = second == null ? firstDb : new LiteDatabase(second, disposeOnClose: false);
            Update(writer, 6);
            var durable6 = DurableLogFlush(writer);
            _output.WriteLine($"commit 6 durableLogFlush={durable6}");
            if (!secondConnection) writer.Checkpoint(); // a later barrier syncs the log for real
            var recovered = power.AfterPowerLoss(64);
            _output.WriteLine($"after power loss value={recovered}");
            recovered.Should().BeGreaterOrEqualTo(durable6 ? 6 : 5, "commits acknowledged as durable survive");
        }

        /// <summary>
        /// D: a full checkpoint whose data file syncs the backfill but answers "cannot sync" at the salt
        /// rotation (RotateWalSalt). The WAL is emptied; an independent connection then commits with the
        /// new salt, which is in the data file's OS cache only.
        /// </summary>
        [Fact]
        public void Scratch_data_stops_syncing_at_salt_rotation_independent_connection()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename);
            using var first = new SharedEngine(new EngineSettings
            {
                Filename = file.Filename,
                CheckpointStage = s => { if (s == "before-reclaim") power.DataFails = true; }
            });
            using var firstDb = new LiteDatabase(first, disposeOnClose: false);
            firstDb.CheckpointSize = 0;
            for (var value = 1; value <= 5; value++)
            {
                Update(firstDb, value);
                DurableLogFlush(firstDb).Should().BeTrue();
            }
            firstDb.Checkpoint();
            power.DataFails.Should().BeTrue();
            DurableLogFlush(firstDb).Should().BeFalse();

            using var second = new SharedEngine(new EngineSettings { Filename = file.Filename });
            using var secondDb = new LiteDatabase(second, disposeOnClose: false);
            Update(secondDb, 6);
            var durable6 = DurableLogFlush(secondDb);
            _output.WriteLine($"second connection commit 6 durableLogFlush={durable6}");
            var recovered = power.AfterPowerLoss(64);
            _output.WriteLine($"after power loss value={recovered}");
            recovered.Should().Be(durable6 ? 6 : recovered, "a commit acknowledged as durable survives");
        }

        /// <summary>
        /// E: file-based, the data file answers "cannot sync", the WAL syncs: count checkpoints per commit.
        /// </summary>
        [Fact]
        public void Scratch_file_data_unsyncable_checkpoint_per_commit()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename) { DataFails = true };
            var checkpoints = 0;
            using var db = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                Filename = file.Filename,
                CheckpointStage = s => { if (s == "data-flushed") checkpoints++; }
            }));
            db.CheckpointSize = 10;
            var col = db.GetCollection("docs");
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 300; i++)
            {
                col.Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('x', 3000) });
                if (i % 100 == 99)
                    _output.WriteLine($"i={i} logFileSize={db.GetCollection("$database").FindAll().Single()["logFileSize"]} checkpoints={checkpoints} elapsed={sw.ElapsedMilliseconds}ms");
            }
            throw new Exception("dump");
        }

        /// <summary>
        /// G: a legacy (5.0.21) data file with no WAL, on storage whose data file cannot sync while the
        /// WAL can. The Probe returns false for an empty WAL, so conversion proceeds; then commits.
        /// </summary>
        [Fact]
        public void Scratch_legacy_conversion_with_empty_wal_data_unsyncable_power_loss()
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            File.WriteAllBytes(file.Filename, Entry("crash.db"));
            int before;
            using (var probe = new LiteDatabase(new LiteEngine(new EngineSettings { Filename = file.Filename, ReadOnly = true, LegacyIndexScan = true })))
            {
                before = -1;
                try { before = probe.GetCollection("docs").Count(); } catch (Exception ex) { _output.WriteLine("ro count failed: " + ex.Message); }
            }
            _output.WriteLine($"legacy docs before={before}");
            File.Exists(logName).Should().BeFalse();
            var original = File.ReadAllBytes(file.Filename);
            try
            {
                using (var power = new SyncPowerLossModel(file.Filename) { DataFails = true })
                {
                    bool durable;
                    using (var db = new LiteDatabase(file.Filename))
                    {
                        db.CheckpointSize = 0;
                        db.GetCollection("docs").Insert(new BsonDocument { ["_id"] = 900001, ["value"] = 1 });
                        durable = DurableLogFlush(db);
                        _output.WriteLine($"durable={durable} data header version={SyncPowerLossModel.ReadShared(file.Filename)[HeaderPage.P_FILE_VERSION]}");
                        // image while the process is alive
                        var dataField = typeof(SyncPowerLossModel).GetField("_durableData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var logField = typeof(SyncPowerLossModel).GetField("_durableLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        var d = (byte[])dataField.GetValue(power);
                        var l = (byte[])logField.GetValue(power);
                        _output.WriteLine($"durable data equals original={d.SequenceEqual(original)} durable data version={d[HeaderPage.P_FILE_VERSION]} durable log length={l.Length}");
                        using var image = new TempFile();
                        File.WriteAllBytes(image.Filename, d);
                        File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), l);
                        power.DataFails = false;
                        NativeFileSync.SimulateErrno = null;
                        try
                        {
                            using var reopened = new LiteDatabase(new LiteEngine(new EngineSettings { Filename = image.Filename, ReadOnly = true, LegacyIndexScan = true }));
                            var count = reopened.GetCollection("docs").Count();
                            _output.WriteLine($"image read-only docs={count}");
                        }
                        catch (Exception ex) { _output.WriteLine("image read-only open failed: " + ex.GetType().Name + ": " + ex.Message); }
                        try
                        {
                            using var reopened = new LiteDatabase(image.Filename);
                            var count = reopened.GetCollection("docs").Count();
                            _output.WriteLine($"image writable docs={count}");
                        }
                        catch (Exception ex) { _output.WriteLine("image writable open failed: " + ex.GetType().Name + ": " + ex.Message); }
                        finally { File.Delete(FileHelper.GetLogFile(image.Filename)); }
                    }
                }
            }
            finally { File.Delete(logName); }
            throw new Exception("dump");
        }

        /// <summary>
        /// G2: as G, then the converting engine closes and a new engine (another connection) commits.
        /// </summary>
        [Fact]
        public void Scratch_legacy_conversion_then_second_engine_power_loss()
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            File.WriteAllBytes(file.Filename, Entry("crash.db"));
            try
            {
                using var power = new SyncPowerLossModel(file.Filename) { DataFails = true };
                using (var db = new LiteDatabase(file.Filename))
                {
                    db.GetCollection("docs").Insert(new BsonDocument { ["_id"] = 900001, ["value"] = 1 });
                    _output.WriteLine($"first durable={DurableLogFlush(db)}");
                }
                _output.WriteLine($"after first close: data version (OS cache)={SyncPowerLossModel.ReadShared(file.Filename)[HeaderPage.P_FILE_VERSION]} log exists={File.Exists(logName)}");
                bool durable;
                using (var db = new LiteDatabase(file.Filename))
                {
                    db.GetCollection("docs").Insert(new BsonDocument { ["_id"] = 900002, ["value"] = 2 });
                    durable = DurableLogFlush(db);
                    _output.WriteLine($"second durable={durable}");
                    var dataField = typeof(SyncPowerLossModel).GetField("_durableData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var logField = typeof(SyncPowerLossModel).GetField("_durableLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var d = (byte[])dataField.GetValue(power);
                    var l = (byte[])logField.GetValue(power);
                    _output.WriteLine($"durable data version={d[HeaderPage.P_FILE_VERSION]} durable log length={l.Length}");
                    using var image = new TempFile();
                    File.WriteAllBytes(image.Filename, d);
                    File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), l);
                    var saved = NativeFileSync.SimulateErrno;
                    NativeFileSync.SimulateErrno = null;
                    try
                    {
                        using var reopened = new LiteDatabase(image.Filename);
                        var found = reopened.GetCollection("docs").FindById(900002) != null;
                        _output.WriteLine($"image docs={reopened.GetCollection("docs").Count()} has 900002={found}");
                        if (durable) found.Should().BeTrue("a commit acknowledged as durable survives the power loss");
                    }
                    finally
                    {
                        NativeFileSync.SimulateErrno = saved;
                        File.Delete(FileHelper.GetLogFile(image.Filename));
                    }
                }
            }
            finally { File.Delete(logName); }
        }

        /// <summary>H: encrypted variant of RetiredSlotPowerLoss independent-connection tests.</summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Scratch_encrypted_independent_connection(bool dataStillFails)
        {
            using var file = new TempFile();
            const string pw = "secret";
            using (var setup = new LiteDatabase(new ConnectionString { Filename = file.Filename, Password = pw }))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, 0)));
            var logName = FileHelper.GetLogFile(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename);
            try
            {
                using var first = new SharedEngine(new EngineSettings { Filename = file.Filename, Password = pw });
                using var firstDb = new LiteDatabase(first, disposeOnClose: false);
                firstDb.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(firstDb, value);
                using (var reader = first.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Worker(() =>
                    {
                        for (var value = 6; value <= 9; value++) Update(firstDb, value);
                        EngineState.SimulateProcessCrash = phase => { if (phase == "checkpoint-before-page-write") power.DataFails = true; };
                        firstDb.Checkpoint();
                        EngineState.SimulateProcessCrash = null;
                    });
                }
                power.DataFails.Should().BeTrue();
                if (!dataStillFails) power.DataFails = false;
                var before = SyncPowerLossModel.ReadShared(logName);
                using var second = new SharedEngine(new EngineSettings { Filename = file.Filename, Password = pw });
                using var secondDb = new LiteDatabase(second, disposeOnClose: false);
                Update(secondDb, 10);
                var after = SyncPowerLossModel.ReadShared(logName);
                var changed = Enumerable.Range(0, before.Length / WalChecksum.FrameSize).Count(frame =>
                    !before.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize)
                        .SequenceEqual(after.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize)));
                var durable = DurableLogFlush(secondDb);
                _output.WriteLine($"changed={changed} durable={durable}");
                if (dataStillFails) { changed.Should().Be(0); durable.Should().BeFalse(); }
                else { changed.Should().BeGreaterThan(0); durable.Should().BeTrue(); }
                var dataField = typeof(SyncPowerLossModel).GetField("_durableData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var logField = typeof(SyncPowerLossModel).GetField("_durableLog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                using var image = new TempFile();
                File.WriteAllBytes(image.Filename, (byte[])dataField.GetValue(power));
                File.WriteAllBytes(FileHelper.GetLogFile(image.Filename), (byte[])logField.GetValue(power));
                try
                {
                    using var db = new LiteDatabase(new ConnectionString { Filename = image.Filename, Password = pw });
                    db.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Should().OnlyContain(x => x == 10);
                }
                finally { File.Delete(FileHelper.GetLogFile(image.Filename)); }
            }
            finally { EngineState.SimulateProcessCrash = null; }
        }

        /// <summary>I: rebuild on storage whose data file cannot sync while the WAL can.</summary>
        [Fact]
        public void Scratch_rebuild_data_unsyncable_orphan_temp_log()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            var dir = Path.GetDirectoryName(Path.GetFullPath(file.Filename));
            var stem = Path.GetFileNameWithoutExtension(file.Filename);
            var name = Path.GetFullPath(file.Filename);
            NativeFileSync.SimulateErrno = path => Path.GetFileName(path).Contains("-log") ? 0 : 22;
            try
            {
                using (var db = new LiteDatabase(file.Filename))
                {
                    Update(db, 1);
                    db.Rebuild();
                    Update(db, 2);
                }
                foreach (var f in Directory.GetFiles(dir, stem + "*")) _output.WriteLine($"{Path.GetFileName(f)} {new FileInfo(f).Length}");
            }
            finally
            {
                NativeFileSync.SimulateErrno = null;
                foreach (var f in Directory.GetFiles(dir, stem + "*")) if (f != name) File.Delete(f);
            }
            throw new Exception("dump");
        }

        private static byte[] Entry(string name)
        {
            using var resource = typeof(UnsyncedBackfillPowerLoss_Tests).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.WalCrash_5_0_21.zip");
            using var zip = new System.IO.Compression.ZipArchive(resource, System.IO.Compression.ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }

        private static void Setup(string filename)
        {
            using var setup = new LiteDatabase(filename);
            setup.GetCollection("rows").Insert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, 0)));
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, value)));

        private static bool DurableLogFlush(LiteDatabase db) =>
            db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;

        private static void Worker(Action action)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo failure = null;
            var thread = new System.Threading.Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); }
            });
            thread.Start();
            thread.Join();
            failure?.Throw();
        }
    }
}
#endif
