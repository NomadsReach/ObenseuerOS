using System;
using System.Collections.Generic;
using ObenseuerOS;

internal static class Program
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    private static void Refused(Action action, string message)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new Exception(message);
    }

    private static void Main()
    {
        OsNotepadStorage.Attach();
        var storage = new OsNotepadStorage();
        Check(storage.Valid("/docs/notes.txt") && !storage.Valid("/other/notes.txt") && !storage.Valid("/docs/no extension"), "document names");
        storage.Write("/docs/notes.txt", "kept");
        Check(storage.Read("/docs/notes.txt") == "kept" && storage.List().Length == 1, "save and list");
        storage.Opened("/docs/notes.txt");
        Check(Os.LastOpened == "/docs/notes.txt", "file-open notification");
        OsBridge.Busy = true;
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "busy write");
        OsBridge.Busy = false;
        Refused(() => storage.Write("/docs/notes.txt", new string('é', OsFiles.MaxFileBytes)), "UTF-8 size");
        Refused(() => storage.Write("/recycle/1.txt", "lost"), "recycle write");
        Refused(() => storage.Read("/vault/secret.txt"), "vault read");
        OsFiles.Full = true;
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "full disk");
        Check(storage.Read("/docs/notes.txt") == "kept", "failed saves preserve content");
        OsFiles.Full = false;
        ObenseuerCore.SaveData.Busy = true;
        Refused(() => storage.Read("/docs/notes.txt"), "read during load");
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "write during load");
        ObenseuerCore.SaveData.Busy = false;
        ObenseuerCore.Events.RaiseLoad();
        Refused(() => storage.List(), "stale list");
        Refused(() => storage.Read("/docs/notes.txt"), "stale read");
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "stale write");
        Refused(() => storage.Opened("/docs/notes.txt"), "stale open event");
        storage = new OsNotepadStorage();
        Check(storage.Read("/docs/notes.txt") == "kept", "new session keeps disk intact");
        ObenseuerCore.Events.RaiseNewGame();
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "stale after new game");
        storage = new OsNotepadStorage();
        ObenseuerCore.Events.RaiseScene("Main Menu");
        Refused(() => storage.Read("/docs/notes.txt"), "stale after main menu");
    }
}

namespace ObenseuerCore
{
    internal static class SaveData
    {
        internal static bool Busy;
    }

    internal static class Events
    {
        internal static event Action<string> AfterLoad;
        internal static event Action NewGameStarted;
        internal static event Action<string> SceneLoaded;
        internal static void RaiseLoad() { if (AfterLoad != null) AfterLoad("save"); }
        internal static void RaiseNewGame() { if (NewGameStarted != null) NewGameStarted(); }
        internal static void RaiseScene(string scene) { if (SceneLoaded != null) SceneLoaded(scene); }
    }
}

namespace ObenseuerOS
{
    internal static class OsBridge
    {
        internal static bool Busy;
        internal static void CheckWritable() { if (Busy) throw new InvalidOperationException("busy"); }
    }

    internal static class Os
    {
        internal static string LastOpened;
        internal static void RaiseFileOpened(string path) { LastOpened = path; }
    }

    internal static class OsFiles
    {
        internal const int MaxFileBytes = 256 * 1024;
        internal static bool Full;
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        internal static bool ValidPath(string path) { return OsPaths.ValidPath(path); }
        internal static bool InVault(string path) { return path.StartsWith("/vault/"); }
        internal static bool InBin(string path) { return path.StartsWith("/recycle/"); }
        internal static string Read(string path) { string value; return Files.TryGetValue(path, out value) ? value : null; }
        internal static bool Write(string path, string value) { if (Full) return false; Files[path] = value; return true; }
        internal static string[] List(string dir) { return new List<string>(Files.Keys).FindAll(p => p.StartsWith(dir + "/")).ToArray(); }
    }
}
