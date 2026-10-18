using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal static class OsScope
    {
        internal const int MaxModFiles = 64;
        internal const int MaxModBytes = 1024 * 1024;
        internal const int MaxDelayHours = 720;
        internal const int MaxModIdLength = 64;
        internal const int MaxAppsPerMod = 8;
        internal const int MaxSitesPerMod = 4;
        internal const int MaxMethodsPerApp = 64;
        internal const int MaxArgBytes = 12 * 1024;
        internal const int MaxIconBytes = 512 * 1024;
        internal const int MaxSiteHost = 60;
        internal const int MaxNotices = 20;
        internal const int CallsPerSecond = 30;
        internal const int EmitsPerSecond = 30;
        internal const int NoticesPer10Seconds = 3;
        private const string Prefix = "mod-";
        private static readonly Regex ModName = new Regex(@"\A[a-z0-9._-]{1,64}\z");
        private static readonly Regex SiteHost = new Regex(@"\A([a-z0-9-]+\.)+(ts|com)\z");
        private static readonly Regex Spaces = new Regex(@" {2,}");
        private static readonly string[] BuiltInHosts = { "stalburg.ts", "mail.stalburg.ts", "mail.stalburg.com", "stalburgbank.ts", "stalburgtimes.com", "stalex.ts", "jobs.stalburg.ts", "shop.stalburg.ts", "bets.stalburg.ts", "lottery.stalburg.ts", "wiki.stalburg.ts", "eyebook.com", "pcmaster.ts", "turvapro.ts", "free-rm.ts", "kolhola-warez.ts", "voittaja.ts" };
        private static readonly string[] ModCalls = { "os.time.now", "os.account.name", "os.mod.read", "os.mod.write", "os.mod.open" };
        private static readonly string[] IconTypes = { ".svg", ".png", ".jpg", ".webp" };
        private static readonly HashSet<string> ReservedAppIds = new HashSet<string>
        {
            "os", "bank", "bets", "browser", "chat", "eyebook", "exchange", "files", "freerm", "jobs", "lotto", "mail",
            "minesweeper", "news", "notes", "pcguard", "pcmaster", "recycle", "settings", "shop", "solitaire", "terminal",
            "turvapro", "turvasite", "virusfx", "voittaja", "warez", "wiki",
        };
        private static readonly string[] BuiltInTitles = { "Stalburg Mail", "Stalburg Web", "Explorer", "Recycle Bin", "Control Panel", "Stal-Ex", "Command Prompt", "Notepad", "Stalburg Bank", "Stalburg Times", "TOR-Turnip Chat v0.3.9", "Shop", "Minesweeper", "Solitaire", "PC Master Guard", "TurvaPro 2000" };

        internal static bool ValidModId(string modId)
        {
            if (string.IsNullOrEmpty(modId) || modId.Length > MaxModIdLength) return false;
            if (modId[0] == '.' || modId[modId.Length - 1] == '.' || modId.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            foreach (char c in modId)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
                if (!ok) return false;
            }
            return true;
        }

        internal static string Folder(string modId)
        {
            return ValidModId(modId) ? "/" + Prefix + modId.Replace('.', ' ') : null;
        }

        internal static string Resolve(string modId, string name)
        {
            string folder = Folder(modId);
            if (folder == null || string.IsNullOrEmpty(name) || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0) return null;
            string path = folder + "/" + name;
            return OsPaths.ValidPath(path) ? path : null;
        }

        internal static string OwnerOf(string path)
        {
            if (!OsPaths.ValidPath(path) || !path.StartsWith("/" + Prefix, StringComparison.Ordinal)) return null;
            string id = path.Substring(1 + Prefix.Length, path.IndexOf('/', 1) - 1 - Prefix.Length).Replace(' ', '.');
            return Folder(id) == path.Substring(0, path.IndexOf('/', 1)) ? id : null;
        }

        internal static string FolderOf(string path)
        {
            string owner = OwnerOf(path);
            return owner == null ? null : Folder(owner);
        }

        internal static bool Fits(int files, int bytes, bool exists, int oldBytes, int addBytes)
        {
            return (exists || files < MaxModFiles) && bytes - oldBytes + addBytes <= MaxModBytes;
        }

        internal static bool InsideMods(string probe, string plugins, string own)
        {
            if (!probe.StartsWith(plugins, StringComparison.OrdinalIgnoreCase) || probe.Equals(plugins, StringComparison.OrdinalIgnoreCase)) return false;
            return string.Equals(own, plugins, StringComparison.OrdinalIgnoreCase) || !probe.StartsWith(own, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool RoomFor(List<string> folders, int max, string[] evictOrder, out int evict)
        {
            evict = -1;
            if (folders.Count < max) return true;
            foreach (string folder in evictOrder)
            {
                evict = folders.IndexOf(folder);
                if (evict >= 0) return true;
            }
            return false;
        }

        internal static bool ValidDelay(int hours)
        {
            return hours >= 0 && hours <= MaxDelayHours;
        }

        internal static bool ValidName(string name)
        {
            return name != null && ModName.IsMatch(name) && !name.StartsWith("os.", StringComparison.Ordinal);
        }

        internal static bool MayTouch(string owner, string modId)
        {
            return owner != null && owner == modId;
        }

        internal static bool IsOsMethod(string method)
        {
            return method != null && method.StartsWith("os.", StringComparison.Ordinal);
        }

        internal static bool ModMayCall(string method)
        {
            return Array.IndexOf(ModCalls, method) >= 0;
        }

        internal static bool IsReservedAppId(string appId)
        {
            return ReservedAppIds.Contains(appId);
        }

        internal static bool IsBuiltInTitle(string title)
        {
            string clean = CleanTitle(title);
            return Array.Exists(BuiltInTitles, x => string.Equals(x, clean, StringComparison.OrdinalIgnoreCase));
        }

        internal static string CleanTitle(string title)
        {
            if (title == null) return null;
            StringBuilder text = new StringBuilder(title.Length);
            foreach (char c in title)
            {
                if (char.IsWhiteSpace(c)) text.Append(' ');
                else if (!IsHidden(c)) text.Append(c);
            }
            return Spaces.Replace(text.ToString(), " ").Trim();
        }

        private static bool IsHidden(char c)
        {
            UnicodeCategory category = char.GetUnicodeCategory(c);
            return c == '\u00AD' || category == UnicodeCategory.Format || category == UnicodeCategory.Control;
        }

        internal static string PlainHost(string host)
        {
            return host.StartsWith("www.", StringComparison.Ordinal) ? host.Substring(4) : host;
        }

        internal static bool ValidSiteHost(string host)
        {
            if (host == null || host.Length > MaxSiteHost || !SiteHost.IsMatch(host)) return false;
            string plain = PlainHost(host);
            if (plain == "stalburg.ts" || plain.EndsWith(".stalburg.ts", StringComparison.Ordinal)) return false;
            return Array.IndexOf(BuiltInHosts, plain) < 0;
        }

        internal static bool ValidRelative(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > 128 || path[0] == '/' || path[0] == '\\') return false;
            if (path.IndexOfAny(new[] { ':', '\\', '%' }) >= 0 || path.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            return PlainText(path, 1, 128);
        }

        internal static bool IsIconFile(string name)
        {
            if (!ValidRelative(name)) return false;
            string lower = name.ToLowerInvariant();
            foreach (string type in IconTypes)
            {
                if (lower.EndsWith(type, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        internal static bool ValidSiteText(string description, string[] keywords)
        {
            if (description != null && !PlainText(description, 0, 120)) return false;
            if (keywords == null) return true;
            if (keywords.Length > 12) return false;
            foreach (string word in keywords)
            {
                if (!PlainText(word, 1, 24)) return false;
            }
            return true;
        }

        internal static bool PlainText(string text, int min, int max)
        {
            if (text == null || text.Length < min || text.Length > max) return false;
            foreach (char c in text)
            {
                if (char.IsControl(c)) return false;
            }
            return true;
        }

        internal static bool FitsArgs(string text)
        {
            return text != null && Encoding.UTF8.GetByteCount(text) <= MaxArgBytes;
        }

        internal static string Label(string appId)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(appId));
                StringBuilder label = new StringBuilder("x");
                for (int i = 0; i < 8; i++) label.Append(hash[i].ToString("x2"));
                return label.ToString();
            }
        }

        internal static long NowMs()
        {
            return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        }

        internal static bool Allow(Dictionary<string, RateLimit> limits, string key, int max, long spanMs, long now)
        {
            RateLimit limit;
            if (!limits.TryGetValue(key, out limit))
            {
                limit = new RateLimit(max, spanMs);
                limits.Add(key, limit);
            }
            return limit.Take(now);
        }
    }

    internal sealed class RateLimit
    {
        private readonly int max;
        private readonly long span;
        private readonly Queue<long> stamps = new Queue<long>();

        internal RateLimit(int max, long span)
        {
            this.max = max;
            this.span = span;
        }

        internal bool Take(long now)
        {
            while (stamps.Count > 0 && now - stamps.Peek() >= span) stamps.Dequeue();
            if (stamps.Count >= max) return false;
            stamps.Enqueue(now);
            return true;
        }
    }

    internal sealed class OsNotice
    {
        internal readonly string ModId;
        internal readonly string From;
        internal readonly string Title;
        internal readonly string Body;

        internal OsNotice(string modId, string from, string title, string body)
        {
            ModId = modId;
            From = from;
            Title = title;
            Body = body;
        }
    }

    internal sealed class NoticeQueue
    {
        private readonly List<OsNotice> items = new List<OsNotice>();

        internal int Count
        {
            get
            {
                return items.Count;
            }
        }

        internal bool Add(OsNotice notice)
        {
            if (items.Count >= OsScope.MaxNotices) return false;
            items.Add(notice);
            return true;
        }

        internal List<OsNotice> Take()
        {
            List<OsNotice> all = new List<OsNotice>(items);
            items.Clear();
            return all;
        }

        internal void RemoveMod(string modId)
        {
            items.RemoveAll(x => x.ModId == modId);
        }

        internal void Clear()
        {
            items.Clear();
        }
    }
}
