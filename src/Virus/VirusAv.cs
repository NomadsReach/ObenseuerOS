using System;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class VirusAv
    {
        internal const string SiteApp = "pcmaster";
        internal const string TurvaSite = "turvasite";
        private const int CidMax = 64;
        private const string TurvaFrom = "TurvaPro <billing@turvapro.ts>";
        private const string PayRefused = "The payment did not go through. Try again.";
        private static readonly Random Rnd = new Random();

        internal static void Attach()
        {
            Os.Handle(SiteApp, "av.state", State);
            Os.Handle(SiteApp, "av.buy", Buy);
            Os.Handle(VirusAvRules.PcGuardApp, "av.state", State);
            Os.Handle(VirusAvRules.PcGuardApp, "av.scan", Scan);
            Os.Handle(TurvaSite, "av.state", CheapState);
            Os.Handle(TurvaSite, "av.buy", CheapBuy);
            Os.Handle(TurvaSite, "av.freescan", FreeScan);
            Os.Handle(VirusAvRules.TurvaApp, "av.state", CheapState);
            Os.Handle(VirusAvRules.TurvaApp, "av.scan", CheapScanNow);
            Os.Handle(VirusAvRules.TurvaApp, "av.cancel", CheapCancel);
            Events.HourPassed += hour => Hour();
        }

        internal static bool Shows(string appId)
        {
            return VirusAvRules.Shows(VirusCore.State, appId);
        }

        private static string State(string args)
        {
            OsBridge.Args(args);
            return VirusAvRules.StateJson(VirusCore.State, Wallet());
        }

        private static string Buy(string args)
        {
            Cid(OsBridge.Args(args));
            Guard();
            bool affordable = Cash.Has(MoneyCurrency.RM, VirusAvRules.PriceRm);
            VirusBuy result = VirusAvRules.Buy(VirusCore.State, affordable, VirusAvRules.PriceRm, Spend);
            if (result == VirusBuy.Short) throw new OsBridgeException("denied", affordable ? PayRefused : "Not enough RM in your wallet. PC Master takes cash.");
            if (result == VirusBuy.Bought)
            {
                OsView.Refresh();
                VirusCore.Toast("PC Master Guard installed. Run a scan.");
            }
            return VirusAvRules.BuyJson(result, VirusCore.State, Wallet());
        }

        private static string Scan(string args)
        {
            OsBridge.Args(args);
            if (!VirusCore.State.RealOwned) throw new OsBridgeException("denied", "PC Master Guard is not installed.");
            Guard();
            VirusScan scan = VirusAvRules.Scan(VirusCore.State, new VirusCore.Disk(), WorldClock.Now.Day);
            VirusCore.Changed(false);
            return VirusAvRules.ScanJson(scan, VirusCore.State, Wallet());
        }

        private static string CheapState(string args)
        {
            OsBridge.Args(args);
            return VirusAvRules.CheapStateJson(VirusCore.State, Minute(), OcWallet());
        }

        private static string CheapBuy(string args)
        {
            string cid = Cid(OsBridge.Args(args));
            Guard();
            bool affordable = Cash.Has(MoneyCurrency.OC, VirusAvRules.CheapPriceOc);
            VirusBuy result = VirusAvRules.CheapBuy(VirusCore.State, cid, Minute(), affordable, SpendOc, Bundle);
            if (result == VirusBuy.Short) throw new OsBridgeException("denied", affordable ? PayRefused : "Not enough OC in your wallet. TurvaPro takes OC.");
            if (result == VirusBuy.Bought)
            {
                Receipt();
                OsView.Refresh();
                VirusCore.Changed(false);
                VirusCore.Toast("TurvaPro 2000 activated. Your PC is protected!");
            }
            return VirusAvRules.CheapStateJson(VirusCore.State, Minute(), OcWallet());
        }

        private static string FreeScan(string args)
        {
            OsBridge.Args(args);
            return "{\"threats\":" + VirusAvRules.FreeThreats(VirusCore.State, Rnd) + "}";
        }

        private static string CheapScanNow(string args)
        {
            OsBridge.Args(args);
            if (!VirusCore.State.CheapOwned) throw new OsBridgeException("denied", "TurvaPro 2000 is not active.");
            Guard();
            int minute = Minute();
            VirusCheapScan scan = VirusAvRules.CheapScan(VirusCore.State, minute, Rnd);
            if (scan == null) throw new OsBridgeException("denied", "The scan is still cooling down. Try again later.");
            VirusCore.Changed(false);
            return VirusAvRules.CheapScanJson(scan, VirusCore.State, minute, OcWallet());
        }

        private static string CheapCancel(string args)
        {
            OsBridge.Args(args);
            Guard();
            if (VirusCore.State.CheapOwned)
            {
                VirusAvRules.Cancel(VirusCore.State);
                OsView.Refresh();
                VirusCore.Changed(false);
            }
            return VirusAvRules.CheapStateJson(VirusCore.State, Minute(), OcWallet());
        }

        private static void Hour()
        {
            if (!OsBridge.Writable() || !OsBridge.WorldReady()) return;
            int day = WorldClock.Now.Day;
            VirusBill bill = Cash.IsAvailable ? VirusAvRules.Bill(VirusCore.State, day, Cash.Has(MoneyCurrency.OC, VirusAvRules.CheapPriceOc), SpendOc) : VirusBill.None;
            bool moved = VirusAvRules.ReturnDue(VirusCore.State, VirusCore.Enabled, day);
            if (bill == VirusBill.None && !moved) return;
            Billed(bill);
            OsView.Refresh();
            VirusCore.Changed(false);
        }

        private static void Billed(VirusBill bill)
        {
            if (bill == VirusBill.Charged)
            {
                Receipt();
                Bundle();
            }
            else if (bill == VirusBill.Lapsed) Send("Payment failed", "We could not take " + VirusAvRules.CheapPriceOc + " OC from your wallet. Your protection is OFF until the payment goes through. We will try again every day.\n\nTurvaPro");
            else if (bill == VirusBill.Ended) Send("Your TurvaPro 2000 subscription has ended", "Your subscription ended after " + VirusAvRules.CheapEndDays + " days without payment. Subscribe again to protect your PC.\n\nTurvaPro");
        }

        private static void Receipt()
        {
            Send("TurvaPro 2000 receipt", "Thank you. " + VirusAvRules.CheapPriceOc + " OC was taken from your wallet for one week of TurvaPro 2000 protection.\n\nTurvaPro");
        }

        private static void Send(string subject, string body)
        {
            OsMail.SendSpam(TurvaFrom, subject, body, "");
        }

        private static void Bundle()
        {
            VirusCore.Infect(VirusVector.TurvaProBill, "TurvaPro 2000");
        }

        private static void Guard()
        {
            OsBridge.CheckWritable();
            if (!OsBridge.WorldReady()) throw new OsBridgeException("busy", "the game is not loaded");
        }

        private static int Minute()
        {
            WorldTime now = WorldClock.Now;
            return now.Day * VirusAvRules.MinutesPerDay + now.Hour * 60 + now.Minute;
        }

        private static float Wallet()
        {
            return Cash.IsAvailable ? Cash.Balance(MoneyCurrency.RM) : 0f;
        }

        private static int OcWallet()
        {
            return Cash.IsAvailable ? (int)Cash.Balance(MoneyCurrency.OC) : 0;
        }

        private static bool Spend(int amount)
        {
            return Cash.TrySpend(MoneyCurrency.RM, amount);
        }

        private static bool SpendOc(int amount)
        {
            return Cash.TrySpend(MoneyCurrency.OC, amount);
        }

        private static string Cid(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > CidMax) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }
    }
}
