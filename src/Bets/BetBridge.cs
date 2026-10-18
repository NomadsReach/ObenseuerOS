using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class BetBridge
    {
        private const string AppId = "bets";
        internal const int FixturesPage = 12;
        internal const int MinePage = 30;
        internal const int ResultsPage = 20;
        private const int MaxCid = 64;
        private const int MaxPage = 100000;

        internal static void Register()
        {
            Os.Handle(AppId, "bet.sports", Sports);
            Os.Handle(AppId, "bet.fixtures", Fixtures);
            Os.Handle(AppId, "bet.quote", Quote);
            Os.Handle(AppId, "bet.place", Place);
            Os.Handle(AppId, "bet.mine", Mine);
            Os.Handle(AppId, "bet.results", Results);
            Os.Handle(AppId, "bet.table", Table);
        }

        private static string Sports(string args)
        {
            OsBridge.Args(args);
            Prepare();
            BetSave state = BetBook.Current;
            int now = BetBook.Now();
            List<object> sports = new List<object>();
            for (int sport = 0; sport < BetModel.Sports; sport++)
            {
                sports.Add(new { id = BetSchedule.Name(sport), name = BetSchedule.Title(sport), league = BetSchedule.League(sport), season = state.Season, round = BetBook.CurrentRound(state, sport, now), rounds = BetModel.Rounds, now = now });
            }
            return JsonConvert.SerializeObject(new { wallet = Wallet(), minStake = BetBook.MinStake, sports = sports });
        }

        private static string Fixtures(string args)
        {
            JObject a = OsBridge.Args(args);
            int sport = SportArg(a);
            int round = RoundArg(a);
            Prepare();
            return FixturesJson(BetBook.Current, sport, round, BetBook.Now());
        }

        private static string Quote(string args)
        {
            JObject a = OsBridge.Args(args);
            int sport;
            string fixture = FixtureArg(a, out sport);
            string pick = PickArg(a, sport);
            int stake = StakeArg(a);
            Prepare();
            FixtureRec match = Match(fixture);
            string problem = BetBook.Problem(BetBook.Current, match, stake, BetBook.Now());
            int odds = BetBook.OddsOf(match, pick);
            return JsonConvert.SerializeObject(new { ok = problem == null, odds = odds, stake = stake, ret = problem == null ? BetBook.Payout(stake, odds) : 0, problems = problem == null ? new string[0] : new[] { problem } });
        }

        private static string Place(string args)
        {
            JObject a = OsBridge.Args(args);
            string cid = CidArg(a);
            int sport;
            string fixture = FixtureArg(a, out sport);
            string pick = PickArg(a, sport);
            int stake = StakeArg(a);
            string print = fixture + "|" + pick + "|" + stake;
            BetRec saved = BetBook.Saved(BetBook.Current, cid, print);
            if (saved != null) return Receipt(saved);
            OsBridge.CheckWritable();
            Prepare();
            FixtureRec match = Match(fixture);
            string problem = BetBook.Problem(BetBook.Current, match, stake, BetBook.Now());
            if (problem != null) throw new OsBridgeException("denied", problem);
            return Receipt(BetBook.Place(BetBook.Current, match, pick, stake, cid, print, BetBook.Now()));
        }

        private static string Mine(string args)
        {
            JObject a = OsBridge.Args(args);
            string status = OsBridge.Text(a, "status");
            if (status != null && status != "open" && status != "settled") throw new OsBridgeException("bad_args", "status must be open or settled");
            int page = OsBridge.Span(a, "page", MaxPage, 0);
            Prepare();
            return MineJson(BetBook.Current, status, page);
        }

        private static string Results(string args)
        {
            JObject a = OsBridge.Args(args);
            int sport = SportArg(a);
            int page = OsBridge.Span(a, "page", MaxPage, 0);
            Prepare();
            return ResultsJson(BetBook.Current, sport, page, BetBook.Now());
        }

        private static string Table(string args)
        {
            OsBridge.Args(args);
            Prepare();
            return TableJson(BetBook.Current, BetBook.Now());
        }

        internal static string TableJson(BetSave state, int now)
        {
            List<object> sports = new List<object>();
            for (int sport = 0; sport < BetModel.Sports; sport++)
            {
                List<object> rows = new List<object>();
                foreach (TableRow row in BetTable.Build(state, sport, now)) rows.Add(TableRowJson(sport, row));
                sports.Add(new { id = BetSchedule.Name(sport), name = BetSchedule.Title(sport), rows = rows });
            }
            return JsonConvert.SerializeObject(new { season = state.Season, sports = sports });
        }

        internal static string FixturesJson(BetSave state, int sport, int round, int now)
        {
            List<FixtureRec> matches = new List<FixtureRec>();
            foreach (FixtureRec fixture in state.Fixtures)
            {
                if (fixture.Sport == sport && fixture.Season == state.Season && (round == 0 || fixture.Round == round)) matches.Add(fixture);
            }
            List<object> rows = new List<object>();
            for (int i = 0; i < matches.Count && i < FixturesPage; i++) rows.Add(FixtureJson(matches[i], now));
            return JsonConvert.SerializeObject(new { total = matches.Count, rows = rows });
        }

        internal static string MineJson(BetSave state, string status, int page)
        {
            List<BetRec> matches = new List<BetRec>();
            for (int i = state.Bets.Count - 1; i >= 0; i--)
            {
                BetRec bet = state.Bets[i];
                if (status == null || (status == "open") == (bet.State == BetBook.Open)) matches.Add(bet);
            }
            List<object> rows = new List<object>();
            for (int i = page * MinePage; i < matches.Count && i < (page + 1) * MinePage; i++) rows.Add(MineJsonRow(matches[i]));
            return JsonConvert.SerializeObject(new { total = matches.Count, rows = rows });
        }

        internal static string ResultsJson(BetSave state, int sport, int page, int now)
        {
            List<FixtureRec> matches = new List<FixtureRec>();
            for (int i = state.Fixtures.Count - 1; i >= 0; i--)
            {
                FixtureRec fixture = state.Fixtures[i];
                if (fixture.Sport == sport && fixture.Done && BetBook.FinalAt(fixture) <= now) matches.Add(fixture);
            }
            List<object> rows = new List<object>();
            for (int i = page * ResultsPage; i < matches.Count && i < (page + 1) * ResultsPage; i++) rows.Add(ResultJson(matches[i]));
            return JsonConvert.SerializeObject(new { total = matches.Count, rows = rows });
        }

        private static object FixtureJson(FixtureRec fixture, int now)
        {
            object odds = fixture.OddsX > 0 ? (object)new { home = fixture.Odds1, draw = fixture.OddsX, away = fixture.Odds2 } : new { home = fixture.Odds1, away = fixture.Odds2 };
            return new { id = fixture.Id, round = fixture.Round, home = BetSchedule.TeamName(fixture.Sport, fixture.Home), away = BetSchedule.TeamName(fixture.Sport, fixture.Away), kickoff = fixture.Kickoff, status = BetBook.StatusOf(fixture, now), odds = odds };
        }

        private static object MineJsonRow(BetRec bet)
        {
            return new { n = bet.N, fixture = bet.Fixture, match = bet.Match, pick = bet.Pick, stake = bet.Stake, odds = bet.OddsX100, ret = bet.Ret, state = bet.State, at = bet.PlacedMin };
        }

        private static object ResultJson(FixtureRec fixture)
        {
            string decider = fixture.Decider == 1 ? "OT" : fixture.Decider == 2 ? "SO" : null;
            return new { fixture = fixture.Id, round = fixture.Round, home = BetSchedule.TeamName(fixture.Sport, fixture.Home), away = BetSchedule.TeamName(fixture.Sport, fixture.Away), score = fixture.HomeGoals + "-" + fixture.AwayGoals, winner = BetSchedule.PickOf(fixture.Outcome), decider = decider, sets = fixture.Sets };
        }

        private static object TableRowJson(int sport, TableRow row)
        {
            string team = BetSchedule.TeamName(sport, row.Team);
            if (sport == BetModel.Football) return new { team = team, played = row.Played, won = row.Won, drawn = row.Drawn, lost = row.Lost, diff = row.Diff, points = row.Points };
            return new { team = team, played = row.Played, won = row.Won, lost = row.Lost, diff = row.Diff, points = row.Points };
        }

        private static string Receipt(BetRec bet)
        {
            return JsonConvert.SerializeObject(new { bet = new { n = bet.N, fixture = bet.Fixture, pick = bet.Pick, stake = bet.Stake, odds = bet.OddsX100, ret = bet.Ret, state = bet.State }, wallet = Wallet() });
        }

        private static object Wallet()
        {
            return new { oc = (int)Cash.Balance(MoneyCurrency.OC) };
        }

        private static void Prepare()
        {
            if (!Cash.IsAvailable || !WorldClock.IsAvailable) throw new OsBridgeException("failed", "The bets are not ready yet.");
            BetBook.Begin();
        }

        private static FixtureRec Match(string id)
        {
            FixtureRec fixture = BetBook.Find(BetBook.Current, id);
            if (fixture == null) throw new OsBridgeException("bad_args", "There is no match with that id.");
            return fixture;
        }

        private static string CidArg(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > MaxCid) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }

        private static string FixtureArg(JObject a, out int sport)
        {
            string id = OsBridge.Text(a, "fixture");
            int season;
            int round;
            int match;
            if (!BetSchedule.TryParse(id, out sport, out season, out round, out match)) throw new OsBridgeException("bad_args", "fixture is not a match id");
            return id;
        }

        private static string PickArg(JObject a, int sport)
        {
            string pick = OsBridge.Text(a, "pick");
            if (!BetSchedule.PickValid(sport, pick)) throw new OsBridgeException("bad_args", "pick is not valid for this sport");
            return pick;
        }

        private static int StakeArg(JObject a)
        {
            JToken token = a["stake"];
            if (token == null || token.Type != JTokenType.Integer) throw new OsBridgeException("bad_args", "stake must be a whole number of OC");
            long value;
            if (!long.TryParse(token.ToString(Formatting.None), out value)) value = long.MaxValue;
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }

        private static int SportArg(JObject a)
        {
            int sport = BetSchedule.SportOf(OsBridge.Text(a, "sport"));
            if (sport < 0) throw new OsBridgeException("bad_args", "sport must be football, hockey or volleyball");
            return sport;
        }

        private static int RoundArg(JObject a)
        {
            int round = OsBridge.Span(a, "round", BetModel.Rounds, 0);
            if (round == 0 && a["round"] != null) throw new OsBridgeException("bad_args", "round must be 1 to 6");
            return round;
        }
    }
}
