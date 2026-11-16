using System;
using System.Text;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class OsNotepadStorage : NotepadStorage
    {
        private static int generation;
        private readonly int session = generation;

        internal static void Attach()
        {
            Events.AfterLoad += delegate(string folder) { generation++; };
            Events.NewGameStarted += delegate { generation++; };
            Events.SceneLoaded += delegate(string scene) { if (scene == "Main Menu") generation++; };
        }

        private void CheckSession()
        {
            if (session != generation) throw new InvalidOperationException("the game session changed; reopen Notepad");
            if (SaveData.Busy) throw new InvalidOperationException("the game is saving or loading");
        }

        public string[] List() { CheckSession(); return OsFiles.List("/docs"); }

        public string Read(string path)
        {
            CheckSession();
            if (!OsFiles.ValidPath(path) || OsFiles.InVault(path) || OsFiles.InBin(path)) throw new InvalidOperationException("invalid file path");
            string value = OsFiles.Read(path);
            if (value == null) throw new InvalidOperationException("no such file");
            return value;
        }

        public void Write(string path, string value)
        {
            CheckSession();
            OsBridge.CheckWritable();
            if (!OsFiles.ValidPath(path) || OsFiles.InVault(path) || OsFiles.InBin(path)) throw new InvalidOperationException("invalid file path");
            if (Encoding.UTF8.GetByteCount(value) > OsFiles.MaxFileBytes) throw new InvalidOperationException("the file is over 256 KB");
            if (!OsFiles.Write(path, value)) throw new InvalidOperationException("the disk is full or has too many files");
        }

        public bool Valid(string path) { return OsFiles.ValidPath(path) && path.StartsWith("/docs/", StringComparison.Ordinal); }

        public void Opened(string path) { CheckSession(); Os.RaiseFileOpened(path); }
    }
}
