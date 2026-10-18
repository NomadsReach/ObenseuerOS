using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using Newtonsoft.Json.Linq;
using ObenseuerCore;

namespace ObenseuerOS
{
    public sealed class ShopListing
    {
        public string Id;
        public string Category;
        public string ShopId;
        public string Hint;
        public int ItemId;
        public int OcPrice;
        public int RmPrice;
        public float Markup;
        public int MaxQty;
        public string[] Tags;
        public ShopGate Gate;
        internal string ModId;

        internal string Key
        {
            get
            {
                return ModId + "/" + Id;
            }
        }
    }

    internal sealed class ShopPlace
    {
        internal string ModId;
        internal string Id;
        internal string Name;
        internal string SceneName;
        internal string Area;
    }

    internal sealed class ShopDelivery
    {
        internal string ModId;
        internal string Key;
        internal string Id;
        internal string Label;
        internal int Hours;
        internal int Flat;
        internal float Pct;
        internal ShopGate Gate;
    }

    internal static class ShopCatalog
    {
        internal const double MinMarkup = 1.2;
        internal const int MaxQtyCap = 1000;
        internal const int MaxKeyBytes = 40;
        internal static readonly List<ShopListing> Listings = new List<ShopListing>();
        internal static readonly List<ShopPlace> Shops = new List<ShopPlace>();
        internal static readonly List<ShopDelivery> Tiers = new List<ShopDelivery>();
        private const string FileName = "os-shop.json";
        private static readonly Regex ValidId = new Regex(@"\A[a-z0-9._-]{1,64}\z");
        private static readonly Regex ValidTitle = new Regex(@"\A[^\p{Cc}\u2028\u2029]{1,40}\z");
        private static readonly string[] KnownListingFields = { "id", "category", "shop", "hint", "item", "maxQty", "tags", "price", "gate" };
        private const int ProbeItemId = 0;
        private static bool loaded;
        private static readonly List<ShopListing> pending = new List<ShopListing>();
        private static CoreLog log;

        internal static CoreLog Log
        {
            get
            {
                if (log == null) log = CoreLog.For(Os.ModId);
                return log;
            }
        }

        internal static bool IsId(string text)
        {
            return text != null && ValidId.IsMatch(text);
        }

        internal static bool ItemsReady
        {
            get
            {
                return Items.Get(ProbeItemId) != null;
            }
        }

        internal static bool IsTitle(string text)
        {
            return text != null && ValidTitle.IsMatch(text);
        }

        internal static bool Refuse(string what, string reason)
        {
            Log.Warn("shop " + what + " refused: " + reason);
            return false;
        }

        internal static ShopPlace Shop(string id)
        {
            foreach (ShopPlace place in Shops)
            {
                if (place.Id == id) return place;
            }
            return null;
        }

        internal static string AreaOf(string sceneName)
        {
            string[] words = sceneName.Split(' ');
            return words.Length > 1 && words[0] == "Interior" ? words[1] : sceneName;
        }

        internal static ShopListing Listing(string key)
        {
            foreach (ShopListing listing in Listings)
            {
                if (listing.Key == key) return listing;
            }
            return null;
        }

        internal static ShopListing Pending(string key)
        {
            foreach (ShopListing listing in pending)
            {
                if (listing.Key == key) return listing;
            }
            return null;
        }

        internal static void Queue(ShopListing listing)
        {
            pending.Add(listing);
        }

        internal static bool AddPlace(string modId, string shopId, string name, string sceneName)
        {
            if (!IsId(shopId)) return Refuse("place " + shopId, "bad id");
            if (Shop(shopId) != null) return Refuse("place " + shopId, "duplicate id");
            if (!IsTitle(name) || string.IsNullOrEmpty(sceneName)) return Refuse("place " + shopId, "bad name or scene");
            Shops.Add(new ShopPlace { ModId = modId, Id = shopId, Name = name, SceneName = sceneName, Area = AreaOf(sceneName) });
            return true;
        }

