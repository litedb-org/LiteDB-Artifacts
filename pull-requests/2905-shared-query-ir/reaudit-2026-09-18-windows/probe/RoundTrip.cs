using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LiteDB;

/// <summary>
/// Cross-version file round trip. One build runs "create", the other runs "mutate", and
/// BOTH run "digest" on the resulting file. Digests must be identical: they walk every
/// index in both directions (level-0 prev/next links), seek every distinct key (upper
/// skip-list levels), and compare against the documents read by a full data scan.
/// </summary>
internal static class RoundTrip
{
    private static readonly string[] IndexedFields = { "_id", "Unique", "Bucket", "Text", "Nested.Score", "Tags[*]" };

    private static BsonDocument Make(int id, Random random)
    {
        var tagCount = random.Next(0, 4);
        return new BsonDocument
        {
            ["_id"] = id,
            ["Unique"] = "u" + id.ToString("D7"),
            ["Bucket"] = random.Next(0, 25),
            // Long keys exercise extended-length index keys (> 255 bytes) as well as short ones.
            ["Text"] = random.Next(0, 10) == 0 ? new string((char)('a' + id % 26), 300 + id % 200) : "t" + random.Next(0, 400),
            ["Nested"] = new BsonDocument { ["Score"] = random.Next(0, 3) == 0 ? (BsonValue)random.NextDouble() : random.Next(-50, 50) },
            ["Tags"] = new BsonArray(Enumerable.Range(0, tagCount).Select(_ => (BsonValue)("tag" + random.Next(0, 12)))),
            ["Payload"] = new string('x', random.Next(10, 1500))
        };
    }

    private static ConnectionString Connection(string file, string password)
    {
        return new ConnectionString { Filename = file, Password = string.IsNullOrEmpty(password) ? null : password };
    }

    public static int Create(string file, string password)
    {
        var random = new Random(12345);
        using (var db = new LiteDatabase(Connection(file, password)))
        {
            var col = db.GetCollection("items");
            col.EnsureIndex("Unique", "$.Unique", true);
            col.EnsureIndex("Bucket", "$.Bucket");
            col.EnsureIndex("Text", "$.Text");
            col.EnsureIndex("NestedScore", "$.Nested.Score");
            col.EnsureIndex("Tags", "$.Tags[*]");
            col.InsertBulk(Enumerable.Range(1, 6000).Select(i => Make(i, random)));
            db.Checkpoint();
        }
        Console.WriteLine("created");
        return 0;
    }

    public static int Mutate(string file, string password)
    {
        var random = new Random(67890);
        using (var db = new LiteDatabase(Connection(file, password)))
        {
            var col = db.GetCollection("items");
            // Delete a third (unlinks nodes at every level), move keys of another third, then insert more.
            var deleted = col.DeleteMany("_id % 3 = 0");
            var updated = 0;
            foreach (var doc in col.Find("_id % 3 = 1").ToList())
            {
                doc["Bucket"] = random.Next(0, 25);
                doc["Text"] = "m" + random.Next(0, 400);
                doc["Nested"] = new BsonDocument { ["Score"] = random.Next(-50, 50) };
                doc["Tags"] = new BsonArray(Enumerable.Range(0, random.Next(0, 4)).Select(_ => (BsonValue)("tag" + random.Next(0, 12))));
                if (col.Update(doc)) updated++;
            }
            col.InsertBulk(Enumerable.Range(6001, 3000).Select(i => Make(i, random)));
            // A transaction that is rolled back must leave no trace in any index.
            db.BeginTrans();
            col.InsertBulk(Enumerable.Range(20001, 500).Select(i => Make(i, random)));
            col.DeleteMany("_id % 5 = 2");
            db.Rollback();
            db.Checkpoint();
            Console.WriteLine($"mutated deleted={deleted} updated={updated} count={col.Count()}");
        }
        return 0;
    }

