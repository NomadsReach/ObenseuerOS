namespace ObenseuerOS
{
    internal sealed class ExplorerEntry
    {
        internal readonly string Path;
        internal readonly int Bytes;
        internal ExplorerEntry(string path, int bytes) { Path = path; Bytes = bytes; }
    }

    internal interface ExplorerStorage
    {
        string[] Folders();
        ExplorerEntry[] ListFiles(string folder);
        string CreateDocument();
        void DeleteFile(string path);
    }
}
