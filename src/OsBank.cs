using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    [Serializable]
    public class BankCidRec
    {
        public string Cid;
        public string Print;
        public string Result;
    }

    [Serializable]
    public class BankSave
    {
        public List<BankCidRec> Cids = new List<BankCidRec>();
    }

    internal static class OsBank
    {
        private const string AppId = "bank";
        private const string StoreId = "nomad.obenseuer.os.bank";
        private const string DebitQuest = "debitcardquest";
        private const string NoAccountHint = "Open an account at Stalburg Bank.";
        private const string StalburgBank = "Stalburg Bank";
        private const string SmallDebtText = "This loan is under 1 RM, which the online bank cannot repay. Repay it at the branch.";
        private const string CardHint = "Call customer service; ask at the phone booth.";
        private const string BusyText = "The game is saving or loading. Try again in a moment.";
        private const string ConflictText = "This request id was already used for a different request.";
        private const int MaxCids = 200;
        private const int MaxCid = 64;
        private const int MaxName = 60;
        private const int MaxDebtor = 40;
        private const int MaxText = 200;
        private const int MaxMemo = 60;
        private const int MaxAccounts = 20;
        private const int MaxInvoices = 30;
        private const int MaxLedger = 25;
        private const int DefaultLedger = 20;
        private const int DueWithinDays = 2;
        private const double MaxAmount = 16000000;
        private const float MaxFeePercent = 20f;
        private const float DefaultFeePercent = 2f;
        private const float TickInterval = 0.25f;
        private static BankSave save = new BankSave();
        private static float feePercent = DefaultFeePercent;
        private static bool dirty;
        private static float lastTick;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly HashSet<string> TickFailed = new HashSet<string>();

        internal static void Attach(float withdrawFeePercent)
        {
            feePercent = CleanFeePercent(withdrawFeePercent);
            SaveData.Register<BankSave>(StoreId, () => save, loaded => save = loaded ?? new BankSave());
            Cash.MoneyChanged += args => dirty = true;
            Invoices.Issued += args => dirty = true;
            Invoices.Paid += args => dirty = true;
            Invoices.Cancelled += args => dirty = true;
            BankLedger.Added += entry => dirty = true;
            Quests.TaskChanged += args => dirty = true;
            Quests.ObjectiveChanged += args => dirty = true;
            Os.Handle(AppId, "bank.status", Status);
            Os.Handle(AppId, "bank.accounts", AccountList);
            Os.Handle(AppId, "bank.ledger", Entries);
            Os.Handle(AppId, "bank.invoices", InvoiceList);
            Os.Handle(AppId, "bank.payInvoice", PayInvoice);
            Os.Handle(AppId, "bank.loan.info", LoanInfo);
            Os.Handle(AppId, "bank.loan.repay", RepayLoan);
            Os.Handle(AppId, "bank.cash.order", CashOrder);
        }

        internal static void Tick()
        {
            if (!dirty || Time.unscaledTime < lastTick + TickInterval) return;
            lastTick = Time.unscaledTime;
            try
            {
                if (!OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "event"), new JProperty("app", AppId), new JProperty("name", "changed")))) return;
                OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "badge"), new JProperty("app", AppId), new JProperty("count", DueSoon())));
                dirty = false;
            }
            catch (Exception e)
            {
                if (TickFailed.Add(e.GetType().FullName + ": " + e.Message)) Log.Error("bank tick failed, logged once per failure: " + e);
            }
        }

        internal static string SelfCheck()
        {
            if (Quantize(MoneyCurrency.OC, 12.9) != 12m || Quantize(MoneyCurrency.OC, 0.5) != 0m) return "whole units";
            if (Quantize(MoneyCurrency.RM, 12.345) != 12.3m || Quantize(MoneyCurrency.RM, 0.04) != 0m) return "RM rounding";
            if (Quantize(MoneyCurrency.OC, MaxAmount + 1) != 0m || Quantize(MoneyCurrency.OC, double.NaN) != 0m) return "amount range";
            if (Fee(100m, MoneyCurrency.OC, 2f) != 2m || Fee(1m, MoneyCurrency.OC, 2f) != 1m) return "OC fee";
            if (Fee(12.3m, MoneyCurrency.RM, 2f) != 0.3m) return "RM fee";
            if (CleanFeePercent(float.NaN) != 2f || CleanFeePercent(float.PositiveInfinity) != 2f || CleanFeePercent(float.NegativeInfinity) != 2f) return "fee not finite";
            if (CleanFeePercent(-1f) != 0f || CleanFeePercent(50f) != 20f || CleanFeePercent(3.5f) != 3.5f) return "fee range";
            if (Gate(false, false, null, null).Value<string>("kind") != "no_account") return "gate without account";
            if (!Gate(true, true, "t", "x").Value<bool>("locked") || (string)Gate(true, true, "t", "x")["objective"]["title"] != "t") return "gate card missing";
            if (Gate(true, false, null, null).Value<bool>("locked")) return "gate open";
            if (Clip(new string('x', 500), MaxText).Length != MaxText) return "clip";
            BankSave state = new BankSave();
            for (int i = 0; i < MaxCids + 5; i++) Remember(state, "c" + i, "p", "r");
            if (state.Cids.Count != MaxCids || state.Cids[0].Cid != "c5") return "cid cap";
            if (Replay(state, "c10", "p") != "r") return "cid replay";
            try
            {
                Replay(state, "c10", "other");
                return "cid conflict";
            }
            catch (OsBridgeException)
            {
            }
            return null;
        }

        private static string Status(string args)
        {
            JObject reason = Reason();
            return JsonConvert.SerializeObject(new { locked = reason["locked"], kind = reason["kind"], heading = reason["heading"], hint = reason["hint"], objective = reason["objective"], hasCard = Banking.HasDebitCard(), badge = DueSoon(), history = BankLedger.IsAvailable, feePercent = feePercent });
        }

        private static string AccountList(string args)
        {
            RequireOpen();
            if (!Cash.IsAvailable) throw new OsBridgeException("failed", "money is not ready");
            List<AccountSummary> accounts = Banking.Accounts();
            return JsonConvert.SerializeObject(new { wallet = new { oc = (int)Cash.Balance(MoneyCurrency.OC), rm = Math.Round((double)Cash.Balance(MoneyCurrency.RM), 2) }, accounts = AccountRows(accounts), total = accounts.Count });
        }

        internal static List<object> AccountRows(List<AccountSummary> accounts)
        {
            List<object> items = new List<object>();
            foreach (AccountSummary account in accounts)
            {
                if (items.Count < MaxAccounts) items.Add(new { bank = Clip(account.BankName, MaxName), oc = account.Oc, rm = Math.Round((double)account.Rm, 2), frozen = account.Frozen });
            }
            return items;
        }

        private static string Entries(string args)
        {
            JObject a = OsBridge.Args(args);
            string bank = BankName(a);
            int max = OsBridge.Span(a, "max", MaxLedger, DefaultLedger);
            int beforeId = OsBridge.Span(a, "beforeId", int.MaxValue, 0);
            RequireOpen();
            AccountOf(bank);
            List<BankLedgerEntry> list = BankLedger.Entries(bank, max, beforeId);
            List<object> items = new List<object>();
            foreach (BankLedgerEntry entry in list)
            {
                items.Add(new { id = entry.Id, day = entry.Day, kind = Clip(entry.Kind, MaxName), ocDelta = entry.OcDelta, rmDelta = entry.RmDelta, debtOcDelta = entry.DebtOcDelta, debtRmDelta = entry.DebtRmDelta, ocAfter = entry.OcAfter, rmAfter = entry.RmAfter, memo = Clip(entry.Memo, MaxMemo) });
            }
            object since = BankLedger.FirstDay < 0 ? null : (object)BankLedger.FirstDay;
            int next = list.Count > 0 && list.Count == max ? list[list.Count - 1].Id : 0;
            return JsonConvert.SerializeObject(new { bank = bank, since = since, entries = items, nextBeforeId = next });
        }

        private static string InvoiceList(string args)
        {
            RequireOpen();
            List<InvoiceInfo> all = Invoices.All();
            List<object> items = new List<object>();
            foreach (InvoiceInfo invoice in all)
            {
                if (items.Count < MaxInvoices) items.Add(new { id = invoice.Id, debtor = Clip(Printable(invoice.Debtor), MaxDebtor), amount = invoice.Amount, currency = CurrencyName(invoice.Currency), dueDay = invoice.DueDay, daysLeft = invoice.DaysRemaining, expired = invoice.Expired });
            }
            return JsonConvert.SerializeObject(new { items = items, total = all.Count });
        }

        private static string PayInvoice(string args)
        {
            JObject a = OsBridge.Args(args);
            string bank = BankName(a);
            int id = InvoiceId(a);
            string cid = Cid(a);
            string print = JsonConvert.SerializeObject(new object[] { "payInvoice", bank, id });
            string saved = Replay(save, cid, print);
            if (saved != null) return saved;
            RequireOpen();
            OsBridge.CheckWritable();
            InvoiceInfo invoice = Invoices.Get(id);
            if (invoice == null) throw new OsBridgeException("bad_args", "There is no such invoice.");
            BankResult result = Banking.PayInvoiceFromAccount(bank, id);
            Check(result, "This invoice cannot be paid. It may have expired.");
            return Remember(save, cid, print, JsonConvert.SerializeObject(new { id = id, bank = bank, amount = invoice.Amount, currency = CurrencyName(invoice.Currency) }));
        }

        private static string LoanInfo(string args)
        {
            JObject a = OsBridge.Args(args);
            string bank = BankName(a);
            RequireOpen();
            return LoanJson(bank, AccountOf(bank));
        }

        private static string RepayLoan(string args)
        {
            JObject a = OsBridge.Args(args);
            string bank = BankName(a);
            MoneyCurrency currency = CurrencyOf(a);
            int amount = (int)Quantize(MoneyCurrency.OC, Amount(a));
            if (amount < 1) throw new OsBridgeException("bad_args", "amount must be at least 1");
            string cid = Cid(a);
            string print = JsonConvert.SerializeObject(new object[] { "loan.repay", bank, CurrencyName(currency), amount });
            string saved = Replay(save, cid, print);
            if (saved != null) return saved;
            RequireOpen();
            OsBridge.CheckWritable();
            BankAccount before = AccountOf(bank);
            if (currency == MoneyCurrency.RM && before.DebtRm > 0f && before.DebtRm < 1f) throw new OsBridgeException("denied", SmallDebtText);
            BankResult result = Banking.RepayLoanFromAccount(bank, currency, amount);
            Check(result, "There is nothing to repay in that currency.");
            return Remember(save, cid, print, LoanJson(bank, Banking.GetByName(bank) ?? before));
        }

        private static string CashOrder(string args)
        {
            JObject a = OsBridge.Args(args);
            string bank = BankName(a);
            MoneyCurrency currency = CurrencyOf(a);
            decimal debit = Quantize(currency, Amount(a));
            if (debit <= 0m) throw new OsBridgeException("bad_args", "amount must be a positive number up to 16000000");
            decimal fee = Fee(debit, currency, feePercent);
            decimal sent = debit - fee;
            if (sent < Unit(currency)) throw new OsBridgeException("bad_args", "the amount is too small to cover the fee");
            string cid = Cid(a);
            string print = JsonConvert.SerializeObject(new object[] { "cash.order", bank, CurrencyName(currency), (double)debit });
            string saved = Replay(save, cid, print);
            if (saved != null) return saved;
            RequireOpen();
            OsBridge.CheckWritable();
            BankResult result = Banking.WithdrawByPost(bank, currency, (float)debit, (float)fee);
            Check(result, "The cash order was refused.");
            return Remember(save, cid, print, JsonConvert.SerializeObject(new { bank = bank, currency = CurrencyName(currency), debited = Num(debit, currency), fee = Num(fee, currency), sent = Num(sent, currency) }));
        }

        internal static decimal Quantize(MoneyCurrency currency, double amount)
        {
            if (!(amount > 0) || amount > MaxAmount) return 0m;
            decimal value = currency == MoneyCurrency.RM ? Math.Round((decimal)amount, 1) : Math.Floor((decimal)amount);
            return value > 0m ? value : 0m;
        }

        internal static float CleanFeePercent(float percent)
        {
            if (float.IsNaN(percent) || float.IsInfinity(percent)) return DefaultFeePercent;
            return Math.Max(0f, Math.Min(MaxFeePercent, percent));
        }

        internal static decimal Fee(decimal debit, MoneyCurrency currency, float percent)
        {
            decimal unit = Unit(currency);
            decimal fee = Math.Ceiling(debit * (decimal)percent / 100m / unit) * unit;
            return Math.Max(fee, unit);
        }

        internal static JObject Gate(bool hasAccount, bool cardMissing, string title, string text)
        {
            if (!hasAccount) return JObject.FromObject(new { locked = true, kind = "no_account", heading = "No bank account", hint = NoAccountHint, objective = (object)null });
            if (cardMissing) return JObject.FromObject(new { locked = true, kind = "card_missing", heading = "Card missing", hint = CardHint, objective = new { title = title, text = text } });
            return JObject.FromObject(new { locked = false, kind = "open", heading = (object)null, hint = (object)null, objective = (object)null });
        }

        internal static string Replay(BankSave state, string cid, string print)
        {
            foreach (BankCidRec rec in state.Cids)
            {
                if (rec.Cid != cid) continue;
                if (rec.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return rec.Result;
            }
            return null;
        }

        internal static string Remember(BankSave state, string cid, string print, string result)
        {
            BankCidRec rec = new BankCidRec();
            rec.Cid = cid;
            rec.Print = print;
            rec.Result = result;
            state.Cids.Add(rec);
            while (state.Cids.Count > MaxCids) state.Cids.RemoveAt(0);
            return result;
        }

        internal static string Clip(string text, int max)
        {
            if (text == null || text.Length <= max) return text;
            return text.Substring(0, max - 3) + "...";
        }

        private static string Printable(string text)
        {
            return text == null ? null : Regex.Replace(text, @"\p{Cc}", string.Empty);
        }

        private static JObject Reason()
        {
            QuestInfo quest = Quests.Get(DebitQuest);
            bool missing = quest != null && quest.State == TaskState.Active && !Banking.HasDebitCard();
            string title = null;
            string text = null;
            if (missing) Current(quest, out title, out text);
            return Gate(Banking.Accounts().Exists(account => account.BankName == StalburgBank), missing, title, text);
        }

        internal static string LockHint()
        {
            JObject reason = Reason();
            return (bool)reason["locked"] ? (string)reason["hint"] : null;
        }

        private static void Current(QuestInfo quest, out string title, out string text)
        {
            title = Clip(quest.Title, MaxText);
            text = Clip(quest.Description, MaxText);
            if (quest.Objectives == null) return;
            foreach (ObjectiveInfo objective in quest.Objectives)
            {
                if (objective == null || objective.State != ObjectiveState.Active) continue;
                title = Clip(objective.Title, MaxText);
                text = Clip(objective.Description, MaxText);
                return;
            }
        }

        private static void RequireOpen()
        {
            JObject reason = Reason();
            if ((bool)reason["locked"]) throw new OsBridgeException("denied", "The bank app is locked. " + (string)reason["hint"]);
        }

        private static BankAccount AccountOf(string bank)
        {
            BankAccount account = Banking.GetByName(bank);
            if (account == null) throw new OsBridgeException("denied", "You have no account at that bank.");
            return account;
        }

        private static string LoanJson(string bank, BankAccount account)
        {
            return JsonConvert.SerializeObject(new { bank = bank, frozen = account.Frozen, debtOc = account.DebtOc, debtRm = Math.Round((double)account.DebtRm, 2) });
        }

        private static string BankName(JObject a)
        {
            string bank = OsBridge.Text(a, "bank");
            if (string.IsNullOrEmpty(bank) || bank.Length > MaxName) throw new OsBridgeException("bad_args", "bank must be the name of an account");
            return bank;
        }

        private static string Cid(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > MaxCid) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }

        private static int InvoiceId(JObject a)
        {
            int id = OsBridge.Span(a, "id", int.MaxValue, 0);
            if (id < 1) throw new OsBridgeException("bad_args", "id must be an invoice number");
            return id;
        }

        private static MoneyCurrency CurrencyOf(JObject a)
        {
            string code = OsBridge.Text(a, "currency");
            if (code == "OC") return MoneyCurrency.OC;
            if (code == "RM") return MoneyCurrency.RM;
            throw new OsBridgeException("bad_args", "currency must be OC or RM");
        }

        private static double Amount(JObject a)
        {
            JToken t = a["amount"];
            if (t == null || (t.Type != JTokenType.Integer && t.Type != JTokenType.Float)) throw new OsBridgeException("bad_args", "amount must be a number");
            return (double)t;
        }

        private static decimal Unit(MoneyCurrency currency)
        {
            return currency == MoneyCurrency.RM ? 0.1m : 1m;
        }

        private static string CurrencyName(MoneyCurrency currency)
        {
            return currency == MoneyCurrency.RM ? "RM" : "OC";
        }

        private static object Num(decimal value, MoneyCurrency currency)
        {
            return currency == MoneyCurrency.RM ? (object)(double)value : (object)(int)value;
        }

        private static int DueSoon()
        {
            int count = 0;
            foreach (InvoiceInfo invoice in Invoices.All())
            {
                if (!invoice.Expired && invoice.DaysRemaining <= DueWithinDays) count++;
            }
            return count;
        }

        private static void Check(BankResult result, string refused)
        {
            switch (result)
            {
                case BankResult.Ok:
                    return;
                case BankResult.Busy:
                    throw new OsBridgeException("busy", BusyText);
                case BankResult.NoAccount:
                    throw new OsBridgeException("denied", "You have no account at that bank.");
                case BankResult.Frozen:
                    throw new OsBridgeException("denied", "This account is frozen.");
                case BankResult.Insufficient:
                    throw new OsBridgeException("denied", "There is not enough money in that account.");
                case BankResult.Refused:
                    throw new OsBridgeException("denied", refused);
                case BankResult.NotFound:
                    throw new OsBridgeException("bad_args", "There is no such invoice.");
                case BankResult.Unavailable:
                    throw new OsBridgeException("failed", "The bank is not available right now.");
                default:
                    throw new OsBridgeException("failed", "The bank could not finish the request.");
            }
        }
    }
}
