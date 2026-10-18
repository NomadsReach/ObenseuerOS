using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class LottoBridge
    {
        private const string AppId = "lotto";
        internal const int PageSize = 25;
        internal const int ResultDraws = 30;
        private static readonly Random Picker = new Random();

        internal static void Register()
        {
            Os.Handle(AppId, "lotto.state", State);
            Os.Handle(AppId, "lotto.quick", QuickPick);
            Os.Handle(AppId, "lotto.buy", Buy);
            Os.Handle(AppId, "lotto.tickets", Tickets);
            Os.Handle(AppId, "lotto.results", Results);
            Os.Handle(AppId, "scratch.buy", ScratchBuy);
            Os.Handle(AppId, "scratch.reveal", ScratchReveal);
            Os.Handle(AppId, "scratch.history", ScratchHistory);
        }

        internal static string ScratchHistoryJson(List<ScratchRec> cards, int today, float rm)
        {
            List<object> rows = new List<object>();
            for (int i = cards.Count - 1; i >= 0; i--) rows.Add(CardJson(cards[i]));
            List<object> kinds = new List<object> { KindJson(LottoScratch.Oc), KindJson(LottoScratch.Spade) };
            return JsonConvert.SerializeObject(new { cards = rows, today = today, perDay = LottoScratch.MaxPerDay, kinds = kinds, rm = rm });
        }

        internal static string StateJson(int oc, int price, int bought, int draw, int now, DrawRec last, int owed)
        {
            int closesIn = LottoGame.CloseMinute(draw) - now;
            int drawsIn = LottoGame.DueMinute(draw) - now;
            return JsonConvert.SerializeObject(new { oc = oc, price = price, maxPerDraw = LottoGame.MaxPerDraw, maxPerBuy = LottoGame.MaxPerBuy, boughtForNext = bought, clockMinute = now % LottoGame.MinutesPerDay, nextDraw = new { n = draw, salesOpen = closesIn > 0, closesIn = closesIn, drawsIn = drawsIn }, last = last == null ? null : DrawJson(last), owedOc = owed });
        }

        internal static string ReceiptJson(PurchaseRec rec, int balance)
        {
            List<object> tickets = new List<object>();
            for (int i = 0; i < rec.TicketIds.Count; i++)
            {
                tickets.Add(new { id = rec.TicketIds[i], draw = rec.Draw, nums = rec.Nums.GetRange(i * LottoDraw.PickCount, LottoDraw.PickCount) });
            }
            return JsonConvert.SerializeObject(new { tickets = tickets, paidOc = rec.Oc, balance = balance });
        }

        internal static string TicketsJson(List<LottoTicket> shown, int total, int page)
        {
            List<object> rows = new List<object>();
            foreach (LottoTicket ticket in shown) rows.Add(TicketJson(ticket));
            return JsonConvert.SerializeObject(new { tickets = rows, total = total, page = page });
        }

        internal static string ResultsJson(List<DrawRec> draws)
        {
            List<object> rows = new List<object>();
            foreach (DrawRec draw in draws) rows.Add(DrawJson(draw));
            return JsonConvert.SerializeObject(new { draws = rows });
        }

        internal static string LinesJson(List<List<int>> lines)
        {
            return JsonConvert.SerializeObject(new { lines = lines });
        }

        private static string State(string args)
        {
            OsBridge.Args(args);
            LottoSave state = Begin();
            int draw = state.NextDraw;
            DrawRec last = state.Draws.Count == 0 ? null : state.Draws[state.Draws.Count - 1];
            return StateJson(Balance(), LottoGame.TicketPrice, LottoGame.CountFor(state, draw), draw, LottoGame.Minutes(WorldClock.Now), last, LottoGame.OwedOc(state));
        }

        private static string QuickPick(string args)
        {
            OsBridge.Args(args);
            return LinesJson(new List<List<int>> { LottoDraw.Numbers(Picker.Next(), 0) });
        }

        private static string Buy(string args)
        {
            JObject a = OsBridge.Args(args);
            string cid = Cid(a);
            List<List<int>> lines = Lines(a);
            string print = LottoGame.Print(lines);
            LottoSave state = Begin();
            PurchaseRec saved = LottoGame.Saved(state, cid, print);
            if (saved != null) return ReceiptJson(saved, Balance());
            OsBridge.CheckWritable();
            return ReceiptJson(LottoGame.Buy(state, lines, cid, print, LottoGame.TicketPrice, LottoGame.Minutes(WorldClock.Now)), Balance());
        }

        private static string Tickets(string args)
        {
            JObject a = OsBridge.Args(args);
            string scope = OsBridge.Text(a, "scope");
            if (scope != "open" && scope != "done") throw new OsBridgeException("bad_args", "scope must be open or done");
            int page = OsBridge.Span(a, "page", int.MaxValue, 0);
            LottoSave state = Begin();
            List<LottoTicket> matching = new List<LottoTicket>();
            for (int i = state.Tickets.Count - 1; i >= 0; i--)
            {
                LottoTicket ticket = state.Tickets[i];
                if ((scope == "open") == (ticket.State == LottoGame.Open)) matching.Add(ticket);
            }
            int from = page > matching.Count / PageSize ? matching.Count : page * PageSize;
            int to = Math.Min(from + PageSize, matching.Count);
            return TicketsJson(matching.GetRange(from, to - from), matching.Count, page);
        }

        private static string Results(string args)
        {
            OsBridge.Args(args);
            LottoSave state = Begin();
            List<DrawRec> recent = new List<DrawRec>();
            for (int i = state.Draws.Count - 1; i >= 0 && recent.Count < ResultDraws; i--) recent.Add(state.Draws[i]);
            return ResultsJson(recent);
        }

        private static string ScratchBuy(string args)
        {
            JObject a = OsBridge.Args(args);
            string cid = Cid(a);
            ScratchKind kind = KindArg(a);
            bool ship = OsBridge.Flag(a, "ship");
            string print = kind.Key + (ship ? ":ship" : ":wallet");
            LottoSave state = Begin();
            ScratchRec rec = LottoScratch.Saved(state, cid, print);
            if (rec == null)
            {
                OsBridge.CheckWritable();
                rec = LottoScratch.Buy(state, kind, ship, cid, print, WorldClock.Now.Day);
            }
            return CardReply(rec);
        }

        private static string ScratchReveal(string args)
        {
            JObject a = OsBridge.Args(args);
            int id = OsBridge.Span(a, "id", int.MaxValue, 0);
            LottoSave state = Begin();
            OsBridge.CheckWritable();
            return CardReply(LottoScratch.Reveal(state, id));
        }

        private static string ScratchHistory(string args)
        {
            OsBridge.Args(args);
            LottoSave state = Begin();
            return ScratchHistoryJson(state.Cards, state.ScratchDay == WorldClock.Now.Day ? state.ScratchToday : 0, Cash.Balance(MoneyCurrency.RM));
        }

        private static ScratchKind KindArg(JObject a)
        {
            ScratchKind kind = LottoScratch.KindByKey(OsBridge.Text(a, "kind"));
            if (kind == null) throw new OsBridgeException("bad_args", "kind must be oc or spade");
            return kind;
        }

        private static string CardReply(ScratchRec rec)
        {
            return JsonConvert.SerializeObject(new { card = CardJson(rec), balance = Balance(), rm = Cash.Balance(MoneyCurrency.RM) });
        }

        private static object CardJson(ScratchRec rec)
        {
            ScratchKind kind = LottoScratch.KindOf(rec.ItemId);
            return new { id = rec.Id, kind = kind.Key, day = rec.Day, paidOc = rec.Paid, shipped = rec.Shipped, revealed = rec.Revealed, prize = rec.Revealed ? (object)rec.Prize : null, currency = kind.Currency.ToString() };
        }

        private static object KindJson(ScratchKind kind)
        {
            int ship = LottoScratch.ShipTotal(kind);
            return new { kind = kind.Key, name = kind.Name, price = kind.Price, shipTotal = ship > 0 ? (object)ship : null, top = kind.Top, currency = kind.Currency.ToString() };
        }

        private static LottoSave Begin()
        {
            if (!Cash.IsAvailable || !WorldClock.IsAvailable) throw new OsBridgeException("failed", "The lottery is not ready yet.");
            LottoSave state = LottoGame.Current;
            if (state.Seed == 0)
            {
                OsBridge.CheckWritable();
                LottoGame.Start(state, LottoGame.Minutes(WorldClock.Now));
            }
            if (OsBridge.Writable()) LottoGame.Settle(state, LottoGame.Minutes(WorldClock.Now));
            return state;
        }

        private static int Balance()
        {
            return (int)Cash.Balance(MoneyCurrency.OC);
        }

        private static string Cid(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > LottoGame.MaxCid) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }

        private static List<List<int>> Lines(JObject a)
        {
            JArray rows = a["lines"] as JArray;
            if (rows == null || rows.Count < 1 || rows.Count > LottoGame.MaxPerBuy) throw new OsBridgeException("bad_args", "lines must hold 1 to 5 lines");
            List<List<int>> lines = new List<List<int>>();
            foreach (JToken row in rows) lines.Add(Line(row as JArray));
            return lines;
        }

        private static List<int> Line(JArray row)
        {
            if (row == null || row.Count != LottoDraw.PickCount) throw new OsBridgeException("bad_args", "each line needs 6 numbers");
            List<int> nums = new List<int>();
            foreach (JToken token in row)
            {
                int n;
                if (token.Type != JTokenType.Integer || !int.TryParse(token.ToString(Formatting.None), out n) || n < 1 || n > LottoDraw.Pool || nums.Contains(n)) throw new OsBridgeException("bad_args", "each line needs 6 different numbers from 1 to 40");
                nums.Add(n);
            }
            nums.Sort();
            return nums;
        }

        private static object TicketJson(LottoTicket ticket)
        {
            bool open = ticket.State == LottoGame.Open;
            return new { id = ticket.Id, draw = ticket.Draw, nums = ticket.Nums, state = ticket.State, matches = open ? (object)null : ticket.Matches, oc = ticket.Oc };
        }

        private static object DrawJson(DrawRec draw)
        {
            return new { n = draw.N, nums = draw.Nums, winners = draw.Winners, paidOc = draw.PaidOc, late = draw.Late };
        }
    }
}
