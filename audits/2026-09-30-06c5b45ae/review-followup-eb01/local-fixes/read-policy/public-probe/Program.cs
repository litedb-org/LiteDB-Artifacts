using System;
using System.IO;
using LiteDB;
using LiteDB.Engine;
foreach (var initial in new[] { false, true })
foreach (var replacement in new[] { false, true })
{
    var file = Path.Combine(Path.GetTempPath(), "litedb-policy-" + Guid.NewGuid() + ".db");
    var calls = 0;
    var changedCalls = 0;
    var policy = "first";
    var settings = new EngineSettings { Filename = file,
        ReadTransform = initial ? (Func<string, BsonValue, BsonValue>)((_, value) => { calls++; value.AsDocument["policy"] = policy; return value; }) : null };
    using (var shared = new SharedEngine(settings))
    using (var db = new LiteDatabase(shared, disposeOnClose:false))
    {
        using (var warm = db.BeginTransaction()) { warm.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1, ["policy"] = "stored" }); warm.Commit(); }
        settings.ReadTransform = replacement ? (Func<string, BsonValue, BsonValue>)((_, value) => { changedCalls++; value.AsDocument["policy"] = "replacement"; return value; }) : null;
        policy = "second";
        var ordinary = db.GetCollection("rows").FindById(1)["policy"].AsString;
        using var tx = db.BeginTransaction();
        var reused = tx.GetCollection("rows").FindById(1)["policy"].AsString;
        tx.Commit();
        if (ordinary != (initial ? "second" : "stored") || reused != ordinary || changedCalls != 0 || calls != (initial ? 2 : 0)) throw new Exception("policy mismatch");
        Console.WriteLine($"initial={initial}, replacement={replacement}: ordinary={ordinary}, handle={reused}, new-callback-calls={changedCalls}");
    }
    using(var cold = new LiteDatabase(file)) if(cold.GetCollection("rows").FindById(1)["policy"].AsString != "stored") throw new Exception("stored mutation");
    foreach(var path in Directory.GetFiles(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)+"*")) File.Delete(path);
}
