using System;
using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    [Serializable]
    public class BetSave
    {
        public int Seed;
        public int StartDay;
        public int Season;
        public int NextBet;
        public List<FixtureRec> Fixtures = new List<FixtureRec>();
        public List<BetRec> Bets = new List<BetRec>();
        public List<PendingRec> Pending = new List<PendingRec>();
    }

    [Serializable]
    public class FixtureRec
    {
        public string Id;
        public int Sport;
        public int Season;
        public int Round;
        public int Match;
        public int Home;
        public int Away;
        public int Kickoff;
        public int Odds1;
        public int OddsX;
        public int Odds2;
        public bool Done;
        public int HomeGoals;
        public int AwayGoals;
        public int Decider;
        public int Outcome;
        public string Sets;
    }

    [Serializable]
    public class BetRec
    {
        public int N;
        public string Cid;
        public string Print;
        public string Fixture;
        public string Match;
        public string Pick;
        public int Stake;
        public int OddsX100;
        public int Ret;
        public string State;
        public int PlacedMin;
    }

    [Serializable]
    public class PendingRec
    {
        public int Oc;
    }

    internal static class BetBook
    {
        internal const string StoreId = "nomad.obenseuer.os.bets";
        internal const int MaxSaved = 200;
        internal const int MinStake = 10;
        internal const int MaxStake = 5000;
        internal const int MaxOpen = 30;
        internal const string Open = "open";
        internal const string Won = "won";
        internal const string Lost = "lost";
        internal const string ConflictText = "This bet was already placed with different contents.";
        private static BetSave save = new BetSave();

        internal static BetSave Current
        {
            get
            {
                return save;
            }
        }

        internal static void Attach()
        {
            SaveData.Register<BetSave>(StoreId, () => save, loaded => save = loaded ?? new BetSave());
            Events.HourPassed += OnHour;
        }

        internal static int Now()
        {
            WorldTime now = WorldClock.Now;
            return now.Day * BetSchedule.MinutesPerDay + now.Hour * 60 + now.Minute;
        }

        internal static void Begin()
        {
            if (SaveData.Busy) return;
            int now = Now();
            if (save.Seed == 0)
            {
                save.Seed = new Random().Next(1, int.MaxValue);
                BetSchedule.Create(save, now / BetSchedule.MinutesPerDay);
            }
            Advance(save, now);
        }

        internal static void Advance(BetSave state, int now)
        {
            if (SaveData.Busy) return;
            foreach (FixtureRec fixture in state.Fixtures)
            {
                if (!fixture.Done && fixture.Kickoff <= now) Play(state.Seed, fixture);
            }
            foreach (BetRec bet in state.Bets)
            {
                FixtureRec fixture = Find(state, bet.Fixture);
                if (bet.State == Open && fixture != null && FinalAt(fixture) <= now) Settle(state, bet, fixture);
            }
            RetryPending(state);
            int day = now / BetSchedule.MinutesPerDay;
            if (day >= state.StartDay + BetModel.Rounds) BetSchedule.Create(state, day);
        }

        internal static BetRec Saved(BetSave state, string cid, string print)
        {
            foreach (BetRec bet in state.Bets)
            {
                if (bet.Cid != cid) continue;
                if (bet.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return bet;
            }
            return null;
        }

        internal static string Problem(BetSave state, FixtureRec fixture, int stake, int now)
        {
            if (fixture.Done || now >= fixture.Kickoff) return "This match has kicked off. Bets are closed.";
            if (stake < MinStake || stake > MaxStake) return "Stakes are from " + MinStake + " to " + MaxStake + " OC.";
            if (!Cash.Has(MoneyCurrency.OC, stake)) return "Not enough OC for this bet.";
            if (OpenCount(state) >= MaxOpen) return "You have too many open bets.";
            return null;
        }

        internal static BetRec Place(BetSave state, FixtureRec fixture, string pick, int stake, string cid, string print, int now)
        {
            int odds = OddsOf(fixture, pick);
            BetRec bet = new BetRec();
            bet.N = state.NextBet + 1;
            bet.Cid = cid;
            bet.Print = print;
            bet.Fixture = fixture.Id;
            bet.Match = BetSchedule.MatchName(fixture);
            bet.Pick = pick;
            bet.Stake = stake;
            bet.OddsX100 = odds;
            bet.Ret = Payout(stake, odds);
            bet.State = Open;
            bet.PlacedMin = now;
            if (!Cash.TrySpend(MoneyCurrency.OC, stake)) throw new OsBridgeException("failed", "The payment was refused. Nothing was spent.");
            state.NextBet = bet.N;
            state.Bets.Add(bet);
            Prune(state);
            return bet;
        }

        private static int OpenCount(BetSave state)
        {
            int open = 0;
            foreach (BetRec bet in state.Bets)
            {
                if (bet.State == Open) open++;
            }
            return open;
        }

        internal static int OddsOf(FixtureRec fixture, string pick)
        {
            if (pick == "draw") return fixture.OddsX;
            return pick == "home" ? fixture.Odds1 : fixture.Odds2;
        }

        internal static int Payout(int stake, int oddsX100)
        {
            return (int)((long)stake * oddsX100 / 100);
        }

        internal static int FinalAt(FixtureRec fixture)
        {
            return fixture.Kickoff + BetSchedule.Length(fixture.Sport);
        }

        internal static string StatusOf(FixtureRec fixture, int now)
        {
            if (now < fixture.Kickoff) return "upcoming";
            return now < FinalAt(fixture) ? "live" : "final";
        }

        internal static FixtureRec Find(BetSave state, string id)
        {
            foreach (FixtureRec fixture in state.Fixtures)
            {
                if (fixture.Id == id) return fixture;
            }
            return null;
        }

        internal static int CurrentRound(BetSave state, int sport, int now)
        {
            int round = BetModel.Rounds;
            foreach (FixtureRec fixture in state.Fixtures)
            {
                if (fixture.Sport == sport && fixture.Season == state.Season && FinalAt(fixture) > now && fixture.Round < round) round = fixture.Round;
            }
            return round;
        }

        internal static void Prune(BetSave state)
        {
            for (int i = 0; i < state.Bets.Count && state.Bets.Count > MaxSaved; )
            {
                if (state.Bets[i].State == Open) i++;
                else state.Bets.RemoveAt(i);
            }
        }

        internal static void Play(int seed, FixtureRec fixture)
        {
            SimResult result = BetSim.Play(seed, fixture.Sport, fixture.Season, fixture.Round, fixture.Match, fixture.Home, fixture.Away);
            fixture.HomeGoals = result.HomeGoals;
            fixture.AwayGoals = result.AwayGoals;
            fixture.Decider = result.Decider;
            fixture.Outcome = result.Outcome;
            fixture.Sets = result.Sets;
            fixture.Done = true;
        }

        private static void Settle(BetSave state, BetRec bet, FixtureRec fixture)
        {
            if (bet.Pick != BetSchedule.PickOf(fixture.Outcome))
            {
                bet.State = Lost;
                return;
            }
            bet.State = Won;
            PendingRec owed = new PendingRec();
            owed.Oc = bet.Ret;
            state.Pending.Add(owed);
        }

        private static void RetryPending(BetSave state)
        {
            for (int i = state.Pending.Count - 1; i >= 0; i--)
            {
                if (Cash.Give(MoneyCurrency.OC, state.Pending[i].Oc)) state.Pending.RemoveAt(i);
            }
        }

        private static void OnHour(int hour)
        {
            if (save.Seed != 0) Advance(save, Now());
        }
    }
}
