using LiteDB;
using LiteDB.Engine;
var bytes = File.ReadAllBytes("damaged.db");
// 1) unmarked caller stream, AutoRebuild: open, read all
{
    var ms = new MemoryStream(); ms.Write(bytes); ms.Position = 0;
    try { using var e = new LiteEngine(new EngineSettings { DataStream = ms, AutoRebuild = true }); using var db = new LiteDatabase(e); Console.WriteLine("5.0.21 open ok, count=" + db.GetCollection("c").Count()); var all = db.GetCollection("c").FindAll().ToList(); Console.WriteLine("read all: " + all.Count); }
    catch (Exception ex) { Console.WriteLine("5.0.21 unmarked: " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
    Console.WriteLine("mark after: " + ms.ToArray()[191]);
    try { using var e = new LiteEngine(new EngineSettings { DataStream = ms, AutoRebuild = true }); Console.WriteLine("5.0.21 reopen ok"); }
    catch (Exception ex) { Console.WriteLine("5.0.21 reopen: " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
}
// 2) marked caller stream, AutoRebuild
{
    var b = (byte[])bytes.Clone(); b[int.Parse(args[0])] = 1;
    var ms = new MemoryStream(); ms.Write(b); ms.Position = 0;
    try { using var e = new LiteEngine(new EngineSettings { DataStream = ms, AutoRebuild = true }); Console.WriteLine("5.0.21 marked open ok"); }
    catch (Exception ex) { Console.WriteLine("5.0.21 marked: " + ex.GetType().Name + ": " + ex.Message.Split('\n')[0]); }
}
