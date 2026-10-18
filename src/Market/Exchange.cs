using System;
using System.Collections.Generic;
using ObenseuerCore;
using UnityEngine.SceneManagement;

namespace ObenseuerOS
{
    internal partial class Exchange
    {
        private const double MaxMove = 0.2;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private readonly Random rng = new Random(Environment.TickCount);
        private readonly MoneyCurrency target;
        private Funds funds;
        private readonly Dictionary<string, int> modEventDay = new Dictionary<string, int>();

        public Exchange(MoneyCurrency currency)
        {
            target = currency;
            Reset("started");
        }

        public MarketState State { get; private set; }

        public bool Loaded { get; private set; }

        public Funds Account
        {
            get { return funds; }
        }

        public int Key
        {
            get { return State.lastKey; }
        }

        internal string TradingMod { get; set; }

        public void Load(MarketState saved)
        {
            modEventDay.Clear();
            if (saved == null)
            {
                Reset("no market data in this save");
                Loaded = true;
                return;
            }
            State = saved;
            funds = new Funds(Funds.Parse(State.currency));
            Normalize();
            Log.Info("market loaded, key " + State.lastKey + ", " + State.positions.Count + " positions, " + State.ledger.Count + " ledger entries, " + funds.Code);
            if (Pending) Migrate();
            Loaded = true;
        }

        private bool Pending
        {
            get { return funds.Currency != target || string.IsNullOrEmpty(State.currency); }
        }

        public void OnHour(int hour)
        {
            if (!Loaded && !SaveData.Busy && SceneManager.GetActiveScene().name != "Main Menu") Loaded = true;
            if (Pending) Migrate();
            Advance(GameTime.Day, hour);
        }

        internal void Advance(int day, int hour)
        {
            int key = day * 24 + hour;
            if (State.lastKey - key > 24)
            {
                int returned;
                if (!Unwind("TIME RESET", out returned)) return;
                Reset("game time moved back");
            }
            if (hour == 0 && key < State.lastKey && State.lastKey - key < 24) key = State.lastKey + 1;
            if (key <= State.lastKey) return;
            State.lastKey = key;
            foreach (CompanyState company in State.companies) Step(company);
            AgeEvents();
            CheckMargins();
            OsMarket.RaiseTicked();
        }

        public void OnDay(int day)
        {
            if (State.nextDividendDay < 0)
            {
                State.nextDividendDay = day + MarketData.DividendDays;
            }
            else if (day >= State.nextDividendDay)
            {
                PayDividends();
                State.nextDividendDay = day + MarketData.DividendDays;
            }
            AccrueInterest();
            if (rng.NextDouble() < 0.6) NewEvent();
            CheckMargins();
        }

        public CompanyState Company(string ticker)
        {
            foreach (CompanyState company in State.companies)
            {
                if (company.ticker == ticker) return company;
            }
            return null;
        }

        public Position PositionOf(string ticker)
        {
            foreach (Position position in State.positions)
            {
                if (position.ticker == ticker) return position;
            }
            return null;
        }

        public float PriceOf(string ticker)
        {
            CompanyState company = Company(ticker);
            return company == null ? 0f : company.price;
        }

        public double ChangePercent(string ticker, int hours)
        {
            CompanyState company = Company(ticker);
            if (company == null || company.history.Count < 2) return 0.0;
            float earlier = company.history[Math.Max(0, company.history.Count - 1 - hours)];
            return earlier <= 0f ? 0.0 : (company.price - earlier) / (double)earlier * 100.0;
        }

        public void Reset(string why)
        {
            modEventDay.Clear();
            funds = new Funds(target);
            State = new MarketState();
            State.currency = funds.Code;
            foreach (CompanyInfo info in MarketData.Companies)
            {
                State.companies.Add(NewCompany(info));
            }
            for (int i = 0; i < MarketData.WarmupHours; i++)
            {
                foreach (CompanyState company in State.companies) Step(company);
            }
            Log.Info("market reset: " + why);
        }

        public void Unload(string why)
        {
            Loaded = false;
            Reset(why);
        }

        private bool Unwind(string kind, out int returned)
        {
            returned = 0;
            for (int i = State.positions.Count - 1; i >= 0; i--)
            {
                Position position = State.positions[i];
                float price = PriceOf(position.ticker);
                int cash = MarketMath.CloseCash(position, price, funds.Scale);
                if (!Settle(cash)) return false;
                State.positions.RemoveAt(i);
                returned += cash;
                Post(cash, kind, position.ticker, position.shares, "closed at " + MarketMath.Format(price) + ", returned " + funds.Number(cash));
            }
            return true;
        }

