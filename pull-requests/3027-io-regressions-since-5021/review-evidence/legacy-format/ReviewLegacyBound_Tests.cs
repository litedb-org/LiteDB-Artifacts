using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using Xunit;

namespace LiteDB.Tests.Review
{
    public class ReviewLegacyBound_Tests
    {
        /// <summary>
        /// Written by the real LiteDB 5.0.21 package: a worker thread held an open explicit
        /// transaction on "a" (300 inserts, pages allocated from the shared header but not yet
        /// written), the main thread committed 3 inserts into "b" (new pages 58, 59), then the
        /// process was killed (Environment.FailFast). The data file has LastPageID 6 and 7 pages;
        /// the WAL has 13 pages; its committed pages 58/59 are legitimate. 5.0.21 itself reopens
        /// the pair and reads b = 13 documents.
        /// </summary>
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Concurrent_writer_crash_WAL_from_5_0_21_opens(bool readOnly)
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            try
            {
                File.WriteAllBytes(file.Filename, Entry("c.db"));
                File.WriteAllBytes(logName, Entry("c-log.db"));
                var connection = $"Filename={file.Filename}" + (readOnly ? ";readonly=true;legacy index scan=true" : "");
                using var db = new LiteDatabase(connection);
                db.GetCollection("b").Count().Should().Be(13);
                db.GetCollection("a").Count().Should().Be(10);
            }
            finally { File.Delete(logName); }
        }

        [Theory]
        [InlineData(";Auto-Rebuild=true")]
        [InlineData(";Connection=shared")]
        [InlineData(";Connection=shared;Auto-Rebuild=true")]
        public void Out_of_range_refusal_changes_neither_file(string options)
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            var data = Crash("crash.db");
            var log = Crash("crash-log.db").Concat(Enumerable.Repeat((byte)0xCD, Constants.PAGE_SIZE)).ToArray();
            try
            {
                File.WriteAllBytes(file.Filename, data);
                File.WriteAllBytes(logName, log);
                Action open = () => { using var db = new LiteDatabase($"Filename={file.Filename}" + options); db.GetCollection("docs").Count(); };
                open.Should().Throw<LiteException>().Where(x => x.ErrorCode == LiteException.INVALID_DATABASE);
                File.ReadAllBytes(file.Filename).Should().Equal(data);
                File.ReadAllBytes(logName).Should().Equal(log);
                Directory.GetFiles(Path.GetDirectoryName(file.Filename), Path.GetFileNameWithoutExtension(file.Filename) + "*")
                    .Select(Path.GetFileName).Should().BeEquivalentTo(new[] { Path.GetFileName(file.Filename), Path.GetFileName(logName) });
            }
            finally { File.Delete(logName); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Committed_non_header_page_zero(bool readOnly)
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            var data = Crash("crash.db");
            var page = new byte[Constants.PAGE_SIZE];
            page[BasePage.P_PAGE_TYPE] = 4; // data page
            BitConverter.GetBytes(999u).CopyTo(page, BasePage.P_TRANSACTION_ID);
            page[BasePage.P_IS_CONFIRMED] = 1;
            for (var i = 32; i < page.Length; i++) page[i] = 0x5A;
            var log = Crash("crash-log.db").Concat(page).ToArray();
            try
            {
                File.WriteAllBytes(file.Filename, data);
                File.WriteAllBytes(logName, log);
                var connection = $"Filename={file.Filename}" + (readOnly ? ";readonly=true;legacy index scan=true" : "");
                try
                {
                    using (var db = new LiteDatabase(connection))
                        Console.WriteLine("open1 docs=" + db.GetCollection("docs").Count());
                    using (var db = new LiteDatabase(connection))
                        Console.WriteLine("open2 docs=" + db.GetCollection("docs").Count());
                }
                catch (Exception ex) { Console.WriteLine("page0 readOnly=" + readOnly + ": " + ex.GetType().Name + " " + (ex as LiteException)?.ErrorCode + " " + ex.Message); }
                var now = File.ReadAllBytes(file.Filename);
                Console.WriteLine("page0 readOnly=" + readOnly + " data page 0 type now=" + now[4] + " byte[100]=" + now[100].ToString("X2") + " unchanged=" + now.SequenceEqual(data) + " log=" + (File.Exists(logName) ? new FileInfo(logName).Length.ToString() : "missing") + " orig log=" + log.Length + " files=" + string.Join(",", Directory.GetFiles(Path.GetDirectoryName(file.Filename), Path.GetFileNameWithoutExtension(file.Filename) + "*").Select(Path.GetFileName)));
                try { using var again = new LiteDatabase(connection); Console.WriteLine("page0 reopen docs=" + again.GetCollection("docs").Count()); }
                catch (Exception ex) { Console.WriteLine("page0 reopen: " + (ex as LiteException)?.ErrorCode + " " + ex.Message); }
            }
            finally { File.Delete(logName); }
        }

        private static byte[] Crash(string name)
        {
            using var zip = new ZipArchive(typeof(ReviewLegacyBound_Tests).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.WalCrash_5_0_21.zip"), ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }

        private static byte[] Entry(string name)
        {
            using var zip = new ZipArchive(typeof(ReviewLegacyBound_Tests).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.ConcurrentWalCrash_5_0_21.zip"), ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
