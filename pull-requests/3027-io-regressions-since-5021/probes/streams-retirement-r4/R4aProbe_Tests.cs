#if DEBUG || TESTING
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Regressions
{
    public class R4aProbe_Tests
    {
        private readonly ITestOutputHelper _out;
        public R4aProbe_Tests(ITestOutputHelper output) { _out = output; }

        private static (byte[] data, byte version) CurrentFile(CompactStorageMode mode, string password = null)
        {
            var data = new MemoryStream();
            var log = new MemoryStream();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, CompactStorage = mode, Password = password })))
            {
                var rows = db.GetCollection("rows");
                rows.EnsureIndex("value");
                rows.Insert(Enumerable.Range(1, 50).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i % 7, ["p"] = new string('x', 300) }));
                db.Checkpoint();
            }
            var bytes = data.ToArray();
            return (bytes, bytes[HeaderPage.P_FILE_VERSION]);
        }

        private static MemoryStream Writable(byte[] bytes)
        {
            var stream = new MemoryStream();
            stream.Write(bytes, 0, bytes.Length);
            stream.Position = 0;
            return stream;
        }

        private static bool ReadOnly(LiteDatabase db) =>
            db.GetCollection("$database").FindAll().Single()["readOnly"].AsBoolean;

        // P1: reopen RO data + a log holding the previous session's commits; keep writing.
        [Theory]
        [InlineData(CompactStorageMode.Auto, null)]
        [InlineData(CompactStorageMode.Legacy, null)]
        [InlineData(CompactStorageMode.Auto, "pw")]
        public void P1_reopen_ro_data_with_log_content(CompactStorageMode mode, string password)
        {
            var (original, version) = CurrentFile(mode, password);
            _out.WriteLine($"version {version}");
            using var log = new MemoryStream();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = new MemoryStream(original, false), LogStream = log, Password = password })))
            {
                ReadOnly(db).Should().BeFalse();
                db.GetCollection("rows").Insert(Enumerable.Range(100, 30).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i % 7, ["p"] = new string('y', 300), ["g"] = new BsonDocument { ["longName"] = i } }));
                db.GetCollection("rows").Update(new BsonDocument { ["_id"] = 1, ["value"] = 99, ["q"] = "upd" }).Should().BeTrue();
                db.GetCollection("rows").Delete(2).Should().BeTrue();
            }
            _out.WriteLine($"log after first session {log.Length}");
            log.Position = 0;
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = new MemoryStream(original, false), LogStream = log, Password = password })))
            {
                _out.WriteLine($"readOnly on reopen: {ReadOnly(db)}");
                db.GetCollection("rows").Count().Should().Be(50 + 30 - 1);
                db.GetCollection("rows").FindById(1)["value"].AsInt32.Should().Be(99);
                db.GetCollection("rows").Count(Query.EQ("value", 99)).Should().Be(1);
                if (!ReadOnly(db))
                {
                    db.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 500, ["value"] = 1 });
                    Action ck = () => db.Checkpoint();
                    ck.Should().Throw<NotSupportedException>();
                    db.GetCollection("rows").Count().Should().Be(50 + 30);
                    db.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 501, ["value"] = 1 });
                    db.GetCollection("rows").Count().Should().Be(50 + 31);
                }
            }
            var logBytes = log.ToArray();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = Writable(original), LogStream = Writable(logBytes), Password = password })))
            {
                _out.WriteLine($"writable reopen count {db.GetCollection("rows").Count()}");
                db.Checkpoint();
                db.GetCollection("rows").Count(Query.EQ("value", 99)).Should().Be(1);
            }
        }

        // P2: RO data + in-memory log, many writes with an open reader: no checkpoint, no throw.
        [Fact]
        public void P2_many_writes_with_reader_on_ro_data_in_memory_log()
        {
            var (original, _) = CurrentFile(CompactStorageMode.Auto);
            using var engine = new LiteEngine(new EngineSettings { DataStream = new MemoryStream(original, false) });
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            db.CheckpointSize = 1;
            using (var reader = engine.Query("rows", new Query()))
            {
                for (var i = 0; i < 50; i++) db.GetCollection("rows").Upsert(new BsonDocument { ["_id"] = i, ["value"] = i, ["p"] = new string('z', 500) });
            }
            for (var i = 0; i < 50; i++) db.GetCollection("rows").Upsert(new BsonDocument { ["_id"] = i, ["value"] = i, ["p"] = new string('w', 500) });
            db.GetCollection("rows").Count().Should().Be(51);
            _out.WriteLine($"log pages {engine.GetWalIndex() != null}");
            Action ck = () => db.Checkpoint();
            ck.Should().Throw<NotSupportedException>();
            db.GetCollection("rows").Count().Should().Be(51);
        }

        // P3: caller's settings unchanged (incl. internal ReadOnlyStorage) with RO data.
        [Fact]
        public void P3_caller_settings_not_changed()
        {
            var (original, _) = CurrentFile(CompactStorageMode.Auto);
            var settings = new EngineSettings { DataStream = new MemoryStream(original, false) };
            using (var engine = new LiteEngine(settings)) { }
            settings.ReadOnlyStorage.Should().BeFalse();
            settings.ReadOnly.Should().BeFalse();
        }

        // P4: explicit ReadOnly + RO streams and rebuild / checkpoint semantics.
        [Fact]
        public void P4_readonly_stream_rebuild_returns_zero()
        {
            var (original, _) = CurrentFile(CompactStorageMode.Auto);
            using var engine = new LiteEngine(new EngineSettings { DataStream = new MemoryStream(original, false), ReadOnly = true });
            engine.Rebuild(null).Should().Be(0);
            engine.Checkpoint().Should().Be(0);
        }

        // P5: stream that is writable at ctor but CanWrite toggles later.
        private sealed class Toggle : MemoryStream
        {
            public bool Writable = true;
            public Toggle(byte[] b) { Write(b, 0, b.Length); Position = 0; }
            public override bool CanWrite => Writable;
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!Writable) throw new NotSupportedException("toggled");
                base.Write(buffer, offset, count);
            }
        }

        [Fact]
        public void P5_can_write_false_then_true()
        {
            var (original, _) = CurrentFile(CompactStorageMode.Auto);
            var data = new Toggle(original) { Writable = false };
            var log = new MemoryStream();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log })))
            {
                data.Writable = true;
                db.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 900 });
                Action ck = () => db.Checkpoint();
                // engine decided at construction: data stays unchanged
                ck.Should().Throw<NotSupportedException>();
            }
            data.ToArray().Should().Equal(original);
        }
        // P6: user ReadOnly engine over writable caller streams / file: error close must not write.
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void P6_readonly_error_close_writes_nothing(bool fileMode)
        {
            var (original, _) = CurrentFile(CompactStorageMode.Auto);
            using var tmp = new TempFile();
            File.WriteAllBytes(tmp.Filename, original);
            var data = Writable(original);
            var log = new MemoryStream();
            var settings = fileMode ? new EngineSettings { Filename = tmp.Filename, ReadOnly = true }
                                    : new EngineSettings { DataStream = data, LogStream = log, ReadOnly = true };
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                engine.SimulateDiskReadFail = page => throw LiteException.InvalidDatafileState("injected damage");
                Action query = () => db.GetCollection("rows").FindAll().ToList();
                query.Should().Throw<LiteException>();
            }
            if (fileMode)
            {
                _out.WriteLine($"file changed: {!File.ReadAllBytes(tmp.Filename).SequenceEqual(original)}; log exists {File.Exists(FileHelper.GetLogFile(tmp.Filename))}");
            }
            else
            {
                _out.WriteLine($"data changed: {!data.ToArray().SequenceEqual(original)}; mark {data.ToArray()[HeaderPage.P_INVALID_DATAFILE_STATE]}; log len {log.Length}");
                var d2 = Writable(data.ToArray()); var l2 = Writable(log.ToArray());
                try
                {
                    using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2 })))
                        _out.WriteLine($"reopen count {db.GetCollection("rows").Count()}");
                    _out.WriteLine($"after reopen mark {d2.ToArray()[HeaderPage.P_INVALID_DATAFILE_STATE]} log {l2.Length}");
                }
                catch (Exception ex) { _out.WriteLine("reopen ex " + ex.GetType().Name + ": " + ex.Message); }
            }
        }
    }
}
#endif
