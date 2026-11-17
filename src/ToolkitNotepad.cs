using System;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitNotepad
    {
        private readonly NotepadStorage storage;
        private readonly Action filesChanged;
        private readonly TextField text;
        private readonly Label title;
        private readonly Label status;
        private readonly VisualElement menuBar;
        private VisualElement popup;
        private bool popupIsMenu;
        private string path;
        private string saved = "";
        private string armed = "";
        private bool wrap = true;
        private bool dark;
        private RecoveredNote retained;
        private readonly CloseConfirmation closeConfirmation = new CloseConfirmation();

        internal readonly VisualElement Window;

        internal ToolkitNotepad(NotepadStorage storage, Action filesChanged)
        {
            this.storage = storage;
            this.filesChanged = filesChanged;
            Window = new VisualElement();
            Window.style.position = UnityEngine.UIElements.Position.Absolute;
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
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(Close);
            close.text = "×";
            close.style.width = 46;
            close.style.height = 20;
            close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255);
            close.style.color = Color.white;
            bar.Add(close);
            Window.Add(bar);
            ToolkitUi.MakeDraggable(Window, bar);
            menuBar = new VisualElement();
            menuBar.style.height = 28;
            menuBar.style.flexDirection = FlexDirection.Row;
            menuBar.Add(Button("File", FileMenu));
            menuBar.Add(Button("Edit", EditMenu));
            Window.Add(menuBar);
            text = new TextField();
            text.multiline = true;
            text.style.flexGrow = 1;
            text.style.backgroundColor = Color.white;
            text.style.color = Color.black;
            text.RegisterValueChangedCallback(delegate { armed = ""; closeConfirmation.Reset(); SetTitle(); });
            Window.Add(text);
            status = new Label(storage == null ? "Files are available when opened through the OS." : "");
            status.style.backgroundColor = Color.white;
            status.style.color = Color.gray;
            status.style.fontSize = 11;
            status.style.height = 24;
            Window.Add(status);
            if (storage is OsNotepadStorage && NotepadRecovery.List().Count > 0) status.text = "Unsaved notes are available under File > Recover.";
            Window.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode != KeyCode.Escape || popup == null) return;
                HidePopup();
                armed = "";
                text.Focus();
                e.StopPropagation();
            });
        }

        internal bool CanClose()
        {
            if (closeConfirmation.Allow(text.value != saved, text.value)) return true;
            Window.style.display = DisplayStyle.Flex; Window.BringToFront();
            status.text = storage is OsNotepadStorage ? "Unsaved changes. Choose Close again. Text may remain recoverable until the game exits." : "Unsaved changes. Choose Close again to discard them.";
            return false;
        }

        internal void RetainDraft()
        {
            NotepadRecovery.Remove(retained); retained = null;
            OsNotepadStorage session = storage as OsNotepadStorage;
            if (session != null) retained = NotepadRecovery.Retain(session.RecoveryKey, session.RecoveryOrigin, text.value, saved);
        }

        internal void ReattachDraft() { NotepadRecovery.Remove(retained); retained = null; closeConfirmation.Reset(); }

        internal void SetTheme(bool value)
        {
            dark = value;
            Color content = Content();
            menuBar.style.backgroundColor = Pane();
            foreach (VisualElement child in menuBar.Children()) StylePopup(child);
            StyleField(text);
            status.style.backgroundColor = content;
            status.style.color = dark ? (Color)new Color32(163, 163, 163, 255) : (Color)new Color32(107, 107, 107, 255);
            if (popup != null) { popup.style.backgroundColor = popupIsMenu ? Content() : Pane(); StylePopup(popup); }
        }

        private Color Ink() { return dark ? (Color)new Color32(236, 236, 236, 255) : Color.black; }
        private Color Content() { return dark ? (Color)new Color32(30, 30, 30, 255) : Color.white; }
        private Color Pane() { return dark ? (Color)new Color32(38, 38, 38, 255) : (Color)new Color32(243, 246, 250, 255); }

        private void StyleField(TextField field)
        {
            field.style.color = Ink(); field.style.backgroundColor = Content();
            VisualElement input = field.Q<VisualElement>("unity-text-input");
            if (input != null) { input.style.color = Ink(); input.style.backgroundColor = Content(); }
        }

        private void StylePopup(VisualElement element)
        {
            TextField field = element as TextField;
            if (field != null) { StyleField(field); return; }
            if (element is UnityEngine.UIElements.Button)
            {
                element.style.color = Ink(); element.style.backgroundColor = Color.clear; return;
            }
            if (element is Label) { element.style.color = Ink(); return; }
            ScrollView scroll = element as ScrollView;
            VisualElement parent = scroll == null ? element : scroll.contentContainer;
            foreach (VisualElement child in parent.Children()) StylePopup(child);
        }

        internal void OpenPath(string file)
        {
            Window.style.display = DisplayStyle.Flex;
            Window.BringToFront();
            if (Discard("Open")) Load(file);
        }

        internal OsResult OpenExternal(string file)
        {
            Window.style.display = DisplayStyle.Flex;
            Window.BringToFront();
            if (storage == null) return OsResult.Unavailable;
            ExternalOpenAction action = ToolkitNotepadOpen.Decide(path, file, text.value != saved);
            if (action == ExternalOpenAction.Focus) return OsResult.Ok;
            if (action == ExternalOpenAction.Load) return Load(file) ? OsResult.Ok : OsResult.Unavailable;
            VisualElement panel = Popup(8, 400);
            Label question = new Label("Discard unsaved changes and open " + file + "?");
            question.enableRichText = false; question.style.whiteSpace = WhiteSpace.Normal; panel.Add(question);
            panel.Add(Button("Discard and open", delegate { Load(file); }));
            panel.Add(Button("Cancel", delegate { HidePopup(); status.text = "Open cancelled."; }));
            StylePopup(panel);
            status.text = "Unsaved changes. Confirm before opening the requested file.";
            return OsResult.Queued;
        }

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

        private UnityEngine.UIElements.Button Button(string label, Action action)
        {
            UnityEngine.UIElements.Button result = new UnityEngine.UIElements.Button(action);
            result.text = label;
            result.enableRichText = false;
            result.RegisterCallback<PointerEnterEvent>(delegate { result.style.backgroundColor = dark ? (Color)new Color32(120, 170, 230, 77) : (Color)new Color32(170, 200, 240, 89); });
            result.RegisterCallback<PointerLeaveEvent>(delegate { result.style.backgroundColor = Color.clear; });
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

        private VisualElement Popup(float x, float width, bool isMenu = false)
        {
            HidePopup();
            popupIsMenu = isMenu;
            popup = new VisualElement();
            popup.style.position = UnityEngine.UIElements.Position.Absolute;
            popup.style.left = x;
            popup.style.top = 64;
            popup.style.width = width;
            popup.style.backgroundColor = isMenu ? Content() : Pane();
            Window.Add(popup);
            return popup;
        }

        private void FileMenu()
        {
            VisualElement menu = Popup(8, 150, true);
            menu.Add(Button("New", delegate { HidePopup(); New(); }));
            menu.Add(Button("Open...", delegate { HidePopup(); Open(); }));
            menu.Add(Button("Save", delegate { HidePopup(); Save(); }));
            menu.Add(Button("Save as...", delegate { HidePopup(); SaveAs(); }));
            OsNotepadStorage session = storage as OsNotepadStorage;
            if (session != null && NotepadRecovery.List(session.RecoveryKey, false).Count > 0) menu.Add(Button("Recover...", delegate { HidePopup(); Recover(false); }));
            if (session != null && NotepadRecovery.List(session.RecoveryKey, true).Count > 0) menu.Add(Button("Recover from another session...", delegate { HidePopup(); Recover(true); }));
            StylePopup(menu);
        }

        private void Recover(bool otherSessions)
        {
            if (!Discard("Recover")) return;
            string original = text.value;
            VisualElement panel = Popup(8, 400);
            panel.Add(new Label("Choose an unsaved note to recover"));
            ScrollView list = new ScrollView(); list.style.height = 160; panel.Add(list);
            foreach (RecoveredNote entry in NotepadRecovery.List(((OsNotepadStorage)storage).RecoveryKey, otherSessions))
            {
                RecoveredNote candidate = entry;
                string preview = (candidate.Text ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (preview.Length == 0) preview = "(empty text)";
                if (preview.Length > 48) preview = preview.Substring(0, 48) + "…";
                list.Add(Button(candidate.Origin + ": " + preview, delegate
                {
                    if (text.value != original && !Discard("Recover")) return;
                    if (!NotepadRecovery.Claim(candidate, ((OsNotepadStorage)storage).RecoveryKey, otherSessions)) { status.text = "That recovery is no longer available."; return; }
                    closeConfirmation.Reset(); saved = null; text.SetValueWithoutNotify(candidate.Text); path = null; armed = "";
                    HidePopup(); SetTitle(); status.text = "Recovered unsaved text. Use Save as to keep it.";
                }));
            }
            panel.Add(Button("Cancel", HidePopup)); StylePopup(panel);
        }

        private void EditMenu()
        {
            VisualElement menu = Popup(50, 150, true);
            menu.Add(Button("Select all", delegate { HidePopup(); text.Focus(); text.SelectAll(); }));
            menu.Add(Button(wrap ? "✓ Word wrap" : "Word wrap", delegate
            {
                HidePopup();
                wrap = !wrap;
                VisualElement input = text.Q<VisualElement>("unity-text-input");
                if (input != null) input.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
            }));
            StylePopup(menu);
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
                StylePopup(panel);
            }
            catch (Exception e) { status.text = "Not opened: " + e.Message; }
        }

        private bool Load(string selected)
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
                return true;
            }
            catch (Exception e) { status.text = "Not opened: " + e.Message; return false; }
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
            StylePopup(panel);
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
            catch (Exception e) { status.text = "Not saved: " + e.Message + ". Nothing was written."; return; }
            if (filesChanged != null)
            {
                try { filesChanged(); }
                catch (Exception) { }
            }
        }
    }
}
