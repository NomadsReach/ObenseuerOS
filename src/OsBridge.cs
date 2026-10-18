using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using UnityEngine.SceneManagement;

namespace ObenseuerOS
{
    public sealed class OsBridgeException : Exception
    {
        public readonly string Code;

        public OsBridgeException(string code, string message) : base(message)
        {
            Code = code;
        }
    }

    internal static class OsBridge
    {
        internal const int MaxResultBytes = 12 * 1024;
        private const int MaxListLimit = 50;
        private const int MaxChunkChars = 8000;
        private const int MaxStaged = 4;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly Dictionary<string, Func<string, string>> BuiltIns = new Dictionary<string, Func<string, string>>
        {
            { "os.time.now", TimeNow },
            { "os.account.name", AccountName },
            { "os.money.balance", MoneyBalance },
            { "os.fs.list", FsList },
            { "os.fs.folders", FsFolders },
            { "os.fs.read", FsRead },
            { "os.fs.write", FsWrite },
            { "os.fs.append", FsAppend },
            { "os.fs.delete", FsDelete },
            { "os.fs.size", FsSize },
            { "os.bin.list", BinList },
            { "os.bin.restore", BinRestore },
            { "os.bin.purge", BinPurge },
            { "os.bin.empty", BinEmpty },
            { "os.file.opened", FileOpened },
        };
        private static readonly Dictionary<string, Func<string, string, string>> ModBuiltIns = new Dictionary<string, Func<string, string, string>>
        {
            { "os.time.now", (owner, args) => TimeNow(args) },
            { "os.account.name", (owner, args) => AccountName(args) },
            { "os.mod.read", ModRead },
            { "os.mod.write", ModWrite },
            { "os.mod.open", ModOpen },
        };
        private static readonly Dictionary<string, Func<string, string>> Handlers = new Dictionary<string, Func<string, string>>();
        private static readonly Dictionary<string, RateLimit> CallLimits = new Dictionary<string, RateLimit>();
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static readonly Dictionary<string, string> Staged = new Dictionary<string, string>();

        internal static bool Add(string appId, string method, Func<string, string> handler)
        {
            string key = appId + " " + method;
            if (Handlers.ContainsKey(key))
            {
                Log.Warn("duplicate OS handler: " + key);
                return false;
            }
            Handlers.Add(key, handler);
            return true;
        }

        internal static void RemoveApp(string appId)
        {
            foreach (string key in new List<string>(Handlers.Keys))
            {
                if (key.StartsWith(appId + " ", StringComparison.Ordinal)) Handlers.Remove(key);
            }
        }

        internal static bool IsBuiltIn(string method)
        {
            return BuiltIns.ContainsKey(method);
        }

        internal static JObject Reply(JObject request)
        {
            JToken id = request["id"];
            if (id == null || id.Type != JTokenType.Integer)
            {
                Log.Warn("OS request without an integer id");
                return null;
            }
            JToken args = request["args"];
            if (args != null && args.Type != JTokenType.Object) return Error(id, "bad_args", "args must be a JSON object");
            return Answer(id, Text(request, "app"), Text(request, "method"), args == null ? "{}" : args.ToString(Formatting.None));
        }

        internal static string SelfCheck()
        {
            if (CodeOf(Answer(new JValue(1), Os.AppId, "os.nope", "{}")) != "unknown_method") return "unknown method";
            if (CodeOf(Answer(new JValue(2), Os.AppId, "os.fs.size", "{")) != "bad_args") return "bad args";
            if (CodeOf(Answer(new JValue(5), "selfcheck.gone", "os.fs.write", "{}")) != "denied") return "unregistered app used a built-in";
            if (CodeOf(Reply(JObject.Parse("{\"id\":4,\"method\":\"os.time.now\",\"args\":5}"))) != "bad_args") return "non-object args";
            if (CodeOf(Answer(new JValue(6), Os.AppId, "os.time.now", new string('x', OsScope.MaxArgBytes + 1))) != "too_large") return "built-in args over 12 KB";
            if (CodeOf(Run(new JValue(3), "self-check", a => "\"" + new string('x', MaxResultBytes) + "\"", "{}")) != "too_large") return "oversize result";
            if (Window("a😀b", 0, 2) != "a" || Window("a😀b", 1, 1) != "😀" || Window("a😀b", 0, 3) != "a😀") return "surrogate window";
            return null;
        }

