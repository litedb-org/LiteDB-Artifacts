#if DEBUG || TESTING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Review
{
    public class HeldWritesReview_Tests
    {
        private readonly ITestOutputHelper _out;
        public HeldWritesReview_Tests(ITestOutputHelper output) => _out = output;

        // Same shape as SharedBufferedLogStream_Tests, but the one-shot tear throws an exception that
        // IsDurableFlushUnsupported classifies as "cannot sync" (a write failure, not a sync answer).
        [Theory]
        [InlineData("wal-page-after-write", "unauthorized")]
        [InlineData("wal-confirmation-after-write", "unauthorized")]
        [InlineData("wal-page-after-write", "einval")]
        [InlineData("wal-confirmation-after-write", "einval")]
        public void Reader_write_on_failure_classified_as_cannot_sync_is_not_lost(string point, string failure)
        {
            using var data = new MemoryStream();
            using var device = new TearingDevice { Kind = failure };
            using var log = new BufferedStream(device, 1 << 20);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log })))
            {
                db.CheckpointSize = 0;
                db.GetCollection("a").Insert(Enumerable.Range(1, 40).Select(Row));
                db.GetCollection("b").Insert(Row(0));
            }

            var acknowledged = new List<int> { 0 };
            Exception readerFailure = null, writerFailure = null;
            byte[] imageData, imageLog;
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
                reader.Should().NotBeNull();
                imageData = data.ToArray();
                imageLog = device.ToArray();
            }
            _out.WriteLine($"torn={device.Torn} reader={readerFailure?.GetType().Name}:{readerFailure?.Message} writer={writerFailure?.GetType().Name} acknowledged={acknowledged.Count}");
            device.Torn.Should().BeTrue("the reader's write-on tore the held batch");

            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(imageData), LogStream = new MemoryStream(imageLog)
            }));
            var ids2 = recovered.GetCollection("b").FindAll().Select(x => x["_id"].AsInt32).ToList();
            _out.WriteLine($"recovered={ids2.Count}");
            ids2.Should().BeEquivalentTo(acknowledged, "every acknowledged commit survives");
        }

        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["payload"] = new string('p', 1500) };

        private sealed class TearingDevice : MemoryStream
        {
            internal volatile bool Armed;
            internal volatile bool Torn;
            internal string Kind;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed && count >= WalChecksum.FrameSize)
                {
                    Armed = false;
                    Torn = true;
                    base.Write(buffer, offset, count / 2);
                    if (Kind == "unauthorized") throw new UnauthorizedAccessException("injected torn write");
                    throw new IOException("injected torn write", RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? unchecked((int)0x80070032) : 22);
                }
                base.Write(buffer, offset, count);
            }
        }
    }
}
#endif
