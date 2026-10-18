using System;
using System.Collections.Generic;
using ObenseuerCore;
using OS.Items;

namespace ObenseuerOS
{
    internal static class ShopBox
    {
        internal const int ItemId = 18570;
        internal const string LabelPrefix = "Order ";
        private const int Probe = 1000000;
        private static bool registered;
        private static bool expiryGuarded;
        private static string pendingName;

        internal static void Attach()
        {
            if (!expiryGuarded) expiryGuarded = CorePatcher.Prefix(typeof(Collectible), "Expire", new[] { typeof(bool) }, typeof(ShopBox), "ExpirePrefix", Os.ModId);
            if (registered || !Survival.IsAvailable) return;
            Survival.Consumables.Intercept(Os.ModId, OnConsume);
            Survival.Consumables.Consumed += OnConsumed;
            registered = true;
        }

        internal static bool Ready()
        {
            Attach();
            return registered && expiryGuarded && Survival.IsAvailable && Mail.IsAvailable && ItemDatabase.instance != null && MailController.instance != null;
        }

        internal static bool Send(string name, List<ShopLine> lines, int delayMinutes)
        {
            MailController controller = MailController.instance;
            Item box = ItemDatabase.instance == null ? null : ItemDatabase.instance.FetchItemByID(ItemId);
            if (controller == null || box == null) return false;
            object[] meta = box.CloneMeta(fullCopy: true);
            ItemPackageData package = ItemMetaUtilities.GetMetaOfType<ItemPackageData>(meta);
            if (package == null) return false;
            package.capacity = -1;
            package.allowedCategories = new string[0];
            package.forbiddenCategories = new string[0];
            package.valueFactor = 1f;
            package.containedItems = new List<ChildItem>();
            foreach (ShopLine line in lines)
            {
                Item item = ItemDatabase.instance.FetchItemByID(line.ItemId);
                if (item == null) return false;
                for (int i = 0; i < line.Qty; i++) package.containedItems.Add(new ChildItem(item, item.CloneMeta(fullCopy: true)));
            }
            object[] labelled = new object[meta.Length + 1];
            meta.CopyTo(labelled, 0);
            labelled[meta.Length] = new ItemTextData { content = name };
            TimeAndDay wait = new TimeAndDay { hours = delayMinutes / 60, minutes = delayMinutes % 60 };
            TimeAndDay saved = controller.delay;
            controller.delay = wait;
            try
            {
                controller.SendMail(name, new ItemReference(ItemId), 1, labelled, MailController.Target.Player, null, wait);
            }
            finally
            {
                controller.delay = saved;
            }
            return true;
        }

        internal static string LabelOf(ItemData itemData)
        {
            if (itemData == null || itemData.item == null) return null;
            return LabelOf(itemData.item.ID, itemData.meta);
        }

        internal static string LabelOf(int itemId, object[] meta)
        {
            if (itemId != ItemId || meta == null) return null;
            ItemTextData text = ItemMetaUtilities.GetMetaOfType<ItemTextData>(meta);
            if (text == null || text.content == null || !text.content.StartsWith(LabelPrefix, StringComparison.Ordinal)) return null;
            return text.content;
        }

        private static bool ExpirePrefix(Collectible __instance)
        {
            CollectibleItem world = __instance as CollectibleItem;
            if (world == null || world.Item == null || LabelOf(world.Item.ID, world.meta) == null) return true;
            __instance.StartExpirationTimer(-1);
            return false;
        }

        private static void OnConsume(ItemData itemData, ItemConsumable consumable, ConsumeArgs args)
        {
            pendingName = null;
            string name = LabelOf(itemData);
            if (name == null) return;
            bool fits = false;
            try
            {
                fits = Fits(itemData, consumable);
            }
            catch (Exception e)
            {
                ShopCatalog.Log.Error("box " + name + " kept closed, check failed: " + e);
            }
            if (fits)
            {
                pendingName = name;
                return;
            }
            args.Cancel = true;
            ShopCatalog.Log.Info("box " + name + " kept, not enough inventory space");
            Hud.Notify("Not enough inventory space");
        }

