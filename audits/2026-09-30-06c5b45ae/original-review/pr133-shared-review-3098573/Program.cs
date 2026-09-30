using System;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;

var file = Path.Combine(Path.GetTempPath(), "pr133-scratch-" + Guid.NewGuid().ToString("N") + ".db");
using (var db = new LiteDatabase($"Filename={file};Connection=Shared"))
{
    var rows = db.GetCollection("rows");
    for (int i = 1; i <= 2000; i++) rows.Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('x', 200) });

    // Leased streaming reader on this thread, then a write (pins), then legacy BeginTrans.
    var withReader = Environment.GetEnvironmentVariable("READER") == "1";
    IBsonDataReader reader = null;
    if (withReader) { reader = db.Execute("SELECT $ FROM rows"); reader.Read(); rows.Insert(new BsonDocument { ["_id"] = 5000 }); }
    Console.WriteLine("legacy begin: " + db.BeginTrans());
    rows.Insert(new BsonDocument { ["_id"] = 5001 });
    var sw = System.Diagnostics.Stopwatch.StartNew();
    try
    {
        using var tx = (Environment.GetEnvironmentVariable("INF") == "1" ? db.BeginTransaction() : db.BeginTransaction(TimeSpan.FromSeconds(3)));
        Console.WriteLine("handle begun after " + sw.ElapsedMilliseconds + "ms");
    }
    catch (Exception e) { Console.WriteLine("begin failed after " + sw.ElapsedMilliseconds + "ms: " + e.GetType().Name + ": " + e.Message); }
    db.Rollback();
}
File.Delete(file);
