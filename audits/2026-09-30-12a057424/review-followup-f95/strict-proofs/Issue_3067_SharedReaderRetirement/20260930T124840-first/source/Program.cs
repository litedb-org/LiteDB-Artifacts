using System.Diagnostics;
using LiteDB;
using LiteDB.Engine;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

namespace Issue_3067_SharedReaderRetirement;
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--peer") return PeerMain(args);
        var host = ReproHostClient.CreateDefault(); ReproConfigurationReporter.SendConfiguration(host);
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "p133-retire-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Console.WriteLine("DATABASE_ROOT " + root);
            var outcomes = new List<bool>();
            foreach (var password in new string?[] { null, "secret" })
            {
                Control(Path.Combine(root, Guid.NewGuid().ToString("N") + ".db"), password);
                bool? outcome = null;
                for (var attempt = 0; attempt < 3 && outcome == null; attempt++)
                    outcome = Run(Path.Combine(root, Guid.NewGuid().ToString("N") + ".db"), password, attempt);
                if (!outcome.HasValue) throw new Exception("No pin setup satisfied the bounded timing precondition.");
                outcomes.Add(outcome.Value);
            }
            if (outcomes.Distinct().Count() != 1) throw new Exception("Plain/encrypted outcomes disagree.");
            Console.WriteLine(outcomes[0] ? "BUG_REPRODUCED" : "VERIFIED_FIXED");
            host.SendResult(outcomes[0], "Forced-pin callback retirement and independent disposal control verified."); return outcomes[0] ? 0 : 10;
        }
        catch (Exception error) { Console.Error.WriteLine(error); host.SendResult(false, "Unexpected failure", new { Error = error.ToString() }); return 2; }
    }
    private static bool? Run(string path, string? password, int attempt)
    {
        Seed(path, password);
        var ready = new ManualResetEventSlim(); var resume = new ManualResetEventSlim(); var retiring = new ManualResetEventSlim();
        Exception? readError = null, closeError = null; var setupSlow = false; Action? callback = null;
        var shared = new SharedEngine(new EngineSettings { Filename = path, Password = password, ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
        var readerThread = new Thread(() =>
        {
            try
            {
                using var leased = shared.Query("rows", new Query());
                if (!leased.Read()) throw new Exception("Leased reader did not start.");
                // Start before the write: even preemption at its return must count.
                var setup = Stopwatch.StartNew();
                shared.Insert("rows", new[] { Row(6) }, BsonAutoId.Int32);
                if (!shared.BeginTrans()) throw new Exception("Legacy transaction did not begin.");
                setup.Stop(); Console.WriteLine($"PIN_SETUP attempt={attempt} encrypted={password != null} ms={setup.Elapsed.TotalMilliseconds:F3}");
                if (setup.Elapsed >= TimeSpan.FromMilliseconds(50)) { setupSlow = true; shared.Rollback(); return; }
                shared.Insert("rows", new[] { Row(99) }, BsonAutoId.Int32);
                using var reader = shared.Query("rows", new Query());
                if (!reader.Read() || reader.Current["_id"] != 1) throw new Exception("First row missing.");
                callback = () =>
                {
                    callback = null; ready.Set();
                    if (!resume.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Callback was never released.");
                    retiring.Set(); leased.Dispose();
                };
                if (!reader.Read() || reader.Current["_id"] != 2) throw new Exception("Late row missing.");
            }
            catch (Exception error) { readError = error; }
            finally { ready.Set(); }
        }) { IsBackground = true };
        readerThread.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Reader setup never finished.");
        if (setupSlow)
        {
            if (!readerThread.Join(TimeSpan.FromSeconds(5)) || readError != null) throw new Exception("Slow setup cleanup failed.", readError);
            shared.Dispose(); Console.WriteLine("SETUP_TOO_SLOW_RETRY"); return null;
        }
        if (readError != null || !readerThread.IsAlive) throw new Exception("Late callback setup failed.", readError);
        var close = new Thread(() => { try { shared.Dispose(); } catch (Exception error) { closeError = error; } }) { IsBackground = true };
        close.Start();
        if (!SpinWait.SpinUntil(() => Waiting(close) || !close.IsAlive, TimeSpan.FromSeconds(5)) || !close.IsAlive) throw new Exception("Disposer never waited with callback active.", closeError);
        // Public refusal confirms connection disposal was published before retiring L.
        try { shared.Pragma("USER_VERSION"); throw new Exception("Connection disposal was not published."); }
        catch (ObjectDisposedException) { }
        resume.Set(); if (!retiring.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Callback never attempted leased reader retirement.");
        var readDone = readerThread.Join(TimeSpan.FromSeconds(3)); var closeDone = close.Join(TimeSpan.FromSeconds(3));
        if (!readDone || !closeDone)
        {
            if (readDone || closeDone || readError != null || closeError != null || !Waiting(readerThread) || !Waiting(close)) throw new Exception("Deadline did not identify the two-sided forced-pin retirement wait.");
            Console.WriteLine("FORCED_PIN_RETIREMENT_BLOCKED " + (password == null ? "plain" : "encrypted")); return true;
        }
        if (readError != null || closeError != null) throw new Exception("Callback or close failed.", readError ?? closeError);
        Peer(path, password, 7); Verify(path, password, 7); GC.KeepAlive(shared); return false;
    }
    private static bool Waiting(Thread thread) => (thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0;
    private static void Control(string path, string? password)
    {
        Seed(path, password);
        using var shared = new SharedEngine(new EngineSettings { Filename = path, Password = password, ReadTransform = (_, value) => value });
        using var reader = shared.Query("rows", new Query()); if (!reader.Read()) throw new Exception("Control reader missing.");
        shared.Insert("rows", new[] { Row(6) }, BsonAutoId.Int32); reader.Dispose(); shared.Dispose();
        Peer(path, password, 7); Verify(path, password, 7); Console.WriteLine("INDEPENDENT_RETIREMENT_OK");
    }
    private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id * 10 };
    private static void Seed(string path, string? password)
    {
        using var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password });
        db.GetCollection("rows").EnsureIndex("value");
        db.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(Row));
        db.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 42, ["value"] = "untouched" });
    }
    private static void Verify(string path, string? password, int count)
    {
        for (var reopen = 0; reopen < 2; reopen++)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password });
            var rows = db.GetCollection("rows");
            if (rows.Count() != count || rows.FindById(99) != null || db.GetCollection("sentinel").FindById(42)?["value"] != "untouched")
                throw new Exception("Cold row/aborted/sentinel model failed.");
            for (var id = 1; id <= count; id++)
            {
                var query = rows.Query().Where(Query.EQ("value", id * 10));
                if (query.GetPlan()["index"]["name"] != "value" || !query.GetPlan()["index"]["mode"].AsString.StartsWith("INDEX SEEK", StringComparison.Ordinal) || query.Single()["_id"] != id)
                    throw new Exception("Cold indexed model failed: " + id);
            }
        }
    }
    private static void Peer(string path, string? password, int id)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        start.ArgumentList.Add("--peer"); start.ArgumentList.Add(path); start.ArgumentList.Add(password ?? ""); start.ArgumentList.Add(id.ToString());
        using var peer = Process.Start(start) ?? throw new Exception("Peer failed to start.");
        if (!peer.WaitForExit(10000)) { peer.Kill(entireProcessTree: true); throw new Exception("Peer writer could not progress."); }
        if (peer.ExitCode != 0 || !peer.StandardOutput.ReadToEnd().Contains("PEER_COMMITTED")) throw new Exception("Peer failed: " + peer.StandardError.ReadToEnd());
    }
    private static int PeerMain(string[] args)
    {
        try
        {
            using (var db = new LiteDatabase(new ConnectionString { Filename = args[1], Password = args[2].Length == 0 ? null : args[2], Connection = ConnectionType.Shared }))
                db.GetCollection("rows").Insert(Row(int.Parse(args[3])));
            Console.WriteLine("PEER_COMMITTED"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }
}
