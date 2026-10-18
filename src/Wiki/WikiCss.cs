using System;
using System.Collections.Generic;
using System.Text;

namespace ObenseuerOS
{
    internal static class WikiCss
    {
        internal const string Empty = "data:,";
        private static readonly HashSet<string> AtRules = new HashSet<string>(new[] { "counter-style", "font-face", "keyframes", "media", "page", "supports" }, StringComparer.Ordinal);
        private static readonly string[] Banned = { "http:", "https:", "//", "image(", "image-set(", "src(", "element(" };

        internal static string Render(string css, WikiAssets assets)
        {
            return Chunks(Urls((css ?? "").Replace("\u001B", ""), assets)).Replace("<", "\\3c ").Replace("&", "\\26 ");
        }

        internal static bool Remote(string text)
        {
            string flat = text.Replace("\t", "").Replace("\r", "").Replace("\n", "");
            foreach (string word in Banned)
            {
                if (flat.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static string Urls(string text, WikiAssets assets)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '"' || c == '\'')
                {
                    int end = StringEnd(text, i);
                    sb.Append(text, i, end - i);
                    i = end;
                }
                else if (c == '/' && At(text, i + 1) == '*')
                {
                    int close = text.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    sb.Append(' ');
                    i = close < 0 ? text.Length : close + 2;
                }
                else if (IsUrl(text, i))
                {
                    i = Url(text, i, assets, sb);
                }
                else
                {
                    sb.Append(c);
                    i++;
                }
            }
            return sb.ToString();
        }

        private static string Chunks(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            int start = 0;
            bool blocked = false;
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == '"' || c == '\'')
                {
                    i = StringEnd(text, i);
                    continue;
                }
                if (c == '\\' || (c == '@' && !AtRules.Contains(AtName(text, i + 1)))) blocked = true;
                if (c == ';' || c == '{' || c == '}')
                {
                    sb.Append(Keep(text.Substring(start, i - start), blocked)).Append(c);
                    start = i + 1;
                    blocked = false;
                }
                i++;
            }
            return sb.Append(Keep(text.Substring(start), blocked)).ToString();
        }

        private static string Keep(string chunk, bool blocked)
        {
            if (blocked || Remote(chunk) || Remote(Unescape(chunk))) return "";
            return chunk;
        }

        private static string AtName(string text, int start)
        {
            int end = start;
            while (end < text.Length && ((text[end] < 128 && char.IsLetterOrDigit(text[end])) || text[end] == '-')) end++;
            return text.Substring(start, end - start).ToLowerInvariant();
        }

        internal static string Unescape(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] != '\\')
                {
                    sb.Append(text[i++]);
                    continue;
                }
                int start = i + 1;
                int end = start;
                while (end < text.Length && end - start < 6 && Uri.IsHexDigit(text[end])) end++;
                if (end == start)
                {
                    int line = LineBreak(text, start);
                    if (line > start)
                    {
                        i = line;
                        continue;
                    }
                    if (start < text.Length) sb.Append(text[start]);
                    i = start + 1;
                    continue;
                }
                int code = Convert.ToInt32(text.Substring(start, end - start), 16);
                sb.Append(code == 0 || code > 0x10FFFF || (code >= 0xD800 && code <= 0xDFFF) ? "\uFFFD" : char.ConvertFromUtf32(code));
                if (At(text, end) == '\r' && At(text, end + 1) == '\n') end += 2;
                else if (end < text.Length && char.IsWhiteSpace(text[end])) end++;
                i = end;
            }
            return sb.ToString();
        }

        private static int StringEnd(string text, int open)
        {
            int i = open + 1;
            while (i < text.Length)
            {
                char c = text[i];
                if (c == text[open]) return i + 1;
                if (c == '\n' || c == '\r' || c == '\f') return i;
                i = c == '\\' ? Escape(text, i + 1) : i + 1;
            }
            return text.Length;
        }

        private static int Escape(string text, int j)
        {
            if (!Uri.IsHexDigit(At(text, j)))
            {
                int line = LineBreak(text, j);
                return line > j ? line : j + 1;
            }
            int end = j;
            while (end < text.Length && end - j < 6 && Uri.IsHexDigit(text[end])) end++;
            if (At(text, end) == ' ' || At(text, end) == '\t') return end + 1;
            return LineBreak(text, end);
        }

        private static int LineBreak(string text, int i)
        {
            if (At(text, i) == '\r' && At(text, i + 1) == '\n') return i + 2;
            return At(text, i) == '\n' || At(text, i) == '\r' || At(text, i) == '\f' ? i + 1 : i;
        }

        private static bool IsUrl(string text, int i)
        {
            return i + 4 <= text.Length && string.Compare(text, i, "url(", 0, 4, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static int Url(string text, int at, WikiAssets assets, StringBuilder sb)
        {
            string value;
            int end = Argument(text, at + 4, out value);
            if (end < 0)
            {
                int close = text.IndexOf(')', at + 4);
                sb.Append("url(").Append(Empty).Append(')');
                return close < 0 ? text.Length : close + 1;
            }
            sb.Append("url(").Append(Local(value, assets) ?? Empty).Append(')');
            return end + 1;
        }

        private static int Argument(string text, int j, out string value)
        {
            value = null;
            while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
            if (j >= text.Length) return -1;
            if (text[j] == '"' || text[j] == '\'')
            {
                int close = text.IndexOf(text[j], j + 1);
                if (close < 0) return -1;
                value = text.Substring(j + 1, close - j - 1);
                j = close + 1;
            }
            else
            {
                int start = j;
                while (j < text.Length && text[j] != ')' && text[j] != '"' && text[j] != '\'' && !char.IsWhiteSpace(text[j])) j++;
                value = text.Substring(start, j - start);
            }
            while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
            return j < text.Length && text[j] == ')' ? j : -1;
        }

        private static string Local(string url, WikiAssets assets)
        {
            string key = WikiHtml.ImageKey(url);
            return key == null ? null : assets.Local(key);
        }

        private static char At(string text, int i)
        {
            return i < text.Length ? text[i] : '\0';
        }
    }
}
