using System;
using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class ChatGame
    {
        private const string StoreId = "nomad.obenseuer.os.chat";
        private const string FallbackPlayer = "Stalburg resident";
        private static readonly Random Picker = new Random();
        private static ChatSave save = new ChatSave();
        private static int burstMinute = -1;
        private static int burstCalls;

        internal static void Attach()
        {
            SaveData.Register<ChatSave>(StoreId, () => save, loaded => { save = loaded ?? new ChatSave(); Normalise(save); });
            Events.HourPassed += OnHour;
            ChatBridge.Register();
        }

        internal static void Tick()
        {
            if (save.Pending.Count == 0 || !WorldClock.IsAvailable) return;
            int now = LottoGame.Minutes(WorldClock.Now);
            if (!AnyDue(now) || !Writable()) return;
            for (int i = 0; i < save.Pending.Count;)
            {
                ChatPending p = save.Pending[i];
                if (p.Due > now)
                {
                    i++;
                    continue;
                }
                save.Pending.RemoveAt(i);
                Post(State(p.Room), p.From, p.Text, false);
            }
        }

        private static bool AnyDue(int now)
        {
            foreach (ChatPending p in save.Pending)
            {
                if (p.Due <= now) return true;
            }
            return false;
        }

        internal static void Send(ChatRoomDef def, string text, WorldTime now)
        {
            Post(State(def.Id), PlayerName(), text, true);
            Reply(def, text, now);
        }

        internal static ChatRoom Open(string id)
        {
            ChatRoom room = State(id);
            room.Seen = room.Next - 1;
            return room;
        }

        internal static int Unread(string id)
        {
            ChatRoom room = save.Rooms.Find(r => r.Id == id);
            return room == null ? 0 : room.Lines.FindAll(l => l.Seq > room.Seen).Count;
        }

        private static void OnHour(int hour)
        {
            if (!Writable()) return;
            WorldTime now = WorldClock.Now;
            int minute = LottoGame.Minutes(now);
            burstCalls = minute == burstMinute ? burstCalls + 1 : 1;
            burstMinute = minute;
            if (burstCalls > ChatData.MaxHourBurst) return;
            foreach (ChatRoomDef def in ChatData.Rooms)
            {
                if (def.Contact) continue;
                Post(State(def.Id), Pick(def.Members), Fill(Pick(def.Lines), hour, now.Weekday, null), false);
            }
        }

        private static void Reply(ChatRoomDef def, string text, WorldTime now)
        {
            int count = def.Contact ? 1 : 1 + Picker.Next(2);
            List<string> pool = new List<string>(def.Members);
            for (int i = 0; i < count && pool.Count > 0 && save.Pending.Count < ChatData.MaxPending; i++)
            {
                int k = Picker.Next(pool.Count);
                string from = pool[k];
                pool.RemoveAt(k);
                int due = LottoGame.Minutes(now) + ChatData.ReplyDelayMinutes + Picker.Next(ChatData.ReplyJitterMinutes);
                save.Pending.Add(new ChatPending { Room = def.Id, From = from, Text = Answer(text, now), Due = due });
            }
        }

        private static string Answer(string text, WorldTime now)
        {
            string lower = text.ToLowerInvariant();
            string[] replies = ChatData.Fallback;
            string subject = null;
            foreach (ChatTopic topic in ChatData.Topics)
            {
                subject = topic.Match(lower);
                if (subject == null) continue;
                replies = topic.Replies;
                break;
            }
            return Fill(Pick(replies), now.Hour, now.Weekday, subject);
        }

        private static string Fill(string template, int hour, DayOfWeek weekday, string subject)
        {
            string item = subject != null && Array.IndexOf(ChatData.Items, subject) >= 0 ? Cap(subject) : Cap(Pick(ChatData.Items));
            string place = subject != null && Array.IndexOf(ChatData.Places, subject) >= 0 ? Cap(subject) : Cap(Pick(ChatData.Places));
            return template.Replace("{item}", item).Replace("{place}", place).Replace("{time}", Period(hour)).Replace("{weather}", Weather()).Replace("{day}", weekday.ToString());
        }

        private static string Period(int hour)
        {
            if (hour < 5 || hour >= 22) return "night";
            if (hour < 12) return "morning";
            if (hour < 18) return "afternoon";
            return "evening";
        }

        private static string Weather()
        {
            if (!WorldWeather.IsAvailable) return "grey";
            return WorldWeather.IsSnowing ? "snowing" : WorldWeather.IsRaining ? "raining" : "dry";
        }

        private static string PlayerName()
        {
            string name = OsView.UserName();
            return name.Length > 0 ? name : FallbackPlayer;
        }

        private static void Post(ChatRoom room, string from, string text, bool own)
        {
            room.Lines.Add(new ChatLine { Seq = room.Next, From = from, Text = text, Own = own });
            room.Next++;
            if (room.Lines.Count > ChatData.MaxLines) room.Lines.RemoveAt(0);
        }

        private static ChatRoom State(string id)
        {
            ChatRoom room = save.Rooms.Find(r => r.Id == id);
            if (room == null)
            {
                room = new ChatRoom { Id = id };
                save.Rooms.Add(room);
            }
            return room;
        }

        private static string Pick(string[] items)
        {
            return items[Picker.Next(items.Length)];
        }

        private static string Cap(string s)
        {
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        private static void Normalise(ChatSave state)
        {
            state.Rooms = state.Rooms ?? new List<ChatRoom>();
            state.Rooms.RemoveAll(x => x == null);
            foreach (ChatRoom room in state.Rooms)
            {
                room.Id = room.Id ?? "";
                room.Lines = room.Lines ?? new List<ChatLine>();
                room.Lines.RemoveAll(x => x == null);
                foreach (ChatLine line in room.Lines)
                {
                    line.From = line.From ?? "";
                    line.Text = line.Text ?? "";
                }
            }
            state.Pending = state.Pending ?? new List<ChatPending>();
            state.Pending.RemoveAll(x => x == null);
            foreach (ChatPending pending in state.Pending)
            {
                pending.Room = pending.Room ?? "";
                pending.From = pending.From ?? "";
                pending.Text = pending.Text ?? "";
            }
        }

        private static bool Writable()
        {
            try
            {
                OsBridge.CheckWritable();
                return true;
            }
            catch (OsBridgeException)
            {
                return false;
            }
        }
    }
}
