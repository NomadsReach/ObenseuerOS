using System;
using System.Collections.Generic;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitTaskbar
    {
        private sealed class Entry
        {
            internal VisualElement Window;
            internal UnityEngine.UIElements.Button Button;
            internal bool Minimized, Maximized;
            internal StyleLength Left, Top, Right, Bottom, Width, Height;
            internal UnityEngine.UIElements.Button Maximize;
            internal ToolkitResizeHandle Resize;
        }
        internal readonly VisualElement Items;
        private readonly VisualElement windows;
        private readonly Func<string, Texture2D> texture;
        private readonly Dictionary<VisualElement, Entry> entries = new Dictionary<VisualElement, Entry>();

        internal ToolkitTaskbar(VisualElement windows, Func<string, Texture2D> texture)
        {
            this.windows = windows; this.texture = texture;
            ScrollView list = new ScrollView(ScrollViewMode.Horizontal);
            list.horizontalScrollerVisibility = ScrollerVisibility.Auto; list.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            list.style.height = 40; list.style.flexGrow = 1; list.style.flexShrink = 1; list.style.minWidth = 0;
            list.contentContainer.style.flexDirection = FlexDirection.Row; Items = list;
            Items.schedule.Execute(Refresh).Every(250);
        }
        internal void Register(VisualElement window, string title, string icon, bool pinned)
        {
            Entry entry = new Entry(); entry.Window = window; entries.Add(window, entry);
            if (!pinned)
            {
                UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(delegate { Click(window, null); });
                button.tooltip = title; button.style.width = 40; button.style.height = 36; button.style.marginTop = 2;
                Texture2D image = texture(icon);
                if (image != null)
                {
                    Image picture = new Image(); picture.image = image; picture.scaleMode = ScaleMode.ScaleToFit;
                    picture.style.width = 22; picture.style.height = 22; picture.style.alignSelf = Align.Center; button.Add(picture);
                }
                else { Label text = new Label(title.Substring(0, 1)); text.enableRichText = false; text.style.color = Color.white; button.Add(text); }
                button.style.display = DisplayStyle.None; entry.Button = button; Items.Add(button);
            }
            Label grip = new Label("◢"); grip.enableRichText = false; grip.tooltip = "Resize";
            grip.style.position = Position.Absolute; grip.style.right = 0; grip.style.bottom = 0;
            grip.style.width = 16; grip.style.height = 16; grip.style.color = (Color)new Color32(110, 130, 150, 255);
            grip.style.unityTextAlign = TextAnchor.MiddleCenter; window.Add(grip);
            entry.Resize = ToolkitUi.MakeResizable(window, grip, 360, 240, delegate { return !entry.Maximized; });
            VisualElement bar = window[0];
            UnityEngine.UIElements.Button minimize = new UnityEngine.UIElements.Button(delegate { Minimize(entry); });
            minimize.text = "−"; minimize.tooltip = "Minimize"; minimize.style.width = 40; minimize.style.height = 20; minimize.style.marginTop = 8;
            bar.Insert(Math.Max(0, bar.childCount - 1), minimize);
            UnityEngine.UIElements.Button maximize = new UnityEngine.UIElements.Button(delegate { ToggleMaximize(entry); });
            maximize.text = "□"; maximize.tooltip = "Maximize"; maximize.style.width = 40; maximize.style.height = 20; maximize.style.marginTop = 8;
            entry.Maximize = maximize; bar.Insert(Math.Max(0, bar.childCount - 1), maximize);
            bar.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent e)
            {
                if (!entry.Maximized || e.button != 0) return;
                VisualElement target = e.target as VisualElement;
                while (target != null && target != bar)
                {
                    if (target.focusable || target is UnityEngine.UIElements.Button) return;
                    target = target.parent;
                }
                if (target == bar) e.StopImmediatePropagation();
            }, TrickleDown.TrickleDown);
        }
        private void ToggleMaximize(Entry entry)
        {
            if (entry.Maximized) { RestoreBounds(entry); return; }
            entry.Resize.Cancel();
            VisualElement window = entry.Window;
            entry.Left = window.style.left; entry.Top = window.style.top;
            entry.Right = window.style.right; entry.Bottom = window.style.bottom;
            entry.Width = window.style.width; entry.Height = window.style.height;
            entry.Maximized = true;
            window.style.left = 0; window.style.top = 0; window.style.right = 0; window.style.bottom = 0;
            window.style.width = StyleKeyword.Auto; window.style.height = StyleKeyword.Auto;
            entry.Maximize.text = "❐"; entry.Maximize.tooltip = "Restore"; window.BringToFront();
            Refresh();
        }
        private void RestoreBounds(Entry entry)
        {
            VisualElement window = entry.Window;
            window.style.left = entry.Left; window.style.top = entry.Top; window.style.right = entry.Right; window.style.bottom = entry.Bottom;
            window.style.width = entry.Width; window.style.height = entry.Height;
            entry.Maximized = false; entry.Maximize.text = "□"; entry.Maximize.tooltip = "Maximize";
        }
        internal void Unregister(VisualElement window)
        {
            Entry entry;
            if (!entries.TryGetValue(window, out entry)) return;
            entries.Remove(window);
            if (entry.Resize != null) entry.Resize.Dispose();
            if (entry.Button != null) entry.Button.RemoveFromHierarchy();
        }
        internal void Click(VisualElement window, Action open)
        {
            Entry entry;
            if (!entries.TryGetValue(window, out entry)) return;
            bool visible = window.style.display != DisplayStyle.None;
            switch (ToolkitTaskbarState.Decide(visible, entry.Minimized, Front() == window))
            {
                case TaskbarAction.Open: if (open != null) open(); break;
                case TaskbarAction.Restore: entry.Minimized = false; window.style.display = DisplayStyle.Flex; window.BringToFront(); break;
                case TaskbarAction.Focus: window.BringToFront(); break;
                case TaskbarAction.Minimize: Minimize(entry); break;
            }
            Refresh();
        }
        internal bool RestoreIfMinimized(VisualElement window)
        {
            Entry entry;
            if (!entries.TryGetValue(window, out entry) || !entry.Minimized) return false;
            Click(window, null);
            return true;
        }
        private void Minimize(Entry entry)
        {
            entry.Resize.Cancel(); entry.Minimized = true; entry.Window.style.display = DisplayStyle.None; Refresh();
        }
        private VisualElement Front()
        {
            for (int i = windows.childCount - 1; i >= 0; i--)
                if (windows[i].style.display != DisplayStyle.None) return windows[i];
            return null;
        }
        private void Refresh()
        {
            VisualElement focused = Front();
            foreach (Entry entry in entries.Values)
            {
                bool visible = entry.Window.style.display != DisplayStyle.None;
                if (!visible) entry.Resize.Cancel();
                if (visible && entry.Minimized) entry.Minimized = false;
                if (!visible && !entry.Minimized && entry.Maximized) RestoreBounds(entry);
                if (entry.Button == null) continue;
                entry.Button.style.display = visible || entry.Minimized ? DisplayStyle.Flex : DisplayStyle.None;
                entry.Button.style.backgroundColor = focused == entry.Window ? (Color)new Color32(80, 110, 140, 255) : Color.clear;
            }
        }
        internal void Reset()
        {
            foreach (Entry entry in entries.Values)
            {
                entry.Resize.Cancel();
                if (entry.Minimized) { entry.Minimized = false; entry.Window.style.display = DisplayStyle.Flex; }
            }
            Refresh();
        }
    }
}
