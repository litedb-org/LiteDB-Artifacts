using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class Drain_Probe
    {
        private readonly ITestOutputHelper _out;
        public Drain_Probe(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void Lease_versions()
        {
            foreach (var version in new[] { 1, 2, 3, 5, 10, 20, 22, 25, 50, 1000 })
            foreach (var partial in new[] { false, true })
            {
                using var data = Entry("crash.db");
                using var log = Entry("crash-log.db");
                if (partial)
                {
                    data.Seek(0, SeekOrigin.End); data.Write(Enumerable.Repeat((byte)0xAB, 100).ToArray(), 0, 100);
                    log.Seek(0, SeekOrigin.End); log.Write(Enumerable.Repeat((byte)0xCD, 100).ToArray(), 0, 100);
                }
                var od = data.ToArray(); var ol = log.ToArray();
                var v = version;
                string result;
                try { new LiteEngine(new EngineSettings { DataStream = data, LogStream = log, SharedReaderVersions = () => new[] { v } }).Dispose(); result = "opened"; }
                catch (LiteException ex) { result = "refused " + ex.ErrorCode; }
                var nd = data.ToArray(); var nl = log.ToArray();
                _out.WriteLine($"lease={version} partial={partial}: {result}; data {(nd.SequenceEqual(od) ? "same" : $"CHANGED {od.Length}->{nd.Length}")}, log {(nl.SequenceEqual(ol) ? "same" : $"CHANGED {ol.Length}->{nl.Length}")}");
            }
        }

        [Fact]
        public void Success_with_garbage_tail()
        {
            using var data = Entry("crash.db");
            using var log = Entry("crash-log.db");
            var origLen = data.Length;
            data.Seek(0, SeekOrigin.End); data.Write(Enumerable.Repeat((byte)0xAB, 100).ToArray(), 0, 100);
            log.Seek(0, SeekOrigin.End); log.Write(Enumerable.Repeat((byte)0xCD, 100).ToArray(), 0, 100);
            using (var engine = new LiteEngine(new EngineSettings { DataStream = data, LogStream = log }))
            using (var db = new LiteDatabase(engine, disposeOnClose: false))
            {
                var docs = db.GetCollection("docs").FindAll().ToList();
                _out.WriteLine($"count={docs.Count} updated={docs.Count(x => x["value"].AsInt32 == 7)}");
            }
            var bytes = data.ToArray();
            var abAt = Enumerable.Range(0, bytes.Length - 4).Where(i => bytes[i] == 0xAB && bytes[i+1] == 0xAB && bytes[i+2] == 0xAB && bytes[i+3] == 0xAB).Take(3).ToArray();
            _out.WriteLine($"origLen={origLen} newLen={bytes.Length} mod={bytes.Length % 8192} garbageAt={string.Join(",", abAt)} logLen={log.Length}");
        }

        private static MemoryStream Entry(string name)
        {
            using var resource = typeof(Drain_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.WalCrash_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            bytes.Position = 0;
            return bytes;
        }
    }
}
