using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal sealed class NewsPhotos
    {
        private static readonly Regex Item = new Regex("file:\\s*\"photos/(?<name>[a-z0-9-]+)\\.jpg\"[^\\r\\n]*?tags:\\s*\\[(?<tags>[^\\]]*)\\]", RegexOptions.CultureInvariant);
        private static readonly Regex Tag = new Regex("\"(?<name>[a-z0-9-]+)\"", RegexOptions.CultureInvariant);
        private readonly Dictionary<string, List<string>> tags = new Dictionary<string, List<string>>();

        internal NewsPhotos(string data)
        {
            foreach (Match item in Item.Matches(data ?? ""))
            {
                foreach (Match tag in Tag.Matches(item.Groups["tags"].Value))
                {
                    string name = tag.Groups["name"].Value;
                    List<string> files;
                    if (!tags.TryGetValue(name, out files)) { files = new List<string>(); tags.Add(name, files); }
                    files.Add(item.Groups["name"].Value);
                }
            }
        }

        internal string Pick(string section, int day, int index, bool thumbnail)
        {
            string tag = section == "Markets" ? "money" : section == "Sport" ? "football" : section == "Lottery" ? "lottery" : section == "Weather" ? "weather" : section == "Local" ? "street" : "scene";
            return PickTag(tag, unchecked(day * 7 + index), thumbnail);
        }

        internal string PickTag(string tag, int seed, bool thumbnail)
        {
            List<string> pool;
            if ((!tags.TryGetValue(tag, out pool) || pool.Count == 0) && (!tags.TryGetValue("scene", out pool) || pool.Count == 0)) return null;
            int at = (int)(Math.Abs((long)seed) % pool.Count);
            return pool[at] + (thumbnail ? ".thumb.jpg" : ".jpg");
        }
    }
}
