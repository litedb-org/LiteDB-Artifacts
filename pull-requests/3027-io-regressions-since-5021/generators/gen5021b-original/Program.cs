using LiteDB;
var dir = args[0];
Directory.CreateDirectory(dir);
foreach (var x in Directory.GetFiles(dir)) File.Delete(x);

void Crash(string name, string f)
{
    File.Copy(f, Path.Combine(dir, name + "-crash.db"), true);
    var log = Path.Combine(Path.GetDirectoryName(f), Path.GetFileNameWithoutExtension(f) + "-log.db");
    if (File.Exists(log)) File.Copy(log, Path.Combine(dir, name + "-crash-log.db"), true);
    Console.WriteLine($"{name}: data {new FileInfo(f).Length / 8192} pages, log {(File.Exists(log) ? new FileInfo(log).Length / 8192 : 0)} pages");
}

string Big(int n) => new string('b', n);

foreach (var enc in new[] { false, true })
{
    var p = enc ? ";Password=secret" : "";
    var e = enc ? "enc-" : "";

    // (a) InitialSize: data file preallocated, LastPageID small, WAL allocates many pages
    {
        var f = Path.Combine(dir, e + "initial.db");
        using var db = new LiteDatabase($"Filename={f}" + (enc ? p : ";Initial Size=4MB"));
        db.Pragma("CHECKPOINT", 0);
        var col = db.GetCollection("docs");
        for (var i = 0; i < 200; i++) col.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(3000) });
        Crash(e + "initial", f);
    }
    // (b) WAL far larger than the data file (fresh file, no checkpoint)
    {
        var f = Path.Combine(dir, e + "bigwal.db");
        using var db = new LiteDatabase($"Filename={f}" + p);
        db.Pragma("CHECKPOINT", 0);
        var col = db.GetCollection("docs");
        col.EnsureIndex("k");
        for (var i = 0; i < 400; i++) col.Insert(new BsonDocument { ["_id"] = i, ["k"] = i, ["pad"] = Big(6000) });
        Crash(e + "bigwal", f);
    }
    // (c) pages allocated, freed (drop / delete), reused; big documents spanning many pages
    {
        var f = Path.Combine(dir, e + "churn.db");
        using var db = new LiteDatabase($"Filename={f}" + p);
        db.Pragma("CHECKPOINT", 0);
        var a = db.GetCollection("a");
        for (var i = 0; i < 100; i++) a.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(200000) });
        db.DropCollection("a");
        var b = db.GetCollection("b");
        for (var i = 0; i < 50; i++) b.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(100000) });
        b.DeleteMany(x => true);
        for (var i = 0; i < 50; i++) b.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(1000) });
        Crash(e + "churn", f);
    }
    // (d) rolled-back transaction after safepoints (new pages returned to the free list)
    {
        var f = Path.Combine(dir, e + "rollback.db");
        using var db = new LiteDatabase($"Filename={f}" + p);
        db.Pragma("CHECKPOINT", 0);
        var col = db.GetCollection("docs");
        col.Insert(new BsonDocument { ["_id"] = -1 });
        db.BeginTrans();
        for (var i = 0; i < 3000; i++) col.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(4000) });
        db.Rollback();
        for (var i = 0; i < 20; i++) col.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(100) });
        Crash(e + "rollback", f);
    }
    // (e) checkpointed data, then a WAL that allocates beyond it, crash image after partial rollback
    {
        var f = Path.Combine(dir, e + "grown.db");
        using var db = new LiteDatabase($"Filename={f}" + (enc ? p : ";Initial Size=1MB"));
        var col = db.GetCollection("docs");
        for (var i = 0; i < 100; i++) col.Insert(new BsonDocument { ["_id"] = i, ["pad"] = Big(5000) });
        db.Checkpoint();
        db.Pragma("CHECKPOINT", 0);
        col.DeleteMany(x => true);
        for (var i = 0; i < 300; i++) col.Insert(new BsonDocument { ["_id"] = 1000 + i, ["pad"] = Big(9000) });
        Crash(e + "grown", f);
    }
}
