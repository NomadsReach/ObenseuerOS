using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using ObenseuerCore;

namespace ObenseuerOS
{
    [Serializable]
    public class OrderRec
    {
        public int N;
        public string Name;
        public List<int> ItemIds = new List<int>();
        public List<int> Counts = new List<int>();
        public int PaidOc;
        public float PaidRm;
        public string Pay;
        public string Tier;
        public int PlacedMin;
        public int DueMin;
        public string State;
        public string Cid;
        public string Print;
        public bool Dismissed;
        public string Via;
    }

    [Serializable]
    public class RefundRec
    {
        public string Bank;
        public int Oc;
        public float Rm;
    }

    internal sealed class ShopLine
    {
        internal string Key;
        internal int Qty;
        internal ShopListing Listing;
        internal int ItemId;
        internal int Oc;
        internal int Rm;
    }

    internal sealed class ShopQuote
    {
        internal readonly List<ShopLine> Lines = new List<ShopLine>();
        internal readonly List<string> Problems = new List<string>();
        internal ShopDelivery Tier;
        internal string Pay;
        internal string Via;
        internal int SubOc;
        internal int SubRm;
        internal int FeeOc;
        internal int TotalOc;
        internal int TotalRm;
        internal int Minutes;
        internal bool Ok = true;
        internal string Blocker;

        internal void Block(string text)
        {
            if (Ok) Blocker = text;
            Ok = false;
            Problems.Add(text);
        }

        internal void Note(string text)
        {
            Problems.Add(text);
        }
    }

    internal static class ShopOrders
    {
        internal const int MaxLines = 12;
        internal const int MaxDelayMinutes = 1439;
        internal const int MinutesPerDay = 1440;
        internal const int MaxSaved = 200;
        internal const int MaxHint = 60;
        internal const long MaxTotal = 16000000;
        internal const string Placed = "placed";
        internal const string Arrived = "arrived";
        internal const string WalletPay = "wallet";
        internal const string ViaParcel = "parcel";
        internal const string ViaMail = "mail";
        internal const string StoreName = "Stalburg Shop";
        internal const string BankPrefix = "bank:";
        internal const string ConflictText = "This order was already placed with different contents.";

        internal static void Attach()
        {
            ShopBox.Attach();
            Mail.PackageReceived += OnPackageReceived;
            Mail.Received += OnLetterReceived;
            Events.HourPassed += RetryOnHour;
        }

        internal static string ViaOf(OrderRec order)
        {
            return order.Via == ViaMail ? ViaMail : ViaParcel;
        }

        internal static string DeliveryText(OrderRec order)
        {
            return ViaOf(order) == ViaMail ? "mail, a cardboard box in your mailbox" : "parcel, collected at the parcel locker";
        }

        internal static int ArrivalMinutes(OrderRec order, int now)
        {
            foreach (PendingMail pending in Mail.Pending())
            {
                if (pending.Name == order.Name) return now + (pending.SecondsLeft + 59) / 60;
            }
            return order.DueMin;
        }

        internal static string ArrivesText(OrderRec order, int now)
        {
            if (order.State != Placed) return null;
            int due = ArrivalMinutes(order, now);
            int days = Math.Max(0, due / MinutesPerDay - now / MinutesPerDay);
            string day = days == 0 ? "today" : days == 1 ? "tomorrow" : "in " + days + " days";
            int clock = due % MinutesPerDay;
            return day + " at " + (clock / 60).ToString("00") + ":" + (clock % 60).ToString("00");
        }

        internal static string OrderName(int n)
        {
            return "Order " + n.ToString("0000");
        }

        internal static bool LinesOk(int count)
        {
            return count >= 1 && count <= MaxLines;
        }

        internal static int ClampQty(int qty, int maxQty, int maxStack)
        {
            return Math.Min(qty, ShopCatalog.QtyCap(maxQty, maxStack));
        }

