using System;
using System.Collections.Generic;
using System.Reflection;
using LiteDB.Client.Direct;
using LiteDB.Engine;

namespace LiteDB.ConcurrencyTesting
{
    internal sealed class ExplorerLifecycle
    {
        private readonly ExplorerSchedule _schedule;
        private readonly ExplorerDatabase _model;
        private readonly List<IDisposable> _resources;
        private readonly ExplorerSchedule.Actor _a, _b, _c;
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

        internal ExplorerLifecycle(ExplorerSchedule schedule, ExplorerDatabase model, List<IDisposable> resources,
            ExplorerSchedule.Actor a, ExplorerSchedule.Actor b, ExplorerSchedule.Actor c)
        { _schedule = schedule; _model = model; _resources = resources; _a = a; _b = b; _c = c; }
        private T Keep<T>(T value) where T : IDisposable { _resources.Add(value); return value; }
        private static object Field(object instance, string name) => instance.GetType().GetField(name, Fields)?.GetValue(instance)
            ?? throw new InvalidOperationException("Missing observation field " + name);
        private static IEnumerable<BsonDocument> Input(ExplorerSchedule.Boundary hold)
        { yield return ExplorerDatabase.Row(2, 20); hold.Hit(); yield return ExplorerDatabase.Row(3, 30); }

        internal void CloseActive(bool releaseCallbackFirst)
        {
            var db = Keep(_model.Open());
            ILiteTransaction tx = null;
            _a.Run("begin-close-owner", () => tx = db.BeginTransaction());
            Keep(tx);
            var input = _schedule.NewBoundary("active callback before close");
            var closing = _schedule.NewBoundary("session closing published before dispatch");
            var session = (SessionLifetime)Field(db, "_lifetime");
            session.BeforeCloseDispatch = closing.Hit;
            var active = _a.Invoke("active-insert", () => tx.GetCollection("rows").Insert(Input(input)));
            input.Wait();
            var close = _b.Invoke("dispose-active-session", db.Dispose);
            closing.Wait();
            _c.Run("overlap-during-close", () => ExplorerDatabase.Refused(tx.Commit));
            if (releaseCallbackFirst) { input.Release(); closing.Release(); }
            else { closing.Release(); input.Release(); }
            _a.Complete(active); _b.Complete(close);
            session.BeforeCloseDispatch = null;
            ExplorerDatabase.Require(tx.State == LiteTransactionState.RolledBack, "close did not roll back active handle");
        }

        internal void Maintenance(bool checkpoint)
        {
            var db = Keep(_model.Open());
            var lease = (DirectEngineLease)Field(db, "_engine");
            var operations = (OperationLifetime)Field(lease.Engine, "_operations");
            var inside = _schedule.NewBoundary("ordinary write callback under operation lease");
            var fenced = _schedule.NewBoundary("late ordinary operation fenced by maintenance");
            var active = _a.Invoke("ordinary-insert", () => db.GetCollection("rows").Insert(Input(inside)));
            inside.Wait();
            operations.WaitingForMaintenance = fenced.Observe;
            var maintenance = _b.Invoke("rebuild", () => db.Rebuild());
            _schedule.Until(() =>
            {
                lock (Field(operations, "_gate")) return (int)Field(operations, "_waitingExclusive") != 0;
            }, "maintenance registered while callback holds engine lease");
            var late = _c.Invoke("late-read", () => ExplorerDatabase.Require(
                db.GetCollection("rows").Count() == 3, "late reader saw incomplete ordinary commit"));
            fenced.Wait();
            ExplorerDatabase.Require(!active.Done.IsSet && !maintenance.Done.IsSet && !late.Done.IsSet,
                "maintenance did not fence the observed active/queued operations");
            inside.Release();
            _a.Complete(active); _b.Complete(maintenance); _c.Complete(late);
            operations.WaitingForMaintenance = null;
            _model.Acknowledge("rows", 2, 20); _model.Acknowledge("rows", 3, 30);
            if (checkpoint) _b.Run("checkpoint-after-rebuild", () => db.Checkpoint());
        }
    }
}
