using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    public sealed class OsNewsStory
    {
        public string Headline;
        public string Body;
    }

    public static partial class Os
    {
        public static bool AddNewsSource(string modId, string section, Func<int, OsNewsStory[]> source)
        {
            try
            {
                if (!Accept("AddNewsSource", modId)) return false;
                string shelf = string.IsNullOrEmpty(section) ? OsContent.DefaultSection : section;
                if (Array.IndexOf(OsContent.Sections, shelf) < 0) return Refuse("AddNewsSource", modId, false, "section must be Markets, Sport, Lottery, Weather or Local");
                if (source == null) return Refuse("AddNewsSource", modId, false, "source is null");
                if (OsContent.News.Exists(x => x.ModId == modId)) return Refuse("AddNewsSource", modId, false, "the mod already has a news source");
                OsContent.News.Add(new OsNewsSource(modId, shelf, source));
                return true;
            }
            catch (Exception e)
            {
                Crash("AddNewsSource", e);
                return false;
            }
        }

        public static bool AddChatRoom(string modId, string roomId, string name, string[] lines, string[] members)
        {
            try
            {
                if (!Accept("AddChatRoom", modId)) return false;
                if (roomId == null || !OsContent.ValidRoomId.IsMatch(roomId)) return Refuse("AddChatRoom", modId, false, "roomId must match [a-z0-9._-]{1,40}");
                string key = modId + "/" + roomId;
                if (ChatData.Find(key) != null) return Refuse("AddChatRoom", modId, false, "duplicate room id " + roomId);
                if (ChatData.Rooms.FindAll(r => OsContent.OwnsRoom(modId, r)).Count >= OsContent.MaxRooms) return Refuse("AddChatRoom", modId, false, "a mod has at most 2 chat rooms");
                string title = OsContent.RoomName(name);
                if (title == null) return Refuse("AddChatRoom", modId, false, "name is empty");
                string[] text = OsContent.Texts(lines, ChatData.MaxText);
                if (text.Length < 1 || text.Length > OsContent.MaxRoomLines) return Refuse("AddChatRoom", modId, false, "lines must be 1 to 50");
                string[] people = OsContent.Texts(members, OsContent.MaxName);
                if (people.Length < 1 || people.Length > OsContent.MaxMembers) return Refuse("AddChatRoom", modId, false, "members must be 1 to 8");
                ChatData.Rooms.Add(new ChatRoomDef(key, title, false, text, people));
                return true;
            }
            catch (Exception e)
            {
                Crash("AddChatRoom", e);
                return false;
            }
        }
    }

    internal sealed class OsNewsSource
    {
        internal readonly string ModId;
        internal readonly string Section;
        internal readonly Func<int, OsNewsStory[]> Source;

        internal OsNewsSource(string modId, string section, Func<int, OsNewsStory[]> source)
        {
            ModId = modId;
            Section = section;
            Source = source;
        }
    }

    internal static class OsContent
    {
        internal const string DefaultSection = "Local";
        internal static readonly string[] Sections = { "Markets", "Sport", "Lottery", "Weather", DefaultSection };
        internal const int MaxStories = 2;
        internal const int MaxModStories = 8;
        internal const int MaxHeadline = 100;
        internal const int MaxBody = 1500;
        internal const int MaxRooms = 2;
        internal const int MaxRoomLines = 50;
        internal const int MaxMembers = 8;
        internal const int MaxName = 40;
        internal static readonly Regex ValidRoomId = new Regex(@"\A[a-z0-9._-]{1,40}\z");
        internal static readonly List<OsNewsSource> News = new List<OsNewsSource>();

        internal static void AddNews(List<NewsStory> into, int day)
        {
            int room = MaxModStories;
            foreach (OsNewsSource source in News.ToArray())
            {
                if (room == 0) return;
                room -= Run(source, day, into, room);
            }
        }

        internal static void ClearMod(string modId)
        {
            News.RemoveAll(x => x.ModId == modId);
            ChatData.Rooms.RemoveAll(r => OwnsRoom(modId, r));
            ShopCatalog.ClearMod(modId);
            ShopCustomGates.RemoveMod(modId);
        }

        internal static bool OwnsRoom(string modId, ChatRoomDef room)
        {
            return room.Id.StartsWith(modId + "/", StringComparison.Ordinal);
        }

        internal static string RoomName(string name)
        {
            string clean = Clean(name, MaxName);
            if (clean.Length == 0) return null;
            return clean[0] == '#' ? clean : Clean("# " + clean, MaxName);
        }

        internal static string[] Texts(string[] values, int max)
        {
            List<string> result = new List<string>();
            if (values == null) return result.ToArray();
            foreach (string value in values)
            {
                string text = Clean(value, max);
                if (text.Length > 0) result.Add(text);
            }
            return result.ToArray();
        }

        private static int Run(OsNewsSource source, int day, List<NewsStory> into, int room)
        {
            try
            {
                OsNewsStory[] written = source.Source(day);
                if (written == null) return 0;
                int added = 0;
                foreach (OsNewsStory story in written)
                {
                    if (added == Math.Min(MaxStories, room)) break;
                    string headline = story == null ? "" : Clean(story.Headline, MaxHeadline);
                    if (headline.Length == 0) continue;
                    into.Add(new NewsStory { Section = source.Section, Headline = headline, Body = Clean(story.Body, MaxBody) });
                    added++;
                }
                return added;
            }
            catch (Exception e)
            {
                Os.Crash("news source " + source.ModId, e);
                return 0;
            }
        }

        private static string Clean(string text, int max)
        {
            if (text == null) return "";
            string clean = Regex.Replace(text, @"\p{Cc}", "").Trim();
            return clean.Length <= max ? clean : clean.Substring(0, max);
        }
    }
}
