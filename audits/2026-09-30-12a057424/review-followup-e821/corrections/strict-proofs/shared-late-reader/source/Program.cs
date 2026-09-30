using System.Diagnostics;
using LiteDB;
using LiteDB.Engine;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

namespace Issue_3067_SharedLateReaderClose;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 0 && args[0] == "--writer") return WriteFromPeer(args[1], args[2]);
        var host = ReproHostClient.CreateDefault();
        ReproConfigurationReporter.SendConfiguration(host);
        try
        {
            // Keep the legacy mutex name below its platform limit independently of
            // the runner's long evidence directory. Both variants use this same volume.
            var root = Path.Combine(Path.GetTempPath(), "p133-late-" + Guid.NewGuid().ToString("N"));
            Console.WriteLine("DATABASE_ROOT " + root);
            Directory.CreateDirectory(root);
            var plain = Reproduce(Path.Combine(root, "plain.db"), "");
            var encrypted = Reproduce(Path.Combine(root, "encrypted.db"), "reader-proof");
            if (plain != encrypted) throw new Exception("Plain and encrypted outcomes disagree.");
            host.SendResult(plain, plain ? "Late callback and close are mutually blocked." : "Callbacks refused; close and peer writes completed.");
            Console.WriteLine(plain ? "BUG_REPRODUCED" : "VERIFIED_FIXED");
            return plain ? 0 : 10;
        }
        catch (Exception error)
        {
            host.SendResult(false, "Unexpected proof failure.", new { Error = error.ToString() });
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static bool Reproduce(string filename, string password)
    {
        using (var seed = new LiteDatabase(new ConnectionString { Filename = filename, Password = password.Length == 0 ? null : password }))
        {
            var rows = seed.GetCollection("rows");
            rows.EnsureIndex("value");
            rows.Insert(Enumerable.Range(0, 8).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i * 10 }));
            seed.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 42, ["value"] = "untouched" });
        }
        // Workers are background threads: the known-bad process exits at a bounded
        // deadline without trying to dispose the very connection whose close is stuck.
        var entered = new ManualResetEventSlim();
        var allowCall = new ManualResetEventSlim();
        var attempting = new ManualResetEventSlim();
        var armed = false;
        Exception? callbackFailure = null, readFailure = null, closeFailure = null;
        SharedEngine? shared = null;
        shared = new SharedEngine(new EngineSettings
        {
            Filename = filename, Password = password.Length == 0 ? null : password,
            ReadTransform = (collection, value) =>
            {
                if (!armed || collection != "rows") return value;
                if (value["_id"].AsInt32 != 1) throw new Exception("Callback was not the second row.");
                entered.Set();
                if (!allowCall.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Callback was never released.");
                attempting.Set();
                try { shared!.Pragma("USER_VERSION"); }
                catch (Exception error) { callbackFailure = error; }
                return value;
            }
        });
        var read = new Thread(() =>
        {
            try
            {
                if (!shared.BeginTrans()) throw new Exception("Legacy writer did not begin.");
                shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 99, ["value"] = 990 } }, BsonAutoId.Int32);
                using var reader = shared.Query("rows", new Query { Select = BsonExpression.Create("$") });
                if (!reader.Read() || reader.Current["_id"].AsInt32 != 0) throw new Exception("First buffered row missing.");
                armed = true; // Query returned and its Shared admission is no longer live.
                if (!reader.Read()) throw new Exception("Second row missing.");
            }
            catch (Exception error) { readFailure = error; }
        }) { IsBackground = true };
        read.Start();
        if (!entered.Wait(TimeSpan.FromSeconds(10))) throw new Exception("Later reader callback never executed.", readFailure);
        var close = new Thread(() =>
        {
            try { shared.Dispose(); }
            catch (Exception error) { closeFailure = error; }
        }) { IsBackground = true };
        close.Start();
        // This dedicated thread performs only Dispose, while the callback owns the
        // engine operation. Its wait establishes drain entry without private hooks.
        if (!SpinWait.SpinUntil(() => Waiting(close) || !close.IsAlive, TimeSpan.FromSeconds(5)) || !close.IsAlive)
            throw new Exception("Dispose never entered its live-reader drain.", closeFailure);
        allowCall.Set();
        if (!attempting.Wait(TimeSpan.FromSeconds(5))) throw new Exception("Callback never attempted its Shared call.");
        var readFinished = read.Join(TimeSpan.FromSeconds(3));
        var closeFinished = close.Join(TimeSpan.FromSeconds(3));
        if (!readFinished || !closeFinished)
        {
            if (readFinished || closeFinished || readFailure != null || closeFailure != null || callbackFailure != null ||
                !Waiting(read) || !Waiting(close))
                throw new Exception("Deadline did not establish the two-sided callback/close deadlock.");
            Console.WriteLine("LATE_CALLBACK_AND_CLOSE_BLOCKED " + (password.Length == 0 ? "plain" : "encrypted"));
            return true;
        }
        if (closeFailure != null) throw new Exception("Close failed unexpectedly.", closeFailure);
        if (!Closed(callbackFailure)) throw new Exception("Late independent call was not promptly refused.", callbackFailure);
        if (readFailure != null && !Closed(readFailure)) throw new Exception("Reader failed unexpectedly.", readFailure);
        RunPeer(filename, password);
        VerifyCold(filename, password);
        shared.Dispose(); // completed close is idempotent
        entered.Dispose(); allowCall.Dispose(); attempting.Dispose();
        return false;
    }

    private static bool Waiting(Thread thread) => (thread.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0;
    private static bool Closed(Exception? error) => error is ObjectDisposedException ||
        error is LiteException lite && lite.ErrorCode == LiteException.ENGINE_DISPOSED;

    private static void RunPeer(string filename, string password)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
        start.ArgumentList.Add("--writer"); start.ArgumentList.Add(filename); start.ArgumentList.Add(password);
        using var peer = Process.Start(start) ?? throw new Exception("Peer failed to start.");
        if (!peer.WaitForExit(10000)) { peer.Kill(entireProcessTree: true); throw new Exception("Native writer ownership was not released."); }
        if (peer.ExitCode != 0 || !peer.StandardOutput.ReadToEnd().Contains("PEER_COMMITTED"))
            throw new Exception("Peer write failed: " + peer.StandardError.ReadToEnd());
    }

    private static int WriteFromPeer(string filename, string password)
    {
        try
        {
            using (var db = new LiteDatabase(new ConnectionString { Filename = filename, Password = password.Length == 0 ? null : password, Connection = ConnectionType.Shared }))
                db.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 900, ["value"] = 9000 });
            Console.WriteLine("PEER_COMMITTED");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    private static void VerifyCold(string filename, string password)
    {
        using var cold = new LiteDatabase(new ConnectionString { Filename = filename, Password = password.Length == 0 ? null : password });
        var rows = cold.GetCollection("rows");
        if (rows.Count() != 9 || rows.FindById(99) != null || rows.FindById(900)?["value"].AsInt32 != 9000 ||
            cold.GetCollection("sentinel").FindById(42)?["value"].AsString != "untouched")
            throw new Exception("Cold acknowledged/aborted/sentinel model failed.");
        for (var i = 0; i < 8; i++)
            if (rows.FindById(i)?["value"].AsInt32 != i * 10 || rows.Count(Query.EQ("value", i * 10)) != 1)
                throw new Exception("Cold record/index model failed at " + i);
        var plan = rows.Query().Where(Query.EQ("value", 30)).GetPlan();
        if (plan["index"]["name"].AsString != "value" || !plan["index"]["mode"].AsString.StartsWith("INDEX SEEK", StringComparison.Ordinal)) throw new Exception("Secondary index path was not selected.");
    }
}
