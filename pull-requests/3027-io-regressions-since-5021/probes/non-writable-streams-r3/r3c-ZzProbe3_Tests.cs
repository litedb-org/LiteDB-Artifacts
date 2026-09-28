using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using LiteDB.Engine;
using LiteDB.Internals;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Engine
{
    /// <summary>MemoryStream with a configurable CanWrite that records every mutation attempt.</summary>
    public class ProbeStream : MemoryStream
    {
        public readonly bool Writable;
        public readonly List<string> Calls = new List<string>();
        public ProbeStream(byte[] bytes, bool writable)
        {
            Writable = true;
            base.Write(bytes, 0, bytes.Length);
            base.Position = 0;
            Writable = writable;
        }
        public override bool CanWrite => Writable;
        private void Record(string what)
        {
            var frames = Environment.StackTrace.Split('\n')
                .Where(x => x.Contains("LiteDB.Engine") && !x.Contains("ProbeStream") && !x.Contains("ConcurrentStream") && !x.Contains("ChecksummedWalStream"))
                .Select(x => x.Trim().Replace("at LiteDB.Engine.", "")).Select(x => x.Split('(')[0]).Take(4);
            Calls.Add(what + " <- " + string.Join(" <- ", frames));
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            Record("Write(" + count + ")@" + Position);
            if (!Writable) throw new NotSupportedException("probe: not writable");
            base.Write(buffer, offset, count);
        }
        public override void WriteByte(byte value)
        {
            Record("WriteByte@" + Position);
            if (!Writable) throw new NotSupportedException("probe: not writable");
            base.WriteByte(value);
        }
        public override void SetLength(long value)
        {
            Record("SetLength(" + value + ") from " + Length);
            if (!Writable) throw new NotSupportedException("probe: not writable");
            base.SetLength(value);
        }
    }

    public class ZzProbe3_Tests
    {
        private readonly ITestOutputHelper _output;
        public ZzProbe3_Tests(ITestOutputHelper output) { _output = output; }

        private static byte[] Entry(string fixture, string name)
        {
            using var resource = typeof(ZzProbe3_Tests).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources." + fixture);
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }

        private static string Describe(LiteDatabase db)
        {
            var sb = new StringBuilder();
            foreach (var name in db.GetCollectionNames().OrderBy(x => x))
            {
                var col = db.GetCollection(name);
                var docs = col.FindAll().ToList();
                var hash = docs.Select(d => d["_id"].ToString() + ":" + Convert.ToBase64String(BsonSerializer.Serialize(d)).GetHashCode())
                    .OrderBy(x => x).Aggregate(17, (h, s) => h * 31 + s.GetHashCode());
                sb.Append(name).Append('=').Append(docs.Count).Append('#').Append(hash).Append(' ');
                // one index query per collection (exercises index paths or legacy scans)
                foreach (var index in db.GetCollection("$indexes").Find(Query.EQ("collection", name)))
                {
                    var expr = index["expression"].AsString;
                    if (index["name"].AsString == "_id") continue;
                    try { sb.Append("[").Append(index["name"].AsString).Append(':').Append(col.Count(expr + " != null")).Append("]"); }
                    catch (Exception ex) { sb.Append("[idxERR ").Append(ex.GetType().Name).Append("]"); }
                }
            }
            return sb.ToString();
        }

        private static string Reference(byte[] data, byte[] log)
        {
            try
            {
                using var engine = new LiteEngine(new EngineSettings
                {
                    DataStream = new MemoryStream((byte[])data.Clone(), false),
                    LogStream = new MemoryStream((byte[])log.Clone(), false),
                    ReadOnly = true, LegacyIndexScan = true
                });
                using var db = new LiteDatabase(engine);
                return Describe(db);
            }
            catch (Exception ex) { return "REF-FAIL " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
        }

        internal static List<(string name, byte[] data, byte[] log)> Images(bool heavy)
        {
            var images = new List<(string, byte[], byte[])>();
            // 1. 5.0.21 migration crash images
            {
                var original = Entry("IndexMigration_5_0_21.zip", "plain.db");
                var originalLog = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
                images.Add(("5021 plain", original, originalLog));
                using var device = new IndexMigrationCrashDevice(original, originalLog);
                using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = device.Data, LogStream = device.Log, TransactionPageLimit = 4 })))
                {
                    db.GetCollection("rows").Count();
                    device.Stage = "checkpoint";
                    db.Checkpoint();
                    device.Armed = false;
                }
                images.AddRange(device.Images.Select((i, n) => ("mig#" + n + " " + i.Event, i.Data, i.Log)));
                images.Add(("mig final", device.Data.ToArray(), device.Log.ToArray()));
            }
            // 2. 5.0.21 WAL crash with torn tails, and crash images of converting one
            {
                var data = Entry("WalCrash_5_0_21.zip", "crash.db");
                var log = Entry("WalCrash_5_0_21.zip", "crash-log.db");
                images.Add(("walcrash", data, log));
                foreach (var tail in new[] { 100, 512, 4096 })
                {
                    var torn = tail == 100 ? Enumerable.Repeat((byte)0xCD, tail).ToArray() : log.Skip(log.Length - 3 * Constants.PAGE_SIZE).Take(tail).ToArray();
                    if (tail != 100) { BitConverter.GetBytes(22u).CopyTo(torn, BasePage.P_TRANSACTION_ID); torn[BasePage.P_IS_CONFIRMED] = 1; }
                    var tornLog = log.Concat(torn).ToArray();
                    images.Add(("walcrash+tail" + tail, data, tornLog));
                    if (tail == 512 && heavy)
                    {
                        using var device = new IndexMigrationCrashDevice(data, tornLog);
                        using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = device.Data, LogStream = device.Log }))) db.GetCollection("docs").Count();
                        device.Armed = false;
                        images.AddRange(device.Images.Select((i, n) => ("walcrash512#" + n + " " + i.Event, i.Data, i.Log)));
                    }
                }
            }
            // 3. promotion power loss images
            foreach (var phase in PromotionPowerLossScenario.Phases)
            {
                foreach (var torn in new[] { -1, 59 })
                {
                    try
                    {
                        PromotionPowerLossScenario.Run(null, false, phase, tornPrefix: torn, damage: torn >= 0,
                            inspectFiles: (d, l) => images.Add(("promo " + phase + " torn" + torn, d, l)));
                    }
                    catch (Exception) { }
                }
            }
            try
            {
                PromotionPowerLossScenario.Run(null, false, "promotion-before-journal-write", tornPrefix: 171, damage: true, tornJournalPart: 0,
                    inspectFiles: (d, l) => images.Add(("promo torn journal", d, l)));
            }
            catch (Exception) { }
            // 4. MVCC retirement crash images (unencrypted: LiteDatabase(Stream) has no password)
            foreach (var compact in new[] { false, true })
            {
                try { MvccRetirementScenario.Run(null, compact, null, inspect: (d, l) => images.Add(("mvcc clean rooted c" + compact, d, l))); }
                catch (Exception) { }
                foreach (var phase in MvccRetirementScenario.Phases)
                {
                    foreach (var prefix in heavy ? new[] { -1, 171 } : new[] { -1 })
                    {
                        try { MvccRetirementScenario.Run(null, compact, phase, prefix, inspect: (d, l) => images.Add(("mvcc " + phase + " p" + prefix + " c" + compact, d, l))); }
                        catch (Exception) { }
                    }
                }
                foreach (var phase in new[] { "checkpoint-before-page-write", "checkpoint-after-page-write",
                    "checkpoint-before-data-flush", "checkpoint-after-data-flush", "before-reclaim",
                    "checkpoint-before-clear", "checkpoint-after-clear" })
                {
                    foreach (var fault in heavy ? new[] { "cut", "flush", "tear" } : new[] { "cut" })
                    {
                        var n = 0;
                        try { MvccRootedCheckpointScenario.Run(null, compact, phase, fault, inspect: (d, l) => images.Add(("rooted " + phase + " " + fault + " c" + compact + " #" + n++, d, l))); }
                        catch (Exception) { }
                    }
                }
                {
                    var n = 0;
                    try { MvccRootedCheckpointScenario.Run(null, compact, "before-reclaim", "tear", repeatRepair: true, inspect: (d, l) => images.Add(("rooted repeat-repair c" + compact + " #" + n++, d, l))); }
                    catch (Exception) { }
                }
            }
            // 5. constructed current-format images
            {
                var (d, l) = CurrentWithWal(CompactStorageMode.Auto);
                images.Add(("current clean wal", d, l));
                images.Add(("current wal +100 garbage", d, l.Concat(Enumerable.Repeat((byte)0xCD, 100)).ToArray()));
                images.Add(("current wal +whole garbage frame", d, l.Concat(Enumerable.Repeat((byte)0xCD, WalChecksum.FrameSize)).ToArray()));
                var bad = (byte[])l.Clone();
                var frames = bad.Length / WalChecksum.FrameSize;
                bad[(frames - 1) * WalChecksum.FrameSize + 100] ^= 0xFF;
                images.Add(("current wal last frame corrupt", d, bad));
                var torn = l.Take(l.Length - 5000).ToArray();
                images.Add(("current wal torn last frame", d, torn));
                images.Add(("current data partial page", d.Concat(Encoding.ASCII.GetBytes("partial")).ToArray(), l));
                images.Add(("current empty log", d, new byte[0]));
                var (d11, l11) = CurrentWithWal(CompactStorageMode.Legacy);
                images.Add(("v11 clean wal", d11, l11));
                images.Add(("v11 wal +100 garbage", d11, l11.Concat(Enumerable.Repeat((byte)0xCD, 100)).ToArray()));
            }
            return images;
        }

        internal static (byte[], byte[]) CurrentWithWal(CompactStorageMode mode)
        {
            var data = new MemoryStream();
            var log = new MemoryStream();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, CompactStorage = mode })))
            {
                db.CheckpointSize = 0;
                var rows = db.GetCollection("rows");
                rows.EnsureIndex("value");
                rows.Insert(Enumerable.Range(1, 50).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i % 7, ["p"] = new string('x', 300) }));
                db.Checkpoint();
                rows.Insert(Enumerable.Range(51, 20).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i % 7, ["p"] = new string('y', 300) }));
                rows.Update(new BsonDocument { ["_id"] = 3, ["value"] = 100 });
                db.GetCollection("other").Insert(new BsonDocument { ["_id"] = 1 });
            }
            return (data.ToArray(), log.ToArray());
        }

        private static string H(byte[] b) => b.Length + ":" + (b.Aggregate(17L, (h, x) => h * 31 + x) & 0xFFFFFF).ToString("x");

        [Fact]
        public void Replay_images_over_non_writable_combinations() => Replay(false);

        [Fact]
        public void Replay_images_over_non_writable_combinations_heavy() => Replay(true);

        private void Replay(bool heavy)
        {
            var images = Images(heavy);
            _output.WriteLine("images: " + images.Count);
            foreach (var g in images.GroupBy(i => i.name.Split(' ')[0])) _output.WriteLine("  " + g.Key + " x" + g.Count());
            var refFails = 0;
            var failures = new List<string>();
            var stats = new Dictionary<string, int>();
            foreach (var (name, dataBytes, logBytes) in images)
            {
                var reference = Reference(dataBytes, logBytes);
                if (reference.StartsWith("REF-FAIL")) { refFails++; _output.WriteLine("reffail " + name + " " + reference); }
                if (name.StartsWith("rooted") || name.StartsWith("mvcc") || name.StartsWith("current") || name.StartsWith("v11")) _output.WriteLine("sample " + name + " v" + dataBytes[HeaderPage.P_FILE_VERSION] + " log=" + logBytes.Length + " ref=" + reference);
                var combos = new List<(bool dataW, bool? logW)> { (false, true), (false, false), (true, false) };
                if (logBytes.Length == 0) combos.Add((false, null));
                foreach (var (dataW, logW) in combos)
                {
                    var data = new ProbeStream(dataBytes, dataW);
                    var log = logW == null ? null : new ProbeStream(logBytes, logW.Value);
                    string result; bool ro = false;
                    try
                    {
                        using (var db = new LiteDatabase(data, null, log))
                        {
                            ro = db.GetCollection("$database").FindAll().Single()["readOnly"].AsBoolean;
                            result = Describe(db);
                        }
                    }
                    catch (Exception ex) { result = "FAIL " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
                    var dataChanged = !data.ToArray().SequenceEqual(dataBytes);
                    var logChanged = log != null && !log.ToArray().SequenceEqual(logBytes);
                    var calls = data.Calls.Select(c => "D:" + c).Concat(log?.Calls.Select(c => "L:" + c) ?? Enumerable.Empty<string>()).ToList();
                    var combo = $"data{(dataW ? "W" : "R")}/log{(logW == null ? "-" : logW.Value ? "W" : "R")}";
                    var ok = result == reference && !dataChanged && !logChanged && calls.Count == 0;
                    var key = combo + " " + (ok ? "OK" : "BAD") + " ro=" + ro;
                    stats[key] = stats.TryGetValue(key, out var c) ? c + 1 : 1;
                    if (!ok)
                    {
                        var line = $"{name} [{combo}] ro={ro} dataChanged={dataChanged} logChanged={logChanged}\n    ref={reference}\n    got={result}" +
                            string.Concat(calls.Take(6).Select(x => "\n    " + x));
                        failures.Add(line);
                    }
                }
            }
            _output.WriteLine("reference failures: " + refFails);
            foreach (var kv in stats.OrderBy(x => x.Key)) _output.WriteLine(kv.Value + " x " + kv.Key);
            foreach (var f in failures) _output.WriteLine(f);
            Assert.Empty(failures);
        }
    
        private static byte[] CurrentCheckpointed(CompactStorageMode mode = CompactStorageMode.Auto)
        {
            var data = new MemoryStream();
            var log = new MemoryStream();
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, CompactStorage = mode })))
            {
                var rows = db.GetCollection("rows");
                rows.EnsureIndex("value");
                rows.Insert(Enumerable.Range(1, 50).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i % 7, ["p"] = new string('x', 300) }));
                db.Checkpoint();
            }
            Assert.Equal(0, log.Length);
            return data.ToArray();
        }

        private static bool RO(LiteDatabase db) => db.GetCollection("$database").FindAll().Single()["readOnly"].AsBoolean;

        private void Dump(string label, ProbeStream s)
        {
            _output.WriteLine(label + " calls=" + s.Calls.Count);
            foreach (var c in s.Calls.Take(8)) _output.WriteLine("   " + c);
        }

        private string Try(Func<object> f)
        {
            try { return "ok " + f(); }
            catch (Exception ex) { return "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
        }

        [Fact]
        public void W1_current_file_data_readonly_log_writable()
        {
            var d = CurrentCheckpointed();
            var data = new ProbeStream(d, false);
            var log = new ProbeStream(new byte[0], true);
            using (var db = new LiteDatabase(data, null, log))
            {
                var rows = db.GetCollection("rows");
                _output.WriteLine("ro=" + RO(db));
                _output.WriteLine("begin " + Try(() => db.BeginTrans()));
                _output.WriteLine("noop delete " + Try(() => rows.DeleteMany(Query.EQ("value", 999))));
                _output.WriteLine("commit " + Try(() => db.Commit()));
                _output.WriteLine("log after no-op commit=" + log.Length);
                _output.WriteLine("begin " + Try(() => db.BeginTrans()));
                _output.WriteLine("rollback " + Try(() => db.Rollback()));
                _output.WriteLine("insert " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 1000, ["value"] = 3 })));
                _output.WriteLine("find " + Try(() => rows.FindById(1000)));
                _output.WriteLine("big insert " + Try(() => rows.Insert(Enumerable.Range(2000, 2500).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i % 7, ["p"] = new string('z', 4000) }))));
                _output.WriteLine("log after big insert=" + log.Length + " pages~" + log.Length / 8256);
                _output.WriteLine("checkpoint " + Try(() => { db.Checkpoint(); return 0; }));
                _output.WriteLine("pragma checkpoint=1 " + Try(() => { db.CheckpointSize = 1; return db.CheckpointSize; }));
                _output.WriteLine("insert2 " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 1001, ["value"] = 3 })));
                _output.WriteLine("ensureIndex " + Try(() => rows.EnsureIndex("p")));
                _output.WriteLine("explicit trans write " + Try(() => { db.BeginTrans(); rows.Insert(new BsonDocument { ["_id"] = 1002 }); return db.Rollback(); }));
                _output.WriteLine("count " + Try(() => rows.Count()) + " idx " + Try(() => rows.Count(Query.EQ("value", 3))));
                _output.WriteLine("delete " + Try(() => rows.DeleteMany(Query.EQ("value", 3))));
                _output.WriteLine("rebuild " + Try(() => db.Rebuild()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d));
            Dump("data", data);
            _output.WriteLine("log length=" + log.Length);
            var logBytes = log.ToArray();
            using (var db = new LiteDatabase(new ProbeStream(d, false), null, new ProbeStream(logBytes, true)))
                _output.WriteLine("reopen RO-data: ro=" + RO(db) + " count=" + db.GetCollection("rows").Count() + " value3=" + db.GetCollection("rows").Count(Query.EQ("value", 3)));
            var d2 = new MemoryStream(); d2.Write(d, 0, d.Length);
            var l2 = new MemoryStream(); l2.Write(logBytes, 0, logBytes.Length);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2 })))
            {
                _output.WriteLine("reopen writable: count=" + db.GetCollection("rows").Count()); db.Checkpoint();
                _output.WriteLine("after checkpoint count=" + db.GetCollection("rows").Count() + " idx p=" + db.GetCollection("rows").Count(Query.Not("p", null)));
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void W2_current_file_data_writable_log_readonly(bool withWal)
        {
            byte[] d, l;
            if (withWal) (d, l) = CurrentWithWal(CompactStorageMode.Auto); else { d = CurrentCheckpointed(); l = new byte[0]; }
            var data = new ProbeStream(d, true);
            var log = new ProbeStream(l, false);
            using (var db = new LiteDatabase(data, null, log))
            {
                var rows = db.GetCollection("rows");
                _output.WriteLine("ro=" + RO(db));
                _output.WriteLine("begin " + Try(() => db.BeginTrans()));
                _output.WriteLine("noop delete " + Try(() => rows.DeleteMany(Query.EQ("value", 999))));
                _output.WriteLine("commit " + Try(() => db.Commit()));
                _output.WriteLine("insert " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 1000, ["value"] = 3 })));
                _output.WriteLine("count after " + Try(() => rows.Count()));
                _output.WriteLine("checkpoint " + Try(() => { db.Checkpoint(); return 0; }));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log changed=" + !log.ToArray().SequenceEqual(l));
            Dump("data", data);
            Dump("log", log);
        }

        [Fact]
        public void W3_current_file_data_readonly_no_log()
        {
            var d = CurrentCheckpointed();
            var data = new ProbeStream(d, false);
            using (var db = new LiteDatabase(data))
            {
                var rows = db.GetCollection("rows");
                _output.WriteLine("ro=" + RO(db));
                _output.WriteLine("big insert " + Try(() => rows.Insert(Enumerable.Range(2000, 2500).Select(i => new BsonDocument { ["_id"] = i, ["p"] = new string('z', 4000) }))));
                _output.WriteLine("count " + Try(() => rows.Count()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d));
            Dump("data", data);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void W4_v11_file_data_readonly_log_writable_compact_write(bool checkpointed)
        {
            byte[] d, l;
            if (checkpointed) { d = CurrentCheckpointed(CompactStorageMode.Legacy); l = new byte[0]; }
            else (d, l) = CurrentWithWal(CompactStorageMode.Legacy);
            _output.WriteLine("file version=" + d[HeaderPage.P_FILE_VERSION]);
            var data = new ProbeStream(d, false);
            var log = new ProbeStream(l, true);
            using (var db = new LiteDatabase(data, null, log))
            {
                var rows = db.GetCollection("rows");
                _output.WriteLine("ro=" + RO(db));
                _output.WriteLine("insert " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 1000, ["value"] = 3, ["nested"] = new BsonDocument { ["a"] = 1 } })));
                _output.WriteLine("count " + Try(() => rows.Count()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log changed=" + !log.ToArray().SequenceEqual(l) + " log " + l.Length + "->" + log.Length);
            Dump("data", data);
            Dump("log", log);
            var logBytes = log.ToArray();
            using (var db = new LiteDatabase(new ProbeStream(d, false), null, new ProbeStream(logBytes, false)))
                _output.WriteLine("reopen RO/RO: ro=" + RO(db) + " count=" + db.GetCollection("rows").Count());
            var d2 = new MemoryStream(); d2.Write(d, 0, d.Length);
            var l2 = new MemoryStream(); l2.Write(logBytes, 0, logBytes.Length);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2 })))
                _output.WriteLine("reopen writable: count=" + db.GetCollection("rows").Count());
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(true, false)]
        public void T1_fallback_explicit_transactions_write_nothing(bool dataW, bool logW)
        {
            var d = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var l = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            var data = new ProbeStream(d, dataW);
            var log = new ProbeStream(l, logW);
            using (var db = new LiteDatabase(data, null, log))
            {
                var rows = db.GetCollection("rows");
                _output.WriteLine("ro=" + RO(db) + " count=" + rows.Count());
                _output.WriteLine("begin " + Try(() => db.BeginTrans()));
                _output.WriteLine("begin nested " + Try(() => db.BeginTrans()));
                IBsonDataReader reader = null;
                _output.WriteLine("open cursor " + Try(() => { reader = db.Execute("SELECT $ FROM rows"); return reader.Read(); }));
                _output.WriteLine("commit with cursor " + Try(() => db.Commit()));
                reader?.Dispose();
                _output.WriteLine("for update " + Try(() => rows.Query().ForUpdate().ToList().Count));
                _output.WriteLine("sql for update " + Try(() => { using var r = db.Execute("SELECT $ FROM rows FOR UPDATE"); var n = 0; while (r.Read()) n++; return n; }));
                _output.WriteLine("write in trans " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 99999 })));
                _output.WriteLine("upsert in trans " + Try(() => rows.Upsert(new BsonDocument { ["_id"] = 99999 })));
                _output.WriteLine("ensure existing index " + Try(() => rows.EnsureIndex("_id")));
                _output.WriteLine("pragma " + Try(() => { db.UserVersion = 3; return 0; }));
                _output.WriteLine("commit " + Try(() => db.Commit()));
                _output.WriteLine("begin/for update/rollback " + Try(() => { db.BeginTrans(); rows.Query().ForUpdate().ToList(); return db.Rollback(); }));
                _output.WriteLine("sql begin/commit " + Try(() => { db.Execute("BEGIN"); db.Execute("SELECT $ FROM rows FOR UPDATE").Dispose(); db.Execute("COMMIT"); return 0; }));
                _output.WriteLine("threads " + Try(() =>
                {
                    var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
                    var threads = Enumerable.Range(0, 4).Select(_ => new System.Threading.Thread(() =>
                    {
                        try { for (var i = 0; i < 20; i++) { db.BeginTrans(); rows.Query().ForUpdate().Limit(3).ToList(); if (i % 2 == 0) db.Commit(); else db.Rollback(); } }
                        catch (Exception ex) { errors.Add(ex.GetType().Name + " " + ex.Message); }
                    })).ToList();
                    threads.ForEach(t => t.Start()); threads.ForEach(t => t.Join());
                    return string.Join("|", errors.Distinct());
                }));
                _output.WriteLine("count end " + Try(() => rows.Count()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log changed=" + !log.ToArray().SequenceEqual(l));
            Dump("data", data);
            Dump("log", log);
        }

        /// <summary>Error close of the read-only fallback over a checksummed (converted) file.</summary>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void E1_fallback_error_close_on_damaged_legacy_page(bool dataW, bool logW)
        {
            var original = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var originalLog = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            var dm = new MemoryStream(); dm.Write(original, 0, original.Length);
            var lm = new MemoryStream(); lm.Write(originalLog, 0, originalLog.Length);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = dm, LogStream = lm }))) { db.GetCollection("rows").Count(); db.Checkpoint(); }
            var d = dm.ToArray();
            _output.WriteLine("migrated version=" + d[HeaderPage.P_FILE_VERSION] + " log=" + lm.Length);
            var damaged = 0;
            for (var pos = Constants.PAGE_SIZE; pos + Constants.PAGE_SIZE <= d.Length; pos += Constants.PAGE_SIZE)
            {
                if (d[pos + BasePage.P_PAGE_TYPE] == (byte)PageType.Data && d[pos + BasePage.P_PAGE_FORMAT] == PageChecksum.Legacy)
                {
                    d[pos + Constants.PAGE_SIZE - 4] = 0xF0; d[pos + Constants.PAGE_SIZE - 3] = 0xFF; // slot 0 position
                    damaged++;
                }
            }
            _output.WriteLine("damaged legacy data pages=" + damaged);
            var partial = d.Concat(Encoding.ASCII.GetBytes("partial")).ToArray(); // forces the fallback
            var l = lm.ToArray();
            var data = new ProbeStream(partial, dataW);
            var log = new ProbeStream(l, logW);
            using (var db = new LiteDatabase(data, null, log))
            {
                _output.WriteLine("ro=" + RO(db));
                _output.WriteLine("read " + Try(() => db.GetCollection("rows").FindAll().Count()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(partial) + " log changed=" + !log.ToArray().SequenceEqual(l) + " log " + l.Length + "->" + log.Length);
            Dump("data", data);
            Dump("log", log);
        }

        private void ReopenAfter(string label, byte[] d, byte[] l)
        {
            foreach (var (dw, lw) in new[] { (false, false), (false, true), (true, false) })
            {
                var data = new ProbeStream(d, dw); var log = new ProbeStream(l, lw);
                string r;
                try
                {
                    using var db = new LiteDatabase(data, null, log);
                    r = "ro=" + RO(db) + " " + Try(() => db.GetCollection("rows").Count()) + " shapes=" + Try(() => db.GetCollection("shapes").Count());
                }
                catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
                _output.WriteLine($"  {label} reopen data{(dw ? "W" : "R")}/log{(lw ? "W" : "R")}: {r} dataChanged={!data.ToArray().SequenceEqual(d)} logChanged={!log.ToArray().SequenceEqual(l)}");
            }
            var d2 = new MemoryStream(); d2.Write(d, 0, d.Length);
            var l2 = new MemoryStream(); l2.Write(l, 0, l.Length);
            try
            {
                using var engine = new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2 });
                using var db = new LiteDatabase(engine);
                _output.WriteLine("  " + label + " reopen writable engine: invalidState=" + engine.InvalidDatafileState + " " + Try(() => db.GetCollection("rows").Count()));
            }
            catch (Exception ex) { _output.WriteLine("  " + label + " reopen writable engine THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void E1b_fallback_error_close_then_reopen(bool dataW, bool logW)
        {
            var original = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var originalLog = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            var dm = new MemoryStream(); dm.Write(original, 0, original.Length);
            var lm = new MemoryStream(); lm.Write(originalLog, 0, originalLog.Length);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = dm, LogStream = lm }))) { db.GetCollection("rows").Count(); db.Checkpoint(); }
            var d = dm.ToArray();
            for (var pos = Constants.PAGE_SIZE; pos + Constants.PAGE_SIZE <= d.Length; pos += Constants.PAGE_SIZE)
                if (d[pos + BasePage.P_PAGE_TYPE] == (byte)PageType.Data && d[pos + BasePage.P_PAGE_FORMAT] == PageChecksum.Legacy)
                { d[pos + Constants.PAGE_SIZE - 4] = 0xF0; d[pos + Constants.PAGE_SIZE - 3] = 0xFF; }
            var partial = d.Concat(Encoding.ASCII.GetBytes("partial")).ToArray();
            var l = lm.ToArray();
            var data = new ProbeStream(partial, dataW);
            var log = new ProbeStream(l, logW);
            using (var db = new LiteDatabase(data, null, log))
                _output.WriteLine("ro=" + RO(db) + " read " + Try(() => db.GetCollection("rows").FindAll().Count()));
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(partial) + " log " + l.Length + "->" + log.Length);
            ReopenAfter("after", data.ToArray(), log.ToArray());
            // explicit read-only engine over the same streams: same behaviour?
            var data2 = new ProbeStream(partial, dataW);
            var log2 = new ProbeStream(l, logW);
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data2, LogStream = log2, ReadOnly = true, LegacyIndexScan = true }))
            using (var db = new LiteDatabase(engine))
                _output.WriteLine("explicit ReadOnly engine read " + Try(() => db.GetCollection("rows").FindAll().Count()));
            _output.WriteLine("explicit ReadOnly: data changed=" + !data2.ToArray().SequenceEqual(partial) + " log " + l.Length + "->" + log2.Length);
        }

        /// <summary>5.0.21 file (legacy, unconverted) with a damaged page over each combination.</summary>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, false)]
        public void E2_5021_damaged_page_error_close(bool dataW, bool logW)
        {
            var d = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var l = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            for (var pos = Constants.PAGE_SIZE; pos + Constants.PAGE_SIZE <= d.Length; pos += Constants.PAGE_SIZE)
                if (d[pos + BasePage.P_PAGE_TYPE] == (byte)PageType.Data) { d[pos + Constants.PAGE_SIZE - 4] = 0xF0; d[pos + Constants.PAGE_SIZE - 3] = 0xFF; }
            var data = new ProbeStream(d, dataW);
            var log = new ProbeStream(l, logW);
            using (var db = new LiteDatabase(data, null, log))
                _output.WriteLine("ro=" + RO(db) + " read " + Try(() => db.GetCollection("rows").FindAll().Count()));
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log changed=" + !log.ToArray().SequenceEqual(l));
            Dump("data", data);
            Dump("log", log);
        }

        /// <summary>Writable ReadOnlyStorage engine (current v12 file) hitting INVALID_DATAFILE_STATE.</summary>
        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(false, null)]
        public void E3_current_file_error_close(bool dataW, bool? logW)
        {
            var d = CurrentCheckpointed();
            var n = 0;
            for (var pos = Constants.PAGE_SIZE; pos + Constants.PAGE_SIZE <= d.Length; pos += Constants.PAGE_SIZE)
                if (d[pos + BasePage.P_PAGE_TYPE] == (byte)PageType.Data)
                {
                    d[pos + Constants.PAGE_SIZE - 4] = 0xF0; d[pos + Constants.PAGE_SIZE - 3] = 0xFF;
                    PageChecksum.Write(new BufferSlice(d, pos, Constants.PAGE_SIZE));
                    n++;
                }
            _output.WriteLine("damaged+restamped pages=" + n + " version=" + d[HeaderPage.P_FILE_VERSION]);
            var data = new ProbeStream(d, dataW);
            var log = logW == null ? null : new ProbeStream(new byte[0], logW.Value);
            using (var db = new LiteDatabase(data, null, log))
                _output.WriteLine("ro=" + RO(db) + " read " + Try(() => db.GetCollection("rows").FindAll().Count()));
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log " + (log == null ? "-" : "0->" + log.Length));
            Dump("data", data);
            if (log != null) { Dump("log", log); ReopenAfter("after", data.ToArray(), log.ToArray()); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void W5_rooted_v13_data_readonly_log_writable_writes_and_reopen(bool compact)
        {
            byte[] d = null, l = null;
            MvccRetirementScenario.Run(null, compact, null, inspect: (a, b) => { d ??= a; l ??= b; });
            _output.WriteLine("version=" + d[HeaderPage.P_FILE_VERSION] + " log=" + l.Length);
            var data = new ProbeStream(d, false);
            var log = new ProbeStream(l, true);
            using (var db = new LiteDatabase(data, null, log))
            {
                _output.WriteLine("ro=" + RO(db));
                var rows = db.GetCollection("rows");
                for (var round = 0; round < 6; round++)
                    _output.WriteLine("update " + round + " " + Try(() => rows.Update(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 20 + round)))));
                _output.WriteLine("insert " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 100, ["value"] = 25 })));
                _output.WriteLine("value25 " + Try(() => rows.Count(Query.EQ("value", 25))));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log " + l.Length + "->" + log.Length);
            Dump("data", data);
            var logBytes = log.ToArray();
            foreach (var (dw, lw) in new[] { (false, false), (false, true), (true, false) })
            {
                using var db = new LiteDatabase(new ProbeStream(d, dw), null, new ProbeStream(logBytes, lw));
                var rows = db.GetCollection("rows");
                _output.WriteLine($"reopen data{(dw ? "W" : "R")}/log{(lw ? "W" : "R")}: ro={RO(db)} count={rows.Count()} value25={rows.Count(Query.EQ("value", 25))} all25={rows.FindAll().Count(x => x["value"].AsInt32 == 25)}");
            }
            var d2 = new MemoryStream(); d2.Write(d, 0, d.Length);
            var l2 = new MemoryStream(); l2.Write(logBytes, 0, logBytes.Length);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2 })))
            {
                var rows = db.GetCollection("rows");
                db.Checkpoint();
                _output.WriteLine($"writable+checkpoint: count={rows.Count()} value25={rows.Count(Query.EQ("value", 25))} untouched={db.GetCollection("untouched").Count()}");
            }
        }

        [Fact]
        public void W6_walcrash_fallback_matches_5021_counts()
        {
            var data = Entry("WalCrash_5_0_21.zip", "crash.db");
            var log = Entry("WalCrash_5_0_21.zip", "crash-log.db");
            foreach (var (dw, lw) in new[] { (false, false), (false, true), (true, false) })
            {
                using var db = new LiteDatabase(new ProbeStream(data, dw), null, new ProbeStream(log, lw));
                var docs = db.GetCollection("docs").FindAll().ToList();
                _output.WriteLine($"data{(dw ? "W" : "R")}/log{(lw ? "W" : "R")}: ro={RO(db)} docs={docs.Count} value7={docs.Count(x => x["value"].AsInt32 == 7)} idx7={db.GetCollection("docs").Count(Query.EQ("value", 7))}");
            }
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void T2_fallback_for_update_with_safepoints(bool dataW, bool logW)
        {
            var d = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var l = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            var data = new ProbeStream(d, dataW);
            var log = new ProbeStream(l, logW);
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, ReadOnlyStorage = true, TransactionPageLimit = 1 }))
            using (var db = new LiteDatabase(engine))
            {
                var rows = db.GetCollection("rows");
                _output.WriteLine("ro=" + RO(db));
                for (var i = 0; i < 3; i++)
                {
                    _output.WriteLine("begin " + Try(() => db.BeginTrans()));
                    _output.WriteLine("for update " + Try(() => rows.Query().ForUpdate().ToList().Count));
                    _output.WriteLine("for update idx " + Try(() => rows.Query().Where("_id > 5").ForUpdate().ToList().Count));
                    _output.WriteLine((i % 2 == 0 ? "commit " : "rollback ") + Try(() => i % 2 == 0 ? db.Commit() : db.Rollback()));
                }
                _output.WriteLine("count " + Try(() => rows.Count()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log changed=" + !log.ToArray().SequenceEqual(l));
            Dump("data", data);
            Dump("log", log);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void T3_fallback_noop_ops(bool dataW, bool logW)
        {
            var d = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var l = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            var data = new ProbeStream(d, dataW);
            var log = new ProbeStream(l, logW);
            using (var db = new LiteDatabase(data, null, log))
                _output.WriteLine("ro=" + RO(db) + " rebuild=" + Try(() => db.Rebuild()) + " checkpoint=" + Try(() => { db.Checkpoint(); return 0; }) + " dropMissing=" + Try(() => db.DropCollection("missing")) + " deleteNone=" + Try(() => db.GetCollection("rows").DeleteMany("1=0")));
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log changed=" + !log.ToArray().SequenceEqual(l) + " calls=" + (data.Calls.Count + log.Calls.Count));
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, false)]
        [InlineData(false, null)]
        [InlineData(true, false)]
        public void X1_engine_settings_non_writable_stream_5021(bool dataW, bool? logW)
        {
            var d = Entry("IndexMigration_5_0_21.zip", "plain.db");
            var l = Entry("IndexMigration_5_0_21.zip", "plain-log.db");
            var data = new ProbeStream(d, dataW);
            var log = logW == null ? null : new ProbeStream(l, logW.Value);
            string r;
            try
            {
                using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log }));
                r = "ro=" + RO(db) + " count=" + db.GetCollection("rows").Count();
            }
            catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
            _output.WriteLine(r);
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log " + (log == null ? "-" : l.Length + "->" + log.Length + " changed=" + !log.ToArray().SequenceEqual(l)));
            Dump("data", data);
            if (log != null) Dump("log", log);
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(false, null)]
        public void X2_engine_settings_encrypted_non_writable_5021(bool dataW, bool? logW)
        {
            var d = Entry("IndexMigration_5_0_21.zip", "encrypted.db");
            var l = Entry("IndexMigration_5_0_21.zip", "encrypted-log.db");
            var data = new ProbeStream(d, dataW);
            var log = logW == null ? null : new ProbeStream(l, logW.Value);
            string r;
            try
            {
                using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, Password = "migration-power-loss" }));
                r = "ro=" + RO(db) + " count=" + db.GetCollection("rows").Count();
            }
            catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
            _output.WriteLine(r);
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log " + (log == null ? "-" : l.Length + "->" + log.Length + " changed=" + !log.ToArray().SequenceEqual(l)));
            try
            {
                using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = new MemoryStream(d, false), Password = "migration-power-loss", ReadOnly = true, LegacyIndexScan = true }));
                _output.WriteLine("workaround ReadOnly+LegacyIndexScan count=" + db.GetCollection("rows").Count());
            }
            catch (Exception ex) { _output.WriteLine("workaround THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
        }

        [Fact]
        public void W7_current_file_readonly_filestream_checkpoint_with_empty_log()
        {
            using var file = new TempFile();
            File.WriteAllBytes(file.Filename, CurrentCheckpointed());
            var original = File.ReadAllBytes(file.Filename);
            using (var stream = new FileStream(file.Filename, FileMode.Open, FileAccess.Read))
            using (var db = new LiteDatabase(stream))
            {
                _output.WriteLine("ro=" + RO(db) + " checkpoint=" + Try(() => { db.Checkpoint(); return 0; }) + " count=" + Try(() => db.GetCollection("rows").Count()));
            }
            _output.WriteLine("changed=" + !File.ReadAllBytes(file.Filename).SequenceEqual(original));
        }

        [Fact]
        public void X3_filename_readonly_begintrans()
        {
            using var file = new TempFile();
            File.WriteAllBytes(file.Filename, Entry("IndexMigration_5_0_21.zip", "plain.db"));
            using var db = new LiteDatabase($"Filename={file.Filename};ReadOnly=true;legacy index scan=true");
            _output.WriteLine("begin=" + Try(() => db.BeginTrans()) + " count=" + Try(() => db.GetCollection("rows").Count()));
        }

        [Theory]
        [InlineData(false, true)]
        [InlineData(true, false)]
        public void W4b_v11_file_compact_promotion_over_non_writable_storage(bool dataW, bool logW)
        {
            var d = CurrentCheckpointed(CompactStorageMode.Legacy);
            _output.WriteLine("file version=" + d[HeaderPage.P_FILE_VERSION]);
            var data = new ProbeStream(d, dataW);
            var log = new ProbeStream(new byte[0], logW);
            using (var db = new LiteDatabase(data, null, log))
            {
                var rows = db.GetCollection("shapes");
                _output.WriteLine("ro=" + RO(db));
                for (var i = 0; i < 4; i++)
                    _output.WriteLine("insert " + i + " " + Try(() => rows.Insert(Enumerable.Range(i * 10, 10).Select(x => new BsonDocument { ["_id"] = x, ["alpha"] = x, ["beta"] = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" + x, ["gamma"] = new BsonDocument { ["g"] = x, ["longFieldNameForCompaction"] = "value" } }))));
                _output.WriteLine("count " + Try(() => rows.Count()));
            }
            _output.WriteLine("data changed=" + !data.ToArray().SequenceEqual(d) + " log 0->" + log.Length);
            Dump("data", data);
            Dump("log", log);
            ReopenAfter("after", data.ToArray(), log.ToArray());
        }

        [Fact]
        public void W4c_migrated_5021_file_readonly_filestream_compactable_write()
        {
            using var file = new TempFile();
            File.WriteAllBytes(file.Filename, Entry("IndexMigration_5_0_21.zip", "plain.db"));
            using (var db = new LiteDatabase(file.Filename)) db.GetCollection("rows").Count(); // migrate once, writable
            var original = File.ReadAllBytes(file.Filename);
            _output.WriteLine("version after migration=" + original[HeaderPage.P_FILE_VERSION]);
            using (var stream = new FileStream(file.Filename, FileMode.Open, FileAccess.Read))
            using (var db = new LiteDatabase(stream))
            {
                var rows = db.GetCollection("rows");
                var sample = rows.FindAll().First();
                _output.WriteLine("ro=" + RO(db) + " sample=" + sample.ToString().Substring(0, Math.Min(120, sample.ToString().Length)));
                _output.WriteLine("small insert " + Try(() => rows.Insert(new BsonDocument { ["_id"] = 5000 })));
                _output.WriteLine("compactable inserts " + Try(() => rows.Insert(Enumerable.Range(6000, 10).Select(x => new BsonDocument { ["_id"] = x, ["alpha"] = x, ["beta"] = new string('b', 80), ["gamma"] = new BsonDocument { ["g"] = x, ["longFieldNameForCompaction"] = "value" } }))));
                _output.WriteLine("count " + Try(() => rows.Count()));
            }
            _output.WriteLine("file changed=" + !File.ReadAllBytes(file.Filename).SequenceEqual(original));
        }
}
}
