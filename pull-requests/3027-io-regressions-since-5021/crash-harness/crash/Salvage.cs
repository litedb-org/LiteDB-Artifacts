using System;
using System.IO;
using System.Linq;
using System.Text;
using LiteDB;
public static class Salvage
{
    public static int Make(string path)
    {
        foreach (var f in new[] { path, path.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        using (var db = new LiteDatabase(path))
        {
            var col = db.GetCollection("c");
            for (var i = 1; i <= 3; i++) col.Insert(new BsonDocument { ["_id"] = i, ["a"] = "keep-" + i, ["b"] = "tail-" + i + "-" + new string('z', 20) });
        }
        // corrupt the int32 length of string field "b" of document 2
        var bytes = File.ReadAllBytes(path);
        var marker = Encoding.UTF8.GetBytes("tail-2-");
        for (var p = 0; p < bytes.Length - marker.Length; p++)
        {
            if (bytes.Skip(p).Take(marker.Length).SequenceEqual(marker))
            {
                // BSON string: type(1) name "b\0" int32 len, then chars
                BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, p - 4);
                Console.WriteLine("corrupted length at " + (p - 4));
                break;
            }
        }
        File.WriteAllBytes(path, bytes);
        return 0;
    }

    public static int Use(string path, string opts, bool rebuild)
    {
        try
        {
            using (var db = new LiteDatabase(CrashHarness.Conn(path, opts)))
            {
                if (rebuild) { db.Rebuild(); Console.WriteLine("rebuild done"); }
                foreach (var d in db.GetCollection("c").FindAll()) Console.WriteLine("  doc " + d.ToString());
                var errs = db.GetCollectionNames().Where(n => n.Contains("rebuild")).ToList();
                foreach (var e in errs) Console.WriteLine("  " + e + " count=" + db.GetCollection(e).Count());
            }
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
        return 0;
    }
}
