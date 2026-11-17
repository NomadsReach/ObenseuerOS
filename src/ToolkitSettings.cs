using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ObenseuerCore;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitSettings
    {
        private const string Preference = "obx.os.toolkit.wallpaper";
        internal readonly VisualElement Window;
        private readonly Image background;
        private readonly DropdownField choice;
        private readonly bool available;
        private readonly string webFolder;
        private readonly List<string> keys = new List<string>();
        private readonly List<string> names = new List<string>();
        private string selected;
        private string shown;
        private string catalog = "";
        private int revision = -1;
        private Texture2D texture;

        internal ToolkitSettings(Image background, bool available)
        {
            this.background = background;
            this.available = available;
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            webFolder = string.IsNullOrEmpty(location) ? null : Path.Combine(location, "web");
            selected = PlayerPrefs.GetString(Preference, "game");
            if (selected != "game" && selected != "sewer" && selected != "plain" && !selected.StartsWith("art:", StringComparison.Ordinal)) selected = "game";
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 320; Window.style.top = 32; Window.style.width = 600; Window.style.height = 400;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement(); bar.style.height = 36; bar.style.flexDirection = FlexDirection.Row;
            Label title = new Label("Control Panel"); title.style.flexGrow = 1; title.style.color = Color.black; title.style.unityTextAlign = TextAnchor.MiddleLeft;
            bar.Add(title);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(delegate { Window.style.display = DisplayStyle.None; });
            close.text = "×"; close.style.width = 46; close.style.height = 20; close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            VisualElement body = new VisualElement(); body.style.flexGrow = 1; body.style.backgroundColor = Color.white;
            Label theme = new Label("Theme     Framework Appearance is not applied in this prototype"); theme.style.color = Color.black; theme.style.marginTop = 12; theme.style.marginLeft = 12; body.Add(theme);
            choice = new DropdownField("Wallpaper"); choice.style.marginTop = 12; choice.style.marginLeft = 12; choice.style.marginRight = 12;
            choice.RegisterValueChangedCallback(delegate(ChangeEvent<string> e)
            {
                int index = names.IndexOf(e.newValue);
                if (index < 0 || index >= keys.Count) return;
                SelectWallpaper(keys[index]);
            });
            body.Add(choice); Window.Add(body);
            Refresh();
        }

        internal void Open() { Refresh(); Window.style.display = DisplayStyle.Flex; Window.BringToFront(); }

        internal List<KeyValuePair<string, string>> Wallpapers()
        {
            Refresh();
            List<KeyValuePair<string, string>> result = new List<KeyValuePair<string, string>>();
            for (int i = 0; i < keys.Count; i++)
                if (names[i] != "Saved wallpaper (unavailable)") result.Add(new KeyValuePair<string, string>(keys[i], names[i]));
            return result;
        }

        internal void SelectWallpaper(string key)
        {
            int index = keys.IndexOf(key);
            if (index < 0 || names[index] == "Saved wallpaper (unavailable)") return;
            selected = key; PlayerPrefs.SetString(Preference, selected);
            choice.SetValueWithoutNotify(names[index]);
            ClearTexture(); background.image = null; shown = null; revision = -1; Apply();
        }

        internal void Refresh()
        {
            keys.Clear(); names.Clear();
            Add("game", "Computer wallpaper");
            if (available && webFolder != null)
            {
                OsGameArt.Request(webFolder);
                OsGameArt.Step();
                foreach (OsGameArt.Entry art in OsGameArt.Ready()) Add("art:" + art.Id, art.Name + " (" + art.Id + ")");
            }
            if (selected.StartsWith("art:", StringComparison.Ordinal) && !keys.Contains(selected)) Add(selected, "Saved wallpaper (unavailable)");
            Add("sewer", "Sewer"); Add("plain", "Plain");
            string updated = string.Join("\n", keys.ToArray()) + "\n" + string.Join("\n", names.ToArray());
            if (updated != catalog)
            {
                catalog = updated; choice.choices = new List<string>(names);
                int index = keys.IndexOf(selected);
                choice.SetValueWithoutNotify(index < 0 ? names[0] : names[index]);
            }
            Apply();
        }

        private void Add(string key, string name) { keys.Add(key); names.Add(name); }

        private void Apply()
        {
            if (selected == "plain") { ClearTexture(); background.image = null; shown = selected; return; }
            if (selected == "sewer")
            {
                if (shown == selected) return;
                ClearTexture();
                texture = Sewer(); background.image = texture; shown = selected; return;
            }
            string path = null;
            int nextRevision = -1;
            if (selected == "game" && available && webFolder != null)
            {
                nextRevision = OsWallpaper.Capture(ComputerApps.ActiveScreen, webFolder);
                if (nextRevision >= 0) path = Path.Combine(Path.Combine(webFolder, "wallpapers"), "game.jpg");
            }
            else if (selected.StartsWith("art:", StringComparison.Ordinal) && available)
            {
                foreach (OsGameArt.Entry art in OsGameArt.Ready()) if ("art:" + art.Id == selected) { path = art.FilePath; nextRevision = art.Rev; break; }
            }
            if (path == null || nextRevision < 0)
            {
                ClearTexture(); background.image = null; shown = null; revision = -1; return;
            }
            if (shown == selected && revision == nextRevision) return;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(loaded, bytes)) { UnityEngine.Object.Destroy(loaded); return; }
                ClearTexture(); texture = loaded; background.image = texture; shown = selected; revision = nextRevision;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static Texture2D Sewer()
        {
            const int width = 512, height = 512;
            Color32[] pixels = new Color32[width * height];
            Color top = new Color32(43, 45, 30, 255), bottom = new Color32(12, 13, 8, 255);
            Color mortar = new Color32(200, 210, 150, 255);
            for (int y = 0; y < height; y++)
            {
                Color baseColor = Color.Lerp(bottom, top, (float)y / (height - 1));
                for (int x = 0; x < width; x++) pixels[y * width + x] = (y % 16 == 0 || x % 32 == 0) ? (Color32)Color.Lerp(baseColor, mortar, 0.06f) : (Color32)baseColor;
            }
            Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
            result.SetPixels32(pixels); result.Apply(); return result;
        }

        private void ClearTexture()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        }

        internal void Release() { background.image = null; ClearTexture(); }
    }
}
