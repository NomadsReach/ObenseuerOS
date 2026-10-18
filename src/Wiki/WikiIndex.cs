using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class WikiIndex
    {
        private const string Key = "index";
        private const string SearchKey = "search";
        private const int ApiCap = 6 * 1024 * 1024;
        private const int MaxPages = 10;

        internal static WikiJob Start(WikiCache cache)
        {
            WikiJob open = WikiJobs.Open(Key);
            if (open != null) return open;
            WikiJob job = WikiJobs.Start(Key);
            WikiJobs.Robots(job, () => Page(cache, job, new List<string>(), null, 1));
            return job;
        }

        internal static List<string> Load(WikiCache cache)
        {
            long fetched;
            List<string> titles = cache.LoadTitles(out fetched);
            if (WikiNet.Online && (titles == null || !WikiCache.IsFresh(fetched, WikiNet.Now()))) Start(cache);
            return titles;
        }

        internal static WikiJob StartSearch(string url, int offset)
        {
            WikiJob job = WikiJobs.Start(SearchKey);
            WikiJobs.Robots(job, () => WikiJobs.Send(job, url, ApiCap, true, WikiNet.Lane.Front, reply => Results(job, offset, reply)));
            return job;
        }

        internal static string Local(WikiCache cache, string q, int offset, int limit)
        {
            long fetched;
            List<string> titles = cache.LoadTitles(out fetched) ?? new List<string>();
            string needle = q.Replace('_', ' ');
            List<string> hits = titles.FindAll(t => t.Replace('_', ' ').IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0);
            JArray items = new JArray();
            for (int i = offset; i < hits.Count && items.Count < limit; i++)
            {
                items.Add(new JObject(new JProperty("title", hits[i].Replace('_', ' ')), new JProperty("snippet", ""), new JProperty("size", 0), new JProperty("words", 0)));
            }
            long next = (long)offset + items.Count;
            return new JObject(new JProperty("total", hits.Count), new JProperty("next", next < hits.Count ? (JToken)new JValue(next) : JValue.CreateNull()), new JProperty("items", items), new JProperty("source", "local")).ToString(Formatting.None);
        }

        private static void Page(WikiCache cache, WikiJob job, List<string> titles, string from, int pages)
        {
            WikiJobs.Send(job, WikiTitle.TitleIndex(from), ApiCap, true, WikiNet.Lane.Back, reply => Listed(cache, job, titles, pages, reply));
        }

        private static void Listed(WikiCache cache, WikiJob job, List<string> titles, int pages, WikiReply reply)
        {
            if (reply.Kind != WikiResponse.Ok)
            {
                WikiJobs.FailKind(job, reply.Kind);
                return;
            }
            JObject body = JObject.Parse(Encoding.UTF8.GetString(reply.Body));
            foreach (JToken item in (JArray)body["query"]["allpages"])
            {
                string normal;
                if (WikiTitle.TryNormalise((string)item["title"], out normal)) titles.Add(normal);
            }
            JToken more = body["continue"];
            string from = more == null ? null : (string)more["apcontinue"];
            if (from == null)
            {
                if (cache.SaveTitles(titles, WikiNet.Now())) job.Done = true;
                else WikiJobs.Fail(job, "failed", "The title index could not be saved.");
                return;
            }
            if (pages >= MaxPages) WikiJobs.Fail(job, "failed", "The wiki has more pages than the title index can hold.");
            else Page(cache, job, titles, from, pages + 1);
        }

        private static void Results(WikiJob job, int offset, WikiReply reply)
        {
            if (reply.Kind != WikiResponse.Ok)
            {
                WikiJobs.FailKind(job, reply.Kind);
                return;
            }
            JObject body = JObject.Parse(Encoding.UTF8.GetString(reply.Body));
            JArray hits = (JArray)body["query"]["search"];
            int total = (int)body["query"]["searchinfo"]["totalhits"];
            JArray items = new JArray();
            int bytes = 0;
            int used = 0;
            foreach (JToken hit in hits)
            {
                string normal;
                if (WikiTitle.TryNormalise((string)hit["title"], out normal))
                {
                    JObject item = new JObject(new JProperty("title", (string)hit["title"]), new JProperty("snippet", WikiResponse.Snippet((string)hit["snippet"])), new JProperty("size", (long)hit["size"]), new JProperty("words", (long)hit["wordcount"]));
                    int cost = WikiChunk.Cost(item.ToString(Formatting.None)) + 1;
                    if (bytes + cost > WikiChunk.TargetBytes) break;
                    bytes += cost;
                    items.Add(item);
                }
                used++;
            }
            long next = (long)offset + used;
            job.Result = new JObject(new JProperty("total", total), new JProperty("next", next < total ? (JToken)new JValue(next) : JValue.CreateNull()), new JProperty("items", items), new JProperty("source", "wiki")).ToString(Formatting.None);
            job.Done = true;
        }
    }
}
