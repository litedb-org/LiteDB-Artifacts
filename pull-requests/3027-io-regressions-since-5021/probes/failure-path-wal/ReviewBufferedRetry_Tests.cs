#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    /// <summary>
    /// Review probe for c4119caba: a BufferedStream keeps a write whose flush failed and writes it
    /// again at the next access (Flush, Length, Position, SetLength, Dispose), at its inner stream's
    /// current position. When the inner stream advanced past the bytes it stored before failing (the
    /// TornLog model of the torn-append tests), the retried page lands shifted and overwrites half of
    /// the next page. Here a checkpoint's data page write tears; the caller then disposes its stream.
    /// </summary>
    public class ReviewBufferedRetry_Tests
    {
        [Theory]
        [InlineData(true, false)]
        [InlineData(true, true)]
        [InlineData(false, false)]
        public void Torn_data_page_retried_by_the_callers_buffered_stream(bool buffered, bool restorePosition)
        {
            var device = new ShiftingDevice { RestorePosition = restorePosition };
            var log = new MemoryStream();
            Stream data = buffered ? new BufferedStream(device, 1 << 20) : device;
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log })))
            {
                db.GetCollection("rows").Insert(Enumerable.Range(1, 200).Select(Row));
                db.Checkpoint();
            }

            Exception failure = null;
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log }))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                var doc = Row(100);
                doc["value"] = "updated";
                db.GetCollection("rows").Update(doc).Should().BeTrue();
                device.Armed = true;
                try { db.Checkpoint(); }
                catch (Exception ex) { failure = ex; }
            }
            device.Torn.Should().BeTrue("the checkpoint tore a data page");
            failure.Should().NotBeNull();
            // The caller disposes its stream, as after any failure.
            data.Dispose();

            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(device.Bytes()), LogStream = new MemoryStream(log.ToArray())
            }));
            var rows = recovered.GetCollection("rows").FindAll().ToList();
            rows.Select(x => x["_id"].AsInt32).Should().BeEquivalentTo(Enumerable.Range(1, 200));
            rows.Single(x => x["_id"].AsInt32 == 100)["value"].AsString.Should().Be("updated");
        }

        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = "v", ["payload"] = new string('p', 1200) };

        /// <summary>Armed, the next non-header page write stores half and fails (position advanced, or restored).</summary>
        private sealed class ShiftingDevice : MemoryStream
        {
            internal bool Armed, Torn, RestorePosition;
            private byte[] _final;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed && count >= Constants.PAGE_SIZE && Position >= Constants.PAGE_SIZE)
                {
                    Armed = false;
                    Torn = true;
                    var start = Position;
                    base.Write(buffer, offset, count / 2);
                    if (RestorePosition) Position = start;
                    throw new IOException("injected torn data page write");
                }
                base.Write(buffer, offset, count);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && _final == null) _final = base.ToArray();
                base.Dispose(disposing);
            }

            internal byte[] Bytes() => _final ?? base.ToArray();
        }
    }
}
#endif
