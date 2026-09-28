using System;
using System.IO;
using System.Linq;
using LiteDB;

public static class DropIdx
{
    // make: v5 creates a db with indexes, drops one
    public static int Make(string path, string keep, string drop)
    {
        foreach (var f in new[] { path, path.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        using (var db = new LiteDatabase(path))
        {
            var col = db.GetCollection("customers");
            col.EnsureIndex("Name");
            col.EnsureIndex("Age");
            col.EnsureIndex("CustomerId");
            col.InsertBulk(Enumerable.Range(1, 200).Select(i => new BsonDocument { ["_id"] = i, ["Name"] = "n" + i, ["Age"] = i % 90, ["CustomerId"] = "C" + i }));
            foreach (var d in drop.Split(',')) Console.WriteLine("drop " + d + " -> " + col.DropIndex(d));
            Console.WriteLine("indexes: " + string.Join(",", db.GetCollection("$indexes").Find("collection = 'customers'").Select(x => x["name"].AsString)));
        }
        return 0;
    }

    public static int Use(string path)
    {
        try
        {
            using (var db = new LiteDatabase(path))
            {
                var col = db.GetCollection("customers");
                Console.WriteLine("open ok; count=" + col.Count() + " indexes: " + string.Join(",", db.GetCollection("$indexes").Find("collection = 'customers'").Select(x => x["name"].AsString)));
                col.Insert(new BsonDocument { ["_id"] = 1000, ["Name"] = "x", ["Age"] = 1, ["CustomerId"] = "C1000" });
                Console.WriteLine("insert ok");
                col.Update(new BsonDocument { ["_id"] = 1, ["Name"] = "y", ["Age"] = 2, ["CustomerId"] = "C1" });
                Console.WriteLine("update ok");
                col.Delete(2);
                Console.WriteLine("delete ok");
                col.EnsureIndex("Age");
                Console.WriteLine("ensureindex ok; count=" + col.Count() + " byCustomerId=" + col.Count(Query.EQ("CustomerId", "C5")));
            }
            using (var db = new LiteDatabase(path)) Console.WriteLine("reopen ok; count=" + db.GetCollection("customers").Count());
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message); }
        return 0;
    }
}
public static class DropIdxAfter
{
    public static int Run(string path, string opts)
    {
        try
        {
            using (var db = new LiteDatabase(CrashHarness.Conn(path, opts)))
            {
                var col = db.GetCollection("customers");
                Console.WriteLine("reopen: count=" + col.Count() + " ids 1..3: " + string.Join(",", col.Find(Query.LTE("_id", 3)).Select(x => x.ToString())));
                try { col.Insert(new BsonDocument { ["_id"] = 2000, ["Name"] = "z", ["Age"] = 3, ["CustomerId"] = "C2000" }); Console.WriteLine("insert ok"); }
                catch (Exception ex) { Console.WriteLine("insert FAIL " + ex.GetType().Name + ": " + ex.Message); }
            }
        }
        catch (Exception ex) { Console.WriteLine("reopen FAIL " + ex.GetType().Name + ": " + ex.Message); }
        return 0;
    }
}
public static class DropIdxFuzz
{
    // v5: create collection with given index expressions, drop some
    public static int Make(string path, string idx, string drop)
    {
        foreach (var f in new[] { path, path.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        using (var db = new LiteDatabase(path))
        {
            var col = db.GetCollection("items");
            var names = idx.Split(',');
            foreach (var n in names) col.EnsureIndex(n);
            col.InsertBulk(Enumerable.Range(1, 50).Select(i => { var d = new BsonDocument { ["_id"] = i }; foreach (var n in names) d[n] = n + i; return d; }));
            foreach (var d in drop.Split(',')) col.DropIndex(d);
        }
        return 0;
    }
    public static int Use(string path, string idx)
    {
        var stage = "open";
        try
        {
            using (var db = new LiteDatabase(path))
            {
                var col = db.GetCollection("items");
                stage = "count"; col.Count();
                stage = "insert"; var d = new BsonDocument { ["_id"] = 1000 }; foreach (var n in idx.Split(',')) d[n] = n + "x"; col.Insert(d);
                stage = "update"; d["_id"] = 1; col.Update(d);
                stage = "delete"; col.Delete(2);
                stage = "ensure"; col.EnsureIndex("zz");
            }
            stage = "reopen";
            using (var db = new LiteDatabase(path)) db.GetCollection("items").Count();
            Console.WriteLine("OK");
        }
        catch (Exception ex) { Console.WriteLine($"FAIL at {stage}: {ex.GetType().Name}: {ex.Message}"); }
        return 0;
    }
}