        private static void OnConsumed(ConsumedArgs args)
        {
            if (args.ItemId != ItemId || pendingName == null) return;
            string name = pendingName;
            pendingName = null;
            Hud.Notify("Items from " + name + " were added to your inventory.");
        }

        private static bool Fits(ItemData itemData, ItemConsumable consumable)
        {
            ItemPackageData package = ItemMetaUtilities.GetMetaOfType<ItemPackageData>(itemData.meta);
            if (package == null || itemData.owner > 0) return false;
            List<FitUnit> units = new List<FitUnit>();
            foreach (ChildItem child in package.containedItems)
            {
                if (child == null || child.item == null) return false;
                units.Add(UnitOf(child.item.ID));
            }
            if (consumable != null && consumable.ItemsToSpawn != null)
            {
                foreach (ItemReference spawn in consumable.ItemsToSpawn) units.Add(UnitOf(spawn.ID));
            }
            return ShopFit.Fits(Snapshot(units), units);
        }

        internal static bool FitsEmptyGrid(List<ShopLine> lines)
        {
            Item box = ItemDatabase.instance == null ? null : ItemDatabase.instance.FetchItemByID(ItemId);
            Inventory inventory = Inventory.instance;
            if (box == null || inventory == null || inventory.Slots == null) return false;
            List<FitUnit> units = new List<FitUnit> { UnitOf(ItemId) };
            foreach (ShopLine line in lines)
            {
                for (int i = 0; i < line.Qty; i++) units.Add(UnitOf(line.ItemId));
            }
            ItemConsumable consumable = ConsumableOf(box);
            if (consumable != null && consumable.ItemsToSpawn != null)
            {
                foreach (ItemReference spawn in consumable.ItemsToSpawn) units.Add(UnitOf(spawn.ID));
            }
            List<FitSlot> slots = new List<FitSlot>();
            foreach (SlotController slot in inventory.Slots) slots.Add(EmptySlotOf(slot, units));
            return ShopFit.Fits(slots, units);
        }

        private static ItemConsumable ConsumableOf(Item box)
        {
            if (box.Actions == null) return null;
            foreach (Item.ItemAction action in box.Actions)
            {
                ItemConsumable consumable = action as ItemConsumable;
                if (consumable != null) return consumable;
            }
            return null;
        }

        private static List<FitSlot> Snapshot(List<FitUnit> units)
        {
            Inventory inventory = Inventory.instance;
            if (inventory == null || inventory.Slots == null) throw new InvalidOperationException("inventory is not ready");
            List<FitSlot> slots = new List<FitSlot>();
            foreach (SlotController slot in inventory.Slots) slots.Add(SlotOf(slot, units));
            return slots;
        }

        private static FitSlot SlotOf(SlotController slot, List<FitUnit> units)
        {
            if (slot.IsEmpty()) return EmptySlotOf(slot, units);
            FitSlot fit = new FitSlot { ItemId = slot.itemStack.itemId };
            foreach (FitUnit unit in units)
            {
                if (unit.ItemId != slot.itemStack.itemId) continue;
                Item item = ItemOf(unit.ItemId);
                if (!slot.CheckIfAllowed(item)) break;
                int remaining;
                SlotController.SlotHasSpaceForItem(item, slot, slot.itemStack, out remaining, -1, Probe, 0, item.CloneMeta(fullCopy: true));
                fit.Space = Probe - remaining;
                break;
            }
            return fit;
        }

        private static FitSlot EmptySlotOf(SlotController slot, List<FitUnit> units)
        {
            foreach (FitUnit unit in units)
            {
                if (!slot.CheckIfAllowed(ItemOf(unit.ItemId))) return new FitSlot { ItemId = ShopFit.BlockedSlot };
            }
            return new FitSlot { ItemId = ShopFit.EmptySlot };
        }

        private static FitUnit UnitOf(int itemId)
        {
            return new FitUnit { ItemId = itemId, Stack = Math.Max(1, ItemOf(itemId).Stackable) };
        }

        private static Item ItemOf(int itemId)
        {
            Item item = ItemDatabase.instance == null ? null : ItemDatabase.instance.FetchItemByID(itemId);
            if (item == null) throw new InvalidOperationException("item " + itemId + " is not in the database");
            return item;
        }
    }
}
