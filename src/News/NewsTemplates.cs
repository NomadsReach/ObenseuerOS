using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class NewsTemplates
    {
        private const int LocalStories = 3;
        private static readonly string[] Places = { "Stalburg", "Ardscratch", "Baltasfell", "Cellecter", "Illo", "Ploughwedge", "Stemlake", "Tarfell", "Tilstrand", "Ershfors", "Forde", "Koksburg", "Sydhavn", "Erzfell", "Kolhola", "Deekula" };
        private static readonly string[] Companies = { "Stalburg Steel LLC", "Hammer Valley Hydro LLC", "Stalburg Water Co.", "Stalburg Mass Transit", "Prism", "Skeida", "O-Market", "Staldrinks", "Osmo Olut", "Stalburg Milk", "Stalenergy Co.", "AtomCraft", "PC Master" };
        private static readonly string[] Organisations = { "Stalburg City Council", "Emergency Management Stalburg", "Raven Research Institute" };
        private static readonly string[][] Templates =
        {
            new[] { "Ferry to {0} cancelled again, {1} blames the sea", "Passengers were advised to wait on the quay. The quay was not consulted." },
            new[] { "{2} opens a complaints desk in {0}, closes it for lunch", "The desk will reopen once the complaints have had their lunch too." },
            new[] { "{1} promises {0} a new road, drawn on the back of a bus ticket", "Officials called the ticket an official document and asked for patience." },
            new[] { "Queue outside {1} in {0} now longer than the queue to leave", "A spokesperson said the queue showed how much people wanted to be somewhere else." },
            new[] { "{2} finds {0} residents still alive, asks for a budget", "The budget was approved in principle, pending a further principle." },
            new[] { "Lamp posts in {0} lit for the first time since the last election", "{1} said the lights would be switched off in spring, to save the cost of looking." },
            new[] { "Tram driver in {0} praised for reaching the end of the line", "{2} is considering a bonus, subject to the line having an end." },
            new[] { "{1} sponsors a bench in {0}, which is already occupied", "The plaque says thank you. The bench says nothing, as usual." },
            new[] { "Mould found in the {0} school canteen, {2} calls it a side dish", "Lunches will continue, with the mould served alongside the potatoes." },
            new[] { "Lost dog returns to {0} after a week in a {1} warehouse", "The dog was found asleep on a pallet of unsold bottles and declined to comment." },
            new[] { "Study from {2} finds mood in {0} unchanged since the last study", "Researchers called the result stable and asked for another year." },
            new[] { "Snow cleared from {0} streets and moved to the next street", "Crews said the snow would be kept for next winter, in a field near {0}." },
            new[] { "Staldrinks recalls a batch of Septi from {0}, the batch was already empty", "Staff said the batch had been sold out for a week, so the recall is mostly for the bottles." },
            new[] { "Canal bridge at {0} closed for repairs until a plan for the repairs is drawn", "{2} said the plan would be drawn once the bridge had been closed for a while." },
            new[] { "Water pipes in {0} sing at night, {1} offers a recording", "Residents asked for a repair. {1} said the singing was the repair and sent a recording of it." },
            new[] { "Mine tunnels under {0} flooded for the third time this month", "The pumps are working, the pumps said. {1} says the flood is being monitored from above ground." },
            new[] { "Stalburg Bank opens a new queue system in {0}, the old queue remains", "The new system gives every customer a number. The number is for the old queue." },
            new[] { "Monetary Bank of Stalburg counts the coins of {0} twice and gets two answers", "A spokesperson said both answers were correct and that the coins had not agreed to be counted." },
            new[] { "Ferry from {0} runs empty for a week, {2} says the fish are grateful", "Passengers were not asked. The fish did not respond." },
            new[] { "Sewer covers in {0} painted over, {2} calls the paint a fix", "The paint is expected to last until the next paint, which the council will also pay for." },
            new[] { "{1} sends ten new guards to {0}, none of them have uniforms", "A spokesperson called it a soft start and said the uniforms were on order from the same company." },
            new[] { "M'Wind turbines near {0} stopped for the holiday, {2} says the wind agreed", "The turbines will restart when the wind returns from its holiday." },
            new[] { "Football pitch in {0} flooded, the match moved to the car park", "Players said the car park was dry, which is more than can be said for the pitch." },
            new[] { "Power cut in {0} lasts two days for maintenance, Stalburg Industry Power says the lamps were warned", "Residents were asked to plan their evenings around the lamps." },
            new[] { "Bus timetable for {0} printed in pencil, {2} says it will be rubbed out later", "The pencil is expected to last until the next timetable, which will also be in pencil." },
            new[] { "Raven Research Institute counts the gulls of {0} and gets a different number", "The count will be repeated next year. The gulls declined to comment." },
            new[] { "Stalburg Water says tap water in {0} is safe, once it has been boiled", "Boil it, let it cool, and then ask Stalburg Water whether it is still safe." },
            new[] { "Free bench offered in {0}, {1} asks for the queue to form first", "The bench will be delivered when the queue has finished forming." },
            new[] { "Stalburg Milk delivers on time to {0}, shops ask for a refund", "The shops say the milk is still warm. It is not supposed to be." },
            new[] { "Pigeons take over the station clock in {0}, {2} asks them to vote", "The pigeons were not on the ballot, but their vote is expected to be the same as last time." },
        };

        internal static void Local(BetRandom rng, List<NewsStory> into, List<NewsEdition> earlier)
        {
            HashSet<string> taken = LocalHeadlines(earlier);
            List<int> used = new List<int>();
            while (used.Count < LocalStories)
            {
                int pick = Pick(rng, Templates.Length);
                if (used.Contains(pick)) continue;
                string place = Places[Pick(rng, Places.Length)];
                string company = Companies[Pick(rng, Companies.Length)];
                string organisation = Organisations[Pick(rng, Organisations.Length)];
                string headline = string.Format(Templates[pick][0], place, company, organisation);
                if (taken.Contains(headline)) continue;
                used.Add(pick);
                taken.Add(headline);
                NewsStory story = new NewsStory();
                story.Section = NewsEditor.Local;
                story.Headline = headline;
                story.Body = string.Format(Templates[pick][1], place, company, organisation);
                into.Add(story);
            }
        }

        internal static HashSet<string> LocalHeadlines(List<NewsEdition> editions)
        {
            HashSet<string> headlines = new HashSet<string>();
            foreach (NewsEdition edition in editions)
            {
                foreach (NewsStory story in edition.Stories)
                {
                    if (story.Section == NewsEditor.Local) headlines.Add(story.Headline);
                }
            }
            return headlines;
        }

        private static int Pick(BetRandom rng, int count)
        {
            return (int)(rng.Uniform() * count);
        }
    }
}
