using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class MailDraft
    {
        private string to = "", subject = "", body = "";
        private long revision;
        internal long Revision { get { return revision; } }
        internal string To { get { return to; } set { Set(ref to, value); } }
        internal string Subject { get { return subject; } set { Set(ref subject, value); } }
        internal string Body { get { return body; } set { Set(ref body, value); } }
        private void Set(ref string field, string value)
        {
            value = value ?? "";
            if (field == value) return;
            field = value; revision++;
        }
        private readonly Dictionary<int, string> replies = new Dictionary<int, string>();
        internal bool ComposeDirty { get { return Subject.Length > 0 || Body.Length > 0; } }
        internal bool Dirty { get { return ComposeDirty || replies.Count > 0; } }
        internal string Reply(int id) { string text; return replies.TryGetValue(id, out text) ? text : ""; }
        internal void SetReply(int id, string text)
        {
            string previous = Reply(id); text = text ?? "";
            if (previous == text) return;
            if (text.Length == 0) replies.Remove(id); else replies[id] = text;
            revision++;
        }
        internal void Clear() { To = ""; Subject = ""; Body = ""; }
    }

    internal static class MailDrafts
    {
        private static int session;
        private static MailDraft draft;

        internal static MailDraft ForSession(int id)
        {
            if (draft == null || session != id) { session = id; draft = new MailDraft(); }
            return draft;
        }
    }
}
