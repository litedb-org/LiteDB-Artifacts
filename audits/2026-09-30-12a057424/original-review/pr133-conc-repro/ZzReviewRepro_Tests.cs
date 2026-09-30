using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.Engine
{
    public class ZzReviewRepro_Tests
    {
        private readonly ITestOutputHelper _out;
        public ZzReviewRepro_Tests(ITestOutputHelper output) { _out = output; }

        private static SessionLifetime Lifetime(LiteDatabase db) =>
            (SessionLifetime)typeof(LiteDatabase).GetField("_lifetime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(db);

#pragma warning disable CS0618
        [Fact]
        public void Repro_shared_legacy_owner_dispose_with_blocked_peer()
        {
            using var file = new TempFile();
            var db = new LiteDatabase(new ConnectionString { Filename = file.Filename, Connection = ConnectionType.Shared });
            db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 0 });
            Lifetime(db).CloseWaitOverride = TimeSpan.FromSeconds(2);
            Assert.True(db.BeginTrans());
            db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 1 });
            var peer = Task.Factory.StartNew(() => db.GetCollection("c").Insert(new BsonDocument { ["_id"] = 2 }), TaskCreationOptions.LongRunning);
            Thread.Sleep(500);
            Assert.False(peer.IsCompleted);
            var error = Record.Exception(() => db.Dispose());
            _out.WriteLine("dispose: " + (error?.GetType().Name ?? "ok") + " " + error?.Message);
            var commit = Record.Exception(() => db.Commit());
            _out.WriteLine("commit after dispose: " + (commit?.GetType().Name ?? "ok"));
            var finished = false;
            try { finished = peer.Wait(TimeSpan.FromSeconds(10)); }
            catch (AggregateException e) { finished = true; _out.WriteLine("peer: " + e.InnerException.GetType().Name); }
            _out.WriteLine("peer finished within 10s after dispose: " + finished);
            var retry = Record.Exception(() => db.Dispose());
            _out.WriteLine("retry dispose: " + (retry?.GetType().Name ?? "ok"));
            Assert.True(finished, "peer ordinary op is still blocked on the native mutex held by the legacy transaction");
        }
#pragma warning restore CS0618

        [Fact]
        public void Repro_handle_dispose_racing_session_close_throws()
        {
            using var file = new TempFile();
            var db = new LiteDatabase(file);
            var tx1 = db.BeginTransaction();
            tx1.GetCollection("a").Insert(new BsonDocument { ["_id"] = 1 });
            var tx2 = db.BeginTransaction();
            tx2.GetCollection("b").Insert(new BsonDocument { ["_id"] = 1 });
            using var reached = new ManualResetEventSlim();
            using var finish = new ManualResetEventSlim();
            var monitor = NativeAdmissionDirectPool_Tests.Engine(db).GetMonitor();
            monitor.AfterTransactionExit = () =>
            {
                monitor.AfterTransactionExit = null;
                reached.Set();
                finish.Wait(TimeSpan.FromSeconds(10));
            };
            var close = Task.Run(() => db.Dispose());
            Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
            var e1 = Record.Exception(() => tx1.Dispose());
            var e2 = Record.Exception(() => tx2.Dispose());
            _out.WriteLine("tx1.Dispose: " + (e1 == null ? "ok" : e1.GetType().Name + ": " + e1.Message));
            _out.WriteLine("tx2.Dispose: " + (e2 == null ? "ok" : e2.GetType().Name + ": " + e2.Message));
            finish.Set();
            close.Wait();
            _out.WriteLine("states: " + tx1.State + " " + tx2.State);
            Assert.Null(e1); Assert.Null(e2);
        }

    }
}
