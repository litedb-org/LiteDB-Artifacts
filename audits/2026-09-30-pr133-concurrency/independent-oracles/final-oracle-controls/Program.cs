using LiteDB.ConcurrencyTesting;
using System.Diagnostics;
var mode = args[0];
if (mode == "strict-refusal")
{
    ExplorerDatabase.Refused(() => throw new InvalidOperationException(ExplorerDatabase.OverlapRefusal));
    foreach (var error in new Exception[] { new InvalidOperationException("unrelated failure"), new ObjectDisposedException("closed") })
    {
        try { ExplorerDatabase.Refused(() => throw error); throw new Exception("Unexpected exception accepted"); }
        catch (Exception observed) when (ReferenceEquals(observed, error)) { }
    }
    Console.WriteLine("PASS exact refusal accepted; unrelated and derived refusals rejected"); return 0;
}
var schedule = new ExplorerSchedule(args[1], mode);
var a = schedule.NewActor("A"); var b = schedule.NewActor("B");
if (mode == "completed-idle")
{
    a.Run("completed", () => { });
    Thread.Sleep(16000);
    b.Run("later-peer", () => { });
    schedule.CheckActors();
    if (!schedule.Stop()) throw new Exception("Workers not stopped");
    schedule.Dispose();
    Console.WriteLine("PASS completed actor remains healthy after deadline idle time"); return 0;
}
if (mode == "stalled-peer")
{
    var elapsed = Stopwatch.StartNew();
    a.Invoke("stalled", () => Thread.Sleep(Timeout.Infinite));
    try
    {
        while (elapsed.Elapsed < TimeSpan.FromSeconds(20)) b.Run("peer-completes", () => Thread.Sleep(20));
    }
    catch (TimeoutException error) when (error.Message == "Worker stalled: A/stalled")
    {
        Console.WriteLine("PASS stalled actor detected despite peer completions after " + elapsed.Elapsed.TotalSeconds); return 0;
    }
    throw new Exception("Stalled actor escaped deadline");
}
throw new Exception("Unknown mode");
