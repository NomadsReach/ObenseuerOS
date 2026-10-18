using System.Collections.Generic;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal sealed class GameMail
    {
        public string Key;
        public string From;
        public string Subject;
        public string Body;
    }

    internal static class OsMailGame
    {
        private const string UnknownFrom = "Unknown sender";
        private const string MailboxFrom = "Mailbox";

        internal static List<GameMail> Collect()
        {
            List<GameMail> found = new List<GameMail>();
            Computer(found);
            Letters(found);
            return found;
        }

        private static void Computer(List<GameMail> found)
        {
            if (InteractableComputer.currentComputer == null) return;
            string id = ComputerContents.IdOf(InteractableComputer.currentComputer.gameObject);
            if (string.IsNullOrEmpty(id)) return;
            List<ComputerDocInfo> emails = ComputerContents.Emails(id);
            for (int i = 0; i < emails.Count; i++)
            {
                found.Add(new GameMail { Key = "pc:" + id + "#" + i, From = string.IsNullOrEmpty(emails[i].Sender) ? UnknownFrom : emails[i].Sender, Subject = emails[i].Title ?? "", Body = emails[i].Body ?? "" });
            }
        }

        private static void Letters(List<GameMail> found)
        {
            foreach (MailInfo mail in Mail.Inbox(MailKind.Letter))
            {
                if (!mail.IsNote) continue;
                string body = mail.Body ?? "";
                found.Add(new GameMail { Key = "letter:" + mail.Name + "#" + Hash(body), From = string.IsNullOrEmpty(mail.Sender) ? MailboxFrom : mail.Sender, Subject = mail.Name ?? "", Body = body });
            }
        }

        private static string Hash(string text)
        {
            ulong hash = 14695981039346656037UL;
            foreach (char c in text) hash = (hash ^ c) * 1099511628211UL;
            return hash.ToString("x16");
        }
    }
}
