using System;
using System.Collections.Generic;
using System.Text;

namespace ObenseuerOS
{
    internal static class VirusCheapChecks
    {
        private const int Seed = 9001;
        private const int Trials = 100000;
        private const int Weeks = 200;
        private const int MaxBytes = 12 * 1024;
        private const double Tolerance = 0.02;
        private static readonly Func<int, bool> Pays = amount => true;
        private static readonly Func<int, bool> Refuses = amount => false;
        private static readonly Action Nothing = () => { };

        internal static List<KeyValuePair<string, bool>> Run()
        {
            List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();
            checks.Add(Check("billing: over 200 weeks of hourly ticks, a paying wallet is charged once on each due day", WeeklyOk()));
            checks.Add(Check("billing: a 3-week gap gives one charge, not three, and the next bill is a week later", GapOk()));
            checks.Add(Check("billing: an unpaid bill lapses at once, retries once a day and ends after 14 lapsed days", LapseOk()));
            checks.Add(Check("billing: a lapse paid on the next day is restored with one charge", RecoverOk()));
            checks.Add(Check("billing: money in the wallet but a refused charge neither charges nor lapses, and the next tick retries", SkipOk()));
            checks.Add(Check("buy: a repeat cid charges once, an active subscription is known, and a short wallet spends nothing", BuyOk()));
            checks.Add(Check("buy: the TurvaProBill roll runs before TurvaPro is marked active", BundleOrderOk()));
            checks.Add(Check("cancel: cancel stops billing and hides the icon", CancelOk()));
            checks.Add(Check("protection: the TurvaPro bundle is never blocked by TurvaPro, and other rolls keep cheap protection", BundleProtectOk()));
            checks.Add(Check("scan: removal is 60% for Hymy and MocaBar and 30% for Sekaisin, within 2 points", RemovalOk()));
            checks.Add(Check("scan: Penni, Kassa and Lukko are quarantined and return exactly on day + 2, and not while disabled", QuarantineOk()));
            checks.Add(Check("scan: TurvaPro.Partner is trusted and never removed", TrustedOk()));
            checks.Add(Check("scan: the scan cooldown holds for 60 game minutes", CooldownOk()));
            checks.Add(Check("free scan: threats are 7 per active virus plus 20 to 60", FreeOk()));
            checks.Add(Check("pulse: a lapsed TurvaPro nags 25% of pulses within 2 points, and an active one never does", LapsedNagOk()));
            checks.Add(Check("state: the worst-case TurvaPro av.state and av.scan JSON are under 12 KB", JsonOk()));
            return checks;
        }

        private static KeyValuePair<string, bool> Check(string name, bool ok)
        {
            return new KeyValuePair<string, bool>(name, ok);
        }

        private static VirusSave Owned()
        {
            VirusSave save = new VirusSave();
            VirusAvRules.CheapBuy(save, "start", 0, true, Pays, Nothing);
            return save;
        }

        private static bool WeeklyOk()
        {
            VirusSave save = Owned();
            List<int> charged = new List<int>();
            for (int hour = 1; hour <= Weeks * 7 * 24; hour++)
            {
                int day = hour / 24;
                if (VirusAvRules.Bill(save, day, true, Pays) == VirusBill.Charged) charged.Add(day);
            }
            if (charged.Count != Weeks) return false;
            for (int i = 0; i < charged.Count; i++)
            {
                if (charged[i] != (i + 1) * 7) return false;
            }
            return true;
        }

        private static bool GapOk()
        {
            VirusSave save = Owned();
            int charges = 0;
            if (VirusAvRules.Bill(save, 21, true, Pays) == VirusBill.Charged) charges++;
            for (int hour = 21 * 24 + 1; hour < 28 * 24; hour++)
            {
                if (VirusAvRules.Bill(save, hour / 24, true, Pays) == VirusBill.Charged) charges++;
            }
            return charges == 1 && save.CheapNextBill == 28;
        }

        private static bool LapseOk()
        {
            VirusSave save = Owned();
            int attempts = 0;
            int lapses = 0;
            int lapseDay = -1;
            int ends = 0;
            int endDay = -1;
            for (int hour = 7 * 24; hour <= 22 * 24; hour++)
            {
                int day = hour / 24;
                VirusBill bill = VirusAvRules.Bill(save, day, false, Refuses);
                if (bill != VirusBill.None) attempts++;
                if (bill == VirusBill.Lapsed)
                {
                    lapses++;
                    lapseDay = day;
                }
                if (bill == VirusBill.Ended)
                {
                    ends++;
                    endDay = day;
                }
            }
            return lapses == 1 && lapseDay == 7 && ends == 1 && endDay == 21 && attempts == 15 && !save.CheapOwned && save.CheapEnded;
        }

