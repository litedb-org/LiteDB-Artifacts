using System;
using System.IO;
using System.Linq;
using LiteDB;
using LiteDB.Vector;

var dir = args[0];
Directory.CreateDirectory(dir);
foreach (var password in new[] { (string)null, "vector-secret" })
{
    var name = Path.Combine(dir, password == null ? "vectors.db" : "vectors-encrypted.db");
    if (File.Exists(name)) File.Delete(name);
    var cs = "Filename=" + name + (password == null ? "" : ";Password=" + password);
    using (var db = new LiteDatabase(cs))
    {
        var docs = db.GetCollection("docs");
        docs.Insert(Enumerable.Range(1, 40).Select(i => new BsonDocument
        {
            ["_id"] = i, ["name"] = "d" + i, ["Embedding"] = new BsonVector(new[] { (float)i, 1f })
        }));
        docs.EnsureIndex("name");
        docs.EnsureIndex("embedding", "$.Embedding", new VectorIndexOptions(2));
        var computed = db.GetCollection("computed");
        computed.Insert(Enumerable.Range(1, 20).Select(i => new BsonDocument
        {
            ["_id"] = i, ["Embedding"] = i % 5 == 0 ? BsonValue.Null : new BsonVector(new[] { 1f, (float)i })
        }));
        computed.EnsureIndex("coalesced", "COALESCE($.Embedding, [0, 0])", new VectorIndexOptions(2));
        var top = docs.Query().TopKNear("Embedding", new[] { 20f, 1f }, 1).ToArray();
        Console.WriteLine($"{name}: top={top.Single()["_id"]} indexes={string.Join(",", db.GetCollection("$indexes").FindAll().Select(x => x["collection"] + "." + x["name"] + ":" + x["type"]))}");
    }
    var bytes = File.ReadAllBytes(name);
    Console.WriteLine($"  file version byte (plain only meaningful) = {bytes[59]} length={bytes.Length}");
}
