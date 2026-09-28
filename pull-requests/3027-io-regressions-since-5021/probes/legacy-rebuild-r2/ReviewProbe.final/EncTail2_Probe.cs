using System;
using System.IO;
using System.Linq;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class EncTail2_Probe
    {
        private readonly ITestOutputHelper _out;
        public EncTail2_Probe(ITestOutputHelper output) { _out = output; }
        const string Dir = "$SCRATCH/crash5021/";

        [Theory]
        [InlineData(true, false, 100)]
        [InlineData(false, true, 100)]
        [InlineData(true, true, 100)]
        [InlineData(true, false, 16)]
        [InlineData(true, false, 4096)]
        [InlineData(false, true, 512)]
        [InlineData(false, true, 4096)]
        [InlineData(false, true, 16)]
        public void Tail(bool dataTail, bool logTail, int bytes)
        {
            var keep = Dir + $"after-{dataTail}-{logTail}-{bytes}.db";
            using var t = new TempFile();
            File.Copy(Dir + "enc-crash.db", t.Filename, true);
            File.Copy(Dir + "enc-crash-log.db", FileHelper.GetLogFile(t.Filename), true);
            if (dataTail) using (var s = new FileStream(t.Filename, FileMode.Append)) s.Write(Enumerable.Repeat((byte)0xAB, bytes).ToArray());
            if (logTail) using (var s = new FileStream(FileHelper.GetLogFile(t.Filename), FileMode.Append)) s.Write(Enumerable.Repeat((byte)0xCD, bytes).ToArray());
            var before = (File.ReadAllBytes(t.Filename), File.ReadAllBytes(FileHelper.GetLogFile(t.Filename)));
            var cs = $"Filename={t.Filename};Password=secret";
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                string r;
                try
                {
                    using var db = new LiteDatabase(cs);
                    var docs = db.GetCollection("docs").FindAll().ToList();
                    r = $"count={docs.Count} updated={docs.Count(x => x["value"].AsInt32 == 7)}";
                }
                catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0] + (attempt == 1 ? " STACK " + string.Join(" <- ", ex.StackTrace.Split('\n').Select(x => x.Trim()).Where(x => x.Contains("LiteDB.Engine")).Select(x => x.Substring(3, x.IndexOf('(') - 3)).Take(14)) : ""); }
                var logPath = FileHelper.GetLogFile(t.Filename);
                var dataNow = File.ReadAllBytes(t.Filename);
                var logNow = File.Exists(logPath) ? File.ReadAllBytes(logPath) : Array.Empty<byte>();
                _out.WriteLine($"data={dataTail} log={logTail} bytes={bytes} attempt {attempt}: {r}; data {before.Item1.Length}->{dataNow.Length} {(dataNow.SequenceEqual(before.Item1) ? "same" : "changed")} ver={dataNow[0x3B]:X2}, log {before.Item2.Length}->{logNow.Length} {(logNow.SequenceEqual(before.Item2) ? "same" : "changed")}");
                if (attempt == 1) { File.Copy(t.Filename, keep, true); if (File.Exists(logPath)) File.Copy(logPath, FileHelper.GetLogFile(keep), true); }
            }
        }
    }
}
