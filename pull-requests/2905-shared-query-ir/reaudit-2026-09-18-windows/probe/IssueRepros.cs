using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LiteDB;

// Minimal reproductions used for upstream issue reports.
internal static class IssueRepros
{
    public class Account { public int Id { get; set; } public decimal Balance { get; set; } public int Flags { get; set; } }

    private static string Rows(IBsonDataReader reader)
    {
        var rows = new List<string>();
        using (reader) { while (reader.Read()) rows.Add(JsonSerializer.Serialize(reader.Current)); }
        return rows.Count == 0 ? "(no rows)" : string.Join(" ", rows);
    }

    private static void Translate(string label, Expression<Func<Account, bool>> predicate)
    {
        try
        {
            var expr = BsonMapper.Global.GetExpression(predicate);
            Console.WriteLine($"  {label,-34} -> {expr.Source}   params={JsonSerializer.Serialize(expr.Parameters)}");
        }
        catch (Exception ex) { Console.WriteLine($"  {label,-34} -> THROW {ex.GetType().Name}"); }
    }

    public static int Run()
    {
        Console.WriteLine("== unary operators in LINQ");
        Translate("-x.Balance > 100m", x => -x.Balance > 100m);
        Translate("-(x.Balance) == -50m", x => -(x.Balance) == -50m);
        Translate("+x.Balance > 100m", x => +x.Balance > 100m);
        Translate("~x.Flags == 0", x => ~x.Flags == 0);
        Translate("-x.Flags < 0", x => -x.Flags < 0);
        Translate("(0 - x.Balance) > 100m  [workaround]", x => (0 - x.Balance) > 100m);
        using (var db = new LiteDatabase(":memory:"))
        {
            var col = db.GetCollection<Account>("accounts");
            col.Insert(new Account { Id = 1, Balance = 120.5m });
            col.Insert(new Account { Id = 2, Balance = -120.5m });
            var ids = col.Find(x => -x.Balance > 100m).Select(a => a.Id).ToArray();
            Console.WriteLine($"  Find(x => -x.Balance > 100m) returned ids [{string.Join(",", ids)}]; LINQ-to-objects gives [2]");
        }

        Console.WriteLine("== GROUP BY and the caller's parameter document");
        using (var db = new LiteDatabase(":memory:"))
        {
            db.GetCollection("rows").InsertBulk(Enumerable.Range(1, 20).Select(i => new BsonDocument { ["_id"] = i, ["City"] = "City" + i % 3 }));
            var parameters = new BsonDocument { ["min"] = 5 };
            Console.WriteLine("  before: " + JsonSerializer.Serialize(parameters));
            Console.WriteLine("  result: " + Rows(db.Execute("SELECT { city: @key, n: COUNT(*) } FROM rows WHERE _id > @min GROUP BY City", parameters)));
            Console.WriteLine("  after:  " + JsonSerializer.Serialize(parameters));

            // A caller whose own parameter happens to be called "key".
            var own = new BsonDocument { ["key"] = 5 };
            Console.WriteLine("  own 'key' before: " + JsonSerializer.Serialize(own));
            try { Console.WriteLine("  result: " + Rows(db.Execute("SELECT { city: @key, n: COUNT(*) } FROM rows WHERE _id > @key GROUP BY City", own))); }
            catch (Exception ex) { Console.WriteLine("  result: THROW " + ex.GetType().Name + ": " + ex.Message); }
            Console.WriteLine("  own 'key' after:  " + JsonSerializer.Serialize(own));
            try { Console.WriteLine("  reuse, SELECT $ WHERE _id > @key -> " + Rows(db.Execute("SELECT _id FROM rows WHERE _id > @key", own))); }
            catch (Exception ex) { Console.WriteLine("  reuse: THROW " + ex.GetType().Name + ": " + ex.Message); }
            Console.WriteLine("  expected for _id > 5: 15 rows (6..20)");
        }
        return 0;
    }
}
