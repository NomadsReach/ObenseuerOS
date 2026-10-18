using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class MarketJson
    {
        public const string Version = "1";
        private const int MaxNews = 20;
        private const int MaxEvents = 10;
        private const int TopCount = 3;
        private const int LedgerRows = 12;
        private const int ThreeDayHours = 72;
        private const int MaxReply = 12288;

        public static string Status()
        {
            return Run("status", () =>
            {
                Exchange market = Open();
                if (market == null) return Obj(Pair("ok", "true"), Pair("version", Str(Version)), Pair("ready", "false"));
                int key = market.Key;
                return Obj(
                    Pair("ok", "true"),
                    Pair("version", Str(Version)),
                    Pair("ready", "true"),
                    Pair("currency", Str(market.Account.Code)),
                    Pair("scale", Int(market.Account.Scale)),
                    Pair("key", Int(key)),
                    Pair("day", Int(key < 0 ? -1 : key / 24)),
                    Pair("hour", Int(key < 0 ? -1 : key % 24)),
                    Pair("nextDividendDay", Int(market.State.nextDividendDay)),
                    Pair("dividendDays", Int(MarketData.DividendDays)),
                    Pair("cash", Money(market.Account.Scale, market.Account.Balance)));
            });
        }

        public static string Indices()
        {
            return Run("indices", () =>
            {
                Exchange market = Open();
                if (market == null) return Closed();
                List<Move> moves = new List<Move>();
                double sum = 0.0;
                int up = 0;
                int down = 0;
                foreach (CompanyInfo info in MarketData.Companies)
                {
                    Move move = new Move(market, info);
                    sum += move.Change;
                    if (move.Change > 0) up++;
                    if (move.Change < 0) down++;
                    moves.Add(move);
                }
                List<Move> rising = new List<Move>(moves);
                rising.Sort((a, b) => b.Change.CompareTo(a.Change));
                List<Move> falling = new List<Move>(moves);
                falling.Sort((a, b) => a.Change.CompareTo(b.Change));
                List<Move> active = new List<Move>(moves);
                active.Sort((a, b) => Math.Abs(b.Change).CompareTo(Math.Abs(a.Change)));
                return Obj(
                    Pair("ok", "true"),
                    Pair("avg24", Num(sum / moves.Count, 2)),
                    Pair("up", Int(up)),
                    Pair("down", Int(down)),
                    Pair("gainers", Lines(market, rising)),
                    Pair("losers", Lines(market, falling)),
                    Pair("movers", Lines(market, active)));
            });
        }

        public static string List(string sector)
        {
            return Run("list", () =>
            {
                bool all = string.IsNullOrEmpty(sector);
                if (!all && Array.IndexOf(MarketData.Sectors, sector) < 0) return Error("bad_args", "Unknown sector.");
                Exchange market = Open();
                if (market == null) return Closed();
                List<string> items = new List<string>();
                foreach (CompanyInfo info in MarketData.Companies)
                {
                    if (all || info.Sector == sector) items.Add(Line(market, info, market.ChangePercent(info.Ticker, MarketTrading.DayHours)));
                }
                return Obj(Pair("ok", "true"), Pair("items", Arr(items)));
            });
        }

        public static string Quote(string ticker)
        {
            return Run("quote", () =>
            {
                CompanyInfo info = MarketData.Find(ticker);
                if (info == null) return Error("bad_args", "Unknown ticker.");
                Exchange market = Open();
                if (market == null) return Closed();
                CompanyState company = market.Company(ticker);
                double high = double.NaN;
                double low = double.NaN;
                foreach (float price in company.history)
                {
                    if (double.IsNaN(high) || price > high) high = price;
                    if (double.IsNaN(low) || price < low) low = price;
                }
                List<string> events = new List<string>();
                foreach (MarketEvent item in market.State.events)
                {
                    if (events.Count == MaxEvents) break;
                    events.Add(Obj(Pair("target", Str(item.target)), Pair("sector", Bool(item.sector)), Pair("hoursLeft", Int(item.hoursLeft))));
                }
                return Obj(
                    Pair("ok", "true"),
                    Pair("t", Str(info.Ticker)),
                    Pair("n", Str(info.Name)),
                    Pair("s", Str(info.Sector)),
                    Pair("p", Num(company.price, 2)),
                    Pair("c1", Num(market.ChangePercent(ticker, MarketTrading.DayHours), 2)),
                    Pair("c3", Num(market.ChangePercent(ticker, ThreeDayHours), 2)),
                    Pair("c7", Num(market.ChangePercent(ticker, MarketData.HistoryHours), 2)),
                    Pair("hi7", Num(high, 2)),
                    Pair("lo7", Num(low, 2)),
                    Pair("yield", Num(info.Yield * 100.0, 2)),
                    Pair("nextDividendDay", Int(market.State.nextDividendDay)),
                    Pair("dividendDays", Int(MarketData.DividendDays)),
                    Pair("events", Arr(events)));
            });
        }

        public static string Chart(string ticker, double hours)
        {
            return Run("chart", () =>
            {
                CompanyInfo info = MarketData.Find(ticker);
                int count = Whole(hours, MarketData.HistoryHours);
                if (info == null || count == 0) return Error("bad_args", "Check the ticker and hours (1 to 168).");
                Exchange market = Open();
                if (market == null) return Closed();
                List<float> history = market.Company(ticker).history;
                List<string> points = new List<string>();
                for (int i = Math.Max(0, history.Count - count); i < history.Count; i++) points.Add(Num(history[i], 2));
                return Obj(Pair("ok", "true"), Pair("t", Str(info.Ticker)), Pair("hours", Int(count)), Pair("points", Arr(points)));
            });
        }

        public static string News(double limit)
        {
            return Run("news", () =>
            {
                int count = Whole(limit, MaxNews);
                if (count == 0) return Error("bad_args", "Limit must be 1 to 20.");
                Exchange market = Open();
                if (market == null) return Closed();
                return NewsReply(market.State.news, count);
            });
        }

        public static string Portfolio()
        {
            return Run("portfolio", () =>
            {
                Exchange market = Open();
                if (market == null) return Closed();
                int equity;
                double dayPl;
                MarketTrading.Holdings(market, out equity, out dayPl);
                int scale = market.Account.Scale;
                List<string> positions = new List<string>();
                foreach (Position position in market.State.positions)
                {
                    positions.Add(PositionJson(scale, position, market.PriceOf(position.ticker), MarketTrading.AveragePrice(market, position), MarketTrading.Pnl(market, position)));
                }
                List<string> ledger = new List<string>();
                for (int i = market.State.ledger.Count - 1; i >= 0 && ledger.Count < LedgerRows; i--) ledger.Add(LedgerJson(scale, market.State.ledger[i]));
                return PortfolioReply(scale, market.Account.Balance, equity, dayPl, MarketTrading.DividendTotal(market, null), positions, ledger);
            });
        }

        public static string Preview(string action, string ticker, double shares, bool margin)
        {
            return Run("preview", () =>
            {
                MarketAction kind;
                int count;
                string bad = Check(action, ticker, shares, margin, out kind, out count);
                if (bad != null) return bad;
                Exchange market = Open();
                if (market == null) return Closed();
                int amount;
                string reason = MarketTrading.Blocked(market, kind, ticker, count, margin, out amount);
                return Obj(
                    Pair("ok", "true"),
                    Pair("allowed", Bool(reason == null)),
                    Pair("amount", Money(market.Account.Scale, amount)),
                    Pair("reason", reason == null ? "null" : Str(Reason(reason))),
                    Pair("message", Str(reason)));
            });
        }

        public static string Trade(string action, string ticker, double shares, bool margin)
        {
            return Run("trade", () =>
            {
                MarketAction kind;
                int count;
                string bad = Check(action, ticker, shares, margin, out kind, out count);
                if (bad != null) return bad;
                Exchange market = Open();
                if (market == null) return Closed();
                if (SaveData.Busy) return Error("busy", "The game is saving or loading. Try again.");
                int amount;
                string message = MarketTrading.Blocked(market, kind, ticker, count, margin, out amount);
                if (message != null) return Denied(message);
                if (!MarketTrading.Execute(market, kind, ticker, count, margin, out message)) return Denied(message);
                return Obj(
                    Pair("ok", "true"),
                    Pair("message", Str(message)),
                    Pair("cash", Money(market.Account.Scale, market.Account.Balance)),
                    Pair("position", HeldPosition(market, ticker)));
            });
        }

        public static string SelfCheck()
        {
            if (Str("a\"b\\c\n\u0001") != "\"a\\\"b\\\\c\\n\\u0001\"") return "json escape";
            if (Num(-0.001, 2) != "0.00" || Num(6.4, 2) != "6.40" || Num(double.NaN, 2) != "null") return "json number";
            if (Num(2.5, 0) != "3" || Num(0.125, 2) != "0.13") return "json rounding";
            if (Error("bad_args", "x") != "{\"ok\":false,\"code\":\"bad_args\",\"message\":\"x\"}") return "json error";
            if (Whole(1, 5) != 1 || Whole(0, 5) != 0 || Whole(-3, 5) != 0 || Whole(1.5, 5) != 0 || Whole(6, 5) != 0) return "whole";
            if (Whole(double.NaN, 5) != 0 || Whole(double.PositiveInfinity, 5) != 0 || Whole(1e18, MarketData.MaxShares) != 0) return "whole huge";
            MarketAction kind;
            int count;
            if (Check("buy", "SIP", 1, false, out kind, out count) != null || kind != MarketAction.Buy || count != 1) return "valid order";
            if (Check("buy", "NOPE", 1, false, out kind, out count) == null) return "unknown ticker";
            if (Check("buy", "SIP", 0, false, out kind, out count) == null || Check("buy", "SIP", double.NaN, false, out kind, out count) == null) return "shares";
            if (Check("buy", "SIP", 1e18, false, out kind, out count) == null) return "huge shares";
            if (Check("sell", "SIP", 1, true, out kind, out count) == null) return "margin on sell";
            if (Check("hold", "SIP", 1, false, out kind, out count) == null) return "action";
            if (ActionOf("SELL", out kind) || ActionOf(null, out kind)) return "action case";
            if (Reason("Not enough RM: need 1") != "funds" || Reason("Cover not settled, try again") != "settle") return "reason funds";
            if (Reason("Cover SIP first") != "side" || Reason("Order too small") != "small") return "reason side";
            if (Reason("You hold no SIP shares") != "side" || Reason("You are not short SIP") != "side") return "reason no holding";
            if (Reason("You hold 100") != "holding" || Reason("You are short 5 SIP") != "holding") return "reason holding";
            if (Reason("Position too large") != "limit") return "reason limit";
            List<string> held = new List<string>();
            Position worst = new Position();
            worst.ticker = "SOMC";
            worst.isShort = true;
            worst.shares = MarketData.MaxShares;
            for (int i = 0; i < 8; i++) held.Add(PositionJson(10, worst, 500f, 500.0, int.MinValue));
            LedgerEntry entry = new LedgerEntry();
            entry.kind = "MARGIN INTEREST";
            entry.ticker = "SOMC";
            entry.shares = MarketData.MaxShares;
            entry.oc = int.MinValue;
            List<string> ledger = new List<string>();
            for (int i = 0; i < LedgerRows; i++) ledger.Add(LedgerJson(10, entry));
            if (Bytes(PortfolioReply(10, int.MinValue, int.MaxValue, -1e15, int.MaxValue, held, ledger)) > MaxReply) return "portfolio size";
            NewsItem headline = new NewsItem();
            headline.day = int.MaxValue;
            headline.hour = 23;
            headline.text = new string('"', 120);
            List<NewsItem> news = new List<NewsItem>();
            for (int i = 0; i < MaxNews; i++) news.Add(headline);
            if (Bytes(NewsReply(news, MaxNews)) > MaxReply) return "news size";
            MarketState legacy = new MarketState();
            CompanyState oldCompany = new CompanyState();
            oldCompany.ticker = "OSP";
            legacy.companies.Add(oldCompany);
            Position oldPosition = new Position();
            oldPosition.ticker = "PNT";
            legacy.positions.Add(oldPosition);
            LedgerEntry oldEntry = new LedgerEntry();
            oldEntry.ticker = "BRN";
            legacy.ledger.Add(oldEntry);
            MarketEvent oldEvent = new MarketEvent();
            oldEvent.target = "STT";
            legacy.events.Add(oldEvent);
            MarketData.RenameLegacy(legacy);
            if (legacy.companies[0].ticker != "SIP" || legacy.positions[0].ticker != "MACS" || legacy.ledger[0].ticker != "OLUT" || legacy.events[0].target != "SBT") return "ticker map";
            foreach (KeyValuePair<string, string> pair in MarketData.LegacyTickers)
            {
                if (MarketData.Find(pair.Value) == null || MarketData.Find(pair.Key) != null || MarketData.Renamed(pair.Value) != pair.Value) return "ticker map";
            }
            return null;
        }

        private static Exchange Open()
        {
            Exchange market = MarketHost.Exchange;
            return market != null && market.Loaded && !MarketHost.LegacyRunning ? market : null;
        }

        private static string Run(string name, Func<string> body)
        {
            try
            {
                return body();
            }
            catch (Exception e)
            {
                CoreLog.For(Os.ModId).Error("market api " + name + " failed: " + e);
                return Error("failed", "The market could not complete the request.");
            }
        }

        private static string Check(string action, string ticker, double shares, bool margin, out MarketAction kind, out int count)
        {
            count = Whole(shares, MarketData.MaxShares);
            if (!ActionOf(action, out kind) || MarketData.Find(ticker) == null || count == 0 || (margin && kind != MarketAction.Buy))
            {
                return Error("bad_args", "Check the action, ticker, shares and margin.");
            }
            return null;
        }

        private static bool ActionOf(string name, out MarketAction action)
        {
            action = MarketAction.Buy;
            switch (name)
            {
                case "buy":
                    return true;
                case "sell":
                    action = MarketAction.Sell;
                    return true;
                case "short":
                    action = MarketAction.Short;
                    return true;
                case "cover":
                    action = MarketAction.Cover;
                    return true;
                default:
                    return false;
            }
        }

        private static int Whole(double value, int max)
        {
            if (double.IsNaN(value) || value < 1 || value > max || Math.Floor(value) != value) return 0;
            return (int)value;
        }

        internal static string Reason(string message)
        {
            if (Starts(message, "Not enough") || Starts(message, "Needs ")) return "funds";
            if (Starts(message, "Enter 1 to")) return "range";
            if (Starts(message, "Order too small")) return "small";
            if (Starts(message, "Order too large") || Starts(message, "Position too large")) return "limit";
            if (Starts(message, "Sale not settled") || Starts(message, "Cover not settled")) return "settle";
            if (Starts(message, "You hold no ") || Starts(message, "You are not short ") || Starts(message, "Cover ") || Starts(message, "Sell your ")) return "side";
            if (Starts(message, "You hold") || Starts(message, "You are")) return "holding";
            return "unknown";
        }

        private static bool Starts(string text, string prefix)
        {
            return text.StartsWith(prefix, StringComparison.Ordinal);
        }

        private static string Closed()
        {
            return Error("failed", "The exchange is closed.");
        }

        private static string Error(string code, string message)
        {
            return Obj(Pair("ok", "false"), Pair("code", Str(code)), Pair("message", Str(message)));
        }

        private static string Denied(string message)
        {
            return Obj(Pair("ok", "false"), Pair("code", Str("denied")), Pair("reason", Str(Reason(message))), Pair("message", Str(message)));
        }

        private static string HeldPosition(Exchange market, string ticker)
        {
            Position position = market.PositionOf(ticker);
            return Obj(
                Pair("t", Str(ticker)),
                Pair("shares", Int(position == null ? 0 : position.shares)),
                Pair("short", Bool(position != null && position.isShort)));
        }

        private static string NewsReply(List<NewsItem> news, int count)
        {
            List<string> items = new List<string>();
            for (int i = news.Count - 1; i >= 0 && items.Count < count; i--)
            {
                items.Add(Obj(Pair("d", Int(news[i].day)), Pair("h", Int(news[i].hour)), Pair("text", Str(news[i].text))));
            }
            return Obj(Pair("ok", "true"), Pair("items", Arr(items)));
        }

        private static string PortfolioReply(int scale, int cash, int equity, double dayPl, int dividends, List<string> positions, List<string> ledger)
        {
            return Obj(
                Pair("ok", "true"),
                Pair("cash", Money(scale, cash)),
                Pair("holdings", Money(scale, equity)),
                Pair("dayPl", Money(scale, dayPl)),
                Pair("dividends", Money(scale, dividends)),
                Pair("positions", Arr(positions)),
                Pair("ledger", Arr(ledger)));
        }

        private static string PositionJson(int scale, Position position, float price, double average, int pnl)
        {
            int value = MarketMath.Proceeds(position.shares, price, scale);
            return Obj(
                Pair("t", Str(position.ticker)),
                Pair("short", Bool(position.isShort)),
                Pair("shares", Int(position.shares)),
                Pair("avg", Num(average, 2)),
                Pair("price", Num(price, 2)),
                Pair("value", Money(scale, value)),
                Pair("pnl", Money(scale, pnl)));
        }

        private static string LedgerJson(int scale, LedgerEntry entry)
        {
            return Obj(
                Pair("d", Int(entry.day)),
                Pair("h", Int(entry.hour)),
                Pair("kind", Str(entry.kind)),
                Pair("t", Str(entry.ticker)),
                Pair("shares", Int(entry.shares)),
                Pair("amount", Money(scale, entry.oc)));
        }

        private static string Lines(Exchange market, List<Move> moves)
        {
            List<string> items = new List<string>();
            for (int i = 0; i < moves.Count && i < TopCount; i++) items.Add(Line(market, moves[i].Info, moves[i].Change));
            return Arr(items);
        }

        private static string Line(Exchange market, CompanyInfo info, double change)
        {
            return Obj(
                Pair("t", Str(info.Ticker)),
                Pair("n", Str(info.Name)),
                Pair("s", Str(info.Sector)),
                Pair("p", Num(market.PriceOf(info.Ticker), 2)),
                Pair("c", Num(change, 2)));
        }

        private static string Money(int scale, double units)
        {
            return Num(units / scale, scale == 1 ? 0 : 2);
        }

        private static int Bytes(string json)
        {
            return Encoding.UTF8.GetByteCount(json);
        }

        private static string Str(string text)
        {
            if (text == null) return "null";
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\b':
                        sb.Append("\\b");
                        break;
                    case '\f':
                        sb.Append("\\f");
                        break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        private static string Num(double value, int decimals)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return "null";
            double rounded = Math.Round(value, decimals, MidpointRounding.AwayFromZero);
            if (rounded == 0) rounded = 0;
            return rounded.ToString("F" + decimals, CultureInfo.InvariantCulture);
        }

        private static string Int(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Bool(bool value)
        {
            return value ? "true" : "false";
        }

        private static string Pair(string key, string json)
        {
            return Str(key) + ":" + json;
        }

        private static string Obj(params string[] pairs)
        {
            return "{" + string.Join(",", pairs) + "}";
        }

        private static string Arr(List<string> items)
        {
            return "[" + string.Join(",", items.ToArray()) + "]";
        }

        private sealed class Move
        {
            public readonly CompanyInfo Info;
            public readonly double Change;

            public Move(Exchange market, CompanyInfo info)
            {
                Info = info;
                Change = market.ChangePercent(info.Ticker, MarketTrading.DayHours);
            }
        }
    }
}
