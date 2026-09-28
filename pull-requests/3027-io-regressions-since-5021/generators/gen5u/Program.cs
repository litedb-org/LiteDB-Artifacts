using System;
using System.IO;
using System.Linq;
using LiteDB;

var name = args[0];
if (File.Exists(name)) File.Delete(name);
using (var db = new LiteDatabase(name))
{
    var c = db.GetCollection("c");
    c.EnsureIndex("b", true);
    c.Insert(Enumerable.Range(1, 4).Select(i => new BsonDocument { ["_id"] = i, ["a"] = "keep-" + i, ["b"] = "u-" + i }));
    db.Checkpoint();
}
// damage the BSON length of string field "a" in documents 2 and 3
var bytes = File.ReadAllBytes(name);
foreach (var i in new[] { 2, 3 })
{
    var pattern = new byte[] { 0x02, (byte)'a', 0 }.Concat(BitConverter.GetBytes(7)).Concat(System.Text.Encoding.UTF8.GetBytes("keep-" + i + "\0")).ToArray();
    var at = Enumerable.Range(0, bytes.Length - pattern.Length).Single(p => bytes.Skip(p).Take(pattern.Length).SequenceEqual(pattern));
    BitConverter.GetBytes(0x7FFFFFF0).CopyTo(bytes, at + 3);
}
File.WriteAllBytes(name, bytes);
// 5.0.21 opens it and reads the undamaged documents
using (var db = new LiteDatabase(name))
{
    Console.WriteLine("5.0.21 FindById(1): " + (object)db.GetCollection("c").FindById(1));
    Console.WriteLine("5.0.21 FindById(4): " + (object)db.GetCollection("c").FindById(4));
    try { Console.WriteLine("5.0.21 FindById(2): " + (object)db.GetCollection("c").FindById(2)); } catch (Exception ex) { Console.WriteLine("5.0.21 FindById(2) fails: " + ex.Message); }
}
File.WriteAllBytes(name + ".pristine", File.ReadAllBytes(name));
using (var db = new LiteDatabase(name))
{
    try { db.Rebuild(); Console.WriteLine("5.0.21 rebuild ok: " + db.GetCollection("c").Count() + " errors=" + db.GetCollection("_rebuild_errors").Count()); }
    catch (Exception ex) { Console.WriteLine("5.0.21 rebuild fails: " + ex.GetType().Name + ": " + ex.Message); }
}
