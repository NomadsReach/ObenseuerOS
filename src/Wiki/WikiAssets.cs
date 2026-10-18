using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class WikiAssets
    {
        internal const int MaxWanted = 200;
        private readonly WikiCache cache;
        private readonly long now;
        private readonly Dictionary<string, string> local = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> wanted = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> saved = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly string current;

        internal WikiAssets(WikiCache cache, long now, string current = null)
        {
            this.cache = cache;
            this.now = now;
            this.current = current;
        }

        internal HashSet<string> Wanted
        {
            get
            {
                return wanted;
            }
        }

        internal string Title
        {
            get
            {
                return current;
            }
        }

        internal bool Saved(string title)
        {
            bool known;
            if (saved.TryGetValue(title, out known)) return known;
            known = title == current || cache.PageFile(title) != null;
            saved[title] = known;
            return known;
        }

        internal string Local(string key)
        {
            string path;
            if (local.TryGetValue(key, out path)) return path;
            string rel = key.StartsWith(WikiTitle.Origin + "/load.php?", StringComparison.Ordinal) ? cache.CssFile(key, now) : cache.ImageFile(key, now);
            if (rel == null)
            {
                if (wanted.Count < MaxWanted) wanted.Add(key);
                local[key] = null;
                return null;
            }
            local[key] = "/" + rel;
            return local[key];
        }
    }
}
