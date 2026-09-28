using System;
using System.Linq;
using LiteDB.Engine;
using Xunit;
using Xunit.Abstractions;

namespace LiteDB.Tests.ReviewProbe
{
    public class Expr_Probe
    {
        private readonly ITestOutputHelper _out;
        public Expr_Probe(ITestOutputHelper output) { _out = output; }

        [Fact]
        public void Which_throw_on_partial()
        {
            var exprs = new[] {
                "JSON($.s)", "INT32($.s)", "DOUBLE($.s)", "ARRAY($.v)", "MAP($.items => @.v)", "$.v[0]", "IIF($.n > 0, $.v, [0,0])",
                "SUBSTRING($.s, 0, 2)", "DATEADD('d', 1, $.d)", "[DOUBLE($.x), DOUBLE($.y)]", "[$.x / $.y, 1]", "[$.x % $.y, 1]", "TO_ARRAY($.v)",
                "ITEMS($.v)", "$.v[*]", "SPLIT($.s, ',')", "[DOUBLE(SPLIT($.s, ',')[0]), 1]", "COUNT($.v)", "[LENGTH($.s), 1]", "[$.x + $.y, 1]",
            };
            var complete = new BsonDocument { ["_id"] = 1, ["s"] = "1,2", ["v"] = new BsonArray(new BsonValue[] { 1.0, 2.0 }), ["n"] = 1, ["x"] = 4, ["y"] = 2,
                ["d"] = DateTime.UtcNow, ["items"] = new BsonArray(new BsonValue[] { new BsonDocument { ["v"] = 1 } }) };
            var partial = new BsonDocument { ["_id"] = 1 };
            foreach (var e in exprs)
            {
                string R(BsonDocument d)
                {
                    try { var x = BsonExpression.Create(e); return "ok:" + x.ExecuteScalar(d, Collation.Default).ToString() + " type=" + x.Type + " scalar=" + x.IsScalar; }
                    catch (Exception ex) { return "THROW " + ex.GetType().Name + ": " + ex.Message; }
                }
                _out.WriteLine($"{e} | complete: {R(complete)} | partial: {R(partial)}");
            }
        }
    }
}
