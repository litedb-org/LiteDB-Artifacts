using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LiteDB;
public class Row { public int Id { get; set; } public string Name { get; set; } }
public static class Prog
{
    static void Try(string label, Func<object> f)
    {
        try { Console.WriteLine(label.PadRight(62) + " => " + f()); }
        catch (Exception ex) { Console.WriteLine(label.PadRight(62) + " THROWS " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
    }
    public static void Main(string[] args)
    {
        var dir = args[0]; Directory.CreateDirectory(dir);
        // (a) #2841: dispose twice, and using + explicit Dispose, on a file database
        var f1 = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".db");
        Try("(a) Dispose twice", () => { var db = new LiteDatabase(f1); db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 1 }); db.Dispose(); db.Dispose(); return "ok"; });
        Try("(a) using + explicit Dispose", () => { using (var db = new LiteDatabase(f1)) { db.Dispose(); } return "ok"; });
        Try("(a) UserVersion after Dispose", () => { var db = new LiteDatabase(f1); db.Dispose(); return db.UserVersion; });

        // (b) #2343 shape: 8192 bytes, byte0 = 1, salt at 1..16, zeros after
        var f2 = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".db");
        var bytes = new byte[8192]; bytes[0] = 1; new Random(7).NextBytes(new Span<byte>(bytes, 1, 16));
        File.WriteAllBytes(f2, bytes);
        Try("(b) open 8192-byte encrypted stub with password + insert", () => { using var db = new LiteDatabase($"Filename={f2};Password=abc"); db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 1 }); return "ok, count=" + db.GetCollection("c").Count(); });

        // (c) #1706: unbounded (no Limit) range, both operand orders, ascending and descending
        using (var db = new LiteDatabase(":memory:"))
        {
            var col = db.GetCollection<Row>("r");
            col.InsertBulk(Enumerable.Range(1, 300_000).Select(i => new Row { Id = i, Name = "n" + i }));
            int lo = 1000, hi = 1200;
            foreach (var (label, q) in new (string, Func<ILiteQueryable<Row>>)[] {
                ("(c) Id>=lo && Id<=hi  asc ", () => col.Query().Where(x => x.Id >= lo && x.Id <= hi)),
                ("(c) Id<=hi && Id>=lo  asc ", () => col.Query().Where(x => x.Id <= hi && x.Id >= lo)),
                ("(c) Id>=lo && Id<=hi  desc", () => col.Query().Where(x => x.Id >= lo && x.Id <= hi).OrderByDescending(x => x.Id)),
                ("(c) Id<=hi && Id>=lo  desc", () => col.Query().Where(x => x.Id <= hi && x.Id >= lo).OrderByDescending(x => x.Id)) })
            {
                q().ToList();
                var sw = Stopwatch.StartNew(); var n = 0;
                for (var i = 0; i < 5; i++) n = q().ToList().Count;
                Console.WriteLine($"{label}  rows={n}  {sw.Elapsed.TotalMilliseconds / 5,8:F2} ms   plan: {q().GetPlan()["index"]["expr"].AsString} {q().GetPlan()["index"]["mode"].AsString}");
            }
        }
    }
}
