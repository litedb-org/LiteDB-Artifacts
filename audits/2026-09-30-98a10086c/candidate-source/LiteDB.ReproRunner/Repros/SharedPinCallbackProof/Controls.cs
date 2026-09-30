using LiteDB;

namespace SharedPinCallbackProof;
internal static class Controls
{
    internal static bool Run(string path, string? password, string scenario)
    {
        if (scenario.StartsWith("leased", StringComparison.Ordinal)) Leased(path, password, scenario.EndsWith("peer", StringComparison.Ordinal));
        else if (scenario == "idle-handoff") Idle(path, password);
        else Pin(path, password, scenario);
        Console.WriteLine("CONTROL_VERIFIED " + scenario); return false;
    }
    private static void Pin(string path, string? password, string scenario)
    {
        var otherPath = path + ".other"; Fixture.Seed(otherPath, password);
        using (var shared = Fixture.Shared(path, password))
        using (var db = new LiteDatabase(shared))
        using (var other = Fixture.Shared(otherPath, password))
        {
            using var anchor = scenario == "no-anchor" ? null : shared.Query("rows", new Query());
            if (anchor != null) { if (!anchor.Read()) throw new Exception("Control anchor missing."); NativeProbe.LeasedAnchor(shared, anchor); }
            using var otherAnchor = scenario == "other-database" ? other.Query("rows", new Query()) : null;
            if (otherAnchor != null) { if (!otherAnchor.Read()) throw new Exception("Other database anchor missing."); NativeProbe.LeasedAnchor(other, otherAnchor); }
            if (scenario == "normal-pin")
            {
                IEnumerable<BsonDocument> Input() { NativeProbe.ActivePin(shared); yield return Fixture.Row(40); }
                shared.Insert("ordinary", Input(), BsonAutoId.Int32);
            }
            else
            {
                using var tx = db.BeginTransaction(); tx.GetCollection("rows").Insert(Fixture.Row(20)); var calls = 0;
                IEnumerable<BsonDocument> Input()
                {
                    calls++;
                    if (scenario == "other-database")
                    {
                        IEnumerable<BsonDocument> OtherInput() { NativeProbe.ActivePin(other); yield return Fixture.Row(40); }
                        other.Insert("ordinary", OtherInput(), BsonAutoId.Int32);
                    }
                    else
                    {
                        try { shared.Insert("ordinary", new[] { Fixture.Row(99) }, BsonAutoId.Int32); throw new Exception("No-anchor dependency succeeded."); }
                        catch (InvalidOperationException) { }
                    }
                    yield return Fixture.Row(21);
                }
                tx.GetCollection("rows").Insert(Input());
                if (calls != 1 || tx.State != LiteTransactionState.Active || tx.GetCollection("rows").FindById(20) == null)
                    throw new Exception("Control callback damaged handle.");
                tx.Rollback();
            }
        }
        Peer(path, password); Fixture.Verify(path, password, new[] { 1, 2, 3, 30 }, scenario == "normal-pin");
        Fixture.Verify(otherPath, password, new[] { 1, 2, 3 }, scenario == "other-database");
    }
    private static void Leased(string path, string? password, bool peer)
    {
        Action? callback = null; var calls = 0;
        using (var shared = Fixture.Shared(path, password, (_, value) => { if (value.AsDocument["_id"] == 2) callback?.Invoke(); return value; }))
        using (var db = new LiteDatabase(shared))
        {
            var target = peer ? new LiteDatabase(Fixture.Shared(path, password)) : db;
            using var reader = shared.Query("rows", new Query());
            if (!reader.Read()) throw new Exception("Leased control reader missing."); NativeProbe.LeasedAnchor(shared, reader);
            callback = () =>
            {
                callback = null; calls++;
                using var tx = target.BeginTransaction(TimeSpan.FromSeconds(5));
                tx.GetCollection("rows").Insert(Fixture.Row(20)); tx.Commit();
            };
            Exception? error = null;
            var thread = new Thread(() => { try { if (!reader.Read() || reader.Current["_id"] != 2) throw new Exception("Leased control late row missing."); } catch (Exception failure) { error = failure; } }) { IsBackground = true };
            thread.Start(); if (!thread.Join(TimeSpan.FromSeconds(10)) || error != null || calls != 1) throw new Exception("Independent leased callback was refused or blocked.", error);
            reader.Dispose(); if (peer) target.Dispose();
        }
        Peer(path, password); Fixture.Verify(path, password, new[] { 1, 2, 3, 20, 30 });
    }
    private static void Idle(string path, string? password)
    {
        using (var shared = Fixture.Shared(path, password))
        using (var db = new LiteDatabase(shared))
        using (var anchor = shared.Query("rows", new Query()))
        {
            if (!anchor.Read()) throw new Exception("Idle anchor missing."); NativeProbe.LeasedAnchor(shared, anchor);
            using var tx = db.BeginTransaction(); tx.GetCollection("rows").Insert(Fixture.Row(20));
            Exception? error = null; var done = new ManualResetEventSlim();
            var caller = new Thread(() => { try
            {
#if PIN_WRITE_PROOF
                _ = db.UserVersion;
#else
                using var next = db.BeginTransaction();
                next.GetCollection("rows").Insert(Fixture.Row(21)); next.Commit();
#endif
            } catch (Exception failure) { error = failure; } finally { done.Set(); } }) { IsBackground = true };
            caller.Start();
#if PIN_WRITE_PROOF
            Func<bool> waiting = () => (int)NativeProbe.Required(shared, "_mutexWaiters") > 0 && NativeProbe.QueuedAtTurnstile(shared);
#else
            Func<bool> waiting = () => NativeProbe.SessionEntered(db, caller) && NativeProbe.Gate(shared)?.CurrentCount == 0;
#endif
            if (!NativeProbe.Wait(shared, caller, done, waiting) || done.Wait(TimeSpan.FromMilliseconds(100)))
                throw new Exception("Idle control did not enter the independently completable ownership wait.", error);
            Exception? completion = null;
            var completing = new Thread(() => { try { tx.Commit(); } catch (Exception failure) { completion = failure; } }) { IsBackground = true };
            completing.Start();
            if (!completing.Join(TimeSpan.FromSeconds(10)) || completion != null || !caller.Join(TimeSpan.FromSeconds(10)) || error != null)
                throw new Exception("Idle handle could not be completed independently.", completion ?? error);
        }
#if PIN_WRITE_PROOF
        var ids = new[] { 1, 2, 3, 20, 30 };
#else
        var ids = new[] { 1, 2, 3, 20, 21, 30 };
#endif
        Peer(path, password); Fixture.Verify(path, password, ids);
    }
    private static void Peer(string path, string? password)
    { if (Processes.Run("--peer", path, password, "") != 10) throw new Exception("Control peer did not commit."); }
}
