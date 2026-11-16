using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitBank
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient client;
        private readonly Label notice;
        private readonly Label warning;
        private readonly UnityEngine.UIElements.Button acknowledge;
        private readonly ScrollView content;
        private JObject status, accounts, ledger, invoices, loan;
        private readonly List<JObject> history = new List<JObject>();
        private string bank = "", section = "Accounts", cashAmount = "", repayAmount = "", cashCurrency = "OC", repayCurrency = "OC", confirm = "", snapshot = "";
        private int beforeId, invoiceId;
        private long acknowledgedVersion = -1;
        private long observedVersion = -1;

        internal ToolkitBank(TransactionClient client)
        {
            this.client = client;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 220; Window.style.top = 32; Window.style.width = 1000; Window.style.height = 660;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("Stalburg Bank"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; confirm = ""; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; notice.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(notice);
            warning = Text(""); warning.style.backgroundColor = Color.white; warning.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(warning);
            acknowledge = Button("I checked or understand the risk", delegate { acknowledgedVersion = client.PendingVersion; UpdatePendingWarning(); }); Window.Add(acknowledge);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            UpdatePendingWarning();
            Window.schedule.Execute(Poll).Every(3000);
        }

        internal void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(true); }
        private void Poll() { if (Window.style.display != DisplayStyle.None) { UpdatePendingWarning(); if (!Editing()) Refresh(false); } }
        private bool Editing()
        {
            VisualElement focused = Window.panel == null ? null : Window.panel.focusController.focusedElement as VisualElement;
            while (focused != null && focused != Window) { if (focused is TextField) return true; focused = focused.parent; }
            return false;
        }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; return row; }
        private static Label Text(string text) { Label label = new Label(text); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button b = new UnityEngine.UIElements.Button(action); b.enableRichText = false; b.text = text; return b; }
        private static JObject Args(string key, object value) { return new JObject(new JProperty(key, value)); }
        private static string Money(JToken value, string currency) { return value == null ? "0 " + currency : string.Format(CultureInfo.CurrentCulture, currency == "OC" ? "{0:N0} OC" : "{0:N2} RM", (decimal)value); }

        private JObject Read(string method, JObject args)
        {
            try { return client.Read(method, args) as JObject; }
            catch (Exception e) { notice.text = e.Message; return null; }
        }

        private void Refresh(bool force)
        {
            if (force) notice.text = "";
            JObject nextStatus = Read("bank.status", null);
            if (nextStatus == null) { Unavailable(); return; }
            JObject nextAccounts = null, nextLedger = null, nextInvoices = null, nextLoan = null;
            if (!(bool)nextStatus["locked"])
            {
                nextAccounts = Read("bank.accounts", null); if (nextAccounts == null) { Unavailable(); return; }
                JArray accountList = (JArray)nextAccounts["accounts"];
                bool found = false;
                foreach (JToken item in accountList) if ((string)item["bank"] == bank) found = true;
                if (!found) bank = accountList.Count == 0 ? "" : (string)accountList[0]["bank"];
                if (bank.Length > 0)
                {
                    if ((bool)nextStatus["history"]) nextLedger = Read("bank.ledger", new JObject(new JProperty("bank", bank), new JProperty("max", 20), new JProperty("beforeId", 0)));
                    nextInvoices = Read("bank.invoices", null);
                    nextLoan = Read("bank.loan.info", Args("bank", bank));
                    if (((bool)nextStatus["history"] && nextLedger == null) || nextInvoices == null || nextLoan == null) { Unavailable(); return; }
                }
            }
            string next = new JArray(nextStatus, nextAccounts, nextLedger, nextInvoices, nextLoan).ToString(Formatting.None);
            if (!force && next == snapshot) return;
            if (next != snapshot) { confirm = ""; notice.text = ""; }
            snapshot = next; status = nextStatus; accounts = nextAccounts; ledger = nextLedger; invoices = nextInvoices; loan = nextLoan;
            history.Clear(); beforeId = 0;
            if (ledger != null)
            {
                foreach (JToken item in (JArray)ledger["entries"]) history.Add((JObject)item);
                beforeId = (int)ledger["nextBeforeId"];
            }
            Render();
        }

        private void Unavailable()
        {
            confirm = ""; snapshot = ""; content.Clear();
            content.Add(Text("Bank information is unavailable. Refresh to try again."));
            content.Add(Button("Refresh", delegate { Refresh(true); }));
        }

        private void UpdatePendingWarning()
        {
            if (observedVersion != client.PendingVersion) { observedVersion = client.PendingVersion; confirm = ""; }
            warning.text = client.HasPending ? "A transaction may have completed. Check recent transactions before making another payment or order." : "";
            acknowledge.style.display = client.HasPending && acknowledgedVersion != client.PendingVersion ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Render()
        {
            content.Clear();
            Label heading = Text("Stalburg Bank"); heading.style.fontSize = 24; content.Add(heading);
            if ((bool)status["locked"])
            {
                content.Add(Text((string)status["heading"])); content.Add(Text((string)status["hint"]));
                if (status["objective"] != null && status["objective"].Type == JTokenType.Object)
                    content.Add(Text((string)status["objective"]["title"] + "\n" + (string)status["objective"]["text"]));
                content.Add(Button("Refresh", delegate { Refresh(true); })); return;
            }
            JArray accountList = (JArray)accounts["accounts"];
            if (accountList.Count == 0) { content.Add(Text("No bank accounts are available.")); return; }
            List<string> names = new List<string>(); foreach (JToken a in accountList) names.Add((string)a["bank"]);
            int selected = Math.Max(0, names.IndexOf(bank));
            DropdownField account = new DropdownField("Account", names, selected);
            account.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { bank = e.newValue; confirm = ""; Refresh(true); }); content.Add(account);
            VisualElement nav = Row();
            foreach (string name in new[] { "Accounts", "Payments", "Loans", "Cards", "Help" })
            {
                string chosen = name;
                nav.Add(Button(name, delegate { section = chosen; confirm = ""; notice.text = ""; Render(); }));
            }
            nav.Add(Button("Refresh", delegate { confirm = ""; Refresh(true); })); content.Add(nav);
            Label page = Text(section); page.style.fontSize = 20; content.Add(page);
            if (section == "Accounts") AccountsPage();
            else if (section == "Payments") PaymentsPage();
            else if (section == "Loans") LoanPage();
            else if (section == "Cards") content.Add(Text((bool)status["hasCard"] ? "A debit card is on file for this save. ATM use needs it." : "No debit card on file. Ask at the branch for a replacement."));
            else content.Add(Text("Accounts are opened at a Stalburg Bank branch.\nCheck recent transactions before trying a payment again.\nCash orders are debited now and sent by post."));
        }

        private void AccountsPage()
        {
            content.Add(Text("Wallet: " + Money(accounts["wallet"]["oc"], "OC") + "   " + Money(accounts["wallet"]["rm"], "RM")));
            foreach (JToken a in (JArray)accounts["accounts"])
                content.Add(Text((string)a["bank"] + "   " + Money(a["oc"], "OC") + "   " + Money(a["rm"], "RM") + ((bool)a["frozen"] ? "   Frozen" : "")));
            content.Add(Text("Recent transactions"));
            if (!(bool)status["history"]) { content.Add(Text("History is not recorded in this save.")); return; }
            if (history.Count == 0) content.Add(Text("No records yet."));
            foreach (JObject item in history)
                content.Add(Text("Day " + (string)item["day"] + "  " + (string)item["kind"] + "  " + Money(item["ocDelta"], "OC") + "  " + Money(item["rmDelta"], "RM") + "\nDebt change: " + Money(item["debtOcDelta"], "OC") + "  " + Money(item["debtRmDelta"], "RM") + "   Balance: " + Money(item["ocAfter"], "OC") + "  " + Money(item["rmAfter"], "RM") + "  " + (string)item["memo"]));
            if (beforeId > 0) content.Add(Button("Load older records", Older));
        }

        private void Older()
        {
            JObject page = Read("bank.ledger", new JObject(new JProperty("bank", bank), new JProperty("max", 20), new JProperty("beforeId", beforeId)));
            if (page == null) return;
            foreach (JToken item in (JArray)page["entries"]) history.Add((JObject)item);
            beforeId = (int)page["nextBeforeId"]; Render();
        }

        private void PaymentsPage()
        {
            content.Add(Text("Unpaid invoices"));
            List<string> labels = new List<string>(); List<int> ids = new List<int>();
            foreach (JToken item in (JArray)invoices["items"])
            {
                string label = "#" + (int)item["id"] + "  " + (string)item["debtor"] + "  " + Money(item["amount"], (string)item["currency"]) + ((bool)item["expired"] ? "  Expired" : "  Due in " + (int)item["daysLeft"] + " days");
                content.Add(Text(label));
                if (!(bool)item["expired"]) { labels.Add(label); ids.Add((int)item["id"]); }
            }
            if (labels.Count == 0) content.Add(Text("No unpaid invoices available to pay."));
            else
            {
                int index = ids.IndexOf(invoiceId); if (index < 0) index = 0; invoiceId = ids[index];
                DropdownField select = new DropdownField("Invoice", labels, index);
                select.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { int at = labels.IndexOf(e.newValue); if (at >= 0) invoiceId = ids[at]; confirm = ""; notice.text = ""; }); content.Add(select);
                content.Add(Button("Pay invoice", delegate { int at = ids.IndexOf(invoiceId); if (at >= 0) Execute("bank.payInvoice", new JObject(new JProperty("bank", bank), new JProperty("id", invoiceId)), "Pay " + labels[at] + " from " + bank + "?"); }));
            }
            content.Add(Text("Order cash by post"));
            DropdownField currency = Currency(cashCurrency, delegate(string v) { cashCurrency = v; confirm = ""; notice.text = ""; }); content.Add(currency);
            TextField amount = new TextField("Amount"); amount.SetValueWithoutNotify(cashAmount);
            Label quote = Text("");
            amount.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { cashAmount = e.newValue ?? ""; confirm = ""; notice.text = ""; UpdateQuote(quote); });
            currency.RegisterValueChangedCallback(delegate { UpdateQuote(quote); }); content.Add(amount); content.Add(quote); UpdateQuote(quote);
            content.Add(Button("Order cash", delegate
            {
                decimal value, debit, fee, sent;
                if (!Parse(cashAmount, out value) || !BankQuote.TryCreate(cashCurrency, value, (decimal)status["feePercent"], out debit, out fee, out sent)) { notice.text = "Enter an amount large enough to cover the fee."; return; }
                JObject args = new JObject(new JProperty("bank", bank), new JProperty("currency", cashCurrency), new JProperty("amount", debit));
                Execute("bank.cash.order", args, "Debit " + debit + " " + cashCurrency + ", fee " + fee + ", send " + sent + " by post?");
            }));
        }

        private void UpdateQuote(Label label)
        {
            decimal value, debit, fee, sent;
            label.text = Parse(cashAmount, out value) && BankQuote.TryCreate(cashCurrency, value, (decimal)status["feePercent"], out debit, out fee, out sent)
                ? "Debited: " + debit + "   Fee: " + fee + "   Sent: " + sent + " " + cashCurrency : "Enter an amount to see the fee.";
        }

        private void LoanPage()
        {
            content.Add(Text("Loan balance: " + Money(loan["debtOc"], "OC") + "   " + Money(loan["debtRm"], "RM")));
            if ((decimal)loan["debtOc"] <= 0m && (decimal)loan["debtRm"] <= 0m) content.Add(Text("No loan to repay."));
            content.Add(Currency(repayCurrency, delegate(string v) { repayCurrency = v; confirm = ""; notice.text = ""; }));
            TextField amount = new TextField("Amount"); amount.SetValueWithoutNotify(repayAmount);
            amount.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { repayAmount = e.newValue ?? ""; confirm = ""; notice.text = ""; }); content.Add(amount);
            content.Add(Button("Repay loan", delegate
            {
                decimal value;
                if (!Parse(repayAmount, out value) || value < 1m || value > 16000000m) { notice.text = "Enter a whole amount of at least 1."; return; }
                int whole = (int)Math.Floor(value);
                Execute("bank.loan.repay", new JObject(new JProperty("bank", bank), new JProperty("currency", repayCurrency), new JProperty("amount", whole)), "Repay " + whole + " " + repayCurrency + " from " + bank + "?");
            }));
        }

        private static DropdownField Currency(string current, Action<string> changed)
        {
            DropdownField field = new DropdownField("Currency", new List<string> { "OC", "RM" }, current == "RM" ? 1 : 0);
            field.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { changed(e.newValue); }); return field;
        }

        private static bool Parse(string value, out decimal amount) { return decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out amount); }

        private void Execute(string method, JObject args, string question)
        {
            UpdatePendingWarning();
            if (!client.MayStart(method, args, acknowledgedVersion == client.PendingVersion))
            {
                confirm = ""; notice.text = "Check recent transactions and acknowledge the warning before starting a different transaction."; return;
            }
            string key = method + args.ToString(Formatting.None);
            if (confirm != key) { confirm = key; notice.text = question + " Press the action again to confirm."; return; }
            confirm = "";
            try
            {
                JToken result = client.Write(method, args);
                if (result == null) throw new InvalidOperationException("No result returned. Check recent transactions before retrying.");
            }
            catch (Exception e) { string error = e.Message; acknowledgedVersion = -1; UpdatePendingWarning(); try { Refresh(true); } catch (Exception) { Unavailable(); } notice.text = error + " Check recent transactions before retrying."; return; }
            UpdatePendingWarning();
            if (method == "bank.cash.order") cashAmount = "";
            if (method == "bank.loan.repay") repayAmount = "";
            try { Refresh(true); notice.text = snapshot.Length == 0 ? "Completed. Refresh to see updated balances." : "Completed."; }
            catch (Exception) { Unavailable(); notice.text = "Completed. Refresh to see updated balances."; }
        }
    }
}
