using System;

namespace ObenseuerOS
{
    internal static class BetModel
    {
        internal const int Football = 0;
        internal const int Hockey = 1;
        internal const int Volleyball = 2;
        internal const int Sports = 3;
        internal const int Rounds = 6;
        internal const double Overround = 1.05;
        internal const int MinOddsX100 = 101;
        internal const double OtShare = 0.75;
        private const double FootballBase = 1.35;
        private const double HockeyBase = 3.0;
        private const double FootballHome = 0.25;
        private const double HockeyHome = 0.25;
        private const double VolleyHome = 0.01;
        private const double RallyEdge = 0.03;
        private const double RallyMin = 0.45;
        private const double RallyMax = 0.55;
        private const int FootballMaxGoals = 12;
        private const int HockeyMaxGoals = 14;

        internal static double Strength(int seed, int sport, int season, int team)
        {
            return new BetRandom(BetRandom.Key(seed, sport, season, 0, team, 1)).Uniform() - 0.5;
        }

        internal static double HomeEdge(int sport)
        {
            if (sport == Volleyball) return VolleyHome;
            return sport == Hockey ? HockeyHome : FootballHome;
        }

        internal static double Diff(int seed, int sport, int season, int home, int away)
        {
            return Strength(seed, sport, season, home) - Strength(seed, sport, season, away) + HomeEdge(sport);
        }

        internal static double Lambda(int sport, double d, bool home)
        {
            return (sport == Hockey ? HockeyBase : FootballBase) * Math.Exp((home ? 0.5 : -0.5) * d);
        }

        internal static double DeciderHome(double d)
        {
            return 1 / (1 + Math.Exp(-d));
        }

        internal static double RallyWin(double d)
        {
            return Math.Min(RallyMax, Math.Max(RallyMin, 0.5 + RallyEdge * d));
        }

        internal static double[] Fair(int sport, double d)
        {
            if (sport == Volleyball)
            {
                double win = MatchWin(RallyWin(d));
                return new[] { win, 1 - win };
            }
            double won;
            double level;
            double lost;
            Tally(sport, d, out won, out level, out lost);
            if (sport == Football)
            {
                double total = won + level + lost;
                return new[] { won / total, level / total, lost / total };
            }
            double decider = DeciderHome(d);
            double home = won + level * decider;
            double away = lost + level * (1 - decider);
            return new[] { home / (home + away), away / (home + away) };
        }

        internal static int[] OddsX100(double[] p)
        {
            int[] odds = new int[p.Length];
            for (int i = 0; i < p.Length; i++) odds[i] = Math.Max(MinOddsX100, (int)Math.Floor(100 / (p[i] * Overround)));
            return odds;
        }

        internal static double[] Pmf(double lambda, int max)
        {
            double[] p = new double[max + 1];
            p[0] = Math.Exp(-lambda);
            for (int k = 1; k <= max; k++) p[k] = p[k - 1] * lambda / k;
            return p;
        }

        internal static double SetWin(double p, int target)
        {
            double[,] memo = new double[target + 2, target + 2];
            for (int a = 0; a < target + 2; a++)
            {
                for (int b = 0; b < target + 2; b++) memo[a, b] = -1;
            }
            return Set(p, target, memo, 0, 0);
        }

        internal static double MatchWin(double p)
        {
            return MatchFrom(0, 0, SetWin(p, 25), SetWin(p, 15));
        }

        private static void Tally(int sport, double d, out double won, out double level, out double lost)
        {
            double[] home = Pmf(Lambda(sport, d, true), sport == Hockey ? HockeyMaxGoals : FootballMaxGoals);
            double[] away = Pmf(Lambda(sport, d, false), sport == Hockey ? HockeyMaxGoals : FootballMaxGoals);
            won = 0;
            level = 0;
            lost = 0;
            for (int i = 0; i < home.Length; i++)
            {
                for (int j = 0; j < away.Length; j++)
                {
                    double chance = home[i] * away[j];
                    if (i > j) won += chance;
                    else if (i == j) level += chance;
                    else lost += chance;
                }
            }
        }

        private static double Set(double p, int target, double[,] memo, int a, int b)
        {
            double q = 1 - p;
            if (a >= target && a - b >= 2) return 1;
            if (b >= target && b - a >= 2) return 0;
            if (a >= target - 1 && b >= target - 1)
            {
                double tie = p * p / (p * p + q * q);
                if (a == b) return tie;
                return a > b ? p + q * tie : p * tie;
            }
            if (memo[a, b] < 0) memo[a, b] = p * Set(p, target, memo, a + 1, b) + q * Set(p, target, memo, a, b + 1);
            return memo[a, b];
        }

        private static double MatchFrom(int home, int away, double set25, double set15)
        {
            if (home == 3) return 1;
            if (away == 3) return 0;
            double s = home + away == 4 ? set15 : set25;
            return s * MatchFrom(home + 1, away, set25, set15) + (1 - s) * MatchFrom(home, away + 1, set25, set15);
        }
    }
}
