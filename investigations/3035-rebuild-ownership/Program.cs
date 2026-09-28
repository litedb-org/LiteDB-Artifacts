using System.Reflection;
using LiteDB;
using LiteDB.Engine;
var filename = args[0];
using (var seed = new LiteDatabase(filename))
{
    seed.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1, ["value"] = "original" });
    seed.Pragma(Pragmas.CHECKPOINT, 0);
}
var hook = typeof(LiteDatabase).Assembly.GetType("LiteDB.Engine.RebuildService")!.GetField("SimulateInstallFailure", BindingFlags.Static | BindingFlags.NonPublic)!;
var acknowledged = false;
var reached = false;
Action<string> interleave = phase =>
{
    if (phase != "before-recovery-marker") return;
    reached = true;
    try
    {
        using var other = new LiteDatabase(filename);
        other.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 99, ["value"] = "acknowledged" });
        acknowledged = true;
    }
    catch (IOException) { Console.WriteLine("CONTENDER DENIED"); }
};
hook.SetValue(null, interleave);
try { using var owner = new LiteDatabase(filename); owner.Rebuild(); }
finally { hook.SetValue(null, null); }
using var reopened = new LiteDatabase(filename);
var present = reopened.GetCollection("rows").FindById(99) != null;
Console.WriteLine($"REACHED={reached} ACKNOWLEDGED={acknowledged} PRESENT_AFTER_REBUILD={present}");
if (!reached) return 3;
return acknowledged && !present ? 2 : 0;
