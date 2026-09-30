using System.Collections;
using System.Diagnostics;
using System.Reflection;
using LiteDB;

namespace SharedPinCallbackProof;
internal static class NativeProbe
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static object? Field(object owner, string name) => (owner.GetType().GetField(name, Private) ?? throw new Exception("Missing production field metadata: " + name)).GetValue(owner);
    internal static object Required(object owner, string name) => Field(owner, name) ?? throw new Exception("Missing production field: " + name);
    internal static SemaphoreSlim? Gate(SharedEngine shared)
    {
        var gates = (IDictionary)(typeof(SharedEngine).GetField("TransactionWriters", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null)
            ?? throw new Exception("Missing production writer gate registry."));
        return gates[Required(shared, "_mutexName")] as SemaphoreSlim;
    }
    internal static void LeasedAnchor(SharedEngine shared, IBsonDataReader reader)
    {
        if (Field(reader, "_ownedSnapshot") == null || Field(shared, "_pin") != null) throw new Exception("Anchor is not an independent leased reader without a pin.");
        var readers = (IDictionary)Required(shared, "_localReaders");
        lock (readers)
            if (!readers.Contains(Environment.CurrentManagedThreadId)) throw new Exception("Callback thread is not the registered local reader.");
        Console.WriteLine("LEASED_LOCAL_ANCHOR_CONFIRMED");
    }
    internal static object ActivePin(SharedEngine shared)
    {
        var pin = Required(shared, "_pin");
        lock (Required(pin, "_sync"))
            if ((int)Required(pin, "_operations") <= 0 || (int)Required(pin, "_holds") != 0)
                throw new Exception("Required active pin with zero holds was not established.");
        Console.WriteLine("PIN_OPERATION_POSITIVE_HOLDS_ZERO"); return pin;
    }
    internal static bool Excluded(SharedEngine shared)
    {
        var mutex = (Mutex)Required(shared, "_mutex");
        try { if (!mutex.WaitOne(0)) return true; }
        catch (AbandonedMutexException) { }
        mutex.ReleaseMutex(); return false;
    }
    internal static void RequireExcluded(SharedEngine shared)
    {
        Exception? error = null;
        var probe = new Thread(() =>
        {
            try { if (!Excluded(shared)) throw new Exception("Enclosing native owner released during its callback."); }
            catch (Exception failure) { error = failure; }
        }) { IsBackground = true };
        probe.Start();
        if (!probe.Join(TimeSpan.FromSeconds(5)) || error != null)
            throw new Exception("Independent native exclusion probe failed.", error);
        Console.WriteLine("CALLBACK_NATIVE_EXCLUSION_VERIFIED");
    }
    internal static void IdleChild(SharedEngine target, SharedEngine child)
    {
        if (!ReferenceEquals(Field(target, "_cachedTransactionChild"), child) ||
            Gate(target)?.CurrentCount != 1 || (int)Required(child, "_mutexWaiters") != 0)
            throw new Exception("Refused begin changed the cached child or entered admission.");
    }
    internal static bool QueuedAtTurnstile(SharedEngine shared)
    {
        var turn = Required(shared, "_turnstile");
        return (bool)(turn.GetType().GetMethod("HasWaiter")?.Invoke(turn, null) ?? throw new Exception("Missing native queue probe."));
    }
    internal static bool Wait(SharedEngine shared, Thread worker, ManualResetEventSlim done, Func<bool> predicate)
    {
        var wait = Stopwatch.StartNew();
        while (wait.Elapsed < TimeSpan.FromSeconds(10))
        {
            if (done.IsSet) return false;
            if (predicate() && Excluded(shared) && (worker.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0) return true;
            Thread.Sleep(1);
        }
        throw new Exception("No refusal or discriminating native dependency boundary observed.");
    }
    internal static bool SessionEntered(LiteDatabase db, Thread caller)
    {
        var lifetime = Required(db, "_lifetime");
        lock (Required(lifetime, "_gate"))
            return ((IDictionary)Required(lifetime, "_threads")).Contains(caller);
    }
    internal static void NoRegisteredHandle(LiteDatabase db)
    {
        var lifetime = Required(db, "_lifetime");
        lock (Required(lifetime, "_gate"))
        {
            var handles = Required(lifetime, "_transactions");
            if ((int)(handles.GetType().GetProperty("Count")?.GetValue(handles) ?? -1) != 0)
                throw new Exception("A refused nested handle was registered.");
        }
    }
}
