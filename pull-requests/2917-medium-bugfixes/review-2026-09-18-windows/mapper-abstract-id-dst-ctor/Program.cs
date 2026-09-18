using System;
using System.Linq;
using LiteDB;

public abstract class Vehicle { public int Id { get; set; } public string Name { get; set; } }
public class Car : Vehicle { public int Doors { get; set; } }
public class Ev { public int Id { get; set; } public DateTime When { get; set; } }
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public Person(int id, string name) { Id = id; /* name intentionally not assigned by ctor */ }
}
public static class Prog
{
    static void Try(string label, Func<object> f)
    {
        try { Console.WriteLine(label + " => " + f()); }
        catch (Exception ex) { Console.WriteLine(label + " THROWS " + ex.GetType().Name + ": " + ex.Message); }
    }
    public static void Main()
    {
        using var db = new LiteDatabase(":memory:", new BsonMapper());
        var v = db.GetCollection<Vehicle>("v");
        v.Insert(new Car { Id = 1, Name = "a", Doors = 4 });
        Try("abstract base: Find(x => x.Id == 1).Count", () => v.Find(x => x.Id == 1).Count());
        Try("abstract base: DeleteMany(x => x.Id == 99)", () => v.DeleteMany(x => x.Id == 99));

        var e = db.GetCollection<Ev>("e");
        Console.WriteLine("local tz: " + TimeZoneInfo.Local.Id);
        Try("insert DST-gap Unspecified 2026-03-29 02:30", () => { e.Insert(new Ev { Id = 1, When = new DateTime(2026, 3, 29, 2, 30, 0) }); return e.FindById(1).When.ToUniversalTime().ToString("u"); });
        db.BeginTrans();
        e.Insert(new Ev { Id = 200, When = DateTime.UtcNow });
        try { e.Insert(new Ev { Id = 201, When = new DateTime(2026, 3, 29, 2, 30, 0) }); } catch { }
        try { e.Insert(new Ev { Id = 202, When = DateTime.UtcNow }); } catch { }
        Try("commit", () => db.Commit());
        Console.WriteLine("rows 200/201/202 present: " + (e.FindById(200) != null) + "/" + (e.FindById(201) != null) + "/" + (e.FindById(202) != null));

        db.GetCollection("people").Insert(new BsonDocument { ["_id"] = 1, ["Name"] = "alice" });
        Try("ctor(id,name) not assigning name: Name", () => "'" + db.GetCollection<Person>("people").FindById(1).Name + "'");
    }
}
