using LiteDB;
using LiteDB.Engine;
using LiteDB.ReproRunner.Shared;
using LiteDB.ReproRunner.Shared.Messaging;

namespace Issue_133_SharedSnapshotCloseRefusal;

internal static class Program
{
    private static int Main()
    {
        var host = ReproHostClient.CreateDefault();
        ReproConfigurationReporter.SendConfiguration(host);
        try
        {
            // Preserve the original local-filesystem fault and keep native mutex names short.
            var directory = Path.Combine(Path.GetTempPath(), "p133-late-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var outcomes = new List<bool>();
            foreach (var password in new string?[] { null, "secret" })
            {
                Reproduce(Path.Combine(directory, password == null ? "leased.db" : "leased-enc.db"), password, leased: true);
                outcomes.Add(Reproduce(Path.Combine(directory, password == null ? "fallback.db" : "fallback-enc.db"), password, leased: false));
            }
            if (outcomes.Distinct().Count() != 1) throw new Exception("Plain/encrypted outcomes disagree.");
            var reproduced = outcomes[0];
            host.SendResult(reproduced, reproduced ? "Refused snapshot close released native protection." : "Refused snapshot close retained native protection.");
            Console.WriteLine(reproduced ? "BUG_REPRODUCED" : "VERIFIED_FIXED");
            return reproduced ? 0 : 10;
        }
        catch (Exception error)
        {
            host.SendResult(false, "The reproduction failed unexpectedly.", new { Error = error.ToString() });
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    private static bool Reproduce(string filename, string? password, bool leased)
    {
        using (var seed = new LiteDatabase(new ConnectionString { Filename = filename, Password = password }))
        {
            seed.GetCollection("rows").Insert(Enumerable.Range(1, 4).Select(Row));
            seed.GetCollection("rows").EnsureIndex("value");
            seed.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 99, ["value"] = "untouched" });
        }
        var registry = filename + "-readers";
        if (!leased) File.WriteAllText(registry, "proof obstacle: not a reader-registry directory");
        var armed = false;
        var callbackReached = false;
        var admittedInsideCallback = false;
        Exception? closeError = null;
        SharedEngine? shared = null;
        using (shared = new SharedEngine(new EngineSettings
        {
            Filename = filename, Password = password, ReadOnly = true,
            ReadTransform = (collection, value) =>
            {
                if (!armed || collection != "rows") return value;
                armed = false;
                callbackReached = true;
                try { shared!.Dispose(); }
                catch (Exception error) { closeError = error; }
                // This is a different public facade and holder, on a different caller
                // Thread. No internal hook, reflection, or lock-name reconstruction.
                admittedInsideCallback = CanAcquireWriter(filename, password);
                return value;
            }
        }))
        {
            using var reader = shared.Query("rows", Query.All());
            if (!reader.Read() || reader.Current["_id"].AsInt32 != 1)
                throw new Exception("First eagerly materialized row was not consumed.");
            if (CanAcquireWriter(filename, password) != leased)
                throw new Exception("The real registry obstacle/control did not establish the expected native ownership.");
            if (leased && (!Directory.Exists(registry) || Directory.GetFiles(registry, "*.lease").Length == 0))
                throw new Exception("Control did not retain an actual reader lease.");
            armed = true;
            if (!reader.Read() || reader.Current["_id"].AsInt32 != 2 || !callbackReached)
                throw new Exception("The callback did not run on a later reader advance after Query returned.");
            if (leased ? closeError != null : closeError is not InvalidOperationException)
                throw new Exception("Unexpected leased/fallback close outcome.", closeError);
            if (leased && !admittedInsideCallback) throw new Exception("Leased reader unnecessarily retained writer ownership.");
            if (leased) WritePeer(filename, password);
            var observed = new List<int> { 1, 2 };
            while (reader.Read())
            {
                var row = reader.Current;
                if (row["value"].AsInt32 != row["_id"].AsInt32 * 10) throw new Exception("Snapshot value changed.");
                observed.Add(row["_id"].AsInt32);
            }
            if (!observed.SequenceEqual(Enumerable.Range(1, 4))) throw new Exception("Snapshot membership changed.");
            if (!leased && !admittedInsideCallback && shared.Pragma("USER_VERSION").AsInt32 != 0)
                throw new Exception("Refused close changed the still-open connection.");
            reader.Dispose();
            // A refusal on the fixed source must leave close retryable.
            shared.Dispose();
        }
        if (!leased)
        {
            if (File.ReadAllText(registry) != "proof obstacle: not a reader-registry directory")
                throw new Exception("The database mutated the unrelated obstacle file.");
            File.Delete(registry);
            WritePeer(filename, password);
        }
        for (var repeat = 0; repeat < 2; repeat++) VerifyCold(filename, password);
        Console.WriteLine($"CASE leased={leased} encrypted={password != null} callback-writer={admittedInsideCallback} cold-model=verified");
        return !leased && admittedInsideCallback;
    }

    private static bool CanAcquireWriter(string filename, string? password)
    {
        var admitted = false;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var peer = new LiteDatabase(new ConnectionString
                { Filename = filename, Password = password, Connection = ConnectionType.Shared });
                try
                {
                    using var tx = peer.BeginTransaction(TimeSpan.Zero);
                    tx.Rollback();
                    admitted = true;
                }
                catch (TimeoutException) { }
            }
            catch (Exception error) { failure = error; }
        }) { IsBackground = true };
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(10))) throw new Exception("Native admission probe did not finish.");
        if (failure != null) throw new Exception("Native admission probe failed unexpectedly.", failure);
        return admitted;
    }

    private static void WritePeer(string filename, string? password)
    {
        using var peer = new LiteDatabase(new ConnectionString
        { Filename = filename, Password = password, Connection = ConnectionType.Shared });
        using var tx = peer.BeginTransaction(TimeSpan.FromSeconds(5));
        tx.GetCollection("rows").Insert(Row(5));
        tx.Commit();
    }

    private static void VerifyCold(string filename, string? password)
    {
        using var cold = new LiteDatabase(new ConnectionString { Filename = filename, Password = password });
        var rows = cold.GetCollection("rows");
        var indexed = rows.Query().Where(Query.GTE("value", 10));
        if (indexed.GetPlan()["index"]["name"].AsString != "value") throw new Exception("Cold query did not use the value index.");
        var actual = indexed.ToArray().OrderBy(row => row["_id"].AsInt32).ToArray();
        if (!actual.Select(row => row["_id"].AsInt32).SequenceEqual(Enumerable.Range(1, 5)) ||
            actual.Any(row => row["value"].AsInt32 != row["_id"].AsInt32 * 10) ||
            rows.Count() != 5 || cold.GetCollection("sentinel").FindById(99)?["value"].AsString != "untouched")
            throw new Exception("Cold committed records/index/sentinel model failed.");
    }

    private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id * 10 };
}
