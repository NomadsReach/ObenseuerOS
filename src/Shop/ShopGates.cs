using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using ObenseuerCore;
using UnityEngine.SceneManagement;

namespace ObenseuerOS
{
    internal enum ShopGateKind
    {
        Quest,
        QuestActive,
        Objective,
        Visited,
        Bought,
        Flag,
        Custom,
        All,
        Any,
        Not
    }

    public sealed class ShopGate
    {
        internal readonly ShopGateKind Kind;
        internal readonly string Name;
        internal readonly string ObjectiveId;
        internal readonly ShopGate[] Parts;

        private ShopGate(ShopGateKind kind, string name, string objectiveId, ShopGate[] parts)
        {
            Kind = kind;
            Name = name;
            ObjectiveId = objectiveId;
            Parts = parts;
        }

        public static ShopGate Quest(string taskId)
        {
            return new ShopGate(ShopGateKind.Quest, Required(taskId, "taskId"), null, null);
        }

        public static ShopGate QuestActive(string taskId)
        {
            return new ShopGate(ShopGateKind.QuestActive, Required(taskId, "taskId"), null, null);
        }

        public static ShopGate Objective(string taskId, string objectiveId)
        {
            return new ShopGate(ShopGateKind.Objective, Required(taskId, "taskId"), Required(objectiveId, "objectiveId"), null);
        }

        public static ShopGate Visited(string sceneName)
        {
            return new ShopGate(ShopGateKind.Visited, Required(sceneName, "sceneName"), null, null);
        }

        public static ShopGate Bought(string shopId)
        {
            return new ShopGate(ShopGateKind.Bought, Required(shopId, "shopId"), null, null);
        }

        public static ShopGate Flag(string key)
        {
            return new ShopGate(ShopGateKind.Flag, Required(key, "key"), null, null);
        }

        public static ShopGate Custom(string name)
        {
            return new ShopGate(ShopGateKind.Custom, Required(name, "name"), null, null);
        }

        public static ShopGate All(params ShopGate[] gates)
        {
            return new ShopGate(ShopGateKind.All, null, null, Copy(gates));
        }

        public static ShopGate Any(params ShopGate[] gates)
        {
            return new ShopGate(ShopGateKind.Any, null, null, Copy(gates));
        }

        public static ShopGate Not(ShopGate gate)
        {
            return new ShopGate(ShopGateKind.Not, null, null, Copy(new ShopGate[] { gate }));
        }

        private static string Required(string value, string param)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException(param + " must be a non-empty string", param);
            return value;
        }