        internal static bool AddListing(string modId, ShopListing listing)
        {
            if (listing == null) return Refuse("listing " + modId, "no listing");
            string what = "listing " + modId + "/" + listing.Id;
            if (!IsId(listing.Id)) return Refuse(what, "bad id");
            if (Encoding.UTF8.GetByteCount(modId + "/" + listing.Id) > MaxKeyBytes) return Refuse(what, "key longer than 40 bytes");
            if (Listing(modId + "/" + listing.Id) != null || Pending(modId + "/" + listing.Id) != null) return Refuse(what, "duplicate id");
            int priceRules = (listing.OcPrice > 0 ? 1 : 0) + (listing.RmPrice > 0 ? 1 : 0) + (listing.Markup > 0 ? 1 : 0);
            if (priceRules != 1 || listing.OcPrice < 0 || listing.RmPrice < 0 || listing.Markup < 0 || float.IsNaN(listing.Markup) || float.IsInfinity(listing.Markup)) return Refuse(what, "price needs exactly one of oc, rm or auto");
            if (listing.MaxQty < 1) return Refuse(what, "maxQty below 1");
            if (!string.IsNullOrEmpty(listing.ShopId) && Shop(listing.ShopId) == null) return Refuse(what, "unknown shop " + listing.ShopId);
            if (listing.Category == null) listing.Category = "";
            if (listing.Tags == null) listing.Tags = new string[0];
            listing.ModId = modId;
            if (!ItemsReady)
            {
                Queue(listing);
                return true;
            }
            string problem = ItemProblem(listing.ItemId);
            if (problem != null) return Refuse(what, problem);
            Listings.Add(listing);
            return true;
        }

        internal static bool AddTier(string modId, string id, string label, int hours, int flat, float pct, ShopGate gate)
        {
            string what = "delivery " + modId + "/" + id;
            if (!IsId(id)) return Refuse(what, "bad id");
            if (Tier(modId + "/" + id) != null) return Refuse(what, "duplicate id");
            if (!IsTitle(label)) return Refuse(what, "bad label");
            if (hours < 1 || hours > 720) return Refuse(what, "hours outside 1 to 720");
            if (flat < 0) return Refuse(what, "negative flat fee");
            if (flat > ShopOrders.MaxTotal) return Refuse(what, "flat fee over the order limit");
            if (!(pct >= 0 && pct <= 1)) return Refuse(what, "pct outside 0 to 1");
            Tiers.Add(new ShopDelivery { ModId = modId, Key = modId + "/" + id, Id = id, Label = label, Hours = hours, Flat = flat, Pct = pct, Gate = gate });
            return true;
        }

        internal static void ClearMod(string modId)
        {
            Listings.RemoveAll(x => x.ModId == modId);
            pending.RemoveAll(x => x.ModId == modId);
            Shops.RemoveAll(x => x.ModId == modId);
            Tiers.RemoveAll(x => x.ModId == modId);
        }

        internal static string ItemProblem(int itemId)
        {
            ItemDef item = Items.Get(itemId);
            return item == null ? "unknown item " + itemId : Exclusion(itemId, item.Categories, item.Perishable, item.LiquidContainer);
        }

        internal static ShopDelivery Tier(string key)
        {
            foreach (ShopDelivery tier in Tiers)
            {
                if (tier.Key == key) return tier;
            }
            return null;
        }

        internal static int AutoOc(int itemPrice, float markup)
        {
            double factor = Math.Max(markup, MinMarkup);
            return (int)Math.Min(int.MaxValue, Math.Max(1.0, Math.Round(itemPrice * factor, MidpointRounding.AwayFromZero)));
        }

        internal static int QtyCap(int maxQty, int maxStack)
        {
            return Math.Min(maxQty, Math.Min(Math.Max(maxStack, 1) * 5, MaxQtyCap));
        }

        internal static string Exclusion(int itemId, string[] categories, bool perishable, bool liquid)
        {
            if (itemId <= 1) return "not a sellable item";
            if (categories != null && Array.IndexOf(categories, "Keycard") >= 0) return "keycard";
            if (perishable) return "perishable";
            if (liquid) return "liquid container";
            return null;
        }

