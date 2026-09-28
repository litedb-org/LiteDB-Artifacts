using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LiteDB.Engine;
using LiteDB.Tests.Engine;
using LiteDB.Internals;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class TailCrash_Probe
    {
        private readonly ITestOutputHelper _out;
        public TailCrash_Probe(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(100, 100)]
        [InlineData(0, 100)]
        [InlineData(100, 0)]
        [InlineData(0, 512)]
        [InlineData(0, 4096)]
        [InlineData(0, 16)]
        [InlineData(0, 8)]
        [InlineData(0, -512)]
        [InlineData(0, -4096)]
        [InlineData(1, -4096)]
        public void Every_image(int dataTail, int logTail)
        {
            var data = Entry("crash.db"); var log = Entry("crash-log.db");
            if (dataTail != 1) data = data.Concat(Enumerable.Repeat((byte)0xAB, dataTail)).ToArray();
            if (logTail >= 0) log = log.Concat(Enumerable.Repeat((byte)0xCD, logTail)).ToArray();
            else
            {
                // realistic torn append: prefix of a copy of the last WAL page (a confirming page) of a new transaction 22
                var last = log.Skip(log.Length - (dataTail == 1 ? 3 : 1) * 8192).Take(-logTail).ToArray();
                BitConverter.GetBytes(22u).CopyTo(last, 14);
                last[18] = 1;
                log = log.Concat(last).ToArray();
            }
            using var device = new IndexMigrationCrashDevice(data, log);
            string first;
            try
            {
                using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = device.Data, LogStream = device.Log })))
                    first = Check(db);
            }
            catch (Exception ex) { first = "THROW " + ex.GetType().Name + ": " + ex.Message; }
            device.Armed = false;
            var bad = 0; string sample = null;
            foreach (var image in device.Images)
            {
                string r;
                try
                {
                    using var d = ChecksumTestFiles.Copy(image.Data);
                    using var l = ChecksumTestFiles.Copy(image.Log);
                    using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d, LogStream = l }))) r = Check(db);
                    using (var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d, LogStream = l }))) r += "|" + Check(db);
                    if (r != "101/21|101/21") { using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d, LogStream = l, AutoRebuild = false })); }
                }
                catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message + (sample == null ? " STACK " + string.Join(" <- ", ex.StackTrace.Split('\n').Select(x => x.Trim()).Where(x => x.Contains("LiteDB")).Select(x => x.Substring(3, Math.Max(0, x.IndexOf('(') - 3))).Take(8)) : ""); }
                if (r != "101/21|101/21")
                {
                    var dir = "$SCRATCH/badimages/";
                    Directory.CreateDirectory(dir);
                    var stem = dir + $"d{dataTail}-l{logTail}-{bad}";
                    File.WriteAllBytes(stem + ".db", image.Data); File.WriteAllBytes(stem + "-log.db", image.Log);
                    File.WriteAllText(stem + ".txt", image.Event);
                    string ro;
                    try
                    {
                        using var d2 = ChecksumTestFiles.Copy(image.Data); using var l2 = ChecksumTestFiles.Copy(image.Log);
                        using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2, ReadOnly = true, LegacyIndexScan = true }));
                        ro = Check(db);
                    }
                    catch (Exception ex) { ro = "THROW " + ex.GetType().Name + ": " + ex.Message.Substring(0, Math.Min(80, ex.Message.Length)); }
                    string ar;
                    try
                    {
                        using var tf = new TempFile();
                        File.WriteAllBytes(tf.Filename, image.Data); File.WriteAllBytes(FileHelper.GetLogFile(tf.Filename), image.Log);
                        try { new LiteDatabase(tf.Filename).Dispose(); } catch (LiteException) { }
                        using var db = new LiteDatabase($"Filename={tf.Filename};Auto-Rebuild=true");
                        ar = Check(db) + " rebuild_errors=" + db.GetCollection("_rebuild_errors").Count();
                    }
                    catch (Exception ex) { ar = "THROW " + ex.GetType().Name + ": " + ex.Message.Substring(0, Math.Min(80, ex.Message.Length)); }
                    _out.WriteLine($"  bad image {stem} {image.Event}: dataLen={image.Data.Length} logLen={image.Log.Length} readonly={ro} autorebuild={ar}");
                }
                if (r != "101/21|101/21") { bad++; sample = (sample == null ? "" : sample + " ;; ") + image.Event + " => " + (sample == null ? r : r.Substring(0, Math.Min(90, r.Length))); }
            }
            _out.WriteLine($"dataTail={dataTail} logTail={logTail}: first={first} images={device.Images.Count} bad={bad} sample={sample}");
        }

        private static string Check(LiteDatabase db)
        {
            var docs = db.GetCollection("docs").FindAll().ToList();
            return $"{docs.Count}/{docs.Count(x => x["value"].AsInt32 == 7)}";
        }

        private static byte[] Entry(string name)
        {
            using var resource = typeof(TailCrash_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.WalCrash_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
