foreach (var n in new[] { 250, 254, 255, 256 })
{
    try { using var m = new Mutex(false, "Global\\" + new string('a', n)); Console.WriteLine($"{n}: ok"); }
    catch (Exception ex) { Console.WriteLine($"{n}: {ex.GetType().Name} {ex.Message}"); }
}
