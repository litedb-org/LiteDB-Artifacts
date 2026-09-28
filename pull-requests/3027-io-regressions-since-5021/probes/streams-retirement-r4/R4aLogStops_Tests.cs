#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Internals;
using LiteDB.Tests.Issues;
using Xunit;

namespace LiteDB.Tests.Regressions
{
    [Collection(NativeFileSyncCollection.Name)]
    public class R4aLogStops_Tests
    {
        [Fact]
        public void Log_that_stops_syncing_after_a_retirement_retires_nothing_more()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, 0)));
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename, DurableCommits = false });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(db, value);
                using (var reader = engine.Query("rows", new Query()))
                {
                    Worker(() => { for (var value = 6; value <= 9; value++) Update(db, value); engine.Checkpoint(); });
                    var root = BitConverter.ToInt64(Header(file.Filename), WalRetirement.RootPosition);
                    root.Should().BeGreaterThan(0);
                    NativeFileSync.SimulateErrno = path => path.EndsWith("-log.db", StringComparison.OrdinalIgnoreCase) ? 22 : 0;
                    Worker(() => { for (var value = 10; value <= 13; value++) Update(db, value); engine.Checkpoint(); });
                    BitConverter.ToInt64(Header(file.Filename), WalRetirement.RootPosition).Should().Be(root, "no witness once the log cannot sync");
                }
            }
            finally { NativeFileSync.SimulateErrno = null; }
            using var reopened = new LiteDatabase(file.Filename);
            reopened.GetCollection("rows").FindAll().Select(x => x["value"].AsInt32).Should().OnlyContain(x => x == 13);
        }

        private static void Update(LiteDatabase db, int value) =>
            db.GetCollection("rows").Update(Enumerable.Range(1, 8).Select(id => MvccRetirementScenario.Document(id, value))).Should().Be(8);

        private static void Worker(Action action)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo failure = null;
            var thread = new System.Threading.Thread(() => { try { action(); } catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); } });
            thread.Start(); thread.Join(); failure?.Throw();
        }

        private static byte[] Header(string filename)
        {
            using var stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var header = new byte[Constants.PAGE_SIZE];
            stream.ReadFully(header, 0, header.Length);
            return header;
        }
    }
}
#endif
