#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using LiteDB.Tests.Issues;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    [Collection(NativeFileSyncCollection.Name)]
    public class ReviewFindings_Tests
    {
        private const int Rows = 64;

        /// <summary>
        /// Finding: the fresh-engine data proof is skipped for caller streams (_dataIsFile), although
        /// the engine reports caller FileStreams durable. Same sequence as
        /// FreshEngineDurability_Tests.Fresh_engine_after_a_salt_rotation_whose_data_sync_failed(false),
        /// with the files passed as DataStream/LogStream.
        /// </summary>
        [Fact]
        public void Caller_streams_fresh_engine_after_a_salt_rotation_whose_data_sync_failed()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename);

            var settings = power.Settings();
            settings.CheckpointStage = stage => { if (stage == "before-reclaim") power.DataFails = true; };
            using (var first = new LiteDatabase(new LiteEngine(settings)))
            {
                first.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(first, value);
                first.Checkpoint();
                DurableLogFlush(first).Should().BeFalse();
            }

            bool durable;
            using (var second = new LiteDatabase(new LiteEngine(power.Settings())))
            {
                Update(second, 6);
                durable = DurableLogFlush(second);
            }
            var after = power.AfterPowerLoss(Rows);
            if (durable) after.Should().Be(6, "a commit reported durable survives the power loss");
        }

        /// <summary>
        /// Finding: storage where neither file syncs empties the WAL ("as before") with its backfill in
        /// the OS cache only. Commits acknowledged durable before that stay recoverable only while no
        /// log sync precedes a data sync. A fresh engine's open repairs a torn WAL tail (a crashed
        /// writer) with a log sync and no data proof: the earlier engine's truncation becomes durable,
        /// its backfill never did.
        /// </summary>
        [Fact]
        public void Fresh_engine_open_repairing_a_torn_tail_does_not_lose_commits_acknowledged_durable()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new BothFilesPowerModel(file.Filename);

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
            using (var b = new LiteDatabase(file.Filename))
            {
                b.Checkpoint(); // backfill and truncation reach the OS cache only
                b.GetCollection("other").Insert(new BsonDocument { ["_id"] = 1 }); // keep the WAL file
                DurableLogFlush(b).Should().BeFalse();
            }
            // A writer that crashed mid-append leaves a partial frame.
            using (var log = new FileStream(FileHelper.GetLogFile(file.Filename), FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                log.Write(Enumerable.Repeat((byte)0x5A, 100).ToArray(), 0, 100);

            power.DataFails = power.LogFails = false; // storage syncs again
            using (var c = new LiteDatabase(file.Filename))
            {
                c.GetCollection("rows").Count().Should().Be(Rows);
            }

            power.AfterPowerLoss(Values).Should().Equal(new[] { 5 }, "value 5 was acknowledged durable");
        }

        /// <summary>
        /// Finding: same, but the fresh engine's first log sync is its checkpoint's header journal,
        /// before the checkpoint's own data sync; a power loss between the two loses the commits.
        /// </summary>
        [Fact]
        public void Fresh_engine_checkpoint_does_not_make_an_unsynced_truncation_durable_before_its_backfill()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new BothFilesPowerModel(file.Filename);

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
            using (var b = new LiteDatabase(file.Filename))
            {
                b.Checkpoint();
                b.GetCollection("other").Insert(new BsonDocument { ["_id"] = 1 });
            }

            power.DataFails = power.LogFails = false;
            (byte[] data, byte[] log) image = default;
            var settings = new EngineSettings { Filename = file.Filename };
            settings.CheckpointStage = stage => { if (stage == "data-page" && image.data == null) image = power.Capture(); };
            using (var c = new LiteDatabase(new LiteEngine(settings)))
            {
                c.Checkpoint();
            }
            image.data.Should().NotBeNull();
            BothFilesPowerModel.Open(image, Values).Should().Equal(new[] { 5 }, "value 5 was acknowledged durable");
        }

        /// <summary>
        /// Finding (liveness): a connection's kept-WAL length survives another connection emptying the WAL,
        /// so this connection's automatic checkpoints stay deferred until the new WAL doubles the old one.
        /// </summary>
        [Fact]
        public void Kept_wal_rationing_is_not_stale_after_another_connection_empties_the_wal()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new SyncPowerLossModel(file.Filename);

            using var engine = new SharedEngine(power.Settings());
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            db.CheckpointSize = 0;
            for (var id = 1; id <= 200; id++) db.GetCollection("log").Insert(new BsonDocument { ["_id"] = id, ["text"] = new string('t', 3000) });
            power.DataFails = true;
            db.Checkpoint(); // kept: _keptWalLength ~ 200 pages
            var kept = new FileInfo(FileHelper.GetLogFile(file.Filename)).Length;
            kept.Should().BeGreaterThan(0);
            power.DataFails = false;

            using (var other = new SharedEngine(power.Settings()))
            using (var otherDb = new LiteDatabase(other, disposeOnClose: false))
                otherDb.Checkpoint();
            new FileInfo(FileHelper.GetLogFile(file.Filename)).Length.Should().Be(0);

            db.CheckpointSize = 10;
            for (var id = 201; id <= 300; id++) db.GetCollection("log").Insert(new BsonDocument { ["_id"] = id, ["text"] = new string('t', 3000) });
            new FileInfo(FileHelper.GetLogFile(file.Filename)).Length.Should().BeLessThan(40L * Constants.PAGE_SIZE * 2,
                "automatic checkpoints resume once another connection emptied the kept WAL");
        }

        /// <summary>Performance: data/log syncs per direct-mode request (open, one insert, close).</summary>
        [Fact]
        public void Measure_syncs_per_request()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            int data = 0, log = 0;
            var dataName = Path.GetFullPath(file.Filename);
            NativeFileSync.SimulateErrno = path =>
            {
                if (string.Equals(Path.GetFullPath(path), dataName, StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref data);
                else Interlocked.Increment(ref log);
                return 0;
            };
            try
            {
                for (var i = 0; i < 10; i++)
                    using (var db = new LiteDatabase(file.Filename)) db.GetCollection("log").Insert(new BsonDocument { ["_id"] = i });
                Console.WriteLine($"MEASURE direct: data={data / 10.0} log={log / 10.0} per request");
                using (var db = new LiteDatabase(file.Filename)) db.CheckpointSize = 0;
                data = log = 0;
                for (var i = 10; i < 20; i++)
                    using (var db = new LiteDatabase(file.Filename)) db.GetCollection("log").Insert(new BsonDocument { ["_id"] = i });
                Console.WriteLine($"MEASURE direct checkpoint=0: data={data / 10.0} log={log / 10.0} per request");
                data = log = 0;
                using (var shared = new LiteDatabase($"Filename={file.Filename};Connection=shared"))
                {
                    shared.GetCollection("log").Insert(new BsonDocument { ["_id"] = 100 });
                    data = log = 0;
                    for (var i = 101; i < 121; i++) shared.GetCollection("log").Insert(new BsonDocument { ["_id"] = i });
                }
                Console.WriteLine($"MEASURE shared: data={data / 20.0} log={log / 20.0} per operation");
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        /// <summary>
        /// Behavioral kill for an unconditional header shortcut: a shared connection's later commit after
        /// another connection left a rotated header in the OS cache only.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Shared_connection_proves_again_after_another_connection_left_its_header_unsynced(bool dataSyncsAgain)
        {
            using var file = new TempFile();
            Setup(file.Filename);
            using var power = new FilePowerLossModel(file.Filename);
            using var engine = new SharedEngine(new EngineSettings { Filename = file.Filename });
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            db.CheckpointSize = 0;
            Update(db, 1);
            DurableLogFlush(db).Should().BeTrue();

            var settings = new EngineSettings { Filename = file.Filename };
            settings.CheckpointStage = stage => { if (stage == "before-reclaim") power.DataFails = true; };
            using (var otherEngine = new SharedEngine(settings))
            using (var other = new LiteDatabase(otherEngine, disposeOnClose: false))
            {
                Update(other, 2);
                other.Checkpoint();
            }
            power.DataFails = !dataSyncsAgain;
            Update(db, 3);
            var durable = DurableLogFlush(db);
            durable.Should().Be(dataSyncsAgain);
            var values = power.AfterPowerLoss(Values);
            if (durable) values.Should().Equal(new[] { 3 }, "a commit reported durable survives the power loss");
        }

        /// <summary>Performance: data syncs per shared operation while retired slots are reused (healthy storage).</summary>
        [Fact]
        public void Measure_slot_reuse_data_syncs_per_shared_operation()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            var logName = FileHelper.GetLogFile(file.Filename);
            int data = 0;
            var dataName = Path.GetFullPath(file.Filename);
            NativeFileSync.SimulateErrno = path =>
            {
                if (string.Equals(Path.GetFullPath(path), dataName, StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref data);
                return 0;
            };
            try
            {
                using var engine = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(db, value);
                using (var reader = engine.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    var t = new Thread(() =>
                    {
                        for (var value = 6; value <= 9; value++) Update(db, value);
                        db.Checkpoint();
                    });
                    t.Start(); t.Join();
                }
                var before = File.ReadAllBytes(logName);
                data = 0;
                const int ops = 5;
                for (var value = 10; value < 10 + ops; value++) Update(db, value);
                var after = File.ReadAllBytes(logName);
                var reused = Enumerable.Range(0, before.Length / WalChecksum.FrameSize).Count(frame =>
                    !before.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize)
                        .SequenceEqual(after.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize)));
                Console.WriteLine($"MEASURE slot reuse: data syncs={data} over {ops} shared ops, reused frames={reused}");
                reused.Should().BeGreaterThan(0);
                data.Should().BeLessThan(ops, "healthy storage: the header shortcut should cover slot reuse too");
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        /// <summary>The external P1 in the default file-backed configuration (SharedFileHandles, no caller streams).</summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void File_backed_independent_shared_connection_after_a_root_left_in_the_os_cache(bool dataSyncsAgain)
        {
            using var file = new TempFile();
            Setup(file.Filename);
            var logName = FileHelper.GetLogFile(file.Filename);
            using var power = new FilePowerLossModel(file.Filename);
            var armed = false;
            using var first = new SharedEngine(new EngineSettings
            {
                Filename = file.Filename,
                CheckpointStage = stage => { if (armed && stage == "retirement-before-header-write") power.DataFails = true; }
            });
            using var firstDb = new LiteDatabase(first, disposeOnClose: false);
            firstDb.CheckpointSize = 0;
            for (var value = 1; value <= 5; value++) Update(firstDb, value);
            Exception failure = null;
            using (var reader = first.Query("rows", new Query()))
            {
                reader.Read().Should().BeTrue();
                var thread = new Thread(() =>
                {
                    try
                    {
                        for (var value = 6; value <= 9; value++) Update(firstDb, value);
                        armed = true;
                        firstDb.Checkpoint();
                        armed = false;
                    }
                    catch (Exception ex) { failure = ex; }
                });
                thread.Start(); thread.Join();
            }
            failure.Should().BeNull();
            power.DataFails.Should().BeTrue("the checkpoint reached its root publication");
            BitConverter.ToInt64(SyncPowerLossModel.ReadShared(file.Filename), WalRetirement.RootPosition).Should().BeGreaterThan(0);

            power.DataFails = !dataSyncsAgain;
            var before = SyncPowerLossModel.ReadShared(logName);
            using var second = new SharedEngine(new EngineSettings { Filename = file.Filename });
            using var secondDb = new LiteDatabase(second, disposeOnClose: false);
            Update(secondDb, 10);
            var after = SyncPowerLossModel.ReadShared(logName);
            var changed = Enumerable.Range(0, before.Length / WalChecksum.FrameSize).Count(frame =>
                !before.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize)
                    .SequenceEqual(after.Skip(frame * WalChecksum.FrameSize).Take(WalChecksum.FrameSize)));
            var durable = DurableLogFlush(secondDb);
            Console.WriteLine($"MEASURE P1 file-backed dataSyncsAgain={dataSyncsAgain}: changed={changed} durable={durable}");
            durable.Should().Be(dataSyncsAgain);
            if (!dataSyncsAgain) changed.Should().Be(0, "no retired slot is reused before the data file syncs");
            else changed.Should().BeGreaterThan(0, "control: slots are reused once the data file syncs");
            var values = power.AfterPowerLoss(Values);
            if (durable) values.Should().Equal(new[] { 10 });
        }

        private static void Setup(string filename)
        {
            using var setup = new LiteDatabase(filename);
            setup.GetCollection("rows").Insert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, 0)));
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, Rows).Select(id => MvccRetirementScenario.Document(id, value)));

        private static int[] Values(LiteDatabase db) =>
            db.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Distinct().ToArray();

        private static bool DurableLogFlush(LiteDatabase db) =>
            db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;

        /// <summary>FilePowerLossModel with both files able to answer "cannot sync".</summary>
        private sealed class BothFilesPowerModel : IDisposable
        {
            private readonly string _data, _log;
            private readonly object _gate = new object();
            private byte[] _durableData, _durableLog;
            internal volatile bool DataFails, LogFails;

            internal BothFilesPowerModel(string filename)
            {
                _data = Path.GetFullPath(filename);
                _log = Path.GetFullPath(FileHelper.GetLogFile(filename));
                _durableData = Read(_data);
                _durableLog = Read(_log);
                NativeFileSync.SimulateErrno = path =>
                {
                    var name = Path.GetFullPath(path);
                    if (string.Equals(name, _log, StringComparison.OrdinalIgnoreCase))
                    {
                        if (LogFails) return 22;
                        lock (_gate) _durableLog = Read(_log);
                    }
                    else if (string.Equals(name, _data, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DataFails) return 22;
                        lock (_gate) _durableData = Read(_data);
                    }
                    return 0;
                };
            }

            internal (byte[] data, byte[] log) Capture()
            {
                lock (_gate) return (_durableData, _durableLog);
            }

            internal T AfterPowerLoss<T>(Func<LiteDatabase, T> read) => Open(this.Capture(), read);

            internal static T Open<T>((byte[] data, byte[] log) image, Func<LiteDatabase, T> read)
            {
                using var temp = new TempFile();
                File.WriteAllBytes(temp.Filename, image.data);
                File.WriteAllBytes(FileHelper.GetLogFile(temp.Filename), image.log);
                var hook = NativeFileSync.SimulateErrno;
                NativeFileSync.SimulateErrno = null;
                try
                {
                    using var db = new LiteDatabase(temp.Filename);
                    return read(db);
                }
                finally
                {
                    NativeFileSync.SimulateErrno = hook;
                    File.Delete(FileHelper.GetLogFile(temp.Filename));
                }
            }

            private static byte[] Read(string filename)
            {
                if (!File.Exists(filename)) return new byte[0];
                using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var bytes = new byte[stream.Length];
                stream.ReadFully(bytes, 0, bytes.Length);
                return bytes;
            }

            public void Dispose() => NativeFileSync.SimulateErrno = null;
        }
    }
}
#endif