        private void Migrate()
        {
            Funds from = funds;
            int count = State.positions.Count;
            int returned;
            if (!Unwind("CURRENCY", out returned))
            {
                Log.Error("currency switch from " + from.Code + " to " + target + " waits, a position could not be settled, retrying next hour");
                return;
            }
            Reset("currency switched from " + from.Code + " to " + target);
            Post(0, "CURRENCY", "", 0, count + " positions closed, " + from.Text(returned) + " paid back in " + from.Code);
            string notice = "Stock Market now trades in " + funds.Code + ". " + count + " open positions closed, " + from.Text(returned) + " paid back.";
            if (!Hud.Notify(notice)) Log.Info("notice not shown: " + notice);
        }

        private void Normalize()
        {
            if (State.companies == null) State.companies = new List<CompanyState>();
            if (State.positions == null) State.positions = new List<Position>();
            if (State.ledger == null) State.ledger = new List<LedgerEntry>();
            if (State.events == null) State.events = new List<MarketEvent>();
            if (State.news == null) State.news = new List<NewsItem>();
            MarketData.RenameLegacy(State);
            for (int i = State.positions.Count - 1; i >= 0; i--)
            {
                Position position = State.positions[i];
                if (MarketData.Find(position.ticker) != null) continue;
                int cash = position.isShort ? Math.Max(0, position.escrow - position.basis) : Math.Max(0, position.basis - position.loan);
                if (!Settle(cash)) Log.Error("unknown ticker " + position.ticker + " position dropped without refund of " + funds.Text(cash));
                State.positions.RemoveAt(i);
                Post(cash, "DELISTED", position.ticker, position.shares, "ticker unknown, returned " + funds.Number(cash));
            }
            for (int i = State.companies.Count - 1; i >= 0; i--)
            {
                if (MarketData.Find(State.companies[i].ticker) == null) State.companies.RemoveAt(i);
            }
            foreach (CompanyInfo info in MarketData.Companies)
            {
                if (Company(info.Ticker) == null) State.companies.Add(NewCompany(info));
            }
            foreach (CompanyState company in State.companies)
            {
                if (company.history == null) company.history = new List<float>();
            }
        }

        private static CompanyState NewCompany(CompanyInfo info)
        {
            CompanyState company = new CompanyState();
            company.ticker = info.Ticker;
            company.price = info.BasePrice;
            return company;
        }

        private void Step(CompanyState company)
        {
            CompanyInfo info = MarketData.Find(company.ticker);
            double move = info.Volatility * Gauss() + Drift(info);
            move = Math.Max(-MaxMove, Math.Min(MaxMove, move));
            float next = (float)(company.price * (1.0 + move));
            company.price = Math.Max(MarketData.MinPrice, Math.Min(MarketData.MaxPrice, next));
            company.history.Add(company.price);
            while (company.history.Count > MarketData.HistoryHours) company.history.RemoveAt(0);
        }

        private double Drift(CompanyInfo info)
        {
            double total = 0.0;
            foreach (MarketEvent e in State.events)
            {
                if (e.sector ? e.target == info.Sector : e.target == info.Ticker) total += e.drift;
            }
            return total;
        }

