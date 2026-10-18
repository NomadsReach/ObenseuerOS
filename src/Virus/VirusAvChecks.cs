using System;
using System.Collections.Generic;
using System.Text;

namespace ObenseuerOS
{
    internal static class VirusAvChecks
    {
        private const int MaxBytes = 12 * 1024;
        private const int Seed = 9001;
        private const int Trials = 10000;

        internal static List<KeyValuePair<string, bool>> Run()
        {
            List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();
            checks.Add(Check("buy: a repeat purchase charges once, answers known and the price is 500 RM", RepeatOk()));
            checks.Add(Check("buy: a short wallet is denied and spends nothing, and a refused spend does the same", ShortOk()));
            checks.Add(Check("scan: after a scan Active, Returning and Moves are empty, the files are back and the scan day is set", ScanOk()));
            checks.Add(Check("scan: the report lists each virus as removed and counts the restored and left files", ReportOk()));
            checks.Add(Check("scan: a file that cannot be put back stays in the save and is counted as left", LeftOk()));
            checks.Add(Check("scan: a deleted file or an edited junk file is not counted, and an edited junk file is kept", HonestCountOk()));
            checks.Add(Check("clean: clearing infections empties Active and Returning, and reports a change only once", ClearOk()));
            checks.Add(Check("quarantine: with PC Master Guard owned, a due return is blocked and counted, and never comes back", GuardReturnOk()));
            checks.Add(Check("protection: PC Master Guard gives real protection on every vector, even with TurvaPro, and no infection gets through", GuardProtectOk()));
            checks.Add(Check("blocks: each real block adds one to Blocked, and a cheap block or an infection adds none", BlockCountOk()));
            checks.Add(Check("state: the worst-case av.state and av.scan JSON are under 12 KB", JsonOk()));
            checks.Add(Check("shows: pcguard is hidden until bought, and the other apps always show", ShowsOk()));
            checks.Add(Check("quarantine: a virus quarantined again keeps one return entry with the later day, and counts Blocked once", RequarantineOk()));
            return checks;
        }

        private static KeyValuePair<string, bool> Check(string name, bool ok)
        {
            return new KeyValuePair<string, bool>(name, ok);
        }

        private static bool RepeatOk()
        {
            VirusSave save = new VirusSave();
            int spent = 0;
            Func<int, bool> spend = amount => { spent += amount; return true; };
            VirusBuy first = VirusAvRules.Buy(save, true, VirusAvRules.PriceRm, spend);
            VirusBuy second = VirusAvRules.Buy(save, true, VirusAvRules.PriceRm, spend);
            return first == VirusBuy.Bought && second == VirusBuy.Known && spent == VirusAvRules.PriceRm && save.RealOwned && VirusAvRules.PriceRm == 500
                && VirusAvRules.BuyJson(first, save, 0f).Contains("\"known\":false") && VirusAvRules.BuyJson(second, save, 0f).Contains("\"known\":true");
        }

        private static bool ShortOk()
        {
            VirusSave save = new VirusSave();
            int spent = 0;
            Func<int, bool> spend = amount => { spent += amount; return true; };
            VirusBuy poor = VirusAvRules.Buy(save, false, VirusAvRules.PriceRm, spend);
            VirusBuy refused = VirusAvRules.Buy(save, true, VirusAvRules.PriceRm, amount => false);
            return poor == VirusBuy.Short && refused == VirusBuy.Short && spent == 0 && !save.RealOwned;
        }

        private static bool ScanOk()
        {
            VirusSave save = Infected();
            FakeDisk disk = new FakeDisk();
            disk.Files["/docs/notes.txt"] = "my notes";
            disk.Files["/docs/letter.txt"] = "dear sir";
            VirusFiles.Stash(disk, save, "/docs/notes.txt");
            VirusFiles.Scramble(disk, save, "/docs/letter.txt");
            VirusAvRules.Scan(save, disk, 5);
            return save.Active.Count == 0 && save.Returning.Count == 0 && save.Moves.Count == 0 && save.ScanDay == 5
                && disk.Read("/docs/notes.txt") == "my notes" && disk.Read("/docs/letter.txt") == "dear sir" && disk.Files.Count == 2;
        }

        private static bool ReportOk()
        {
            VirusSave save = Infected();
            FakeDisk disk = new FakeDisk();
            disk.Files["/docs/notes.txt"] = "my notes";
            VirusFiles.Stash(disk, save, "/docs/notes.txt");
            VirusScan scan = VirusAvRules.Scan(save, disk, 5);
            string json = VirusAvRules.ScanJson(scan, save, 12.5f);
            return scan.Found.Count == 3 && scan.Found.Contains(VirusRules.Lukko) && scan.Found.Contains(VirusRules.Kassa)
                && scan.Restored == 1 && scan.Junk == 0 && scan.Left == 0
                && json.Contains("\"name\":\"Lukko.Locker\",\"result\":\"removed\"") && json.Contains("\"files\":{\"restored\":1,\"junk\":0,\"left\":0}");
        }

        private static bool LeftOk()
        {
            VirusSave save = new VirusSave();
            FakeDisk disk = new FakeDisk();
            disk.Files["/docs/notes.txt"] = "my notes";
            VirusFiles.Stash(disk, save, "/docs/notes.txt");
            disk.NoMove = true;
            VirusScan scan = VirusAvRules.Scan(save, disk, 5);
            return scan.Restored == 0 && scan.Left == 1 && save.Moves.Count == 1
                && VirusAvRules.ScanJson(scan, save, 0f).Contains("\"files\":{\"restored\":0,\"junk\":0,\"left\":1}");
        }

