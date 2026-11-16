using System;
using ObenseuerCore.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenseuerOS
{
    internal sealed class NotepadWindow
    {
        private readonly GameObject window;
        private readonly RectTransform content;
        private readonly TMP_InputField text;
        private readonly Text title;
        private readonly Text status;
        private readonly Font font;
        private readonly float scale;
        private readonly NotepadStorage storage;
        private RectTransform popup;
        private string path;
        private string saved = "";
        private string armed = "";
        private bool wrap = true;

        internal NotepadWindow(GameObject window, RectTransform content, TMP_InputField text, Text title, Text status, Font font, float scale, NotepadStorage storage)
        {
            this.window = window;
            this.content = content;
            this.text = text;
            this.title = title;
            this.status = status;
            this.font = font;
            this.scale = scale;
            this.storage = storage;
            text.onValueChanged.AddListener(delegate { armed = ""; SetTitle(); });
            SetTitle();
            if (storage == null) status.text = "Files are available when opened through the OS.";
        }

        internal void Close()
        {
            if (!Discard("Close")) return;
            HidePopup();
            text.text = "";
            saved = "";
            path = null;
            armed = "";
            SetTitle();
            status.text = storage == null ? "Files are available when opened through the OS." : "";
            window.SetActive(false);
        }

        internal void FileMenu()
        {
            Menu(new[] { "New", "Open...", "Save", "Save as..." }, new Action[] { New, Open, Save, SaveAs }, 8f);
        }

        internal void EditMenu()
        {
            Menu(new[] { "Select all", wrap ? "✓ Word wrap" : "Word wrap" }, new Action[] { SelectAll, ToggleWrap }, 45f);
        }

        private void Menu(string[] names, Action[] actions, float x)
        {
            HidePopup();
            popup = DesktopUi.Panel(content, "Notepad menu", new Color32(245, 245, 245, 255));
            DesktopUi.TopLeft(popup, x, 27f, 150f, names.Length * 28f + 4f, scale);
            for (int i = 0; i < names.Length; i++)
            {
                Action action = actions[i];
                MakeButton(popup, names[i], 2f, 2f + i * 28f, 146f, 26f, delegate { HidePopup(); action(); });
            }
        }

        private void MakeButton(RectTransform parent, string name, float x, float y, float width, float height, Action action)
        {
            RectTransform item = DesktopUi.Panel(parent, name, new Color32(240, 240, 240, 255));
            DesktopUi.TopLeft(item, x, y, width, height, scale);
            Text label = DesktopUi.Label(item, font, name, Mathf.RoundToInt(13f * scale), Color.black, TextAnchor.MiddleLeft);
            DesktopUi.Stretch((RectTransform)label.transform);
            UnityEngine.UI.Button button = item.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = item.GetComponent<Image>();
            button.onClick.AddListener(delegate { action(); });
        }

        private void HidePopup()
        {
            if (popup == null) return;
            popup.gameObject.SetActive(false);
            UnityEngine.Object.Destroy(popup.gameObject);
            popup = null;
        }

        private bool Discard(string action)
        {
            if (text.text == saved || armed == action) { armed = ""; return true; }
            armed = action;
            status.text = "Unsaved changes. Choose " + action + " again to discard them.";
            return false;
        }

        private void SetTitle()
        {
            string name = path == null ? "Untitled" : path.Substring(path.LastIndexOf('/') + 1);
            title.text = (text.text == saved ? "" : "● ") + name + " - Notepad";
        }

        private void New()
        {
            if (!Discard("New")) return;
            text.text = "";
            saved = "";
            path = null;
            status.text = "";
            SetTitle();
        }

        private void Open()
        {
            if (!Discard("Open")) return;
            if (storage == null) { status.text = "Files are available when opened through the OS."; return; }
            try
            {
                string[] paths = storage.List();
                string original = text.text;
                HidePopup();
                popup = DesktopUi.Panel(content, "Open document", new Color32(245, 245, 245, 255));
                DesktopUi.TopLeft(popup, 8f, 30f, 300f, 220f, scale);
                MakeButton(popup, "Cancel", 220f, 186f, 70f, 26f, HidePopup);
                RectTransform viewport = DesktopUi.Panel(popup, "Documents", Color.white);
                DesktopUi.TopLeft(viewport, 8f, 8f, 284f, 170f, scale);
                viewport.gameObject.AddComponent<RectMask2D>();
                ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
                scroll.viewport = viewport;
                scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 30f * scale;
                RectTransform list = DesktopUi.Panel(viewport, "Document list", Color.clear);
                list.GetComponent<Image>().raycastTarget = false;
                DesktopUi.TopLeft(list, 0f, 0f, 284f, Mathf.Max(170f, paths.Length * 27f), scale);
                scroll.content = list;
                for (int i = 0; i < paths.Length; i++)
                {
                    string selected = paths[i];
                    string name = selected.Substring(selected.LastIndexOf('/') + 1);
                    MakeButton(list, name, 0f, i * 27f, 284f, 26f, delegate
                    {
                        if (text.text != original && !Discard("Open")) return;
                        Load(selected);
                    });
                }
            }
            catch (Exception e) { status.text = "Not opened: " + e.Message; }
        }

        private void Load(string selected)
        {
            try
            {
                string value = storage.Read(selected);
                text.text = value;
                saved = value;
                path = selected;
                armed = "";
                HidePopup();
                status.text = "";
                SetTitle();
                try { storage.Opened(selected); } catch (Exception) { }
            }
            catch (Exception e) { status.text = "Not opened: " + e.Message; }
        }

        private void Save()
        {
            if (path == null) { SaveAs(); return; }
            Write(path);
        }

        private void SaveAs()
        {
            if (storage == null) { status.text = "Files are available when opened through the OS."; return; }
            armed = "";
            HidePopup();
            popup = DesktopUi.Panel(content, "Save document", new Color32(245, 245, 245, 255));
            DesktopUi.TopLeft(popup, 8f, 30f, 400f, 100f, scale);
            Text label = DesktopUi.Label(popup, font, "Documents /docs/", Mathf.RoundToInt(13f * scale), Color.black, TextAnchor.MiddleLeft);
            DesktopUi.TopLeft((RectTransform)label.transform, 8f, 5f, 180f, 24f, scale);
            RectTransform box = DesktopUi.Panel(popup, "File name", Color.white);
            DesktopUi.TopLeft(box, 8f, 30f, 250f, 26f, scale);
            TextMeshProUGUI value = new GameObject("Input text", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            value.transform.SetParent(box, false);
            value.font = UiTheme.Font;
            value.fontSize = 14f * scale;
            value.color = Color.black;
            value.raycastTarget = false;
            DesktopUi.Stretch((RectTransform)value.transform);
            TMP_InputField name = box.gameObject.AddComponent<TMP_InputField>();
            name.textComponent = value;
            name.textViewport = box;
            name.targetGraphic = box.GetComponent<Image>();
            name.text = path == null ? "" : path.Substring(path.LastIndexOf('/') + 1);
            MakeButton(popup, "Save", 265f, 30f, 60f, 26f, delegate { CommitAs(name.text.Trim()); });
            MakeButton(popup, "Cancel", 330f, 30f, 60f, 26f, delegate { armed = ""; HidePopup(); });
            name.ActivateInputField();
        }

        private void CommitAs(string name)
        {
            string candidate = "/docs/" + name;
            if (!storage.Valid(candidate)) { status.text = "Name not allowed. Use a valid file name with an extension."; return; }
            try
            {
                bool exists = Array.IndexOf(storage.List(), candidate) >= 0;
                if (exists && armed != "overwrite " + candidate)
                {
                    armed = "overwrite " + candidate;
                    status.text = candidate + " already exists. Choose Save again to overwrite it.";
                    return;
                }
                Write(candidate);
            }
            catch (Exception e) { status.text = "Not saved: " + e.Message; }
        }

        private void Write(string target)
        {
            if (storage == null) { status.text = "Files are available when opened through the OS."; return; }
            try
            {
                string value = text.text;
                storage.Write(target, value);
                path = target;
                saved = value;
                armed = "";
                HidePopup();
                status.text = "Saved to this computer";
                SetTitle();
            }
            catch (Exception e) { status.text = "Not saved: " + e.Message + ". Nothing was written."; }
        }

        private void SelectAll()
        {
            text.ActivateInputField();
            text.SelectAll();
        }

        private void ToggleWrap()
        {
            wrap = !wrap;
            text.textComponent.enableWordWrapping = wrap;
        }
    }
}
