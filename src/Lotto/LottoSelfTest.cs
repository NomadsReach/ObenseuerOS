using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace ObenseuerOS
{
    internal static class LottoSelfTest
    {
        private const int Seed = 4242;

        internal static string Run()
        {
            if (Choose(LottoDraw.Pool, LottoDraw.PickCount) != 3838380) return "ticket count";
            long sum = 0;
            for (int m = 0; m <= LottoDraw.PickCount; m++) sum += Count(m);
            if (sum != 3838380) return "match counts";
            double rtp = Rtp(LottoGame.TicketPrice);
            if (rtp >= 1 || Math.Abs(rtp - 0.5066) > 0.00005) return "table return";
            if (!NumbersOk()) return "draw numbers";
            if (!StartOk()) return "first draw";
            if (!CatchUpOk()) return "catch-up";
            if (!ReplayOk()) return "cid replay";
            foreach (string json in WorstCase())
            {
                if (Encoding.UTF8.GetByteCount(json) > OsBridge.MaxResultBytes) return "result over 12 KB";
            }
            return LottoScratchSelfTest.Run();
        }

        internal static List<string> WorstCase()
        {
            List<LottoTicket> page = new List<LottoTicket>();
            for (int i = 0; i < LottoBridge.PageSize; i++) page.Add(Ticket(int.MaxValue, LottoGame.Lost, 6, 50000));
            List<DrawRec> draws = new List<DrawRec>();
            for (int i = 0; i < LottoBridge.ResultDraws; i++) draws.Add(Draw(int.MaxValue, 1000000));
            PurchaseRec purchase = new PurchaseRec();
            purchase.Draw = int.MaxValue;
            purchase.Oc = 1000000;
            List<List<int>> lines = new List<List<int>>();
            for (int i = 0; i < LottoGame.MaxPerBuy; i++)
            {
                purchase.TicketIds.Add(int.MaxValue);
                purchase.Nums.AddRange(Forty());
                lines.Add(Forty());
            }
            return new List<string>
            {
                LottoBridge.TicketsJson(page, int.MaxValue, int.MaxValue),
                LottoBridge.ResultsJson(draws),
                LottoBridge.ReceiptJson(purchase, int.MaxValue),
                LottoBridge.StateJson(int.MaxValue, int.MaxValue, LottoGame.MaxPerDraw, int.MaxValue, 0, Draw(int.MaxValue, 1000000), int.MaxValue),
                LottoBridge.LinesJson(lines),
            };
        }

        private static bool NumbersOk()
        {
            string previous = null;
            for (int n = 0; n < 20; n++)
            {
                List<int> nums = LottoDraw.Numbers(Seed, n);
                if (nums.Count != LottoDraw.PickCount) return false;
                for (int i = 0; i < nums.Count; i++)
                {
                    if (nums[i] < 1 || nums[i] > LottoDraw.Pool) return false;
                    if (i > 0 && nums[i] <= nums[i - 1]) return false;
                }
                string key = string.Join(",", nums);
                if (key == previous || key != string.Join(",", LottoDraw.Numbers(Seed, n))) return false;
                previous = key;
            }
            return true;
        }

        private static bool StartOk()
        {
            LottoSave early = new LottoSave();
            LottoGame.Start(early, 5 * 1440 + 1199);
            int seed = early.Seed;
            LottoGame.Start(early, 5 * 1440 + 1250);
            LottoSave late = new LottoSave();
            LottoGame.Start(late, 5 * 1440 + 1200);
            return seed != 0 && early.Seed == seed && early.NextDraw == 5 && late.NextDraw == 6;
        }

        private static bool CatchUpOk()
        {
            LottoSave step = Fixture();
            LottoSave once = Fixture();
            for (int d = 0; d < 6; d++) LottoGame.Settle(step, LottoGame.DueMinute(d));
            LottoGame.Settle(once, LottoGame.DueMinute(5));
            foreach (DrawRec draw in step.Draws) draw.Late = false;
            foreach (DrawRec draw in once.Draws) draw.Late = false;
            return step.NextDraw == 6 && once.NextDraw == 6 && step.Owed.Count >= 6 && JsonConvert.SerializeObject(step) == JsonConvert.SerializeObject(once);
        }

        private static bool ReplayOk()
        {
            List<List<int>> lines = new List<List<int>>();
            lines.Add(new List<int> { 1, 2, 3, 4, 5, 6 });
            lines.Add(new List<int> { 7, 8, 9, 10, 11, 12 });
            List<List<int>> swapped = new List<List<int>>();
            swapped.Add(lines[1]);
            swapped.Add(lines[0]);
            if (LottoGame.Print(lines) != LottoGame.Print(swapped)) return false;
            LottoSave state = new LottoSave();
            PurchaseRec rec = new PurchaseRec();
            rec.Cid = "c1";
            rec.Print = LottoGame.Print(lines);
            state.Purchases.Add(rec);
            if (LottoGame.Saved(state, "c1", rec.Print) != rec || LottoGame.Saved(state, "c2", rec.Print) != null) return false;
            try
            {
                LottoGame.Saved(state, "c1", LottoGame.Print(new List<List<int>> { new List<int> { 1, 2, 3, 4, 5, 7 } }));
                return false;
            }
            catch (OsBridgeException e)
            {
                return e.Code == "bad_args";
            }
        }

        private static LottoSave Fixture()
        {
            LottoSave state = new LottoSave();
            state.Seed = Seed;
            for (int d = 0; d < 6; d++)
            {
                Add(state, d, LottoDraw.Numbers(Seed, d));
                Add(state, d, new List<int> { 1, 2, 3, 4, 5, 6 });
            }
            return state;
        }

        private static void Add(LottoSave state, int draw, List<int> nums)
        {
            LottoTicket ticket = new LottoTicket();
            ticket.Id = ++state.NextTicket;
            ticket.Draw = draw;
            ticket.Nums = nums;
            ticket.State = LottoGame.Open;
            state.Tickets.Add(ticket);
        }

        private static LottoTicket Ticket(int id, string state, int matches, int oc)
        {
            LottoTicket ticket = new LottoTicket();
            ticket.Id = id;
            ticket.Draw = id;
            ticket.Nums = Forty();
            ticket.State = state;
            ticket.Matches = matches;
            ticket.Oc = oc;
            return ticket;
        }

        private static DrawRec Draw(int n, int paid)
        {
            DrawRec draw = new DrawRec();
            draw.N = n;
            draw.Nums = Forty();
            draw.Winners = new List<int> { paid, paid, paid, paid };
            draw.PaidOc = paid;
            draw.Late = true;
            return draw;
        }

        private static List<int> Forty()
        {
            return new List<int> { 40, 40, 40, 40, 40, 40 };
        }

        private static long Count(int matches)
        {
            return Choose(LottoDraw.PickCount, matches) * Choose(LottoDraw.Pool - LottoDraw.PickCount, LottoDraw.PickCount - matches);
        }

        private static long Choose(int n, int k)
        {
            long c = 1;
            for (int i = 1; i <= k; i++) c = c * (n - k + i) / i;
            return c;
        }

        private static double Rtp(int price)
        {
            double ev = 0;
            for (int m = LottoDraw.MinWin; m <= LottoDraw.PickCount; m++) ev += (double)Count(m) * LottoDraw.Prize(m);
            return ev / Choose(LottoDraw.Pool, LottoDraw.PickCount) / price;
        }
    }
}
