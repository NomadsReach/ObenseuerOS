using System.Text;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class ChatSelfTest
    {
        internal static string Run()
        {
            if (!Fits(new string('"', ChatData.MaxText))) return "200 quote lines overflow the chat.since result";
            if (!Fits(new string((char)0x4e2d, ChatData.MaxText))) return "200 CJK lines overflow the chat.since result";
            string cleaned = ChatBridge.Body(JObject.Parse("{\"text\":\"a\\u0001b\\r\\nc\\u2028d\\t\\t e \"}"));
            if (cleaned != "a b c d e") return "Body does not strip control characters";
            return null;
        }

        private static bool Fits(string text)
        {
            ChatRoom room = new ChatRoom { Id = "internet", Next = ChatData.MaxReturn };
            for (int i = 0; i < ChatData.MaxReturn; i++)
            {
                room.Lines.Add(new ChatLine { Seq = i, From = "Test", Text = text, Own = false });
            }
            return Encoding.UTF8.GetByteCount(ChatBridge.SinceJson(room, 0)) < OsBridge.MaxResultBytes;
        }
    }
}
