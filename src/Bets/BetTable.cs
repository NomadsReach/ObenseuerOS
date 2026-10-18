using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class TableRow
    {
        internal int Team;
        internal int Played;
        internal int Won;
        internal int Drawn;
        internal int Lost;
        internal int For;
        internal int Against;
        internal int Points;

        internal int Diff
        {
            get
            {
                return For - Against;
            }
        }
    }

    internal static class BetTable
    {
        private const int FootballDraw = 1;
        private const int ClearWin = 3;
        private const int ClearLoss = 0;
        private const int CloseWin = 2;
        private const int CloseLoss = 1;
        private const int CloseSets = 2;

        internal static List<TableRow> Build(BetSave state, int sport, int now)
        {
            Dictionary<int, TableRow> rows = new Dictionary<int, TableRow>();
            foreach (FixtureRec fixture in state.Fixtures)
            {
                if (fixture.Sport != sport || fixture.Season != state.Season || !fixture.Done || BetBook.FinalAt(fixture) > now) continue;
                TableRow home = Row(rows, fixture.Home);
                TableRow away = Row(rows, fixture.Away);
                home.Played++;
                away.Played++;
                home.For += fixture.HomeGoals;
                home.Against += fixture.AwayGoals;
                away.For += fixture.AwayGoals;
                away.Against += fixture.HomeGoals;
                if (fixture.Outcome == 1)
                {
                    home.Drawn++;
                    away.Drawn++;
                    home.Points += FootballDraw;
                    away.Points += FootballDraw;
                    continue;
                }
                TableRow winner = fixture.Outcome == 0 ? home : away;
                TableRow loser = fixture.Outcome == 0 ? away : home;
                bool close = sport == BetModel.Hockey ? fixture.Decider != 0 : sport == BetModel.Volleyball && Math.Min(fixture.HomeGoals, fixture.AwayGoals) == CloseSets;
                winner.Won++;
                loser.Lost++;
                winner.Points += close ? CloseWin : ClearWin;
                loser.Points += close ? CloseLoss : ClearLoss;
            }
            List<TableRow> table = new List<TableRow>(rows.Values);
            table.Sort(Order);
            return table;
        }

        private static TableRow Row(Dictionary<int, TableRow> rows, int team)
        {
            TableRow row;
            if (!rows.TryGetValue(team, out row))
            {
                row = new TableRow();
                row.Team = team;
                rows.Add(team, row);
            }
            return row;
        }

        private static int Order(TableRow a, TableRow b)
        {
            if (a.Points != b.Points) return b.Points.CompareTo(a.Points);
            if (a.Diff != b.Diff) return b.Diff.CompareTo(a.Diff);
            if (a.For != b.For) return b.For.CompareTo(a.For);
            return a.Team.CompareTo(b.Team);
        }
    }
}
