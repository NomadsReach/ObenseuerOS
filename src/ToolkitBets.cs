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
    internal sealed class ToolkitBets
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient client;
        private readonly Action checkSession;
        private readonly Label notice, warning;
        private readonly UnityEngine.UIElements.Button acknowledge;
        private readonly ScrollView content;
        private JObject info, fixtures, openBets, settledBets, results, table;
        private string sport = "football", tab = "sports", fixture = "", fixtureSport = "", pick = "", match = "", stake = "10", confirmation = "", forgetConfirmation = "";
        private int minePage, resultsPage;
        private long acknowledgedVersion = -1;

        internal ToolkitBets(TransactionClient client, Action checkSession)
        {
            this.client = client; this.checkSession = checkSession;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 160; Window.style.top = 32; Window.style.width = 1080; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("NaSpoBet"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; confirmation = ""; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            warning = Text(""); warning.style.backgroundColor = Color.white; warning.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(warning);
            acknowledge = Button("I checked my bets and understand the risk", delegate { acknowledgedVersion = client.PendingVersion; UpdateWarning(); }); Window.Add(acknowledge);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && info != null) Session(); }).Every(1000);
            UpdateWarning();
        }
        internal void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(); }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = value; return button; }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private static string Odds(JToken value) { return value == null ? "—" : ((decimal)value / 100m).ToString("N2", CultureInfo.CurrentCulture); }
        private bool Session()
        {
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { info = null; content.Clear(); notice.text = e.Message; return false; }
        }
        private void UpdateWarning()
        {
            warning.text = client.HasPending ? "A bet may have been placed. Check My bets before placing another. Uncertain bets:\n" + client.PendingDetails : "";
            acknowledge.style.display = client.HasPending && acknowledgedVersion != client.PendingVersion ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private JObject Read(string method, JObject args)
        {
            try { return client.Read(method, args) as JObject; }
            catch (Exception e) { notice.text = e.Message; return null; }
        }
        private void Refresh()
        {
            if (!Session()) return;
            JObject next = Read("bet.sports", null), games = Read("bet.fixtures", new JObject(new JProperty("sport", sport)));
            JObject opened = Read("bet.mine", new JObject(new JProperty("status", "open"), new JProperty("page", minePage)));
            JObject settled = Read("bet.mine", new JObject(new JProperty("status", "settled"), new JProperty("page", minePage)));
            JObject scores = Read("bet.results", new JObject(new JProperty("sport", sport), new JProperty("page", resultsPage)));
            JObject standings = Read("bet.table", null);
            if (next == null || games == null || opened == null || settled == null || scores == null || standings == null)
            { info = null; content.Clear(); content.Add(Text("NaSpoBet is unavailable.")); content.Add(Button("Refresh", Refresh)); return; }
            info = next; fixtures = games; openBets = opened; settledBets = settled; results = scores; table = standings; Render();
        }
        private void Render()
        {
            if (info == null || !Session()) return;
            UpdateWarning(); content.Clear(); Label title = Text("NaSpoBet"); title.style.fontSize = 28; content.Add(title);
            content.Add(Text("Wallet: " + info["wallet"]["oc"] + " OC · 18+ · Play within your means."));
            VisualElement nav = Row();
            foreach (string section in new[] { "sports", "mine", "results", "standings", "responsible", "contact" }) { string name = section; nav.Add(Button(name, delegate { tab = name; confirmation = ""; Render(); })); }
            nav.Add(Button("Refresh", Refresh)); content.Add(nav);
            foreach (string pending in client.PendingKeys)
            {
                if (!pending.StartsWith("bet.place ", StringComparison.Ordinal)) continue;
                string saved = pending.Substring("bet.place ".Length);
                content.Add(Button("Check uncertain bet: " + saved, delegate { CheckPending(saved); }));
                content.Add(Button("Clear this uncertain bet", delegate { Forget(JObject.Parse(saved)); }));
            }
            if (tab == "sports") Sports(); else if (tab == "mine") Mine(); else if (tab == "results") Results(); else if (tab == "standings") Standings();
            else if (tab == "responsible") content.Add(Text("NaSpoBet is for people aged 18 and over. Bet only OC you can spare. Do not chase losses or borrow money to bet. Bets close at kick-off. If betting stops being fun, stop and speak to someone you trust."));
            else content.Add(Text("Questions about a bet: note its number in My bets and write to NaSpoBet through Stalburg Mail. Staff will never ask for your password or card details."));
        }
        private void SportsBar()
        {
            VisualElement row = Row();
            foreach (string name in new[] { "football", "hockey", "volleyball" })
            {
                string selected = name; row.Add(Button(name, delegate { sport = selected; resultsPage = 0; confirmation = ""; Refresh(); }));
            }
            content.Add(row);
        }
        private void Sports()
        {
            SportsBar();
            foreach (JToken entry in (JArray)info["sports"]) if ((string)entry["id"] == sport) content.Add(Text((string)entry["league"] + " · Season " + entry["season"] + " · Round " + entry["round"] + " of " + entry["rounds"]));
            if (((JArray)fixtures["rows"]).Count == 0) content.Add(Text("No matches."));
            foreach (JObject game in (JArray)fixtures["rows"])
            {
                JObject selected = game; content.Add(Text("Round " + game["round"] + " · " + (string)game["home"] + " v " + (string)game["away"] + " · " + TimeText((int)game["kickoff"]) + " · " + (string)game["status"]));
                VisualElement choices = Row();
                foreach (string side in new[] { "home", "draw", "away" })
                {
                    JToken price = game["odds"][side]; if (price == null) continue; string selection = side;
                    UnityEngine.UIElements.Button choice = Button(side + " " + Odds(price), delegate { fixture = (string)selected["id"]; fixtureSport = sport; pick = selection; match = (string)selected["home"] + " v " + (string)selected["away"]; confirmation = ""; Render(); });
                    choice.SetEnabled((string)game["status"] == "upcoming"); choices.Add(choice);
                }
                content.Add(choices);
            }
            if (fixture.Length == 0) { content.Add(Text("Select a match and outcome to create a bet slip.")); return; }
            content.Add(Text("Bet slip: " + match + " · " + pick));
            if (fixtureSport == "hockey") content.Add(Text("Ice hockey bets include overtime and shootouts."));
            TextField amount = new TextField("Stake (OC)"); amount.SetValueWithoutNotify(stake); content.Add(amount);
            Label quoteText = Text(""); amount.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { stake = e.newValue ?? ""; confirmation = ""; ShowQuote(quoteText); });
            VisualElement presets = Row(); foreach (int value in new[] { 10, 50, 100, 500 }) { int n = value; presets.Add(Button(n + " OC", delegate { stake = n.ToString(CultureInfo.InvariantCulture); confirmation = ""; Render(); })); }
            content.Add(presets); content.Add(quoteText); ShowQuote(quoteText);
            content.Add(Button("Review / place bet", Place)); content.Add(Button("Clear slip", delegate { fixture = ""; pick = ""; confirmation = ""; Render(); }));
        }
        private JObject Args()
        {
            int amount; if (!int.TryParse(stake, NumberStyles.None, CultureInfo.InvariantCulture, out amount)) return null;
            return new JObject(new JProperty("fixture", fixture), new JProperty("pick", pick), new JProperty("stake", amount));
        }
        private void ShowQuote(Label label)
        {
            JObject args = Args(); if (args == null) { label.text = "Enter a whole-number stake."; return; }
            JObject quote = Read("bet.quote", args); if (quote == null) { label.text = "Quote unavailable."; return; }
            label.text = (bool)quote["ok"] ? "Odds " + Odds(quote["odds"]) + " · Possible return " + quote["ret"] + " OC" : string.Join(" ", ((JArray)quote["problems"]).ToObject<string[]>());
        }
        private void Place()
        {
            if (!Session()) return; JObject args = Args(); if (args == null) { confirmation = ""; notice.text = "Enter a whole-number stake."; return; }
            foreach (string pending in client.PendingKeys)
            {
                if (!pending.StartsWith("bet.place ", StringComparison.Ordinal)) continue;
                string saved = pending.Substring("bet.place ".Length);
                if (BetReceipt.SameOrder(args, JObject.Parse(saved))) { confirmation = ""; CheckPending(saved); return; }
            }
            JObject quote = Read("bet.quote", args);
            if (quote == null || !(bool)quote["ok"]) { confirmation = ""; notice.text = "The bet cannot be placed. Check the current quote."; return; }
            args["expectedOdds"] = quote["odds"].DeepClone();
            if (!client.MayStart("bet.place", args, acknowledgedVersion == client.PendingVersion)) { confirmation = ""; notice.text = "Check My bets and acknowledge the warning first."; return; }
            if (client.PendingFor("bet.place", args)) { CheckPending(args.ToString(Formatting.None)); return; }
            string key = args.ToString(Formatting.None) + "|" + quote["odds"] + "|" + quote["ret"];
            if (confirmation != key) { confirmation = key; notice.text = "Review " + match + ", " + pick + ": " + stake + " OC at " + Odds(quote["odds"]) + ", possible return " + quote["ret"] + " OC. Press again to place the bet."; return; }
            confirmation = "";
            try { client.Write("bet.place", args, delegate(JToken result) { return BetReceipt.Valid(result, args); }); }
            catch (Exception e) { acknowledgedVersion = -1; UpdateWarning(); Render(); notice.text = e.Message + " Check My bets before trying again."; return; }
            fixture = ""; pick = ""; UpdateWarning(); Completed("Bet placed. Check My bets for details.");
        }
        private void CheckPending(string saved)
        {
            if (!Session()) return; JObject args; bool found;
            try { args = JObject.Parse(saved); found = client.Resolve("bet.place", args, "bet.result", args); }
            catch (Exception e) { notice.text = e.Message; return; }
            if (!found) { notice.text = "This bet cannot be confirmed. Check My bets; it may still have been placed."; return; }
            JObject current = Args(); if (BetReceipt.SameOrder(current, args)) { fixture = ""; pick = ""; }
            confirmation = ""; UpdateWarning(); Completed("The previous bet was placed. Check My bets for details.");
        }
        private void Forget(JObject args)
        {
            if (!Session()) return;
            if (acknowledgedVersion != client.PendingVersion) { notice.text = "Check My bets and acknowledge the warning first."; return; }
            if (!client.PendingFor("bet.place", args)) { forgetConfirmation = ""; notice.text = "No uncertain bet matches."; return; }
            string key = args.ToString(Formatting.None) + client.PendingVersion;
            if (forgetConfirmation != key) { forgetConfirmation = key; notice.text = "Clearing this record allows a new bet that could duplicate one already placed. Check My bets, then press clear again."; return; }
            client.Forget("bet.place", args); forgetConfirmation = ""; confirmation = ""; UpdateWarning(); Render(); notice.text = "Uncertain bet cleared. A new bet could repeat it.";
        }
        private void Completed(string message)
        {
            try { Refresh(); string error = info == null ? notice.text : ""; notice.text = message + (error.Length == 0 ? "" : " Refresh failed: " + error); }
            catch (Exception e) { notice.text = message + " Refresh failed: " + e.Message; }
        }
        private static string TimeText(int minute) { int time = minute % 1440; return "Day " + (minute / 1440) + ", " + (time / 60).ToString("00") + ":" + (time % 60).ToString("00"); }
        private void Mine()
        {
            content.Add(Text("My bets · Page " + (minePage + 1)));
            foreach (JObject group in new[] { openBets, settledBets }) foreach (JToken bet in (JArray)group["rows"])
                content.Add(Text("#" + bet["n"] + " · " + (string)bet["match"] + " · " + (string)bet["pick"] + " · " + bet["stake"] + " OC · Odds " + Odds(bet["odds"]) + " · Return " + ((string)bet["state"] == "lost" ? "0" : bet["ret"].ToString()) + " OC · " + (string)bet["state"] + " · " + TimeText((int)bet["at"])));
            VisualElement pages = Row(); pages.Add(Button("Previous", delegate { if (minePage > 0) { minePage--; Refresh(); } }));
            pages.Add(Button("Next", delegate { if ((minePage + 1) * 30 < Math.Max((int)openBets["total"], (int)settledBets["total"])) { minePage++; Refresh(); } })); content.Add(pages);
        }
        private void Results()
        {
            SportsBar(); content.Add(Text("Results · Page " + (resultsPage + 1)));
            foreach (JToken game in (JArray)results["rows"]) content.Add(Text((string)game["home"] + " v " + (string)game["away"] + " · " + (string)game["score"] + " " + (string)game["decider"] + " " + (string)game["sets"] + " · Winner " + (string)game["winner"] + " · Round " + game["round"]));
            VisualElement pages = Row(); pages.Add(Button("Previous", delegate { if (resultsPage > 0) { resultsPage--; Refresh(); } }));
            pages.Add(Button("Next", delegate { if ((resultsPage + 1) * 20 < (int)results["total"]) { resultsPage++; Refresh(); } })); content.Add(pages);
        }
        private void Standings()
        {
            content.Add(Text("Season " + table["season"]));
            foreach (JToken group in (JArray)table["sports"])
            {
                content.Add(Text((string)group["name"]));
                foreach (JToken row in (JArray)group["rows"]) content.Add(Text((string)row["team"] + " · Played " + row["played"] + " · Won " + row["won"] + (row["drawn"] == null ? "" : " · Drawn " + row["drawn"]) + " · Lost " + row["lost"] + " · Difference " + row["diff"] + " · Points " + row["points"]));
            }
        }
    }
}
