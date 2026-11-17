using System;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("Chat history check failed"); }
    private static void Main()
    {
        ChatRoom room = new ChatRoom();
        for (int i = 0; i < 100; i++) room.Lines.Add(new ChatLine { Seq = i, Text = i.ToString() });
        room.Next = 100;
        var all = ChatHistory.Since(room, 0);
        Check(all.Count == 100 && all[0].Seq == 0 && all[99].Seq == 99);
        var fresh = ChatHistory.Since(room, 96);
        Check(fresh.Count == 4 && fresh[0].Seq == 96 && fresh[3].Seq == 99);
        Check(ChatHistory.Since(room, room.Next).Count == 0);
        room.Lines.RemoveRange(0, 40);
        Check(ChatHistory.Since(room, 0).Count == 60 && ChatHistory.Since(room, 0)[0].Seq == 40);
        Console.WriteLine("Chat history checks passed");
    }
}
