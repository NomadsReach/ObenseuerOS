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
        var storage = OsNotepadStorage.Instance;
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
