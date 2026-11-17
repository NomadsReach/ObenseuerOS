using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace UnityEngine { internal static class Time { internal static float unscaledTime; } }
namespace ObenseuerCore
{
    internal enum MoneyCurrency { OC, RM }
    internal enum BankResult { Ok, Busy, NoAccount, Frozen, Insufficient, Refused, NotFound, Unavailable }
    internal enum TaskState { Active }
    internal enum ObjectiveState { Active }
    internal sealed class CoreLog { internal static CoreLog For(string owner) { return new CoreLog(); } internal void Error(string value) { } }
    internal static class SaveData { internal static void Register<T>(string id, Func<T> get, Action<T> set) { } }
    internal static class Cash
    {
        internal static event Action<object> MoneyChanged { add { } remove { } }
        internal static bool IsAvailable { get { return true; } }
        internal static decimal Balance(MoneyCurrency currency) { return 0; }
    }
    internal sealed class InvoiceInfo
    {
        internal int Id, Amount, DueDay, DaysRemaining;
        internal string Debtor;
        internal MoneyCurrency Currency;
        internal bool Expired;
    }
    internal static class Invoices
    {
        internal static readonly List<InvoiceInfo> Values = new List<InvoiceInfo>();
        internal static event Action<object> Issued { add { } remove { } }
        internal static event Action<object> Paid { add { } remove { } }
        internal static event Action<object> Cancelled { add { } remove { } }
        internal static List<InvoiceInfo> All() { return new List<InvoiceInfo>(Values); }
        internal static InvoiceInfo Get(int id) { return Values.Find(item => item.Id == id); }
    }
    internal sealed class AccountSummary { internal string BankName; internal decimal Oc, Rm; internal bool Frozen; }
    internal sealed class BankAccount { internal bool Frozen; internal decimal DebtOc; internal float DebtRm; }
    internal static class Banking
    {
        internal static bool Open = true;
        internal static List<AccountSummary> Accounts() { return Open ? new List<AccountSummary> { new AccountSummary { BankName = "Stalburg Bank" } } : new List<AccountSummary>(); }
        internal static bool HasDebitCard() { return true; }
        internal static BankAccount GetByName(string bank) { return new BankAccount(); }
        internal static BankResult PayInvoiceFromAccount(string bank, int id) { return BankResult.Ok; }
        internal static BankResult RepayLoanFromAccount(string bank, MoneyCurrency currency, int amount) { return BankResult.Ok; }
        internal static BankResult WithdrawByPost(string bank, MoneyCurrency currency, float debit, float fee) { return BankResult.Ok; }
    }
    internal sealed class BankLedgerEntry
    {
        internal int Id, Day;
        internal string Kind, Memo;
        internal decimal OcDelta, RmDelta, DebtOcDelta, DebtRmDelta, OcAfter, RmAfter;
    }
    internal static class BankLedger
    {
        internal static event Action<object> Added { add { } remove { } }
        internal static int FirstDay { get { return -1; } }
        internal static bool IsAvailable { get { return true; } }
        internal static List<BankLedgerEntry> Entries(string bank, int max, int before) { return new List<BankLedgerEntry>(); }
    }
    internal sealed class ObjectiveInfo { internal ObjectiveState State; internal string Title, Description; }
    internal sealed class QuestInfo { internal TaskState State; internal string Title, Description; internal List<ObjectiveInfo> Objectives; }
    internal static class Quests
    {
        internal static event Action<object> TaskChanged { add { } remove { } }
        internal static event Action<object> ObjectiveChanged { add { } remove { } }
        internal static QuestInfo Get(string id) { return null; }
    }
}
namespace ObenseuerOS
{
    internal static class Os
    {
        internal const string ModId = "test";
        internal static readonly Dictionary<string, Func<string, string>> Handlers = new Dictionary<string, Func<string, string>>();
        internal static bool Handle(string app, string method, Func<string, string> handler) { Handlers[method] = handler; return true; }
    }
    internal sealed class OsBridgeException : Exception { internal OsBridgeException(string code, string message) : base(message) { } }
    internal static class OsBridge
    {
        internal static JObject Args(string args) { return JObject.Parse(args); }
        internal static int Span(JObject value, string key, int max, int fallback)
        {
            JToken token = value[key]; if (token == null) return fallback;
            long number;
            if (token.Type != JTokenType.Integer || !long.TryParse(token.ToString(), out number) || number < 0 || number > max) throw new OsBridgeException("bad_args", "out of range");
            return (int)number;
        }
        internal static bool Flag(JObject value, string key)
        {
            JToken token = value[key]; if (token == null || token.Type == JTokenType.Null) return false;
            if (token.Type != JTokenType.Boolean) throw new OsBridgeException("bad_args", "not a boolean");
            return (bool)token;
        }
        internal static string Text(JObject value, string key) { return (string)value[key]; }
        internal static void CheckWritable() { }
    }
    internal static class OsView { internal static bool Deliver(JObject value) { return true; } }
}
