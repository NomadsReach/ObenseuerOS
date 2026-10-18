using System;
using System.Collections.Generic;
using System.Text;
using ObenseuerCore;

namespace ObenseuerOS
{
    [Serializable]
    public class OsFile
    {
        public string path;
        public string text;
        public string origin;
        public string deleted;
        [NonSerialized] internal int bytes;
    }

    [Serializable]
    public class OsDisk
    {
        public List<OsFile> files = new List<OsFile>();
        public int binSeq;
        [NonSerialized] internal int total;
    }

    public enum OsRestore
    {
        Done,
        Missing,
        NoName,
        Full,
        OwnerFull
    }

    internal static class OsFiles
    {
        public const int MaxFileBytes = 256 * 1024;
        public const int MaxTotalBytes = 8 * 1024 * 1024;
        public const int MaxPathLength = OsPaths.MaxPathLength;
        public const int MaxFiles = 1024;
        private const int FileOverhead = 32;
        private const string BinDir = "/recycle";
        private const string VaultDir = "/vault";
        private static OsDisk disk = new OsDisk();

        public static string Read(string path)
        {
            OsFile file = Find(disk, path);
            return file == null ? null : file.text;
        }

        public static bool Write(string path, string text)
        {
            return Put(disk, path, text);
        }

        public static string[] List(string dir)
        {
            return dir == VaultDir ? new string[0] : ListAll(dir);
        }

        internal static string[] ListAll(string dir)
        {
            return ListIn(disk, dir);
        }

        internal static string[] ModFolders()
        {
            SortedSet<string> folders = new SortedSet<string>(StringComparer.Ordinal);
            foreach (OsFile f in disk.files)
            {
                string folder = OsScope.FolderOf(f.path);
                if (folder != null) folders.Add(folder);
            }
            return new List<string>(folders).ToArray();
        }

        internal static bool InVault(string path)
        {
            return path.StartsWith(VaultDir + "/", StringComparison.Ordinal);
        }

        internal static bool Move(string from, string to)
        {
            return Shift(disk, from, to);
        }

        internal static bool Discard(string path)
        {
            return !InBin(path) && Remove(disk, path);
        }

        public static bool Delete(string path, string deleted)
        {
            return Trash(disk, path, deleted);
        }

        public static OsFile[] Trashed()
        {
            return TrashedIn(disk).ToArray();
        }

        public static bool Purge(string path)
        {
            return Erase(disk, path);
        }

        public static OsRestore Restore(string path, out string target)
        {
            return Recover(disk, path, out target);
        }

        public static void EmptyBin()
        {
            Clear(disk);
        }

        public static int UsedBytes
        {
            get
            {
                return disk.total;
            }
        }

        internal static void Attach()
        {
            SaveData.Register<OsDisk>(Os.ModId, () => disk, d => disk = Load(d));
            Events.SceneLoaded += scene => { if (scene == "Main Menu") disk = new OsDisk(); };
            Events.SceneLoaded += scene => { OsBridge.ClearStaged(); OsView.DropHeld(); };
        }

