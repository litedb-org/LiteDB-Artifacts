using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LiteDB.Client.Direct;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Engine
{
    public class TransactionHandleLateCallbackClose_Tests
    {
        // Raw owning cores and mutex-backed Shared snapshots must drain this callback.
        // Pooled Direct and independently leased Shared readers own their own lifetime.
        [Theory]
        [InlineData(0, null)] [InlineData(0, "secret")]
        [InlineData(1, null)] [InlineData(1, "secret")]
        [InlineData(2, null)] [InlineData(2, "secret")]
        [InlineData(3, null)] [InlineData(3, "secret")]
        [InlineData(4, null)] [InlineData(4, "secret")]
        [InlineData(5, null)] [InlineData(5, "secret")]
        public void Late_callback_close_refuses_only_a_self_dependent_drain(int mode, string password)
        {
            using var file = new TempFile();
            Seed(file, password);
            LiteDatabase db = null;
            Exception closeError = null;
            var callbacks = 0;
            var settings = Settings(file, password, mode, (_, value) =>
            {
                if (value.IsDocument && value["_id"] == 2)
                {
                    callbacks++;
                    closeError = Record.Exception(db.Dispose);
                }
                return value;
            });
            using var engine = Open(settings, mode);
            using (db = new LiteDatabase(engine, disposeOnClose: mode != 1))
            {
                Lifetime(db).CloseWaitOverride = TimeSpan.FromMilliseconds(100);
                using (var reader = db.Execute(mode == 5 ? "SELECT $ FROM rows FOR UPDATE" : "SELECT $ FROM rows"))
                {
                    Assert.True(reader.Read());
                    Assert.Equal(1, reader.Current["_id"].AsInt32);
                    Assert.Equal(0, callbacks);
                    Assert.True(reader.Read());
                    Assert.Equal(2, reader.Current["_id"].AsInt32);
                }
                Assert.Equal(1, callbacks);
                if (mode == 0 || mode == 4 || mode == 5)
                {
                    Assert.IsType<InvalidOperationException>(closeError);
                    // Refusal is before session close publication: ordinary use still works.
                    db.GetCollection("rows").Insert(Row(4));
                }
                else Assert.Null(closeError);
            }
            engine.Dispose();
            Verify(file, password, mode == 0 || mode == 4 || mode == 5 ? 4 : 3);
        }

        [Theory]
        [InlineData(0, null)] [InlineData(0, "secret")]
        [InlineData(4, null)] [InlineData(4, "secret")]
        [InlineData(5, null)] [InlineData(5, "secret")]
        public async Task Cross_thread_close_drains_late_callback_without_refusing_it(int mode, string password)
        {
            using var file = new TempFile();
            Seed(file, password);
            using var entered = new ManualResetEventSlim();
            using var resume = new ManualResetEventSlim();
            var settings = Settings(file, password, mode, (_, value) =>
            {
                if (value.IsDocument && value["_id"] == 2)
                {
                    entered.Set();
                    Assert.True(resume.Wait(TimeSpan.FromSeconds(10)));
                }
                return value;
            });
            using var engine = Open(settings, mode);
            using (var db = new LiteDatabase(engine))
            using (var reader = db.Execute(mode == 5 ? "SELECT $ FROM rows FOR UPDATE" : "SELECT $ FROM rows"))
            {
                Assert.True(reader.Read());
                var reading = Task.Run(reader.Read);
                Task closing = null;
                try
                {
                    Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
                    closing = Task.Run(db.Dispose);
                    Assert.True(SpinWait.SpinUntil(() => Lifetime(db).Closing.IsCancellationRequested, TimeSpan.FromSeconds(5)));
                    Assert.False(closing.IsCompleted);
                }
                finally { resume.Set(); }
                Assert.True(await reading);
                await closing;
            }
            engine.Dispose();
            Verify(file, password, 3);
        }

        private static EngineSettings Settings(string file, string password, int mode, Func<string, BsonValue, BsonValue> transform)
        {
            var settings = new EngineSettings { Filename = file, Password = password, ReadTransform = transform };
            if (mode == 4) settings.SharedReaderFiles = (_, __) => throw new UnauthorizedAccessException("registry denied");
            return settings;
        }
        private static ILiteEngine Open(EngineSettings settings, int mode) =>
            mode >= 3 ? (ILiteEngine)new SharedEngine(settings) : mode == 2 ? DirectEnginePool.Open(settings) : new LiteEngine(settings);
        private static SessionLifetime Lifetime(LiteDatabase db) => (SessionLifetime)typeof(LiteDatabase)
            .GetField("_lifetime", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(db);
        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id * 10 };
        private static void Seed(string file, string password)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            db.GetCollection("rows").EnsureIndex("value");
            db.GetCollection("rows").Insert(new[] { Row(1), Row(2), Row(3) });
            db.GetCollection("sentinel").Insert(Row(99));
        }
        private static void Verify(string file, string password, int count)
        {
            using var db = new LiteDatabase(new ConnectionString { Filename = file, Password = password });
            Assert.Equal(count, db.GetCollection("rows").Count());
            for (var id = 1; id <= count; id++)
                Assert.Equal(id, db.GetCollection("rows").FindOne(Query.EQ("value", id * 10))["_id"].AsInt32);
            Assert.Equal(99, db.GetCollection("sentinel").FindById(99)["_id"].AsInt32);
        }
    }
}
