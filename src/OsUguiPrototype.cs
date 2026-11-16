using System;
using ObenseuerCore.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Tahoma", "Arial" }, 14);
            RectTransform desktop = Box(host, "Desktop", new Color32(45, 95, 139, 255));
            Stretch(desktop);
            GameObject window = MakeWindow(desktop, font, scale);
            window.SetActive(false);
            int rows = Mathf.Max(1, Mathf.FloorToInt((host.rect.height / scale - 56f) / 104f));
            for (int i = 0; i < Names.Length; i++)
            {
                RectTransform tile = Box(desktop, Names[i], Color.clear);
                TopLeft(tile, 8f + (i / rows) * 88f, 8f + (i % rows) * 104f, 88f, 104f, scale);
                Text glyph = Label(tile, font, Glyphs[i], 33, i == 2 ? new Color32(242, 201, 76, 255) : Color.white, TextAnchor.MiddleCenter);
                TopLeft((RectTransform)glyph.transform, 20f, 4f, 48f, 48f, scale);
                Text title = Label(tile, font, Names[i], 12, Color.white, TextAnchor.UpperCenter);
                TopLeft((RectTransform)title.transform, 4f, 56f, 80f, 42f, scale);
                if (i == 7)
                {
                    Button open = tile.gameObject.AddComponent<Button>();
                    open.targetGraphic = tile.GetComponent<Image>();
                    open.onClick.AddListener(delegate { window.SetActive(true); window.transform.SetAsLastSibling(); });
                }
            }
            RectTransform taskbar = Box(desktop, "Taskbar", new Color32(205, 218, 232, 209));
            taskbar.anchorMin = new Vector2(0f, 0f);
            taskbar.anchorMax = new Vector2(1f, 0f);
            taskbar.pivot = new Vector2(0f, 0f);
            taskbar.offsetMin = Vector2.zero;
            taskbar.offsetMax = new Vector2(0f, 40f * scale);
            Text start = Label(taskbar, font, "⊞", 26, new Color32(30, 30, 30, 255), TextAnchor.MiddleCenter);
            TopLeft((RectTransform)start.transform, 0f, 0f, 40f, 40f, scale);
            Text clock = Label(taskbar, font, DateTime.Now.ToString("HH:mm\ndd/MM/yyyy"), 12, new Color32(30, 30, 30, 255), TextAnchor.MiddleCenter);
            RectTransform clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = new Vector2(1f, 0f);
            clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.offsetMin = new Vector2(-100f * scale, 0f);
            clockRect.offsetMax = Vector2.zero;
        }

        private static GameObject MakeWindow(RectTransform parent, Font font, float scale)
        {
            RectTransform frame = Box(parent, "Notepad window", new Color32(190, 205, 222, 217));
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(720f * scale, 520f * scale);
            frame.anchoredPosition = Vector2.zero;
            RectTransform bar = Box(frame, "Title bar", Color.clear);
            TopLeft(bar, 6f, 6f, 708f, 30f, scale);
            UguiPrototypeDrag drag = bar.gameObject.AddComponent<UguiPrototypeDrag>();
            drag.Window = frame;
            Text title = Label(bar, font, "Untitled - Notepad", 14, Color.black, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)title.transform, 8f, 0f, 570f, 30f, scale);
            RectTransform close = Box(bar, "Close", new Color32(199, 55, 47, 255));
            TopLeft(close, 656f, 5f, 46f, 20f, scale);
            Text closeText = Label(close, font, "×", 17, Color.white, TextAnchor.MiddleCenter);
            Stretch((RectTransform)closeText.transform);
            Button closeButton = close.gameObject.AddComponent<Button>();
            closeButton.targetGraphic = close.GetComponent<Image>();
            closeButton.onClick.AddListener(delegate { frame.gameObject.SetActive(false); });
            RectTransform content = Box(frame, "Content", Color.white);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = new Vector2(6f * scale, 6f * scale);
            content.offsetMax = new Vector2(-6f * scale, -36f * scale);
            Text menu = Label(content, font, "File    Edit", 14, Color.black, TextAnchor.MiddleLeft);
            TopLeft((RectTransform)menu.transform, 8f, 0f, 300f, 28f, scale);
            RectTransform input = Box(content, "Notepad text", Color.white);
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
            Stretch((RectTransform)value.transform);
            TMP_InputField field = input.gameObject.AddComponent<TMP_InputField>();
            field.textComponent = value;
            field.textViewport = input;
            field.lineType = TMP_InputField.LineType.MultiLineNewline;
            field.targetGraphic = input.GetComponent<Image>();
            Text status = Label(content, font, "Prototype: text is not saved", 11, new Color32(100, 100, 100, 255), TextAnchor.MiddleLeft);
            RectTransform statusRect = (RectTransform)status.transform;
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.offsetMin = new Vector2(8f * scale, 0f);
            statusRect.offsetMax = new Vector2(-8f * scale, 24f * scale);
            return frame.gameObject;
        }

        private static RectTransform Box(Transform parent, string name, Color color)
        {
            GameObject item = new GameObject(name, typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            item.GetComponent<Image>().color = color;
            return (RectTransform)item.transform;
        }

        private static Text Label(Transform parent, Font font, string text, int size, Color color, TextAnchor alignment)
        {
            GameObject item = new GameObject("Text", typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            Text label = item.GetComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.text = text;
            label.color = color;
            label.alignment = alignment;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height, float scale)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x * scale, -y * scale);
            rect.sizeDelta = new Vector2(width * scale, height * scale);
        }
    }

    internal sealed class UguiPrototypeDrag : MonoBehaviour, IDragHandler
    {
        internal RectTransform Window;

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform parent = Window.parent as RectTransform;
            Vector2 previous;
            Vector2 current;
            if (parent != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position - eventData.delta, eventData.pressEventCamera, out previous) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, eventData.position, eventData.pressEventCamera, out current))
                Window.anchoredPosition += current - previous;
        }
    }
}
