using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ObenseuerCore;

namespace ObenseuerOS
{
    public enum OsTradeAction
    {
        Buy,
        Sell,
        Short,
        Cover
    }

    public sealed class OsStock
    {
        public string Ticker { get; private set; }
        public string Name { get; private set; }
        public string Sector { get; private set; }
        public float Price { get; private set; }
        public double Change24 { get; private set; }

        internal OsStock(string ticker, string name, string sector, float price, double change24)
        {
            Ticker = ticker;
            Name = name;
            Sector = sector;
            Price = price;
            Change24 = change24;
        }
    }

    public sealed class OsHolding
    {
        public string Ticker { get; private set; }
        public bool Short { get; private set; }
        public int Shares { get; private set; }
        public double Average { get; private set; }
        public double Value { get; private set; }
        public double Pnl { get; private set; }

        internal OsHolding(string ticker, bool isShort, int shares, double average, double value, double pnl)
        {
            Ticker = ticker;
            Short = isShort;
            Shares = shares;
            Average = average;
            Value = value;
            Pnl = pnl;
        }
    }

    public sealed class OsTradeResult
    {
        public bool Ok { get; private set; }
        public string Code { get; private set; }
        public string Reason { get; private set; }
        public double Amount { get; private set; }
        public string Message { get; private set; }

        internal OsTradeResult(bool ok, string code, string reason, double amount, string message)
        {
            Ok = ok;
            Code = code;
            Reason = reason;
            Amount = amount;
            Message = message;
        }
    }

    public sealed class OsLedgerArgs
    {
        public string Kind { get; private set; }
        public string Ticker { get; private set; }
        public int Shares { get; private set; }
        public double Amount { get; private set; }
        public string ModId { get; private set; }

        internal OsLedgerArgs(string kind, string ticker, int shares, double amount, string modId)
        {
            Kind = kind;
            Ticker = ticker;
            Shares = shares;
            Amount = amount;
            ModId = modId;
        }
    }

    public sealed class OsMarketEventArgs
    {
        public string Target { get; private set; }
        public bool Sector { get; private set; }
        public bool Up { get; private set; }
        public int Hours { get; private set; }
        public string Headline { get; private set; }
        public string ModId { get; private set; }

        internal OsMarketEventArgs(string target, bool sector, bool up, int hours, string headline, string modId)
        {
            Target = target;
            Sector = sector;
            Up = up;
            Hours = hours;
            Headline = headline;
            ModId = modId;
        }
    }

    public static class OsMarket
    {
        private const int MinEventHours = 48;
        private const int MaxEventHours = 144;
        private const int MaxHeadline = 100;
        private const string ModIdText = "The mod id is not valid.";
        private const string BadArgsText = "Check the action, ticker, shares and margin.";
        private const string ClosedText = "The exchange is closed.";
        private const string BusyText = "The game is saving or loading. Try again.";
        private const string UpdatingText = "The market is updating. Try again.";
        private const string FailedText = "The market could not complete the request.";
        private static Action ticked;
        private static Action<OsLedgerArgs> posted;
        private static Action<OsMarketEventArgs> eventStarted;
        private static int raising;

        public static bool Ready
        {
            get
            {
                return Open() != null;
            }
        }

        public static string Currency
        {
            get
            {
                Exchange market = Open();
                return market == null ? null : market.Account.Code;
            }
        }

        public static OsStock[] Stocks()
        {
            try
            {
                List<OsStock> stocks = new List<OsStock>();
                Exchange market = Open();
                if (market != null)
                {
                    foreach (CompanyInfo info in MarketData.Companies)
                    {
                        OsStock stock = StockOf(market, info.Ticker);
                        if (stock != null) stocks.Add(stock);
                    }
                }
                return stocks.ToArray();
            }
            catch (Exception e)
            {
                Os.Crash("OsMarket.Stocks", e);
                return new OsStock[0];
            }
        }

