using System;
using System.IO;
using System.Linq;
using LiteDB;
using LiteDB.Vector;

var name = args[0];
if (File.Exists(name)) File.Delete(name);
const string Expression = "IIF(SUBSTRING(COALESCE($.b, 'x'), 1, 2) = '-0', $.v, $.v)";
using (var db = new LiteDatabase(name))
{
    var c = db.GetCollection("vectors");
    c.Insert(Enumerable.Range(1, 3).Select(i => new BsonDocument
    {
        ["_id"] = i, ["a"] = "keep-" + i, ["b"] = "u-" + i, ["v"] = new BsonVector(new[] { (float)i, 1f })
    }));
    c.EnsureIndex("vv", Expression, new VectorIndexOptions(2));
    db.Checkpoint();
    Console.WriteLine("indexes: " + string.Join(",", db.GetCollection("$indexes").FindAll().Select(x => x["name"].AsString)));
}
var bytes = File.ReadAllBytes(name);
Console.WriteLine("file version byte = " + bytes[59]);
var pattern = new byte[] { 0x02, (byte)'b', 0 }.Concat(BitConverter.GetBytes(4)).Concat(System.Text.Encoding.UTF8.GetBytes("u-2\0")).ToArray();
var hits = Enumerable.Range(0, bytes.Length - pattern.Length).Where(p => bytes.Skip(p).Take(pattern.Length).SequenceEqual(pattern)).ToArray();
Console.WriteLine("damaged: " + hits.Length);
foreach (var at in hits) BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, at + 3);
File.WriteAllBytes(name, bytes);
