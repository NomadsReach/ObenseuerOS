using System;
using System.Collections.Generic;
using System.Globalization;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class VirusMoney
    {
        private static readonly Random Rnd = new Random();

        internal static void Attach()
        {
            Events.HourPassed += hour => Hour();
        }

        private static void Hour()
        {
            if (!OsBridge.Writable() || !OsBridge.WorldReady() || !Cash.IsAvailable) return;
            int day = WorldClock.Now.Day;
            AccountSummary account = OpenAccount();
            int bankRm = account == null ? 0 : VirusRules.BankBalance(account.Rm);
            List<VirusSkim> takes = VirusRules.Skim(VirusCore.State, VirusCore.Enabled && VirusCore.MoneyTheft, day, Cash.Oc, bankRm, account != null, Rnd);
            foreach (VirusSkim take in takes) Take(take, day, account);
        }

        private static AccountSummary OpenAccount()
        {
            foreach (AccountSummary account in Banking.Accounts())
            {
                if (!account.Frozen) return account;
            }
            return null;
        }

        private static void Take(VirusSkim take, int day, AccountSummary account)
        {
            if (take.Oc > 0 && !Cash.TrySpend(MoneyCurrency.OC, take.Oc)) return;
            if (take.Rm > 0 && Banking.Charge(account.BankName, MoneyCurrency.RM, take.Rm) != BankResult.Ok) return;
            if (VirusRules.Record(VirusCore.State, take, day)) Receipt(take, account);
        }

        private static void Receipt(VirusSkim take, AccountSummary account)
        {
            if (take.Oc > 0)
            {
                OsMail.SendSpam("PenniPay <receipts@pennipay.ts>", "Pocket transfer receipt", "A transfer of " + Amount(take.Oc, "OC") + " left your wallet through PenniPay.\n\nPenniPay", "");
            }
            else
            {
                OsMail.SendSpam("Kassa24 <billing@kassa24.ts>", "Kassa24 payment confirmation", "Your payment of " + Amount(take.Rm, "RM") + " to Kassa24 was taken from your " + account.BankName + " account.\n\nKassa24", "");
            }
        }

        private static string Amount(int value, string currency)
        {
            return value.ToString("#,0.##", CultureInfo.InvariantCulture) + " " + currency;
        }
    }
}
