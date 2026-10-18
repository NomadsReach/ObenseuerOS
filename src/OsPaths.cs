using System;

namespace ObenseuerOS
{
    internal static class OsPaths
    {
        internal const int MaxPathLength = 128;

        internal static bool ValidPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length > MaxPathLength || path[0] != '/') return false;
            string[] parts = path.Substring(1).Split('/');
            return parts.Length == 2 && ValidSegment(parts[0], false) && ValidSegment(parts[1], true);
        }

        internal static bool ValidDir(string dir)
        {
            return !string.IsNullOrEmpty(dir) && dir.Length > 1 && dir.Length <= MaxPathLength && dir[0] == '/' && ValidSegment(dir.Substring(1), false);
        }

        private static bool ValidSegment(string s, bool file)
        {
            if (s.Length == 0 || s[0] == ' ' || s[s.Length - 1] == ' ' || s.IndexOf("..", StringComparison.Ordinal) >= 0) return false;
            foreach (char c in s)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_' || c == '-' || c == '.' || c == ' ' || c == '(' || c == ')';
                if (!ok) return false;
            }
            if (!file) return s.IndexOf('.') < 0;
            int dot = s.LastIndexOf('.');
            return dot > 0 && dot < s.Length - 1;
        }
    }
}