        public static OsStock Stock(string ticker)
        {
            try
            {
                Exchange market = Open();
                return market == null ? null : StockOf(market, ticker);
            }
            catch (Exception e)
            {
                Os.Crash("OsMarket.Stock", e);
                return null;
            }
        }

        public static float[] History(string ticker, int hours)
        {
            try
            {
                Exchange market = Open();
                CompanyState company = market == null ? null : market.Company(ticker);
                if (company == null) return new float[0];
                int take = Math.Min(Math.Max(1, Math.Min(hours, MarketData.HistoryHours)), company.history.Count);
                float[] prices = new float[take];
                company.history.CopyTo(company.history.Count - take, prices, 0, take);
                return prices;
            }
            catch (Exception e)
            {
                Os.Crash("OsMarket.History", e);
                return new float[0];
            }
        }

        public static OsHolding[] Holdings()
        {
            try
            {
                List<OsHolding> holdings = new List<OsHolding>();
                Exchange market = Open();
                if (market != null)
                {
                    int scale = market.Account.Scale;
                    foreach (Position position in market.State.positions)
                    {
                        double value = MarketMath.Proceeds(position.shares, market.PriceOf(position.ticker), scale) / (double)scale;
                        holdings.Add(new OsHolding(position.ticker, position.isShort, position.shares, MarketTrading.AveragePrice(market, position), value, MarketTrading.Pnl(market, position) / (double)scale));
                    }
                }
                return holdings.ToArray();
            }
            catch (Exception e)
            {
                Os.Crash("OsMarket.Holdings", e);
                return new OsHolding[0];
            }
        }

        public static OsTradeResult Preview(OsTradeAction action, string ticker, int shares, bool margin)
        {
            try
            {
                MarketAction kind;
                if (!Valid(action, ticker, shares, margin, out kind)) return Refused("bad_args", BadArgsText);
                Exchange market = Open();
                if (market == null) return Refused("failed", ClosedText);
                int amount;
                string message = MarketTrading.Blocked(market, kind, ticker, shares, margin, out amount);
                double value = (double)amount / market.Account.Scale;
                return message == null ? new OsTradeResult(true, null, null, value, null) : Denied(message, value);
            }
            catch (Exception e)
            {
                Os.Crash("OsMarket.Preview", e);
                return Refused("failed", FailedText);
            }
        }

        public static OsTradeResult Trade(string modId, OsTradeAction action, string ticker, int shares, bool margin)
        {
            try
            {
                if (!Os.Accept("OsMarket.Trade", modId)) return Refused("bad_args", ModIdText);
                MarketAction kind;
                if (!Valid(action, ticker, shares, margin, out kind)) return Refused("bad_args", BadArgsText);
                Exchange market = Open();
                if (market == null) return Refused("failed", ClosedText);
                if (SaveData.Busy) return Refused("busy", BusyText);
                if (raising > 0) return Refused("busy", UpdatingText);
                int amount;
                string message = MarketTrading.Blocked(market, kind, ticker, shares, margin, out amount);
                double value = (double)amount / market.Account.Scale;
                if (message != null) return Denied(message, value);
                string previous = market.TradingMod;
                market.TradingMod = modId;
                try
                {
                    if (!MarketTrading.Execute(market, kind, ticker, shares, margin, out message)) return Denied(message, value);
                }
                finally
                {
                    market.TradingMod = previous;
                }
                return new OsTradeResult(true, null, null, value, null);
            }
            catch (Exception e)
            {
                Os.Crash("OsMarket.Trade", e);
                return Refused("failed", FailedText);
            }
        }

