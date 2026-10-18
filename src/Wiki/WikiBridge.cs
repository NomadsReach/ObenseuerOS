using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class WikiBridge
    {
        private const string AppId = "wiki";
        private const int MaxSearch = 10;
        private const int MaxTitles = 200;
        private static readonly bool SavedCopy = false;
        private const string SavedOff = "Saved copies are off in this release.";
        private static WikiCache cache;
        private static string folder;

        internal static bool SavesCopies { get { return SavedCopy; } }

        internal static string Folder
        {
            get
            {
                return folder;
            }
        }

        internal static void Attach(bool enabled, int cacheMb, int minIntervalMs)
        {
            string root = Path.Combine(Paths.BepInExRootPath, "cache", "ObenseuerOS");
            folder = Path.Combine(root, "wiki-site");
            cache = SavedCopy ? new WikiCache(folder, Math.Max(1, cacheMb) * 1024L * 1024L) : null;
            string state = Path.Combine(root, "wiki");
            Directory.CreateDirectory(state);
            WikiNet.Attach(state, enabled, minIntervalMs);
        }

        internal static void Register()
        {
            Os.Handle(AppId, "wiki.status", Status);
            Os.Handle(AppId, "wiki.consent", Consent);
            Os.Handle(AppId, "wiki.page", Page);
            Os.Handle(AppId, "wiki.poll", Poll);
            Os.Handle(AppId, "wiki.search", Search);
            Os.Handle(AppId, "wiki.titles", Titles);
            Os.Handle(AppId, "wiki.bulk", Bulk);
            Os.Handle(AppId, "wiki.clear", Clear);
        }

        internal static void Tick()
        {
            WikiNet.Tick();
            if (SavedCopy) WikiBulk.Tick(cache);
        }

        private static string Status(string args)
        {
            return new JObject(new JProperty("online", WikiNet.Online), new JProperty("consent", WikiNet.Consented), new JProperty("saving", SavedCopy), new JProperty("cachePages", SavedCopy ? cache.PageCount() : 0), new JProperty("cacheBytes", SavedCopy ? cache.TotalBytes() : 0), new JProperty("bulk", SavedCopy ? WikiBulk.Status() : new JObject(new JProperty("state", "idle"))), new JProperty("host", WikiTitle.Host), new JProperty("live", OsView.Live)).ToString(Formatting.None);
        }

        private static string Consent(string args)
        {
            JToken allow = OsBridge.Args(args)["allow"];
            if (allow == null || allow.Type != JTokenType.Boolean) throw new OsBridgeException("bad_args", "allow must be true or false");
            try
            {
                WikiNet.SetConsent((bool)allow);
            }
            catch (IOException)
            {
                throw new OsBridgeException("failed", "The choice could not be saved.");
            }
            catch (UnauthorizedAccessException)
            {
                throw new OsBridgeException("failed", "The choice could not be saved.");
            }
            return "true";
        }

        private static string Page(string args)
        {
            if (!SavedCopy) throw new OsBridgeException("blocked", SavedOff);
            JObject a = OsBridge.Args(args);
            string title = Title(a);
            bool refresh = OsBridge.Flag(a, "refresh");
            long now = WikiNet.Now();
            WikiPage page = cache.LoadPage(title, now);
            if (page != null && WikiCache.IsFresh(page.Fetched, now) && !refresh) return WikiJobs.ReadyJson(page, false);
            if (!WikiNet.Online)
            {
                if (page == null) throw new OsBridgeException("denied", WikiJobs.DeniedText);
                return WikiJobs.ReadyJson(page, true);
            }
            if (page != null && !refresh)
            {
                WikiSaver.Start(cache, title, true, false);
                return WikiJobs.ReadyJson(page, true);
            }
            return Pending(WikiSaver.Start(cache, title, true, false));
        }

        private static string Poll(string args)
        {
            WikiJob job = WikiJobs.Find(OsBridge.Text(OsBridge.Args(args), "job"));
            if (job == null) throw new OsBridgeException("bad_args", "unknown job");
            if (!job.Done) return new JObject(new JProperty("state", "pending")).ToString(Formatting.None);
            if (job.Code != null) throw new OsBridgeException(job.Code, job.Message);
            return job.Result;
        }

        private static string Search(string args)
        {
            JObject a = OsBridge.Args(args);
            string q = OsBridge.Text(a, "q");
            int offset = OsBridge.Span(a, "offset", int.MaxValue, 0);
            int limit = Math.Max(1, OsBridge.Span(a, "limit", MaxSearch, MaxSearch));
            string url = q == null ? null : WikiTitle.Search(q, offset, limit);
            if (url == null) throw new OsBridgeException("bad_args", "q must be a search of 1 to 300 characters");
            if (!WikiNet.Online)
            {
                if (!SavedCopy) throw new OsBridgeException("denied", WikiJobs.DeniedText);
                return WikiIndex.Local(cache, q, offset, limit);
            }
            return Pending(WikiIndex.StartSearch(url, offset));
        }

        private static string Titles(string args)
        {
            JObject a = OsBridge.Args(args);
            int offset = OsBridge.Span(a, "offset", int.MaxValue, 0);
            int limit = Math.Max(1, OsBridge.Span(a, "limit", MaxTitles, MaxTitles));
            List<string> titles = SavedCopy ? WikiIndex.Load(cache) ?? new List<string>() : new List<string>();
            JArray items = new JArray();
            int bytes = 0;
            for (int i = offset; i < titles.Count && items.Count < limit; i++)
            {
                int cost = WikiChunk.Cost(titles[i]) + 4;
                if (bytes + cost > WikiChunk.TargetBytes) break;
                bytes += cost;
                items.Add(titles[i]);
            }
            return new JObject(new JProperty("total", titles.Count), new JProperty("items", items)).ToString(Formatting.None);
        }

        private static string Bulk(string args)
        {
            if (!SavedCopy) throw new OsBridgeException("blocked", SavedOff);
            JObject a = OsBridge.Args(args);
            string action = OsBridge.Text(a, "action");
            if (action == "start")
            {
                if (!WikiNet.Online) throw new OsBridgeException("denied", WikiJobs.DeniedText);
                WikiBulk.Start(cache, OsBridge.Flag(a, "images"));
            }
            else if (action == "pause") WikiBulk.Pause();
            else if (action == "resume") WikiBulk.Resume();
            else if (action == "cancel") WikiBulk.Cancel();
            else throw new OsBridgeException("bad_args", "action must be start, pause, resume or cancel");
            return WikiBulk.Status().ToString(Formatting.None);
        }

        private static string Clear(string args)
        {
            if (!SavedCopy) throw new OsBridgeException("blocked", SavedOff);
            WikiBulk.Cancel();
            cache.Clear();
            return "true";
        }

        private static string Title(JObject a)
        {
            string raw = OsBridge.Text(a, "title");
            if (raw == null) throw new OsBridgeException("bad_args", "title must be a string");
            string title;
            if (!WikiTitle.TryNormalise(raw, out title)) throw new OsBridgeException("blocked", "That title is not allowed on the wiki.");
            return title;
        }

        private static string Pending(WikiJob job)
        {
            return new JObject(new JProperty("state", "pending"), new JProperty("job", job.Id)).ToString(Formatting.None);
        }
    }
}