        internal static int DeliveryMinutes(int hours)
        {
            return Math.Min(hours * 60, MaxDelayMinutes);
        }

        internal static int EtaHours(int minutes)
        {
            return (int)Math.Round(minutes / 60.0, MidpointRounding.AwayFromZero);
        }

        internal static long Fee(long equivalentOc, long flat, float pct)
        {
            return flat + (long)Math.Round((decimal)pct * equivalentOc, MidpointRounding.AwayFromZero);
        }

        internal static string Clip(string text, int max)
        {
            if (text == null || text.Length <= max) return text;
            return text.Substring(0, max - 3) + "...";
        }

        internal static string StripControl(string text)
        {
            return text == null ? null : new string(Array.FindAll(text.ToCharArray(), c => !char.IsControl(c) && c != (char)0x2028 && c != (char)0x2029));
        }

        internal static void Sum(List<ShopLine> lines, out long oc, out long rm)
        {
            oc = 0;
            rm = 0;
            foreach (ShopLine line in lines)
            {
                oc += (long)line.Qty * line.Oc;
                rm += (long)line.Qty * line.Rm;
            }
        }

        internal static OrderRec Arrive(ShopSave state, string name)
        {
            foreach (OrderRec order in state.Orders)
            {
                if (order.Name != name || order.State != Placed) continue;
                order.State = Arrived;
                return order;
            }
            return null;
        }

        internal static int HoursLeft(OrderRec order, int now)
        {
            if (order.State != Placed) return 0;
            return (int)Math.Ceiling(Math.Max(0, order.DueMin - now) / 60.0);
        }

        internal static int Now()
        {
            WorldTime now = WorldClock.Now;
            return now.Day * MinutesPerDay + now.Hour * 60 + now.Minute;
        }

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

        internal static void UnitPrice(ShopListing listing, ItemDef item, out int oc, out int rm)
        {
            oc = 0;
            rm = 0;
            if (listing.OcPrice > 0) oc = listing.OcPrice;
            else if (listing.RmPrice > 0) rm = listing.RmPrice;
            else oc = ShopCatalog.AutoOc(item.Price, listing.Markup);
        }

        internal static void Totals(ShopQuote quote, long subOc, long subRm, long rmOc, ShopDelivery tier)
        {
            long fee = tier == null ? 0 : Fee(subOc + rmOc, tier.Flat, tier.Pct);
            long totalOc = subOc + fee;
            if (subOc < 0 || rmOc < 0 || fee < 0 || (totalOc <= 0 && subRm <= 0))
            {
                quote.Block("The order total is not valid.");
            }
            else if (totalOc > MaxTotal || subRm > MaxTotal)
            {
                quote.Block("The order is too large.");
            }
            else
            {
                quote.SubOc = (int)subOc;
                quote.SubRm = (int)subRm;
                quote.FeeOc = (int)fee;
                quote.TotalOc = (int)totalOc;
                quote.TotalRm = (int)subRm;
            }
        }

