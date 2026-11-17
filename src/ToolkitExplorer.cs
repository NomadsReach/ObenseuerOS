using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitExplorer
    {
        private readonly ExplorerStorage storage;
        private readonly Action<string> openFile;
        private readonly Action deleted;
        private readonly Action openRecycle;
        private readonly List<string> history = new List<string>();
        private readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();
        private readonly string pictures;
        private readonly Label crumb;
        private readonly Label status;
        private readonly ScrollView content;
        private readonly TextField search;
        private readonly UnityEngine.UIElements.Button back;
        private readonly UnityEngine.UIElements.Button forward;
        private readonly UnityEngine.UIElements.Button up;
        private readonly UnityEngine.UIElements.Button create;
        private readonly UnityEngine.UIElements.Button remove;
        private readonly UnityEngine.UIElements.Button view;
        private readonly VisualElement confirm;
        private readonly Label question;
        private ExplorerEntry[] files = new ExplorerEntry[0];
        private ExplorerEntry selected;
        private string here = "root";
        private string filter = "";
        private int historyIndex;
        private bool details;
        private VisualElement viewer;
        private Texture2D fullImage;
        private int pictureIndex;
        private int selectedPicture = -1;
        private readonly List<string> pictureNames = new List<string>();

        internal readonly VisualElement Window;

        internal ToolkitExplorer(ExplorerStorage storage, Action<string> openFile, Action deleted, Action openRecycle)
        {
            this.storage = storage;
            this.openFile = openFile;
            this.deleted = deleted;
            this.openRecycle = openRecycle;
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            pictures = string.IsNullOrEmpty(location) ? null : Path.Combine(Path.Combine(location, "web"), "pictures");
            for (int i = 1; i <= 23; i++) pictureNames.Add("obenseuer-" + i.ToString("00") + ".jpg");
            Window = new VisualElement();
            Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 320;
            Window.style.top = 32;
            Window.style.width = 720;
            Window.style.height = 560;
            Window.style.paddingLeft = 6;
            Window.style.paddingRight = 6;
            Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            Window.style.display = DisplayStyle.None;
            VisualElement titlebar = Row();
            titlebar.style.height = 36;
            Label title = new Label("Explorer");
            title.style.flexGrow = 1;
            title.style.color = Color.black;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            titlebar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", Close);
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
            back = Button("‹", delegate { Walk(-1); });
            forward = Button("›", delegate { Walk(1); });
            up = Button("Up", delegate { Go("root"); });
            toolbar.Add(back);
            toolbar.Add(forward);
            toolbar.Add(up);
            crumb = new Label();
            crumb.style.flexGrow = 1;
            crumb.style.color = Color.black;
            toolbar.Add(crumb);
            toolbar.Add(Button("Refresh", delegate { Show(here); }));
            body.Add(toolbar);
            VisualElement commands = Row();
            create = Button("New text file", NewFile);
            remove = Button("Delete", AskDelete);
            view = Button("Details", delegate { details = !details; Render(); });
            commands.Add(create);
            commands.Add(remove);
            commands.Add(view);
            search = new TextField();
            search.style.flexGrow = 1;
            search.RegisterValueChangedCallback(delegate(ChangeEvent<string> e)
            {
                filter = (e.newValue ?? "").Trim();
                Render();
            });
            commands.Add(search);
            body.Add(commands);
            VisualElement area = Row();
            area.style.flexGrow = 1;
            area.style.minHeight = 0;
            body.Add(area);
            VisualElement side = new VisualElement();
            side.style.width = 120;
            side.Add(Button("Documents", delegate { Go("/docs"); }));
            side.Add(Button("Pictures", delegate { Go("/pictures"); }));
            side.Add(Button("Downloads", delegate { Go("/downloads"); }));
            side.Add(Button("Recycle Bin", delegate { if (openRecycle != null) openRecycle(); }));
            area.Add(side);
            content = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            content.style.flexGrow = 1;
            area.Add(content);
            confirm = Row();
            question = new Label();
            question.style.flexGrow = 1;
            confirm.Add(question);
            confirm.Add(Button("Delete", DeleteSelected));
            confirm.Add(Button("Cancel", HideConfirm));
            confirm.style.display = DisplayStyle.None;
            body.Add(confirm);
            status = new Label();
            status.style.height = 24;
            status.style.color = Color.black;
            body.Add(status);
            Window.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Escape && viewer != null) { CloseViewer(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Escape && confirm.style.display != DisplayStyle.None) { HideConfirm(); e.StopPropagation(); }
                else if (e.keyCode == KeyCode.Delete && viewer == null && !InsideSearch(e.target as VisualElement)) { AskDelete(); e.StopPropagation(); }
                else if (viewer != null && e.keyCode == KeyCode.LeftArrow) { ShowPicture(pictureIndex - 1); e.StopPropagation(); }
                else if (viewer != null && e.keyCode == KeyCode.RightArrow) { ShowPicture(pictureIndex + 1); e.StopPropagation(); }
            });
        }

        private bool InsideSearch(VisualElement target)
        {
            while (target != null && target != Window)
            {
                if (target == search) return true;
                target = target.parent;
            }
            return false;
        }

        internal void Open()
        {
            if (Window.style.display == DisplayStyle.None)
            {
                history.Clear();
                history.Add("root");
                historyIndex = 0;
                Show("root");
                Window.style.display = DisplayStyle.Flex;
            }
            Window.BringToFront();
        }

        internal void RefreshIfOpen()
        {
            if (Window.style.display != DisplayStyle.None) Show(here);
        }

        internal void Release()
        {
            CloseViewer();
            foreach (Texture2D texture in thumbnails.Values) if (texture != null) UnityEngine.Object.Destroy(texture);
            thumbnails.Clear();
        }

        private void Close()
        {
            HideConfirm();
            CloseViewer();
            Window.style.display = DisplayStyle.None;
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static UnityEngine.UIElements.Button Button(string text, Action action)
        {
            UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action);
            button.text = text;
            return button;
        }

        private void Go(string place)
        {
            if (history.Count == 0) { history.Add("root"); historyIndex = 0; }
            if (history[historyIndex] != place)
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(place);
                historyIndex = history.Count - 1;
            }
            Show(place);
        }

        private void Walk(int direction)
        {
            int next = historyIndex + direction;
            if (next < 0 || next >= history.Count) return;
            historyIndex = next;
            Show(history[historyIndex]);
        }

        private void Show(string place)
        {
            here = place;
            selected = null;
            selectedPicture = -1;
            filter = "";
            search.SetValueWithoutNotify("");
            HideConfirm();
            status.text = "";
            crumb.text = place == "root" ? "Computer" : "Computer ▸ " + PlaceName(place);
            back.SetEnabled(historyIndex > 0);
            forward.SetEnabled(historyIndex + 1 < history.Count);
            up.style.display = place == "root" ? DisplayStyle.None : DisplayStyle.Flex;
            create.style.display = place == "/docs" ? DisplayStyle.Flex : DisplayStyle.None;
            remove.style.display = Deletable(place) ? DisplayStyle.Flex : DisplayStyle.None;
            view.style.display = place == "/pictures" ? DisplayStyle.Flex : DisplayStyle.None;
            create.SetEnabled(storage != null);
            remove.SetEnabled(false);
            files = new ExplorerEntry[0];
            if (place != "root" && place != "/pictures")
            {
                if (storage == null) status.text = "Files are available in game.";
                else
                {
                    try { files = storage.ListFiles(place); }
                    catch (Exception e) { status.text = e.Message; }
                }
            }
            Render();
        }

        private void Render()
        {
            content.Clear();
            view.text = details ? "Large icons" : "Details";
            if (here == "root")
            {
                AddFolder("Documents", "/docs");
                AddFolder("Pictures", "/pictures");
                AddFolder("Downloads", "/downloads");
                if (storage != null)
                {
                    try { foreach (string folder in storage.Folders()) AddFolder(PlaceName(folder), folder); }
                    catch (Exception e) { status.text = e.Message; }
                }
                return;
            }
            if (here == "/pictures") { RenderPictures(); return; }
            content.Add(new Label("Name                                  Type                  Size"));
            int count = 0;
            foreach (ExplorerEntry item in files)
            {
                ExplorerEntry entry = item;
                string name = Name(entry.Path);
                if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                count++;
                VisualElement row = Row();
                row.focusable = true;
                row.Add(new Label(name + "    Text file    " + Size(entry.Bytes)));
                row.RegisterCallback<ClickEvent>(delegate(ClickEvent e)
                {
                    Select(entry);
                    if (e.clickCount >= 2 && openFile != null) openFile(entry.Path);
                });
                row.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter || e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Delete)
                    {
                        Select(entry);
                        if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && openFile != null) openFile(entry.Path);
                        else if (e.keyCode == KeyCode.Delete) AskDelete();
                        e.StopPropagation();
                    }
                });
                row.userData = entry;
                content.Add(row);
            }
            if (storage != null && files.Length == 0 && status.text == "") status.text = "This folder is empty.";
            if (count > 0) Mark();
        }

        private void AddFolder(string name, string path)
        {
            if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) return;
            content.Add(Button(name, delegate { Go(path); }));
        }

        private void Select(ExplorerEntry entry)
        {
            selected = entry;
            HideConfirm();
            Mark();
        }

        private void Mark()
        {
            foreach (VisualElement child in content.contentContainer.Children())
                child.style.backgroundColor = child.userData != null && child.userData == selected ? (Color)new Color32(120, 170, 230, 77) : Color.clear;
            remove.SetEnabled(selected != null && Deletable(here));
        }

        private static bool Deletable(string folder)
        {
            return folder == "/docs" || folder.StartsWith("/mod-", StringComparison.Ordinal);
        }

        private static string PlaceName(string place)
        {
            if (place == "/docs") return "Documents";
            if (place == "/downloads") return "Downloads";
            if (place == "/pictures") return "Pictures";
            return Name(place);
        }

        private static string Name(string path)
        {
            return path.Substring(path.LastIndexOf('/') + 1);
        }

        private static string Size(long bytes)
        {
            return bytes < 1024 ? bytes + " bytes" : Math.Round(bytes / 1024.0) + " KB";
        }

        private void NewFile()
        {
            if (storage == null || here != "/docs") return;
            try
            {
                string path = storage.CreateDocument();
                Show(here);
                if (openFile != null) openFile(path);
            }
            catch (Exception e) { status.text = "New file failed: " + e.Message; }
        }

        private void AskDelete()
        {
            if (selected == null || !Deletable(here)) return;
            question.text = "Move " + Name(selected.Path) + " to the Recycle Bin?";
            confirm.style.display = DisplayStyle.Flex;
        }

        private void HideConfirm() { confirm.style.display = DisplayStyle.None; }

        private void DeleteSelected()
        {
            ExplorerEntry entry = selected;
            HideConfirm();
            if (entry == null || storage == null || !Deletable(here)) return;
            try
            {
                storage.DeleteFile(entry.Path);
                Show(here);
                if (deleted != null) deleted();
            }
            catch (Exception e) { status.text = "Delete failed: " + e.Message; }
        }

        private void RenderPictures()
        {
            if (string.IsNullOrEmpty(pictures) || !Directory.Exists(pictures))
            {
                status.text = "Pictures are not available in this installation.";
                return;
            }
            VisualElement grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            content.Add(grid);
            for (int i = 0; i < pictureNames.Count; i++)
            {
                int index = i;
                string name = pictureNames[i];
                if (name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                VisualElement tile = new VisualElement();
                tile.userData = index;
                tile.focusable = true;
                tile.style.backgroundColor = index == selectedPicture ? (Color)new Color32(120, 170, 230, 77) : Color.clear;
                tile.style.width = details ? 520 : 210;
                tile.style.height = details ? 42 : 140;
                if (!details)
                {
                    Image image = new Image();
                    image.image = Thumbnail(name);
                    image.scaleMode = ScaleMode.ScaleToFit;
                    image.style.width = 200;
                    image.style.height = 113;
                    tile.Add(image);
                }
                long bytes = File.Exists(Path.Combine(pictures, name)) ? new FileInfo(Path.Combine(pictures, name)).Length : 0;
                tile.Add(new Label(details ? name + "   JPEG image   1920 × 1080   " + Size(bytes) : name));
                tile.RegisterCallback<ClickEvent>(delegate(ClickEvent e)
                {
                    SelectPicture(grid, index);
                    if (e.clickCount >= 2) OpenPicture(index);
                });
                tile.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                    {
                        SelectPicture(grid, index);
                        OpenPicture(index);
                        e.StopPropagation();
                    }
                });
                grid.Add(tile);
            }
        }

        private void SelectPicture(VisualElement grid, int index)
        {
            selectedPicture = index;
            foreach (VisualElement child in grid.Children())
                child.style.backgroundColor = child.userData is int && (int)child.userData == selectedPicture ? (Color)new Color32(120, 170, 230, 77) : Color.clear;
            string path = Path.Combine(pictures, pictureNames[index]);
            long bytes = File.Exists(path) ? new FileInfo(path).Length : 0;
            status.text = pictureNames[index] + " · JPEG image, 1920 × 1080, " + Size(bytes);
        }

        private Texture2D Thumbnail(string name)
        {
            Texture2D texture;
            if (thumbnails.TryGetValue(name, out texture)) return texture;
            string thumb = name.Substring(0, name.Length - 4) + ".thumb.jpg";
            texture = LoadImage(Path.Combine(pictures, thumb));
            thumbnails[name] = texture;
            return texture;
        }

        private static Texture2D LoadImage(string path)
        {
            if (!File.Exists(path)) return null;
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (ImageConversion.LoadImage(texture, File.ReadAllBytes(path))) return texture;
            }
            catch (Exception) { }
            UnityEngine.Object.Destroy(texture);
            return null;
        }

        private void OpenPicture(int index)
        {
            if (viewer == null)
            {
                viewer = new VisualElement();
                viewer.focusable = true;
                viewer.style.position = UnityEngine.UIElements.Position.Absolute;
                viewer.style.left = 0;
                viewer.style.right = 0;
                viewer.style.top = 36;
                viewer.style.bottom = 0;
                viewer.style.backgroundColor = Color.black;
                Window.Add(viewer);
            }
            ShowPicture(index);
            viewer.Focus();
        }

        private void ShowPicture(int index)
        {
            if (viewer == null || index < 0 || index >= pictureNames.Count) return;
            pictureIndex = index;
            viewer.Clear();
            if (fullImage != null) UnityEngine.Object.Destroy(fullImage);
            fullImage = LoadImage(Path.Combine(pictures, pictureNames[index]));
            VisualElement bar = Row();
            Label count = new Label((index + 1) + " of " + pictureNames.Count);
            count.style.color = Color.white;
            count.style.flexGrow = 1;
            bar.Add(count);
            bar.Add(Button("Close", CloseViewer));
            viewer.Add(bar);
            VisualElement stage = Row();
            stage.style.flexGrow = 1;
            UnityEngine.UIElements.Button previous = Button("‹", delegate { ShowPicture(pictureIndex - 1); });
            previous.SetEnabled(index > 0);
            stage.Add(previous);
            Image image = new Image();
            image.image = fullImage;
            image.scaleMode = ScaleMode.ScaleToFit;
            image.style.flexGrow = 1;
            stage.Add(image);
            UnityEngine.UIElements.Button next = Button("›", delegate { ShowPicture(pictureIndex + 1); });
            next.SetEnabled(index + 1 < pictureNames.Count);
            stage.Add(next);
            viewer.Add(stage);
            Label caption = new Label(pictureNames[index]);
            caption.style.color = Color.white;
            viewer.Add(caption);
            if (fullImage == null) status.text = "Could not load picture.";
        }

        private void CloseViewer()
        {
            if (viewer != null) viewer.RemoveFromHierarchy();
            viewer = null;
            if (fullImage != null) UnityEngine.Object.Destroy(fullImage);
            fullImage = null;
        }
    }
}