        public static OsResult AddEvent(string modId, string target, bool up, int hours, string headline)
        {
            try
            {
                if (!Os.Accept("OsMarket.AddEvent", modId)) return OsResult.Invalid;
                Exchange market = Open();
                if (market == null) return OsResult.Unavailable;
                if (SaveData.Busy || raising > 0) return OsResult.Busy;
                bool sector = Array.IndexOf(MarketData.Sectors, target) >= 0;
                if (!sector && MarketData.Find(target) == null) return Os.Refuse("OsMarket.AddEvent", modId, OsResult.Invalid, "unknown ticker or sector");
                string text = headline == null ? null : Regex.Replace(headline, @"\p{Cc}", "");
                if (text != null && (text.Length < 1 || text.Length > MaxHeadline)) return Os.Refuse("OsMarket.AddEvent", modId, OsResult.Invalid, "headline must be 1 to 100 characters");
                int length = Math.Max(MinEventHours, Math.Min(MaxEventHours, hours));
                return market.StartModEvent(modId, target, sector, up, length, text) ? OsResult.Ok : OsResult.Full;
            }
            catch (Exception e)
            {
                return Os.Crash("OsMarket.AddEvent", e);
            }
        }

        public static event Action Ticked
        {
            add
            {
                ticked += value;
            }
            remove
            {
                ticked -= value;
            }
        }

        public static event Action<OsLedgerArgs> Posted
        {
            add
            {
                posted += value;
            }
            remove
            {
                posted -= value;
            }
        }

        public static event Action<OsMarketEventArgs> EventStarted
        {
            add
            {
                eventStarted += value;
            }
            remove
            {
                eventStarted -= value;
            }
        }

        internal static void RaiseTicked()
        {
            if (ticked == null) return;
            raising++;
            try
            {
                foreach (Action handler in ticked.GetInvocationList()) Safely(handler, "Ticked");
            }
            finally
            {
                raising--;
            }
        }

        internal static void RaisePosted(OsLedgerArgs args)
        {
            if (posted == null) return;
            raising++;
            try
            {
                foreach (Action<OsLedgerArgs> handler in posted.GetInvocationList()) Safely(() => handler(args), "Posted");
            }
            finally
            {
                raising--;
            }
        }

        internal static void RaiseEventStarted(OsMarketEventArgs args)
        {
            if (eventStarted == null) return;
            raising++;
            try
            {
                foreach (Action<OsMarketEventArgs> handler in eventStarted.GetInvocationList()) Safely(() => handler(args), "EventStarted");
            }
            finally
            {
                raising--;
            }
        }

        private static void Safely(Action call, string name)
        {
            try
            {
                call();
            }
            catch (Exception e)
            {
                Os.Crash(name + " handler", e);
            }
        }

        private static Exchange Open()
        {
            Exchange market = MarketHost.Exchange;
            return market != null && market.Loaded && !MarketHost.LegacyRunning ? market : null;
        }

        private static OsStock StockOf(Exchange market, string ticker)
        {
            CompanyInfo info = MarketData.Find(ticker);
            CompanyState company = market.Company(ticker);
            if (info == null || company == null) return null;
            return new OsStock(info.Ticker, info.Name, info.Sector, company.price, market.ChangePercent(ticker, MarketTrading.DayHours));
        }

        private static bool Valid(OsTradeAction action, string ticker, int shares, bool margin, out MarketAction kind)
        {
            kind = MarketAction.Buy;
            switch (action)
            {
                case OsTradeAction.Sell:
                    kind = MarketAction.Sell;
                    break;
                case OsTradeAction.Short:
                    kind = MarketAction.Short;
                    break;
                case OsTradeAction.Cover:
                    kind = MarketAction.Cover;
                    break;
                case OsTradeAction.Buy:
                    break;
                default:
                    return false;
            }
            return MarketData.Find(ticker) != null && shares >= 1 && shares <= MarketData.MaxShares && (!margin || kind == MarketAction.Buy);
        }

        private static OsTradeResult Refused(string code, string message)
        {
            return new OsTradeResult(false, code, null, 0.0, message);
        }

        private static OsTradeResult Denied(string message, double amount)
        {
            return new OsTradeResult(false, "denied", MarketJson.Reason(message), amount, message);
        }
    }
}
