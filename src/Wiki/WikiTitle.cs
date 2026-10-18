using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal static class WikiTitle
    {
        internal const string Host = "stalburg.net";
        internal const string Origin = "https://" + Host;
        private const string Common = "&format=json&formatversion=2&maxlag=5";
        private const int MaxBytes = 255;
        private const int MaxInput = 300;
        private const int MaxResults = 10;
        private const string Forbidden = "|#<>[]{}";
        private static readonly string BidiControls = new string(new[] { (char)0x200E, (char)0x200F, (char)0x202A, (char)0x202B, (char)0x202C, (char)0x202D, (char)0x202E, (char)0x2066, (char)0x2067, (char)0x2068, (char)0x2069 });
        private static readonly string UnicodeSpaces = new string(new[] { ' ', (char)0x00A0, (char)0x1680, (char)0x2000, (char)0x2001, (char)0x2002, (char)0x2003, (char)0x2004, (char)0x2005, (char)0x2006, (char)0x2007, (char)0x2008, (char)0x2009, (char)0x200A, (char)0x2028, (char)0x2029, (char)0x202F, (char)0x205F, (char)0x3000 });
        private const string Segment = @"(?:(?!%(?:2[eEfF]|5[cC]))[^/?#\\\x00-\x1f\x7f])+";
        private static readonly string[] Denylist = { "Special", "Media", "MediaWiki", "User", "Talk", "Template", "Module", "Help" };
        private static readonly Regex ImagePattern = new Regex(@"^/images/(thumb/)?[0-9a-f]/[0-9a-f]{2}/" + Segment + @"(/[0-9]{1,4}px-" + Segment + @")?\z", RegexOptions.CultureInvariant);
        private static readonly Regex AssetPattern = new Regex(@"^/resources/assets/" + Segment + @"\.(png|jpg|gif|webp)\z", RegexOptions.CultureInvariant);
        private static readonly Regex LoadPattern = new Regex(@"^/load\.php\?[A-Za-z0-9_.~%&=,;:+*()!'\-]{1,1000}\z", RegexOptions.CultureInvariant);

        internal static bool TryNormalise(string raw, out string title)
        {
            title = null;
            if (!Short(raw)) return false;
            string text = Spaces(raw).Trim(' ', '_');
            string prefix = "";
            int colon = text.IndexOf(':');
            if (colon >= 0)
            {
                string space = text.Substring(0, colon).Replace('_', ' ').Trim();
                if (string.Equals(space, "File", StringComparison.OrdinalIgnoreCase)) prefix = "File:";
                else if (string.Equals(space, "Category", StringComparison.OrdinalIgnoreCase)) prefix = "Category:";
                else if (colon == 0 || Denied(space)) return false;
                if (prefix.Length > 0) text = text.Substring(colon + 1);
            }
            string body = Regex.Replace(text.Replace(' ', '_'), "_+", "_").Trim('_');
            if (body.Length == 0) return false;
            body = char.ToUpperInvariant(body[0]) + body.Substring(1);
            foreach (char c in body)
            {
                if (char.IsControl(c) || Forbidden.IndexOf(c) >= 0 || BidiControls.IndexOf(c) >= 0 || (c == ':' && prefix.Length > 0)) return false;
            }
            foreach (string part in body.Split('/'))
            {
                if (part.Length == 0 || part == "." || part == "..") return false;
            }
            string full = prefix + body;
            if (Encoding.UTF8.GetByteCount(full) > MaxBytes) return false;
            title = full;
            return true;
        }

        internal static string Article(string title)
        {
            string t;
            return TryNormalise(title, out t) ? Origin + "/" + ArticlePath(t) : null;
        }

        internal static string Search(string query, int offset, int limit)
        {
            if (string.IsNullOrWhiteSpace(query) || !Short(query)) return null;
            return Origin + "/api.php?action=query&list=search&srsearch=" + Uri.EscapeDataString(query) + "&srnamespace=0&srlimit=" + Math.Min(MaxResults, Math.Max(1, limit)) + "&sroffset=" + Math.Max(0, offset) + "&srprop=snippet%7Csize%7Cwordcount" + Common;
        }

        internal static string TitleIndex(string continueFrom)
        {
            if (!string.IsNullOrEmpty(continueFrom) && !Short(continueFrom)) return null;
            string more = string.IsNullOrEmpty(continueFrom) ? "" : "&apcontinue=" + Uri.EscapeDataString(continueFrom);
            return Origin + "/api.php?action=query&list=allpages&apnamespace=0&apfilterredir=nonredirects&aplimit=500" + more + Common;
        }

        internal static bool IsImagePath(string path)
        {
            if (path == null || path.Length > MaxInput || !WellFormed(path) || !(ImagePattern.IsMatch(path) || AssetPattern.IsMatch(path))) return false;
            foreach (string part in path.Split('/'))
            {
                if (part == "." || part == "..") return false;
            }
            return true;
        }

        internal static string Image(string path)
        {
            return IsImagePath(path) ? Origin + path : null;
        }

        internal static bool Allowed(string url)
        {
            if (url == null || !url.StartsWith(Origin + "/", StringComparison.Ordinal) || !Plain(url)) return false;
            string rest = url.Substring(Origin.Length);
            return rest == "/robots.txt" || IsImagePath(rest) || Api(rest) || LoadPattern.IsMatch(rest) || Canonical(rest);
        }

        internal static string ArticlePath(string title)
        {
            List<string> parts = new List<string>();
            foreach (string part in title.Split('/')) parts.Add(Uri.EscapeDataString(part));
            return string.Join("/", parts.ToArray());
        }

        private static bool Api(string rest)
        {
            return rest.StartsWith("/api.php?", StringComparison.Ordinal) && rest.IndexOf('#') < 0;
        }

        private static bool Canonical(string rest)
        {
            if (rest.Length < 2 || rest.IndexOf('?') >= 0 || rest.IndexOf('#') >= 0) return false;
            string title = Uri.UnescapeDataString(rest.Substring(1));
            string normal;
            return TryNormalise(title, out normal) && normal == title && ArticlePath(title) == rest.Substring(1);
        }

        private static string Spaces(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            foreach (char c in text) sb.Append(UnicodeSpaces.IndexOf(c) >= 0 ? ' ' : c);
            return sb.ToString();
        }

        private static bool Denied(string space)
        {
            foreach (string name in Denylist)
            {
                if (string.Equals(space, name, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return space.EndsWith(" talk", StringComparison.OrdinalIgnoreCase);
        }

        private static bool Plain(string text)
        {
            foreach (char c in text)
            {
                if (c <= ' ' || c > '~' || c == '\\') return false;
            }
            return true;
        }

        private static bool Short(string text)
        {
            return text != null && text.Length <= MaxInput && WellFormed(text);
        }

        private static bool WellFormed(string text)
        {
            try
            {
                new UTF8Encoding(false, true).GetByteCount(text);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }
    }
}
