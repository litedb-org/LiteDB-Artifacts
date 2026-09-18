using System;
using System.Linq;
using System.Threading;
using LiteDB;

internal static class LinqHang
{
    public class Person { public int Id { get; set; } public string Name { get; set; } }

    public static int Run()
    {
        foreach (var term in new[] { "%a", "%b", "a" })
        {
            var done = new ManualResetEventSlim(false);
            var result = -1;
            var worker = new Thread(() =>
            {
                using var db = new LiteDatabase(":memory:");
                var col = db.GetCollection<Person>("people");
                col.Insert(new Person { Id = 1, Name = "ab" });
                col.Insert(new Person { Id = 2, Name = "50%a" });
                result = col.Find(x => x.Name.EndsWith(term)).Count();
                done.Set();
            }) { IsBackground = true };
            worker.Start();
            Console.WriteLine(done.Wait(TimeSpan.FromSeconds(8))
                ? $"EndsWith(\"{term}\") finished, rows={result}"
                : $"HANG: col.Find(x => x.Name.EndsWith(\"{term}\")) did not finish within 8 s");
        }
        return 0;
    }
}
