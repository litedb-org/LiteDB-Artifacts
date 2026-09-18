using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;

public static class Prog
{
    public static void Main(string[] args)
    {
        var dir = args[0]; var holdReader = args[1] == "hold"; var seconds = 8;
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, Guid.NewGuid().ToString("N") + ".db");
        using var db = new LiteDatabase("Filename=" + file);
        var w = db.GetCollection("w"); var r = db.GetCollection("r");
        w.InsertBulk(Enumerable.Range(1, 2000).Select(i => new BsonDocument { ["_id"] = i, ["v"] = 0, ["pad"] = new string('x', 2000) }));
        r.InsertBulk(Enumerable.Range(1, 2000).Select(i => new BsonDocument { ["_id"] = i, ["v"] = i }));
        db.Checkpoint();
        long writes = 0, reads = 0; var stop = false;
        var threads = new[]
        {
            new Thread(() => { var i = 0; while (!stop) { i = i % 2000 + 1; w.Update(new BsonDocument { ["_id"] = i, ["v"] = i, ["pad"] = new string('y', 2000) }); Interlocked.Increment(ref writes); } }),
            new Thread(() => { var i = 0; while (!stop) { i = i % 2000 + 1; r.FindById(i); Interlocked.Increment(ref reads); } }),
            new Thread(() => { var i = 0; while (!stop) { i = i % 2000 + 1; r.FindById(i); Interlocked.Increment(ref reads); } }),
            new Thread(() => { if (!holdReader) return; foreach (var d in r.FindAll()) { if (stop) break; Thread.Sleep(5); } }),
        };
        foreach (var t in threads) t.Start();
        long lw = 0, lr = 0;
        for (var s = 1; s <= seconds; s++)
        {
            Thread.Sleep(1000);
            var cw = Interlocked.Read(ref writes); var cr = Interlocked.Read(ref reads);
            var log = new FileInfo(file.Replace(".db", "-log.db"));
            Console.WriteLine($"  s{s}: writes/s {cw - lw,7} reads/s {cr - lr,8} log {(log.Exists ? log.Length / 1024 / 1024 : 0)} MB");
            lw = cw; lr = cr;
        }
        stop = true;
        foreach (var t in threads) if (!t.Join(20000)) Console.WriteLine("  THREAD HANG");
    }
}