        private static JObject Answer(JToken id, string appId, string method, string args)
        {
            if (!OsScope.FitsArgs(args)) return Error(id, "too_large", "the call is over 12 KB");
            OsApp app = Os.Apps.Find(x => x.Id == appId);
            if (app != null && app.ModId != Os.ModId) return ModAnswer(id, app, method, args);
            if (app == null && !OsScope.IsReservedAppId(appId)) return Error(id, "denied", "the app is not registered");
            Func<string, string> handler = Find(appId, method);
            return handler == null ? Error(id, "unknown_method", "unknown method: " + method) : Run(id, appId + " " + method, handler, args);
        }

        private static JObject ModAnswer(JToken id, OsApp app, string method, string args)
        {
            if (!OsScope.Allow(CallLimits, app.Id, OsScope.CallsPerSecond, 1000, OsScope.NowMs())) return Error(id, "rate_limited", "more than 30 calls a second");
            string key = app.Id + " " + method;
            if (OsScope.IsOsMethod(method))
            {
                if (!OsScope.ModMayCall(method)) return Error(id, "denied", method + " is not available to mod apps");
                Func<string, string, string> safe;
                if (ModBuiltIns.TryGetValue(method, out safe))
                {
                    string owner = app.ModId;
                    return Run(id, key, a => safe(owner, a), args);
                }
            }
            Func<string, string> own;
            return Handlers.TryGetValue(key, out own) ? Run(id, key, own, args) : Error(id, "unknown_method", "unknown method: " + method);
        }

        private static Func<string, string> Find(string appId, string method)
        {
            Func<string, string> handler;
            if (method == null) return null;
            if (BuiltIns.TryGetValue(method, out handler)) return handler;
            return Handlers.TryGetValue(appId + " " + method, out handler) ? handler : null;
        }

        private static JObject Run(JToken id, string key, Func<string, string> handler, string args)
        {
            try
            {
                string json = handler(args);
                if (Encoding.UTF8.GetByteCount(json) > MaxResultBytes) throw new OsBridgeException("too_large", "the result is over 12 KB");
                return new JObject(new JProperty("type", "res"), new JProperty("id", id), new JProperty("ok", true), new JProperty("result", JToken.Parse(json)));
            }
            catch (OsBridgeException e)
            {
                return Error(id, e.Code, e.Message);
            }
            catch (Exception e)
            {
                if (Failed.Add(key)) Log.Error("OS request " + key + " failed, logged once: " + e);
                return Error(id, "failed", "the request failed");
            }
        }

        private static JObject Error(JToken id, string code, string message)
        {
            return new JObject(new JProperty("type", "res"), new JProperty("id", id), new JProperty("ok", false), new JProperty("error", code), new JProperty("message", message));
        }

        private static string CodeOf(JObject reply)
        {
            return reply.Value<string>("error");
        }

        internal static bool Writable()
        {
            return !SaveData.Busy;
        }

        internal static bool WorldReady()
        {
            return WorldClock.IsAvailable && SceneManager.GetActiveScene().name != "Main Menu";
        }

        internal static void CheckWritable()
        {
            if (!Writable()) throw new OsBridgeException("busy", "the game is saving or loading");
        }

        internal static string Text(JObject o, string key)
        {
            JToken t = o[key];
            return t != null && t.Type == JTokenType.String ? (string)t : null;
        }

        internal static JObject Args(string args)
        {
            try
            {
                return JObject.Parse(args);
            }
            catch (Exception)
            {
                throw new OsBridgeException("bad_args", "args must be a JSON object");
            }
        }

        internal static int Span(JObject a, string key, int max, int fallback)
        {
            JToken t = a[key];
            if (t == null) return fallback;
            long value;
            if (t.Type != JTokenType.Integer || !long.TryParse(t.ToString(Formatting.None), out value) || value < 0 || value > max) throw new OsBridgeException("bad_args", key + " is out of range");
            return (int)value;
        }

        internal static bool Flag(JObject a, string key)
        {
            JToken t = a[key];
            if (t == null || t.Type == JTokenType.Null) return false;
            if (t.Type != JTokenType.Boolean) throw new OsBridgeException("bad_args", key + " must be true or false");
            return (bool)t;
        }

        internal static string Window(string text, int offset, int length)
        {
            int end = offset + length;
            if (end > offset && end < text.Length && char.IsHighSurrogate(text[end - 1])) end = end - 1 > offset ? end - 1 : end + 1;
            return text.Substring(offset, end - offset);
        }

        internal static void ClearStaged()
        {
            Staged.Clear();
        }

        private static string FilePath(JObject a)
        {
            string path = Text(a, "path");
            if (!OsFiles.ValidPath(path) || OsFiles.InVault(path)) throw new OsBridgeException("bad_args", "path is not a valid file path");
            return path;
        }

