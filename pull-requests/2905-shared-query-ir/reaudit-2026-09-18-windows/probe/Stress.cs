using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LiteDB;

/// <summary>
/// Concurrent cache soundness: many threads share ONE mapper and ONE database while using
/// different captured values / parameters. Every answer must equal the single-threaded one.
/// </summary>
internal static class Stress
{
    public static int Linq(int threads, int rounds)
    {
        var expected = new Dictionary<int, string[]>();
        for (var set = 0; set < 3; set++) expected[set] = Corpus.Snapshot(new BsonMapper(), set);

        var shared = new BsonMapper();
        var mismatches = 0;
        var checks = 0;
        var firstProblem = (string)null;
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, worker =>
        {
            var random = new Random(1000 + worker);
            for (var round = 0; round < rounds; round++)
            {
                var set = random.Next(0, 3);
                var actual = Corpus.Snapshot(shared, set);
                for (var i = 0; i < actual.Length; i++)
                {
                    Interlocked.Increment(ref checks);
                    if (actual[i] == expected[set][i]) continue;
                    Interlocked.Increment(ref mismatches);
                    Interlocked.CompareExchange(ref firstProblem, $"set={set} item={i}\n      expected: {expected[set][i]}\n      actual:   {actual[i]}", null);
                }
            }
        });
        Console.WriteLine($"LINQ concurrent: threads={threads} rounds={rounds} checks={checks} mismatches={mismatches}");
        if (firstProblem != null) Console.WriteLine("    " + firstProblem);
        return mismatches;
    }

    public static int Sql(int threads, int iterations)
    {
        using (var db = new LiteDatabase(":memory:"))
        {
            var col = db.GetCollection("rows");
            var data = Enumerable.Range(1, 2000).Select(i => new { Id = i, Age = i % 100, Name = "n" + i % 50 }).ToArray();
            col.InsertBulk(data.Select(d => new BsonDocument { ["_id"] = d.Id, ["Age"] = d.Age, ["Name"] = d.Name }));
            col.EnsureIndex("Age", "$.Age");
            col.EnsureIndex("Name", "$.Name");

            var mismatches = 0;
            var checks = 0;
            var firstProblem = (string)null;
            Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, worker =>
            {
                var random = new Random(5000 + worker);
                for (var i = 0; i < iterations; i++)
                {
                    string sql;
                    BsonDocument parameters;
                    Func<int, int, string, bool> predicate;
                    switch (random.Next(0, 4))
                    {
                        case 0:
                        {
                            int lo = random.Next(0, 100), hi = random.Next(0, 100);
                            sql = "SELECT $ FROM rows WHERE Age >= @lo AND Age <= @hi";
                            parameters = new BsonDocument { ["lo"] = lo, ["hi"] = hi };
                            predicate = (id, age, name) => age >= lo && age <= hi;
                            break;
                        }
                        case 1:
                        {
                            var wanted = "n" + random.Next(0, 60);
                            var age = random.Next(0, 100);
                            sql = "SELECT $ FROM rows WHERE Name = @name OR Age = @age";
                            parameters = new BsonDocument { ["name"] = wanted, ["age"] = age };
                            predicate = (id, a, name) => name == wanted || a == age;
                            break;
                        }
                        case 2:
                        {
                            var list = Enumerable.Range(0, random.Next(0, 6)).Select(_ => random.Next(0, 100)).ToArray();
                            sql = "SELECT $ FROM rows WHERE Age IN @list AND _id > @min";
                            var min = random.Next(0, 2000);
                            parameters = new BsonDocument { ["list"] = new BsonArray(list.Select(x => (BsonValue)x)), ["min"] = min };
                            predicate = (id, a, name) => list.Contains(a) && id > min;
                            break;
                        }
                        default:
                        {
                            int a1 = random.Next(0, 100), a2 = random.Next(0, 100), b1 = random.Next(0, 100), b2 = random.Next(0, 100);
                            sql = "SELECT $ FROM rows WHERE (Age > @a1 AND Age < @a2) OR (Age >= @b1 AND Age <= @b2)";
                            parameters = new BsonDocument { ["a1"] = a1, ["a2"] = a2, ["b1"] = b1, ["b2"] = b2 };
                            predicate = (id, a, name) => (a > a1 && a < a2) || (a >= b1 && a <= b2);
                            break;
                        }
                    }

                    var found = new List<int>();
                    using (var reader = db.Execute(sql, parameters)) { while (reader.Read()) found.Add(reader.Current["_id"].AsInt32); }
                    found.Sort();
                    var wantedIds = data.Where(d => predicate(d.Id, d.Age, d.Name)).Select(d => d.Id).ToList();
                    Interlocked.Increment(ref checks);
                    if (found.SequenceEqual(wantedIds)) continue;
                    Interlocked.Increment(ref mismatches);
                    Interlocked.CompareExchange(ref firstProblem, $"{sql} {JsonSerializer.Serialize(parameters)} found={found.Count} expected={wantedIds.Count}", null);
                }
            });
            Console.WriteLine($"SQL concurrent: threads={threads} iterations={iterations} checks={checks} mismatches={mismatches}");
            if (firstProblem != null) Console.WriteLine("    " + firstProblem);
            return mismatches;
        }
    }
}
