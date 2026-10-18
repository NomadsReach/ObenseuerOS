using System;
using System.Collections.Generic;

namespace ObenseuerOS
{
    internal sealed class ShopCustomGate
    {
        internal readonly Func<bool> Test;
        internal readonly string Hint;
        internal string ModId;

        internal ShopCustomGate(Func<bool> test, string hint)
        {
            Test = test;
            Hint = hint;
        }
    }

    internal static class ShopCustomGates
    {
        private static readonly Dictionary<string, ShopCustomGate> customs = new Dictionary<string, ShopCustomGate>();

        internal static bool Register(string modId, string name, Func<bool> test, string hint)
        {
            if (!ShopCatalog.IsId(name) || test == null) return ShopCatalog.Refuse("gate " + name, "bad name or test");
            if (customs.ContainsKey(name)) return ShopCatalog.Refuse("gate " + name, "duplicate name");
            customs[name] = new ShopCustomGate(test, hint) { ModId = modId };
            return true;
        }

        internal static ShopCustomGate Get(string name)
        {
            ShopCustomGate custom;
            return customs.TryGetValue(name, out custom) ? custom : null;
        }

        internal static void RemoveMod(string modId)
        {
            List<string> owned = new List<string>();
            foreach (KeyValuePair<string, ShopCustomGate> pair in customs)
            {
                if (pair.Value.ModId == modId) owned.Add(pair.Key);
            }
            foreach (string name in owned) customs.Remove(name);
        }
    }
}
