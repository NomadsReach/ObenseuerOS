using System;

namespace ObenseuerOS
{
    internal static class BetSchedule
    {
        internal const int MinutesPerDay = 1440;
        internal const int MatchesPerRound = 2;
        private static readonly string[] Codes = { "fb", "ih", "vb" };
        private static readonly string[] Names = { "football", "hockey", "volleyball" };
        private static readonly string[] Titles = { "Football", "Ice hockey", "Volleyball" };
        private static readonly string[] Leagues = { "Stolland League (football)", "Stolland League (ice hockey)", "Stolland League (volleyball)" };
        private static readonly string[] Suffixes = { "FC", "HC", "VC" };
        private static readonly int[] KickoffHours = { 15, 19, 18 };
        private static readonly int[] LengthMinutes = { 120, 150, 90 };
        private static readonly string[][] Towns =
        {
            new[] { "Stalburg", "Ardscratch", "Baltasfell", "Cellecter" },
            new[] { "Illo", "Ploughwedge", "Stemlake", "Tarfell" },
            new[] { "Tilstrand", "Ershfors", "Forde", "Koksburg" },
        };
        private static readonly int[][] Pairs =
        {
            new[] { 0, 3, 1, 2 },
            new[] { 0, 2, 3, 1 },
            new[] { 0, 1, 2, 3 },
            new[] { 3, 0, 2, 1 },
            new[] { 2, 0, 1, 3 },
            new[] { 1, 0, 3, 2 },
        };

        internal static string Name(int sport)
        {
            return Names[sport];
        }

        internal static string Title(int sport)
        {
            return Titles[sport];
        }

        internal static string League(int sport)
        {
            return Leagues[sport];
        }

        internal static int SportOf(string name)
        {
            return name == null ? -1 : Array.IndexOf(Names, name);
        }

        internal static int Length(int sport)
        {
            return LengthMinutes[sport];
        }

        internal static string TeamName(int sport, int team)
        {
            return Towns[sport][team] + " " + Suffixes[sport];
        }

        internal static string MatchName(FixtureRec fixture)
        {
            return TeamName(fixture.Sport, fixture.Home) + " - " + TeamName(fixture.Sport, fixture.Away);
        }

        internal static bool PickValid(int sport, string pick)
        {
            return pick == "home" || pick == "away" || (sport == BetModel.Football && pick == "draw");
        }

        internal static string PickOf(int outcome)
        {
            return outcome == 0 ? "home" : outcome == 1 ? "draw" : "away";
        }

        internal static string Id(int sport, int season, int round, int match)
        {
            return Codes[sport] + "-" + season + "-" + round + "-" + match;
        }

        internal static bool TryParse(string id, out int sport, out int season, out int round, out int match)
        {
            sport = -1;
            season = 0;
            round = 0;
            match = 0;
            string[] parts = id == null ? new string[0] : id.Split('-');
            if (parts.Length != 4) return false;
            sport = Array.IndexOf(Codes, parts[0]);
            return sport >= 0
                && int.TryParse(parts[1], out season) && season >= 1
                && int.TryParse(parts[2], out round) && round >= 1 && round <= BetModel.Rounds
                && int.TryParse(parts[3], out match) && match >= 1 && match <= MatchesPerRound;
        }

        internal static void Create(BetSave state, int startDay)
        {
            state.Season++;
            state.StartDay = startDay;
            for (int sport = 0; sport < BetModel.Sports; sport++)
            {
                for (int round = 1; round <= BetModel.Rounds; round++)
                {
                    for (int match = 1; match <= MatchesPerRound; match++)
                    {
                        state.Fixtures.Add(Make(state, sport, round, match));
                    }
                }
            }
            state.Fixtures.RemoveAll(old => old.Season < state.Season - 1);
        }

        private static FixtureRec Make(BetSave state, int sport, int round, int match)
        {
            int home = Pairs[round - 1][(match - 1) * 2];
            int away = Pairs[round - 1][(match - 1) * 2 + 1];
            int[] odds = BetModel.OddsX100(BetModel.Fair(sport, BetModel.Diff(state.Seed, sport, state.Season, home, away)));
            FixtureRec fixture = new FixtureRec();
            fixture.Id = Id(sport, state.Season, round, match);
            fixture.Sport = sport;
            fixture.Season = state.Season;
            fixture.Round = round;
            fixture.Match = match;
            fixture.Home = home;
            fixture.Away = away;
            fixture.Kickoff = (state.StartDay + round - 1) * MinutesPerDay + KickoffHours[sport] * 60;
            fixture.Odds1 = odds[0];
            fixture.OddsX = odds.Length == 3 ? odds[1] : 0;
            fixture.Odds2 = odds[odds.Length - 1];
            return fixture;
        }
    }
}
