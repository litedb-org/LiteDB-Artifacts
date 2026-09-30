using System.Diagnostics;
using LiteDB;
using LiteDB.Engine;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

namespace Issue_3067_LeasedReaderSelfDispose;
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--peer") return PeerMain(args);
        var host = ReproHostClient.CreateDefault(); ReproConfigurationReporter.SendConfiguration(host);
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "p133-self-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Console.WriteLine("DATABASE_ROOT " + root);
            var outcomes = new List<bool>();
            foreach (var password in new string?[] { null, "secret" })
            foreach (var operationCore in new[] { false, true })
            {
                outcomes.Add(Run(Path.Combine(root, Guid.NewGuid().ToString("N") + ".db"), password, operationCore, true));
                if (Run(Path.Combine(root, Guid.NewGuid().ToString("N") + ".db"), password, operationCore, false)) throw new Exception("Ordinary reader disposal leaked admission.");
            }
            if (outcomes.Distinct().Count() != 1) throw new Exception("Plain/encrypted or query path outcomes disagree.");
            var reproduced = outcomes[0]; Console.WriteLine(reproduced ? "BUG_REPRODUCED" : "VERIFIED_FIXED");
            host.SendResult(reproduced, "Self-disposal retry, normal controls and native admission checked."); return reproduced ? 0 : 10;
        }
        catch (Exception error) { Console.Error.WriteLine(error); host.SendResult(false, "Unexpected failure", new { Error = error.ToString() }); return 2; }
    }
    private static bool Run(string path, string? password, bool operationCore, bool selfDispose)
    {
        Seed(path, password);
        Action? callback = null; Exception? refusal = null; var callbacks = 0;
        var shared = new SharedEngine(new EngineSettings { Filename = path, Password = password, ReadOnly = !operationCore,
            ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
        // FOR UPDATE retains the writable parent core, steering the ordinary query
        // through QueryCore. Without it the read-only connection uses QuerySnapshot.
        var holder = operationCore ? shared.Query("rows", new Query { ForUpdate = true }) : null;
        var reader = shared.Query("rows", new Query());
        if (!reader.Read() || reader.Current["_id"] != 1) throw new Exception("First row missing.");
        if (selfDispose) callback = () =>
        {
            callback = null; callbacks++;
            try { reader.Dispose(); } catch (Exception error) { refusal = error; }
        };
        if (!reader.Read() || reader.Current["_id"] != 2) throw new Exception("Late row missing.");
        if (selfDispose && (callbacks != 1 || refusal is not InvalidOperationException)) throw new Exception("Expected self-disposal refusal missing.", refusal);
        reader.Dispose(); holder?.Dispose(); shared.Dispose();
        var reproduced = false;
        try { using var direct = new LiteEngine(new EngineSettings { Filename = path, Password = password }); }
        catch (DatabaseAdmissionException error) when (selfDispose && error.InnerException is IOException inner &&
            inner.Message.Contains("Incompatible local database access.", StringComparison.Ordinal))
        { reproduced = true; Console.WriteLine($"RETAINED_SHARED_ADMISSION core={operationCore} encrypted={password != null}"); }
        // Keep every apparently disposed public owner rooted through the native probe:
        // collection/finalizer cleanup must not rescue the known-bad outcome.
        GC.KeepAlive(reader); GC.KeepAlive(holder); GC.KeepAlive(shared);
        if (!reproduced) { Peer(path, password, 6); Verify(path, password, 6); }
        GC.KeepAlive(reader); GC.KeepAlive(holder); GC.KeepAlive(shared);
        return reproduced;
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