        internal static string SelfCheck()
        {
            if (!ValidPath("/docs/readme.txt") || !ValidPath("/a_b-1/x.y")) return "valid path refused";
            if (!ValidPath("/" + new string('a', 120) + "/x.abcd") || ValidPath("/" + new string('a', 121) + "/x.abcd")) return "128 character limit";
            if (ValidPath("docs/a.txt") || ValidPath("/docs") || ValidPath("/docs/a") || ValidPath("/docs/a.") || ValidPath("/docs/.a")) return "shape";
            if (ValidPath("/docs/a.txt/b.txt") || ValidPath("/docs//a.txt") || ValidPath("/docs/a!.txt")) return "characters";
            if (ValidPath("/docs/ a.txt") || ValidPath("/docs/a.txt ") || ValidPath("/ docs/a.txt") || ValidPath("/docs /a.txt")) return "edge spaces";
            if (!ValidPath("/docs/a .txt")) return "inner space";
            if (!ValidPath("/docs/a (2).txt")) return "restore suffix name";
            if (ValidPath("/../a.txt") || ValidPath("/docs/..") || ValidPath("/docs/../a.txt") || ValidPath("/docs/a..b.txt") || ValidPath("/.docs/a.txt")) return "dot segments";
            OsDisk d = new OsDisk();
            if (!Put(d, "/docs/big.txt", new string('x', MaxFileBytes))) return "file at the limit refused";
            if (Put(d, "/docs/over.txt", new string('x', MaxFileBytes + 1))) return "file over the limit accepted";
            if (Put(d, "/docs/wide.txt", new string('é', MaxFileBytes / 2 + 1))) return "utf-8 bytes not counted";
            int n = 1;
            while (Put(d, "/docs/f" + n + ".txt", new string('x', MaxFileBytes))) n++;
            if (MaxTotalBytes - d.total >= MaxFileBytes + MaxPathLength + FileOverhead) return "total cap not reached";
            if (d.total > MaxTotalBytes || d.total != Recount(d)) return "running total";
            if (!Put(d, "/docs/big.txt", "") || d.total != Recount(d)) return "overwrite frees room";
            if (!Put(d, "/docs/extra.txt", "x") || d.total != Recount(d)) return "room after overwrite";
            if (!Remove(d, "/docs/f1.txt") || Remove(d, "/docs/f1.txt") || d.total != Recount(d)) return "delete";
            if (ListIn(d, "/docs").Length != d.files.Count) return "list count";
            if (ListIn(d, "/other").Length != 0 || ListIn(d, "docs").Length != 0) return "list other folder";
            OsDisk c = new OsDisk();
            for (int i = 0; i < MaxFiles; i++)
            {
                if (!Put(c, "/docs/c" + i + ".txt", "x")) return "file cap refused early";
            }
            if (Put(c, "/docs/over.txt", "x")) return "file cap";
            if (!Put(c, "/docs/c0.txt", "yy") || c.total != Recount(c)) return "overwrite at the file cap";
            if (!Remove(c, "/docs/c1.txt") || !Put(c, "/docs/over.txt", "x")) return "room under the file cap";
            OsDisk raw = new OsDisk();
            raw.files.Add(null);
            raw.files.Add(new OsFile { path = "docs/bad.txt", text = "x" });
            raw.files.Add(new OsFile { path = "/docs/a.txt", text = "a" });
            raw.files.Add(new OsFile { path = "/docs/a.txt", text = "dup" });
            raw.files.Add(new OsFile { path = "/docs/nul.txt", text = null });
            OsDisk loaded = Load(raw);
            if (loaded.files.Count != 1) return "load invalid entries";
            if (Find(loaded, "/docs/a.txt") == null || Find(loaded, "/docs/a.txt").text != "a" || loaded.total != Recount(loaded)) return "load duplicate";
            OsDisk many = new OsDisk();
            for (int i = 0; i <= MaxFiles; i++) many.files.Add(new OsFile { path = "/docs/m" + i + ".txt", text = "x" });
            if (Load(many).files.Count != MaxFiles) return "load file cap";
            OsDisk heavy = new OsDisk();
            for (int i = 0; i < 40; i++) heavy.files.Add(new OsFile { path = "/docs/b" + i + ".txt", text = new string('x', MaxFileBytes) });
            OsDisk fit = Load(heavy);
            if (fit.total > MaxTotalBytes || fit.total != Recount(fit) || fit.files.Count == 0 || fit.files.Count >= 40) return "load total cap";
            OsDisk t = new OsDisk();
            if (!Put(t, "/docs/a.txt", "one") || !Put(t, "/docs/b.txt", "two")) return "bin setup";
            if (!Trash(t, "/docs/a.txt", "2026-10-12 09:00") || Find(t, "/docs/a.txt") != null || t.total != Recount(t)) return "trash";
            OsFile tossed = Find(t, "/recycle/1.txt");
            if (tossed == null || tossed.origin != "/docs/a.txt" || tossed.deleted != "2026-10-12 09:00" || TrashedIn(t).Count != 1) return "trash record";
            if (Trash(t, "/recycle/1.txt", "x") || Put(t, "/recycle/2.txt", "x")) return "bin is read only";
            if (!Put(t, "/docs/a.txt", "again")) return "old name is free";
            string at;
            if (Recover(t, "/recycle/1.txt", out at) != OsRestore.Done || at != "/docs/a (2).txt" || Find(t, "/docs/a (2).txt").text != "one" || t.total != Recount(t)) return "restore name";
            if (Recover(t, "/docs/a (2).txt", out at) != OsRestore.Missing || !Trash(t, "/docs/b.txt", "2026-10-12 10:00") || !Trash(t, "/docs/a (2).txt", "2026-10-12 10:01")) return "restore and trash";
            if (Find(t, "/recycle/2.txt") == null || Find(t, "/recycle/3.txt") == null || TrashedIn(t).Count != 2) return "bin paths";
            if (Erase(t, "/docs/a.txt") || !Erase(t, "/recycle/3.txt") || TrashedIn(t).Count != 1 || t.total != Recount(t)) return "purge";
            Clear(t);
            if (TrashedIn(t).Count != 0 || Find(t, "/docs/a.txt") == null || t.files.Count != 1 || t.total != Recount(t)) return "empty bin";
            if (!Put(t, "/docs/c.txt", "c") || !Trash(t, "/docs/c.txt", "2026-10-12 12:00") || Find(t, "/recycle/4.txt") == null || t.binSeq != 4) return "bin ids keep counting";
            OsDisk f = new OsDisk();
            Put(f, "/docs/s.txt", "x");
            Trash(f, "/docs/s.txt", "2026-10-12 13:00");
            Put(f, "/docs/s.txt", "y");
            int k = 0;
            while (Put(f, "/docs/p" + k + ".txt", new string('x', MaxFileBytes))) k++;
            Put(f, "/docs/q.txt", new string('x', MaxTotalBytes - f.total - 45));
            if (Recover(f, "/recycle/1.txt", out at) != OsRestore.Full || Find(f, "/recycle/1.txt") == null || f.total != Recount(f)) return "restore on a full disk";
            OsDisk owned = new OsDisk();
            for (int i = 0; i < OsScope.MaxModFiles; i++) Put(owned, "/mod-example mod/f" + i + ".txt", "x");
            Trash(owned, "/mod-example mod/f0.txt", "2026-10-18 10:00");
            Put(owned, "/mod-example mod/g.txt", "x");
            if (Recover(owned, "/recycle/1.txt", out at) != OsRestore.OwnerFull || Find(owned, "/recycle/1.txt") == null || owned.total != Recount(owned)) return "restore into a full mod folder";
            Remove(owned, "/mod-example mod/g.txt");
            if (Recover(owned, "/recycle/1.txt", out at) != OsRestore.Done || at != "/mod-example mod/f0.txt" || owned.total != Recount(owned)) return "restore after room";
            OsDisk stored = new OsDisk();
            stored.binSeq = 2;
            stored.files.Add(new OsFile { path = "/recycle/4.txt", text = "kept", origin = "/docs/k.txt", deleted = "2026-10-12 11:00" });
            stored.files.Add(new OsFile { path = "/recycle/5.txt", text = "bad", origin = "docs/bad.txt" });
            stored.files.Add(new OsFile { path = "/docs/z.txt", text = "z", origin = "/docs/y.txt" });
            stored.files.Add(new OsFile { path = "/recycle/6.txt", text = "t", origin = "/recycle/2.txt" });
            stored.files.Add(new OsFile { path = "/recycle/7.txt", text = "p" });
            stored.files.Add(new OsFile { path = "/recycle/x.txt", text = "x", origin = "/docs/x.txt" });
            OsDisk back = Load(stored);
            if (back.files.Count != 1 || TrashedIn(back).Count != 1 || back.total != Recount(back) || TrashedIn(back)[0].deleted != "2026-10-12 11:00" || back.binSeq != 4) return "load bin";
            OsDisk m = new OsDisk();
            if (!Put(m, "/docs/a.txt", "one") || !Put(m, "/docs/b.txt", "two")) return "move setup";
            if (!Shift(m, "/docs/a.txt", "/vault/v1.dat") || Find(m, "/docs/a.txt") != null || Find(m, "/vault/v1.dat").text != "one" || m.total != Recount(m)) return "move";
            if (Shift(m, "/vault/v1.dat", "/docs/b.txt") || Shift(m, "/vault/v1.dat", "/recycle/9.txt") || Shift(m, "/docs/zz.txt", "/docs/y.txt")) return "move refusals";
            if (!Shift(m, "/vault/v1.dat", "/docs/c.txt") || !Remove(m, "/docs/c.txt") || m.total != Recount(m)) return "move back";
            OsDisk tight = new OsDisk();
            int q = 0;
            while (Put(tight, "/docs/p" + q + ".txt", new string('x', MaxFileBytes))) q++;
            if (!Put(tight, "/docs/fill.txt", new string('x', MaxTotalBytes - tight.total - 56))) return "fill setup";
            if (Shift(tight, "/docs/p0.txt", "/docs/" + new string('a', 100) + ".txt") || Find(tight, "/docs/p0.txt") == null || tight.total != Recount(tight)) return "move on a full disk";
            OsAppOptions probe = new OsAppOptions { Title = "title", OnOpen = () => { } };
            if (Os.RegisterApp("selfcheck", "Bad", probe) || Os.RegisterApp("selfcheck", Os.AppId, probe)) return "app id refused";
            if (Os.RegisterApp("selfcheck", "warez", probe) || Os.RegisterApp("selfcheck", "turvapro", probe)) return "reserved app id refused";
            return null;
        }

