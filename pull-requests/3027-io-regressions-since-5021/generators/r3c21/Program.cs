using LiteDB;
using System.Text;

public class ProbeStream : MemoryStream
{
    public readonly bool Writable;
    public readonly List<string> Calls = new List<string>();
    public ProbeStream(byte[] bytes, bool writable) { Writable = true; base.Write(bytes, 0, bytes.Length); base.Position = 0; Writable = writable; }
    public override bool CanWrite => Writable;
    void Record(string w) => Calls.Add(w + " <- " + string.Join(" <- ", Environment.StackTrace.Split('\n').Where(x => x.Contains("LiteDB.Engine")).Select(x => x.Trim().Replace("at LiteDB.Engine.", "").Split('(')[0]).Take(3)));
    public override void Write(byte[] b, int o, int c) { Record("Write(" + c + ")@" + Position); if (!Writable) throw new NotSupportedException("probe"); base.Write(b, o, c); }
    public override void WriteByte(byte v) { Record("WriteByte@" + Position); if (!Writable) throw new NotSupportedException("probe"); base.WriteByte(v); }
    public override void SetLength(long v) { Record("SetLength(" + v + ") from " + Length); if (!Writable) throw new NotSupportedException("probe"); base.SetLength(v); }
}

public static class P
{
    static string Try(Func<object> f) { try { return "ok " + f(); } catch (Exception ex) { return "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; } }
    public static void Main()
    {
        Console.WriteLine("5.0.21 Filename ReadOnly=true BeginTrans: " + Try(() => { using var db = new LiteDatabase("Filename=ro21.db;ReadOnly=true"); var b = db.BeginTrans(); var c = db.Commit(); return b + "/" + c; }));
        var d = File.ReadAllBytes("fx/plain.db");
        var l = File.ReadAllBytes("fx/plain-log.db");
        Console.WriteLine("file version byte=" + d[59] + " data=" + d.Length + " log=" + l.Length);
        // damaged legacy data page footer
        var dd = (byte[])d.Clone(); var damaged = 0;
        for (var pos = 8192; pos + 8192 <= dd.Length; pos += 8192)
            if (dd[pos + 4] == 4) { dd[pos + 8192 - 4] = 0xF0; dd[pos + 8192 - 3] = 0xFF; damaged++; }
        Console.WriteLine("damaged=" + damaged);
        foreach (var (dw, lw) in new[] { (false, true), (false, false), (true, false) })
        {
            var de = new ProbeStream(d, dw); var le = new ProbeStream(l, lw);
            Console.WriteLine($"5.0.21 LiteEngine(EngineSettings) data{(dw ? "W" : "R")}/log{(lw ? "W" : "R")}: " + Try(() => { using var db = new LiteDatabase(new LiteDB.Engine.LiteEngine(new LiteDB.Engine.EngineSettings { DataStream = de, LogStream = le })); return db.GetCollection("rows").Count(); }) + " changed data=" + !de.ToArray().SequenceEqual(d) + " log=" + !le.ToArray().SequenceEqual(l) + " calls=" + (de.Calls.Count + le.Calls.Count));
        }
        {
            var ed = File.ReadAllBytes("fx/encrypted.db"); var el = File.ReadAllBytes("fx/encrypted-log.db");
            var de = new ProbeStream(ed, false); var le = new ProbeStream(el, true);
            Console.WriteLine("5.0.21 encrypted EngineSettings dataR/logW: " + Try(() => { using var db = new LiteDatabase(new LiteDB.Engine.LiteEngine(new LiteDB.Engine.EngineSettings { DataStream = de, LogStream = le, Password = "migration-power-loss" })); return db.GetCollection("rows").Count(); }) + " changed data=" + !de.ToArray().SequenceEqual(ed) + " log=" + !le.ToArray().SequenceEqual(el));
            var de2 = new ProbeStream(ed, false);
            Console.WriteLine("5.0.21 encrypted EngineSettings dataR/log-: " + Try(() => { using var db = new LiteDatabase(new LiteDB.Engine.LiteEngine(new LiteDB.Engine.EngineSettings { DataStream = de2, Password = "migration-power-loss" })); return db.GetCollection("rows").Count(); }) + " changed data=" + !de2.ToArray().SequenceEqual(ed));
        }
        foreach (var (dw, lw) in new[] { (false, true), (false, false) })
        {
            var data0 = new ProbeStream(d, dw); var log0 = new ProbeStream(l, lw);
            using (var db = new LiteDatabase(data0, null, log0))
            {
                Console.WriteLine($"5.0.21 noop ops data{(dw ? "W" : "R")}/log{(lw ? "W" : "R")}: rebuild=" + Try(() => db.Rebuild()) + " checkpoint=" + Try(() => { db.Checkpoint(); return 0; }) + " dropMissing=" + Try(() => db.DropCollection("missing")) + " deleteNone=" + Try(() => db.GetCollection("rows").DeleteMany("1=0")));
            }
            Console.WriteLine("   changed data=" + !data0.ToArray().SequenceEqual(d) + " log=" + !log0.ToArray().SequenceEqual(l) + " calls=" + (data0.Calls.Count + log0.Calls.Count));
        }
        foreach (var (dw, lw) in new[] { (true, false), (false, true), (false, false) })
        {
            foreach (var (label, bytes) in new[] { ("clean", d), ("damaged", dd) })
            {
                var data = new ProbeStream(bytes, dw); var log = new ProbeStream(l, lw);
                string r;
                try
                {
                    using var db = new LiteDatabase(data, null, log);
                    r = "count=" + Try(() => db.GetCollection("rows").FindAll().Count()) + " begin=" + Try(() => db.BeginTrans()) + " commit=" + Try(() => db.Commit());
                }
                catch (Exception ex) { r = "OPEN/DISPOSE THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
                Console.WriteLine($"5.0.21 data{(dw ? "W" : "R")}/log{(lw ? "W" : "R")} {label}: {r} dataChanged={!data.ToArray().SequenceEqual(bytes)} logChanged={!log.ToArray().SequenceEqual(l)}");
                foreach (var c in data.Calls.Take(4)) Console.WriteLine("   D:" + c);
                foreach (var c in log.Calls.Take(4)) Console.WriteLine("   L:" + c);
            }
        }
    }
}
