namespace ObenseuerOS
{
    internal sealed class RecycleEntry
    {
        internal readonly string Path;
        internal readonly string Origin;
        internal readonly string Deleted;

        internal RecycleEntry(string path, string origin, string deleted)
        {
            Path = path;
            Origin = origin;
            Deleted = deleted;
        }
    }

    internal interface RecycleStorage
    {
        RecycleEntry[] ListBin();
        string Restore(string path);
        void Purge(string path);
        void EmptyBin();
    }
}
