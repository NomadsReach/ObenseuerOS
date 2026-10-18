using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class VirusTheftChecks
    {
        private const int Seed = 9001;
        private const int Hours = 10000;
        private const int DayOcCap = 60;
        private const int DayRmCap = 6;
        private const int WeekOcCap = 200;
        private const int WeekRmCap = 20;
        private const int OcFloor = 100;
        private const int RmFloor = 50;

        internal static List<KeyValuePair<string, bool>> Run()
        {
            List<KeyValuePair<string, bool>> checks = new List<KeyValuePair<string, bool>>();
            checks.Add(Check("money: 10000 simulated hours never pass 60 OC or 6 RM a day, 200 OC or 20 RM a week, or the floors (100 OC, 50 RM)", CapsOk()));
            checks.Add(Check("money: SkimAmount stays in its range and gives nothing when the room or the balance is under the minimum", ClampOk()));
            checks.Add(Check("money: nothing is taken with Enabled or MoneyTheft off, and something is taken with both on", OffOk()));
            checks.Add(Check("money: Kassa tries once a game day, so it takes at most one RM amount a day", KassaDayOk()));
            checks.Add(Check("money: each virus sends at most one receipt a day", ReceiptOk()));
            checks.Add(Check("money: a bank balance that is NaN, negative or huge reads as 0 or the cap, never a wrapped value", BankBalanceOk()));
            return checks;
        }

        private static bool BankBalanceOk()
        {
            return VirusRules.BankBalance(double.NaN) == 0
                && VirusRules.BankBalance(-5) == 0
                && VirusRules.BankBalance(double.PositiveInfinity) == VirusRules.MaxBankRm
                && VirusRules.BankBalance(3e12) == VirusRules.MaxBankRm
                && VirusRules.BankBalance(320.9) == 320;
        }

        private static KeyValuePair<string, bool> Check(string name, bool ok)
        {
            return new KeyValuePair<string, bool>(name, ok);
        }

        private static bool CapsOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            save.Active.Add(VirusRules.Penni);
            save.Active.Add(VirusRules.Kassa);
            int wallet = 150;
            int bank = RmFloor;
            int stolenOc = 0;
            int stolenRm = 0;
            Dictionary<int, int> dayOc = new Dictionary<int, int>();
            Dictionary<int, int> dayRm = new Dictionary<int, int>();
            Dictionary<int, int> weekOc = new Dictionary<int, int>();
            Dictionary<int, int> weekRm = new Dictionary<int, int>();
            for (int hour = 0; hour < Hours; hour++)
            {
                int day = hour / 24;
                wallet = Math.Max(0, wallet + rnd.Next(-15, 16));
                bank = RmFloor + rnd.Next(0, 12);
                foreach (VirusSkim take in VirusRules.Skim(save, true, day, wallet, bank, true, rnd))
                {
                    if (take.Oc > 0 && (take.Rm > 0 || wallet - take.Oc < OcFloor)) return false;
                    if (take.Rm > 0 && (take.Oc > 0 || bank - take.Rm < RmFloor)) return false;
                    wallet -= take.Oc;
                    bank -= take.Rm;
                    Add(dayOc, day, take.Oc);
                    Add(dayRm, day, take.Rm);
                    Add(weekOc, day / 7, take.Oc);
                    Add(weekRm, day / 7, take.Rm);
                    VirusRules.Record(save, take, day);
                    stolenOc += take.Oc;
                    stolenRm += take.Rm;
                }
            }
            foreach (int total in dayOc.Values)
            {
                if (total > DayOcCap) return false;
            }
            foreach (int total in dayRm.Values)
            {
                if (total > DayRmCap) return false;
            }
            foreach (int total in weekOc.Values)
            {
                if (total > WeekOcCap) return false;
            }
            foreach (int total in weekRm.Values)
            {
                if (total > WeekRmCap) return false;
            }
            return stolenOc > 0 && stolenRm > 0;
        }

        private static void Add(Dictionary<int, int> totals, int key, int amount)
        {
            int current;
            totals.TryGetValue(key, out current);
            totals[key] = current + amount;
        }

        private static bool ClampOk()
        {
            Random rnd = new Random(Seed);
            for (int i = 0; i < 2000; i++)
            {
                int room = rnd.Next(0, 30);
                int available = rnd.Next(-10, 30);
                int amount = VirusRules.SkimAmount(5, 20, room, available, rnd);
                if (amount != 0 && (amount < 5 || amount > 20 || amount > room || amount > available)) return false;
            }
            return VirusRules.SkimAmount(5, 20, 3, 100, rnd) == 0 && VirusRules.SkimAmount(5, 20, 100, 4, rnd) == 0;
        }

        private static bool OffOk()
        {
            Random rnd = new Random(Seed);
            VirusSave off = new VirusSave();
            VirusSave on = new VirusSave();
            off.Active.Add(VirusRules.Penni);
            off.Active.Add(VirusRules.Kassa);
            on.Active.Add(VirusRules.Penni);
            on.Active.Add(VirusRules.Kassa);
            int offTakes = 0;
            int onTakes = 0;
            for (int hour = 0; hour < 1000; hour++)
            {
                offTakes += VirusRules.Skim(off, false, hour / 24, 5000, 500, true, rnd).Count;
                onTakes += VirusRules.Skim(on, true, hour / 24, 5000, 500, true, rnd).Count;
            }
            return offTakes == 0 && onTakes > 0;
        }

        private static bool KassaDayOk()
        {
            Random rnd = new Random(Seed);
            VirusSave save = new VirusSave();
            save.Active.Add(VirusRules.Kassa);
            int rmTakes = 0;
            Dictionary<int, int> perDay = new Dictionary<int, int>();
            for (int hour = 0; hour < Hours; hour++)
            {
                int day = hour / 24;
                foreach (VirusSkim take in VirusRules.Skim(save, true, day, 1000, 1000, true, rnd))
                {
                    if (take.Rm == 0) continue;
                    rmTakes++;
                    Add(perDay, day, 1);
                }
            }
            foreach (int count in perDay.Values)
            {
                if (count > 1) return false;
            }
            return rmTakes > 0;
        }

        private static bool ReceiptOk()
        {
            VirusSave save = new VirusSave();
            bool first = VirusRules.Record(save, new VirusSkim(VirusRules.Penni, 5, 0), 3);
            bool repeat = VirusRules.Record(save, new VirusSkim(VirusRules.Penni, 5, 0), 3);
            bool other = VirusRules.Record(save, new VirusSkim(VirusRules.Kassa, 0, 2), 3);
            bool nextDay = VirusRules.Record(save, new VirusSkim(VirusRules.Penni, 5, 0), 4);
            return first && !repeat && other && nextDay && save.DayOc == 5 && save.Stolen.Count == 2;
        }
    }
}
