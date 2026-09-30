using System;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using LiteDB.Engine;
using Xunit;
using static LiteDB.Tests.Engine.TransactionHandleSharedCallback_Tests;

namespace LiteDB.Tests.Engine
{
    public class TransactionHandleSharedCallbackControl_Tests
    {
        [Theory]
        [InlineData(null, false)] [InlineData("secret", false)]
        [InlineData(null, true)] [InlineData("secret", true)]
        public void Nested_other_database_handle_keeps_outer_dependency_and_restores_it(string password, bool otherShared)
        {
            using var first = new TempFile();
            using var second = new TempFile();
            Seed(first, password);
            Seed(second, password);
            using (var aEngine = new SharedEngine(Settings(first, password)))
            using (var a = new LiteDatabase(aEngine))
            using (var b = new LiteDatabase(new ConnectionString { Filename = second, Password = password,
                Connection = otherShared ? ConnectionType.Shared : ConnectionType.Direct }))
            using (var outer = a.BeginTransaction())
            {
                Turnstile(aEngine).BeforeContendedWait = _ => throw new OperationCanceledException("outer native self-wait");
                if (otherShared)
                    Turnstile((SharedEngine)typeof(LiteDatabase).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)
                        .GetValue(b)).BeforeContendedWait = _ => throw new OperationCanceledException("inner native self-wait");
                IEnumerable<BsonDocument> Nested()
                {
                    Assert.Throws<InvalidOperationException>(() => a.UserVersion);
                    if (otherShared) Assert.Throws<InvalidOperationException>(() => b.UserVersion);
                    else b.GetCollection("independent").Insert(Row(7));
                    yield return Row(4);
                }
                IEnumerable<BsonDocument> Input()
                {
                    // Ordinary work in another Shared namespace remains independent.
                    Assert.Equal(0, b.UserVersion);
                    using (var inner = b.BeginTransaction())
                    {
                        inner.GetCollection("rows").Insert(Row(3));
                        inner.GetCollection("rows").Insert(Nested());
                        Assert.Equal(LiteTransactionState.Active, inner.State);
                        inner.Rollback();
                    }
                    // The inner scope is gone, while the enclosing Shared handle remains bound.
                    Assert.Equal(0, b.UserVersion);
                    Assert.Throws<InvalidOperationException>(() => a.UserVersion);
                    yield return Row(4);
                }
                outer.GetCollection("rows").Insert(Row(3));
                outer.GetCollection("rows").Insert(Input());
                Assert.Equal(LiteTransactionState.Active, outer.State);
                outer.Rollback();
                PeerWrite(a);
                PeerWrite(b);
            }
            Verify(first, password, new[] { 1, 2, 5 });
            Verify(second, password, new[] { 1, 2, 5 });
            using var cold = new LiteDatabase(new ConnectionString { Filename = second, Password = password });
            Assert.Equal(otherShared ? 0 : 1, cold.GetCollection("independent").Count());
            if (!otherShared) Assert.Equal(70, cold.GetCollection("independent").FindById(7)["value"].AsInt32);
        }

        [Theory]
        [InlineData(null)] [InlineData("secret")]
        public void Ordinary_other_database_callback_cannot_erase_enclosing_shared_dependency(string password)
        {
            using var first = new TempFile();
            using var second = new TempFile();
            Seed(first, password);
            Seed(second, password);
            using (var engine = new SharedEngine(Settings(first, password)))
            using (var a = new LiteDatabase(engine))
            using (var b = new LiteDatabase(new ConnectionString { Filename = second, Password = password }))
            using (var tx = a.BeginTransaction())
            {
                Turnstile(engine).BeforeContendedWait = _ => throw new OperationCanceledException("suppressed native self-wait");
                IEnumerable<BsonDocument> Inner()
                {
                    Assert.Throws<InvalidOperationException>(() => a.UserVersion);
                    yield return Row(3);
                }
                IEnumerable<BsonDocument> Input()
                {
                    b.GetCollection("rows").Insert(Inner());
                    yield return Row(3);
                }
                tx.GetCollection("rows").Insert(Input());
                tx.Rollback();
                PeerWrite(a);
                PeerWrite(b);
            }
            Verify(first, password, new[] { 1, 2, 5 });
            Verify(second, password, new[] { 1, 2, 3, 5 });
        }

        [Theory]
        [InlineData(null)] [InlineData("secret")]
        public void Ordinary_wait_for_idle_handle_can_finish_after_cross_thread_completion(string password)
        {
            using var file = new TempFile();
            Seed(file, password);
            using (var engine = new SharedEngine(Settings(file, password)))
            using (var db = new LiteDatabase(engine))
            using (var tx = db.BeginTransaction())
            using (var waiting = new ManualResetEventSlim())
            {
                tx.GetCollection("rows").Insert(Row(3));
                Turnstile(engine).BeforeContendedWait = _ => waiting.Set();
                Exception failure = null;
                var completing = new Thread(() =>
                {
                    try { Assert.True(waiting.Wait(TimeSpan.FromSeconds(5))); tx.Commit(); }
                    catch (Exception error) { failure = error; }
                }) { IsBackground = true };
                completing.Start();
                // Enter/Exit from earlier handle operations must leave no ambient dependency.
                Assert.Equal(0, db.UserVersion);
                Assert.True(completing.Join(TimeSpan.FromSeconds(10)));
                Assert.True(waiting.IsSet);
                Assert.Null(failure);
                Assert.Equal(LiteTransactionState.Committed, tx.State);
                PeerWrite(db);
            }
            Verify(file, password, new[] { 1, 2, 3, 5 });
        }

        [Theory]
        [InlineData(null)] [InlineData("secret")]
        public async Task Ordinary_admission_cancellation_does_not_release_an_idle_handle(string password)
        {
            using var file = new TempFile();
            Seed(file, password);
            using (var owner = new LiteDatabase(new SharedEngine(Settings(file, password))))
            using (var waitingEngine = new SharedEngine(Settings(file, password)))
            using (var waiter = new LiteDatabase(waitingEngine))
            using (var tx = owner.BeginTransaction())
            using (var reached = new ManualResetEventSlim())
            {
                tx.GetCollection("rows").Insert(Row(3));
                Turnstile(waitingEngine).BeforeContendedWait = _ => reached.Set();
                var call = Task.Run(() => Record.Exception(() => waiter.UserVersion));
                try
                {
                    Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
                    waiter.Dispose();
                    Assert.IsType<OperationCanceledException>(await call);
                    Assert.Equal(LiteTransactionState.Active, tx.State);
                    Assert.NotNull(tx.GetCollection("rows").FindById(3));
                    tx.Commit();
                }
                finally { waiter.Dispose(); }
                PeerWrite(owner);
            }
            Verify(file, password, new[] { 1, 2, 3, 5 });
        }
    }
}
