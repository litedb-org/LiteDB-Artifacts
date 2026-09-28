using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using LiteDB;

// Black-box process-crash harness, compiled against LiteDB 5.0.21 (package) and HEAD.
// child:  crash child <db> <seed> <opts>   - performs random committed operations, prints ACK <n>
// parent: crash parent <db> <rounds> <seed> <opts> [minMs] [maxMs]
public static class CrashHarness
{
    const int Colls = 3;

    public sealed class Doc { public int V; public int PadLen; public int PadSeed; }

    public sealed class Model
    {
        public Dictionary<int, Doc>[] C = Enumerable.Range(0, Colls).Select(_ => new Dictionary<int, Doc>()).ToArray();
        public int[] Next = new int[Colls];
        public Model Clone()
        {
            var m = new Model();
            for (var i = 0; i < Colls; i++)
            {
                m.C[i] = C[i].ToDictionary(x => x.Key, x => new Doc { V = x.Value.V, PadLen = x.Value.PadLen, PadSeed = x.Value.PadSeed });
                m.Next[i] = Next[i];
            }
            return m;
        }
        public string Hash()
        {
            var sb = new StringBuilder();
            for (var i = 0; i < Colls; i++)
                foreach (var kv in C[i].OrderBy(x => x.Key))
                    sb.Append(i).Append(':').Append(kv.Key).Append('=').Append(kv.Value.V).Append('/').Append(kv.Value.PadLen).Append('/').Append(kv.Value.PadSeed).Append(';');
            return sb.ToString();
        }
    }

    public static string Pad(int len, int seed)
    {
        var chars = new char[len];
        var r = new Random(seed);
        for (var i = 0; i < len; i++) chars[i] = (char)('a' + r.Next(26));
        return new string(chars);
    }

    static BsonDocument ToBson(int id, Doc d) => new BsonDocument
    {
        ["_id"] = id, ["v"] = d.V, ["pl"] = d.PadLen, ["ps"] = d.PadSeed, ["pad"] = Pad(d.PadLen, d.PadSeed)
    };

    // One logical op = list of primitive actions; tx ops may roll back.
    public sealed class Op
    {
        public string Kind; // single, tx, bigtx, checkpoint
        public bool Rollback;
        public List<(int coll, char act, int id, Doc doc)> Actions = new List<(int, char, int, Doc)>();
    }

    static Doc RandDoc(Random r)
    {
        var len = r.Next(100) < 5 ? r.Next(9000, 40000) : r.Next(0, 3000);
        return new Doc { V = r.Next(1000), PadLen = len, PadSeed = r.Next() };
    }

    static (int, char, int, Doc) RandAction(Random r, Model m)
    {
        var c = r.Next(Colls);
        var k = r.Next(100);
        if (m.C[c].Count == 0 || k < 45)
        {
            var id = m.Next[c]++;
            var d = RandDoc(r);
            m.C[c][id] = d;
            return (c, 'i', id, d);
        }
        var ids = m.C[c].Keys.OrderBy(x => x).ToList();
        var pick = ids[r.Next(ids.Count)];
        if (k < 80)
        {
            var d = RandDoc(r);
            m.C[c][pick] = d;
            return (c, 'u', pick, d);
        }
        m.C[c].Remove(pick);
        return (c, 'd', pick, null);
    }

