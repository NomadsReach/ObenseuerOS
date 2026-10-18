using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    [Serializable]
    public class ChatSave
    {
        public List<ChatRoom> Rooms = new List<ChatRoom>();
        public List<ChatPending> Pending = new List<ChatPending>();
    }

    [Serializable]
    public class ChatRoom
    {
        public string Id;
        public int Next;
        public int Seen = -1;
        public List<ChatLine> Lines = new List<ChatLine>();
    }

    [Serializable]
    public class ChatLine
    {
        public int Seq;
        public string From;
        public string Text;
        public bool Own;
    }

    [Serializable]
    public class ChatPending
    {
        public string Room;
        public string From;
        public string Text;
        public int Due;
    }
}
