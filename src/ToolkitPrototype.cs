using System;
using System.Collections.Generic;
using System.IO;
using ObenseuerCore;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
internal sealed class ToolkitPrototype : MonoBehaviour
{
    private static readonly string[] Names = { "Stalburg Mail", "Stalburg Web", "Explorer", "Recycle Bin", "Control Panel", "Stal-Ex", "Command Prompt", "Notepad", "Stalburg Bank", "Stalburg Times", "TOR-Turnip Chat v0.3.9", "Shop", "Minesweeper", "Solitaire" };
    private static readonly string[] Icons = { "mail", "world", "folder-filled", "trash", "settings", "chart-line", "terminal-2", "notes", "building-bank", "news", "message-circle", "shopping-cart", "device-gamepad-2", "device-gamepad-2" };
    private readonly List<Texture2D> textures = new List<Texture2D>();
    private VisualElement selected;
    private NotepadStorage storage;

    internal static void Open(RectTransform host)
    {
        Open(host, null);
    }

    internal static void Open(RectTransform host, NotepadStorage storage)
    {
        GameObject owner = new GameObject("UI Toolkit comparison");
        owner.transform.SetParent(host, false);
        ToolkitPrototype prototype = owner.AddComponent<ToolkitPrototype>();
        prototype.storage = storage;
        prototype.Build();
    }

    private void Build()
    {
        VisualElement root = ToolkitUi.CreateOverlay(transform);
        root.style.flexGrow = 1;
        root.style.backgroundColor = (Color)new Color32(45, 95, 139, 255);
        VisualElement desktop = new VisualElement();
        desktop.style.flexGrow = 1;
        desktop.style.flexDirection = FlexDirection.Column;
        desktop.style.flexWrap = Wrap.Wrap;
        desktop.style.alignContent = Align.FlexStart;
        desktop.style.paddingLeft = 8;
        desktop.style.paddingTop = 8;
        root.Add(desktop);
        ToolkitNotepad notepad = new ToolkitNotepad(storage);
        ToolkitMinesweeper minesweeper = new ToolkitMinesweeper();
        ToolkitSolitaire solitaire = new ToolkitSolitaire();
        ComputerApps.SetCloseGuard(transform.parent, notepad.CanClose);
        VisualElement window = notepad.Window;
        for (int i = 0; i < Names.Length; i++)
        {
            int index = i;
            VisualElement tile = new VisualElement();
            tile.style.width = 88;
            tile.style.height = 104;
            tile.style.alignItems = Align.Center;
            Image icon = new Image();
            icon.image = Texture(Icons[i]);
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.tintColor = i == 2 ? (Color)new Color32(242, 201, 76, 255) : Color.white;
            icon.style.width = 48;
            icon.style.height = 48;
            icon.style.marginTop = 4;
            tile.Add(icon);
            Label label = new Label(Names[i]);
            label.style.color = Color.white;
            label.style.fontSize = 12;
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.width = 80;
            label.style.marginTop = 4;
            tile.Add(label);
            tile.RegisterCallback<ClickEvent>(delegate(ClickEvent e)
            {
                if (e.button != 0) return;
                if (selected != null) selected.style.backgroundColor = Color.clear;
                selected = tile;
                selected.style.backgroundColor = (Color)new Color32(120, 170, 230, 77);
                if (index == 7 && e.clickCount >= 2) { window.style.display = DisplayStyle.Flex; window.BringToFront(); }
                else if (index == 12 && e.clickCount >= 2) minesweeper.Open();
                else if (index == 13 && e.clickCount >= 2) solitaire.Open();
            });
            desktop.Add(tile);
        }
        VisualElement windows = new VisualElement();
        windows.pickingMode = PickingMode.Ignore;
        windows.style.position = UnityEngine.UIElements.Position.Absolute;
        windows.style.left = 0;
        windows.style.right = 0;
        windows.style.top = 0;
        windows.style.bottom = 40;
        root.Add(windows);
        windows.Add(window);
        windows.Add(minesweeper.Window);
        windows.Add(solitaire.Window);
        VisualElement taskbar = new VisualElement();
        taskbar.style.height = 40;
        taskbar.style.flexShrink = 0;
        taskbar.style.backgroundColor = (Color)new Color32(38, 50, 64, 255);
        taskbar.style.flexDirection = FlexDirection.Row;
        foreach (string name in new[] { "viewports", "folder-filled", "world" })
        {
            Image icon = new Image();
            icon.image = Texture(name);
            icon.scaleMode = ScaleMode.ScaleToFit;
            icon.style.width = 26;
            icon.style.height = 26;
            icon.style.marginLeft = 14;
            icon.style.marginTop = 7;
            taskbar.Add(icon);
        }
        root.Add(taskbar);
        UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(delegate { if (notepad.CanClose()) ComputerApps.Close(storage == null ? "os.toolkit-prototype" : "os.toolkit-os"); });
        close.text = "Close";
        close.style.position = UnityEngine.UIElements.Position.Absolute;
        close.style.right = 16;
        close.style.top = 16;
        close.style.width = 120;
        close.style.height = 40;
        root.Add(close);
    }

    private Texture2D Texture(string name)
    {
        using (Stream stream = typeof(ToolkitPrototype).Assembly.GetManifestResourceStream("desktop-icons/" + name + ".png"))
        {
            if (stream == null) return null;
            using (MemoryStream buffer = new MemoryStream())
            {
                stream.CopyTo(buffer);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, buffer.ToArray())) { Destroy(texture); return null; }
                textures.Add(texture);
                return texture;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
    }
}

}
