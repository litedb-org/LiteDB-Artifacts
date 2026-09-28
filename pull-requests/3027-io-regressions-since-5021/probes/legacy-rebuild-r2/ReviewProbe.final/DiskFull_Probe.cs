using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class DiskFull_Probe
    {
        private readonly ITestOutputHelper _out;
        public DiskFull_Probe(ITestOutputHelper output) { _out = output; }

        private sealed class FailingStream : MemoryStream
        {
            public bool Fail = true;
            public int Writes;
            public FailingStream(byte[] b) { base.Write(b, 0, b.Length); Position = 0; }
            public override void Write(byte[] buffer, int offset, int count)
            {
                Writes++;
                if (Fail) throw new IOException("There is not enough space on the disk.");
                base.Write(buffer, offset, count);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-4096)]
        [InlineData(-512)]
        public void Data_write_fails_during_drain(int logTail)
        {
            var data = Entry("crash.db"); var log = Entry("crash-log.db");
            if (logTail < 0)
            {
                var last = log.Skip(log.Length - 3 * 8192).Take(-logTail).ToArray(); // prefix of a data page, new transaction 22, confirming
                BitConverter.GetBytes(22u).CopyTo(last, 14);
                last[18] = 1;
                log = log.Concat(last).ToArray();
            }
            var d = new FailingStream(data);
            var l = new MemoryStream(); l.Write(log, 0, log.Length); l.Position = 0;
            string first;
            try { using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d, LogStream = l })); first = Check(db); }
            catch (Exception ex) { first = "THROW " + ex.GetType().Name + ": " + ex.Message.Substring(0, Math.Min(70, ex.Message.Length)); }
            var d2 = new MemoryStream(); var dataNow = d.ToArray(); d2.Write(dataNow, 0, dataNow.Length); d2.Position = 0;
            var l2 = new MemoryStream(); var logNow = l.ToArray(); l2.Write(logNow, 0, logNow.Length); l2.Position = 0;
            string second;
            try { using var db = new LiteDatabase(new LiteEngine(new EngineSettings { DataStream = d2, LogStream = l2 })); second = Check(db); }
            catch (Exception ex) { second = "THROW " + ex.GetType().Name + ": " + ex.Message.Substring(0, Math.Min(120, ex.Message.Length)); }
            _out.WriteLine($"logTail={logTail}: first open {first}; log {log.Length}->{logNow.Length}; reopen after disk space freed: {second}");
        }

        private static string Check(LiteDatabase db)
        {
            var docs = db.GetCollection("docs").FindAll().ToList();
            return $"{docs.Count}/{docs.Count(x => x["value"].AsInt32 == 7)}";
        }

        private static byte[] Entry(string name)
        {
            using var resource = typeof(DiskFull_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.WalCrash_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
