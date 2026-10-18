using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal enum VirusVector
    {
        PopupClaim,
        FreeRmDownload,
        WarezDownload,
        VoittajaClaim,
        SpamAttachment,
        ShadyPageView,
        OtherPageView,
        TurvaProBill,
    }

    internal enum VirusProtect
    {
        None,
        Cheap,
        Real,
    }

    internal enum VirusOutcome
    {
        Clean,
        Infected,
        Blocked,
    }

    internal sealed class VirusDecision
    {
        internal readonly VirusOutcome Outcome;
        internal readonly string Id;

        internal VirusDecision(VirusOutcome outcome, string id)
        {
            Outcome = outcome;
            Id = id;
        }
    }

    internal sealed class VirusSource
    {
        internal readonly int Percent;
        internal readonly KeyValuePair<string, int>[] Pool;

        internal VirusSource(int percent, params KeyValuePair<string, int>[] pool)
        {
            Percent = percent;
            Pool = pool;
        }
    }

    internal sealed class VirusEffects
    {
        internal int Popups;
        internal int Lag;
        internal bool Wall;
        internal bool Shuffle;
        internal bool Errors;
        internal bool Nag;
    }

    internal sealed class VirusPulse
    {
        internal int Popups;
        internal bool Error;
        internal bool Nag;
    }

    internal sealed class VirusSkim
    {
        internal readonly string Id;
        internal readonly int Oc;
        internal readonly int Rm;

        internal VirusSkim(string id, int oc, int rm)
        {
            Id = id;
            Oc = oc;
            Rm = rm;
        }
    }

    internal static class VirusRules
    {
        internal const string Hymy = "hymy";
        internal const string Moca = "moca";
        internal const string Sekaisin = "sekaisin";
        internal const string Penni = "penni";
        internal const string Kassa = "kassa";
        internal const string Lukko = "lukko";
        internal const string Turvabar = "turvabar";
        internal const int CheapBlockPercent = 25;
        private const int MocaLag = 1500;
        private const int SekaisinLag = 1000;
        private const int MaxLag = 3000;
        private const int PulsePopupPercent = 30;
        private const int PulseMaxPopups = 2;
        private const int PulseErrorPercent = 20;
        private const int PulseNagPercent = 30;
        private const int PulseLapsedNagPercent = 25;
        internal const int SpamPercent = 40;
        internal const int SpamHour = 9;
        internal const int SpamMailboxMax = 180;
        internal const int SpamAttachPercent = 60;
        internal const int OcDayCap = 60;
        internal const int RmDayCap = 6;
        internal const int OcWeekCap = 200;
        internal const int RmWeekCap = 20;
        internal const int OcFloor = 100;
        internal const int RmFloor = 50;
        internal const int PennyPercent = 25;
        internal const int PennyLow = 5;
        internal const int PennyHigh = 20;
        internal const int KassaPercent = 50;
        internal const int KassaLow = 2;
        internal const int KassaHigh = 6;
        internal const double MaxBankRm = 1000000000;
        private const int DaysPerWeek = 7;
        internal const string Docs = "/docs";
        internal const string Vault = "/vault";

        internal static readonly KeyValuePair<string, string>[] Catalogue =
        {
            new KeyValuePair<string, string>(Hymy, "Hymy.Popup"),
            new KeyValuePair<string, string>(Moca, "MocaBar.Toolbar"),
            new KeyValuePair<string, string>(Sekaisin, "Sekaisin.Worm"),
            new KeyValuePair<string, string>(Penni, "Penni.Skim"),
            new KeyValuePair<string, string>(Kassa, "Kassa24.Charger"),
            new KeyValuePair<string, string>(Lukko, "Lukko.Locker"),
            new KeyValuePair<string, string>(Turvabar, "TurvaPro.Partner"),
        };

        internal static readonly string[] ShadyHosts = { "www.free-rm.ts", "www.kolhola-warez.ts", "www.voittaja.ts" };

        private static readonly Dictionary<VirusVector, VirusSource> Sources = new Dictionary<VirusVector, VirusSource>
        {
            { VirusVector.PopupClaim, new VirusSource(50, Weight(Hymy, 40), Weight(Moca, 30), Weight(Penni, 30)) },
            { VirusVector.FreeRmDownload, new VirusSource(85, Weight(Penni, 50), Weight(Kassa, 30), Weight(Hymy, 20)) },
            { VirusVector.WarezDownload, new VirusSource(70, Weight(Sekaisin, 40), Weight(Lukko, 30), Weight(Moca, 30)) },
            { VirusVector.VoittajaClaim, new VirusSource(60, Weight(Hymy, 40), Weight(Penni, 30), Weight(Kassa, 30)) },
            { VirusVector.SpamAttachment, new VirusSource(80, Weight(Lukko, 40), Weight(Kassa, 30), Weight(Penni, 30)) },
            { VirusVector.ShadyPageView, new VirusSource(4, Weight(Hymy, 50), Weight(Moca, 30), Weight(Sekaisin, 20)) },
            { VirusVector.OtherPageView, new VirusSource(1, Weight(Hymy, 60), Weight(Moca, 40)) },
            { VirusVector.TurvaProBill, new VirusSource(20, Weight(Turvabar, 100)) },
        };

        private static readonly Dictionary<VirusVector, Dictionary<string, string>> Downloads = new Dictionary<VirusVector, Dictionary<string, string>>
        {
            { VirusVector.FreeRmDownload, new Dictionary<string, string> { { "generator", "generator.exe" } } },
            {
                VirusVector.WarezDownload,
                new Dictionary<string, string>
                {
                    { "stalex-insider", "stalex_insider_2.0.exe" },
                    { "naspo-autobet", "naspobet_autobet.exe" },
                    { "lotto-predictor", "lotto_predictor_100.exe" },
                    { "minesweeper-gold", "minesweeper_gold_crack.exe" },
                    { "zigzag-bot", "zigzag_views_bot_2001.exe" },
                    { "solitaire-keygen", "solitaire_keygen.exe" },
                    { "postal-spy", "postal_messenger_spy_1.3.exe" },
                    { "eyebook-finder", "eyebook_finder.exe" },
                    { "torchat-skin", "torchat_turnip_skin.exe" },
                    { "stalmail-sender", "stalburg_mail_sender_pro.exe" },
                }
            },
        };

        internal static VirusSource Source(VirusVector vector)
        {
            return Sources[vector];
        }

        internal static string DownloadFile(VirusVector vector, string item)
        {
            string file;
            return Downloads[vector].TryGetValue(item, out file) ? file : null;
        }

        internal static string Name(string id)
        {
            foreach (KeyValuePair<string, string> item in Catalogue)
            {
                if (item.Key == id) return item.Value;
            }
            return null;
        }

        internal static VirusProtect ProtectionOf(VirusSave save, VirusVector vector)
        {
            if (save.RealOwned) return VirusProtect.Real;
            return save.CheapOwned && !save.CheapLapsed && vector != VirusVector.TurvaProBill ? VirusProtect.Cheap : VirusProtect.None;
        }

        internal static bool CountRealBlock(VirusSave save, VirusDecision decision, VirusProtect protect)
        {
            if (decision.Outcome != VirusOutcome.Blocked || protect != VirusProtect.Real) return false;
            save.Blocked++;
            return true;
        }

        internal static string Pick(VirusVector vector, List<string> active, Random rnd)
        {
            KeyValuePair<string, int>[] pool = Sources[vector].Pool;
            int total = 0;
            foreach (KeyValuePair<string, int> item in pool)
            {
                if (!active.Contains(item.Key)) total += item.Value;
            }
            if (total == 0) return null;
            int roll = rnd.Next(total);
            foreach (KeyValuePair<string, int> item in pool)
            {
                if (active.Contains(item.Key)) continue;
                if (roll < item.Value) return item.Key;
                roll -= item.Value;
            }
            return null;
        }

        internal static VirusDecision Decide(VirusSave save, bool enabled, VirusVector vector, VirusProtect protect, Random rnd)
        {
            VirusSource source = Sources[vector];
            if (!enabled || rnd.Next(100) >= source.Percent) return new VirusDecision(VirusOutcome.Clean, null);
            string id = Pick(vector, save.Active, rnd);
            if (id == null) return new VirusDecision(VirusOutcome.Clean, null);
            if (protect == VirusProtect.Real) return new VirusDecision(VirusOutcome.Blocked, id);
            if (protect == VirusProtect.Cheap && rnd.Next(100) < CheapBlockPercent) return new VirusDecision(VirusOutcome.Blocked, id);
            return new VirusDecision(VirusOutcome.Infected, id);
        }

        internal static bool IsShady(string host)
        {
            return Array.IndexOf(ShadyHosts, host) >= 0;
        }

        internal static VirusDecision Browse(VirusSave save, bool enabled, bool shady, int day, VirusProtect protect, Random rnd)
        {
            if (save.LastRandomDay == day) return new VirusDecision(VirusOutcome.Clean, null);
            VirusDecision decision = Decide(save, enabled, shady ? VirusVector.ShadyPageView : VirusVector.OtherPageView, protect, rnd);
            if (decision.Outcome != VirusOutcome.Clean) save.LastRandomDay = day;
            return decision;
        }

        internal static bool SpamDue(VirusSave save, int day, int hour, int mailItems, Random rnd)
        {
            if (hour < SpamHour || save.SpamDay == day || mailItems >= SpamMailboxMax) return false;
            save.SpamDay = day;
            return rnd.Next(100) < SpamPercent;
        }

        internal static VirusEffects Effects(List<string> active, bool lapsed)
        {
            VirusEffects fx = new VirusEffects();
            foreach (string id in active)
            {
                if (id == Hymy || id == Moca) fx.Popups++;
                if (id == Moca)
                {
                    fx.Wall = true;
                    fx.Lag += MocaLag;
                }
                if (id == Sekaisin)
                {
                    fx.Shuffle = true;
                    fx.Errors = true;
                    fx.Lag += SekaisinLag;
                }
                if (id == Turvabar) fx.Nag = true;
            }
            if (lapsed) fx.Nag = true;
            fx.Lag = Math.Min(fx.Lag, MaxLag);
            return fx;
        }

        internal static VirusPulse Pulse(List<string> active, bool lapsed, Random rnd)
        {
            VirusPulse pulse = new VirusPulse();
            foreach (string id in active)
            {
                if ((id == Hymy || id == Moca) && pulse.Popups < PulseMaxPopups && rnd.Next(100) < PulsePopupPercent) pulse.Popups++;
                if (id == Sekaisin && rnd.Next(100) < PulseErrorPercent) pulse.Error = true;
                if (id == Turvabar && rnd.Next(100) < PulseNagPercent) pulse.Nag = true;
            }
            if (lapsed && rnd.Next(100) < PulseLapsedNagPercent) pulse.Nag = true;
            return pulse;
        }

        internal static List<VirusSkim> Skim(VirusSave save, bool enabled, int day, int wallet, int bankRm, bool bank, Random rnd)
        {
            List<VirusSkim> takes = new List<VirusSkim>();
            if (!enabled) return takes;
            ResetCaps(save, day);
            if (save.Active.Contains(Penni) && rnd.Next(100) < PennyPercent)
            {
                int room = Math.Min(OcDayCap - save.DayOc, OcWeekCap - save.WeekOc);
                int oc = SkimAmount(PennyLow, PennyHigh, room, wallet - OcFloor, rnd);
                if (oc > 0) takes.Add(new VirusSkim(Penni, oc, 0));
            }
            if (bank && save.Active.Contains(Kassa) && save.KassaDay != day)
            {
                save.KassaDay = day;
                if (rnd.Next(100) < KassaPercent)
                {
                    int room = Math.Min(RmDayCap - save.DayRm, RmWeekCap - save.WeekRm);
                    int rm = SkimAmount(KassaLow, KassaHigh, room, bankRm - RmFloor, rnd);
                    if (rm > 0) takes.Add(new VirusSkim(Kassa, 0, rm));
                }
            }
            return takes;
        }

        internal static int BankBalance(double rm)
        {
            if (double.IsNaN(rm) || rm < 0) return 0;
            return (int)Math.Floor(Math.Min(rm, MaxBankRm));
        }

        internal static int SkimAmount(int low, int high, int room, int available, Random rnd)
        {
            int amount = Math.Min(low + rnd.Next(high - low + 1), Math.Min(room, available));
            return amount < low ? 0 : amount;
        }

        internal static bool Record(VirusSave save, VirusSkim take, int day)
        {
            ResetCaps(save, day);
            save.DayOc += take.Oc;
            save.WeekOc += take.Oc;
            save.DayRm += take.Rm;
            save.WeekRm += take.Rm;
            VirusStolen total = Stolen(save, take.Id);
            total.Oc += take.Oc;
            total.Rm += take.Rm;
            bool receipt = total.ReceiptDay != day;
            total.ReceiptDay = day;
            return receipt;
        }

        internal static string RenamePath(string path)
        {
            int slash = path.LastIndexOf('/');
            int dot = path.LastIndexOf('.');
            if (slash < 0 || dot <= slash + 1) return null;
            string renamed = path.Substring(0, slash + 1) + Leet(path.Substring(slash + 1, dot - slash - 1)) + "_x" + path.Substring(dot);
            return OsPaths.ValidPath(renamed) ? renamed : null;
        }

        internal static string VaultPath(int seq)
        {
            return Vault + "/v" + seq + ".dat";
        }

        internal static string JunkPath(int seq)
        {
            return Docs + "/README_NOW_" + seq + ".txt";
        }

        internal static string StubPath(string path)
        {
            string stub = path + ".lukko";
            return OsPaths.ValidPath(stub) ? stub : null;
        }

        internal static string RestoredPath(string path, int n)
        {
            int dot = path.LastIndexOf('.');
            if (dot <= 0) return null;
            string tag = n == 1 ? " (restored)" : " (restored " + n + ")";
            string restored = path.Substring(0, dot) + tag + path.Substring(dot);
            if (OsPaths.ValidPath(restored)) return restored;
            string shortName = Docs + "/restored_" + n + path.Substring(dot);
            return OsPaths.ValidPath(shortName) ? shortName : null;
        }

        internal static string RecoveredPath(int n)
        {
            return Docs + "/recovered_" + n + ".txt";
        }

        private static string Leet(string name)
        {
            return name.Replace('o', '0').Replace('e', '3').Replace('a', '4').Replace('i', '1');
        }

        private static void ResetCaps(VirusSave save, int day)
        {
            if (save.CapDay != day)
            {
                save.CapDay = day;
                save.DayOc = 0;
                save.DayRm = 0;
            }
            if (save.CapWeek != day / DaysPerWeek)
            {
                save.CapWeek = day / DaysPerWeek;
                save.WeekOc = 0;
                save.WeekRm = 0;
            }
        }

        private static VirusStolen Stolen(VirusSave save, string id)
        {
            foreach (VirusStolen item in save.Stolen)
            {
                if (item.Id == id) return item;
            }
            VirusStolen added = new VirusStolen { Id = id };
            save.Stolen.Add(added);
            return added;
        }

        private static KeyValuePair<string, int> Weight(string id, int weight)
        {
            return new KeyValuePair<string, int>(id, weight);
        }
    }
}
