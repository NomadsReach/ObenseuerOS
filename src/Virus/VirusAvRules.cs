using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ObenseuerOS
{
    internal enum VirusBuy
    {
        Bought,
        Known,
        Short,
    }

    internal enum VirusBill
    {
        None,
        Charged,
        Lapsed,
        Retry,
        Ended,
    }

    internal sealed class VirusScan
    {
        internal readonly List<string> Found = new List<string>();
        internal int Restored;
        internal int Junk;
        internal int Left;
    }

    internal sealed class VirusCheapScan
    {
        internal readonly List<KeyValuePair<string, string>> Found = new List<KeyValuePair<string, string>>();
        internal int Cookies;
    }

    internal static class VirusAvRules
    {
        internal const string PcGuardApp = "pcguard";
        internal const string TurvaApp = "turvapro";
        internal const int PriceRm = 500;
        internal const int CheapPriceOc = 99;
        internal const int CheapWeekDays = 7;
        internal const int CheapEndDays = 14;
        internal const int CheapPaidKept = 20;
        internal const int ScanCooldownMinutes = 60;
        internal const int MinutesPerDay = 1440;
        internal const int QuarantineDays = 2;
        private const int PopupRemovePercent = 60;
        private const int WormRemovePercent = 30;
        private const int CookiesMin = 30;
        private const int CookiesSpan = 60;
        private const int FreeBaseThreats = 20;
        private const int FreeSpan = 41;
        private const int FreePerVirus = 7;
        private const string Removed = "removed";
        private const string Missed = "missed";
        private const string Quarantined = "quarantined";
        private const string Trusted = "trusted";

        internal static VirusBuy Buy(VirusSave save, bool affordable, int price, Func<int, bool> spend)
        {
            if (save.RealOwned) return VirusBuy.Known;
            if (!affordable || !spend(price)) return VirusBuy.Short;
            save.RealOwned = true;
            return VirusBuy.Bought;
        }

        internal static VirusBuy CheapBuy(VirusSave save, string cid, int minute, bool affordable, Func<int, bool> spend, Action bundle)
        {
            if (save.Paid.Contains(cid) || CheapActive(save)) return VirusBuy.Known;
            if (!affordable || !spend(CheapPriceOc)) return VirusBuy.Short;
            save.Paid.Add(cid);
            if (save.Paid.Count > CheapPaidKept) save.Paid.RemoveAt(0);
            bundle();
            save.CheapOwned = true;
            save.CheapLapsed = false;
            save.CheapEnded = false;
            save.CheapLapsedSince = -1;
            save.CheapNextBill = Day(minute) + CheapWeekDays;
            save.CheapScanAt = -1;
            return VirusBuy.Bought;
        }

        internal static VirusBill Bill(VirusSave save, int day, bool affordable, Func<int, bool> spend)
        {
            if (!save.CheapOwned || day < save.CheapNextBill) return VirusBill.None;
            if (affordable)
            {
                if (!spend(CheapPriceOc)) return VirusBill.None;
                save.CheapLapsed = false;
                save.CheapLapsedSince = -1;
                save.CheapNextBill = day + CheapWeekDays;
                return VirusBill.Charged;
            }
            if (!save.CheapLapsed)
            {
                save.CheapLapsed = true;
                save.CheapLapsedSince = day;
                save.CheapNextBill = day + 1;
                return VirusBill.Lapsed;
            }
            if (day - save.CheapLapsedSince >= CheapEndDays)
            {
                Cancel(save);
                save.CheapEnded = true;
                return VirusBill.Ended;
            }
            save.CheapNextBill = day + 1;
            return VirusBill.Retry;
        }

        internal static void Cancel(VirusSave save)
        {
            save.CheapOwned = false;
            save.CheapLapsed = false;
            save.CheapEnded = false;
            save.CheapLapsedSince = -1;
            save.CheapNextBill = -1;
        }

        internal static bool ReturnDue(VirusSave save, bool enabled, int day)
        {
            bool moved = false;
            if (!enabled) return false;
            for (int i = save.Returning.Count - 1; i >= 0; i--)
            {
                VirusReturn item = save.Returning[i];
                if (item.Day > day) continue;
                save.Returning.RemoveAt(i);
                if (save.RealOwned) save.Blocked++;
                else if (!save.Active.Contains(item.Id)) save.Active.Add(item.Id);
                moved = true;
            }
            return moved;
        }

        internal static bool ClearInfections(VirusSave save)
        {
            bool any = save.Active.Count > 0 || save.Returning.Count > 0;
            save.Active.Clear();
            save.Returning.Clear();
            return any;
        }

        internal static int CheapReadyIn(VirusSave save, int minute)
        {
            if (save.CheapScanAt < 0) return 0;
            return Math.Max(0, save.CheapScanAt + ScanCooldownMinutes - minute);
        }

        internal static VirusCheapScan CheapScan(VirusSave save, int minute, Random rnd)
        {
            if (CheapReadyIn(save, minute) > 0) return null;
            save.CheapScanAt = minute;
            VirusCheapScan scan = new VirusCheapScan { Cookies = CookiesMin + rnd.Next(CookiesSpan) };
            int day = Day(minute);
            foreach (string id in save.Active.ToArray())
            {
                string result = CheapResult(id, rnd);
                scan.Found.Add(new KeyValuePair<string, string>(id, result));
                if (result == Quarantined) Quarantine(save, id, day + QuarantineDays);
                if (result == Removed || result == Quarantined) save.Active.Remove(id);
            }
            return scan;
        }

        internal static void Quarantine(VirusSave save, string id, int returnDay)
        {
            foreach (VirusReturn held in save.Returning)
            {
                if (held.Id != id) continue;
                held.Day = Math.Max(held.Day, returnDay);
                return;
            }
            save.Returning.Add(new VirusReturn { Id = id, Day = returnDay });
        }

        internal static int FreeThreats(VirusSave save, Random rnd)
        {
            return save.Active.Count * FreePerVirus + FreeBaseThreats + rnd.Next(FreeSpan);
        }

        internal static bool Shows(VirusSave save, string appId)
        {
            return (appId != PcGuardApp || save.RealOwned) && (appId != TurvaApp || save.CheapOwned);
        }

        internal static VirusScan Scan(VirusSave save, IVirusDisk disk, int day)
        {
            VirusScan scan = new VirusScan();
            scan.Found.AddRange(save.Active);
            foreach (VirusReturn item in save.Returning) scan.Found.Add(item.Id);
            VirusRestored done = VirusFiles.Restore(disk, save);
            scan.Restored = done.Put;
            scan.Junk = done.Junk;
            scan.Left = save.Moves.Count;
            ClearInfections(save);
            save.ScanDay = day;
            return scan;
        }

        internal static string StateJson(VirusSave save, float wallet)
        {
            StringBuilder b = new StringBuilder();
            b.Append("{\"owned\":").Append(Flag(save.RealOwned));
            b.Append(",\"priceRm\":").Append(PriceRm);
            b.Append(",\"walletRm\":").Append(Math.Round(wallet, 2).ToString("0.##", CultureInfo.InvariantCulture));
            b.Append(",\"blocked\":").Append(save.Blocked);
            b.Append(",\"lastScan\":").Append(save.ScanDay < 0 ? "null" : save.ScanDay.ToString(CultureInfo.InvariantCulture));
            b.Append(",\"quarantine\":[");
            for (int i = 0; i < save.Stolen.Count; i++)
            {
                VirusStolen item = save.Stolen[i];
                if (i > 0) b.Append(',');
                b.Append("{\"name\":\"" + VirusRules.Name(item.Id) + "\",\"day\":" + item.ReceiptDay + ",\"stolenOc\":" + item.Oc + ",\"stolenRm\":" + item.Rm + "}");
            }
            return b.Append("]}").ToString();
        }

        internal static string ScanJson(VirusScan scan, VirusSave save, float wallet)
        {
            StringBuilder b = new StringBuilder("{\"found\":[");
            for (int i = 0; i < scan.Found.Count; i++)
            {
                if (i > 0) b.Append(',');
                b.Append("{\"name\":\"" + VirusRules.Name(scan.Found[i]) + "\",\"result\":\"" + Removed + "\"}");
            }
            b.Append("],\"files\":{\"restored\":" + scan.Restored + ",\"junk\":" + scan.Junk + ",\"left\":" + scan.Left + "},\"state\":");
            return b.Append(StateJson(save, wallet)).Append('}').ToString();
        }

        internal static string BuyJson(VirusBuy result, VirusSave save, float wallet)
        {
            string state = StateJson(save, wallet);
            return state.Substring(0, state.Length - 1) + ",\"known\":" + Flag(result == VirusBuy.Known) + "}";
        }

        internal static string CheapStateJson(VirusSave save, int minute, int wallet)
        {
            int day = Day(minute);
            StringBuilder b = new StringBuilder("{\"owned\":" + Flag(save.CheapOwned));
            b.Append(",\"status\":\"" + CheapStatus(save) + "\"");
            b.Append(",\"priceOc\":" + CheapPriceOc);
            b.Append(",\"daysToBill\":" + (save.CheapOwned ? Math.Max(0, save.CheapNextBill - day) : 0));
            b.Append(",\"lapsedDays\":" + (save.CheapOwned && save.CheapLapsed ? day - save.CheapLapsedSince : 0));
            b.Append(",\"scanReadyIn\":" + CheapReadyIn(save, minute));
            b.Append(",\"lastScan\":" + (save.CheapScanAt < 0 ? "null" : Day(save.CheapScanAt).ToString(CultureInfo.InvariantCulture)));
            return b.Append(",\"walletOc\":" + wallet + "}").ToString();
        }

        internal static string CheapScanJson(VirusCheapScan scan, VirusSave save, int minute, int wallet)
        {
            StringBuilder b = new StringBuilder("{\"found\":[");
            for (int i = 0; i < scan.Found.Count; i++)
            {
                if (i > 0) b.Append(',');
                b.Append("{\"name\":\"" + VirusRules.Name(scan.Found[i].Key) + "\",\"result\":\"" + scan.Found[i].Value + "\"}");
            }
            return b.Append("],\"cookies\":" + scan.Cookies + ",\"state\":" + CheapStateJson(save, minute, wallet) + "}").ToString();
        }

        private static string CheapResult(string id, Random rnd)
        {
            if (id == VirusRules.Turvabar) return Trusted;
            if (id == VirusRules.Hymy || id == VirusRules.Moca) return Removal(rnd, PopupRemovePercent);
            if (id == VirusRules.Sekaisin) return Removal(rnd, WormRemovePercent);
            return Quarantined;
        }

        private static string Removal(Random rnd, int percent)
        {
            return rnd.Next(100) < percent ? Removed : Missed;
        }

        private static bool CheapActive(VirusSave save)
        {
            return save.CheapOwned && !save.CheapLapsed;
        }

        private static string CheapStatus(VirusSave save)
        {
            if (save.CheapOwned) return CheapActive(save) ? "active" : "lapsed";
            return save.CheapEnded ? "ended" : "none";
        }

        private static int Day(int minute)
        {
            return minute / MinutesPerDay;
        }

        private static string Flag(bool value)
        {
            return value ? "true" : "false";
        }
    }
}
