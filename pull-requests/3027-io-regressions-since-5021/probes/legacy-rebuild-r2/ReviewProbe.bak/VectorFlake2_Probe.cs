using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Vector;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class VectorFlake2_Probe
    {
        private readonly ITestOutputHelper _out;
        public VectorFlake2_Probe(ITestOutputHelper output) { _out = output; }

        private static int NearestComputed(ILiteCollection<BsonDocument> computed, float y)
        {
            var query = computed.Query().TopKNear(BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new[] { 1f, y }, 1);
            return query.ToArray().Single()["_id"].AsInt32;
        }

        [Theory]
        [InlineData("none")]
        [InlineData("rebuild")]
        [InlineData("renamed")]
        public void Fixture_nearest(string mode)
        {
            var failures = 0; var runs = 100;
            string sample = "";
            for (var run = 0; run < runs; run++)
            {
                using var file = new TempFile();
                var bytes = Fixture("vectors.db");
                if (mode == "renamed")
                {
                    var name = System.Text.Encoding.UTF8.GetBytes("embedding\0");
                    var occ = Enumerable.Range(0, bytes.Length - name.Length).Where(i => bytes.Skip(i).Take(name.Length).SequenceEqual(name)).ToArray();
                    bytes[occ[1] + name.Length - 2] = (byte)'X';
                    if (run % 2 == 0) bytes[HeaderPage.P_INVALID_DATAFILE_STATE] = 1;
                }
                File.WriteAllBytes(file.Filename, bytes);
                if (mode == "renamed" && run % 2 == 1) { try { new LiteDatabase(file.Filename).Dispose(); } catch (LiteException) { } }
                using var db = new LiteDatabase($"Filename={file.Filename};Auto-Rebuild=true");
                if (mode == "rebuild") db.Rebuild();
                var col = db.GetCollection("computed");
                var id = NearestComputed(col, 7f);
                if (id != 7)
                {
                    failures++;
                    if (sample == "") sample = string.Join(",", col.Query().TopKNear(BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new[] { 1f, 7f }, 20).ToArray().Select(x => x["_id"].AsInt32));
                }
            }
            _out.WriteLine($"mode={mode} failures={failures}/{runs} sample={sample}");
        }

        private static byte[] Fixture(string name)
        {
            using var resource = typeof(VectorFlake2_Probe).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.Vectors_6_0_0_prerelease_114.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
