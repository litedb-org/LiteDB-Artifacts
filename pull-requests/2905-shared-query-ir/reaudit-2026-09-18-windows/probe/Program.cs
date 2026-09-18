using System;
using LiteDB;

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine("assembly=" + typeof(LiteDatabase).Assembly.Location);
        switch (args.Length > 0 ? args[0] : "")
        {
            case "like": return LikeFuzz.Run(int.Parse(args[1]), int.Parse(args[2])) == 0 ? 0 : 1;
            case "likehang": return LikeHang.Run(int.Parse(args[1]), int.Parse(args[2]));
            case "linqhang": return LinqHang.Run();
            case "corpus": return Corpus.Run(args[1], int.Parse(args[2]));
            case "misc": return Misc.Run();
            case "rt-create": return RoundTrip.Create(args[1], args.Length > 2 ? args[2] : null);
            case "rt-mutate": return RoundTrip.Mutate(args[1], args.Length > 2 ? args[2] : null);
            case "rt-digest": return RoundTrip.Digest(args[1], args.Length > 2 ? args[2] : null);
            case "groupby": return GroupByLeak.Run();
            case "stress": return (Stress.Linq(int.Parse(args[1]), int.Parse(args[2])) + Stress.Sql(int.Parse(args[1]), int.Parse(args[3]))) == 0 ? 0 : 1;
            case "api": return Api.Run();
            case "extreme": return ExtremeBounds.Run();
            case "extreme2": return Extreme2.Run();
            case "throwing": return ThrowingBounds.Run();
            case "issues": return IssueRepros.Run();
            default: Console.Error.WriteLine("unknown command"); return 2;
        }
    }
}
