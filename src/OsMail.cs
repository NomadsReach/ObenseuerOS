using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    [Serializable]
    public class MailRec
    {
        public int Id;
        public string Folder = "";
        public string From = "";
        public string To = "";
        public string Subject = "";
        public string Body = "";
        public string When = "";
        public int Stamp;
        public bool Read;
        public string Mod = "";
        public string Attach = "";
        public bool Opened;
    }

    [Serializable]
    public class MailPendingRec
    {
        public string From = "";
        public string Subject = "";
        public string Body = "";
        public string Mod = "";
        public int Due;
    }

    [Serializable]
    public class MailSave
    {
        public string Username = "";
        public string Domain = "";
        public int NextId = 1;
        public List<MailRec> Items = new List<MailRec>();
        public List<MailPendingRec> Pending = new List<MailPendingRec>();
        public List<string> Imported = new List<string>();
        public List<int> Starred = new List<int>();
    }

    internal static class OsMail
    {
        internal const string AppId = "mail";
        private const string StoreId = "nomad.obenseuer.os.mail";
        private const string Inbox = "inbox";
        private const string Sent = "sent";
        private const string Trash = "trash";
        private const string Important = "important";
        private const string WelcomeFrom = "Stalburg Mail";
        private const string WelcomeSubject = "Welcome to Stalburg Mail";
        private const string NoAccount = "Create a Stalburg Mail account first.";
        private const int MinUser = 3;
        private const int MaxUser = 20;
        private const int MaxItems = 200;
        private const int MaxImported = 500;
        private const int MaxPending = 20;
        private const int MaxModPending = 40;
        private const int MaxModPendingEach = 10;
        internal const int MaxSubject = 100;
        internal const int MaxFrom = 80;
        internal const int MaxBody = 3000;
        private const int ReplyRoom = 512;
        private const int ListMax = 30;
        private const int ListDefault = 20;
        private const int ListMaxBytes = 10 * 1024;
        private const int AutoReplyMinutes = 120;
        private const int MinutesPerDay = 1440;
        private const float TickInterval = 0.25f;
        private static readonly string[] Evictable = { Trash, Sent };
        private static readonly string[] Domains = { "stalburg.ts", "opensewer.ts" };
        private static readonly string[] Recipients = { "info@osmo.ts", "info@hotelkorso.ts", "info@muddymarkku.ts", "info@ankea.ts", "aleksi.kivi@opensewer.ts", "hilla.palo@opensewer.ts", "ivan.tsarnov@opensewer.ts" };
        private static readonly HashSet<string> ReservedNames = new HashSet<string> { "admin", "info", "support", "service", "postmaster", "noreply" };
        private static readonly Regex UserChars = new Regex(@"\A(?!.*\.\.)[a-z0-9][a-z0-9._-]*(?<!\.)\z");
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly HashSet<string> Failed = new HashSet<string>();
        private static MailSave save = new MailSave();
        private static float lastTick;
        private static int lastBadge = -1;

        internal static void Attach()
        {
            SaveData.Register<MailSave>(StoreId, () => save, loaded => { save = loaded ?? new MailSave(); Normalise(save); lastBadge = -1; });
            Events.HourPassed += hour => Sync();
            Mail.Received += args => Sync();
            Os.Handle(AppId, "mail.status", Status);
            Os.Handle(AppId, "mail.signup", SignUp);
            Os.Handle(AppId, "mail.list", ListFolder);
            Os.Handle(AppId, "mail.read", ReadMail);
            Os.Handle(AppId, "mail.delete", DeleteMail);
            Os.Handle(AppId, "mail.send", ComposeMail);
            Os.Handle(AppId, "mail.unread", MarkUnread);
            Os.Handle(AppId, "mail.star", Star);
            Os.Handle(AppId, "mail.attachment", OpenAttachment);
        }

        internal static void Tick()
        {
            if (Time.unscaledTime < lastTick + TickInterval) return;
            lastTick = Time.unscaledTime;
            int unread = save.Username.Length == 0 ? 0 : Unread(save);
            if (unread == lastBadge) return;
            try
            {
                lastBadge = OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "badge"), new JProperty("app", AppId), new JProperty("count", unread))) ? unread : -1;
            }
            catch (Exception e)
            {
                Fail("mail badge", e);
            }
        }

        internal static bool Send(string from, string subject, string body)
        {
            if (string.IsNullOrEmpty(from)) throw new ArgumentException("from must be a non-empty string", "from");
            if (string.IsNullOrEmpty(subject)) throw new ArgumentException("subject must be a non-empty string", "subject");
            if (save.Username.Length == 0 || !Writable() || !MakeRoom(save)) return false;
            Add(save, Make(Inbox, from, Address(), Line(subject), Printable(body)));
            return true;
        }

        internal static OsResult SendFrom(string modId, string from, string subject, string body, int delayHours)
        {
            if (!OsScope.ValidDelay(delayHours)) return Os.Refuse("SendMail", modId, OsResult.Invalid, "delayHours must be 0 to " + OsScope.MaxDelayHours);
            string sender = Line(from);
            string title = Line(subject);
            if (sender.Length == 0 || title.Length == 0) return Os.Refuse("SendMail", modId, OsResult.Invalid, "from and subject must not be empty");
            if (!Writable()) return OsResult.Busy;
            if (!OsBridge.WorldReady()) return Os.Refuse("SendMail", modId, OsResult.Unavailable, "no save is loaded yet, send mail once the player is in the game");
            string text = Printable(body);
            if (delayHours == 0)
            {
                if (!MakeRoom(save)) return Os.Refuse("SendMail", modId, OsResult.Full, "the inbox holds " + MaxItems + " messages, the player has to delete some");
                Add(save, Make(Inbox, sender, Address(), title, text)).Mod = modId;
                return OsResult.Ok;
            }
            if (save.Pending.FindAll(x => !string.IsNullOrEmpty(x.Mod)).Count >= MaxModPending || save.Pending.FindAll(x => x.Mod == modId).Count >= MaxModPendingEach) return Os.Refuse("SendMail", modId, OsResult.Full, "too many delayed mails are waiting (" + MaxModPendingEach + " per mod, " + MaxModPending + " in all)");
            save.Pending.Add(new MailPendingRec { From = Clip(sender, MaxFrom), Subject = Clip(title, MaxSubject), Body = Clip(text, MaxBody), Mod = modId, Due = NowMinutes() + delayHours * 60 });
            return OsResult.Queued;
        }

        internal static OsResult ClearFrom(string modId)
        {
            if (!Writable()) return OsResult.Busy;
            if (!OsBridge.WorldReady()) return Os.Refuse("Clear", modId, OsResult.Unavailable, "no save is loaded yet, clear once the player is in the game");
            save.Pending.RemoveAll(x => x.Mod == modId);
            return OsResult.Ok;
        }

        internal static string SelfCheck()
        {
            if (!ValidUser("abc") || ValidUser("ab") || ValidUser("-abc") || ValidUser(new string('a', MaxUser + 1)) || !ValidUser(new string('a', MaxUser))) return "username rule";
            if (ValidUser(".abc") || ValidUser("abc.") || ValidUser("ab..c") || !ValidUser("a.b-c_d")) return "username dots";
            if (!Reserved("hilla.palo") || Reserved("hilla")) return "recipient reserved";
            if (Clip("abcdef", 4) != "a...") return "clip";
            if (Printable("a\u0001b\r\nc") != "ab\nc" || Line(" a\nb ") != "ab") return "printable";
            if (Printable("a\u2028b\u2029c") != "abc" || Line("\u2029a\u2028b\u2029") != "ab") return "line separators";
            MailSave state = new MailSave();
            for (int i = 0; i < MaxItems; i++) Add(state, new MailRec { Folder = Inbox, Subject = "s" + i });
            if (MakeRoom(state) || state.Items.Count != MaxItems || state.Items[0].Subject != "s0" || Unread(state) != MaxItems) return "item cap";
            MailSave full = new MailSave();
            for (int i = 0; i < MaxItems - 1; i++) Add(full, new MailRec { Folder = Inbox, Subject = "i" + i });
            Add(full, new MailRec { Folder = Trash, Subject = "t" });
            if (!MakeRoom(full)) return "trash room";
            Add(full, new MailRec { Folder = Inbox, Subject = "last" });
            if (full.Items.Count != MaxItems || full.Items[0].Subject != "i0" || full.Items.Exists(x => x.Subject == "t")) return "trash dropped first";
            if (Encoding.UTF8.GetByteCount(new string('€', MaxBody)) + ReplyRoom > OsBridge.MaxResultBytes) return "read reply size";
            return null;
        }

        private static string Status(string args)
        {
            if (WorldClock.IsAvailable) Sync();
            return JsonConvert.SerializeObject(new { account = Account(), unread = Unread(save), recipients = Recipients });
        }

        private static string SignUp(string args)
        {
            JObject a = OsBridge.Args(args);
            if (save.Username.Length > 0) throw new OsBridgeException("denied", "This save already has a Stalburg Mail account.");
            string user = (OsBridge.Text(a, "username") ?? "").Trim().ToLowerInvariant();
            if (Reserved(user)) throw new OsBridgeException("bad_args", "That name is reserved. Choose another.");
            if (!ValidUser(user)) throw new OsBridgeException("bad_args", "Use 3 to 20 letters, digits, dots, dashes or underscores. Start with a letter or digit.");
            string domain = OsBridge.Text(a, "domain") ?? "";
            if (Array.IndexOf(Domains, domain) < 0) throw new OsBridgeException("bad_args", "Choose stalburg.ts or opensewer.ts.");
            OsBridge.CheckWritable();
            save.Username = user;
            save.Domain = domain;
            foreach (MailRec rec in save.Items)
            {
                if (rec.To.Length == 0) rec.To = Address();
            }
            Send(WelcomeFrom, WelcomeSubject, WelcomeBody(Address()));
            return JsonConvert.SerializeObject(Account());
        }

        private static string ListFolder(string args)
        {
            JObject a = OsBridge.Args(args);
            string folder = FolderOf(a);
            int offset = OsBridge.Span(a, "offset", int.MaxValue, 0);
            int limit = OsBridge.Span(a, "limit", ListMax, ListDefault);
            RequireAccount();
            if (WorldClock.IsAvailable) Sync();
            List<MailRec> rows = folder == Important ? save.Items.FindAll(x => x.Folder != Trash && save.Starred.Contains(x.Id)) : save.Items.FindAll(x => x.Folder == folder);
            rows.Sort((x, y) => y.Id.CompareTo(x.Id));
            List<object> items = new List<object>();
            int bytes = 0;
            for (int i = offset; i < rows.Count && items.Count < limit; i++)
            {
                MailRec rec = rows[i];
                object row = new { id = rec.Id, from = rec.From, to = rec.To, subject = rec.Subject, when = rec.When, read = rec.Read };
                int size = Encoding.UTF8.GetByteCount(JsonConvert.SerializeObject(row)) + 1;
                if (bytes + size > ListMaxBytes) break;
                bytes += size;
                items.Add(row);
            }
            return JsonConvert.SerializeObject(new { items = items, total = rows.Count });
        }

        private static string ReadMail(string args)
        {
            JObject a = OsBridge.Args(args);
            RequireAccount();
            MailRec rec = Message(a);
            string json = ReadJson(rec);
            if (Encoding.UTF8.GetByteCount(json) > OsBridge.MaxResultBytes) throw new OsBridgeException("too_large", "the message is over 12 KB");
            if (!rec.Read)
            {
                OsBridge.CheckWritable();
                rec.Read = true;
                Os.RaiseMailRead(new MailReadArgs(rec.Id, rec.From, rec.Subject, rec.Mod));
            }
            return json;
        }

        internal static string ReadJson(MailRec rec)
        {
            object attachment = string.IsNullOrEmpty(rec.Attach) ? null : (object)new { file = rec.Attach, opened = rec.Opened };
            return JsonConvert.SerializeObject(new { id = rec.Id, folder = rec.Folder, from = rec.From, to = rec.To, subject = rec.Subject, body = rec.Body, when = rec.When, starred = save.Starred.Contains(rec.Id), attachment = attachment });
        }

        private static string OpenAttachment(string args)
        {
            JObject a = OsBridge.Args(args);
            RequireAccount();
            MailRec rec = Message(a);
            if (string.IsNullOrEmpty(rec.Attach)) throw new OsBridgeException("bad_args", "This message has no attachment.");
            if (rec.Opened) throw new OsBridgeException("denied", "This attachment was already opened.");
            OsBridge.CheckWritable();
            rec.Opened = true;
            VirusDecision decision = VirusCore.Infect(VirusVector.SpamAttachment, rec.Attach);
            return JsonConvert.SerializeObject(new { file = rec.Attach, outcome = decision.Outcome.ToString().ToLowerInvariant() });
        }

        private static string DeleteMail(string args)
        {
            JObject a = OsBridge.Args(args);
            RequireAccount();
            MailRec rec = Message(a);
            OsBridge.CheckWritable();
            bool purged = rec.Folder == Trash;
            if (purged) save.Items.Remove(rec);
            else rec.Folder = Trash;
            save.Starred.Remove(rec.Id);
            return JsonConvert.SerializeObject(new { purged = purged });
        }

        private static string MarkUnread(string args)
        {
            JObject a = OsBridge.Args(args);
            RequireAccount();
            MailRec rec = Message(a);
            if (rec.Folder != Inbox) throw new OsBridgeException("bad_args", "Only Inbox messages can be marked unread.");
            OsBridge.CheckWritable();
            rec.Read = false;
            return JsonConvert.SerializeObject(new { unread = Unread(save) });
        }

        private static string Star(string args)
        {
            JObject a = OsBridge.Args(args);
            RequireAccount();
            MailRec rec = Message(a);
            if (a["on"] == null || a["on"].Type != JTokenType.Boolean) throw new OsBridgeException("bad_args", "on must be true or false");
            bool on = (bool)a["on"];
            if (on && rec.Folder == Trash) throw new OsBridgeException("bad_args", "Trashed mail cannot be starred.");
            OsBridge.CheckWritable();
            save.Starred.Remove(rec.Id);
            if (on) save.Starred.Add(rec.Id);
            return JsonConvert.SerializeObject(new { starred = on });
        }

        private static string ComposeMail(string args)
        {
            JObject a = OsBridge.Args(args);
            RequireAccount();
            string to = OsBridge.Text(a, "to") ?? "";
            if (Array.IndexOf(Recipients, to) < 0) throw new OsBridgeException("bad_args", "Choose an address from the list.");
            string subject = Line(OsBridge.Text(a, "subject"));
            if (subject.Length == 0 || subject.Length > MaxSubject) throw new OsBridgeException("bad_args", "Write a subject of up to 100 characters.");
            string body = OsBridge.Text(a, "body") ?? "";
            if (body.Length > MaxBody) throw new OsBridgeException("bad_args", "The message is over 3000 characters.");
            if (!WorldClock.IsAvailable) throw new OsBridgeException("failed", "the clock is not ready");
            OsBridge.CheckWritable();
            if (save.Pending.FindAll(x => string.IsNullOrEmpty(x.Mod)).Count >= MaxPending) throw new OsBridgeException("denied", "Too many messages are waiting for a reply.");
            if (!MakeRoom(save)) throw new OsBridgeException("denied", "Mailbox full. Delete some messages first.");
            MailRec rec = Add(save, Make(Sent, Address(), to, subject, Printable(body)));
            save.Pending.Add(new MailPendingRec { From = to, Subject = subject, Due = NowMinutes() + AutoReplyMinutes });
            return JsonConvert.SerializeObject(new { id = rec.Id });
        }

        internal static bool HasAccount()
        {
            return save.Username.Length > 0;
        }

        internal static int ItemCount()
        {
            return save.Items.Count;
        }

        internal static bool SendSpam(string from, string subject, string body, string attach)
        {
            if (!HasAccount() || !Writable() || save.Items.Count >= MaxItems) return false;
            Add(save, Make(Inbox, from, Address(), Line(subject), Printable(body))).Attach = attach;
            return true;
        }

        private static void Normalise(MailSave state)
        {
            state.Username = state.Username ?? "";
            state.Domain = state.Domain ?? "";
            state.Items = state.Items ?? new List<MailRec>();
            state.Items.RemoveAll(x => x == null);
            foreach (MailRec rec in state.Items)
            {
                rec.Folder = rec.Folder ?? "";
                rec.From = rec.From ?? "";
                rec.To = rec.To ?? "";
                rec.Subject = rec.Subject ?? "";
                rec.Body = rec.Body ?? "";
                rec.When = rec.When ?? "";
                rec.Mod = rec.Mod ?? "";
                rec.Attach = rec.Attach ?? "";
            }
            state.Pending = state.Pending ?? new List<MailPendingRec>();
            state.Pending.RemoveAll(x => x == null);
            foreach (MailPendingRec pending in state.Pending)
            {
                pending.From = pending.From ?? "";
                pending.Subject = pending.Subject ?? "";
                pending.Body = pending.Body ?? "";
                pending.Mod = pending.Mod ?? "";
            }
            state.Imported = state.Imported ?? new List<string>();
            state.Starred = state.Starred ?? new List<int>();
        }

        private static void Sync()
        {
            if (!Writable()) return;
            int now = NowMinutes();
            foreach (MailPendingRec pending in save.Pending.FindAll(x => x.Due <= now))
            {
                if (!string.IsNullOrEmpty(pending.Mod))
                {
                    if (!MakeRoom(save)) continue;
                    save.Pending.Remove(pending);
                    Add(save, Make(Inbox, pending.From, Address(), pending.Subject, pending.Body)).Mod = pending.Mod;
                    continue;
                }
                if (!MakeRoom(save)) continue;
                save.Pending.Remove(pending);
                Add(save, Make(Inbox, pending.From, Address(), "Re: " + pending.Subject, ReplyBody(pending.Subject)));
            }
            foreach (GameMail mail in Collected())
            {
                if (save.Imported.Contains(mail.Key)) continue;
                if (!MakeRoom(save)) continue;
                save.Imported.Add(mail.Key);
                if (save.Imported.Count > MaxImported) save.Imported.RemoveAt(0);
                Add(save, Make(Inbox, mail.From, Address(), mail.Subject, Printable(mail.Body)));
            }
        }

        private static List<GameMail> Collected()
        {
            try
            {
                return OsMailGame.Collect();
            }
            catch (Exception e)
            {
                Fail("mail import", e);
                return new List<GameMail>();
            }
        }

        private static MailRec Make(string folder, string from, string to, string subject, string body)
        {
            MailRec rec = new MailRec();
            rec.Folder = folder;
            rec.From = Clip(from, MaxFrom);
            rec.To = Clip(to, MaxFrom);
            rec.Subject = Clip(subject, MaxSubject);
            rec.Body = Clip(body, MaxBody);
            rec.Read = folder != Inbox;
            if (WorldClock.IsAvailable)
            {
                WorldTime now = WorldClock.Now;
                rec.Stamp = Minutes(now);
                rec.When = When(now);
            }
            return rec;
        }

        private static MailRec Add(MailSave state, MailRec rec)
        {
            rec.Id = state.NextId++;
            state.Items.Add(rec);
            return rec;
        }

        private static bool MakeRoom(MailSave state)
        {
            int at;
            if (!OsScope.RoomFor(state.Items.ConvertAll(x => x.Folder), MaxItems, Evictable, out at)) return false;
            if (at >= 0) Evict(state, at);
            return true;
        }

        private static void Evict(MailSave state, int index)
        {
            state.Starred.Remove(state.Items[index].Id);
            state.Items.RemoveAt(index);
        }

        private static int Unread(MailSave state)
        {
            int count = 0;
            foreach (MailRec rec in state.Items)
            {
                if (rec.Folder == Inbox && !rec.Read) count++;
            }
            return count;
        }

        private static MailRec Message(JObject a)
        {
            int id = OsBridge.Span(a, "id", int.MaxValue, 0);
            MailRec rec = save.Items.Find(x => x.Id == id);
            if (rec == null) throw new OsBridgeException("bad_args", "There is no such message.");
            return rec;
        }

        private static void RequireAccount()
        {
            if (save.Username.Length == 0) throw new OsBridgeException("denied", NoAccount);
        }

        private static string FolderOf(JObject a)
        {
            string folder = OsBridge.Text(a, "folder");
            if (folder == Inbox || folder == Sent || folder == Trash || folder == Important) return folder;
            throw new OsBridgeException("bad_args", "folder must be inbox, important, sent or trash");
        }

        private static object Account()
        {
            return save.Username.Length == 0 ? null : (object)new { address = Address(), username = save.Username, domain = save.Domain };
        }

        private static string Address()
        {
            return save.Username.Length == 0 ? "" : save.Username + "@" + save.Domain;
        }

        private static bool Writable()
        {
            try
            {
                OsBridge.CheckWritable();
                return true;
            }
            catch (OsBridgeException)
            {
                return false;
            }
        }

        private static int NowMinutes()
        {
            return WorldClock.IsAvailable ? Minutes(WorldClock.Now) : 0;
        }

        private static int Minutes(WorldTime now)
        {
            return now.Day * MinutesPerDay + now.Hour * 60 + now.Minute;
        }

        private static string When(WorldTime now)
        {
            return string.Format("{0:0000}-{1:00}-{2:00} {3:00}:{4:00}", now.Year, now.Month, now.CalendarDay, now.Hour, now.Minute);
        }

        private static bool ValidUser(string user)
        {
            return user.Length >= MinUser && user.Length <= MaxUser && UserChars.IsMatch(user);
        }

        private static bool Reserved(string user)
        {
            return ReservedNames.Contains(user) || Array.Exists(Recipients, to => to.StartsWith(user + "@", StringComparison.Ordinal));
        }

        private static string Clip(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text ?? "";
            int cut = max - 3;
            if (char.IsHighSurrogate(text[cut - 1])) cut--;
            return text.Substring(0, cut) + "...";
        }

        private static string Printable(string text)
        {
            return string.IsNullOrEmpty(text) ? "" : Regex.Replace(text, @"[\p{Cc}\u2028\u2029-[\n]]", string.Empty);
        }

        private static string Line(string text)
        {
            return Regex.Replace(text ?? "", @"[\p{Cc}\u2028\u2029]", string.Empty).Trim();
        }

        private static string ReplyBody(string subject)
        {
            return "Thank you for your message about \"" + subject + "\".\n\nI am out of the office at the moment and will reply when I am back.\n\nThis is an automatic reply.";
        }

        private static string WelcomeBody(string address)
        {
            return "Your Stalburg Mail address is " + address + ".\n\nMessages you send from this app and mail you receive in the game are kept here.";
        }

        private static void Fail(string where, Exception e)
        {
            if (Failed.Add(where + " " + e.GetType().FullName)) Log.Error(where + " failed, logged once per type: " + e);
        }
    }
}
