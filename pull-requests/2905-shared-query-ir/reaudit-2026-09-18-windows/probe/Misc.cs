using System;
using System.Linq;
using System.Linq.Expressions;
using LiteDB;

internal static class Misc
{
    private static void Try(string label, Func<string> action)
    {
        try { Console.WriteLine($"{label}: OK {action()}"); }
        catch (Exception ex) { Console.WriteLine($"{label}: {ex.GetType().Name}: {ex.Message}"); }
    }

    public static int Run()
    {
        var mapper = new BsonMapper();
        Expression<Func<Customer, bool>> anyObject = x => x.Phones.Any(p => p.Prefix == 1);
        Try("message for Any(object predicate)", () => mapper.GetExpression(anyObject).Source);

        using var db = new LiteDatabase(":memory:");
        var col = db.GetCollection<Customer>("customers");
        col.Insert(new Customer
        {
            Id = 1, Name = "a",
            Phones = new System.Collections.Generic.List<Phone> { new Phone { Prefix = 40, Number = 5 }, new Phone { Prefix = 41, Number = 6 } }
        });

        // Index expressions whose nested selector contains a constant (bound as a parameter by LINQ).
        Try("EnsureIndex LINQ Where(const).Select", () => col.EnsureIndex(x => x.Phones.Where(p => p.Prefix == 40).Select(p => p.Number)).ToString());
        Try("EnsureIndex LINQ Select only", () => col.EnsureIndex(x => x.Phones.Select(p => p.Number)).ToString());
        Try("EnsureIndex LINQ ToLower", () => col.EnsureIndex(x => x.Name.ToLower()).ToString());
        Try("EnsureIndex text FILTER const", () => col.EnsureIndex("f1", "MAP(FILTER($.Phones[*]=>(@.Prefix=40))=>@.Number)").ToString());
        Try("index list", () => string.Join(" ; ", db.Execute("SELECT name, expression FROM $indexes WHERE collection = 'customers'").ToEnumerable().Select(d => d["name"].AsString + " => " + d["expression"].AsString)));

        var viaLinq = mapper.GetIndexExpression<Customer, object>(x => x.Phones.Where(p => p.Prefix == 40).Select(p => p.Number));
        Console.WriteLine($"GetIndexExpression: src={viaLinq.Source} immutable={viaLinq.IsImmutable} params={JsonSerializer.Serialize(viaLinq.Parameters)}");
        return 0;
    }
}
