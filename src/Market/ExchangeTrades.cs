using System;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal partial class Exchange
    {
        public bool Buy(string ticker, int shares, bool margin, out string message)
        {
            float price;
            if (!CheckOrder(ticker, shares, out price, out message)) return false;
            Position position = PositionOf(ticker);
            if (position != null && position.isShort)
            {
                message = "Cover " + ticker + " before buying it";
                return false;
            }
            int notional = MarketMath.Cost(shares, price, funds.Scale);
            int down = margin ? MarketMath.InitialMargin(notional) : notional;
            int financed = notional - down;
            if (!Room(position, notional, financed, 0))
            {
                message = "Position too large";
                return false;
            }
            if (!Settle(-down))
            {
                message = "Not enough " + funds.Code + ": need " + funds.Number(down);
                return false;
            }
            if (position == null)
            {
                position = new Position();
                position.ticker = ticker;
                State.positions.Add(position);
            }
            position.shares += shares;
            position.basis += notional;
            position.loan += financed;
            Post(-down, margin ? "BUY ON MARGIN" : "BUY", ticker, shares, "at " + MarketMath.Format(price) + ", financed " + funds.Number(financed));
            message = "Bought " + shares + " " + ticker + " at " + MarketMath.Format(price);
            return true;
        }

        public bool Sell(string ticker, int shares, out string message)
        {
            float price;
            if (!CheckOrder(ticker, shares, out price, out message)) return false;
            Position position = PositionOf(ticker);
            if (position == null || position.isShort)
            {
                message = "You hold no " + ticker + " shares";
                return false;
            }
            if (shares > position.shares)
            {
                message = "You hold only " + position.shares + " " + ticker;
                return false;
            }
            int proceeds = MarketMath.Proceeds(shares, price, funds.Scale);
            int loanPart = MarketMath.Portion(position.loan, shares, position.shares);
            int basisPart = MarketMath.Portion(position.basis, shares, position.shares);
            int cash = Math.Max(0, proceeds - loanPart);
            if (!Settle(cash))
            {
                message = "Sale not settled, try again";
                return false;
            }
            if (proceeds < loanPart) Log.Warn("sale of " + ticker + " short of its loan by " + funds.Text(loanPart - proceeds) + ", broker absorbs");
            position.shares -= shares;
            position.loan -= loanPart;
            position.basis -= basisPart;
            if (position.shares == 0) State.positions.Remove(position);
            Post(cash, "SELL", ticker, shares, "at " + MarketMath.Format(price) + ", repaid " + funds.Number(Math.Min(loanPart, proceeds)));
            message = "Sold " + shares + " " + ticker + " at " + MarketMath.Format(price);
            return true;
        }

        public bool Short(string ticker, int shares, out string message)
        {
            float price;
            if (!CheckOrder(ticker, shares, out price, out message)) return false;
            Position position = PositionOf(ticker);
            if (position != null && !position.isShort)
            {
                message = "Sell your " + ticker + " shares before shorting";
                return false;
            }
            int proceeds = MarketMath.Proceeds(shares, price, funds.Scale);
            if (proceeds < 1)
            {
                message = "Order too small";
                return false;
            }
            int collateral = MarketMath.InitialMargin(proceeds);
            if (!Room(position, proceeds, 0, proceeds + collateral))
            {
                message = "Position too large";
                return false;
            }
            if (!Settle(-collateral))
            {
                message = "Not enough " + funds.Code + " for collateral: need " + funds.Number(collateral);
                return false;
            }
            if (position == null)
            {
                position = new Position();
                position.ticker = ticker;
                position.isShort = true;
                State.positions.Add(position);
            }
            position.shares += shares;
            position.basis += proceeds;
            position.escrow += proceeds + collateral;
            Post(-collateral, "SHORT", ticker, shares, "at " + MarketMath.Format(price) + ", escrow " + funds.Number(proceeds + collateral));
            message = "Shorted " + shares + " " + ticker + " at " + MarketMath.Format(price);
            return true;
        }

        public bool Cover(string ticker, int shares, out string message)
        {
            float price;
            if (!CheckOrder(ticker, shares, out price, out message)) return false;
            Position position = PositionOf(ticker);
            if (position == null || !position.isShort)
            {
                message = "You are not short " + ticker;
                return false;
            }
            if (shares > position.shares)
            {
                message = "You are short only " + position.shares + " " + ticker;
                return false;
            }
            int cost = MarketMath.Cost(shares, price, funds.Scale);
            int escrowPart = MarketMath.Portion(position.escrow, shares, position.shares);
            int basisPart = MarketMath.Portion(position.basis, shares, position.shares);
            int net = escrowPart - cost;
            if (!Settle(net))
            {
                message = net < 0 ? "Not enough " + funds.Code + " to cover: need " + funds.Number(-net) : "Cover not settled, try again";
                return false;
            }
            position.shares -= shares;
            position.escrow -= escrowPart;
            position.basis -= basisPart;
            if (position.shares == 0) State.positions.Remove(position);
            Post(net, "COVER", ticker, shares, "at " + MarketMath.Format(price) + ", cost " + funds.Number(cost) + ", escrow used " + funds.Number(escrowPart));
            message = "Covered " + shares + " " + ticker + " at " + MarketMath.Format(price);
            return true;
        }

        private bool CheckOrder(string ticker, int shares, out float price, out string message)
        {
            message = null;
            price = 0f;
            CompanyState company = Company(ticker);
            if (company == null)
            {
                message = "Unknown ticker " + ticker;
                return false;
            }
            if (shares < 1 || shares > MarketData.MaxShares)
            {
                message = "Enter 1 to " + MarketData.MaxShares + " shares";
                return false;
            }
            if ((double)shares * company.price > MarketData.MaxOrder)
            {
                message = "Order too large";
                return false;
            }
            price = company.price;
            return true;
        }

        internal static bool Room(Position position, int basis, int loan, int escrow)
        {
            if (position == null) return true;
            return (long)position.basis + basis <= int.MaxValue
                && (long)position.loan + loan <= int.MaxValue
                && (long)position.escrow + escrow <= int.MaxValue;
        }

        private bool Settle(int delta)
        {
            if (delta == 0) return true;
            return delta > 0 ? funds.Give(delta) : funds.Spend(-delta);
        }

        private void Post(int oc, string kind, string ticker, int shares, string text)
        {
            LedgerEntry entry = new LedgerEntry();
            entry.day = GameTime.Day;
            entry.hour = GameTime.Hour;
            entry.kind = kind;
            entry.ticker = ticker;
            entry.shares = shares;
            entry.oc = oc;
            entry.text = text;
            State.ledger.Add(entry);
            while (State.ledger.Count > MarketData.LedgerMax) State.ledger.RemoveAt(0);
            Log.Info(kind + " " + ticker + " x" + shares + ": wallet " + funds.Signed(oc) + ", " + text + ", balance " + funds.Text(funds.Balance));
            OsMarket.RaisePosted(new OsLedgerArgs(kind, ticker, shares, (double)oc / funds.Scale, TradingMod));
        }
    }
}
