using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Engine
{
    public class TracingStream : MemoryStream
    {
        public static System.Collections.Generic.List<string> Trace = new System.Collections.Generic.List<string>();
        public override void SetLength(long value) { Trace.Add("SetLength(" + value + ") from " + Length + "\n" + Environment.StackTrace); base.SetLength(value); }
        public override void Write(byte[] b, int o, int c) { Trace.Add("Write(" + c + ") at " + Position + "\n" + Environment.StackTrace); base.Write(b, o, c); }
    }

    public class ZzReviewScratch_Tests
    {
        private readonly ITestOutputHelper _output;
        public ZzReviewScratch_Tests(ITestOutputHelper output) { _output = output; }

        private static byte[] Fixture(out byte[] log)
        {
            using var resource = typeof(ZzReviewScratch_Tests).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.IndexMigration_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using (var logEntry = zip.GetEntry("plain-log.db").Open())
            using (var logBytes = new MemoryStream()) { logEntry.CopyTo(logBytes); log = logBytes.ToArray(); }
            using var entry = zip.GetEntry("plain.db").Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }

        private static string H(byte[] b) { using var sha = SHA256.Create(); return Convert.ToBase64String(sha.ComputeHash(b)).Substring(0, 10) + "/" + b.Length; }

        internal static bool ReadOnlyLog; internal static bool TraceLog;
        private string Probe(byte[] data, byte[] log, bool explicitReadOnly)
        {
            var ds = new MemoryStream(data, writable: false);
            MemoryStream ls;
            if (ReadOnlyLog) ls = new MemoryStream((byte[])log.Clone(), writable: false); else { ls = new MemoryStream(); ls.Write(log, 0, log.Length); ls.Position = 0; }
            if (TraceLog) { var t = new TracingStream(); t.Write(log, 0, log.Length); t.Position = 0; TracingStream.Trace.Clear(); ls = t; }
            string result;
            try
            {
                if (explicitReadOnly)
                {
                    using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = ds, LogStream = ls, ReadOnly = true, LegacyIndexScan = true }));
                    result = Describe(db);
                }
                else
                {
                    using var db = new LiteDatabase(ds, null, ls);
                    result = Describe(db);
                }
            }
            catch (Exception ex) { result = "OPEN FAILED " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
            var after = ls.ToArray();
            var changed = !after.SequenceEqual(log);
            return result + (changed ? $" LOG CHANGED {H(log)} -> {H(after)}" : " log unchanged");
        }

        private static string Describe(LiteDatabase db)
        {
            var ro = db.GetCollection("$database").FindAll().Single()["readOnly"].AsBoolean;
            var n = db.GetCollection("rows").FindAll().Count();
            return $"readOnly={ro} rows={n}";
        }

        [Fact]
        public void Crash_images_of_5021_migration_over_readonly_data_stream()
        {
            var original = Fixture(out var originalLog);
            using var device = new IndexMigrationCrashDevice(original, originalLog);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = device.Data, LogStream = device.Log, TransactionPageLimit = 4, DurableCommits = true })))
            {
                db.GetCollection("rows").Count();
                device.Stage = "checkpoint";
                db.Checkpoint();
                device.Armed = false;
            }
            _output.WriteLine("baseline: " + Probe(original, originalLog, false));
            var stats = new Dictionary<string, int>();
            foreach (var image in device.Images)
            {
                var branch = Probe(image.Data, image.Log, false);
                var ro = Probe(image.Data, image.Log, true);
                var key = "BRANCH[" + branch.Replace(H(image.Log), "L") + "] vs RO[" + ro + "]";
                _output.WriteLine(image.Event + " data=" + image.Data.Length + " log=" + image.Log.Length + " => " + key);
                stats[key] = stats.TryGetValue(key, out var c) ? c + 1 : 1;
            }
            foreach (var kv in stats) _output.WriteLine(kv.Value + " x " + kv.Key);
        }

        [Fact]
        public void Crash_images_of_5021_migration_over_readonly_data_and_log_streams()
        {
            ReadOnlyLog = true;
            try { Crash_images_of_5021_migration_over_readonly_data_stream(); } finally { ReadOnlyLog = false; }
        }

        [Fact]
        public void Trace_log_changes()
        {
            var original = Fixture(out var originalLog);
            using var device = new IndexMigrationCrashDevice(original, originalLog);
            using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = device.Data, LogStream = device.Log, TransactionPageLimit = 4, DurableCommits = true })))
            {
                db.GetCollection("rows").Count();
                device.Stage = "checkpoint";
                db.Checkpoint();
                device.Armed = false;
            }
            foreach (var ev in new[] { "migration:log.torn-write/15", "migration:data.after-sync/32768", "migration:log.after-sync/24576" })
            {
                var parts = ev.Split('/');
                var image = device.Images.First(x => x.Event == parts[0] && x.Log.Length == int.Parse(parts[1]));
                TraceLog = true;
                try { _output.WriteLine("=== " + ev + " data v" + image.Data[HeaderPage.P_FILE_VERSION] + ": " + Probe(image.Data, image.Log, false)); }
                finally { TraceLog = false; }
                foreach (var t in TracingStream.Trace) _output.WriteLine(string.Join("\n", t.Split('\n').Where(x => x.Contains("LiteDB.Engine") || !x.StartsWith("   at")).Take(12)));
            }
        }

        [Fact]
        public void Trace_tail_repair()
        {
            var dir = "$SCRATCH/pr/";
            TraceLog = true;
            try { _output.WriteLine("=== tail: " + Probe(File.ReadAllBytes(dir + "q2.db"), File.ReadAllBytes(dir + "q2.log"), false)); }
            finally { TraceLog = false; }
            foreach (var t in TracingStream.Trace) _output.WriteLine(string.Join("\n", t.Split('\n').Where(x => x.Contains("LiteDB.Engine") || !x.StartsWith("   at")).Take(12)));
        }

        [Fact]
        public void Torn_promotion_header_over_readonly_data_stream()
        {
            byte[] d = null, l = null;
            PromotionPowerLossScenario.Run(null, false, "promotion-before-header-write", tornPrefix: 59, damage: true,
                inspectFiles: (dataBytes, logBytes) => { d = dataBytes; l = logBytes; });
            var ds = new MemoryStream(d, writable: false);
            var ls = new MemoryStream(); ls.Write(l, 0, l.Length); ls.Position = 0;
            try
            {
                using var db = new LiteDatabase(ds, null, ls);
                _output.WriteLine("branch LiteDatabase(Stream): " + db.GetCollection("rows").Count() + " readOnly=" + db.GetCollection("$database").FindAll().Single()["readOnly"]);
            }
            catch (Exception ex) { _output.WriteLine("branch LiteDatabase(Stream) OPEN FAILED " + ex.GetType().Name + ": " + ex.Message); }
            _output.WriteLine("log changed: " + !ls.ToArray().SequenceEqual(l));
            var ds2 = new MemoryStream(d, writable: false);
            var ls2 = new MemoryStream(); ls2.Write(l, 0, l.Length); ls2.Position = 0;
            try
            {
                using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = ds2, LogStream = ls2, ReadOnly = true, LegacyIndexScan = true }));
                _output.WriteLine("explicit ReadOnly: rows=" + db.GetCollection("rows").Count());
            }
            catch (Exception ex) { _output.WriteLine("explicit ReadOnly OPEN FAILED " + ex.GetType().Name + ": " + ex.Message); }
            _output.WriteLine("log changed (ro): " + !ls2.ToArray().SequenceEqual(l));
        }
    }
}
