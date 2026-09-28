using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Regressions
{
    /// <summary>Review measurement: cost of the per-write flush on caller streams (not an assertion).</summary>
    public class ReviewWriteThroughCost_Tests
    {
        private readonly ITestOutputHelper _output;
        public ReviewWriteThroughCost_Tests(ITestOutputHelper output) => _output = output;

        [Theory]
        [InlineData("file4k")]
        [InlineData("file1m")]
        [InlineData("buffered1m")]
        [InlineData("memory")]
        public void Measure(string kind)
        {
            var dir = Path.Combine(Path.GetTempPath(), "rvcost-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Stream Open(string name) => kind switch
                {
                    "file4k" => new FileStream(Path.Combine(dir, name), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096),
                    "file1m" => new FileStream(Path.Combine(dir, name), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20),
                    "buffered1m" => new BufferedStream(new FileStream(Path.Combine(dir, name), FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4096), 1 << 20),
                    _ => new MemoryStream()
                };
                using var data = Open("d.db");
                using var log = Open("d-log.db");
                var sw = Stopwatch.StartNew();
                using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, DurableCommits = false })))
                {
                    var col = db.GetCollection("rows");
                    col.Insert(Enumerable.Range(1, 20000).Select(id => new BsonDocument { ["_id"] = id, ["payload"] = new string('p', 300) }));
                    var bulk = sw.ElapsedMilliseconds;
                    for (var i = 0; i < 2000; i++) col.Update(new BsonDocument { ["_id"] = i + 1, ["payload"] = new string('q', 300) });
                    var small = sw.ElapsedMilliseconds - bulk;
                    db.Checkpoint();
                    var total = sw.ElapsedMilliseconds;
                    _output.WriteLine($"COST {kind}: bulk={bulk}ms small={small}ms total={total}ms");
                    Console.WriteLine($"COST {kind}: bulk={bulk}ms small={small}ms total={total}ms");
                }
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }
    }
}
