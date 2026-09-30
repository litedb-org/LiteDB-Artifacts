using LiteDB;
using LiteDB.Engine;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

namespace Issue_3067_SharedCallbackNativeWait;
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length > 0) return ChildMain(args);
        var host = ReproHostClient.CreateDefault(); ReproConfigurationReporter.SendConfiguration(host);
        try
        {
            var root = Path.Combine(Path.GetTempPath(), "p133-native-callback-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); Console.WriteLine("DATABASE_ROOT " + root);
            var outcomes = new List<int>();
            foreach (var password in new string?[] { null, "secret" })
            {
                foreach (var scenario in new[] { "input-pragma", "input-write", "late-read" })
                    outcomes.Add(Processes.Run("--case", Path.Combine(root, Guid.NewGuid() + ".db"), password, scenario));
                foreach (var control in new[] { "other-database", "direct", "idle-handoff", "cancel-admission" })
                    if (Processes.Run("--control", Path.Combine(root, Guid.NewGuid() + ".db"), password, control) != 10)
                        throw new Exception("Control failed: " + control);
            }
            if (outcomes.Distinct().Count() != 1 || (outcomes[0] != 10 && outcomes[0] != 20)) throw new Exception("Case outcomes disagree.");
            var reproduced = outcomes[0] == 20;
            Console.WriteLine(reproduced ? "BUG_REPRODUCED" : "VERIFIED_FIXED");
            host.SendResult(reproduced, "Six isolated native callback cases and eight independent-work/admission controls verified.");
            return reproduced ? 0 : 10;
        }
        catch (Exception error) { Console.Error.WriteLine(error); host.SendResult(false, "Unexpected failure", new { Error = error.ToString() }); return 2; }
    }
    private static int ChildMain(string[] args)
    {
        try
        {
            Console.WriteLine("CHILD_READY");
            if (Console.ReadLine() != "go") throw new Exception("Missing parent handshake.");
            var password = args[2].Length == 0 ? null : args[2];
            if (args[0] == "--peer")
            {
                using (var db = new LiteDatabase(new ConnectionString { Filename = args[1], Password = password, Connection = ConnectionType.Shared }))
                    db.GetCollection("rows").Insert(Row(30));
                Console.WriteLine("PEER_COMMITTED"); return 10;
            }
            Seed(args[1], password);
            var reproduced = args[0] == "--case" ? Run(args[1], password, args[3]) : Controls.Run(args[1], password, args[3]);
            Console.WriteLine(reproduced ? "NATIVE_SELF_WAIT_VERIFIED" : "CASE_FIXED");
            return reproduced ? 20 : 10;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 2; }
    }
    private static bool Run(string path, string? password, string scenario)
    {
        Action? transform = null;
        var shared = new SharedEngine(new EngineSettings { Filename = path, Password = password, ReadTransform = (_, value) => { transform?.Invoke(); return value; } });
        var db = new LiteDatabase(shared);
        var entered = new ManualResetEventSlim(); var finished = new ManualResetEventSlim();
        Exception? error = null; Exception? refusal = null; var callbacks = 0; var committed = scenario != "late-read";
        ILiteTransaction? tx = null;
        void Callback()
        {
            transform = null; callbacks++; entered.Set(); Console.WriteLine("CALLBACK_ENTERED " + scenario);
            try
            {
                if (scenario == "input-write") db.GetCollection("unrelated").Insert(Row(90));
                else _ = db.UserVersion;
            }
            catch (InvalidOperationException rejected) { refusal = rejected; }
        }
        IEnumerable<BsonDocument> Input() { Callback(); yield return Row(21); }
        var worker = new Thread(() =>
        {
            try
            {
                using (tx = db.BeginTransaction())
                {
                    var rows = tx.GetCollection("rows"); rows.Insert(Row(20));
                    if (scenario == "late-read")
                    {
                        using var reader = rows.Query().OrderBy("_id").ToEnumerable().GetEnumerator();
                        if (!reader.MoveNext()) throw new Exception("Reader did not start.");
                        transform = Callback;
                        if (!reader.MoveNext()) throw new Exception("Late reader did not advance.");
                        transform = null;
                    }
                    else rows.Insert(Input());
                    if (callbacks != 1 || refusal == null || tx.State != LiteTransactionState.Active || rows.FindById(20) == null)
                        throw new Exception("Caught refusal did not leave prior writes and handle Active.");
                    if (committed) tx.Commit(); else tx.Rollback();
                }
            }
            catch (Exception failure) { error = failure; }
            finally { finished.Set(); }
        }) { IsBackground = true };
        worker.Start();
        if (!entered.Wait(TimeSpan.FromSeconds(15))) throw new Exception("Callback never started.", error);
        var boundary = Processes.WaitNativeBoundary(shared, worker, finished);
        var reproduced = boundary && !finished.Wait(TimeSpan.FromSeconds(1));
        if (reproduced)
        {
            Console.WriteLine("PARENT_NATIVE_WAIT_CONFIRMED " + scenario);
            Processes.Close(db);
            if (!worker.Join(TimeSpan.FromSeconds(10)) || error is not OperationCanceledException)
                throw new Exception("Native wait did not unwind through session cancellation.", error);
            if (tx?.State == LiteTransactionState.Active) throw new Exception("Cancelled handle stayed Active.");
        }
        else
        {
            if (!worker.Join(TimeSpan.FromSeconds(10)) || error != null) throw new Exception("Fixed callback failed.", error);
            db.Dispose();
        }
        if (Processes.Run("--peer", path, password, "") != 10) throw new Exception("Peer could not write after handle release.");
        Verify(path, password, !reproduced && committed ? new[] { 1, 2, 3, 20, 21, 30 } : new[] { 1, 2, 3, 30 });
        return reproduced;
    }
    internal static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id * 10 };
    internal static void Seed(string path, string? password)
    {
        using var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password });
        db.GetCollection("rows").EnsureIndex("value"); db.GetCollection("rows").Insert(Enumerable.Range(1, 3).Select(Row));
        db.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 42, ["value"] = "untouched" });
    }
    internal static void Verify(string path, string? password, int[] ids, bool ordinary = false)
    {
        for (var reopen = 0; reopen < 2; reopen++)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password });
            var rows = db.GetCollection("rows");
            if (!rows.FindAll().Select(row => row["_id"].AsInt32).OrderBy(id => id).SequenceEqual(ids) ||
                db.GetCollection("unrelated").Count() != (ordinary ? 1 : 0) || db.GetCollection("sentinel").FindById(42)?["value"] != "untouched")
                throw new Exception("Cold row/independent-write/sentinel model failed.");
            foreach (var id in ids)
            {
                var query = rows.Query().Where(Query.EQ("value", id * 10));
                if (query.GetPlan()["index"]["name"] != "value" || !query.GetPlan()["index"]["mode"].AsString.StartsWith("INDEX SEEK", StringComparison.Ordinal) || query.Single()["_id"] != id)
                    throw new Exception("Cold indexed model failed: " + id);
            }
        }
    }
}
