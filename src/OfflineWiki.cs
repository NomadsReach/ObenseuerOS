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
        internal string SourceFolder;
        internal readonly List<string> Links = new List<string>();
        internal readonly List<string> Images = new List<string>();
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
        private static readonly Regex Images = new Regex(@"<img\b[^>]*?\bsrc\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Breaks = new Regex(@"</?(p|div|br|li|h[1-6]|tr|hr)\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Tags = new Regex(@"<[^>]*>", RegexOptions.Singleline | RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Spaces = new Regex(@"[ \t\f\v]+", RegexOptions.CultureInvariant, MatchTimeout);
        private static readonly Regex Newlines = new Regex(@"\n[ \t]*\n(?:[ \t]*\n)+", RegexOptions.CultureInvariant, MatchTimeout);

        internal static List<OfflineWikiPage> Load(string folder, out bool limited)
        {
            return Load(new[] { folder }, out limited);
        }
        internal static List<OfflineWikiPage> Load(string[] folders, out bool limited)
        {
            limited = false; long total = 0;
            List<OfflineWikiPage> pages = new List<OfflineWikiPage>();
            List<string> files = new List<string>();
            if (folders == null) return pages;
            foreach (string folder in folders)
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) continue;
                List<string> source = new List<string>();
                try
                {
                    foreach (string file in Directory.EnumerateFiles(folder, "*.page", SearchOption.TopDirectoryOnly))
                    {
                        if (files.Count + source.Count >= MaxFiles) { limited = true; break; }
                        source.Add(file);
                    }
                }
                catch (IOException) { limited = true; }
                catch (UnauthorizedAccessException) { limited = true; }
                source.Sort(StringComparer.Ordinal); files.AddRange(source);
                if (files.Count >= MaxFiles) break;
            }
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
                    if (page != null && seen.Add(page.Title)) { page.SourceFolder = Path.GetDirectoryName(file); pages.Add(page); }
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
            HashSet<string> images = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match match in Images.Matches(visible))
            {
                string src = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
                string key = ImageKey(src);
                if (key != null && images.Add(key)) page.Images.Add(key);
                if (page.Images.Count >= 64) break;
            }
            string text = Breaks.Replace(visible, "\n"); text = Tags.Replace(text, " ");
            text = WebUtility.HtmlDecode(text).Replace("\r", ""); text = Spaces.Replace(text, " "); text = Newlines.Replace(text, "\n\n").Trim();
            page.Text = text;
            return page;
        }
        internal static string ImageKey(string src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            string path = WebUtility.HtmlDecode(src);
            if (path.StartsWith(WikiTitle.Origin + "/", StringComparison.Ordinal)) path = path.Substring(WikiTitle.Origin.Length);
            int query = path.IndexOf('?'); if (query >= 0) path = path.Substring(0, query);
            return WikiTitle.IsImagePath(path) ? path : null;
        }
        internal static byte[] ReadImage(string pagesFolder, string key)
        {
            if (string.IsNullOrEmpty(pagesFolder) || ImageKey(key) != key) return null;
            string root = Path.GetDirectoryName(pagesFolder); if (string.IsNullOrEmpty(root)) return null;
            string name = WikiImage.Hash(key);
            foreach (string ext in new[] { ".png", ".jpg" })
            {
                string path = Path.Combine(root, "img", name + ext);
                try
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        if (stream.Length < 1 || stream.Length > 2 * 1024 * 1024) continue;
                        byte[] data = new byte[(int)stream.Length]; int offset = 0;
                        while (offset < data.Length) { int n = stream.Read(data, offset, data.Length - offset); if (n == 0) return null; offset += n; }
                        bool format = ext == ".png" ? WikiImage.At(data, 0, "\u0089PNG\r\n\u001a\n") : data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
                        if (format && WikiImage.Fits(data, 2048)) return data;
                    }
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            return null;
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
