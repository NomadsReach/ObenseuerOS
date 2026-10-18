using System;
using System.Collections.Generic;
using System.IO;

namespace ObenseuerOS
{
    internal sealed class CompanyInfo
    {
        public readonly string Ticker;
        public readonly string Name;
        public readonly string Sector;
        public readonly float Volatility;
        public readonly float Yield;
        public readonly float BasePrice;

        public CompanyInfo(string ticker, string name, string sector, float volatility, float yield, float basePrice)
        {
            Ticker = ticker;
            Name = name;
            Sector = sector;
            Volatility = volatility;
            Yield = yield;
            BasePrice = basePrice;
        }
    }

    internal static class MarketData
    {
        internal const string StoreId = "nomad.obenseuer.os.market";
        internal const string LegacyId = "nomad.obenseuer.stockmarket";
        public const int HistoryHours = 168;
        public const int WarmupHours = 72;
        public const int NewsMax = 40;
        public const int LedgerMax = 200;
        public const int DividendDays = 7;
        public const int MaxShares = 1000000;
        public const int WalletMax = 16000000;
        public const int MaxOrder = WalletMax;
        public const float MinPrice = 0.10f;
        public const float MaxPrice = 500f;

        public static readonly CompanyInfo[] Companies = new CompanyInfo[]
        {
            new CompanyInfo("SIP", "Stalburg Industry Power", "Energy", 0.012f, 0.045f, 6.4f),
            new CompanyInfo("SBB", "Stalburg Bank", "Finance", 0.007f, 0.030f, 12f),
            new CompanyInfo("SOMC", "Stalburg-Obenseuer Mining Company", "Industry", 0.018f, 0.020f, 3.5f),
            new CompanyInfo("MOBL", "M\u00f6belmann Furnitures", "Retail", 0.014f, 0.055f, 2.2f),
            new CompanyInfo("WALT", "Walter Corporation", "Construction", 0.011f, 0.035f, 4.8f),
            new CompanyInfo("MACS", "Macrosoftware", "Tech", 0.022f, 0.005f, 15f),
            new CompanyInfo("SBT", "Stalburg Times LLC", "Media", 0.010f, 0.040f, 1.8f),
            new CompanyInfo("OLUT", "Osmo Olut Brewing Company", "Food", 0.015f, 0.038f, 0.95f)
        };

        public static readonly string[] Sectors = new string[] { "Energy", "Finance", "Industry", "Retail", "Tech", "Media", "Food", "Construction" };

        public static readonly string[] CompanyUp = new string[]
        {
            "{0} signs a big deal",
            "Analysts upgrade {0}",
            "{0} posts record profit",
            "Merger rumours lift {0}",
            "{0} expands into new markets"
        };

        public static readonly string[] CompanyDown = new string[]
        {
            "{0} hit by a scandal",
            "Strike halts work at {0}",
            "{0} fined by regulators",
            "{0} loses a major contract",
            "Fraud rumours swirl around {0}"
        };

        public static readonly string[] SectorUp = new string[]
        {
            "{0} sector rallies",
            "Demand surges across {0}",
            "Government grant boosts {0}"
        };

        public static readonly string[] SectorDown = new string[]
        {
            "{0} sector slumps",
            "Investors flee {0}",
            "New tax squeezes {0}"
        };

        public static readonly Dictionary<string, string> LegacyTickers = new Dictionary<string, string>
        {
            { "OSP", "SIP" },
            { "SBK", "SBB" },
            { "MNS", "SOMC" },
            { "GSH", "MOBL" },
            { "LLN", "WALT" },
            { "PNT", "MACS" },
            { "STT", "SBT" },
            { "BRN", "OLUT" }
        };

        public static string Renamed(string ticker)
        {
            string next;
            return ticker != null && LegacyTickers.TryGetValue(ticker, out next) ? next : ticker;
        }

        public static void RenameLegacy(MarketState state)
        {
            foreach (CompanyState company in state.companies) company.ticker = Renamed(company.ticker);
            foreach (Position position in state.positions) position.ticker = Renamed(position.ticker);
            foreach (LedgerEntry entry in state.ledger) entry.ticker = Renamed(entry.ticker);
            foreach (MarketEvent e in state.events) e.target = Renamed(e.target);
        }

        public static bool CarryOver(string folder)
        {
            if (folder == null) return false;
            string mods = Path.Combine(folder, "mods");
            if (File.Exists(Path.Combine(mods, StoreId + ".json"))) return false;
            return !Directory.Exists(mods) || Directory.GetFiles(mods, StoreId + "*.bad").Length == 0;
        }

        public static CompanyInfo Find(string ticker)
        {
            foreach (CompanyInfo info in Companies)
            {
                if (info.Ticker == ticker) return info;
            }
            return null;
        }
    }

    [Serializable]
    internal class CompanyState
    {
        public string ticker;
        public float price;
        public List<float> history = new List<float>();
    }

    [Serializable]
    internal class Position
    {
        public string ticker;
        public bool isShort;
        public int shares;
        public int basis;
        public int loan;
        public int escrow;
        public float carry;
        public float interestCarry;
    }

    [Serializable]
    internal class LedgerEntry
    {
        public int day;
        public int hour;
        public string kind;
        public string ticker;
        public int shares;
        public int oc;
        public string text;
    }

    [Serializable]
    internal class MarketEvent
    {
        public string target;
        public bool sector;
        public float drift;
        public int hoursLeft;
    }

    [Serializable]
    internal class NewsItem
    {
        public int day;
        public int hour;
        public string text;
    }

    [Serializable]
    internal class MarketState
    {
        public string currency;
        public int lastKey = -1;
        public int nextDividendDay = -1;
        public List<CompanyState> companies = new List<CompanyState>();
        public List<Position> positions = new List<Position>();
        public List<LedgerEntry> ledger = new List<LedgerEntry>();
        public List<MarketEvent> events = new List<MarketEvent>();
        public List<NewsItem> news = new List<NewsItem>();
    }
}
