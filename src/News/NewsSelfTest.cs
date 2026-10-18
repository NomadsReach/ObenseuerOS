using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ObenseuerOS
{
    internal static class NewsSelfTest
    {
        internal static string Run()
        {
            List<NewsEdition> editions = new List<NewsEdition>();
            if (!NewsEditor.Due(editions, 5, 5)) return "no first edition on a fresh save";
            NewsEdition first = new NewsEdition();
            first.Day = 5;
            editions.Add(first);
            if (NewsEditor.Due(editions, 5, 5)) return "edition before 06:00";
            if (!NewsEditor.Due(editions, 6, 6)) return "no edition at 06:00";
            for (int day = 1; day <= 20; day++)
            {
                NewsEdition edition = new NewsEdition();
                edition.Day = day;
                editions.Add(edition);
                NewsEditor.Trim(editions);
            }
            if (editions.Count != NewsEditor.Keep || editions[0].Day != 20 - NewsEditor.Keep + 1) return "keep limit";
            if (NewsEditor.Due(editions, 20, 9)) return "second edition on one day";
            if (NewsEditor.Find(editions, 6) != null || NewsEditor.Find(editions, 20) == null) return "find";
            if (BetRandom.Key(1, 2) != BetRandom.Key(1, 2) || BetRandom.Key(1, 2) == BetRandom.Key(1, 3)) return "seed key";
            List<NewsStory> stories = new List<NewsStory>();
            NewsTemplates.Local(new BetRandom(BetRandom.Key(1, 1)), stories, new List<NewsEdition>());
            if (stories.Count == 0) return "no articles";
            NewsEdition written = new NewsEdition();
            written.Day = 1;
            written.Weekday = "Monday";
            written.Stories = stories;
            NewsSave save = new NewsSave();
            save.Editions.Add(written);
            NewsSave back = JsonConvert.DeserializeObject<NewsSave>(JsonConvert.SerializeObject(save));
            if (back.Editions.Count != 1 || back.Editions[0].Stories.Count != stories.Count) return "edition did not round trip";
            foreach (NewsStory story in back.Editions[0].Stories)
            {
                if (string.IsNullOrEmpty(story.Headline) || string.IsNullOrEmpty(story.Body)) return "article did not round trip";
            }
            return null;
        }
    }
}
