using System.Diagnostics;
using LiteDB;
using LiteDB.Engine;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

if (args.Length != 0 && args[0] == "--writer") return PeerWrite(args[1], args[2]);

var host = ReproHostClient.CreateDefault();
ReproConfigurationReporter.SendConfiguration(host);
// Both variants use short temp-volume paths, below legacy native mutex name limits.
var root = Path.Combine(Path.GetTempPath(), "p133-facade-" + Guid.NewGuid().ToString("N"));
Console.WriteLine("DATABASE_ROOT " + root);
Directory.CreateDirectory(root);
try
{
    var outcomes = new List<bool>();
    foreach (var password in new string?[] { null, "secret" })
    {
        foreach (var mode in new[] { "raw", "shared-writer", "shared-leased" })
        {
            var outcome = Run(Path.Combine(root, "late-close-" + Guid.NewGuid().ToString("N") + ".db"), password, mode);
            if (mode != "shared-leased") outcomes.Add(outcome);
        }
    }
    if (outcomes.Distinct().Count() != 1) throw new Exception("Inconsistent unsafe callback outcomes");
    var reproduced = outcomes[0];
    Console.WriteLine(reproduced ? "REGRESSION_REPRODUCED" : "FIXED_STATE_VERIFIED");
    host.SendResult(reproduced, "Late callback disposition, safe lease control and cold indexed state verified.");
    return reproduced ? 0 : 10;
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    host.SendResult(false, "Unexpected result: " + error);
    return 2;
}

static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id * 10 };
static bool Run(string path, string? password, string mode)
{
    var connection = new ConnectionString { Filename = path, Password = password };
    using (var seed = new LiteDatabase(connection))
    {
        seed.GetCollection("rows").EnsureIndex("value");
        seed.GetCollection("rows").Insert(new[] { Row(1), Row(2), Row(3) });
        seed.GetCollection("sentinel").Insert(Row(99));
    }
    LiteDatabase? db = null;
    Exception? closeError = null;
    TimeSpan elapsed = default;
    var callbacks = 0;
    var settings = new EngineSettings
    {
        Filename = path, Password = password,
        ReadTransform = (_, value) =>
        {
            if (value.IsDocument && value["_id"] == 2)
            {
                callbacks++;
                var timer = Stopwatch.StartNew();
                try { db!.Dispose(); }
                catch (Exception error) { closeError = error; }
                elapsed = timer.Elapsed;
            }
            return value;
        }
    };
    using var engine = mode == "raw" ? (ILiteEngine)new LiteEngine(settings) : new SharedEngine(settings);
    bool reproduced;
    using (db = new LiteDatabase(engine))
    {
        using (var reader = db.Execute(mode == "shared-writer" ? "SELECT $ FROM rows FOR UPDATE" : "SELECT $ FROM rows"))
        {
            if (!reader.Read() || reader.Current["_id"] != 1 || callbacks != 0)
                throw new Exception("Did not reach late reader boundary");
            if (!reader.Read() || reader.Current["_id"] != 2 || callbacks != 1)
                throw new Exception("Late callback not executed exactly once");
        }
        if (mode == "shared-leased")
        {
            if (closeError != null) throw new Exception("Safe leased reader close failed", closeError);
            reproduced = false;
        }
        else
        {
            reproduced = closeError is TimeoutException &&
                closeError.Message.StartsWith("Session close is still draining active work.", StringComparison.Ordinal) &&
                elapsed >= TimeSpan.FromSeconds(9);
            if (!reproduced && (closeError is not InvalidOperationException || elapsed >= TimeSpan.FromSeconds(2)))
                throw new Exception("Unexpected callback close disposition: " + elapsed, closeError);
            if (!reproduced) db.GetCollection("rows").Insert(Row(4));
            // Reproduced close is already draining; after the reader finishes its retry must complete.
            db.Dispose();
        }
        Console.WriteLine($"{mode} encrypted={password != null}: {closeError?.GetType().Name ?? "success"} after {elapsed}");
    }
    engine.Dispose();
    RunPeer(path, password);
    var count = !reproduced && mode != "shared-leased" ? 4 : 3;
    for (var reopen = 0; reopen < 2; reopen++)
    {
        using var cold = new LiteDatabase(connection);
        if (cold.GetCollection("rows").Count() != count + 1 || cold.GetCollection("rows").FindById(50)?["value"] != 500 || cold.GetCollection("sentinel").FindById(99) == null)
            throw new Exception("Cold row/sentinel model mismatch");
        for (var id = 1; id <= count; id++)
        {
            var query = cold.GetCollection("rows").Query().Where(Query.EQ("value", id * 10));
            if (query.GetPlan()["index"]["name"] != "value" ||
                !query.GetPlan()["index"]["mode"].AsString.StartsWith("INDEX SEEK", StringComparison.Ordinal) || query.Single()["_id"] != id)
                throw new Exception("Cold indexed model mismatch");
        }
    }
    return reproduced;
}

static void RunPeer(string filename, string? password)
{
    var start = new ProcessStartInfo(Environment.ProcessPath!)
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
        start.ArgumentList.Add(Environment.GetCommandLineArgs()[0]);
    start.ArgumentList.Add("--writer"); start.ArgumentList.Add(filename); start.ArgumentList.Add(password ?? "");
    using var peer = Process.Start(start) ?? throw new Exception("Peer failed to start.");
    if (!peer.WaitForExit(10000)) { peer.Kill(entireProcessTree: true); throw new Exception("Native writer ownership was not released."); }
    if (peer.ExitCode != 0 || !peer.StandardOutput.ReadToEnd().Contains("PEER_COMMITTED"))
        throw new Exception("Peer write failed: " + peer.StandardError.ReadToEnd());
}

static int PeerWrite(string filename, string password)
{
    try
    {
        using (var db = new LiteDatabase(new ConnectionString { Filename = filename, Password = password.Length == 0 ? null : password, Connection = ConnectionType.Shared }))
            db.GetCollection("rows").Insert(Row(50));
        Console.WriteLine("PEER_COMMITTED");
        return 0;
    }
    catch (Exception error) { Console.Error.WriteLine(error); return 2; }
}
