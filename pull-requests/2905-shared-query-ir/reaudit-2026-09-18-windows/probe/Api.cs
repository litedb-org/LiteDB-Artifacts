using System;
using System.Linq;
using System.Reflection;
using LiteDB;

internal static class Api
{
    public static int Run()
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var lines = typeof(LiteDatabase).Assembly.GetTypes()
            .Where(t => t.IsVisible)
            .SelectMany(t => t.GetMembers(flags)
                .Where(m => m is MethodBase mb ? (mb.IsPublic || mb.IsFamily || mb.IsFamilyOrAssembly)
                          : m is FieldInfo f ? (f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly)
                          : m is Type nt ? nt.IsVisible
                          : m is PropertyInfo || m is EventInfo)
                .Where(m => !(m is PropertyInfo p) || (p.GetAccessors(true).Any(a => a.IsPublic || a.IsFamily || a.IsFamilyOrAssembly)))
                .Select(m => t.FullName + " :: " + m.MemberType + " " + m.ToString()))
            .Concat(typeof(LiteDatabase).Assembly.GetTypes().Where(t => t.IsVisible).Select(t => "TYPE " + t.FullName + " : " + t.BaseType?.FullName + " [" + string.Join(",", t.GetInterfaces().Select(i => i.FullName).OrderBy(x => x)) + "]"))
            .Distinct().OrderBy(x => x, StringComparer.Ordinal);
        foreach (var line in lines) Console.WriteLine(line);
        return 0;
    }
}
