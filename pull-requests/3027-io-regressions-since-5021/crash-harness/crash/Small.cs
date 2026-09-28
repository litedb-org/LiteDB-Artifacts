using System;
using System.IO;
using System.Linq;
using LiteDB;
public static class Small
{
    static void Try(string name, Func<string> a) { try { Console.WriteLine(name + ": OK " + a()); } catch (Exception ex) { Console.WriteLine(name + ": FAIL " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); } }
    public static int Run(string dir, string legacyDb)
    {
        Directory.CreateDirectory(dir);
        var f1 = Path.Combine(dir, "cs.db"); if (File.Exists(f1)) File.Delete(f1);
        Try("Cache Size=5000", () => { using var db = new LiteDatabase("Filename=" + f1 + ";Cache Size=5000"); db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 1 }); return ""; });
        Try("read-only FileStream of 5.0.21 file", () => { using var fs = new FileStream(legacyDb, FileMode.Open, FileAccess.Read); using var db = new LiteDatabase(fs); return "count=" + db.GetCollection("c").Count(); });
        var f3 = Path.Combine(dir, "torn.db"); File.WriteAllBytes(f3, new byte[4096]);
        Try("torn creation (4096 zero bytes)", () => { using var db = new LiteDatabase(f3); db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 1 }); return "count=" + db.GetCollection("c").Count(); });
        return 0;
    }
}
