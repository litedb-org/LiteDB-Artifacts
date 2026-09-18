using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LiteDB;

// Enumerates small value/pattern pairs in order of increasing size and reports the
// first pairs whose LIKE evaluation does not finish within a generous timeout.
internal static class LikeHang
{
    private static IEnumerable<string> Words(char[] alphabet, int maxLength)
    {
        yield return "";
        var frontier = new List<string> { "" };
        for (var length = 1; length <= maxLength; length++)
        {
            var next = new List<string>();
            foreach (var prefix in frontier)
            {
                foreach (var c in alphabet)
                {
                    next.Add(prefix + c);
                }
            }
            foreach (var word in next) yield return word;
            frontier = next;
        }
    }

    public static int Run(int maxValue, int maxPattern)
    {
        var collation = new Collation("en-US/None");
        var values = Words(new[] { 'a', 'b' }, maxValue).ToArray();
        var patterns = Words(new[] { 'a', 'b', '%', '_' }, maxPattern).ToArray();
        var pairs = values.SelectMany(v => patterns.Select(p => (v, p)))
            .OrderBy(x => x.v.Length + x.p.Length).ThenBy(x => x.p.Length).ToArray();
        Console.WriteLine($"pairs={pairs.Length}");

        foreach (var (value, pattern) in pairs)
        {
            var done = new ManualResetEventSlim(false);
            var worker = new Thread(() =>
            {
                var expr = BsonExpression.Create("@0 LIKE @1");
                expr.Parameters["0"] = value;
                expr.Parameters["1"] = pattern;
                expr.ExecuteScalar(collation);
                done.Set();
            }) { IsBackground = true };
            worker.Start();
            if (!done.Wait(TimeSpan.FromSeconds(5)))
            {
                Console.WriteLine($"HANG: '{value}' LIKE '{pattern}' did not finish within 5 s");
                return 1;
            }
        }
        Console.WriteLine("no hang found");
        return 0;
    }
}