        internal static void Ensure()
        {
            if (!ItemsReady) return;
            ProcessPending();
            if (loaded) return;
            loaded = true;
            try
            {
                LoadFile(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "web", "apps", "shop", FileName));
                if (Directory.Exists(Paths.PluginPath))
                {
                    foreach (string dir in Directory.GetDirectories(Paths.PluginPath)) LoadFile(Path.Combine(dir, FileName));
                }
                string folder = Path.Combine(Paths.ConfigPath, "ObenseuerOS", "shop");
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder, "*.json")) LoadFile(file);
                }
            }
            catch (Exception e)
            {
                Log.Error("shop catalogue folders could not be read: " + e);
            }
        }

        private static void ProcessPending()
        {
            ShopListing[] queued = pending.ToArray();
            pending.Clear();
            foreach (ShopListing listing in queued)
            {
                string problem = ItemProblem(listing.ItemId);
                if (problem != null) Refuse("listing " + listing.Key, problem);
                else Listings.Add(listing);
            }
        }

        private static void LoadFile(string path)
        {
            if (!File.Exists(path)) return;
            try
            {
                JObject root = JObject.Parse(File.ReadAllText(path));
                string modId = Text(root["modGuid"]);
                if (string.IsNullOrEmpty(modId) || Int(root["format"]) != 1)
                {
                    Log.Error("shop file " + path + " needs format 1 and a modGuid");
                    return;
                }
                Each(path, root["shops"], shop => AddPlace(modId, Text(shop["id"]), Text(shop["name"]), Text(shop["scene"])));
                Each(path, root["delivery"], tier => AddTier(modId, Text(tier["id"]), Text(tier["label"]), Int(tier["hours"]), Int(tier["flat"]), (float)Num(tier["pct"]), ShopGates.ParseOptional(tier["gate"])));
                Each(path, root["listings"], listing => AddListingFrom(modId, listing));
            }
            catch (Exception e)
            {
                Log.Error("shop file " + path + " could not be read: " + e.Message);
            }
        }

        private static void Each(string path, JToken array, Action<JObject> add)
        {
            JArray items = array as JArray;
            if (items == null) return;
            foreach (JToken token in items)
            {
                JObject entry = token as JObject;
                if (entry == null) continue;
                try
                {
                    add(entry);
                }
                catch (Exception e)
                {
                    Log.Error("shop entry in " + path + " skipped: " + e.Message);
                }
            }
        }

        private static void AddListingFrom(string modId, JObject o)
        {
            foreach (KeyValuePair<string, JToken> field in o)
            {
                if (Array.IndexOf(KnownListingFields, field.Key) < 0) Log.Warn("shop listing field ignored: " + field.Key);
            }
            ShopListing listing = new ShopListing();
            listing.Id = Text(o["id"]);
            listing.Category = Text(o["category"]);
            listing.ShopId = Text(o["shop"]);
            listing.Hint = Text(o["hint"]);
            listing.ItemId = ItemOf(o["item"]);
            listing.MaxQty = Int(o["maxQty"]);
            listing.Tags = Strings(o["tags"]);
            JObject price = o["price"] as JObject;
            if (price != null)
            {
                listing.OcPrice = Int(price["oc"]);
                listing.RmPrice = Int(price["rm"]);
                listing.Markup = (float)Num(price["auto"]);
            }
            listing.Gate = ShopGates.ParseOptional(o["gate"]);
            AddListing(modId, listing);
        }

        private static int ItemOf(JToken token)
        {
            if (token == null) return 0;
            if (token.Type == JTokenType.Integer) return (int)token;
            string text = Text(token) ?? "";
            int colon = text.IndexOf(':');
            if (colon <= 0 || colon == text.Length - 1) return 0;
            return ItemRegistry.IdFor(text.Substring(0, colon), text.Substring(colon + 1));
        }

        internal static string Text(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? null : (string)token;
        }

        private static int Int(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? 0 : (int)token;
        }

        private static double Num(JToken token)
        {
            return token == null || token.Type == JTokenType.Null ? 0.0 : (double)token;
        }

        private static string[] Strings(JToken token)
        {
            JArray array = token as JArray;
            if (array == null) return new string[0];
            List<string> result = new List<string>();
            foreach (JToken item in array) result.Add(Text(item));
            return result.ToArray();
        }
    }

    public static class OsShop
    {
        public static event Action<string> ListingUnlocked;

        public static bool AddShop(string modId, string shopId, string name, string sceneName)
        {
            return Os.Accept("AddShop", modId) && ShopCatalog.AddPlace(modId, shopId, name, sceneName);
        }

        public static bool AddListing(string modId, ShopListing listing)
        {
            return Os.Accept("AddListing", modId) && ShopCatalog.AddListing(modId, listing);
        }

        public static bool AddDeliveryTier(string modId, string id, string label, int hours, int flat, float pct, ShopGate gate)
        {
            return Os.Accept("AddDeliveryTier", modId) && ShopCatalog.AddTier(modId, id, label, hours, flat, pct, gate);
        }

        public static bool RegisterGate(string modId, string name, Func<bool> test, string hint)
        {
            return Os.Accept("RegisterGate", modId) && ShopCustomGates.Register(modId, name, test, hint);
        }

        public static bool Remove(string modId, string listingId)
        {
            if (!Os.Accept("Remove", modId)) return false;
            ShopListing listing = ShopCatalog.Listing(modId + "/" + listingId);
            if (listing == null) return false;
            ShopCatalog.Listings.Remove(listing);
            return true;
        }

        internal static void RaiseUnlocked(string key)
        {
            if (ListingUnlocked == null) return;
            foreach (Delegate handler in ListingUnlocked.GetInvocationList())
            {
                try
                {
                    ((Action<string>)handler)(key);
                }
                catch (Exception e)
                {
                    ShopCatalog.Log.Error("listing unlock handler failed: " + e);
                }
            }
        }
    }
}
