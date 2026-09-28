using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LiteDB;

public static class DiskFull
{
    static void Fill(string path, long leaveBytes)
    {
        using (var f = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            var buf = new byte[65536];
            try { while (true) f.Write(buf, 0, buf.Length); } catch (IOException) { }
            try { f.Flush(); } catch (IOException) { }
            f.SetLength(Math.Max(0, f.Length - leaveBytes));
        }
    }

    static BsonDocument Doc(int i) => new BsonDocument { ["_id"] = i, ["v"] = i % 101, ["pad"] = new string((char)('a' + i % 26), 3000 + i % 2000) };

    static string Verify(string db, string opts, int lastAck, int maxPossible)
    {
        var problems = new List<string>();
        try
        {
            using (var d = new LiteDatabase(CrashHarness.Conn(db, opts)))
            {
                var col = d.GetCollection("c");
                var all = col.FindAll().ToDictionary(x => x["_id"].AsInt32);
                for (var i = 0; i <= lastAck; i++)
                {
                    if (!all.TryGetValue(i, out var doc)) { problems.Add("missing acked " + i); if (problems.Count > 5) break; continue; }
                    if (doc["pad"].AsString != Doc(i)["pad"].AsString) problems.Add("corrupt " + i);
                }
                var extra = all.Keys.Where(k => k > maxPossible).ToList();
                if (extra.Count > 0) problems.Add("unexpected ids " + string.Join(",", extra.Take(5)));
                var viaIdx = col.Find(Query.All("v")).Count();
                if (viaIdx != all.Count) problems.Add($"index count {viaIdx} != {all.Count}");
                // db must be writable again
                col.Upsert(Doc(10_000_000));
                return $"count={all.Count} lastAck={lastAck} " + (problems.Count == 0 ? "OK" : "PROBLEMS: " + string.Join("; ", problems.Take(8)));
            }
        }
        catch (Exception ex) { return "REOPEN FAILED " + ex.GetType().Name + ": " + ex.Message; }
    }

    public static int Run(string dir, string mode, string opts, long leave)
    {
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        var db = Path.Combine(dir, "t.db");
        var filler = Path.Combine(dir, "filler.bin");
        var lastAck = -1;
        var attempted = -1;
        LiteDatabase d = null;
        try
        {
            d = new LiteDatabase(CrashHarness.Conn(db, opts));
            var col = d.GetCollection("c");
            col.EnsureIndex("v");
            if (mode == "insert")
            {
                Fill(filler, leave);
                for (var i = 0; ; i++) { attempted = i; col.Insert(Doc(i)); lastAck = i; }
            }
            else if (mode == "tx")
            {
                Fill(filler, leave);
                for (var b = 0; ; b++)
                {
                    d.BeginTrans();
                    for (var i = b * 50; i < b * 50 + 50; i++) { attempted = i; col.Insert(Doc(i)); }
                    d.Commit();
                    lastAck = b * 50 + 49;
                }
            }
            else if (mode == "checkpoint")
            {
                d.Pragma("CHECKPOINT", 0);
                for (var i = 0; i < 800; i++) { attempted = i; col.Insert(Doc(i)); lastAck = i; }
                Fill(filler, leave);
                Console.WriteLine("filled; log=" + new FileInfo(db.Replace(".db", "-log.db")).Length + " data=" + new FileInfo(db).Length);
                d.Checkpoint();
                Console.WriteLine("checkpoint succeeded?!");
                for (var i = 800; ; i++) { attempted = i; col.Insert(Doc(i)); lastAck = i; }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"failure after lastAck={lastAck} attempted={attempted}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
        }
        // try continue using the same instance a bit (5.0.21 vs head behaviour)
        try { d?.GetCollection("c").Insert(Doc(9_000_000)); Console.WriteLine("post-failure insert succeeded (!)"); lastAck = lastAck; }
        catch (Exception ex) { Console.WriteLine("post-failure insert: " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
        try { d?.Dispose(); Console.WriteLine("dispose ok"); } catch (Exception ex) { Console.WriteLine("dispose: " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
        File.Delete(filler);
        var files = string.Join(" ", Directory.GetFiles(dir).Select(f => Path.GetFileName(f) + "=" + new FileInfo(f).Length));
        Console.WriteLine("files: " + files);
        var r = Verify(db, opts, lastAck, Math.Max(attempted, 9_000_000));
        Console.WriteLine("verify: " + r);
        var r2 = Verify(db, opts, lastAck, 10_000_000);
        Console.WriteLine("verify2: " + r2);
        return r.Contains("OK") ? 0 : 1;
    }
}
