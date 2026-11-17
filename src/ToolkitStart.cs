using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitStart
    {
        internal readonly VisualElement Menu;
        private readonly TextField search;
        private readonly List<UnityEngine.UIElements.Button> buttons = new List<UnityEngine.UIElements.Button>();
        private readonly string[] names;
        private readonly Func<int, bool> open;

        internal ToolkitStart(string[] names, string[] icons, Func<string, Texture2D> texture, Func<int, bool> open, Action close)
        {
            this.names = names; this.open = open;
            Menu = new VisualElement(); Menu.style.position = UnityEngine.UIElements.Position.Absolute;
            Menu.style.left = 0; Menu.style.bottom = 40; Menu.style.width = 460; Menu.style.maxHeight = Length.Percent(90);
            Menu.style.paddingLeft = 12; Menu.style.paddingRight = 12; Menu.style.paddingTop = 12; Menu.style.paddingBottom = 12;
            Menu.style.backgroundColor = (Color)new Color32(21, 40, 63, 255); Menu.style.display = DisplayStyle.None;
            string username = OsView.UserName();
            Label user = new Label(string.IsNullOrEmpty(username) ? "Stalburg resident" : username);
            user.style.color = Color.white; user.style.fontSize = 20; Menu.Add(user);
            search = new TextField(); search.style.marginTop = 8; search.style.marginBottom = 8;
            search.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { Filter(e.newValue); });
            search.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    for (int i = 0; i < buttons.Count; i++)
                    {
                        if (buttons[i].style.display == DisplayStyle.None || !buttons[i].enabledSelf) continue;
                        Launch(i); break;
                    }
                    e.StopPropagation();
                }
            }, TrickleDown.TrickleDown);
            Menu.Add(search);
            ScrollView scroll = new ScrollView(); scroll.style.flexShrink = 1;
            scroll.contentContainer.style.flexDirection = FlexDirection.Row;
            scroll.contentContainer.style.flexWrap = Wrap.Wrap;
            Menu.Add(scroll);
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(delegate { Launch(index); });
                button.style.width = 96; button.style.height = 96; button.style.marginRight = 8; button.style.marginBottom = 8;
                button.style.backgroundColor = (Color)new Color32(47, 102, 179, 255);
                Image icon = new Image(); icon.image = texture(icons[i]); icon.scaleMode = ScaleMode.ScaleToFit;
                icon.style.width = 32; icon.style.height = 32; icon.style.alignSelf = Align.Center; icon.style.marginTop = 8;
                icon.tintColor = i == 2 ? (Color)new Color32(242, 201, 76, 255) : Color.white;
                button.Add(icon);
                Label label = new Label(names[i]); label.style.color = Color.white; label.style.fontSize = 12; label.style.whiteSpace = WhiteSpace.Normal;
                button.Add(label);
                button.SetEnabled(i >= 0 && i <= 13);
                buttons.Add(button); scroll.Add(button);
            }
            UnityEngine.UIElements.Button power = new UnityEngine.UIElements.Button(delegate { Hide(); close(); }); power.text = "Close Viewports 8";
            power.style.alignSelf = Align.FlexEnd; Menu.Add(power);
            Menu.RegisterCallback<KeyDownEvent>(Navigate, TrickleDown.TrickleDown);
        }

        internal void Toggle()
        {
            if (Menu.style.display != DisplayStyle.None) { Hide(); return; }
            search.value = ""; Filter(""); Menu.style.display = DisplayStyle.Flex; Menu.BringToFront(); search.Focus();
        }

        internal void Hide() { Menu.style.display = DisplayStyle.None; }

        private void Launch(int index) { if (open(index)) Hide(); }

        private void Filter(string query)
        {
            string text = (query ?? "").Trim();
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].style.display = names[i].IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Navigate(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape) { Hide(); e.StopPropagation(); return; }
            int step;
            if (e.keyCode == KeyCode.LeftArrow) step = -1;
            else if (e.keyCode == KeyCode.RightArrow) step = 1;
            else if (e.keyCode == KeyCode.UpArrow) step = -4;
            else if (e.keyCode == KeyCode.DownArrow) step = 4;
            else return;
            List<UnityEngine.UIElements.Button> visible = new List<UnityEngine.UIElements.Button>();
            foreach (UnityEngine.UIElements.Button button in buttons)
                if (button.style.display != DisplayStyle.None) visible.Add(button);
            VisualElement target = e.target as VisualElement;
            int index = -1;
            for (int i = 0; i < visible.Count; i++) if (visible[i].Contains(target)) { index = i; break; }
            if (index < 0)
            {
                if (!search.Contains(target) || e.keyCode != KeyCode.DownArrow) return;
                foreach (UnityEngine.UIElements.Button button in visible) if (button.enabledSelf) { button.Focus(); break; }
            }
            else if (e.keyCode == KeyCode.UpArrow && index + step < 0) search.Focus();
            else
            {
                int next = index + step;
                while (next >= 0 && next < visible.Count && !visible[next].enabledSelf) next += step;
                if (next >= 0 && next < visible.Count) visible[next].Focus();
            }
            e.StopPropagation();
        }
    }
}
