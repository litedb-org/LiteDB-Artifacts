using System;
using System.Collections.Generic;
using System.Threading;
using Xunit;
using static LiteDB.Tests.Engine.TransactionHandleSharedCallback_Tests;

namespace LiteDB.Tests.Engine
{
    public class TransactionHandleSharedCallbackBegin_Tests
    {
        [Theory]
        [InlineData(null, false)] [InlineData("secret", false)]
        [InlineData(null, true)] [InlineData("secret", true)]
        public void Callback_begin_refuses_same_namespace_before_local_admission(string password, bool peerFacade)
        {
            using var file = new TempFile();
            Seed(file, password);
            using (var db = new LiteDatabase(new SharedEngine(Settings(file, password))))
            using (var peer = new LiteDatabase(new SharedEngine(Settings(file, password))))
            using (var tx = db.BeginTransaction())
            using (var cancelSelfWait = new CancellationTokenSource())
            {
                tx.GetCollection("rows").Insert(Row(3));
                var waits = 0;
                var previous = TransactionAdmission.Observe;
                TransactionAdmission.Observe = stage =>
                {
                    if (stage != "local-wait") return;
                    waits++;
                    // Bound a regressed infinite begin only after reaching its real gate.
                    cancelSelfWait.CancelAfter(TimeSpan.FromMilliseconds(30));
                };
                Exception refusal = null;
                IEnumerable<BsonDocument> Input()
                {
                    refusal = Record.Exception(() =>
                    {
                        using var nested = (peerFacade ? peer : db).BeginTransaction(
                            Timeout.InfiniteTimeSpan, cancelSelfWait.Token);
                    });
                    yield return Row(4);
                }
                try { tx.GetCollection("rows").Insert(Input()); }
                finally { TransactionAdmission.Observe = previous; }
                Assert.IsType<InvalidOperationException>(refusal);
                Assert.Equal(0, waits);
                Assert.Equal(LiteTransactionState.Active, tx.State);
                Assert.NotNull(tx.GetCollection("rows").FindById(3));
                Assert.NotNull(tx.GetCollection("rows").FindById(4));
                if (peerFacade) tx.Rollback();
                else tx.Commit();
                PeerWrite(peer);
            }
            Verify(file, password, peerFacade ? new[] { 1, 2, 5 } : new[] { 1, 2, 3, 4, 5 });
        }
    }
}
