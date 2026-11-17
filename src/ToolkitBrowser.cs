using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitBrowser
    {
        internal readonly VisualElement Window;
        private readonly NotepadStorage storage;
        private readonly Func<int, bool> launch;
        private readonly Action<string> openWiki, openEye;
        private readonly Action checkSession;
        private readonly TransactionClient browser;
        private readonly ScrollView content;
        private readonly Label notice;
        private readonly TextField address;
        private BrowserState state;
        private readonly ToolkitSite site;
        private bool disposed;
        private static readonly string[,] Sites = {
            { "mail.stalburg.ts", "Stalburg Mail", "Email for Stalburg residents.", "0" },
            { "www.stalburgbank.ts", "Stalburg Bank", "Banking, balances and loans.", "8" },
            { "www.stalburgtimes.com", "Stalburg Times", "News from the outside world.", "9" },
            { "www.stalex.ts", "Stal-Ex", "The Stalburg stock exchange.", "5" },
            { "jobs.stalburg.ts", "Jobs", "Work stations and wages.", "19" },
            { "shop.stalburg.ts", "Shop", "Goods for sale.", "11" },
            { "bets.stalburg.ts", "NaSpoBet", "Football, ice hockey and volleyball odds.", "16" },
            { "lottery.stalburg.ts", "LOTTO by NaSpoBet", "Scratch cards and a number draw.", "20" },
            { "wiki.stalburg.ts", "Stalburg Wiki", "Articles on Stolland and Obenseuer.", "25" },
            { "www.eyebook.com", "EYEbook", "International social network.", "17" },
            { "www.pcmaster.ts", "PC Master", "Computers, parts and security software.", "18" },
            { "www.turvapro.ts", "TurvaPro 2000", "Antivirus and security.", "21" },
            { "www.free-rm.ts", "FREE RM GENERATOR", "Free RM generator.", "22" },
            { "www.kolhola-warez.ts", "Kolhola Warez", "Games and tools.", "23" },
            { "www.voittaja.ts", "VOITTAJA!", "Claim your prize.", "24" }
        };
        private const string Home = "www.stalburg.ts";

        internal ToolkitBrowser(NotepadStorage storage, Func<int, bool> launch, Action<string> openWiki, Action<string> openEye, Action checkSession, TransactionClient browser)
        {
            this.storage = storage; this.launch = launch; this.openWiki = openWiki; this.openEye = openEye; this.checkSession = checkSession; this.browser = browser;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 120; Window.style.top = 28; Window.style.width = 1100; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement titlebar = Row(); titlebar.style.height = 36;
            Label title = Text("Stalburg Web"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; titlebar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", Close);
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            titlebar.Add(close); Window.Add(titlebar); ToolkitUi.MakeDraggable(Window, titlebar);
            VisualElement toolbar = Row();
            toolbar.Add(Button("←", delegate { Move(-1); }));
            toolbar.Add(Button("→", delegate { Move(1); }));
            toolbar.Add(Button("Home", delegate { Visit(Home); }));
            address = new TextField(); address.style.flexGrow = 1;
            address.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e) { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Go(address.value); e.StopPropagation(); } });
            toolbar.Add(address); toolbar.Add(Button("Go", delegate { Go(address.value); }));
            toolbar.Add(Button("☆", Bookmark)); toolbar.Add(Button("History", delegate { Visit(Home + "/history"); })); toolbar.Add(Button("Bookmarks", delegate { Visit(Home + "/bookmarks"); })); Window.Add(toolbar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            site = new ToolkitSite(content, Navigate);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && state != null) Session(); }).Every(1000);
        }
        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront();
            if (!Session()) return;
            if (state == null)
            {
                try { state = new BrowserState(storage); }
                catch (Exception e) { notice.text = e.Message; content.Clear(); content.Add(Button("Retry", Open)); return; }
            }
            if (state.Current.Length == 0) Visit(Home);
            else if (site.IsCurrent(state.Current)) Render();
            else Transition(state.Current, delegate { });
        }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = text; return button; }
        internal bool CanClose() { return site.CanClose(); }
        internal void Dispose() { disposed = true; site.ForceClose(null); }
        internal void Reattach()
        {
            disposed = false;
            if (Window.style.display != DisplayStyle.None && state != null) Transition(state.Current, delegate { });
        }
        internal void DropSite(OsApp app)
        {
            if (!site.IsSite(app)) return;
            site.ForceClose(delegate { content.Clear(); content.Add(Text("This site is no longer available.")); });
        }
        private void Close()
        {
            if (site.Dismiss()) Window.style.display = DisplayStyle.None;
        }
        private bool Session()
        {
            if (disposed) return false;
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { site.ForceClose(delegate { state = null; address.SetValueWithoutNotify(""); content.Clear(); notice.text = e.Message; }); return false; }
        }
        private void Move(int direction)
        {
            if (state == null || !Session()) return;
            string next = state.Peek(direction);
            if (next != null) Transition(next, delegate { state.Move(direction); });
        }
        private static string Host(string url)
        {
            int end = url.IndexOfAny(new[] { '/', '?', '#' }); string host = (end < 0 ? url : url.Substring(0, end)).ToLowerInvariant();
            if (host == "stalburg.net") return "wiki.stalburg.ts";
            if (host == "mail.stalburg.com") return "mail.stalburg.ts";
            for (int i = 0; i < Sites.GetLength(0); i++) if (string.Equals(host.StartsWith("www.") ? host.Substring(4) : host, Sites[i, 0].StartsWith("www.") ? Sites[i, 0].Substring(4) : Sites[i, 0], StringComparison.Ordinal)) return Sites[i, 0];
            if (host == "stalburg.ts") return Home;
            return host;
        }
        private string Title(string url)
        {
            string host = Host(url); if (host == Home) return "Stalburg Web";
            for (int i = 0; i < Sites.GetLength(0); i++) if (Sites[i, 0] == host) return Sites[i, 1];
            OsApp site = BrowserSiteCatalog.Find(Os.Apps, host);
            return site == null ? "Server not found" : site.Title;
        }
        private void Go(string text)
        {
            string input = (text ?? "").Trim(); if (input.Length == 0) return;
            if (input.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) input = input.Substring(8);
            else if (input.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) input = input.Substring(7);
            if (input.IndexOf(' ') >= 0 || input.IndexOf('.') < 0) Visit(Home + "/search?q=" + Uri.EscapeDataString(input));
            else Visit(input.Length > 300 ? input.Substring(0, 300) : input);
        }
        internal bool Navigate(string url) { if (state == null) Open(); if (state == null || !Session()) return false; Window.style.display = DisplayStyle.Flex; if (!Visit(url) || state == null || state.Current != url) return false; Window.BringToFront(); return true; }
        private bool Visit(string url)
        {
            if (state == null || !Session()) return false;
            if (url == state.Current && site.IsCurrent(url)) { Render(); return true; }
            return Transition(url, delegate
            {
                bool changed = state.Visit(url, Title(url));
                if (changed && browser != null)
                    try { browser.Read("virus.browse", new JObject(new JProperty("host", Host(url)))); } catch (Exception) { }
            });
        }
        private bool Transition(string url, Action commit)
        {
            if (state == null || !Session()) return false;
            OsApp destination = BrowserSiteCatalog.Find(Os.Apps, Host(url));
            bool committed = false;
            bool done = site.Transition(destination, url, Session, delegate { commit(); committed = true; });
            if (!done)
            {
                notice.text = committed ? "The destination became unavailable." : "Navigation was cancelled or the site could not be opened.";
                if (committed && state != null) Render();
                return false;
            }
            notice.text = ""; Render(); return true;
        }
        private void Bookmark()
        {
            if (state == null || !Session()) return;
            try { state.ToggleMark(Title(state.Current)); notice.text = ""; }
            catch (Exception e) { notice.text = "Bookmark was not saved: " + e.Message; }
            Render();
        }
        private void Render()
        {
            if (state == null || !Session()) return;
            string url = state.Current; address.SetValueWithoutNotify(url);
            if (site.IsCurrent(url)) return;
            content.Clear();
            string host = Host(url);
            if (host == Home)
            {
                if (url.IndexOf("/history", StringComparison.OrdinalIgnoreCase) >= 0) { Entries("History", state.History); return; }
                if (url.IndexOf("/bookmarks", StringComparison.OrdinalIgnoreCase) >= 0) { Entries("Bookmarks", state.Marks); return; }
                int search = url.IndexOf("/search?q=", StringComparison.OrdinalIgnoreCase);
                string query = search < 0 ? "" : Uri.UnescapeDataString(url.Substring(search + 10));
                Label heading = Text(search < 0 ? "Stalburg Web" : "Search: " + query); heading.style.fontSize = 28; content.Add(heading);
                TextField field = new TextField("Search Stalburg Web"); field.SetValueWithoutNotify(query); content.Add(field);
                content.Add(Button("Search", delegate { Visit(Home + "/search?q=" + Uri.EscapeDataString(field.value ?? "")); }));
                for (int i = 0; i < Sites.GetLength(0); i++)
                {
                    int site = i;
                    if (query.Length > 0 && (Sites[i, 0] + " " + Sites[i, 1] + " " + Sites[i, 2]).IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    content.Add(Button(Sites[i, 1] + " — " + Sites[i, 2], delegate { Visit(Sites[site, 0]); }));
                }
                foreach (OsApp entry in Os.Apps)
                {
                    if (!BrowserSiteCatalog.Matches(entry, query)) continue;
                    OsApp site = entry;
                    content.Add(Button(site.Title + " — " + site.Description, delegate { Visit(site.Host); }));
                }
                return;
            }
            for (int i = 0; i < Sites.GetLength(0); i++)
            {
                if (Sites[i, 0] != host) continue;
                content.Add(Text(Sites[i, 1])); content.Add(Text(Sites[i, 2]));
                int app;
                if (int.TryParse(Sites[i, 3], out app))
                {
                    int pathAt = url.IndexOfAny(new[] { '/', '?', '#' });
                    string path = pathAt < 0 ? "" : url.Substring(pathAt);
                    content.Add(Button("Open " + Sites[i, 1], delegate { if (!Session()) return; if (app == 25 && openWiki != null) openWiki(path); else if (app == 17 && openEye != null) openEye(path); else launch(app); }));
                }
                else content.Add(Text(host == "wiki.stalburg.ts" ? "Offline wiki articles are not installed yet." : "This site is not available in the Toolkit interface yet."));
                return;
            }
            OsApp modSite = BrowserSiteCatalog.Find(Os.Apps, host);
            if (modSite != null)
            {
                content.Add(Text(modSite.Title)); content.Add(Text(modSite.Description));
                content.Add(Text("This mod site needs a native UI Toolkit version before it can open here."));
                return;
            }
            content.Add(Text("Server not found: " + host));
        }
        private void Entries(string title, JArray entries)
        {
            content.Add(Text(title));
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                string url = (string)entries[i]["url"]; string label = (string)entries[i]["title"];
                content.Add(Button(label + " — " + url, delegate { Visit(url); }));
            }
            if (entries.Count == 0) content.Add(Text("None yet."));
        }
    }
}
