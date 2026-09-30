#if !NETFRAMEWORK
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using Xunit;

namespace LiteDB.Tests.Engine
{
    [Collection(nameof(SharedReaderPinCollection))]
    public class SharedPinProgress_Tests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("secret")]
        public async Task Progress_oracle_rejects_a_writer_blocked_by_a_held_pin(string password)
        {
            using var file = new TempFile();
            using (var engine = new SharedEngine(new EngineSettings { Filename = file.Filename, Password = password }))
            {
                engine.Insert("docs", Enumerable.Range(1, 200).Select(id => new BsonDocument
                    { ["_id"] = id, ["value"] = 0, ["payload"] = new string('p', 200) }), BsonAutoId.Int32);
                engine.EnsureIndex("docs", "value", "$.value", false);
                engine.Insert("untouched", new[] { new BsonDocument { ["_id"] = 1, ["value"] = "preserved" } }, BsonAutoId.Int32);
                using var child = new SharedPinProgressProbe(file.Filename, password);
                await child.Ready();
                using var pinned = new ManualResetEventSlim();
                using var finish = new ManualResetEventSlim();
                Exception ownerError = null;
                var owner = new Thread(() =>
                {
                    try
                    {
                        using var reader = engine.Query("docs", new Query());
                        reader.Read().Should().BeTrue();
                        engine.Update("docs", new[] { new BsonDocument
                            { ["_id"] = 1, ["value"] = 1, ["payload"] = new string('p', 200) } });
                        // A real transaction hold cannot expire while this owner lives.
                        engine.BeginTrans().Should().BeTrue();
                        pinned.Set();
                        finish.Wait();
                    }
                    catch (Exception error) { ownerError = error; pinned.Set(); }
                }) { IsBackground = true };
                owner.Start();
                try
                {
                    pinned.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
                    ownerError.Should().BeNull();
                    child.Start();
                    await child.ObserveAdmission();
                    var failure = await Assert.ThrowsAsync<TimeoutException>(() => child.Complete(TimeSpan.FromSeconds(1)));
                    failure.Message.Should().Contain("committed:1000");
                    owner.IsAlive.Should().BeTrue();
                }
                finally
                {
                    // Kill the blocked probe before allowing the owner to release.
                    child.Dispose();
                    finish.Set();
                    owner.Join(TimeSpan.FromSeconds(10)).Should().BeTrue();
                }
                ownerError.Should().BeNull();
            }
            SharedPinProgressProbe.VerifyCold(file.Filename, 200, password, inserted: false);
        }
    }
}
#endif
