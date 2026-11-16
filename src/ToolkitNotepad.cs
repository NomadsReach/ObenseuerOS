using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitNotepad
    {
        private readonly NotepadStorage storage;
        private readonly TextField text;
        private readonly Label title;
        private readonly Label status;
        private VisualElement popup;
        private string path;
        private string saved = "";
        private string armed = "";
        private bool wrap = true;

        internal readonly VisualElement Window;

        internal ToolkitNotepad(NotepadStorage storage)
        {
            this.storage = storage;
            Window = new VisualElement();
            Window.style.position = Position.Absolute;
            Window.style.left = 320;
            Window.style.top = 32;
            Window.style.width = 600;
            Window.style.height = 400;
            Window.style.paddingLeft = 6;
            Window.style.paddingRight = 6;
            Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement();
            bar.style.height = 36;
            bar.style.flexDirection = FlexDirection.Row;
            title = new Label("Untitled - Notepad");
            title.style.flexGrow = 1;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            title.style.color = Color.black;
            bar.Add(title);
            Button close = new Button(Close);
            close.text = "×";
            close.style.width = 46;
            close.style.height = 20;
            close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255);
            close.style.color = Color.white;
            bar.Add(close);
            Window.Add(bar);
            VisualElement menu = new VisualElement();
            menu.style.height = 28;
            menu.style.flexDirection = FlexDirection.Row;
            menu.style.backgroundColor = Color.white;
            menu.Add(Button("File", FileMenu));
            menu.Add(Button("Edit", EditMenu));
            Window.Add(menu);
            text = new TextField();
            text.multiline = true;
            text.style.flexGrow = 1;
            text.style.backgroundColor = Color.white;
            text.style.color = Color.black;
            text.RegisterValueChangedCallback(delegate { armed = ""; SetTitle(); });
            Window.Add(text);
            status = new Label(storage == null ? "Files are available when opened through the OS." : "");
            status.style.backgroundColor = Color.white;
            status.style.color = Color.gray;
            status.style.fontSize = 11;
            status.style.height = 24;
            Window.Add(status);
            Window.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode != KeyCode.Escape || popup == null) return;
                HidePopup();
                armed = "";
                text.Focus();
                e.StopPropagation();
            });
        }

        internal bool CanClose() { return Discard("Close"); }

        private void Close()
        {
            if (!CanClose()) return;
            HidePopup();
            text.value = "";
            saved = "";
            path = null;
            armed = "";
            SetTitle();
            status.text = storage == null ? "Files are available when opened through the OS." : "";
            Window.style.display = DisplayStyle.None;
        }

        private static Button Button(string label, Action action)
        {
            Button result = new Button(action);
            result.text = label;
            return result;
        }

        private void SetTitle()
        {
            string name = path == null ? "Untitled" : path.Substring(path.LastIndexOf('/') + 1);
            title.text = (text.value == saved ? "" : "● ") + name + " - Notepad";
        }

        private bool Discard(string action)
        {
            if (text.value == saved || armed == action) { armed = ""; return true; }
            armed = action;
            status.text = "Unsaved changes. Choose " + action + " again to discard them.";
            return false;
        }

        private void HidePopup()
        {
            if (popup == null) return;
            popup.RemoveFromHierarchy();
            popup = null;
        }

        private VisualElement Popup(float x, float width)
        {
            HidePopup();
            popup = new VisualElement();
            popup.style.position = Position.Absolute;
            popup.style.left = x;
            popup.style.top = 64;
            popup.style.width = width;
            popup.style.backgroundColor = (Color)new Color32(245, 245, 245, 255);
            Window.Add(popup);
            return popup;
        }

        private void FileMenu()
        {
            VisualElement menu = Popup(8, 150);
            menu.Add(Button("New", delegate { HidePopup(); New(); }));
            menu.Add(Button("Open...", delegate { HidePopup(); Open(); }));
            menu.Add(Button("Save", delegate { HidePopup(); Save(); }));
            menu.Add(Button("Save as...", delegate { HidePopup(); SaveAs(); }));
        }

        private void EditMenu()
        {
            VisualElement menu = Popup(50, 150);
            menu.Add(Button("Select all", delegate { HidePopup(); text.Focus(); text.SelectAll(); }));
            menu.Add(Button(wrap ? "✓ Word wrap" : "Word wrap", delegate
            {
                HidePopup();
                wrap = !wrap;
                VisualElement input = text.Q<VisualElement>("unity-text-input");
                if (input != null) input.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            }));
        }

        private void New()
        {
            if (!Discard("New")) return;
            text.value = "";
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
                string original = text.value;
                VisualElement panel = Popup(8, 300);
                Label label = new Label("Open from Documents");
                panel.Add(label);
                ScrollView list = new ScrollView();
                list.style.height = 160;
                panel.Add(list);
                if (paths.Length == 0) list.Add(new Label("No files in Documents."));
                foreach (string entry in paths)
                {
                    string selected = entry;
                    string name = selected.Substring(selected.LastIndexOf('/') + 1);
                    list.Add(Button(name, delegate
                    {
                        if (text.value != original && !Discard("Open")) return;
                        Load(selected);
                    }));
                }
                panel.Add(Button("Cancel", HidePopup));
            }
            catch (Exception e) { status.text = "Not opened: " + e.Message; }
        }

        private void Load(string selected)
        {
            try
            {
                string value = storage.Read(selected);
                text.value = value;
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
            VisualElement panel = Popup(8, 400);
            panel.Add(new Label("Documents /docs/"));
            TextField name = new TextField();
            name.value = path == null ? "" : path.Substring(path.LastIndexOf('/') + 1);
            panel.Add(name);
            Action commit = delegate { CommitAs(name.value.Trim()); };
            panel.Add(Button("Save", commit));
            panel.Add(Button("Cancel", delegate { armed = ""; HidePopup(); }));
            name.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                commit();
                e.StopPropagation();
            });
            name.Focus();
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
                string value = text.value;
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
    }
}
