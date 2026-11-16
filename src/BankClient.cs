using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class BankClient
    {
        private readonly Func<JObject, JObject> reply;
        private readonly Action checkSession;
        private readonly Dictionary<string, string> pending = new Dictionary<string, string>();
        private long pendingVersion;

        internal BankClient(Func<JObject, JObject> reply, Action checkSession) { this.reply = reply; this.checkSession = checkSession; }
        internal bool HasPending { get { return pending.Count > 0; } }
        internal long PendingVersion { get { return pendingVersion; } }
        internal bool PendingFor(string method, JObject args) { return pending.ContainsKey(Key(method, args)); }
        internal bool MayStart(string method, JObject args, bool acknowledged) { return !HasPending || acknowledged || PendingFor(method, args); }

        internal JToken Read(string method, JObject args) { return Call(method, args); }

        internal JToken Write(string method, JObject args)
        {
            if (checkSession != null) checkSession();
            string key = Key(method, args);
            string cid;
            if (!pending.TryGetValue(key, out cid))
            {
                if (pending.Count >= 20) throw new InvalidOperationException("Too many uncertain transactions. Check recent transactions before trying again.");
                cid = Guid.NewGuid().ToString("N"); pending.Add(key, cid); pendingVersion++;
            }
            JObject request = (JObject)args.DeepClone(); request["cid"] = cid;
            JToken result;
            try { result = Call(method, request); }
            catch { pendingVersion++; throw; }
            pending.Remove(key); pendingVersion++;
            return result;
        }

        private static string Key(string method, JObject args) { return method + " " + args.ToString(Formatting.None); }

        private JToken Call(string method, JObject args)
        {
            if (checkSession != null) checkSession();
            if (reply == null) throw new InvalidOperationException("The bank works inside the game.");
            JObject request = new JObject(new JProperty("id", 1), new JProperty("app", "bank"), new JProperty("method", method), new JProperty("args", args ?? new JObject()));
            JObject response = reply(request);
            if (response == null) throw new InvalidOperationException("The game did not answer. Check recent transactions before trying again.");
            if ((bool?)response["ok"] != true) throw new InvalidOperationException((string)response["message"] ?? "Bank request failed.");
            JToken result = response["result"];
            if (result == null || result.Type == JTokenType.Null) throw new InvalidOperationException("The game did not return a result. Check recent transactions before trying again.");
            return result;
        }
    }

    internal static class BankClients
    {
        private static int session;
        private static BankClient client;
        internal static BankClient ForSession(int id, Func<JObject, JObject> reply, Action checkSession)
        {
            if (client == null || session != id) { session = id; client = new BankClient(reply, checkSession); }
            return client;
        }
    }
}
