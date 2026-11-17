using System;
using System.IO;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool value) { if (!value) throw new Exception("News check failed"); }
    private static void Main()
    {
        string data = "{ file: \"photos/a-1.jpg\", thumb: \"photos/a-1.thumb.jpg\", tags: [\"scene\", \"weather\"] },\n" +
            "{ file: \"photos/b-1.jpg\", thumb: \"photos/b-1.thumb.jpg\", tags: [\"scene\", \"football\"] }";
        NewsPhotos photos = new NewsPhotos(data);
        Check(photos.Pick("Weather", 0, 0, false) == "a-1.jpg");
        Check(photos.Pick("Sport", 0, 0, true) == "b-1.thumb.jpg");
        Check(photos.Pick("Markets", 0, 1, false) == "b-1.jpg");
        Check(photos.Pick("Markets", int.MinValue, 0, false) == "a-1.jpg");
        Check(new NewsPhotos("").Pick("Local", 0, 0, false) == null);
        Check(new NewsPhotos("{ file: \"photos/../bad.jpg\", tags: [\"scene\"] }").Pick("Local", 0, 0, false) == null);
        NewsPhotos packaged = new NewsPhotos(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "photos-data.js")));
        Check(packaged.Pick("Weather", 0, 0, false) == "game-08.jpg");
        Check(packaged.Pick("Sport", 0, 0, true) == "football-1.thumb.jpg");
        Check(packaged.Pick("Lottery", 0, 0, false) == "lottery-2.jpg");
        Check(packaged.Pick("Markets", 0, 0, false) == "game-01.jpg");
        Check(packaged.PickTag("industry", 0, true) == "game-05.thumb.jpg");
        bool busy = false; int calls = 0;
        NewsClient client = new NewsClient(request => { calls++; Check((string)request["app"] == "news"); return JObject.Parse("{\"ok\":true,\"result\":{\"days\":[]}}"); }, () => { if (busy) throw new InvalidOperationException("stale"); });
        Check(client.Available && !new NewsClient(null, null).Available);
        Check(client.Call("news.editions", null)["days"].Type == JTokenType.Array && calls == 1);
        busy = true;
        try { client.Call("news.editions", null); throw new Exception("guard ignored"); } catch (InvalidOperationException) { Check(calls == 1); }
        Console.WriteLine("News checks passed");
    }
}
