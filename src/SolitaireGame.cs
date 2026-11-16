using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal enum SolitairePile { Stock, Waste, Foundation, Tableau }

    internal sealed class SolitaireCard
    {
        internal readonly int Suit;
        internal readonly int Rank;
        internal bool Up;
        internal bool Red { get { return Suit == 1 || Suit == 2; } }

        internal SolitaireCard(int suit, int rank, bool up)
        {
            Suit = suit;
            Rank = rank;
            Up = up;
        }

        internal SolitaireCard Copy() { return new SolitaireCard(Suit, Rank, Up); }
    }

    internal sealed class SolitaireGame
    {
        internal readonly int DrawCount;
        internal readonly List<SolitaireCard> Stock = new List<SolitaireCard>();
        internal readonly List<SolitaireCard> Waste = new List<SolitaireCard>();
        internal readonly List<SolitaireCard>[] Foundations = Piles(4);
        internal readonly List<SolitaireCard>[] Tableau = Piles(7);
        internal int Score { get; private set; }

        private SolitaireGame(int draw) { DrawCount = draw == 3 ? 3 : 1; }

        internal static SolitaireGame Deal(int draw, Random random)
        {
            if (random == null) throw new ArgumentNullException("random");
            SolitaireGame game = new SolitaireGame(draw);
            for (int suit = 0; suit < 4; suit++)
                for (int rank = 1; rank <= 13; rank++) game.Stock.Add(new SolitaireCard(suit, rank, false));
            for (int i = game.Stock.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                SolitaireCard swap = game.Stock[i];
                game.Stock[i] = game.Stock[j];
                game.Stock[j] = swap;
            }
            for (int pile = 0; pile < game.Tableau.Length; pile++)
            {
                for (int i = 0; i <= pile; i++)
                {
                    int last = game.Stock.Count - 1;
                    game.Tableau[pile].Add(game.Stock[last]);
                    game.Stock.RemoveAt(last);
                }
                game.Tableau[pile][pile].Up = true;
            }
            return game;
        }

        internal SolitaireGame Copy()
        {
            SolitaireGame copy = new SolitaireGame(DrawCount);
            copy.Score = Score;
            CopyInto(Stock, copy.Stock);
            CopyInto(Waste, copy.Waste);
            for (int i = 0; i < Foundations.Length; i++) CopyInto(Foundations[i], copy.Foundations[i]);
            for (int i = 0; i < Tableau.Length; i++) CopyInto(Tableau[i], copy.Tableau[i]);
            return copy;
        }

        internal bool Draw()
        {
            if (Stock.Count > 0)
            {
                int count = Math.Min(DrawCount, Stock.Count);
                for (int i = 0; i < count; i++)
                {
                    int last = Stock.Count - 1;
                    SolitaireCard card = Stock[last];
                    Stock.RemoveAt(last);
                    card.Up = true;
                    Waste.Add(card);
                }
                return true;
            }
            if (Waste.Count == 0) return false;
            while (Waste.Count > 0)
            {
                int last = Waste.Count - 1;
                SolitaireCard card = Waste[last];
                Waste.RemoveAt(last);
                card.Up = false;
                Stock.Add(card);
            }
            return true;
        }

        internal bool Move(SolitairePile from, int pile, int at, SolitairePile to, int destination)
        {
            List<SolitaireCard> source = Source(from, pile);
            List<SolitaireCard> target = Target(to, destination);
            if (source == null || target == null || source == target || source.Count == 0) return false;
            int start = from == SolitairePile.Tableau ? at : source.Count - 1;
            if (start < 0 || start >= source.Count || !ValidRun(source, start)) return false;
            int count = source.Count - start;
            SolitaireCard card = source[start];
            if (to == SolitairePile.Foundation)
            {
                if (count != 1 || !LegalFoundation(card, target)) return false;
            }
            else if (!LegalTableau(card, target)) return false;
            target.AddRange(source.GetRange(start, count));
            source.RemoveRange(start, count);
            if (from == SolitairePile.Tableau && source.Count > 0 && !source[source.Count - 1].Up)
            {
                source[source.Count - 1].Up = true;
                Gain(5);
            }
            if (from == SolitairePile.Waste) Gain(to == SolitairePile.Foundation ? 10 : 5);
            else if (from == SolitairePile.Tableau && to == SolitairePile.Foundation) Gain(10);
            else if (from == SolitairePile.Foundation) Gain(-15);
            return true;
        }

        internal bool ToFoundation(SolitairePile from, int pile, int at)
        {
            for (int i = 0; i < Foundations.Length; i++)
                if (Move(from, pile, at, SolitairePile.Foundation, i)) return true;
            return false;
        }

        internal bool Won
        {
            get
            {
                int count = 0;
                foreach (List<SolitaireCard> pile in Foundations) count += pile.Count;
                return count == 52;
            }
        }

        internal bool CanFinish
        {
            get
            {
                if (Stock.Count != 0 || Waste.Count != 0) return false;
                foreach (List<SolitaireCard> pile in Tableau)
                    foreach (SolitaireCard card in pile) if (!card.Up) return false;
                return true;
            }
        }

        internal bool Step()
        {
            if (ToFoundation(SolitairePile.Waste, 0, 0)) return true;
            for (int i = 0; i < Tableau.Length; i++)
                if (Tableau[i].Count > 0 && ToFoundation(SolitairePile.Tableau, i, Tableau[i].Count - 1)) return true;
            return false;
        }

        private List<SolitaireCard> Source(SolitairePile kind, int pile)
        {
            if (kind == SolitairePile.Waste) return Waste;
            if (kind == SolitairePile.Foundation && pile >= 0 && pile < Foundations.Length) return Foundations[pile];
            if (kind == SolitairePile.Tableau && pile >= 0 && pile < Tableau.Length) return Tableau[pile];
            return null;
        }

        private List<SolitaireCard> Target(SolitairePile kind, int pile)
        {
            if (kind == SolitairePile.Foundation && pile >= 0 && pile < Foundations.Length) return Foundations[pile];
            if (kind == SolitairePile.Tableau && pile >= 0 && pile < Tableau.Length) return Tableau[pile];
            return null;
        }

        private static bool LegalFoundation(SolitaireCard card, List<SolitaireCard> pile)
        {
            if (pile.Count == 0) return card.Rank == 1;
            SolitaireCard top = pile[pile.Count - 1];
            return top.Suit == card.Suit && top.Rank == card.Rank - 1;
        }

        private static bool LegalTableau(SolitaireCard card, List<SolitaireCard> pile)
        {
            if (pile.Count == 0) return card.Rank == 13;
            SolitaireCard top = pile[pile.Count - 1];
            return top.Up && top.Rank == card.Rank + 1 && top.Red != card.Red;
        }

        private static bool ValidRun(List<SolitaireCard> pile, int start)
        {
            for (int i = start; i < pile.Count; i++)
            {
                if (!pile[i].Up) return false;
                if (i > start && (pile[i - 1].Rank != pile[i].Rank + 1 || pile[i - 1].Red == pile[i].Red)) return false;
            }
            return true;
        }

        private void Gain(int points) { Score = Math.Max(0, Score + points); }

        private static List<SolitaireCard>[] Piles(int count)
        {
            List<SolitaireCard>[] piles = new List<SolitaireCard>[count];
            for (int i = 0; i < count; i++) piles[i] = new List<SolitaireCard>();
            return piles;
        }

        private static void CopyInto(List<SolitaireCard> source, List<SolitaireCard> destination)
        {
            foreach (SolitaireCard card in source) destination.Add(card.Copy());
        }
    }
}
