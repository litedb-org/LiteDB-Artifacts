using System; using System.Threading;
int max = 0;
for (var n = 1; n < 400; n++)
{
    var name = "Global\\" + new string('a', n);
    try { using var m = new Mutex(false, name); max = name.Length; } catch (Exception ex) { Console.WriteLine($"{Environment.Version}: first failure at total length {name.Length}: {ex.GetType().Name}"); break; }
}
Console.WriteLine($"{Environment.Version}: max total name length (incl. Global\\) = {max}");