        internal static ShopQuote Build(List<ShopLine> requested, string tierKey, string pay, string via, IShopFacts facts)
        {
            ShopSave state = ShopGates.Current;
            ShopQuote quote = new ShopQuote();
            quote.Pay = pay;
            quote.Via = via;
            if (via != ViaParcel && via != ViaMail) quote.Block("No such delivery method.");
            else if (via == ViaMail && !ShopBox.Ready()) quote.Block("Mail delivery is not available right now.");
            foreach (ShopLine line in requested) Merge(quote.Lines, line);
            List<ShopLine> priced = new List<ShopLine>();
            foreach (ShopLine line in quote.Lines)
            {
                line.Listing = ShopCatalog.Listing(line.Key);
                if (line.Listing == null)
                {
                    quote.Block("No such item.");
                    continue;
                }
                if (!ShopGates.Open(state, line.Listing, facts))
                {
                    quote.Block(Clip(StripControl(line.Listing.Hint ?? ShopGates.Hint(line.Listing.Gate, facts)), MaxHint));
                    continue;
                }
                ItemDef item = Items.Get(line.Listing.ItemId);
                if (item == null)
                {
                    quote.Block("No such item.");
                    continue;
                }
                line.ItemId = item.Id;
                UnitPrice(line.Listing, item, out line.Oc, out line.Rm);
                if (line.Oc < 1 && line.Rm < 1)
                {
                    quote.Block(StripControl(item.DisplayName) + " has no valid price.");
                    continue;
                }
                int qty = ClampQty(line.Qty, line.Listing.MaxQty, item.MaxStack);
                if (qty < line.Qty) quote.Note(StripControl(item.DisplayName) + " is limited to " + qty + ".");
                line.Qty = qty;
                priced.Add(line);
            }
            long oc;
            long rm;
            Sum(priced, out oc, out rm);
            if (via == ViaMail && !ShopBox.FitsEmptyGrid(priced)) quote.Block("Too many items for one mail box. Choose Parcel for this order.");
            long rmOc = rm <= MaxTotal ? Cash.RmToOc(rm) : 0;
            ShopDelivery tier = ShopCatalog.Tier(tierKey);
            if (tier == null)
            {
                quote.Block("No such delivery tier.");
            }
            else
            {
                quote.Tier = tier;
                quote.Minutes = DeliveryMinutes(tier.Hours);
                if (tier.Gate != null && !ShopGates.Evaluate(tier.Gate, facts)) quote.Block(ShopGates.Hint(tier.Gate, facts));
            }
            Totals(quote, oc, rm, rmOc, tier);
            if (pay != WalletPay) BankFunds(quote, pay);
            else
            {
                if (quote.TotalOc > 0 && !Cash.Has(MoneyCurrency.OC, quote.TotalOc)) quote.Block("Not enough OC for this order.");
                if (quote.TotalRm > 0 && !Cash.Has(MoneyCurrency.RM, quote.TotalRm)) quote.Block("Not enough RM for this order.");
            }
            return quote;
        }

        internal static void RetryRefunds()
        {
            List<RefundRec> refunds = ShopGates.Current.Refunds;
            for (int i = refunds.Count - 1; i >= 0; i--)
            {
                if (TryGive(refunds[i])) refunds.RemoveAt(i);
            }
        }

        internal static OrderRec Place(ShopQuote quote, string cid, string print)
        {
            ShopSave state = ShopGates.Current;
            int now = Now();
            if (quote.Via == ViaMail && !ShopBox.Ready()) throw new OsBridgeException("failed", "Mail delivery is not available right now.");
            string refusal = Charge(quote);
            if (refusal != null) throw new OsBridgeException("failed", refusal);
            OrderRec order = new OrderRec();
            order.N = state.NextOrder + 1;
            order.Name = OrderName(order.N);
            order.Pay = quote.Pay;
            order.Tier = quote.Tier.Key;
            order.Via = quote.Via;
            order.Cid = cid;
            order.Print = print;
            order.PaidOc = quote.TotalOc;
            order.PaidRm = quote.TotalRm;
            List<MailLine> mail = new List<MailLine>();
            foreach (ShopLine line in quote.Lines)
            {
                order.ItemIds.Add(line.ItemId);
                order.Counts.Add(line.Qty);
                MailLine part = new MailLine();
                part.ItemId = line.ItemId;
                part.Count = line.Qty;
                mail.Add(part);
            }
            bool sent;
            try
            {
                sent = quote.Via == ViaMail ? ShopBox.Send(order.Name, quote.Lines, quote.Minutes) : Mail.SendBundle(order.Name, mail, MailKind.Package, quote.Minutes);
            }
            catch (Exception e)
            {
                ShopCatalog.Log.Error("order " + order.Name + " send threw, refunding: " + e);
                sent = false;
            }
            if (!sent)
            {
                string back = RefundText(Refund(BankOf(quote.Pay), quote.TotalOc, quote.TotalRm));
                throw new OsBridgeException("failed", "The order could not be sent. " + back);
            }
            order.PlacedMin = now;
            order.DueMin = now + quote.Minutes;
            order.State = Placed;
            state.NextOrder = order.N;
            state.Orders.Add(order);
            Prune(state);
            try
            {
                Confirm(order, now);
            }
            catch (Exception e)
            {
                ShopCatalog.Log.Error("order " + order.Name + " confirm threw, order kept: " + e);
            }
            return order;
        }

