using System;
using System.Collections.Generic;
using System.Text;

namespace ObenseuerOS
{
    internal static class BetSelfTest
    {
        internal static string Run()
        {
            if (new BetRandom(0).NextU64() != 0xE220A8397B1DCDAFUL) return "splitmix golden";
            BetRandom draws = new BetRandom(BetRandom.Key(7));
            double total = 0;
            for (int i = 0; i < 100000; i++) total += draws.Poisson(2.5);
            if (Math.Abs(total / 100000 - 2.5) > 2.5 * 0.02) return "poisson mean";
            foreach (double d in new[] { 0.0, 0.5 })
            {
                double[] p = BetModel.Fair(BetModel.Football, d);
                if (Math.Abs(p[0] + p[1] + p[2] - 1) > 1e-9) return "football 1X2 sum";
            }
            double[] hockey = BetModel.Fair(BetModel.Hockey, 0.3);
            if (Math.Abs(hockey[0] + hockey[1] - 1) > 1e-9) return "hockey sum";
            if (Math.Abs(BetModel.SetWin(0.5, 25) - 0.5) > 1e-9 || Math.Abs(BetModel.SetWin(0.5, 15) - 0.5) > 1e-9) return "volleyball half";
            if (Math.Abs(BetModel.SetWin(0.55, 25) - 0.7641) > 0.001) return "volleyball set";
            int[] odds = BetModel.OddsX100(BetModel.Fair(BetModel.Football, BetModel.HomeEdge(BetModel.Football)));
            if (odds[0] != 211 || odds[1] != 376 || odds[2] != 321) return "football odds";
            if (BetBook.Payout(15, 211) != 31) return "payout rounds down";
            if (!SameResult(BetSim.Play(123, BetModel.Hockey, 1, 2, 1, 0, 3), BetSim.Play(123, BetModel.Hockey, 1, 2, 1, 0, 3))) return "same seed";
            if (!SameResult(BetSim.Play(9, BetModel.Volleyball, 2, 4, 2, 1, 2), BetSim.Play(9, BetModel.Volleyball, 2, 4, 2, 1, 2))) return "same seed volleyball";
            BetSave state = new BetSave();
            state.Seed = 5;
            BetSchedule.Create(state, 10);
            FixtureRec first = BetBook.Find(state, "fb-1-1-1");
            if (first == null || first.Kickoff != 10 * BetSchedule.MinutesPerDay + 15 * 60) return "football kickoff";
            if (BetBook.Find(state, "fb-1-6-2") == null || BetBook.Find(state, "vb-1-1-3") != null) return "fixture ids";
            int sport;
            int season;
            int round;
            int match;
            if (!BetSchedule.TryParse("ih-1-3-2", out sport, out season, out round, out match) || sport != BetModel.Hockey || round != 3 || match != 2) return "fixture id parse";
            if (BetSchedule.TryParse("fb-1-7-1", out sport, out season, out round, out match)) return "fixture id round";
            BetRec rec = new BetRec();
            rec.Cid = "c1";
            rec.Print = "p1";
            BetSave replay = new BetSave();
            replay.Bets.Add(rec);
            if (BetBook.Saved(replay, "c1", "p1") != rec || BetBook.Saved(replay, "c2", "p1") != null) return "replay";
            if (!Refused(replay)) return "conflict accepted";
            if (Size(BetBridge.MineJson(Worst(), null, 0)) > 12288) return "mine too large";
            if (Size(BetBridge.FixturesJson(Worst(), BetModel.Football, 0, 0)) > 12288) return "fixtures too large";
            if (Size(BetBridge.ResultsJson(Worst(), BetModel.Volleyball, 0, int.MaxValue / 2)) > 12288) return "results too large";
            if (Size(BetBridge.TableJson(Worst(), int.MaxValue / 2)) > 12288) return "table too large";
            BetSave league = new BetSave();
            league.Seed = 77;
            BetSchedule.Create(league, 10);
            int expected = 0;
            foreach (FixtureRec fixture in league.Fixtures)
            {
                BetBook.Play(league.Seed, fixture);
                expected += fixture.Outcome == 1 ? 2 : 3;
            }
            int tablePoints = 0;
            for (int s = 0; s < BetModel.Sports; s++)
            {
                foreach (TableRow row in BetTable.Build(league, s, int.MaxValue / 2))
                {
                    tablePoints += row.Points;
                    if (row.Played != BetModel.Rounds || row.Won + row.Drawn + row.Lost != row.Played) return "table row";
                }
            }
            if (tablePoints != expected) return "table points";
            if (!PointsAre(Final(BetModel.Football, 1, 1, 1, 0), 1, 1)) return "football draw points";
            if (!PointsAre(Final(BetModel.Football, 2, 0, 0, 0), 3, 0)) return "football win points";
            if (!PointsAre(Final(BetModel.Hockey, 2, 2, 2, 1), 1, 2)) return "hockey overtime points";
            if (!PointsAre(Final(BetModel.Hockey, 3, 1, 0, 0), 3, 0)) return "hockey regulation points";
            if (!PointsAre(Final(BetModel.Volleyball, 3, 2, 0, 0), 2, 1)) return "volleyball 3-2 points";
            if (!PointsAre(Final(BetModel.Volleyball, 3, 1, 0, 0), 3, 0)) return "volleyball 3-1 points";
            return null;
        }

