using System;
using System.Collections.Generic;
using System.IO;
using ObenseuerCore;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
internal sealed class ToolkitPrototype : MonoBehaviour
{
    private static readonly string[] Names = { "Stalburg Mail", "Stalburg Web", "Explorer", "Recycle Bin", "Control Panel", "Stal-Ex", "Command Prompt", "Notepad", "Stalburg Bank", "Stalburg Times", "TOR-Turnip Chat v0.3.9", "Shop", "Minesweeper", "Solitaire" };
    private static readonly string[] Icons = { "mail", "world", "folder-filled", "trash", "settings", "chart-line", "terminal-2", "notes", "building-bank", "news", "message-circle", "shopping-cart", "device-gamepad-2", "device-gamepad-2" };
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private VisualElement selected;
    private NotepadStorage storage;
    private ToolkitExplorer explorer;
    private Label clock;
    private Label money;
    private float nextStatusUpdate;

    internal static void Open(RectTransform host)
    {
        Open(host, null);
    }

    internal static void Open(RectTransform host, NotepadStorage storage)
    {
        GameObject owner = new GameObject("UI Toolkit comparison");
        owner.transform.SetParent(host, false);
        ToolkitPrototype prototype = owner.AddComponent<ToolkitPrototype>();
        prototype.storage = storage;
        prototype.Build();
    }

