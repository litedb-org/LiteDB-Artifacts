using LiteDB;
using LiteDB.Engine;

namespace SharedPinCallbackProof;
internal static class Fixture
{
    internal static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id * 10 };
    internal static SharedEngine Shared(string path, string? password, Func<string, BsonValue, BsonValue>? transform = null) =>
        new SharedEngine(new EngineSettings { Filename = path, Password = password, ReadTransform = transform ?? ((_, value) => value) });
    internal static void Seed(string path, string? password)
    {
        using var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password });
        db.GetCollection("rows").EnsureIndex("value"); db.GetCollection("rows").Insert(Enumerable.Range(1, 3).Select(Row));
        db.GetCollection("ordinary").EnsureIndex("value");
        db.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 42, ["value"] = "untouched" });
    }
    internal static void Verify(string path, string? password, int[] ids, bool ordinary = false)
    {
        for (var reopen = 0; reopen < 2; reopen++)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password });
            var rows = db.GetCollection("rows");
            if (!rows.FindAll().Select(row => row["_id"].AsInt32).OrderBy(id => id).SequenceEqual(ids) ||
                db.GetCollection("ordinary").Count() != (ordinary ? 1 : 0) || db.GetCollection("sentinel").FindById(42)?["value"] != "untouched")
                throw new Exception("Cold record/rollback/sentinel model failed.");
            foreach (var id in ids) Indexed(rows, id);
            if (ordinary) Indexed(db.GetCollection("ordinary"), 40);
            if (db.GetCollection("ordinary").FindById(99) != null || rows.FindById(99) != null)
                throw new Exception("Attempted nested write escaped refusal.");
        }
        Console.WriteLine("TWO_COLD_INDEXED_MODELS_VERIFIED " + path);
    }
    private static void Indexed(ILiteCollection<BsonDocument> rows, int id)
    {
        var query = rows.Query().Where(Query.EQ("value", id * 10));
        if (query.GetPlan()["index"]["name"] != "value" || !query.GetPlan()["index"]["mode"].AsString.StartsWith("INDEX SEEK", StringComparison.Ordinal) || query.Single()["_id"] != id)
            throw new Exception("Cold indexed model failed: " + id);
    }
}
