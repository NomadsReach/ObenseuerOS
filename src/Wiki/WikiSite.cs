using System;
using System.Net;

namespace ObenseuerOS
{
    internal static class WikiSite
    {
        internal const string MissingName = "missing.html";
        internal const string MissingPath = "/" + MissingName;
        internal const string Csp = "<meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; form-action 'none'; base-uri 'none'\">";
        internal const string MissingHtml = "<!doctype html>\n<html lang=\"en\">\n<head>\n<meta charset=\"utf-8\">\n" + Csp + "\n<title>Not saved yet</title>\n</head>\n<body>\n<p>This page is not saved on this computer yet.</p>\n<p>Find it with the wiki search to save it.</p>\n</body>\n</html>\n";
        private const string PageFolder = "/pages/";
        private const string CreditStyle = "margin:1em 0 0;padding:.5em;border-top:1px solid #a2a9b1;background:#f8f9fa;color:#202122;font-size:small";
        private const string Licence = "Text from the Stalburg Wiki is available under the Creative Commons Attribution-ShareAlike 4.0 licence (CC BY-SA 4.0).";
        private const string NotAffiliated = "Not affiliated with or endorsed by the wiki. Images are not licensed with the text.";
        private const string LicenceUri = "https://creativecommons.org/licenses/by-sa/4.0/";
        private const string Modified = "Changed for offline reading: scripts removed and links made local.";
        private const string Creator = "By the Stalburg Wiki contributors.";
        private const int MaxFragment = 100;
        private static readonly string[] Folders = { "/images/", "/resources/", "/skins/", "/w/" };

        internal static string PagePath(string title)
        {
            return PageFolder + WikiCache.Hash(title) + ".html";
        }

        internal static bool ArticleLink(string href, out string title, out string fragment)
        {
            title = null;
            fragment = "";
            string v = href.Trim();
            if (v.StartsWith(WikiTitle.Origin + "/", StringComparison.Ordinal)) v = v.Substring(WikiTitle.Origin.Length);
            if (!v.StartsWith("/", StringComparison.Ordinal) || v.StartsWith("//", StringComparison.Ordinal)) return false;
            int hash = v.IndexOf('#');
            if (hash >= 0)
            {
                fragment = Fragment(v.Substring(hash));
                v = v.Substring(0, hash);
            }
            int query = v.IndexOf('?');
            if (query >= 0) v = v.Substring(0, query);
            if (v.Length < 2 || v.IndexOf(".php", StringComparison.OrdinalIgnoreCase) >= 0 || Folder(v)) return false;
            string normal;
            if (!WikiTitle.TryNormalise(Uri.UnescapeDataString(v.Substring(1)), out normal)) return false;
            if (normal.StartsWith("File:", StringComparison.Ordinal) || normal.StartsWith("Category:", StringComparison.Ordinal)) return false;
            title = normal;
            return true;
        }

        internal static string Fragment(string text)
        {
            if (text == null || text.Length < 1 || text.Length > MaxFragment || text[0] != '#') return "";
            foreach (char c in text)
            {
                if (!(c < 128 && char.IsLetterOrDigit(c)) && "_.:%()-'#".IndexOf(c) < 0) return "";
            }
            return text;
        }

        internal static string Credit(string title)
        {
            return "<footer style=\"" + CreditStyle + "\"><p>" + Licence + " " + WebUtility.HtmlEncode(LicenceUri) + "</p><p>" + NotAffiliated + "</p><p>" + WebUtility.HtmlEncode(Modified) + "</p><p>" + WebUtility.HtmlEncode(Creator) + "</p><p>Source: " + WebUtility.HtmlEncode(WikiTitle.Origin + "/" + WikiTitle.ArticlePath(title)) + "</p></footer>";
        }

        private static bool Folder(string path)
        {
            foreach (string folder in Folders)
            {
                if (path.StartsWith(folder, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
