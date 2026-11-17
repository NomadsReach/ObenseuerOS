using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitEye
    {
        internal readonly VisualElement Window;
        private readonly Func<EyeState> openState;
        private readonly Action checkSession;
        private readonly Func<string> accountName;
        private readonly Func<int, bool> launch;
        private readonly NewsClient news;
        private readonly NewsPhotos photos;
        private readonly string photoFolder;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly ScrollView content;
        private readonly Label notice;
        private EyeState state;
        private JArray headlines = new JArray();
        private string view = "home", query = "";
        private int thread = -1, commentPost = -1;
        private static readonly string[] Names = { "Stalburg Times", "Jarmo Pettinen", "Osmo Olut", "Johan Wider", "Stalburg Mass Transit", "Pera Harju", "Hammer Valley Hydro" };
        private static readonly string[] Posts = {
            "The water tunnel story is on the front page again. Our timeline puts the dates in order before you believe the rumours.",
            "Queue at the Money Exchange in Kolhola again. The rate changes faster than the line moves.",
            "Friday at the bar: a cold one, a quiet corner and nobody asking about the tunnel.",
            "Does the 7 tram run on Sunday? The route board says one thing and the platform says another.",
            "Service update: the Cellecter line runs a reduced timetable this weekend. Check the stop boards before you travel.",
            "Evening walk through the old quarter. The lights are back on in the hill houses.",
            "Visitor centre opening hours for the weekend are on the notice board at the dam. Bring a coat."
        };
        private static readonly int[] Likes = { 412, 37, 128, 21, 96, 54, 33 }, Comments = { 58, 9, 14, 17, 6, 11, 4 };
        private static readonly string[] Contacts = { "Jarmo Pettinen", "Johan Wider", "Pera Harju", "Markku Siltanen" };
        private static readonly string[,] Messages = {
            { "Are you still in the Kolhola queue at the Money Exchange? The rate moved again.", "Never mind, I got through. Bring OC next time." },
            { "Did the 7 tram run on Sunday? The board said one thing and the platform another.", "Forget it. I walked from Tilstrand." },
            { "I have new pictures of the old quarter in Obenseuer. Want a copy?", "Send me the Times piece when it is out. I want the water tunnel part." },
            { "Did you read the Bergmann Group story in the Times? Read it before you talk about the tunnels.", "Call me when you have a minute. Not on the phone." }
        };

        internal ToolkitEye(Func<EyeState> openState, Action checkSession, Func<string> accountName, NewsClient news, Func<int, bool> launch)
        {
            this.openState = openState; this.checkSession = checkSession; this.accountName = accountName; this.news = news; this.launch = launch;
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            photoFolder = string.IsNullOrEmpty(location) ? null : Path.Combine(location, "web", "photos"); string data = "";
            try { if (photoFolder != null) data = File.ReadAllText(Path.Combine(photoFolder, "photos-data.js")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            photos = new NewsPhotos(data);
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 160; Window.style.top = 32; Window.style.width = 1080; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36; Label title = Text("EYEbook"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; }); close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && state != null) Session(); }).Every(1000);
        }
        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront(); if (!Session()) return;
            if (state == null) { try { state = openState(); } catch (Exception e) { notice.text = e.Message; content.Clear(); content.Add(Button("Retry", Open)); return; } }
            try { JObject front = news.Call("news.front", null); headlines = front["stories"] as JArray ?? new JArray(); } catch (Exception) { headlines = new JArray(); }
            Render();
        }
        private bool Session()
        {
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { state = null; headlines = new JArray(); content.Clear(); notice.text = e.Message; return false; }
        }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = value; return button; }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private void Change(Action action)
        {
            if (state == null || !Session()) return;
            try { action(); notice.text = ""; Render(); } catch (Exception e) { notice.text = e.Message; }
        }
        private void Render()
        {
            if (state == null || !Session()) return;
            content.Clear(); Label heading = Text("EYEbook"); heading.style.fontSize = 30; heading.style.backgroundColor = (Color)new Color32(59, 89, 152, 255); heading.style.color = Color.white; content.Add(heading);
            VisualElement top = Row();
            foreach (string name in new[] { "home", "friends", "postal", "notifications", "profile", "saved", "photos", "pages" })
            {
                string selected = name; top.Add(Button(name, delegate { view = selected; query = ""; thread = -1; commentPost = -1; notice.text = ""; Render(); }));
            }
            top.Add(state.Name.Length == 0 ? Button("Log in", delegate { Change(delegate { state.Login(accountName == null ? "" : accountName()); }); }) : Text("Signed in as " + state.Name)); content.Add(top);
            VisualElement search = Row(); TextField searchText = new TextField("Search EYEbook"); searchText.SetValueWithoutNotify(query); searchText.style.flexGrow = 1; search.Add(searchText);
            search.Add(Button("Search", delegate { query = searchText.value ?? ""; Render(); })); search.Add(Button("Clear", delegate { query = ""; Render(); })); content.Add(search);
            if (query.Trim().Length > 0) { Search(); return; }
            if (view == "home") Home(); else if (view == "friends") Friends(); else if (view == "postal") Postal(); else if (view == "notifications") Notifications(); else if (view == "profile") Profile(); else if (view == "saved") Saved(); else if (view == "photos") Filtered(false); else Filtered(true);
        }
        private void Home()
        {
            if (state.Name.Length == 0)
            {
                content.Add(Text("...SO WE CAN KEEP AN EYE ON YOU!"));
                content.Add(Button("Create new account", delegate { notice.text = "New registrations are closed."; }));
                content.Add(Text("EYEbook collects information about you. Sign up is free; account deletion typically takes about 30 years."));
            }
            Composer("What is on your mind?", delegate(string text) { state.Publish(text); });
            JArray statuses = state.Statuses; for (int i = statuses.Count - 1; i >= 0; i--) content.Add(Text(state.Name + " · Status · Public\n" + (string)statuses[i]["t"]));
            JArray acts = state.Actions; for (int i = acts.Count - 1; i >= 0; i--) if ((string)acts[i]["k"] == "share") Post((int)acts[i]["p"], true);
            for (int i = 0; i < headlines.Count && i < 3; i++)
            {
                JObject story = headlines[i] as JObject;
                if (story == null || story["section"] == null || story["section"].Type != JTokenType.String || story["headline"] == null || story["headline"].Type != JTokenType.String) continue;
                content.Add(Text("Stalburg Times · " + (string)story["section"] + "\n" + (string)story["headline"]));
                content.Add(Button("Read in the Times", delegate { if (Session()) launch(9); }));
            }
            for (int i = 0; i < Posts.Length; i++) Post(i, false);
        }
        private void Composer(string label, Action<string> send)
        {
            VisualElement row = Row(); TextField input = new TextField(label); input.maxLength = 140; input.style.flexGrow = 1; row.Add(input);
            row.Add(Button("Post", delegate { string value = input.value; Change(delegate { send(value); }); })); content.Add(row);
        }
        private void Post(int index, bool shared)
        {
            int i = index; JArray acts = state.Actions; int comments = 0;
            foreach (JToken act in acts) if ((string)act["k"] == "comment" && (int)act["p"] == i) comments++;
            if (shared) content.Add(Text("You shared this"));
            content.Add(Button(Names[i], delegate { query = Names[i]; Render(); })); content.Add(Text(Posts[i]));
            if (i == 0 || i == 5) Photo(i == 0 ? "industry" : "street", i);
            content.Add(Text((Likes[i] + (state.Has("like", i) ? 1 : 0)) + " likes · " + (Comments[i] + comments) + " comments"));
            VisualElement actions = Row(); actions.Add(Button(state.Has("like", i) ? "Unlike" : "Like", delegate { Change(delegate { state.Like(i); }); }));
            actions.Add(Button("Comment", delegate { if (state.Name.Length == 0) { notice.text = "Log in to EYEbook first."; return; } commentPost = commentPost == i ? -1 : i; Render(); }));
            actions.Add(Button("Share", delegate { Change(delegate { state.Share(i); }); })); content.Add(actions);
            foreach (JToken act in acts) if ((string)act["k"] == "comment" && (int)act["p"] == i) content.Add(Text(state.Name + ": " + (string)act["t"]));
            if (commentPost == i) Composer("Write a comment", delegate(string value) { state.Comment(i, value); commentPost = -1; });
        }
        private void Photo(string tag, int seed)
        {
            string file = photos.PickTag(tag, seed, true); if (file == null || photoFolder == null) return; Texture2D texture;
            if (!textures.TryGetValue(file, out texture))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(Path.Combine(photoFolder, file)); texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, bytes)) { UnityEngine.Object.Destroy(texture); return; }
                    textures.Add(file, texture);
                }
                catch (IOException) { return; }
                catch (UnauthorizedAccessException) { return; }
            }
            Image image = new Image(); image.image = texture; image.scaleMode = ScaleMode.ScaleAndCrop; image.style.width = 480; image.style.height = 260; content.Add(image);
        }
        internal void Release() { foreach (Texture2D texture in textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture); textures.Clear(); }
        private void Friends() { content.Add(Text("Friends")); foreach (string name in Contacts) { string selected = name; content.Add(Button(name, delegate { query = selected; Render(); })); } }
        private void Postal()
        {
            content.Add(Text("Postal"));
            if (thread < 0) { for (int i = 0; i < Contacts.Length; i++) { int selected = i; content.Add(Button(Contacts[i], delegate { thread = selected; Render(); })); } return; }
            content.Add(Button("Back to messages", delegate { thread = -1; Render(); })); content.Add(Text(Contacts[thread]));
            for (int i = 0; i < 2; i++) content.Add(Text(Messages[thread, i]));
        }
        private void Notifications()
        {
            if (state.Name.Length == 0) { content.Add(Text("Log in to EYEbook first.")); return; }
            JArray acts = state.Actions; int shown = 0;
            for (int i = acts.Count - 1; i >= 0; i--) { JToken act = acts[i]; string kind = (string)act["k"]; if (kind == "share") continue; shown++; content.Add(Text(kind == "like" ? "You liked a post by " + Names[(int)act["p"]] + "." : "You commented on a post by " + Names[(int)act["p"]] + ": " + (string)act["t"])); }
            if (shown == 0) content.Add(Text("No likes or comments yet."));
        }
        private void Profile()
        {
            if (state.Name.Length == 0) { content.Add(Text("Log in to EYEbook first.")); return; }
            content.Add(Text(state.Name)); JArray statuses = state.Statuses;
            for (int i = statuses.Count - 1; i >= 0; i--) content.Add(Text((string)statuses[i]["t"]));
            JArray acts = state.Actions; int count = 0; for (int i = acts.Count - 1; i >= 0; i--) if ((string)acts[i]["k"] == "share") { count++; Post((int)acts[i]["p"], true); }
            if (count == 0) content.Add(Text("You have not shared anything yet."));
        }
        private void Saved()
        {
            if (state.Name.Length == 0) { content.Add(Text("Log in to EYEbook first.")); return; }
            int count = 0; for (int i = 0; i < Posts.Length; i++) if (state.Has("like", i)) { count++; Post(i, false); }
            if (count == 0) content.Add(Text("Nothing saved yet. Like a post to save it."));
        }
        private void Filtered(bool pages)
        {
            content.Add(Text(pages ? "Pages" : "Photos"));
            for (int i = 0; i < Posts.Length; i++) if (pages ? i == 0 || i == 2 || i == 4 || i == 6 : i == 0 || i == 5) Post(i, false);
        }
        private void Search()
        {
            string term = query.Trim(); content.Add(Text("People")); int count = 0;
            foreach (string name in Contacts) if (name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) { string selected = name; content.Add(Button(name, delegate { query = selected; Render(); })); count++; }
            for (int i = 0; i < Names.Length; i++) if (Names[i].IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0 && Array.IndexOf(Contacts, Names[i]) < 0) { string selected = Names[i]; content.Add(Button(selected, delegate { query = selected; Render(); })); count++; }
            if (count == 0) content.Add(Text("No people match.")); count = 0;
            for (int i = 0; i < Posts.Length; i++) if ((Names[i] + " " + Posts[i]).IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0) { Post(i, false); count++; }
            if (count == 0) content.Add(Text("No posts match."));
        }
    }
}
