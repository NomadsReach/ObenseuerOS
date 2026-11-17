using System;
using System.Collections.Generic;

namespace ObenseuerCore
{
    internal enum MoneyCurrency { OC, RM }
    internal static class Cash
    {
        internal static bool Has(MoneyCurrency currency, int amount) { return true; }
        internal static bool TrySpend(MoneyCurrency currency, int amount) { throw new Exception("Unexpected charge"); }
        internal static bool Give(MoneyCurrency currency, int amount) { return true; }
    }
    internal static class Mail { internal static bool IsAvailable { get { return false; } } }
    internal static class Items { internal static object Get(int id) { return null; } }
}
namespace ObenseuerOS
{
    internal sealed class OrderRec { internal string Cid, Print; internal int PaidOc, PlacedMin; }
    internal sealed class ShopSave { internal readonly List<OrderRec> Orders = new List<OrderRec>(); }
    internal sealed class ShopDelivery { internal object Gate; internal int Hours, Flat, Pct; }
    internal sealed class ShopLine { internal int ItemId, Qty; }
    internal sealed class ShopQuote
    {
        internal ShopDelivery Tier; internal string Pay; internal int Minutes, TotalOc; internal bool Ok = true; internal string Blocker;
        internal readonly List<ShopLine> Lines = new List<ShopLine>();
    }
    internal static class LottoGame
    {
        internal const int MinutesPerDay = 1440;
        internal static bool Refund(LottoSave state, int amount) { return true; }
    }
    internal static class ShopGates { internal static ShopSave Current = new ShopSave(); }
    internal static class ShopCatalog { internal static readonly List<ShopDelivery> Tiers = new List<ShopDelivery>(); }
    internal static class ShopOrders
    {
        internal const string WalletPay = "wallet";
        internal static OrderRec Saved(ShopSave state, string cid, string print)
        {
            foreach (OrderRec order in state.Orders) if (order.Cid == cid) { if (order.Print != print) throw new OsBridgeException("bad_args", "conflict"); return order; }
            return null;
        }
        internal static void RetryRefunds() { }
        internal static int DeliveryMinutes(int hours) { return hours * 60; }
        internal static long Fee(int price, int flat, int pct) { return flat; }
        internal static void Totals(ShopQuote quote, int oc, int rm, int ignored, ShopDelivery tier) { quote.TotalOc = oc; }
        internal static OrderRec Place(ShopQuote quote, string cid, string print) { throw new Exception("Unexpected shipment"); }
    }
}