        private static ShopGate[] Copy(ShopGate[] gates)
        {
            if (gates == null) throw new ArgumentNullException("gates");
            foreach (ShopGate gate in gates)
            {
                if (gate == null) throw new ArgumentException("gates must not contain null", "gates");
            }
            return (ShopGate[])gates.Clone();
        }
    }

    internal interface IShopFacts
    {
        ShopQuest Quest(string taskId);
        bool Visited(string sceneName);
        bool Bought(string shopId);
        bool Flag(string key);
        ShopCustomGate Custom(string name);
        string ShopName(string shopId);
        string ShopNameForScene(string sceneName);
    }

    internal sealed class ShopQuest
    {
        internal readonly string Title;
        internal readonly bool Finished;
        internal readonly bool Active;
        internal readonly string[] DoneObjectives;

        internal ShopQuest(string title, bool finished, bool active, string[] doneObjectives)
        {
            Title = title;
            Finished = finished;
            Active = active;
            DoneObjectives = doneObjectives;
        }
    }

    [Serializable]
    public class ShopSave
    {
        public List<string> Unlocked = new List<string>();
        public List<string> BoughtFrom = new List<string>();
        public int NextOrder;
        public List<OrderRec> Orders = new List<OrderRec>();
        public List<RefundRec> Refunds = new List<RefundRec>();
    }

    internal sealed class CoreShopFacts : IShopFacts
    {
        private readonly ShopSave save;
        private readonly Dictionary<string, ShopQuest> quests = new Dictionary<string, ShopQuest>();
        private HashSet<string> levels;

        internal CoreShopFacts(ShopSave save)
        {
            this.save = save;
        }

        public ShopQuest Quest(string taskId)
        {
            ShopQuest known;
            if (quests.TryGetValue(taskId, out known)) return known;
            QuestInfo info = Quests.Get(taskId);
            if (info != null)
            {
                List<string> done = new List<string>();
                foreach (ObjectiveInfo objective in info.Objectives)
                {
                    if (objective.State == ObjectiveState.Finished) done.Add(objective.Id);
                }
                known = new ShopQuest(info.Title, info.State == TaskState.Finished, info.State == TaskState.Active, done.ToArray());
            }
            quests[taskId] = known;
            return known;
        }

        public bool Visited(string sceneName)
        {
            if (sceneName == WorldLocation.CurrentScene) return true;
            if (SaveData.Folder == null) return false;
            if (levels == null) levels = ShopVisits.Levels(SaveData.Folder);
            return levels.Contains(sceneName);
        }

        public bool Bought(string shopId)
        {
            ShopPlace place = ShopCatalog.Shop(shopId);
            return place != null && save.BoughtFrom.Contains(place.Name);
        }

        public bool Flag(string key)
        {
            return GlobalFlags.Get(key);
        }

        public ShopCustomGate Custom(string name)
        {
            return ShopCustomGates.Get(name);
        }

        public string ShopName(string shopId)
        {
            ShopPlace place = ShopCatalog.Shop(shopId);
            return place == null ? null : place.Name;
        }

        public string ShopNameForScene(string sceneName)
        {
            foreach (ShopPlace place in ShopCatalog.Shops)
            {
                if (place.SceneName == sceneName) return place.Name;
            }
            return null;
        }
    }

    internal static class ShopGates
    {
        private const string StoreId = "nomad.obenseuer.os.shop";
        private const string Generic = "Not available yet.";
        private const string Unknown = "Needs a task you have not found yet.";
        private static ShopSave save = new ShopSave();

        internal static ShopSave Current
        {
            get
            {
                return save;
            }
        }

        internal static void Attach()
        {
            SaveData.Register<ShopSave>(StoreId, () => save, loaded => { save = loaded ?? new ShopSave(); Normalise(save); });
            Quests.TaskChanged += args => Refresh();
            GlobalFlags.Changed += args => Refresh();
            Shops.Bought += OnBought;
            ItemRegistry.Loaded += ShopCatalog.Ensure;
            SceneManager.activeSceneChanged += (oldScene, newScene) => Refresh();
            Events.Loaded += Refresh;
        }

        internal static ShopGate ParseOptional(JToken token)
        {
            return token == null ? null : Parse(token);
        }

        internal static ShopGate Parse(JToken token)
        {
            JObject o = token as JObject;
            if (o == null) throw new ArgumentException("a gate must be a JSON object");
            if (o["quest"] != null)
            {
                string state = o["state"] == null ? "finished" : ShopCatalog.Text(o["state"]);
                if (state == "finished") return ShopGate.Quest(ShopCatalog.Text(o["quest"]));
                if (state == "active") return ShopGate.QuestActive(ShopCatalog.Text(o["quest"]));
                throw new ArgumentException("unknown quest state " + state);
            }
            if (o["objective"] != null)
            {
                JArray pair = o["objective"] as JArray;
                if (pair == null || pair.Count != 2) throw new ArgumentException("objective needs [task, objective]");
                return ShopGate.Objective(ShopCatalog.Text(pair[0]), ShopCatalog.Text(pair[1]));
            }
            if (o["visited"] != null) return ShopGate.Visited(ShopCatalog.Text(o["visited"]));
            if (o["bought"] != null) return ShopGate.Bought(ShopCatalog.Text(o["bought"]));
            if (o["flag"] != null) return ShopGate.Flag(ShopCatalog.Text(o["flag"]));
            if (o["custom"] != null) return ShopGate.Custom(ShopCatalog.Text(o["custom"]));
            if (o["all"] != null) return ShopGate.All(Parts(o["all"]));
            if (o["any"] != null) return ShopGate.Any(Parts(o["any"]));
            if (o["not"] != null) return ShopGate.Not(Parse(o["not"]));
            throw new ArgumentException("unknown gate");
        }

        internal static bool Evaluate(ShopGate gate, IShopFacts facts)
        {
            switch (gate.Kind)
            {
                case ShopGateKind.Quest:
                    {
                        ShopQuest quest = facts.Quest(gate.Name);
                        return quest != null && quest.Finished;
                    }
                case ShopGateKind.QuestActive:
                    {
                        ShopQuest quest = facts.Quest(gate.Name);
                        return quest != null && quest.Active;
                    }
                case ShopGateKind.Objective:
                    {
                        ShopQuest quest = facts.Quest(gate.Name);
                        return quest != null && Array.IndexOf(quest.DoneObjectives, gate.ObjectiveId) >= 0;
                    }
                case ShopGateKind.Visited:
                    return facts.Visited(gate.Name);
                case ShopGateKind.Bought:
                    return facts.Bought(gate.Name);
                case ShopGateKind.Flag:
                    return facts.Flag(gate.Name);
                case ShopGateKind.Custom:
                    return CustomOpen(facts.Custom(gate.Name));
                case ShopGateKind.All:
                    foreach (ShopGate part in gate.Parts)
                    {
                        if (!Evaluate(part, facts)) return false;
                    }
                    return true;
                case ShopGateKind.Any:
                    foreach (ShopGate part in gate.Parts)
                    {
                        if (Evaluate(part, facts)) return true;
                    }
                    return false;
                case ShopGateKind.Not:
                    return !Evaluate(gate.Parts[0], facts);
                default:
                    return false;
            }
        }

        internal static string Hint(ShopGate gate, IShopFacts facts)
        {
            switch (gate.Kind)
            {
                case ShopGateKind.Quest:
                case ShopGateKind.QuestActive:
                case ShopGateKind.Objective:
                    return QuestHint(gate.Name, facts);
                case ShopGateKind.Visited:
                    {
                        string shop = facts.ShopNameForScene(gate.Name);
                        return shop == null ? "Visit a place to unlock this." : "Visit " + shop + " to unlock this.";
                    }
                case ShopGateKind.Bought:
                    {
                        string shop = facts.ShopName(gate.Name);
                        return shop == null ? "Buy something at a shop first." : "Buy something at " + shop + " first.";
                    }
                case ShopGateKind.Custom:
                    {
                        ShopCustomGate custom = facts.Custom(gate.Name);
                        return custom == null || custom.Hint == null ? Generic : custom.Hint;
                    }
                case ShopGateKind.All:
                    foreach (ShopGate part in gate.Parts)
                    {
                        if (!Evaluate(part, facts)) return Hint(part, facts);
                    }
                    return Generic;
                case ShopGateKind.Any:
                    return AnyHint(gate.Parts, facts);
                default:
                    return Generic;
            }
        }

        internal static bool Open(ShopSave state, ShopListing listing, IShopFacts facts)
        {
            if (listing.Gate == null || state.Unlocked.Contains(listing.Key)) return true;
            if (!Evaluate(listing.Gate, facts)) return false;
            state.Unlocked.Add(listing.Key);
            OsShop.RaiseUnlocked(listing.Key);
            return true;
        }

        internal static void Refresh()
        {
            if (SaveData.Busy) return;
            ShopCatalog.Ensure();
            CoreShopFacts facts = new CoreShopFacts(save);
            foreach (ShopListing listing in ShopCatalog.Listings.ToArray()) Open(save, listing, facts);
        }

        private static void Normalise(ShopSave state)
        {
            state.Unlocked = state.Unlocked ?? new List<string>();
            state.BoughtFrom = state.BoughtFrom ?? new List<string>();
            state.Orders = state.Orders ?? new List<OrderRec>();
            state.Orders.RemoveAll(x => x == null);
            foreach (OrderRec order in state.Orders)
            {
                order.Name = order.Name ?? "";
                order.ItemIds = order.ItemIds ?? new List<int>();
                order.Counts = order.Counts ?? new List<int>();
            }
            state.Refunds = state.Refunds ?? new List<RefundRec>();
            state.Refunds.RemoveAll(x => x == null);
        }

        private static void OnBought(TradeArgs trade)
        {
            string name = trade.Shop == null ? null : trade.Shop.Name;
            if (!string.IsNullOrEmpty(name) && !save.BoughtFrom.Contains(name)) save.BoughtFrom.Add(name);
            Refresh();
        }

        private static ShopGate[] Parts(JToken token)
        {
            JArray array = token as JArray;
            if (array == null) throw new ArgumentException("all and any need an array of gates");
            ShopGate[] parts = new ShopGate[array.Count];
            for (int i = 0; i < parts.Length; i++) parts[i] = Parse(array[i]);
            return parts;
        }

        private static bool CustomOpen(ShopCustomGate custom)
        {
            if (custom == null) return false;
            try
            {
                return custom.Test();
            }
            catch (Exception e)
            {
                ShopCatalog.Log.Error("shop custom gate failed: " + e);
                return false;
            }
        }

        private static string QuestHint(string taskId, IShopFacts facts)
        {
            ShopQuest quest = facts.Quest(taskId);
            return quest == null || string.IsNullOrEmpty(quest.Title) ? Unknown : "Finish the task '" + quest.Title + "'.";
        }

        private static string AnyHint(ShopGate[] parts, IShopFacts facts)
        {
            List<string> hints = new List<string>();
            foreach (ShopGate part in parts)
            {
                string hint = Hint(part, facts);
                if (!hints.Contains(hint)) hints.Add(hint);
            }
            return hints.Count == 0 ? Generic : string.Join(" or ", hints.ToArray());
        }
    }
}
