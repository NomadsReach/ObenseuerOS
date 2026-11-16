using System;
using ObenseuerCore.Ui;
using TMPro;
using UnityEngine;

using UnityEngine.UI;

namespace ObenseuerOS
{
    internal static class OsUguiPrototype
    {
        private static readonly string[] Names = { "Stalburg Mail", "Stalburg Web", "Explorer", "Recycle Bin", "Control Panel", "Stal-Ex", "Command Prompt", "Notepad", "Stalburg Bank", "Stalburg Times", "TOR-Turnip Chat", "Shop", "Minesweeper", "Solitaire" };
        private static readonly string[] Glyphs = { "M", "W", "F", "R", "C", "S", ">_", "N", "B", "T", "C", "S", "M", "S" };
        private static Font font;

        internal static void Build(RectTransform host)
        {
            float scale = Mathf.Min(host.rect.width / 1920f, host.rect.height / 1080f);
            if (scale <= 0f) scale = 1f;
            scale = Mathf.Clamp(scale, 0.75f, 2f);
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Tahoma", "Arial" }, 14);
            RectTransform desktop = DesktopUi.Panel(host, "Desktop", new Color32(45, 95, 139, 255));
            DesktopUi.Stretch(desktop);
            int rows = Mathf.Max(1, Mathf.FloorToInt((host.rect.height / scale - 56f) / 104f));
            GameObject window = MakeWindow(desktop, font, scale, rows);
            window.SetActive(false);
            for (int i = 0; i < Names.Length; i++)
            {
                RectTransform tile = DesktopUi.Panel(desktop, Names[i], Color.clear);
                DesktopUi.TopLeft(tile, 8f + (i / rows) * 88f, 8f + (i % rows) * 104f, 88f, 104f, scale);
                Text glyph = DesktopUi.Label(tile, font, Glyphs[i], Mathf.RoundToInt(33 * scale), i == 2 ? (Color)new Color32(242, 201, 76, 255) : Color.white, TextAnchor.MiddleCenter);
                DesktopUi.TopLeft((RectTransform)glyph.transform, 20f, 4f, 48f, 48f, scale);
                Text title = DesktopUi.Label(tile, font, Names[i], Mathf.RoundToInt(12 * scale), Color.white, TextAnchor.UpperCenter);
                DesktopUi.TopLeft((RectTransform)title.transform, 4f, 56f, 80f, 42f, scale);
                if (i == 7)
                {
                    UnityEngine.UI.Button open = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
                    open.targetGraphic = tile.GetComponent<Image>();
                    open.onClick.AddListener(delegate { window.SetActive(true); window.transform.SetSiblingIndex(desktop.childCount - 2); });
                }
            }
            RectTransform taskbar = DesktopUi.Panel(desktop, "Taskbar", new Color32(205, 218, 232, 209));
            taskbar.anchorMin = new Vector2(0f, 0f);
            taskbar.anchorMax = new Vector2(1f, 0f);
            taskbar.pivot = new Vector2(0f, 0f);
            taskbar.offsetMin = Vector2.zero;
            taskbar.offsetMax = new Vector2(0f, 40f * scale);
            Text start = DesktopUi.Label(taskbar, font, "⊞", Mathf.RoundToInt(26 * scale), new Color32(30, 30, 30, 255), TextAnchor.MiddleCenter);
            DesktopUi.TopLeft((RectTransform)start.transform, 0f, 0f, 40f, 40f, scale);
            Text clock = DesktopUi.Label(taskbar, font, DateTime.Now.ToString("HH:mm\ndd/MM/yyyy"), Mathf.RoundToInt(12 * scale), new Color32(30, 30, 30, 255), TextAnchor.MiddleCenter);
            RectTransform clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = new Vector2(1f, 0f);
            clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.offsetMin = new Vector2(-100f * scale, 0f);
            clockRect.offsetMax = Vector2.zero;
        }

