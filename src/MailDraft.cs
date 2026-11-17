using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class MailDraft
    {
        internal string To = "";
        internal string Subject = "";
        internal string Body = "";
        private readonly Dictionary<int, string> replies = new Dictionary<int, string>();
        internal bool ComposeDirty { get { return Subject.Length > 0 || Body.Length > 0; } }
        internal bool Dirty { get { return ComposeDirty || replies.Count > 0; } }
        internal string Reply(int id) { string text; return replies.TryGetValue(id, out text) ? text : ""; }
        internal void SetReply(int id, string text)
        {
            if (string.IsNullOrEmpty(text)) replies.Remove(id); else replies[id] = text;
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
