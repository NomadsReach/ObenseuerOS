using System;
using System.Collections.Generic;
using System.Text;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class LottoScratchSelfTest
    {
        private const int Grid = 100000;

        internal static string Run()
        {
            foreach (ScratchKind kind in new[] { LottoScratch.Oc, LottoScratch.Spade })
            {
                string error = Odds(kind);
                if (error != null) return kind.Key + " " + error;
            }
            if (!PruneOk()) return "scratch prune";
            if (Encoding.UTF8.GetByteCount(LottoBridge.ScratchHistoryJson(WorstCards(), int.MaxValue, float.MaxValue)) > OsBridge.MaxResultBytes) return "scratch history over 12 KB";
            return null;
        }

        private static string Odds(ScratchKind kind)
        {
            if (LottoScratch.Prize(kind, 0) != kind.Top) return "top prize";
            if (LottoScratch.Prize(kind, 0.25) != 0) return "zero share";
            if (LottoScratch.Prize(kind, 0.2499) != 1) return "lowest win";
            int wins = 0;
            long sum = 0;
            int last = int.MaxValue;
            for (int i = 0; i < Grid; i++)
            {
                int prize = LottoScratch.Prize(kind, (i + 0.5) / Grid);
                if (prize > last) return "not monotone";
                last = prize;
                if (prize > 0) wins++;
                sum += prize;
            }
            if (Math.Abs((double)wins / Grid - 0.25) > 0.001) return "win share";
            if (Math.Abs((double)sum / Grid - Expected(kind)) > 0.25) return "expected prize";
            if ((double)sum / Grid * (kind.Currency == MoneyCurrency.RM ? Cash.RmToOc(1f) : 1) > kind.Price * 0.9) return "return over 90 percent";
            return null;
        }

        private static double Expected(ScratchKind kind)
        {
            double band = (1 - kind.ZeroShare - kind.TopChance) / (1 - kind.ZeroShare);
            double share = Math.Log(kind.Curve * band + 1) / kind.Curve - band * band / (2 * (kind.Curve + 1));
            return kind.TopChance * kind.Top + (1 - kind.ZeroShare) * kind.Top * share;
        }

        private static bool PruneOk()
        {
            LottoSave state = new LottoSave();
            for (int i = 1; i <= 30; i++) state.Cards.Add(Card(i, i > 5));
            LottoScratch.Prune(state);
            return state.Cards.Count == 25 && LottoScratch.OpenCount(state) == 5 && state.Cards[0].Id == 1;
        }

        private static ScratchRec Card(int id, bool revealed)
        {
            return new ScratchRec { Id = id, ItemId = LottoScratch.Oc.ItemId, Revealed = revealed, Cid = "c" + id };
        }

        private static List<ScratchRec> WorstCards()
        {
            List<ScratchRec> cards = new List<ScratchRec>();
            for (int i = 0; i < LottoScratch.MaxHistory * 2; i++)
            {
                cards.Add(new ScratchRec { Id = int.MaxValue, ItemId = LottoScratch.Spade.ItemId, Day = int.MaxValue, Paid = int.MaxValue, Shipped = i % 2 == 0, Revealed = true, Prize = LottoScratch.Spade.Top });
            }
            return cards;
        }
    }
}
