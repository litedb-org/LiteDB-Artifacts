using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Engine
{
    public class ReviewLateSharedReader_Tests
    {
        private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        [Theory]
        [InlineData(null, false)]
        [InlineData("secret", false)]
        [InlineData(null, true)]
        [InlineData("secret", true)]
        public void Late_reader_self_close_preserves_unleased_protection_or_leased_independence(string password, bool leased)
        {
            using var file = new TempFile();
            using (var seed = new LiteDatabase(new ConnectionString { Filename = file, Password = password }))
            {
                seed.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i }));
                seed.GetCollection("rows").EnsureIndex("value");
            }
            var obstruction = file.Filename + "-readers";
            if (!leased) File.WriteAllText(obstruction, "refuse registration");
            Action callback = null;
            using var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password, ReadOnly = true,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
            var reader = shared.Query("rows", new Query());
            Assert.True(reader.Read());
            Assert.Equal(1, reader.Current["_id"].AsInt32);
            Assert.Null(Field(shared, "_engine"));
            var entered = 0;
            Exception refusal = null;
            var acquired = false;
            callback = () =>
            {
                callback = null;
                entered++;
                refusal = Record.Exception(shared.Dispose);
                var probe = new Thread(() =>
                {
                    if (!shared.MutexOwner.Mutex.WaitOne(0)) return;
                    acquired = true;
                    shared.MutexOwner.Mutex.ReleaseMutex();
                }) { IsBackground = true };
                probe.Start();
                Assert.True(probe.Join(TimeSpan.FromSeconds(3)));
            };
            Assert.True(reader.Read());
            Assert.Equal(2, reader.Current["_id"].AsInt32);
            var count = 2;
            while (reader.Read()) count++;
            reader.Dispose();
            shared.Dispose();
            if (!leased) File.Delete(obstruction);
            Assert.Equal(1, entered);
            Assert.Equal(5, count);
            if (leased) { Assert.Null(refusal); Assert.True(acquired); }
            else { Assert.IsType<InvalidOperationException>(refusal); Assert.False(acquired); }
            using var cold = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            Assert.Equal(Enumerable.Range(1, 5), cold.GetCollection("rows").Find(Query.GTE("value", 0)).Select(x => x["_id"].AsInt32));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("secret", false)]
        [InlineData(null, true)]
        [InlineData("secret", true)]
        public void Late_reader_callback_can_reenter_after_foreign_close_starts_draining(string password, bool snapshot)
        {
            var file = new TempFile();
            using (var seed = new LiteDatabase(new ConnectionString { Filename = file, Password = password }))
                seed.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(i => new BsonDocument { ["_id"] = i }));
            var obstruction = file.Filename + "-readers";
            if (snapshot) File.WriteAllText(obstruction, "refuse registration");
            var reached = new ManualResetEventSlim();
            var resume = new ManualResetEventSlim();
            var readerDone = new ManualResetEventSlim();
            var closeDone = new ManualResetEventSlim();
            Action callback = null;
            var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password, ReadOnly = snapshot, ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
            Exception readError = null, closeError = null, reentryError = null;
            LiteEngine core = null;
            var readerThread = new Thread(() =>
            {
                try
                {
                    if (!snapshot) shared.BeginTrans();
                    using var reader = shared.Query("rows", new Query());
                    Assert.True(reader.Read());
                    core = snapshot ? ((System.Collections.Generic.HashSet<LiteEngine>)Field(shared, "_mutexSnapshots")).Single()
                        : (LiteEngine)Field(shared, "_engine");
                    callback = () => { callback = null; reached.Set(); resume.Wait(); reentryError = Record.Exception(() => shared.Pragma("USER_VERSION")); };
                    reader.Read();
                }
                catch (Exception error) { readError = error; }
                finally { readerDone.Set(); }
            }) { IsBackground = true };
            readerThread.Start();
            Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
            var closer = new Thread(() => { closeError = Record.Exception(shared.Dispose); closeDone.Set(); }) { IsBackground = true };
            closer.Start();
            var operations = Field(core, "_operations");
            Assert.True(SpinWait.SpinUntil(() => (int)Field(operations, "_waitingExclusive") != 0, TimeSpan.FromSeconds(5)));
            resume.Set();
            var completed = readerDone.Wait(TimeSpan.FromSeconds(3)) && closeDone.Wait(TimeSpan.FromSeconds(3));
            Assert.True(completed, "Reader callback and disposer deadlocked after core-close drain was established.");
            Assert.NotNull(reentryError);
            Assert.Null(readError);
            Assert.Null(closeError);
            if (snapshot) File.Delete(obstruction);
            using (var cold = new LiteDatabase(new ConnectionString { Filename = file, Password = password }))
                Assert.Equal(5, cold.GetCollection("rows").Count());
            file.Dispose();
        }
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Review_owner_exit_during_transferred_unleased_reader_callback_does_not_deadlock(bool pin)
        {
            var file = new TempFile();
            using (var seed = new LiteDatabase(file))
                seed.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(i => new BsonDocument { ["_id"] = i }));
            if (!pin) File.WriteAllText(file.Filename + "-readers", "no leases");
            var published = new ManualResetEventSlim();
            var exitOwner = new ManualResetEventSlim();
            var reached = new ManualResetEventSlim();
            var resume = new ManualResetEventSlim();
            var done = new ManualResetEventSlim();
            Action callback = null;
            var shared = new SharedEngine(new EngineSettings { Filename = file, ReadOnly = !pin,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
            IBsonDataReader reader = null;
            var creator = new Thread(() =>
            {
                IBsonDataReader leased = null;
                if (pin)
                {
                    leased = shared.Query("rows", new Query());
                    leased.Read();
                    shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 6 } }, BsonAutoId.Int32);
                    shared.BeginTrans();
                }
                reader = shared.Query("rows", new Query());
                reader.Read();
                leased?.Dispose();
                published.Set();
                exitOwner.Wait();
            }) { IsBackground = true };
            creator.Start();
            Assert.True(published.Wait(TimeSpan.FromSeconds(5)));
            var core = pin ? (LiteEngine)Field(shared, "_engine") : ((System.Collections.Generic.HashSet<LiteEngine>)Field(shared, "_mutexSnapshots")).Single();
            callback = () => { callback = null; reached.Set(); resume.Wait(); Record.Exception(() => shared.Pragma("USER_VERSION")); };
            var advancing = new Thread(() => { try { reader.Read(); } finally { done.Set(); } }) { IsBackground = true };
            advancing.Start();
            Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
            exitOwner.Set();
            Assert.True(creator.Join(TimeSpan.FromSeconds(5)));
            var operations = Field(core, "_operations");
            Assert.True(SpinWait.SpinUntil(() => (int)Field(operations, "_waitingExclusive") != 0, TimeSpan.FromSeconds(5)));
            resume.Set();
            Assert.True(done.Wait(TimeSpan.FromSeconds(3)), "Owner-exit cleanup deadlocked with transferred reader's late callback.");
            reader.Dispose();
            shared.Dispose();
            if (!pin) File.Delete(file.Filename + "-readers");
            file.Dispose();
        }
        [Fact]
        public void Review_raw_rollback_after_dispose_remains_a_noop()
        {
            using var file = new TempFile();
            using var shared = new SharedEngine(new EngineSettings { Filename = file });
            shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 1 } }, BsonAutoId.Int32);
            shared.BeginTrans();
            shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 2 } }, BsonAutoId.Int32);
            shared.Dispose();
            Assert.False(shared.Rollback());
            using var cold = new LiteDatabase(file);
            Assert.Equal(1, cold.GetCollection("rows").Count());
        }
        [Fact]
        public void Review_own_pin_callback_can_reenter_shared_pragma()
        {
            using var file = new TempFile();
            using var shared = new SharedEngine(new EngineSettings { Filename = file, ReadTransform = (_, value) => value });
            shared.Insert("rows", Enumerable.Range(1, 5).Select(i => new BsonDocument { ["_id"] = i }), BsonAutoId.Int32);
            using var reader = shared.Query("rows", new Query());
            Assert.True(reader.Read());
            var called = false;
            System.Collections.Generic.IEnumerable<BsonDocument> Input()
            {
                yield return new BsonDocument { ["_id"] = 6 };
                Assert.NotNull(Field(shared, "_pin"));
                Assert.False(shared.MutexOwner.IsOwnedByCurrentThread);
                Assert.Equal(0, shared.Pragma("USER_VERSION").AsInt32);
                called = true;
                yield return new BsonDocument { ["_id"] = 7 };
            }
            Assert.Equal(2, shared.Insert("rows", Input(), BsonAutoId.Int32));
            Assert.True(called);
            reader.Dispose();
            shared.Dispose();
            using var cold = new LiteDatabase(file);
            Assert.Equal(7, cold.GetCollection("rows").Count());
        }
        [Fact]
        public void Review_last_reader_close_fences_fresh_operation_until_core_retired()
        {
            using var file = new TempFile();
            using (var seed = new LiteDatabase(file))
                seed.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 1 });
            using var shared = new SharedEngine(new EngineSettings { Filename = file, ReadTransform = (_, value) => value });
            var reader = shared.Query("rows", new Query { ForUpdate = true });
            Assert.True(reader.Read());
            var core = (LiteEngine)Field(shared, "_engine");
            using var reached = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var starting = new ManualResetEventSlim();
            var mode = core.GetType().GetField("_modeGuard", BindingFlags.Instance | BindingFlags.NonPublic);
            mode.SetValue(core, new AfterDisposal((IDisposable)mode.GetValue(core), () => { reached.Set(); release.Wait(); }));
            Exception closeError = null;
            var disposer = new Thread(() => closeError = Record.Exception(reader.Dispose)) { IsBackground = true };
            disposer.Start();
            Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
            shared.BeforeCountingUser = starting.Set;
            var resumer = new Thread(() => { starting.Wait(TimeSpan.FromSeconds(1)); release.Set(); }) { IsBackground = true };
            resumer.Start();
            var error = Record.Exception(() => shared.Pragma("USER_VERSION"));
            Assert.True(disposer.Join(TimeSpan.FromSeconds(5)));
            Assert.True(resumer.Join(TimeSpan.FromSeconds(5)));
            shared.BeforeCountingUser = null;
            Assert.Null(closeError);
            Assert.Null(error);
            shared.Dispose();
            using var cold = new LiteDatabase(file);
            Assert.Equal(1, cold.GetCollection("rows").Count());
        }
        private sealed class AfterDisposal : IDisposable
        {
            private readonly IDisposable _inner;
            private readonly Action _after;
            internal AfterDisposal(IDisposable inner, Action after) { _inner = inner; _after = after; }
            public void Dispose() { _inner.Dispose(); _after(); }
        }
    }
}
