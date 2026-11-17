using System;
using System.Collections.Generic;
using ObenseuerOS;

namespace ObenseuerOS
{
    internal sealed class OsApp { internal string Id, ModId, Title; internal bool Site, Desktop; internal Action OnOpen; }
}
internal static class Program
{
    private static void Check(bool ok) { if (!ok) throw new Exception("Launcher catalog check failed"); }
    private static void Main()
    {
        OsApp launcher = new OsApp { Id = "mod-tool", ModId = "mod", Title = "Tool", Desktop = true, OnOpen = delegate { } };
        OsApp hidden = new OsApp { Id = "hidden", ModId = "mod", Title = "Hidden", OnOpen = delegate { } };
        List<OsApp> apps = new List<OsApp> { launcher, hidden, new OsApp { Id = "built-in", ModId = "os", OnOpen = delegate { } }, new OsApp { Id = "site", ModId = "mod", Site = true, OnOpen = delegate { } }, new OsApp { Id = "html", ModId = "mod" } };
        List<OsApp> first = LauncherCatalog.Select(apps, "os", id => id != "hidden");
        Check(first.Count == 1 && object.ReferenceEquals(first[0], launcher));
        Check(LauncherCatalog.Same(first, LauncherCatalog.Select(apps, "os", id => id != "hidden")));
        apps.Remove(launcher); Check(!LauncherCatalog.Same(first, LauncherCatalog.Select(apps, "os", id => true)));
        Console.WriteLine("Launcher catalog checks passed");
    }
}
