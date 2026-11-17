using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitNotifications
    {
        internal readonly UnityEngine.UIElements.Button Tray;
        private readonly VisualElement panel, popups;
        private readonly ScrollView list;
        private readonly List<OsNotice> notices = new List<OsNotice>();
        private bool unread;

        internal ToolkitNotifications(VisualElement root)
        {
            Tray = new UnityEngine.UIElements.Button(Toggle); Tray.text = "▲"; Tray.tooltip = "Notifications";
            Tray.style.width = 32; Tray.style.height = 30; Tray.style.marginTop = 5;
            panel = new VisualElement(); panel.style.position = Position.Absolute; panel.style.right = 8; panel.style.bottom = 40;
            panel.style.width = 320; panel.style.maxHeight = 400; panel.style.paddingLeft = 8; panel.style.paddingRight = 8;
            panel.style.paddingTop = 8; panel.style.paddingBottom = 8; panel.style.backgroundColor = (Color)new Color32(38, 50, 64, 255);
            panel.style.display = DisplayStyle.None;
            Label title = Text("Notifications"); title.style.fontSize = 18; panel.Add(title);
            UnityEngine.UIElements.Button clear = new UnityEngine.UIElements.Button(Clear); clear.text = "Clear all"; panel.Add(clear);
            list = new ScrollView(); list.style.maxHeight = 320; panel.Add(list); root.Add(panel);
            popups = new VisualElement(); popups.style.position = Position.Absolute; popups.style.right = 8; popups.style.bottom = 48;
            popups.style.width = 320; popups.pickingMode = PickingMode.Ignore; root.Add(popups);
        }
        private static Label Text(string value)
        {
            Label label = new Label(value ?? ""); label.enableRichText = false; label.style.whiteSpace = WhiteSpace.Normal; label.style.color = Color.white; return label;
        }
        private static VisualElement Entry(OsNotice notice)
        {
            VisualElement entry = new VisualElement(); entry.style.paddingLeft = 8; entry.style.paddingRight = 8; entry.style.paddingTop = 6; entry.style.paddingBottom = 6;
            entry.style.marginBottom = 4; entry.style.backgroundColor = (Color)new Color32(45, 65, 83, 255);
            if (!string.IsNullOrEmpty(notice.From)) entry.Add(Text(notice.From));
            Label title = Text(notice.Title); title.style.unityFontStyleAndWeight = FontStyle.Bold; entry.Add(title);
            if (!string.IsNullOrEmpty(notice.Body)) entry.Add(Text(notice.Body));
            return entry;
        }
        internal void Add(OsNotice notice)
        {
            if (notice == null) return;
            notices.Insert(0, notice); if (notices.Count > 20) notices.RemoveAt(notices.Count - 1);
            unread = panel.style.display == DisplayStyle.None; Refresh();
            VisualElement popup = Entry(notice); popups.Add(popup);
            while (popups.childCount > 4) popups.RemoveAt(0);
            popup.schedule.Execute(delegate() { popup.RemoveFromHierarchy(); }).StartingIn(6000);
        }
        private void Refresh()
        {
            Tray.text = unread ? "▲ •" : "▲";
            list.Clear();
            if (notices.Count == 0) list.Add(Text("No notifications"));
            else foreach (OsNotice notice in notices) list.Add(Entry(notice));
        }
        private void Toggle()
        {
            if (panel.style.display != DisplayStyle.None) { panel.style.display = DisplayStyle.None; return; }
            unread = false; Refresh(); panel.style.display = DisplayStyle.Flex; panel.BringToFront();
        }
        internal void Clear()
        {
            notices.Clear(); unread = false; popups.Clear(); panel.style.display = DisplayStyle.None; Refresh();
        }
    }
}
