using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitMail
    {
        internal readonly VisualElement Window;
        private readonly MailClient client;
        private readonly MailDraft draft;
        private readonly VisualElement body;
        private readonly Label status;
        private readonly List<JObject> rows = new List<JObject>();
        private readonly HashSet<int> picked = new HashSet<int>();
        private readonly List<string> recipients = new List<string>();
        private ScrollView list;
        private VisualElement view;
        private TextField search;
        private JObject current;
        private string folder = "inbox";
        private string armedReplace = "";
        private bool composing, armedDelete, pageFailed;
        private readonly CloseConfirmation closeConfirmation = new CloseConfirmation();
        private int total, searchGeneration;

        internal ToolkitMail(MailClient client, MailDraft draft)
        {
            this.client = client;
            this.draft = draft;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 250; Window.style.top = 32; Window.style.width = 980; Window.style.height = 620;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("Stalburg Mail"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", Close); close.style.width = 46; close.style.height = 20; close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            status = Text(""); status.style.color = (Color)new Color32(170, 25, 25, 255); status.style.backgroundColor = Color.white; Window.Add(status);
            body = new VisualElement(); body.style.flexGrow = 1; body.style.backgroundColor = Color.white; Window.Add(body);
        }

        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront();
            if (composing) return;
            Refresh();
        }

        internal bool CanClose()
        {
            if (closeConfirmation.Allow(Dirty(), draft.Revision)) return true;
            Window.style.display = DisplayStyle.Flex; Window.BringToFront();
            status.text = "Draft not sent. Close again to leave; it will remain for this session."; return false;
        }

        private bool Dirty() { return draft.Dirty; }
        internal void ResetClose() { closeConfirmation.Reset(); }
        private void Close() { if (CanClose()) { closeConfirmation.Reset(); Window.style.display = DisplayStyle.None; } }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; return row; }
        private static Label Text(string text) { Label label = new Label(text); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button b = new UnityEngine.UIElements.Button(action); b.enableRichText = false; b.text = text; return b; }
        private static JObject Args(string key, object value) { return new JObject(new JProperty(key, value)); }

        private JToken Call(string method, JObject args)
        {
            try { return client.Call(method, args); }
            catch (Exception e) { status.text = e.Message; return null; }
        }

        private void Refresh()
        {
            searchGeneration++;
            armedDelete = false;
            status.text = "";
            JObject state = Call("mail.status", null) as JObject;
            if (state == null) return;
            recipients.Clear();
            foreach (JToken item in (JArray)state["recipients"]) recipients.Add((string)item);
            if (state["account"] == null || state["account"].Type == JTokenType.Null) { SignUp(); return; }
            string query = search == null ? "" : search.value;
            BuildMailbox((string)state["account"]["address"], (int)state["unread"]);
            search.SetValueWithoutNotify(query);
            LoadRows();
            if (!string.IsNullOrWhiteSpace(query) && !pageFailed) SearchRows();
            if (composing) Compose("", "", "");
            else if (current != null) ShowMessage(current); else Hint();
            if (Dirty() && !composing) status.text = "Unsent mail kept for this session.";
        }

        private void SignUp()
        {
            body.Clear();
            Label heading = Text("Create your Stalburg Mail account"); heading.style.fontSize = 22; heading.style.marginTop = 16; body.Add(heading);
            body.Add(Text("Pick a name and a domain. The account belongs to this save."));
            TextField name = new TextField("Username"); name.maxLength = 20; body.Add(name);
            DropdownField domain = new DropdownField("Domain", new List<string> { "stalburg.ts", "opensewer.ts" }, 0); body.Add(domain);
            body.Add(Button("Create account", delegate
            {
                if (Call("mail.signup", new JObject(new JProperty("username", name.value), new JProperty("domain", domain.value))) != null) Refresh();
            }));
        }

        private void BuildMailbox(string address, int unread)
        {
            body.Clear();
            VisualElement columns = Row(); columns.style.flexGrow = 1; body.Add(columns);
            VisualElement rail = new VisualElement(); rail.style.width = 160; rail.style.flexShrink = 0;
            rail.Add(Text("Stalburg MAIL")); rail.Add(Text(address));
            rail.Add(Button("Compose", delegate { Compose("", "", ""); }));
            string[] ids = { "inbox", "important", "sent", "trash" };
            string[] labels = { "Inbox", "Important", "Sent Mail", "Trash" };
            for (int i = 0; i < ids.Length; i++)
            {
                string id = ids[i];
                string text = labels[i] + (id == "inbox" && unread > 0 ? " (" + unread + ")" : "");
                rail.Add(Button(text, delegate { folder = id; picked.Clear(); armedDelete = false; current = null; Refresh(); }));
            }
            columns.Add(rail);
            VisualElement middle = new VisualElement(); middle.style.width = 270; middle.style.flexShrink = 0;
            search = new TextField("Search"); search.RegisterValueChangedCallback(delegate { SearchRows(); }); middle.Add(search);
            VisualElement tools = Row();
            tools.Add(Button("All", delegate { Pick(true); })); tools.Add(Button("None", delegate { Pick(false); }));
            tools.Add(Button("Read", delegate { PickRead(true); })); tools.Add(Button("Unread", delegate { PickRead(false); })); middle.Add(tools);
            list = new ScrollView(); list.style.flexGrow = 1; middle.Add(list); columns.Add(middle);
            VisualElement right = new VisualElement(); right.style.flexGrow = 1;
            VisualElement actions = Row();
            actions.Add(Button("Refresh", Refresh)); actions.Add(Button("Mark unread", MarkUnread)); actions.Add(Button("Delete", Delete));
            actions.Add(Button("◀", delegate { Step(-1); })); actions.Add(Button("▶", delegate { Step(1); })); right.Add(actions);
            view = new ScrollView(); view.style.flexGrow = 1; right.Add(view); columns.Add(right);
        }

        private void LoadRows()
        {
            searchGeneration++;
            rows.Clear(); total = 0; pageFailed = false;
            LoadPage();
            RenderRows();
        }

        private bool LoadPage()
        {
            JObject page = Call("mail.list", new JObject(new JProperty("folder", folder), new JProperty("offset", rows.Count), new JProperty("limit", 20))) as JObject;
            if (page != null && MailPages.Append(rows, page, ref total)) { pageFailed = false; return true; }
            pageFailed = true;
            status.text = "Could not load messages. Retry to try again.";
            return false;
        }

        private void SearchRows()
        {
            int generation = ++searchGeneration;
            status.text = "";
            RenderRows();
            SearchMore(generation, 10);
        }

        private void SearchMore(int generation, int remaining)
        {
            if (generation != searchGeneration || search == null || string.IsNullOrWhiteSpace(search.value) || rows.Exists(Matches) || rows.Count >= total) return;
            if (remaining == 0) { status.text = "No match in the messages loaded so far. Show more to keep searching."; return; }
            Window.schedule.Execute(delegate()
            {
                if (generation != searchGeneration) return;
                if (!LoadPage()) { RenderRows(); return; }
                RenderRows();
                SearchMore(generation, remaining - 1);
            });
        }

        private void ShowMore()
        {
            searchGeneration++;
            if (LoadPage()) { RenderRows(); SearchMore(searchGeneration, 10); }
            else RenderRows();
        }

        private bool Matches(JObject row)
        {
            string q = search == null ? "" : (search.value ?? "").Trim();
            return q.Length == 0 || (((string)row["from"] ?? "") + " " + ((string)row["to"] ?? "") + " " + ((string)row["subject"] ?? "")).IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void RenderRows()
        {
            if (list == null) return;
            list.Clear();
            int shown = 0;
            foreach (JObject row in rows)
            {
                if (!Matches(row)) continue;
                shown++;
                int id = (int)row["id"];
                VisualElement line = Row();
                Toggle select = new Toggle(); select.SetValueWithoutNotify(picked.Contains(id));
                select.RegisterValueChangedCallback(delegate(ChangeEvent<bool> e) { if (e.newValue) picked.Add(id); else picked.Remove(id); armedDelete = false; });
                line.Add(select);
                string from = folder == "sent" ? (string)row["to"] : (string)row["from"];
                UnityEngine.UIElements.Button open = Button(((bool)row["read"] ? "" : "● ") + from + "\n" + (string)row["subject"], delegate { Read(id); });
                open.style.flexGrow = 1; line.Add(open); list.Add(line);
            }
            if (shown == 0) list.Add(Text(pageFailed ? "Messages could not be loaded." : rows.Count == 0 ? "No messages here." : "No matching messages."));
            if (pageFailed || rows.Count < total) list.Add(Button(pageFailed ? "Retry" : "Show more", ShowMore));
        }

        private void Pick(bool value) { picked.Clear(); if (value) foreach (JObject row in rows) if (Matches(row)) picked.Add((int)row["id"]); armedDelete = false; RenderRows(); }
        private void PickRead(bool read) { picked.Clear(); foreach (JObject row in rows) if (Matches(row) && (bool)row["read"] == read) picked.Add((int)row["id"]); armedDelete = false; RenderRows(); }
        private List<int> Targets() { List<int> ids = new List<int>(picked); if (ids.Count == 0 && current != null) ids.Add((int)current["id"]); return ids; }

        private void MarkUnread()
        {
            if (folder != "inbox") { status.text = "Only Inbox messages can be marked unread."; return; }
            foreach (int id in Targets()) if (Call("mail.unread", Args("id", id)) == null) return;
            picked.Clear(); Refresh();
        }

        private void Delete()
        {
            List<int> ids = Targets(); if (ids.Count == 0) return;
            bool hasReplies = ids.Exists(id => draft.Reply(id).Length > 0);
            if ((folder == "trash" || hasReplies) && !armedDelete)
            {
                armedDelete = true;
                status.text = hasReplies ? "Delete again to discard unsent replies and delete the selected messages." : "Delete again to permanently delete the selected messages.";
                return;
            }
            armedDelete = false;
            foreach (int id in ids)
            {
                if (Call("mail.delete", Args("id", id)) == null) { picked.Clear(); current = null; LoadRows(); return; }
                draft.SetReply(id, "");
            }
            picked.Clear(); current = null; Refresh(); status.text = folder == "trash" ? "Deleted." : "Moved to Trash.";
        }

        private void Step(int direction)
        {
            List<JObject> visible = rows.FindAll(Matches);
            int at = current == null ? -1 : visible.FindIndex(row => (int)row["id"] == (int)current["id"]);
            int next = at + direction; if (at >= 0 && next >= 0 && next < visible.Count) Read((int)visible[next]["id"]);
        }

        private void Read(int id)
        {
            armedDelete = false; armedReplace = "";
            JObject message = Call("mail.read", Args("id", id)) as JObject;
            if (message == null) return;
            current = message; composing = false; ShowMessage(message);
            foreach (JObject row in rows) if ((int)row["id"] == id) row["read"] = true;
            RenderRows();
        }

        private void Hint() { if (view != null) { view.Clear(); view.Add(Text("Choose a message to read it.")); } }

        private void ShowMessage(JObject message)
        {
            view.Clear();
            Label subject = Text((string)message["subject"]); subject.style.fontSize = 20; view.Add(subject);
            view.Add(Text((string)message["from"] + "  to " + (string)message["to"] + "\n" + (string)message["when"]));
            view.Add(Text((string)message["body"]));
            int id = (int)message["id"];
            bool starred = (bool)message["starred"];
            view.Add(Button(starred ? "★ Important" : "☆ Important", delegate
            {
                JObject result = Call("mail.star", new JObject(new JProperty("id", id), new JProperty("on", !starred))) as JObject;
                if (result != null) { message["starred"] = (bool)result["starred"]; ShowMessage(message); }
            }));
            JToken attachment = message["attachment"];
            if (attachment != null && attachment.Type == JTokenType.Object)
            {
                bool opened = (bool)attachment["opened"];
                UnityEngine.UIElements.Button open = Button((string)attachment["file"] + (opened ? " (Opened)" : " — Open"), delegate
                {
                    JObject result = Call("mail.attachment", Args("id", id)) as JObject;
                    if (result == null) return;
                    attachment["opened"] = true; ShowMessage(message);
                    status.text = (string)result["outcome"] == "blocked" ? "Blocked by your security software." : "The attachment opened.";
                });
                open.SetEnabled(!opened); view.Add(open);
            }
            string from = (string)message["from"] ?? "";
            int left = from.LastIndexOf('<'), right = from.LastIndexOf('>');
            string address = left >= 0 && right > left ? from.Substring(left + 1, right - left - 1).Trim() : from.Trim();
            if ((string)message["folder"] == "inbox" && recipients.Contains(address))
            {
                TextField reply = new TextField("Quick reply"); reply.multiline = true; reply.maxLength = 3000; reply.style.height = 100;
                reply.SetValueWithoutNotify(draft.Reply(id));
                reply.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { draft.SetReply(id, e.newValue); closeConfirmation.Reset(); armedDelete = false; });
                view.Add(reply);
                view.Add(Button("Reply", delegate
                {
                    string text = draft.Reply(id);
                    if (string.IsNullOrWhiteSpace(text)) { reply.Focus(); return; }
                    try { if (!client.SendReply(draft, id, address, (string)message["subject"] ?? "")) return; }
                    catch (Exception e) { status.text = e.Message; return; }
                    reply.SetValueWithoutNotify(""); closeConfirmation.Reset(); status.text = "Sent.";
                }));
            }
            view.Add(Button("Forward", delegate
            {
                string s = (string)message["subject"] ?? "";
                string quoted = "> " + ((string)message["body"] ?? "").Replace("\n", "\n> ");
                Compose("", s.StartsWith("Fwd: ", StringComparison.OrdinalIgnoreCase) ? s : "Fwd: " + s, "\n\n" + quoted);
            }));
        }

        private void Compose(string to, string subject, string text)
        {
            if ((to.Length > 0 || subject.Length > 0 || text.Length > 0) && draft.ComposeDirty)
            {
                string key = to + "\n" + subject + "\n" + text;
                if (armedReplace != key) { armedReplace = key; status.text = "Draft not sent. Choose the same action again to replace it."; return; }
            }
            armedReplace = "";
            composing = true; closeConfirmation.Reset();
            if (to.Length > 0 || subject.Length > 0 || text.Length > 0) { draft.To = to; draft.Subject = subject; draft.Body = text; }
            view.Clear();
            view.Add(Text("New message"));
            if (recipients.Count == 0) { view.Add(Text("No recipients are available.")); return; }
            int index = recipients.IndexOf(draft.To); if (index < 0) index = 0;
            DropdownField toField = new DropdownField("To", new List<string>(recipients), index); draft.To = toField.value;
            toField.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { draft.To = e.newValue; closeConfirmation.Reset(); }); view.Add(toField);
            TextField subj = new TextField("Subject"); subj.maxLength = 100; subj.SetValueWithoutNotify(draft.Subject);
            subj.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { draft.Subject = e.newValue ?? ""; closeConfirmation.Reset(); armedReplace = ""; }); view.Add(subj);
            TextField message = new TextField("Message"); message.multiline = true; message.maxLength = 3000; message.style.height = 180; message.SetValueWithoutNotify(draft.Body);
            message.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { draft.Body = e.newValue ?? ""; closeConfirmation.Reset(); armedReplace = ""; }); view.Add(message);
            view.Add(Button("Send", delegate
            {
                JObject args = new JObject(new JProperty("to", draft.To), new JProperty("subject", draft.Subject), new JProperty("body", draft.Body));
                if (Call("mail.send", args) == null) return;
                composing = false; ClearDraft(); current = null; folder = "sent"; Refresh(); status.text = "Sent.";
            }));
            view.Add(Button("Discard", delegate { composing = false; ClearDraft(); Hint(); }));
            subj.Focus();
        }

        private void ClearDraft() { draft.Clear(); closeConfirmation.Reset(); armedReplace = ""; }
    }
}
