using System;
using System.IO;
using System.Linq;
using LiteDB;

var dir = args[0];
Directory.CreateDirectory(dir);
var f = Path.Combine(dir, "encrypted.db");
foreach (var x in Directory.GetFiles(dir)) File.Delete(x);
var cs = $"Filename={f};Password=wal-secret";
var db = new LiteDatabase(cs);
db.Pragma("CHECKPOINT", 0);
var col = db.GetCollection("docs");
col.EnsureIndex("value");
col.Insert(Enumerable.Range(0, 60).Select(i => new BsonDocument { ["_id"] = i, ["value"] = 0 }));
db.Checkpoint();
col.Update(Enumerable.Range(0, 10).Select(i => new BsonDocument { ["_id"] = i, ["value"] = 7 }));
col.Insert(Enumerable.Range(60, 5).Select(i => new BsonDocument { ["_id"] = i, ["value"] = 7 }));
// process-crash image: copy both files while the database is open
File.Copy(f, Path.Combine(dir, "crash.db"), true);
File.Copy(Path.Combine(dir, "encrypted-log.db"), Path.Combine(dir, "crash-log.db"), true);
Console.WriteLine($"data {new FileInfo(Path.Combine(dir, "crash.db")).Length} log {new FileInfo(Path.Combine(dir, "crash-log.db")).Length}");
db.Dispose();
// 5.0.21 recovers the image
File.Copy(Path.Combine(dir, "crash.db"), Path.Combine(dir, "check.db"), true);
File.Copy(Path.Combine(dir, "crash-log.db"), Path.Combine(dir, "check-log.db"), true);
using (var check = new LiteDatabase($"Filename={Path.Combine(dir, "check.db")};Password=wal-secret"))
    Console.WriteLine($"5.0.21 recovers: count={check.GetCollection("docs").Count()} value7={check.GetCollection("docs").Count(Query.EQ("value", 7))}");
