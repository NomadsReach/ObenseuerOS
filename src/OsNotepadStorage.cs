using System;
using System.Text;

namespace ObenseuerOS
{
    internal sealed class OsNotepadStorage : NotepadStorage
    {
        internal static readonly OsNotepadStorage Instance = new OsNotepadStorage();

        public string[] List() { return OsFiles.List("/docs"); }

        public string Read(string path)
        {
            if (!OsFiles.ValidPath(path) || OsFiles.InVault(path) || OsFiles.InBin(path)) throw new InvalidOperationException("invalid file path");
            string value = OsFiles.Read(path);
            if (value == null) throw new InvalidOperationException("no such file");
            return value;
        }

        public void Write(string path, string value)
        {
            OsBridge.CheckWritable();
            if (!OsFiles.ValidPath(path) || OsFiles.InVault(path) || OsFiles.InBin(path)) throw new InvalidOperationException("invalid file path");
            if (Encoding.UTF8.GetByteCount(value) > OsFiles.MaxFileBytes) throw new InvalidOperationException("the file is over 256 KB");
            if (!OsFiles.Write(path, value)) throw new InvalidOperationException("the disk is full or has too many files");
        }

        public bool Valid(string path) { return OsFiles.ValidPath(path) && path.StartsWith("/docs/", StringComparison.Ordinal); }

        public void Opened(string path) { Os.RaiseFileOpened(path); }
    }
}
