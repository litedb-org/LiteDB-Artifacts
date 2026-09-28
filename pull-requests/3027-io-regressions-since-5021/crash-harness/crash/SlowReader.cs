using System;
using System.Linq;
using System.Threading;
using LiteDB;
public static class SlowReader
{
    public static int Run(string path, string opts, int holdMs)
    {
        try
        {
            using (var db = new LiteDatabase(CrashHarness.Conn(path, opts)))
            {
                var n = 0;
                foreach (var d in db.GetCollection("c").FindAll())
                {
                    if (n++ == 10) { Console.WriteLine("READER holding cursor"); Console.Out.Flush(); Thread.Sleep(holdMs); }
                }
                Console.WriteLine("READER done n=" + n);
            }
        }
        catch (Exception ex) { Console.WriteLine("READER FAIL " + ex.GetType().Name + ": " + ex.Message); }
        return 0;
    }
}
