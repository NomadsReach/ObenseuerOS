using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class SimResult
    {
        internal int HomeGoals;
        internal int AwayGoals;
        internal int Decider;
        internal int Outcome;
        internal string Sets;
    }

    internal static class BetSim
    {
        private const int GoalStage = 2;
        private const int RallyStage = 3;
        private const int DeciderStage = 4;

        internal static SimResult Play(int seed, int sport, int season, int round, int match, int home, int away)
        {
            double d = BetModel.Diff(seed, sport, season, home, away);
            SimResult result = new SimResult();
            if (sport == BetModel.Volleyball)
            {
                PlayMatch(new BetRandom(BetRandom.Key(seed, sport, season, round, match, RallyStage)), BetModel.RallyWin(d), result);
                return result;
            }
            BetRandom goals = new BetRandom(BetRandom.Key(seed, sport, season, round, match, GoalStage));
            result.HomeGoals = goals.Poisson(BetModel.Lambda(sport, d, true));
            result.AwayGoals = goals.Poisson(BetModel.Lambda(sport, d, false));
            if (result.HomeGoals > result.AwayGoals) result.Outcome = 0;
            else if (result.HomeGoals < result.AwayGoals) result.Outcome = 2;
            else if (sport == BetModel.Football) result.Outcome = 1;
            else
            {
                BetRandom decider = new BetRandom(BetRandom.Key(seed, sport, season, round, match, DeciderStage));
                result.Outcome = decider.Uniform() < BetModel.DeciderHome(d) ? 0 : 2;
                result.Decider = decider.Uniform() < BetModel.OtShare ? 1 : 2;
            }
            return result;
        }

        private static void PlayMatch(BetRandom rally, double p, SimResult result)
        {
            List<string> sets = new List<string>();
            int homeSets = 0;
            int awaySets = 0;
            while (homeSets < 3 && awaySets < 3)
            {
                int target = homeSets + awaySets == 4 ? 15 : 25;
                int a = 0;
                int b = 0;
                while (!(a >= target && a - b >= 2) && !(b >= target && b - a >= 2))
                {
                    if (rally.Uniform() < p) a++;
                    else b++;
                }
                if (a > b) homeSets++;
                else awaySets++;
                sets.Add(a + "-" + b);
            }
            result.HomeGoals = homeSets;
            result.AwayGoals = awaySets;
            result.Outcome = homeSets > awaySets ? 0 : 2;
            result.Sets = string.Join(",", sets.ToArray());
        }
    }
}
