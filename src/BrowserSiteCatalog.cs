using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class BrowserSiteCatalog
    {
        internal static OsApp Find(IEnumerable<OsApp> apps, string host)
        {
            string name = Plain(host);
            foreach (OsApp app in apps)
                if (app != null && app.Site && name.Length > 0 && string.Equals(name, Plain(app.Host), StringComparison.OrdinalIgnoreCase)) return app;
            return null;
        }

        internal static bool Matches(OsApp app, string query)
        {
            if (app == null || !app.Site) return false;
            if (string.IsNullOrEmpty(query)) return true;
            if (Contains(app.Host, query) || Contains(app.Title, query) || Contains(app.Description, query)) return true;
            if (app.Keywords != null) foreach (string keyword in app.Keywords) if (Contains(keyword, query)) return true;
            return false;
        }

        private static string Plain(string host)
        {
            if (string.IsNullOrEmpty(host)) return "";
            return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host.Substring(4) : host;
        }

        private static bool Contains(string text, string query)
        {
            return text != null && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
