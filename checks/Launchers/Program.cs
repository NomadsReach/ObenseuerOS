using System;
using System.Collections.Generic;
using ObenseuerOS;

namespace ObenseuerOS
{
    internal sealed class OsApp { internal string Id, ModId, Title; internal bool Site, Desktop; internal Action OnOpen, BuildUi; }
}
internal static class Program
{
    private static void Check(bool ok) { if (!ok) throw new Exception("Launcher catalog check failed"); }
    private static void Main()
    {
        OsApp launcher = new OsApp { Id = "mod-tool", ModId = "mod", Title = "Tool", Desktop = true, OnOpen = delegate { } };
        OsApp hidden = new OsApp { Id = "hidden", ModId = "mod", Title = "Hidden", OnOpen = delegate { } };
        OsApp native = new OsApp { Id = "native", ModId = "mod", BuildUi = delegate { } };
        List<OsApp> apps = new List<OsApp> { launcher, hidden, native, new OsApp { Id = "built-in", ModId = "os", OnOpen = delegate { } }, new OsApp { Id = "site", ModId = "mod", Site = true, OnOpen = delegate { } }, new OsApp { Id = "html", ModId = "mod" } };
        List<OsApp> first = LauncherCatalog.Select(apps, "os", id => id != "hidden");
        Check(first.Count == 2 && object.ReferenceEquals(first[0], launcher) && object.ReferenceEquals(first[1], native));
        Check(object.ReferenceEquals(LauncherCatalog.Find(first, "MOD-TOOL"), launcher));
        Check(object.ReferenceEquals(LauncherCatalog.Find(first, "tool"), launcher));
        Check(object.ReferenceEquals(LauncherCatalog.Find(first, "NATIVE"), native));
        Check(LauncherCatalog.Find(first, "hidden") == null && LauncherCatalog.Find(first, "site") == null);
        OsApp sameTitle = new OsApp { Id = "other", ModId = "mod2", Title = "Tool", OnOpen = delegate { } };
        List<OsApp> ambiguous = new List<OsApp>(first) { sameTitle };
        Check(LauncherCatalog.Find(ambiguous, "tool") == null);
        Check(object.ReferenceEquals(LauncherCatalog.Find(ambiguous, "mod-tool"), launcher));
        Check(LauncherCatalog.Same(first, LauncherCatalog.Select(apps, "os", id => id != "hidden")));
        apps.Remove(launcher); Check(!LauncherCatalog.Same(first, LauncherCatalog.Select(apps, "os", id => true)));
        for (int mode = 0; mode < 8; mode++)
        {
            bool page = (mode & 1) != 0, callback = (mode & 2) != 0, ui = (mode & 4) != 0;
            bool one = mode == 1 || mode == 2 || mode == 4;
            Check(NativeAppRegistration.Valid(page, callback, ui, false) == one);
            Check(NativeAppRegistration.Valid(page, callback, ui, true) == (mode == 4));
        }
        WindowVisibility windows = new WindowVisibility();
        Check(!windows.Update(false, false));
        Check(windows.Update(true, false) && windows.Visible);
        Check(!windows.Update(true, true) && !windows.Update(false, true));
        Check(windows.Update(false, false) && !windows.Visible);
        Check(windows.Update(false, true) && windows.Visible);
        Check(windows.Update(false, false) && !windows.Visible);
        int delivered = 0;
        Action<string, string> handler = (name, json) => { Check(name == "hour" && json == "{\"hour\":2}"); delivered++; };
        Check(!ToolkitNativeEvents.Send(new[] { false, false }, open => open, handler, "hour", "{\"hour\":2}") && delivered == 0);
        Check(!ToolkitNativeEvents.Send(new[] { true }, open => open, null, "hour", "{}"));
        Check(ToolkitNativeEvents.Send(new[] { false, true, true }, open => open, handler, "hour", "{\"hour\":2}") && delivered == 1);
        try { ToolkitNativeEvents.Send(new[] { true }, open => open, (name, json) => { throw new InvalidOperationException("handler"); }, "hour", "{}"); throw new Exception("callback failure ignored"); }
        catch (InvalidOperationException e) { Check(e.Message == "handler"); }
        Console.WriteLine("Launcher catalog checks passed");
    }
}
