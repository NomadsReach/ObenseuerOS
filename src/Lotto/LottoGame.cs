using System;
using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class LottoGame
    {
        internal const int TicketPrice = 16;
        internal const int MaxPerDraw = 20;
        internal const int MaxPerBuy = 5;
        internal const int MaxTickets = 200;
        internal const int MaxDraws = 30;
        internal const int MaxPurchases = 50;
        internal const int MaxCid = 64;
        internal const string Open = "open";
        internal const string Lost = "lost";
        internal const string Won = "won";
        internal const string ConflictText = "This ticket purchase was already made with different numbers.";
        private const string StoreId = "nomad.obenseuer.os.lotto";
        internal const int MinutesPerDay = 1440;
        private const int DrawMinute = 20 * 60;
        private const int SalesCloseMinute = 19 * 60 + 30;
        private const int LateMinutes = 60;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static LottoSave save = new LottoSave();

        internal static LottoSave Current
        {
            get
            {
                return save;
            }
        }

        internal static void Attach()
        {
            SaveData.Register<LottoSave>(StoreId, () => save, loaded => save = loaded ?? new LottoSave());
            Events.HourPassed += OnHour;
        }

        internal static int Minutes(WorldTime time)
        {
            return Minutes(time.Day, time.Hour, time.Minute);
        }

        private static int Minutes(int day, int hour, int minute)
        {
            return day * MinutesPerDay + hour * 60 + minute;
        }

        internal static int DueMinute(int draw)
        {
            return draw * MinutesPerDay + DrawMinute;
        }

        internal static int CloseMinute(int draw)
        {
            return draw * MinutesPerDay + SalesCloseMinute;
        }

        internal static void Start(LottoSave state, int now)
        {
            if (state.Seed != 0) return;
            state.Seed = new Random().Next(1, int.MaxValue);
            int day = now / MinutesPerDay;
            state.NextDraw = now % MinutesPerDay < DrawMinute ? day : day + 1;
        }

        internal static void Settle(LottoSave state, int now)
        {
            if (state.Seed == 0) return;
            RetryOwed(state);
            while (DueMinute(state.NextDraw) <= now) Resolve(state, now);
        }

        internal static int OwedOc(LottoSave state)
        {
            int total = 0;
            foreach (OwedRec rec in state.Owed) total += rec.Oc;
            return total;
        }

        internal static int CountFor(LottoSave state, int draw)
        {
            int count = 0;
            foreach (LottoTicket ticket in state.Tickets)
            {
                if (ticket.Draw == draw) count++;
            }
            return count;
        }

        internal static string Print(List<List<int>> lines)
        {
            List<string> parts = new List<string>();
            foreach (List<int> line in lines) parts.Add(string.Join(",", line));
            parts.Sort((a, b) => string.CompareOrdinal(a, b));
            return string.Join(";", parts);
        }

        internal static PurchaseRec Saved(LottoSave state, string cid, string print)
        {
            foreach (PurchaseRec rec in state.Purchases)
            {
                if (rec.Cid != cid) continue;
                if (rec.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return rec;
            }
            return null;
        }

        internal static PurchaseRec Buy(LottoSave state, List<List<int>> lines, string cid, string print, int price, int now)
        {
            Settle(state, now);
            int draw = state.NextDraw;
            if (now >= CloseMinute(draw)) throw new OsBridgeException("denied", "Sales for this draw are closed.");
            if (CountFor(state, draw) + lines.Count > MaxPerDraw) throw new OsBridgeException("denied", "This draw has reached the ticket limit.");
            long cost = (long)price * lines.Count;
            if (price < 1 || cost > int.MaxValue) throw new OsBridgeException("failed", "The ticket price is not valid.");
            int total = (int)cost;
            if (!Cash.Has(MoneyCurrency.OC, total)) throw new OsBridgeException("denied", "Not enough OC for these tickets.");
            if (!Cash.TrySpend(MoneyCurrency.OC, total)) throw new OsBridgeException("failed", "The payment was refused. Nothing was spent.");
            return Record(state, lines, cid, print, draw, total);
        }

        private static PurchaseRec Record(LottoSave state, List<List<int>> lines, string cid, string print, int draw, int total)
        {
            PurchaseRec rec = new PurchaseRec();
            rec.Cid = cid;
            rec.Print = print;
            rec.Draw = draw;
            rec.Oc = total;
            List<LottoTicket> fresh = new List<LottoTicket>();
            foreach (List<int> line in lines)
            {
                LottoTicket ticket = new LottoTicket();
                ticket.Id = state.NextTicket + fresh.Count + 1;
                ticket.Draw = draw;
                ticket.Nums = line;
                ticket.State = Open;
                fresh.Add(ticket);
                rec.TicketIds.Add(ticket.Id);
                rec.Nums.AddRange(line);
            }
            int nextTicket = state.NextTicket;
            try
            {
                state.NextTicket += fresh.Count;
                state.Tickets.AddRange(fresh);
                state.Purchases.Add(rec);
                Prune(state);
            }
            catch (Exception e)
            {
                state.NextTicket = nextTicket;
                state.Tickets.RemoveAll(fresh.Contains);
                state.Purchases.Remove(rec);
                Log.Error("lotto purchase " + cid + " failed, refunding: " + e);
                string back = Pay(state, 0, total) ? "Your money was refunded." : "Your money is being returned.";
                throw new OsBridgeException("failed", "The tickets could not be recorded. " + back);
            }
            return rec;
        }

        private static void Resolve(LottoSave state, int now)
        {
            int n = state.NextDraw;
            List<int> nums = LottoDraw.Numbers(state.Seed, n);
            DrawRec rec = new DrawRec();
            rec.N = n;
            rec.Nums = nums;
            rec.Winners = new List<int> { 0, 0, 0, 0 };
            rec.Late = now - DueMinute(n) > LateMinutes;
            foreach (LottoTicket ticket in state.Tickets)
            {
                if (ticket.Draw != n || ticket.State != Open) continue;
                ticket.Matches = LottoDraw.Matches(ticket.Nums, nums);
                ticket.Oc = LottoDraw.Prize(ticket.Matches);
                ticket.State = ticket.Oc > 0 ? Won : Lost;
                if (ticket.Matches >= LottoDraw.MinWin) rec.Winners[ticket.Matches - LottoDraw.MinWin]++;
                if (ticket.Oc > 0)
                {
                    rec.PaidOc += ticket.Oc;
                    Pay(state, ticket.Id, ticket.Oc);
                }
            }
            state.Draws.Add(rec);
            state.NextDraw = n + 1;
            Prune(state);
        }

        private static bool Pay(LottoSave state, int ticket, int oc)
        {
            if (Cash.Give(MoneyCurrency.OC, oc)) return true;
            state.Owed.Add(new OwedRec { Ticket = ticket, Oc = oc });
            return false;
        }

        private static void RetryOwed(LottoSave state)
        {
            for (int i = state.Owed.Count - 1; i >= 0; i--)
            {
                if (Cash.Give(MoneyCurrency.OC, state.Owed[i].Oc)) state.Owed.RemoveAt(i);
            }
        }

        private static void Prune(LottoSave state)
        {
            for (int i = 0; i < state.Tickets.Count && state.Tickets.Count > MaxTickets; )
            {
                if (state.Tickets[i].State == Open) i++;
                else state.Tickets.RemoveAt(i);
            }
            while (state.Draws.Count > MaxDraws) state.Draws.RemoveAt(0);
            while (state.Purchases.Count > MaxPurchases) state.Purchases.RemoveAt(0);
        }

        private static void OnHour(int hour)
        {
            if (save.Seed == 0 || !OsBridge.Writable()) return;
            Settle(save, Minutes(WorldClock.Now.Day, hour, 0));
        }
    }
}
