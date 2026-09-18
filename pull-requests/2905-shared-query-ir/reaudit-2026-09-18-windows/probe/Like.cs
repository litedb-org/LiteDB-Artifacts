using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using LiteDB;

internal static class LikeFuzz
{
    // Reference semantics: % = zero or more UTF-16 units, _ = exactly one unit, everything else literal.
    private static Regex ToRegex(string pattern, bool ignoreCase)
    {
        var sb = new StringBuilder(@"\A");
        foreach (var c in pattern)
        {
            if (c == '%') sb.Append(@"[\s\S]*");
            else if (c == '_') sb.Append(@"[\s\S]");
            else sb.Append(Regex.Escape(c.ToString()));
        }
        sb.Append(@"\z");
        return new Regex(sb.ToString(), RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None));
    }

    public static int Run(int seed, int iterations)
    {
        var random = new Random(seed);
        var valueAlphabet = new[] { 'a', 'b', 'A', 'c', ' ' };
        var patternAlphabet = new[] { 'a', 'b', 'A', 'c', ' ', '%', '%', '_', '_' };
        var expr = BsonExpression.Create("@0 LIKE @1");
        var total = 0;
        foreach (var (name, ignoreCase) in new[] { ("en-US/None", false), ("en-US/IgnoreCase", true) })
        {
            var collation = new Collation(name);
            var mismatches = 0;
            var samples = new List<string>();
            for (var i = 0; i < iterations; i++)
            {
                var value = new string(Enumerable.Range(0, random.Next(0, 9)).Select(_ => valueAlphabet[random.Next(valueAlphabet.Length)]).ToArray());
                var pattern = new string(Enumerable.Range(0, random.Next(0, 7)).Select(_ => patternAlphabet[random.Next(patternAlphabet.Length)]).ToArray());
                var expected = ToRegex(pattern, ignoreCase).IsMatch(value);
                expr.Parameters["0"] = value;
                expr.Parameters["1"] = pattern;
                bool actual;
                try
                {
                    actual = expr.ExecuteScalar(collation).AsBoolean;
                }
                catch (Exception ex)
                {
                    actual = !expected;
                    if (samples.Count < 8) samples.Add($"THROW '{value}' LIKE '{pattern}': {ex.GetType().Name}");
                }
                if (actual != expected)
                {
                    mismatches++;
                    if (samples.Count < 8) samples.Add($"'{value}' LIKE '{pattern}' expected={expected} actual={actual}");
                }
            }
            Console.WriteLine($"LIKE {name}: {iterations} cases, {mismatches} disagree with reference");
            foreach (var s in samples) Console.WriteLine("    " + s);
            total += mismatches;
        }
        return total;
    }
}