        private static void Confirm(OrderRec order, int now)
        {
            string arrives = ArrivesText(order, now);
            ShopCatalog.Log.Info("order " + order.Name + " placed by " + ViaOf(order) + ": " + ItemList(order) + ", arrives " + arrives);
            if (!OsMail.Send(StoreName, "Receipt " + order.Name, "Order: " + order.Name + "\nItems: " + ItemList(order) + "\nPaid: " + order.PaidOc + " OC" + (order.PaidRm > 0f ? " and " + order.PaidRm.ToString("#,0.00", CultureInfo.InvariantCulture).Replace(',', '\u202F').Replace('.', ',') + " RM" : "") + "\nDelivery: " + DeliveryText(order) + "\nArrives: " + arrives + "."))
            {
                ShopCatalog.Log.Info("order " + order.Name + " receipt not mailed, no Stalburg Mail account or mailbox full");
            }
        }

        private static string ItemList(OrderRec order)
        {
            List<string> parts = new List<string>();
            for (int i = 0; i < order.ItemIds.Count; i++)
            {
                ItemDef item = Items.Get(order.ItemIds[i]);
                string name = item == null ? null : StripControl(item.DisplayName);
                parts.Add(order.Counts[i] + "x " + (string.IsNullOrWhiteSpace(name) ? "Unknown item" : name));
            }
            return string.Join(", ", parts.ToArray());
        }

        internal static void Prune(ShopSave state)
        {
            for (int i = 0; i < state.Orders.Count && state.Orders.Count > MaxSaved; )
            {
                if (state.Orders[i].State == Placed) i++;
                else state.Orders.RemoveAt(i);
            }
        }

        internal static void Dismiss(ShopSave state, int n)
        {
            foreach (OrderRec order in state.Orders)
            {
                if (order.N != n) continue;
                if (order.Dismissed) return;
                if (order.State != Arrived) throw new OsBridgeException("denied", "This order has not arrived yet.");
                order.Dismissed = true;
                return;
            }
            throw new OsBridgeException("bad_args", "There is no order with that number.");
        }

        private static void Merge(List<ShopLine> lines, ShopLine line)
        {
            foreach (ShopLine existing in lines)
            {
                if (existing.Key != line.Key) continue;
                existing.Qty += line.Qty;
                return;
            }
            lines.Add(new ShopLine { Key = line.Key, Qty = line.Qty });
        }

        private static string Charge(ShopQuote quote)
        {
            if (quote.Pay != WalletPay) return ChargeBank(quote);
            if (quote.TotalOc > 0 && !Cash.TrySpend(MoneyCurrency.OC, quote.TotalOc)) return "The payment was refused. Nothing was spent.";
            if (quote.TotalRm > 0 && !Cash.TrySpend(MoneyCurrency.RM, quote.TotalRm))
            {
                if (quote.TotalOc <= 0) return "The payment was refused. Nothing was spent.";
                return "The payment was refused. " + RefundText(Refund(null, quote.TotalOc, 0f));
            }
            return null;
        }

        private static string BankOf(string pay)
        {
            return pay == WalletPay ? null : pay.Substring(BankPrefix.Length);
        }

