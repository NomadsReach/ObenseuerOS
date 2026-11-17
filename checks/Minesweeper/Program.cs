using System;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    private static void Main()
    {
        Check(MinesweeperLayout.CellSize(660, 390, 9, 9) == 43, "beginner board fits viewport");
        Check(MinesweeperLayout.CellSize(300, 200, 30, 16) == 16, "expert board keeps minimum cell size");
        Check(MinesweeperLayout.CellSize(900, 600, 30, 16) == 30, "expert board grows with viewport");
        Check(MinesweeperLayout.CellSize(float.NaN, 0, 9, 9) == 16, "unlaid out viewport uses minimum size");
        MinesweeperGame clicks = new MinesweeperGame(9, 9, 10, new Random(11));
        MinesweeperInput input = new MinesweeperInput(clicks);
        Check(!input.Press(0, 0, 1) && !input.Release(1, 0, false) && !clicks.Placed, "release on another cell");
        Check(input.Press(0, 1, 2) && clicks.Cells[0].Mark == 1, "right click flags");
        Check(!input.Press(0, 0, 1) && !input.Release(0, 0, false) && !clicks.Placed, "flagged cell stays closed");
        Check(input.Press(0, 1, 2) && clicks.Cells[0].Mark == 2, "right click questions");
        Check(input.Press(0, 1, 2) && clicks.Cells[0].Mark == 0, "right click clears mark");
        Check(!input.Press(0, 0, 1) && !input.Press(0, 2, 5) && !input.Release(0, 0, false) && !clicks.Placed, "middle click suppresses reveal");
        Check(!input.Press(0, 0, 1) && !input.Press(0, 1, 3) && !input.Release(0, 0, true) && !input.Release(0, 1, false) && !clicks.Placed, "two-button chord suppresses reveal");
        Check(!input.Press(0, 2, 4) && !input.Press(0, 0, 5) && !input.Release(0, 2, true) && !input.Release(0, 0, false) && !clicks.Placed, "middle held before left suppresses reveal");
        Check(!input.Press(40, 0, 1) && input.Release(40, 0, false) && clicks.Placed && clicks.Cells[40].Open, "left click reveals");
        MinesweeperGame game = new MinesweeperGame(9, 9, 10, new Random(17));
        Check(game.Nearby(0).Count == 3 && game.Nearby(40).Count == 8, "neighbor bounds");
        Check(game.Toggle(0) && game.Cells[0].Mark == 1 && !game.Reveal(0), "flag prevents reveal");
        Check(game.Toggle(0) && game.Cells[0].Mark == 2 && game.Toggle(0) && game.Cells[0].Mark == 0, "mark cycle");
        Check(game.Reveal(40) && game.Placed && !game.Lost && game.Cells[40].Open, "first reveal");
        foreach (int neighbor in game.Nearby(40)) Check(!game.Cells[neighbor].Mine, "first reveal neighbors are safe");
        int mineCount = 0;
        foreach (MinesweeperGame.Cell cell in game.Cells) if (cell.Mine) mineCount++;
        Check(mineCount == 10, "mine count");
        int number = -1;
        for (int i = 0; i < game.Cells.Length; i++)
            if (game.Cells[i].Open && game.Cells[i].Adjacent > 0) { number = i; break; }
        Check(number >= 0, "revealed numbered edge");
        foreach (int neighbor in game.Nearby(number))
            if (game.Cells[neighbor].Mine) Check(game.Toggle(neighbor), "flag adjacent mine");
        Check(game.Chord(number) && !game.Lost, "correctly flagged chord");
        for (int i = 0; i < game.Cells.Length; i++)
            if (!game.Cells[i].Mine && !game.Cells[i].Open) game.Reveal(i);
        Check(game.Won && !game.Lost && game.Opened == 71, "winning board");
        for (int i = 0; i < game.Cells.Length; i++)
            if (game.Cells[i].Mine) Check(game.Cells[i].Mark == 1, "win marks mines");
        MinesweeperGame loss = new MinesweeperGame(9, 9, 10, new Random(19));
        Check(loss.Reveal(40), "prepare loss board");
        int mine = -1;
        for (int i = 0; i < loss.Cells.Length; i++) if (loss.Cells[i].Mine) { mine = i; break; }
        Check(mine >= 0 && loss.Reveal(mine) && loss.Lost && loss.Exploded == mine, "mine loses game");
        Check(!loss.Toggle(mine) && !loss.Reveal(mine), "lost board is locked");
        Console.WriteLine("Minesweeper board checks passed.");
    }
}
