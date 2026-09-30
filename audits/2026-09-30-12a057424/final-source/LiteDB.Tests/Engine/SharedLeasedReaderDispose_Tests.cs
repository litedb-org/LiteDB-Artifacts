using System;
using System.Linq;
using System.Reflection;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Engine
{
    public class SharedLeasedReaderDispose_Tests
    {
        [Theory]
        [InlineData(null, false, true)] [InlineData("secret", false, true)]
        [InlineData(null, true, true)] [InlineData("secret", true, true)]
        [InlineData(null, false, false)] [InlineData("secret", false, false)]
        [InlineData(null, true, false)] [InlineData("secret", true, false)]
        public void Leased_snapshot_disposal_refuses_callback_before_mutation_and_can_retry(
            string password, bool queryCore, bool selfDispose)
        {
            using var file = new TempFile();
            var connection = new ConnectionString { Filename = file, Password = password };
            using (var seed = new LiteDatabase(connection))
            {
                seed.GetCollection("rows").Insert(Enumerable.Range(1, 5).Select(Row));
                seed.GetCollection("rows").EnsureIndex("value");
                seed.GetCollection("sentinel").Insert(new BsonDocument { ["_id"] = 99, ["value"] = "untouched" });
            }
            Action callback = null;
            using var shared = new SharedEngine(new EngineSettings
            {
                Filename = file, Password = password, ReadOnly = !queryCore,
                ReadTransform = (_, value) => { callback?.Invoke(); return value; }
            }) { PinIdleLimit = TimeSpan.FromMinutes(1), PinHoldLimit = TimeSpan.FromMinutes(1) };
            IBsonDataReader anchor = null;
            if (queryCore)
            {
                // A leased reader plus an ordinary write establishes and retains the
                // pin, routing the next query through QueryCore's snapshot construction.
                anchor = shared.Query("rows", new Query());
                using var facade = new LiteDatabase(shared, disposeOnClose: false);
                facade.GetCollection("sentinel").Update(new BsonDocument { ["_id"] = 99, ["value"] = "untouched" });
                Assert.NotNull(Field(shared, "_pin"));
            }
            var reader = shared.Query("rows", new Query());
            var inner = (BsonDataReader)Field(reader, "_reader");
            var snapshot = (LiteEngine)Field(Field(inner, "_context"), "_engine");
            Assert.True(((EngineSettings)Field(snapshot, "_settings")).ReadOnly);
            Assert.NotSame(Field(shared, "_engine"), snapshot);
            try
            {
                Assert.True(reader.Read());
                Assert.Equal(1, reader.Current["_id"].AsInt32);
                Exception refusal = null;
                if (selfDispose) callback = () =>
                {
                    callback = null;
                    refusal = Record.Exception(reader.Dispose);
                };
                Assert.True(reader.Read());
                Assert.Equal(2, reader.Current["_id"].AsInt32);
                if (selfDispose) Assert.IsType<InvalidOperationException>(refusal);
                reader.Dispose(); // Must finish after refused self-disposal, without GC.
                anchor?.Dispose();
                shared.Dispose();
                Assert.True(snapshot.IsDisposed);
                using (var direct = new LiteDatabase(connection))
                {
                    direct.GetCollection("rows").Insert(Row(6));
                }
                GC.KeepAlive(reader);
                using var cold = new LiteDatabase(connection);
                var rows = cold.GetCollection("rows");
                Assert.Equal(Enumerable.Range(1, 6), rows.FindAll().Select(row => row["_id"].AsInt32).OrderBy(id => id));
                var indexed = rows.Query().Where(Query.GTE("value", 0));
                Assert.Equal("value", indexed.GetPlan()["index"]["name"].AsString);
                Assert.Equal(Enumerable.Range(1, 6), indexed.ToArray().Select(row => row["_id"].AsInt32));
                Assert.Equal("untouched", cold.GetCollection("sentinel").FindById(99)["value"].AsString);
                GC.KeepAlive(reader);
            }
            finally
            {
                // Failed-before assertions must not rely on finalization to release files.
                reader.Dispose();
                snapshot.Dispose();
                anchor?.Dispose();
            }
        }
        private static object Field(object value, string name) => value.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id };
    }
}
