using System.Text;

namespace ObenseuerOS
{
    internal static class OsModFiles
    {
        internal static OsResult Write(string modId, string name, string content)
        {
            string path = OsScope.Resolve(modId, name);
            if (path == null) return Os.Refuse("WriteFile", modId, OsResult.Invalid, "path must be a plain file name like notes.txt (letters, digits, space, _ - . ( ), with an extension), not a folder or absolute path");
            if (content == null) return Os.Refuse("WriteFile", modId, OsResult.Invalid, "content must not be null");
            int add = Encoding.UTF8.GetByteCount(content);
            if (add > OsFiles.MaxFileBytes) return Os.Refuse("WriteFile", modId, OsResult.TooLarge, "a file is at most " + OsFiles.MaxFileBytes + " bytes of UTF-8");
            if (!OsBridge.Writable()) return OsResult.Busy;
            if (!OsBridge.WorldReady()) return Os.Refuse("WriteFile", modId, OsResult.Unavailable, "no save is loaded yet, write files once the player is in the game");
            int files = 0;
            int bytes = 0;
            int old = 0;
            bool exists = false;
            foreach (string other in OsFiles.List(OsScope.Folder(modId)))
            {
                int size = Encoding.UTF8.GetByteCount(OsFiles.Read(other));
                files++;
                bytes += size;
                if (other != path) continue;
                exists = true;
                old = size;
            }
            if (!OsScope.Fits(files, bytes, exists, old, add)) return Os.Refuse("WriteFile", modId, OsResult.Full, "a mod may keep " + OsScope.MaxModFiles + " files and " + OsScope.MaxModBytes + " bytes");
            return OsFiles.Write(path, content) ? OsResult.Ok : Os.Refuse("WriteFile", modId, OsResult.Full, "the OS disk is full");
        }

        internal static string Read(string modId, string name)
        {
            string path = OsScope.Resolve(modId, name);
            if (path == null)
            {
                Os.Refuse("ReadFile", modId, OsResult.Invalid, "path must be a plain file name like notes.txt");
                return null;
            }
            return OsFiles.Read(path);
        }

        internal static OsResult Open(string modId, string name)
        {
            string path = OsScope.Resolve(modId, name);
            if (path == null) return Os.Refuse("OpenFile", modId, OsResult.Invalid, "path must be a plain file name like notes.txt");
            if (!OsBridge.WorldReady()) return Os.Refuse("OpenFile", modId, OsResult.Unavailable, "no save is loaded yet, open files once the player is in the game");
            if (OsFiles.Read(path) == null) return OsResult.Missing;
            if (!Os.IsAvailable) return Os.Refuse("OpenFile", modId, OsResult.Unavailable, "the OS view is not available");
            return OsView.OpenFile(path) ? OsResult.Ok : OsResult.Queued;
        }
    }
}
