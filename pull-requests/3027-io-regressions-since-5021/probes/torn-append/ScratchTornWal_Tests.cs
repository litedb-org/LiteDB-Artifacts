using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Regressions
{
    // SCRATCH - adversarial review, never commit.
    public class ScratchTornWal_Tests
    {
        private readonly ITestOutputHelper _out;
        public ScratchTornWal_Tests(ITestOutputHelper output) => _out = output;

        internal sealed class Log : MemoryStream
        {
            internal int TearFrame;
            internal bool IoFailure, CompleteFrame, FailSetLength, Torn, SetLengthFailed, Trace;
            internal int MinCount = WalChecksum.FrameSize;
            internal readonly List<string> Events = new List<string>();

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Trace) Events.Add($"W pos={Position} slot={Position / WalChecksum.FrameSize} rem={Position % WalChecksum.FrameSize} n={count} len={Length} :: {Caller()}");
                if (TearFrame > 0 && count >= MinCount && --TearFrame == 0)
                {
                    base.Write(buffer, offset, CompleteFrame ? count : count / 2);
                    Torn = true;
                    throw Failure("injected torn frame write");
                }
                base.Write(buffer, offset, count);
            }

            public override void SetLength(long value)
            {
                if (Trace) Events.Add($"SetLength {value} (len {Length}) :: {Caller()}");
                if (FailSetLength && Torn)
                {
                    SetLengthFailed = true;
                    throw Failure("injected truncation failure");
                }
                base.SetLength(value);
            }

            private Exception Failure(string message) =>
                IoFailure ? new IOException(message) : new InvalidOperationException(message);

            private static string Caller() => string.Join(" < ", new StackTrace().GetFrames().Skip(2).Take(7)
                .Select(f => f.GetMethod()?.Name).Where(n => n != null && !n.StartsWith("<") && n != "MoveNext"));
        }

        private static BsonDocument Row(int id, int value, int size = 500) => new BsonDocument
        {
            ["_id"] = id, ["value"] = value, ["payload"] = new string('p', size)
        };

        private void Dump(Log log)
        {
            foreach (var e in log.Events) _out.WriteLine(e);
            log.Events.Clear();
        }

        // 1. Which writes are "frame 1/2" in TornWalAppend_Tests, and what writes slot N next?
        [Theory]
        [InlineData(1, false)]
        [InlineData(2, false)]
        [InlineData(1, true)]
        public void Trace_original(int frame, bool complete)
        {
            using var data = new MemoryStream();
            using var log = new Log();
            var settings = new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 1 };
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 20).Select(id => Row(id, 0)));
                _out.WriteLine($"-- armed; log len {log.Length}");
                log.Trace = true;
                log.TearFrame = frame;
                log.CompleteFrame = complete;
                log.FailSetLength = true;
                Action failed = () => db.GetCollection("rows").Insert(Enumerable.Range(100, 30).Select(id => Row(id, 0)));
                failed.Should().Throw<InvalidOperationException>();
                log.FailSetLength = false;
                _out.WriteLine("-- after failure (includes rollback)");
                Dump(log);
                var rows = db.GetCollection("rows");
                rows.Update(Enumerable.Range(1, 20).Select(id => Row(id, 7))).Should().Be(20);
                rows.Insert(Row(200, 0));
                _out.WriteLine("-- later writes");
                Dump(log);
            }
        }

        // 2. Update-only transaction (no new pages -> rollback writes nothing), then a full checkpoint
        //    or no checkpoint, then later commits; crash image must hold every acknowledged commit.
        [Theory]
        [InlineData(1, false, false)]
        [InlineData(2, false, false)]
        [InlineData(1, true, false)]
        [InlineData(1, false, true)]
        [InlineData(2, false, true)]
        [InlineData(1, true, true)]
        public void Update_only_failure_then_checkpoint(int frame, bool complete, bool checkpoint)
        {
            using var data = new MemoryStream();
            using var log = new Log();
            var settings = new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 1 };
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                var rows = db.GetCollection("rows");
                rows.Insert(Enumerable.Range(1, 20).Select(id => Row(id, 0)));
                log.Trace = true;
                log.TearFrame = frame;
                log.CompleteFrame = complete;
                log.FailSetLength = true;
                Action failed = () => rows.Update(Enumerable.Range(1, 20).Select(id => Row(id, 3)));
                failed.Should().Throw<InvalidOperationException>();
                log.SetLengthFailed.Should().BeTrue();
                log.FailSetLength = false;
                _out.WriteLine("-- after failure");
                Dump(log);
                if (checkpoint) db.Checkpoint();
                _out.WriteLine("-- after checkpoint");
                Dump(log);
                rows.Update(Enumerable.Range(1, 20).Select(id => Row(id, 7))).Should().Be(20);
                rows.Insert(Row(200, 0));
                Dump(log);
            }
            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(data.ToArray()), LogStream = new MemoryStream(log.ToArray())
            }));
            var docs = recovered.GetCollection("rows").FindAll().ToList();
            docs.Select(x => x["_id"].AsInt32).Should().BeEquivalentTo(Enumerable.Range(1, 20).Append(200));
            docs.Where(x => x["_id"].AsInt32 <= 20).Should().OnlyContain(x => x["value"].AsInt32 == 7);
        }

        /// <summary>Tears one append (Position >= Length) and fails the next SetLength once.</summary>
        internal sealed class RaceLog : MemoryStream
        {
            internal bool Armed, IoFailure = true, FailOneSetLength = true, Torn, SetLengthFailed;
            internal Action BeforeThrow;
            internal long TornAt = -1;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed && count == WalChecksum.FrameSize && Position >= Length - 8192)
                {
                    Armed = false;
                    TornAt = Position;
                    base.Write(buffer, offset, count / 2);
                    Torn = true;
                    BeforeThrow?.Invoke();
                    throw IoFailure ? (Exception)new IOException("injected torn append") : new InvalidOperationException("injected torn append");
                }
                base.Write(buffer, offset, count);
            }

            public override void SetLength(long value)
            {
                if (Torn && FailOneSetLength && !SetLengthFailed)
                {
                    SetLengthFailed = true;
                    throw IoFailure ? (Exception)new IOException("injected truncation failure") : new InvalidOperationException("injected truncation failure");
                }
                base.SetLength(value);
            }
        }

        // 3. An I/O failure stops the engine only after WriteLogDisk released the writer lock
        //    (EngineState.Handle runs in ExecuteAutoTransaction). A committer already waiting
        //    for the writer gets in first. It must not be appended behind the torn frame.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Committer_waiting_for_the_writer_during_a_torn_append(bool ioFailure)
        {
            using var committerStarted = new System.Threading.ManualResetEventSlim();
            using var failing = new System.Threading.ManualResetEventSlim();
            using var data = new MemoryStream();
            using var log = new RaceLog { IoFailure = ioFailure };
            log.BeforeThrow = () =>
            {
                failing.Set();
                committerStarted.Wait(TimeSpan.FromSeconds(10));
                System.Threading.Thread.Sleep(300); // let the committer block on the WAL writer lock
            };
            var settings = new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 1 };
            byte[] crashData, crashLog;
            bool acknowledged;
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("a").Insert(Enumerable.Range(1, 20).Select(id => Row(id, 0)));
                db.GetCollection("b").Insert(new BsonDocument { ["_id"] = 1 });

                var committer = System.Threading.Tasks.Task.Run(() =>
                {
                    if (!failing.Wait(TimeSpan.FromSeconds(10))) return false;
                    committerStarted.Set();
                    try { db.GetCollection("b").Insert(new BsonDocument { ["_id"] = 2 }); return true; }
                    catch (Exception ex) { _out.WriteLine("committer: " + ex.GetType().Name + " " + ex.Message); return false; }
                });

                log.Armed = true;
                Action failed = () => db.GetCollection("a").Insert(Enumerable.Range(100, 30).Select(id => Row(id, 0)));
                failed.Should().Throw<Exception>();
                log.SetLengthFailed.Should().BeTrue();
                acknowledged = committer.Result;
                _out.WriteLine($"tornAt={log.TornAt} acknowledged={acknowledged} logLen={log.Length}");
                crashData = data.ToArray();
                crashLog = log.ToArray();
            }
            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(crashData), LogStream = new MemoryStream(crashLog)
            }));
            recovered.GetCollection("a").Count().Should().Be(20);
            if (acknowledged) ((object)recovered.GetCollection("b").FindById(2)).Should().NotBeNull("an acknowledged commit must survive");
        }

        private sealed class HookedDocs : IEnumerable<BsonDocument>
        {
            private readonly IEnumerable<BsonDocument> _docs;
            private readonly Action _onDispose;
            public HookedDocs(IEnumerable<BsonDocument> docs, Action onDispose) { _docs = docs; _onDispose = onDispose; }
            public IEnumerator<BsonDocument> GetEnumerator() => new E(_docs.GetEnumerator(), _onDispose);
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
            private sealed class E : IEnumerator<BsonDocument>
            {
                private readonly IEnumerator<BsonDocument> _inner; private readonly Action _onDispose;
                public E(IEnumerator<BsonDocument> inner, Action onDispose) { _inner = inner; _onDispose = onDispose; }
                public BsonDocument Current => _inner.Current;
                object System.Collections.IEnumerator.Current => Current;
                public bool MoveNext() => _inner.MoveNext();
                public void Reset() => _inner.Reset();
                public void Dispose() { _onDispose(); _inner.Dispose(); }
            }
        }

        // 3b. Deterministic: the failing insert's document enumerator is disposed while the
        //     exception unwinds - after WriteLogDisk released the WAL writer lock, before
        //     ExecuteAutoTransaction calls EngineState.Handle (which stops on I/O errors).
        //     The committer waiting on the writer lock completes inside that window.
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Committer_inside_the_unwind_window_of_a_torn_append(bool ioFailure)
        {
            using var committerStarted = new System.Threading.ManualResetEventSlim();
            using var failing = new System.Threading.ManualResetEventSlim();
            using var committerDone = new System.Threading.ManualResetEventSlim();
            using var data = new MemoryStream();
            using var log = new RaceLog { IoFailure = ioFailure };
            log.BeforeThrow = () =>
            {
                failing.Set();
                committerStarted.Wait(TimeSpan.FromSeconds(10));
                System.Threading.Thread.Sleep(300); // committer blocks on the WAL writer lock
            };
            var settings = new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 1 };
            byte[] crashData, crashLog;
            bool acknowledged;
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("a").Insert(Enumerable.Range(1, 20).Select(id => Row(id, 0)));
                db.GetCollection("b").Insert(new BsonDocument { ["_id"] = 1 });

                var committer = System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (!failing.Wait(TimeSpan.FromSeconds(10))) return false;
                        committerStarted.Set();
                        db.GetCollection("b").Insert(new BsonDocument { ["_id"] = 2 });
                        return true;
                    }
                    catch (Exception ex) { _out.WriteLine("committer: " + ex.GetType().Name + " " + ex.Message); return false; }
                    finally { committerDone.Set(); }
                });

                var docs = new HookedDocs(Enumerable.Range(100, 30).Select(id => Row(id, 0)), () =>
                {
                    if (log.Torn) committerDone.Wait(TimeSpan.FromSeconds(10));
                });
                log.Armed = true;
                Action failed = () => engine.Insert("a", docs, BsonAutoId.Int32);
                failed.Should().Throw<Exception>();
                log.SetLengthFailed.Should().BeTrue();
                acknowledged = committer.Result;
                _out.WriteLine($"tornAt={log.TornAt} acknowledged={acknowledged} logLen={log.Length}");
                crashData = data.ToArray();
                crashLog = log.ToArray();
            }
            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(crashData), LogStream = new MemoryStream(crashLog)
            }));
            recovered.GetCollection("a").Count().Should().Be(20);
            acknowledged.Should().BeTrue("the committer ran inside the window before the engine stopped");
            ((object)recovered.GetCollection("b").FindById(2)).Should().NotBeNull("an acknowledged commit must survive");
        }

        // 6. Encrypted WAL (AesStream under ChecksummedWalStream): torn append whose truncation
        //    fails, non-I/O and I/O, then later commits (after reopen for I/O), crash image recovery.
        [Theory]
        [InlineData(1, false, false)]
        [InlineData(2, false, false)]
        [InlineData(1, true, false)]
        [InlineData(1, false, true)]
        [InlineData(2, false, true)]
        public void Encrypted_torn_append(int frame, bool complete, bool io)
        {
            using var data = new MemoryStream();
            using var log = new Log { IoFailure = io, MinCount = 4096 };
            var settings = new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 1, Password = "pw" };
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 20).Select(id => Row(id, 0)));
                log.Trace = true;
                log.TearFrame = frame;
                log.CompleteFrame = complete;
                log.FailSetLength = true;
                Action failed = () => db.GetCollection("rows").Insert(Enumerable.Range(100, 30).Select(id => Row(id, 0)));
                failed.Should().Throw<Exception>().Which.Should().Match(e => io ? e is IOException : !(e is IOException));
                log.Torn.Should().BeTrue();
                log.SetLengthFailed.Should().BeTrue();
                log.FailSetLength = false;
                _out.WriteLine(string.Join("\n", log.Events.Take(6)));
                log.Trace = false;
                if (!io)
                {
                    var rows = db.GetCollection("rows");
                    rows.Update(Enumerable.Range(1, 20).Select(id => Row(id, 7))).Should().Be(20);
                    rows.Insert(Row(200, 0));
                }
            }
            if (io)
            {
                using var reopened = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, Password = "pw" }));
                reopened.CheckpointSize = 0;
                var rows = reopened.GetCollection("rows");
                rows.Update(Enumerable.Range(1, 20).Select(id => Row(id, 7))).Should().Be(20);
                rows.Insert(Row(200, 0));
            }
            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(data.ToArray()), LogStream = new MemoryStream(log.ToArray()), Password = "pw"
            }));
            var docs = recovered.GetCollection("rows").FindAll().ToList();
            docs.Select(x => x["_id"].AsInt32).Should().BeEquivalentTo(Enumerable.Range(1, 20).Append(200));
            docs.Where(x => x["_id"].AsInt32 <= 20).Should().OnlyContain(x => x["value"].AsInt32 == 7);
        }

        // 4. A query-triggered safepoint inside an explicit write transaction fails with a non-I/O
        //    exception. QueryExecutor only calls EngineState.Handle (no rollback), so the explicit
        //    transaction stays Active; the failed frame's buffer was discarded by WriteLogPage.
        //    What does a later Commit() persist?
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Failed_query_safepoint_in_explicit_transaction_then_commit(bool failTruncation)
        {
            using var data = new MemoryStream();
            using var log = new RaceLog { IoFailure = false, FailOneSetLength = failTruncation };
            var settings = new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 6 };
            byte[] crashData, crashLog;
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                var big = db.GetCollection("big");
                big.Insert(Enumerable.Range(1, 100).Select(id => Row(id, 0, 2000)));
                var rows = db.GetCollection("rows");
                rows.EnsureIndex("value");
                rows.Insert(Row(1, 1));
                db.Checkpoint();

                db.BeginTrans().Should().BeTrue();
                rows.Insert(Enumerable.Range(2, 3).Select(id => Row(id, id)));
                log.Armed = true;
                Exception queryFailure = null;
                try { big.FindAll().ToList(); } catch (Exception ex) { queryFailure = ex; }
                _out.WriteLine("query: " + queryFailure?.GetType().Name + " " + queryFailure?.Message + " torn=" + log.Torn);
                log.Torn.Should().BeTrue("the query's safepoint wrote the explicit transaction's dirty pages");

                bool committed;
                try { committed = db.Commit(); }
                catch (Exception ex) { _out.WriteLine("commit: " + ex.GetType().Name + " " + ex.Message); committed = false; }
                _out.WriteLine("committed=" + committed);
                if (committed)
                {
                    DescribeRows(db, "live");
                }
                crashData = data.ToArray();
                crashLog = log.ToArray();
            }
            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(crashData), LogStream = new MemoryStream(crashLog)
            }));
            var ids = DescribeRows(recovered, "recovered");
            (ids.SetEquals(new[] { 1 }) || ids.SetEquals(new[] { 1, 2, 3, 4 })).Should().BeTrue(
                "the explicit transaction must commit atomically or not at all, found {0}", string.Join(",", ids));
        }

        // 5. Same as 4, file-backed, no torn bytes: the WAL write fails before writing anything
        //    (engine fault hook) with UnauthorizedAccessException (EACCES/EPERM on Unix).
        [Fact]
        public void Failed_query_safepoint_file_backed_no_bytes_written()
        {
            var dir = Path.Combine(Path.GetTempPath(), "scratch-torn-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "db.db");
            try
            {
                using (var engine = new LiteEngine(new EngineSettings { Filename = file, TransactionPageLimit = 6 }))
                using (var db = new LiteDatabase(engine, disposeOnClose: false))
                {
                    var big = db.GetCollection("big");
                    big.Insert(Enumerable.Range(1, 100).Select(id => Row(id, 0, 2000)));
                    var rows = db.GetCollection("rows");
                    rows.EnsureIndex("value");
                    rows.Insert(Row(1, 1));
                    db.Checkpoint();

                    db.BeginTrans().Should().BeTrue();
                    rows.Insert(Enumerable.Range(2, 3).Select(id => Row(id, id)));
                    var fired = 0;
                    engine.SimulateDiskWriteFail = page =>
                    {
                        if (fired++ == 0) throw new UnauthorizedAccessException("injected EACCES on WAL write");
                    };
                    Exception queryFailure = null;
                    try { big.FindAll().ToList(); } catch (Exception ex) { queryFailure = ex; }
                    engine.SimulateDiskWriteFail = null;
                    _out.WriteLine("query: " + queryFailure?.GetType().Name + " " + queryFailure?.Message + " fired=" + fired);
                    fired.Should().BeGreaterThan(0);
                    db.Commit().Should().BeTrue("the explicit transaction is still active and commits");
                    DescribeRows(db, "live");
                }
                using (var reopened = new LiteDatabase(new ConnectionString { Filename = file }))
                {
                    var ids = DescribeRows(reopened, "reopened");
                    (ids.SetEquals(new[] { 1 }) || ids.SetEquals(new[] { 1, 2, 3, 4 })).Should().BeTrue(
                        "the explicit transaction must commit atomically or not at all, found {0}", string.Join(",", ids));
                }
            }
            finally { try { Directory.Delete(dir, true); } catch { } }
        }

        // 5b. Scope: Rollback instead of Commit, or another write before Commit.
        [Theory]
        [InlineData("rollback")]
        [InlineData("write-then-commit")]
        public void Failed_query_safepoint_followups(string mode)
        {
            using var data = new MemoryStream();
            using var log = new MemoryStream();
            byte[] crashData, crashLog;
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, TransactionPageLimit = 6 }))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                var big = db.GetCollection("big");
                big.Insert(Enumerable.Range(1, 100).Select(id => Row(id, 0, 2000)));
                var rows = db.GetCollection("rows");
                rows.EnsureIndex("value");
                rows.Insert(Row(1, 1));
                db.Checkpoint();

                db.BeginTrans().Should().BeTrue();
                rows.Insert(Enumerable.Range(2, 3).Select(id => Row(id, id)));
                var fired = 0;
                engine.SimulateDiskWriteFail = page => { if (fired++ == 0) throw new UnauthorizedAccessException("injected"); };
                Action query = () => big.FindAll().ToList();
                query.Should().Throw<UnauthorizedAccessException>();
                engine.SimulateDiskWriteFail = null;
                if (mode == "rollback") db.Rollback().Should().BeTrue();
                else
                {
                    rows.Insert(Row(5, 5));
                    db.Commit().Should().BeTrue();
                }
                DescribeRows(db, "live");
                crashData = data.ToArray();
                crashLog = log.ToArray();
            }
            using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
            {
                DataStream = new MemoryStream(crashData), LogStream = new MemoryStream(crashLog)
            }));
            var ids = DescribeRows(recovered, "recovered");
            (ids.SetEquals(new[] { 1 }) || ids.SetEquals(new[] { 1, 2, 3, 4, 5 })).Should().BeTrue(
                "atomic or nothing, found {0}", string.Join(",", ids));
        }

        /// <summary>Tears the next 8 KiB write at position 0 (the data header) and throws a non-I/O error.</summary>
        internal sealed class TornHeaderData : MemoryStream
        {
            internal bool Armed, Torn;
            internal Action OnTear;
            internal readonly List<string> Events = new List<string>();
            public override void Write(byte[] buffer, int offset, int count)
            {
                if (Armed) Events.Add($"data W pos={Position} n={count} :: " + string.Join(" < ", new StackTrace().GetFrames().Skip(1).Take(6).Select(f => f.GetMethod()?.Name)));
                if (Armed && Position == 0 && count == 8192)
                {
                    Armed = false;
                    // New checksum (bytes 14-17) lands, new version byte (59) does not.
                    base.Write(buffer, offset, 40);
                    Torn = true;
                    OnTear?.Invoke();
                    throw new UnauthorizedAccessException("injected torn header write");
                }
                base.Write(buffer, offset, count);
            }
        }

        // 7. A format promotion inside a write transaction journals the header into the WAL, then
        //    its data-header write tears with a non-I/O error. The transaction rolls back and the
        //    engine continues with the journal outstanding. The next WAL append fails ("Cannot
        //    append WAL pages during header publication"), and WriteLogPage's cleanup truncates to
        //    stream.Length, which excludes the journal: does the header's only recovery copy survive?
        [Fact]
        public void Failed_append_cleanup_versus_outstanding_header_journal()
        {
            using var data = new TornHeaderData();
            using var log = new MemoryStream();
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, CompactStorage = CompactStorageMode.Legacy }))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(id => Row(id, 0)));
                _out.WriteLine("created version: " + db.Execute("SELECT $ FROM $database").ToEnumerable().Single().ToString());
            }
            _out.WriteLine("data header file version byte: " + data.ToArray()[59] + " logLen=" + log.Length);
            var settings = new EngineSettings { DataStream = data, LogStream = log, CompactStorage = CompactStorageMode.Auto };
            byte[] beforeNextWrite, crashData, crashLog, atTearData = null, atTearLog = null;
            data.OnTear = () => { atTearData = data.ToArray(); atTearLog = log.ToArray(); };
            using (var engine = new LiteEngine(settings))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                db.CheckpointSize = 0;
                data.Armed = true;
                Exception promotion = null;
                try
                {
                    db.GetCollection("compact").Insert(Enumerable.Range(1, 10).Select(i =>
                    {
                        var d = new BsonDocument { ["_id"] = i };
                        for (var f = 0; f < 12; f++) d["aVeryLongFieldNameNumber" + f] = f;
                        return d;
                    }));
                }
                catch (Exception ex) { promotion = ex; }
                _out.WriteLine("promotion failure: " + promotion?.GetType().Name + " " + promotion?.Message + " torn=" + data.Torn);
                foreach (var e in data.Events) _out.WriteLine(e);
                data.Torn.Should().BeTrue();
                _out.WriteLine($"log bytes at tear: {atTearLog.Length}; after failure: {log.Length}");
                crashData = data.ToArray();
                crashLog = log.ToArray();
            }
            beforeNextWrite = atTearLog;

            // Process crash at the tear (journal intact):
            Recover(atTearData, atTearLog, "crash at tear (journal intact)").Should().BeTrue();
            // Process crash after the failed call returned (rollback's failed append cleaned up):
            Recover(crashData, crashLog, "crash after failure").Should().BeTrue("the torn header must stay recoverable");

            bool Recover(byte[] d, byte[] l, string label)
            {
                try
                {
                    using var recovered = new LiteDatabase(new LiteEngine(new EngineSettings
                    {
                        DataStream = new MemoryStream(d), LogStream = new MemoryStream(l)
                    }));
                    _out.WriteLine($"{label}: rows={recovered.GetCollection("rows").Count()}");
                    return true;
                }
                catch (Exception ex) { _out.WriteLine($"{label}: open failed {ex.GetType().Name}: {ex.Message}"); return false; }
            }
        }

        private HashSet<int> DescribeRows(LiteDatabase db, string label)
        {
            var rows = db.GetCollection("rows");
            var result = new HashSet<int>();
            try
            {
                var all = rows.FindAll().ToList();
                foreach (var d in all) result.Add(d["_id"].AsInt32);
                _out.WriteLine($"{label}: FindAll ids=[{string.Join(",", all.Select(d => d["_id"].AsInt32))}] payloadOk={all.All(d => d["payload"].AsString == new string('p', 500))}");
            }
            catch (Exception ex) { _out.WriteLine($"{label}: FindAll threw {ex.GetType().Name}: {ex.Message}"); result.Add(-1); }
            try { _out.WriteLine($"{label}: Count={rows.Count()} byIndex=[{string.Join(",", rows.Find(Query.GTE("value", 0)).Select(d => d["_id"].AsInt32))}]"); }
            catch (Exception ex) { _out.WriteLine($"{label}: index query threw {ex.GetType().Name}: {ex.Message}"); result.Add(-2); }
            for (var id = 1; id <= 4; id++)
            {
                try { _out.WriteLine($"{label}: FindById({id}) = {(rows.FindById(id) == null ? "null" : "found")}"); }
                catch (Exception ex) { _out.WriteLine($"{label}: FindById({id}) threw {ex.GetType().Name}: {ex.Message}"); result.Add(-3); }
            }
            return result;
        }
    }
}
