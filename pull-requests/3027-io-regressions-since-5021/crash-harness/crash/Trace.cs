using System;
using System.Linq;
using LiteDB;
public static class Trace
{
    public static int Run(string path, string opts)
    {
        var stage = "open";
        try
        {
            using (var db = new LiteDatabase(CrashHarness.Conn(path, opts)))
            {
                stage = "count"; Console.WriteLine("count=" + db.GetCollection("c").Count());
                stage = "findall"; var all = db.GetCollection("c").FindAll().ToList(); Console.WriteLine("findall=" + all.Count + " updated=" + all.Count(x => x["s"] == "upd") + " maxId=" + all.Max(x => x["_id"].AsInt32));
                stage = "index"; Console.WriteLine("viaIndex=" + db.GetCollection("c").Find(Query.All("v")).Count());
            }
            stage = "reopen";
            using (var db = new LiteDatabase(CrashHarness.Conn(path, opts))) Console.WriteLine("reopen count=" + db.GetCollection("c").Count());
        }
        catch (Exception ex) { Console.WriteLine("FAILED at " + stage + ": " + ex.ToString().Split('\n').Take(25).Aggregate((a, b) => a + "\n" + b)); }
        return 0;
    }
}
