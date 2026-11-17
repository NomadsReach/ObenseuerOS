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
    private static readonly string[] Names = { "Stalburg Mail", "Stalburg Web", "Explorer", "Recycle Bin", "Control Panel", "Stal-Ex", "Command Prompt", "Notepad", "Stalburg Bank", "Stalburg Times", "TOR-Turnip Chat v0.3.9", "Shop", "Minesweeper", "Solitaire", "PC Master Guard", "TurvaPro 2000" };
    private static readonly string[] Ids = { "mail", "browser", "files", "recycle", "settings", "exchange", "terminal", "notes", "bank", "news", "chat", "shop", "minesweeper", "solitaire", "pcguard", "turvapro" };
    private static readonly string[] Icons = { "mail", "world", "folder-filled", "trash", "settings", "chart-line", "terminal-2", "notes", "building-bank", "news", "message-circle", "shopping-cart", "device-gamepad-2", "device-gamepad-2", "", "" };
    private static readonly List<ToolkitPrototype> active = new List<ToolkitPrototype>();
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private readonly Dictionary<string, Texture2D> iconTextures = new Dictionary<string, Texture2D>();
    private ToolkitModWindows modWindows;
    private ToolkitNotifications notifications;
    private VisualElement selected;
    private NotepadStorage storage;
    private ToolkitExplorer explorer;
    private ToolkitSettings settings;
    private ToolkitNews news;
    private ToolkitLottery lottery;
    private ToolkitEye eye;
    private ToolkitWiki wiki;
    private ToolkitVirusFx virusFx;
    private Label clock;
    private Label money;
    private float nextStatusUpdate;
    private float nextWallpaperUpdate;

    internal static bool Visible
    {
        get
        {
            foreach (ToolkitPrototype prototype in active)
            {
                if (prototype == null || !prototype.isActiveAndEnabled || !prototype.gameObject.activeInHierarchy) continue;
                OsNotepadStorage current = prototype.storage as OsNotepadStorage;
                if (current == null) continue;
                try { current.CheckSession(); return true; }
                catch (Exception) { }
            }
            return false;
        }
    }

    private bool HasSession()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return false;
        OsNotepadStorage current = storage as OsNotepadStorage;
        if (current == null) return false;
        try { current.CheckSession(); return true; }
        catch (Exception) { return false; }
    }

    internal static bool OpenNative(OsApp app)
    {
        foreach (ToolkitPrototype prototype in new List<ToolkitPrototype>(active))
        {
            if (prototype == null || prototype.modWindows == null) continue;
            prototype.modWindows.Reconcile();
            if (prototype.modWindows.Has(app) && prototype.HasSession()) return prototype.modWindows.Open(app);
        }
        for (int i = active.Count - 1; i >= 0; i--)
        {
            ToolkitPrototype prototype = active[i];
            if (prototype != null && prototype.modWindows != null && prototype.HasSession()) return prototype.modWindows.Open(app);
        }
        return false;
    }

    internal static bool LaunchRegistered(OsApp app)
    {
        if (!Visible || app == null || !Os.Apps.Contains(app) || app.ModId == Os.ModId || app.Site || !VirusAv.Shows(app.Id)) return false;
        if (app.BuildUi != null) return OpenNative(app);
        if (app.OnOpen == null) return false;
        Os.Opened(app.Id);
        return true;
    }

    internal static bool EmitNative(OsApp app, string name, string json)
    {
        if (app == null || app.OnEvent == null || !Os.Apps.Contains(app)) return false;
        return ToolkitNativeEvents.Send(new List<ToolkitPrototype>(active),
            delegate(ToolkitPrototype prototype) { return prototype != null && prototype.modWindows != null && prototype.HasSession() && prototype.modWindows.Has(app); },
            app.OnEvent, name, json);
    }

    internal static void CloseNative(OsApp app)
    {
        foreach (ToolkitPrototype prototype in new List<ToolkitPrototype>(active))
            if (prototype != null && prototype.modWindows != null) prototype.modWindows.Close(app, true);
    }

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
        if (storage is OsNotepadStorage)
        {
            if (!active.Contains(prototype)) active.Add(prototype);
            Os.SyncWindow();
        }
    }

    private void Build()
    {
        VisualElement root = ToolkitUi.CreateOverlay(transform);
        root.style.flexGrow = 1;
        root.style.backgroundColor = (Color)new Color32(45, 95, 139, 255);
        Image wallpaper = new Image();
        wallpaper.pickingMode = PickingMode.Ignore;
        wallpaper.scaleMode = ScaleMode.ScaleAndCrop;
        wallpaper.style.position = UnityEngine.UIElements.Position.Absolute;
        wallpaper.style.left = 0; wallpaper.style.right = 0; wallpaper.style.top = 0; wallpaper.style.bottom = 40;
        root.Add(wallpaper);
        VisualElement virusWall = new VisualElement(); virusWall.pickingMode = PickingMode.Ignore; virusWall.style.position = Position.Absolute;
        virusWall.style.left = 0; virusWall.style.right = 0; virusWall.style.top = 0; virusWall.style.bottom = 40; virusWall.style.display = DisplayStyle.None;
        virusWall.style.flexDirection = FlexDirection.Column; virusWall.style.overflow = Overflow.Hidden;
        for (int i = 0; i < 12; i++)
        {
            Label line = new Label("MOCA-COLA ICE COLD  *  FREE CRATE  *  CLAIM NOW!!!"); line.pickingMode = PickingMode.Ignore; line.style.flexGrow = 1; line.style.fontSize = 34; line.style.unityFontStyleAndWeight = FontStyle.Bold;
            line.style.color = i % 2 == 0 ? Color.white : Color.yellow; line.style.backgroundColor = i % 3 == 0 ? Color.red : i % 3 == 1 ? Color.yellow : (Color)new Color32(0, 136, 255, 255); virusWall.Add(line);
        }
        root.Add(virusWall);
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
        settings = new ToolkitSettings(wallpaper, osStorage != null);
        ToolkitChat chat = new ToolkitChat(osStorage != null, osStorage == null ? (Action)null : osStorage.CheckSession);
        ToolkitMail mail = new ToolkitMail(new MailClient(osStorage == null ? (Func<Newtonsoft.Json.Linq.JObject, Newtonsoft.Json.Linq.JObject>)null : OsBridge.Reply, osStorage == null ? (Action)null : osStorage.CheckSession), osStorage == null ? new MailDraft() : MailDrafts.ForSession(osStorage.SessionId));
        ToolkitBank bank = new ToolkitBank(osStorage == null ? new TransactionClient("bank", null, null) : TransactionClients.ForSession("bank", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession));
        ToolkitExchange exchange = new ToolkitExchange(osStorage == null ? new TransactionClient("exchange", null, null) : TransactionClients.ForSession("exchange", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession));
        ToolkitShop shop = new ToolkitShop(osStorage == null ? new TransactionClient("shop", null, null) : TransactionClients.ForSession("shop", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession));
        ToolkitJobs jobs = null;
        lottery = new ToolkitLottery(osStorage == null ? new TransactionClient("lotto", null, null) : TransactionClients.ForSession("lotto", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? (Action)null : osStorage.CheckSession);
        ToolkitBets bets = new ToolkitBets(osStorage == null ? new TransactionClient("bets", null, null) : TransactionClients.ForSession("bets", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? (Action)null : osStorage.CheckSession);
        news = new ToolkitNews(new NewsClient(osStorage == null ? (Func<Newtonsoft.Json.Linq.JObject, Newtonsoft.Json.Linq.JObject>)null : OsBridge.Reply, osStorage == null ? (Action)null : osStorage.CheckSession));
        ToolkitTerminal terminal = null;
        ToolkitBrowser browser = null;
        ToolkitSecurity security = null;
        ToolkitTurva turva = null;
        ToolkitShady shady = null;
        wiki = new ToolkitWiki(WikiBridge.BundledPages, WikiBridge.LegacyPages);
        Func<string, bool> installed = delegate(string id)
        {
            if (osStorage == null) return false;
            try { osStorage.CheckSession(); return VirusAv.Shows(id); }
            catch (Exception) { return false; }
        };
        Func<int, bool> launch = delegate(int index)
        {
            switch (index)
            {
                case 0: mail.Open(); return true;
                case 1: browser.Open(); return true;
                case 2: explorer.Open(); return true;
                case 3: recycle.Open(); return true;
                case 4: settings.Open(); return true;
                case 5: exchange.Open(); return true;
                case 6: terminal.Open(); return true;
                case 7: notepad.Window.style.display = DisplayStyle.Flex; notepad.Window.BringToFront(); return true;
                case 8: bank.Open(); return true;
                case 9: news.Open(); return true;
                case 10: chat.Open(); return true;
                case 11: shop.Open(); return true;
                case 12: minesweeper.Open(); return true;
                case 13: solitaire.Open(); return true;
                case 14: if (!installed("pcguard")) return false; security.OpenGuard(); return true;
                case 15: if (!installed("turvapro")) return false; turva.OpenProgram(); return true;
                case 16: bets.Open(); return true;
                case 17: eye.Open(); return true;
                case 18: security.OpenStore(); return true;
                case 19: jobs.Open(); return true;
                case 20: lottery.Open(); return true;
                case 21: turva.OpenSite(); return true;
                case 22: shady.Open(0); return true;
                case 23: shady.Open(1); return true;
                case 24: shady.Open(2); return true;
                case 25: wiki.Open(); return true;
                default: return false;
            }
        };
        browser = new ToolkitBrowser(storage, launch, osStorage == null ? (Action)null : osStorage.CheckSession, osStorage == null ? null : new TransactionClient("browser", OsBridge.Reply, osStorage.CheckSession));
        security = new ToolkitSecurity(osStorage == null ? new TransactionClient("pcmaster", null, null) : TransactionClients.ForSession("pcmaster", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? new TransactionClient("pcguard", null, null) : TransactionClients.ForSession("pcguard", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? (Action)null : osStorage.CheckSession);
        turva = new ToolkitTurva(osStorage == null ? new TransactionClient("turvasite", null, null) : TransactionClients.ForSession("turvasite", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? new TransactionClient("turvapro", null, null) : TransactionClients.ForSession("turvapro", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? (Action)null : osStorage.CheckSession);
        shady = new ToolkitShady(osStorage == null ? new TransactionClient("freerm", null, null) : TransactionClients.ForSession("freerm", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? new TransactionClient("warez", null, null) : TransactionClients.ForSession("warez", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? new TransactionClient("voittaja", null, null) : TransactionClients.ForSession("voittaja", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession), osStorage == null ? (Action)null : osStorage.CheckSession, browser.Navigate);
        eye = new ToolkitEye(osStorage == null ? (Func<EyeState>)(() => new EyeState(storage)) : () => EyeStates.ForSession(osStorage.SessionId, storage), osStorage == null ? (Action)null : osStorage.CheckSession, OsView.UserName, new NewsClient(osStorage == null ? (Func<Newtonsoft.Json.Linq.JObject, Newtonsoft.Json.Linq.JObject>)null : OsBridge.Reply, osStorage == null ? (Action)null : osStorage.CheckSession), launch);
        jobs = new ToolkitJobs(new TransactionClient("jobs", osStorage == null ? (Func<Newtonsoft.Json.Linq.JObject, Newtonsoft.Json.Linq.JObject>)null : OsBridge.Reply, osStorage == null ? (Action)null : osStorage.CheckSession), osStorage == null ? (Action)null : osStorage.CheckSession, launch);
        terminal = new ToolkitTerminal(storage, storage as ExplorerStorage, OsView.UserName,
            delegate { if (!WorldClock.IsAvailable) return "The game clock is not ready."; WorldTime now = WorldClock.Now; return string.Format("The current date is: {0}.1.1843, {1}", now.CalendarDay, now.Weekday); },
            delegate { if (!WorldClock.IsAvailable) return "The game clock is not ready."; WorldTime now = WorldClock.Now; return string.Format("The current time is: {0:00}:{1:00}", now.Hour, now.Minute); },
            delegate { if (!Cash.IsAvailable) return "The game clock is not ready."; return string.Format("{0:N0} OC   {1:N2} RM", (int)Cash.Balance(MoneyCurrency.OC), Cash.Balance(MoneyCurrency.RM)); },
            delegate(string app)
            {
                for (int i = 0; i < Ids.Length; i++)
                    if (string.Equals(app, Ids[i], StringComparison.OrdinalIgnoreCase) || string.Equals(app, Names[i], StringComparison.OrdinalIgnoreCase)) return launch(i);
                if (!HasSession()) return false;
                return LaunchRegistered(LauncherCatalog.Find(LauncherCatalog.Select(Os.Apps, Os.ModId, VirusAv.Shows), app));
            },
            delegate(string path) { if (path == null) { notepad.Window.style.display = DisplayStyle.Flex; notepad.Window.BringToFront(); } else notepad.OpenPath(path); if (explorer != null) explorer.RefreshIfOpen(); });
        Func<bool> canClose = delegate { return notepad.CanClose() && mail.CanClose() && (modWindows == null || modWindows.CanCloseAll()); };
        ComputerApps.SetCloseGuard(transform.parent, canClose);
        VisualElement window = notepad.Window;
        VisualElement guardTile = null, turvaTile = null;
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
            if (i >= 14)
            {
                Label letter = new Label(Names[i].Substring(0, 1)); letter.style.color = Color.white; letter.style.fontSize = 36; letter.style.height = 52; letter.style.unityTextAlign = TextAnchor.MiddleCenter; tile.Add(letter);
                if (i == 14) guardTile = tile; else turvaTile = tile; tile.style.display = DisplayStyle.None;
            }
            else tile.Add(icon);
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
                if (e.clickCount >= 2) launch(index);
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
        modWindows = new ToolkitModWindows(windows, HasSession);
        windows.Add(window);
        windows.Add(minesweeper.Window);
        windows.Add(solitaire.Window);
        windows.Add(recycle.Window);
        windows.Add(explorer.Window);
        windows.Add(terminal.Window);
        windows.Add(chat.Window);
        windows.Add(settings.Window);
        windows.Add(mail.Window);
        windows.Add(bank.Window);
        windows.Add(exchange.Window);
        windows.Add(shop.Window);
        windows.Add(news.Window);
        windows.Add(browser.Window);
        windows.Add(security.Window);
        windows.Add(turva.Window);
        windows.Add(shady.Window);
        windows.Add(wiki.Window);
        windows.Add(jobs.Window);
        windows.Add(lottery.Window);
        windows.Add(eye.Window);
        windows.Add(bets.Window);
        ToolkitStart start = new ToolkitStart(Names, Icons, Texture, launch, delegate { if (canClose()) ComputerApps.Close(storage == null ? "os.toolkit-prototype" : "os.toolkit-os"); }, delegate(int index) { return index < 14 || installed(Ids[index]); });
        VisualElement pinned = new VisualElement(); pinned.style.flexDirection = FlexDirection.Row;
        Dictionary<OsApp, Label> pinnedBadges = new Dictionary<OsApp, Label>();
        Action updateBadges = delegate { foreach (KeyValuePair<OsApp, Label> entry in pinnedBadges) entry.Value.text = entry.Key.Badge > 0 ? entry.Key.Badge.ToString() : ""; };
        List<OsApp> launchers = new List<OsApp>();
        List<VisualElement> launcherTiles = new List<VisualElement>();
        Func<OsApp, bool> openLauncher = delegate(OsApp app)
        {
            if (osStorage == null) return false;
            try { osStorage.CheckSession(); }
            catch (Exception) { return false; }
            return LaunchRegistered(app);
        };
        Action updateLaunchers = delegate
        {
            List<OsApp> current = new List<OsApp>();
            if (osStorage != null)
            {
                try { osStorage.CheckSession(); current = LauncherCatalog.Select(Os.Apps, Os.ModId, VirusAv.Shows); }
                catch (Exception) { }
            }
            if (LauncherCatalog.Same(launchers, current)) { updateBadges(); return; }
            foreach (VisualElement old in launcherTiles) { if (selected == old) selected = null; old.RemoveFromHierarchy(); }
            launcherTiles.Clear(); pinned.Clear(); pinnedBadges.Clear(); launchers = current;
            foreach (OsApp entry in launchers)
            {
                OsApp app = entry;
                if (entry.Pinned)
                {
                    UnityEngine.UIElements.Button pin = new UnityEngine.UIElements.Button(delegate { openLauncher(app); });
                    pin.tooltip = entry.Title; pin.style.width = 40; pin.style.height = 36; pin.style.marginTop = 2;
                    pin.style.paddingLeft = 2; pin.style.paddingRight = 2; pin.style.flexDirection = FlexDirection.Row;
                    pin.Add(ModIcon(entry, 22));
                    Label badge = new Label(); badge.style.color = Color.white; badge.style.backgroundColor = Color.red; badge.style.fontSize = 9;
                    pin.Add(badge); pinnedBadges.Add(entry, badge); pinned.Add(pin);
                }
                if (!entry.Desktop) continue;
                VisualElement tile = new VisualElement(); tile.style.width = 88; tile.style.height = 104; tile.style.alignItems = Align.Center;
                VisualElement icon = ModIcon(entry, 48); icon.style.marginTop = 4; tile.Add(icon);
                Label text = new Label(entry.Title); text.style.color = Color.white; text.style.fontSize = 12; text.style.unityTextAlign = TextAnchor.UpperCenter; text.style.whiteSpace = WhiteSpace.Normal; text.style.width = 80; text.style.marginTop = 4; tile.Add(text);
                tile.RegisterCallback<ClickEvent>(delegate(ClickEvent e)
                {
                    if (e.button != 0) return;
                    if (selected != null) selected.style.backgroundColor = Color.clear;
                    selected = tile; selected.style.backgroundColor = (Color)new Color32(120, 170, 230, 77);
                    if (e.clickCount >= 2) openLauncher(app);
                });
                launcherTiles.Add(tile); desktop.Add(tile);
            }
            start.SetLaunchers(launchers, openLauncher, ModIcon);
            updateBadges();
        };
        Action updateGuard = delegate { guardTile.style.display = installed("pcguard") ? DisplayStyle.Flex : DisplayStyle.None; turvaTile.style.display = installed("turvapro") ? DisplayStyle.Flex : DisplayStyle.None; updateLaunchers(); start.Refresh(); };
        updateGuard(); desktop.schedule.Execute(updateGuard).Every(1000);
        VisualElement taskbar = new VisualElement();
        taskbar.style.height = 40;
        taskbar.style.flexShrink = 0;
        taskbar.style.backgroundColor = (Color)new Color32(38, 50, 64, 255);
        taskbar.style.flexDirection = FlexDirection.Row;
        Image startIcon = null;
        foreach (string name in new[] { "viewports", "folder-filled", "world" })
        {
            Image icon = new Image();
            icon.image = Texture(name);
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.style.width = 26;
            icon.style.height = 26;
            icon.style.marginLeft = 14;
            icon.style.marginTop = 7;
            if (name == "viewports")
            {
                startIcon = icon;
                icon.RegisterCallback<ClickEvent>(delegate(ClickEvent e) { if (e.button == 0) start.Toggle(); });
            }
            if (name == "folder-filled") icon.RegisterCallback<ClickEvent>(delegate(ClickEvent e) { if (e.button == 0) explorer.Open(); });
            if (name == "world") icon.RegisterCallback<ClickEvent>(delegate(ClickEvent e) { if (e.button == 0) browser.Open(); });
            taskbar.Add(icon);
        }
        taskbar.Add(pinned);
        VisualElement spacer = new VisualElement();
        spacer.style.flexGrow = 1;
        taskbar.Add(spacer);
        notifications = new ToolkitNotifications(root);
        taskbar.Add(notifications.Tray);
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
        root.Add(start.Menu);
        VisualElement virusLayer = new VisualElement(); virusLayer.pickingMode = PickingMode.Ignore; virusLayer.style.position = UnityEngine.UIElements.Position.Absolute; virusLayer.style.left = 0; virusLayer.style.right = 0; virusLayer.style.top = 0; virusLayer.style.bottom = 40; root.Add(virusLayer);
        virusFx = new ToolkitVirusFx(virusLayer, desktop, virusWall, delegate { if (!isActiveAndEnabled || !gameObject.activeInHierarchy || osStorage == null) return false; try { osStorage.CheckSession(); return true; } catch (Exception) { return false; } }, browser.Navigate, delegate(int index) { launch(index); }, delegate { return installed("turvapro"); }, turva.OpenProgram, osStorage == null ? new TransactionClient("virusfx", null, null) : TransactionClients.ForSession("virusfx", osStorage.SessionId, OsBridge.Reply, osStorage.CheckSession));
        root.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent e)
        {
            VisualElement target = e.target as VisualElement;
            if (target == null || target == start.Menu || start.Menu.Contains(target) || target == startIcon || (startIcon != null && startIcon.Contains(target))) return;
            start.Hide();
        }, TrickleDown.TrickleDown);
        UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(delegate { if (canClose()) ComputerApps.Close(storage == null ? "os.toolkit-prototype" : "os.toolkit-os"); });
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
        if (settings != null && Time.unscaledTime >= nextWallpaperUpdate)
        {
            nextWallpaperUpdate = Time.unscaledTime + 2f;
            settings.Refresh();
        }
        if (clock == null || money == null || Time.unscaledTime < nextStatusUpdate) return;
        nextStatusUpdate = Time.unscaledTime + 1f;
        if (storage is OsNotepadStorage) Os.SyncWindow();
        if (modWindows != null) modWindows.Reconcile();
        if (notifications != null)
        {
            if (HasSession()) foreach (OsNotice notice in OsView.TakeNotices()) notifications.Add(notice);
            else notifications.Clear();
        }
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

    private VisualElement ModIcon(OsApp app, int size)
    {
        Texture2D texture = string.IsNullOrEmpty(app.Icon) ? null : Texture(app.Icon);
        if (texture != null)
        {
            Image image = new Image(); image.image = texture; image.scaleMode = ScaleMode.ScaleToFit;
            image.style.width = size; image.style.height = size; return image;
        }
        Label label = new Label(app.Title.Substring(0, 1)); label.enableRichText = false; label.style.color = Color.white;
        label.style.fontSize = size * 3 / 4; label.style.width = size; label.style.height = size;
        label.style.unityTextAlign = TextAnchor.MiddleCenter; return label;
    }

    private Texture2D Texture(string name)
    {
        Texture2D cached;
        if (iconTextures.TryGetValue(name, out cached)) return cached;
        using (Stream stream = typeof(ToolkitPrototype).Assembly.GetManifestResourceStream("desktop-icons/" + name + ".png"))
        {
            if (stream == null) return null;
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, buffer.ToArray())) { Destroy(texture); return null; }
                textures.Add(texture);
                iconTextures[name] = texture;
                return texture;
            }
        }
    }

    private void OnEnable()
    {
        if (!(storage is OsNotepadStorage) || modWindows == null) return;
        if (!active.Contains(this)) active.Add(this);
        Os.SyncWindow();
    }

    private void OnDisable()
    {
        active.Remove(this);
        if (modWindows != null) modWindows.CloseAll();
        if (notifications != null) notifications.Clear();
        if (storage is OsNotepadStorage) Os.SyncWindow();
    }

    private void OnDestroy()
    {
        active.Remove(this);
        if (storage is OsNotepadStorage) Os.SyncWindow();
        if (virusFx != null) virusFx.Release();
        if (wiki != null) wiki.Release();
        if (news != null) news.Release();
        if (lottery != null) lottery.Release();
        if (eye != null) eye.Release();
        if (settings != null) settings.Release();
        if (explorer != null) explorer.Release();
        foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
    }
}

}
