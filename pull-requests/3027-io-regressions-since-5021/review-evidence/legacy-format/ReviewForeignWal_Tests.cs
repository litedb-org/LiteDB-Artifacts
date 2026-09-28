using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace LiteDB.Tests.Review
{
    public class ReviewForeignWal_Tests
    {
        // WAL written by the real 5.0.21 package for ANOTHER database (1730 pages, LastPageID 1732 after
        // one committed insert into a new collection "fresh"), placed beside WalCrash_5_0_21's crash.db.
        private const string ForeignLog = "$SCRATCH/big1/c-log.db";

        [Theory]
        [InlineData("")]
        [InlineData(";readonly=true;legacy index scan=true")]
        [InlineData(";Auto-Rebuild=true")]
        public void Foreign_5_0_21_wal_beside_a_smaller_legacy_file_is_refused(string options)
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            var data = Entry("crash.db");
            var log = File.ReadAllBytes(ForeignLog);
            try
            {
                File.WriteAllBytes(file.Filename, data);
                File.WriteAllBytes(logName, log);
                Exception error = null;
                try
                {
                    using var db = new LiteDatabase($"Filename={file.Filename}" + options);
                    Console.WriteLine("foreign[" + options + "] collections=" + string.Join(",", db.GetCollectionNames()) +
                        " fresh=" + (db.GetCollectionNames().Contains("fresh") ? db.GetCollection("fresh").Count().ToString() : "-"));
                }
                catch (Exception ex) { error = ex; Console.WriteLine("foreign[" + options + "] " + ex.GetType().Name + " " + (ex as LiteException)?.ErrorCode + ": " + ex.Message); }
                var now = File.ReadAllBytes(file.Filename);
                Console.WriteLine("foreign[" + options + "] data " + data.Length + " -> " + now.Length + " unchanged=" + now.SequenceEqual(data) +
                    " log=" + (File.Exists(logName) ? new FileInfo(logName).Length.ToString() : "missing") +
                    " files=" + string.Join(",", Directory.GetFiles(Path.GetDirectoryName(file.Filename), Path.GetFileNameWithoutExtension(file.Filename) + "*").Select(Path.GetFileName)));
                error.Should().BeOfType<LiteException>().Which.ErrorCode.Should().Be(LiteException.INVALID_DATABASE);
                now.Should().Equal(data);
            }
            finally
            {
                File.Delete(logName);
                foreach (var f in Directory.GetFiles(Path.GetDirectoryName(file.Filename), Path.GetFileNameWithoutExtension(file.Filename) + "-*")) File.Delete(f);
            }
        }

        // A foreign 5.0.21 header page (valid header string, another creation time) whose LastPageID
        // covers a data page far beyond both files, committed in one transaction after the crash WAL.
        [Theory]
        [InlineData(100000u, false)]
        [InlineData(0xCDCDCDCDu, true)]
        public void Foreign_header_raises_the_bound_for_a_far_page(uint farPage, bool readOnly)
        {
            using var file = new TempFile();
            var logName = FileHelper.GetLogFile(file.Filename);
            var data = Entry("crash.db");
            var foreignHeader = File.ReadAllBytes("$SCRATCH/big1/c.db").Take(Constants.PAGE_SIZE).ToArray();
            var far = new byte[Constants.PAGE_SIZE];
            BitConverter.GetBytes(farPage).CopyTo(far, 0);
            far[4] = 4; // data page
            BitConverter.GetBytes(0x7FFF0001u).CopyTo(far, 14);
            BitConverter.GetBytes(0x7FFF0001u).CopyTo(foreignHeader, 14);
            foreignHeader[18] = 1; // confirmed
            BitConverter.GetBytes(farPage).CopyTo(foreignHeader, 64); // LastPageID
            var log = Entry("crash-log.db").Concat(far).Concat(foreignHeader).ToArray();
            try
            {
                File.WriteAllBytes(file.Filename, data);
                File.WriteAllBytes(logName, log);
                Exception error = null;
                try
                {
                    using var db = new LiteDatabase($"Filename={file.Filename}" + (readOnly ? ";readonly=true;legacy index scan=true" : ""));
                    Console.WriteLine("far[" + farPage + "] opened, collections=" + string.Join(",", db.GetCollectionNames()));
                }
                catch (Exception ex) { error = ex; Console.WriteLine("far[" + farPage + "] " + (ex as LiteException)?.ErrorCode + ": " + ex.Message); }
                Console.WriteLine("far[" + farPage + "] data " + data.Length + " -> " + new FileInfo(file.Filename).Length + " log=" + (File.Exists(logName) ? "kept" : "missing"));
                error.Should().BeOfType<LiteException>().Which.ErrorCode.Should().Be(LiteException.INVALID_DATABASE);
                File.ReadAllBytes(file.Filename).Should().Equal(data);
            }
            finally { File.Delete(logName); }
        }

        private static byte[] Entry(string name)
        {
            using var zip = new ZipArchive(typeof(ReviewForeignWal_Tests).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.WalCrash_5_0_21.zip"), ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
