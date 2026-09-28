using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Engine;
using LiteDB.Vector;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class VectorFlake_Probe
    {
        private readonly ITestOutputHelper _out;
        public VectorFlake_Probe(ITestOutputHelper output) { _out = output; }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Fresh_computed_vector_nearest(bool indexFirst)
        {
            var failures = 0;
            for (var run = 0; run < 200; run++)
            {
                using var db = new LiteDatabase(new MemoryStream());
                var col = db.GetCollection("computed");
                if (indexFirst) col.EnsureIndex("coalesced", BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new VectorIndexOptions(2));
                for (var i = 0; i < 20; i++)
                {
                    var doc = new BsonDocument { ["_id"] = i };
                    doc["Embedding"] = i % 5 == 0 ? BsonValue.Null : new BsonVector(new[] { 1f, (float)i });
                    col.Insert(doc);
                }
                if (!indexFirst) col.EnsureIndex("coalesced", BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new VectorIndexOptions(2));
                var q = col.Query().TopKNear(BsonExpression.Create("COALESCE($.Embedding, [0, 0])"), new[] { 1f, 7f }, 1);
                var id = q.ToArray().Single()["_id"].AsInt32;
                if (id != 7) failures++;
            }
            _out.WriteLine($"indexFirst={indexFirst} failures={failures}/200");
        }
    }
}
