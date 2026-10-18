using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    internal static class OsView
    {
        private const string Page = "index.html";
        private const int KeyF10 = 121;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static WebView view;
        private static bool failed;
        private static bool pageReady;
        private static int lastMinute = -1;
        private static string webFolder;
        private static bool live;
        private static string held;
        private static string heldApp;
        private static readonly NoticeQueue notices = new NoticeQueue();

        internal static bool Available
        {
            get
            {
                return WebUi.IsAvailable && !failed;
            }
        }

        internal static bool Live
        {
            get
            {
                return live;
            }
        }

        internal static bool Visible
        {
            get
            {
                return view != null && view.IsVisible;
            }
        }

        internal static bool Show()
        {
            return Show(ComputerApps.ActiveScreen);
        }

        internal static bool Show(RectTransform screen)
        {
            if (!Available) return false;
            if ((view == null || view.IsClosed) && !Create()) return false;
            bool was = Visible;
            view.PinTo(screen);
            if (!view.Show()) return false;
            Push();
            if (!was) Os.RaiseWindow(true);
            if (screen != null) Log.Debug("view.pinned", "OS pinned, projected bounds " + ProjectedBounds(screen));
            return true;
        }

        internal static void Dismiss()
        {
            OsLaptop.Close(false);
            if (view == null) return;
            bool was = view.IsVisible;
            if (was) view.Hide();
            ComputerApps.Close(Os.AppId);
            if (was) Os.RaiseWindow(false);
        }

        internal static void Reset(bool mainMenu)
        {
            Dismiss();
            Os.SceneReset(mainMenu);
            if (mainMenu) notices.Clear();
            heldApp = null;
            held = null;
            if (view == null) return;
            view.Close();
            view = null;
        }

        internal static void Refresh()
        {
            if (view != null && view.IsVisible) Push();
        }

        internal static void Map(OsApp app)
        {
            if (view == null || view.IsClosed || app.Folder == null) return;
            if (!view.MapFolder(app.Label, app.Folder)) Log.Warn("app folder not mapped: " + Os.Shown(app.Id));
        }

        internal static bool Notify(OsNotice notice)
        {
            if (Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "toast"), new JProperty("title", notice.Title), new JProperty("body", notice.Body), new JProperty("from", notice.From)))) return true;
            return notices.Add(notice);
        }

        internal static void ClearNotices(string modId)
        {
            notices.RemoveMod(modId);
        }

        internal static OsResult OpenApp(string appId)
        {
            if (view == null || !view.IsVisible || !pageReady)
            {
                heldApp = appId;
                return OsResult.Queued;
            }
            Send(new JObject(new JProperty("v", 1), new JProperty("type", "openapp"), new JProperty("app", appId)));
            return OsResult.Ok;
        }

        internal static void Tick()
        {
            if (view == null || !view.IsVisible || !pageReady) return;
            if (OsGameArt.Step()) PushGameArt();
            if (WorldClock.IsAvailable && WorldClock.Now.Minute != lastMinute) PushClock();
        }

        private static string ProjectedBounds(RectTransform rect)
        {
            Camera camera = Camera.main;
            if (camera == null) return "no camera";
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 low = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 high = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3 corner in corners)
            {
                Vector2 point = camera.WorldToScreenPoint(corner);
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            return "x " + low.x + " y " + (Screen.height - high.y) + " w " + (high.x - low.x) + " h " + (high.y - low.y) + " (screen px)";
        }

        private static bool Create()
        {
            webFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web");
            view = WebUi.Open(Os.ModId, webFolder, Page, true);
            if (view == null)
            {
                failed = true;
                Log.Error("OS unavailable: " + WebUi.LastError);
                return false;
            }
            ShopIcons.Map(view);
            pageReady = false;
            view.EscapeHides = false;
            view.Message += OnMessage;
            view.KeyPressed += OnKey;
            if (WikiBridge.SavesCopies && !view.MapFolder("wikisite", WikiBridge.Folder)) Log.Warn("wiki site folder not mapped");
            if (!view.MapFolder("sdk", Path.Combine(webFolder, "sdk"))) Log.Warn("sdk folder not mapped");
            foreach (OsApp app in Os.Apps) Map(app);
            live = false;
            try
            {
                live = AllowLiveHost(view);
            }
            catch (MissingMethodException)
            {
            }
            if (!live) Log.Warn("wiki live pages off");
            view.Dismissed += Dismiss;
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool AllowLiveHost(WebView target)
        {
            return target.AllowFrameHost(WikiTitle.Host);
        }

        internal static void OnScreenChanged(RectTransform screen)
        {
            if (view == null || !view.IsVisible || OsLaptop.Active) return;
            if (screen != null)
            {
                view.PinTo(screen);
                PushWallpaper(screen);
            }
            else Dismiss();
        }

        internal static void OnMoneyChanged(MoneyChangedArgs args)
        {
            if (view != null && view.IsVisible) PushMoney();
        }

        internal static bool Deliver(JObject message)
        {
            if (view == null || !view.IsVisible || !pageReady) return false;
            Send(message);
            return true;
        }

        internal static bool OpenFile(string path)
        {
            if (view == null || !view.IsVisible || !pageReady)
            {
                held = path;
                return false;
            }
            SendOpenFile(path);
            return true;
        }

        internal static void DropHeld()
        {
            held = null;
        }

        private static void SendOpenFile(string path)
        {
            Send(new JObject(new JProperty("v", 1), new JProperty("type", "openfile"), new JProperty("path", path)));
        }

        private static void OnKey(int vk)
        {
            if (vk == KeyF10) Dismiss();
        }

        private static void OnMessage(string json)
        {
            if (view == null) return;
            JObject m;
            string type;
            try
            {
                m = JObject.Parse(json);
                type = m.Value<string>("type");
            }
            catch (Exception)
            {
                Log.Warn("OS message is not a JSON object");
                return;
            }
            switch (type)
            {
                case "ready":
                    pageReady = true;
                    Push();
                    break;
                case "close":
                    Dismiss();
                    break;
                case "open":
                    Os.Opened(OsBridge.Text(m, "app"));
                    break;
                case "closed":
                    Os.Closed(OsBridge.Text(m, "app"));
                    break;
                case "req":
                    SendReply(m);
                    break;
            }
        }

        internal static string UserName()
        {
            return (PlayerState.FirstName + " " + PlayerState.LastName).Trim();
        }

        private static void SendReply(JObject request)
        {
            JObject reply = OsBridge.Reply(request);
            if (reply != null) Send(reply);
        }

        private static void Push()
        {
            if (!pageReady) return;
            Send(new JObject(new JProperty("v", 1), new JProperty("type", "theme"), new JProperty("mode", WebUi.Theme)));
            PushWallpaper(ComputerApps.ActiveScreen);
            PushGameArt();
            JArray apps = new JArray();
            JArray sites = new JArray();
            List<OsApp> ordered = Os.Apps.FindAll(x => x.ModId == Os.ModId);
            ordered.AddRange(Os.Apps.FindAll(x => x.ModId != Os.ModId));
            foreach (OsApp app in ordered)
            {
                if (app.Site) sites.Add(SiteEntry(app));
                else if (VirusAv.Shows(app.Id)) apps.Add(AppEntry(app));
            }
            JObject list = new JObject(new JProperty("v", 1), new JProperty("type", "apps"), new JProperty("apps", apps), new JProperty("sites", sites));
            string user = UserName();
            if (user.Length > 0) list.Add(new JProperty("user", user));
            Send(list);
            foreach (OsNotice notice in notices.Take())
            {
                Send(new JObject(new JProperty("v", 1), new JProperty("type", "toast"), new JProperty("title", notice.Title), new JProperty("body", notice.Body), new JProperty("from", notice.From), new JProperty("quiet", true)));
            }
            if (heldApp != null)
            {
                Send(new JObject(new JProperty("v", 1), new JProperty("type", "openapp"), new JProperty("app", heldApp)));
                heldApp = null;
            }
            if (held != null)
            {
                SendOpenFile(held);
                held = null;
            }
            PushMoney();
            lastMinute = -1;
            PushClock();
        }

        private static JObject AppEntry(OsApp app)
        {
            JObject entry = new JObject(new JProperty("id", app.Id), new JProperty("title", app.Title));
            if (app.Icon != null) entry.Add(new JProperty("icon", app.Icon));
            if (app.IconFile != null) entry.Add(new JProperty("iconUrl", FrameOf(app, app.IconFile)));
            if (app.OnOpen != null) entry.Add(new JProperty("launch", true));
            if (app.Folder != null)
            {
                entry.Add(new JProperty("frame", FrameOf(app, app.Page)));
                entry.Add(new JProperty("w", app.Width));
                entry.Add(new JProperty("h", app.Height));
            }
            if (app.ModId != Os.ModId)
            {
                entry.Add(new JProperty("mod", app.ModId));
                entry.Add(new JProperty("desk", app.Desktop));
                entry.Add(new JProperty("pin", app.Pinned));
                entry.Add(new JProperty("badge", app.Badge));
            }
            return entry;
        }

        private static JObject SiteEntry(OsApp site)
        {
            return new JObject(new JProperty("id", site.Id), new JProperty("mod", site.ModId), new JProperty("host", site.Host), new JProperty("title", site.Title), new JProperty("description", site.Description), new JProperty("keywords", JArray.FromObject(site.Keywords)), new JProperty("frame", FrameOf(site, site.Page)));
        }

        private static string FrameOf(OsApp app, string file)
        {
            return view.Origin.Insert("https://".Length, app.Label + ".") + file;
        }

        private static void PushWallpaper(RectTransform screen)
        {
            if (!pageReady) return;
            int rev = OsWallpaper.Capture(screen, webFolder);
            if (rev >= 0) Send(new JObject(new JProperty("v", 1), new JProperty("type", "wallpaper"), new JProperty("name", "game"), new JProperty("rev", rev)));
        }

        private static void PushGameArt()
        {
            if (!pageReady) return;
            OsGameArt.Request(webFolder);
            JArray items = new JArray();
            foreach (OsGameArt.Entry art in OsGameArt.Ready()) items.Add(new JObject(new JProperty("id", art.Id), new JProperty("name", art.Name), new JProperty("rev", art.Rev)));
            Send(new JObject(new JProperty("v", 1), new JProperty("type", "gamewalls"), new JProperty("items", items)));
        }

        private static void PushMoney()
        {
            if (!pageReady || !Cash.IsAvailable) return;
            Send(new JObject(new JProperty("v", 1), new JProperty("type", "money"), new JProperty("oc", (int)Cash.Balance(MoneyCurrency.OC)), new JProperty("rm", Math.Round(Cash.Balance(MoneyCurrency.RM), 2))));
        }

        private static void PushClock()
        {
            if (!pageReady || !WorldClock.IsAvailable) return;
            WorldTime now = WorldClock.Now;
            lastMinute = now.Minute;
            Send(new JObject(new JProperty("v", 1), new JProperty("type", "clock"), new JProperty("day", now.CalendarDay), new JProperty("hour", now.Hour), new JProperty("minute", now.Minute), new JProperty("weekday", now.Weekday.ToString())));
        }

        private static void Send(JObject message)
        {
            if (view == null) return;
            if (!view.Post(message.ToString(Formatting.None))) Log.Warn("OS post failed");
        }
    }
}
