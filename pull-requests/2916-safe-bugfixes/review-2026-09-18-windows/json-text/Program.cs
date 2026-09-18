using System;
using LiteDB;
foreach (var d in new[] { 0.1, 0.3, 19.99, 2.675, 1.0, 1.5, 100.0, 1e-7, 1e21, 1.0/3 })
    Console.WriteLine(new BsonValue(d).ToString() + "   | doc: " + JsonSerializer.Serialize(new BsonDocument { ["price"] = d }));
