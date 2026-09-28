using System.IO;
using System.Linq;
using FluentAssertions;
using LiteDB.Vector;
using Xunit;

namespace LiteDB.Tests.Review
{
    public class ReviewVectorSection_Tests
    {
        // Written by 6.0.0-prerelease.114: collection docs, indexes name, zeta (vector $.A, 2 dims),
        // Alpha (vector $.B, 3 dims, Euclidean); vector index "mid" created then dropped by the prerelease.
        private const string Source = "$SCRATCH/pre2/multi.db";

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Two_vector_indexes_of_a_prerelease_file_keep_their_metadata(bool readOnly)
        {
            using var file = new TempFile();
            File.Copy(Source, file.Filename, true);
            var connection = $"Filename={file.Filename}" + (readOnly ? ";readonly=true;legacy index scan=true" : "");
            for (var pass = 0; pass < 2; pass++)
            {
                using var db = new LiteDatabase(connection);
                var docs = db.GetCollection("docs");
                docs.Count().Should().Be(30 + (readOnly ? 0 : pass));
                if (readOnly) continue;
                var q1 = docs.Query().TopKNear(BsonExpression.Create("$.A"), new[] { 20f, 1f }, 5);
                q1.GetPlan()["index"]["name"].AsString.Should().Be("zeta");
                q1.ToArray().Select(x => x["_id"].AsInt32).Should().Contain(20);
                var q2 = docs.Query().TopKNear(BsonExpression.Create("$.B"), new[] { 1f, 12f, 2f }, 5);
                q2.GetPlan()["index"]["name"].AsString.Should().Be("Alpha");
                q2.ToArray().Select(x => x["_id"].AsInt32).Should().Contain(12);
                docs.EnsureIndex("zeta", "$.A", new VectorIndexOptions(2)).Should().BeFalse();
                docs.EnsureIndex("Alpha", "$.B", new VectorIndexOptions(3, VectorDistanceMetric.Euclidean)).Should().BeFalse();
                docs.Insert(new BsonDocument { ["_id"] = 100 + pass, ["A"] = new BsonVector(new[] { 500f, 1f }), ["B"] = new BsonVector(new[] { 1f, 500f, 2f }) });
            }
        }
    

        // multi.db after the real 5.0.21 package ran DropIndex("name") on it.
        private const string Dropped = "$SCRATCH/pre3/multi.db";

        [Fact]
        public void Real_5_0_21_drop_index_on_a_two_vector_prerelease_file()
        {
            using var file = new TempFile();
            File.Copy(Dropped, file.Filename, true);
            var before = File.ReadAllBytes(file.Filename);
            using (var ro = new LiteDatabase($"Filename={file.Filename};readonly=true;legacy index scan=true"))
                ro.GetCollection("docs").Count().Should().Be(30);
            File.ReadAllBytes(file.Filename).Should().Equal(before);

            System.Action open = () => new LiteDatabase(file.Filename).Dispose();
            var ex = open.Should().Throw<LiteException>().Which;
            System.Console.WriteLine("writable: " + ex.ErrorCode + " " + ex.Message);
            ex.ErrorCode.Should().Be(LiteException.INVALID_DATAFILE_STATE);

            using var db = new LiteDatabase($"Filename={file.Filename};Auto-Rebuild=true");
            db.GetCollection("docs").Count().Should().Be(30);
            var names = db.GetCollection("$indexes").Find(Query.EQ("collection", "docs")).Select(x => x["name"].AsString).ToArray();
            System.Console.WriteLine("indexes after rebuild: " + string.Join(",", names));
            foreach (var e in db.GetCollection("_rebuild_errors").FindAll()) System.Console.WriteLine("rebuild error: " + e["message"].AsString);
        }
    }
}
