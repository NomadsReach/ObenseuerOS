using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class EyeState
    {
        internal const string Path = "/eyebook/state.json";
        private readonly NotepadStorage storage;
        private JObject data;
        internal EyeState(NotepadStorage storage)
        {
            this.storage = storage; string raw = null;
            if (storage != null) { try { raw = storage.Read(Path); } catch (InvalidOperationException e) { if (e.Message != "no such file") throw; } }
            if (raw == null) data = new JObject(new JProperty("name", ""), new JProperty("acts", new JArray()), new JProperty("status", new JArray()));
            else { try { data = JObject.Parse(raw); } catch (JsonException) { throw new InvalidOperationException("EYEbook data could not be read; it has not been changed."); } }
            if (data["name"] == null || data["name"].Type != JTokenType.String || !(data["acts"] is JArray) || !(data["status"] is JArray) || !Valid()) throw new InvalidOperationException("EYEbook data has an unsupported format; it has not been changed.");
        }
        private bool Valid()
        {
            if (((string)data["name"]).Length > 40 || ((JArray)data["acts"]).Count > 40 || ((JArray)data["status"]).Count > 10) return false;
            foreach (JToken act in (JArray)data["acts"])
            {
                if (!(act is JObject) || act["k"] == null || act["k"].Type != JTokenType.String || act["p"] == null || act["p"].Type != JTokenType.Integer) return false;
                string kind = (string)act["k"]; long post; if (!long.TryParse(act["p"].ToString(Formatting.None), out post)) return false;
                if ((kind != "like" && kind != "share" && kind != "comment") || post < 0 || post >= 7) return false;
                if (kind == "comment" && (act["t"] == null || act["t"].Type != JTokenType.String || ((string)act["t"]).Length > 140)) return false;
            }
            foreach (JToken status in (JArray)data["status"]) if (!(status is JObject) || status["t"] == null || status["t"].Type != JTokenType.String || string.IsNullOrEmpty((string)status["t"]) || ((string)status["t"]).Length > 140) return false;
            return true;
        }
        internal string Name { get { return (string)data["name"]; } }
        internal JArray Actions { get { return (JArray)data["acts"].DeepClone(); } }
        internal JArray Statuses { get { return (JArray)data["status"].DeepClone(); } }
        internal bool Has(string kind, int post)
        {
            foreach (JToken act in (JArray)data["acts"]) if ((string)act["k"] == kind && (int)act["p"] == post) return true;
            return false;
        }
        internal void Login(string name)
        {
            string value = (name ?? "").Trim(); if (value.Length == 0) throw new InvalidOperationException("EYEbook could not read your name.");
            if (value.Length > 40) value = value.Substring(0, 40);
            Commit(delegate { data["name"] = value; });
        }
        internal void Like(int post)
        {
            Require(post); Commit(delegate
            {
                JArray acts = (JArray)data["acts"]; bool found = Has("like", post);
                if (found) { for (int i = acts.Count - 1; i >= 0; i--) if ((string)acts[i]["k"] == "like" && (int)acts[i]["p"] == post) acts.RemoveAt(i); }
                else Add(acts, new JObject(new JProperty("k", "like"), new JProperty("p", post)), 40);
            });
        }
        internal void Share(int post)
        {
            Require(post); if (Has("share", post)) throw new InvalidOperationException("You already shared this post.");
            Commit(delegate { Add((JArray)data["acts"], new JObject(new JProperty("k", "share"), new JProperty("p", post)), 40); });
        }
        internal void Comment(int post, string text)
        {
            Require(post); string value = Message(text); if (value.Length == 0) return;
            Commit(delegate { Add((JArray)data["acts"], new JObject(new JProperty("k", "comment"), new JProperty("p", post), new JProperty("t", value)), 40); });
        }
        internal void Publish(string text)
        {
            Require(0); string value = Message(text); if (value.Length == 0) return;
            Commit(delegate { Add((JArray)data["status"], new JObject(new JProperty("t", value)), 10); });
        }
        private static string Message(string text) { string value = (text ?? "").Trim(); return value.Length > 140 ? value.Substring(0, 140) : value; }
        private void Require(int post) { if (Name.Length == 0) throw new InvalidOperationException("Log in to EYEbook first."); if (post < 0 || post >= 7) throw new InvalidOperationException("Unknown post."); }
        private static void Add(JArray list, JObject value, int limit) { list.Add(value); while (list.Count > limit) list.RemoveAt(0); }
        private void Commit(Action change)
        {
            JObject before = (JObject)data.DeepClone();
            try { change(); if (storage != null) storage.Write(Path, data.ToString(Formatting.None)); }
            catch { data = before; throw; }
        }
    }

    internal static class EyeStates
    {
        private static bool started;
        private static int session;
        private static EyeState current;
        internal static EyeState ForSession(int id, NotepadStorage storage)
        {
            if (!started || session != id)
            {
                EyeState next = new EyeState(storage);
                current = next; session = id; started = true;
            }
            return current;
        }
    }
}
