using LiteDB;
var root = args[0];
foreach (var dir in Directory.GetDirectories(root).OrderBy(x => x))
{
    var lines = File.ReadAllLines(Path.Combine(dir, "expected.txt"));
    var expected = lines[0];
    var password = lines.Length > 1 && lines[1].Length > 0 ? lines[1] : null;
    int ok = 0, rejected = 0, bad = 0;
    var notes = new List<string>();
    foreach (var db in Directory.GetFiles(dir, "*.db").Where(f => !f.EndsWith("-log.db")).OrderBy(x => x))
    {
        var ev = File.ReadAllText(Path.ChangeExtension(db, ".txt"));
        var tmp = Path.Combine(Path.GetTempPath(), "p21-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        var target = Path.Combine(tmp, "x.db");
        File.Copy(db, target);
        var log = db.Substring(0, db.Length - 3) + "-log.db";
        if (new FileInfo(log).Length > 0) File.Copy(log, Path.Combine(tmp, "x-log.db"));
        string r;
        try
        {
            var cs = $"Filename={target}" + (password != null ? $";Password={password}" : "");
            string once(){ using var d = new LiteDatabase(cs); var docs = d.GetCollection("docs").FindAll().ToList(); return $"{docs.Count}/{docs.Count(x => x["value"].AsInt32 == 7)}"; }
            r = once(); r += "|" + once();
        }
        catch (Exception ex) { r = "THROW " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]; }
        if (r == expected + "|" + expected) ok++;
        else if (r.StartsWith("THROW")) { rejected++; notes.Add($"  {Path.GetFileName(db)} {ev}: {r}"); }
        else { bad++; notes.Add($"  BAD {Path.GetFileName(db)} {ev}: {r}"); }
        Directory.Delete(tmp, true);
    }
    Console.WriteLine($"{Path.GetFileName(dir)}: ok={ok} rejected={rejected} BAD={bad}");
    foreach (var n in notes) Console.WriteLine(n);
}
