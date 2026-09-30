using System.Reflection;
using LiteDB;
using LiteDB.Engine;

internal static class SharedPinProgressHarness
{
    internal static bool TryRun(string mode, string filename, string? password, string[] args)
    {
        if (mode != "pin-insert-progress") return false;
        using var engine = new SharedEngine(new EngineSettings
        {
            Filename = filename, Password = password, TransactionPageLimit = 1, CacheSize = 8192
        });
        using var database = new LiteDatabase(engine, disposeOnClose: false);
        var collection = database.GetCollection("docs");
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var turnstile = typeof(SharedEngine).GetField("_turnstile", flags)!.GetValue(engine)!;
        var observed = 0;
        turnstile.GetType().GetProperty("BeforeMainWait", flags)!.SetValue(turnstile, (Action)(() =>
        {
            // This observes actual native admission, not just entry into Insert.
            // It does not assert that the mutex was still occupied when WaitOne ran.
            if (Interlocked.Exchange(ref observed, 1) == 0) Console.WriteLine("native-wait");
        }));
        Console.WriteLine("ready");
        if (Console.ReadLine() != "go") throw new InvalidOperationException("Missing pin probe go command");
        var start = int.Parse(args[4]);
        for (var id = start; id < start + 20; id++)
        {
            // Preserve the original twenty ordinary, separately committed writes.
            collection.Insert(new BsonDocument { ["_id"] = id });
            Console.WriteLine("committed:" + id);
        }
        Console.WriteLine("done");
        return true;
    }
}
