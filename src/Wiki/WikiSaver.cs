using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal sealed class WikiSaver
    {
        private const string CacheFull = "Bulk save stopped: cache limit reached. Raise CacheMaxMB in the mod settings or clear the saved copy.";
        private const string StyleFailed = "A stylesheet from the wiki could not be saved, so the page was not saved.";
        private const int PageCap = 8 * 1024 * 1024;
        private const int CssCap = 1024 * 1024;
        private const int ImageCap = 2 * 1024 * 1024;
        private const int MaxRounds = 2;
        private static readonly Regex RevisionPattern = new Regex("\"wgCurRevisionId\":([0-9]{1,15})", RegexOptions.CultureInvariant);
        private readonly WikiCache cache;
        private readonly string title;
        private readonly bool images;
        private readonly bool bulk;
        private readonly WikiJob job;
        private string html;
        private long revid;
        private List<string> keys = new List<string>();
        private HashSet<string> queued = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> styles = new Dictionary<string, string>(StringComparer.Ordinal);
        private int next;
        private int rounds;

        private WikiSaver(WikiCache cache, string title, bool images, bool bulk, WikiJob job)
        {
            this.cache = cache;
            this.title = title;
            this.images = images;
            this.bulk = bulk;
            this.job = job;
        }

        internal static WikiJob Start(WikiCache cache, string title, bool images, bool bulk)
        {
            string key = "page:" + title;
            WikiJob open = WikiJobs.Open(key);
            if (open != null) return open;
            WikiJob job = WikiJobs.Start(key);
            WikiSaver saver = new WikiSaver(cache, title, images, bulk, job);
            WikiJobs.Robots(job, saver.Article);
            return job;
        }

        private void Article()
        {
            string url = WikiTitle.Article(title);
            if (url == null || WikiRobots.Blocked("/" + WikiTitle.ArticlePath(title)))
            {
                WikiJobs.Fail(job, "blocked", "That page is not allowed on the wiki.");
                return;
            }
            Fetch(url, PageCap, WikiNet.Lane.Front, reply =>
            {
                if (reply.Kind != WikiResponse.Ok)
                {
                    WikiJobs.FailKind(job, reply.Kind);
                    return;
                }
                html = Text(reply.Body);
                revid = RevisionOf(html);
                WikiAssets plan = new WikiAssets(cache, WikiNet.Now());
                WikiHtml.Render(html, plan);
                keys = new List<string>(plan.Wanted).FindAll(k => images || IsStyle(k));
                queued = new HashSet<string>(keys, StringComparer.Ordinal);
                job.Total = keys.Count;
                Next();
            });
        }

        private void Next()
        {
            while (true)
            {
                job.Steps = next;
                job.Total = keys.Count;
                if (next >= keys.Count)
                {
                    Finish();
                    return;
                }
                string key = keys[next++];
                bool style = IsStyle(key);
                string path = style ? key.Substring(WikiTitle.Origin.Length) : key;
                string url = style ? key : WikiTitle.Image(key);
                if (url == null || WikiRobots.Blocked(path)) continue;
                Fetch(url, style ? CssCap : ImageCap, WikiNet.Lane.Back, reply =>
                {
                    if (reply.Kind == WikiResponse.Ok)
                    {
                        if (!Store(key, reply.Body)) return;
                    }
                    else if (Stops(reply.Kind))
                    {
                        WikiJobs.FailKind(job, reply.Kind);
                        return;
                    }
                    Next();
                });
                return;
            }
        }

        private void Finish()
        {
            long now = WikiNet.Now();
            foreach (KeyValuePair<string, string> style in styles)
            {
                string css = WikiCss.Render(style.Value, new WikiAssets(cache, now));
                if (!Room(Encoding.UTF8.GetByteCount(css))) return;
                if (cache.SaveCss(style.Key, css, now) == null)
                {
                    WikiJobs.Fail(job, "failed", StyleFailed);
                    return;
                }
            }
            WikiAssets left = new WikiAssets(cache, now, title);
            string rendered = WikiHtml.Render(html, left);
            if (images && left.Wanted.Count > 0 && rounds < MaxRounds)
            {
                rounds++;
                keys = new List<string>(left.Wanted);
                queued = new HashSet<string>(keys, StringComparer.Ordinal);
                next = 0;
                Next();
                return;
            }
            if (!Room(Encoding.UTF8.GetByteCount(rendered))) return;
            if (!cache.SavePage(title, revid, now, rendered, now))
            {
                WikiJobs.Fail(job, "failed", "The page could not be saved.");
                return;
            }
            job.Result = WikiJobs.ReadyJson(new WikiPage { Title = title, RevId = revid, Fetched = now, File = cache.PageFile(title) }, false);
            job.Done = true;
        }

        private bool Store(string key, byte[] body)
        {
            if (IsStyle(key))
            {
                string text = Text(body);
                WikiAssets found = new WikiAssets(cache, WikiNet.Now());
                WikiCss.Render(text, found);
                foreach (string image in found.Wanted)
                {
                    if (images && keys.Count < WikiAssets.MaxWanted && queued.Add(image)) keys.Add(image);
                }
                styles[key] = text;
                return true;
            }
            if (!WikiResponse.ImageFits(body)) return true;
            if (!Room(body.Length)) return false;
            cache.SaveImage(key, body, WikiNet.Now());
            return true;
        }

        private bool Room(long bytes)
        {
            if (!bulk || cache.Fits(bytes)) return true;
            WikiJobs.Fail(job, "cache_full", CacheFull);
            return false;
        }

        private void Fetch(string url, int cap, WikiNet.Lane lane, Action<WikiReply> then)
        {
            WikiJobs.Send(job, url, cap, false, lane, then);
        }

        private static bool IsStyle(string key)
        {
            return key.StartsWith(WikiTitle.Origin, StringComparison.Ordinal);
        }

        private static bool Stops(string kind)
        {
            return kind == WikiResponse.RateLimited || kind == WikiResponse.Challenge || kind == WikiResponse.Maxlag || kind == WikiResponse.Network || kind == WikiResponse.Timeout || kind == WikiResponse.Dropped;
        }

        private static long RevisionOf(string text)
        {
            Match match = RevisionPattern.Match(text);
            long value;
            return match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out value) ? value : 0;
        }

        private static string Text(byte[] body)
        {
            return Encoding.UTF8.GetString(body);
        }
    }
}
