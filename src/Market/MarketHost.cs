using System;
using System.IO;
using BepInEx;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class MarketHost
    {
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static Exchange exchange;
        private static bool legacyWarned;

        internal static Exchange Exchange
        {
            get
            {
                return exchange;
            }
        }

        internal static bool LegacyRunning
        {
            get
            {
                return Loader.Find(MarketData.LegacyId) != null;
            }
        }

        internal static void Attach(string currency)
        {
            exchange = new Exchange(Funds.Parse(currency));
            SaveData.Register<MarketState>(MarketData.StoreId, () => Gated() ? null : exchange.State, Load);
            SaveController.PlayerWillLoadGame += () => exchange.Unload("a save is loading");
            Events.SceneLoaded += scene => { if (scene == "Main Menu") exchange.Unload("main menu"); };
            Events.HourPassed += hour => { if (!Gated()) exchange.OnHour(hour); };
            Events.DayPassed += day => { if (!Gated()) exchange.OnDay(day); };
        }

        internal static string LegacyCurrency()
        {
            string path = Path.Combine(Paths.ConfigPath, MarketData.LegacyId + ".cfg");
            try
            {
                if (!File.Exists(path) || OwnCurrency()) return null;
                string code = Funds.CurrencyIn(File.ReadAllText(path));
                if (code == null) Log.Warn("the Stock Market config has no valid Currency, the OS market uses RM");
                return code;
            }
            catch (Exception e)
            {
                Log.Warn("the Stock Market config could not be read, the OS market uses RM: " + e.Message);
                return null;
            }
        }

        private static void Load(MarketState saved)
        {
            if (Gated()) return;
            if (saved == null && MarketData.CarryOver(SaveData.Folder))
            {
                saved = SaveData.Peek<MarketState>(MarketData.LegacyId);
                if (saved != null) Log.Info("market carried over from the Stock Market mod save");
            }
            exchange.Load(saved);
        }

        private static bool Gated()
        {
            if (!LegacyRunning) return false;
            if (!legacyWarned)
            {
                legacyWarned = true;
                Log.Warn("the Stock Market mod is installed, so the OS market stays closed; remove ObenseuerStockMarket to trade in the OS market");
            }
            return true;
        }

        private static bool OwnCurrency()
        {
            string own = Path.Combine(Paths.ConfigPath, Os.ModId + ".cfg");
            return File.Exists(own) && Funds.CurrencyIn(File.ReadAllText(own)) != null;
        }
    }
}