        internal static bool ValidPath(string path)
        {
            return OsPaths.ValidPath(path);
        }

        internal static bool ValidDir(string dir)
        {
            return OsPaths.ValidDir(dir);
        }

        private static OsDisk Load(OsDisk saved)
        {
            OsDisk d = new OsDisk();
            if (saved == null || saved.files == null) return d;
            d.binSeq = Math.Max(saved.binSeq, 0);
            foreach (OsFile f in saved.files)
            {
                if (f == null || !ValidPath(f.path) || f.text == null || Encoding.UTF8.GetByteCount(f.text) > MaxFileBytes || Find(d, f.path) != null) continue;
                if (InBin(f.path) != (f.origin != null)) continue;
                if (f.origin != null && (!ValidPath(f.origin) || InBin(f.origin))) continue;
                if (InBin(f.path) && BinId(f.path) == 0) continue;
                OsFile kept = new OsFile { path = f.path, text = f.text, origin = f.origin, deleted = f.deleted };
                kept.bytes = Cost(kept.origin ?? kept.path, kept.text);
                if (d.files.Count >= MaxFiles || d.total + kept.bytes > MaxTotalBytes) break;
                d.files.Add(kept);
                d.total += kept.bytes;
                if (kept.origin != null) d.binSeq = Math.Max(d.binSeq, BinId(kept.path));
            }
            return d;
        }

