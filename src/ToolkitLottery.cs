using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitLottery
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient client;
        private readonly Action checkSession;
        private readonly Label notice, warning;
        private Label wallet;
        private readonly UnityEngine.UIElements.Button acknowledge;
        private readonly ScrollView content;
        private readonly List<int> picks = new List<int>();
        private readonly Dictionary<string, Texture2D> cardTextures = new Dictionary<string, Texture2D>();
        private readonly JArray lines = new JArray();
        private JObject state, scratch, tickets, results;
        private string tab = "draw", scope = "open", confirmation = "", forgetConfirmation = "";
        private bool ship;
        private int page;
        private long acknowledgedVersion = -1;

        internal ToolkitLottery(TransactionClient client, Action checkSession)
        {
            this.client = client; this.checkSession = checkSession;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 160; Window.style.top = 32; Window.style.width = 1080; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("LOTTO by NaSpoBet"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; confirmation = ""; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            warning = Text(""); warning.style.backgroundColor = Color.white; warning.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(warning);
            acknowledge = Button("I checked my tickets and cards and understand the risk", delegate { acknowledgedVersion = client.PendingVersion; UpdateWarning(); }); Window.Add(acknowledge);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            Window.schedule.Execute(delegate() { if (Window.style.display != DisplayStyle.None && state != null) Session(); }).Every(1000);
            UpdateWarning();
        }
        internal void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(); }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = value; return button; }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private bool Session()
        {
            try { if (checkSession != null) checkSession(); return true; }
            catch (Exception e) { state = null; content.Clear(); notice.text = e.Message; return false; }
        }
        private void UpdateWarning()
        {
            warning.text = client.HasPending ? "A lottery purchase may have completed. Check your tickets and cards before another purchase. Uncertain purchases:\n" + client.PendingDetails : "";
            acknowledge.style.display = client.HasPending && acknowledgedVersion != client.PendingVersion ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private JObject Read(string method, JObject args)
        {
            try { return client.Read(method, args) as JObject; }
            catch (Exception e) { notice.text = e.Message; return null; }
        }
        private void Refresh()
        {
            if (!Session()) return;
            JObject next = Read("lotto.state", null), cards = Read("scratch.history", null);
            JObject draws = Read("lotto.results", null), pageData = Read("lotto.tickets", new JObject(new JProperty("scope", scope), new JProperty("page", page)));
            if (next == null || cards == null || draws == null || pageData == null) { state = null; content.Clear(); content.Add(Text("Lottery is unavailable.")); content.Add(Button("Refresh", Refresh)); return; }
            state = next; scratch = cards; results = draws; tickets = pageData; Render();
        }
        private void Render()
        {
            if (state == null || !Session()) return;
            UpdateWarning(); content.Clear();
            Label title = Text("LOTTO by NaSpoBet"); title.style.fontSize = 28; content.Add(title);
            content.Add(Text("18+ · Play within your means. Lottery games are entertainment, not a way to make money."));
            wallet = Text(WalletText()); content.Add(wallet);
            VisualElement nav = Row();
            foreach (string name in new[] { "draw", "scratch", "tickets", "results" }) { string section = name; nav.Add(Button(name, delegate { tab = section; confirmation = ""; Render(); })); }
            nav.Add(Button("Refresh", Refresh)); content.Add(nav);
            foreach (string pending in client.PendingKeys)
            {
                int split = pending.IndexOf(' '); if (split < 0) continue;
                string method = pending.Substring(0, split), saved = pending.Substring(split + 1);
                if (method != "lotto.buy" && method != "scratch.buy") continue;
                content.Add(Button("Check uncertain purchase: " + saved, delegate { CheckPending(method, saved); }));
                content.Add(Button("Clear this uncertain purchase", delegate { Forget(method, JObject.Parse(saved)); }));
            }
            if (tab == "draw") Draw(); else if (tab == "scratch") Scratch(); else if (tab == "tickets") Tickets(); else Results();
        }
        private string WalletText() { return "Wallet: " + state["oc"] + " OC / " + ((decimal)scratch["rm"]).ToString("N2", CultureInfo.CurrentCulture) + " RM"; }
        private void Draw()
        {
            JToken next = state["nextDraw"];
            content.Add(Text("Next draw " + next["n"] + " · Sales " + ((bool)next["salesOpen"] ? "open" : "closed") + " · Draw in " + next["drawsIn"] + " minutes"));
            content.Add(Text("Ticket price " + state["price"] + " OC · " + state["boughtForNext"] + " of " + state["maxPerDraw"] + " tickets for this draw"));
            content.Add(Text("Choose six different numbers from 1 to 40."));
            VisualElement grid = Row();
            for (int i = 1; i <= 40; i++)
            {
                int n = i; UnityEngine.UIElements.Button b = Button((picks.Contains(n) ? "✓ " : "") + n, delegate { if (picks.Contains(n)) picks.Remove(n); else if (picks.Count < 6) picks.Add(n); confirmation = ""; Render(); });
                b.style.width = 65; grid.Add(b);
            }
            content.Add(grid);
            VisualElement controls = Row();
            controls.Add(Button("Add line", delegate { if (picks.Count != 6) { notice.text = "Select six numbers."; return; } if (lines.Count >= 5) { notice.text = "A purchase can contain up to five lines."; return; } picks.Sort(); lines.Add(new JArray(picks)); picks.Clear(); confirmation = ""; Render(); }));
            controls.Add(Button("Quick pick", delegate { JObject quick = Read("lotto.quick", null); if (quick == null) return; JArray found = (JArray)quick["lines"]; if (lines.Count >= 5) { notice.text = "A purchase can contain up to five lines."; return; } lines.Add(found[0].DeepClone()); confirmation = ""; Render(); }));
            controls.Add(Button("Clear selection", delegate { picks.Clear(); Render(); })); content.Add(controls);
            for (int i = 0; i < lines.Count; i++)
            {
                int index = i; VisualElement row = Row(); row.Add(Text("Line " + (i + 1) + ": " + string.Join(", ", ((JArray)lines[i]).ToObject<int[]>())));
                row.Add(Button("Remove", delegate { lines.RemoveAt(index); confirmation = ""; Render(); })); content.Add(row);
            }
            content.Add(Text("Total: " + ((int)state["price"] * lines.Count) + " OC"));
            content.Add(Button("Review / buy tickets", delegate { if (lines.Count == 0) { notice.text = "Add at least one line."; return; } Purchase("lotto.buy", new JObject(new JProperty("lines", lines.DeepClone()))); }));
            content.Add(Text("Prizes: 3 numbers 120 OC; 4 numbers 1,500 OC; 5 numbers 20,000 OC; 6 numbers 50,000 OC."));
            if ((int)state["owedOc"] > 0) content.Add(Text("Pending payout: " + state["owedOc"] + " OC"));
        }
        private void Scratch()
        {
            content.Add(Text("Scratch cards: " + scratch["today"] + " of " + scratch["perDay"] + " purchased today."));
            Toggle delivery = new Toggle("Ship physical card to the parcel locker") { value = ship };
            delivery.RegisterValueChangedCallback(delegate(ChangeEvent<bool> e) { ship = e.newValue; confirmation = ""; Render(); }); content.Add(delivery);
            foreach (JObject kind in (JArray)scratch["kinds"])
            {
                string key = (string)kind["kind"], name = (string)kind["name"];
                string cost = ship ? (kind["shipTotal"].Type == JTokenType.Null ? "unavailable" : (string)kind["shipTotal"] + " OC") : (string)kind["price"] + " OC";
                content.Add(Text(name + " · Price " + cost + " · Top prize " + kind["top"] + " " + kind["currency"]));
                content.Add(Button("Review / buy " + name, delegate { Purchase("scratch.buy", new JObject(new JProperty("kind", key), new JProperty("ship", ship))); }));
            }
            content.Add(Text("Your cards"));
            foreach (JObject card in (JArray)scratch["cards"])
            {
                int id = (int)card["id"]; bool shipped = (bool)card["shipped"], revealed = (bool)card["revealed"];
                Label status = Text("Card " + id + " · " + (string)card["kind"] + " · " + (shipped ? "shipped" : revealed ? CardResult(card) : "not revealed"));
                content.Add(status);
                if (!shipped) Card(card, status);
            }
        }
        private void Card(JObject card, Label status)
        {
            string kind = (string)card["kind"]; int id = (int)card["id"]; bool revealed = (bool)card["revealed"];
            Texture2D texture = CardTexture(kind);
            if (texture == null) { if (!revealed) content.Add(Button("Reveal card " + id, delegate { Reveal(id, kind); })); return; }
            VisualElement panel = new VisualElement(); panel.style.width = 240; panel.style.height = 360;
            Image art = new Image(); art.image = texture; art.scaleMode = ScaleMode.StretchToFill; art.style.position = UnityEngine.UIElements.Position.Absolute;
            art.style.left = 0; art.style.top = 0; art.style.width = 240; art.style.height = 360; art.pickingMode = PickingMode.Ignore; panel.Add(art);
            float top = kind == "spade" ? 253f : 267f, height = kind == "spade" ? 68f : 54f;
            Label result = Text(revealed ? CardResult(card) : "");
            result.style.position = UnityEngine.UIElements.Position.Absolute; result.style.left = 15; result.style.top = top; result.style.width = 210; result.style.height = height;
            result.style.backgroundColor = Color.white; result.style.unityTextAlign = TextAnchor.MiddleCenter; result.pickingMode = PickingMode.Ignore; panel.Add(result);
            if (!revealed)
            {
                ScratchProgress progress = new ScratchProgress(); bool opened = false, opening = false;
                for (int i = 0; i < 30; i++)
                {
                    int index = i; VisualElement cover = new VisualElement(); cover.style.position = UnityEngine.UIElements.Position.Absolute;
                    cover.style.left = 15 + (i % 10) * 21; cover.style.top = top + (i / 10) * (height / 3); cover.style.width = 21; cover.style.height = height / 3;
                    cover.style.backgroundColor = (Color)new Color32(145, 153, 158, 255);
                    Action clear = delegate
                    {
                        if (!Session()) return;
                        if (!opened)
                        {
                            if (opening) return;
                            opening = true; JObject reply;
                            try { reply = Read("scratch.reveal", new JObject(new JProperty("id", id))); }
                            finally { opening = false; }
                            JObject value = reply == null ? null : reply["card"] as JObject;
                            if (!ScratchReply.Valid(reply, id, kind)) { notice.text = "Card result could not be confirmed. Check card history before trying again."; return; }
                            card["revealed"] = true; card["prize"] = value["prize"].DeepClone(); card["currency"] = value["currency"].DeepClone(); opened = true;
                            state["oc"] = reply["balance"].DeepClone(); scratch["rm"] = reply["rm"].DeepClone();
                            result.text = CardResult(card); wallet.text = WalletText(); status.text = "Card " + id + " · " + kind + " · Scratching";
                        }
                        if (progress.Mark(index)) cover.style.visibility = Visibility.Hidden;
                        if (progress.Finished) Completed("Card revealed. Check card history for the result.");
                    };
                    cover.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent e) { if (e.button == 0) clear(); });
                    cover.RegisterCallback<PointerEnterEvent>(delegate(PointerEnterEvent e) { if ((e.pressedButtons & 1) != 0) clear(); });
                    cover.RegisterCallback<PointerMoveEvent>(delegate(PointerMoveEvent e) { if ((e.pressedButtons & 1) != 0) clear(); });
                    panel.Add(cover);
                }
                content.Add(panel); content.Add(Button("Reveal card " + id, delegate { Reveal(id, kind); }));
            }
            else content.Add(panel);
        }
        private static string CardResult(JObject card) { return (int)card["prize"] > 0 ? "Prize: " + card["prize"] + " " + card["currency"] : "No win. Try again."; }
        private Texture2D CardTexture(string kind)
        {
            string file = kind == "spade" ? "spade.jpg" : "oc-pot.jpg"; Texture2D texture;
            if (cardTextures.TryGetValue(file, out texture)) return texture;
            string location = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(location)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(Path.Combine(location, "web", "apps", "lottery", file));
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!ImageConversion.LoadImage(texture, bytes)) { UnityEngine.Object.Destroy(texture); return null; }
                cardTextures.Add(file, texture); return texture;
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
        internal void Release() { foreach (Texture2D texture in cardTextures.Values) if (texture != null) UnityEngine.Object.Destroy(texture); cardTextures.Clear(); }
        private void Purchase(string method, JObject args)
        {
            if (!Session()) return;
            if (!client.MayStart(method, args, acknowledgedVersion == client.PendingVersion)) { confirmation = ""; notice.text = "Check your tickets and cards and acknowledge the warning first."; return; }
            if (client.PendingFor(method, args)) { CheckPending(method, args.ToString(Formatting.None)); return; }
            string cost, terms;
            if (method == "lotto.buy")
            {
                JObject current = Read("lotto.state", null);
                if (current == null || !(bool)current["nextDraw"]["salesOpen"]) { confirmation = ""; notice.text = "Ticket sales are unavailable or closed."; return; }
                cost = ((int)current["price"] * ((JArray)args["lines"]).Count) + " OC";
                terms = "draw " + current["nextDraw"]["n"] + ", " + cost;
            }
            else
            {
                JObject current = Read("scratch.history", null); JToken found = null;
                if (current != null) foreach (JToken kind in (JArray)current["kinds"]) if ((string)kind["kind"] == (string)args["kind"]) found = kind;
                if (found == null) { confirmation = ""; notice.text = "Scratch card pricing is unavailable."; return; }
                JToken amount = (bool)args["ship"] ? found["shipTotal"] : found["price"];
                if (amount == null || amount.Type == JTokenType.Null) { confirmation = ""; notice.text = "Delivery is unavailable."; return; }
                cost = amount + " OC"; terms = (string)found["name"] + ", " + ((bool)args["ship"] ? "shipped, " : "in game, ") + cost;
            }
            string key = method + args.ToString(Formatting.None) + terms;
            if (confirmation != key) { confirmation = key; notice.text = "Review purchase: " + terms + ". Press again to confirm."; return; }
            confirmation = "";
            try
            {
                client.Write(method, args, delegate(JToken result) { return result is JObject && (method == "lotto.buy" ? result["tickets"] is JArray && ((JArray)result["tickets"]).Count > 0 : result["card"] is JObject && result["card"]["id"] != null && result["card"]["id"].Type == JTokenType.Integer); });
            }
            catch (Exception e) { acknowledgedVersion = -1; UpdateWarning(); Render(); notice.text = e.Message + " Check your tickets and cards before trying again."; return; }
            if (method == "lotto.buy") lines.Clear(); UpdateWarning(); Completed("Purchase completed. Check your tickets or cards.");
        }
        private void CheckPending(string method, string saved)
        {
            if (!Session()) return;
            JObject args;
            bool found;
            try
            {
                args = JObject.Parse(saved); string lookup = method == "lotto.buy" ? "lotto.result" : "scratch.result";
                found = client.Resolve(method, args, lookup, args);
            }
            catch (Exception e) { notice.text = e.Message; return; }
            if (!found) { notice.text = "This purchase cannot be confirmed. Check your tickets and cards; it may still have completed."; return; }
            if (method == "lotto.buy" && JToken.DeepEquals(args["lines"], lines)) lines.Clear();
            confirmation = ""; UpdateWarning(); Completed("The previous purchase completed. Check your tickets or cards.");
        }
        private void Completed(string message)
        {
            try { Refresh(); string error = state == null ? notice.text : ""; notice.text = message + (error.Length == 0 ? "" : " Refresh failed: " + error); }
            catch (Exception e) { notice.text = message + " Refresh failed: " + e.Message; }
        }
        private void Forget(string method, JObject args)
        {
            if (!Session()) return;
            if (acknowledgedVersion != client.PendingVersion) { notice.text = "Check tickets and cards and acknowledge the warning first."; return; }
            if (!client.PendingFor(method, args)) { forgetConfirmation = ""; notice.text = "No uncertain purchase matches."; return; }
            string key = method + args.ToString(Formatting.None) + client.PendingVersion;
            if (forgetConfirmation != key) { forgetConfirmation = key; notice.text = "Clearing this record allows a new purchase that could duplicate one already completed. Check your tickets and cards, then press clear again."; return; }
            client.Forget(method, args); forgetConfirmation = ""; confirmation = ""; UpdateWarning(); Render(); notice.text = "Uncertain purchase cleared. A new purchase could repeat it.";
        }
        private void Reveal(int id, string kind)
        {
            if (!Session()) return;
            JObject result = Read("scratch.reveal", new JObject(new JProperty("id", id)));
            if (!ScratchReply.Valid(result, id, kind)) { notice.text = "Card result could not be confirmed. Check card history before trying again."; return; }
            Completed("Card revealed. Check card history for the result.");
        }
        private void Tickets()
        {
            VisualElement nav = Row(); nav.Add(Button("Open", delegate { scope = "open"; page = 0; Refresh(); })); nav.Add(Button("Finished", delegate { scope = "done"; page = 0; Refresh(); }));
            nav.Add(Button("Previous", delegate { if (page > 0) { page--; Refresh(); } })); nav.Add(Button("Next", delegate { if ((page + 1) * 25 < (int)tickets["total"]) { page++; Refresh(); } })); content.Add(nav);
            content.Add(Text(scope + " tickets · Page " + (page + 1) + " · " + tickets["total"] + " total"));
            foreach (JToken ticket in (JArray)tickets["tickets"]) content.Add(Text("Draw " + ticket["draw"] + " · " + string.Join(", ", ((JArray)ticket["nums"]).ToObject<int[]>()) + " · " + ticket["state"] + (ticket["matches"].Type == JTokenType.Null ? "" : " · " + ticket["matches"] + " right") + " · " + ticket["oc"] + " OC"));
        }
        private void Results()
        {
            content.Add(Text("Recent draws"));
            foreach (JToken draw in (JArray)results["draws"])
            {
                JArray winners = (JArray)draw["winners"];
                content.Add(Text("Draw " + draw["n"] + ((bool)draw["late"] ? " (late)" : "") + " · " + string.Join(", ", ((JArray)draw["nums"]).ToObject<int[]>()) + " · Winners: 3 right " + winners[0] + ", 4 right " + winners[1] + ", 5 right " + winners[2] + ", 6 right " + winners[3] + " · Paid " + draw["paidOc"] + " OC"));
            }
        }
    }
}
