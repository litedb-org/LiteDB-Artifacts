#if DEBUG || TESTING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>
    /// Review probe for 76a28bdd4: a buffering caller log stream is shared by the WAL writer and
    /// the readers (each through its own ConcurrentStream). A reader's seek writes on the frame the
    /// writer's batch left in the buffer, and can tear it on the reader's thread, where a non-I/O
    /// failure stops nothing. The writer's batch then carries on and its commit is acknowledged.
    /// </summary>
    public class ReviewBufferedReaderTear_Tests
    {
        [Theory]
        [InlineData(false, "wal-page-after-write")]
        [InlineData(true, "wal-page-after-write")]
        [InlineData(false, "wal-confirmation-after-write")]
        [InlineData(true, "wal-confirmation-after-write")]
        public void Reader_that_tears_a_held_frame_loses_no_acknowledged_commit(bool ioFailure, string point)
        {
            using var data = new MemoryStream();
            using var device = new TearingDevice { IoFailure = ioFailure };
            using var log = new BufferedStream(device, 1 << 20);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log })))
            {
                db.CheckpointSize = 0;
                db.GetCollection("a").Insert(Enumerable.Range(1, 40).Select(id => Row(id)));
                db.GetCollection("b").Insert(Row(0));
            }

            var acknowledged = new List<int> { 0 };
            Exception readerFailure = null;
            var readerReadLog = false;
            byte[] imageData, imageLog;
            var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 4 });
            var db2 = new LiteDatabase(engine, disposeOnClose: false);
            try
            {
                var calls = 0;
                // After a frame's write returned (the BufferedStream holds it), before the writer's next
                // stream access: a concurrent reader reads the WAL through the same caller stream.
                EngineState.SimulateProcessCrash = phase =>
                {
                    if (phase != point || Thread.CurrentThread.Name == "reader" || ++calls != 1) return;
                    device.Armed = true;
                    var reader = new Thread(() =>
                    {
                        engine.BeforePageRead = (position, origin) => { if (origin == FileOrigin.Log) readerReadLog = true; };
                        try { db2.GetCollection("a").FindAll().ToList(); }
                        catch (Exception ex) { readerFailure = ex; }
                    }) { Name = "reader" };
                    reader.Start();
                    if (!reader.Join(TimeSpan.FromSeconds(20))) readerFailure = new TimeoutException("reader blocked");
                    device.Armed = false;
                };
                try
                {
                    db2.GetCollection("b").Insert(Enumerable.Range(1, 30).Select(id => Row(id)));
                    acknowledged.AddRange(Enumerable.Range(1, 30));
                }
                catch (Exception) { }
                EngineState.SimulateProcessCrash = null;
                try
                {
                    db2.GetCollection("b").Insert(Row(100));
                    acknowledged.Add(100);
                }
                catch (Exception) { }

                Console.WriteLine($"PROBE point={point} io={ioFailure} calls={calls} readLog={readerReadLog} readerFailure={readerFailure?.GetType().Name} torn={device.Torn} ack={acknowledged.Count}");
                device.Torn.Should().BeTrue("the reader's seek wrote the held frame on, torn");
                readerReadLog.Should().BeTrue();
                readerFailure.Should().NotBeNull();
                imageData = data.ToArray();
                imageLog = device.ToArray();
            }
            finally
            {
                EngineState.SimulateProcessCrash = null;
                try { db2.Dispose(); } catch { }
                try { engine.Dispose(); } catch { }
            }

            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(imageData), LogStream = new MemoryStream(imageLog)
            }));
            recovered.GetCollection("a").Count().Should().Be(40);
            recovered.GetCollection("b").FindAll().Select(x => x["_id"].AsInt32).Should().BeEquivalentTo(acknowledged,
                "every acknowledged commit survives (reader failure: {0})", readerFailure?.GetType().Name);
        }

        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["payload"] = new string('p', 1500) };

        /// <summary>Armed, the next frame-sized write stores half of it and fails.</summary>
        private sealed class TearingDevice : MemoryStream
        {
            internal volatile bool Armed;
            internal bool IoFailure, Torn;
            internal int Reads, Writes;
            public override int Read(byte[] buffer, int offset, int count) { Reads++; return base.Read(buffer, offset, count); }

            public override void Write(byte[] buffer, int offset, int count)
            {
                Writes++;
                if (Armed && count >= WalChecksum.FrameSize)
                {
                    Armed = false;
                    Torn = true;
                    base.Write(buffer, offset, count / 2);
                    throw IoFailure ? new IOException("injected torn write") : new InvalidOperationException("injected torn write");
                }
                base.Write(buffer, offset, count);
            }
        }
    }
}
#endif
