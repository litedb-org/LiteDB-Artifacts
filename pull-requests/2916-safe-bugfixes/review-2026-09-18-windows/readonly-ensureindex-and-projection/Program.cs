using System;
using System.IO;
using System.Linq;
using LiteDB;

public class NameDto { public string Name { get; set; } public int Extra { get; set; } }
public class P { public int Id { get; set; } public string Name { get; set; } }
public static class Prog
{
    public static void Main(string[] args)
    {
        var file = Path.Combine(args[0], "ro-" + Guid.NewGuid().ToString("N") + ".db");
        // writer stays open (so a -log file exists) while a read-only reader opens: the realistic multi-process pattern
        using (var w = new LiteDatabase("Filename=" + file + ";Connection=shared"))
        {
            var c = w.GetCollection<P>("p");
            c.EnsureIndex(x => x.Name);
            c.Insert(new P { Id = 1, Name = "a" });
        }
        Console.WriteLine("log exists after close: " + File.Exists(file.Replace(".db", "-log.db")));
        foreach (var withLog in new[] { false, true })
        {
            var log = file.Replace(".db", "-log.db");
            if (withLog && !File.Exists(log)) File.WriteAllBytes(log, new byte[0]);
            try
            {
                using var r = new LiteDatabase("Filename=" + file + ";ReadOnly=true");
                var c = r.GetCollection<P>("p");
                try { Console.WriteLine($"[log={withLog}] EnsureIndex existing => " + c.EnsureIndex(x => x.Name)); }
                catch (Exception ex) { Console.WriteLine($"[log={withLog}] EnsureIndex existing THROWS {ex.GetType().Name}: {ex.Message}"); }
                try { Console.WriteLine($"[log={withLog}] count after => " + c.Count()); }
                catch (Exception ex) { Console.WriteLine($"[log={withLog}] count after THROWS {ex.GetType().Name}: {ex.Message}"); }
            }
            catch (Exception ex) { Console.WriteLine($"[log={withLog}] open THROWS {ex.GetType().Name}: {ex.Message}"); }
        }

        using var db = new LiteDatabase(":memory:");
        var col = db.GetCollection<P>("p");
        col.Insert(new P { Id = 1, Name = "n1" });
        try
        {
            var docs = col.Query().Select("$.Name").ToList();
            Console.WriteLine("projection doc: " + docs[0].ToString());
            var dto = BsonMapper.Global.ToObject<NameDto>(docs[0]);
            Console.WriteLine("ToObject<NameDto> => Name=" + dto.Name);
        }
        catch (Exception ex) { Console.WriteLine("ToObject<NameDto> THROWS " + ex.GetType().Name + ": " + ex.Message); }
    }
}
