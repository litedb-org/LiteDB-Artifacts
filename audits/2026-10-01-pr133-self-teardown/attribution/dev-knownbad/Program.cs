using LiteDB;
using LiteDB.Engine;
using System.Reflection;

var mode = args[0];
var encrypted = args[1] == "encrypted";
var dir = Path.Combine(Path.GetTempPath(), "litedb-teardown-attribution-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
var file = Path.Combine(dir, "data.db");
var password = encrypted ? "secret" : null;
Console.WriteLine($"FIXTURE {dir}");
BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id };
using (var seed = new LiteDatabase(new ConnectionString { Filename = file, Password = password }))
{
    seed.GetCollection("rows").Insert(Row(1));
    seed.GetCollection("rows").EnsureIndex("value");
    seed.GetCollection("sentinel").Insert(Row(42));
}
var data = new CallbackFile(file);
var log = new CallbackFile(Path.Combine(dir, "data-log.db"));
var outer = new SharedEngine(new EngineSettings { Filename = file, Password = password, DataStream = data, LogStream = log, ReadTransform = (_, v) => v });
var mutex = (Mutex)typeof(SharedEngine).GetField("_mutex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(outer)!;
bool Probe()
{
    var acquired = false;
    var t = new Thread(() => { acquired = mutex.WaitOne(0); if (acquired) mutex.ReleaseMutex(); });
    t.Start();
    if (!t.Join(5000)) throw new Exception("native probe did not complete");
    return acquired;
}
var callback = 0;
var reader = outer.Query("sentinel", new Query { ForUpdate = true });
if (Probe()) throw new Exception("setup failed to retain native ownership");
outer.Insert("rows", Enumerable.Range(100,60).Select(i => { var r = Row(i); r["payload"] = new string('p',4000); return r; }).ToArray(), BsonAutoId.Int32);
data.Arm(() =>
{
    callback++;
    Console.WriteLine("CALLBACK_ENTER native_acquirable=" + Probe());
    try
    {
        if (mode == "pragma") Console.WriteLine("PRAGMA_RETURN " + outer.Pragma("USER_VERSION").ToString());
        else if (mode == "dispose") { outer.Dispose(); Console.WriteLine("DISPOSE_RETURN"); }
    }
    catch (Exception ex) { Console.WriteLine("CALLBACK_EXCEPTION " + ex.GetType().FullName + " " + ex.Message); }
    Console.WriteLine("CALLBACK_AFTER native_acquirable=" + Probe());
});
Console.WriteLine("READER_DISPOSE_START");
reader.Dispose();
Console.WriteLine("READER_DISPOSE_RETURN callbacks=" + callback + " native_acquirable=" + Probe());
if (callback != 1) throw new Exception("callback not reached exactly once");
outer.Dispose(); data.Dispose(); log.Dispose();
using (var peer = new LiteDatabase(new ConnectionString { Filename = file, Password = password, Connection = ConnectionType.Shared })) peer.GetCollection("rows").Insert(Row(9));
for (var cold = 0; cold < 2; cold++)
{
    using var db = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
    var rows = db.GetCollection("rows");
    var expected = new[] { 1,9 }.Concat(Enumerable.Range(100,60)).ToArray();
    if (!rows.FindAll().Select(d=>d["_id"].AsInt32).OrderBy(i=>i).SequenceEqual(expected)) throw new Exception("cold rows mismatch");
    foreach (var id in expected)
    {
        var q = rows.Query().Where(Query.EQ("value", id));
        if(q.GetPlan()["index"]["name"].AsString != "value" || q.Single()["_id"].AsInt32 != id) throw new Exception("indexed lookup mismatch");
    }
    if(db.GetCollection("sentinel").FindById(42)["value"].AsInt32 != 42) throw new Exception("sentinel changed");
}
Console.WriteLine("COLD_INDEXED_STATE_VERIFIED");

sealed class CallbackFile : FileStream
{
    Action action;
    public CallbackFile(string path) : base(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete) { }
    public void Arm(Action value) => action = value;
    void Fire() => Interlocked.Exchange(ref action,null)?.Invoke();
    public override void Write(byte[] b,int o,int c) { Fire(); base.Write(b,o,c); }
    public override void Write(ReadOnlySpan<byte> b) { Fire(); base.Write(b); }
}