        private double Gauss()
        {
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        private void AgeEvents()
        {
            for (int i = State.events.Count - 1; i >= 0; i--)
            {
                State.events[i].hoursLeft--;
                if (State.events[i].hoursLeft <= 0) State.events.RemoveAt(i);
            }
        }

        private void NewEvent()
        {
            bool sector = rng.NextDouble() < 0.4;
            bool up = rng.NextDouble() < 0.5;
            string target = sector ? MarketData.Sectors[rng.Next(MarketData.Sectors.Length)] : MarketData.Companies[rng.Next(MarketData.Companies.Length)].Ticker;
            int hours = sector ? 72 + rng.Next(72) : 48 + rng.Next(72);
            Start(target, sector, up, hours, null, null);
        }

        internal bool StartModEvent(string modId, string target, bool sector, bool up, int hours, string headline)
        {
            int day = GameTime.Day;
            int last;
            if (modEventDay.TryGetValue(modId, out last) && last == day) return false;
            modEventDay[modId] = day;
            Start(target, sector, up, hours, headline, modId);
            return true;
        }

        private void Start(string target, bool sector, bool up, int hours, string headline, string modId)
        {
            int sign = up ? 1 : -1;
            MarketEvent e = new MarketEvent();
            e.target = target;
            e.sector = sector;
            e.hoursLeft = hours;
            float shock;
            if (sector)
            {
                shock = (float)(0.02 + 0.03 * rng.NextDouble());
                e.drift = (float)(sign * (0.001 + 0.0015 * rng.NextDouble()));
            }
            else
            {
                shock = (float)(0.05 + 0.04 * rng.NextDouble());
                e.drift = (float)(sign * (0.0025 + 0.0025 * rng.NextDouble()));
            }
            foreach (CompanyState company in State.companies)
            {
                CompanyInfo info = MarketData.Find(company.ticker);
                if (sector ? info.Sector == e.target : info.Ticker == e.target)
                {
                    company.price = Math.Max(MarketData.MinPrice, Math.Min(MarketData.MaxPrice, company.price * (1f + sign * shock)));
                }
            }
            State.events.Add(e);
            string name = sector ? e.target : MarketData.Find(e.target).Name;
            string[] templates = sector ? (up ? MarketData.SectorUp : MarketData.SectorDown) : (up ? MarketData.CompanyUp : MarketData.CompanyDown);
            string text = headline ?? string.Format(templates[rng.Next(templates.Length)], name);
            Headline(text);
            Log.Info("event " + (up ? "up " : "down ") + (sector ? "sector " : "company ") + e.target + ", drift " + e.drift + " per hour for " + e.hoursLeft + " h");
            OsMarket.RaiseEventStarted(new OsMarketEventArgs(e.target, sector, up, hours, text, modId));
        }

        private void Headline(string text)
        {
            NewsItem item = new NewsItem();
            item.day = GameTime.Day;
            item.hour = GameTime.Hour;
            item.text = text;
            State.news.Add(item);
            while (State.news.Count > MarketData.NewsMax) State.news.RemoveAt(0);
        }

        private void CheckMargins()
        {
            for (int i = State.positions.Count - 1; i >= 0; i--)
            {
                Position position = State.positions[i];
                float price = PriceOf(position.ticker);
                int value = MarketMath.Proceeds(position.shares, price, funds.Scale);
                if (!position.isShort && MarketMath.LongMarginCall(value, position.loan))
                {
                    ForceCloseLong(position, value, price);
                }
                else if (position.isShort && MarketMath.ShortMarginCall(value, position.escrow))
                {
                    ForceCoverShort(position, price);
                }
            }
        }

        private void ForceCloseLong(Position position, int value, float price)
        {
            int repaid = Math.Min(position.loan, value);
            int cash = value - repaid;
            if (!Settle(cash))
            {
                Log.Error("margin call on " + position.ticker + " not settled, retrying next tick");
                return;
            }
            State.positions.Remove(position);
            if (repaid < position.loan) Log.Warn("margin call on " + position.ticker + ": broker absorbs " + funds.Text(position.loan - repaid));
            Post(cash, "MARGIN CALL", position.ticker, position.shares, "sold at " + MarketMath.Format(price) + ", repaid " + funds.Number(repaid) + " of loan " + funds.Number(position.loan) + ", returned " + funds.Number(cash));
        }

        private void ForceCoverShort(Position position, float price)
        {
            int cost = MarketMath.Cost(position.shares, price, funds.Scale);
            int back = Math.Max(0, position.escrow - cost);
            if (!Settle(back))
            {
                Log.Error("margin call on " + position.ticker + " not settled, retrying next tick");
                return;
            }
            State.positions.Remove(position);
            if (position.escrow < cost) Log.Warn("margin call on " + position.ticker + ": broker absorbs " + funds.Text(cost - position.escrow));
            Post(back, "MARGIN CALL", position.ticker, position.shares, "covered at " + MarketMath.Format(price) + ", cost " + funds.Number(cost) + ", escrow " + funds.Number(position.escrow) + ", returned " + funds.Number(back));
        }

        private void PayDividends()
        {
            for (int i = 0; i < State.positions.Count; i++)
            {
                Position position = State.positions[i];
                double due = MarketMath.Dividend(position.shares, PriceOf(position.ticker), MarketData.Find(position.ticker).Yield, MarketData.DividendDays, funds.Scale) + position.carry;
                int pay = (int)Math.Floor(due);
                if (position.isShort)
                {
                    int take = Math.Min(pay, position.escrow);
                    position.escrow -= take;
                    position.carry = (float)(due - pay);
                    if (take > 0) Post(0, "SHORT DIVIDEND", position.ticker, position.shares, "charged " + funds.Number(take) + " from escrow, escrow now " + funds.Number(position.escrow));
                }
                else
                {
                    if (pay > 0 && !Settle(pay)) continue;
                    position.carry = (float)(due - pay);
                    if (pay > 0) Post(pay, "DIVIDEND", position.ticker, position.shares, "paid " + funds.Text(pay));
                }
            }
        }

        private void AccrueInterest()
        {
            foreach (Position position in State.positions)
            {
                if (position.isShort || position.loan <= 0) continue;
                double due = position.loan * MarketMath.MarginInterestPerDay + position.interestCarry;
                int add = Math.Min((int)Math.Floor(due), int.MaxValue - position.loan);
                position.interestCarry = (float)(due - add);
                if (add <= 0) continue;
                position.loan += add;
                Post(0, "MARGIN INTEREST", position.ticker, position.shares, "loan up " + funds.Number(add) + " to " + funds.Number(position.loan));
            }
        }
    }
}
