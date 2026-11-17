using System;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitSite
    {
        private readonly VisualElement content;
        private OsApp current;
        private VisualElement page;
        private OsSitePage lifecycle;
        private string url;
        private bool changing, checking;

        internal ToolkitSite(VisualElement content) { this.content = content; }
        internal bool IsCurrent(string value) { return current != null && url == value && Os.Apps.Contains(current); }
        internal bool IsSite(OsApp value) { return current == value; }

        internal bool CanClose() { return !changing && CheckClose(); }

        private bool CheckClose()
        {
            if (checking) return false;
            OsApp original = current; OsSitePage originalLifecycle = lifecycle;
            if (original == null || originalLifecycle == null || originalLifecycle.CanClose == null) return true;
            checking = true;
            try { return originalLifecycle.CanClose() && current == original && lifecycle == originalLifecycle && Os.Apps.Contains(original); }
            catch (Exception e) { Os.Crash("native site " + original.Id + " close guard", e); return false; }
            finally { checking = false; }
        }

        internal bool Dismiss()
        {
            if (changing) return false;
            changing = true;
            try { if (!CheckClose()) return false; Close(false); return true; }
            finally { changing = false; }
        }

        internal bool Transition(OsApp next, string nextUrl, Func<bool> valid, Action commit)
        {
            if (changing || !valid()) return false;
            changing = true;
            VisualElement candidate = null;
            bool built = false;
            OsSitePage candidateLifecycle = null;
            try
            {
                if (!CheckClose() || !valid()) return false;
                if (next != null && next.BuildSite != null)
                {
                    if (!Os.Apps.Contains(next)) return false;
                    candidate = new VisualElement();
                    int start = nextUrl.IndexOfAny(new[] { '/', '?', '#' });
                    candidateLifecycle = next.BuildSite(candidate, start < 0 ? "" : nextUrl.Substring(start));
                    built = true;
                    if (!valid() || !Os.Apps.Contains(next)) return false;
                }
                commit();
                Close(false);
                if (!valid() || (next != null && !Os.Apps.Contains(next))) return false;
                content.Clear();
                if (candidate != null)
                {
                    content.Add(candidate);
                    if (!valid() || !Os.Apps.Contains(next)) return false;
                    current = next; page = candidate; lifecycle = candidateLifecycle; url = nextUrl; candidate = null;
                }
                return true;
            }
            catch (Exception e) { Os.Crash("native site transition", e); return false; }
            finally
            {
                try
                {
                    if (candidate != null && built)
                        try { candidate.RemoveFromHierarchy(); }
                        finally { Notify(next, candidateLifecycle, true); }
                }
                finally { changing = false; }
            }
        }

        internal void ForceClose(Action cleanup)
        {
            bool previous = changing; changing = true;
            try { try { Close(true); } finally { if (cleanup != null) cleanup(); } }
            finally { changing = previous; }
        }

        private void Close(bool forced)
        {
            OsApp old = current; VisualElement oldPage = page; OsSitePage oldLifecycle = lifecycle;
            current = null; page = null; lifecycle = null; url = null;
            try { if (oldPage != null) oldPage.RemoveFromHierarchy(); }
            finally { if (old != null) Notify(old, oldLifecycle, forced); }
        }

        private static void Notify(OsApp app, OsSitePage instance, bool forced)
        {
            if (app == null || instance == null || instance.OnClosed == null) return;
            try { instance.OnClosed(forced); }
            catch (Exception e) { Os.Crash("native site " + app.Id + " close notification", e); }
        }
    }
}
