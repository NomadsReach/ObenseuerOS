using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitExchange
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient client;
        private readonly Label notice, warning;
        private readonly UnityEngine.UIElements.Button acknowledge;
        private readonly ScrollView content;
        private JObject market, indices, portfolio, quote, chart;
        private JArray companies = new JArray(), headlines = new JArray();
        private string section = "Markets", ticker = "", sector = "All sectors", query = "", action = "buy", quantity = "1", funding = "Cash", confirmation = "", snapshot = "";
        private int range = 24;
        private decimal previewAmount;
        private long acknowledgedVersion = -1, observedVersion = -1;
        private bool loaded;

        internal ToolkitExchange(TransactionClient client)
        {
            this.client = client;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 180; Window.style.top = 32; Window.style.width = 1100; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("Stal-Ex"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; confirmation = ""; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; notice.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(notice);
            warning = Text(""); warning.style.backgroundColor = Color.white; warning.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(warning);
            acknowledge = Button("I checked or understand the risk", delegate { acknowledgedVersion = client.PendingVersion; UpdateWarning(); }); Window.Add(acknowledge);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            UpdateWarning(); Window.schedule.Execute(Poll).Every(5000);
        }

        internal void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(true); }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; return row; }
        private static Label Text(string text) { Label label = new Label(text); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string text, Action action) { UnityEngine.UIElements.Button b = new UnityEngine.UIElements.Button(action); b.enableRichText = false; b.text = text; return b; }
        private static string Number(JToken value, int decimals) { return value == null || value.Type == JTokenType.Null ? "—" : ((decimal)value).ToString("N" + decimals, CultureInfo.CurrentCulture); }
        private string Currency { get { return market == null ? "" : (string)market["currency"] ?? ""; } }
        private string Money(JToken value) { return Number(value, market != null && (int?)market["scale"] == 1 ? 0 : 2) + " " + Currency; }
        private string Price(JToken value) { return Number(value, 2) + " " + Currency; }

        private void UpdateWarning()
        {
            if (observedVersion != client.PendingVersion) { observedVersion = client.PendingVersion; confirmation = ""; }
            warning.text = client.HasPending ? "An order may have completed. Check your portfolio before placing a different order. Uncertain orders:\n" + client.PendingDetails : "";
            acknowledge.style.display = client.HasPending && acknowledgedVersion != client.PendingVersion ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private JObject Read(string method, JObject args)
        {
            try { return client.Read(method, args) as JObject; }
            catch (Exception e) { notice.text = e.Message; return null; }
        }

        private bool Editing()
        {
            VisualElement focused = Window.panel == null ? null : Window.panel.focusController.focusedElement as VisualElement;
            while (focused != null && focused != Window) { if (focused is TextField) return true; focused = focused.parent; }
            return false;
        }

        private void Poll()
        {
            if (Window.style.display == DisplayStyle.None) return;
            UpdateWarning();
            if (!Editing()) Refresh(false);
        }

        private void Refresh(bool force)
        {
            if (force) notice.text = "";
            JObject next = Read("exchange.status", null);
            if (next == null || !(bool)next["ready"]) { Closed(); return; }
            string nextSnapshot = next.ToString(Formatting.None);
            if (!force && loaded && nextSnapshot == snapshot) return;
            JObject ix = Read("exchange.indices", null);
            JObject list = Read("exchange.list", null);
            JObject port = Read("exchange.portfolio", null);
            JObject news = Read("exchange.news", new JObject(new JProperty("limit", 20)));
            if (ix == null || list == null || port == null || news == null) { Closed(); return; }
            JArray listItems = (JArray)list["items"];
            bool found = false; foreach (JToken item in listItems) if ((string)item["t"] == ticker) found = true;
            if (!found) ticker = listItems.Count == 0 ? "" : (string)listItems[0]["t"];
            JObject nextQuote = null, nextChart = null;
            if (ticker.Length > 0)
            {
                nextQuote = Read("exchange.quote", new JObject(new JProperty("ticker", ticker)));
                nextChart = Read("exchange.chart", new JObject(new JProperty("ticker", ticker), new JProperty("hours", range)));
                if (nextQuote == null || nextChart == null) { Closed(); return; }
            }
            if (nextSnapshot != snapshot) confirmation = "";
            snapshot = nextSnapshot; market = next; indices = ix; companies = listItems; portfolio = port; headlines = (JArray)news["items"]; quote = nextQuote; chart = nextChart; loaded = true;
            Render();
        }

        private void Closed()
        {
            confirmation = ""; loaded = false; snapshot = ""; content.Clear();
            content.Add(Text("The market is closed or unavailable.")); content.Add(Button("Refresh", delegate { Refresh(true); }));
        }

        private void Render()
        {
            content.Clear();
            Label title = Text("Stal-Ex — The Stalburg stock exchange"); title.style.fontSize = 24; content.Add(title);
            content.Add(Text((int)market["day"] < 0 ? "Market opens soon" : "Day " + (int)market["day"] + ", " + ((int)market["hour"]).ToString("00") + ":00   Cash " + Money(market["cash"])));
            VisualElement nav = Row();
            foreach (string item in new[] { "Markets", "Portfolio", "News", "Help" })
            {
                string choice = item; nav.Add(Button(item, delegate { section = choice; confirmation = ""; notice.text = ""; Render(); }));
            }
            nav.Add(Button("Refresh", delegate { confirmation = ""; Refresh(true); })); content.Add(nav);
            if (section == "Markets") Markets();
            else if (section == "Portfolio") Portfolio();
            else if (section == "News") News();
            else Help();
            content.Add(Text("Stal-Ex is the stock exchange of Stalburg. Shares can fall as well as rise, and margin and short positions can lose more than the money put in."));
        }

        private void Markets()
        {
            content.Add(Text("Market average: " + Number(indices["avg24"], 2) + "%    Up: " + Number(indices["up"], 0) + "    Down: " + Number(indices["down"], 0)));
            Movers("Biggest movers", (JArray)indices["movers"]); Movers("Top gainers", (JArray)indices["gainers"]); Movers("Top losers", (JArray)indices["losers"]);
            List<string> sectors = new List<string> { "All sectors" };
            foreach (JToken item in companies) { string name = (string)item["s"]; if (!sectors.Contains(name)) sectors.Add(name); }
            int selected = Math.Max(0, sectors.IndexOf(sector));
            DropdownField sectorsField = new DropdownField("Sector", sectors, selected);
            sectorsField.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { sector = e.newValue; confirmation = ""; Render(); }); content.Add(sectorsField);
            TextField search = new TextField("Search companies"); search.SetValueWithoutNotify(query);
            VisualElement list = new VisualElement();
            search.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { query = e.newValue ?? ""; confirmation = ""; RenderCompanies(list); });
            content.Add(search); content.Add(list); RenderCompanies(list);
            if (quote != null) Detail();
        }

        private void Movers(string heading, JArray items)
        {
            VisualElement group = Row(); group.Add(Text(heading + ": "));
            foreach (JToken item in items)
            {
                string selected = (string)item["t"];
                group.Add(Button(selected + " " + Number(item["c"], 2) + "%", delegate { Select(selected); }));
            }
            content.Add(group);
        }

        private void RenderCompanies(VisualElement list)
        {
            list.Clear(); int shown = 0;
            foreach (JToken item in companies)
            {
                string symbol = (string)item["t"], name = (string)item["n"], category = (string)item["s"];
                if (sector != "All sectors" && sector != category) continue;
                if ((symbol + " " + name + " " + category).IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(Button(symbol + "   " + name + "   " + Price(item["p"]) + "   " + Number(item["c"], 2) + "%", delegate { Select(symbol); })); shown++;
            }
            if (shown == 0) list.Add(Text("No companies match."));
        }

        private void Select(string symbol) { ticker = symbol; confirmation = ""; Refresh(true); }

        private void Detail()
        {
            content.Add(Text((string)quote["n"] + "  " + ticker + " · " + (string)quote["s"] + "   " + Price(quote["p"])));
            content.Add(Text("1 day " + Number(quote["c1"], 2) + "%   3 days " + Number(quote["c3"], 2) + "%   7 days " + Number(quote["c7"], 2) + "%"));
            content.Add(Text("7 day high " + Price(quote["hi7"]) + "   low " + Price(quote["lo7"]) + "   yield " + Number(quote["yield"], 2) + "%"));
            content.Add(Text("Next dividend: day " + Number(quote["nextDividendDay"], 0) + "   Every " + Number(quote["dividendDays"], 0) + " days"));
            VisualElement ranges = Row();
            foreach (int hours in new[] { 24, 72, 168 })
            {
                int value = hours; ranges.Add(Button(hours == 24 ? "1D" : hours == 72 ? "3D" : "7D", delegate { range = value; confirmation = ""; Refresh(true); }));
            }
            content.Add(ranges); DrawChart();
            JArray events = (JArray)quote["events"];
            if (events.Count == 0) content.Add(Text("No market notices."));
            foreach (JToken e in events) content.Add(Text((string)e["target"] + ((bool)e["sector"] ? " (sector)" : "") + ", " + (int)e["hoursLeft"] + " h left"));
            Trade();
        }

        private void DrawChart()
        {
            List<double> values = new List<double>();
            foreach (JToken point in (JArray)chart["points"])
            {
                if (point.Type != JTokenType.Integer && point.Type != JTokenType.Float) continue;
                double value = (double)point; if (!double.IsNaN(value) && !double.IsInfinity(value)) values.Add(value);
            }
            if (values.Count == 0) { content.Add(Text("No price history yet.")); return; }
            double[] prices = values.ToArray(); MarketChartPoint[] points = MarketChart.Points(prices);
            double low = prices[0], high = prices[0];
            foreach (double value in prices) { low = Math.Min(low, value); high = Math.Max(high, value); }
            VisualElement graph = new VisualElement(); graph.style.width = 600; graph.style.height = 240;
            graph.style.backgroundColor = Color.white;
            for (int i = 0; i <= 4; i++)
            {
                Label axis = Text((high - (high - low) * i / 4).ToString("N2", CultureInfo.CurrentCulture));
                axis.pickingMode = PickingMode.Ignore; axis.style.position = UnityEngine.UIElements.Position.Absolute;
                axis.style.left = 0; axis.style.top = 2 + 51 * i; axis.style.width = 50; axis.style.unityTextAlign = TextAnchor.MiddleRight;
                graph.Add(axis);
            }
            string[] timeLabels = { (prices.Length - 1) + " h ago", ((prices.Length - 1) / 2) + " h ago", "Now" };
            for (int i = 0; i < timeLabels.Length; i++)
            {
                Label axis = Text(timeLabels[i]); axis.pickingMode = PickingMode.Ignore; axis.style.position = UnityEngine.UIElements.Position.Absolute;
                axis.style.left = 56 + 214 * i; axis.style.top = 220; axis.style.width = 100;
                axis.style.unityTextAlign = i == 0 ? TextAnchor.MiddleLeft : i == 1 ? TextAnchor.MiddleCenter : TextAnchor.MiddleRight; graph.Add(axis);
            }
            graph.generateVisualContent += delegate(MeshGenerationContext context)
            {
                const float left = 56f, width = 528f, top = 12f, height = 204f;
                Painter2D painter = context.painter2D; painter.strokeColor = (Color)new Color32(207, 207, 207, 255); painter.lineWidth = 1f;
                for (int i = 0; i <= 4; i++) { float y = top + height * i / 4; painter.BeginPath(); painter.MoveTo(new Vector2(left, y)); painter.LineTo(new Vector2(left + width, y)); painter.Stroke(); }
                painter.strokeColor = prices[prices.Length - 1] >= prices[0] ? (Color)new Color32(31, 135, 82, 255) : (Color)new Color32(190, 45, 45, 255);
                painter.BeginPath(); painter.MoveTo(new Vector2(left + points[0].X * width, top + points[0].Y * height));
                for (int i = 1; i < points.Length; i++) painter.LineTo(new Vector2(left + points[i].X * width, top + points[i].Y * height));
                painter.Stroke();
            };
            Label hover = Text("");
            graph.RegisterCallback<PointerMoveEvent>(delegate(PointerMoveEvent e)
            {
                float ratio = Mathf.Clamp01((e.localPosition.x - 56f) / 528f);
                int at = Mathf.Clamp(Mathf.RoundToInt(ratio * (prices.Length - 1)), 0, prices.Length - 1);
                hover.text = prices[at].ToString("N2", CultureInfo.CurrentCulture) + " " + Currency + ", " + (at == prices.Length - 1 ? "now" : (prices.Length - 1 - at) + " h ago");
            });
            graph.RegisterCallback<PointerLeaveEvent>(delegate { hover.text = ""; });
            content.Add(graph); content.Add(hover);
        }

        private void Trade()
        {
            content.Add(Text("Trade"));
            Label detail = Text("");
            DropdownField side = new DropdownField("Order", new List<string> { "buy", "sell", "short", "cover" }, new List<string> { "buy", "sell", "short", "cover" }.IndexOf(action));
            side.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { action = e.newValue; confirmation = ""; notice.text = ""; Render(); }); content.Add(side);
            TextField shares = new TextField("Shares"); shares.SetValueWithoutNotify(quantity);
            shares.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { quantity = e.newValue ?? ""; confirmation = ""; notice.text = ""; UpdatePreview(detail); }); content.Add(shares);
            VisualElement presets = Row();
            foreach (int amount in new[] { 1, 10, 100, 1000 }) { int value = amount; presets.Add(Button(value.ToString(), delegate { quantity = value.ToString(); confirmation = ""; notice.text = ""; Render(); })); }
            content.Add(presets);
            if (action == "buy")
            {
                DropdownField source = new DropdownField("Funding", new List<string> { "Cash", "Margin loan" }, funding == "Margin loan" ? 1 : 0);
                source.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { funding = e.newValue; confirmation = ""; notice.text = ""; UpdatePreview(detail); }); content.Add(source);
            }
            content.Add(detail); UpdatePreview(detail);
            content.Add(Button("Review / confirm order", ReviewOrder));
            if (client.HasPending) content.Add(Button("Clear matching uncertain order after checking portfolio", ForgetOrder));
        }

        private void ForgetOrder()
        {
            int shares;
            if (acknowledgedVersion != client.PendingVersion) { notice.text = "Check your portfolio and acknowledge the warning first."; return; }
            if (!int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out shares) || shares < 1 || shares > 1000000) { notice.text = "Enter the original whole number of shares."; return; }
            JObject args = new JObject(new JProperty("ticker", ticker), new JProperty("shares", shares), new JProperty("margin", action == "buy" && funding == "Margin loan"));
            if (!client.Forget("exchange." + action, args)) { notice.text = "No uncertain order matches these details."; return; }
            confirmation = ""; UpdateWarning(); Render();
            notice.text = "Uncertain order cleared. A new order will use a new ID and could repeat a completed trade.";
        }

        private static string AmountLabel(string side, bool margin, decimal amount)
        {
            return side == "buy" ? (margin ? "Down payment" : "Cost") : side == "sell" ? "Proceeds" : side == "short" ? "Collateral" : amount < 0m ? "You pay" : "You receive";
        }

        private void UpdatePreview(Label label)
        {
            int shares;
            if (!int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out shares) || shares < 1 || shares > 1000000) { label.text = "Enter 1 to 1,000,000 shares."; return; }
            bool margin = action == "buy" && funding == "Margin loan";
            JObject args = new JObject(new JProperty("action", action), new JProperty("ticker", ticker), new JProperty("shares", shares), new JProperty("margin", margin));
            JObject result = Read("exchange.preview", args);
            if (result == null) { label.text = "Preview unavailable."; return; }
            if (!(bool)result["allowed"]) { label.text = OrderError((string)result["reason"]); return; }
            decimal amount = (decimal)result["amount"];
            label.text = AmountLabel(action, margin, amount) + ": " + Money(new JValue(Math.Abs(amount)));
        }

        private void ReviewOrder()
        {
            UpdateWarning();
            int shares;
            if (!int.TryParse(quantity, NumberStyles.Integer, CultureInfo.InvariantCulture, out shares) || shares < 1 || shares > 1000000) { confirmation = ""; notice.text = "Enter a whole number from 1 to 1,000,000 shares."; return; }
            bool margin = action == "buy" && funding == "Margin loan";
            string method = "exchange." + action;
            JObject args = new JObject(new JProperty("ticker", ticker), new JProperty("shares", shares), new JProperty("margin", margin));
            if (!client.MayStart(method, args, acknowledgedVersion == client.PendingVersion))
            {
                confirmation = ""; notice.text = "Check your portfolio and acknowledge the warning before placing a different order."; return;
            }
            string key = method + args.ToString(Formatting.None);
            bool retry = client.PendingFor(method, args);
            if (retry)
            {
                try
                {
                    JObject lookup = (JObject)args.DeepClone(); lookup["action"] = action;
                    if (client.Resolve(method, args, "exchange.result", lookup))
                    {
                        confirmation = ""; UpdateWarning();
                        try { Refresh(true); notice.text = loaded ? "The previous order completed. Your portfolio has been refreshed." : "The previous order completed. Refresh to see your portfolio."; }
                        catch (Exception) { Closed(); notice.text = "The previous order completed. Refresh to see your portfolio."; }
                        return;
                    }
                    confirmation = ""; notice.text = "The previous order cannot be confirmed. Check your portfolio before placing another order."; return;
                }
                catch (Exception e) { confirmation = ""; notice.text = e.Message + " Check your portfolio before retrying."; return; }
            }
            JObject p = (JObject)args.DeepClone(); p["action"] = action;
            JObject preview = Read("exchange.preview", p);
            if (preview == null || !(bool)preview["allowed"]) { confirmation = ""; if (preview != null) notice.text = OrderError((string)preview["reason"]); return; }
            decimal amount = (decimal)preview["amount"];
            if (confirmation == key && amount != previewAmount) { confirmation = ""; notice.text = "The order value changed. Review it again."; return; }
            previewAmount = amount;
            if (confirmation != key)
            {
                confirmation = key;
                string amountLabel = AmountLabel(action, margin, previewAmount);
                notice.text = "Review: " + action + " " + shares + " " + ticker + (margin ? " with margin" : "") + ". " + amountLabel + ": " + Money(new JValue(Math.Abs(previewAmount))) + ". Press again to confirm.";
                return;
            }
            confirmation = "";
            try { client.Write(method, args); }
            catch (Exception e)
            {
                string error = OrderError(e.Message); acknowledgedVersion = -1; UpdateWarning();
                try { Refresh(true); } catch (Exception) { Closed(); }
                notice.text = error + " Check your portfolio before retrying."; return;
            }
            UpdateWarning();
            try { Refresh(true); notice.text = loaded ? "Order completed." : "Order completed. Refresh to see your portfolio."; }
            catch (Exception) { Closed(); notice.text = "Order completed. Refresh to see your portfolio."; }
        }

        private static string OrderError(string reason)
        {
            switch (reason)
            {
                case "funds": return "You do not have enough money for this order.";
                case "range": return "Enter a whole number of shares from 1 to 1,000,000.";
                case "holding": return "You do not hold enough shares for this order.";
                case "side": return "This order does not fit your position.";
                case "limit": return "This order is too large for the market.";
                case "small": return "This order is too small.";
                case "settle": return "The last trade is still settling. Try again in a moment.";
                case "unknown": return "The market refused this order.";
                default: return string.IsNullOrEmpty(reason) ? "The market refused this order." : reason;
            }
        }

        private void Portfolio()
        {
            content.Add(Text("Cash " + Money(portfolio["cash"]) + "   Holdings " + Money(portfolio["holdings"]) + "   Day result " + Money(portfolio["dayPl"]) + "   Dividends " + Money(portfolio["dividends"])));
            content.Add(Text("Positions"));
            JArray positions = (JArray)portfolio["positions"]; if (positions.Count == 0) content.Add(Text("No positions."));
            foreach (JToken p in positions) content.Add(Text((string)p["t"] + ((bool)p["short"] ? " Short  " : " Long  ") + Number(p["shares"], 0) + " shares  Average " + Price(p["avg"]) + "  Price " + Price(p["price"]) + "  Value " + Money(p["value"]) + "  P/L " + Money(p["pnl"])));
            content.Add(Text("Ledger"));
            JArray ledger = (JArray)portfolio["ledger"]; if (ledger.Count == 0) content.Add(Text("No entries yet."));
            foreach (JToken entry in ledger) content.Add(Text("Day " + Number(entry["d"], 0) + ", " + Number(entry["h"], 0) + ":00  " + (string)entry["kind"] + "  " + (string)entry["t"] + "  " + Number(entry["shares"], 0) + "  " + Money(entry["amount"])));
        }

        private void News()
        {
            if (headlines.Count == 0) content.Add(Text("No news yet."));
            foreach (JToken item in headlines) content.Add(Text("Day " + Number(item["d"], 0) + ", " + Number(item["h"], 0) + ":00  " + (string)item["text"]));
        }

        private void Help()
        {
            content.Add(Text("Market overview: The average change over the last 24 hours, and the number of companies that rose or fell.\nTop gainers and losers: The companies with the biggest changes over the last day.\nSectors: Filter the company list by sector, or choose All sectors.\nMarket notices: Events move a company or sector; each shows its remaining hours.\nOrder types: Buy and Sell trade shares. Short sells shares you do not own; Cover buys them back. Review an order before sending it.\nMargin loans: The initial margin is half the order value. A long position is called below 30% equity and accrues 0.2% interest per day.\nShort selling: Sale proceeds and collateral are held in escrow. A short can be called after an adverse price move.\nDividends: Each company pays on its cycle; the next dividend and cycle appear in its details.\nAccount statements: The Portfolio ledger lists buys, sells, dividends, interest and margin calls.\nAbout the exchange: Stal-Ex is the stock exchange of Stalburg.\nListings: Search or filter the company list to see tickers, prices and changes."));
        }
    }
}
