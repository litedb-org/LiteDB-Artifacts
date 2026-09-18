using System;
using LiteDB;
internal static class Extreme2
{
    public static int Run()
    {
        using (var db = new LiteDatabase(":memory:"))
        {
            var rows = db.GetCollection("rows");
            rows.Insert(new BsonDocument { ["_id"] = 3, ["Score"] = 50 });
            try { Console.WriteLine("count=" + rows.Count(BsonExpression.Create("Score > @p AND Score < 100", new BsonDocument { ["p"] = double.NaN }))); }
            catch (Exception ex) { Console.WriteLine(ex.ToString()); }
        }
        return 0;
    }
}
