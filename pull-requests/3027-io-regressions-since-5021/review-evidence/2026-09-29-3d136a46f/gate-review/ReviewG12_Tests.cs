#if DEBUG || TESTING
using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Tests.Issues;
using Xunit;
using static LiteDB.Constants;

namespace LiteDB.Tests.Regressions
{
    [Collection(NativeFileSyncCollection.Name)]
    public class ReviewG12_Tests
    {
        // A log whose syncs cannot report failure (no C library): the truncation's "sync" proves nothing,
        // yet the failed commit is reported NotCommitted.
        [Fact]
        public void Unverifiable_sync_still_reports_not_committed()
        {
            using var file = new TempFile();
            NativeFileSync.SimulateRuntimeSync = true;
            try
            {
                using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                var rows = db.GetCollection("rows");
                rows.Insert(new BsonDocument { ["_id"] = 1 });
                db.GetCollection("$database").FindAll().Single()["durableLogFlush"].AsBoolean.Should().BeFalse("the log's syncs cannot report failure");
                var failed = false;
                engine.SimulateCrashPoint = point =>
                {
                    if (point != "wal-confirmation-after-write" || failed) return;
                    failed = true;
                    throw new IOException("injected");
                };
                Action insert = () => rows.Insert(new BsonDocument { ["_id"] = 2 });
                var outcome = (string)insert.Should().Throw<IOException>().Which.Data["LiteDB.CommitOutcome"];
                engine.SimulateCrashPoint = null;
                outcome.Should().Be("Unknown", "an unverifiable sync proves no durability (RuntimeSyncDurability_Tests)");
            }
            finally { NativeFileSync.SimulateRuntimeSync = false; }
        }

        // A confirmation whose write returned, then a later step (the written callback) threw: the frame
        // stays in the log (no truncation). Checks the second claimed fix of ad3382f6a.
        [Fact]
        public void Confirmation_published_then_failed_is_unknown()
        {
            var settings = new EngineSettings { DataStream = new MemoryStream(), LogStream = new MemoryStream(), CacheSize = PAGE_SIZE * 8L };
            var state = new EngineState(null, settings);
            using var disk = new DiskService(settings, state, new[] { 2 });
            var page = disk.NewPage();
            page.Write((uint)5, BasePage.P_PAGE_ID);
            page.Write(true, BasePage.P_IS_CONFIRMED);
            Action write = () => disk.WriteLogDisk(new[] { page }, (id, position) => throw new IOException("callback failed"));
            var ex = write.Should().Throw<IOException>().Which;
            ex.Data["LiteDB.CommitOutcome"].Should().Be("Unknown");
        }
        // A write failure that is not an IOException (EPERM/EACCES surface as UnauthorizedAccessException):
        // the truncation synced, yet the caller gets no outcome at all.
        [Fact]
        public void Non_io_write_failure_carries_no_outcome()
        {
            using var file = new TempFile();
            using var engine = new LiteEngine(new EngineSettings { Filename = file.Filename });
            using var db = new LiteDatabase(engine, disposeOnClose: false);
            db.CheckpointSize = 0;
            var rows = db.GetCollection("rows");
            rows.Insert(new BsonDocument { ["_id"] = 1 });
            var failed = false;
            engine.SimulateCrashPoint = point =>
            {
                if (point != "wal-confirmation-after-write" || failed) return;
                failed = true;
                throw new UnauthorizedAccessException("injected EPERM");
            };
            Action insert = () => rows.Insert(new BsonDocument { ["_id"] = 2 });
            var ex = insert.Should().Throw<Exception>().Which;
            engine.SimulateCrashPoint = null;
            ex.Data.Contains("LiteDB.CommitOutcome").Should().BeTrue("a failed commit carries its outcome (" + ex.GetType().Name + ")");
        }
    }
}
#endif
