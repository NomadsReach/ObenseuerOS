namespace ObenseuerOS
{
    internal interface NotepadStorage
    {
        string[] List();
        string Read(string path);
        void Write(string path, string value);
        bool Valid(string path);
        void Opened(string path);
    }
}
