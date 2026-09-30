using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Engine
{
    public class ReviewSharedReaderCleanup_Tests
    {
        private static object Field(object obj, string name) => obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(obj);
        private static void Seed(string file, string password)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            db.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(i => new BsonDocument { ["_id"] = i, ["value"] = i }));
            db.GetCollection("rows").EnsureIndex("value");
            db.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 1, ["value"] = "untouched" });
        }
        [Theory]
        [InlineData(null, false, false)] [InlineData("secret", false, false)]
        [InlineData(null, true, false)] [InlineData("secret", true, false)]
        [InlineData(null, false, true)] [InlineData("secret", false, true)]
        [InlineData(null, true, true)] [InlineData("secret", true, true)]
        public void Leased_self_dispose_refusal_is_retryable_and_retires_native_admission(string password, bool openCore, bool self)
        {
            using var file = new TempFile();
            Seed(file, password);
            Action callback = null;
            var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password, ReadOnly = !openCore,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
            var owner = openCore ? shared.Query("rows", new Query { ForUpdate = true }) : null;
            var reader = shared.Query("rows", new Query());
            Assert.True(reader.Read());
            var snapshot = (LiteEngine)Field(Field(Field(reader, "_reader"), "_state"), "_engine");
            Assert.NotSame(Field(shared, "_engine"), snapshot);
            Exception refused = null;
            var reached = false;
            if (self) callback = () => { callback = null; reached = true; refused = Record.Exception(reader.Dispose); };
            Assert.True(reader.Read());
            if (self) { Assert.True(reached); Assert.IsType<InvalidOperationException>(refused); }
            reader.Dispose();
            owner?.Dispose();
            shared.Dispose();
            var admission = Record.Exception(() => { using var direct = new LiteEngine(new EngineSettings { Filename = file, Password = password }); });
            Assert.True(snapshot.IsDisposed, "Snapshot remained open; Direct probe: " + admission);
            Assert.Null(admission);
            GC.KeepAlive(reader);
            using var cold = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            Assert.Equal(5, cold.GetCollection("rows").Count());
            Assert.Equal(3, cold.GetCollection("rows").FindOne(Query.EQ("value", 3))["_id"].AsInt32);
        }

        [Theory]
        [InlineData(null, false)] [InlineData("secret", false)]
        [InlineData(null, true)] [InlineData("secret", true)]
        public void Disposing_other_leased_reader_does_not_join_the_core_draining_this_callback(string password, bool ownerExit)
        {
            var file = new TempFile();
            Seed(file, password);
            var entered = new ManualResetEventSlim();
            var resume = new ManualResetEventSlim();
            var readingDone = new ManualResetEventSlim();
            var closeDone = new ManualResetEventSlim();
            var published = new ManualResetEventSlim();
            var exitOwner = new ManualResetEventSlim();
            Action callback = null;
            var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
            shared.PinIdleLimit = TimeSpan.FromMinutes(5);
            shared.PinHoldLimit = TimeSpan.FromMinutes(5);
            IBsonDataReader leased = null, reader = null;
            LiteEngine core = null;
            Exception readingError = null, closeError = null, otherDisposeError = null;
            Thread owner = null;
            if (ownerExit)
            {
                leased = shared.Query("rows", new Query());
                leased.Read();
                using (var peer = new LiteDatabase(new ConnectionString { Filename = file, Password = password, Connection = ConnectionType.Shared }))
                    peer.GetCollection("rows").Insert(new BsonDocument { ["_id"] = 6, ["value"] = 6 });
                Assert.True(new FileInfo(FileHelper.GetLogFile(file.Filename)).Length > 0);
                owner = new Thread(() =>
                {
                    shared.BeginTrans();
                    shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 99, ["value"] = 99 } }, BsonAutoId.Int32);
                    reader = shared.Query("rows", new Query());
                    reader.Read();
                    core = (LiteEngine)Field(shared, "_engine");
                    published.Set();
                    exitOwner.Wait();
                }) { IsBackground = true };
                owner.Start();
                Assert.True(published.Wait(TimeSpan.FromSeconds(5)));
                Assert.Null(Field(shared, "_pin"));
            }
            var advancing = new Thread(() =>
            {
                try
                {
                    if (!ownerExit)
                    {
                        leased = shared.Query("rows", new Query()); leased.Read();
                        shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 6, ["value"] = 6 } }, BsonAutoId.Int32);
                        shared.BeginTrans();
                        shared.Insert("rows", new[] { new BsonDocument { ["_id"] = 99, ["value"] = 99 } }, BsonAutoId.Int32);
                        reader = shared.Query("rows", new Query()); reader.Read();
                        core = (LiteEngine)Field(shared, "_engine");
                        Assert.NotNull(Field(shared, "_pin"));
                    }
                    callback = () => { callback = null; entered.Set(); resume.Wait(); otherDisposeError = Record.Exception(leased.Dispose); };
                    reader.Read();
                    reader.Dispose();
                }
                catch (Exception error) { readingError = error; }
                finally { readingDone.Set(); }
            }) { IsBackground = true };
            advancing.Start();
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            if (ownerExit) { exitOwner.Set(); Assert.True(owner.Join(TimeSpan.FromSeconds(5))); }
            else new Thread(() => { closeError = Record.Exception(shared.Dispose); closeDone.Set(); }) { IsBackground = true }.Start();
            var operations = Field(core, "_operations");
            Assert.True(SpinWait.SpinUntil(() => (int)Field(operations, "_waitingExclusive") != 0, TimeSpan.FromSeconds(5)));
            resume.Set();
            Assert.True(readingDone.Wait(TimeSpan.FromSeconds(3)), "Other-reader cleanup joined the operation currently executing it.");
            if (ownerExit) { closeError = Record.Exception(shared.Dispose); closeDone.Set(); }
            Assert.True(closeDone.Wait(TimeSpan.FromSeconds(3)));
            Assert.Null(otherDisposeError);
            Assert.Null(readingError);
            Assert.Null(closeError);
            var acquired = false;
            var probe = new Thread(() => { acquired = shared.MutexOwner.Mutex.WaitOne(TimeSpan.FromSeconds(2)); if (acquired) shared.MutexOwner.Mutex.ReleaseMutex(); }) { IsBackground = true };
            probe.Start(); Assert.True(probe.Join(TimeSpan.FromSeconds(3))); Assert.True(acquired);
            using (var cold = new LiteDatabase(new ConnectionString { Filename = file, Password = password }))
            {
                Assert.Equal(Enumerable.Range(1, 6), cold.GetCollection("rows").Find(Query.GTE("value", 0)).Select(x => x["_id"].AsInt32));
                Assert.Null(cold.GetCollection("rows").FindById(99));
                Assert.Equal("untouched", cold.GetCollection("sentinel").FindById(1)["value"].AsString);
            }
            GC.KeepAlive(leased);
            file.Dispose();
        }
    }
}
