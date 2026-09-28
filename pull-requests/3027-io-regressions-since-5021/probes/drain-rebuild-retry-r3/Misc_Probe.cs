using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LiteDB.Engine;
using LiteDB.Internals;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe3
{
    public class Misc_Probe
    {
        private readonly ITestOutputHelper _out;
        public Misc_Probe(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void Refused_drain_with_partial_tails_changes_nothing()
        {
            foreach (var lease in new Func<int[]>[] { () => new[] { 1 }, () => new[] { 22 }, () => new[] { 1000 }, () => null })
            foreach (var tails in new[] { (0, 0), (100, 0), (0, 100), (100, 100), (0, 4096) })
            {
                using var data = ChecksumTestFiles.Copy(TornTailSweep_Probe.Entry("WalCrash_5_0_21.zip", "crash.db"));
                using var log = ChecksumTestFiles.Copy(TornTailSweep_Probe.Entry("WalCrash_5_0_21.zip", "crash-log.db"));
                data.Seek(0, SeekOrigin.End); data.Write(Enumerable.Repeat((byte)0xAB, tails.Item1).ToArray(), 0, tails.Item1);
                log.Seek(0, SeekOrigin.End); log.Write(Enumerable.Repeat((byte)0xCD, tails.Item2).ToArray(), 0, tails.Item2);
                data.Position = 0; log.Position = 0;
                var od = data.ToArray(); var ol = log.ToArray();
                string result;
                try { new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, SharedReaderVersions = lease }).Dispose(); result = "opened"; }
                catch (LiteException ex) { result = "refused " + ex.ErrorCode; }
                var same = data.ToArray().SequenceEqual(od) && log.ToArray().SequenceEqual(ol);
                _out.WriteLine($"lease={(lease() == null ? "null" : lease()[0].ToString())} tails={tails}: {result}; files {(same ? "same" : "CHANGED")}");
                Assert.True(result == "opened" || same);
            }
        }

        /// <summary>Caller stream with damaged 5.0.21 data and AutoRebuild: what does the second open do?</summary>
        [Fact]
        public void Caller_stream_second_open()
        {
            var original = Damaged();
            using var data = ChecksumTestFiles.Copy(original);
            for (var open = 0; open < 3; open++)
            {
                string r;
                try { new LiteEngine(new EngineSettings { DataStream = data, AutoRebuild = true }).Dispose(); r = "opened"; }
                catch (Exception ex) { r = ex.GetType().Name + ": " + ex.Message; }
                _out.WriteLine($"open {open}: mark={data.ToArray()[HeaderPage.P_INVALID_DATAFILE_STATE]} -> {r}");
            }
            // and via LiteDatabase(Stream) without auto-rebuild, then read-only
            using var data2 = ChecksumTestFiles.Copy(original);
            try { new LiteDatabase(data2).Dispose(); } catch (Exception ex) { _out.WriteLine("LiteDatabase(stream): " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
            _out.WriteLine("mark after LiteDatabase(stream): " + data2.ToArray()[HeaderPage.P_INVALID_DATAFILE_STATE]);
            try { using var db = new LiteDatabase(data2); _out.WriteLine("second LiteDatabase(stream) count: " + db.GetCollection("c").Count()); }
            catch (Exception ex) { _out.WriteLine("second LiteDatabase(stream): " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
        }

        internal static byte[] Damaged()
        {
            using var resource = typeof(Misc_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.DamagedDocument_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.Entries.First(e => e.Name.EndsWith(".db") && !e.Name.Contains("-log")).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
namespace LiteDB.Tests.ReviewProbe3
{
    public class VecSalvage_Probe
    {
        private readonly Xunit.Abstractions.ITestOutputHelper _out;
        public VecSalvage_Probe(Xunit.Abstractions.ITestOutputHelper output) { _out = output; }

        [Xunit.Fact]
        public void Salvaged_document_whose_vector_key_throws()
        {
            var src = "$SCRATCH/r3a/vecthrow.db";
            foreach (var mark in new[] { true, false })
            {
                using var file = new TempFile();
                var bytes = System.IO.File.ReadAllBytes(src);
                if (mark) bytes[LiteDB.Engine.HeaderPage.P_INVALID_DATAFILE_STATE] = 1;
                System.IO.File.WriteAllBytes(file.Filename, bytes);
                try
                {
                    using var db = new LiteDatabase($"Filename={file.Filename};Auto-Rebuild=true");
                    var ids = string.Join(",", System.Linq.Enumerable.Select(db.GetCollection("vt").FindAll(), x => x["_id"].AsInt32));
                    var errs = string.Join(" | ", System.Linq.Enumerable.Select(db.GetCollection("_rebuild_errors").FindAll(), x => x["message"].AsString));
                    _out.WriteLine($"mark={mark}: ids={ids} errors={errs}");
                }
                catch (System.Exception ex) { _out.WriteLine($"mark={mark}: THROW {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"); }
                _out.WriteLine("  backup exists: " + System.IO.File.Exists(FileHelper.GetSuffixFile(file.Filename, "-backup", false)) + ", mark now " + System.IO.File.ReadAllBytes(file.Filename)[LiteDB.Engine.HeaderPage.P_INVALID_DATAFILE_STATE]);
            }
        }
    }
}
