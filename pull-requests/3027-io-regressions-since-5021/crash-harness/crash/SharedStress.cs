using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;

public static class SharedStress
{
    // worker: increments counter + inserts log doc per iteration, in one transaction
    public static int Worker(string db, int id, int iters, string opts)
    {
        var r = new Random(id * 31 + 7);
        using (var d = new LiteDatabase(CrashHarness.Conn(db, "Connection=shared" + (opts == "-" ? "" : ";" + opts))))
        {
            var cnt = d.GetCollection("counter");
            var log = d.GetCollection("log");
            for (var i = 0; i < iters; i++)
            {
                var k = r.Next(100);
                if (k < 5) { d.Checkpoint(); }
                if (k < 20)
                {
                    // reader: invariant check inside a read
                    var c = cnt.FindById(1);
                    var n = log.Count();
                    var cv = c == null ? 0 : c["n"].AsInt32;
                    if (cv != n) { Console.WriteLine($"INVARIANT counter={cv} log={n}"); }
                }
                if (!d.BeginTrans()) throw new Exception("begin");
                var cur = cnt.FindById(1);
                var v = cur == null ? 0 : cur["n"].AsInt32;
                cnt.Upsert(new BsonDocument { ["_id"] = 1, ["n"] = v + 1 });
                log.Insert(new BsonDocument { ["_id"] = id * 1000000 + i, ["w"] = id, ["pad"] = new string('p', r.Next(0, 4000)) });
                if (r.Next(100) < 10) { d.Rollback(); i--; continue; }
                d.Commit();
                Console.WriteLine("ACK " + i);
            }
        }
        return 0;
    }

    public static int Parent(string db, int procs, int iters, string opts, int killMs)
    {
        foreach (var f in Directory.GetFiles(Path.GetDirectoryName(db), Path.GetFileNameWithoutExtension(db) + "*")) File.Delete(f);
        var dll = typeof(SharedStress).Assembly.Location;
        var ps = new List<Process>();
        var outs = new List<System.Text.StringBuilder>();
        for (var p = 0; p < procs; p++)
        {
            var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
            foreach (var a in new[] { dll, "sworker", db, p.ToString(), iters.ToString(), opts }) psi.ArgumentList.Add(a);
            var proc = Process.Start(psi);
            var sb = new System.Text.StringBuilder();
            proc.OutputDataReceived += (s, e) => { if (e.Data != null && !e.Data.StartsWith("ACK")) lock (sb) sb.AppendLine(e.Data); };
            proc.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine("ERR " + e.Data); };
            proc.BeginOutputReadLine(); proc.BeginErrorReadLine();
            ps.Add(proc); outs.Add(sb);
        }
        if (killMs > 0)
        {
            Thread.Sleep(killMs);
            ps[0].Kill(true);
            Console.WriteLine("killed worker 0");
        }
        var sw = Stopwatch.StartNew();
        foreach (var p in ps) if (!p.WaitForExit(600000)) { Console.WriteLine("HANG worker " + ps.IndexOf(p)); p.Kill(true); }
        Console.WriteLine($"workers done in {sw.ElapsedMilliseconds}ms; exit codes {string.Join(",", ps.Select(x => x.ExitCode))}");
        for (var i = 0; i < procs; i++) if (outs[i].Length > 0) Console.WriteLine($"worker {i} output:\n{string.Join("\n", outs[i].ToString().Split('\n').Take(12))}");
        using (var d = new LiteDatabase(CrashHarness.Conn(db, opts)))
        {
            var c = d.GetCollection("counter").FindById(1)?["n"].AsInt32 ?? 0;
            var logs = d.GetCollection("log").FindAll().ToList();
            var perW = logs.GroupBy(x => x["w"].AsInt32).ToDictionary(g => g.Key, g => g.Count());
            Console.WriteLine($"counter={c} log={logs.Count} expectedMax={procs * iters} perWorker=[{string.Join(",", perW.OrderBy(x => x.Key).Select(x => x.Key + ":" + x.Value))}]");
            var ok = c == logs.Count && (killMs > 0 || c == procs * iters);
            Console.WriteLine(ok ? "RESULT OK" : "RESULT FAIL");
            return ok ? 0 : 1;
        }
    }
}
