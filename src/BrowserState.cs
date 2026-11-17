using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal sealed class BrowserState
    {
        internal const string Path = "/browser/state.json";
        private readonly NotepadStorage storage;
        private readonly JObject data;
        private readonly JArray history, marks;
        private int index;

        internal BrowserState(NotepadStorage storage)
        {
            this.storage = storage;
            string raw = null;
            if (storage != null)
            {
                try { raw = storage.Read(Path); }
                catch (InvalidOperationException e) { if (e.Message != "no such file") throw; }
            }
            if (raw == null) data = new JObject(new JProperty("marks", new JArray()), new JProperty("history", new JArray()));
            else
            {
                try { data = JObject.Parse(raw); }
                catch (JsonException) { throw new InvalidOperationException("Browser history could not be read; it has not been changed."); }
            }
            marks = data["marks"] as JArray; history = data["history"] as JArray;
            if (marks == null || history == null || !Valid(marks) || !Valid(history)) throw new InvalidOperationException("Browser history has an unsupported format; it has not been changed.");
            index = history.Count - 1;
        }

        private static bool Valid(JArray entries)
        {
            foreach (JToken entry in entries)
                if (!(entry is JObject) || entry["url"] == null || entry["url"].Type != JTokenType.String || string.IsNullOrEmpty((string)entry["url"]) || entry["title"] == null || entry["title"].Type != JTokenType.String) return false;
            return true;
        }
        internal string Current { get { return index < 0 ? "" : (string)history[index]["url"]; } }
        internal bool CanBack { get { return index > 0; } }
        internal bool CanForward { get { return index >= 0 && index < history.Count - 1; } }
        internal JArray History { get { return (JArray)history.DeepClone(); } }
        internal JArray Marks { get { return (JArray)marks.DeepClone(); } }
        internal bool IsMarked { get { return FindMark(Current) >= 0; } }

        internal bool Visit(string url, string title)
        {
            if (string.IsNullOrEmpty(url) || url == Current) return false;
            Commit(delegate
            {
                while (history.Count > index + 1) history.RemoveAt(history.Count - 1);
                history.Add(new JObject(new JProperty("url", url), new JProperty("title", title ?? "")));
                while (history.Count > 100) history.RemoveAt(0);
                index = history.Count - 1;
            });
            return true;
        }
        internal void Move(int delta)
        {
            int next = index + delta;
            if (next >= 0 && next < history.Count) index = next;
        }
        internal void ToggleMark(string title)
        {
            if (Current.Length == 0) return;
            Commit(delegate
            {
                int found = FindMark(Current);
                if (found >= 0) marks.RemoveAt(found);
                else
                {
                    if (marks.Count >= 50) throw new InvalidOperationException("The bookmark limit is 50.");
                    marks.Add(new JObject(new JProperty("url", Current), new JProperty("title", title ?? "")));
                }
            });
        }
        private int FindMark(string url)
        {
            for (int i = 0; i < marks.Count; i++) if ((string)marks[i]["url"] == url) return i;
            return -1;
        }
        private void Commit(Action change)
        {
            JArray oldHistory = (JArray)history.DeepClone(), oldMarks = (JArray)marks.DeepClone(); int oldIndex = index;
            try { change(); if (storage != null) storage.Write(Path, data.ToString(Formatting.None)); }
            catch
            {
                history.RemoveAll(); foreach (JToken item in oldHistory) history.Add(item.DeepClone());
                marks.RemoveAll(); foreach (JToken item in oldMarks) marks.Add(item.DeepClone());
                index = oldIndex; throw;
            }
        }
    }
}
