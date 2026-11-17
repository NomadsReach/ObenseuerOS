using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ObenseuerOS
{
    internal static partial class ShopBridge
    {
        private const int MaxAskedQty = 100000;
        private const int MaxCid = 64;
        internal static string ResultFor(ShopSave state, string args)
        {
            JObject a = OsBridge.Args(args);
            string cid = Cid(a);
            string print = ShopOrders.Fingerprint(Lines(a), Required(a, "tier"), PayOf(a), DeliveryOf(a));
            return new JObject(new JProperty("found", ShopOrders.Saved(state, cid, print) != null)).ToString(Formatting.None);
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
    }

    internal static partial class ShopOrders
    {
        internal const int MaxLines = 12;
        internal const string WalletPay = "wallet";
        internal const string ViaParcel = "parcel";
        internal const string ViaMail = "mail";
        internal const string ConflictText = "This order was already placed with different contents.";
        internal static bool LinesOk(int count) { return count >= 1 && count <= MaxLines; }

        internal static string Fingerprint(List<ShopLine> lines, string tier, string pay, string via)
        {
            List<string> parts = new List<string>();
            foreach (ShopLine line in lines) parts.Add(JsonConvert.SerializeObject(new object[] { line.Key, line.Qty }));
            parts.Sort((a, b) => string.CompareOrdinal(a, b));
            return JsonConvert.SerializeObject(new { lines = parts, tier = tier, pay = pay, via = via });
        }

        internal static OrderRec Saved(ShopSave state, string cid, string print)
        {
            foreach (OrderRec order in state.Orders)
            {
                if (order.Cid != cid) continue;
                if (order.Print != print) throw new OsBridgeException("bad_args", ConflictText);
                return order;
            }
            return null;
        }
    }
}
