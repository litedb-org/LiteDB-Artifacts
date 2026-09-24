using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;
using LiteDB.Engine;

// Like-for-like benchmark: one source, compiled against each LiteDB build.
// Usage: fbench <dir> <mode> <scenario> <n>
//   mode:     shared | coord (client of a separate coordinator process; improved build only) | direct
//   scenario: upd | ins | qry | mixed | scan | held | iter
// Internal:  fbench host <dir>
// Output (one line): mode scenario n total_ms ms_per_op p50_ms p99_ms   (or "blocked")
internal static class Program
{
    private const int Rows = 2000;
    private static readonly TimeSpan BlockedAfter = TimeSpan.FromSeconds(10);

    private static int Main(string[] args)
    {
        if (args[0] == "host") return Host(args[1]);
        var dir = args[0];
        var mode = args[1];
        var scenario = args[2];
        var n = int.Parse(args[3]);
        Directory.CreateDirectory(dir);
        Clean(dir);
        var file = Path.Combine(dir, "bench.db");
        using (var seed = new LiteDatabase(new ConnectionString { Filename = file }))
            seed.GetCollection("rows").InsertBulk(Enumerable.Range(1, Rows).Select(i => Row(i, 0)));

        Process host = null;
        if (mode == "coord")
        {
            host = Process.Start(new ProcessStartInfo("dotnet", $"\"{typeof(Program).Assembly.Location}\" host \"{dir}\"")
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false
            });
            if (host.StandardOutput.ReadLine() != "ready") throw new InvalidOperationException("host failed");
        }

        string result;
        try
        {
            result = Run(mode, file, scenario, n);
        }
        finally
        {
            if (host != null)
            {
                host.StandardInput.WriteLine("stop");
                if (!host.WaitForExit(30000)) host.Kill(true);
            }
        }
        Console.WriteLine($"{mode} {scenario} n={n} {result}");
        Clean(dir);
        return 0;
    }

    private static string Run(string mode, string file, string scenario, int n)
    {
        var lat = new List<long>(n);
        long total;
        using (var db = Open(mode, file))
        {
            var col = db.GetCollection("rows");
            void Timed(Action op)
            {
                var t = Stopwatch.GetTimestamp();
                op();
                lat.Add(Stopwatch.GetTimestamp() - t);
            }
            var start = Stopwatch.GetTimestamp();
            switch (scenario)
            {
                case "upd":
                    for (var i = 0; i < n; i++) Timed(() => col.Update(Row(i % Rows + 1, i)));
                    break;
                case "ins":
                    for (var i = 0; i < n; i++) Timed(() => col.Insert(Row(Rows + 1 + i, i)));
                    break;
                case "qry":
                    for (var i = 0; i < n; i++) Timed(() => col.FindById(i % Rows + 1));
                    break;
                case "mixed":
                    // One update per ten operations, the rest point reads.
                    for (var i = 0; i < n; i++)
                    {
                        if (i % 10 == 0) Timed(() => col.Update(Row(i % Rows + 1, i)));
                        else Timed(() => col.FindById(i % Rows + 1));
                    }
                    break;
                case "scan":
                    for (var i = 0; i < n; i++)
                        Timed(() => { if (col.FindAll().Count() != Rows) throw new InvalidOperationException("scan count"); });
                    break;
                case "iter":
                    // n updates while iterating a cursor of the same connection, on the same thread.
                    {
                        var count = 0;
                        foreach (var doc in col.FindAll())
                        {
                            if (count >= n) break;
                            doc["v"] = doc["v"].AsInt32 + 1;
                            Timed(() => col.Update(doc));
                            count++;
                        }
                        if (count != Math.Min(n, Rows)) throw new InvalidOperationException("iter count " + count);
                    }
                    break;
                case "held":
                    return Held(mode, file, col, n, lat);
                default:
                    throw new ArgumentException(scenario);
            }
            total = Stopwatch.GetTimestamp() - start;
        }
        return Format(total, lat);
    }

    private static string Held(string mode, string file, ILiteCollection<BsonDocument> col, int n, List<long> lat)
    {
        using var ready = new ManualResetEventSlim();
        using var stop = new ManualResetEventSlim();
        var reader = new Thread(() =>
        {
            using var rdb = Open(mode, file);
            using var cursor = rdb.GetCollection("rows").FindAll().GetEnumerator();
            cursor.MoveNext();
            ready.Set();
            stop.Wait();
        }) { IsBackground = true };
        reader.Start();
        ready.Wait();

        long total = 0;
        Exception error = null;
        var writer = new Thread(() =>
        {
            try
            {
                var start = Stopwatch.GetTimestamp();
                for (var i = 0; i < n; i++)
                {
                    var t = Stopwatch.GetTimestamp();
                    col.Update(Row(i % Rows + 1, i));
                    lat.Add(Stopwatch.GetTimestamp() - t);
                }
                total = Stopwatch.GetTimestamp() - start;
            }
            catch (Exception ex) { error = ex; }
        }) { IsBackground = true };
        writer.Start();
        if (!writer.Join(BlockedAfter))
        {
            // The writer waits for the reader (pre-stack shared mode). Report and leave without cleanup waits.
            Console.WriteLine($"{mode} held n={n} blocked after={BlockedAfter.TotalSeconds}s completed={lat.Count}");
            Console.Out.Flush();
            Environment.Exit(0);
        }
        stop.Set();
        reader.Join();
        if (error != null) throw error;
        return Format(total, lat);
    }

    private static string Format(long total, List<long> lat)
    {
        double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
        lat.Sort();
        var p50 = lat.Count > 0 ? Ms(lat[(int)(lat.Count * 0.50)]) : 0;
        var p99 = lat.Count > 0 ? Ms(lat[Math.Min(lat.Count - 1, (int)(lat.Count * 0.99))]) : 0;
        return $"total_ms={Ms(total):F1} ms_per_op={Ms(total) / Math.Max(1, lat.Count):F4} p50={p50:F4} p99={p99:F4}";
    }

    private static LiteDatabase Open(string mode, string file) => mode switch
    {
        "coord" => new LiteDatabase(CreateCoordinated(file)),
        "shared" => new LiteDatabase(new ConnectionString { Filename = file, Connection = ConnectionType.Shared }),
        "direct" => new LiteDatabase(new ConnectionString { Filename = file, Connection = ConnectionType.Direct }),
        _ => throw new ArgumentException(mode)
    };

    // Reflection keeps the source compilable against builds without the experimental coordinator.
    private static ILiteEngine CreateCoordinated(string file)
    {
        var type = typeof(LiteDatabase).Assembly.GetType("LiteDB.Engine.CoordinatedEngine")
            ?? throw new NotSupportedException("this build has no coordinator");
        return (ILiteEngine)Activator.CreateInstance(type, file);
    }

    private static int Host(string dir)
    {
        using var engine = CreateCoordinated(Path.Combine(dir, "bench.db"));
        var isCoordinator = (bool)engine.GetType().GetProperty("IsCoordinator").GetValue(engine);
        if (!isCoordinator) throw new InvalidOperationException("not coordinator");
        Console.WriteLine("ready");
        Console.ReadLine();
        return 0;
    }

    private static BsonDocument Row(int id, int v) => new BsonDocument { ["_id"] = id, ["v"] = v, ["pad"] = new string('x', 200) };

    private static void Clean(string dir)
    {
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        foreach (var d in Directory.GetDirectories(dir)) Directory.Delete(d, true);
    }
}
