using System;

namespace ObenseuerOS
{
    internal sealed class BetRandom
    {
        private const ulong Gamma = 0x9E3779B97F4A7C15UL;
        private ulong state;

        internal BetRandom(ulong start)
        {
            state = start;
        }

        internal static ulong Key(params int[] parts)
        {
            ulong h = 0;
            foreach (int part in parts) h = Mix(h + Gamma + (ulong)(uint)part);
            return h;
        }

        internal ulong NextU64()
        {
            state += Gamma;
            return Mix(state);
        }

        internal double Uniform()
        {
            return (NextU64() >> 11) * (1.0 / 9007199254740992.0);
        }

        internal int Poisson(double lambda)
        {
            double limit = Math.Exp(-lambda);
            double product = 1.0;
            int count = -1;
            do
            {
                count++;
                product *= Uniform();
            }
            while (product > limit);
            return count;
        }

        private static ulong Mix(ulong z)
        {
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }
}
