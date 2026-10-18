using System;
using System.Globalization;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class MarketMath
    {
        public const int MaintenancePercent = 30;
        public const double MarginInterestPerDay = 0.002;

        public static int Cost(int shares, float price, int scale)
        {
            return (int)Math.Min(int.MaxValue, Math.Ceiling(Notional(shares, price, scale)));
        }

        public static int Proceeds(int shares, float price, int scale)
        {
            return (int)Math.Min(int.MaxValue, Math.Floor(Notional(shares, price, scale)));
        }

        public static int CloseCash(Position position, float price, int scale)
        {
            if (position.isShort) return Math.Max(0, position.escrow - Cost(position.shares, price, scale));
            return Math.Max(0, Proceeds(position.shares, price, scale) - position.loan);
        }

        public static int InitialMargin(int notional)
        {
            return (notional + 1) / 2;
        }

        public static int Portion(int total, int part, int whole)
        {
            return whole <= 0 ? 0 : (int)((long)total * part / whole);
        }

        public static bool LongMarginCall(int value, int loan)
        {
            return (long)(value - loan) * 100 < (long)value * MaintenancePercent;
        }

        public static bool ShortMarginCall(int value, int escrow)
        {
            return (long)(escrow - value) * 100 < (long)value * MaintenancePercent;
        }

        public static double Dividend(int shares, float price, float yearlyYield, int days, int scale)
        {
            return shares * (double)price * yearlyYield * days / 365.0 * scale;
        }

        public static string Format(float price)
        {
            return price.ToString("F2", CultureInfo.InvariantCulture);
        }

        public static string Signed(double percent)
        {
            return (percent >= 0 ? "+" : "") + percent.ToString("F2", CultureInfo.InvariantCulture);
        }

        public static string SelfCheck()
        {
            if (Cost(3, 10.5f, 1) != 32) return "cost";
            if (Proceeds(3, 10.5f, 1) != 31) return "proceeds";
            if (Cost(1, 6.4f, 10) != 64 || Proceeds(3, 6.4f, 10) != 192) return "tenths";
            if (Proceeds(1, MarketData.MinPrice, 10) <= 0) return "floor proceeds";
            if (InitialMargin(101) != 51 || InitialMargin(100) != 50) return "initial margin";
            if (Portion(101, 1, 3) != 33 || Portion(100, 3, 3) != 100) return "portion";
            if (!LongMarginCall(100, 71) || LongMarginCall(100, 70)) return "long margin call";
            if (!ShortMarginCall(100, 129) || ShortMarginCall(100, 130)) return "short margin call";
            if (Proceeds(1000000, 3000f, 1) != int.MaxValue || Cost(1000000, 3000f, 1) != int.MaxValue) return "value cap";
            if (LongMarginCall(Proceeds(1000000, 3000f, 1), 0)) return "unlevered huge position";
            if (Math.Abs(Dividend(100, 50f, 0.05f, 7, 1) - 4.7945) > 0.0001) return "dividend";
            Exchange market = new Exchange(MoneyCurrency.OC);
            market.Advance(30, 23);
            Position position = new Position();
            position.ticker = "SIP";
            position.shares = 1;
            market.State.positions.Add(position);
            int steps = market.State.companies[0].history.Count;
            market.Advance(30, 0);
            if (market.State.companies[0].history.Count != steps + 1) return "midnight key";
            market.Advance(31, 0);
            market.Advance(31, 1);
            if (market.Key != 745 || market.State.positions.Count != 1 || market.State.companies[0].history.Count != steps + 2) return "midnight key";
            Exchange tenths = new Exchange(MoneyCurrency.RM);
            Position held = new Position();
            held.ticker = "SIP";
            held.shares = 100;
            held.basis = 6400;
            CompanyState sip = tenths.Company("SIP");
            sip.history.Clear();
            sip.history.Add(7f);
            sip.price = 8f;
            if (Math.Abs(MarketTrading.AveragePrice(tenths, held) - 6.4) > 0.0001) return "average price";
            if (Math.Abs(MarketTrading.DayChange(tenths, held) - 1000.0) > 0.0001) return "day change";
            return null;
        }

        private static decimal Notional(int shares, float price, int scale)
        {
            return shares * (decimal)price * scale;
        }
    }
}