        private static void BankFunds(ShopQuote quote, string pay)
        {
            string hint = OsBank.LockHint();
            if (hint != null) quote.Block("Bank payment is locked. " + hint);
            AccountSummary account = BankAccountNamed(BankOf(pay));
            if (account == null) quote.Block("You have no account at that bank.");
            else if (account.Frozen) quote.Block("This account is frozen.");
            else if (quote.TotalOc > 0 && account.Oc < quote.TotalOc) quote.Block("Not enough OC in that account for this order.");
            else if (quote.TotalRm > 0 && account.Rm < quote.TotalRm) quote.Block("Not enough RM in that account for this order.");
        }

        private static AccountSummary BankAccountNamed(string bank)
        {
            foreach (AccountSummary account in Banking.Accounts())
            {
                if (account.BankName == bank) return account;
            }
            return null;
        }

        private static string ChargeBank(ShopQuote quote)
        {
            string bank = BankOf(quote.Pay);
            if (quote.TotalOc > 0 && Banking.Charge(bank, MoneyCurrency.OC, quote.TotalOc) != BankResult.Ok) return "The payment was refused. Nothing was spent.";
            if (quote.TotalRm > 0 && Banking.Charge(bank, MoneyCurrency.RM, quote.TotalRm) != BankResult.Ok)
            {
                if (quote.TotalOc <= 0) return "The payment was refused. Nothing was spent.";
                return "The payment was refused. " + RefundText(Refund(bank, quote.TotalOc, 0f));
            }
            return null;
        }

        private static bool Refund(string bank, int oc, float rm)
        {
            RefundRec rec = new RefundRec();
            rec.Bank = bank;
            rec.Oc = oc;
            rec.Rm = rm;
            if (TryGive(rec)) return true;
            ShopGates.Current.Refunds.Add(rec);
            return false;
        }

        private static string RefundText(bool back)
        {
            return back ? "Your money was refunded." : "Your money is being returned.";
        }

        private static bool TryGive(RefundRec rec)
        {
            if (rec.Oc > 0 && GiveBack(rec, MoneyCurrency.OC, rec.Oc)) rec.Oc = 0;
            if (rec.Rm > 0 && GiveBack(rec, MoneyCurrency.RM, rec.Rm)) rec.Rm = 0;
            return rec.Oc <= 0 && rec.Rm <= 0;
        }

        private static bool GiveBack(RefundRec rec, MoneyCurrency currency, float amount)
        {
            if (rec.Bank == null) return Cash.Give(currency, amount);
            BankResult result = Banking.Refund(rec.Bank, currency, amount);
            return result == BankResult.Ok || (result == BankResult.NoAccount && Cash.Give(currency, amount));
        }

        private static void OnPackageReceived(MailReceivedArgs args)
        {
            OnArrival(args.Name);
        }

        private static void OnLetterReceived(MailReceivedArgs args)
        {
            OnArrival(args.Name);
        }

        private static void OnArrival(string name)
        {
            OrderRec order = Arrive(ShopGates.Current, name);
            if (order == null) return;
            ShopBridge.Notify();
            bool box = ViaOf(order) == ViaMail;
            ShopCatalog.Log.Info("order " + order.Name + " arrived by " + ViaOf(order));
            string subject = box ? "Your order is in your mailbox" : "Your parcel is ready";
            string body = box
                ? order.Name + " arrived as a cardboard box in your mailbox. Take the mail at the mailbox, then use the box to put its items in your inventory. The box stays closed until everything fits."
                : order.Name + " is ready. Your parcel is waiting at the parcel locker. Claim it there.";
            if (!OsMail.Send(StoreName, subject, body)) ShopCatalog.Log.Info("order " + order.Name + " arrival not mailed, no Stalburg Mail account or mailbox full");
            Hud.Notify(box ? order.Name + " is in your mailbox." : order.Name + " is ready at the parcel locker.");
        }

        private static void RetryOnHour(int hour)
        {
            RetryRefunds();
        }
    }
}
