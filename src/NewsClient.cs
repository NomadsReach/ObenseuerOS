using System;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class NewsClient
    {
        private readonly Func<JObject, JObject> reply;
        private readonly Action checkSession;
        internal NewsClient(Func<JObject, JObject> reply, Action checkSession) { this.reply = reply; this.checkSession = checkSession; }
        internal bool Available { get { return reply != null; } }

        internal JObject Call(string method, JObject args)
        {
            if (checkSession != null) checkSession();
            if (reply == null) throw new InvalidOperationException("The Stalburg Times works inside the game.");
            JObject request = new JObject(new JProperty("id", 1), new JProperty("app", "news"), new JProperty("method", method), new JProperty("args", args ?? new JObject()));
            JObject response = reply(request);
            if (response == null || (bool?)response["ok"] != true) throw new InvalidOperationException(response == null ? "News service did not respond." : (string)response["message"] ?? "News request failed.");
            JObject result = response["result"] as JObject;
            if (result == null) throw new InvalidOperationException("The game sent an unexpected news response.");
            return result;
        }
    }
}
