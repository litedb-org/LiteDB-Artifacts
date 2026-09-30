using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Engine
{
    public class SharedReaderRetirement_Tests
    {
        private static object Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);

#if DEBUG || TESTING
        [Theory]
        [InlineData(null, false)]
        [InlineData("secret", false)]
        [InlineData(null, true)]
        [InlineData("secret", true)]
        public void Last_other_leased_reader_cleanup_does_not_join_forced_pin_draining_this_callback(string password, bool failClose)
        {
            var file = new TempFile();
            Seed(file, password);
            using var reached = new ManualResetEventSlim();
            using var resume = new ManualResetEventSlim();
            using var readDone = new ManualResetEventSlim();
            using var closeDone = new ManualResetEventSlim();
            Action callback = null;
            var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; } })
            { PinIdleLimit = TimeSpan.FromMinutes(1), PinHoldLimit = TimeSpan.FromMinutes(1) };
            Exception readerError = null, closeError = null;
            LiteEngine core = null;
            var injected = new IOException("pin retirement close failure");
            var owner = new Thread(() =>
            {
                try
                {
                    var leased = shared.Query("rows", new Query());
                    Assert.True(leased.Read());
                    shared.Insert("rows", new[] { Row(6) }, BsonAutoId.Int32);
                    Assert.NotNull(Field(shared, "_pin"));
                    Assert.True(shared.BeginTrans());
                    shared.Insert("rows", new[] { Row(99) }, BsonAutoId.Int32);
                    var reader = shared.Query("rows", new Query());
                    Assert.True(reader.Read());
                    core = (LiteEngine)Field(shared, "_engine");
                    if (failClose)
                    {
                        var mode = core.GetType().GetField("_modeGuard", BindingFlags.Instance | BindingFlags.NonPublic);
                        mode.SetValue(core, new AfterDisposal((IDisposable)mode.GetValue(core), () => throw injected));
                    }
                    callback = () => { callback = null; reached.Set(); resume.Wait(); leased.Dispose(); };
                    Assert.True(reader.Read());
                    reader.Dispose();
                    GC.KeepAlive(leased);
                }
                catch (Exception error) { readerError = error; }
                finally { readDone.Set(); }
            }) { IsBackground = true };
            owner.Start();
            Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
            var closer = new Thread(() => { closeError = Record.Exception(shared.Dispose); closeDone.Set(); }) { IsBackground = true };
            closer.Start();
            WaitForDrain(core);
            Assert.False(NativeAvailable(shared));
            resume.Set();
            Assert.True(readDone.Wait(TimeSpan.FromSeconds(3)), "Callback joined the pin that is draining this reader operation.");
            Assert.True(closeDone.Wait(TimeSpan.FromSeconds(3)));
            Assert.Null(readerError);
            if (failClose) Assert.Same(injected, closeError);
            else Assert.Null(closeError);
            Assert.True(NativeAvailable(shared, wait: true));
            VerifyCold(file, password, 6);
            file.Dispose();
        }
