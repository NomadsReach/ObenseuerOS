using System;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class MailClient
    {
        private readonly Func<JObject, JObject> reply;
        private readonly Action checkSession;

        internal MailClient(Func<JObject, JObject> reply, Action checkSession)
        {
            this.reply = reply;
            this.checkSession = checkSession;
        }

        internal bool SendReply(MailDraft draft, int id, string to, string subject)
        {
            string text = draft.Reply(id);
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase)) subject = "Re: " + subject;
            JObject args = new JObject(new JProperty("to", to), new JProperty("subject", subject), new JProperty("body", text));
            if (Call("mail.send", args) == null) return false;
            draft.SetReply(id, "");
            return true;
        }

        internal JToken Call(string method, JObject args)
        {
            if (checkSession != null) checkSession();
            if (reply == null) throw new InvalidOperationException("Stalburg Mail works inside the game.");
            JObject request = new JObject(new JProperty("id", 1), new JProperty("app", "mail"), new JProperty("method", method), new JProperty("args", args ?? new JObject()));
            JObject response = reply(request);
            if (response == null) throw new InvalidOperationException("Mail service did not respond.");
            if ((bool?)response["ok"] != true) throw new InvalidOperationException((string)response["message"] ?? "Mail request failed.");
            return response["result"];
        }
    }
}
