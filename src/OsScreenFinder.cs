using System.Collections.Generic;
using ObenseuerCore;
using UnityEngine;

namespace ObenseuerOS
{
    internal struct ScreenFrame
    {
        internal Vector3 Centre;
        internal Vector3 Normal;
        internal Vector3 Up;
        internal Vector2 Size;
    }

    internal static class OsScreenFinder
    {
        private const float PlaneCos = 0.9995f;
        private const float PlaneGap = 0.0001f;
        private const float NormalCell = 0.05f;
        private const float WeldGap = 0.00001f;
        private const float DegenerateArea = 0.000000001f;
        private const float RectRatio = 0.9f;
        private const float MinAspect = 1.2f;
        private const float MaxAspect = 2.0f;
        private const float FacingMin = 0.3f;
        private const float MaxUpright = 0.7f;
        private const float InsetRatio = 0.05f;
        private const float InnerRatio = 0.5f;
        private const int MaxTriangles = 20000;
        private static readonly CoreLog Log = CoreLog.For(Os.ModId);
        private static readonly Dictionary<int, List<Vector3[]>> cache = new Dictionary<int, List<Vector3[]>>();

        internal static bool Find(Mesh mesh, Matrix4x4 toWorld, Vector3 viewer, out ScreenFrame frame, out float score)
        {
            frame = new ScreenFrame();
            score = 0f;
            if (!mesh.isReadable)
            {
                Log.Warn("laptop screen: mesh " + mesh.name + " is not readable, skipped");
                return false;
            }
            int key = mesh.GetInstanceID();
            List<Vector3[]> rects;
            if (!cache.TryGetValue(key, out rects))
            {
                int[] triangles = mesh.triangles;
                if (triangles.Length > 3 * MaxTriangles)
                {
                    Log.Warn("laptop screen: mesh " + mesh.name + " has " + triangles.Length / 3 + " triangles, over " + MaxTriangles + ", skipped");
                    rects = new List<Vector3[]>();
                }
                else rects = Rects(mesh.vertices, triangles);
                cache[key] = rects;
            }
            int candidates;
            if (!Pick(rects, toWorld, viewer, out frame, out candidates, out score))
            {
                Log.Warn("laptop screen: mesh " + mesh.name + " has " + rects.Count + " rectangular clusters and none faces the viewer upright, skipped");
                return false;
            }
            Vector2 size = frame.Size;
            Log.Info("laptop screen: cluster on " + mesh.name + " normal " + frame.Normal + " size " + size + " aspect " + Mathf.Max(size.x, size.y) / Mathf.Min(size.x, size.y) + " score " + score + " candidates " + candidates + " of " + rects.Count + " rectangular clusters");
            return true;
        }

        internal static bool Pick(List<Vector3[]> rects, Matrix4x4 toWorld, Vector3 viewer, out ScreenFrame frame, out int candidates, out float score)
        {
            List<Candidate> panels = Panels(rects, toWorld, viewer, 1f);
            if (panels.Count == 0)
            {
                panels = Panels(rects, toWorld, viewer, -1f);
                if (panels.Count > 0) Log.Warn("laptop screen: winding faces away from the viewer, using the flipped normal");
            }
            candidates = panels.Count;
            frame = new ScreenFrame();
            score = 0f;
            if (panels.Count == 0) return false;
            Candidate best = panels[0];
            foreach (Candidate panel in panels)
            {
                if (panel.Width * panel.Height > best.Width * best.Height) best = panel;
            }
            score = best.Width * best.Height;
            Candidate inner = null;
            float depth = 0f;
            foreach (Candidate panel in panels)
            {
                Vector3 offset = panel.Centre - best.Centre;
                float along = Vector3.Dot(offset, best.Normal);
                if (panel == best || along <= 0f || panel.Width * panel.Height < InnerRatio * score || Mathf.Abs(Vector3.Dot(panel.Normal, best.Normal)) < PlaneCos) continue;
                if (Mathf.Abs(Vector3.Dot(offset, best.Across)) > best.Width * 0.5f || Mathf.Abs(Vector3.Dot(offset, best.Upward)) > best.Height * 0.5f) continue;
                if (inner == null || along > depth)
                {
                    inner = panel;
                    depth = along;
                }
            }
            Candidate screen = inner ?? best;
            float scale = inner == null ? 1f - 2f * InsetRatio : 1f;
            frame.Centre = screen.Centre;
            frame.Normal = screen.Normal;
            frame.Up = screen.Upward;
            frame.Size = new Vector2(screen.Width * scale, screen.Height * scale);
            return true;
        }

