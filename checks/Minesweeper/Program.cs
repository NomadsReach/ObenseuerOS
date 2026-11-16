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
