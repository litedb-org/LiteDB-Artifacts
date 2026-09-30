using LiteDB;

namespace SharedPinCallbackProof;
internal static class Cases
{
    internal static bool Run(string path, string? password, string scenario) => scenario.StartsWith("write", StringComparison.Ordinal)
        ? Write(path, password, scenario == "write-commit") : Begin(path, password, scenario);

    private static bool Write(string path, string? password, bool commit)
    {
        var shared = Fixture.Shared(path, password); var db = new LiteDatabase(shared);
        var entered = new ManualResetEventSlim(); var done = new ManualResetEventSlim(); Exception? error = null;
        var worker = new Thread(() =>
        {
            try
            {
                using var anchor = shared.Query("rows", new Query()); if (!anchor.Read()) throw new Exception("Anchor did not start.");
                NativeProbe.LeasedAnchor(shared, anchor);
                using var tx = db.BeginTransaction(); tx.GetCollection("rows").Insert(Fixture.Row(20));
                var refusals = 0;
                IEnumerable<BsonDocument> Input()
                {
                    NativeProbe.LeasedAnchor(shared, anchor); NativeProbe.RequireExcluded(shared); entered.Set(); Console.WriteLine("PIN_WRITE_CALLBACK_ENTERED");
                    try { shared.Insert("ordinary", new[] { Fixture.Row(99) }, BsonAutoId.Int32); }
                    catch (InvalidOperationException) { refusals++; }
                    NativeProbe.RequireExcluded(shared);
                    yield return Fixture.Row(21);
                }
                tx.GetCollection("rows").Insert(Input());
                if (refusals != 1 || tx.State != LiteTransactionState.Active || tx.GetCollection("rows").FindById(20) == null)
                    throw new Exception("Refusal did not preserve the active handle and prior write.");
                if (commit) tx.Commit(); else tx.Rollback();
                anchor.Dispose(); db.Dispose();
            }
            catch (Exception failure) { error = failure; }
            finally { done.Set(); }
        }) { IsBackground = true };
        worker.Start(); if (!entered.Wait(TimeSpan.FromSeconds(15))) throw new Exception("Input callback did not start.", error);
        var boundary = NativeProbe.Wait(shared, worker, done, () => (int)NativeProbe.Required(shared, "_mutexWaiters") > 0 && NativeProbe.QueuedAtTurnstile(shared));
        if (boundary && !done.Wait(TimeSpan.FromSeconds(1)))
        {
            Console.WriteLine("START_PIN_NATIVE_WAIT_CONFIRMED parentWaiters>0 occupiedTurnstile nativeExcluded");
            GC.KeepAlive(db); return true;
        }
        if (!worker.Join(TimeSpan.FromSeconds(10)) || error != null) throw new Exception("Fixed pin callback failed.", error);
        return false;
    }

    private static bool Begin(string path, string? password, string scenario)
    {
        Action? transform = null;
        var shared = Fixture.Shared(path, password, (_, value) => { if (value.AsDocument["_id"] == 2) transform?.Invoke(); return value; });
        var db = new LiteDatabase(shared);
        var target = scenario.EndsWith("peer", StringComparison.Ordinal)
            ? new LiteDatabase(Fixture.Shared(path, password)) : db;
        var isReader = scenario.StartsWith("reader", StringComparison.Ordinal);
        var targetShared = scenario.EndsWith("peer", StringComparison.Ordinal)
            ? (SharedEngine)NativeProbe.Required(target, "_engine") : shared;
        // Warm an inert wrapper before establishing the enclosing owner so the
        // monitor can identify the exact child that attempts native admission.
        using (var warm = target.BeginTransaction()) warm.Rollback();
        var child = (SharedEngine)NativeProbe.Required(targetShared, "_cachedTransactionChild");
        var entered = new ManualResetEventSlim(); var done = new ManualResetEventSlim();
        Exception? error = null; var refusals = 0; var callbacks = 0;
        IBsonDataReader? reader = null; object? pin = null;
        void Callback()
        {
            transform = null; callbacks++;
            NativeProbe.IdleChild(targetShared, child);
            if (!isReader) pin = NativeProbe.ActivePin(shared);
            else if (NativeProbe.Field(reader!, "_ownedSnapshot") != null) throw new Exception("Transferred reader is independently leased.");
            NativeProbe.RequireExcluded(shared);
            entered.Set(); Console.WriteLine("ORDINARY_CALLBACK_DEFAULT_BEGIN_ENTERED " + scenario);
            try { using var unexpected = target.BeginTransaction(); throw new Exception("Default dependent handle unexpectedly opened."); }
            catch (InvalidOperationException) { refusals++; }
            NativeProbe.RequireExcluded(shared);
            NativeProbe.NoRegisteredHandle(target);
            NativeProbe.IdleChild(targetShared, child);
            if (refusals != 1) throw new Exception("Begin must refuse before admission.");
        }
        if (isReader)
        {
            reader = shared.Query("rows", new Query { ForUpdate = true });
            if (!reader.Read() || reader.Current["_id"] != 1 || NativeProbe.Field(reader, "_ownedSnapshot") != null)
                throw new Exception("Transferred mutex reader setup failed.");
            transform = Callback;
        }
        var worker = new Thread(() =>
        {
            try
            {
                if (isReader)
                {
                    if (!reader!.Read() || reader.Current["_id"] != 2) throw new Exception("Transferred reader did not advance.");
                    reader.Dispose();
                }
                else
                {
                    using var anchor = shared.Query("rows", new Query()); if (!anchor.Read()) throw new Exception("Anchor did not start.");
                    NativeProbe.LeasedAnchor(shared, anchor);
                    IEnumerable<BsonDocument> Input() { Callback(); yield return Fixture.Row(40); }
                    shared.Insert("ordinary", Input(), BsonAutoId.Int32);
                }
                if (callbacks != 1 || refusals != 1) throw new Exception("Callback count or refusal count mismatch.");
                target.Dispose(); if (!ReferenceEquals(target, db)) db.Dispose();
            }
            catch (Exception failure) { error = failure; }
            finally { done.Set(); }
        }) { IsBackground = true };
        worker.Start(); if (!entered.Wait(TimeSpan.FromSeconds(15))) throw new Exception("Ordinary callback did not start.", error);
        bool NativeWait() => NativeProbe.Gate(shared)?.CurrentCount == 0 &&
            NativeProbe.Field(targetShared, "_cachedTransactionChild") == null &&
            (int)NativeProbe.Required(child, "_mutexWaiters") > 0;
        var acquired = NativeProbe.Wait(shared, worker, done, NativeWait);
        if (!acquired)
        {
            if (!worker.Join(TimeSpan.FromSeconds(10)) || error != null) throw new Exception("Fixed ordinary callback failed.", error);
            return false;
        }
        if (done.Wait(TimeSpan.FromSeconds(1)) || !NativeWait() || !NativeProbe.Excluded(shared))
            throw new Exception("Default begin did not retain the confirmed child native wait.", error);
        if (!isReader && !ReferenceEquals(pin, NativeProbe.ActivePin(shared))) throw new Exception("The enclosing pin was replaced.");
        NativeProbe.NoRegisteredHandle(target);
        Console.WriteLine("DEFAULT_BEGIN_NATIVE_DEPENDENCY_CONFIRMED cachedChildCheckedOut childNativeWaiters>0 localGateAcquired nativeExcluded");
        GC.KeepAlive(reader); GC.KeepAlive(db); GC.KeepAlive(target); return true;
    }
}
