using System;
using System.IO;
using System.Linq;
using LiteDB;

var name = args[0];
if (File.Exists(name)) File.Delete(name);
var indexes = new (string collection, string expression)[]
{
    ("maxed", "COALESCE($.b, MAXVALUE())"),
    ("mined", "COALESCE($.b, MINVALUE())"),
    ("long", "COALESCE($.b, '" + new string('k', 1100) + "')"),
    ("throws", "SUBSTRING(COALESCE($.b, 'x'), 1, 2)"),
};
using (var db = new LiteDatabase(name))
{
    foreach (var (collection, expression) in indexes)
    {
        var c = db.GetCollection(collection);
        c.Insert(Enumerable.Range(1, 3).Select(i => new BsonDocument { ["_id"] = i, ["a"] = "keep-" + i, ["b"] = "u-" + i }));
        c.EnsureIndex("k", expression);
        Console.WriteLine(collection + " keys: " + string.Join(",", c.Query().Select(BsonExpression.Create(expression)).ToArray().Select(x => x.ToString().Substring(0, Math.Min(20, x.ToString().Length)))));
    }
    db.Checkpoint();
}
// damage the BSON length of string field "b" of document 2 in every collection
var bytes = File.ReadAllBytes(name);
var pattern = new byte[] { 0x02, (byte)'b', 0 }.Concat(BitConverter.GetBytes(4)).Concat(System.Text.Encoding.UTF8.GetBytes("u-2\0")).ToArray();
var hits = Enumerable.Range(0, bytes.Length - pattern.Length).Where(p => bytes.Skip(p).Take(pattern.Length).SequenceEqual(pattern)).ToArray();
Console.WriteLine("damaged: " + hits.Length);
foreach (var at in hits) BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, at + 3);
File.WriteAllBytes(name, bytes);
using (var db = new LiteDatabase(name))
    foreach (var (collection, _) in indexes)
        Console.WriteLine($"5.0.21 {collection}: doc1={db.GetCollection(collection).FindById(1) != null} doc3={db.GetCollection(collection).FindById(3) != null}");
