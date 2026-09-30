using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;
using static LiteDB.Tests.Engine.TransactionHandleSharedCallback_Tests;

namespace LiteDB.Tests.Engine
{
    public class TransactionHandleCallbackScopeRetention_Tests
    {
        [Theory]
        [InlineData(null, false)] [InlineData(null, true)]
        [InlineData("secret", false)] [InlineData("secret", true)]
        public void Idle_caller_thread_does_not_root_completed_application_graph(string password, bool fail)
        {
            using var file = new TempFile();
            Seed(file, password);
            using var ready = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            WeakReference[] graph = null;
            Exception failure = null;
            var worker = new Thread(() =>
            {
                try
                {
                    graph = Work(file, password, fail);
                    var list = (IList)typeof(SharedEngine).GetField("_executingCalls", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                    Assert.NotNull(list);
                    Assert.Empty(list);
                    var backing = (SharedEngine[])list.GetType().GetField("_items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(list);
                    Assert.All(backing, value => Assert.Null(value));
                }
                catch (Exception ex) { failure = ex; }
                finally { ready.Set(); }
                release.Wait();
            }) { IsBackground = true };
            worker.Start();
            try
            {
                Assert.True(ready.Wait(TimeSpan.FromSeconds(15)));
                Assert.Null(failure);
                for (var attempt = 0; attempt < 100 && graph.Any(reference => reference.IsAlive); attempt++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Thread.Sleep(25);
                }
                Assert.All(graph, reference => Assert.False(reference.IsAlive));
                Assert.True(worker.IsAlive);
            }
            finally { release.Set(); Assert.True(worker.Join(TimeSpan.FromSeconds(10))); }
            Verify(file, password, fail ? new[] { 1, 2 } : new[] { 1, 2, 3 });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference[] Work(string file, string password, bool fail)
        {
            var state = new State();
            var settings = Settings(file, password);
            settings.ReadTransform = (_, value) => { state.Calls++; return value; };
            using var shared = new SharedEngine(settings);
            using var db = new LiteDatabase(shared);
            using (var anchor = shared.Query("rows", new Query()))
            {
                Assert.True(anchor.Read());
                IEnumerable<BsonDocument> Input()
                {
                    yield return Row(3);
                    if (fail) throw new ApplicationException("independent input failure");
                }
                if (fail) Assert.Throws<ApplicationException>(() => shared.Insert("rows", Input(), BsonAutoId.Int32));
                else Assert.Equal(1, shared.Insert("rows", Input(), BsonAutoId.Int32));
            }
            Assert.True(state.Calls > 0);
            return new[] { new WeakReference(db), new WeakReference(shared), new WeakReference(settings), new WeakReference(state) };
        }
        private sealed class State { internal int Calls; }
    }
}
