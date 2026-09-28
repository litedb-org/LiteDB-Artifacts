using LiteDB;
var dir = args[0];
var mode = args[1];
var path = Path.Combine(dir, "c.db");
switch (mode)
{
    case "create":
    {
        using var db = new LiteDatabase($"Filename={path};Connection=direct");
        var a = db.GetCollection("a"); var b = db.GetCollection("b");
        for (int i = 0; i < 10; i++)
        {
            a.Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('a', 100) });
            b.Insert(new BsonDocument { ["_id"] = i, ["v"] = "b" });
        }
        break;
    }
    case "crash":
    {
        // T1 (worker thread) holds an open explicit transaction on "a" that allocated many
        // new pages (kept in memory); T2 (main thread) commits small inserts into "b".
        var db = new LiteDatabase($"Filename={path};Connection=direct");
        var a = db.GetCollection("a"); var b = db.GetCollection("b");
        var started = new ManualResetEventSlim();
        var t1 = new Thread(() =>
        {
            db.BeginTrans();
            for (int i = 100; i < 400; i++) a.Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('x', 1000) });
            started.Set();
            Thread.Sleep(Timeout.Infinite);
        }) { IsBackground = true };
        t1.Start();
        started.Wait();
        for (int i = 100; i < 103; i++) b.Insert(new BsonDocument { ["_id"] = i, ["v"] = new string('y', 6000) });
        Thread.Sleep(2000);
        Console.WriteLine("crashing");
        Environment.FailFast("simulated crash after T2 committed while T1 is open");
        break;
    }
    case "dropindex":
    {
        using var db = new LiteDatabase($"Filename={args[2]};Connection=direct");
        Console.WriteLine("5.0.21 DropIndex: " + db.GetCollection(args[3]).DropIndex(args[4]));
        Console.WriteLine("5.0.21 count: " + db.GetCollection(args[3]).Count());
        break;
    }
    case "bigcrash":
    {
        // A larger database (about 1400 pages): its WAL commits a new collection's pages and header.
        using (var db0 = new LiteDatabase($"Filename={path};Connection=direct"))
        {
            var big = db0.GetCollection("big");
            big.InsertBulk(Enumerable.Range(0, 10000).Select(i => new BsonDocument { ["_id"] = i, ["v"] = new string('z', 1000) }));
        }
        var db = new LiteDatabase($"Filename={path};Connection=direct");
        db.GetCollection("fresh").Insert(new BsonDocument { ["_id"] = 1, ["v"] = "foreign" });
        Thread.Sleep(2000);
        Console.WriteLine("crashing");
        Environment.FailFast("simulated crash");
        break;
    }
    case "verify":
    {
        using var db = new LiteDatabase($"Filename={path};Connection=direct");
        Console.WriteLine($"5.0.21: a={db.GetCollection("a").Count()} b={db.GetCollection("b").Count()} b>=100:{db.GetCollection("b").Count(Query.GTE("_id", 100))}");
        break;
    }
}