        private static string Stored(JObject a)
        {
            string text = OsFiles.Read(FilePath(a));
            if (text == null) throw new OsBridgeException("failed", "no such file");
            return text;
        }

        private static string Chunk(JObject a)
        {
            string text = Text(a, "text");
            if (text == null) throw new OsBridgeException("bad_args", "text must be a string");
            if (text.Length > MaxChunkChars) throw new OsBridgeException("bad_args", "text is over 8000 characters");
            return text;
        }

        private static void Store(string path, string whole)
        {
            if (OsFiles.InBin(path)) throw new OsBridgeException("failed", "the Recycle Bin is read only");
            if (OsFiles.Write(path, whole)) return;
            if (Encoding.UTF8.GetByteCount(whole) > OsFiles.MaxFileBytes) throw new OsBridgeException("failed", "the file is over 256 KB");
            throw new OsBridgeException("failed", "the disk is full or has too many files" + (OsFiles.Trashed().Length > 0 ? " (empty the Recycle Bin)" : ""));
        }

        private static string FileOpened(string args)
        {
            string path = FilePath(Args(args));
            if (OsFiles.InBin(path) || OsFiles.Read(path) == null) throw new OsBridgeException("failed", "no such file");
            Os.RaiseFileOpened(path);
            return "{}";
        }

        private static string TimeNow(string args)
        {
            if (!WorldClock.IsAvailable) throw new OsBridgeException("failed", "the clock is not ready");
            WorldTime now = WorldClock.Now;
            return new JObject(new JProperty("year", now.Year), new JProperty("month", now.Month), new JProperty("day", now.CalendarDay), new JProperty("hour", now.Hour), new JProperty("minute", now.Minute), new JProperty("weekday", now.Weekday.ToString())).ToString(Formatting.None);
        }

        private static string AccountName(string args)
        {
            return JsonConvert.SerializeObject(OsView.UserName());
        }

        private static string ModName(JObject a)
        {
            string name = Text(a, "name");
            if (name == null) throw new OsBridgeException("bad_args", "name must be a string");
            return name;
        }

        private static string ModRead(string owner, string args)
        {
            string text = OsModFiles.Read(owner, ModName(Args(args)));
            if (text == null) throw new OsBridgeException("failed", "no such file");
            return JsonConvert.SerializeObject(text);
        }

        private static string ModWrite(string owner, string args)
        {
            JObject a = Args(args);
            string name = ModName(a);
            string text = Text(a, "text");
            if (text == null) throw new OsBridgeException("bad_args", "text must be a string");
            switch (OsModFiles.Write(owner, name, text))
            {
                case OsResult.Ok:
                    return "true";
                case OsResult.Busy:
                    throw new OsBridgeException("busy", "the game is saving or loading");
                case OsResult.Invalid:
                    throw new OsBridgeException("bad_args", "name is not a plain file name");
                default:
                    throw new OsBridgeException("failed", "the file could not be saved, the mod folder may be full");
            }
        }

        private static string ModOpen(string owner, string args)
        {
            switch (OsModFiles.Open(owner, ModName(Args(args))))
            {
                case OsResult.Ok:
                case OsResult.Queued:
                    return "true";
                case OsResult.Missing:
                    throw new OsBridgeException("failed", "no such file");
                case OsResult.Invalid:
                    throw new OsBridgeException("bad_args", "name is not a plain file name");
                default:
                    throw new OsBridgeException("failed", "the file could not be opened");
            }
        }

        private static string MoneyBalance(string args)
        {
            if (!Cash.IsAvailable) throw new OsBridgeException("failed", "money is not ready");
            return new JObject(new JProperty("oc", (int)Cash.Balance(MoneyCurrency.OC)), new JProperty("rm", Math.Round(Cash.Balance(MoneyCurrency.RM), 2))).ToString(Formatting.None);
        }

        private static string FsList(string args)
        {
            JObject a = Args(args);
            string dir = Text(a, "dir");
            if (!OsFiles.ValidDir(dir)) throw new OsBridgeException("bad_args", "dir is not a valid folder");
            int offset = Span(a, "offset", int.MaxValue, 0);
            int limit = Span(a, "limit", MaxListLimit, MaxListLimit);
            string[] paths = OsFiles.List(dir);
            JArray items = new JArray();
            for (int i = offset; i < paths.Length && items.Count < limit; i++)
            {
                items.Add(new JObject(new JProperty("path", paths[i]), new JProperty("bytes", Encoding.UTF8.GetByteCount(OsFiles.Read(paths[i])))));
            }
            return new JObject(new JProperty("items", items), new JProperty("total", paths.Length)).ToString(Formatting.None);
        }