        private static GameObject MakeWindow(RectTransform parent, Font font, float scale, int rows)
        {
            RectTransform frame = DesktopUi.Panel(parent, "Notepad window", new Color32(190, 205, 222, 217));
            int columns = (Names.Length + rows - 1) / rows;
            float width = Mathf.Min(600f, Mathf.Max(1f, parent.rect.width / scale - 64f));
            float height = Mathf.Min(400f, Mathf.Max(1f, parent.rect.height / scale - 64f));
            float x = Mathf.Min(16f + columns * 88f + 24f + 32f, Mathf.Max(0f, parent.rect.width / scale - width));
            float y = Mathf.Min(32f, Mathf.Max(0f, parent.rect.height / scale - height));
            DesktopUi.TopLeft(frame, x, y, width, height, scale);
            RectTransform bar = DesktopUi.Panel(frame, "Title bar", Color.clear);
            DesktopUi.TopLeft(bar, 6f, 6f, Mathf.Max(0f, width - 12f), 30f, scale);
            DesktopUi.MakeDraggable(bar, frame);
            Text title = DesktopUi.Label(bar, font, "Untitled - Notepad", Mathf.RoundToInt(14 * scale), Color.black, TextAnchor.MiddleLeft);
            DesktopUi.TopLeft((RectTransform)title.transform, 8f, 0f, Mathf.Max(0f, width - 150f), 30f, scale);
            RectTransform close = DesktopUi.Panel(bar, "Close", new Color32(199, 55, 47, 255));
            DesktopUi.TopLeft(close, Mathf.Max(0f, width - 64f), 5f, 46f, 20f, scale);
            Text closeText = DesktopUi.Label(close, font, "×", Mathf.RoundToInt(17 * scale), Color.white, TextAnchor.MiddleCenter);
            DesktopUi.Stretch((RectTransform)closeText.transform);
            UnityEngine.UI.Button closeButton = close.gameObject.AddComponent<UnityEngine.UI.Button>();
            closeButton.targetGraphic = close.GetComponent<Image>();
            closeButton.onClick.AddListener(delegate { frame.gameObject.SetActive(false); });
            RectTransform content = DesktopUi.Panel(frame, "Content", Color.white);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(6f * scale, 6f * scale);
            content.offsetMax = new Vector2(-6f * scale, -36f * scale);
            Text menu = DesktopUi.Label(content, font, "File    Edit", Mathf.RoundToInt(14 * scale), Color.black, TextAnchor.MiddleLeft);
            DesktopUi.TopLeft((RectTransform)menu.transform, 8f, 0f, 300f, 28f, scale);
            RectTransform input = DesktopUi.Panel(content, "Notepad text", Color.white);
            input.anchorMin = Vector2.zero;
            input.anchorMax = Vector2.one;
            input.offsetMin = new Vector2(8f * scale, 28f * scale);
            input.offsetMax = new Vector2(-8f * scale, -30f * scale);
            GameObject valueObject = new GameObject("Input text", typeof(RectTransform), typeof(TextMeshProUGUI));
            valueObject.transform.SetParent(input, false);
            TextMeshProUGUI value = valueObject.GetComponent<TextMeshProUGUI>();
            value.font = UiTheme.Font;
            value.fontSize = 14f * scale;
            value.color = Color.black;
            value.alignment = TextAlignmentOptions.TopLeft;
            value.raycastTarget = false;
            DesktopUi.Stretch((RectTransform)value.transform);
            TMP_InputField field = input.gameObject.AddComponent<TMP_InputField>();
            field.textComponent = value;
            field.textViewport = input;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.targetGraphic = input.GetComponent<Image>();
            Text status = DesktopUi.Label(content, font, "Prototype: text is not saved", Mathf.RoundToInt(11 * scale), new Color32(100, 100, 100, 255), TextAnchor.MiddleLeft);
            RectTransform statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.offsetMin = new Vector2(8f * scale, 0f);
            statusRect.offsetMax = new Vector2(-8f * scale, 24f * scale);
            return frame.gameObject;
        }

    }
}
