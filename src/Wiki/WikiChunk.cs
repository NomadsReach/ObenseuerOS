using System;

namespace ObenseuerOS
{
    internal static class WikiChunk
    {
        internal const int TargetBytes = 10 * 1024;
        internal const int HardLimitBytes = 11 * 1024;

        internal static int Cost(string text)
        {
            int total = 0;
            int i = 0;
            while (i < text.Length)
            {
                int width = Width(text, i);
                total += Unit(text[i], width);
                i += width;
            }
            return total;
        }

        private static int Width(string text, int i)
        {
            return char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
        }

        private static int Unit(char c, int width)
        {
            if (width == 2) return 4;
            if (char.IsSurrogate(c)) return 6;
            if (c == '"' || c == '\\') return 2;
            if (c < 0x20) return 6;
            if (c == 0x85 || c == 0x2028 || c == 0x2029) return 6;
            if (c < 0x80) return 1;
            if (c < 0x800) return 2;
            return 3;
        }
    }
}