        private static bool HonestCountOk()
        {
            VirusSave save = new VirusSave();
            FakeDisk disk = new FakeDisk();
            disk.Files["/docs/gone.txt"] = "gone";
            disk.Files["/docs/notes.txt"] = "my notes";
            VirusFiles.Stash(disk, save, "/docs/gone.txt");
            VirusFiles.Stash(disk, save, "/docs/notes.txt");
            disk.Files.Remove("/vault/v1.dat");
            save.Junk.Add(new VirusJunk { Path = "/docs/README_NOW_1.txt", Text = "junk" });
            save.Junk.Add(new VirusJunk { Path = "/docs/README_NOW_2.txt", Text = "junk" });
            disk.Files["/docs/README_NOW_1.txt"] = "edited";
            disk.Files["/docs/README_NOW_2.txt"] = "junk";
            VirusScan scan = VirusAvRules.Scan(save, disk, 5);
            return scan.Restored == 1 && scan.Junk == 1 && scan.Left == 0 && save.Junk.Count == 0
                && disk.Read("/docs/notes.txt") == "my notes" && disk.Read("/docs/README_NOW_1.txt") == "edited";
        }

        private static bool ClearOk()
        {
            VirusSave save = Infected();
            bool cleared = VirusAvRules.ClearInfections(save);
            return cleared && save.Active.Count == 0 && save.Returning.Count == 0 && !VirusAvRules.ClearInfections(save);
        }

        private static bool RequarantineOk()
        {
            VirusSave save = new VirusSave();
            save.RealOwned = true;
            VirusAvRules.Quarantine(save, "penni", 5);
            VirusAvRules.Quarantine(save, "penni", 9);
            VirusAvRules.Quarantine(save, "penni", 7);
            bool one = save.Returning.Count == 1 && save.Returning[0].Day == 9;
            VirusAvRules.ReturnDue(save, true, 9);
            return one && save.Returning.Count == 0 && save.Blocked == 1;
        }

        private static bool GuardReturnOk()
        {
            VirusSave save = new VirusSave { RealOwned = true };
            save.Returning.Add(new VirusReturn { Id = VirusRules.Penni, Day = 3 });
            bool moved = VirusAvRules.ReturnDue(save, true, 3);
            return moved && save.Active.Count == 0 && save.Returning.Count == 0 && save.Blocked == 1;
        }

        private static bool GuardProtectOk()
        {
            VirusSave save = new VirusSave { RealOwned = true, CheapOwned = true };
            save.Active.Add(VirusRules.Hymy);
            save.Active.Add(VirusRules.Sekaisin);
            Random rnd = new Random(Seed);
            foreach (VirusVector vector in (VirusVector[])Enum.GetValues(typeof(VirusVector)))
            {
                VirusProtect protect = VirusRules.ProtectionOf(save, vector);
                if (protect != VirusProtect.Real) return false;
                for (int i = 0; i < Trials; i++)
                {
                    if (VirusRules.Decide(save, true, vector, protect, rnd).Outcome == VirusOutcome.Infected) return false;
                }
            }
            return true;
        }

        private static bool BlockCountOk()
        {
            VirusSave save = new VirusSave { RealOwned = true };
            save.Active.Add(VirusRules.Hymy);
            Random rnd = new Random(Seed);
            int counted = 0;
            for (int i = 0; i < Trials; i++)
            {
                VirusProtect protect = VirusRules.ProtectionOf(save, VirusVector.PopupClaim);
                if (VirusRules.CountRealBlock(save, VirusRules.Decide(save, true, VirusVector.PopupClaim, protect, rnd), protect)) counted++;
            }
            VirusSave cheap = new VirusSave { CheapOwned = true };
            bool cheapCounts = VirusRules.CountRealBlock(cheap, new VirusDecision(VirusOutcome.Blocked, VirusRules.Moca), VirusProtect.Cheap);
            bool infectionCounts = VirusRules.CountRealBlock(save, new VirusDecision(VirusOutcome.Infected, VirusRules.Moca), VirusProtect.Real);
            return counted > 0 && save.Blocked == counted && !cheapCounts && cheap.Blocked == 0 && !infectionCounts;
        }

        private static bool JsonOk()
        {
            VirusSave save = new VirusSave();
            VirusScan scan = new VirusScan();
            foreach (KeyValuePair<string, string> item in VirusRules.Catalogue)
            {
                save.Active.Add(item.Key);
                save.Stolen.Add(new VirusStolen { Id = item.Key, Oc = 999999, Rm = 999999, ReceiptDay = 99999 });
                save.Returning.Add(new VirusReturn { Id = item.Key, Day = 99999 });
                scan.Found.Add(item.Key);
                scan.Found.Add(item.Key);
            }
            save.RealOwned = true;
            save.Blocked = 999999;
            save.ScanDay = 99999;
            scan.Restored = 999;
            scan.Junk = 999;
            return Encoding.UTF8.GetByteCount(VirusAvRules.StateJson(save, 16000000f)) < MaxBytes
                && Encoding.UTF8.GetByteCount(VirusAvRules.ScanJson(scan, save, 16000000f)) < MaxBytes;
        }

        private static bool ShowsOk()
        {
            VirusSave save = new VirusSave();
            bool hidden = !VirusAvRules.Shows(save, VirusAvRules.PcGuardApp);
            save.RealOwned = true;
            return hidden && VirusAvRules.Shows(save, VirusAvRules.PcGuardApp) && VirusAvRules.Shows(new VirusSave(), "shop");
        }

        private static VirusSave Infected()
        {
            VirusSave save = new VirusSave();
            save.Active.Add(VirusRules.Lukko);
            save.Active.Add(VirusRules.Penni);
            save.Returning.Add(new VirusReturn { Id = VirusRules.Kassa, Day = 3 });
            return save;
        }
    }
}
