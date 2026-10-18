using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class VirusRuleChecks
    {
        private const int Seed = 4242;
        private const int Rolls = 400000;
        private const int ProtectedRolls = 10000;
        private const int PickRolls = 200;
        private const double Tolerance = 0.003;
        private const int BrowseDays = 20;
        private const int ViewsPerDay = 1000;
        private const int SpamDays = 10000;
        private const int MailDays = 2000;

        private static readonly string[] DesignRows =
        {
            "PopupClaim|50|hymy:40,moca:30,penni:30",
            "FreeRmDownload|85|penni:50,kassa:30,hymy:20",
            "WarezDownload|70|sekaisin:40,lukko:30,moca:30",
            "VoittajaClaim|60|hymy:40,penni:30,kassa:30",
            "SpamAttachment|80|lukko:40,kassa:30,penni:30",
            "ShadyPageView|4|hymy:50,moca:30,sekaisin:20",
            "OtherPageView|1|hymy:60,moca:40",
            "TurvaProBill|20|turvabar:100",
        };

        private static readonly string[] DesignNames =
        {
            "hymy=Hymy.Popup",
            "moca=MocaBar.Toolbar",
            "sekaisin=Sekaisin.Worm",
            "penni=Penni.Skim",
            "kassa=Kassa24.Charger",
            "lukko=Lukko.Locker",
            "turvabar=TurvaPro.Partner",
        };

        private static readonly string[] WarezRows =
        {
            "stalex-insider", "naspo-autobet", "lotto-predictor", "minesweeper-gold", "zigzag-bot",
            "solitaire-keygen", "postal-spy", "eyebook-finder", "torchat-skin", "stalmail-sender",
        };

        internal static List<KeyValuePair<string, bool>> Run()
        {
            List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();
            checks.Add(Check("tables: VirusRules and Catalogue match the design doc literals", TableOk()));
            checks.Add(Check("ids: catalogue ids are unique", UniqueIds()));
            checks.Add(Check("ids: every pool id is in the catalogue", PoolIdsKnown()));
            checks.Add(Check("chances: every vector matches its table chance within 0.3 points over 400000 rolls", ChancesOk()));
            checks.Add(Check("pools: each pick follows its table weights within 0.3 points", WeightsOk()));
            checks.Add(Check("protection: real protection gives 0 infections in 10000 rolls and does block", RealOk()));
            checks.Add(Check("protection: cheap protection blocks 25% plus or minus 0.3 points", CheapOk()));
            checks.Add(Check("pick: never returns an active id", PickSkipsActive()));
            checks.Add(Check("pick: a pool with every virus active returns clean", FullPoolClean()));
            checks.Add(Check("enabled off: every decision is clean", DisabledClean()));
            checks.Add(Check("effects: Moca gives one pop-up, 1500 ms lag and the ad wallpaper", MocaEffects()));
            checks.Add(Check("pulse: never more than 2 pop-ups, even with every virus active", PulseCapOk()));
            checks.Add(Check("pulse: a pop-up virus adds a pop-up 30% of the time within 0.3 points", PulseChanceOk()));
            checks.Add(Check("pulse: no active virus gives no pulse", PulseQuietOk()));
            checks.Add(Check("shady: the shady hosts are the three design hosts and only they are shady", ShadyHostsOk()));
            checks.Add(Check("browse: at most one random infection per game day over 1000 page views a day", BrowseCapOk()));
            checks.Add(Check("spam: at most one spam mail per game day, about 40 percent of days", SpamDayOk()));
            checks.Add(Check("spam: none once the mailbox holds 180 items, and some below 180", SpamMailboxOk()));
            checks.Add(Check("spam: never due at hours 0 to 8", SpamHoursOk()));
            checks.Add(Check("downloads: a listed item of its own site is a download, an unknown or other-site item is rejected", DownloadsOk()));
            checks.Add(Check("downloads: each of the ten Kolhola rows has its own valid file under /downloads", WarezRowsOk()));
            checks.AddRange(VirusTheftChecks.Run());
            checks.AddRange(VirusFileChecks.Run());
            checks.AddRange(VirusAvChecks.Run());
            checks.AddRange(VirusCheapChecks.Run());
            return checks;
        }

        private static KeyValuePair<string, bool> Check(string name, bool ok)
        {
            return new KeyValuePair<string, bool>(name, ok);
        }

        private static List<VirusVector> Vectors()
        {
            List<VirusVector> all = new List<VirusVector>();
            foreach (VirusVector vector in Enum.GetValues(typeof(VirusVector))) all.Add(vector);
            return all;
        }

        private static List<string> PoolIds(VirusVector vector)
        {
            List<string> ids = new List<string>();
            foreach (KeyValuePair<string, int> item in VirusRules.Source(vector).Pool) ids.Add(item.Key);
            return ids;
        }

        private static bool TableOk()
        {
            List<string> rows = new List<string>();
            foreach (VirusVector vector in Vectors())
            {
                VirusSource source = VirusRules.Source(vector);
                List<string> pool = new List<string>();
                foreach (KeyValuePair<string, int> item in source.Pool) pool.Add(item.Key + ":" + item.Value);
                rows.Add(vector + "|" + source.Percent + "|" + string.Join(",", pool.ToArray()));
            }
            List<string> names = new List<string>();
            foreach (KeyValuePair<string, string> item in VirusRules.Catalogue) names.Add(item.Key + "=" + item.Value);
            return string.Join(";", DesignRows) == string.Join(";", rows.ToArray())
                && string.Join(";", DesignNames) == string.Join(";", names.ToArray());
        }

        private static bool UniqueIds()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (KeyValuePair<string, string> item in VirusRules.Catalogue)
            {
                if (!seen.Add(item.Key)) return false;
            }
            return true;
        }

        private static bool PoolIdsKnown()
        {
            foreach (VirusVector vector in Vectors())
            {
                foreach (string id in PoolIds(vector))
                {
                    if (VirusRules.Name(id) == null) return false;
                }
            }
            return true;
        }

        private static bool ChancesOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            foreach (VirusVector vector in Vectors())
            {
                int hits = 0;
                for (int i = 0; i < Rolls; i++)
                {
                    if (VirusRules.Decide(save, true, vector, VirusProtect.None, rnd).Outcome == VirusOutcome.Infected) hits++;
                }
                if (Math.Abs(hits / (double)Rolls - VirusRules.Source(vector).Percent / 100.0) > Tolerance) return false;
            }
            return true;
        }

        private static bool WeightsOk()
        {
            Random rnd = new Random(Seed);
            List<string> none = new List<string>();
            foreach (VirusVector vector in Vectors())
            {
                KeyValuePair<string, int>[] pool = VirusRules.Source(vector).Pool;
                Dictionary<string, int> counts = new Dictionary<string, int>();
                int total = 0;
                foreach (KeyValuePair<string, int> item in pool)
                {
                    counts[item.Key] = 0;
                    total += item.Value;
                }
                for (int i = 0; i < Rolls; i++)
                {
                    string id = VirusRules.Pick(vector, none, rnd);
                    if (id == null || !counts.ContainsKey(id)) return false;
                    counts[id]++;
                }
                foreach (KeyValuePair<string, int> item in pool)
                {
                    if (Math.Abs(counts[item.Key] / (double)Rolls - item.Value / (double)total) > Tolerance) return false;
                }
            }
            return true;
        }

        private static bool RealOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            int blocked = 0;
            foreach (VirusVector vector in Vectors())
            {
                for (int i = 0; i < ProtectedRolls; i++)
                {
                    VirusOutcome outcome = VirusRules.Decide(save, true, vector, VirusProtect.Real, rnd).Outcome;
                    if (outcome == VirusOutcome.Infected) return false;
                    if (outcome == VirusOutcome.Blocked) blocked++;
                }
            }
            return blocked > 0;
        }

        private static bool CheapOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            int blocked = 0;
            int infected = 0;
            for (int i = 0; i < Rolls; i++)
            {
                VirusOutcome outcome = VirusRules.Decide(save, true, VirusVector.PopupClaim, VirusProtect.Cheap, rnd).Outcome;
                if (outcome == VirusOutcome.Blocked) blocked++;
                if (outcome == VirusOutcome.Infected) infected++;
            }
            return Math.Abs(blocked / (double)(blocked + infected) - 0.25) <= Tolerance;
        }

        private static bool PickSkipsActive()
        {
            Random rnd = new Random(Seed);
            foreach (VirusVector vector in Vectors())
            {
                List<string> ids = PoolIds(vector);
                for (int mask = 0; mask < (1 << ids.Count); mask++)
                {
                    List<string> active = new List<string>();
                    for (int bit = 0; bit < ids.Count; bit++)
                    {
                        if ((mask & (1 << bit)) != 0) active.Add(ids[bit]);
                    }
                    for (int i = 0; i < PickRolls; i++)
                    {
                        string id = VirusRules.Pick(vector, active, rnd);
                        if (id == null)
                        {
                            if (active.Count < ids.Count) return false;
                        }
                        else if (active.Contains(id) || !ids.Contains(id))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private static bool FullPoolClean()
        {
            Random rnd = new Random(Seed);
            foreach (VirusVector vector in Vectors())
            {
                VirusSave save = new VirusSave();
                save.Active.AddRange(PoolIds(vector));
                for (int i = 0; i < ProtectedRolls; i++)
                {
                    if (VirusRules.Decide(save, true, vector, VirusProtect.None, rnd).Outcome != VirusOutcome.Clean) return false;
                }
            }
            return true;
        }

        private static bool DisabledClean()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            foreach (VirusVector vector in Vectors())
            {
                foreach (VirusProtect protect in new[] { VirusProtect.None, VirusProtect.Cheap, VirusProtect.Real })
                {
                    for (int i = 0; i < ProtectedRolls; i++)
                    {
                        if (VirusRules.Decide(save, false, vector, protect, rnd).Outcome != VirusOutcome.Clean) return false;
                    }
                }
            }
            return true;
        }

        private static bool MocaEffects()
        {
            VirusEffects fx = VirusRules.Effects(new List<string> { VirusRules.Moca }, false);
            return fx.Popups == 1 && fx.Lag == 1500 && fx.Wall && !fx.Shuffle && !fx.Errors && !fx.Nag;
        }

        private static bool PulseCapOk()
        {
            Random rnd = new Random(Seed);
            List<string> popups = new List<string>();
            for (int i = 0; i < 5; i++) popups.Add(VirusRules.Hymy);
            popups.Add(VirusRules.Moca);
            for (int i = 0; i < Rolls; i++)
            {
                if (VirusRules.Pulse(popups, false, rnd).Popups > 2) return false;
            }
            return true;
        }

        private static bool PulseChanceOk()
        {
            Random rnd = new Random(Seed);
            List<string> hymy = new List<string> { VirusRules.Hymy };
            int hits = 0;
            for (int i = 0; i < Rolls; i++) hits += VirusRules.Pulse(hymy, false, rnd).Popups;
            return Math.Abs(hits / (double)Rolls - 0.30) <= Tolerance;
        }

        private static bool ShadyHostsOk()
        {
            string[] design = { "www.free-rm.ts", "www.kolhola-warez.ts", "www.voittaja.ts" };
            return string.Join(",", VirusRules.ShadyHosts) == string.Join(",", design)
                && VirusRules.IsShady("www.free-rm.ts") && VirusRules.IsShady("www.kolhola-warez.ts") && VirusRules.IsShady("www.voittaja.ts")
                && !VirusRules.IsShady("www.stalburg.ts") && !VirusRules.IsShady("www.stalburgtimes.com");
        }

        private static bool BrowseCapOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            int infectedDays = 0;
            for (int day = 0; day < BrowseDays; day++)
            {
                save.Active.Clear();
                int infections = 0;
                for (int view = 0; view < ViewsPerDay; view++)
                {
                    if (VirusRules.Browse(save, true, view % 2 == 0, day, VirusProtect.None, rnd).Outcome == VirusOutcome.Infected) infections++;
                }
                if (infections > 1) return false;
                if (infections == 1) infectedDays++;
            }
            return infectedDays >= BrowseDays - 5;
        }

        private static bool SpamDayOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            int sent = 0;
            for (int day = 0; day < SpamDays; day++)
            {
                int perDay = 0;
                for (int hour = 0; hour < 24; hour++)
                {
                    if (VirusRules.SpamDue(save, day, hour, 0, rnd)) perDay++;
                }
                if (perDay > 1) return false;
                sent += perDay;
            }
            return Math.Abs(sent / (double)SpamDays - VirusRules.SpamPercent / 100.0) <= 0.02;
        }

        private static bool SpamMailboxOk()
        {
            Random rnd = new Random(Seed);
            VirusSave full = new VirusSave();
            for (int day = 0; day < MailDays; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    if (VirusRules.SpamDue(full, day, hour, VirusRules.SpamMailboxMax, rnd)) return false;
                }
            }
            VirusSave room = new VirusSave();
            int sent = 0;
            for (int day = 0; day < MailDays; day++)
            {
                for (int hour = 0; hour < 24; hour++)
                {
                    if (VirusRules.SpamDue(room, day, hour, VirusRules.SpamMailboxMax - 1, rnd)) sent++;
                }
            }
            return sent > 0;
        }

        private static bool SpamHoursOk()
        {
            Random rnd = new Random(Seed);
            for (int day = 0; day < SpamDays; day++)
            {
                for (int hour = 0; hour < VirusRules.SpamHour; hour++)
                {
                    if (VirusRules.SpamDue(new VirusSave(), day, hour, 0, rnd)) return false;
                }
            }
            return true;
        }

        private static bool DownloadsOk()
        {
            return VirusRules.DownloadFile(VirusVector.FreeRmDownload, "generator") == "generator.exe"
                && VirusRules.DownloadFile(VirusVector.WarezDownload, "solitaire-keygen") == "solitaire_keygen.exe"
                && VirusRules.DownloadFile(VirusVector.FreeRmDownload, "solitaire-keygen") == null
                && VirusRules.DownloadFile(VirusVector.WarezDownload, "generator") == null
                && VirusRules.DownloadFile(VirusVector.WarezDownload, "unknown-item") == null;
        }

        private static bool WarezRowsOk()
        {
            HashSet<string> files = new HashSet<string>();
            foreach (string item in WarezRows)
            {
                string file = VirusRules.DownloadFile(VirusVector.WarezDownload, item);
                if (file == null || !OsPaths.ValidPath("/downloads/" + file) || !files.Add(file)) return false;
            }
            return WarezRows.Length == 10 && files.Count == 10;
        }

        private static bool PulseQuietOk()
        {
            Random rnd = new Random(Seed);
            List<string> none = new List<string>();
            for (int i = 0; i < Rolls; i++)
            {
                VirusPulse pulse = VirusRules.Pulse(none, false, rnd);
                if (pulse.Popups != 0 || pulse.Error || pulse.Nag) return false;
            }
            return true;
        }
    }
}
