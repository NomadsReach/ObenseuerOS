using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitDesktopMenu
    {
        private readonly VisualElement root;
        private readonly VisualElement desktop;
        private readonly ToolkitSettings settings;
        private readonly VisualElement menu;
        private readonly ScrollView wallpapers;
        private Vector2 position;

        internal ToolkitDesktopMenu(VisualElement root, VisualElement desktop, ToolkitSettings settings, Action openSettings, Action refresh)
        {
            this.root = root; this.desktop = desktop; this.settings = settings;
            menu = new VisualElement(); menu.style.position = Position.Absolute; menu.style.width = 200;
            menu.style.backgroundColor = (Color)new Color32(245, 245, 245, 255); menu.style.paddingTop = 4; menu.style.paddingBottom = 4;
            menu.style.display = DisplayStyle.None; menu.focusable = true;
            wallpapers = new ScrollView(); wallpapers.style.maxHeight = 260; wallpapers.style.display = DisplayStyle.None;
            AddButton(menu, "Change wallpaper  ▸", delegate { wallpapers.style.display = wallpapers.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None; });
            menu.Add(wallpapers);
            AddButton(menu, "Control Panel", delegate { Hide(); openSettings(); });
            AddButton(menu, "Refresh", delegate { Hide(); refresh(); });
            menu.RegisterCallback<GeometryChangedEvent>(delegate { Clamp(); });
            menu.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e) { if (e.keyCode == KeyCode.Escape) { Hide(); e.StopPropagation(); } });
            root.Add(menu);
            root.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent e)
            {
                VisualElement target = e.target as VisualElement;
                if (target == menu || (target != null && menu.Contains(target))) return;
                Hide();
                if (e.button == 1 && target == desktop) { Show(e.position); e.StopPropagation(); }
            }, TrickleDown.TrickleDown);
        }

        private void Show(Vector2 screenPosition)
        {
            wallpapers.Clear();
            foreach (KeyValuePair<string, string> option in settings.Wallpapers())
            {
                string key = option.Key;
                AddButton(wallpapers, option.Value, delegate { settings.SelectWallpaper(key); Hide(); });
            }
            wallpapers.style.display = DisplayStyle.None;
            position = root.WorldToLocal(screenPosition);
            menu.style.display = DisplayStyle.Flex; menu.BringToFront(); Clamp(); menu.Focus();
        }

        private void Clamp()
        {
            float width = menu.resolvedStyle.width, height = menu.resolvedStyle.height;
            if (float.IsNaN(width) || float.IsNaN(height)) return;
            menu.style.left = Mathf.Clamp(position.x, 0, Mathf.Max(0, desktop.layout.width - width));
            menu.style.top = Mathf.Clamp(position.y, 0, Mathf.Max(0, desktop.layout.height - height));
        }

        private void Hide() { menu.style.display = DisplayStyle.None; }

        private static void AddButton(VisualElement parent, string label, Action action)
        {
            Button button = new Button(action); button.text = label; button.style.height = 28;
            button.style.marginLeft = 2; button.style.marginRight = 2; button.style.unityTextAlign = TextAnchor.MiddleLeft;
            parent.Add(button);
        }
    }
}