        private static bool RecoverOk()
        {
            VirusSave save = Owned();
            VirusBill missed = VirusAvRules.Bill(save, 7, false, Refuses);
            VirusBill paid = VirusAvRules.Bill(save, 8, true, Pays);
            return missed == VirusBill.Lapsed && paid == VirusBill.Charged && !save.CheapLapsed && save.CheapNextBill == 15;
        }

        private static bool SkipOk()
        {
            VirusSave save = Owned();
            VirusBill refused = VirusAvRules.Bill(save, 7, true, Refuses);
            bool untouched = save.CheapOwned && !save.CheapLapsed && save.CheapLapsedSince == -1 && save.CheapNextBill == 7;
            VirusBill retried = VirusAvRules.Bill(save, 7, true, Pays);
            return refused == VirusBill.None && untouched && retried == VirusBill.Charged && save.CheapNextBill == 14;
        }

        private static bool BuyOk()
        {
            VirusSave save = new VirusSave();
            int spent = 0;
            Func<int, bool> spend = amount =>
            {
                spent += amount;
                return true;
            };
            VirusBuy first = VirusAvRules.CheapBuy(save, "a", 0, true, spend, Nothing);
            VirusBuy repeat = VirusAvRules.CheapBuy(save, "a", 0, true, spend, Nothing);
            VirusBuy active = VirusAvRules.CheapBuy(save, "b", 0, true, spend, Nothing);
            VirusSave poor = new VirusSave();
            VirusBuy unaffordable = VirusAvRules.CheapBuy(poor, "c", 0, false, spend, Nothing);
            VirusBuy refused = VirusAvRules.CheapBuy(poor, "c", 0, true, Refuses, Nothing);
            return first == VirusBuy.Bought && repeat == VirusBuy.Known && active == VirusBuy.Known && spent == VirusAvRules.CheapPriceOc
                && unaffordable == VirusBuy.Short && refused == VirusBuy.Short && !poor.CheapOwned && poor.Paid.Count == 0 && save.Paid.Count == 1;
        }

        private static bool BundleOrderOk()
        {
            VirusSave save = new VirusSave();
            bool ownedDuringRoll = true;
            Action bundle = () => ownedDuringRoll = save.CheapOwned;
            VirusAvRules.CheapBuy(save, "a", 0, true, Pays, bundle);
            return !ownedDuringRoll && save.CheapOwned;
        }

        private static bool CancelOk()
        {
            VirusSave save = Owned();
            VirusAvRules.Cancel(save);
            return VirusAvRules.Bill(save, 7, true, Pays) == VirusBill.None && VirusAvRules.Bill(save, 14, true, Pays) == VirusBill.None
                && !VirusAvRules.Shows(save, VirusAvRules.TurvaApp) && VirusAvRules.Shows(save, "shop");
        }

        private static bool BundleProtectOk()
        {
            VirusSave save = Owned();
            VirusProtect bundle = VirusRules.ProtectionOf(save, VirusVector.TurvaProBill);
            Random rnd = new Random(Seed);
            int blocked = 0;
            int infected = 0;
            for (int i = 0; i < Trials; i++)
            {
                VirusOutcome outcome = VirusRules.Decide(save, true, VirusVector.TurvaProBill, bundle, rnd).Outcome;
                if (outcome == VirusOutcome.Blocked) blocked++;
                if (outcome == VirusOutcome.Infected) infected++;
            }
            VirusSave real = Owned();
            real.RealOwned = true;
            return bundle == VirusProtect.None && blocked == 0 && Math.Abs(infected / (double)Trials - 0.20) <= Tolerance
                && VirusRules.ProtectionOf(save, VirusVector.OtherPageView) == VirusProtect.Cheap
                && VirusRules.ProtectionOf(real, VirusVector.TurvaProBill) == VirusProtect.Real;
        }

        private static bool RemovalOk()
        {
            return RemovalRate(VirusRules.Hymy, 60) && RemovalRate(VirusRules.Moca, 60) && RemovalRate(VirusRules.Sekaisin, 30);
        }

