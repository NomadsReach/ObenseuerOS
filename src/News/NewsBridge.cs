using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class NewsBridge
    {
        private const string AppId = "news";
        private const string NotReady = "The Stalburg Times is not ready yet.";
        private const string NoEdition = "No edition for that day.";
        private const string NoStory = "No story with that number.";

        internal static void Attach()
        {
            NewsEditor.Attach();
            Os.Handle(AppId, "news.editions", Editions);
            Os.Handle(AppId, "news.front", Front);
            Os.Handle(AppId, "news.article", Article);
            Os.Handle("browser", "news.front", Front);
            Os.Handle("eyebook", "news.front", Front);
        }

        private static string Editions(string args)
        {
            Prepare();
            List<NewsEdition> all = NewsEditor.Current.Editions;
            List<object> days = new List<object>();
            for (int i = all.Count - 1; i >= 0; i--)
            {
                days.Add(new { day = all[i].Day, weekday = all[i].Weekday });
            }
            return JsonConvert.SerializeObject(new { days = days });
        }

        private static string Front(string args)
        {
            Prepare();
            NewsEdition edition = Pick(OsBridge.Args(args));
            if (edition == null || edition.Stories.Count == 0) return JsonConvert.SerializeObject(new { stories = new object[0] });
            List<object> list = new List<object>();
            for (int i = 0; i < edition.Stories.Count; i++)
            {
                list.Add(new { i = i, section = edition.Stories[i].Section, headline = edition.Stories[i].Headline });
            }
            return JsonConvert.SerializeObject(new { day = edition.Day, weekday = edition.Weekday, lead = Full(edition.Day, 0, edition.Stories[0]), stories = list });
        }

        private static string Article(string args)
        {
            Prepare();
            JObject a = OsBridge.Args(args);
            NewsEdition edition = Pick(a);
            if (edition == null) throw new OsBridgeException("bad_args", NoEdition);
            int i = OsBridge.Span(a, "i", int.MaxValue, -1);
            if (i < 0 || i >= edition.Stories.Count) throw new OsBridgeException("bad_args", NoStory);
            return JsonConvert.SerializeObject(Full(edition.Day, i, edition.Stories[i]));
        }

        private static NewsEdition Pick(JObject a)
        {
            List<NewsEdition> all = NewsEditor.Current.Editions;
            int day = OsBridge.Span(a, "day", int.MaxValue, -1);
            if (day < 0) return all.Count == 0 ? null : all[all.Count - 1];
            NewsEdition edition = NewsEditor.Find(all, day);
            if (edition == null) throw new OsBridgeException("bad_args", NoEdition);
            return edition;
        }

        private static object Full(int day, int i, NewsStory story)
        {
            List<object> rows = new List<object>();
            foreach (NewsRow row in story.Rows) rows.Add(new { label = row.Label, value = row.Value, dec = row.Dec, unit = row.Unit });
            return new { day = day, i = i, section = story.Section, headline = story.Headline, body = story.Body, rows = rows, nums = story.Nums };
        }

        private static void Prepare()
        {
            if (!WorldClock.IsAvailable) throw new OsBridgeException("failed", NotReady);
            if (!SaveData.Busy) NewsEditor.Ensure(WorldClock.Now);
        }
    }
}
