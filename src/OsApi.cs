using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using ObenseuerCore;

namespace ObenseuerOS
{
    public enum OsResult
    {
        Ok,
        Queued,
        Invalid,
        TooLarge,
        Full,
        Busy,
        Missing,
        Unavailable
    }

    public sealed class MailReadArgs
    {
        public int Id { get; private set; }
        public string From { get; private set; }
        public string Subject { get; private set; }
        public string ModId { get; private set; }

        internal MailReadArgs(int id, string from, string subject, string modId)
        {
            Id = id;
            From = from;
            Subject = subject;
            ModId = string.IsNullOrEmpty(modId) ? null : modId;
        }
    }

    public sealed class FileOpenedArgs
    {
        public string Path { get; private set; }
        public string ModId { get; private set; }

        internal FileOpenedArgs(string path, string modId)
        {
            Path = path;
            ModId = modId;
        }
    }

    public static partial class Os
    {
        private const int MaxLogged = 256;
        private const int MaxShown = 64;
        private static readonly Regex IconName = new Regex(@"\A[a-z0-9-]{1,40}\z");
        private static readonly HashSet<string> Logged = new HashSet<string>();

        public static event Action<MailReadArgs> MailRead;
        public static event Action<FileOpenedArgs> FileOpened;

        public static OsResult SendMail(string modId, string from, string subject, string body)
        {
            return SendMail(modId, from, subject, body, 0);
        }

        public static OsResult SendMail(string modId, string from, string subject, string body, int delayHours)
        {
            try
            {
                if (!Accept("SendMail", modId)) return OsResult.Invalid;
                if (!OsScope.ValidDelay(delayHours)) return Refuse("SendMail", modId, OsResult.Invalid, "delayHours must be 0 to " + OsScope.MaxDelayHours);
                return OsMail.SendFrom(modId, from, subject, body, delayHours);
            }
            catch (Exception e)
            {
                return Crash("SendMail", e);
            }
        }

        public static OsResult WriteFile(string modId, string path, string content)
        {
            try
            {
                return Accept("WriteFile", modId) ? OsModFiles.Write(modId, path, content) : OsResult.Invalid;
            }
            catch (Exception e)
            {
                return Crash("WriteFile", e);
            }
        }

        public static string ReadFile(string modId, string path)
        {
            try
            {
                return Accept("ReadFile", modId) ? OsModFiles.Read(modId, path) : null;
            }
            catch (Exception e)
            {
                Crash("ReadFile", e);
                return null;
            }
        }

        public static OsResult OpenFile(string modId, string path)
        {
            try
            {
                return Accept("OpenFile", modId) ? OsModFiles.Open(modId, path) : OsResult.Invalid;
            }
            catch (Exception e)
            {
                return Crash("OpenFile", e);
            }
        }

        public static OsResult Clear(string modId)
        {
            try
            {
                if (!Accept("Clear", modId)) return OsResult.Invalid;
                foreach (OsApp app in Apps.FindAll(x => x.ModId == modId)) Drop(app);
                OsView.ClearNotices(modId);
                OsContent.ClearMod(modId);
                OsView.Refresh();
                return OsMail.ClearFrom(modId);
            }
            catch (Exception e)
            {
                return Crash("Clear", e);
            }
        }

        internal static void Drop(OsApp app)
        {
            Closed(app.Id);
            Apps.Remove(app);
            OsBridge.RemoveApp(app.Id);
        }

        internal static bool IconExists(string icon)
        {
            try
            {
                return IconName.IsMatch(icon) && File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web", "icons", icon + ".svg"));
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static string Shown(string text)
        {
            if (text == null) return "null";
            string clean = Regex.Replace(text, @"\p{Cc}", "?");
            return clean.Length <= MaxShown ? clean : clean.Substring(0, MaxShown) + "...";
        }

        internal static bool Accept(string api, string modId)
        {
            if (modId == Os.ModId) return Refuse(api, modId, false, "the mod id " + Os.ModId + " belongs to the OS");
            if (!OsScope.ValidModId(modId)) return Refuse(api, null, false, "modId must be 1 to 64 letters, digits, '.', '_' or '-', and not start or end with '.' or contain '..'");
            return true;
        }

        internal static OsResult Refuse(string api, string modId, OsResult result, string why)
        {
            Refuse(api, modId, false, why);
            return result;
        }

        internal static bool Refuse(string api, string modId, bool result, string why)
        {
            string who = modId == null ? "" : " by " + modId;
            string key = api + who + " " + why;
            if (Logged.Count < MaxLogged && Logged.Add(key)) Log.Warn(api + " refused" + who + ": " + why);
            return result;
        }

        internal static OsResult Crash(string where, Exception e)
        {
            string key = where + " " + e.GetType().FullName;
            if (Logged.Count < MaxLogged && Logged.Add(key)) Log.Error(where + " failed, logged once per type: " + e);
            return OsResult.Unavailable;
        }

        internal static void RaiseMailRead(MailReadArgs args)
        {
            Raise(MailRead, args, "MailRead");
        }

        internal static void RaiseFileOpened(string path)
        {
            Raise(FileOpened, new FileOpenedArgs(path, OsScope.OwnerOf(path)), "FileOpened");
        }

        private static void Raise(Action handlers, string name)
        {
            if (handlers == null) return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Crash(name + " handler", e);
                }
            }
        }

        private static void Raise<T>(Action<T> handlers, T args, string name)
        {
            if (handlers == null) return;
            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<T>)handler)(args);
                }
                catch (Exception e)
                {
                    Crash(name + " handler", e);
                }
            }
        }
    }
}
