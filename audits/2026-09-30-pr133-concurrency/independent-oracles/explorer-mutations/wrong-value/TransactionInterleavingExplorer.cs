using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace LiteDB.ConcurrencyTesting
{
    /// <summary>Bounded schedules, shared by xUnit and the isolated fuzz runner.</summary>
    internal sealed class TransactionInterleavingExplorer
    {
        internal const int ScheduleCount = 32;
        private readonly ExplorerSchedule _schedule;
        private readonly ExplorerDatabase _model;
        private ExplorerDatabase _otherModel;
        private readonly List<IDisposable> _resources = new List<IDisposable>();
        private readonly bool _shared, _peer;
        private readonly int _order;
        private readonly ExplorerSchedule.Actor _a, _b, _c;

        private TransactionInterleavingExplorer(string file, bool shared, bool encrypted, int schedule, int seed)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file)));
            _schedule = new ExplorerSchedule(file + ".history", "seed=" + seed + " schedule=" + schedule +
                " shared=" + shared + " encrypted=" + encrypted);
            _shared = shared;
            _peer = (schedule & 1) != 0;
            _order = (schedule / 2) % 6;
            _model = new ExplorerDatabase(file, shared, encrypted);
            _a = _schedule.NewActor("A"); _b = _schedule.NewActor("B"); _c = _schedule.NewActor("C");
        }

        internal static void Run(string file, bool shared, bool encrypted, int schedule, int seed)
        {
            if (schedule < 0 || schedule >= ScheduleCount) throw new ArgumentOutOfRangeException(nameof(schedule));
            var run = new TransactionInterleavingExplorer(file, shared, encrypted, schedule, seed);
            Exception failure = null;
            try
            {
                if (schedule < 12) run.OverlapAndReaderTransfer(seed);
                else if (schedule < 24)
                {
                    if (shared) run.SharedAdmission(schedule % 6);
                    else run.IndependentHandles(schedule % 6);
                }
                else if (shared && schedule >= 28)
                {
                    if (schedule == 30) run._otherModel = new ExplorerDatabase(file + ".other", true, encrypted);
                    ExplorerOrdinaryCallbacks.Run(run._schedule, run._model, run._otherModel, run._resources, run._a, run._c, schedule - 28);
                }
                else
                {
                    var lifecycle = new ExplorerLifecycle(run._schedule, run._model, run._resources, run._a, run._b, run._c);
                    if (!shared && schedule < 26) lifecycle.Maintenance(schedule == 25);
                    else lifecycle.CloseActive((schedule & 1) != 0);
                }
                run._schedule.CheckActors();
            }
            catch (Exception error)
            {
                failure = error; error.Data["ExplorerFixture"] = file;
                error.Data["ExplorerSchedule"] = schedule; error.Data["ExplorerSeed"] = seed;
                run._schedule.Event("FAIL " + error);
            }
            finally
            {
                TransactionAdmission.Observe = null;
                var stopped = run._schedule.Stop();
                if (!stopped && failure == null) failure = new TimeoutException("A worker did not terminate; retained " + file);
                if (stopped)
                {
                    for (var i = run._resources.Count - 1; i >= 0; i--)
                    {
                        try { run._resources[i].Dispose(); }
                        catch (Exception error)
                        {
                            if (failure == null) failure = error;
                            else failure.Data["ExplorerCleanup" + i] = error;
                        }
                    }
                    // Cleanup failure must not suppress the independent persisted-state check.
                    try { run._model.VerifyCold(); run._otherModel?.VerifyCold(); }
                    catch (Exception error)
                    {
                        if (failure == null) failure = error;
                        else failure.Data["ExplorerColdOracle"] = error;
                    }
                    run._schedule.Event(failure == null ? "PASS cold-state-verified" : "FAIL retained " + file);
                    run._schedule.Dispose();
                }
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private T Keep<T>(T resource) where T : IDisposable { _resources.Add(resource); return resource; }
        private LiteDatabase Open() => Keep(_model.Open());
        private static IEnumerable<BsonDocument> Input(ExplorerSchedule.Boundary boundary, int id, int value, Action callback = null)
        { callback?.Invoke(); boundary.Hit(); yield return ExplorerDatabase.Row(id, value + 1); }

        private void OverlapAndReaderTransfer(int seed)
        {
            var db = Open();
            var peer = _peer ? Open() : db;
            _otherModel = new ExplorerDatabase(_model.Connection.Filename + ".other", _shared, _model.Connection.Password != null);
            var otherDatabase = Keep(_otherModel.Open());
            ILiteTransaction tx = null;
            _a.Run("begin", () => tx = db.BeginTransaction());
            Keep(tx);
            var rows = tx.GetCollection("rows");
            var inside = _schedule.NewBoundary("A inside bound input callback");
            var writing = _a.Invoke("insert-callback", () => rows.Insert(Input(inside, 2, 20, () =>
            {
                // An ordinary call never silently enlists in the handle. Shared must
                // refuse its native dependency, while Direct unrelated writes are legal.
                Action ordinary = () => peer.GetCollection("other").Insert(ExplorerDatabase.Row(2, 50));
                if (_shared) ExplorerDatabase.Refused(ordinary);
                else { ordinary(); _model.Acknowledge("other", 2, 50); }
                otherDatabase.GetCollection("rows").Insert(ExplorerDatabase.Row(2, 60));
                _otherModel.Acknowledge("rows", 2, 60);
                _schedule.Event("callback same-file policy and other-database positive control reached");
            })));
            inside.Wait();
            // Every permutation explores a different order of three enabled contenders while
            // A is verifiably executing the same handle. Timeouts are never accepted refusals.
            var actions = new Action[]
            {
                () => ExplorerDatabase.Refused(() => rows.FindById(1)),
                () => ExplorerDatabase.Refused(tx.Commit),
                () => ExplorerDatabase.Refused(tx.Rollback)
            };
            var permutations = new[] { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
                new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };
            foreach (var operation in permutations[_order]) _b.Run("overlap-" + operation, actions[operation]);
            ExplorerDatabase.Require(tx.State == LiteTransactionState.Active, "overlap aborted legitimate writer");
            inside.Release(); _a.Complete(writing);

            IEnumerator<BsonDocument> cursor = null;
            _a.Run("open-bound-cursor", () =>
            {
                cursor = rows.FindAll().GetEnumerator();
                ExplorerDatabase.Require(cursor.MoveNext(), "bound cursor empty");
            });
            Keep(cursor);
            _b.Run("commit-with-reader", () => ExplorerDatabase.Refused(tx.Commit));
            _c.Run("transfer-read-dispose", () =>
            {
                var ids = new List<int> { cursor.Current["_id"].AsInt32 };
                while (cursor.MoveNext()) ids.Add(cursor.Current["_id"].AsInt32);
                ids.Sort();
                ExplorerDatabase.Require(string.Join(",", ids) == "1,2", "bound reader transaction contents");
                cursor.Dispose();
            });
            if ((seed & 1) == 0)
            {
                _b.Run("commit-transferred", tx.Commit);
                _model.Acknowledge("rows", 2, 20);
            }
            else _b.Run("rollback-transferred", tx.Rollback);
            _c.Run("ordinary-read-after-owner-release", () => ExplorerDatabase.Require(
                peer.GetCollection("sentinel").FindById(42)["value"] == 900, "independent read"));
            _c.Run("disposed-reader-refusal", () =>
            {
                try { cursor.MoveNext(); }
                catch (ObjectDisposedException) { return; }
                throw new InvalidOperationException("Disposed reader accepted use");
            });
        }

        private void IndependentHandles(int variant)
        {
            var db = Open(); var peer = _peer ? Open() : db;
            ILiteTransaction first = null, second = null;
            _a.Run("begin-independent-A", () => first = db.BeginTransaction());
            _b.Run("begin-independent-B", () => second = peer.BeginTransaction());
            Keep(first); Keep(second);
            var firstInside = _schedule.NewBoundary("A pending writes");
            var secondInside = _schedule.NewBoundary("B pending writes");
            Action<ILiteTransaction, string> change = (tx, name) =>
            {
                var rows = tx.GetCollection(name);
                ExplorerDatabase.Require(rows.Update(ExplorerDatabase.Row(1, 30)), "update missed existing row");
                rows.Insert(ExplorerDatabase.Row(3, 40));
                ExplorerDatabase.Require(rows.Delete(3), "delete missed pending row");
            };
            _a.Run("update-delete-A", () => change(first, "rows"));
            _b.Run("update-delete-B", () => change(second, "other"));
            var aw = _a.Invoke("insert-A", () => first.GetCollection("rows").Insert(Input(firstInside, 2, 20)));
            firstInside.Wait();
            var bw = _b.Invoke("insert-B", () => second.GetCollection("other").Insert(Input(secondInside, 2, 20)));
            secondInside.Wait();
            _c.Run("no-uncommitted-visibility", () =>
            {
                foreach (var name in new[] { "rows", "other" })
                {
                    var rows = db.GetCollection(name);
                    ExplorerDatabase.Require(rows.FindById(1)["value"] == 10, "dirty updated value");
                    ExplorerDatabase.Require(rows.FindById(2) == null && rows.FindById(3) == null, "dirty insert");
                }
            });
            var reverse = variant % 2 != 0;
            var rollback = variant / 2;
            Action finishA = () =>
            {
                firstInside.Release(); _a.Complete(aw);
                _a.Run("finish-A", rollback == 1 ? (Action)first.Rollback : first.Commit);
                if (rollback != 1) { _model.Acknowledge("rows", 1, 30); _model.Acknowledge("rows", 2, 20); }
            };
            Action finishB = () =>
            {
                secondInside.Release(); _b.Complete(bw);
                _b.Run("finish-B", rollback == 2 ? (Action)second.Rollback : second.Commit);
                if (rollback != 2) { _model.Acknowledge("other", 1, 30); _model.Acknowledge("other", 2, 20); }
            };
            if (reverse) { finishB(); finishA(); } else { finishA(); finishB(); }
        }

        private void SharedAdmission(int variant)
        {
            var owner = Open(); var waiter = _peer ? Open() : owner;
            var legacy = variant >= 3;
            ILiteTransaction tx = null;
            _a.Run("owner-begin-write", () =>
            {
                if (legacy)
                {
                    ExplorerDatabase.Require(owner.BeginTrans(), "legacy begin failed");
                    owner.GetCollection("rows").Insert(ExplorerDatabase.Row(2, 20));
                }
                else { tx = owner.BeginTransaction(); tx.GetCollection("rows").Insert(ExplorerDatabase.Row(2, 20)); }
            });
            if (tx != null) Keep(tx);
            var wait = _schedule.NewBoundary(legacy ? "native-wait" : "local-wait");
            var stage = legacy ? "native-wait" : "local-wait";
            TransactionAdmission.Observe = value => { if (value == stage) wait.Observe(); };
            using var cancel = new CancellationTokenSource();
            var disposition = variant % 3; // cancellation, committed predecessor, closing waiter
            var pending = _b.Invoke("contended-begin", () =>
            {
                try
                {
                    using var next = waiter.BeginTransaction(Timeout.InfiniteTimeSpan, cancel.Token);
                    ExplorerDatabase.Require(disposition == 1, "cancelled/closing admission succeeded");
                    next.GetCollection("other").Insert(ExplorerDatabase.Row(2, 20));
                    next.Commit();
                }
                catch (OperationCanceledException error)
                {
                    ExplorerDatabase.Require(disposition != 1, "legitimate waiter cancelled");
                    if (disposition == 0) ExplorerDatabase.Require(error.CancellationToken == cancel.Token, "wrong cancellation token");
                }
            });
            wait.Wait();
            ExplorerDatabase.Require(!pending.Done.IsSet, "contended begin already finished");
            if (disposition == 0) { cancel.Cancel(); _b.Complete(pending); }
            if (disposition == 2 && !ReferenceEquals(owner, waiter))
                _c.Run("close-waiter", waiter.Dispose);
            // Closing the owner's facade must cancel the pending begin and roll back its owner.
            if (disposition == 2 && ReferenceEquals(owner, waiter))
            {
                _c.Run("close-owner-session", owner.Dispose);
                _b.Complete(pending);
            }
            else
            {
                _a.Run("owner-commit", () =>
                {
                    if (legacy) ExplorerDatabase.Require(owner.Commit(), "legacy commit failed"); else tx.Commit();
                });
                _model.Acknowledge("rows", 2, 20);
                _b.Complete(pending);
                if (disposition == 1) _model.Acknowledge("other", 2, 20);
            }
        }
    }
}