    // Generates the next op and returns the model after it (input model is not mutated).
    public static (Op op, Model after) Generate(Random r, Model before)
    {
        var m = before.Clone();
        var op = new Op();
        var k = r.Next(100);
        if (k < 3)
        {
            op.Kind = "checkpoint";
            return (op, m);
        }
        if (k < 8)
        {
            // big transaction: forces safepoints (unconfirmed pages in WAL), re-dirties pages after safepoint
            op.Kind = "bigtx";
            var n = r.Next(1200, 2500);
            for (var i = 0; i < n; i++)
            {
                var c = 0;
                var big = m.C[c].Count > 3000;
                if (big && r.Next(100) < 25 && m.C[c].Count > 0)
                {
                    var ids0 = m.C[c].Keys.OrderBy(x => x).ToList();
                    var del = ids0[r.Next(ids0.Count)];
                    m.C[c].Remove(del);
                    op.Actions.Add((c, 'd', del, null));
                }
                else if (m.C[c].Count > 0 && r.Next(100) < (big ? 90 : 40))
                {
                    var ids = m.C[c].Keys.OrderBy(x => x).ToList();
                    var pick = ids[r.Next(ids.Count)];
                    var d = new Doc { V = r.Next(1000), PadLen = r.Next(1000, 6000), PadSeed = r.Next() };
                    m.C[c][pick] = d;
                    op.Actions.Add((c, 'u', pick, d));
                }
                else
                {
                    var id = m.Next[c]++;
                    var d = new Doc { V = r.Next(1000), PadLen = r.Next(1000, 6000), PadSeed = r.Next() };
                    m.C[c][id] = d;
                    op.Actions.Add((c, 'i', id, d));
                }
            }
            // delete a few too
            for (var i = 0; i < 50 && m.C[0].Count > 0; i++)
            {
                var ids = m.C[0].Keys.OrderBy(x => x).ToList();
                var pick = ids[r.Next(ids.Count)];
                m.C[0].Remove(pick);
                op.Actions.Add((0, 'd', pick, null));
            }
            op.Rollback = r.Next(100) < 30;
            return (op, op.Rollback ? before.Clone() : m);
        }
        if (k < 30)
        {
            op.Kind = "tx";
            var n = r.Next(1, 40);
            for (var i = 0; i < n; i++) op.Actions.Add(RandAction(r, m));
            op.Rollback = r.Next(100) < 20;
            return (op, op.Rollback ? before.Clone() : m);
        }
        op.Kind = "single";
        op.Actions.Add(RandAction(r, m));
        return (op, m);
    }

    static void Apply(LiteDatabase db, (int coll, char act, int id, Doc doc) a)
    {
        var col = db.GetCollection("c" + a.coll);
        switch (a.act)
        {
            case 'i': col.Insert(ToBson(a.id, a.doc)); break;
            case 'u': if (!col.Update(ToBson(a.id, a.doc))) throw new Exception("update missed " + a.coll + ":" + a.id); break;
            case 'd': if (!col.Delete(a.id)) throw new Exception("delete missed " + a.coll + ":" + a.id); break;
        }
    }

    public static void Execute(LiteDatabase db, Op op)
    {
        if (op.Kind == "checkpoint") { db.Checkpoint(); return; }
        if (op.Kind == "single") { Apply(db, op.Actions[0]); return; }
        if (!db.BeginTrans()) throw new Exception("BeginTrans false");
        foreach (var a in op.Actions) Apply(db, a);
        if (op.Rollback) { if (!db.Rollback()) throw new Exception("Rollback false"); }
        else { if (!db.Commit()) throw new Exception("Commit false"); }
    }

    public static string Conn(string db, string opts) => "Filename=" + db + (string.IsNullOrEmpty(opts) || opts == "-" ? "" : ";" + opts);

    public static Model Read(LiteDatabase db, bool checkIndex, List<string> problems)
    {
        var m = new Model();
        for (var i = 0; i < Colls; i++)
        {
            var col = db.GetCollection("c" + i);
            foreach (var d in col.FindAll())
            {
                var id = d["_id"].AsInt32;
                var doc = new Doc { V = d["v"].AsInt32, PadLen = d["pl"].AsInt32, PadSeed = d["ps"].AsInt32 };
                var pad = d["pad"].AsString;
                if (pad.Length != doc.PadLen || pad != Pad(doc.PadLen, doc.PadSeed)) problems.Add($"c{i}:{id} pad content mismatch");
                if (m.C[i].ContainsKey(id)) problems.Add($"c{i}:{id} duplicate in scan");
                m.C[i][id] = doc;
                m.Next[i] = Math.Max(m.Next[i], id + 1);
            }
            if (checkIndex)
            {
                var viaIndex = col.Find(Query.All("v")).Select(x => (x["_id"].AsInt32, x["v"].AsInt32)).ToList();
                var last = int.MinValue;
                foreach (var (id, v) in viaIndex)
                {
                    if (v < last) problems.Add($"c{i}: index order broken at {id}");
                    last = v;
                    if (!m.C[i].TryGetValue(id, out var doc) || doc.V != v) problems.Add($"c{i}:{id} index->doc mismatch");
                }
                if (viaIndex.Count != m.C[i].Count) problems.Add($"c{i}: index count {viaIndex.Count} != scan {m.C[i].Count}");
                var ids = col.Find(Query.All("_id")).Count();
                if (ids != m.C[i].Count) problems.Add($"c{i}: pk index count {ids} != scan {m.C[i].Count}");
            }
        }
        return m;
    }

