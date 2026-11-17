using System.Collections.Generic;

namespace ObenseuerOS
{
    internal static class ChatHistory
    {
        internal static List<ChatLine> Since(ChatRoom room, int from)
        {
            List<ChatLine> lines = new List<ChatLine>();
            foreach (ChatLine line in room.Lines)
                if (line.Seq >= from) lines.Add(line);
            return lines;
        }
    }
}
