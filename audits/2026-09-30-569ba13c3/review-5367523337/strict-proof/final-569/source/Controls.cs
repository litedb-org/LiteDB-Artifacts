using LiteDB;
using LiteDB.Engine;

namespace Issue_3067_SharedCallbackNativeWait;
internal static class Controls
{
    internal static bool Run(string path, string? password, string scenario)
    {
        if (scenario == "other-database" || scenario == "direct") Independent(path, password, scenario == "direct");
        else Admission(path, password, scenario == "cancel-admission");
        Console.WriteLine("CONTROL_VERIFIED " + scenario);
        return false;
    }
    private static void Independent(string path, string? password, bool direct)
    {
        var otherPath = path + ".other"; Program.Seed(otherPath, password);
        using (var db = new LiteDatabase(new ConnectionString { Filename = path, Password = password, Connection = direct ? ConnectionType.Direct : ConnectionType.Shared }))
        using (var other = new LiteDatabase(new ConnectionString { Filename = otherPath, Password = password, Connection = ConnectionType.Shared }))
        using (var tx = db.BeginTransaction())
        {
            var callbacks = 0;
            IEnumerable<BsonDocument> Input()
            {
                callbacks++;
                if (direct)
                {
                    if (db.GetCollection("rows").FindById(20) != null) throw new Exception("Direct ordinary callback implicitly enlisted.");
                    db.GetCollection("unrelated").Insert(Program.Row(90));
                }
                else
                {
                    _ = other.UserVersion;
                    other.GetCollection("rows").Insert(Program.Row(30));
                }
                yield return Program.Row(21);
            }
            tx.GetCollection("rows").Insert(Program.Row(20));
            tx.GetCollection("rows").Insert(Input());
            if (callbacks != 1 || tx.State != LiteTransactionState.Active || tx.GetCollection("rows").FindById(20) == null)
                throw new Exception("Independent callback damaged handle.");
            tx.Rollback();
        }
        Program.Verify(path, password, new[] { 1, 2, 3 }, direct);
        Program.Verify(otherPath, password, direct ? new[] { 1, 2, 3 } : new[] { 1, 2, 3, 30 });
    }
    private static void Admission(string path, string? password, bool cancel)
    {
        var shared = new SharedEngine(new EngineSettings { Filename = path, Password = password });
        var db = new LiteDatabase(shared);
        var owner = cancel ? new LiteDatabase(new ConnectionString { Filename = path, Password = password, Connection = ConnectionType.Shared }) : db;
        using var tx = owner.BeginTransaction(); tx.GetCollection("rows").Insert(Program.Row(20));
        var finished = new ManualResetEventSlim(); Exception? error = null;
        var ordinary = new Thread(() =>
        {
            try { _ = db.UserVersion; }
            catch (Exception failure) { error = failure; }
            finally { finished.Set(); }
        }) { IsBackground = true };
        ordinary.Start();
        if (!Processes.WaitNativeBoundary(shared, ordinary, finished) || finished.IsSet)
            throw new Exception("Idle handle admission control did not reach the native wait.", error);
        if (cancel) Processes.Close(db);
        else
        {
            Exception? handoffError = null;
            var completing = new Thread(() => { try { tx.Commit(); } catch (Exception failure) { handoffError = failure; } }) { IsBackground = true };
            completing.Start();
            if (!completing.Join(TimeSpan.FromSeconds(10)) || handoffError != null) throw new Exception("Idle handle cross-thread handoff failed.", handoffError);
        }
        if (!ordinary.Join(TimeSpan.FromSeconds(10)) || (cancel ? error is not OperationCanceledException : error != null))
            throw new Exception("Ordinary admission completion/cancellation failed.", error);
        db.Dispose();
        if (cancel) { tx.Rollback(); owner.Dispose(); }
        if (Processes.Run("--peer", path, password, "") != 10) throw new Exception("Admission control peer failed.");
        Program.Verify(path, password, cancel ? new[] { 1, 2, 3, 30 } : new[] { 1, 2, 3, 20, 30 });
    }
}
