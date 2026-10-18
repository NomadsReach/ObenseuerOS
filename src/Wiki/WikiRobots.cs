using System;
using System.Collections.Generic;
using System.Globalization;

namespace ObenseuerOS
{
    internal static class WikiRobots
    {
        internal const string RobotsPath = "/robots.txt";
        internal const long FreshSeconds = 24 * 60 * 60;
        internal const int MinDelayMs = 1000;
        internal const int Cap = 256 * 1024;
        private const double MaxDelaySeconds = 3600;
        private static readonly char[] Space = { ' ', '\t', '\r' };
        private static Policy policy = Parse("");
        private static long fetched = -1;

        internal static int DelayMs
        {
            get
            {
                return policy.DelayMs;
            }
        }

        internal static bool Fresh(long now)
        {
            return fetched >= 0 && now >= fetched && now - fetched < FreshSeconds;
        }

        internal static void Set(string text, long now)
        {
            policy = Parse((text ?? "").TrimStart('\uFEFF'));
            fetched = now;
        }

        internal static bool Refuses(string url)
        {
            string path = url.Substring(WikiTitle.Origin.Length);
            return path != RobotsPath && Blocked(path);
        }

        internal static bool Blocked(string path)
        {
            Rule best = null;
            foreach (Rule rule in policy.Rules)
            {
                if (!rule.Matches(path)) continue;
                if (best == null || rule.Length > best.Length || (rule.Length == best.Length && rule.Allow)) best = rule;
            }
            return best != null && !best.Allow;
        }

        internal static Policy Parse(string text)
        {
            Policy result = new Policy();
            bool star = false;
            bool inAgents = false;
            foreach (string raw in text.Split('\n'))
            {
                int hash = raw.IndexOf('#');
                string line = (hash >= 0 ? raw.Substring(0, hash) : raw).Trim(Space);
                int colon = line.IndexOf(':');
                if (colon < 0) continue;
                string field = line.Substring(0, colon).Trim().ToLowerInvariant();
                string value = line.Substring(colon + 1).Trim();
                if (field == "user-agent")
                {
                    if (!inAgents) star = false;
                    if (value == "*") star = true;
                    inAgents = true;
                    continue;
                }
                inAgents = false;
                if (!star || value.Length == 0) continue;
                if (field == "disallow") result.Rules.Add(new Rule(value, false));
                else if (field == "allow") result.Rules.Add(new Rule(value, true));
                else if (field == "crawl-delay") result.DelayMs = Math.Max(result.DelayMs, Delay(value));
            }
            return result;
        }

        private static int Delay(string value)
        {
            double seconds;
            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out seconds) || !(seconds >= 0)) return 0;
            return (int)(Math.Min(MaxDelaySeconds, seconds) * 1000);
        }

        internal sealed class Policy
        {
            internal readonly List<Rule> Rules = new List<Rule>();
            internal int DelayMs = MinDelayMs;
        }

        internal sealed class Rule
        {
            private readonly string glob;
            internal readonly bool Allow;
            internal readonly int Length;

            internal Rule(string text, bool allow)
            {
                bool anchored = text.EndsWith("$", StringComparison.Ordinal);
                string pattern = anchored ? text.Substring(0, text.Length - 1) : text;
                glob = anchored ? pattern : pattern + "*";
                Allow = allow;
                Length = text.Length;
            }

            internal bool Matches(string path)
            {
                int p = 0;
                int t = 0;
                int star = -1;
                int mark = 0;
                while (t < path.Length)
                {
                    if (p < glob.Length && glob[p] == '*')
                    {
                        star = p++;
                        mark = t;
                    }
                    else if (p < glob.Length && glob[p] == path[t])
                    {
                        p++;
                        t++;
                    }
                    else if (star >= 0)
                    {
                        p = star + 1;
                        t = ++mark;
                    }
                    else return false;
                }
                while (p < glob.Length && glob[p] == '*') p++;
                return p == glob.Length;
            }
        }
    }
}
