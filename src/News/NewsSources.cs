using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class NewsSources
    {
        internal static readonly string Celsius = ((char)0xB0).ToString() + "C";
        private const int MarketHeadlines = 20;
        private const int MaxMarketNews = 2;
        private const int MaxMatches = 3;
        private static readonly int[] PriceWatch = { 20210, 10650, 21, 10620, 10840, 7, 8 };

        internal static void Market(int day, List<NewsStory> into)
        {
            StalExApi api;
            try
            {
                api = StalExMarket.Get();
            }
            catch (OsBridgeException)
            {
                return;
            }
            JObject indices = JObject.Parse(api.Indices());
            if (indices.Value<bool>("ok")) into.Add(IndexStory(indices));
            JObject news = JObject.Parse(api.News(MarketHeadlines));
            if (!news.Value<bool>("ok")) return;
            int added = 0;
            foreach (JObject item in (JArray)news["items"])
            {
                if (added == MaxMarketNews || item.Value<int>("d") < day) break;
                into.Add(Story(NewsEditor.Markets, item.Value<string>("text"), "From the Stal-Ex news wire."));
                added++;
            }
        }

        internal static void Results(int day, List<NewsStory> into)
        {
            int added = 0;
            foreach (FixtureRec fixture in BetBook.Current.Fixtures)
            {
                if (!fixture.Done || fixture.Kickoff / BetSchedule.MinutesPerDay != day || added == MaxMatches) continue;
                into.Add(Match(fixture));
                added++;
            }
        }

        internal static void Lottery(int day, List<NewsStory> into)
        {
            foreach (DrawRec draw in LottoGame.Current.Draws)
            {
                if (draw.N != day) continue;
                int winners = 0;
                foreach (int count in draw.Winners) winners += count;
                NewsStory story = Story(NewsEditor.Lottery, winners == 0 ? "The LOTTO drew its numbers last night" : "Lottery pays out to the winners last night", "The NaSpoBet LOTTO draw, held at 20:00.");
                story.Rows.Add(Row("Prize paid", draw.PaidOc, 0, NewsEditor.Oc));
                story.Rows.Add(Row("Winning tickets", winners, 0, ""));
                story.Nums.AddRange(draw.Nums);
                into.Add(story);
                return;
            }
        }

        internal static void Weather(List<NewsStory> into)
        {
            string name = WorldWeather.IsAvailable ? WorldWeather.CurrentName : null;
            if (name == null) return;
            NewsStory story = Story(NewsEditor.Weather, "Weather over Stalburg: " + name, "Conditions when this edition went to press.");
            story.Rows.Add(Row("Temperature", WorldWeather.Temperature, 1, Celsius));
            into.Add(story);
        }

        internal static void Crime(List<NewsStory> into, List<NewsEdition> earlier)
        {
            if (!Pursuit.IsAvailable || !Pursuit.IsPursued) return;
            const string headline = "Police chase under way in Stalburg, residents asked to stay indoors";
            if (NewsTemplates.LocalHeadlines(earlier).Contains(headline)) return;
            into.Add(Story(NewsEditor.Local, headline, "Officers are in pursuit. Drivers and pedestrians are asked to keep clear of the road until the chase ends."));
        }

        internal static void Prices(List<NewsStory> into)
        {
            NewsStory story = Story(NewsEditor.Markets, "Price watch: staples on the shelves", "Catalogue prices in OC, as the game lists them today.");
            foreach (int id in PriceWatch)
            {
                ItemDef item = Items.Get(id);
                if (item != null) story.Rows.Add(Row(item.DisplayName, item.Price, 0, NewsEditor.Oc));
            }
            if (story.Rows.Count > 0) into.Add(story);
        }

        private static NewsStory IndexStory(JObject indices)
        {
            double average = indices.Value<double>("avg24");
            NewsStory story = Story(NewsEditor.Markets, average > 0 ? "Stal-Ex stocks rise over the last 24 hours" : average < 0 ? "Stal-Ex stocks slip over the last 24 hours" : "Stal-Ex stocks go nowhere in 24 hours", "Average change across all listed companies.");
            story.Rows.Add(Row("Average change, 24 h", average, 2, "%"));
            story.Rows.Add(Row("Companies up", indices.Value<int>("up"), 0, ""));
            story.Rows.Add(Row("Companies down", indices.Value<int>("down"), 0, ""));
            AddMover(story, "Top riser", indices["gainers"]);
            AddMover(story, "Top faller", indices["losers"]);
            return story;
        }

        private static void AddMover(NewsStory story, string label, JToken list)
        {
            JArray moves = list as JArray;
            if (moves == null || moves.Count == 0) return;
            JObject first = (JObject)moves[0];
            story.Rows.Add(Row(label + ": " + first.Value<string>("n"), first.Value<double>("c"), 2, "%"));
        }

        private static NewsStory Match(FixtureRec fixture)
        {
            string home = BetSchedule.TeamName(fixture.Sport, fixture.Home);
            string away = BetSchedule.TeamName(fixture.Sport, fixture.Away);
            string headline = fixture.Outcome == 1 ? home + " and " + away + " share the points" : fixture.Outcome == 0 ? home + " beat " + away : away + " beat " + home;
            string body = BetSchedule.League(fixture.Sport) + (fixture.Decider == 1 ? ", after extra time" : fixture.Decider == 2 ? ", after a shoot-out" : "");
            NewsStory story = Story(NewsEditor.Sport, headline, body);
            story.Rows.Add(Row(home, fixture.HomeGoals, 0, ""));
            story.Rows.Add(Row(away, fixture.AwayGoals, 0, ""));
            return story;
        }

        private static NewsStory Story(string section, string headline, string body)
        {
            NewsStory story = new NewsStory();
            story.Section = section;
            story.Headline = headline;
            story.Body = body;
            return story;
        }

        private static NewsRow Row(string label, double value, int dec, string unit)
        {
            NewsRow row = new NewsRow();
            row.Label = label;
            row.Value = (float)value;
            row.Dec = dec;
            row.Unit = unit;
            return row;
        }
    }
}
