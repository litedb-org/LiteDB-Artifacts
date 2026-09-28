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
    public class Layout_Probe
    {
        private readonly ITestOutputHelper _out;
        public Layout_Probe(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData("a")]
        [InlineData("b")]
        [InlineData("c")]
        [InlineData("d")]
        public void Layouts(string scenario)
        {
            using var file = new TempFile();
            File.WriteAllBytes(file.Filename, Fixture("customers.db"));
            using (var db = new LiteDatabase(file.Filename))
            {
                var col = db.GetCollection("customers");
                for (var id = 1; id <= 200; id++)
                {
                    var doc = col.FindById(id);
                    if (doc == null) continue;
                    doc["E1"] = new BsonVector(new[] { (float)id, 1f });
                    doc["E2"] = new BsonVector(new[] { 1f, (float)id });
                    col.Update(doc);
                }
                switch (scenario)
                {
                    case "a": col.EnsureIndex("v1", "$.E1", new VectorIndexOptions(2, VectorDistanceMetric.Euclidean)); col.DropIndex("Name"); break;
                    case "b": col.EnsureIndex("v1", "$.E1", new VectorIndexOptions(2)); col.DropIndex("v1"); col.DropIndex("CustomerId"); break;
                    case "c": col.EnsureIndex("v1", "$.E1", new VectorIndexOptions(2)); col.EnsureIndex("v2", "$.E2", new VectorIndexOptions(2, VectorDistanceMetric.Euclidean)); col.DropIndex("v1"); col.DropIndex("Name"); break;
                    case "d": col.EnsureIndex("v1", "$.E1", new VectorIndexOptions(2, VectorDistanceMetric.Euclidean)); col.DropIndex("Name"); col.DropIndex("CustomerId"); col.EnsureIndex("Name"); break;
                }
            }
            for (var pass = 0; pass < 2; pass++)
            using (var db = new LiteDatabase(file.Filename))
            {
                var col = db.GetCollection("customers");
                var idx = db.GetCollection("$indexes").Find(Query.EQ("collection", "customers")).Select(x => x["name"].AsString + ":" + x["indexType"].AsInt32).ToArray();
                _out.WriteLine(scenario + " pass " + pass + " indexes: " + string.Join(",", idx));
                col.Insert(new BsonDocument { ["_id"] = 5000 + pass, ["Name"] = "z", ["E1"] = new BsonVector(new[] { 5000f, 1f }), ["E2"] = new BsonVector(new[] { 1f, 5000f }) });
                if (scenario == "a" || scenario == "d")
                {
                    var q = col.Query().TopKNear(BsonExpression.Create("$.E1"), new[] { 50f, 1f }, 1);
                    q.GetPlan()["index"]["name"].AsString.Should().Be("v1");
                    q.ToArray().Single()["_id"].AsInt32.Should().Be(50);
                }
                if (scenario == "c")
                {
                    var q = col.Query().TopKNear(BsonExpression.Create("$.E2"), new[] { 1f, 50f }, 1);
                    q.GetPlan()["index"]["name"].AsString.Should().Be("v2");
                    q.ToArray().Single()["_id"].AsInt32.Should().Be(50);
                }
                col.Count().Should().Be(201 + pass);
            }
            using (var db = new LiteDatabase(file.Filename)) { db.Rebuild(); db.GetCollection("_rebuild_errors").Count().Should().Be(0); db.GetCollection("customers").Count().Should().Be(202); }
        }

        private static byte[] Fixture(string name)
        {
            using var resource = typeof(Layout_Probe).Assembly.GetManifestResourceStream("LiteDB.Tests.Resources.DropIndex_5_0_21.zip");
            using var zip = new ZipArchive(resource, ZipArchiveMode.Read);
            using var entry = zip.GetEntry(name).Open();
            using var bytes = new MemoryStream();
            entry.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
