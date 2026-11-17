using System;
using System.Collections.Generic;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Main()
    {
        SolitaireGame game = SolitaireGame.Deal(3, new Random(42));
        HashSet<string> cards = new HashSet<string>();
        foreach (SolitaireCard card in game.Stock) Check(cards.Add(card.Suit + ":" + card.Rank) && !card.Up, "stock");
        for (int i = 0; i < game.Tableau.Length; i++)
        {
            Check(game.Tableau[i].Count == i + 1, "deal pile length");
            for (int j = 0; j < game.Tableau[i].Count; j++)
            {
                SolitaireCard card = game.Tableau[i][j];
                Check(cards.Add(card.Suit + ":" + card.Rank) && card.Up == (j == i), "deal cards");
            }
        }
        Check(cards.Count == 52 && game.Stock.Count == 24, "complete deck");
        SolitaireGame snapshot = game.Copy();
        Check(game.Draw() && game.Stock.Count == 21 && game.Waste.Count == 3, "draw three");
        Check(snapshot.Stock.Count == 24 && snapshot.Waste.Count == 0, "copy isolated");
        while (game.Stock.Count > 0) Check(game.Draw(), "drain stock");
        SolitaireCard first = game.Waste[0];
        Check(game.Draw() && game.Stock.Count == 24 && game.Waste.Count == 0 && !first.Up, "recycle stock");
        Check(game.Draw() && game.Waste[0].Suit == first.Suit && game.Waste[0].Rank == first.Rank, "recycle order");

        game = SolitaireGame.Deal(1, new Random(1));
        game.Stock.Clear();
        game.Waste.Clear();
        foreach (List<SolitaireCard> pile in game.Tableau) pile.Clear();
        game.Tableau[0].Add(new SolitaireCard(0, 4, false));
        game.Tableau[0].Add(new SolitaireCard(0, 13, true));
        game.Tableau[0].Add(new SolitaireCard(1, 12, true));
        Check(!game.CanFinish && !game.Won, "face-down cards prevent auto-finish");
        Check(!game.Move(SolitairePile.Tableau, 0, 2, SolitairePile.Tableau, 1), "only king on empty tableau");
        Check(game.Move(SolitairePile.Tableau, 0, 1, SolitairePile.Tableau, 1), "move alternating run");
        Check(game.Tableau[0][0].Up && game.Score == 5 && game.Tableau[1].Count == 2, "reveal and score");
        game.Waste.Add(new SolitaireCard(2, 1, true));
        Check(game.ToFoundation(SolitairePile.Waste, 0, 0) && game.Score == 15, "ace to foundation");
        game.Tableau[2].Add(new SolitaireCard(0, 2, true));
        Check(game.Move(SolitairePile.Foundation, 0, 0, SolitairePile.Tableau, 2) && game.Score == 0, "foundation penalty");
        Check(!game.Move(SolitairePile.Tableau, 2, 1, SolitairePile.Tableau, 2), "same pile refused");
        Check(!game.Move(SolitairePile.Tableau, 8, 0, SolitairePile.Foundation, 0), "bad source refused");
        Check(game.CanFinish && game.Step() && game.Foundations[0].Count == 1 && !game.Won, "auto-finish moves available ace");
        foreach (List<SolitaireCard> pile in game.Foundations) pile.Clear();
        for (int suit = 0; suit < 4; suit++)
            for (int rank = 1; rank <= 13; rank++) game.Foundations[suit].Add(new SolitaireCard(suit, rank, true));
        Check(game.Won, "all foundations win");
        Console.WriteLine("Solitaire rules checks passed.");
    }
}
