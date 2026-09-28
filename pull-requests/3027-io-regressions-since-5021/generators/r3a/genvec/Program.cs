using LiteDB;
using LiteDB.Vector;
var name = args[0];
if (File.Exists(name)) File.Delete(name);
var expression = "IIF(SUBSTRING(COALESCE($.b, 'x'), 1, 2) = 'u-', $.v, $.v)";
using (var db = new LiteDatabase(name))
{
    var c = db.GetCollection("vt");
    c.Insert(Enumerable.Range(1, 3).Select(i => new BsonDocument { ["_id"] = i, ["a"] = "keep-" + i, ["b"] = "u-" + i, ["v"] = new BsonVector(new[] { (float)i, 1f }) }));
    c.EnsureIndex("vx", expression, new VectorIndexOptions(2));
    Console.WriteLine("indexes: " + string.Join(",", db.GetCollection("$indexes").FindAll().Select(x => x["collection"] + "." + x["name"] + ":" + x["type"])));
    db.Checkpoint();
}
var bytes = File.ReadAllBytes(name);
var pattern = new byte[] { 0x02, (byte)'b', 0 }.Concat(BitConverter.GetBytes(4)).Concat(System.Text.Encoding.UTF8.GetBytes("u-2\0")).ToArray();
var hits = Enumerable.Range(0, bytes.Length - pattern.Length).Where(p => bytes.Skip(p).Take(pattern.Length).SequenceEqual(pattern)).ToArray();
Console.WriteLine("damaged: " + hits.Length + " version=" + bytes[59]);
foreach (var at in hits) BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, at + 3);
File.WriteAllBytes(name, bytes);