    public static string Sha(string s) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(s)));

    // chain: several killed children back-to-back without a verifying open between them
    public static int Chain(string self, string dbPath, int rounds, int chain, int seed, string opts, int minMs, int maxMs)
    {
        var rr = new Random(seed ^ 0x77);
        foreach (var f in new[] { dbPath, dbPath.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        using (var db = new LiteDatabase(Conn(dbPath, opts))) { }
        var candidates = new List<Model> { new Model() };
        for (var round = 0; round < rounds; round++)
        {
            var log = new StringBuilder();
            for (var c = 0; c < chain; c++)
            {
                var childSeed = rr.Next();
                var psi = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
                psi.ArgumentList.Add(self); psi.ArgumentList.Add("child"); psi.ArgumentList.Add(dbPath); psi.ArgumentList.Add(childSeed.ToString()); psi.ArgumentList.Add(opts);
                var acked = 0; string start = null;
                var err = new StringBuilder();
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data == null) return;
                        if (e.Data.StartsWith("START ")) Volatile.Write(ref start, e.Data.Substring(6));
                        else if (e.Data.StartsWith("ACK ")) Volatile.Write(ref acked, int.Parse(e.Data.Substring(4)));
                    };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    var delay = rr.Next(minMs, maxMs);
                    if (!p.WaitForExit(delay)) { p.Kill(true); }
                    p.WaitForExit();
                    if (p.ExitCode != 137 && p.ExitCode != -1 && err.Length > 0) { Console.WriteLine($"round {round} child {c} exited {p.ExitCode}: {err.ToString().Split('\n')[0]}"); return 1; }
                }
                var st = Volatile.Read(ref start);
                if (st == null) { log.Append($"[killed-before-start]"); continue; }
                var baseModel = candidates.FirstOrDefault(m => Sha(m.Hash()) == st);
                if (baseModel == null) { Console.WriteLine($"round {round} child {c}: START state matches no candidate (had {candidates.Count}) acked={acked}"); return 1; }
                var n = Volatile.Read(ref acked);
                var r = new Random(childSeed);
                baseModel = baseModel.Clone();
                for (var ci = 0; ci < Colls; ci++) baseModel.Next[ci] = baseModel.C[ci].Count == 0 ? 0 : baseModel.C[ci].Keys.Max() + 1;
                var cur = baseModel; var ms = new List<Model> { cur };
                for (var i = 0; i < n + 1; i++) { var (_, after) = Generate(r, cur); ms.Add(after); cur = after; }
                candidates = new List<Model> { ms[n], ms[n + 1] };
                log.Append($"[ack {n}]");
            }
            var problems = new List<string>();
            Model observed;
            try { using (var db = new LiteDatabase(Conn(dbPath, opts))) observed = Read(db, true, problems); }
            catch (Exception ex) { Console.WriteLine($"round {round}: OPEN/READ FAILED {log}: {ex}"); return 1; }
            var h = observed.Hash();
            var ok = candidates.Any(m => m.Hash() == h);
            if (!ok) problems.Add("state matches no candidate");
            Console.WriteLine($"round {round} {log} -> {(problems.Count == 0 ? "ok" : "FAIL")} docs={observed.C.Sum(x => x.Count)}" + (problems.Count > 0 ? "\n   " + string.Join("\n   ", problems.Take(10)) : ""));
            if (problems.Count > 0) return 1;
            candidates = new List<Model> { observed };
        }
        Console.WriteLine("DONE");
        return 0;
    }

    public static int Child(string dbPath, int seed, string opts)
    {
        using (var db = new LiteDatabase(Conn(dbPath, opts)))
        {
            for (var i = 0; i < Colls; i++) db.GetCollection("c" + i).EnsureIndex("v");
            var problems = new List<string>();
            var model = Read(db, false, problems);
            var r = new Random(seed);
            var n = 0;
            Console.Out.WriteLine("START " + Sha(model.Hash()));
            Console.Out.WriteLine("READY");
            Console.Out.Flush();
            while (true)
            {
                var (op, after) = Generate(r, model);
                Execute(db, op);
                model = after;
                n++;
                Console.Out.WriteLine("ACK " + n);
                Console.Out.Flush();
            }
        }
    }

    public static int Parent(string self, string dbPath, int rounds, int seed, string opts, int minMs, int maxMs)
    {
        var rr = new Random(seed ^ 0x5eed);
        foreach (var f in new[] { dbPath, dbPath.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        Model start;
        using (var db = new LiteDatabase(Conn(dbPath, opts))) { start = new Model(); }
        var failures = 0;
        for (var round = 0; round < rounds; round++)
        {
            var childSeed = rr.Next();
            var psi = new ProcessStartInfo(self.EndsWith(".dll") ? "dotnet" : self)
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false
            };
            if (self.EndsWith(".dll")) psi.ArgumentList.Add(self);
            psi.ArgumentList.Add("child"); psi.ArgumentList.Add(dbPath); psi.ArgumentList.Add(childSeed.ToString()); psi.ArgumentList.Add(opts);
            var acked = 0;
            var ready = new ManualResetEventSlim();
            var err = new StringBuilder();
            using (var p = Process.Start(psi))
            {
                p.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null) return;
                    if (e.Data == "READY") ready.Set();
                    else if (e.Data.StartsWith("ACK ")) Volatile.Write(ref acked, int.Parse(e.Data.Substring(4)));
                };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (err) err.AppendLine(e.Data); };
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                if (!ready.Wait(60000)) { Console.WriteLine($"round {round}: child not ready: {err}"); p.Kill(true); return 2; }
                var delay = rr.Next(minMs, maxMs);
                var exited = p.WaitForExit(delay);
                if (!exited) { p.Kill(true); p.WaitForExit(); }
                else Console.WriteLine($"round {round}: child exited early code={p.ExitCode}: {err}");
                p.WaitForExit();
            }
            var n = Volatile.Read(ref acked);
            // regenerate the child's op stream
            var r = new Random(childSeed);
            var models = new List<Model> { start };
            var cur = start;
            for (var i = 0; i < n + 1; i++) { var (_, after) = Generate(r, cur); models.Add(after); cur = after; }
            var problems = new List<string>();
            Model observed;
            try
            {
                using (var db = new LiteDatabase(Conn(dbPath, opts)))
                {
                    observed = Read(db, true, problems);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"round {round} seed {childSeed} acked {n}: OPEN/READ FAILED: {ex}");
                return 1;
            }
            var h = observed.Hash();
            string verdict;
            if (h == models[n].Hash()) verdict = "acked";
            else if (h == models[n + 1].Hash()) verdict = "acked+inflight";
            else
            {
                verdict = "MISMATCH";
                // find nearest earlier state
                var match = models.FindLastIndex(x => x.Hash() == h);
                problems.Add($"state matches none of models[{n}]/[{n + 1}] (matches index {match})");
                for (var i = 0; i < Colls; i++)
                {
                    var exp = models[n].C[i];
                    var got = observed.C[i];
                    var missing = exp.Keys.Except(got.Keys).Take(5).ToList();
                    var extra = got.Keys.Except(exp.Keys).Take(5).ToList();
                    var diff = exp.Keys.Intersect(got.Keys).Where(k => exp[k].V != got[k].V || exp[k].PadSeed != got[k].PadSeed).Take(5).ToList();
                    if (missing.Count + extra.Count + diff.Count > 0)
                        problems.Add($"c{i}: exp {exp.Count} got {got.Count} missing[{string.Join(",", missing)}] extra[{string.Join(",", extra)}] changed[{string.Join(",", diff)}]");
                }
            }
            var dbLen = new FileInfo(dbPath).Length;
            var logPath = dbPath.Replace(".db", "-log.db");
            var logLen = File.Exists(logPath) ? new FileInfo(logPath).Length : -1;
            Console.WriteLine($"round {round} seed {childSeed} acked {n} -> {verdict} docs={observed.C.Sum(x => x.Count)} data={dbLen} log={logLen}" + (problems.Count > 0 ? "\n   " + string.Join("\n   ", problems) : ""));
            if (problems.Count > 0) { failures++; return 1; }
            start = observed;
        }
        Console.WriteLine($"DONE failures={failures}");
        return failures == 0 ? 0 : 1;
    }

    public static int Main(string[] args)
    {
        if (args[0] == "scen") return Scen.Run(args);
        if (args[0] == "salvmake") return Salvage.Make(args[1]);
        if (args[0] == "salvuse") return Salvage.Use(args[1], args[2], args[3] == "rebuild");
        if (args[0] == "small") return Small.Run(args[1], args[2]);
        if (args[0] == "slowreader") return SlowReader.Run(args[1], args[2], int.Parse(args[3]));
        if (args[0] == "trace") return Trace.Run(args[1], args[2]);
        if (args[0] == "smallcrash") return SmallCrash.Run(args[1]);
        if (args[0] == "tornsp") return TornSafepoint.Run(args[1] == "inplace");
        if (args[0] == "dfmake") return DropIdxFuzz.Make(args[1], args[2], args[3]);
        if (args[0] == "dfuse") return DropIdxFuzz.Use(args[1], args[2]);
        if (args[0] == "dropafter") return DropIdxAfter.Run(args[1], args[2]);
        if (args[0] == "dropmake") return DropIdx.Make(args[1], args[2], args[3]);
        if (args[0] == "dropuse") return DropIdx.Use(args[1]);
        if (args[0] == "streamdispose") return StreamDispose.Run(args[1]);
        if (args[0] == "bank") return Bank.Run(args[1], args[2], int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), int.Parse(args[6]));
        if (args[0] == "perf") return Perf.Run(args[1], args[2], int.Parse(args[3]));
        if (args[0] == "diskfull") return DiskFull.Run(args[1], args[2], args[3], long.Parse(args[4]));
        if (args[0] == "sworker") return SharedStress.Worker(args[1], int.Parse(args[2]), int.Parse(args[3]), args[4]);
        if (args[0] == "sparent") return SharedStress.Parent(args[1], int.Parse(args[2]), int.Parse(args[3]), args[4], int.Parse(args[5]));
        if (args[0] == "mkseed") return MigrateHarness.MkSeed(args[1], int.Parse(args[2]), int.Parse(args[3]), args[4]);
        if (args[0] == "open") return MigrateHarness.Open(args[1], args[2]);
        if (args[0] == "migrate") return MigrateHarness.Parent(args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), int.Parse(args[5]), args[6]);
        if (args[0] == "chain") return Chain(typeof(CrashHarness).Assembly.Location, args[1], int.Parse(args[2]), int.Parse(args[3]), int.Parse(args[4]), args[5], int.Parse(args[6]), int.Parse(args[7]));
        if (args[0] == "child") return Child(args[1], int.Parse(args[2]), args[3]);
        if (args[0] == "parent")
        {
            var self = Environment.ProcessPath;
            var dll = typeof(CrashHarness).Assembly.Location;
            return Parent(dll, args[1], int.Parse(args[2]), int.Parse(args[3]), args[4],
                args.Length > 5 ? int.Parse(args[5]) : 200, args.Length > 6 ? int.Parse(args[6]) : 3000);
        }
        return 3;
    }
}