        private static List<Candidate> Panels(List<Vector3[]> rects, Matrix4x4 toWorld, Vector3 viewer, float sign)
        {
            List<Candidate> panels = new List<Candidate>();
            foreach (Vector3[] rect in rects)
            {
                Candidate panel = Facing(rect, toWorld, viewer, sign);
                if (panel != null) panels.Add(panel);
            }
            return panels;
        }

        private static List<Vector3[]> Rects(Vector3[] vertices, int[] triangles)
        {
            List<Vector3[]> rects = new List<Vector3[]>();
            if (vertices.Length == 0 || triangles.Length < 3) return rects;
            Bounds bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 vertex in vertices) bounds.Encapsulate(vertex);
            float diagonal = bounds.size.magnitude;
            if (diagonal <= 0f) return rects;
            int[] weld = Weld(vertices, diagonal * WeldGap);
            float gap = diagonal * PlaneGap;
            float degenerate = diagonal * diagonal * DegenerateArea;
            List<Vector3> normals = new List<Vector3>();
            List<float> offsets = new List<float>();
            List<List<int>> planes = new List<List<int>>();
            Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
            int count = triangles.Length / 3;
            for (int t = 0; t < count; t++)
            {
                Vector3 a = vertices[triangles[3 * t]];
                Vector3 cross = Vector3.Cross(vertices[triangles[3 * t + 1]] - a, vertices[triangles[3 * t + 2]] - a);
                float length = cross.magnitude;
                if (length <= degenerate) continue;
                Vector3 normal = cross / length;
                float offset = Vector3.Dot(normal, a);
                int plane = Match(buckets, normal, offset, normals, offsets, gap);
                if (plane < 0)
                {
                    plane = normals.Count;
                    normals.Add(normal);
                    offsets.Add(offset);
                    planes.Add(new List<int>());
                    long key = Key(normal, offset, gap);
                    List<int> bucket;
                    if (!buckets.TryGetValue(key, out bucket))
                    {
                        bucket = new List<int>();
                        buckets[key] = bucket;
                    }
                    bucket.Add(plane);
                }
                planes[plane].Add(t);
            }
            for (int p = 0; p < planes.Count; p++) Clusters(vertices, triangles, weld, planes[p], normals[p], rects);
            return rects;
        }

