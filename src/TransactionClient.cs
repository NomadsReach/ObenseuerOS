using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class TransactionClient
    {
        private readonly Func<JObject, JObject> reply;
        private readonly Action checkSession;
        private readonly string app;
        private readonly Dictionary<string, string> pending = new Dictionary<string, string>();
        private long pendingVersion;

        internal TransactionClient(string app, Func<JObject, JObject> reply, Action checkSession) { this.app = app; this.reply = reply; this.checkSession = checkSession; }
        internal bool HasPending { get { return pending.Count > 0; } }
        internal string PendingDetails { get { return string.Join("\n", new List<string>(pending.Keys).ToArray()); } }
        internal string[] PendingKeys { get { return new List<string>(pending.Keys).ToArray(); } }
        internal long PendingVersion { get { return pendingVersion; } }
        internal bool PendingFor(string method, JObject args) { return pending.ContainsKey(Key(method, args)); }
        internal bool Forget(string method, JObject args) { if (!pending.Remove(Key(method, args))) return false; pendingVersion++; return true; }
        internal bool MayStart(string method, JObject args, bool acknowledged) { return !HasPending || acknowledged || PendingFor(method, args); }

        internal JToken Read(string method, JObject args) { return Call(method, args); }

        internal JToken Write(string method, JObject args, Func<JToken, bool> validate = null)
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
            try { result = Call(method, request); if (validate != null && !validate(result)) throw new InvalidOperationException("The game returned an invalid transaction receipt. Check recent transactions before trying again."); }
            catch { pendingVersion++; throw; }
            pending.Remove(key); pendingVersion++;
            return result;
        }

        private static string Key(string method, JObject args) { return method + " " + args.ToString(Formatting.None); }

        internal bool Resolve(string method, JObject args, string lookupMethod, JObject lookupArgs)
        {
            string key = Key(method, args), cid;
            if (!pending.TryGetValue(key, out cid)) return false;
            JObject request = (JObject)lookupArgs.DeepClone(); request["cid"] = cid;
            JObject result = Call(lookupMethod, request) as JObject;
            if (result == null || result["found"] == null || result["found"].Type != JTokenType.Boolean) throw new InvalidOperationException("The game returned an invalid transaction result.");
            if (!(bool)result["found"]) return false;
            pending.Remove(key); pendingVersion++; return true;
        }

        private JToken Call(string method, JObject args)
        {
            if (checkSession != null) checkSession();
            if (reply == null) throw new InvalidOperationException("This service works inside the game.");
            JObject request = new JObject(new JProperty("id", 1), new JProperty("app", app), new JProperty("method", method), new JProperty("args", args ?? new JObject()));
            JObject response = reply(request);
            if (response == null) throw new InvalidOperationException("The game did not answer. Check recent transactions before trying again.");
            if ((bool?)response["ok"] != true) throw new InvalidOperationException((string)response["message"] ?? "Bank request failed.");
            JToken result = response["result"];
            if (result == null || result.Type == JTokenType.Null) throw new InvalidOperationException("The game did not return a result. Check recent transactions before trying again.");
            return result;
        }
    }

    internal static class TransactionClients
    {
        private static int session;
        private static readonly Dictionary<string, TransactionClient> clients = new Dictionary<string, TransactionClient>();
        private static bool started;
        internal static TransactionClient ForSession(string app, int id, Func<JObject, JObject> reply, Action checkSession)
        {
            if (!started || session != id) { started = true; session = id; clients.Clear(); }
            TransactionClient client;
            if (!clients.TryGetValue(app, out client)) { client = new TransactionClient(app, reply, checkSession); clients.Add(app, client); }
            return client;
        }
    }
}
