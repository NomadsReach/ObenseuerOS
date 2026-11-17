using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class LauncherCatalog
    {
        internal static List<OsApp> Select(IEnumerable<OsApp> apps, string osOwner, Func<string, bool> visible)
        {
            List<OsApp> result = new List<OsApp>();
            foreach (OsApp app in apps)
                if (app != null && app.ModId != osOwner && !app.Site && app.OnOpen != null && visible(app.Id)) result.Add(app);
            return result;
        }
        internal static bool Same(List<OsApp> first, List<OsApp> second)
        {
            if (first.Count != second.Count) return false;
            for (int i = 0; i < first.Count; i++) if (!object.ReferenceEquals(first[i], second[i])) return false;
            return true;
        }
    }
}
