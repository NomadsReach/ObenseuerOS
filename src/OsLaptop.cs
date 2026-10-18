using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    internal static class OsLaptop
    {
        private const float MoveSpeed = 4f;
        private const float TurnSpeed = 300f;
        private const float FillRatio = 0.7f;
        private const float FlatRatio = 0.25f;
        private const float Clearance = 0.05f;
        private const float MoveDone = 0.001f;
        private const float TurnDone = 0.03f;
        private const float MaxMoveSeconds = 5f;
        private const int SearchDepth = 4;
        private const int ExitDelayFrames = 2;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly string[] ScreenWords = { "screen", "display", "monitor" };
        private static readonly FieldInfo RotationDisabledField = typeof(ThirdPersonCameraController).GetField("rotationDisabled", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly HashSet<int> dumped = new HashSet<int>();
        private static MonoBehaviour host;
        private static GameObject holder;
        private static Coroutine run;
        private static bool active;
        private static bool returning;
        private static RectTransform pin;
        private static Transform cam;
        private static Vector3 savedPosition;
        private static Quaternion savedRotation;
        private static ThirdPersonCameraController rig;
        private static ThirdPersonCameraCollision collision;
        private static bool savedCollision;
        private static bool savedFollowDisabled;
        private static bool savedRotationDisabled;
        private static GameController locked;
        private static int exitFrame;

        internal static bool Active
        {
            get
            {
                return active;
            }
        }

        internal static bool Open(MonoBehaviour owner, GameObject laptop)
        {
            if (active) return true;
            RectTransform rect = null;
            Vector3 target = Vector3.zero;
            Quaternion look = Quaternion.identity;
            try
            {
                Transform root = Root(laptop.transform);
                Camera main = Camera.main;
                ScreenFrame screen;
                bool found = Choose(root, main == null ? Vector3.zero : main.transform.position, out screen);
                if (dumped.Add(root.GetInstanceID())) Dump(root, string.Empty);
                if (!found) return false;
                if (main == null || GameController.instance == null || ThirdPersonCameraController.instance == null || ThirdPersonCameraCollision.instance == null || OpenSewerCharacterController.instance == null || ThirdPersonCameraController.instance.FollowTarget != null)
                {
                    Log.Warn("laptop screen: camera or controls not ready, using the full window");
                    return false;
                }
                rect = MakePin(screen);
                Vector3 normal = -rect.forward;
                float reach = 2f * FillRatio * Mathf.Tan(main.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float distance = Mathf.Max(rect.sizeDelta.y / reach, rect.sizeDelta.x / (reach * main.aspect));
                target = rect.position + normal * Mathf.Max(distance, main.nearClipPlane + Clearance);
                look = Quaternion.LookRotation(-normal, rect.up);
                Log.Debug("laptop.face", "laptop screen: face " + rect.sizeDelta + " distance " + distance + " normal " + normal + " target " + target);
                Vector3[] corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                if (!(Vector3.Dot(corners[3] - corners[0], Vector3.Cross(Vector3.up, -normal)) > 0f))
                {
                    Log.Warn("laptop screen: pin is mirrored, using the full window");
                    UnityEngine.Object.Destroy(rect.gameObject);
                    return false;
                }
                if (!OsView.Show(rect))
                {
                    UnityEngine.Object.Destroy(rect.gameObject);
                    return false;
                }
                host = owner;
                if (holder == null)
                {
                    holder = new GameObject("ObenseuerOS laptop lock");
                    UnityEngine.Object.DontDestroyOnLoad(holder);
                }
                pin = rect;
                cam = main.transform;
                savedPosition = cam.position;
                savedRotation = cam.rotation;
                rig = ThirdPersonCameraController.instance;
                collision = ThirdPersonCameraCollision.instance;
                savedCollision = collision.disableCollisionUpdate;
                savedFollowDisabled = rig.followPlayerDisabled;
                savedRotationDisabled = (bool)RotationDisabledField.GetValue(rig);
                exitFrame = Time.frameCount + ExitDelayFrames;
            }
            catch (Exception e)
            {
                Log.Error("laptop screen: start failed, using the full window: " + e);
                OsView.Dismiss();
                if (rect != null) UnityEngine.Object.Destroy(rect.gameObject);
                return false;
            }
            active = true;
            try
            {
                collision.disableCollisionUpdate = true;
                rig.followPlayerDisabled = true;
                rig.DisableRotation();
                locked = GameController.instance;
                locked.ControlsDisabled(holder, ShowCursor: true);
                run = host.StartCoroutine(Travel(target, look, false));
            }
            catch (Exception e)
            {
                Abort("not started: " + e);
                return false;
            }
            if (run == null) Abort("approach did not start");
            return active;
        }

        internal static void Close(bool snap)
        {
            if (!active || (returning && !snap)) return;
            try
            {
                if (run != null) host.StopCoroutine(run);
                run = null;
                if (snap || cam == null)
                {
                    Restore();
                    Release();
                    return;
                }
                returning = true;
                run = host.StartCoroutine(Travel(savedPosition, savedRotation, true));
                if (run == null) Close(true);
            }
            catch (Exception e)
            {
                Abort("close: " + e);
            }
        }

        internal static void Tick()
        {
            try
            {
                if (active && !returning && (cam == null || !OsView.Visible)) Close(false);
                else if (active && !returning && Time.frameCount >= exitFrame && ExitPressed()) OsView.Dismiss();
            }
            catch (Exception e)
            {
                Abort("tick: " + e);
            }
        }

        private static bool ExitPressed()
        {
            InputManager input = InputManager.instance;
            return (input.inputType == InputManager.InputType.Keyboard && (input.GetKeyDown("Pause Menu") || input.GetKeyDown("Interact"))) || Input.GetKeyUp(KeyCode.Joystick1Button1);
        }

        private static void Abort(string reason)
        {
            if (!active) return;
            try
            {
                if (run != null) host.StopCoroutine(run);
                Restore();
            }
            finally
            {
                Release();
            }
            Log.Error("laptop screen: session aborted: " + reason);
            OsView.Dismiss();
        }

        private static IEnumerator Travel(Vector3 position, Quaternion rotation, bool back)
        {
            float started = Time.realtimeSinceStartup;
            while (true)
            {
                bool moving;
                try
                {
                    moving = Move(position, rotation);
                }
                catch (Exception e)
                {
                    Abort("camera move: " + e);
                    yield break;
                }
                if (!moving || Time.realtimeSinceStartup - started > MaxMoveSeconds) break;
                yield return null;
            }
            if (back || Time.realtimeSinceStartup - started > MaxMoveSeconds)
            {
                Restore();
                Release();
                if (!back) OsView.Dismiss();
            }
        }

        private static bool Move(Vector3 position, Quaternion rotation)
        {
            if (cam == null) return false;
            cam.position = Vector3.MoveTowards(cam.position, position, MoveSpeed * Time.deltaTime);
            cam.rotation = Quaternion.RotateTowards(cam.rotation, rotation, TurnSpeed * Time.deltaTime);
            if (Vector3.Distance(cam.position, position) > MoveDone || Quaternion.Angle(cam.rotation, rotation) > TurnDone) return true;
            cam.position = position;
            cam.rotation = rotation;
            return false;
        }

        private static void Restore()
        {
            if (cam == null) return;
            cam.position = savedPosition;
            cam.rotation = savedRotation;
        }

        private static void Release()
        {
            GameController controls = locked;
            GameObject key = holder;
            RectTransform rect = pin;
            ThirdPersonCameraController rigBack = rig;
            ThirdPersonCameraCollision collisionBack = collision;
            active = false;
            returning = false;
            run = null;
            locked = null;
            pin = null;
            cam = null;
            rig = null;
            collision = null;
            if (controls != null) TryRestore("controls", () => controls.ControlsEnabled(key));
            if (collisionBack != null) TryRestore("collision", () => collisionBack.disableCollisionUpdate = savedCollision);
            if (rect != null) TryRestore("pin", () => UnityEngine.Object.Destroy(rect.gameObject));
            if (rigBack != null)
            {
                TryRestore("camera follow", () => rigBack.followPlayerDisabled = savedFollowDisabled);
                if (!savedRotationDisabled) TryRestore("camera rotation", () => rigBack.EnableRotation());
            }
        }

        private static void TryRestore(string what, Action restore)
        {
            try
            {
                restore();
            }
            catch (Exception e)
            {
                Log.Error("laptop screen: " + what + " not restored: " + e);
            }
        }

        private static ScreenFrame FlatScreen(Transform node, Bounds local, Vector3 viewer)
        {
            Vector3 size = local.size;
            int thin = Thin(ScaledSize(size, node));
            int first = (thin + 1) % 3;
            int second = (thin + 2) % 3;
            int height = Mathf.Abs(Vector3.Dot(node.TransformDirection(Axis(first)), Vector3.up)) > Mathf.Abs(Vector3.Dot(node.TransformDirection(Axis(second)), Vector3.up)) ? first : second;
            int width = height == first ? second : first;
            Vector3 centre = node.TransformPoint(local.center);
            Vector3 normal = node.TransformDirection(Axis(thin)).normalized;
            Vector3 upward = node.TransformDirection(Axis(height)).normalized;
            if (Vector3.Dot(normal, viewer - centre) < 0f) normal = -normal;
            if (Vector3.Dot(upward, Vector3.up) < 0f) upward = -upward;
            ScreenFrame frame = new ScreenFrame();
            frame.Centre = centre + normal * (size[thin] * Mathf.Abs(node.lossyScale[thin]) * 0.5f);
            frame.Normal = normal;
            frame.Up = upward;
            frame.Size = new Vector2(size[width] * Mathf.Abs(node.lossyScale[width]), size[height] * Mathf.Abs(node.lossyScale[height]));
            return frame;
        }

        private static RectTransform MakePin(ScreenFrame frame)
        {
            RectTransform rect = (RectTransform)new GameObject("ObenseuerOS laptop screen", typeof(RectTransform)).transform;
            try
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = frame.Size;
                rect.position = frame.Centre;
                rect.rotation = Quaternion.LookRotation(-frame.Normal, frame.Up);
                rect.localScale = Vector3.one;
            }
            catch
            {
                UnityEngine.Object.Destroy(rect.gameObject);
                throw;
            }
            return rect;
        }

        private static bool Choose(Transform root, Vector3 viewer, out ScreenFrame screen)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null || !renderer.enabled || !Named(renderer.name) || !Flat(mesh.bounds, renderer.transform)) continue;
                Log.Debug("laptop.candidate", "laptop screen: candidate " + PathOf(renderer.transform) + " by name, mesh " + mesh.name);
                screen = FlatScreen(renderer.transform, mesh.bounds, viewer);
                return true;
            }
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null || !renderer.enabled || !mesh.isReadable) continue;
                Material[] materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length && i < mesh.subMeshCount; i++)
                {
                    if (materials[i] == null || !Named(materials[i].name)) continue;
                    Bounds part = SubmeshBounds(mesh, i);
                    if (!Flat(part, renderer.transform)) continue;
                    Log.Debug("laptop.candidate", "laptop screen: candidate " + PathOf(renderer.transform) + " by material " + materials[i].name + ", submesh " + i);
                    screen = FlatScreen(renderer.transform, part, viewer);
                    return true;
                }
            }
            ScreenFrame best = new ScreenFrame();
            float bestScore = 0f;
            bool found = false;
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null || !renderer.enabled) continue;
                ScreenFrame candidate;
                float score;
                if (OsScreenFinder.Find(mesh, renderer.transform.localToWorldMatrix, viewer, out candidate, out score) && (!found || score > bestScore))
                {
                    best = candidate;
                    bestScore = score;
                    found = true;
                }
            }
            if (found)
            {
                screen = best;
                return true;
            }
            screen = new ScreenFrame();
            Log.Warn("laptop screen: no flat screen surface or screen cluster under " + PathOf(root) + ", using the full window");
            return false;
        }

        private static Bounds SubmeshBounds(Mesh mesh, int index)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.GetTriangles(index, true);
            if (triangles.Length == 0) return new Bounds();
            Bounds part = new Bounds(vertices[triangles[0]], Vector3.zero);
            foreach (int corner in triangles) part.Encapsulate(vertices[corner]);
            return part;
        }

        private static bool Flat(Bounds bounds, Transform node)
        {
            Vector3 size = ScaledSize(bounds.size, node);
            int thin = Thin(size);
            float face = Mathf.Min(size[(thin + 1) % 3], size[(thin + 2) % 3]);
            return face > 0f && size[thin] <= FlatRatio * face;
        }

        private static int Thin(Vector3 size)
        {
            int thin = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] < size[thin]) thin = i;
            }
            return thin;
        }

        private static Vector3 ScaledSize(Vector3 size, Transform node)
        {
            Vector3 scale = node.lossyScale;
            for (int i = 0; i < 3; i++) size[i] *= Mathf.Abs(scale[i]);
            return size;
        }

        private static Vector3 Axis(int index)
        {
            return index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;
        }

        private static bool Named(string name)
        {
            foreach (string word in ScreenWords)
            {
                if (name.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || renderer.isPartOfStaticBatch) return null;
            return filter.sharedMesh;
        }

        private static Transform Root(Transform hit)
        {
            Transform named = null;
            Transform node = hit;
            for (int depth = 0; depth < SearchDepth && node != null; depth++)
            {
                if (OsLaptopPrompt.IsLaptopName(node.name)) named = node;
                node = node.parent;
            }
            return named != null ? named : hit;
        }

        private static void Dump(Transform node, string indent)
        {
            Log.Debug("laptop.dump", "laptop screen: " + indent + node.name + " active " + node.gameObject.activeSelf + " position " + node.localPosition + " rotation " + node.localEulerAngles + " scale " + node.localScale);
            Renderer renderer = node.GetComponent<Renderer>();
            if (renderer != null) Log.Debug("laptop.dump", "laptop screen: " + indent + "  renderer " + renderer.GetType().Name + " enabled " + renderer.enabled + " materials " + MaterialNames(renderer) + " mesh " + MeshText(renderer) + " static " + renderer.isPartOfStaticBatch);
            for (int i = 0; i < node.childCount; i++) Dump(node.GetChild(i), indent + "  ");
        }

        private static string MaterialNames(Renderer renderer)
        {
            List<string> names = new List<string>();
            foreach (Material material in renderer.sharedMaterials) names.Add(material == null ? "null" : material.name);
            return "[" + string.Join(", ", names.ToArray()) + "]";
        }

        private static string MeshText(Renderer renderer)
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return "none";
            Mesh mesh = filter.sharedMesh;
            return mesh.name + " readable " + mesh.isReadable + " submeshes " + mesh.subMeshCount + " bounds centre " + mesh.bounds.center + " size " + mesh.bounds.size;
        }

        private static string PathOf(Transform node)
        {
            string path = node.name;
            for (Transform parent = node.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}
