using System;
using System.Collections.Generic;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitModWindows
    {
        private readonly VisualElement windows;
        private readonly Func<bool> valid;
        private ToolkitTaskbar taskbar;
        private readonly Dictionary<OsApp, VisualElement> opened = new Dictionary<OsApp, VisualElement>();
        internal ToolkitModWindows(VisualElement windows, Func<bool> valid) { this.windows = windows; this.valid = valid; }
        internal void SetTaskbar(ToolkitTaskbar value) { taskbar = value; }
        internal bool Has(OsApp app) { return app != null && opened.ContainsKey(app); }
        internal bool Open(OsApp app)
        {
            if (!valid() || app == null || app.BuildUi == null || !Os.Apps.Contains(app)) return false;
            VisualElement existing;
            if (opened.TryGetValue(app, out existing)) { if (taskbar == null || !taskbar.RestoreIfMinimized(existing)) existing.BringToFront(); return true; }
            VisualElement frame = new VisualElement(); frame.style.position = Position.Absolute; frame.style.left = 160; frame.style.top = 40;
            frame.style.width = app.Width > 0 ? app.Width : 600; frame.style.height = app.Height > 0 ? app.Height : 450;
            frame.style.paddingLeft = 6; frame.style.paddingRight = 6; frame.style.paddingBottom = 6; frame.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            VisualElement bar = new VisualElement(); bar.style.flexDirection = FlexDirection.Row; bar.style.height = 36;
            Label title = new Label(app.Title); title.enableRichText = false; title.style.flexGrow = 1; title.style.color = Color.black; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(delegate { Close(app, false); }); close.text = "×"; close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); frame.Add(bar); ToolkitUi.MakeDraggable(frame, bar);
            VisualElement content = new VisualElement(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; frame.Add(content);
            windows.Add(frame); opened.Add(app, frame);
            try
            {
                if (taskbar != null) taskbar.Register(frame, app.Title, app.Icon ?? "", false);
                app.BuildUi(content);
                if (!valid() || !Os.Apps.Contains(app) || !opened.ContainsKey(app)) { Close(app, true); return false; }
                Os.Opened(app.Id);
                return opened.ContainsKey(app);
            }
            catch (Exception e) { Close(app, true); Os.Crash("native app " + app.Id, e); return false; }
        }
        private static bool MayClose(OsApp app)
        {
            if (app.CanClose == null) return true;
            try { return app.CanClose(); }
            catch (Exception e) { Os.Crash("native app " + app.Id + " close guard", e); return false; }
        }
        internal bool CanCloseAll()
        {
            foreach (OsApp app in new List<OsApp>(opened.Keys)) if (!MayClose(app)) return false;
            return true;
        }
        internal void Close(OsApp app, bool force)
        {
            VisualElement frame;
            if (!opened.TryGetValue(app, out frame) || (!force && !MayClose(app))) return;
            VisualElement current;
            if (!opened.TryGetValue(app, out current) || current != frame) return;
            opened.Remove(app);
            if (taskbar != null) taskbar.Unregister(frame);
            frame.RemoveFromHierarchy(); Os.Closed(app.Id);
        }
        internal void Reconcile()
        {
            foreach (OsApp app in new List<OsApp>(opened.Keys)) if (!valid() || !Os.Apps.Contains(app)) Close(app, true);
        }
        internal void CloseAll()
        {
            foreach (OsApp app in new List<OsApp>(opened.Keys)) Close(app, true);
        }
    }
}
