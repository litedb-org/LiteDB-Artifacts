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
    public class ReviewProbe_Tests
    {
        private readonly ITestOutputHelper _out;
        public ReviewProbe_Tests(ITestOutputHelper output) { _out = output; }

        private static bool LogPath(string path) => path.EndsWith("-log.db", StringComparison.OrdinalIgnoreCase);

        // P1: data-only unsyncable, first detection by a partial checkpoint: does it still retire?
        [Fact]
        public void P1_first_detection_partial_checkpoint_retires_and_clears()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 0)));
            NativeFileSync.SimulateErrno = path => LogPath(path) ? 0 : 22;
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update(db, v);
                using (var reader = engine.Query("rows", new Query()))
                {
                    Worker(() =>
                    {
                        for (var v = 6; v <= 9; v++) Update(db, v);
                        engine.Checkpoint();
                    });
                    var header = Read(file.Filename);
                    var log = Read(file.Filename.Replace(".db", "-log.db"));
                    _out.WriteLine($"version={header[HeaderPage.P_FILE_VERSION]} root={BitConverter.ToInt64(header, WalRetirement.RootPosition)} blank={Blank(log)}");
                    header[HeaderPage.P_FILE_VERSION].Should().Be(HeaderPage.MVCC_FILE_VERSION, "first-detection checkpoint promoted");
                    BitConverter.ToInt64(header, WalRetirement.RootPosition).Should().BeGreaterThan(0, "root published with a degraded data barrier");
                    Blank(log).Should().BeGreaterThan(0, "committed frames cleared although the root is not durable");
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        // P2: durable commits=false on fully unsyncable storage: every engine's first partial checkpoint retires.
        [Fact]
        public void P2_opted_out_commits_first_checkpoint_retires_per_engine()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 0)));
            NativeFileSync.SimulateErrno = _ => 22;
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename, DurableCommits = false });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update(db, v);
                using (var reader = engine.Query("rows", new Query()))
                {
                    Worker(() =>
                    {
                        for (var v = 6; v <= 9; v++) Update(db, v);
                        engine.Checkpoint();
                    });
                    var header = Read(file.Filename);
                    var log = Read(file.Filename.Replace(".db", "-log.db"));
                    _out.WriteLine($"version={header[HeaderPage.P_FILE_VERSION]} root={BitConverter.ToInt64(header, WalRetirement.RootPosition)} blank={Blank(log)}");
                    BitConverter.ToInt64(header, WalRetirement.RootPosition).Should().BeGreaterThan(0);
                    Blank(log).Should().BeGreaterThan(0);
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        // P3: shared mode, data-only unsyncable, two connections: does a second connection's fresh engine reuse slots?
        [Fact]
        public void P3_second_shared_connection_reuses_slots_when_only_the_data_file_failed()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                Update64(setup, 0);
            NativeFileSync.SimulateErrno = path => LogPath(path) ? 0 : 22;
            var logName = file.Filename.Replace(".db", "-log.db");
            try
            {
                using var e1 = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db1 = new LiteDatabase(e1, disposeOnClose: false);
                db1.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update64(db1, v);
                int[] blank;
                using (var reader = e1.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Worker(() =>
                    {
                        for (var v = 6; v <= 9; v++) Update64(db1, v);
                        db1.Checkpoint();
                    });
                    blank = BlankOffsets(Read(logName));
                    _out.WriteLine($"after C1 checkpoint: root={BitConverter.ToInt64(Read(file.Filename), WalRetirement.RootPosition)} blank={blank.Length} durable1={Durable(db1)}");
                }

                using var e2 = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db2 = new LiteDatabase(e2, disposeOnClose: false);
                db2.CheckpointSize = 0;
                Update64(db2, 10);
                var written = Read(logName);
                var reused = blank.Count(offset => offset < written.Length && !IsBlank(written, offset));
                _out.WriteLine($"C2: reused={reused} of {blank.Length} durable2={Durable(db2)}");
                reused.Should().Be(0, "commit message: a fresh shared engine never reuses slots when only the data file failed");
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }


        // P6: same shared connection, data-only unsyncable: later operation engines must not reuse the slots.
        [Fact]
        public void P6_same_shared_connection_does_not_reuse_slots_when_only_the_data_file_failed()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename)) Update64(setup, 0);
            NativeFileSync.SimulateErrno = path => LogPath(path) ? 0 : 22;
            var logName = file.Filename.Replace(".db", "-log.db");
            try
            {
                using var e1 = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db1 = new LiteDatabase(e1, disposeOnClose: false);
                db1.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update64(db1, v);
                int[] blank;
                using (var reader = e1.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Worker(() => { for (var v = 6; v <= 9; v++) Update64(db1, v); db1.Checkpoint(); });
                    blank = BlankOffsets(Read(logName));
                }
                blank.Should().NotBeEmpty();
                Update64(db1, 10);
                var written = Read(logName);
                var reused = blank.Count(offset => offset < written.Length && !IsBlank(written, offset));
                _out.WriteLine($"P6 reused={reused} of {blank.Length}");
                reused.Should().Be(0);
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        // P7: same shared connection, data failure detected by an earlier operation; a later engine's partial checkpoint must not retire.
        [Fact]
        public void P7_later_shared_engine_does_not_retire_after_an_earlier_data_failure()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename)) Update64(setup, 0);
            NativeFileSync.SimulateErrno = path => LogPath(path) ? 0 : 22;
            try
            {
                using var e1 = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db1 = new LiteDatabase(e1, disposeOnClose: false);
                db1.CheckpointSize = 0;
                Update64(db1, 1);
                db1.Checkpoint(); // first detection, nothing to retire
                for (var v = 2; v <= 5; v++) Update64(db1, v);
                using (var reader = e1.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Worker(() => { for (var v = 6; v <= 9; v++) Update64(db1, v); db1.Checkpoint(); });
                    var header = Read(file.Filename);
                    _out.WriteLine($"P7 version={header[HeaderPage.P_FILE_VERSION]} root={BitConverter.ToInt64(header, WalRetirement.RootPosition)}");
                    BitConverter.ToInt64(header, WalRetirement.RootPosition).Should().Be(0);
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }


        // P8: WAL directory cannot sync (EACCES on open): does retirement still publish a root? And a rooted data file without its WAL?
        [Fact]
        public void P8_directory_unsyncable_retires_and_rooted_file_requires_wal()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 0)));
            NativeFileSync.SimulateDirectoryErrno = _ => 13;
            byte[] header;
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update(db, v);
                using (var reader = engine.Query("rows", new Query()))
                {
                    Worker(() => { for (var v = 6; v <= 9; v++) Update(db, v); engine.Checkpoint(); });
                    header = Read(file.Filename);
                    _out.WriteLine($"P8 durable={Durable(db)} version={header[HeaderPage.P_FILE_VERSION]} root={BitConverter.ToInt64(header, WalRetirement.RootPosition)}");
                }
            }
            finally { NativeFileSync.SimulateDirectoryErrno = null; }

            // Simulate a lost (never durably named) WAL: the data file alone.
            var dataOnly = Read(file.Filename);
            Exception error = null;
            try
            {
                using var engine = Open(Copy(dataOnly, new Dev()), new Dev());
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.GetCollection("rows").Count();
            }
            catch (Exception ex) { error = ex; }
            _out.WriteLine($"P8 data-only open: {error?.GetType().Name ?? "opened"} {error?.Message}");
        }


        // P9: encrypted writable open on EINVAL storage: what is left behind, and do later opens work?
        [Fact]
        public void P9_encrypted_writable_failure_leftovers()
        {
            using var file = new TempFile();
            using (var db = new LiteDatabase($"Filename={file.Filename};Password=secret"))
                db.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1, ["value"] = "kept" });
            var log = file.Filename.Replace(".db", "-log.db");
            _out.WriteLine($"P9 log exists before={File.Exists(log)}");
            NativeFileSync.SimulateErrno = _ => 22;
            try
            {
                try { new LiteDatabase($"Filename={file.Filename};Password=secret").Dispose(); _out.WriteLine("P9 writable opened"); }
                catch (Exception ex) { _out.WriteLine($"P9 writable failed: {ex.GetType().Name}: {ex.Message}"); }
                _out.WriteLine($"P9 log exists after failure={File.Exists(log)} len={(File.Exists(log) ? new FileInfo(log).Length : -1)}");
                using (var db = new LiteDatabase($"Filename={file.Filename};Password=secret;ReadOnly=true"))
                    _out.WriteLine($"P9 read-only after failure: {db.GetCollection("rows").FindById(1)["value"].AsString}");
            }
            finally { NativeFileSync.SimulateErrno = null; }
            using (var db = new LiteDatabase($"Filename={file.Filename};Password=secret"))
                _out.WriteLine($"P9 later writable: {db.GetCollection("rows").FindById(1)["value"].AsString}");
        }

        // P4: an already-rooted file opened on storage that cannot sync: partial and full checkpoints, reopen.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void P4_rooted_file_on_unsyncable_storage(bool dataOnly)
        {
            using var data = new Dev();
            using var log = new Dev();
            byte[] d0, l0;
            using (var engine = Open(data, log))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").EnsureIndex("value");
                for (var v = 0; v <= 5; v++) Update(db, v);
                using var reader = engine.Query("rows", new Query());
                Worker(() => { for (var v = 6; v <= 9; v++) Update(db, v); engine.Checkpoint(); });
                d0 = data.ToArray(); l0 = log.ToArray();
            }
            var root0 = BitConverter.ToInt64(d0, WalRetirement.RootPosition);
            root0.Should().BeGreaterThan(0);

            using var data1 = Copy(d0, new Dev { Fail = true });
            using var log1 = Copy(l0, new Dev { Fail = !dataOnly });
            byte[] d1, l1;
            using (var engine = Open(data1, log1))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").FindAll().Should().OnlyContain(x => x["value"].AsInt32 == 9);
                Update(db, 10);
                using (var reader = engine.Query("rows", new Query()))
                {
                    Worker(() => { for (var v = 11; v <= 14; v++) Update(db, v); engine.Checkpoint(); for (var v = 15; v <= 16; v++) Update(db, v); });
                    var root1 = BitConverter.ToInt64(data1.ToArray(), WalRetirement.RootPosition);
                    _out.WriteLine($"dataOnly={dataOnly} root0={root0} root1={root1} logLen0={l0.Length} logLen1={log1.Length}");
                    var n = 0;
                    while (reader.Read()) { reader.Current["value"].AsInt32.Should().Be(10); n++; }
                    n.Should().Be(8);
                }
                d1 = data1.ToArray(); l1 = log1.ToArray();
                // crash image before the full checkpoint
                Verify(d1, l1, 16);
                db.Checkpoint();
                BitConverter.ToInt64(data1.ToArray(), WalRetirement.RootPosition).Should().Be(0, "full checkpoint clears the root");
                log1.Length.Should().Be(0);
            }
            Verify(data1.ToArray(), log1.ToArray(), 16);
        }

        // P5: NativeFileSync on a disposed FileStream vs FileStream.Flush(true).
        [Fact]
        public void P5_disposed_handle()
        {
            using var file = new TempFile();
            var fs = new FileStream(file.Filename, FileMode.Create, FileAccess.ReadWrite);
            fs.WriteByte(1);
            fs.Dispose();
            Action native = () => NativeFileSync.FlushToDisk(fs);
            Action runtime = () => fs.Flush(true);
            Exception n = null, r = null;
            try { native(); } catch (Exception ex) { n = ex; }
            try { runtime(); } catch (Exception ex) { r = ex; }
            _out.WriteLine($"native={n?.GetType().Name ?? "no exception"} runtime={r?.GetType().Name ?? "no exception"}");
        }

        private static void Verify(byte[] d, byte[] l, int value)
        {
            using var data = Copy(d, new Dev());
            using var log = Copy(l, new Dev());
            using (var engine = Open(data, log))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.GetCollection("rows").FindAll().Should().HaveCount(8).And.OnlyContain(x => x["value"].AsInt32 == value);
                db.GetCollection("rows").Count(Query.EQ("value", value)).Should().Be(8);
                db.Checkpoint();
            }
            using (var engine = Open(data, log))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
                db.GetCollection("rows").FindAll().Should().OnlyContain(x => x["value"].AsInt32 == value);
        }

        private static LiteEngine Open(Stream data, Stream log) => new LiteEngine(new EngineSettings { DataStream = data, LogStream = log });

        private static T Copy<T>(byte[] bytes, T target) where T : MemoryStream { target.Write(bytes, 0, bytes.Length); target.Position = 0; return target; }

        private sealed class Dev : MemoryStream, IDurableStream
        {
            internal bool Fail;
            public void FlushToDisk() { if (Fail) throw new UnauthorizedAccessException("sync unsupported"); }
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, value)));

        private static void Update64(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, value)));

        private static bool Durable(LiteDatabase db) => db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;

        private static byte[] Read(string name)
        {
            using var s = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var b = new byte[s.Length]; s.ReadFully(b, 0, b.Length); return b;
        }

        private static int Blank(byte[] log) => BlankOffsets(log).Length;

        private static int[] BlankOffsets(byte[] log) => Enumerable.Range(0, log.Length / WalChecksum.FrameSize)
            .Select(f => f * WalChecksum.FrameSize).Where(o => IsBlank(log, o)).ToArray();

        private static bool IsBlank(byte[] log, int offset) => log.Skip(offset).Take(WalChecksum.FrameSize).All(x => x == 0);

        private static void Worker(Action action)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo failure = null;
            var t = new System.Threading.Thread(() => { try { action(); } catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); } });
            t.Start(); t.Join(); failure?.Throw();
        }
    }
}
#endif