#endif

        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public void Last_other_leased_reader_cleanup_does_not_join_exited_owner_draining_this_callback(string password)
        {
            var file = new TempFile();
            Seed(file, password);
            using var published = new ManualResetEventSlim();
            using var exitOwner = new ManualResetEventSlim();
            using var reached = new ManualResetEventSlim();
            using var resume = new ManualResetEventSlim();
            using var readDone = new ManualResetEventSlim();
            Action callback = null;
            var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; } });
            IBsonDataReader leased = null, reader = null;
            Exception ownerError = null, readError = null;
            var owner = new Thread(() =>
            {
                try
                {
                    leased = shared.Query("rows", new Query());
                    Assert.True(leased.Read());
                    Assert.True(shared.BeginTrans());
                    shared.Insert("rows", new[] { Row(6) }, BsonAutoId.Int32);
                    Assert.True(shared.Commit());
                    Assert.True(new FileInfo(FileHelper.GetLogFile(file.Filename)).Length > 0);
                    Assert.True(shared.BeginTrans());
                    shared.Insert("rows", new[] { Row(99) }, BsonAutoId.Int32);
                    reader = shared.Query("rows", new Query());
                    Assert.True(reader.Read());
                    Assert.Null(Field(shared, "_pin"));
                }
                catch (Exception error) { ownerError = error; }
                finally { published.Set(); }
                exitOwner.Wait();
            }) { IsBackground = true };
            owner.Start();
            Assert.True(published.Wait(TimeSpan.FromSeconds(5)));
            Assert.Null(ownerError);
            var core = (LiteEngine)Field(shared, "_engine");
            callback = () => { callback = null; reached.Set(); resume.Wait(); leased.Dispose(); };
            var advancing = new Thread(() => { readError = Record.Exception(() => reader.Read()); readDone.Set(); }) { IsBackground = true };
            advancing.Start();
            Assert.True(reached.Wait(TimeSpan.FromSeconds(5)));
            exitOwner.Set();
            Assert.True(owner.Join(TimeSpan.FromSeconds(5)));
            WaitForDrain(core);
            Assert.False(NativeAvailable(shared));
            resume.Set();
            Assert.True(readDone.Wait(TimeSpan.FromSeconds(3)), "Last-reader checkpoint joined the native owner draining this callback.");
            Assert.Null(readError);
            reader.Dispose();
            Assert.IsType<LiteException>(Record.Exception(() => shared.Pragma("USER_VERSION")));
            Assert.Equal(0, shared.Pragma("USER_VERSION").AsInt32);
            shared.Dispose();
            Assert.True(NativeAvailable(shared, wait: true));
            VerifyCold(file, password, 6);
            GC.KeepAlive(leased);
            file.Dispose();
        }

#if DEBUG || TESTING
        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public void Independent_last_leased_reader_disposal_still_joins_pin_cleanup(string password)
        {
            using var file = new TempFile();
            Seed(file, password);
            using var shared = new SharedEngine(new EngineSettings { Filename = file, Password = password,
                ReadTransform = (_, value) => value })
            { PinIdleLimit = TimeSpan.FromMinutes(1), PinHoldLimit = TimeSpan.FromMinutes(1) };
            var reader = shared.Query("rows", new Query());
            Assert.True(reader.Read());
            shared.Insert("rows", new[] { Row(6) }, BsonAutoId.Int32);
            Assert.NotNull(Field(shared, "_pin"));
            reader.Dispose();
            Assert.Null(Field(shared, "_pin"));
            Assert.True(NativeAvailable(shared, wait: true));
            shared.Dispose();
            VerifyCold(file, password, 6);
        }
#endif

        private static void WaitForDrain(LiteEngine core)
        {
            var operations = Field(core, "_operations");
            Assert.True(SpinWait.SpinUntil(() => (int)Field(operations, "_waitingExclusive") != 0, TimeSpan.FromSeconds(5)));
        }

        private static bool NativeAvailable(SharedEngine shared, bool wait = false)
        {
            var acquired = false;
            var probe = new Thread(() =>
            {
                acquired = shared.MutexOwner.Mutex.WaitOne(wait ? 3000 : 0);
                if (acquired) shared.MutexOwner.Mutex.ReleaseMutex();
            }) { IsBackground = true };
            probe.Start();
            Assert.True(probe.Join(TimeSpan.FromSeconds(5)));
            return acquired;
        }

        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id };
        private static void Seed(TempFile file, string password)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            db.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(Row));
            db.GetCollection("rows").EnsureIndex("value");
            db.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 1, ["value"] = "untouched" });
        }
        private static void VerifyCold(TempFile file, string password, int count)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            var rows = db.GetCollection("rows");
            Assert.Equal(Enumerable.Range(1, count), rows.FindAll().Select(x => x["_id"].AsInt32).OrderBy(x => x));
            var indexed = rows.Query().Where(Query.EQ("value", 3));
            Assert.Equal("value", indexed.GetPlan()["index"]["name"].AsString);
            Assert.StartsWith("INDEX SEEK", indexed.GetPlan()["index"]["mode"].AsString);
            Assert.Equal(3, Assert.Single(indexed.ToArray())["_id"].AsInt32);
            Assert.Equal(Enumerable.Range(1, count), rows.Find(Query.GTE("value", 0)).Select(x => x["_id"].AsInt32));
            Assert.Null(rows.FindById(99));
            Assert.Equal("untouched", db.GetCollection("sentinel").FindById(1)["value"].AsString);
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
