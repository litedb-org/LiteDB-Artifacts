#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB.Engine;
using LiteDB.Internals;
using LiteDB.Tests.Issues;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Regressions
{
    /// <summary>Measurement only (not part of the change): device syncs per scenario on storage that syncs.</summary>
    [Collection(NativeFileSyncCollection.Name)]
    public class SyncCost_Probe_Tests
    {
        private readonly ITestOutputHelper _out;
        public SyncCost_Probe_Tests(ITestOutputHelper output) => _out = output;

        private int _data, _log;

        private void Count(string filename)
        {
            var dataName = Path.GetFullPath(filename);
            NativeFileSync.SimulateErrno = path =>
            {
                if (string.Equals(Path.GetFullPath(path), dataName, StringComparison.OrdinalIgnoreCase)) Interlocked.Increment(ref _data);
                else Interlocked.Increment(ref _log);
                return 0;
            };
        }

        private (int Data, int Log) Take() { var r = (_data, _log); _data = _log = 0; return r; }

        private static void Setup(string filename)
        {
            using var setup = new LiteDatabase(filename);
            setup.GetCollection("rows").Insert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, 0)));
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => MvccRetirementScenario.Document(id, value)));

        [Fact]
        public void Measure()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            Count(file.Filename);
            try
            {
                Take();
                for (var i = 0; i < 10; i++)
                    using (var db = new LiteDatabase(file.Filename)) db.GetCollection("log").Insert(new BsonDocument { ["_id"] = i });
                var r = Take();
                _out.WriteLine($"MEASURE direct open+insert+dispose: data={r.Data / 10.0} log={r.Log / 10.0}");

                using (var db = new LiteDatabase(file.Filename)) db.CheckpointSize = 0;
                Take();
                for (var i = 10; i < 20; i++)
                    using (var db = new LiteDatabase(file.Filename)) db.GetCollection("log").Insert(new BsonDocument { ["_id"] = i });
                r = Take();
                _out.WriteLine($"MEASURE direct CHECKPOINT=0: data={r.Data / 10.0} log={r.Log / 10.0}");

                using (var shared = new LiteDatabase($"Filename={file.Filename};Connection=shared"))
                {
                    shared.GetCollection("log").Insert(new BsonDocument { ["_id"] = 100 });
                    Take();
                    for (var i = 101; i < 121; i++) shared.GetCollection("log").Insert(new BsonDocument { ["_id"] = i });
                    r = Take();
                }
                _out.WriteLine($"MEASURE shared per operation: data={r.Data / 20.0} log={r.Log / 20.0}");

                using (var db = new LiteDatabase(file.Filename))
                {
                    db.CheckpointSize = 0;
                    Update(db, 1);
                    Take();
                    db.Checkpoint();
                    r = Take();
                    _out.WriteLine($"MEASURE full checkpoint: data={r.Data} log={r.Log}");
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        [Fact]
        public void Measure_retiring()
        {
            using var file = new TempFile();
            Setup(file.Filename);
            Count(file.Filename);
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var round = 1; round <= 2; round++)
                {
                    for (var value = 1; value <= 5; value++) Update(db, round * 100 + value);
                    using (var reader = engine.Query("rows", new Query()))
                    {
                        reader.Read();
                        (int Data, int Log) r = default;
                        var t = new Thread(() =>
                        {
                            for (var value = 6; value <= 9; value++) Update(db, round * 100 + value);
                            Take();
                            db.Checkpoint();
                            r = Take();
                        });
                        t.Start(); t.Join();
                        _out.WriteLine($"MEASURE retiring partial checkpoint #{round}: data={r.Data} log={r.Log}");
                    }
                    var header = SyncPowerLossModel.ReadShared(file.Filename);
                    _out.WriteLine($"  file version after #{round}: {header[HeaderPage.P_FILE_VERSION]}");
                    // Reuse: next commits reuse retired slots
                    Take();
                    Update(db, round * 100 + 50);
                    var u = Take();
                    _out.WriteLine($"MEASURE first commit after retiring #{round}: data={u.Data} log={u.Log}");
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }
    }
}
#endif
