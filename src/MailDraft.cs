namespace ObenseuerOS
{
    internal sealed class MailDraft
    {
        internal string To = "";
        internal string Subject = "";
        internal string Body = "";
        internal bool Dirty { get { return Subject.Length > 0 || Body.Length > 0; } }
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
