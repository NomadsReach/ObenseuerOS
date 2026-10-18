using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class LottoDraw
    {
        internal const int Pool = 40;
        internal const int PickCount = 6;
        internal const int MinWin = 3;
        private static readonly int[] PrizeTable = { 0, 0, 0, 120, 1500, 20000, 50000 };

        internal static int Prize(int matches)
        {
            return PrizeTable[matches];
        }

        internal static int Matches(IList<int> ticket, IList<int> drawn)
        {
            int hits = 0;
            foreach (int n in ticket)
            {
                if (drawn.Contains(n)) hits++;
            }
            return hits;
        }

        internal static List<int> Numbers(int seed, int n)
        {
            ulong state = ((ulong)(uint)seed << 32) ^ (ulong)(uint)n;
            List<int> nums = new List<int>();
            while (nums.Count < PickCount)
            {
                int pick = (int)(Next(ref state) % Pool) + 1;
                if (!nums.Contains(pick)) nums.Add(pick);
            }
            nums.Sort();
            return nums;
        }

        private static ulong Next(ref ulong state)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong z = state;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
