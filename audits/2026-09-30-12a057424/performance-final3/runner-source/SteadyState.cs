using System.Diagnostics;
using System.Security.Cryptography;
using JsonSerializer = System.Text.Json.JsonSerializer;
using LiteDB;

internal static class SteadyState
{
    internal static void Run(string[] args)
    {
        // One scenario per fresh process. The external driver alternates build order.
        var revision = args[0];
        var shared = args[2] == "shared";
        var mode = args[3];
        var operation = args[4];
        var reads = args.Length > 5 ? int.Parse(args[5]) : 1;
        var warmupSeconds = args.Length > 6 ? int.Parse(args[6]) : 10;
        var windows = args.Length > 7 ? int.Parse(args[7]) : 10;
        if (reads < 0 || warmupSeconds < 0 || windows < 1) throw new ArgumentException("Invalid measurement dimensions");
        var file = Path.Combine(Path.GetTempPath(), "litedb-handle-steady-" + Guid.NewGuid() + ".db");
        var cs = new ConnectionString { Filename = file, Connection = shared ? ConnectionType.Shared : ConnectionType.Direct,
            DurableCommits = true };
        using (var seed = new LiteDatabase(cs))
        {
            seed.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1, ["value"] = 42 });
            seed.GetCollection("rows").EnsureIndex("value");
            seed.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 9 });
            seed.Checkpoint();
        }
        var dll = typeof(LiteDatabase).Assembly.Location;
        Console.WriteLine(JsonSerializer.Serialize(new { phase = "metadata", revision, shared, mode, operation, reads,
            warmupSeconds, windows, runtime = Environment.Version.ToString(), os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
            architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            tiered = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"), dll,
            sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))).ToLowerInvariant() }));
        using (var db = new LiteDatabase(cs))
        {
            // Allocate harness buffers before warmup/counters, never inside an allocation window.
            var samples = new double[250000];
            var clock = Stopwatch.StartNew();
            while (clock.Elapsed.TotalSeconds < warmupSeconds) Perform(db, cs, mode, operation, reads);
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            using var process = Process.GetCurrentProcess();
            for (var window = 0; window < windows; window++)
            {
                process.Refresh();
                var beforeThreads = process.Threads.Count;
                var cpu = process.TotalProcessorTime;
                var allocated = GC.GetTotalAllocatedBytes(true);
                var count = 0;
                var start = Stopwatch.GetTimestamp();
                var end = start + Stopwatch.Frequency;
                long now;
                do
                {
                    var tick = Stopwatch.GetTimestamp();
                    Perform(db, cs, mode, operation, reads);
                    now = Stopwatch.GetTimestamp();
                    if (count < samples.Length) samples[count] = (now - tick) * 1000000d / Stopwatch.Frequency;
                    count++;
                } while (now < end);
                var elapsed = (now - start) / (double)Stopwatch.Frequency;
                allocated = GC.GetTotalAllocatedBytes(true) - allocated;
                process.Refresh();
                var cpuMs = (process.TotalProcessorTime - cpu).TotalMilliseconds;
                var sampled = Math.Min(count, samples.Length);
                Array.Sort(samples, 0, sampled);
                Console.WriteLine(JsonSerializer.Serialize(new { phase = "window", revision, shared, mode, operation, reads, window,
                    count, elapsed, opsPerSecond = count / elapsed, readsPerSecond = count * reads / elapsed, sampled,
                    p50us = samples[sampled / 2], p95us = samples[(int)(sampled * .95)], p99us = samples[(int)(sampled * .99)],
                    bytesPerOp = allocated / (double)count, cpuMs, beforeThreads, afterThreads = process.Threads.Count,
                    handles = process.HandleCount }));
            }
            GC.KeepAlive(db);
        }
        using (var verify = new LiteDatabase(cs))
        {
            if (verify.GetCollection("rows").Count() != 1 || verify.GetCollection("rows").FindById(1)["value"] != 42 ||
                verify.GetCollection("rows").Find(Query.EQ("value", 42)).Count() != 1 ||
                verify.GetCollection("sentinel").FindById(9) == null) throw new Exception("Incorrect cold state");
        }
        foreach (var path in Directory.GetFiles(Path.GetDirectoryName(file)!, Path.GetFileName(file) + "*")) File.Delete(path);
        Console.WriteLine(JsonSerializer.Serialize(new { phase = "verified", revision }));
    }

    private static void Perform(LiteDatabase db, ConnectionString cs, string mode, string operation, int reads)
    {
        if (operation == "open")
        {
            using var attached = new LiteDatabase(cs);
            if (attached.GetCollection("rows").Count() != 1) throw new Exception("Incorrect attached state");
            return;
        }
#if HANDLES
        if (mode == "handle")
        {
            using var tx = db.BeginTransaction();
            Read(tx.GetCollection("rows"), reads);
            tx.Commit();
            return;
        }
#endif
        if (mode == "legacy") db.BeginTrans();
        else if (mode != "ordinary") throw new ArgumentException("Unknown mode");
        Read(db.GetCollection("rows"), reads);
        if (mode == "legacy") db.Commit();
    }

    private static void Read(ILiteCollection<BsonDocument> rows, int count)
    {
        for (var i = 0; i < count; i++)
            if (rows.FindById(1)["value"] != 42) throw new Exception("Incorrect point read");
    }
}
