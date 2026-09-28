using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace LiteDB.Tests.Review
{
    public class ReviewSalvage_Tests
    {
        /// <summary>
        /// DamagedSalvageDuplicate_5_0_21 with document 7's b restored to "u-7": the first readable part
        /// {_id: 7, b: "u-7"} fits and is kept; the second, {_id: 7}, must be reported as a duplicate _id.
        /// </summary>
        [Fact]
        public void Second_readable_part_with_a_kept_id_is_reported_as_a_duplicate()
        {
            using var file = new TempFile();
            var bytes = Fixture();
            var pattern = new byte[] { 0x10, (byte)'_', (byte)'i', (byte)'d', 0, 7, 0, 0, 0, 2, (byte)'b', 0, 4, 0, 0, 0, (byte)'u', (byte)'-', (byte)'1', 0 };
            var at = Enumerable.Range(0, bytes.Length - pattern.Length).Single(i => bytes.Skip(i).Take(pattern.Length).SequenceEqual(pattern));
            bytes[at + 18] = (byte)'7';
            File.WriteAllBytes(file.Filename, bytes);

            using var db = new LiteDatabase($"Filename={file.Filename};Auto-Rebuild=true");
            var col = db.GetCollection("c");
            col.FindAll().Select(x => x["_id"].AsInt32).Should().BeEquivalentTo(new[] { 1, 7, 9 });
            col.FindById(7)["b"].AsString.Should().Be("u-7");
            var errors = db.GetCollection("_rebuild_errors").FindAll().Select(x => x["message"].AsString).ToList();
            foreach (var e in errors) Console.WriteLine("salvage error: " + e);
            errors.Should().Contain(x => x.Contains("damaged document 7 was not kept: another document has the same _id"));
            errors.Count(x => x.Contains("Only the readable part of damaged document 7 was kept")).Should().Be(1);
        }

        private static byte[] Fixture()
        {
            using var resource = typeof(ReviewSalvage_Tests).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.DamagedSalvageDuplicate_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry("damaged.db").Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
