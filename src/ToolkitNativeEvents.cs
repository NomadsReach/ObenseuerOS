using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class ToolkitNativeEvents
    {
        internal static bool Send<T>(IEnumerable<T> hosts, Func<T, bool> eligible, Action<string, string> handler, string name, string json)
        {
            if (handler == null) return false;
            foreach (T host in hosts)
            {
                if (!eligible(host)) continue;
                handler(name, json);
                return true;
            }
            return false;
        }
    }
}