        private static string FsFolders(string args)
        {
            JObject a = Args(args);
            int offset = Span(a, "offset", int.MaxValue, 0);
            int limit = Span(a, "limit", MaxListLimit, MaxListLimit);
            string[] folders = OsFiles.ModFolders();
            JArray items = new JArray();
            for (int i = offset; i < folders.Length && items.Count < limit; i++) items.Add(folders[i]);
            return new JObject(new JProperty("items", items), new JProperty("total", folders.Length)).ToString(Formatting.None);
        }

        private static string FsRead(string args)
        {
            JObject a = Args(args);
            string text = Stored(a);
            int offset = Span(a, "offset", text.Length, 0);
            return JsonConvert.SerializeObject(Window(text, offset, Span(a, "length", text.Length - offset, text.Length - offset)));
        }

        private static string FsSize(string args)
        {
            return JsonConvert.SerializeObject(Stored(Args(args)).Length);
        }

        private static string FsWrite(string args)
        {
            JObject a = Args(args);
            string path = FilePath(a);
            string text = Chunk(a);
            bool more = Flag(a, "more");
            CheckWritable();
            if (more && !Staged.ContainsKey(path) && Staged.Count >= MaxStaged) throw new OsBridgeException("busy", "too many unfinished saves");
            Staged.Remove(path);
            if (more) Staged[path] = text;
            else Store(path, text);
            return "true";
        }

        private static string FsAppend(string args)
        {
            JObject a = Args(args);
            string path = FilePath(a);
            string text = Chunk(a);
            bool more = Flag(a, "more");
            CheckWritable();
            string staged;
            if (!Staged.TryGetValue(path, out staged)) throw new OsBridgeException("bad_args", "no staged write for this path");
            Staged.Remove(path);
            string whole = staged + text;
            if (Encoding.UTF8.GetByteCount(whole) > OsFiles.MaxFileBytes) throw new OsBridgeException("failed", "the file is over 256 KB");
            if (more) Staged[path] = whole;
            else Store(path, whole);
            return "true";
        }

        private static string FsDelete(string args)
        {
            string path = FilePath(Args(args));
            CheckWritable();
            if (!OsFiles.Delete(path, Stamp())) throw new OsBridgeException("failed", "no such file");
            return "true";
        }

        private static string Stamp()
        {
            if (!WorldClock.IsAvailable) return "";
            WorldTime now = WorldClock.Now;
            return string.Format("{0:0000}-{1:00}-{2:00} {3:00}:{4:00}", now.Year, now.Month, now.CalendarDay, now.Hour, now.Minute);
        }

        private static string BinList(string args)
        {
            JObject a = Args(args);
            int offset = Span(a, "offset", int.MaxValue, 0);
            int limit = Span(a, "limit", MaxListLimit, MaxListLimit);
            OsFile[] items = OsFiles.Trashed();
            JArray list = new JArray();
            for (int i = offset; i < items.Length && list.Count < limit; i++)
            {
                list.Add(new JObject(new JProperty("path", items[i].path), new JProperty("origin", items[i].origin), new JProperty("deleted", items[i].deleted ?? ""), new JProperty("bytes", Encoding.UTF8.GetByteCount(items[i].text))));
            }
            return new JObject(new JProperty("items", list), new JProperty("total", items.Length)).ToString(Formatting.None);
        }

        private static string BinRestore(string args)
        {
            string path = FilePath(Args(args));
            CheckWritable();
            string target;
            switch (OsFiles.Restore(path, out target))
            {
                case OsRestore.Done:
                    return JsonConvert.SerializeObject(target);
                case OsRestore.NoName:
                    throw new OsBridgeException("failed", "no free name for the file");
                case OsRestore.Full:
                    throw new OsBridgeException("failed", "the disk is full, so the file cannot be restored");
                case OsRestore.OwnerFull:
                    throw new OsBridgeException("full", "the mod folder is full (64 files or 1 MB), so the file stays in the Recycle Bin");
                default:
                    throw new OsBridgeException("failed", "no such file in the Recycle Bin");
            }
        }

        private static string BinPurge(string args)
        {
            string path = FilePath(Args(args));
            CheckWritable();
            if (!OsFiles.Purge(path)) throw new OsBridgeException("failed", "no such file in the Recycle Bin");
            return "true";
        }

        private static string BinEmpty(string args)
        {
            CheckWritable();
            OsFiles.EmptyBin();
            return "true";
        }
    }
}
