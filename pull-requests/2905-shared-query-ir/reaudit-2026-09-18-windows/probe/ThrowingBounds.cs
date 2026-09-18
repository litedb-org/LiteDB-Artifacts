using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;

// Differential: predicates whose BOUND expression throws when evaluated. A full scan over an empty
// collection never evaluates the predicate, so the planner must not introduce an error there.
// Output is compared verbatim between builds; any difference is a behaviour change.
internal static class ThrowingBounds
{
    private static readonly string[] Predicates =
    {
        "Score > (1 % 0) AND Score < 3",
        "Score < 3 AND Score > (1 % 0)",
        "Score >= 1 AND Score <= (1 % 0)",
        "Score BETWEEN (1 % 0) AND 3",
        "Score BETWEEN 1 AND 3 AND Score > (1 % 0)",
        "Score IN [1, 2] AND Score > (1 % 0)",
        "Score = 1 AND Score > (1 % 0)",
        "Score != 1 AND Score > (1 % 0)",
        "Score > SUBSTRING('abc', 10) AND Score < 'z'",
        "(Score > (1 % 0) AND Score < 3) OR Score = 50",
        "Score = 50 OR (Score > (1 % 0) AND Score < 3)",
        "Score > 1 AND Score < 3 AND Name > (1 % 0)",
        "Score > 7 AND Score < 3 AND Name = SUBSTRING('abc', 10)",
        "Name = SUBSTRING('abc', 10) AND Score > 7 AND Score < 3",
    };

    private static string Evaluate(bool indexed, bool populated, string predicate)
    {
        using (var db = new LiteDatabase(":memory:"))
        {
            var rows = db.GetCollection("rows");
            if (populated)
            {
                rows.Insert(new BsonDocument { ["_id"] = 1, ["Score"] = 2, ["Name"] = "a" });
                rows.Insert(new BsonDocument { ["_id"] = 2, ["Score"] = 50, ["Name"] = "b" });
                rows.Insert(new BsonDocument { ["_id"] = 3, ["Name"] = "c" });
            }
            if (indexed)
            {
                rows.EnsureIndex("score", "Score");
                rows.EnsureIndex("name", "Name");
            }
            try
            {
                var ids = rows.Find(BsonExpression.Create(predicate)).Select(d => d["_id"].AsInt32).OrderBy(i => i);
                return "rows=[" + string.Join(",", ids) + "]";
            }
            catch (Exception ex)
            {
                return "THROW " + ex.GetType().Name;
            }
        }
    }

    public static int Run()
    {
        foreach (var predicate in Predicates)
        {
            Console.WriteLine(predicate);
            foreach (var indexed in new[] { false, true })
            {
                foreach (var populated in new[] { false, true })
                {
                    Console.WriteLine($"    indexed={indexed,-5} populated={populated,-5} -> {Evaluate(indexed, populated, predicate)}");
                }
            }
        }
        return 0;
    }
}
