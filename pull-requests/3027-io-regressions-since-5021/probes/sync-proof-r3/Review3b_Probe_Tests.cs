#if DEBUG || TESTING
using System;
using System.Collections.Concurrent;
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
    public class Review3b_Probe_Tests
    {
        private readonly ITestOutputHelper _out;
        public Review3b_Probe_Tests(ITestOutputHelper output) { _out = output; }

        private static bool LogPath(string path) => path.EndsWith("-log.db", StringComparison.OrdinalIgnoreCase);

        // R1: only the WAL answers "cannot sync" and commits opted out of syncs: nothing synced the
        // log before the partial checkpoint, so only the proof's log sync can find out.
        [Theory]
        [InlineData(false)]
        public void R1_opted_out_commits_log_only_unsyncable_retires_nothing(bool encrypted)
        {
            using var file = new TempFile();
            var cs = encrypted ? $"Filename={file.Filename};Password=secret" : file.Filename;
            using (var setup = new LiteDatabase(cs))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 0)));
            NativeFileSync.SimulateErrno = path => LogPath(path) ? 22 : 0;
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename, DurableCommits = false, Password = encrypted ? "secret" : null });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update(db, v);
                using (var reader = engine.Query("rows", new Query()))
                {
                    Worker(() => { for (var v = 6; v <= 9; v++) Update(db, v); engine.Checkpoint(); });
                    var header = Read(file.Filename);
                    var log = Read(FileHelper.GetLogFile(file.Filename));
                    var root = encrypted ? -1 : BitConverter.ToInt64(header, WalRetirement.RootPosition);
                    _out.WriteLine($"R1 encrypted={encrypted} version={(encrypted ? -1 : header[HeaderPage.P_FILE_VERSION])} root={root} blank={(encrypted ? -1 : Blank(log))} walFrames={log.Length / WalChecksum.FrameSize}");
                    if (!encrypted)
                    {
                        header[HeaderPage.P_FILE_VERSION].Should().BeLessThan(HeaderPage.MVCC_FILE_VERSION, "no promotion");
                        root.Should().Be(0, "no root");
                        Blank(log).Should().Be(0, "no cleared frame");
                    }
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        // R2: count device syncs per partial / reclaiming checkpoint (direct and shared).
        [Theory]
        [InlineData("direct-partial")]
        [InlineData("direct-reclaim")]
        [InlineData("shared-partial")]
        [InlineData("shared-reclaim")]
        [InlineData("direct-partial-encrypted")]
        public void R2_sync_cost(string mode)
        {
            using var file = new TempFile();
            var encrypted = mode.EndsWith("encrypted");
            var cs = encrypted ? $"Filename={file.Filename};Password=secret" : file.Filename;
            using (var setup = new LiteDatabase(cs)) Update64(setup, 0);
            var counts = new ConcurrentDictionary<string, int>();
            var dirs = 0;
            NativeFileSync.SimulateErrno = path => { counts.AddOrUpdate(LogPath(path) ? "log" : "data", 1, (_, n) => n + 1); return 0; };
            NativeFileSync.SimulateDirectoryErrno = _ => { System.Threading.Interlocked.Increment(ref dirs); return 0; };
            try
            {
                var settings = new EngineSettings { Filename = file.Filename, Password = encrypted ? "secret" : null };
                ILiteEngine engine = mode.StartsWith("shared") ? new SharedEngine(settings) : new LiteEngine(settings);
                using (engine)
                using (var db = new LiteDatabase(engine, disposeOnClose: false))
                {
                    db.CheckpointSize = 0;
                    for (var round = 1; round <= 4; round++)
                    {
                        for (var v = 1; v <= 3; v++) Update64(db, round * 10 + v);
                        IBsonDataReader reader = null;
                        if (mode.Contains("partial")) { reader = engine.Query("rows", new Query()); reader.Read().Should().BeTrue(); }
                        for (var v = 4; v <= 6; v++) Update64(db, round * 10 + v);
                        counts.Clear(); dirs = 0;
                        Worker(() => db.Checkpoint());
                        _out.WriteLine($"{mode} round {round}: data={C(counts,"data")} log={C(counts,"log")} dir={dirs} logFrames={(File.Exists(FileHelper.GetLogFile(file.Filename)) ? new FileInfo(FileHelper.GetLogFile(file.Filename)).Length / WalChecksum.FrameSize : 0)}");
                        reader?.Dispose();
                    }
                }
            }
            finally { NativeFileSync.SimulateErrno = null; NativeFileSync.SimulateDirectoryErrno = null; }
        }


        // R3: accepted residual, second shared connection after a mid-checkpoint data failure.
        [Fact]
        public void R3_second_connection_after_mid_checkpoint_data_failure()
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            using (var setup = new LiteDatabase(file.Filename)) Update64(setup, 0);
            var dataFails = false;
            NativeFileSync.SimulateErrno = path => LogPath(path) || !dataFails ? 0 : 22;
            EngineState.SimulateProcessCrash = phase => { if (phase == "checkpoint-before-page-write") dataFails = true; };
            try
            {
                using var e1 = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db1 = new LiteDatabase(e1, disposeOnClose: false);
                db1.CheckpointSize = 0;
                for (var v = 1; v <= 5; v++) Update64(db1, v);
                int[] cleared;
                using (var reader = e1.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    Worker(() => { for (var v = 6; v <= 9; v++) Update64(db1, v); db1.Checkpoint(); });
                    cleared = BlankOffsets(Read(logName));
                }
                EngineState.SimulateProcessCrash = null;
                using var e2 = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db2 = new LiteDatabase(e2, disposeOnClose: false);
                db2.CheckpointSize = 0;
                Update64(db2, 10);
                var written = Read(logName);
                var reused = cleared.Count(o => o < written.Length && !IsBlank(written, o));
                _out.WriteLine($"R3 cleared={cleared.Length} reusedBySecondConnection={reused} root={BitConverter.ToInt64(Read(file.Filename), WalRetirement.RootPosition)}");
            }
            finally { EngineState.SimulateProcessCrash = null; NativeFileSync.SimulateErrno = null; }
        }

        // R4: long-lived direct engine; data file starts failing between checkpoints (after the one-time proof).
        [Fact]
        public void R4_long_lived_engine_data_fails_between_checkpoints()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename)) Update64(setup, 0);
            var dataFails = false;
            NativeFileSync.SimulateErrno = path => LogPath(path) || !dataFails ? 0 : 22;
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var round = 1; round <= 2; round++)
                {
                    for (var v = 1; v <= 3; v++) Update64(db, round * 10 + v);
                    using (var reader = engine.Query("rows", new Query()))
                    {
                        reader.Read().Should().BeTrue();
                        Worker(() => { for (var v = 4; v <= 6; v++) Update64(db, round * 10 + v); db.Checkpoint(); });
                    }
                    var header = Read(file.Filename);
                    var log = Read(FileHelper.GetLogFile(file.Filename));
                    _out.WriteLine($"R4 round {round} dataFails={dataFails} root={BitConverter.ToInt64(header, WalRetirement.RootPosition)} blank={BlankOffsets(log).Length} durable={db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean}");
                    dataFails = true;
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        private static int[] BlankOffsets(byte[] log) => Enumerable.Range(0, log.Length / WalChecksum.FrameSize)
            .Select(f => f * WalChecksum.FrameSize).Where(o => IsBlank(log, o)).ToArray();

        private static bool IsBlank(byte[] log, int offset) => log.Skip(offset).Take(WalChecksum.FrameSize).All(x => x == 0);

        private static int C(ConcurrentDictionary<string, int> d, string k) => d.TryGetValue(k, out var n) ? n : 0;

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, value)));

        private static void Update64(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, value)));

        private static byte[] Read(string name)
        {
            using var s = new FileStream(name, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var b = new byte[s.Length]; s.ReadFully(b, 0, b.Length); return b;
        }

        private static int Blank(byte[] log) => Enumerable.Range(0, log.Length / WalChecksum.FrameSize)
            .Select(f => f * WalChecksum.FrameSize).Count(o => log.Skip(o).Take(WalChecksum.FrameSize).All(x => x == 0));

        private static void Worker(Action action)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo failure = null;
            var t = new System.Threading.Thread(() => { try { action(); } catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); } });
            t.Start(); t.Join(); failure?.Throw();
        }
    }
}
#endif
