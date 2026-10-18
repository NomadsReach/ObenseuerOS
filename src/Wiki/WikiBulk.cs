using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class WikiBulk
    {
        private const string Idle = "idle";
        private const string Indexing = "indexing";
        private const string Running = "running";
        private const string Paused = "paused";
        private const string Done = "done";
        private const string Stopped = "stopped";
        private const int MaxFailures = 3;
        private static string state = Idle;
        private static string message = "";
        private static bool images;
        private static List<string> titles = new List<string>();
        private static int at;
        private static int saved;
        private static int errors;
        private static int failures;
        private static WikiJob index;
        private static WikiJob page;

        internal static void Start(WikiCache cache, bool withImages)
        {
            if (state == Running || state == Indexing || state == Paused) return;
            images = withImages;
            at = 0;
            saved = 0;
            errors = 0;
            failures = 0;
            message = "";
            page = null;
            long fetched;
            List<string> cached = cache.LoadTitles(out fetched);
            if (cached != null && WikiCache.IsFresh(fetched, WikiNet.Now()))
            {
                titles = cached;
                state = Running;
                return;
            }
            titles = new List<string>();
            index = WikiIndex.Start(cache);
            state = Indexing;
        }

        internal static void Pause()
        {
            if (state == Running) state = Paused;
        }

        internal static void Resume()
        {
            if (state == Paused) state = Running;
        }

        internal static void Cancel()
        {
            if (state != Running && state != Paused && state != Indexing) return;
            WikiNet.Drop();
            page = null;
            index = null;
            state = Idle;
            message = "Cancelled after " + saved + " pages saved.";
        }

        internal static void Tick(WikiCache cache)
        {
            if (state == Indexing) TickIndex(cache);
            else if ((state == Running || state == Paused) && !WikiNet.Online) Stop(WikiJobs.DeniedText);
            else if (state == Running) TickPages(cache);
        }

        internal static JObject Status()
        {
            return new JObject(new JProperty("state", state), new JProperty("done", at), new JProperty("total", titles.Count), new JProperty("saved", saved), new JProperty("errors", errors), new JProperty("images", images), new JProperty("message", message));
        }

        private static void TickIndex(WikiCache cache)
        {
            if (!index.Done) return;
            if (index.Code != null)
            {
                Stop(index.Message);
                return;
            }
            long fetched;
            List<string> loaded = cache.LoadTitles(out fetched);
            if (loaded == null)
            {
                Stop("The title list could not be read.");
                return;
            }
            titles = loaded;
            state = Running;
        }

        private static void TickPages(WikiCache cache)
        {
            if (page != null)
            {
                if (!page.Done) return;
                Finished(page);
                page = null;
                if (state != Running) return;
            }
            while (at < titles.Count && cache.PageFile(titles[at]) != null) at++;
            if (at >= titles.Count)
            {
                state = Done;
                return;
            }
            page = WikiSaver.Start(cache, titles[at++], images, true);
        }

        private static void Finished(WikiJob job)
        {
            if (job.Code == null)
            {
                saved++;
                failures = 0;
                return;
            }
            errors++;
            failures++;
            if (job.Code == "rate_limited" || job.Code == "denied" || job.Code == "cache_full") Stop(job.Message);
            else if (failures >= MaxFailures) Stop("Stopped: " + MaxFailures + " pages in a row failed. " + job.Message);
        }

        private static void Stop(string text)
        {
            state = Stopped;
            message = text;
        }
    }
}
