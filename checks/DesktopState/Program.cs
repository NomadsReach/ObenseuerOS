using System;
using System.Collections.Generic;
using ObenseuerOS;

namespace ObenseuerCore
{
    internal static class PlayerState { internal static string FirstName, LastName; }
}

internal static class Program
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static void Main()
    {
        ObenseuerCore.PlayerState.FirstName = "Ada"; ObenseuerCore.PlayerState.LastName = "Lovelace";
        Check(OsDesktopState.UserName() == "Ada Lovelace", "player name");
        ObenseuerCore.PlayerState.FirstName = "Grace"; ObenseuerCore.PlayerState.LastName = null;
        Check(OsDesktopState.UserName() == "Grace", "name reflects current player");
        for (int i = 0; i < OsScope.MaxNotices; i++)
            Check(OsDesktopState.AddNotice(new OsNotice(i == 1 ? "other" : "owner", "from", i.ToString(), "body")), "queue capacity");
        Check(!OsDesktopState.AddNotice(new OsNotice("owner", "from", "overflow", "")), "queue rejects overflow");
        OsDesktopState.Reset(false);
        OsDesktopState.ClearNotices("other");
        List<OsNotice> saved = OsDesktopState.TakeNotices();
        Check(saved.Count == OsScope.MaxNotices - 1 && saved[0].Title == "0" && saved[1].Title == "2", "remove owner keeps order across scene reset");
        Check(OsDesktopState.TakeNotices().Count == 0, "take drains queue");
        Check(OsDesktopState.AddNotice(new OsNotice("owner", "from", "reset", "")), "queue after drain");
        OsDesktopState.Reset(true);
        Check(OsDesktopState.TakeNotices().Count == 0, "main menu reset clears queue");
        Console.WriteLine("Desktop state checks passed.");
    }
}
