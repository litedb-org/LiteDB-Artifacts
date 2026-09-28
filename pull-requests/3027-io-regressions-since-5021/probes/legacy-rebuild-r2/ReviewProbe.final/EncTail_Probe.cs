using System;
using System.IO;
using System.Linq;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class EncTail_Probe
    {
        private readonly ITestOutputHelper _out;
        public EncTail_Probe(ITestOutputHelper output) { _out = output; }
        const string Dir = "$SCRATCH/crash5021/";

        [Theory]
        [InlineData("plain", null)]
        [InlineData("enc", "secret")]
        public void Tail(string name, string password)
        {
            string Run(bool tail)
            {
                using var t = new TempFile();
                File.Copy(Dir + name + "-crash.db", t.Filename, true);
                File.Copy(Dir + name + "-crash-log.db", FileHelper.GetLogFile(t.Filename), true);
                if (tail)
                {
                    using (var s = new FileStream(t.Filename, FileMode.Append)) s.Write(Enumerable.Repeat((byte)0xAB, 100).ToArray());
                    using (var s = new FileStream(FileHelper.GetLogFile(t.Filename), FileMode.Append)) s.Write(Enumerable.Repeat((byte)0xCD, 100).ToArray());
                }
                var cs = $"Filename={t.Filename}" + (password == null ? "" : $";Password={password}");
                string r;
                try
                {
                    using (var db = new LiteDatabase(cs))
                    {
                        var docs = db.GetCollection("docs").FindAll().ToList();
                        r = $"count={docs.Count} updated={docs.Count(x => x["value"].AsInt32 == 7)} idx7={db.GetCollection("docs").Count(Query.EQ("value", 7))}";
                    }
                    using (var db = new LiteDatabase(cs)) r += $" reopen={db.GetCollection("docs").Count()} len%={new FileInfo(t.Filename).Length % 8192}";
                }
                catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message; }
                return r;
            }
            _out.WriteLine($"{name}: control {Run(false)} | tail {Run(true)}");
        }
    }
}
