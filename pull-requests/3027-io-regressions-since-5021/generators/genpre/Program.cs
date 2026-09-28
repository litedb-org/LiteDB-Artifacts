using LiteDB;
using LiteDB.Vector;
var path = args[0];
using var db = new LiteDatabase($"Filename={path};Connection=direct");
var docs = db.GetCollection("docs");
docs.EnsureIndex("name", "$.name");
docs.EnsureIndex("zeta", "$.A", new VectorIndexOptions(2));
docs.EnsureIndex("Alpha", "$.B", new VectorIndexOptions(3, VectorDistanceMetric.Euclidean));
docs.EnsureIndex("mid", "$.A", new VectorIndexOptions(2, VectorDistanceMetric.DotProduct));
for (int i = 1; i <= 30; i++)
    docs.Insert(new BsonDocument { ["_id"] = i, ["name"] = "d" + i, ["A"] = new BsonVector(new[] { (float)i, 1f }), ["B"] = new BsonVector(new[] { 1f, (float)i, 2f }) });
docs.DropIndex("mid");
docs.EnsureIndex("other", "$._id");
Console.WriteLine("indexes: " + string.Join(",", db.GetCollection("$indexes").FindAll().Select(x => x["name"].AsString)));