        private static bool RemovalRate(string id, int percent)
        {
            Random rnd = new Random(Seed);
            int removed = 0;
            for (int i = 0; i < Trials; i++)
            {
                VirusSave save = new VirusSave();
                save.Active.Add(id);
                if (VirusAvRules.CheapScan(save, 0, rnd).Found[0].Value == "removed") removed++;
            }
            return Math.Abs(removed / (double)Trials - percent / 100.0) <= Tolerance;
        }

        private static bool QuarantineOk()
        {
            VirusSave save = Owned();
            save.Active.Add(VirusRules.Penni);
            save.Active.Add(VirusRules.Kassa);
            save.Active.Add(VirusRules.Lukko);
            VirusAvRules.CheapScan(save, 10 * 1440 + 600, new Random(Seed));
            bool quarantined = save.Active.Count == 0 && save.Returning.Count == 3;
            bool early = !VirusAvRules.ReturnDue(save, true, 11) && save.Active.Count == 0;
            bool disabled = !VirusAvRules.ReturnDue(save, false, 12) && save.Returning.Count == 3;
            bool back = VirusAvRules.ReturnDue(save, true, 12) && save.Active.Count == 3 && save.Returning.Count == 0;
            return quarantined && early && disabled && back;
        }

        private static bool TrustedOk()
        {
            VirusSave save = Owned();
            save.Active.Add(VirusRules.Turvabar);
            VirusCheapScan scan = VirusAvRules.CheapScan(save, 0, new Random(Seed));
            return save.Active.Count == 1 && save.Active[0] == VirusRules.Turvabar && scan.Found.Count == 1 && scan.Found[0].Value == "trusted";
        }

        private static bool CooldownOk()
        {
            VirusSave save = Owned();
            Random rnd = new Random(Seed);
            bool first = VirusAvRules.CheapScan(save, 1000, rnd) != null;
            bool cooling = VirusAvRules.CheapScan(save, 1059, rnd) == null && VirusAvRules.CheapReadyIn(save, 1059) == 1;
            bool ready = VirusAvRules.CheapScan(save, 1060, rnd) != null;
            return first && cooling && ready;
        }

        private static bool FreeOk()
        {
            VirusSave save = new VirusSave();
            Random rnd = new Random(Seed);
            for (int i = 0; i < 1000; i++)
            {
                int threats = VirusAvRules.FreeThreats(save, rnd);
                if (threats < 20 || threats > 60) return false;
            }
            save.Active.Add(VirusRules.Hymy);
            save.Active.Add(VirusRules.Moca);
            save.Active.Add(VirusRules.Sekaisin);
            for (int i = 0; i < 1000; i++)
            {
                int threats = VirusAvRules.FreeThreats(save, rnd);
                if (threats < 41 || threats > 81) return false;
            }
            return true;
        }

        private static bool LapsedNagOk()
        {
            Random rnd = new Random(Seed);
            List<string> none = new List<string>();
            int nags = 0;
            for (int i = 0; i < Trials; i++)
            {
                if (VirusRules.Pulse(none, true, rnd).Nag) nags++;
            }
            bool quiet = true;
            for (int i = 0; i < 1000; i++)
            {
                if (VirusRules.Pulse(none, false, rnd).Nag) quiet = false;
            }
            return Math.Abs(nags / (double)Trials - 0.25) <= Tolerance && quiet;
        }

        private static bool JsonOk()
        {
            VirusSave save = Owned();
            save.CheapLapsed = true;
            save.CheapLapsedSince = 99999;
            save.CheapNextBill = 99999;
            save.CheapScanAt = 99999 * 1440;
            VirusCheapScan scan = new VirusCheapScan { Cookies = 999 };
            foreach (KeyValuePair<string, string> item in VirusRules.Catalogue)
            {
                save.Active.Add(item.Key);
                save.Returning.Add(new VirusReturn { Id = item.Key, Day = 99999 });
                scan.Found.Add(new KeyValuePair<string, string>(item.Key, "quarantined"));
            }
            int minute = 99999 * 1440 + 1439;
            string state = VirusAvRules.CheapStateJson(save, minute, 2000000000);
            string json = VirusAvRules.CheapScanJson(scan, save, minute, 2000000000);
            return Encoding.UTF8.GetByteCount(state) < MaxBytes && Encoding.UTF8.GetByteCount(json) < MaxBytes;
        }
    }
}
