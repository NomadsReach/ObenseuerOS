using System;
using System.IO;
using Newtonsoft.Json.Linq;
using ObenseuerOS;

internal sealed class Storage : NotepadStorage
{
    internal string Value;
    internal bool FailRead, FailWrite;
    public string[] List() { return new string[0]; }
    public string Read(string path) { if (FailRead) throw new InvalidOperationException("session changed"); if (Value == null) throw new InvalidOperationException("no such file"); return Value; }
    public void Write(string path, string value) { if (FailWrite) throw new InvalidOperationException("disk full"); Value = value; }
    public bool Valid(string path) { return true; }
    public void Opened(string path) { }
}
internal static class Program
{
    private static void Check(bool ok) { if (!ok) throw new Exception("Browser state check failed"); }
    private static void Main()
    {
        Storage storage = new Storage { Value = "{\"marks\":[{\"url\":\"mod.ts\",\"title\":\"Mod\"}],\"history\":[{\"url\":\"old.ts\",\"title\":\"Old\"}],\"extra\":42}" };
        BrowserState state = new BrowserState(storage);
        Check(state.Current == "old.ts" && state.Marks.Count == 1);
        Check(state.Visit("new.ts", "New")); Check(!state.Visit("new.ts", "New") && !state.Visit("", "Empty")); state.ToggleMark("New");
        Check(state.CanBack && state.IsMarked && (int)JObject.Parse(storage.Value)["extra"] == 42);
        state.Move(-1); Check(state.Current == "old.ts" && state.CanForward);
        state.Visit("third.ts", "Third"); Check(!state.CanForward && state.History.Count == 2);
        BrowserState reloaded = new BrowserState(storage); Check(reloaded.Current == "third.ts" && reloaded.Marks.Count == 2);
        string broken = "not json"; storage.Value = broken;
        try { new BrowserState(storage); throw new Exception("expected invalid history"); } catch (InvalidOperationException) { }
        Check(storage.Value == broken);
        storage.FailRead = true;
        try { new BrowserState(storage); throw new Exception("expected read failure"); } catch (InvalidOperationException) { }
        Check(storage.Value == broken);
        storage.FailRead = false; storage.Value = null;
        BrowserState fresh = new BrowserState(storage); storage.FailWrite = true;
        try { fresh.Visit("one.ts", "One"); throw new Exception("expected write failure"); } catch (InvalidOperationException) { }
        Check(storage.Value == null && fresh.Current == "" && fresh.History.Count == 0);
        Storage eyeDisk = new Storage { Value = "{\"name\":\"Ricky\",\"acts\":[{\"k\":\"like\",\"p\":1}],\"status\":[{\"t\":\"Hello\"}],\"extra\":7}" };
        EyeState eye = new EyeState(eyeDisk); Check(eye.Name == "Ricky" && eye.Has("like", 1) && eye.Statuses.Count == 1);
        eye.Like(1); Check(!eye.Has("like", 1)); eye.Share(2); eye.Comment(2, "Hello there"); eye.Publish("New status");
        Check(eye.Has("share", 2) && eye.Actions.Count == 2 && eye.Statuses.Count == 2 && (int)JObject.Parse(eyeDisk.Value)["extra"] == 7);
        try { eye.Share(2); throw new Exception("expected duplicate share"); } catch (InvalidOperationException) { }
        string before = eyeDisk.Value; eyeDisk.FailWrite = true;
        try { eye.Like(0); throw new Exception("expected write failure"); } catch (InvalidOperationException) { }
        Check(!eye.Has("like", 0) && eyeDisk.Value == before);
        eyeDisk.FailWrite = false; eyeDisk.Value = "bad data";
        try { new EyeState(eyeDisk); throw new Exception("expected invalid data"); } catch (InvalidOperationException) { }
        Check(eyeDisk.Value == "bad data");
        JArray tooMany = new JArray(); for (int i = 0; i < 41; i++) tooMany.Add(new JObject(new JProperty("k", "like"), new JProperty("p", 0)));
        eyeDisk.Value = new JObject(new JProperty("name", "Ricky"), new JProperty("acts", tooMany), new JProperty("status", new JArray())).ToString();
        string oversized = eyeDisk.Value;
        try { new EyeState(eyeDisk); throw new Exception("expected oversized data rejection"); } catch (InvalidOperationException) { }
        Check(eyeDisk.Value == oversized);
        Storage sharedDisk = new Storage(); EyeState first = EyeStates.ForSession(71, sharedDisk); EyeState second = EyeStates.ForSession(71, sharedDisk);
        Check(object.ReferenceEquals(first, second)); first.Login("Ricky"); second.Publish("Shared"); Check(first.Statuses.Count == 1);
        Check(!object.ReferenceEquals(first, EyeStates.ForSession(72, sharedDisk)));
        OfflineWikiPage wiki = OfflineWiki.Parse("OBXWIKI1\ntitle:Main_Page\nrev:3\nfetched:4\n\n<h1>Welcome</h1><script>bad()</script><p>Tea &amp; turnips</p>");
        Check(wiki != null && wiki.Title == "Main Page" && wiki.Text.Contains("Tea & turnips") && !wiki.Text.Contains("bad()"));
        Check(OfflineWiki.Parse("OBXWIKI1\ntitle:Main_Page\nrev:no\nfetched:4\n\ntext") == null && OfflineWiki.Parse("bad") == null);
        bool wikiLimited; Check(OfflineWiki.Load(null, out wikiLimited).Count == 0 && !wikiLimited);
        string wikiFolder = Path.Combine(Path.GetTempPath(), "offline-wiki-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(wikiFolder);
        try
        {
            string wikiFile = Path.Combine(wikiFolder, "one.page");
            string cached = "OBXWIKI1\ntitle:Main_Page\nrev:3\nfetched:4\n\n<p>Saved &amp; safe</p>";
            File.WriteAllText(wikiFile, cached); File.WriteAllText(Path.Combine(wikiFolder, "bad.page"), "bad");
            var saved = OfflineWiki.Load(wikiFolder, out wikiLimited);
            Check(!wikiLimited && saved.Count == 1 && saved[0].Title == "Main Page" && saved[0].Text == "Saved & safe" && File.ReadAllText(wikiFile) == cached);
            for (int i = 0; i < 500; i++) File.WriteAllText(Path.Combine(wikiFolder, "page" + i + ".page"), "OBXWIKI1\ntitle:Page_" + i + "\nrev:1\nfetched:1\n\n<p>Text</p>");
            saved = OfflineWiki.Load(wikiFolder, out wikiLimited);
            Check(wikiLimited && saved.Count == 500 && File.ReadAllText(wikiFile) == cached);
        }
        finally { Directory.Delete(wikiFolder, true); }
        Console.WriteLine("Browser state checks passed");
    }
}
