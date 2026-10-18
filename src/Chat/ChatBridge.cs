using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class ChatBridge
    {
        private const string AppId = "chat";

        internal static void Register()
        {
            Os.Handle(AppId, "chat.since", Since);
            Os.Handle(AppId, "chat.send", Send);
        }

        private static string Since(string args)
        {
            JObject a = OsBridge.Args(args);
            ChatRoomDef def = RoomArg(a);
            int since = OsBridge.Span(a, "from", int.MaxValue, 0);
            return SinceJson(ChatGame.Open(def.Id), since);
        }

        internal static string SinceJson(ChatRoom room, int since)
        {
            List<object> lines = new List<object>();
            int bytes = 0;
            for (int i = room.Lines.Count - 1; i >= 0 && lines.Count < ChatData.MaxReturn; i--)
            {
                if (room.Lines[i].Seq < since) break;
                object line = LineJson(room.Lines[i]);
                int size = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(line)) + 1;
                if (bytes + size > ChatData.MaxReturnBytes) break;
                bytes += size;
                lines.Add(line);
            }
            lines.Reverse();
            return JsonConvert.SerializeObject(new { room = room.Id, next = room.Next, lines = lines, rooms = Rooms() });
        }

        private static string Send(string args)
        {
            JObject a = OsBridge.Args(args);
            ChatRoomDef def = RoomArg(a);
            string text = Body(a);
            if (!WorldClock.IsAvailable) throw new OsBridgeException("failed", "the clock is not ready");
            OsBridge.CheckWritable();
            ChatGame.Send(def, text, WorldClock.Now);
            return "true";
        }

        private static ChatRoomDef RoomArg(JObject a)
        {
            ChatRoomDef def = ChatData.Find(OsBridge.Text(a, "room"));
            if (def == null) throw new OsBridgeException("bad_args", "room is not a known chat room");
            return def;
        }

        internal static string Body(JObject a)
        {
            string text = OsBridge.Text(a, "text");
            if (text == null) throw new OsBridgeException("bad_args", "text must be a string");
            text = Regex.Replace(text, @"[\p{Cc}\s]+", " ").Trim();
            if (text.Length == 0 || text.Length > ChatData.MaxText) throw new OsBridgeException("bad_args", "text must be 1 to 200 characters");
            return text;
        }

        private static List<object> Rooms()
        {
            List<object> rows = new List<object>();
            foreach (ChatRoomDef def in ChatData.Rooms)
            {
                rows.Add(new { id = def.Id, name = def.Name, contact = def.Contact, unread = ChatGame.Unread(def.Id) });
            }
            return rows;
        }

        private static object LineJson(ChatLine line)
        {
            return new { seq = line.Seq, from = line.From, text = line.Text, own = line.Own };
        }
    }
}
