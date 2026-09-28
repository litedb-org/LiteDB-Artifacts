using LiteDB;
var dir = args[0]; var mode = args[1];
var path = Path.Combine(dir, "d.db");
string Conn(string extra = "") => $"Filename={path};Connection=direct" + extra;
void Seed(string extra = "")
{
    using var db = new LiteDatabase(Conn(extra));
    db.GetCollection("s").InsertBulk(Enumerable.Range(0, 300).Select(i => new BsonDocument { ["_id"] = i, ["v"] = new string('s', 500) }));
}
void CrashAfter(LiteDatabase db)
{
    db.GetCollection("n").Insert(new BsonDocument { ["_id"] = 1, ["v"] = "after" });
    db.GetCollection("s").Insert(new BsonDocument { ["_id"] = 1000, ["v"] = "after" });
    Thread.Sleep(1500);
    Environment.FailFast("crash " + mode);
}
switch (mode)
{
    case "pragma":
    {
        Seed();
        var db = new LiteDatabase(Conn());
        db.UserVersion = 7;
        db.Pragma("UTC_DATE", true);
        db.Pragma("CHECKPOINT", 0);
        db.Pragma("TIMEOUT", 90);
        CrashAfter(db);
        break;
    }
    case "rebuild":
    {
        Seed();
        var db = new LiteDatabase(Conn());
        db.GetCollection("s").DeleteMany(Query.LT("_id", 150));
        db.Rebuild();
        CrashAfter(db);
        break;
    }
    case "rebuild-collation":
    {
        Seed();
        var db = new LiteDatabase(Conn());
        db.Rebuild(new LiteDB.Engine.RebuildOptions { Collation = new Collation("pt-BR/IgnoreCase") });
        CrashAfter(db);
        break;
    }
    case "encrypted-rebuild":
    {
        Seed(";Password=pw");
        var db = new LiteDatabase(Conn(";Password=pw"));
        db.Rebuild(new LiteDB.Engine.RebuildOptions { Password = "pw" });
        CrashAfter(db);
        break;
    }
    case "upgrade":
    {
        File.Copy(args[2], path, true);
        var db = new LiteDatabase(Conn(";Upgrade=true"));
        Console.WriteLine("upgraded collections: " + string.Join(",", db.GetCollectionNames()));
        CrashAfter(db);
        break;
    }
    case "checkpoint0":
    {
        Seed();
        var db = new LiteDatabase(Conn());
        db.Pragma("CHECKPOINT", 0);
        for (int i = 0; i < 50; i++) db.GetCollection("c" + (i % 5)).Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('c', 3000) });
        db.DropCollection("c1");
        db.GetCollection("s").EnsureIndex("v");
        db.GetCollection("s").DropIndex("v");
        db.DropCollection("c2");
        CrashAfter(db);
        break;
    }
    case "verify":
    {
        using var db = new LiteDatabase(Conn(args.Length > 2 ? args[2] : ""));
        Console.WriteLine("5.0.21 " + string.Join(" ", db.GetCollectionNames().OrderBy(x => x).Select(c => c + "=" + db.GetCollection(c).Count())) + " userVersion=" + db.UserVersion);
        break;
    }
}
