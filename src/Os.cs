using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ObenseuerCore;

namespace ObenseuerOS
{
    public static partial class Os
    {
        internal const string ModId = "nomad.obenseuer.os";
        internal const string AppId = "os";
        private static readonly Regex ValidAppId = new Regex(@"\A[a-z0-9._-]{1,64}\z");
        private static readonly Regex ValidTitle = new Regex(@"\A[^\p{Cc}]{1,40}\z");
        private static readonly CoreLog Log = CoreLog.For(ModId);
        internal static readonly List<OsApp> Apps = new List<OsApp>();

        public static bool IsAvailable
        {
            get
            {
                return ComputerApps.IsAvailable && OsView.Available;
            }
        }

        [Obsolete("Always refused. Use RegisterApp(modId, appId, OsAppOptions) with Folder or OnOpen set.")]
        public static bool RegisterApp(string modId, string appId, string title)
        {
            return RegisterApp(modId, appId, title, null, null);
        }

        [Obsolete("Refused when onOpen is null. Use RegisterApp(modId, appId, OsAppOptions) with Folder or OnOpen set.")]
        public static bool RegisterApp(string modId, string appId, string title, string icon, Action onOpen)
        {
            OsAppOptions options = new OsAppOptions();
            options.Title = title;
            options.Icon = icon;
            options.OnOpen = onOpen;
            return RegisterApp(modId, appId, options);
        }

        public static bool UnregisterApp(string modId, string appId)
        {
            try
            {
                if (!Accept("UnregisterApp", modId)) return false;
                OsApp app = Apps.Find(x => x.Id == appId);
                if (app == null) return Refuse("UnregisterApp", modId, false, "no app " + Shown(appId));
                if (app.ModId != modId) return Refuse("UnregisterApp", modId, false, "app " + appId + " belongs to another mod");
                if (app.Site) return Refuse("UnregisterApp", modId, false, appId + " is a site, use UnregisterSite");
                Drop(app);
                OsView.Refresh();
                return true;
            }
            catch (Exception e)
            {
                Crash("UnregisterApp", e);
                return false;
            }
        }

        internal static bool Add(string modId, string appId, string title, string icon, Action onOpen)
        {
            if (!CheckNew("RegisterApp", modId, appId, title)) return false;
            string shown = null;
            if (icon != null)
            {
                if (IconExists(icon)) shown = icon;
                else Refuse("RegisterApp", modId, false, "unknown icon " + Shown(icon) + ", the app shows its first letter");
            }
            Apps.Add(new OsApp(modId, appId, title, shown, onOpen));
            OsView.Refresh();
            return true;
        }

        internal static bool CheckNew(string api, string modId, string appId, string title)
        {
            if (string.IsNullOrEmpty(appId) || !ValidAppId.IsMatch(appId) || appId == AppId) return Refuse(api, modId, false, "appId must match [a-z0-9._-]{1,64} and not be 'os'");
            if (modId != ModId && OsScope.IsReservedAppId(appId)) return Refuse(api, modId, false, "appId is reserved for an OS app");
            if (string.IsNullOrEmpty(title) || !ValidTitle.IsMatch(title)) return Refuse(api, modId, false, "title must be 1 to 40 characters with no control characters");
            if (modId != ModId && OsScope.IsBuiltInTitle(title)) return Refuse(api, modId, false, "title is the name of an OS app");
            if (Apps.Exists(x => x.Id == appId)) return Refuse(api, modId, false, "duplicate app id " + appId);
            return true;
        }

        internal static void Opened(string appId)
        {
            OsApp app = Apps.Find(x => x.Id == appId);
            if (app == null) return;
            if (app.OnOpen == null)
            {
                TrackOpened(app);
                return;
            }
            try
            {
                app.OnOpen();
            }
            catch (Exception e)
            {
                Crash("app " + appId + " onOpen", e);
            }
        }

        internal static bool Handle(string appId, string method, Func<string, string> handler)
        {
            if (string.IsNullOrEmpty(appId)) throw new ArgumentException("appId must be a non-empty string", "appId");
            if (string.IsNullOrEmpty(method)) throw new ArgumentException("method must be a non-empty string", "method");
            if (handler == null) throw new ArgumentNullException("handler");
            if (!ValidAppId.IsMatch(appId) || OsBridge.IsBuiltIn(method))
            {
                Log.Warn("OS handler refused: " + Shown(appId) + " " + Shown(method));
                return false;
            }
            return OsBridge.Add(appId, method, handler);
        }

        internal static void RegisterBuiltIns()
        {
            Add(ModId, "mail", "Stalburg Mail", null, null);
            Add(ModId, "browser", "Stalburg Web", null, null);
            Add(ModId, "files", "Explorer", null, null);
            Add(ModId, "recycle", "Recycle Bin", null, null);
            Add(ModId, "settings", "Control Panel", null, null);
            Add(ModId, "exchange", "Stal-Ex", null, null);
            Add(ModId, "terminal", "Command Prompt", null, null);
            Add(ModId, "notes", "Notepad", null, null);
            Add(ModId, "bank", "Stalburg Bank", null, null);
            Add(ModId, "news", "Stalburg Times", null, null);
            Add(ModId, "chat", "TOR-Turnip Chat v0.3.9", null, null);
            Add(ModId, "shop", "Shop", null, null);
            Add(ModId, "minesweeper", "Minesweeper", null, null);
            Add(ModId, "solitaire", "Solitaire", null, null);
            Add(ModId, VirusAvRules.PcGuardApp, "PC Master Guard", null, null);
            Add(ModId, VirusAvRules.TurvaApp, "TurvaPro 2000", null, null);
            ShopBridge.Register();
            LottoBridge.Register();
            WikiBridge.Register();
            JobsBridge.Register();
        }

        public static bool Open()
        {
            return OsView.Show();
        }

        public static void Close()
        {
            OsView.Dismiss();
        }
    }

    internal class OsApp
    {
        internal readonly string ModId;
        internal readonly string Id;
        internal readonly string Title;
        internal string Icon;
        internal readonly Action OnOpen;
        internal string IconFile;
        internal string Folder;
        internal string Page;
        internal string Label;
        internal int Width;
        internal int Height;
        internal bool Desktop = true;
        internal bool Pinned;
        internal bool Site;
        internal string Host;
        internal string Description;
        internal string[] Keywords;
        internal int Badge;
        internal int Methods;

        internal OsApp(string modId, string id, string title, string icon, Action onOpen)
        {
            ModId = modId;
            Id = id;
            Title = title;
            Icon = icon;
            OnOpen = onOpen;
        }
    }
}
