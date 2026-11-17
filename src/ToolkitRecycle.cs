using System;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitRecycle
    {
        private readonly RecycleStorage storage;
        private readonly Action filesChanged;
        private readonly ScrollView list;
        private readonly Label status;
        private readonly VisualElement confirm;
        private readonly Label question;
        private readonly UnityEngine.UIElements.Button restore;
        private readonly UnityEngine.UIElements.Button purge;
        private readonly UnityEngine.UIElements.Button empty;
        private readonly UnityEngine.UIElements.Button confirmAction;
        private RecycleEntry[] items = new RecycleEntry[0];
        private RecycleEntry selected;
        private Action pending;

        internal readonly VisualElement Window;

        internal ToolkitRecycle(RecycleStorage storage, Action filesChanged)
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
            VisualElement titlebar = Row();
            titlebar.style.height = 36;
            Label title = new Label("Recycle Bin");
            title.style.flexGrow = 1;
            title.style.color = Color.black;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            titlebar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { HideConfirm(); Window.style.display = DisplayStyle.None; });
            close.style.width = 46;
            close.style.height = 20;
            close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255);
            close.style.color = Color.white;
            titlebar.Add(close);
            Window.Add(titlebar);
            ToolkitUi.MakeDraggable(Window, titlebar);
            VisualElement body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.backgroundColor = Color.white;
            Window.Add(body);
            VisualElement toolbar = Row();
            toolbar.Add(new Label("Recycle Bin"));
            restore = Button("Restore", Restore);
            purge = Button("Delete", AskPurge);
            empty = Button("Empty Recycle Bin", AskEmpty);
            toolbar.Add(restore);
            toolbar.Add(purge);
            toolbar.Add(empty);
            body.Add(toolbar);
            VisualElement headers = Row();
            headers.Add(Column("Name", 180));
            headers.Add(Column("Original location", 220));
            headers.Add(Column("Date deleted", 140));
            body.Add(headers);
            list = new ScrollView();
            list.style.flexGrow = 1;
            list.style.minHeight = 0;
            body.Add(list);
            confirm = Row();
            question = new Label();
            question.style.flexGrow = 1;
            confirm.Add(question);
            confirmAction = Button("Delete", Confirm);
            confirm.Add(confirmAction);
            confirm.Add(Button("Cancel", HideConfirm));
            confirm.style.display = DisplayStyle.None;
            body.Add(confirm);
            status = new Label();
            status.style.height = 24;
            status.style.color = Color.black;
            body.Add(status);
            Window.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Escape && pending != null)
                {
                    HideConfirm();
                    e.StopPropagation();
                }
                else if (e.keyCode == KeyCode.Delete)
                {
                    AskPurge();
                    e.StopPropagation();
                }
            });
            SetButtons();
        }

        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex;
            Window.BringToFront();
            Load("");
        }

        internal void RefreshIfOpen()
        {
            if (Window.style.display != DisplayStyle.None) Load("");
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static Label Column(string value, int width)
        {
            Label label = new Label(value);
            label.style.width = width;
            label.style.color = Color.black;
            return label;
        }

        private static UnityEngine.UIElements.Button Button(string label, Action action)
        {
            UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action);
            button.text = label;
            return button;
        }

        private void Load(string note)
        {
            selected = null;
            HideConfirm();
            list.Clear();
            if (storage == null)
            {
                items = new RecycleEntry[0];
                status.text = "The Recycle Bin is available in game.";
                SetButtons();
                return;
            }
            try
            {
                items = storage.ListBin();
                foreach (RecycleEntry item in items)
                {
                    RecycleEntry entry = item;
                    UnityEngine.UIElements.Button row = Button("", delegate { Select(entry); });
                    row.style.flexDirection = FlexDirection.Row;
                    row.Add(Column(Name(entry.Origin), 180));
                    row.Add(Column(Place(entry.Origin), 220));
                    row.Add(Column(string.IsNullOrEmpty(entry.Deleted) ? "Unknown" : entry.Deleted, 140));
                    row.userData = entry;
                    list.Add(row);
                }
                status.text = string.IsNullOrEmpty(note) && items.Length == 0 ? "The Recycle Bin is empty." : note;
            }
            catch (Exception e)
            {
                items = new RecycleEntry[0];
                status.text = e.Message;
            }
            SetButtons();
        }

        private static string Name(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            return path.Substring(path.LastIndexOf('/') + 1);
        }

        private static string Place(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            int slash = path.LastIndexOf('/');
            string folder = slash < 0 ? "" : path.Substring(0, slash);
            if (folder == "/docs") return "Computer ▸ Documents";
            if (folder == "/downloads") return "Computer ▸ Downloads";
            return folder;
        }

        private void Select(RecycleEntry entry)
        {
            HideConfirm();
            selected = entry;
            foreach (VisualElement child in list.contentContainer.Children())
                child.style.backgroundColor = child.userData == entry ? (Color)new Color32(120, 170, 230, 77) : Color.clear;
            SetButtons();
        }

        private void SetButtons()
        {
            restore.SetEnabled(selected != null);
            purge.SetEnabled(selected != null);
            empty.SetEnabled(items.Length > 0);
        }

        private void HideConfirm()
        {
            pending = null;
            confirm.style.display = DisplayStyle.None;
        }

        private void Ask(string text, string label, Action action)
        {
            pending = action;
            question.text = text;
            confirmAction.text = label;
            confirm.style.display = DisplayStyle.Flex;
            confirmAction.Focus();
        }

        private void Confirm()
        {
            Action action = pending;
            HideConfirm();
            if (action != null) action();
        }

        private void Restore()
        {
            RecycleEntry entry = selected;
            if (entry == null) return;
            try
            {
                string path = storage.Restore(entry.Path);
                Load("Restored to " + path);
                if (filesChanged != null) filesChanged();
            }
            catch (Exception e) { status.text = "Restore failed: " + e.Message; }
        }

        private void AskPurge()
        {
            RecycleEntry entry = selected;
            if (entry == null) return;
            Ask("Permanently delete " + Name(entry.Origin) + "?", "Delete", delegate
            {
                try { storage.Purge(entry.Path); Load(""); }
                catch (Exception e) { status.text = "Delete failed: " + e.Message; }
            });
        }

        private void AskEmpty()
        {
            int count = items.Length;
            if (count == 0) return;
            Ask("Permanently delete all " + count + (count == 1 ? " item" : " items") + " in the Recycle Bin?", "Empty Recycle Bin", delegate
            {
                try { storage.EmptyBin(); Load(""); }
                catch (Exception e) { status.text = "Empty failed: " + e.Message; }
            });
        }
    }
}
