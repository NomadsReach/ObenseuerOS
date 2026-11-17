using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitVirusFx
    {
        private readonly VisualElement layer, desktop, wall;
        private readonly List<VisualElement> desktopOrder = new List<VisualElement>();
        private bool shuffled;
        private readonly Func<bool> visible;
        private readonly Func<string, bool> navigate;
        private readonly Action<int> openSite;
        private readonly Func<bool> turvaInstalled;
        private readonly Action openTurva;
        private readonly TransactionClient client;
        private readonly System.Random random = new System.Random();
        private readonly List<VisualElement> live = new List<VisualElement>();
        private static readonly string[] Brands = { "VOITTAJA! Lucky visitor", "Moca-cola", "FREE RM GENERATOR", "TurvaPro", "Viewports 8", "TurvaPro 2000", "VOITTAJA!" };
        private static readonly string[] Headlines = { "YOU WON 5 000 RM!", "Free crate of Moca-cola, claim now", "Free RM generator, 100 % working", "Your Viewports is SLOW!", "Error 0x0000E44 in KOLHOLA.DLL", "TurvaPro found 3 threats! Scan now", "Prize claimed, check your mail" };
        private static readonly string[] Details = { "Click CLAIM PRIZE now. Offer ends today!", "Ice cold, 100 % free. Only 3 crates left!", "Generate 1 000 RM in seconds.", "Speed up your PC for free.", "The application has performed an illegal operation.", "Your PC is at risk.", "Your prize will arrive by post." };
        private static readonly string[] Buttons = { "CLAIM PRIZE", "CLAIM NOW", "DOWNLOAD NOW", "SPEED UP", "", "SCAN NOW", "" };

        internal ToolkitVirusFx(VisualElement layer, VisualElement desktop, VisualElement wall, Func<bool> visible, Func<string, bool> navigate, Action<int> openSite, Func<bool> turvaInstalled, Action openTurva, TransactionClient client)
        {
            this.layer = layer; this.desktop = desktop; this.wall = wall; this.visible = visible; this.navigate = navigate; this.openSite = openSite; this.turvaInstalled = turvaInstalled; this.openTurva = openTurva; this.client = client;
            foreach (VisualElement child in desktop.Children()) desktopOrder.Add(child);
            VirusFx.Pulsed += Pulse;
            layer.schedule.Execute(Poll).Every(2000);
        }
        internal void Release() { VirusFx.Pulsed -= Pulse; Clear(); ResetDesktop(); }
        private void Clear() { foreach (VisualElement popup in live) popup.RemoveFromHierarchy(); live.Clear(); }
        private void Poll()
        {
            if (!visible()) { Clear(); ResetDesktop(); return; }
            try
            {
                JObject state = client.Read("virus.state", null) as JObject; JObject fx = state == null ? null : state["fx"] as JObject;
                bool wallOn, shuffleOn;
                if (fx == null || !VirusDesktopState.TryRead(state, out wallOn, out shuffleOn)) return;
                wall.style.display = wallOn ? DisplayStyle.Flex : DisplayStyle.None;
                if (shuffleOn && !shuffled) Shuffle();
                else if (!shuffleOn && shuffled) RestoreOrder();
                if ((int?)fx["popups"] == 0 && (int?)fx["lag"] == 0 && (bool?)fx["wall"] == false && (bool?)fx["shuffle"] == false && (bool?)fx["errors"] == false && (bool?)fx["nag"] == false) Clear();
            }
            catch (Exception) { }
        }
        private void ResetDesktop() { wall.style.display = DisplayStyle.None; if (shuffled) RestoreOrder(); }
        private void RestoreOrder() { foreach (VisualElement child in desktopOrder) child.BringToFront(); shuffled = false; }
        private void Shuffle()
        {
            List<VisualElement> order = new List<VisualElement>(desktopOrder);
            for (int i = order.Count - 1; i > 0; i--) { int j = random.Next(i + 1); VisualElement swap = order[i]; order[i] = order[j]; order[j] = swap; }
            foreach (VisualElement child in order) child.BringToFront();
            shuffled = true;
        }
        private void Pulse(VirusPulse pulse)
        {
            if (!visible()) return;
            if (shuffled && random.NextDouble() < 0.3) Shuffle();
            for (int i = 0; i < pulse.Popups; i++) Open(random.Next(4), false);
            if (pulse.Error) Open(4, false);
            if (pulse.Nag) Open(5, false);
        }
        private void Open(int kind, bool spawned)
        {
            if (!visible() || !VirusPopupRules.Room(live.Count)) return;
            VisualElement popup = new VisualElement(); popup.style.position = Position.Absolute;
            popup.style.left = Length.Percent(random.Next(5, 65)); popup.style.top = Length.Percent(random.Next(5, 65));
            popup.style.width = 360; popup.style.paddingBottom = 10; popup.style.backgroundColor = (Color)new Color32(245, 237, 185, 255);
            VisualElement bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.backgroundColor = (Color)new Color32(33, 62, 120, 255);
            Label brand = new Label(Brands[kind]); brand.style.color = Color.white; brand.style.flexGrow = 1; bar.Add(brand);
            int dodges = 0, corner = 0;
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(); close.text = kind == 4 || kind == 6 ? "OK" : "X";
            close.clicked += delegate
            {
                if (VirusPopupRules.Dodge(dodges, random.NextDouble()))
                {
                    dodges++; corner = (corner + 1 + random.Next(3)) % 4;
                    popup.style.left = Length.Percent(corner % 2 == 0 ? 5 : 65); popup.style.top = Length.Percent(corner < 2 ? 5 : 65); return;
                }
                popup.RemoveFromHierarchy(); live.Remove(popup);
                if (VirusPopupRules.Spawn(live.Count, spawned, random.NextDouble())) { Open(random.Next(4), true); Open(random.Next(4), true); }
            };
            bar.Add(close); popup.Add(bar);
            Label headline = new Label(Headlines[kind]); headline.style.color = Color.black; headline.style.fontSize = 18; headline.style.whiteSpace = WhiteSpace.Normal; popup.Add(headline);
            Label detail = new Label(Details[kind]); detail.style.color = Color.black; detail.style.whiteSpace = WhiteSpace.Normal; popup.Add(detail);
            if (Buttons[kind].Length > 0)
            {
                UnityEngine.UIElements.Button go = new UnityEngine.UIElements.Button(delegate
                {
                    if (!visible()) return;
                    popup.RemoveFromHierarchy(); live.Remove(popup);
                    if (kind < 2) { Claim(kind == 0 ? "winner" : "moca"); return; }
                    if (kind == 5 && turvaInstalled()) { openTurva(); return; }
                    string url = kind == 2 ? "www.free-rm.ts" : "www.turvapro.ts";
                    if (navigate(url)) openSite(kind == 2 ? 22 : 21);
                });
                go.text = Buttons[kind]; popup.Add(go);
            }
            live.Add(popup); layer.Add(popup);
        }
        private void Claim(string kind)
        {
            if (!visible()) return;
            if (client.HasPending) { Uncertain(); return; }
            try
            {
                JObject result = client.Write("virus.claim", new JObject(new JProperty("kind", kind)), delegate(JToken r) { return ShadyReply.Valid(r as JObject, null); }) as JObject;
                if ((string)result["outcome"] != "blocked") Open(6, false);
            }
            catch (Exception) { Uncertain(); }
        }
        private void Uncertain()
        {
            if (!visible() || !VirusPopupRules.Room(live.Count)) return;
            VisualElement popup = new VisualElement(); popup.style.position = Position.Absolute; popup.style.left = Length.Percent(20); popup.style.top = Length.Percent(20); popup.style.width = 400; popup.style.backgroundColor = Color.white;
            Label message = new Label("The claim may have completed. Repeating it may cause its effects again."); message.style.color = Color.black; message.style.whiteSpace = WhiteSpace.Normal; popup.Add(message);
            bool confirm = false;
            UnityEngine.UIElements.Button clear = null;
            clear = new UnityEngine.UIElements.Button(delegate
            {
                if (!confirm) { confirm = true; clear.text = "Confirm clearing uncertain claim"; return; }
                client.Forget("virus.claim", new JObject(new JProperty("kind", "winner"))); client.Forget("virus.claim", new JObject(new JProperty("kind", "moca")));
                popup.RemoveFromHierarchy(); live.Remove(popup);
            });
            clear.text = "Clear uncertain claim"; popup.Add(clear);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(delegate { popup.RemoveFromHierarchy(); live.Remove(popup); }); close.text = "Close"; popup.Add(close);
            live.Add(popup); layer.Add(popup);
        }
    }
}
