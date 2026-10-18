using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class FitSlot
    {
        internal int ItemId;
        internal int Space;
    }

    internal sealed class FitUnit
    {
        internal int ItemId;
        internal int Stack;
    }

    internal static class ShopFit
    {
        internal const int EmptySlot = -1;
        internal const int BlockedSlot = -2;

        internal static bool Fits(IList<FitSlot> slots, IList<FitUnit> units)
        {
            List<FitSlot> work = new List<FitSlot>();
            foreach (FitSlot slot in slots) work.Add(new FitSlot { ItemId = slot.ItemId, Space = slot.Space });
            foreach (FitUnit unit in units)
            {
                if (!Place(work, unit)) return false;
            }
            return true;
        }

        private static bool Place(List<FitSlot> slots, FitUnit unit)
        {
            foreach (FitSlot slot in slots)
            {
                if (slot.ItemId == unit.ItemId && slot.Space > 0)
                {
                    slot.Space--;
                    return true;
                }
                if (slot.ItemId == EmptySlot)
                {
                    slot.ItemId = unit.ItemId;
                    slot.Space = unit.Stack - 1;
                    return true;
                }
            }
            return false;
        }
    }
}
