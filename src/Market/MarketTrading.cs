using System;
using System.Globalization;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal enum MarketAction
    {
        Buy,
        Sell,
        Short,
        Cover
    }

    internal static class MarketTrading
    {
        public const int DayHours = 24;

        public static string Shares(int count)
        {
            return count.ToString("#,0", CultureInfo.InvariantCulture);
        }

        public static void Holdings(Exchange exchange, out int equity, out double dayPl)
        {
            equity = 0;
            dayPl = 0.0;
            foreach (Position position in exchange.State.positions)
            {
                equity += Equity(exchange, position);
                dayPl += DayChange(exchange, position);
            }
        }

        public static int Equity(Exchange exchange, Position position)
        {
            float price = exchange.PriceOf(position.ticker);
            if (position.isShort) return position.escrow - MarketMath.Cost(position.shares, price, exchange.Account.Scale);
            return MarketMath.Proceeds(position.shares, price, exchange.Account.Scale) - position.loan;
        }

        public static int Pnl(Exchange exchange, Position position)
        {
            float price = exchange.PriceOf(position.ticker);
            if (position.isShort) return position.basis - MarketMath.Cost(position.shares, price, exchange.Account.Scale);
            return MarketMath.Proceeds(position.shares, price, exchange.Account.Scale) - position.basis;
        }

        public static double DayChange(Exchange exchange, Position position)
        {
            CompanyState company = exchange.Company(position.ticker);
            if (company == null || company.history.Count == 0) return 0.0;
            int earlier = Math.Max(0, company.history.Count - 1 - DayHours);
            double move = ((double)company.price - company.history[earlier]) * position.shares * exchange.Account.Scale;
            return position.isShort ? -move : move;
        }

        public static double AveragePrice(Exchange exchange, Position position)
        {
            return position.shares > 0 ? position.basis / (double)position.shares / exchange.Account.Scale : 0.0;
        }

        public static int DividendTotal(Exchange exchange, string ticker)
        {
            int total = 0;
            foreach (LedgerEntry entry in exchange.State.ledger)
            {
                if (entry.kind == "DIVIDEND" && (ticker == null || entry.ticker == ticker)) total += entry.oc;
            }
            return total;
        }

        public static string HoldingLine(Exchange exchange, string ticker)
        {
            Position position = exchange.PositionOf(ticker);
            if (position == null) return "No position in " + ticker;
            int value = MarketMath.Proceeds(position.shares, exchange.PriceOf(ticker), exchange.Account.Scale);
            string side = position.isShort ? "short " : "long ";
            return "You are " + side + Shares(position.shares) + ", value " + exchange.Account.Text(value) + ", P&L " + exchange.Account.Signed(Pnl(exchange, position));
        }

        public static string Blocked(Exchange exchange, MarketAction action, string ticker, int shares, bool margin, out int amount)
        {
            amount = 0;
            float price = exchange.PriceOf(ticker);
            int scale = exchange.Account.Scale;
            if (shares < 1 || shares > MarketData.MaxShares) return "Enter 1 to " + Shares(MarketData.MaxShares) + " shares";
            if ((double)shares * price > MarketData.MaxOrder) return "Order too large";
            Position position = exchange.PositionOf(ticker);
            if (action == MarketAction.Buy)
            {
                if (position != null && position.isShort) return "Cover " + ticker + " first";
                int notional = MarketMath.Cost(shares, price, scale);
                amount = margin ? MarketMath.InitialMargin(notional) : notional;
                return Afford(exchange.Account, amount);
            }
            if (action == MarketAction.Sell)
            {
                if (position == null || position.isShort) return "You hold no " + ticker;
                if (shares > position.shares) return "You hold " + Shares(position.shares);
                amount = Math.Max(0, MarketMath.Proceeds(shares, price, scale) - MarketMath.Portion(position.loan, shares, position.shares));
                return null;
            }
            if (action == MarketAction.Short)
            {
                if (position != null && !position.isShort) return "Sell your shares first";
                int proceeds = MarketMath.Proceeds(shares, price, scale);
                if (proceeds < 1) return "Order too small";
                amount = MarketMath.InitialMargin(proceeds);
                return Afford(exchange.Account, amount);
            }
            if (position == null || !position.isShort) return "You are not short " + ticker;
            if (shares > position.shares) return "You are short " + Shares(position.shares);
            amount = MarketMath.Portion(position.escrow, shares, position.shares) - MarketMath.Cost(shares, price, scale);
            return amount < 0 ? Afford(exchange.Account, -amount) : null;
        }

        public static string Hint(Funds funds, MarketAction action, int amount)
        {
            switch (action)
            {
                case MarketAction.Buy:
                    return "Costs " + funds.Text(amount);
                case MarketAction.Sell:
                    return "Receives " + funds.Text(amount);
                case MarketAction.Short:
                    return "Collateral " + funds.Text(amount);
                default:
                    return amount < 0 ? "Costs " + funds.Text(-amount) : "Receives " + funds.Text(amount);
            }
        }

        public static bool Execute(Exchange exchange, MarketAction action, string ticker, int shares, bool margin, out string message)
        {
            switch (action)
            {
                case MarketAction.Buy:
                    return exchange.Buy(ticker, shares, margin, out message);
                case MarketAction.Sell:
                    return exchange.Sell(ticker, shares, out message);
                case MarketAction.Short:
                    return exchange.Short(ticker, shares, out message);
                default:
                    return exchange.Cover(ticker, shares, out message);
            }
        }

        private static string Afford(Funds funds, int needed)
        {
            return needed <= funds.Balance ? null : "Needs " + funds.Text(needed);
        }
    }
}
