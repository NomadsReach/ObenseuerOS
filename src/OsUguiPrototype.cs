using System;
using System.Collections.Generic;
using System.IO;
using ObenseuerCore;
using ObenseuerCore.Ui;
using TMPro;
using UnityEngine;

using UnityEngine.UI;

namespace ObenseuerOS
{
    internal static class OsUguiPrototype
    {
        private static readonly string[] Names = { "Stalburg Mail", "Stalburg Web", "Explorer", "Recycle Bin", "Control Panel", "Stal-Ex", "Command Prompt", "Notepad", "Stalburg Bank", "Stalburg Times", "TOR-Turnip Chat v0.3.9", "Shop", "Minesweeper", "Solitaire" };
        private static readonly string[] Icons = { "mail", "world", "folder-filled", "trash", "settings", "chart-line", "terminal-2", "notes", "building-bank", "news", "message-circle", "shopping-cart", "device-gamepad-2", "device-gamepad-2" };
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
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
                Image icon = Icon(tile, Icons[i], i == 2 ? (Color)new Color32(242, 201, 76, 255) : Color.white);
                DesktopUi.TopLeft((RectTransform)icon.transform, 20f, 4f, 48f, 48f, scale);
                Text title = DesktopUi.Label(tile, font, Names[i], Mathf.RoundToInt(12 * scale), Color.white, TextAnchor.UpperCenter);
                DesktopUi.TopLeft((RectTransform)title.transform, 4f, 56f, 80f, 42f, scale);
                if (i == 7)
                {
                    UnityEngine.UI.Button open = tile.gameObject.AddComponent<UnityEngine.UI.Button>();
                    open.targetGraphic = tile.GetComponent<Image>();
                    open.onClick.AddListener(delegate { window.SetActive(true); window.transform.SetSiblingIndex(desktop.childCount - 2); });
                }
            }
            RectTransform taskbar = DesktopUi.Panel(desktop, "Taskbar", new Color32(38, 50, 64, 209));
            taskbar.anchorMin = new Vector2(0f, 0f);
            taskbar.anchorMax = new Vector2(1f, 0f);
            taskbar.pivot = new Vector2(0f, 0f);
            taskbar.offsetMin = Vector2.zero;
            taskbar.offsetMax = new Vector2(0f, 40f * scale);
            Image start = Icon(taskbar, "viewports", Color.white);
            DesktopUi.TopLeft((RectTransform)start.transform, 7f, 7f, 26f, 26f, scale);
            Image folder = Icon(taskbar, "folder-filled", Color.white);
            DesktopUi.TopLeft((RectTransform)folder.transform, 52f, 8f, 24f, 24f, scale);
            Image browser = Icon(taskbar, "world", Color.white);
            DesktopUi.TopLeft((RectTransform)browser.transform, 104f, 8f, 24f, 24f, scale);
            Text clock = DesktopUi.Label(taskbar, font, "", Mathf.RoundToInt(12 * scale), new Color32(236, 236, 236, 255), TextAnchor.MiddleCenter);
            RectTransform clockRect = (RectTransform)clock.transform;
            clockRect.anchorMin = new Vector2(1f, 0f);
            clockRect.anchorMax = new Vector2(1f, 1f);
            clockRect.offsetMin = new Vector2(-100f * scale, 0f);
            clockRect.offsetMax = Vector2.zero;
            Text money = DesktopUi.Label(taskbar, font, "", Mathf.RoundToInt(11 * scale), new Color32(236, 236, 236, 255), TextAnchor.MiddleRight);
            RectTransform moneyRect = (RectTransform)money.transform;
            moneyRect.anchorMin = new Vector2(1f, 0f);
            moneyRect.anchorMax = new Vector2(1f, 1f);
            moneyRect.offsetMin = new Vector2(-220f * scale, 0f);
            moneyRect.offsetMax = new Vector2(-110f * scale, 0f);
            PrototypeStatus status = taskbar.gameObject.AddComponent<PrototypeStatus>();
            status.Clock = clock;
            status.Money = money;
        }

        private static Image Icon(Transform parent, string name, Color color)
        {
            GameObject item = new GameObject(name + " icon", typeof(RectTransform), typeof(Image));
            item.transform.SetParent(parent, false);
            Image image = item.GetComponent<Image>();
            image.raycastTarget = false;
            image.color = color;
            Sprite sprite;
            if (!sprites.TryGetValue(name, out sprite))
            {
                using (Stream stream = typeof(OsUguiPrototype).Assembly.GetManifestResourceStream("desktop-icons/" + name + ".png"))
                {
                    if (stream != null)
                    {
                        using (MemoryStream buffer = new MemoryStream())
                        {
                            stream.CopyTo(buffer);
                            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                            if (ImageConversion.LoadImage(texture, buffer.ToArray()))
                                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                        }
                    }
                }
                sprites[name] = sprite;
            }
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            return image;
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

    internal sealed class PrototypeStatus : MonoBehaviour
    {
        internal Text Clock;
        internal Text Money;
        private float nextUpdate;

        private void Update()
        {
            if (Time.unscaledTime < nextUpdate) return;
            nextUpdate = Time.unscaledTime + 1f;
            if (WorldClock.IsAvailable)
            {
                WorldTime now = WorldClock.Now;
                Clock.text = string.Format("{0:00}:{1:00}\n{2}.1.1843", now.Hour, now.Minute, now.CalendarDay);
            }
            if (Cash.IsAvailable)
                Money.text = string.Format("{0:N0} OC\n{1:N2} RM", (int)Cash.Balance(MoneyCurrency.OC), Cash.Balance(MoneyCurrency.RM));
        }
    }
}
