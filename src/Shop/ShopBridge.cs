using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    internal static class ShopBridge
    {
        private const string AppId = "shop";
        internal const int PageSize = 16;
        internal const int MaxCategories = 20;
        internal const int MaxTitle = 40;
        internal const int MaxCategory = 24;
        internal const int MaxShop = 24;
        private const int MaxItemsText = 60;
        internal const int MaxOrders = 30;
        private const int MaxAskedQty = 100000;
        private const int MaxCid = 64;
        internal const int MaxTiers = 10;
        internal const int MaxStores = 24;
        private const int MaxDescription = 400;
        private const int MaxRowDescription = 160;

        internal static void Register()
        {
            Cash.MoneyChanged += args => Notify();
            BankLedger.Added += entry => Notify();
            Os.Handle(AppId, "shop.list", ListItems);
            Os.Handle(AppId, "shop.quote", QuoteCart);
            Os.Handle(AppId, "shop.buy", BuyCart);
            Os.Handle(AppId, "shop.orders", ListOrders);
            Os.Handle(AppId, "shop.dismiss", DismissOrder);
            Os.Handle(AppId, "shop.account", Account);
            Os.Handle(AppId, "shop.stores", Stores);
            Os.Handle(AppId, "shop.item", Product);
        }

        private static string ListItems(string args)
        {
            JObject a = OsBridge.Args(args);
            Begin();
            string q = Lower(OsBridge.Text(a, "q")).Trim();
            string category = OsBridge.Text(a, "category");
            string shop = OsBridge.Text(a, "shop");
            string sort = OsBridge.Text(a, "sort") ?? "name";
            if (sort != "name" && sort != "price_up" && sort != "price_down" && sort != "unlocked") throw new OsBridgeException("bad_args", "sort must be name, price_up, price_down or unlocked");
            bool showLocked = Flag(a, "showLocked");
            int page = OsBridge.Span(a, "page", 100000, 0);
            CoreShopFacts facts = new CoreShopFacts(ShopGates.Current);
            List<Row> matches = new List<Row>();
            foreach (ShopListing listing in ShopCatalog.Listings.ToArray())
            {
                if (!string.IsNullOrEmpty(shop) && listing.ShopId != shop) continue;
                Row row = Make(listing, facts);
                if (row != null && Matches(row, q) && (showLocked || !row.Locked)) matches.Add(row);
            }
            return ListJson(matches, category, sort, page);
        }

        private static string Stores(string args)
        {
            OsBridge.Args(args);
            Begin();
            List<object> stores = new List<object>();
            foreach (ShopPlace place in ShopCatalog.Shops)
            {
                if (stores.Count >= MaxStores) break;
                ShopListing first = null;
                int count = 0;
                foreach (ShopListing listing in ShopCatalog.Listings)
                {
                    if (listing.ShopId != place.Id) continue;
                    if (first == null) first = listing;
                    count++;
                }
                if (first == null) continue;
                stores.Add(new { id = place.Id, name = ShopOrders.Clip(ShopOrders.StripControl(place.Name), MaxShop), area = ShopOrders.Clip(ShopOrders.StripControl(place.Area), MaxShop), count = count, icon = ShopIcons.Url(first.ItemId) });
            }
            return JsonConvert.SerializeObject(new { stores = stores });
        }

        private static string Product(string args)
        {
            JObject a = OsBridge.Args(args);
            string key = Required(a, "id");
            Begin();
            ShopListing listing = ShopCatalog.Listing(key);
            ItemDef item = listing == null ? null : Items.Get(listing.ItemId);
            if (item == null) throw new OsBridgeException("bad_args", "No such product.");
            Row row = Make(listing, new CoreShopFacts(ShopGates.Current));
            ShopPlace place = ShopCatalog.Shop(listing.ShopId);
            return JsonConvert.SerializeObject(new
            {
                id = row.Key,
                title = ShopOrders.Clip(ShopOrders.StripControl(row.Title), MaxTitle),
                icon = ShopIcons.Url(row.ItemId),
                category = ShopOrders.StripControl(row.Category),
                shop = ShopOrders.Clip(ShopOrders.StripControl(row.Shop), MaxShop),
                area = place == null ? null : ShopOrders.Clip(ShopOrders.StripControl(place.Area), MaxShop),
                description = ShopOrders.Clip(ShopOrders.StripControl(item.Description), MaxDescription),
                priceOc = row.Oc > 0 ? (int?)row.Oc : null,
                priceRm = row.Rm > 0 ? (int?)row.Rm : null,
                maxQty = row.MaxQty,
                locked = row.Locked,
                hint = ShopOrders.StripControl(row.Hint)
            });
        }

        internal static string ListJson(List<Row> matches, string category, string sort, int page)
        {
            foreach (Row row in matches)
            {
                row.Category = ShopOrders.StripControl(row.Category);
                row.Hint = ShopOrders.StripControl(row.Hint);
                row.Title = ShopOrders.StripControl(row.Title);
                row.Shop = ShopOrders.StripControl(row.Shop);
            }
            SortedDictionary<string, int[]> counts = new SortedDictionary<string, int[]>(StringComparer.Ordinal);
            foreach (Row row in matches)
            {
                int[] pair;
                if (!counts.TryGetValue(row.Category, out pair))
                {
                    pair = new int[2];
                    counts[row.Category] = pair;
                }
                pair[0]++;
                if (row.Locked) pair[1]++;
            }
            List<string> ranked = new List<string>(counts.Keys);
            ranked.Sort((x, y) => counts[x][0] != counts[y][0] ? counts[y][0].CompareTo(counts[x][0]) : string.CompareOrdinal(x, y));
            if (ranked.Count > MaxCategories) ranked.RemoveRange(MaxCategories, ranked.Count - MaxCategories);
            List<object> categories = new List<object>();
            foreach (KeyValuePair<string, int[]> entry in counts)
            {
                if (ranked.Contains(entry.Key)) categories.Add(new { name = entry.Key, count = entry.Value[0], locked = entry.Value[1] });
            }
            List<Row> shown = new List<Row>();
            foreach (Row row in matches)
            {
                if (!string.IsNullOrEmpty(category) && row.Category != category) continue;
                shown.Add(row);
            }
            shown.Sort((x, y) => Compare(x, y, sort));
            List<object> items = new List<object>();
            for (int i = page * PageSize; i < shown.Count && i < (page + 1) * PageSize; i++) items.Add(RowJson(shown[i]));
            return JsonConvert.SerializeObject(new { categories = categories, items = items, total = shown.Count });
        }

        private static string QuoteCart(string args)
        {
            JObject a = OsBridge.Args(args);
            List<ShopLine> lines = Lines(a);
            string tier = Required(a, "tier");
            string pay = PayOf(a);
            string via = DeliveryOf(a);
            Begin();
            Ready();
            ShopQuote quote = ShopOrders.Build(lines, tier, pay, via, new CoreShopFacts(ShopGates.Current));
            return JsonConvert.SerializeObject(QuoteJson(quote));
        }

        private static string BuyCart(string args)
        {
            JObject a = OsBridge.Args(args);
            string cid = Cid(a);
            List<ShopLine> lines = Lines(a);
            string tier = Required(a, "tier");
            string pay = PayOf(a);
            string via = DeliveryOf(a);
            string print = ShopOrders.Fingerprint(lines, tier, pay, via);
            OrderRec saved = ShopOrders.Saved(ShopGates.Current, cid, print);
            if (saved != null) return Receipt(saved);
            Begin();
            OsBridge.CheckWritable();
            Ready();
            ShopOrders.RetryRefunds();
            ShopQuote quote = ShopOrders.Build(lines, tier, pay, via, new CoreShopFacts(ShopGates.Current));
            if (quote.Tier == null) throw new OsBridgeException("bad_args", "No such delivery tier.");
            if (!quote.Ok) throw new OsBridgeException("denied", quote.Blocker);
            return Receipt(ShopOrders.Place(quote, cid, print));
        }

        internal static void Notify()
        {
            OsView.Deliver(new JObject(new JProperty("v", 1), new JProperty("type", "event"), new JProperty("app", AppId), new JProperty("name", "changed")));
        }

        private static string ListOrders(string args)
        {
            OsBridge.Args(args);
            Begin();
            Ready();
            return OrdersJson(ShopGates.Current.Orders, ShopOrders.Now(), ItemName);
        }

        internal static string OrdersJson(List<OrderRec> orders, int now, Func<int, string> names)
        {
            List<object> rows = new List<object>();
            for (int i = orders.Count - 1; i >= 0 && rows.Count < MaxOrders; i--)
            {
                if (!orders[i].Dismissed) rows.Add(OrderJson(orders[i], now, names));
            }
            return JsonConvert.SerializeObject(new { orders = rows });
        }

        private static string DismissOrder(string args)
        {
            JToken n = OsBridge.Args(args)["n"];
            if (n == null || n.Type != JTokenType.Integer || (long)n < 0 || (long)n > int.MaxValue) throw new OsBridgeException("bad_args", "n must be an order number");
            Begin();
            OsBridge.CheckWritable();
            ShopOrders.Dismiss(ShopGates.Current, (int)(long)n);
            return "{}";
        }

        private static string Account(string args)
        {
            OsBridge.Args(args);
            Begin();
            Ready();
            CoreShopFacts facts = new CoreShopFacts(ShopGates.Current);
            List<object> tiers = TierRows(ShopCatalog.Tiers, tier => tier.Gate != null && !ShopGates.Evaluate(tier.Gate, facts), tier => ShopGates.Hint(tier.Gate, facts));
            return JsonConvert.SerializeObject(new { wallet = Wallet(), accounts = OsBank.AccountRows(Banking.Accounts()), canPayBank = OsBank.LockHint() == null, tiers = tiers });
        }

        internal static List<object> TierRows(List<ShopDelivery> tiers, Func<ShopDelivery, bool> isLocked, Func<ShopDelivery, string> hintOf)
        {
            List<object> rows = new List<object>();
            for (int i = 0; i < tiers.Count && i < MaxTiers; i++)
            {
                ShopDelivery tier = tiers[i];
                bool locked = isLocked(tier);
                rows.Add(new
                {
                    id = tier.Key,
                    label = ShopOrders.StripControl(tier.Label),
                    hours = ShopOrders.EtaHours(ShopOrders.DeliveryMinutes(tier.Hours)),
                    locked = locked,
                    hint = locked ? ShopOrders.Clip(ShopOrders.StripControl(hintOf(tier)), ShopOrders.MaxHint) : null
                });
            }
            return rows;
        }

        private static void Begin()
        {
            ShopCatalog.Ensure();
        }

        private static void Ready()
        {
            if (!Cash.IsAvailable || !Mail.IsAvailable || !WorldClock.IsAvailable) throw new OsBridgeException("failed", "The shop is not ready yet.");
        }

        private static object Wallet()
        {
            return new { oc = (int)Cash.Balance(MoneyCurrency.OC), rm = Math.Round(Cash.Balance(MoneyCurrency.RM), 2) };
        }

        private static string Receipt(OrderRec order)
        {
            return JsonConvert.SerializeObject(new { order = new { n = order.N, name = order.Name, etaHours = ShopOrders.EtaHours(order.DueMin - order.PlacedMin), via = ShopOrders.ViaOf(order), arrives = ShopOrders.ArrivesText(order, ShopOrders.Now()) }, balance = Wallet() });
        }

        private static object QuoteJson(ShopQuote quote)
        {
            return new
            {
                subtotal = new { oc = quote.SubOc, rm = quote.SubRm },
                fee = new { oc = quote.FeeOc, rm = 0 },
                total = new { oc = quote.TotalOc, rm = quote.TotalRm },
                etaHours = quote.Tier == null ? (int?)null : ShopOrders.EtaHours(quote.Minutes),
                ok = quote.Ok,
                problems = quote.Problems
            };
        }

        private static object OrderJson(OrderRec order, int now, Func<int, string> names)
        {
            return new
            {
                n = order.N,
                name = order.Name,
                items = ItemsText(order, names),
                paid = new { oc = order.PaidOc, rm = order.PaidRm },
                state = order.State,
                etaHours = ShopOrders.HoursLeft(order, now),
                via = ShopOrders.ViaOf(order),
                arrives = ShopOrders.ArrivesText(order, now)
            };
        }

        private static string ItemsText(OrderRec order, Func<int, string> names)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < order.ItemIds.Count; i++)
            {
                string name = ShopOrders.StripControl(names(order.ItemIds[i]));
                parts.Add(order.Counts[i] + "x " + (string.IsNullOrWhiteSpace(name) ? "Unknown item" : name));
            }
            return ShopOrders.Clip(string.Join(", ", parts.ToArray()), MaxItemsText);
        }

        private static string ItemName(int id)
        {
            ItemDef item = Items.Get(id);
            return item == null ? null : item.DisplayName;
        }

        private static string Required(JObject a, string key)
        {
            string text = OsBridge.Text(a, key);
            if (string.IsNullOrEmpty(text)) throw new OsBridgeException("bad_args", key + " is required");
            return text;
        }

        private static string Cid(JObject a)
        {
            string cid = OsBridge.Text(a, "cid");
            if (string.IsNullOrEmpty(cid) || cid.Length > MaxCid) throw new OsBridgeException("bad_args", "cid is required and up to 64 characters");
            return cid;
        }

        private static string PayOf(JObject a)
        {
            string pay = Required(a, "pay");
            if (pay != ShopOrders.WalletPay && !pay.StartsWith("bank:", StringComparison.Ordinal)) throw new OsBridgeException("bad_args", "pay must be wallet or bank:<name>");
            return pay;
        }

        private static string DeliveryOf(JObject a)
        {
            string via = OsBridge.Text(a, "via");
            if (via == null) return ShopOrders.ViaParcel;
            if (via != ShopOrders.ViaParcel && via != ShopOrders.ViaMail) throw new OsBridgeException("bad_args", "via must be parcel or mail");
            return via;
        }

        private static bool Flag(JObject a, string key)
        {
            JToken token = a[key];
            if (token == null) return true;
            if (token.Type != JTokenType.Boolean) throw new OsBridgeException("bad_args", key + " must be true or false");
            return (bool)token;
        }

        private static List<ShopLine> Lines(JObject a)
        {
            JArray array = a["lines"] as JArray;
            if (array == null || !ShopOrders.LinesOk(array.Count)) throw new OsBridgeException("bad_args", "a cart needs 1 to 12 lines");
            List<ShopLine> lines = new List<ShopLine>();
            foreach (JToken token in array)
            {
                JObject line = token as JObject;
                string id = line == null ? null : OsBridge.Text(line, "id");
                JToken qty = line == null ? null : line["qty"];
                if (string.IsNullOrEmpty(id) || qty == null || qty.Type != JTokenType.Integer || (long)qty < 1) throw new OsBridgeException("bad_args", "each line needs an id and a quantity of 1 or more");
                lines.Add(new ShopLine { Key = id, Qty = (int)Math.Min((long)qty, MaxAskedQty) });
            }
            return lines;
        }

        internal sealed class Row
        {
            internal string Key;
            internal string Title;
            internal string Category;
            internal string Shop;
            internal string[] Tags;
            internal int Oc;
            internal int Rm;
            internal int MaxQty;
            internal bool Locked;
            internal string Hint;
            internal string Description;
            internal int ItemId;
        }

        private static Row Make(ShopListing listing, IShopFacts facts)
        {
            ItemDef item = Items.Get(listing.ItemId);
            if (item == null) return null;
            Row row = new Row();
            row.Key = listing.Key;
            row.ItemId = listing.ItemId;
            row.Title = item.DisplayName;
            row.Description = ShopOrders.Clip(ShopOrders.StripControl(item.Description), MaxRowDescription);
            row.Category = ShopOrders.Clip(listing.Category, MaxCategory);
            row.Tags = listing.Tags;
            ShopOrders.UnitPrice(listing, item, out row.Oc, out row.Rm);
            row.MaxQty = ShopCatalog.QtyCap(listing.MaxQty, item.MaxStack);
            row.Locked = !ShopGates.Open(ShopGates.Current, listing, facts);
            if (row.Locked) row.Hint = ShopOrders.Clip(listing.Hint ?? ShopGates.Hint(listing.Gate, facts), ShopOrders.MaxHint);
            ShopPlace place = listing.ShopId == null ? null : ShopCatalog.Shop(listing.ShopId);
            row.Shop = place == null ? null : place.Name;
            return row;
        }

        private static object RowJson(Row row)
        {
            return new
            {
                id = row.Key,
                title = ShopOrders.Clip(row.Title, MaxTitle),
                icon = ShopIcons.Url(row.ItemId),
                category = row.Category,
                shop = ShopOrders.Clip(row.Shop, MaxShop),
                description = row.Description,
                priceOc = row.Oc > 0 ? (int?)row.Oc : null,
                priceRm = row.Rm > 0 ? (int?)row.Rm : null,
                maxQty = row.MaxQty,
                locked = row.Locked,
                hint = row.Hint
            };
        }

        private static bool Matches(Row row, string q)
        {
            if (q.Length == 0) return true;
            if (Lower(row.Title).Contains(q) || Lower(row.Category).Contains(q)) return true;
            foreach (string tag in row.Tags)
            {
                if (Lower(tag).Contains(q)) return true;
            }
            return false;
        }

        private static int Compare(Row x, Row y, string sort)
        {
            int first = 0;
            if (sort == "unlocked")
            {
                if (x.Locked != y.Locked) first = x.Locked ? 1 : -1;
            }
            else if (sort == "price_up") first = OcValue(x).CompareTo(OcValue(y));
            else if (sort == "price_down") first = OcValue(y).CompareTo(OcValue(x));
            if (first != 0) return first;
            int byName = string.Compare(x.Title, y.Title, StringComparison.OrdinalIgnoreCase);
            return byName != 0 ? byName : string.CompareOrdinal(x.Key, y.Key);
        }

        private static int OcValue(Row row)
        {
            return row.Oc > 0 ? row.Oc : Cash.RmToOc(row.Rm);
        }

        private static string Lower(string text)
        {
            return (text ?? "").ToLowerInvariant();
        }
    }
}
