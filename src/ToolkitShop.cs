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
    internal sealed class ToolkitShop
    {
        internal readonly VisualElement Window;
        private readonly TransactionClient client;
        private readonly ScrollView content;
        private readonly Label notice, warning;
        private readonly UnityEngine.UIElements.Button acknowledge;
        private readonly List<JObject> cart = new List<JObject>();
        private JObject account, listing, detail;
        private JArray stores = new JArray(), orders = new JArray();
        private string view = "stores", store = "", category = "", query = "", sort = "name", tier = "", pay = "wallet", via = "parcel", confirmation = "", forgetConfirmation = "";
        private bool showLocked = true;
        private int page;
        private long acknowledgedVersion = -1;

        internal ToolkitShop(TransactionClient client)
        {
            this.client = client;
            Window = new VisualElement(); Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 180; Window.style.top = 32; Window.style.width = 1100; Window.style.height = 700;
            Window.style.paddingLeft = 6; Window.style.paddingRight = 6; Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217); Window.style.display = DisplayStyle.None;
            VisualElement bar = Row(); bar.style.height = 36;
            Label title = Text("Shop"); title.style.flexGrow = 1; title.style.unityTextAlign = TextAnchor.MiddleLeft; bar.Add(title);
            UnityEngine.UIElements.Button close = Button("×", delegate { Window.style.display = DisplayStyle.None; confirmation = ""; });
            close.style.width = 46; close.style.height = 20; close.style.marginTop = 8; close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255); close.style.color = Color.white;
            bar.Add(close); Window.Add(bar); ToolkitUi.MakeDraggable(Window, bar);
            notice = Text(""); notice.style.backgroundColor = Color.white; Window.Add(notice);
            warning = Text(""); warning.style.backgroundColor = Color.white; warning.style.color = (Color)new Color32(160, 30, 30, 255); Window.Add(warning);
            acknowledge = Button("I checked my order history and understand the risk", delegate { acknowledgedVersion = client.PendingVersion; UpdateWarning(); }); Window.Add(acknowledge);
            content = new ScrollView(); content.style.flexGrow = 1; content.style.backgroundColor = Color.white; Window.Add(content);
            UpdateWarning();
        }

        internal void Open() { Window.style.display = DisplayStyle.Flex; Window.BringToFront(); Refresh(); }
        private static VisualElement Row() { VisualElement row = new VisualElement(); row.style.flexDirection = FlexDirection.Row; row.style.flexWrap = Wrap.Wrap; return row; }
        private static Label Text(string value) { Label label = new Label(value); label.enableRichText = false; label.style.color = Color.black; label.style.whiteSpace = WhiteSpace.Normal; return label; }
        private static UnityEngine.UIElements.Button Button(string value, Action action) { UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action); button.enableRichText = false; button.text = value; return button; }
        private static string Money(JToken money) { return money == null ? "—" : string.Format(CultureInfo.CurrentCulture, "{0:N0} OC / {1:N2} RM", (decimal)money["oc"], (decimal)money["rm"]); }
        private static string ProductPrice(JToken item) { return (item["priceOc"].Type == JTokenType.Null ? "" : item["priceOc"] + " OC ") + (item["priceRm"].Type == JTokenType.Null ? "" : item["priceRm"] + " RM"); }
        private void UpdateWarning()
        {
            warning.text = client.HasPending ? "A purchase may have completed. Check order history before placing another order. Uncertain orders:\n" + client.PendingDetails : "";
            acknowledge.style.display = client.HasPending && acknowledgedVersion != client.PendingVersion ? DisplayStyle.Flex : DisplayStyle.None;
        }
        private JObject Read(string method, JObject args)
        {
            try { return client.Read(method, args) as JObject; }
            catch (Exception e) { notice.text = e.Message; return null; }
        }
        private void Refresh()
        {
            account = Read("shop.account", null);
            JObject places = Read("shop.stores", null);
            if (account == null || places == null) { content.Clear(); content.Add(Text("Shop is unavailable.")); content.Add(Button("Refresh", Refresh)); return; }
            stores = (JArray)places["stores"];
            if (string.IsNullOrEmpty(tier)) foreach (JToken item in (JArray)account["tiers"]) if (!(bool)item["locked"]) { tier = (string)item["id"]; break; }
            if (view == "products" && !LoadList()) return;
            if (view == "orders" && !LoadOrders()) return;
            Render();
        }
        private bool LoadList()
        {
            listing = Read("shop.list", new JObject(new JProperty("q", query), new JProperty("category", category), new JProperty("shop", store), new JProperty("sort", sort), new JProperty("showLocked", showLocked), new JProperty("page", page)));
            return listing != null;
        }
        private bool LoadOrders() { JObject response = Read("shop.orders", null); if (response == null) return false; orders = (JArray)response["orders"]; return true; }
        private void Products(string selectedStore, string selectedCategory)
        {
            store = selectedStore; category = selectedCategory; page = 0; view = "products"; confirmation = "";
            if (LoadList()) Render();
        }
        private void Render()
        {
            UpdateWarning(); content.Clear();
            VisualElement nav = Row();
            nav.Add(Button("Shop", delegate { view = "stores"; confirmation = ""; Render(); }));
            nav.Add(Button("All products", delegate { Products("", ""); }));
            nav.Add(Button("Cart (" + cart.Count + ")", delegate { view = "cart"; confirmation = ""; Render(); }));
            nav.Add(Button("Order history", delegate { view = "orders"; confirmation = ""; if (LoadOrders()) Render(); }));
            nav.Add(Button("Refresh", Refresh)); content.Add(nav);
            foreach (string pending in client.PendingKeys)
            {
                if (!pending.StartsWith("shop.buy ", StringComparison.Ordinal)) continue;
                string saved = pending.Substring("shop.buy ".Length);
                content.Add(Button("Check uncertain purchase: " + saved, delegate { CheckPending(saved); }));
                content.Add(Button("Clear this uncertain purchase", delegate { Forget(JObject.Parse(saved)); }));
            }
            content.Add(Text("Balance: " + Money(account["wallet"]) + "   Parcels arrive at your parcel locker; mail arrives in your mailbox."));
            if (view == "stores") Stores(); else if (view == "products") Catalog(); else if (view == "detail") Detail(); else if (view == "cart") Cart(); else Orders();
        }
        private void CheckPending(string saved)
        {
            try
            {
                JObject args = JObject.Parse(saved);
                if (!client.Resolve("shop.buy", args, "shop.result", args)) { notice.text = "This order cannot be confirmed. Check order history; it may still have completed."; return; }
                if (Purchase().ToString(Formatting.None) == args.ToString(Formatting.None)) cart.Clear();
                confirmation = ""; UpdateWarning(); Render(); notice.text = "The previous order completed. Check order history for delivery details.";
            }
            catch (Exception e) { notice.text = e.Message; }
        }
        private void Stores()
        {
            content.Add(Text("Choose a store"));
            foreach (JToken item in stores)
            {
                string id = (string)item["id"];
                content.Add(Button((string)item["name"] + " — " + (string)item["area"] + " (" + (int)item["count"] + ")", delegate { Products(id, ""); }));
            }
        }
        private void Catalog()
        {
            VisualElement search = Row(); TextField field = new TextField("Search products"); field.SetValueWithoutNotify(query); search.Add(field);
            search.Add(Button("Search", delegate { query = field.value ?? ""; page = 0; if (LoadList()) Render(); }));
            search.Add(Button("Clear", delegate { query = ""; page = 0; if (LoadList()) Render(); })); content.Add(search);
            List<string> sorts = new List<string> { "name", "price_up", "price_down", "unlocked" };
            DropdownField order = new DropdownField("Sort", sorts, Math.Max(0, sorts.IndexOf(sort)));
            order.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { sort = e.newValue; page = 0; if (LoadList()) Render(); }); content.Add(order);
            Toggle locked = new Toggle("Show locked") { value = showLocked };
            locked.RegisterValueChangedCallback(delegate(ChangeEvent<bool> e) { showLocked = e.newValue; page = 0; if (LoadList()) Render(); }); content.Add(locked);
            VisualElement categories = Row(); categories.Add(Button("All categories", delegate { Products(store, ""); }));
            foreach (JToken item in (JArray)listing["categories"]) { string name = (string)item["name"]; categories.Add(Button(name + " (" + (int)item["count"] + ")", delegate { Products(store, name); })); }
            content.Add(categories); content.Add(Text((int)listing["total"] + " products"));
            foreach (JToken item in (JArray)listing["items"])
            {
                string id = (string)item["id"]; string label = (string)item["title"] + " — " + ProductPrice(item) + ((bool)item["locked"] ? " Locked: " + (string)item["hint"] : "");
                content.Add(Button(label, delegate { JObject product = Read("shop.item", new JObject(new JProperty("id", id))); if (product != null) { detail = product; view = "detail"; Render(); } }));
            }
            VisualElement pages = Row(); pages.Add(Button("Previous", delegate { if (page > 0) { page--; if (LoadList()) Render(); } }));
            pages.Add(Text("Page " + (page + 1))); pages.Add(Button("Next", delegate { if ((page + 1) * 16 < (int)listing["total"]) { page++; if (LoadList()) Render(); } })); content.Add(pages);
        }
        private void Detail()
        {
            content.Add(Button("Back", delegate { view = "products"; if (LoadList()) Render(); }));
            content.Add(Text((string)detail["title"] + " — " + ProductPrice(detail)));
            content.Add(Text((string)detail["shop"] + " / " + (string)detail["category"] + " / " + (string)detail["area"]));
            content.Add(Text((string)detail["description"] ?? ""));
            if ((bool)detail["locked"]) { content.Add(Text("Locked: " + (string)detail["hint"])); return; }
            content.Add(Button("Add to cart", delegate { Add(detail); }));
        }
        private void Add(JObject item)
        {
            string id = (string)item["id"];
            foreach (JObject line in cart) if ((string)line["id"] == id) { line["qty"] = Math.Min((int)item["maxQty"], (int)line["qty"] + 1); confirmation = ""; notice.text = "Added to cart."; return; }
            if (cart.Count >= 12) { notice.text = "A cart can contain up to 12 products."; return; }
            JObject copy = (JObject)item.DeepClone(); copy["qty"] = 1; cart.Add(copy); confirmation = ""; notice.text = "Added to cart.";
        }
        private JObject Purchase()
        {
            JArray lines = new JArray(); foreach (JObject item in cart) lines.Add(new JObject(new JProperty("id", (string)item["id"]), new JProperty("qty", (int)item["qty"])));
            return new JObject(new JProperty("lines", lines), new JProperty("tier", tier), new JProperty("pay", pay), new JProperty("via", via));
        }
        private void Cart()
        {
            content.Add(Text("Your cart"));
            if (cart.Count == 0) { content.Add(Text("Your cart is empty.")); return; }
            foreach (JObject item in cart.ToArray())
            {
                JObject line = item; VisualElement row = Row(); row.Add(Text((string)line["title"] + " — " + ProductPrice(line) + "  Quantity: " + (int)line["qty"]));
                row.Add(Button("−", delegate { line["qty"] = Math.Max(1, (int)line["qty"] - 1); confirmation = ""; Render(); }));
                row.Add(Button("+", delegate { line["qty"] = Math.Min((int)line["maxQty"], (int)line["qty"] + 1); confirmation = ""; Render(); }));
                row.Add(Button("Remove", delegate { cart.Remove(line); confirmation = ""; Render(); })); content.Add(row);
            }
            List<string> tiers = new List<string>(); foreach (JToken item in (JArray)account["tiers"]) if (!(bool)item["locked"]) tiers.Add((string)item["id"]);
            if (tiers.Count == 0) { content.Add(Text("No delivery speed is available.")); return; }
            if (!tiers.Contains(tier)) tier = tiers[0];
            DropdownField speed = new DropdownField("Delivery speed", tiers, tiers.IndexOf(tier)); speed.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { tier = e.newValue; confirmation = ""; Render(); }); content.Add(speed);
            foreach (JToken item in (JArray)account["tiers"]) content.Add(Text((string)item["label"] + " — " + (int)item["hours"] + " hours" + ((bool)item["locked"] ? " (locked: " + (string)item["hint"] + ")" : "")));
            List<string> delivery = new List<string> { "parcel", "mail" }; DropdownField destination = new DropdownField("Deliver by", delivery, delivery.IndexOf(via)); destination.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { via = e.newValue; confirmation = ""; Render(); }); content.Add(destination);
            List<string> payments = new List<string> { "wallet" }; if ((bool)account["canPayBank"]) foreach (JToken item in (JArray)account["accounts"]) if (!(bool)item["frozen"]) payments.Add("bank:" + (string)item["bank"]);
            if (!payments.Contains(pay)) pay = "wallet";
            DropdownField payment = new DropdownField("Pay with", payments, payments.IndexOf(pay)); payment.RegisterValueChangedCallback(delegate(ChangeEvent<string> e) { pay = e.newValue; confirmation = ""; Render(); }); content.Add(payment);
            JObject quote = Read("shop.quote", Purchase());
            if (quote == null) return;
            content.Add(Text("Subtotal: " + Money(quote["subtotal"]) + "   Fee: " + Money(quote["fee"]) + "   Total: " + Money(quote["total"])));
            content.Add(Text("Estimated delivery: " + (string)quote["etaHours"] + " hours"));
            foreach (JToken problem in (JArray)quote["problems"]) content.Add(Text((string)problem));
            content.Add(Button("Review / place order", Place));
            if (client.HasPending) content.Add(Button("Clear matching uncertain order after checking history", delegate { Forget(Purchase()); }));
        }
        private void Place()
        {
            JObject args = Purchase(); string key = args.ToString(Formatting.None);
            if (!client.MayStart("shop.buy", args, acknowledgedVersion == client.PendingVersion)) { notice.text = "Check order history and acknowledge the warning first."; confirmation = ""; return; }
            if (client.PendingFor("shop.buy", args))
            {
                bool found;
                try
                {
                    found = client.Resolve("shop.buy", args, "shop.result", args);
                }
                catch (Exception e) { confirmation = ""; notice.text = e.Message; return; }
                confirmation = "";
                if (!found) { notice.text = "The previous order cannot be confirmed. Check order history before placing another order."; return; }
                cart.Clear(); UpdateWarning();
                try { Refresh(); notice.text = "The previous order completed. Check order history."; }
                catch (Exception) { notice.text = "The previous order completed. Refresh to see order history."; }
                return;
            }
            JObject quote = Read("shop.quote", args); if (quote == null || !(bool)quote["ok"]) { confirmation = ""; notice.text = "This order is not currently available. Check the quote."; return; }
            string fingerprint = key + quote["total"].ToString(Formatting.None) + quote["etaHours"].ToString(Formatting.None);
            if (confirmation != fingerprint) { confirmation = fingerprint; notice.text = "Review total " + Money(quote["total"]) + ", delivery " + quote["etaHours"] + " hours. Press again to place the order."; return; }
            confirmation = "";
            try { client.Write("shop.buy", args, delegate(JToken result) { return result is JObject && result["order"] is JObject && result["order"]["name"] != null && result["order"]["name"].Type == JTokenType.String; }); }
            catch (Exception e) { acknowledgedVersion = -1; UpdateWarning(); Render(); notice.text = e.Message + " Check order history before trying again."; return; }
            cart.Clear(); UpdateWarning();
            try { Refresh(); notice.text = "Order placed. Check order history for delivery details."; }
            catch (Exception) { notice.text = "Order placed. Refresh to see delivery details."; }
        }
        private void Forget(JObject args)
        {
            if (acknowledgedVersion != client.PendingVersion) { notice.text = "Check order history and acknowledge the warning first."; return; }
            if (!client.PendingFor("shop.buy", args)) { forgetConfirmation = ""; notice.text = "No uncertain order matches this cart and delivery."; return; }
            string key = args.ToString(Formatting.None) + client.PendingVersion;
            if (forgetConfirmation != key) { forgetConfirmation = key; notice.text = "Clearing this order allows a new purchase that could duplicate a completed order. Check your history, then press clear again to continue."; return; }
            client.Forget("shop.buy", args); forgetConfirmation = ""; confirmation = ""; UpdateWarning(); Render(); notice.text = "Uncertain order cleared. A new order could repeat a completed purchase.";
        }
        private void Orders()
        {
            content.Add(Text("Order history")); if (orders.Count == 0) content.Add(Text("No orders yet."));
            foreach (JToken item in orders)
            {
                int number = (int)item["n"]; content.Add(Text((string)item["name"] + ": " + (string)item["items"] + " — " + (string)item["state"] + " — " + Money(item["paid"]) + " — " + (string)item["arrives"]));
                if ((string)item["state"] == "arrived") content.Add(Button("Dismiss", delegate { if (Read("shop.dismiss", new JObject(new JProperty("n", number))) != null && LoadOrders()) Render(); }));
            }
        }
    }
}
