using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    public sealed class OsAppOptions
    {
        public string Title;
        public string Icon;
        public string Folder;
        public string Page = "index.html";
        public int Width;
        public int Height;
        public bool Desktop = true;
        public bool Pinned;
        public Action OnOpen;
    }

    public sealed class OsAppArgs
    {
        public string AppId { get; private set; }
        public string ModId { get; private set; }

        internal OsAppArgs(string appId, string modId)
        {
            AppId = appId;
            ModId = modId;
        }
    }

    public sealed class OsSiteOptions
    {
        public string Host;
        public string Title;
        public string Description;
        public string[] Keywords;
        public string Folder;
        public string Page = "index.html";
    }

    public static partial class Os
    {
        public static string ApiVersion
        {
            get
            {
                return "1.0.0";
            }
        }

        private static Action<OsAppArgs> appOpened;
        private static Action<OsAppArgs> appClosed;
        private static Action windowOpened;
        private static Action windowClosed;
        private static readonly Dictionary<string, string> OpenApps = new Dictionary<string, string>();
        private static readonly Dictionary<string, RateLimit> ModLimits = new Dictionary<string, RateLimit>();

        public static event Action<OsAppArgs> AppOpened
        {
            add
            {
                appOpened += value;
            }
            remove
            {
                appOpened -= value;
            }
        }

        public static event Action<OsAppArgs> AppClosed
        {
            add
            {
                appClosed += value;
            }
            remove
            {
                appClosed -= value;
            }
        }

        public static event Action WindowOpened
        {
            add
            {
                windowOpened += value;
            }
            remove
            {
                windowOpened -= value;
            }
        }

        public static event Action WindowClosed
        {
            add
            {
                windowClosed += value;
            }
            remove
            {
                windowClosed -= value;
            }
        }

        public static bool IsOpen
        {
            get
            {
                return OsView.Visible;
            }
        }

        public static string UserName
        {
            get
            {
                try
                {
                    if (!OsBridge.WorldReady()) return null;
                    string name = OsView.UserName();
                    return name.Length == 0 ? null : name;
                }
                catch (Exception e)
                {
                    Crash("UserName", e);
                    return null;
                }
            }
        }

        public static bool RegisterApp(string modId, string appId, OsAppOptions options)
        {
            try
            {
                if (!Accept("RegisterApp", modId)) return false;
                if (options == null) return Refuse("RegisterApp", modId, false, "options must not be null");
                if ((options.Folder == null) == (options.OnOpen == null)) return Refuse("RegisterApp", modId, false, "set exactly one of Folder (a page app) or OnOpen (a launcher app)");
                string title = OsScope.CleanTitle(options.Title);
                if (!CheckNew("RegisterApp", modId, appId, title)) return false;
                if (CountOf(modId, false) >= OsScope.MaxAppsPerMod) return Refuse("RegisterApp", modId, false, "a mod may register 8 apps");
                if (options.Width < 0 || options.Height < 0) return Refuse("RegisterApp", modId, false, "width and height are 0 for the default or a size in pixels");
                OsApp app = new OsApp(modId, appId, title, null, options.OnOpen);
                app.Desktop = options.Desktop;
                app.Pinned = options.Pinned;
                app.Width = Size(options.Width, 360);
                app.Height = Size(options.Height, 240);
                if (options.Folder != null && !SetPage(app, "RegisterApp", modId, options.Folder, options.Page)) return false;
                SetIcon(app, modId, options.Icon);
                Apps.Add(app);
                OsView.Map(app);
                OsView.Refresh();
                return true;
            }
            catch (Exception e)
            {
                Crash("RegisterApp", e);
                return false;
            }
        }

        public static bool Handle(string modId, string appId, string method, Func<string, string> handler)
        {
            try
            {
                if (!Accept("Handle", modId)) return false;
                OsApp app = OwnedApp("Handle", modId, appId);
                if (app == null) return false;
                if (!OsScope.ValidName(method)) return Refuse("Handle", modId, false, "method must be 1 to 64 letters, digits, '.', '_' or '-' and not start with 'os.'");
                if (handler == null) return Refuse("Handle", modId, false, "handler must not be null");
                if (app.Methods >= OsScope.MaxMethodsPerApp) return Refuse("Handle", modId, false, "an app may have 64 methods");
                if (!OsBridge.Add(appId, method, handler)) return false;
                app.Methods++;
                return true;
            }
            catch (Exception e)
            {
                Crash("Handle", e);
                return false;
            }
        }

        public static bool Emit(string modId, string appId, string name, string json)
        {
            try
            {
                if (!Accept("Emit", modId)) return false;
                if (OwnedApp("Emit", modId, appId) == null) return false;
                if (!OsScope.ValidName(name)) return Refuse("Emit", modId, false, "name must be 1 to 64 letters, digits, '.', '_' or '-' and not start with 'os.'");
                if (!OsScope.FitsArgs(json)) return Refuse("Emit", modId, false, "json must be text of at most 12 KB");
                JToken data;
                try
                {
                    data = JToken.Parse(json);
                }
                catch (Exception)
                {
                    return Refuse("Emit", modId, false, "json must be a JSON value");
                }
                if (!OsScope.Allow(ModLimits, "emit " + modId, OsScope.EmitsPerSecond, 1000, OsScope.NowMs())) return Refuse("Emit", modId, false, "30 events a second");
                if (!OpenApps.ContainsKey(appId)) return false;
                return OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "event"), new JProperty("app", appId), new JProperty("name", name), new JProperty("data", data)));
            }
            catch (Exception e)
            {
                Crash("Emit", e);
                return false;
            }
        }

        public static bool Notify(string modId, string title, string body)
        {
            try
            {
                if (!Accept("Notify", modId)) return false;
                if (!OsScope.PlainText(title, 1, 60) || !OsScope.PlainText(body ?? "", 0, 200)) return Refuse("Notify", modId, false, "title is 1 to 60 characters and body up to 200, with no control characters");
                if (!OsScope.Allow(ModLimits, "notify " + modId, OsScope.NoticesPer10Seconds, 10000, OsScope.NowMs())) return Refuse("Notify", modId, false, "3 notices per 10 seconds");
                if (OsView.Notify(new OsNotice(modId, FromOf(modId), title, body ?? ""))) return true;
                return Refuse("Notify", modId, false, "the OS is closed and 20 notices are already queued");
            }
            catch (Exception e)
            {
                Crash("Notify", e);
                return false;
            }
        }

        public static bool SetBadge(string modId, string appId, int count)
        {
            try
            {
                if (!Accept("SetBadge", modId)) return false;
                OsApp app = OwnedApp("SetBadge", modId, appId);
                if (app == null) return false;
                if (count < 0 || count > 999) return Refuse("SetBadge", modId, false, "count must be 0 to 999");
                app.Badge = count;
                OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "badge"), new JProperty("app", appId), new JProperty("count", count)));
                return true;
            }
            catch (Exception e)
            {
                Crash("SetBadge", e);
                return false;
            }
        }

        public static OsResult OpenApp(string modId, string appId)
        {
            try
            {
                if (!Accept("OpenApp", modId)) return OsResult.Invalid;
                OsApp app = OwnedApp("OpenApp", modId, appId);
                if (app == null) return OsResult.Invalid;
                if (app.Site) return Refuse("OpenApp", modId, OsResult.Invalid, appId + " is a site, it opens in Stalburg Web");
                if (!IsAvailable) return OsResult.Unavailable;
                return OsView.OpenApp(appId);
            }
            catch (Exception e)
            {
                return Crash("OpenApp", e);
            }
        }

        public static bool RegisterSite(string modId, string siteId, OsSiteOptions options)
        {
            try
            {
                if (!Accept("RegisterSite", modId)) return false;
                if (options == null) return Refuse("RegisterSite", modId, false, "options must not be null");
                if (!OsScope.ValidSiteHost(options.Host)) return Refuse("RegisterSite", modId, false, "host must be a lower case name ending .ts or .com that is not a built-in, stalburg or reserved host");
                string plain = OsScope.PlainHost(options.Host);
                if (Apps.Exists(x => x.Site && OsScope.PlainHost(x.Host) == plain)) return Refuse("RegisterSite", modId, false, "host " + Shown(options.Host) + " is already used");
                if (!OsScope.ValidSiteText(options.Description, options.Keywords)) return Refuse("RegisterSite", modId, false, "description is up to 120 characters, and up to 12 keywords of up to 24 characters");
                if (CountOf(modId, true) >= OsScope.MaxSitesPerMod) return Refuse("RegisterSite", modId, false, "a mod may register 4 sites");
                string title = OsScope.CleanTitle(options.Title);
                if (!CheckNew("RegisterSite", modId, siteId, title)) return false;
                OsApp site = new OsApp(modId, siteId, title, null, null);
                site.Site = true;
                site.Host = options.Host;
                site.Description = options.Description ?? "";
                site.Keywords = options.Keywords ?? new string[0];
                if (!SetPage(site, "RegisterSite", modId, options.Folder, options.Page)) return false;
                Apps.Add(site);
                OsView.Map(site);
                OsView.Refresh();
                return true;
            }
            catch (Exception e)
            {
                Crash("RegisterSite", e);
                return false;
            }
        }

        public static bool UnregisterSite(string modId, string siteId)
        {
            try
            {
                if (!Accept("UnregisterSite", modId)) return false;
                OsApp site = OwnedApp("UnregisterSite", modId, siteId);
                if (site == null) return false;
                if (!site.Site) return Refuse("UnregisterSite", modId, false, siteId + " is an app, use UnregisterApp");
                Drop(site);
                OsView.Refresh();
                return true;
            }
            catch (Exception e)
            {
                Crash("UnregisterSite", e);
                return false;
            }
        }

        internal static void RaiseWindow(bool opened)
        {
            if (opened) Raise(windowOpened, "WindowOpened");
            else Raise(windowClosed, "WindowClosed");
        }

        internal static void Closed(string appId)
        {
            string owner;
            if (appId == null || !OpenApps.TryGetValue(appId, out owner)) return;
            OpenApps.Remove(appId);
            Raise(appClosed, new OsAppArgs(appId, owner), "AppClosed");
        }

        internal static void SceneReset(bool mainMenu)
        {
            foreach (string id in new List<string>(OpenApps.Keys)) Closed(id);
            if (!mainMenu) return;
            foreach (OsApp app in Apps) app.Badge = 0;
        }

        private static void TrackOpened(OsApp app)
        {
            if (OpenApps.ContainsKey(app.Id)) return;
            string owner = app.ModId == ModId ? null : app.ModId;
            OpenApps.Add(app.Id, owner);
            Raise(appOpened, new OsAppArgs(app.Id, owner), "AppOpened");
        }

        private static OsApp OwnedApp(string api, string modId, string appId)
        {
            OsApp app = Apps.Find(x => x.Id == appId);
            if (app == null) Refuse(api, modId, false, "no app " + Shown(appId));
            else if (!OsScope.MayTouch(app.ModId, modId)) Refuse(api, modId, false, "app " + appId + " belongs to another mod");
            else return app;
            return null;
        }

        private static int CountOf(string modId, bool site)
        {
            return Apps.FindAll(x => x.ModId == modId && x.Site == site).Count;
        }

        private static string FromOf(string modId)
        {
            OsApp first = Apps.Find(x => x.ModId == modId);
            return first == null ? modId : first.Title;
        }

        private static int Size(int value, int min)
        {
            return value == 0 ? 0 : Math.Max(min, value);
        }

        private static bool SetPage(OsApp app, string api, string modId, string folder, string page)
        {
            string dir = ModFolder(folder);
            if (dir == null) return Refuse(api, modId, false, "folder must be an existing folder under the plugins folder, and not the OS folder");
            if (!OsScope.ValidRelative(page) || !File.Exists(Path.Combine(dir, page))) return Refuse(api, modId, false, "page must be a file inside the folder");
            app.Folder = dir;
            app.Page = page;
            app.Label = OsScope.Label(app.Id);
            return true;
        }

        private static void SetIcon(OsApp app, string modId, string icon)
        {
            if (icon == null) return;
            if (IconExists(icon)) app.Icon = icon;
            else if (app.Folder != null && OsScope.IsIconFile(icon) && IconFits(Path.Combine(app.Folder, icon))) app.IconFile = icon;
            else Refuse("RegisterApp", modId, false, "unknown icon " + Shown(icon) + ", the app shows its first letter");
        }

        private static bool IconFits(string path)
        {
            return File.Exists(path) && new FileInfo(path).Length <= OsScope.MaxIconBytes;
        }

        private static string ModFolder(string folder)
        {
            if (folder == null || !Path.IsPathRooted(folder)) return null;
            try
            {
                string sep = Path.DirectorySeparatorChar.ToString();
                string full = Path.GetFullPath(folder);
                string probe = full.TrimEnd(Path.DirectorySeparatorChar) + sep;
                string plugins = Path.GetFullPath(Paths.PluginPath).TrimEnd(Path.DirectorySeparatorChar) + sep;
                string own = Path.GetFullPath(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)).TrimEnd(Path.DirectorySeparatorChar) + sep;
                if (!OsScope.InsideMods(probe, plugins, own)) return null;
                return Directory.Exists(full) ? full : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
