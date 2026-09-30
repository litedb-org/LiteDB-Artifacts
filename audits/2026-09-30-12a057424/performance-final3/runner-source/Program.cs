using System.Diagnostics;
using System.Text.Json;
using LiteDB;
var revision = args[0];
if (args.Length > 1 && args[1] == "steady") { SteadyState.Run(args); return; }
#if HANDLES
if (args.Length > 1 && args[1] == "resources") { Resources(revision); return; }
#endif
if (args.Length > 1 && args[1] == "contention") { Contention(revision); return; }
foreach (var shared in new[] { false, true })
foreach (var mode in Modes())
foreach (var operation in new[] { "read", "write", "open" })
{
    if (operation == "open" && mode != "ordinary") continue;
    for (var repeat = -1; repeat < 5; repeat++)
    {
        var file = Path.Combine(Path.GetTempPath(), "litedb-handle-benchmark-" + Guid.NewGuid() + ".db");
        var cs = new ConnectionString { Filename = file, Connection = shared ? ConnectionType.Shared : ConnectionType.Direct, DurableCommits = true };
        using (var seed = new LiteDatabase(cs))
        {
            seed.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1, ["value"] = 1 });
            seed.Checkpoint();
        }
        var count = operation == "read" && !shared ? 10000 : 200;
        using (var db = new LiteDatabase(cs))
        {
            for (var i = 0; i < 50; i++) Perform(db, cs, mode, operation);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var bytes = GC.GetTotalAllocatedBytes(true);
            var beforeThreads = Process.GetCurrentProcess().Threads.Count;
            var samples = new double[count];
            var clock = Stopwatch.StartNew();
            for (var i = 0; i < count; i++)
            {
                var start = Stopwatch.GetTimestamp();
                Perform(db, cs, mode, operation);
                samples[i] = Stopwatch.GetElapsedTime(start).TotalMicroseconds;
            }
            clock.Stop();
            bytes = GC.GetTotalAllocatedBytes(true) - bytes;
            Array.Sort(samples);
            var process = Process.GetCurrentProcess();
            if (repeat >= 0) Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { revision, shared, mode, operation, repeat, count,
                elapsedMs = clock.Elapsed.TotalMilliseconds, opsPerSecond = count / clock.Elapsed.TotalSeconds,
                p50us = samples[count / 2], p95us = samples[(int)(count * .95)], p99us = samples[(int)(count * .99)],
                bytesPerOp = bytes / (double)count, beforeThreads, afterThreads = process.Threads.Count, handles = process.HandleCount }));
        }
        using (var verify = new LiteDatabase(cs))
            if (verify.GetCollection("rows").Count() != 1 || verify.GetCollection("rows").FindById(1)["value"] != 1) throw new Exception("incorrect result");
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(file), Path.GetFileName(file) + "*")) File.Delete(path);
    }
}
static IEnumerable<string> Modes()
{
    yield return "ordinary"; yield return "legacy";
#if HANDLES
    yield return "handle";
#endif
}
static void Perform(LiteDatabase db, ConnectionString cs, string mode, string operation, int id = 1)
{
    if (operation == "open") { using var attached = new LiteDatabase(cs); if (attached.GetCollection("rows").Count() != 1) throw new Exception(); return; }
#if HANDLES
    if (mode == "handle")
    {
        using var tx = db.BeginTransaction();
        Access(tx.GetCollection("rows"), operation, id);
        tx.Commit();
        return;
    }
#endif
    if (mode == "legacy") db.BeginTrans();
    Access(db.GetCollection("rows"), operation, id);
    if (mode == "legacy") db.Commit();
}
static void Access(ILiteCollection<BsonDocument> rows, string operation, int id)
{
    if (operation == "read") { if (rows.FindById(id)["value"] != 1) throw new Exception(); }
    else rows.Update(new BsonDocument { ["_id"] = id, ["value"] = 1 });
}
static void Contention(string revision)
{
    foreach (var shared in new[] { false, true })
    foreach (var mode in Modes())
    for (var repeat = 0; repeat < 3; repeat++)
    {
        var file = Path.Combine(Path.GetTempPath(), "litedb-handle-contention-" + Guid.NewGuid() + ".db");
        var cs = new ConnectionString { Filename = file, Connection = shared ? ConnectionType.Shared : ConnectionType.Direct, DurableCommits = true };
        using (var seed = new LiteDatabase(cs))
            for (var id = 1; id <= 4; id++) seed.GetCollection("rows").Insert(new BsonDocument { ["_id"] = id, ["value"] = 0 });
        using var ready = new CountdownEvent(4);
        using var start = new ManualResetEventSlim();
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var samples = new System.Collections.Concurrent.ConcurrentBag<double>();
        var threads = Enumerable.Range(1, 4).Select(id => new Thread(() =>
        {
            try
            {
                using var db = new LiteDatabase(cs);
                ready.Signal();
                start.Wait();
                for (var i = 0; i < 40; i++)
                {
                    var tick = Stopwatch.GetTimestamp();
                    Perform(db, cs, mode, "write", id);
                    samples.Add(Stopwatch.GetElapsedTime(tick).TotalMicroseconds);
                }
            }
            catch (Exception error) { errors.Enqueue(error); }
        })).ToArray();
        foreach (var thread in threads) thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(20))) throw new Exception("worker startup failed");
        var process = Process.GetCurrentProcess();
        var beforeThreads = process.Threads.Count;
        var peakThreads = beforeThreads;
        var peakHandles = process.HandleCount;
        var bytes = GC.GetTotalAllocatedBytes(true);
        var clock = Stopwatch.StartNew();
        start.Set();
        while (threads.Any(thread => thread.IsAlive))
        {
            process.Refresh();
            peakThreads = Math.Max(peakThreads, process.Threads.Count);
            peakHandles = Math.Max(peakHandles, process.HandleCount);
            if (clock.Elapsed > TimeSpan.FromMinutes(2)) throw new Exception("contention exceeded liveness limit");
            Thread.Sleep(10);
        }
        clock.Stop();
        if (!errors.IsEmpty) throw new AggregateException(errors);
        bytes = GC.GetTotalAllocatedBytes(true) - bytes;
        var latency = samples.OrderBy(value => value).ToArray();
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { revision, shared, mode, operation = "contention", repeat,
            count = latency.Length, opsPerSecond = latency.Length / clock.Elapsed.TotalSeconds,
            p50us = latency[latency.Length / 2], p95us = latency[(int)(latency.Length * .95)], p99us = latency[(int)(latency.Length * .99)],
            bytesPerOp = bytes / (double)latency.Length, beforeThreads, peakThreads, peakHandles }));
        using (var verify = new LiteDatabase(cs))
        {
            if (verify.GetCollection("rows").Count() != 4 || verify.GetCollection("rows").FindAll().Any(row => row["value"] != 1))
                throw new Exception("incorrect committed state");
        }
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(file), Path.GetFileName(file) + "*")) File.Delete(path);
    }
}
#if HANDLES
static void Resources(string revision)
{
    foreach (var callback in new[] { false, true })
    for (var repeat = 0; repeat < 3; repeat++)
    {
        var file = Path.Combine(Path.GetTempPath(), "litedb-handle-resources-" + Guid.NewGuid() + ".db");
        var settings = new LiteDB.Engine.EngineSettings { Filename = file,
            ReadTransform = callback ? (collection, value) => value : null };
        using (var seed = new LiteDatabase(file)) seed.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1 });
        using var shared = new SharedEngine(settings);
        using var db = new LiteDatabase(shared, disposeOnClose: false);
        var process = Process.GetCurrentProcess();
        object Sample(string phase)
        {
            process.Refresh();
            return new { phase, threads = process.Threads.Count, handles = process.HandleCount,
                managedBytes = GC.GetTotalMemory(false), workingSet = process.WorkingSet64 };
        }
        var before = Sample("idle-session");
        var owner = db.BeginTransaction();
        if (owner.GetCollection("rows").FindById(1) == null) throw new Exception("missing row");
        var active = Sample("active-handle");
        using var ready = new CountdownEvent(12);
        var errors = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
        var callers = Enumerable.Range(0, 12).Select(_ => new Thread(() =>
        {
            try
            {
                ready.Signal();
                using var tx = db.BeginTransaction();
                if (tx.GetCollection("rows").FindById(1) == null) throw new Exception("missing row");
                tx.Rollback();
            }
            catch (Exception error) { errors.Enqueue(error); }
        })).ToArray();
        foreach (var thread in callers) thread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(20))) throw new Exception("callers did not start");
        Thread.Sleep(100); // sampling window; deterministic pending-admission assertions live in tests
        var pending = Sample("twelve-waiting-callers");
        var clock = Stopwatch.StartNew();
        owner.Rollback();
        foreach (var thread in callers) if (!thread.Join(TimeSpan.FromSeconds(30))) throw new Exception("caller stuck");
        if (!errors.IsEmpty) throw new AggregateException(errors);
        var drained = Sample("all-handles-completed");
        db.Dispose();
        shared.Dispose();
        clock.Stop();
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var closed = Sample("closed-collected");
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { revision, operation = "resources", callback, repeat,
            before, active, pending, drained, closed, drainMs = clock.Elapsed.TotalMilliseconds }));
        using (var verify = new LiteDatabase(file)) if (verify.GetCollection("rows").Count() != 1) throw new Exception("incorrect cold state");
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(file), Path.GetFileName(file) + "*")) File.Delete(path);
    }
}
#endif
