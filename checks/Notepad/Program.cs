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
        Check(ToolkitNotepadOpen.Decide("/docs/one.txt", "/docs/one.txt", true) == ExternalOpenAction.Focus, "same file retains edits");
        Check(ToolkitNotepadOpen.Decide("/docs/one.txt", "/docs/two.txt", true) == ExternalOpenAction.Confirm, "dirty file needs confirmation");
        Check(ToolkitNotepadOpen.Decide("/docs/one.txt", "/docs/two.txt", false) == ExternalOpenAction.Load, "clean file opens");
        Check(ToolkitNotepadOpen.Decide(null, "/docs/two.txt", true) == ExternalOpenAction.Confirm, "dirty untitled file needs confirmation");
        CloseConfirmation notesClose = new CloseConfirmation(), mailClose = new CloseConfirmation();
        Check(!notesClose.Allow(true, "note") && notesClose.Allow(true, "note") && !mailClose.Allow(true, 1L), "separate dirty app prompts");
        Check(notesClose.Allow(true, "note") && mailClose.Allow(true, 1L), "prior approval survives another app veto");
        mailClose.Reset(); Check(!mailClose.Allow(true, 1L) && mailClose.Allow(true, 1L), "actual close requires fresh confirmation when reopened");
        Check(!notesClose.Allow(true, "edited") && !mailClose.Allow(true, 2L), "new edits require renewed confirmation");
        Check(notesClose.Allow(false, "edited") && !notesClose.Allow(true, "edited"), "clean state resets confirmation");
        OsNotepadStorage.Attach();
        ObenseuerCore.SaveData.Folder = null;
        OsNotepadStorage newGame = new OsNotepadStorage();
        RecoveredNote newGameDraft = NotepadRecovery.Retain(newGame.RecoveryKey, newGame.RecoveryOrigin, "new game draft", "");
        ObenseuerCore.SaveData.Folder = "save-a";
        ObenseuerCore.Events.RaiseLoad();
        var storage = new OsNotepadStorage();
        string owner = storage.RecoveryKey;
        Check(NotepadRecovery.List(owner, false).Count == 0 && NotepadRecovery.List(owner, true).Contains(newGameDraft), "new game draft requires explicit other-session recovery");
        Check(!NotepadRecovery.Claim(newGameDraft, owner, false) && NotepadRecovery.Claim(newGameDraft, owner, true) && !NotepadRecovery.Claim(newGameDraft, owner, true), "explicit recovery claims once");
        RecoveredNote oldGeneration = NotepadRecovery.Retain(owner, storage.RecoveryOrigin, "old generation", "");
        ObenseuerCore.Events.RaiseLoad();
        storage = new OsNotepadStorage(); owner = storage.RecoveryKey;
        Check(NotepadRecovery.List(owner, false).Count == 0 && NotepadRecovery.Claim(oldGeneration, owner, true), "reused save folder requires explicit recovery");
        ObenseuerCore.SaveData.Folder = "save-b";
        Check(storage.RecoveryKey == owner && new OsNotepadStorage().RecoveryKey != owner, "recovery belongs to the original save");
        Check(NotepadRecovery.Retain(owner, "save-a", "same", "same") == null, "clean text is not retained");
        RecoveredNote firstDraft = NotepadRecovery.Retain(owner, "save-a", "changed", "original");
        RecoveredNote deletedText = NotepadRecovery.Retain(owner, "save-a", "", "previous contents");
        Check(NotepadRecovery.List(owner, false).Count == 2 && NotepadRecovery.List(owner, false)[0] == firstDraft, "drafts remain available in order");
        Check(deletedText != null && deletedText.Text == "", "empty unsaved edit is recoverable");
        Check(NotepadRecovery.Retain(owner, "save-a", "", null) != null && NotepadRecovery.Retain(owner, "save-a", "original", null) != null, "recovered untitled text remains dirty");
        Check(!NotepadRecovery.Claim(firstDraft, new OsNotepadStorage().RecoveryKey, false), "another save cannot claim current-session recovery");
        RecoveredNote resumed = NotepadRecovery.Retain(owner, "save-a", "pending", "");
        NotepadRecovery.Remove(resumed);
        Check(!NotepadRecovery.List(owner, false).Contains(resumed), "reattached host removes its pending recovery");
        int firstSession = storage.SessionId;
        Check(new OsNotepadStorage().SessionId == firstSession, "reopened apps share session");
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
        OsFiles.AddBin("/recycle/1.txt", "/docs/old.txt");
        Check(storage.ListBin().Length == 1, "bin list");
        OsBridge.Busy = true;
        Refused(() => storage.Purge("/recycle/1.txt"), "busy purge");
        Refused(() => storage.EmptyBin(), "busy empty");
        Check(storage.ListBin().Length == 1, "busy bin unchanged");
        OsBridge.Busy = false;
        OsFiles.RestoreResult = OsRestore.Full;
        Refused(() => storage.Restore("/recycle/1.txt"), "full restore");
        Check(storage.ListBin().Length == 1, "failed restore preserves item");
        OsFiles.RestoreResult = OsRestore.OwnerFull;
        Refused(() => storage.Restore("/recycle/1.txt"), "owner full restore");
        Check(storage.ListBin().Length == 1, "owner full restore preserves item");
        OsFiles.RestoreResult = OsRestore.NoName;
        Refused(() => storage.Restore("/recycle/1.txt"), "restore without name");
        Check(storage.ListBin().Length == 1, "restore without name preserves item");
        OsFiles.RestoreResult = OsRestore.Done;
        Check(storage.Restore("/recycle/1.txt") == "/docs/old.txt" && storage.ListBin().Length == 0, "restore bin file");
        OsFiles.AddBin("/recycle/2.txt", "/docs/another.txt");
        storage.Purge("/recycle/2.txt");
        Check(storage.ListBin().Length == 0, "purge bin file");
        OsFiles.AddBin("/recycle/3.txt", "/docs/third.txt");
        storage.EmptyBin();
        Check(storage.ListBin().Length == 0, "empty bin");
        storage.Write("/docs/new-1.txt", "existing");
        string created = storage.CreateDocument();
        Check(created == "/docs/new-2.txt" && storage.Read("/docs/new-1.txt") == "existing", "new name does not overwrite");
        Check(storage.ListFiles("/docs").Length >= 2 && storage.Folders().Length == 0, "explorer listing");
        Refused(() => storage.ListFiles("/vault"), "vault listing");
        storage.Write("/downloads/locked.txt", "kept");
        Refused(() => storage.DeleteFile("/downloads/locked.txt"), "downloads read only");
        OsBridge.Busy = true;
        Refused(() => storage.DeleteFile(created), "busy delete");
        Refused(() => storage.CreateDocument(), "busy create");
        OsBridge.Busy = false;
        storage.DeleteFile(created);
        Check(storage.Read("/docs/new-1.txt") == "existing" && storage.ListBin().Length == 1, "delete moves selected file to bin");
        ObenseuerCore.SaveData.Busy = true;
        Check(storage.SessionCurrent, "saving does not invalidate the session");
        Refused(() => storage.CheckSession(), "chat session during load");
        Refused(() => storage.Read("/docs/notes.txt"), "read during load");
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "write during load");
        ObenseuerCore.SaveData.Busy = false;
        ObenseuerCore.Events.RaiseLoad();
        Check(new OsNotepadStorage().SessionId != firstSession, "loaded save gets a new session");
        Check(!storage.SessionCurrent, "loaded save invalidates the old session");
        Refused(() => storage.CheckSession(), "stale chat session");
        Refused(() => storage.List(), "stale list");
        Refused(() => storage.Read("/docs/notes.txt"), "stale read");
        Refused(() => storage.Write("/docs/notes.txt", "lost"), "stale write");
        Refused(() => storage.Opened("/docs/notes.txt"), "stale open event");
        Refused(() => storage.ListBin(), "stale bin list");
        Refused(() => storage.EmptyBin(), "stale empty bin");
        Refused(() => storage.CreateDocument(), "stale create");
        Refused(() => storage.DeleteFile("/docs/new-1.txt"), "stale delete");
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
        internal static string Folder;
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
    internal enum OsRestore { Done, Missing, NoName, Full, OwnerFull }
    internal sealed class OsFile
    {
        internal string path;
        internal string origin;
        internal string deleted;
    }

    internal static class OsBridge
    {
        internal static bool Busy;
        internal static void CheckWritable() { if (Busy) throw new InvalidOperationException("busy"); }
        internal static string Stamp() { return "time"; }
    }

    internal static class Os
    {
        internal static string LastOpened;
        internal static void RaiseFileOpened(string path) { LastOpened = path; }
    }

    internal static class OsFiles
    {
        internal const int MaxFileBytes = 256 * 1024;
        internal const int MaxFiles = 1024;
        internal static bool Full;
        internal static OsRestore RestoreResult = OsRestore.Done;
        private static readonly Dictionary<string, string> Files = new Dictionary<string, string>();
        private static readonly Dictionary<string, OsFile> Bin = new Dictionary<string, OsFile>();
        internal static bool ValidPath(string path) { return OsPaths.ValidPath(path); }
        internal static bool ValidDir(string path) { return OsPaths.ValidDir(path); }
        internal static bool InVault(string path) { return path.StartsWith("/vault/"); }
        internal static bool InBin(string path) { return path.StartsWith("/recycle/"); }
        internal static string Read(string path) { string value; return Files.TryGetValue(path, out value) ? value : null; }
        internal static bool Write(string path, string value) { if (Full) return false; Files[path] = value; return true; }
        internal static string[] List(string dir) { return new List<string>(Files.Keys).FindAll(p => p.StartsWith(dir + "/")).ToArray(); }
        internal static string[] ModFolders() { return new string[0]; }
        internal static bool Delete(string path, string deleted)
        {
            if (!Files.Remove(path)) return false;
            Bin["/recycle/test.txt"] = new OsFile { path = "/recycle/test.txt", origin = path, deleted = deleted };
            return true;
        }
        internal static void AddBin(string path, string origin) { Bin[path] = new OsFile { path = path, origin = origin }; }
        internal static OsFile[] Trashed() { return new List<OsFile>(Bin.Values).ToArray(); }
        internal static OsRestore Restore(string path, out string target)
        {
            if (RestoreResult != OsRestore.Done) { target = null; return RestoreResult; }
            OsFile file;
            if (!Bin.TryGetValue(path, out file)) { target = null; return OsRestore.Missing; }
            target = file.origin;
            Bin.Remove(path);
            Files[target] = "";
            return OsRestore.Done;
        }
        internal static bool Purge(string path) { return Bin.Remove(path); }
        internal static void EmptyBin() { Bin.Clear(); }
    }
}
