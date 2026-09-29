#if DEBUG || TESTING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Review
{
    public class HeldWritesReopen_Tests
    {
        private readonly ITestOutputHelper _out;
        public HeldWritesReopen_Tests(ITestOutputHelper output) => _out = output;

        // After the reader's write-on tore the held frame, the engine should continue read-only
        // (decision 6) over the same caller streams, like after the writer's own write failure.
        [Theory]
        [InlineData("wal-page-after-write", "io")]
        [InlineData("wal-confirmation-after-write", "io")]
        [InlineData("wal-page-after-write", "other")]
        [InlineData("wal-confirmation-after-write", "other")]
        public void Reader_write_on_failure_leaves_a_read_only_continuation(string point, string failure)
        {
            using var data = new MemoryStream();
            using var device = new TearingDevice { IoFailure = failure == "io" };
            using var log = new BufferedStream(device, 1 << 20);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log })))
            {
                db.CheckpointSize = 0;
                db.GetCollection("a").Insert(Enumerable.Range(1, 40).Select(Row));
                db.GetCollection("b").Insert(Row(0));
            }

            var acknowledged = new List<int> { 0 };
            Exception readerFailure = null, writerFailure = null, afterFailure = null;
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 4 }))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                Thread reader = null;
                engine.SimulateCrashPoint = phase =>
                {
                    if (phase != point || reader != null) return;
                    device.Armed = true;
                    reader = new Thread(() =>
                    {
                        try { db.GetCollection("a").FindAll().ToList(); }
                        catch (Exception ex) { readerFailure = ex; }
                    });
                    reader.Start();
                    reader.Join(TimeSpan.FromSeconds(30));
                    device.Armed = false;
                };
                foreach (var ids in new[] { Enumerable.Range(1, 30).ToArray(), new[] { 100 } })
                {
                    try
                    {
                        db.GetCollection("b").Insert(ids.Select(Row));
                        acknowledged.AddRange(ids);
                    }
                    catch (Exception ex) { writerFailure ??= ex; }
                }
                List<int> read = null;
                try { read = db.GetCollection("b").FindAll().Select(x => x["_id"].AsInt32).ToList(); }
                catch (Exception ex) { afterFailure = ex; }
                _out.WriteLine($"reader={readerFailure?.GetType().Name} writer={writerFailure?.Message} after={afterFailure?.GetType().Name}:{afterFailure?.Message} read={read?.Count}");
                afterFailure.Should().BeNull("the engine continues read-only after a write failure (decision 6)");
                read.Should().BeEquivalentTo(acknowledged);
            }
        }

        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["payload"] = new string('p', 1500) };

        private sealed class TearingDevice : MemoryStream
        {
            internal volatile bool Armed;
            internal bool IoFailure;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed && count >= WalChecksum.FrameSize)
                {
                    Armed = false;
                    base.Write(buffer, offset, count / 2);
                    throw IoFailure ? new IOException("injected torn write") : new InvalidOperationException("injected torn write");
                }
                base.Write(buffer, offset, count);
            }
        }
    }
}
#endif
