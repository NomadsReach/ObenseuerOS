using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal sealed class OfflineWikiPage
    {
        internal string Title;
        internal string Text;
        internal readonly List<string> Links = new List<string>();
    }

    internal static class OfflineWiki
    {
        private const long MaxBytes = 9L * 1024 * 1024;
        private const long MaxTotalBytes = 32L * 1024 * 1024;
        private const int MaxPages = 500;
        private const int MaxFiles = 2000;
        private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(2);
        private static readonly Regex Hidden = new Regex(@"<!--.*?-->|<(script|style|template|noscript)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Anchors = new Regex(@"<a\b[^>]*?\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Breaks = new Regex(@"</?(p|div|br|li|h[1-6]|tr|hr)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Tags = new Regex(@"<[^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Spaces = new Regex(@"[ \t\f\v]+", RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Newlines = new Regex(@"\n[ \t]*\n(?:[ \t]*\n)+", RegexOptions.CultureInvariant, MatchTimeout);

        internal static List<OfflineWikiPage> Load(string folder, out bool limited)
        {
            limited = false; long total = 0;
            List<OfflineWikiPage> pages = new List<OfflineWikiPage>();
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return pages;
            List<string> files = new List<string>();
            try
            {
                foreach (string file in Directory.EnumerateFiles(folder, "*.page", SearchOption.TopDirectoryOnly))
                {
                    if (files.Count >= MaxFiles) { limited = true; break; }
                    files.Add(file);
                }
            }
            catch (IOException) { limited = true; return pages; }
            catch (UnauthorizedAccessException) { limited = true; return pages; }
            files.Sort(StringComparer.Ordinal);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                try
                {
                    if (pages.Count >= MaxPages) { limited = true; break; }
                    long size = new FileInfo(file).Length;
                    if (size > MaxBytes || size > MaxTotalBytes - total) { limited = true; continue; }
                    total += size;
                    OfflineWikiPage page = Parse(File.ReadAllText(file, Encoding.UTF8));
                    if (page != null && seen.Add(page.Title)) pages.Add(page);
                }
                catch (IOException) { limited = true; }
                catch (UnauthorizedAccessException) { limited = true; }
                catch (RegexMatchTimeoutException) { limited = true; }
            }
            pages.Sort(delegate(OfflineWikiPage a, OfflineWikiPage b) { return StringComparer.OrdinalIgnoreCase.Compare(a.Title, b.Title); });
            return pages;
        }
        internal static OfflineWikiPage Parse(string raw)
        {
            if (raw == null) return null;
            string normalized = raw.Replace("\r\n", "\n");
            int split = normalized.IndexOf("\n\n", StringComparison.Ordinal);
            if (split < 0) return null;
            string[] headers = normalized.Substring(0, split).Split('\n');
            if (headers.Length != 4 || headers[0] != "OBXWIKI1" || !headers[1].StartsWith("title:", StringComparison.Ordinal) || !headers[2].StartsWith("rev:", StringComparison.Ordinal) || !headers[3].StartsWith("fetched:", StringComparison.Ordinal)) return null;
            string title = headers[1].Substring(6).Trim(); long revision, fetched;
            if (title.Length == 0 || title.Length > 255 || !long.TryParse(headers[2].Substring(4), out revision) || revision < 0 || !long.TryParse(headers[3].Substring(8), out fetched) || fetched < 0) return null;
            string html = normalized.Substring(split + 2);
            string visible = Hidden.Replace(html, " ");
            OfflineWikiPage page = new OfflineWikiPage { Title = title.Replace('_', ' ') };
            HashSet<string> links = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Anchors.Matches(visible))
            {
                string href = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                string target = LinkTitle(href);
                if (target != null && links.Add(target)) page.Links.Add(target);
                if (page.Links.Count >= 500) break;
            }
            string text = Breaks.Replace(visible, "\n"); text = Tags.Replace(text, " ");
            text = WebUtility.HtmlDecode(text).Replace("\r", ""); text = Spaces.Replace(text, " "); text = Newlines.Replace(text, "\n\n").Trim();
            page.Text = text;
            return page;
        }
        internal static string LinkTitle(string href)
        {
            if (string.IsNullOrEmpty(href) || href.Length > 1024) return null;
            href = WebUtility.HtmlDecode(href);
            if (!href.StartsWith("/", StringComparison.Ordinal) || href.StartsWith("//", StringComparison.Ordinal) || href.IndexOf('?') >= 0) return null;
            int hash = href.IndexOf('#'); if (hash >= 0) href = href.Substring(0, hash);
            if (href.Length <= 1) return null;
            try
            {
                string title;
                if (!WikiTitle.TryNormalise(Uri.UnescapeDataString(href.Substring(1)), out title)) return null;
                return title.Replace('_', ' ');
            }
            catch (UriFormatException) { return null; }
        }
    }
}
