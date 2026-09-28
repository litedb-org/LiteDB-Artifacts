using System.Security.Cryptography;
using LiteDB;

static string H(string p) => File.Exists(p) ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))[..12] + "/" + new FileInfo(p).Length : "missing";
static void T(string name, Func<object> f)
{
    try { var r = f(); Console.WriteLine($"  {name}: OK {r}"); }
    catch (Exception ex) { Console.WriteLine($"  {name}: THROW {ex.GetType().Name}: {ex.Message.Split('\n')[0]}"); }
}
var mode = args[0];
if (mode == "create")
{
    var dir = args[1];
    Directory.CreateDirectory(dir);
    using (var db = new LiteDatabase(Path.Combine(dir, "legacy.db")))
    {
        var c = db.GetCollection("col");
        for (int i = 0; i < 200; i++) c.Insert(new BsonDocument { ["_id"] = i, ["name"] = "n" + i });
        c.EnsureIndex("name");
    }
    // data+log pair with committed transactions left in the log
    var data = new FileStream(Path.Combine(dir, "ds.db"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
    var log = new FileStream(Path.Combine(dir, "ds.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
    var db2 = new LiteDatabase(data, null, log);
    var c2 = db2.GetCollection("col");
    c2.EnsureIndex("name");
    db2.Checkpoint();
    for (int i = 0; i < 20; i++) c2.Insert(new BsonDocument { ["_id"] = i, ["name"] = "n" + i });
    Thread.Sleep(1000);
    data.Flush(true); log.Flush(true);
    Console.WriteLine($"ds.db {H(Path.Combine(dir, "ds.db"))} ds.log {H(Path.Combine(dir, "ds.log"))}");
    Environment.Exit(0);
}
if (mode == "ops")
{
    var dataPath = args[1];
    var logPath = args.Length > 2 && args[2] != "-" ? args[2] : null;
    Console.WriteLine($"before data {H(dataPath)} log {(logPath == null ? "-" : H(logPath))}");
    var data = new FileStream(dataPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    var log = logPath == null ? null : new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
    LiteDatabase db = null;
    T("open", () => { db = new LiteDatabase(data, null, log); return ""; });
    if (db != null)
    {
        var c = db.GetCollection("col");
        T("count", () => c.Count());
        T("findByName", () => c.FindOne(Query.EQ("name", "n5"))?["_id"]);
        T("BeginTrans", () => db.BeginTrans());
        T("DeleteMany none", () => c.DeleteMany(Query.EQ("name", "missing")));
        T("Commit", () => db.Commit());
        T("BeginTrans2", () => db.BeginTrans());
        T("Rollback", () => db.Rollback());
        if (args.Contains("write"))
        {
            T("Insert", () => c.Insert(new BsonDocument { ["_id"] = 5000, ["name"] = "new" }));
            T("count after insert", () => c.Count());
            T("find new", () => c.FindById(5000)?["name"]);
        }
        if (args.Contains("bulk"))
        {
            T("InsertBulk 3000", () => c.InsertBulk(Enumerable.Range(10000, 3000).Select(i => new BsonDocument { ["_id"] = i, ["name"] = "b" + i, ["pad"] = new string((char)(97 + i % 26), 3000) })));
            T("count after bulk", () => c.Count());
        }
        if (args.Contains("checkpoint")) T("Checkpoint", () => { db.Checkpoint(); return ""; });
        T("Dispose", () => { db.Dispose(); return ""; });
    }
    data.Dispose(); log?.Dispose();
    Console.WriteLine($"after  data {H(dataPath)} log {(logPath == null ? "-" : H(logPath))}");
}
if (mode == "count")
{
    // open a copy writable to see persisted state
    var tmp = Path.GetTempFileName(); File.Copy(args[1], tmp, true);
    string tmpLog = null;
    if (args.Length > 2 && args[2] != "-") { tmpLog = Path.GetTempFileName(); File.Copy(args[2], tmpLog, true); }
    using var d = new FileStream(tmp, FileMode.Open, FileAccess.ReadWrite);
    using var l = tmpLog == null ? null : new FileStream(tmpLog, FileMode.Open, FileAccess.ReadWrite);
    using var db = new LiteDatabase(d, null, l);
    var c = db.GetCollection("col");
    Console.WriteLine($"  persisted count={c.Count()} has5000={c.FindById(5000) != null}");
}
if (mode == "createcur")
{
    using var db = new LiteDatabase(args[1]);
    var c = db.GetCollection("col");
    for (int i = 0; i < 200; i++) c.Insert(new BsonDocument { ["_id"] = i, ["name"] = "n" + i });
    c.EnsureIndex("name");
}
if (mode == "createpair")
{
    var dir = args[1];
    Directory.CreateDirectory(dir);
    var data = new FileStream(Path.Combine(dir, "p.db"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
    var log = new FileStream(Path.Combine(dir, "p.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
    var db2 = new LiteDatabase(data, null, log);
    var c2 = db2.GetCollection("col");
    c2.EnsureIndex("name");
    db2.Checkpoint();
    for (int i = 0; i < 20; i++) c2.Insert(new BsonDocument { ["_id"] = i, ["name"] = "n" + i });
    Thread.Sleep(500);
    data.Flush(true); log.Flush(true);
    Console.WriteLine($"p.db {H(Path.Combine(dir, "p.db"))} p.log {H(Path.Combine(dir, "p.log"))}");
    Environment.Exit(0);
}
if (mode == "readonlyflag")
{
    var data = new FileStream(args[1], FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
    var log = args.Length > 2 && args[2] != "-" ? new FileStream(args[2], FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite) : null;
    using var db = new LiteDatabase(data, null, log);
    var d = db.GetCollection("$database").FindAll().Single();
    Console.WriteLine($"  readOnly={d["readOnly"]} count={db.GetCollection("col").Count()}");
}
