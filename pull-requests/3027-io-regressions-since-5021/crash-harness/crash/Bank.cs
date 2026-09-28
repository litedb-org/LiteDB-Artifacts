using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

public static class Bank
{
    public static int Run(string path, string opts, int seconds, int accounts, int writers, int readers)
    {
        foreach (var f in new[] { path, path.Replace(".db", "-log.db") }) if (File.Exists(f)) File.Delete(f);
        const long Initial = 1000;
        var errors = new System.Collections.Concurrent.ConcurrentQueue<string>();
        long commits = 0, rollbacks = 0, reads = 0, checkpoints = 0;
        using (var db = new LiteDatabase(CrashHarness.Conn(path, opts)))
        {
            var col = db.GetCollection("acc");
            col.EnsureIndex("bal");
            db.GetCollection("lock").Insert(new BsonDocument { ["_id"] = 1, ["n"] = 0 });
            col.InsertBulk(Enumerable.Range(0, accounts).Select(i => new BsonDocument { ["_id"] = i, ["bal"] = Initial, ["pad"] = new string('p', 100) }));
            var total = Initial * accounts;
            var stop = DateTime.UtcNow.AddSeconds(seconds);
            var tasks = new List<Task>();
            for (var w = 0; w < writers; w++)
            {
                var seed = w;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    var r = new Random(seed);
                    while (DateTime.UtcNow < stop && errors.Count == 0)
                    {
                        try
                        {
                            if (!db.BeginTrans()) throw new Exception("begin false");
                            col.Update(new BsonDocument { ["_id"] = -5, ["bal"] = 0L, ["pad"] = "" });
                            var n = r.Next(1, 6);
                            for (var k = 0; k < n; k++)
                            {
                                var a = r.Next(accounts); var b = r.Next(accounts);
                                if (a == b) continue;
                                var da = col.FindById(a); var dbb = col.FindById(b);
                                var amt = r.Next(0, 50);
                                da["bal"] = da["bal"].AsInt64 - amt; dbb["bal"] = dbb["bal"].AsInt64 + amt;
                                da["pad"] = new string('a', r.Next(10, r.Next(100) < 5 ? 20000 : 3000));
                                dbb["pad"] = new string('b', r.Next(10, 3000));
                                col.Update(da); col.Update(dbb);
                            }
                            if (r.Next(100) < 15) { db.Rollback(); Interlocked.Increment(ref rollbacks); }
                            else { db.Commit(); Interlocked.Increment(ref commits); }
                        }
                        catch (LiteException ex) when (ex.ErrorCode == LiteException.LOCK_TIMEOUT) { try { db.Rollback(); } catch { } }
                        catch (Exception ex) { errors.Enqueue("writer: " + ex); try { db.Rollback(); } catch { } }
                    }
                }, TaskCreationOptions.LongRunning));
            }
            for (var q = 0; q < readers; q++)
            {
                var seed = 100 + q;
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    var r = new Random(seed);
                    while (DateTime.UtcNow < stop && errors.Count == 0)
                    {
                        try
                        {
                            long sum = 0; int cnt = 0;
                            IEnumerable<BsonDocument> src = r.Next(2) == 0 ? col.FindAll() : col.Find(Query.All("bal"));
                            long last = long.MinValue; var viaIdx = src is not null && false;
                            foreach (var d in src) { sum += d["bal"].AsInt64; cnt++; if (d["pad"].AsString.Any(ch => ch != d["pad"].AsString[0])) errors.Enqueue("torn pad " + d["_id"]); }
                            if (sum != total || cnt != accounts) errors.Enqueue($"reader: sum={sum} cnt={cnt} expected {total}/{accounts}");
                            Interlocked.Increment(ref reads);
                        }
                        catch (Exception ex) { errors.Enqueue("reader: " + ex); }
                    }
                }, TaskCreationOptions.LongRunning));
            }
            tasks.Add(Task.Factory.StartNew(() =>
            {
                while (DateTime.UtcNow < stop && errors.Count == 0)
                {
                    Thread.Sleep(200);
                    try { db.Checkpoint(); Interlocked.Increment(ref checkpoints); } catch (Exception ex) { errors.Enqueue("checkpoint: " + ex); }
                }
            }, TaskCreationOptions.LongRunning));
            Task.WaitAll(tasks.ToArray());
        }
        using (var db = new LiteDatabase(CrashHarness.Conn(path, opts)))
        {
            var sum = db.GetCollection("acc").FindAll().Sum(x => x["bal"].AsInt64);
            if (sum != Initial * accounts) errors.Enqueue("after reopen sum=" + sum);
        }
        Console.WriteLine($"commits={commits} rollbacks={rollbacks} reads={reads} checkpoints={checkpoints} errors={errors.Count}");
        foreach (var e in errors.Take(5)) Console.WriteLine("  " + e.Split('\n').Take(6).Aggregate((a, b) => a + "\n  " + b));
        return errors.Count == 0 ? 0 : 1;
    }
}
