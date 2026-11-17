using System;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitSecurity
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient store, guard;
        private readonly Action checkSession;
        private readonly ScrollView content;
        private readonly Label notice, title;
        private JObject state;
        private bool program, confirmForget, confirmScan;
        private string category = "Security";

        internal ToolkitSecurity(TransactionClient store, TransactionClient guard, Action checkSession)
        {
            this.store = store; this.guard = guard; this.checkSession = checkSession;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 180; Window.style.top = 32; Window.style.width = 950; Window.style.height = 650;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.height = 36;
            title = Text(""); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && state != null) Session(); }).Every(1000);
        }
        internal void OpenStore() { program = false; Open(); }
        private void OpenGuard() { program = true; Open(); }
        private void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(); }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = value; return button; }
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
                JObject next = (program ? guard : store).Read("av.state", null) as JObject;
                if (next == null || next["owned"] == null || next["owned"].Type != JTokenType.Boolean || !(next["quarantine"] is JArray)) throw new InvalidOperationException("Invalid security status.");
                state = next;
                if ((bool)state["owned"] && store.HasPending) store.Forget("av.buy", new JObject());
                notice.text = program && guard.HasPending ? "The previous scan may have finished, but its report was lost. Check your files before scanning again." : store.HasPending ? "Purchase result unknown. Check your wallet and refresh before trying again." : "";
                Render();
            }
            catch (Exception e) { state = null; content.Clear(); notice.text = e.Message; content.Add(Button("Refresh", Refresh)); }
        }
        private void Render()
        {
            if (state == null || !Session()) return;
            content.Clear(); title.text = program ? "PC Master Guard" : "PC Master";
            Label heading = Text(program ? "PC Master Guard 3.0" : "PC MASTER"); heading.style.fontSize = 28; content.Add(heading);
            if (program)
            {
                if (!(bool)state["owned"]) { content.Add(Text("PC Master Guard is not installed.")); content.Add(Button("Visit PC Master", OpenStore)); return; }
                content.Add(Text("Real-time protection: ON    Threats blocked: " + state["blocked"] + "    Last full scan: " + (state["lastScan"].Type == JTokenType.Null ? "never" : "day " + state["lastScan"])));
                if (!guard.HasPending) content.Add(Button("SCAN NOW", Scan));
                else content.Add(Button(confirmScan ? "Confirm: clear uncertain scan" : "Clear uncertain scan", delegate
                {
                    if (!confirmScan) { confirmScan = true; notice.text = "The scan may have restored files and removed threats. Its report cannot be recovered. Select again to confirm."; Render(); return; }
                    guard.Forget("av.scan", new JObject()); confirmScan = false; Refresh();
                }));
                content.Add(Text("Stolen money report"));
                JArray stolen = (JArray)state["quarantine"];
                if (stolen.Count == 0) content.Add(Text("Nothing has been stolen from this computer."));
                foreach (JToken item in stolen) content.Add(Text(item["name"] + " — day " + item["day"] + ": " + item["stolenOc"] + " OC, " + item["stolenRm"] + " RM"));
                content.Add(Button("Refresh", Refresh)); return;
            }
            content.Add(Text("Computers, parts and software for all of Stolland."));
            VisualElement tabs = new VisualElement(); tabs.style.flexDirection = FlexDirection.Row;
            foreach (string name in new[] { "Desktops", "Laptops", "Parts", "Software", "Security" })
            { string selected = name; tabs.Add(Button(name, delegate { category = selected; Render(); })); }
            content.Add(tabs);
            if (category != "Security") { Catalogue(); return; }
            content.Add(Text("PC Master Guard 3.0 — one payment, no subscription."));
            content.Add(Text("Real-time protection, full scan, file recovery and stolen money report."));
            content.Add(Text("Price: " + state["priceRm"] + " RM    Your wallet: " + state["walletRm"] + " RM"));
            if ((bool)state["owned"]) content.Add(Button("Open PC Master Guard", OpenGuard));
            else if (!store.HasPending) content.Add(Button("Buy PC Master Guard", Buy));
            else content.Add(Button(confirmForget ? "Confirm: clear uncertain purchase" : "Clear uncertain purchase", delegate
            {
                if (!confirmForget) { confirmForget = true; notice.text = "A previous purchase may have charged you. Check your wallet before clearing it; select again to confirm."; Render(); return; }
                store.Forget("av.buy", new JObject()); confirmForget = false; Refresh();
            }));
            content.Add(Button("Refresh", Refresh));
        }
        private void Catalogue()
        {
            content.Add(Text(category + " — catalogue only. PC Master Guard is the one product you can order online."));
            if (category == "Desktops") content.Add(Text("Stalburg Home 2001 — 1290 RM\nOffice Korso — 1790 RM\nMine Edition — 1490 RM"));
            if (category == "Laptops") content.Add(Text("Kolhola Portable — 2190 RM\nTraveller 2 — 1990 RM"));
            if (category == "Parts") content.Add(Text("RAM 64 MB — 89 RM\nDisk 4 GB — 240 RM\nLagTel modem — 65 RM"));
            if (category == "Software") content.Add(Text("Notepad Plus — 35 RM\nTramtimes 2001 — 49 RM\nSolitaire Deluxe — Free"));
        }
        private void Buy()
        {
            if (!Session() || store.HasPending) return;
            try
            {
                store.Write("av.buy", new JObject(), GuardPurchase.Accepted);
                Refresh(); if (state != null && (bool)state["owned"]) notice.text = "PC Master Guard is installed.";
            }
            catch (Exception e) { Refresh(); if (state != null && !(bool)state["owned"]) notice.text = e.Message + " Check your wallet and refresh before trying again."; }
        }
        private void Scan()
        {
            if (!Session()) return;
            try
            {
                JObject result = guard.Write("av.scan", new JObject(), delegate(JToken r) { return r is JObject && r["found"] is JArray && r["files"] is JObject; }) as JObject;
                if (result == null || !(result["found"] is JArray) || !(result["files"] is JObject)) throw new InvalidOperationException("The scan result is unavailable. Refresh before scanning again.");
                Refresh();
                content.Add(Text("Scan results:"));
                JArray found = (JArray)result["found"];
                if (found.Count == 0) content.Add(Text("No threats found. Your computer is clean."));
                foreach (JToken item in found) content.Add(Text(item["name"] + ": " + item["result"]));
                content.Add(Text("Files put back: " + result["files"]["restored"] + ". Junk files removed: " + result["files"]["junk"] + ". Files still to restore: " + result["files"]["left"] + "."));
            }
            catch (Exception e) { Refresh(); notice.text = e.Message + " The scan may have finished; check your files before scanning again."; }
        }
    }
}
