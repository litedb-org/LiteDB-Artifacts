using System;
using System.IO;
using LiteDB;

public static class SmallCrash
{
    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "src.db");
        foreach (var f in new[] { path, path.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        var db = new LiteDatabase(path);
        var col = db.GetCollection("docs");
        col.EnsureIndex("value");
        for (var i = 0; i < 100; i++) col.Insert(new BsonDocument { ["_id"] = i, ["value"] = 0 });
        db.Checkpoint();
        for (var i = 0; i < 20; i++) col.Update(new BsonDocument { ["_id"] = i, ["value"] = 7 });
        col.Insert(new BsonDocument { ["_id"] = 100, ["value"] = 7 });
        File.Copy(path, Path.Combine(dir, "crash.db"), true);
        File.Copy(path.Replace(".db", "-log.db"), Path.Combine(dir, "crash-log.db"), true);
        Console.WriteLine("data " + new FileInfo(Path.Combine(dir, "crash.db")).Length + " log " + new FileInfo(Path.Combine(dir, "crash-log.db")).Length);
        Environment.Exit(0);
        return 0;
    }
}
