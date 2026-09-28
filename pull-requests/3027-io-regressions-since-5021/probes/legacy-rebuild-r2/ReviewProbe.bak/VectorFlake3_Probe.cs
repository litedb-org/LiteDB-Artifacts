using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using LiteDB.Engine;
using LiteDB.Vector;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class VectorFlake3_Probe
    {
        private readonly ITestOutputHelper _out;
        public VectorFlake3_Probe(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData("fresh-after")]
        [InlineData("fresh-before")]
        [InlineData("rebuild")]
        public void Seeds(string mode)
        {
            var failing = new System.Collections.Generic.List<int>();
            var previous = VectorIndexService.LevelRandomFactory;
            try
            {
                for (var seed = 0; seed < 300; seed++)
                {
                    var s = seed;
                    VectorIndexService.LevelRandomFactory = () => new Random(s);
                    int id;
                    if (mode == "rebuild")
                    {
                        using var file = new TempFile();
                        File.WriteAllBytes(file.Filename, Fixture("vectors.db"));
                        using var db = new LiteDatabase(file.Filename);
                        db.Rebuild();
                        id = db.GetCollection("computed").Query().TopKNear(BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new[] { 1f, 7f }, 1).ToArray().Single()["_id"].AsInt32;
                    }
                    else
                    {
                        using var db = new LiteDatabase(new MemoryStream());
                        var col = db.GetCollection("computed");
                        if (mode == "fresh-before") col.EnsureIndex("coalesced", BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new VectorIndexOptions(2));
                        for (var i = 0; i < 20; i++)
                        {
                            var doc = new BsonDocument { ["_id"] = i };
                            doc["Embedding"] = i % 5 == 0 ? BsonValue.Null : new BsonVector(new[] { 1f, (float)i });
                            col.Insert(doc);
                        }
                        if (mode == "fresh-after") col.EnsureIndex("coalesced", BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new VectorIndexOptions(2));
                        id = col.Query().TopKNear(BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new[] { 1f, 7f }, 1).ToArray().Single()["_id"].AsInt32;
                    }
                    if (id != 7) failing.Add(seed);
                }
            }
            finally { VectorIndexService.LevelRandomFactory = previous; }
            _out.WriteLine($"mode={mode} failing seeds={failing.Count}/300: {string.Join(",", failing.Take(20))}");
        }

        private static byte[] Fixture(string name)
        {
            using var resource = typeof(VectorFlake3_Probe).Assembly.GetManifestResourceStream(
                "LiteDB.Tests.Resources.Vectors_6_0_0_prerelease_114.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
