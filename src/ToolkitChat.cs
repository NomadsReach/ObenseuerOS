using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitChat
    {
        internal readonly VisualElement Window;
        private readonly bool available;
        private readonly Action checkSession;
        private readonly ScrollView rooms;
        private readonly ScrollView messages;
        private readonly Label title;
        private readonly Label status;
        private readonly TextField input;
        private readonly Dictionary<string, UnityEngine.UIElements.Button> roomButtons = new Dictionary<string, UnityEngine.UIElements.Button>();
        private string roomLayout = "";
        private string room = "internet";
        private int next;

        internal ToolkitChat(bool available, Action checkSession)
        {
            this.available = available;
            this.checkSession = checkSession;
            Window = new VisualElement();
            Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 320; Window.style.top = 32; Window.style.width = 760; Window.style.height = 520;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement(); bar.style.height = 36; bar.style.flexDirection = FlexDirection.Row;
            Label heading = new Label("TOR-Turnip Chat v0.3.9");
            heading.style.flexGrow = 1; heading.style.color = Color.black; heading.style.unityTextAlign = TextAnchor.MiddleLeft;
            bar.Add(heading);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(Close);
            close.text = "×"; close.style.width = 46; close.style.height = 20; close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            VisualElement body = new VisualElement(); body.style.flexGrow = 1; body.style.flexDirection = FlexDirection.Row;
            body.style.backgroundColor = Color.white;
            rooms = new ScrollView(); rooms.style.width = 200; rooms.style.flexShrink = 0; body.Add(rooms);
            VisualElement main = new VisualElement(); main.style.flexGrow = 1;
            title = new Label("TOR-Turnip"); title.style.color = Color.black; title.style.fontSize = 18; main.Add(title);
            status = new Label(); status.style.color = Color.red; main.Add(status);
            messages = new ScrollView(); messages.style.flexGrow = 1; main.Add(messages);
            VisualElement composer = new VisualElement(); composer.style.flexDirection = FlexDirection.Row;
            input = new TextField(); input.maxLength = ChatData.MaxText; input.style.flexGrow = 1;
            input.RegisterCallback<KeyDownEvent>(delegate(KeyDownEvent e)
            {
                if (e.keyCode == KeyCode.Escape) { e.StopPropagation(); return; }
                if (e.keyCode != KeyCode.Return && e.keyCode != KeyCode.KeypadEnter) return;
                Send(); e.StopPropagation();
            }, TrickleDown.TrickleDown);
            composer.Add(input);
            UnityEngine.UIElements.Button send = new UnityEngine.UIElements.Button(Send); send.text = "Send"; composer.Add(send);
            main.Add(composer); body.Add(main); Window.Add(body);
            Window.schedule.Execute(PollVisible).Every(2000);
        }

        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Poll();
        }

        private void Close() { Window.style.display = DisplayStyle.None; }
        private void PollVisible() { if (Window.style.display != DisplayStyle.None) Poll(); }

        private void Poll()
        {
            if (!available) { status.text = "TOR-Turnip works inside the game."; return; }
            try
            {
                if (checkSession != null) checkSession();
                if (ChatData.Find(room) == null && ChatData.Rooms.Count > 0) { room = ChatData.Rooms[0].Id; next = 0; messages.Clear(); }
                JObject result = JObject.Parse(ChatBridge.Since(new JObject(new JProperty("room", room), new JProperty("from", next)).ToString()));
                JArray list = (JArray)result["rooms"];
                string layout = "";
                foreach (JToken item in list) layout += ((string)item["id"]) + "\n" + ((string)item["name"]) + "\n" + ((bool)item["contact"] ? "1" : "0") + "\n";
                if (layout != roomLayout)
                {
                    roomLayout = layout; rooms.Clear(); roomButtons.Clear();
                    AddRooms(list, true, "Private Chats"); AddRooms(list, false, "Channels");
                }
                foreach (JToken item in list)
                {
                    string id = (string)item["id"];
                    string name = (string)item["name"];
                    int unread = (int)item["unread"];
                    UnityEngine.UIElements.Button button = roomButtons[id];
                    button.text = name + (id != room && unread > 0 ? "  " + (unread > 99 ? "99+" : unread.ToString()) : "");
                    button.style.backgroundColor = id == room ? (Color)new Color32(180, 205, 230, 255) : Color.clear;
                    if (id == room) title.text = name;
                }
                foreach (JToken line in (JArray)result["lines"])
                {
                    Label row = new Label((string)line["from"] + ": " + (string)line["text"]);
                    row.style.color = (bool)line["own"] ? (Color)new Color32(33, 84, 151, 255) : Color.black;
                    row.style.whiteSpace = WhiteSpace.Normal; messages.Add(row);
                    messages.schedule.Execute(delegate() { messages.ScrollTo(row); });
                }
                next = (int)result["next"];
                status.text = "";
            }
            catch (Exception e) { status.text = e.Message; }
        }

        private void AddRooms(JArray list, bool contact, string heading)
        {
            Label label = new Label(heading); label.style.color = Color.black; rooms.Add(label);
            foreach (JToken item in list)
            {
                if ((bool)item["contact"] != contact) continue;
                string id = (string)item["id"];
                UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(delegate { Switch(id); });
                roomButtons.Add(id, button); rooms.Add(button);
            }
        }

        private void Switch(string id)
        {
            if (id == room) return;
            room = id; next = 0; messages.Clear(); status.text = ""; Poll();
        }

        private void Send()
        {
            string text = (input.value ?? "").Trim();
            if (text.Length == 0) return;
            if (!available) { status.text = "TOR-Turnip works inside the game."; return; }
            try
            {
                if (checkSession != null) checkSession();
                ChatBridge.Send(new JObject(new JProperty("room", room), new JProperty("text", text)).ToString());
                input.value = ""; Poll();
            }
            catch (Exception e) { status.text = e.Message; }
        }
    }
}