        private static bool Put(OsDisk d, string path, string text)
        {
            if (!ValidPath(path) || text == null || InBin(path)) return false;
            if (Encoding.UTF8.GetByteCount(text) > MaxFileBytes) return false;
            OsFile file = Find(d, path);
            if (file == null && d.files.Count >= MaxFiles) return false;
            int cost = Cost(path, text);
            int old = file == null ? 0 : file.bytes;
            if (d.total - old + cost > MaxTotalBytes) return false;
            if (file == null)
            {
                file = new OsFile { path = path };
                d.files.Add(file);
            }
            file.text = text;
            file.bytes = cost;
            d.total += cost - old;
            return true;
        }

        private static OsFile Find(OsDisk d, string path)
        {
            if (!ValidPath(path)) return null;
            foreach (OsFile f in d.files)
            {
                if (f.path == path) return f;
            }
            return null;
        }

        private static bool Remove(OsDisk d, string path)
        {
            OsFile file = Find(d, path);
            if (file == null) return false;
            d.total -= file.bytes;
            return d.files.Remove(file);
        }

        private static bool Trash(OsDisk d, string path, string deleted)
        {
            OsFile file = Find(d, path);
            if (file == null || file.origin != null) return false;
            d.binSeq++;
            file.origin = path;
            file.deleted = deleted;
            file.path = BinDir + "/" + d.binSeq + ".txt";
            return true;
        }

