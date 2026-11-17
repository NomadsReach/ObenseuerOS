using System;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitTurva
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient site, app;
        private readonly Action checkSession;
        private readonly ScrollView content;
        private readonly Label title, notice;
        private JObject state;
        private bool program, confirm;
        private string report = "";

        internal ToolkitTurva(TransactionClient site, TransactionClient app, Action checkSession)
        {
            this.site = site; this.app = app; this.checkSession = checkSession;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 180; Window.style.top = 32; Window.style.width = 950; Window.style.height = 650;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.height = 36;
            title = Text("TurvaPro 2000"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && state != null) Session(); }).Every(1000);
        }
        internal void OpenSite() { program = false; report = ""; Open(); }
        internal void OpenProgram() { program = true; report = ""; Open(); }
        private void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(); }
        private static Label Text(string text) { Label label = new Label(text); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = text; return button; }
        private bool Session()
        {
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { state = null; content.Clear(); notice.text = e.Message; return false; }
        }
        private void Refresh()
        {
            if (!Session()) return;
            try
            {
                JObject next = (program ? app : site).Read("av.state", null) as JObject;
                if (next == null || next["owned"] == null || next["owned"].Type != JTokenType.Boolean || next["status"] == null || next["status"].Type != JTokenType.String) throw new InvalidOperationException("Invalid subscription status.");
                state = next;
                if ((string)state["status"] == "active" && site.HasPending) site.Forget("av.buy", new JObject());
                if (!(bool)state["owned"] && app.PendingFor("av.cancel", new JObject())) app.Forget("av.cancel", new JObject());
                notice.text = site.HasPending || app.HasPending ? "A previous action may have completed. Check your subscription, wallet and scan status before trying again." : "";
                Render();
            }
            catch (Exception e) { state = null; content.Clear(); notice.text = e.Message; content.Add(Button("Refresh", Refresh)); }
        }
        private void Render()
        {
            if (state == null || !Session()) return;
            content.Clear(); title.text = "TurvaPro 2000";
            Label heading = Text(program ? "TurvaPro 2000" : "#1 ANTIVIRUS!! TurvaPro 2000"); heading.style.fontSize = 28; content.Add(heading);
            content.Add(Text("Status: " + state["status"] + "    Last scan: " + state["lastScan"]));
            if (!program)
            {
                content.Add(Text("99 OC a week. Full protection. Cancel any time!!!"));
                content.Add(Text("Your wallet: " + state["walletOc"] + " OC"));
                content.Add(Button("FREE ONLINE SCAN", FreeScan));
                if ((string)state["status"] != "active" && !site.HasPending) content.Add(Button("Subscribe / pay now", Buy));
                if ((bool)state["owned"]) content.Add(Button("Open TurvaPro 2000", OpenProgram));
            }
            else
            {
                content.Add(Text("Days to next bill: " + state["daysToBill"] + "; unpaid days: " + state["lapsedDays"]));
                if ((string)state["status"] == "lapsed") content.Add(Button("PAY NOW", OpenSite));
                if ((bool)state["owned"] && !app.HasPending)
                {
                    if ((int)state["scanReadyIn"] == 0) content.Add(Button("SCAN NOW", Scan));
                    else content.Add(Text("Scan available in " + state["scanReadyIn"] + " game minutes."));
                    content.Add(Button("Cancel subscription", Cancel));
                }
            }
            if (report.Length > 0) content.Add(Text(report));
            TransactionClient pending = program && app.HasPending ? app : site.HasPending ? site : null;
            if (pending != null) content.Add(Button(confirm ? "Confirm: clear uncertain action" : "Clear uncertain action", delegate
            {
                if (!confirm) { confirm = true; notice.text = "The previous action may have completed. Clearing it can repeat charges or lose scan results. Select again to confirm."; Render(); return; }
                foreach (string method in new[] { "av.buy", "av.scan", "av.cancel" }) pending.Forget(method, new JObject());
                confirm = false; Refresh();
            }));
            content.Add(Button("Refresh", Refresh));
        }
        private void FreeScan()
        {
            if (!Session()) return;
            try { JObject result = site.Read("av.freescan", null) as JObject; if (result == null || result["threats"] == null) throw new InvalidOperationException("Scan result unavailable."); report = "Scan complete: " + result["threats"] + " threats found!!! Subscribe now to remove them."; Render(); }
            catch (Exception e) { notice.text = e.Message; }
        }
        private void Buy()
        {
            if (!Session() || site.HasPending) return;
            try { site.Write("av.buy", new JObject(), TurvaReceipt.Accepted); Refresh(); if (state != null) notice.text = "Subscription activated. A receipt is on its way to your mail."; }
            catch (Exception e) { Refresh(); notice.text = e.Message + " Check the subscription and wallet before another attempt."; }
        }
        private void Scan()
        {
            if (!Session() || app.HasPending) return;
            try
            {
                JObject result = app.Write("av.scan", new JObject(), TurvaReceipt.ScanAccepted) as JObject;
                report = "Tracking cookies: " + result["cookies"];
                JArray found = (JArray)result["found"];
                if (found.Count == 0) report += "\nNo threats found.";
                foreach (JToken item in found) report += "\n" + item["name"] + ": " + item["result"];
                Refresh();
            }
            catch (Exception e) { Refresh(); notice.text = e.Message + " The scan may have completed; its report may be lost."; }
        }
        private void Cancel()
        {
            if (!Session() || app.HasPending) return;
            try { app.Write("av.cancel", new JObject(), delegate(JToken r) { return r is JObject && r["owned"] != null && r["owned"].Type == JTokenType.Boolean && !(bool)r["owned"]; }); Refresh(); if (state != null) notice.text = "Subscription cancelled. TurvaPro 2000 is off."; }
            catch (Exception e) { Refresh(); notice.text = e.Message + " Check your subscription before trying again."; }
        }
    }
}
