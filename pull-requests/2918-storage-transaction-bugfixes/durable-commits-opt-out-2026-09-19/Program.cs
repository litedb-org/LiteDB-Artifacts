using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LiteDB;

internal static class Program
{
    private const int N = 200;

    private static void Main()
    {
        var dir = Path.Combine(Path.GetTempPath(), "litedb-optout-probe-" + Guid.NewGuid().ToString("n").Substring(0, 6));
        Directory.CreateDirectory(dir);
        Console.WriteLine(typeof(LiteDatabase).Assembly.Location);

        try
        {
            foreach (var password in new[] { null, "secret" })
            {
                for (var round = 0; round < 3; round++)
                {
                    foreach (var durable in new[] { true, false })
                    {
                        Run(dir, durable, password, round);
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static void Run(string dir, bool durable, string password, int round)
    {
        var file = Path.Combine(dir, $"p-{durable}-{password}-{round}.db");
        var cs = new ConnectionString { Filename = file, Password = password, DurableCommits = durable };

        using (var db = new LiteDatabase(cs))
        {
            var col = db.GetCollection("rows");
            col.Insert(new BsonDocument { ["_id"] = 0, ["v"] = "warm" });

            var sw = Stopwatch.StartNew();
            for (var i = 1; i <= N; i++) col.Insert(new BsonDocument { ["_id"] = i, ["v"] = "inserted" });
            var insert = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            for (var i = 1; i <= N; i++) col.Update(new BsonDocument { ["_id"] = i, ["v"] = "updated" });
            var update = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            db.BeginTrans();
            for (var i = N + 1; i <= 2 * N; i++) col.Insert(new BsonDocument { ["_id"] = i, ["v"] = "batched" });
            db.Commit();
            var batch = sw.Elapsed.TotalMilliseconds;

            var flag = db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean;
            Console.WriteLine($"pwd={(password ?? "-"),-6} durable={durable,-5} flag={flag,-5} insert200={insert,8:F1} ms  update200={update,8:F1} ms  batch200={batch,6:F1} ms");
        }
    }
}
