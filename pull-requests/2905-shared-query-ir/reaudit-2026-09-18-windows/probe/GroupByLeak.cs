using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

// Does GROUP BY write its reserved "key" parameter into the document the CALLER passed in?
internal static class GroupByLeak
{
    private static LiteDatabase Seed()
    {
        var db = new LiteDatabase(":memory:");
        db.GetCollection("rows").InsertBulk(Enumerable.Range(1, 20).Select(i => new BsonDocument { ["_id"] = i, ["City"] = "City" + i % 3 }));
        return db;
    }

    private static string Rows(IBsonDataReader reader)
    {
        var rows = new List<string>();
        using (reader) { while (reader.Read()) rows.Add(JsonSerializer.Serialize(reader.Current)); }
        return string.Join(" ", rows);
    }

    private static void Scenario(string label, string sql, int repeats)
    {
        using (var db = Seed())
        {
            var parameters = new BsonDocument { ["min"] = 5 };
            var results = new List<string>();
            for (var i = 0; i < repeats; i++)
            {
                try { results.Add(Rows(db.Execute(sql, parameters))); }
                catch (Exception ex) { results.Add("THROW " + ex.GetType().Name + ": " + ex.Message); }
            }
            Console.WriteLine($"{label}");
            Console.WriteLine($"    sql: {sql}");
            Console.WriteLine($"    caller document afterwards: {JsonSerializer.Serialize(parameters)}  (leaked 'key': {parameters.ContainsKey("key")})");
            Console.WriteLine($"    all {repeats} runs identical: {results.Distinct().Count() == 1}");
            Console.WriteLine($"    first result: {results[0]}");
        }
    }

    public static int Run()
    {
        // Repeats > 2 so the PR's SQL cache is admitted and then HIT.
        Scenario("A root select", "SELECT $ FROM rows WHERE _id > @min GROUP BY City", 4);
        Scenario("B aggregate select", "SELECT { city: @key, n: COUNT(*) } FROM rows WHERE _id > @min GROUP BY City", 4);
        Scenario("C having", "SELECT { city: @key, n: COUNT(*) } FROM rows WHERE _id > @min GROUP BY City HAVING COUNT(*) > 1", 4);
        return 0;
    }
}
