using System;
using System.Net;

namespace ObenseuerOS
{
    internal static class EyeRoute
    {
        internal static void Resolve(string url, out string view, out string query)
        {
            view = "home"; query = "";
            if (string.IsNullOrEmpty(url) || url.Length > 4096) return;
            int fragment = url.IndexOf('#'); if (fragment >= 0) url = url.Substring(0, fragment);
            int question = url.IndexOf('?');
            string path = question < 0 ? url : url.Substring(0, question);
            switch (path)
            {
                case "/profile": case "/friends": case "/postal": case "/notifications":
                case "/saved": case "/photos": case "/pages": view = path.Substring(1); break;
                case "/search":
                    if (question < 0) break;
                    foreach (string part in url.Substring(question + 1).Split('&'))
                    {
                        int equals = part.IndexOf('=');
                        string key = WebUtility.UrlDecode(equals < 0 ? part : part.Substring(0, equals));
                        if (key != "q") continue;
                        query = WebUtility.UrlDecode(equals < 0 ? "" : part.Substring(equals + 1)) ?? "";
                        break;
                    }
                    break;
            }
        }
    }
}
