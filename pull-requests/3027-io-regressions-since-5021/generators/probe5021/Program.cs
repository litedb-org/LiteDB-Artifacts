using LiteDB;
var dir = "$SCRATCH/badimages/";
foreach (var stem in new[] { "d1-l-4096-0", "d0-l-4096-0" })
{
    var f = Path.Combine(Path.GetTempPath(), "p5021-" + Guid.NewGuid() + ".db");
    File.Copy(dir + stem + ".db", f); File.Copy(dir + stem + "-log.db", f.Replace(".db", "-log.db"));
    try
    {
        using var db = new LiteDatabase(f);
        var docs = db.GetCollection("docs").FindAll().ToList();
        Console.WriteLine($"{stem}: 5.0.21 count={docs.Count} updated={docs.Count(x => x["value"].AsInt32 == 7)}");
    }
    catch (Exception ex) { Console.WriteLine($"{stem}: 5.0.21 THROW {ex.GetType().Name}: {ex.Message}"); }
}