        private static bool SameResult(SimResult a, SimResult b)
        {
            return a.HomeGoals == b.HomeGoals && a.AwayGoals == b.AwayGoals && a.Decider == b.Decider && a.Outcome == b.Outcome && a.Sets == b.Sets;
        }

        private static bool Refused(BetSave state)
        {
            try
            {
                BetBook.Saved(state, "c1", "other");
                return false;
            }
            catch (OsBridgeException e)
            {
                return e.Code == "bad_args";
            }
        }

        private static FixtureRec Final(int sport, int homeGoals, int awayGoals, int outcome, int decider)
        {
            FixtureRec fixture = new FixtureRec();
            fixture.Sport = sport;
            fixture.Season = 1;
            fixture.Home = 0;
            fixture.Away = 1;
            fixture.HomeGoals = homeGoals;
            fixture.AwayGoals = awayGoals;
            fixture.Outcome = outcome;
            fixture.Decider = decider;
            fixture.Done = true;
            return fixture;
        }

        private static bool PointsAre(FixtureRec fixture, int homePoints, int awayPoints)
        {
            BetSave state = new BetSave();
            state.Season = 1;
            state.Fixtures.Add(fixture);
            List<TableRow> rows = BetTable.Build(state, fixture.Sport, int.MaxValue / 2);
            return rows.Count == 2 && PointsOf(rows, 0) == homePoints && PointsOf(rows, 1) == awayPoints;
        }

        private static int PointsOf(List<TableRow> rows, int team)
        {
            foreach (TableRow row in rows)
            {
                if (row.Team == team) return row.Points;
            }
            return -1;
        }

        private static int Size(string json)
        {
            return Encoding.UTF8.GetByteCount(json);
        }

        private static BetSave Worst()
        {
            BetSave state = new BetSave();
            state.Season = 99999;
            for (int i = 0; i < 40; i++)
            {
                BetRec bet = new BetRec();
                bet.N = 999999;
                bet.Cid = new string('c', 64);
                bet.Print = new string('p', 80);
                bet.Fixture = "vb-99999-6-2";
                bet.Match = "Ploughwedge FC - Ploughwedge FC";
                bet.Pick = "draw";
                bet.Stake = 5000;
                bet.OddsX100 = 99999;
                bet.Ret = 49999999;
                bet.State = "settled";
                bet.PlacedMin = 999999999;
                state.Bets.Add(bet);
            }
            for (int i = 0; i < 12; i++)
            {
                FixtureRec fixture = new FixtureRec();
                fixture.Id = "fb-99999-6-2";
                fixture.Sport = BetModel.Football;
                fixture.Season = 99999;
                fixture.Round = 6;
                fixture.Match = 2;
                fixture.Home = 3;
                fixture.Away = 0;
                fixture.Kickoff = 999999999;
                fixture.Odds1 = 9999;
                fixture.OddsX = 9999;
                fixture.Odds2 = 9999;
                state.Fixtures.Add(fixture);
            }
            for (int i = 0; i < 20; i++)
            {
                FixtureRec fixture = new FixtureRec();
                fixture.Id = "vb-99999-6-2";
                fixture.Sport = BetModel.Volleyball;
                fixture.Season = 99999;
                fixture.Round = 6;
                fixture.Match = 2;
                fixture.Home = 3;
                fixture.Away = 0;
                fixture.Kickoff = 999999999;
                fixture.Odds1 = 9999;
                fixture.Odds2 = 9999;
                fixture.Done = true;
                fixture.HomeGoals = 3;
                fixture.AwayGoals = 2;
                fixture.Outcome = 0;
                fixture.Sets = "25-20,25-18,22-25,22-25,15-13";
                state.Fixtures.Add(fixture);
            }
            return state;
        }
    }
}
