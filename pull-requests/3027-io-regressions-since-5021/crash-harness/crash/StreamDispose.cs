using System;
using System.IO;
using LiteDB;

public static class StreamDispose
{
    static void Try(string name, Action a)
    {
        try { a(); Console.WriteLine(name + ": OK"); }
        catch (Exception ex) { Console.WriteLine(name + ": FAIL " + ex.GetType().Name + ": " + ex.Message); }
    }

    public static int Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var f1 = Path.Combine(dir, "d1.db"); if (File.Exists(f1)) File.Delete(f1);
        Try("second dispose", () =>
        {
            using var s = new FileStream(f1, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            var db = new LiteDatabase(s);
            db.GetCollection("items").Insert(new BsonDocument { ["_id"] = 1 });
            db.Dispose();
            db.Dispose();
        });
        var f2 = Path.Combine(dir, "d2.db"); if (File.Exists(f2)) File.Delete(f2);
        Try("dispose with open tx", () =>
        {
            using (var s = new FileStream(f2, FileMode.OpenOrCreate, FileAccess.ReadWrite))
            {
                var db = new LiteDatabase(s);
                db.GetCollection("items").Insert(new BsonDocument { ["_id"] = 1 });
                db.BeginTrans();
                db.GetCollection("items").Insert(new BsonDocument { ["_id"] = 2 });
                db.Dispose();
            }
            using (var db = new LiteDatabase(f2))
            {
                var two = db.GetCollection("items").FindById(2);
                var one = db.GetCollection("items").FindById(1);
                if (two != null) throw new Exception("uncommitted row 2 visible");
                if (one == null) throw new Exception("row 1 missing");
            }
        });
        var f3 = Path.Combine(dir, "d3.db"); if (File.Exists(f3)) File.Delete(f3);
        Try("checkpoint pragma after abandoned stream db", () =>
        {
            using (var db = new LiteDatabase(f3)) db.GetCollection("items").Insert(new BsonDocument { ["_id"] = 1 });
            var s = new FileStream(f3, FileMode.Open, FileAccess.ReadWrite);
            var abandoned = new LiteDatabase(s);
            abandoned.GetCollection("items").Insert(new BsonDocument { ["_id"] = 2 });
            s.Dispose();
            GC.SuppressFinalize(abandoned);
            using (var db = new LiteDatabase(f3))
            {
                Console.WriteLine("   count=" + db.GetCollection("items").Count() + " checkpointSize=" + db.CheckpointSize);
                if (db.CheckpointSize != 1000) throw new Exception("CheckpointSize=" + db.CheckpointSize);
            }
        });
        return 0;
    }
}
