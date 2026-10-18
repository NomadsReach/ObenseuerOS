using System;
using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class NewsEditor
    {
        internal const string StoreId = "nomad.obenseuer.os.news";
        internal const int EditionHour = 6;
        internal const int Keep = 14;
        internal const string Markets = "Markets";
        internal const string Sport = "Sport";
        internal const string Lottery = "Lottery";
        internal const string Weather = "Weather";
        internal const string Local = "Local";
        internal const string Oc = "OC";
        private static readonly string[] LeadOrder = { Sport, Lottery, Markets, Local, Weather };
        private static NewsSave save = new NewsSave();
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);

        internal static NewsSave Current
        {
            get
            {
                return save;
            }
        }

        internal static void Attach()
        {
            SaveData.Register<NewsSave>(StoreId, () => save, loaded => { save = loaded ?? new NewsSave(); Normalise(save); });
            Events.HourPassed += OnHour;
        }

        private static void OnHour(int hour)
        {
            Ensure(WorldClock.Now, hour);
        }

        internal static void Ensure(WorldTime now)
        {
            Ensure(now, now.Hour);
        }

        internal static void Ensure(WorldTime now, int hour)
        {
            if (SaveData.Busy) return;
            Guard("games", StartGames);
            if (save.Seed == 0) save.Seed = new Random().Next(1, int.MaxValue);
            if (!Due(save.Editions, now.Day, hour)) return;
            save.Editions.Add(Build(save.Seed, now, save.Editions));
            Trim(save.Editions);
        }

        private static void StartGames()
        {
            if (!Cash.IsAvailable || !WorldClock.IsAvailable) return;
            BetBook.Begin();
            LottoSave lotto = LottoGame.Current;
            if (lotto.Seed != 0) return;
            OsBridge.CheckWritable();
            LottoGame.Start(lotto, LottoGame.Minutes(WorldClock.Now));
        }

        internal static bool Due(List<NewsEdition> editions, int day, int hour)
        {
            return (hour >= EditionHour || editions.Count == 0) && Find(editions, day) == null;
        }

        internal static NewsEdition Find(List<NewsEdition> editions, int day)
        {
            foreach (NewsEdition edition in editions)
            {
                if (edition.Day == day) return edition;
            }
            return null;
        }

        internal static void Trim(List<NewsEdition> editions)
        {
            while (editions.Count > Keep) editions.RemoveAt(0);
        }

        private static NewsEdition Build(int seed, WorldTime now, List<NewsEdition> earlier)
        {
            BetRandom rng = new BetRandom(BetRandom.Key(seed, now.Day));
            List<NewsStory> stories = new List<NewsStory>();
            Guard("market", () => NewsSources.Market(now.Day - 1, stories));
            Guard("results", () => NewsSources.Results(now.Day - 1, stories));
            Guard("lottery", () => NewsSources.Lottery(now.Day - 1, stories));
            Guard("weather", () => NewsSources.Weather(stories));
            Guard("prices", () => NewsSources.Prices(stories));
            Guard("local", () => NewsTemplates.Local(rng, stories, earlier));
            Guard("crime", () => NewsSources.Crime(stories, earlier));
            Guard("content", () => OsContent.AddNews(stories, now.Day - 1));
            Lead(stories);
            NewsEdition edition = new NewsEdition();
            edition.Day = now.Day;
            edition.Weekday = now.Weekday.ToString();
            edition.Stories = stories;
            return edition;
        }

        private static void Normalise(NewsSave state)
        {
            state.Editions = state.Editions ?? new List<NewsEdition>();
            state.Editions.RemoveAll(x => x == null);
            foreach (NewsEdition edition in state.Editions)
            {
                edition.Weekday = edition.Weekday ?? "";
                edition.Stories = edition.Stories ?? new List<NewsStory>();
                edition.Stories.RemoveAll(x => x == null);
                foreach (NewsStory story in edition.Stories)
                {
                    story.Section = story.Section ?? "";
                    story.Headline = story.Headline ?? "";
                    story.Body = story.Body ?? "";
                    story.Rows = story.Rows ?? new List<NewsRow>();
                    story.Rows.RemoveAll(x => x == null);
                    foreach (NewsRow row in story.Rows)
                    {
                        row.Label = row.Label ?? "";
                        row.Unit = row.Unit ?? "";
                    }
                    story.Nums = story.Nums ?? new List<int>();
                }
            }
        }

        private static void Guard(string name, Action build)
        {
            try
            {
                build();
            }
            catch (Exception e)
            {
                Log.Error("news section " + name + " skipped: " + e);
            }
        }

        private static void Lead(List<NewsStory> stories)
        {
            foreach (string section in LeadOrder)
            {
                for (int i = 0; i < stories.Count; i++)
                {
                    if (stories[i].Section != section) continue;
                    NewsStory lead = stories[i];
                    stories.RemoveAt(i);
                    stories.Insert(0, lead);
                    return;
                }
            }
        }
    }
}
