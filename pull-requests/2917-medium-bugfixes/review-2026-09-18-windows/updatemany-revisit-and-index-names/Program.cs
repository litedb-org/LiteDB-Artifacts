using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

public static class Prog
{
    public static void Main(string[] args)
    {
        var mode = args[0];
        if (mode == "hw")
        {
            using var db = new LiteDatabase(":memory:");
            var c = db.GetCollection("c");
            c.EnsureIndex("k");
            c.InsertBulk(Enumerable.Range(1, 5000).Select(i => new BsonDocument { ["_id"] = i, ["k"] = i % 100, ["n"] = 0 }));
            var updated = c.UpdateMany("{k:$.k+1, n:$.n+1}", "$.k BETWEEN 10 AND 20");
            Console.WriteLine($"UpdateMany k+1 WHERE k BETWEEN 10 AND 20: returned {updated} (expect 550), docs updated more than once: {c.Count("$.n > 1")}, SUM(k)={c.FindAll().Sum(x => x["k"].AsInt32)} (expect 248050)");
            var t = Task.Run(() => c.UpdateMany("{k:$.k+1000}", "$.k >= 0"));
            Console.WriteLine(t.Wait(20000) ? $"UpdateMany k+1000 WHERE k>=0: returned {t.Result} (expect 5000)" : "UpdateMany k+1000 WHERE k>=0: STILL RUNNING after 20 s (hang)");
            Environment.Exit(0);
        }
        if (mode == "idx-create" || mode == "idx-ensure")
        {
            using var db = new LiteDatabase("Filename=" + args[1]);
            var c = db.GetCollection("c");
            if (mode == "idx-create") c.Insert(new BsonDocument { ["_id"] = 1, ["Größe"] = 1, ["naïve"] = 2 });
            Console.WriteLine($"EnsureIndex($.Größe) => {c.EnsureIndex("$.Größe")}, EnsureIndex($.naïve) => {c.EnsureIndex("$.naïve")}");
            Console.WriteLine("indexes: " + string.Join(", ", db.GetCollection("$indexes").Query().Where("collection = 'c'").ToList().Select(x => x["name"].AsString + "=" + x["expression"].AsString)));
        }
        if (mode == "rb")
        {
            using var db = new LiteDatabase(":memory:");
            var c = db.GetCollection("c"); var other = db.GetCollection("other");
            c.Insert(new BsonDocument { ["_id"] = 1 });
            var ready = new ManualResetEventSlim(); var done = new ManualResetEventSlim();
            var a = new Thread(() => { db.BeginTrans(); other.Insert(new BsonDocument { ["_id"] = 1 }); ready.Set(); done.Wait(); db.Commit(); });
            a.Start(); ready.Wait();
            try
            {
                try { db.BeginTrans(); c.Insert(new BsonDocument { ["_id"] = 1 }); db.Commit(); }
                catch { db.Rollback(); throw; }
            }
            catch (Exception ex) { Console.WriteLine("caller sees: " + ex.GetType().Name + ": " + ex.Message); }
            done.Set(); a.Join();
        }
    }
}
