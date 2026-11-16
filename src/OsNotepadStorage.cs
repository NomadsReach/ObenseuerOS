using System;
using System.Text;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class OsNotepadStorage : NotepadStorage, RecycleStorage, ExplorerStorage
    {
        private static int generation;
        private readonly int session = generation;

        internal static void Attach()
        {
            Events.AfterLoad += delegate(string folder) { generation++; };
            Events.NewGameStarted += delegate { generation++; };
            Events.SceneLoaded += delegate(string scene) { if (scene == "Main Menu") generation++; };
        }

        internal void CheckSession()
        {
            if (session != generation) throw new InvalidOperationException("the game session changed; reopen the OS");
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

        public RecycleEntry[] ListBin()
        {
            CheckSession();
            OsFile[] files = OsFiles.Trashed();
            RecycleEntry[] result = new RecycleEntry[files.Length];
            for (int i = 0; i < files.Length; i++)
                result[i] = new RecycleEntry(files[i].path, files[i].origin, files[i].deleted);
            return result;
        }

        public string Restore(string path)
        {
            CheckSession();
            OsBridge.CheckWritable();
            string target;
            switch (OsFiles.Restore(path, out target))
            {
                case OsRestore.Done: return target;
                case OsRestore.NoName: throw new InvalidOperationException("no free name for the file");
                case OsRestore.Full: throw new InvalidOperationException("the disk is full, so the file cannot be restored");
                case OsRestore.OwnerFull: throw new InvalidOperationException("the mod folder is full, so the file stays in the Recycle Bin");
                default: throw new InvalidOperationException("no such file in the Recycle Bin");
            }
        }

        public void Purge(string path)
        {
            CheckSession();
            OsBridge.CheckWritable();
            if (!OsFiles.Purge(path)) throw new InvalidOperationException("no such file in the Recycle Bin");
        }

        public void EmptyBin()
        {
            CheckSession();
            OsBridge.CheckWritable();
            OsFiles.EmptyBin();
        }

        public string[] Folders()
        {
            CheckSession();
            return OsFiles.ModFolders();
        }

        public ExplorerEntry[] ListFiles(string folder)
        {
            CheckSession();
            if (!OsFiles.ValidDir(folder) || folder == "/vault" || folder == "/recycle")
                throw new InvalidOperationException("invalid folder");
            string[] paths = OsFiles.List(folder);
            ExplorerEntry[] result = new ExplorerEntry[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                result[i] = new ExplorerEntry(paths[i], Encoding.UTF8.GetByteCount(OsFiles.Read(paths[i])));
            return result;
        }

        public string CreateDocument()
        {
            CheckSession();
            OsBridge.CheckWritable();
            for (int i = 1; i <= OsFiles.MaxFiles + 1; i++)
            {
                string path = "/docs/new-" + i + ".txt";
                if (OsFiles.Read(path) != null) continue;
                if (!OsFiles.Write(path, "")) throw new InvalidOperationException("the disk is full or has too many files");
                return path;
            }
            throw new InvalidOperationException("no free file name");
        }

        public void DeleteFile(string path)
        {
            CheckSession();
            OsBridge.CheckWritable();
            if (!OsFiles.ValidPath(path)) throw new InvalidOperationException("invalid file path");
            string folder = path.Substring(0, path.LastIndexOf('/'));
            if (folder != "/docs" && !folder.StartsWith("/mod-", StringComparison.Ordinal))
                throw new InvalidOperationException("this folder is read only");
            if (!OsFiles.Delete(path, OsBridge.Stamp())) throw new InvalidOperationException("no such file");
        }
    }
}