    private void Build()
    {
        VisualElement root = ToolkitUi.CreateOverlay(transform);
        root.style.flexGrow = 1;
        root.style.backgroundColor = (Color)new Color32(45, 95, 139, 255);
        VisualElement desktop = new VisualElement();
        desktop.style.flexGrow = 1;
        desktop.style.flexDirection = FlexDirection.Column;
        desktop.style.flexWrap = Wrap.Wrap;
        desktop.style.alignContent = Align.FlexStart;
        desktop.style.paddingLeft = 8;
        desktop.style.paddingTop = 8;
        root.Add(desktop);
        ToolkitNotepad notepad = new ToolkitNotepad(storage, delegate { if (explorer != null) explorer.RefreshIfOpen(); });
        ToolkitMinesweeper minesweeper = new ToolkitMinesweeper();
        ToolkitSolitaire solitaire = new ToolkitSolitaire();
        ToolkitRecycle recycle = null;
        explorer = new ToolkitExplorer(storage as ExplorerStorage, notepad.OpenPath, delegate { if (recycle != null) recycle.RefreshIfOpen(); }, delegate { if (recycle != null) recycle.Open(); });
        recycle = new ToolkitRecycle(storage as RecycleStorage, delegate { explorer.RefreshIfOpen(); });
        OsNotepadStorage osStorage = storage as OsNotepadStorage;
        ToolkitChat chat = new ToolkitChat(osStorage != null, osStorage == null ? (Action)null : osStorage.CheckSession);
        ToolkitTerminal terminal = null;
        terminal = new ToolkitTerminal(storage, storage as ExplorerStorage, OsView.UserName,
            delegate { if (!WorldClock.IsAvailable) return "The game clock is not ready."; WorldTime now = WorldClock.Now; return string.Format("The current date is: {0}.1.1843, {1}", now.CalendarDay, now.Weekday); },
            delegate { if (!WorldClock.IsAvailable) return "The game clock is not ready."; WorldTime now = WorldClock.Now; return string.Format("The current time is: {0:00}:{1:00}", now.Hour, now.Minute); },
            delegate { if (!Cash.IsAvailable) return "The game clock is not ready."; return string.Format("{0:N0} OC   {1:N2} RM", (int)Cash.Balance(MoneyCurrency.OC), Cash.Balance(MoneyCurrency.RM)); },
            delegate(string app)
            {
                if (string.Equals(app, "files", StringComparison.OrdinalIgnoreCase) || string.Equals(app, "Explorer", StringComparison.OrdinalIgnoreCase)) { explorer.Open(); return true; }
                if (string.Equals(app, "recycle", StringComparison.OrdinalIgnoreCase) || string.Equals(app, "Recycle Bin", StringComparison.OrdinalIgnoreCase)) { recycle.Open(); return true; }
                if (string.Equals(app, "notes", StringComparison.OrdinalIgnoreCase) || string.Equals(app, "Notepad", StringComparison.OrdinalIgnoreCase)) { notepad.Window.style.display = DisplayStyle.Flex; notepad.Window.BringToFront(); return true; }
                if (string.Equals(app, "minesweeper", StringComparison.OrdinalIgnoreCase)) { minesweeper.Open(); return true; }
                if (string.Equals(app, "solitaire", StringComparison.OrdinalIgnoreCase)) { solitaire.Open(); return true; }
                if (string.Equals(app, "terminal", StringComparison.OrdinalIgnoreCase) || string.Equals(app, "Command Prompt", StringComparison.OrdinalIgnoreCase)) { terminal.Open(); return true; }
                if (string.Equals(app, "chat", StringComparison.OrdinalIgnoreCase) || string.Equals(app, "TOR-Turnip Chat v0.3.9", StringComparison.OrdinalIgnoreCase)) { chat.Open(); return true; }
                return false;
            },
            delegate(string path) { if (path == null) { notepad.Window.style.display = DisplayStyle.Flex; notepad.Window.BringToFront(); } else notepad.OpenPath(path); if (explorer != null) explorer.RefreshIfOpen(); });
        ComputerApps.SetCloseGuard(transform.parent, notepad.CanClose);
        VisualElement window = notepad.Window;
        for (int i = 0; i < Names.Length; i++)
        {
            int index = i;
            VisualElement tile = new VisualElement();
            tile.style.width = 88;
            tile.style.height = 104;
            tile.style.alignItems = Align.Center;
            Image icon = new Image();
            icon.image = Texture(Icons[i]);
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.tintColor = i == 2 ? (Color)new Color32(242, 201, 76, 255) : Color.white;
            icon.style.width = 48;
            icon.style.height = 48;
            icon.style.marginTop = 4;
            tile.Add(icon);
            Label label = new Label(Names[i]);
            label.style.color = Color.white;
            label.style.fontSize = 12;
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.width = 80;
            label.style.marginTop = 4;
            tile.Add(label);
            tile.RegisterCallback<ClickEvent>(delegate(ClickEvent e)
            {
                if (e.button != 0) return;
                if (selected != null) selected.style.backgroundColor = Color.clear;
                selected = tile;
                selected.style.backgroundColor = (Color)new Color32(120, 170, 230, 77);
                if (index == 7 && e.clickCount >= 2) { window.style.display = DisplayStyle.Flex; window.BringToFront(); }
                else if (index == 2 && e.clickCount >= 2) explorer.Open();
                else if (index == 3 && e.clickCount >= 2) recycle.Open();
                else if (index == 6 && e.clickCount >= 2) terminal.Open();
                else if (index == 10 && e.clickCount >= 2) chat.Open();
                else if (index == 12 && e.clickCount >= 2) minesweeper.Open();
                else if (index == 13 && e.clickCount >= 2) solitaire.Open();
            });
            desktop.Add(tile);
        }
        VisualElement windows = new VisualElement();
        windows.pickingMode = PickingMode.Ignore;
        windows.style.position = UnityEngine.UIElements.Position.Absolute;
        windows.style.left = 0;
        windows.style.right = 0;
        windows.style.top = 0;
        windows.style.bottom = 40;
        root.Add(windows);
        windows.Add(window);
        windows.Add(minesweeper.Window);
        windows.Add(solitaire.Window);
        windows.Add(recycle.Window);
        windows.Add(explorer.Window);
        windows.Add(terminal.Window);
        windows.Add(chat.Window);
        VisualElement taskbar = new VisualElement();
        taskbar.style.height = 40;
        taskbar.style.flexShrink = 0;
        taskbar.style.backgroundColor = (Color)new Color32(38, 50, 64, 255);
        taskbar.style.flexDirection = FlexDirection.Row;
        foreach (string name in new[] { "viewports", "folder-filled", "world" })
        {
            Image icon = new Image();
            icon.image = Texture(name);
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.style.width = 26;
            icon.style.height = 26;
            icon.style.marginLeft = 14;
            icon.style.marginTop = 7;
            if (name == "folder-filled") icon.RegisterCallback<ClickEvent>(delegate(ClickEvent e) { if (e.button == 0) explorer.Open(); });
            taskbar.Add(icon);
        }
        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        taskbar.Add(spacer);
        money = new Label();
        money.style.color = Color.white;
        money.style.unityTextAlign = TextAnchor.MiddleRight;
        money.style.fontSize = 11;
        money.style.marginRight = 16;
        taskbar.Add(money);
        clock = new Label();
        clock.style.color = Color.white;
        clock.style.unityTextAlign = TextAnchor.MiddleRight;
        clock.style.fontSize = 11;
        clock.style.marginRight = 8;
        taskbar.Add(clock);
        root.Add(taskbar);
        UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(delegate { if (notepad.CanClose()) ComputerApps.Close(storage == null ? "os.toolkit-prototype" : "os.toolkit-os"); });
        close.text = "Close";
        close.style.position = UnityEngine.UIElements.Position.Absolute;
        close.style.right = 16;
        close.style.top = 16;
        close.style.width = 120;
        close.style.height = 40;
        root.Add(close);
    }

    private void Update()
    {
        if (clock == null || money == null || Time.unscaledTime < nextStatusUpdate) return;
        nextStatusUpdate = Time.unscaledTime + 1f;
        if (WorldClock.IsAvailable)
        {
            WorldTime now = WorldClock.Now;
            clock.text = string.Format("{0:00}:{1:00}\n{2}.1.1843", now.Hour, now.Minute, now.CalendarDay);
        }
        else clock.text = "";
        if (Cash.IsAvailable)
            money.text = string.Format("{0:N0} OC\n{1:N2} RM", (int)Cash.Balance(MoneyCurrency.OC), Cash.Balance(MoneyCurrency.RM));
        else money.text = "";
    }

    private Texture2D Texture(string name)
    {
        using (Stream stream = typeof(ToolkitPrototype).Assembly.GetManifestResourceStream("desktop-icons/" + name + ".png"))
        {
            if (stream == null) return null;
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, buffer.ToArray())) { Destroy(texture); return null; }
                textures.Add(texture);
                return texture;
            }
        }
    }

    private void OnDestroy()
    {
        if (explorer != null) explorer.Release();
        foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
    }
}

}
