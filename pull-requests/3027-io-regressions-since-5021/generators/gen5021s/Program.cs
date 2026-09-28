using LiteDB;
var path = Path.Combine(args[0], "damaged.db");
File.Delete(path);
using (var db = new LiteDatabase($"Filename={path};Connection=direct"))
{
    var c = db.GetCollection("c");
    c.EnsureIndex("b", "$.b", unique: true);
    c.Insert(new BsonDocument { ["_id"] = 1, ["a"] = "keep-1", ["b"] = "u-1" });
    c.Insert(new BsonDocument { ["_id"] = 7, ["b"] = "u-7", ["a"] = "tail-7" });
    c.Insert(new BsonDocument { ["_id"] = 8, ["a"] = "keep-8", ["b"] = "u-8" });
    c.Insert(new BsonDocument { ["_id"] = 9, ["a"] = "keep-9", ["b"] = "u-9" });
}
using (var db = new LiteDatabase($"Filename={path};Connection=direct;ReadOnly=true"))
    Console.WriteLine("5.0.21 wrote: " + string.Join(" ", db.GetCollection("c").FindAll().Select(x => x.ToString())));
