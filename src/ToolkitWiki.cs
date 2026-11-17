using System;
using System.Collections.Generic;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitWiki
    {
        internal readonly VisualElement Window;
        private readonly string folder;
        private readonly ScrollView body;
        private readonly TextField search;
        private bool limited;
        private readonly Stack<OfflineWikiPage> history = new Stack<OfflineWikiPage>();
        private OfflineWikiPage current;
        private List<OfflineWikiPage> pages = new List<OfflineWikiPage>();
        internal ToolkitWiki(string folder)
        {
            this.folder = folder;
            Window = new VisualElement(); Window.style.position = Position.Absolute;
            Window.style.left = 120; Window.style.top = 28; Window.style.width = 1100; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.height = 36;
            Label title = Text("Stalburg Wiki (offline)"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; }); close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            VisualElement tools = new VisualElement(); tools.style.flexDirection = FlexDirection.Row;
            search = new TextField(); search.style.flexGrow = 1; search.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e) { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { Index(); e.StopPropagation(); } });
            tools.Add(search); tools.Add(Button("Search", Index)); tools.Add(Button("All articles", delegate { search.value = ""; Index(); })); tools.Add(Button("Reload", Open)); Window.Add(tools);
            body = new ScrollView(); body.style.flexGrow = 1; body.style.backgroundColor = Color.white; Window.Add(body);
        }
        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront(); pages = OfflineWiki.Load(folder, out limited); history.Clear(); current = null; Index();
        }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = value; return button; }
        private void Index()
        {
            history.Clear(); current = null; body.Clear(); Label heading = Text("Stalburg Wiki"); heading.style.fontSize = 28; body.Add(heading);
            body.Add(Text("Offline saved pages. No network connection is used."));
            if (limited) body.Add(Text("Some saved pages could not be loaded or exceed the reader limit."));
            string query = (search.value ?? "").Trim(); int shown = 0;
            foreach (OfflineWikiPage page in pages)
            {
                OfflineWikiPage selected = page;
                if (query.Length > 0 && page.Title.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0 && page.Text.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                body.Add(Button(page.Title, delegate { Show(selected, true); })); shown++;
            }
            if (pages.Count == 0) body.Add(Text(limited ? "No readable saved articles were found." : "No saved wiki articles are installed."));
            else if (shown == 0) body.Add(Text("No saved articles match your search."));
        }
        private void Show(OfflineWikiPage page, bool record)
        {
            if (record && current != null && current != page && history.Count < 100) history.Push(current);
            current = page; body.Clear(); body.Add(Button("← Articles", Index));
            if (history.Count > 0) body.Add(Button("← Previous article", delegate { Show(history.Pop(), false); }));
            Label title = Text(page.Title); title.style.fontSize = 28; body.Add(title);
            body.Add(Text(page.Text));
            bool heading = false;
            foreach (string link in page.Links)
            {
                foreach (OfflineWikiPage candidate in pages)
                {
                    if (!string.Equals(link, candidate.Title, StringComparison.OrdinalIgnoreCase) || candidate == page) continue;
                    if (!heading) { body.Add(Text("Related saved articles")); heading = true; }
                    OfflineWikiPage target = candidate; body.Add(Button(target.Title, delegate { Show(target, true); })); break;
                }
            }
            body.Add(Text("Saved copy from Stalburg Wiki; content may be outdated. Links to unsaved pages are unavailable."));
        }
    }
}
