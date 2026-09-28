using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>
    /// Review probe for c4119caba: write-through flushing and locked Length/Flush in ConcurrentStream
    /// under concurrent readers and a writer, with encrypted and plain caller streams that buffer,
    /// and a caller TempStream that switches to disk mid-run. No deadlock, no lost commit.
    /// </summary>
    public class ReviewCallerStreamConcurrency_Tests
    {
        [Theory]
        [InlineData("buffered", null)]
        [InlineData("buffered", "secret")]
        [InlineData("temp", null)]
        [InlineData("temp", "secret")]
        public void Concurrent_readers_and_writer_over_caller_streams(string kind, string password)
        {
            var dataDevice = new MemoryStream();
            var logDevice = new MemoryStream();
            Stream data = kind == "buffered" ? new BufferedStream(dataDevice, 1 << 16) : new TempStream(maxMemoryUsage: 64 * 1024);
            Stream log = kind == "buffered" ? new BufferedStream(logDevice, 1 << 16) : new TempStream(maxMemoryUsage: 64 * 1024);
            try
            {
                var settings = new EngineSettings { DataStream = data, LogStream = log, Password = password, TransactionPageLimit = 8 };
                var committed = 0;
                using (var db = new LiteDatabase(new LiteEngine(settings)))
                {
                    db.CheckpointSize = 50;
                    var col = db.GetCollection("rows");
                    col.Insert(new BsonDocument { ["_id"] = 0, ["payload"] = new string('x', 500) });
                    committed = 1;
                    using var cts = new CancellationTokenSource();
                    Task[] readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
                    {
                        while (!cts.IsCancellationRequested)
                        {
                            var n = db.GetCollection("rows").FindAll().Count();
                            n.Should().BeGreaterThan(0);
                            (db.GetCollection("rows").FindById(0) != null).Should().BeTrue();
                        }
                    })).ToArray();
                    var writer = Task.Run(() =>
                    {
                        for (var batch = 0; batch < 60; batch++)
                        {
                            var ids = Enumerable.Range(1 + batch * 10, 10).ToArray();
                            db.GetCollection("rows").Insert(ids.Select(id => new BsonDocument { ["_id"] = id, ["payload"] = new string((char)('a' + batch % 26), 1500) }));
                            Interlocked.Add(ref committed, ids.Length);
                            if (batch % 7 == 0) db.Checkpoint();
                        }
                    });
                    writer.Wait(TimeSpan.FromSeconds(120)).Should().BeTrue("the writer must not deadlock");
                    cts.Cancel();
                    Task.WaitAll(readers, TimeSpan.FromSeconds(60)).Should().BeTrue("the readers must not deadlock");
                    db.GetCollection("rows").Count().Should().Be(committed);
                }
                var reopen = new EngineSettings { DataStream = data, LogStream = log, Password = password };
                using (var db = new LiteDatabase(new LiteEngine(reopen)))
                {
                    db.GetCollection("rows").Count().Should().Be(601);
                }
            }
            finally
            {
                data.Dispose();
                log.Dispose();
            }
        }
    }
}
