using System;
using System.Linq;
using LiteDB;

// Reviewer claim: combining an extreme Double bound with a differently typed numeric bound on the
// same field throws OverflowException at PLAN time, even when no row would ever be compared.
internal static class ExtremeBounds
{
    private static readonly bool PrintStack = Environment.GetEnvironmentVariable("PROBE_STACK") == "1";

    private static void Try(string label, Func<string> action)
    {
        try
        {
            Console.WriteLine($"  {label}: {action()}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  {label}: THROW {ex.GetType().Name}: {ex.Message}");
            if (!PrintStack) return;
            var frames = ex.StackTrace.Split((char)10)
                .Where(line => line.Contains("LiteDB."))
                .Take(9)
                .Select(line => "        " + line.Trim().Split(new[] { " in " }, StringSplitOptions.None)[0]);
            foreach (var frame in frames) Console.WriteLine(frame);
        }
    }

    public static int Run()
    {
        var extremes = new (string Name, double Value)[] { ("NaN", double.NaN), ("+Infinity", double.PositiveInfinity), ("double.MaxValue", double.MaxValue), ("1e20 (control)", 1e20) };
        foreach (var indexed in new[] { true, false })
        {
            foreach (var populated in new[] { false, true })
            {
                Console.WriteLine($"indexed={indexed} populated={populated}");
                foreach (var (name, value) in extremes)
                {
                    using (var db = new LiteDatabase(":memory:"))
                    {
                        var rows = db.GetCollection("rows");
                        if (populated)
                        {
                            rows.Insert(new BsonDocument { ["_id"] = 1, ["Score"] = "not-a-number" });
                            rows.Insert(new BsonDocument { ["_id"] = 2 });
                            rows.Insert(new BsonDocument { ["_id"] = 3, ["Score"] = 50 });
                        }
                        if (indexed) rows.EnsureIndex("score", "Score");
                        Try($"Score > {name} AND Score < 100", () => "count=" + rows.Count(BsonExpression.Create("Score > @p AND Score < 100", new BsonDocument { ["p"] = value })));
                    }
                }
            }
        }
        return 0;
    }
}
