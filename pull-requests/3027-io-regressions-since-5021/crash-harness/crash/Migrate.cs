using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using LiteDB;

public static class MigrateHarness
{
    public static CrashHarness.Model Seed(int n, int seed)
    {
        var r = new Random(seed);
        var m = new CrashHarness.Model();
        for (var c = 0; c < 3; c++)
            for (var i = 0; i < n; i++)
            {
                var len = r.Next(100) < 3 ? r.Next(9000, 20000) : r.Next(0, 800);
                m.C[c][i] = new CrashHarness.Doc { V = r.Next(1000), PadLen = len, PadSeed = r.Next() };
                m.Next[c] = i + 1;
            }
        return m;
    }

    public static int MkSeed(string db, int n, int seed, string opts)
    {
        var m = Seed(n, seed);
        using (var d = new LiteDatabase(CrashHarness.Conn(db, opts)))
        {
            for (var c = 0; c < 3; c++)
            {
                var col = d.GetCollection("c" + c);
                col.EnsureIndex("v");
                col.EnsureIndex("x", "$.pl * 1.5 + $.v");
                col.InsertBulk(m.C[c].Select(kv => new BsonDocument { ["_id"] = kv.Key, ["v"] = kv.Value.V, ["pl"] = kv.Value.PadLen, ["ps"] = kv.Value.PadSeed, ["pad"] = CrashHarness.Pad(kv.Value.PadLen, kv.Value.PadSeed) }));
            }
        }
        Console.WriteLine("seeded " + new FileInfo(db).Length);
        return 0;
    }

    public static int Open(string db, string opts)
    {
        var sw = Stopwatch.StartNew();
        using (var d = new LiteDatabase(CrashHarness.Conn(db, opts)))
        {
            Console.WriteLine("OPENED " + sw.ElapsedMilliseconds);
            Console.Out.Flush();
            var n = d.GetCollection("c0").Count();
            Console.WriteLine("COUNT " + n + " " + sw.ElapsedMilliseconds);
        }
        Console.WriteLine("CLOSED " + sw.ElapsedMilliseconds);
        return 0;
    }

    static void Kill(string dll, string db, string opts, int ms, out string outp)
    {
        var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(dll); psi.ArgumentList.Add("open"); psi.ArgumentList.Add(db); psi.ArgumentList.Add(opts);
        using (var p = Process.Start(psi))
        {
            var o = new System.Text.StringBuilder();
            p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (o) o.AppendLine(e.Data); };
            p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (o) o.AppendLine("ERR " + e.Data); };
            p.BeginOutputReadLine(); p.BeginErrorReadLine();
            if (!p.WaitForExit(ms)) { p.Kill(true); }
            p.WaitForExit();
            lock (o) outp = o.ToString();
        }
    }

    public static int Parent(string pristine, int n, int seed, int trials, int maxMs, string opts)
    {
        var dll = typeof(MigrateHarness).Assembly.Location;
        var expected = Seed(n, seed).Hash();
        var dir = Path.GetDirectoryName(pristine);
        var rr = new Random(seed * 7 + 1);
        var failures = 0;
        for (var t = 0; t < trials; t++)
        {
            var db = Path.Combine(dir, "trial.db");
            foreach (var f in Directory.GetFiles(dir, "trial*")) File.Delete(f);
            File.Copy(pristine, db);
            var p0 = pristine.Replace(".db", "-log.db");
            if (File.Exists(p0)) File.Copy(p0, db.Replace(".db", "-log.db"));
            var kills = new List<int>();
            var outs = "";
            var k = rr.Next(1, 4);
            for (var i = 0; i < k; i++)
            {
                var ms = rr.Next(50, maxMs);
                kills.Add(ms);
                Kill(dll, db, opts, ms, out var o);
                outs += o;
            }
            var problems = new List<string>();
            string verdict;
            try
            {
                using (var d = new LiteDatabase(CrashHarness.Conn(db, opts)))
                {
                    var m = CrashHarness.Read(d, true, problems);
                    // also check expression index
                    for (var c = 0; c < 3; c++)
                    {
                        var cnt = d.GetCollection("c" + c).Find(Query.All("x")).Count();
                        if (cnt != m.C[c].Count) problems.Add($"c{c}: x index count {cnt} != {m.C[c].Count}");
                    }
                    verdict = m.Hash() == expected ? "ok" : "DATA MISMATCH";
                    if (verdict != "ok") problems.Add(verdict);
                }
                using (var d = new LiteDatabase(CrashHarness.Conn(db, opts)))
                {
                    var m2 = CrashHarness.Read(d, true, problems);
                    if (m2.Hash() != expected) problems.Add("second reopen mismatch");
                }
            }
            catch (Exception ex) { problems.Add("OPEN FAILED " + ex.GetType().Name + ": " + ex.Message); verdict = "FAIL"; }
            var files = string.Join(" ", Directory.GetFiles(dir, "trial*").Select(f => Path.GetFileName(f) + "=" + new FileInfo(f).Length));
            Console.WriteLine($"trial {t} kills [{string.Join(",", kills)}] -> {(problems.Count == 0 ? "ok" : "FAIL")} {files} | child: {outs.Replace("\n", " ").Trim()}" + (problems.Count > 0 ? "\n   " + string.Join("\n   ", problems.Take(10)) : ""));
            if (problems.Count > 0) failures++;
        }
        Console.WriteLine("DONE failures=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
