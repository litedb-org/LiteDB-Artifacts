#if DEBUG || TESTING
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Tests.Issues;
using Xunit;
using Xunit.Abstractions;
using static LiteDB.Constants;

namespace LiteDB.Tests.Regressions
{
    [Trait("Category", "IoSafety")]
    [Collection(NativeFileSyncCollection.Name)]
    public class ReviewA_Tests
    {
        private readonly ITestOutputHelper _output;
        public ReviewA_Tests(ITestOutputHelper output) { _output = output; }

        private static BsonDocument Row(int id) => new BsonDocument { ["_id"] = id, ["value"] = id % 7 };

        /// <summary>
        /// Decision 14: "Shared mode adds no work per operation". Count the syncs a shared connection's
        /// write operations pay beyond the commit's own log sync.
        /// </summary>
        [Fact]
        public void Shared_mode_syncs_per_operation()
        {
            using var file = new TempFile();
            var dataName = Path.GetFullPath(file.Filename);
            var logName = Path.GetFullPath(FileHelper.GetLogFile(file.Filename));
            var data = 0; var log = 0; var dir = 0;
            try
            {
                using var db = new LiteDatabase($"Filename={file.Filename};Connection=shared");
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Row(0));
                NativeFileSync.SimulateErrno = path =>
                {
                    var full = Path.GetFullPath(path);
                    if (full == dataName) data++;
                    else if (full == logName) log++;
                    return 0;
                };
                NativeFileSync.SimulateDirectoryErrno = _ => { dir++; return 0; };
                for (var id = 1; id <= 10; id++) db.GetCollection("rows").Insert(Row(id));
                _output.WriteLine($"10 shared inserts: data syncs {data}, log syncs {log}, directory syncs {dir}");
                dir.Should().Be(0, "the log's directory was proven durable by the first commit (DurableLogs)");
            }
            finally
            {
                NativeFileSync.SimulateErrno = null;
                NativeFileSync.SimulateDirectoryErrno = null;
            }
        }

        /// <summary>
        /// FileReaderV8 (rebuild) is meant to take a lost data header from the WAL's header frame
        /// (FileReaderV8.Checksums.cs). InitializeChecksums casts _logStream to ChecksummedWalStream,
        /// but Open() wraps _logStream only after InitializeChecksums returned.
        /// </summary>
        [Fact]
        public void Rebuild_reader_takes_a_lost_header_from_the_header_frame()
        {
            using var file = new TempFile();
            using (var db = new LiteDatabase($"Filename={file.Filename};Durable Commits=false"))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 20).Select(Row));
            }
            var data = File.ReadAllBytes(file.Filename);
            Array.Clear(data, 0, 512); // the header's first sector never written back (the rest of this header page is zeros)
            File.WriteAllBytes(file.Filename, data);

            var errors = new List<FileReaderError>();
            using (var reader = new FileReaderV8(new EngineSettings { Filename = file.Filename }, errors))
            {
                reader.Open();
                foreach (var e in errors) _output.WriteLine($"REVIEW reader error: {e.Exception?.GetType().Name}: {e.Message}");
                errors.Should().BeEmpty();
                reader.GetCollections().Should().Equal("rows");
                reader.GetDocuments("rows").Count().Should().Be(20);
            }
        }

        /// <summary>HeaderFrame_Tests damage 2 clears the header's second half: check that it changes any byte.</summary>
        [Fact]
        public void Torn_half_way_damage_changes_the_header()
        {
            using var file = new TempFile();
            using (var db = new LiteDatabase($"Filename={file.Filename};Durable Commits=false"))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 50).Select(Row));
                db.GetCollection("rows").EnsureIndex("value");
            }
            var data = File.ReadAllBytes(file.Filename);
            data.Skip(PAGE_SIZE / 2).Take(PAGE_SIZE / 2).Any(b => b != 0).Should().BeTrue("otherwise the damage is a no-op");
        }

        /// <summary>
        /// Implementation note 9: an encrypted header page partly written back (its second half never
        /// reached the device: zero ciphertext) should be restored like a plain one (the plain case is
        /// HeaderFrame_Tests.Opted_out_commits_survive_a_data_file_that_lost_its_header(2)).
        /// </summary>
        [Fact]
        public void Encrypted_header_page_half_written_back_is_restored()
        {
            using var file = new TempFile();
            var connection = $"Filename={file.Filename};Password=secret;Durable Commits=false";
            using (var db = new LiteDatabase(connection))
            {
                db.CheckpointSize = 0;
                db.GetCollection("rows").Insert(Enumerable.Range(1, 12).Select(Row));
            }
            var data = File.ReadAllBytes(file.Filename);
            Array.Clear(data, PAGE_SIZE + PAGE_SIZE / 2, PAGE_SIZE / 2); // second half of the header page's ciphertext
            File.WriteAllBytes(file.Filename, data);

            using var reopened = new LiteDatabase(connection);
            reopened.GetCollection("rows").Count().Should().Be(12);
        }

        /// <summary>
        /// Implementation note 15:"reuse costs no sync per engine or shared operation". Count the log
        /// syncs of shared operations that reuse retired slots (ProveSlotReuse still syncs the raw log
        /// once per engine).
        /// </summary>
        [Fact]
        public void Shared_slot_reuse_log_syncs_per_operation()
        {
            using var file = new TempFile();
            using (var setup = new LiteDatabase(file.Filename))
                setup.GetCollection("rows").Insert(Enumerable.Range(1, 64).Select(id => LiteDB.Internals.MvccRetirementScenario.Document(id, 0)));
            var logName = Path.GetFullPath(FileHelper.GetLogFile(file.Filename));
            void Update(LiteDatabase db, int value) =>
                db.GetCollection("rows").Upsert(Enumerable.Range(1, 64).Select(id => LiteDB.Internals.MvccRetirementScenario.Document(id, value)));
            var log = 0;
            try
            {
                using var engine = new SharedEngine(new EngineSettings { Filename = file.Filename });
                using var db = new LiteDatabase(engine, disposeOnClose: false);
                db.CheckpointSize = 0;
                for (var value = 1; value <= 5; value++) Update(db, value);
                using (var reader = engine.Query("rows", new Query()))
                {
                    reader.Read().Should().BeTrue();
                    var worker = new System.Threading.Thread(() =>
                    {
                        for (var value = 6; value <= 9; value++) Update(db, value);
                        db.Checkpoint();
                    });
                    worker.Start();
                    worker.Join();
                }
                var dataSyncs = 0;
                var dataName = Path.GetFullPath(file.Filename);
                NativeFileSync.SimulateErrno = path => { var f = Path.GetFullPath(path); if (f == logName) log++; else if (f == dataName) dataSyncs++; return 0; };
                NativeFileSync.SimulateDirectoryErrno = _ => 0;
                var logBefore = new FileInfo(logName).Length;
                for (var value = 10; value < 15; value++) Update(db, value);
                _output.WriteLine($"REVIEW 5 shared updates reusing slots: log syncs {log}, data syncs {dataSyncs}, log bytes {logBefore} -> {new FileInfo(logName).Length}");
                log.Should().Be(5, "one log sync per commit");
            }
            finally
            {
                NativeFileSync.SimulateErrno = null;
                NativeFileSync.SimulateDirectoryErrno = null;
            }
        }
    }
}
#endif
