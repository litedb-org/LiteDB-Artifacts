using System;
using System.Diagnostics;
using System.Linq;
using LiteDB;

public class P { public int Id { get; set; } public string Name { get; set; } public string Opt { get; set; } }
public static class Prog
{
    public static void Main()
    {
        using var db = new LiteDatabase(":memory:");
        Console.WriteLine("collation: " + db.Collation);
        var col = db.GetCollection<P>("p");
        col.EnsureIndex(x => x.Name);
        col.InsertBulk(Enumerable.Range(1, 200_000).Select(i => new P { Id = i, Name = "name" + i.ToString("D6"), Opt = i % 3 == 0 ? null : "X" + i }));

        var plan = col.Query().Where(x => x.Name.StartsWith("name00001")).GetPlan();
        Console.WriteLine("StartsWith plan: " + plan["index"]["mode"].AsString + " | expr " + plan["index"]["expr"].AsString);
        col.Query().Where(x => x.Name.StartsWith("name00001")).Count();
        var sw = Stopwatch.StartNew();
        var n = 0;
        for (var i = 0; i < 20; i++) n = col.Query().Where(x => x.Name.StartsWith("name00001")).Count();
        Console.WriteLine($"StartsWith: {n} rows, {sw.Elapsed.TotalMilliseconds / 20:F2} ms/query");
        sw.Restart();
        for (var i = 0; i < 20; i++) n = db.Execute("SELECT COUNT(*) FROM p WHERE Name LIKE 'name00001%'").ToList().Count;
        Console.WriteLine($"SQL LIKE 'name00001%': {sw.Elapsed.TotalMilliseconds / 20:F2} ms/query");

        try { Console.WriteLine("Opt.Equals(v, OrdinalIgnoreCase) => " + col.Count(x => x.Opt.Equals("x1", StringComparison.OrdinalIgnoreCase))); }
        catch (Exception ex) { Console.WriteLine("Opt.Equals(v, OrdinalIgnoreCase) THROWS " + ex.GetType().Name + ": " + ex.Message); }
        try { Console.WriteLine("Opt.StartsWith(v, Ordinal) => " + col.Count(x => x.Opt.StartsWith("X1", StringComparison.Ordinal))); }
        catch (Exception ex) { Console.WriteLine("Opt.StartsWith(v, Ordinal) THROWS " + ex.GetType().Name + ": " + ex.Message); }
        var p2 = col.Query().Where(x => x.Name.Equals("name000010", StringComparison.OrdinalIgnoreCase)).GetPlan();
        Console.WriteLine("Name.Equals(v, OrdinalIgnoreCase) plan: " + p2["index"]["mode"].AsString);
    }
}
