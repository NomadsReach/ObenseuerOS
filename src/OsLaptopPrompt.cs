using System;
using System.Collections.Generic;
using System.Reflection;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    internal static class OsLaptopPrompt
    {
        private const int SearchDepth = 4;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly List<string> names = new List<string> { "laptop_001b", "Laptop" };
        private static GameObject lastLook;
        private static bool failed;
        private static bool logLook;

        internal static bool IsLaptopName(string name)
        {
            return names.Contains(name);
        }

        internal static void Attach(string extraNames, bool logTargets)
        {
            foreach (string name in extraNames.Split(','))
            {
                if (name.Trim().Length > 0 && !names.Contains(name.Trim())) names.Add(name.Trim());
            }
            logLook = logTargets;
            MethodInfo postfix = typeof(OsLaptopPrompt).GetMethod("LookPostfix", BindingFlags.NonPublic | BindingFlags.Static);
            if (!CorePatcher.Patch(typeof(InteractObjects), "RaycastInteract", new Type[] { typeof(bool) }, null, postfix, null, false, Os.ModId))
            {
                failed = true;
                Log.Error("laptop prompt off: RaycastInteract patch failed");
            }
        }

        private static void LookPostfix(InteractObjects __instance)
        {
            Look(__instance);
        }

        private static void Look(InteractObjects objects)
        {
            GameObject target = objects.currentGameObject;
            if (failed || target == lastLook) return;
            lastLook = target;
            try
            {
                if (logLook) LogLook(objects, target);
                if (target != null) AttachLaptop(target);
            }
            catch (Exception e)
            {
                failed = true;
                Log.Error("laptop prompt disabled: " + e);
            }
        }

        private static void AttachLaptop(GameObject hit)
        {
            if (!IsLaptop(hit.transform)) return;
            if (ComputerApps.AddInteraction(hit, Os.ModId, "Laptop", "ObenseuerOS", () => Open(hit))) Log.Info("laptop prompt attached to " + PathOf(hit.transform) + ", scene " + hit.scene.name);
        }

        private static void Open(GameObject laptop)
        {
            if (!OsPlugin.OpenLaptop(laptop)) Log.Warn("laptop prompt: the OS did not open");
        }

        private static bool IsLaptop(Transform node)
        {
            if (!HasLaptopName(node)) return false;
            for (int depth = 0; depth < SearchDepth && node != null; depth++)
            {
                if (HasOtherInteractable(node)) return false;
                node = node.parent;
            }
            return true;
        }

        private static bool HasLaptopName(Transform node)
        {
            for (int depth = 0; depth < SearchDepth && node != null; depth++)
            {
                if (names.Contains(node.name)) return true;
                node = node.parent;
            }
            return false;
        }

        private static bool HasOtherInteractable(Transform node)
        {
            foreach (Component part in node.GetComponents<Component>())
            {
                if (part is IInteractable) return true;
            }
            return false;
        }

        private static void LogLook(InteractObjects objects, GameObject target)
        {
            if (target == null)
            {
                Log.Info("look target: none");
                return;
            }
            Log.Info("look target " + PathOf(target.transform) + ", layer " + target.layer + ", usable " + objects.IsUsableLayer(target.layer) + ", colliders " + TypeNames<Collider>(target) + ", interactables " + TypeNames<IInteractable>(target) + ", scene " + target.scene.name);
        }

        private static string PathOf(Transform node)
        {
            string path = node.name;
            for (Transform parent = node.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }

        private static string TypeNames<T>(GameObject go) where T : class
        {
            List<string> found = new List<string>();
            foreach (Component part in go.GetComponents<Component>())
            {
                if (part is T) found.Add(part.GetType().Name);
            }
            return found.Count == 0 ? "none" : string.Join(",", found.ToArray());
        }
    }
}
