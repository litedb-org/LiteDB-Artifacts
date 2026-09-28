using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using LiteDB;

public static class Scen
{
    static string Sha(string path) => File.Exists(path) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).Substring(0, 12) + "/" + new FileInfo(path).Length : "missing";

    public static int Run(string[] a)
    {
        var name = a[1];
        switch (name)
        {
            // create a db with some data; optionally leave a WAL behind by copying files while db is open
            case "create":
            {
                var path = a[2];
                var conn = a.Length > 3 && a[3] != "-" ? a[3] : "";
                var n = a.Length > 4 ? int.Parse(a[4]) : 1000;
                using (var db = new LiteDatabase("Filename=" + path + (conn == "" ? "" : ";" + conn)))
                {
                    var col = db.GetCollection("c");
                    col.EnsureIndex("v");
                    for (var i = 0; i < n; i++) col.Insert(new BsonDocument { ["_id"] = i, ["v"] = i % 97, ["s"] = new string('x', i % 500) });
                }
                Console.WriteLine("created " + Sha(path));
                return 0;
            }
            // create db, commit data, then snapshot files (db + -log) while engine open -> crash image with WAL
            case "crashimage":
            {
                var path = a[2];
                var outDir = a[3];
                var conn = a.Length > 4 && a[4] != "-" ? ";" + a[4] : "";
                Directory.CreateDirectory(outDir);
                var db = new LiteDatabase("Filename=" + path + conn);
                var col = db.GetCollection("c");
                col.EnsureIndex("v");
                for (var i = 0; i < 1000; i++) col.Insert(new BsonDocument { ["_id"] = i, ["v"] = i % 97, ["s"] = new string('x', i % 500) });
                db.Checkpoint();
                for (var i = 1000; i < 1500; i++) col.Insert(new BsonDocument { ["_id"] = i, ["v"] = i % 97, ["s"] = new string('y', i % 500) });
                for (var i = 0; i < 100; i++) col.Update(new BsonDocument { ["_id"] = i, ["v"] = 1000 + i, ["s"] = "upd" });
                col.DeleteMany("_id >= 1400");
                var log = path.Replace(".db", "-log.db");
                File.Copy(path, Path.Combine(outDir, "t.db"), true);
                if (File.Exists(log)) File.Copy(log, Path.Combine(outDir, "t-log.db"), true);
                Console.WriteLine("image data " + Sha(Path.Combine(outDir, "t.db")) + " log " + Sha(Path.Combine(outDir, "t-log.db")));
                Environment.Exit(0); // do not dispose
                return 0;
            }
            case "verify":
            {
                var path = a[2];
                var conn = a.Length > 3 && a[3] != "-" ? ";" + a[3] : "";
                var log = path.Replace(".db", "-log.db");
                Console.WriteLine("before data " + Sha(path) + " log " + Sha(log));
                try
                {
                    using (var db = new LiteDatabase("Filename=" + path + conn))
                    {
                        var col = db.GetCollection("c");
                        var all = col.FindAll().ToList();
                        var viaIdx = col.Find(Query.All("v")).Count();
                        var upd = all.Count(x => x["s"] == "upd");
                        Console.WriteLine($"count={all.Count} viaIndex={viaIdx} updated={upd} maxId={(all.Count > 0 ? all.Max(x => x["_id"].AsInt32) : -1)}");
                    }
                }
                catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message); }
                Console.WriteLine("after  data " + Sha(path) + " log " + Sha(log));
                return 0;
            }
            case "memstream":
            {
                // typical in-memory round trip through a byte[]
                byte[] bytes;
                using (var ms = new MemoryStream())
                {
                    using (var db = new LiteDatabase(ms))
                    {
                        var col = db.GetCollection("c");
                        for (var i = 0; i < 2000; i++) col.Insert(new BsonDocument { ["_id"] = i, ["s"] = new string('x', 300) });
                    }
                    bytes = ms.ToArray();
                    Console.WriteLine("stream after dispose: canRead=" + ms.CanRead + " len=" + bytes.Length);
                }
                using (var ms2 = new MemoryStream())
                {
                    ms2.Write(bytes, 0, bytes.Length);
                    ms2.Position = 0;
                    using (var db = new LiteDatabase(ms2))
                    {
                        Console.WriteLine("reopen count=" + db.GetCollection("c").Count());
                    }
                }
                return 0;
            }
            case "memstream-reuse":
            {
                // reuse the same MemoryStream for a second LiteDatabase (not disposed by engine in 5.0.21)
                var ms = new MemoryStream();
                using (var db = new LiteDatabase(ms))
                {
                    db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 1 });
                }
                try
                {
                    using (var db = new LiteDatabase(ms))
                    {
                        Console.WriteLine("second open count=" + db.GetCollection("c").Count());
                        db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 2 });
                    }
                    using (var db = new LiteDatabase(ms)) Console.WriteLine("third open count=" + db.GetCollection("c").Count());
                }
                catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message); }
                return 0;
            }
            case "userstreams":
            {
                // user-supplied data + log streams
                var data = new MemoryStream();
                var log = new MemoryStream();
                using (var db = new LiteDatabase(data, null, log))
                {
                    var col = db.GetCollection("c");
                    for (var i = 0; i < 500; i++) col.Insert(new BsonDocument { ["_id"] = i });
                    Console.WriteLine($"open: data={data.Length} log={log.Length}");
                }
                Console.WriteLine($"closed: data canRead={data.CanRead} log canRead={log.CanRead}");
                try { Console.WriteLine($"closed: data={data.Length} log={log.Length}"); } catch (Exception ex) { Console.WriteLine("len fail " + ex.GetType().Name); }
                return 0;
            }
            case "readonlyfile":
            {
                var path = a[2];
                File.SetAttributes(path, FileAttributes.ReadOnly);
                try
                {
                    using (var db = new LiteDatabase("Filename=" + path + ";ReadOnly=true"))
                        Console.WriteLine("ro count=" + db.GetCollection("c").Count());
                }
                catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message); }
                finally { File.SetAttributes(path, FileAttributes.Normal); }
                return 0;
            }
        }
        return 3;
    }
}
public static class Perf
{
    public static int Run(string path, string opts, int n)
    {
        foreach (var f in new[] { path, path.Replace(".db", "-log.db") }) if (System.IO.File.Exists(f)) System.IO.File.Delete(f);
        using (var db = new LiteDB.LiteDatabase(CrashHarness.Conn(path, opts)))
        {
            var col = db.GetCollection("c");
            col.Insert(new LiteDB.BsonDocument { ["_id"] = -1 });
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < n; i++) col.Insert(new LiteDB.BsonDocument { ["_id"] = i, ["s"] = new string('x', 200) });
            System.Console.WriteLine($"{n} single inserts: {sw.ElapsedMilliseconds} ms ({n * 1000.0 / System.Math.Max(1, sw.ElapsedMilliseconds):F0}/s)");
        }
        return 0;
    }
}