    public static int Digest(string file, string password)
    {
        using (var db = new LiteDatabase(Connection(file, password)))
        using (var sha = SHA256.Create())
        {
            var col = db.GetCollection("items");
            var documents = col.Query().ToList();
            var byId = documents.ToDictionary(d => d["_id"].AsInt32);
            Console.WriteLine($"documents={documents.Count} idSum={documents.Sum(d => (long)d["_id"].AsInt32)}");
            var problems = 0;

            // Walk each scalar index in both directions via ORDER BY on its exact expression.
            foreach (var field in new[] { "_id", "Unique", "Bucket", "Text", "Nested.Score" })
            {
                var ascending = col.Find(Query.All(field, Query.Ascending)).Select(d => d["_id"].AsInt32).ToList();
                var descending = col.Find(Query.All(field, Query.Descending)).Select(d => d["_id"].AsInt32).ToList();
                var plan = col.Query().OrderBy(BsonExpression.Create("$." + field)).GetPlan();
                var complete = new HashSet<int>(ascending).SetEquals(byId.Keys) && ascending.Count == documents.Count;
                // Keys must be non-decreasing ascending and non-increasing descending, and both walks must cover the same documents.
                BsonValue Key(int id) { BsonValue v = byId[id]; foreach (var part in field.Split('.')) v = v[part]; return v; }
                var ascSorted = ascending.Zip(ascending.Skip(1), (a, b) => Key(a).CompareTo(Key(b)) <= 0).All(x => x);
                var descSorted = descending.Zip(descending.Skip(1), (a, b) => Key(a).CompareTo(Key(b)) >= 0).All(x => x);
                var sameSet = new HashSet<int>(ascending).SetEquals(descending) && descending.Count == documents.Count;
                if (!complete || !ascSorted || !descSorted || !sameSet) problems++;
                var hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join(",", ascending) + "|" + string.Join(",", descending)))).Replace("-", "").Substring(0, 16);
                Console.WriteLine($"walk {field}: planIndex={plan["index"]["name"].AsString} n={ascending.Count} complete={complete} ascSorted={ascSorted} descSorted={descSorted} sameSet={sameSet} hash={hash}");
            }

            // Seek every distinct key (drives the upper skip-list levels) and compare with the scanned documents.
            foreach (var field in new[] { "Bucket", "Text", "Unique", "Nested.Score" })
            {
                BsonValue Key(BsonDocument d) { BsonValue v = d; foreach (var part in field.Split('.')) v = v[part]; return v; }
                var groups = documents.GroupBy(d => JsonSerializer.Serialize(Key(d))).ToList();
                var wrong = 0;
                foreach (var group in groups)
                {
                    var found = col.Find(BsonExpression.Create("$." + field + " = @0", Key(group.First()))).Select(d => d["_id"].AsInt32).OrderBy(i => i);
                    var expectedIds = group.Select(d => d["_id"].AsInt32).OrderBy(i => i).ToList();
                    var foundIds = found.ToList();
                    if (!foundIds.SequenceEqual(expectedIds))
                    {
                        if (wrong < 3) Console.WriteLine($"    MISMATCH {field} key={JsonSerializer.Serialize(Key(group.First()))} type={Key(group.First()).Type} found=[{string.Join(",", foundIds.Take(6))}] expected=[{string.Join(",", expectedIds.Take(6))}]");
                        wrong++;
                    }
                }
                if (wrong > 0) problems++;
                var plan = col.Query().Where(BsonExpression.Create("$." + field + " = @0", Key(documents[0]))).GetPlan();
                Console.WriteLine($"seek {field}: planIndex={plan["index"]["name"].AsString} distinctKeys={groups.Count} wrong={wrong}");
            }

            var range = col.Find(Query.Between("Nested.Score", -10, 10)).Count();
            var rangeExpected = documents.Count(d => d["Nested"]["Score"].IsNumber && d["Nested"]["Score"].AsDouble >= -10 && d["Nested"]["Score"].AsDouble <= 10);
            if (range != rangeExpected) problems++;
            Console.WriteLine($"range Nested.Score[-10,10]: index={range} scan={rangeExpected}");

            var tagWrong = 0;
            var tags = documents.SelectMany(d => d["Tags"].AsArray.Select(t => t.AsString)).Distinct().OrderBy(t => t).ToList();
            foreach (var tag in tags)
            {
                var found = col.Find(BsonExpression.Create("$.Tags[*] ANY = @0", tag)).Select(d => d["_id"].AsInt32).OrderBy(i => i);
                var expectedIds = documents.Where(d => d["Tags"].AsArray.Any(t => t.AsString == tag)).Select(d => d["_id"].AsInt32).OrderBy(i => i);
                if (!found.SequenceEqual(expectedIds)) tagWrong++;
            }
            if (tagWrong > 0 || tags.Count == 0) problems++;
            var tagPlan = col.Query().Where(BsonExpression.Create("$.Tags[*] ANY = @0", "tag3")).GetPlan();
            Console.WriteLine($"multikey Tags[*]: planIndex={tagPlan["index"]["name"].AsString} distinctTags={tags.Count} wrong={tagWrong}");
            Console.WriteLine($"problems={problems}");
            return problems == 0 ? 0 : 1;
        }
    }
}
