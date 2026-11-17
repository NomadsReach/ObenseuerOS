using System;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitTerminal
    {
        internal readonly VisualElement Window;
        private readonly ScrollView output;
        private readonly Label prompt;
        private readonly TextField input;
        private readonly TerminalSession session;

        internal ToolkitTerminal(NotepadStorage files, ExplorerStorage folders, Func<string> user, Func<string> date, Func<string> time, Func<string> money, Func<string, bool> openApp, Action<string> openFile)
        {
            session = new TerminalSession(files, folders, user, date, time, money, openApp, openFile, Close);
            Window = new VisualElement();
            Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 320; Window.style.top = 32; Window.style.width = 720; Window.style.height = 480;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement();
            bar.style.height = 36; bar.style.flexDirection = FlexDirection.Row;
            Label title = new Label("Command Prompt");
            title.style.flexGrow = 1; title.style.color = Color.black; title.style.unityTextAlign = TextAnchor.MiddleLeft;
            bar.Add(title);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(Close);
            close.text = "×"; close.style.width = 46; close.style.height = 20; close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            VisualElement body = new VisualElement();
            body.style.flexGrow = 1; body.style.backgroundColor = Color.black;
            output = new ScrollView(); output.style.flexGrow = 1; body.Add(output);
            VisualElement line = new VisualElement(); line.style.flexDirection = FlexDirection.Row;
            prompt = new Label(); prompt.style.color = Color.white; line.Add(prompt);
            input = new TextField(); input.style.flexGrow = 1; input.style.color = Color.white; input.style.backgroundColor = Color.black;
            input.RegisterCallback<KeyDownEvent>(KeyDown, TrickleDown.TrickleDown);
            line.Add(input); body.Add(line); Window.Add(body);
            Add(TerminalSession.Banner);
        }

        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront();
            prompt.text = session.Prompt; input.Focus();
        }

        private void Close() { Window.style.display = DisplayStyle.None; }

        private void KeyDown(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.UpArrow || e.keyCode == KeyCode.DownArrow)
            {
                input.value = session.Recall(e.keyCode == KeyCode.UpArrow ? -1 : 1);
                e.StopPropagation(); return;
            }
            if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
            string raw = input.value ?? "";
            Add(session.Prompt + raw.Trim());
            try
            {
                string result = session.Run(raw);
                if (session.ClearRequested) output.Clear();
                else if (result.Length > 0) Add(result);
                prompt.text = session.Prompt;
            }
            catch (Exception ex) { Add(ex.Message); }
            input.value = "";
            e.StopPropagation();
        }

        private void Add(string text)
        {
            Label line = new Label(text); line.style.color = Color.white; line.style.whiteSpace = WhiteSpace.Normal;
            output.Add(line); output.schedule.Execute(delegate() { output.ScrollTo(line); });
        }
    }
}