        private static OsRestore Recover(OsDisk d, string path, out string target)
        {
            target = null;
            OsFile file = Find(d, path);
            if (file == null || file.origin == null) return OsRestore.Missing;
            string name = FreeName(d, file.origin);
            if (name == null) return OsRestore.NoName;
            int cost = Cost(name, file.text);
            if (d.total - file.bytes + cost > MaxTotalBytes) return OsRestore.Full;
            if (!OwnerRoom(d, name, file.text)) return OsRestore.OwnerFull;
            d.total += cost - file.bytes;
            file.bytes = cost;
            file.path = name;
            file.origin = null;
            file.deleted = null;
            target = name;
            return OsRestore.Done;
        }

        private static bool OwnerRoom(OsDisk d, string path, string text)
        {
            string folder = OsScope.FolderOf(path);
            if (folder == null) return true;
            int files = 0;
            int bytes = 0;
            foreach (string other in ListIn(d, folder))
            {
                files++;
                bytes += Encoding.UTF8.GetByteCount(Find(d, other).text);
            }
            return OsScope.Fits(files, bytes, false, 0, Encoding.UTF8.GetByteCount(text));
        }

        private static bool Shift(OsDisk d, string from, string to)
        {
            OsFile file = Find(d, from);
            if (file == null || file.origin != null || !ValidPath(to) || InBin(to) || Find(d, to) != null) return false;
            int cost = Cost(to, file.text);
            if (d.total - file.bytes + cost > MaxTotalBytes) return false;
            d.total += cost - file.bytes;
            file.bytes = cost;
            file.path = to;
            return true;
        }

        private static int BinId(string path)
        {
            int id;
            if (!InBin(path) || !path.EndsWith(".txt", StringComparison.Ordinal)) return 0;
            string digits = path.Substring(BinDir.Length + 1, path.Length - BinDir.Length - 5);
            return int.TryParse(digits, out id) && id > 0 && digits == id.ToString() ? id : 0;
        }

        private static string FreeName(OsDisk d, string path)
        {
            if (Find(d, path) == null) return path;
            int dot = path.LastIndexOf('.');
            for (int n = 2; ; n++)
            {
                string candidate = path.Substring(0, dot) + " (" + n + ")" + path.Substring(dot);
                if (!ValidPath(candidate)) return null;
                if (Find(d, candidate) == null) return candidate;
            }
        }

        private static bool Erase(OsDisk d, string path)
        {
            OsFile file = Find(d, path);
            return file != null && file.origin != null && Remove(d, path);
        }

        private static void Clear(OsDisk d)
        {
            d.files.RemoveAll(f => f.origin != null);
            d.total = Recount(d);
        }

        private static List<OsFile> TrashedIn(OsDisk d)
        {
            List<OsFile> items = new List<OsFile>();
            foreach (OsFile f in d.files)
            {
                if (f.origin != null) items.Add(f);
            }
            return items;
        }

        private static string[] ListIn(OsDisk d, string dir)
        {
            List<string> paths = new List<string>();
            if (!ValidDir(dir)) return paths.ToArray();
            string prefix = dir + "/";
            foreach (OsFile f in d.files)
            {
                if (f.path != null && f.path.StartsWith(prefix, StringComparison.Ordinal) && f.path.IndexOf('/', prefix.Length) < 0) paths.Add(f.path);
            }
            return paths.ToArray();
        }

        public static bool InBin(string path)
        {
            return path.StartsWith(BinDir + "/", StringComparison.Ordinal);
        }

        private static int Cost(string path, string text)
        {
            return Encoding.UTF8.GetByteCount(text) + path.Length + FileOverhead;
        }

        private static int Recount(OsDisk d)
        {
            int total = 0;
            foreach (OsFile f in d.files) total += Cost(f.origin ?? f.path, f.text);
            return total;
        }
    }
}
