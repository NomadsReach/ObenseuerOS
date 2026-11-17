using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitNews
    {
        private static readonly string[] Sections = { "Markets", "Sport", "Lottery", "Weather", "Local" };
        internal readonly VisualElement Window;
        private readonly NewsClient client;
        private readonly Label notice;
        private readonly Label date;
        private readonly DropdownField picker;
        private readonly ScrollView body;
        private readonly NewsPhotos photos;
        private readonly string photoFolder;
        private readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private readonly List<int> days = new List<int>();
        private readonly List<string> dayLabels = new List<string>();
        private JArray stories = new JArray();
        private JObject front;
        private int day = -1;
        private bool waiting;
        private int retries;
        private float nextRetry;

        internal ToolkitNews(NewsClient client)
        {
            this.client = client;
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            photoFolder = string.IsNullOrEmpty(location) ? null : Path.Combine(Path.Combine(location, "web"), "photos");
            string data = "";
            try { if (photoFolder != null) data = File.ReadAllText(Path.Combine(photoFolder, "photos-data.js")); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            photos = new NewsPhotos(data);
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 200; Window.style.top = 32; Window.style.width = 1050; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("Stalburg Times"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            VisualElement paper = new VisualElement(); paper.style.flexGrow = 1; paper.style.backgroundColor = Color.white; Window.Add(paper);
            Label masthead = Text("Stalburg Times"); masthead.style.fontSize = 32; masthead.style.unityFontStyleAndWeight = FontStyle.Bold; paper.Add(masthead);
            paper.Add(Text("Established 1881"));
            VisualElement nav = Row(); nav.style.backgroundColor = (Color)new Color32(47, 102, 179, 255);
            nav.Add(Button("ST NEWS", delegate { ShowFront(""); }));
            foreach (string section in Sections) { string name = section; nav.Add(Button(name, delegate { ShowFront(name); })); }
            paper.Add(nav);
            VisualElement utility = Row(); date = Text(""); date.style.flexGrow = 1; utility.Add(date);
            picker = new DropdownField("Edition"); picker.style.width = 260;
            picker.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { int index = dayLabels.IndexOf(e.newValue); if (index >= 0) LoadFront(days[index]); });
            utility.Add(picker); paper.Add(utility);
            notice = Text(""); notice.style.color = (Color)new Color32(160, 30, 30, 255); paper.Add(notice);
            body = new ScrollView(); body.style.flexGrow = 1; paper.Add(body);
            Window.schedule.Execute(Poll).Every(2000);
        }

        internal void Open()
        {
            Window.style.display = DisplayStyle.Flex; Window.BringToFront();
            retries = 0;
            int newest = days.Count == 0 ? -1 : days[0];
            if (!LoadDays()) return;
            if (front == null || waiting || (days.Count > 0 && days[0] != newest)) LoadFront(null);
        }

        private void Poll()
        {
            if (Window.style.display == DisplayStyle.None || !waiting || (retries >= 10 && Time.unscaledTime < nextRetry)) return;
            if (retries < 10) retries++;
            if (retries >= 10) nextRetry = Time.unscaledTime + 30f;
            if (LoadDays()) LoadFront(null);
        }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; return row; }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button b = new UnityEngine.UIElements.Button(action); b.enableRichText = false; b.text = text; return b; }

        private JObject Call(string method, JObject args)
        {
            try { return client.Call(method, args); }
            catch (Exception e) { notice.text = e.Message; return null; }
        }

        private bool LoadDays()
        {
            JObject result = Call("news.editions", null);
            if (result == null) { waiting = client.Available; return false; }
            days.Clear(); dayLabels.Clear();
            foreach (JToken item in (JArray)result["days"])
            {
                days.Add((int)item["day"]); dayLabels.Add("Day " + (int)item["day"] + ", " + (string)item["weekday"]);
            }
            picker.choices = new List<string>(dayLabels);
            int index = days.IndexOf(day);
            if (index >= 0) picker.SetValueWithoutNotify(dayLabels[index]);
            return true;
        }

        private void LoadFront(int? selected)
        {
            JObject args = selected.HasValue ? new JObject(new JProperty("day", selected.Value)) : new JObject();
            JObject result = Call("news.front", args);
            if (result == null) { waiting = client.Available; return; }
            JArray items = result["stories"] as JArray;
            if (items == null || items.Count == 0 || result["lead"] == null)
            {
                waiting = true; front = null; stories = new JArray(); day = -1; date.text = ""; ClearBody();
                body.Add(Text("The first edition comes out at 06:00 game time.")); return;
            }
            waiting = false; retries = 0; notice.text = ""; front = result; stories = items; day = (int)result["day"];
            date.text = "Day " + day + ", " + (string)result["weekday"];
            int index = days.IndexOf(day); if (index >= 0) picker.SetValueWithoutNotify(dayLabels[index]);
            ShowFront("");
        }

        private void ShowFront(string selectedSection)
        {
            if (front == null) { LoadFront(null); return; }
            notice.text = ""; ClearBody();
            JObject lead = (JObject)front["lead"];
            AddPhoto(body, (string)lead["section"], day, (int)lead["i"], false);
            body.Add(Text((string)lead["section"]));
            UnityEngine.UIElements.Button headline = StoryButton(lead); headline.style.fontSize = 24; body.Add(headline);
            body.Add(Text((string)lead["body"])); AddFigures(body, lead);
            VisualElement target = null;
            foreach (string section in Sections)
            {
                Label heading = Text(section); heading.style.fontSize = 20; body.Add(heading);
                if (section == selectedSection) target = heading;
                int count = 0;
                for (int i = 1; i < stories.Count; i++)
                {
                    JObject story = (JObject)stories[i]; if ((string)story["section"] != section) continue;
                    VisualElement card = Row(); AddPhoto(card, section, day, (int)story["i"], true); card.Add(StoryButton(story)); body.Add(card); count++;
                }
                if (count == 0) body.Add(Text("Nothing today."));
            }
            body.Add(Text("Latest"));
            for (int i = 1; i < stories.Count && i <= 5; i++) body.Add(StoryButton((JObject)stories[i]));
            body.Add(Text("Advertisement — Osmo Olut: The World's Strongest Beer."));
            body.Add(Text("Advertisement — Stalburg Steel: Steel for the whole of Stolland."));
            Footer();
            if (target != null) { VisualElement selected = target; body.schedule.Execute(delegate() { body.ScrollTo(selected); }); }
            else body.scrollOffset = Vector2.zero;
        }

        private UnityEngine.UIElements.Button StoryButton(JObject story)
        {
            int index = (int)story["i"];
            return Button((string)story["headline"], delegate { OpenArticle(index); });
        }

        private void OpenArticle(int index)
        {
            JObject article = Call("news.article", new JObject(new JProperty("day", day), new JProperty("i", index)));
            if (article == null) return;
            notice.text = ""; ClearBody(); body.Add(Button("Front page", delegate { ShowFront(""); }));
            body.Add(Text((string)article["section"]));
            Label headline = Text((string)article["headline"]); headline.style.fontSize = 24; body.Add(headline);
            body.Add(Text("By Stalburg Times staff · " + date.text));
            AddPhoto(body, (string)article["section"], day, index, false);
            foreach (string paragraph in ((string)article["body"] ?? "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)) body.Add(Text(paragraph.Trim()));
            AddFigures(body, article);
            JArray numbers = article["nums"] as JArray;
            if (numbers != null && numbers.Count > 0) body.Add(Text(string.Join("   ", Array.ConvertAll(numbers.ToObject<int[]>(), x => x.ToString(CultureInfo.CurrentCulture)))));
            List<JObject> same = new List<JObject>(), others = new List<JObject>();
            foreach (JToken item in stories)
            {
                JObject candidate = (JObject)item; if ((int)candidate["i"] == index) continue;
                others.Add(candidate); if ((string)candidate["section"] == (string)article["section"]) same.Add(candidate);
            }
            List<JObject> related = same.Count > 0 ? same : others;
            if (related.Count > 0) body.Add(Text("Related stories"));
            for (int i = 0; i < related.Count && i < 3; i++) body.Add(StoryButton(related[i]));
            Footer(); body.scrollOffset = Vector2.zero;
        }

        private static void AddFigures(VisualElement parent, JObject article)
        {
            JArray rows = article["rows"] as JArray; if (rows == null) return;
            foreach (JToken row in rows)
            {
                int decimals = Math.Max(0, Math.Min(6, (int)row["dec"]));
                string unit = (string)row["unit"] ?? "";
                parent.Add(Text((string)row["label"] + ": " + ((decimal)row["value"]).ToString("N" + decimals, CultureInfo.CurrentCulture) + (unit.Length == 0 ? "" : " " + unit)));
            }
        }

        private void Footer()
        {
            VisualElement links = Row();
            links.Add(Button("About us", delegate { Page("About us", "Stalburg Times LLC publishes the Stalburg Times from Stalburg. The paper has been printed since 1881.\nA new edition comes out each morning at 06:00 game time."); }));
            links.Add(Button("Corrections", delegate { Page("Corrections", "Errors are corrected in the next edition, on the page where the error was printed."); }));
            links.Add(Button("Privacy", delegate { Page("Privacy", "The Stalburg Times does not ask for your name or an account to read the paper."); }));
            links.Add(Button("Terms of use", delegate { Page("Terms of use", "The stories in this paper are for reading. Copy them only with the paper's permission."); }));
            body.Add(links); body.Add(Text("© Stalburg Times LLC. All rights reserved."));
        }

        private void Page(string heading, string content)
        {
            ClearBody(); body.Add(Button("Front page", delegate { ShowFront(""); }));
            Label title = Text(heading); title.style.fontSize = 24; body.Add(title); body.Add(Text(content)); Footer(); body.scrollOffset = Vector2.zero;
        }

        private void AddPhoto(VisualElement parent, string section, int selectedDay, int index, bool thumbnail)
        {
            string file = photos.Pick(section, selectedDay, index, thumbnail);
            if (file == null || photoFolder == null) return;
            Texture2D texture;
            if (!textures.TryGetValue(file, out texture))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(Path.Combine(photoFolder, file));
                    texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!ImageConversion.LoadImage(texture, bytes)) { UnityEngine.Object.Destroy(texture); return; }
                    textures.Add(file, texture);
                }
                catch (IOException) { return; }
                catch (UnauthorizedAccessException) { return; }
            }
            Image image = new Image(); image.image = texture; image.scaleMode = ScaleMode.ScaleAndCrop;
            image.style.width = thumbnail ? 120 : 520; image.style.height = thumbnail ? 80 : 240; parent.Add(image);
            if (!thumbnail) parent.Add(Text("Stalburg Times file photo."));
        }

        private void ClearBody() { body.Clear(); Release(); }
        internal void Release() { foreach (Texture2D texture in textures.Values) if (texture != null) UnityEngine.Object.Destroy(texture); textures.Clear(); }
    }
}
