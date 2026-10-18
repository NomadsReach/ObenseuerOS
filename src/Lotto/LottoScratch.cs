using System;
using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class ScratchKind
    {
        internal string Key;
        internal string Name;
        internal int ItemId;
        internal int Top;
        internal double Curve;
        internal double TopChance;
        internal double ZeroShare;
        internal MoneyCurrency Currency;
        internal int Price;
    }

    internal static class LottoScratch
    {
        internal const int MaxPerDay = 20;
        internal const int MaxOpen = 20;
        internal const int MaxHistory = 20;
        internal const string ConflictText = "This card was already bought with a different kind or delivery.";
        internal static readonly ScratchKind Oc = new ScratchKind { Key = "oc", Name = "OC Pot", ItemId = 17740, Price = 45, Top = 50000, Curve = 3499, TopChance = 0.00025, ZeroShare = 0.75, Currency = MoneyCurrency.OC };
        internal static readonly ScratchKind Spade = new ScratchKind { Key = "spade", Name = "Spade of Aces", ItemId = 17760, Price = 84, Top = 10000, Curve = 3799, TopChance = 0.00025, ZeroShare = 0.75, Currency = MoneyCurrency.RM };
        private static readonly Random Roller = new Random();

        internal static ScratchKind KindByKey(string key)
        {
            if (key == Oc.Key) return Oc;
            if (key == Spade.Key) return Spade;
            return null;
        }

        internal static ScratchKind KindOf(int itemId)
        {
            return itemId == Spade.ItemId ? Spade : Oc;
        }

        internal static int Prize(ScratchKind kind, double u)
        {
            if (u <= kind.TopChance) return kind.Top;
            if (u >= 1 - kind.ZeroShare) return 0;
            double x = (u - kind.TopChance) / (1 - kind.ZeroShare);
            double share = 1 / (kind.Curve * x + 1) - x / (kind.Curve + 1);
            return Math.Max(1, (int)(share * kind.Top));
        }

        internal static int ShipTotal(ScratchKind kind)
        {
            ShopDelivery tier = DeliveryTier();
            int price = kind.Price;
            return tier == null || price < 1 ? 0 : price + (int)ShopOrders.Fee(price, tier.Flat, tier.Pct);
        }

        internal static ScratchRec Saved(LottoSave state, string cid, string print)
        {
            foreach (ScratchRec rec in state.Cards)
            {
                if (rec.Cid != cid) continue;
                if (rec.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return rec;
            }
            return null;
        }

        internal static ScratchRec Find(LottoSave state, int id)
        {
            foreach (ScratchRec rec in state.Cards)
            {
                if (rec.Id == id) return rec;
            }
            return null;
        }

        internal static int OpenCount(LottoSave state)
        {
            int open = 0;
            foreach (ScratchRec rec in state.Cards)
            {
                if (!rec.Revealed && !rec.Shipped) open++;
            }
            return open;
        }

        internal static ScratchRec Buy(LottoSave state, ScratchKind kind, bool ship, string cid, string print, int day)
        {
            if (state.ScratchDay != day)
            {
                state.ScratchDay = day;
                state.ScratchToday = 0;
            }
            if (state.ScratchToday >= MaxPerDay) throw new OsBridgeException("denied", "You have reached today's limit of " + MaxPerDay + " cards.");
            if (OpenCount(state) >= MaxOpen) throw new OsBridgeException("denied", "Scratch your open cards before you buy more.");
            ScratchRec rec = new ScratchRec();
            rec.Id = state.NextCard + 1;
            rec.ItemId = kind.ItemId;
            rec.Day = day;
            rec.Shipped = ship;
            rec.Cid = cid;
            rec.Print = print;
            if (ship)
            {
                rec.Paid = Ship(kind, cid, print);
            }
            else
            {
                rec.Paid = kind.Price;
                if (rec.Paid < 1) throw new OsBridgeException("failed", "The card price is not ready yet.");
                if (!Cash.Has(MoneyCurrency.OC, rec.Paid)) throw new OsBridgeException("denied", "Not enough OC for this card.");
                if (!Cash.TrySpend(MoneyCurrency.OC, rec.Paid)) throw new OsBridgeException("failed", "The payment was refused. Nothing was spent.");
                rec.Prize = Prize(kind, Roller.NextDouble());
            }
            state.NextCard = rec.Id;
            state.ScratchToday++;
            state.Cards.Add(rec);
            Prune(state);
            return rec;
        }

        internal static ScratchRec Reveal(LottoSave state, int id)
        {
            ScratchRec rec = Find(state, id);
            if (rec == null) throw new OsBridgeException("bad_args", "There is no card with that number.");
            if (rec.Shipped) throw new OsBridgeException("denied", "This card was shipped. Scratch it in the game.");
            if (rec.Revealed) return rec;
            if (rec.Prize > 0 && !Cash.Give(KindOf(rec.ItemId).Currency, rec.Prize)) throw new OsBridgeException("failed", "The payout was refused. Try again in a moment.");
            rec.Revealed = true;
            return rec;
        }

        internal static void Prune(LottoSave state)
        {
            int settled = state.Cards.Count - OpenCount(state);
            for (int i = 0; i < state.Cards.Count && settled > MaxHistory; )
            {
                ScratchRec rec = state.Cards[i];
                if (rec.Revealed || rec.Shipped)
                {
                    state.Cards.RemoveAt(i);
                    settled--;
                }
                else i++;
            }
        }

        private static int Ship(ScratchKind kind, string cid, string print)
        {
            if (!Mail.IsAvailable) throw new OsBridgeException("denied", "Mail is not available right now. Nothing was charged.");
            ShopOrders.RetryRefunds();
            ShopDelivery tier = DeliveryTier();
            if (tier == null || Items.Get(kind.ItemId) == null) throw new OsBridgeException("failed", "Delivery is not available right now.");
            ShopQuote quote = new ShopQuote();
            quote.Tier = tier;
            quote.Pay = ShopOrders.WalletPay;
            quote.Minutes = ShopOrders.DeliveryMinutes(tier.Hours);
            ShopLine line = new ShopLine();
            line.ItemId = kind.ItemId;
            line.Qty = 1;
            quote.Lines.Add(line);
            ShopOrders.Totals(quote, kind.Price, 0, 0, tier);
            if (!quote.Ok) throw new OsBridgeException("failed", quote.Blocker);
            if (!Cash.Has(MoneyCurrency.OC, quote.TotalOc)) throw new OsBridgeException("denied", "Not enough OC for this card and its delivery.");
            ShopOrders.Place(quote, cid, print);
            return quote.TotalOc;
        }

        private static ShopDelivery DeliveryTier()
        {
            foreach (ShopDelivery tier in ShopCatalog.Tiers)
            {
                if (tier.Gate == null) return tier;
            }
            return null;
        }
    }
}
