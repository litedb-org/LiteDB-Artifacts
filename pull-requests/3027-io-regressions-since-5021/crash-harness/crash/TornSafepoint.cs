using System;
using System.IO;
using System.Linq;
using LiteDB;

public static class TornSafepoint
{
    sealed class FailingLog : MemoryStream
    {
        public bool Armed; public bool InPlaceOnly; public long FailedAt = -1; public long LengthAtFail;
        public override void Write(byte[] buffer, int offset, int count)
        {
            if (Armed && (!InPlaceOnly || Position < Length))
            {
                Armed = false; FailedAt = Position; LengthAtFail = Length;
                base.Write(buffer, offset, Math.Min(count, 127));
                throw new UnauthorizedAccessException("injected partial write (EACCES)");
            }
            base.Write(buffer, offset, count);
        }
    }

    public static int Run(bool inPlaceOnly)
    {
        var data = new MemoryStream();
        var log = new FailingLog { InPlaceOnly = inPlaceOnly };
        byte[] crashData, crashLog;
        var db = new LiteDatabase(data, null, log);
        var a = db.GetCollection("a"); var b = db.GetCollection("b");
        b.Insert(new BsonDocument { ["_id"] = 1 });
        db.Checkpoint();
        db.BeginTrans();
        var pad = new string('x', 6000);
        long lenBefore = log.Length;
        var i = 0;
        for (; i < 1500; i++) a.Insert(new BsonDocument { ["_id"] = i, ["v"] = 0, ["pad"] = pad });
        Console.WriteLine($"after inserts: log grew {log.Length - lenBefore} bytes (safepoint happened: {log.Length > lenBefore})");
        log.Armed = true;
        try
        {
            for (var round = 1; round < 4; round++)
                for (var k = 0; k < 1500; k++) a.Update(new BsonDocument { ["_id"] = k, ["v"] = round, ["pad"] = pad });
            Console.WriteLine("no failure injected");
        }
        catch (Exception ex) { Console.WriteLine($"update failed: {ex.GetType().Name} at log pos {log.FailedAt} (len {log.LengthAtFail}, in-place={log.FailedAt < log.LengthAtFail})"); }
        try { db.Rollback(); } catch (Exception ex) { Console.WriteLine("rollback: " + ex.GetType().Name); }
        b.Insert(new BsonDocument { ["_id"] = 2 });
        Console.WriteLine("acknowledged insert b:2; visible=" + (b.FindById(2) != null));
        crashData = data.ToArray(); crashLog = log.ToArray();
        GC.SuppressFinalize(db);
        var d2 = new MemoryStream(); d2.Write(crashData, 0, crashData.Length);
        var l2 = new MemoryStream(); l2.Write(crashLog, 0, crashLog.Length);
        try
        {
            using (var re = new LiteDatabase(d2, null, l2))
            {
                var ok = re.GetCollection("b").FindById(2) != null;
                Console.WriteLine($"after crash recovery: b:2 present={ok}; a count={re.GetCollection("a").Count()}");
                return ok ? 0 : 1;
            }
        }
        catch (Exception ex) { Console.WriteLine("reopen FAIL " + ex.GetType().Name + ": " + ex.Message); return 1; }
    }
}
