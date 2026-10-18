using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static class ShopSelfTest
    {
        internal static string Run()
        {
            FakeFacts facts = new FakeFacts();
            const string quest = "{\"quest\":\"Started6\"}";
            const string active = "{\"quest\":\"Started6\",\"state\":\"active\"}";
            const string visited = "{\"visited\":\"Interior Bazaar Books\"}";
            const string bought = "{\"bought\":\"books\"}";
            if (Opens(quest, facts)) return "unstarted quest opened";
            if (Hint(quest, facts) != "Needs a task you have not found yet.") return "unstarted quest names a task";
            facts.SetQuest("Started6", "Getting started", false, true, new string[0]);
            if (Opens(quest, facts) || !Opens(active, facts)) return "active quest state";
            if (Hint(quest, facts) != "Finish the task 'Getting started'.") return "started quest hint";
            facts.SetQuest("Started6", "Getting started", true, false, new string[0]);
            if (!Opens(quest, facts) || Opens(active, facts)) return "finished quest state";
            facts.SetQuest("Farmer1", "Turnip farmer", false, true, new string[] { "Turnips" });
            if (!Opens("{\"objective\":[\"Farmer1\",\"Turnips\"]}", facts)) return "objective gate";
            if (Opens("{\"objective\":[\"Farmer1\",\"Peppers\"]}", facts)) return "objective gate open early";
            if (Opens(visited, facts) || Hint(visited, facts) != "Visit Bookstore to unlock this.") return "visited before entering";
            facts.Scenes.Add("Interior Bazaar Books");
            if (!Opens(visited, facts)) return "visited gate";
            if (Opens(bought, facts) || Hint(bought, facts) != "Buy something at Bookstore first.") return "bought before buying";
            facts.BoughtShops.Add("books");
            if (!Opens(bought, facts)) return "bought gate";
            if (Opens("{\"flag\":\"k\"}", facts)) return "flag open early";
            facts.Flags.Add("k");
            if (!Opens("{\"flag\":\"k\"}", facts)) return "flag gate";
            if (Opens("{\"custom\":\"self.custom\"}", facts)) return "custom open early";
            facts.CustomOpen = true;
            if (!Opens("{\"custom\":\"self.custom\"}", facts) || Hint("{\"custom\":\"self.custom\"}", facts) != "Custom hint.") return "custom gate";
            if (!Opens("{\"all\":[{\"flag\":\"k\"},{\"visited\":\"Interior Bazaar Books\"}]}", facts)) return "all";
            if (Opens("{\"all\":[{\"flag\":\"k\"},{\"flag\":\"missing\"}]}", facts)) return "all open";
            if (Hint("{\"all\":[{\"visited\":\"x\"},{\"flag\":\"missing\"}]}", facts) != "Visit a place to unlock this.") return "all hint";
            if (!Opens("{\"any\":[{\"flag\":\"missing\"},{\"flag\":\"k\"}]}", facts)) return "any";
            if (Opens("{\"any\":[{\"flag\":\"missing\"}]}", facts)) return "any open";
            if (Hint("{\"any\":[{\"quest\":\"Nope\"},{\"flag\":\"missing\"}]}", facts) != "Needs a task you have not found yet. or Not available yet.") return "any hint";
            if (!Opens("{\"not\":{\"flag\":\"missing\"}}", facts) || Opens("{\"not\":{\"flag\":\"k\"}}", facts)) return "not";
            foreach (string bad in new string[] { "{\"quest\":\"\"}", "{\"nope\":1}", "{\"objective\":[\"a\"]}", "{\"quest\":\"a\",\"state\":\"sideways\"}", "{\"all\":{}}", "[]" })
            {
                if (!ParseFails(bad)) return "accepted bad gate " + bad;
            }
            ShopSave state = new ShopSave();
            ShopListing listing = new ShopListing { Id = "l1", ModId = "self.test", Gate = ShopGate.Flag("k") };
            FakeFacts sticky = new FakeFacts();
            sticky.Flags.Add("k");
            if (!ShopGates.Open(state, listing, sticky)) return "listing not open";
            if (state.Unlocked.Count != 1 || state.Unlocked[0] != "self.test/l1") return "unlock not recorded";
            if (!ShopGates.Open(state, listing, new FakeFacts())) return "unlock not sticky";
            if (ShopGates.Open(new ShopSave(), listing, new FakeFacts())) return "fresh save opened without the gate";
            if (!ShopGates.Open(state, new ShopListing { Id = "free", ModId = "self.test" }, new FakeFacts()) || state.Unlocked.Count != 1) return "open listing recorded";
            if (ShopCatalog.AutoOc(220, 1.5f) != 330) return "auto price";
            if (ShopCatalog.AutoOc(220, 1.0f) != 264) return "auto price floor";
            if (ShopCatalog.AutoOc(0, 1.5f) != 1) return "auto price minimum";
            if (ShopCatalog.AutoOc(int.MaxValue, 2f) != int.MaxValue) return "auto price overflow";
            if (ShopCatalog.QtyCap(20, 8) != 20 || ShopCatalog.QtyCap(100, 8) != 40) return "quantity cap by stack";
            if (ShopCatalog.QtyCap(5000, 1000) != 1000 || ShopCatalog.QtyCap(100, 1) != 5) return "quantity cap limits";
            if (ShopCatalog.Exclusion(1, new string[0], false, false) == null) return "coin not excluded";
            if (ShopCatalog.Exclusion(17340, new string[] { "Keycard" }, false, false) == null) return "keycard not excluded";
            if (ShopCatalog.Exclusion(17340, new string[0], true, false) == null) return "perishable not excluded";
            if (ShopCatalog.Exclusion(17340, new string[0], false, true) == null) return "liquid not excluded";
            if (ShopCatalog.Exclusion(17340, new string[] { "Readable" }, false, false) != null) return "plain item excluded";
            if (!ShopCatalog.IsId("notebook") || !ShopCatalog.IsId("mymod.soup-1")) return "valid id refused";
            if (ShopCatalog.IsId("Bad") || ShopCatalog.IsId("") || ShopCatalog.IsId(null) || ShopCatalog.IsId(new string('a', 65))) return "invalid id accepted";
            if (ShopCatalog.IsTitle("a" + (char)0x2029 + "b") || ShopCatalog.IsTitle("a" + (char)0x2028 + "b") || !ShopCatalog.IsTitle("ab")) return "title line separator accepted";
            return OrderChecks() ?? PageChecks(false) ?? PageChecks(true) ?? OrdersChecks() ?? TierChecks() ?? UnknownItemChecks() ?? VisitChecks() ?? FitChecks();
        }

        private static string OrderChecks()
        {
            if (ShopOrders.Fee(220, 10, 0.05f) != 21 || ShopOrders.Fee(230, 10, 0.05f) != 22 || ShopOrders.Fee(245, 30, 0.10f) != 55) return "fee rounding";
            List<ShopLine> cart = new List<ShopLine>();
            cart.Add(new ShopLine { Key = "a", Qty = 3, Oc = 22 });
            cart.Add(new ShopLine { Key = "b", Qty = 2, Rm = 4 });
            long oc;
            long rm;
            ShopOrders.Sum(cart, out oc, out rm);
            if (oc != 66 || rm != 8) return "quote subtotal";
            ShopQuote fine = new ShopQuote();
            ShopOrders.Totals(fine, 220, 0, 0, new ShopDelivery { Flat = 10, Pct = 0.05f });
            if (!fine.Ok || fine.FeeOc != 21 || fine.TotalOc != 241) return "quote totals";
            ShopQuote flatTier = new ShopQuote();
            ShopOrders.Totals(flatTier, 100, 0, 0, new ShopDelivery { Flat = int.MaxValue, Pct = 0f });
            if (flatTier.Ok) return "huge tier flat accepted";
            List<ShopLine> dear = new List<ShopLine>();
            dear.Add(new ShopLine { Key = "h", Qty = 2, Oc = int.MaxValue });
            long dearOc;
            long dearRm;
            ShopOrders.Sum(dear, out dearOc, out dearRm);
            ShopQuote dearQuote = new ShopQuote();
            ShopOrders.Totals(dearQuote, dearOc, dearRm, 0, null);
            if (dearQuote.Ok) return "huge listing price accepted";
            if (ShopOrders.DeliveryMinutes(24) != 1439 || ShopOrders.DeliveryMinutes(6) != 360) return "delivery clamp";
            if (ShopOrders.EtaHours(1439) != 24 || ShopOrders.EtaHours(360) != 6) return "eta hours";
            if (!ShopOrders.LinesOk(1) || !ShopOrders.LinesOk(12) || ShopOrders.LinesOk(0) || ShopOrders.LinesOk(13)) return "cart line cap";
            if (ShopOrders.ClampQty(5000, 5000, 1000) != 1000 || ShopOrders.ClampQty(50, 20, 8) != 20 || ShopOrders.ClampQty(3, 20, 8) != 3 || ShopOrders.ClampQty(100, 100, 1) != 5) return "quantity clamp";
            if (ShopOrders.OrderName(1) != "Order 0001" || ShopOrders.OrderName(12) != "Order 0012") return "order name";
            ShopSave state = new ShopSave();
            state.Orders.Add(new OrderRec { N = 1, Name = "Order 0001", State = "placed" });
            if (ShopOrders.Arrive(state, "Order 0001") == null || state.Orders[0].State != "arrived") return "arrival";
            if (ShopOrders.Arrive(state, "Order 0001") != null || state.Orders[0].State != "arrived") return "second arrival raise";
            if (ShopOrders.Arrive(state, "Order 0002") != null) return "arrival of unknown order";
            List<ShopLine> reordered = new List<ShopLine>();
            reordered.Add(cart[1]);
            reordered.Add(cart[0]);
            string print = ShopOrders.Fingerprint(cart, "t", "wallet", "parcel");
            if (ShopOrders.Fingerprint(reordered, "t", "wallet", "parcel") != print) return "same lines in another order";
            List<ShopLine> bigger = new List<ShopLine>();
            bigger.Add(new ShopLine { Key = "a", Qty = 4 });
            if (ShopOrders.Fingerprint(bigger, "t", "wallet", "parcel") == print) return "different quantity same print";
            if (ShopOrders.Fingerprint(cart, "t", "wallet", "mail") == print) return "different delivery same print";
            state.Orders.Add(new OrderRec { N = 3, Name = "Order 0003", State = "placed", Cid = "c3", Print = print });
            if (ShopOrders.Saved(state, "c3", print) == null) return "saved cid not found";
            if (ShopOrders.Saved(state, "c4", print) != null) return "unknown cid found";
            if (!Refuses(state, "c3", ShopOrders.Fingerprint(cart, "t", "bank:x", "parcel"))) return "cid with other pay accepted";
            if (!Refuses(state, "c3", ShopOrders.Fingerprint(bigger, "t", "wallet", "parcel"))) return "saved cid with other lines accepted";
            ShopOrders.Arrive(state, "Order 0003");
            ShopOrders.Dismiss(state, 3);
            ShopOrders.Dismiss(state, 3);
            if (ShopOrders.Saved(state, "c3", print) == null) return "dismissed order lost its cid";
            ShopSave pruned = new ShopSave();
            for (int i = 1; i <= 250; i++) pruned.Orders.Add(new OrderRec { N = i, State = i == 1 ? "placed" : "arrived" });
            ShopOrders.Prune(pruned);
            if (pruned.Orders.Count != 200 || pruned.Orders[0].N != 1 || pruned.Orders[0].State != "placed") return "prune kept the wrong orders";
            ShopSave placed = new ShopSave();
            for (int i = 1; i <= 250; i++) placed.Orders.Add(new OrderRec { N = i, State = "placed" });
            ShopOrders.Prune(placed);
            if (placed.Orders.Count != 250) return "placed order pruned";
            if (ShopCatalog.AreaOf("Interior Bazaar Books") != "Bazaar" || ShopCatalog.AreaOf("Interior Tenement Pharmacy") != "Tenement" || ShopCatalog.AreaOf("Test Map") != "Test Map") return "shop area";
            return null;
        }

        internal static string Page(bool controls)
        {
            return ShopBridge.ListJson(Rows(controls), "", "name", 0);
        }

        private static List<ShopBridge.Row> Rows(bool controls)
        {
            List<ShopBridge.Row> rows = new List<ShopBridge.Row>();
            for (int i = 0; i < 24; i++)
            {
                string category = controls ? (char)(0x4E00 + i) + new string((char)0x2029, ShopBridge.MaxCategory - 1) : new string((char)(0x4E00 + i), ShopBridge.MaxCategory);
                for (int j = 0; j <= i; j++)
                {
                    rows.Add(new ShopBridge.Row
                    {
                        Key = new string('k', ShopCatalog.MaxKeyBytes),
                        Title = controls ? new string((char)1, ShopBridge.MaxTitle / 2) + new string((char)0x2029, ShopBridge.MaxTitle - ShopBridge.MaxTitle / 2) : new string((char)0x20AC, ShopBridge.MaxTitle),
                        Category = category,
                        Shop = controls ? new string((char)1, ShopBridge.MaxShop / 2) + new string((char)0x2029, ShopBridge.MaxShop - ShopBridge.MaxShop / 2) : new string((char)0x20AC, ShopBridge.MaxShop),
                        Tags = new string[0],
                        Oc = int.MaxValue,
                        MaxQty = ShopCatalog.MaxQtyCap,
                        Locked = true,
                        Hint = controls ? new string((char)1, ShopOrders.MaxHint / 2) + new string((char)0x2028, ShopOrders.MaxHint / 2) : new string((char)0x20AC, ShopOrders.MaxHint)
                    });
                }
            }
            return rows;
        }

        private const int KeptListings = 290;

        private static string PageChecks(bool controls)
        {
            string json = Page(controls);
            int bytes = Encoding.UTF8.GetByteCount(json);
            if (bytes >= OsBridge.MaxResultBytes) return "a worst-case shop page is " + bytes + " bytes";
            JObject page = JObject.Parse(json);
            if (!Clean(page)) return "a shop page carries control characters";
            JArray kept = (JArray)page["categories"];
            int listings = 0;
            foreach (JToken category in kept) listings += (int)category["count"];
            return kept.Count == ShopBridge.MaxCategories && listings == KeptListings ? null : "the shop page kept the wrong categories";
        }

        internal static string Orders(bool controls)
        {
            List<OrderRec> orders = new List<OrderRec>();
            for (int i = 1; i <= ShopBridge.MaxOrders; i++)
            {
                OrderRec order = new OrderRec { N = i, Name = ShopOrders.OrderName(i), PaidOc = int.MaxValue, PaidRm = 99999.99f, State = ShopOrders.Placed, DueMin = 100000 };
                for (int k = 0; k < ShopOrders.MaxLines; k++)
                {
                    order.ItemIds.Add(k + 1);
                    order.Counts.Add(int.MaxValue);
                }
                orders.Add(order);
            }
            return ShopBridge.OrdersJson(orders, 0, id => controls ? new string((char)1, 100) + new string((char)0x2029, 100) : new string((char)0x20AC, 200));
        }

        private static string OrdersChecks()
        {
            foreach (bool controls in new bool[] { false, true })
            {
                string json = Orders(controls);
                int bytes = Encoding.UTF8.GetByteCount(json);
                if (bytes >= OsBridge.MaxResultBytes) return "30 worst-case orders are " + bytes + " bytes";
                if (!Clean(JObject.Parse(json))) return "worst-case orders carry control characters";
            }
            return null;
        }

        private static string TierChecks()
        {
            List<ShopDelivery> tiers = new List<ShopDelivery>();
            for (int i = 0; i < 12; i++)
            {
                tiers.Add(new ShopDelivery { Key = "m/t" + i, Id = "t" + i, Label = (char)1 + "Label" + (char)0x2029, Hours = 6 });
            }
            List<object> rows = ShopBridge.TierRows(tiers, tier => true, tier => (char)1 + "Hint" + (char)0x2029);
            JObject page = JObject.Parse(JsonConvert.SerializeObject(new { tiers = rows }));
            if (((JArray)page["tiers"]).Count != 10) return "the tier list is not capped at 10";
            return Clean(page) ? null : "tier rows carry control characters";
        }

        private static string UnknownItemChecks()
        {
            OrderRec order = new OrderRec { N = 1, Name = ShopOrders.OrderName(1), State = ShopOrders.Placed };
            order.ItemIds.Add(1);
            order.Counts.Add(1);
            List<OrderRec> orders = new List<OrderRec>();
            orders.Add(order);
            string items = (string)JObject.Parse(ShopBridge.OrdersJson(orders, 0, id => (char)1 + " "))["orders"][0]["items"];
            return items == "1x Unknown item" ? null : "a control-only item name shows as " + items;
        }

        private static string VisitChecks()
        {
            string folder = Path.Combine(Path.GetTempPath(), "obx-shop-visits-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                if (ShopVisits.Levels(folder).Count != 0 || ShopVisits.Levels(null).Count != 0) return "empty save has visits";
                File.WriteAllText(Path.Combine(folder, "Interior Tenement General Store.tnmt"), "{}");
                FakeFacts saved = new FakeFacts();
                foreach (string level in ShopVisits.Levels(folder)) saved.Scenes.Add(level);
                if (!Opens("{\"visited\":\"Interior Tenement General Store\"}", saved)) return "saved level does not unlock";
                if (Opens("{\"visited\":\"Interior Bazaar Books\"}", saved)) return "unsaved level unlocks";
                File.WriteAllText(Path.Combine(folder, "Interior Bazaar Books.tnmt"), "{}");
                FakeFacts later = new FakeFacts();
                foreach (string level in ShopVisits.Levels(folder)) later.Scenes.Add(level);
                if (!Opens("{\"visited\":\"Interior Bazaar Books\"}", later)) return "later save not re-read";
                return null;
            }
            finally
            {
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            }
        }

        private static bool Clean(JToken token)
        {
            JValue value = token as JValue;
            if (value != null) return value.Type != JTokenType.String || !Dirty((string)value);
            foreach (JToken child in token.Children())
            {
                if (!Clean(child)) return false;
            }
            return true;
        }

        private static bool Dirty(string text)
        {
            foreach (char c in text)
            {
                if (char.IsControl(c) || c == (char)0x2028 || c == (char)0x2029) return true;
            }
            return false;
        }

        private static bool Refuses(ShopSave state, string cid, string print)
        {
            try
            {
                ShopOrders.Saved(state, cid, print);
                return false;
            }
            catch (OsBridgeException e)
            {
                return e.Code == "bad_args" && e.Message == "This order was already placed with different contents.";
            }
        }

        private static ShopGate Gate(string json)
        {
            return ShopGates.Parse(JToken.Parse(json));
        }

        private static string FitChecks()
        {
            if (!ShopFit.Fits(Slots(Empty(), Empty()), Units(1, 20, 10))) return "box fit exact";
            if (ShopFit.Fits(Slots(Empty(), Empty()), Units(1, 21, 10))) return "box fit one over";
            if (!ShopFit.Fits(Slots(Room(1, 3), Empty()), Units(1, 13, 10))) return "box fit partial stacks";
            if (ShopFit.Fits(Slots(Room(1, 3), Empty()), Units(1, 14, 10))) return "box fit partial stacks over";
            List<FitUnit> ordered = Units(1, 3, 10);
            ordered.AddRange(Units(2, 1, 10));
            if (ShopFit.Fits(Slots(Empty(), Room(1, 5)), ordered)) return "box fit slot order";
            List<FitUnit> shared = Units(1, 1, 10);
            shared.AddRange(Units(2, 1, 10));
            if (ShopFit.Fits(Slots(Empty()), shared)) return "box fit shared empty slot";
            if (!ShopFit.Fits(Slots(new FitSlot { ItemId = ShopFit.BlockedSlot }, Empty()), Units(1, 1, 10))) return "box fit blocked slot";
            if (ShopFit.Fits(Slots(new FitSlot { ItemId = ShopFit.BlockedSlot }), Units(1, 1, 10))) return "box fit blocked only";
            if (ShopFit.Fits(Slots(Room(2, 9)), Units(1, 1, 10))) return "box fit other item";
            if (!ShopFit.Fits(Slots(Empty()), Units(21, 16, 16))) return "box fit perishable potato stack";
            if (ShopFit.Fits(Slots(Empty()), Units(21, 17, 16))) return "box fit perishable potato one over";
            if (!ShopFit.Fits(Slots(Room(21, 15)), Units(21, 15, 16))) return "box fit perishable potato room";
            if (ShopFit.Fits(Slots(Room(21, 15)), Units(21, 16, 16))) return "box fit perishable potato room over";
            List<FitUnit> boxed = Units(18570, 1, 1);
            boxed.AddRange(Units(1, 20, 10));
            if (ShopFit.Fits(Slots(Empty(), Empty()), boxed)) return "box fit exact grid with box slot";
            boxed = Units(18570, 1, 1);
            boxed.AddRange(Units(1, 10, 10));
            if (!ShopFit.Fits(Slots(Empty(), Empty()), boxed)) return "box fit grid with box slot";
            return null;
        }

        private static List<FitSlot> Slots(params FitSlot[] slots)
        {
            return new List<FitSlot>(slots);
        }

        private static FitSlot Empty()
        {
            return new FitSlot { ItemId = ShopFit.EmptySlot };
        }

        private static FitSlot Room(int itemId, int space)
        {
            return new FitSlot { ItemId = itemId, Space = space };
        }

        private static List<FitUnit> Units(int itemId, int count, int stack)
        {
            List<FitUnit> units = new List<FitUnit>();
            for (int i = 0; i < count; i++) units.Add(new FitUnit { ItemId = itemId, Stack = stack });
            return units;
        }

        private static bool Opens(string json, IShopFacts facts)
        {
            return ShopGates.Evaluate(Gate(json), facts);
        }

        private static string Hint(string json, IShopFacts facts)
        {
            return ShopGates.Hint(Gate(json), facts);
        }

        private static bool ParseFails(string json)
        {
            try
            {
                Gate(json);
                return false;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private sealed class FakeFacts : IShopFacts
        {
            private readonly Dictionary<string, ShopQuest> quests = new Dictionary<string, ShopQuest>();
            internal readonly List<string> Scenes = new List<string>();
            internal readonly List<string> BoughtShops = new List<string>();
            internal readonly List<string> Flags = new List<string>();
            internal bool CustomOpen;

            internal void SetQuest(string id, string title, bool finished, bool active, string[] done)
            {
                quests[id] = new ShopQuest(title, finished, active, done);
            }

            public ShopQuest Quest(string taskId)
            {
                ShopQuest quest;
                return quests.TryGetValue(taskId, out quest) ? quest : null;
            }

            public bool Visited(string sceneName)
            {
                return Scenes.Contains(sceneName);
            }

            public bool Bought(string shopId)
            {
                return BoughtShops.Contains(shopId);
            }

            public bool Flag(string key)
            {
                return Flags.Contains(key);
            }

            public ShopCustomGate Custom(string name)
            {
                return name == "self.custom" ? new ShopCustomGate(() => CustomOpen, "Custom hint.") : null;
            }

            public string ShopName(string shopId)
            {
                return shopId == "books" ? "Bookstore" : null;
            }

            public string ShopNameForScene(string sceneName)
            {
                return sceneName == "Interior Bazaar Books" ? "Bookstore" : null;
            }
        }
    }
}