        private static int Match(Dictionary<long, List<int>> buckets, Vector3 normal, float offset, List<Vector3> normals, List<float> offsets, float gap)
        {
            int x = Cell(normal.x, NormalCell);
            int y = Cell(normal.y, NormalCell);
            int z = Cell(normal.z, NormalCell);
            int d = Cell(offset, 2f * gap);
            int plane = -1;
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        for (int dd = -1; dd <= 1; dd++)
                        {
                            List<int> bucket;
                            if (!buckets.TryGetValue(Bucket(x + dx, y + dy, z + dz, d + dd), out bucket)) continue;
                            foreach (int p in bucket)
                            {
                                if ((plane < 0 || p < plane) && Vector3.Dot(normal, normals[p]) >= PlaneCos && Mathf.Abs(offset - offsets[p]) <= gap) plane = p;
                            }
                        }
                    }
                }
            }
            return plane;
        }

        private static long Key(Vector3 normal, float offset, float gap)
        {
            return Bucket(Cell(normal.x, NormalCell), Cell(normal.y, NormalCell), Cell(normal.z, NormalCell), Cell(offset, 2f * gap));
        }

        private static int Cell(float value, float size)
        {
            return Mathf.FloorToInt(value / size);
        }

        private static long Bucket(int x, int y, int z, int d)
        {
            return ((((long)d * 100 + x + 50) * 100 + y + 50) * 100 + z + 50);
        }

        private static void Clusters(Vector3[] vertices, int[] triangles, int[] weld, List<int> plane, Vector3 normal, List<Vector3[]> rects)
        {
            int[] parent = new int[plane.Count];
            Dictionary<int, int> owner = new Dictionary<int, int>();
            Dictionary<long, int> uses = new Dictionary<long, int>();
            for (int i = 0; i < parent.Length; i++) parent[i] = i;
            for (int i = 0; i < plane.Count; i++)
            {
                for (int k = 0; k < 3; k++)
                {
                    int vertex = weld[triangles[3 * plane[i] + k]];
                    int other;
                    if (owner.TryGetValue(vertex, out other)) Join(parent, i, other);
                    else owner[vertex] = i;
                    long pair = Edge(triangles, weld, vertices.Length, 3 * plane[i] + k);
                    int count;
                    uses.TryGetValue(pair, out count);
                    uses[pair] = count + 1;
                }
            }
            Dictionary<int, List<int>> parts = new Dictionary<int, List<int>>();
            for (int i = 0; i < parent.Length; i++)
            {
                int root = Root(parent, i);
                if (!parts.ContainsKey(root)) parts[root] = new List<int>();
                parts[root].Add(i);
            }
            foreach (List<int> part in parts.Values)
            {
                Vector3 origin = vertices[triangles[3 * plane[part[0]]]];
                Vector3 side = Vector3.zero;
                float longest = 0f;
                float area = 0f;
                foreach (int i in part)
                {
                    int t = plane[i];
                    Vector3 a = vertices[triangles[3 * t]];
                    area += Vector3.Cross(vertices[triangles[3 * t + 1]] - a, vertices[triangles[3 * t + 2]] - a).magnitude * 0.5f;
                    for (int k = 0; k < 3; k++)
                    {
                        int corner = 3 * t + k;
                        if (uses[Edge(triangles, weld, vertices.Length, corner)] != 1) continue;
                        Vector3 span = vertices[triangles[3 * t + (k + 1) % 3]] - vertices[triangles[corner]];
                        if (span.sqrMagnitude > longest)
                        {
                            longest = span.sqrMagnitude;
                            side = span;
                        }
                    }
                }
                if (longest <= 0f) continue;
                Vector3 u = (side - normal * Vector3.Dot(side, normal)).normalized;
                Vector3 w = Vector3.Cross(normal, u);
                float minU = float.MaxValue;
                float maxU = float.MinValue;
                float minW = float.MaxValue;
                float maxW = float.MinValue;
                foreach (int i in part)
                {
                    int t = plane[i];
                    for (int k = 0; k < 3; k++)
                    {
                        Vector3 offset = vertices[triangles[3 * t + k]] - origin;
                        float x = Vector3.Dot(offset, u);
                        float y = Vector3.Dot(offset, w);
                        minU = Mathf.Min(minU, x);
                        maxU = Mathf.Max(maxU, x);
                        minW = Mathf.Min(minW, y);
                        maxW = Mathf.Max(maxW, y);
                    }
                }
                float width = maxU - minU;
                float height = maxW - minW;
                if (width <= 0f || height <= 0f || area < RectRatio * width * height) continue;
                rects.Add(new Vector3[] { origin + u * minU + w * minW, origin + u * minU + w * maxW, origin + u * maxU + w * maxW, origin + u * maxU + w * minW });
            }
        }

        private static long Edge(int[] triangles, int[] weld, int count, int corner)
        {
            int a = weld[triangles[corner]];
            int b = weld[triangles[3 * (corner / 3) + (corner + 1) % 3]];
            return a < b ? (long)a * count + b : (long)b * count + a;
        }

        private static Vector3 WorldNormal(Matrix4x4 toWorld, Vector3 local)
        {
            Vector3 c0 = new Vector3(toWorld.m00, toWorld.m10, toWorld.m20);
            Vector3 c1 = new Vector3(toWorld.m01, toWorld.m11, toWorld.m21);
            Vector3 c2 = new Vector3(toWorld.m02, toWorld.m12, toWorld.m22);
            Vector3 cofactor = Vector3.Cross(c1, c2) * local.x + Vector3.Cross(c2, c0) * local.y + Vector3.Cross(c0, c1) * local.z;
            return Vector3.Dot(c0, Vector3.Cross(c1, c2)) < 0f ? -cofactor : cofactor;
        }

        private static Candidate Facing(Vector3[] rect, Matrix4x4 toWorld, Vector3 viewer, float sign)
        {
            Vector3 bottomLeft = toWorld.MultiplyPoint3x4(rect[0]);
            Vector3 topLeft = toWorld.MultiplyPoint3x4(rect[1]);
            Vector3 topRight = toWorld.MultiplyPoint3x4(rect[2]);
            Vector3 bottomRight = toWorld.MultiplyPoint3x4(rect[3]);
            Vector3 centre = (bottomLeft + topRight) * 0.5f;
            Vector3 toViewer = viewer - centre;
            float distance = toViewer.magnitude;
            Vector3 across = bottomRight - bottomLeft;
            Vector3 up = topLeft - bottomLeft;
            float a = across.magnitude;
            float b = up.magnitude;
            if (distance <= 0f || a <= 0f || b <= 0f) return null;
            Vector3 normal = WorldNormal(toWorld, Vector3.Cross(rect[3] - rect[0], rect[1] - rect[0])).normalized * sign;
            if (!(Vector3.Dot(normal, toViewer / distance) >= FacingMin) || Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > MaxUpright) return null;
            if (Mathf.Max(a, b) > MaxAspect * Mathf.Min(a, b) || Mathf.Max(a, b) < MinAspect * Mathf.Min(a, b)) return null;
            bool tall = Mathf.Abs(Vector3.Dot(up / b, Vector3.up)) >= Mathf.Abs(Vector3.Dot(across / a, Vector3.up));
            Vector3 upward = tall ? up / b : across / a;
            if (Vector3.Dot(upward, Vector3.up) < 0f) upward = -upward;
            return new Candidate
            {
                Centre = centre,
                Normal = normal,
                Across = tall ? across / a : up / b,
                Upward = upward,
                Width = tall ? a : b,
                Height = tall ? b : a
            };
        }

        private static int[] Weld(Vector3[] vertices, float gap)
        {
            int[] weld = new int[vertices.Length];
            Dictionary<Vector3, int> first = new Dictionary<Vector3, int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                Vector3 key = new Vector3(Mathf.Round(p.x / gap), Mathf.Round(p.y / gap), Mathf.Round(p.z / gap));
                if (!first.ContainsKey(key)) first[key] = i;
                weld[i] = first[key];
            }
            return weld;
        }

        private static int Root(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        private static void Join(int[] parent, int a, int b)
        {
            parent[Root(parent, a)] = Root(parent, b);
        }

        internal static string SelfCheck()
        {
            return Check("ring", true, false, false, 0f, 1, new Vector2(0.63f, 0.36f))
                ?? Check("solid", false, false, false, 0f, 2, new Vector2(0.7f, 0.4f))
                ?? Check("back shell", false, true, false, 0f, 2, new Vector2(0.7f, 0.4f))
                ?? Check("logo", false, false, true, 0f, 3, new Vector2(0.7f, 0.4f))
                ?? Check("rotated", false, false, false, 15f, 2, new Vector2(0.7f, 0.4f));
        }

        private static string Check(string name, bool bezel, bool back, bool logo, float turn, int expected, Vector2 size)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            Quad(vertices, triangles, new Vector3(-0.4f, 0f, 0f), new Vector3(0.4f, 0f, 0f), new Vector3(0.4f, 0f, 0.5f), new Vector3(-0.4f, 0f, 0.5f));
            if (bezel)
            {
                Quad(vertices, triangles, new Vector3(-0.4f, 0f, 0f), new Vector3(0.4f, 0f, 0f), new Vector3(0.4f, 0.05f, 0f), new Vector3(-0.4f, 0.05f, 0f));
                Quad(vertices, triangles, new Vector3(-0.4f, 0.45f, 0f), new Vector3(0.4f, 0.45f, 0f), new Vector3(0.4f, 0.5f, 0f), new Vector3(-0.4f, 0.5f, 0f));
                Quad(vertices, triangles, new Vector3(-0.4f, 0.05f, 0f), new Vector3(-0.35f, 0.05f, 0f), new Vector3(-0.35f, 0.45f, 0f), new Vector3(-0.4f, 0.45f, 0f));
                Quad(vertices, triangles, new Vector3(0.35f, 0.05f, 0f), new Vector3(0.4f, 0.05f, 0f), new Vector3(0.4f, 0.45f, 0f), new Vector3(0.35f, 0.45f, 0f));
            }
            else Quad(vertices, triangles, new Vector3(-0.4f, 0f, 0f), new Vector3(0.4f, 0f, 0f), new Vector3(0.4f, 0.5f, 0f), new Vector3(-0.4f, 0.5f, 0f));
            Quad(vertices, triangles, new Vector3(-0.35f, 0.05f, 0.002f), new Vector3(0.35f, 0.05f, 0.002f), new Vector3(0.35f, 0.45f, 0.002f), new Vector3(-0.35f, 0.45f, 0.002f));
            if (back) Quad(vertices, triangles, new Vector3(-0.45f, 0f, -0.05f), new Vector3(-0.45f, 0.55f, -0.05f), new Vector3(0.45f, 0.55f, -0.05f), new Vector3(0.45f, 0f, -0.05f));
            if (logo) Quad(vertices, triangles, new Vector3(-0.3f, 0.06f, 0.004f), new Vector3(-0.18f, 0.06f, 0.004f), new Vector3(-0.18f, 0.14f, 0.004f), new Vector3(-0.3f, 0.14f, 0.004f));
            float cos = Mathf.Cos(turn * Mathf.Deg2Rad);
            float sin = Mathf.Sin(turn * Mathf.Deg2Rad);
            Vector3 pivot = new Vector3(0f, 0.25f, 0f);
            for (int i = 0; i < vertices.Count; i++)
            {
                Vector3 p = vertices[i] - pivot;
                vertices[i] = new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos, p.z) + pivot;
            }
            ScreenFrame frame;
            int found;
            float score;
            List<Vector3[]> rects = Rects(vertices.ToArray(), triangles.ToArray());
            if (!Pick(rects, Matrix4x4.identity, new Vector3(0f, 1.5f, 1f), out frame, out found, out score)) return "screen finder " + name + ": no screen picked";
            if (found != expected) return "screen finder " + name + ": " + found + " candidates, expected " + expected;
            Vector3 up = new Vector3(-sin, cos, 0f);
            if (!Near(frame.Centre, new Vector3(0f, 0.25f, 0.002f)) || !Near(frame.Normal, Vector3.forward) || !Near(frame.Up, up) || !Near(frame.Size, size)) return "screen finder " + name + ": picked centre " + frame.Centre + " normal " + frame.Normal + " up " + frame.Up + " size " + frame.Size;
            return null;
        }

        private static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 bottomLeft, Vector3 bottomRight, Vector3 topRight, Vector3 topLeft)
        {
            int first = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(bottomRight);
            vertices.Add(topRight);
            vertices.Add(topLeft);
            triangles.AddRange(new int[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }

        private static bool Near(Vector3 a, Vector3 b)
        {
            return (a - b).magnitude < 0.0001f;
        }

        private static bool Near(Vector2 a, Vector2 b)
        {
            return (a - b).magnitude < 0.0001f;
        }

        private sealed class Candidate
        {
            internal Vector3 Centre;
            internal Vector3 Normal;
            internal Vector3 Across;
            internal Vector3 Upward;
            internal float Width;
            internal float Height;
        }
    }
}
