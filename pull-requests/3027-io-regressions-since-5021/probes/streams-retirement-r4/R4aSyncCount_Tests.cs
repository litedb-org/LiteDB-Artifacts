#if DEBUG || TESTING
using System;
using System.Collections.Generic;
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
    public class R4aSyncCount_Tests
    {
        private readonly ITestOutputHelper _out;
        public R4aSyncCount_Tests(ITestOutputHelper o) { _out = o; }

        [Fact]
        public void Count_syncs_per_retiring_checkpoint()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 0)));
            var counts = new List<string>();
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename, DurableCommits = false });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(db, value);
                using (var reader = engine.Query("rows", new Query()))
                {
                    for (var round = 0; round < 3; round++)
                    {
                        for (var value = 6; value <= 9; value++) Update(db, value + round * 10);
                        lock (counts) counts.Clear();
                        NativeFileSync.SimulateErrno = path => { lock (counts) counts.Add(path.EndsWith("-log.db") ? "log" : "data"); return 0; };
                        var t = new System.Threading.Thread(() => engine.Checkpoint());
                        t.Start(); t.Join();
                        NativeFileSync.SimulateErrno = null;
                        _out.WriteLine($"round {round}: data={counts.Count(x => x == "data")} log={counts.Count(x => x == "log")}");
                    }
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Update(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, value))).Should().Be(8);
    }
}
#endif
