using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ObenseuerOS
{
    internal sealed class WikiPage
    {
        internal string Title;
        internal long RevId;
        internal long Fetched;
        internal string File;
    }

    // Not thread-safe: use from one thread only (the Unity main thread in the game).
    internal sealed class WikiCache
    {
        internal const string Schema = "OBXWIKI4";
        internal const long FreshSeconds = 24 * 60 * 60;
        private const string PagesDir = "pages";
        private const string CssDir = "css";
        private const string ImagesDir = "img";
        private const string SchemaName = "schema";
        private const string TitlesName = "titles";
        private const string TitlesSchema = "OBXWIKI1";
        private const long MaxPageBytes = 9L * 1024 * 1024;
        private const long MaxCssBytes = 2L * 1024 * 1024;
        private const long MaxTitlesBytes = 4L * 1024 * 1024;
        private const string MetaStart = "<!--" + Schema + " ";
        private const string MetaEnd = " -->";
        private static readonly string[] ImageExts = { ".png", ".jpg", ".gif", ".webp" };
        private static readonly Regex CacheName = new Regex(@"^[0-9a-f]{40}(\.html|\.css|\.png|\.jpg|\.gif|\.webp)\z", RegexOptions.CultureInvariant);
        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);
        private readonly string root;
        private readonly long capBytes;
        private long used;

        internal WikiCache(string root, long capBytes)
        {
            this.root = root;
            this.capBytes = capBytes;
            Directory.CreateDirectory(Path.Combine(root, PagesDir));
            Directory.CreateDirectory(Path.Combine(root, CssDir));
            Directory.CreateDirectory(Path.Combine(root, ImagesDir));
            PurgeOldSchema();
            WriteAtomic(Path.Combine(root, WikiSite.MissingName), Utf8.GetBytes(WikiSite.MissingHtml));
            used = Measure();
        }

        private void PurgeOldSchema()
        {
            string marker = Path.Combine(root, SchemaName);
            string stored = null;
            Quietly(() => stored = File.Exists(marker) ? File.ReadAllText(marker, Utf8) : null);
            if (stored == Schema) return;
            DeleteAll(PagesDir);
            DeleteAll(CssDir);
            WriteAtomic(marker, Utf8.GetBytes(Schema));
        }

        internal long CapBytes
        {
            get
            {
                return capBytes;
            }
        }

        internal static string Hash(string text)
        {
            using (SHA1 sha = SHA1.Create())
            {
                StringBuilder sb = new StringBuilder();
                foreach (byte b in sha.ComputeHash(Utf8.GetBytes(text))) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        internal static bool IsFresh(long fetched, long now)
        {
            return now >= fetched && now - fetched < FreshSeconds;
        }

        internal bool SavePage(string title, long revId, long fetched, string html, long now)
        {
            string normal;
            if (!WikiTitle.TryNormalise(title, out normal) || normal != title) return false;
            if (html == null || revId < 0 || fetched < 0) return false;
            byte[] bytes = Utf8.GetBytes(MetaStart + "title=" + Uri.EscapeDataString(title) + " rev=" + Num(revId) + " fetched=" + Num(fetched) + MetaEnd + "\n" + html);
            if (bytes.Length > MaxPageBytes) return false;
            return Save(PagesDir, Hash(title) + ".html", bytes, now) != null;
        }

        internal WikiPage LoadPage(string title, long now)
        {
            string rel = PageFile(title);
            if (rel == null) return null;
            string file = Path.Combine(root, rel);
            WikiPage page = ReadMeta(file);
            if (page == null || page.Title != title)
            {
                Remove(file);
                return null;
            }
            Touch(file, now);
            return page;
        }

        internal string PageFile(string title)
        {
            return Present(PagesDir + "/" + Hash(title) + ".html");
        }

        internal string SaveCss(string key, string text, long now)
        {
            if (key == null || text == null) return null;
            byte[] bytes = Utf8.GetBytes(text);
            return bytes.Length > MaxCssBytes ? null : Save(CssDir, Hash(key) + ".css", bytes, now);
        }

        internal string CssFile(string key, long now)
        {
            return Hit(CssDir + "/" + Hash(key) + ".css", now);
        }

        internal string SaveImage(string path, byte[] data, long now)
        {
            string ext = ImageExt(data);
            if (ext == null || !WikiTitle.IsImagePath(path)) return null;
            return Save(ImagesDir, Hash(path) + ext, data, now);
        }

        internal string ImageFile(string path, long now)
        {
            foreach (string ext in ImageExts)
            {
                string rel = Hit(ImagesDir + "/" + Hash(path) + ext, now);
                if (rel != null) return rel;
            }
            return null;
        }

        internal long TotalBytes()
        {
            return used;
        }

        internal bool Fits(long bytes)
        {
            return used + bytes <= capBytes;
        }

        internal int PageCount()
        {
            int count = 0;
            foreach (string file in Listing(PagesDir))
            {
                if (CacheName.IsMatch(Path.GetFileName(file))) count++;
            }
            return count;
        }

        internal bool SaveTitles(List<string> titles, long fetched)
        {
            if (titles == null || fetched < 0) return false;
            StringBuilder text = new StringBuilder(TitlesSchema + "\nfetched:" + Num(fetched) + "\n");
            foreach (string title in titles)
            {
                string normal;
                if (!WikiTitle.TryNormalise(title, out normal) || normal != title) return false;
                text.Append(title).Append('\n');
            }
            return WriteAtomic(Path.Combine(root, TitlesName), Utf8.GetBytes(text.ToString()));
        }

        internal List<string> LoadTitles(out long fetched)
        {
            fetched = 0;
            string file = Path.Combine(root, TitlesName);
            if (!File.Exists(file)) return null;
            string[] lines;
            try
            {
                if (new FileInfo(file).Length > MaxTitlesBytes) return null;
                lines = File.ReadAllText(file, Utf8).Split('\n');
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            long when;
            if (lines.Length < 2 || lines[0] != TitlesSchema || !long.TryParse(Field(lines[1], "fetched:") ?? "", NumberStyles.None, CultureInfo.InvariantCulture, out when)) return null;
            fetched = when;
            List<string> titles = new List<string>();
            for (int i = 2; i < lines.Length; i++)
            {
                if (i == lines.Length - 1 && lines[i].Length == 0) break;
                string normal;
                if (!WikiTitle.TryNormalise(lines[i], out normal) || normal != lines[i]) return null;
                titles.Add(lines[i]);
            }
            return titles;
        }

        internal void Clear()
        {
            DeleteAll(PagesDir);
            DeleteAll(CssDir);
            DeleteAll(ImagesDir);
            Quietly(() => File.Delete(Path.Combine(root, TitlesName)));
            used = Measure();
        }

        private void DeleteAll(string dir)
        {
            foreach (string file in Listing(dir))
            {
                string name = Path.GetFileName(file);
                if (CacheName.IsMatch(name) || name.EndsWith(".tmp", StringComparison.Ordinal)) Remove(file);
            }
        }

        private string[] Listing(string dir)
        {
            string path = Path.Combine(root, dir);
            return Directory.Exists(path) ? Directory.GetFiles(path) : new string[0];
        }

        private string Present(string rel)
        {
            return File.Exists(Path.Combine(root, rel)) ? rel : null;
        }

        private string Hit(string rel, long now)
        {
            string file = Path.Combine(root, rel);
            if (!File.Exists(file)) return null;
            Touch(file, now);
            return rel;
        }

        private string Save(string dir, string name, byte[] bytes, long now)
        {
            string file = Path.Combine(root, dir, name);
            long before = Size(file);
            if (!WriteAtomic(file, bytes)) return null;
            used += bytes.Length - before;
            Touch(file, now);
            Evict(file);
            return dir + "/" + name;
        }

        private long Measure()
        {
            long total = 0;
            foreach (FileInfo info in CacheFiles()) total += info.Length;
            return total;
        }

        private static long Size(string file)
        {
            FileInfo info = new FileInfo(file);
            return info.Exists ? info.Length : 0;
        }

        private void Remove(string file)
        {
            long length = Size(file);
            Quietly(() => File.Delete(file));
            if (!File.Exists(file) && CacheName.IsMatch(Path.GetFileName(file))) used -= length;
        }

        private List<FileInfo> CacheFiles()
        {
            List<FileInfo> files = new List<FileInfo>();
            foreach (string dir in new[] { PagesDir, CssDir, ImagesDir })
            {
                foreach (string path in Listing(dir))
                {
                    FileInfo info = new FileInfo(path);
                    if (CacheName.IsMatch(info.Name)) files.Add(info);
                }
            }
            return files;
        }

        private void Evict(string keep)
        {
            if (used <= capBytes) return;
            DateTime newest = new FileInfo(keep).LastWriteTimeUtc;
            List<FileInfo> files = CacheFiles();
            files.Sort(OldestFirst);
            foreach (FileInfo info in files)
            {
                if (used <= capBytes || info.LastWriteTimeUtc >= newest) break;
                Remove(info.FullName);
            }
        }

        private static int OldestFirst(FileInfo a, FileInfo b)
        {
            int order = a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
            return order != 0 ? order : string.CompareOrdinal(a.Name, b.Name);
        }

        private static void Touch(string file, long now)
        {
            Quietly(() => File.SetLastWriteTimeUtc(file, Epoch.AddSeconds(now)));
        }

        private static WikiPage ReadMeta(string file)
        {
            string line;
            try
            {
                if (new FileInfo(file).Length > MaxPageBytes) return null;
                using (StreamReader reader = new StreamReader(file, Utf8))
                {
                    line = reader.ReadLine();
                }
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
            if (line == null || !line.StartsWith(MetaStart, StringComparison.Ordinal) || !line.EndsWith(MetaEnd, StringComparison.Ordinal)) return null;
            string[] fields = line.Substring(MetaStart.Length, line.Length - MetaStart.Length - MetaEnd.Length).Split(' ');
            if (fields.Length != 3) return null;
            string title = Field(fields[0], "title=");
            string rev = Field(fields[1], "rev=");
            string fetched = Field(fields[2], "fetched=");
            long revId;
            long when;
            if (title == null || rev == null || fetched == null) return null;
            if (!long.TryParse(rev, NumberStyles.None, CultureInfo.InvariantCulture, out revId)) return null;
            if (!long.TryParse(fetched, NumberStyles.None, CultureInfo.InvariantCulture, out when)) return null;
            return new WikiPage { Title = Uri.UnescapeDataString(title), RevId = revId, Fetched = when, File = PagesDir + "/" + Path.GetFileName(file) };
        }

        private static string Field(string text, string name)
        {
            return text.StartsWith(name, StringComparison.Ordinal) ? text.Substring(name.Length) : null;
        }

        private static bool WriteAtomic(string target, byte[] bytes)
        {
            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, bytes);
                Publish(temp, target);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            finally
            {
                Quietly(() => File.Delete(temp));
            }
        }

        private static void Publish(string temp, string target)
        {
            try
            {
                if (File.Exists(target)) File.Replace(temp, target, null);
                else File.Move(temp, target);
            }
            catch (IOException)
            {
                File.Copy(temp, target, true);
            }
            catch (UnauthorizedAccessException)
            {
                File.Copy(temp, target, true);
            }
        }

        private static void Quietly(Action action)
        {
            try
            {
                action();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (ArgumentException)
            {
            }
        }

        private static string Num(long value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string ImageExt(byte[] data)
        {
            if (data == null) return null;
            if (At(data, 0, "\u0089PNG\r\n\u001a\n")) return ".png";
            if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return ".jpg";
            if (At(data, 0, "GIF87a") || At(data, 0, "GIF89a")) return ".gif";
            if (At(data, 0, "RIFF") && At(data, 8, "WEBP")) return ".webp";
            return null;
        }

        internal static bool At(byte[] data, int offset, string magic)
        {
            if (data.Length < offset + magic.Length) return false;
            for (int i = 0; i < magic.Length; i++)
            {
                if (data[offset + i] != (byte)magic[i]) return false;
            }
            return true;
        }
    }
}
