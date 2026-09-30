using System;
using System.Collections.Generic;
using System.Linq;

namespace LiteDB.ConcurrencyTesting
{
    /// <summary>Independent acknowledged-state model; no implementation snapshot assumptions.</summary>
    internal sealed class ExplorerDatabase
    {
        internal readonly ConnectionString Connection;
        private readonly Dictionary<string, Dictionary<int, int>> _committed = new Dictionary<string, Dictionary<int, int>>();
        internal ExplorerDatabase(string path, bool shared, bool encrypted)
        {
            Connection = new ConnectionString { Filename = path, Password = encrypted ? "explorer-password" : null,
                Connection = shared ? ConnectionType.Shared : ConnectionType.Direct, TransactionPageLimit = 1 };
            using var db = Open();
            foreach (var collection in new[] { "rows", "other" })
            {
                db.GetCollection(collection).EnsureIndex("value");
                db.GetCollection(collection).Insert(Row(1, 10));
                _committed.Add(collection, new Dictionary<int, int> { [1] = 10 });
            }
            db.GetCollection("sentinel").Insert(Row(42, 900));
        }

        internal LiteDatabase Open()
        {
            var db = new LiteDatabase(Connection);
            db.Timeout = TimeSpan.FromSeconds(5);
            db.CheckpointSize = 0;
            return db;
        }

        internal static BsonDocument Row(int id, int value) => new BsonDocument
        { ["_id"] = id, ["value"] = value, ["payload"] = new string('x', 8192) };

        internal void Acknowledge(string collection, int id, int value) => _committed[collection][id] = value;
        internal void Deleted(string collection, int id) => _committed[collection].Remove(id);
        internal static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException("Oracle: " + message); }

        internal void VerifyCold()
        {
            // Two opens ensure that verification itself did not mask a recovery/reopen defect.
            for (var reopen = 0; reopen < 2; reopen++)
            {
                using var db = new LiteDatabase(Connection);
                foreach (var entry in _committed)
                {
                    var rows = db.GetCollection(entry.Key);
                    Require(rows.FindAll().OrderBy(x => x["_id"].AsInt32).Select(Key)
                        .SequenceEqual(entry.Value.OrderBy(x => x.Key).Select(x => x.Key + ":" + x.Value)), "cold exact state " + entry.Key);
                    foreach (var row in entry.Value)
                        Require(BsonSerializer.Serialize(rows.FindById(row.Key)).SequenceEqual(
                            BsonSerializer.Serialize(Row(row.Key, row.Value))), "cold exact payload " + entry.Key + "/" + row.Key);
                    using var indexes = db.Execute("select name from $indexes where collection = @0", new BsonValue[] { entry.Key });
                    var names = new List<string>();
                    while (indexes.Read()) names.Add(indexes.Current["name"].AsString);
                    Require(names.OrderBy(x => x).SequenceEqual(new[] { "_id", "value" }), "exact index catalog " + entry.Key);
                    foreach (var value in new[] { 10, 20, 30, 40, 50, 60 })
                    {
                        var query = rows.Query().Where(Query.EQ("value", value));
                        var plan = query.GetPlan()["index"];
                        Require(plan["name"] == "value" && plan["mode"].AsString.StartsWith("INDEX SEEK", StringComparison.Ordinal),
                            "cold value query did not exercise its index " + entry.Key);
                        Require(query.ToEnumerable().Select(x => x["_id"].AsInt32).OrderBy(x => x)
                            .SequenceEqual(entry.Value.Where(x => x.Value == value).Select(x => x.Key).OrderBy(x => x)), "cold indexed state " + entry.Key);
                    }
                }
                var sentinels = db.GetCollection("sentinel").FindAll().ToArray();
                Require(sentinels.Length == 1 && BsonSerializer.Serialize(sentinels[0]).SequenceEqual(
                    BsonSerializer.Serialize(Row(42, 900))), "exact sentinel state");
            }
        }
        private static string Key(BsonDocument row) => row["_id"].AsInt32 + ":" + row["value"].AsInt32;

        internal const string OverlapRefusal = "Overlapping or reentrant transaction handle use is not supported.";
        internal const string ReaderRefusal = "Close transaction-bound readers before committing.";
        internal const string SharedCallbackRefusal = "Cannot wait for shared writer ownership from inside a transaction handle callback for the same database.";
        internal const string OrdinaryCallbackRefusal = "Cannot wait for shared writer ownership from inside an ordinary callback for the same database.";

        internal static void Refused(Action action, string expected = OverlapRefusal)
        {
            try { action(); }
            catch (InvalidOperationException error) when (error.GetType() == typeof(InvalidOperationException) && error.Message == expected) { return; }
            throw new InvalidOperationException("Expected immediate InvalidOperationException was not raised");
        }
    }
}
