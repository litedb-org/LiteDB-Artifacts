using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LiteDB;

// usage: probe perf <dir> | write <file> | verify <file>
var mode = args[0];
if (mode == "perf") Perf(args[1]);
else if (mode == "write") WriteDb(args[1], args.Length > 2 ? args[2] : null);
else if (mode == "verify") VerifyDb(args[1], args.Length > 2 ? args[2] : null);
return;

static string Cs(string file, string pw) => pw == null ? $"Filename={file}" : $"Filename={file};Password={pw}";

static void Perf(string dir)
{
    Directory.CreateDirectory(dir);
    foreach (var round in Enumerable.Range(0, 3))
    {
        var file = Path.Combine(dir, $"perf-{Guid.NewGuid():N}.db");
        using (var db = new LiteDatabase(Cs(file, null)))
        {
            var col = db.GetCollection<BsonDocument>("c");
            col.Insert(new BsonDocument { ["_id"] = -1 });

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < 200; i++) col.Insert(new BsonDocument { ["_id"] = i, ["name"] = "item " + i });
            var single = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            col.InsertBulk(Enumerable.Range(1000, 100_000).Select(i => new BsonDocument { ["_id"] = i, ["name"] = "item " + i }));
            var bulk = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            db.BeginTrans();
            for (var i = 0; i < 200; i++) col.Insert(new BsonDocument { ["_id"] = 500_000 + i, ["name"] = "item " + i });
            db.Commit();
            var tx = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (var i = 0; i < 200; i++) col.Update(new BsonDocument { ["_id"] = i, ["name"] = "upd " + i });
            var upd = sw.Elapsed.TotalMilliseconds;

            Console.WriteLine($"round {round}: 200 single inserts {single:F1} ms ({single / 200:F2} ms/commit) | 200 single updates {upd:F1} ms | 200 inserts in 1 tx {tx:F1} ms | InsertBulk 100k {bulk:F1} ms");
        }
        File.Delete(file);
        var log = file.Replace(".db", "-log.db");
        if (File.Exists(log)) File.Delete(log);
    }
}

static void WriteDb(string file, string pw)
{
    using var db = new LiteDatabase(Cs(file, pw));
    var col = db.GetCollection<BsonDocument>("items");
    col.EnsureIndex("name");
    col.EnsureIndex("grp", "$.grp");
    col.InsertBulk(Enumerable.Range(0, 20_000).Select(i => new BsonDocument
    {
        ["_id"] = i, ["name"] = "name-" + (i * 7919 % 20_000), ["grp"] = i % 13,
        ["payload"] = new string((char)('a' + i % 26), i % 900), ["when"] = new DateTime(2020, 1, 1).AddMinutes(i), ["dec"] = (decimal)i / 7
    }));
    col.DeleteMany("_id % 5 = 0");
    var big = new byte[3_000_000];
    new Random(42).NextBytes(big);
    db.FileStorage.Upload("big", "big.bin", new MemoryStream(big));
    db.FileStorage.Upload("empty", "empty.bin", new MemoryStream());
    Console.WriteLine($"wrote {col.Count()} docs, user_version {db.UserVersion}");
}

static void VerifyDb(string file, string pw)
{
    using var db = new LiteDatabase(Cs(file, pw));
    var col = db.GetCollection<BsonDocument>("items");
    var count = col.Count();
    long sum = 0;
    foreach (var d in col.FindAll())
    {
        var i = d["_id"].AsInt32;
        if (d["name"].AsString != "name-" + (i * 7919 % 20_000)) throw new Exception("name mismatch " + i);
        if (d["payload"].AsString.Length != i % 900) throw new Exception("payload mismatch " + i);
        sum += i;
    }
    var byIndex = col.Count(Query.EQ("grp", 3));
    var byName = col.FindOne(Query.EQ("name", "name-7919"));
    var ms = new MemoryStream();
    db.FileStorage.Download("big", ms);
    var big = new byte[3_000_000];
    new Random(42).NextBytes(big);
    var fileOk = ms.ToArray().AsSpan().SequenceEqual(big);
    var ms2 = new MemoryStream();
    db.FileStorage.Download("empty", ms2);
    // also prove it is writable by this version
    col.Insert(new BsonDocument { ["_id"] = 1_000_000 + Environment.TickCount, ["name"] = "x", ["grp"] = 1, ["payload"] = "" });
    Console.WriteLine($"verify OK: count={count} (expect 16000) sum={sum} grp3={byIndex} name7919={(byName?["_id"].ToString() ?? "null")} bigFileOk={fileOk} emptyLen={ms2.Length} indexes={string.Join(",", db.GetCollection("$indexes").Query().Where("collection = 'items'").Select("name").ToList().Select(x => x["name"].AsString))}");
}
