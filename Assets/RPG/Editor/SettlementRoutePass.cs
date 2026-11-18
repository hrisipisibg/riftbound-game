using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class SettlementRoutePass
    {
        const string Evidence = ".code-check/overnight/";
        const string Folder = "Assets/RPG/Terrain/OvernightRoutes";
        static Vector3 Ground(Vector3 p) { p.y = WorldRegions.GroundHeight(p); return p; }

        public static void SurveyNorthGate()
        {
            try
            {
                Open();
                var path = FindNorthGatePath();
                File.WriteAllText(Evidence + "north-gate-survey.txt", string.Join("\n", path.Select(p => p.ToString("F2"))));
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail("north-gate-survey", e); }
        }

        static void Open()
        {
            if (!Application.isBatchMode || !File.Exists(".coordination/world-edit-context.md")) throw new Exception("Isolated batch with local edit authorization only.");
            Directory.CreateDirectory(Evidence); EditorSceneManager.OpenScene(WorldRegions.ScenePath); Physics.SyncTransforms();
        }

        internal static List<Vector3> FindNorthGatePath()
        {
            const int width = 41, depth = 109;
            const float step = .5f, minX = -212, minZ = -64;
            int count = width * depth;
            var tested = new byte[count]; var closed = new bool[count]; var costs = Enumerable.Repeat(float.PositiveInfinity, count).ToArray();
            var parent = Enumerable.Repeat(-1, count).ToArray();
            Func<int, Vector3> point = id => Ground(new Vector3(minX + id % width * step, 0, minZ + id / width * step));
            Func<Vector3, int> index = p => Mathf.RoundToInt((p.z - minZ) / step) * width + Mathf.RoundToInt((p.x - minX) / step);
            Func<int, bool> free = id =>
            {
                if (tested[id] == 0) tested[id] = (byte)(Clear(point(id)) ? 1 : 2);
                return tested[id] == 1;
            };
            int start = index(new Vector3(-200, 0, -60)), goal = index(new Vector3(-204, 0, -15));
            if (!free(start) || !free(goal)) throw new Exception("North-gate route endpoint is obstructed; inspect before moving scenery.");
            var open = new List<int> { start }; costs[start] = 0;
            bool found = false;
            while (open.Count > 0)
            {
                int bestAt = 0; float best = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = costs[open[i]] + Vector3.Distance(point(open[i]), point(goal));
                    if (f < best) { best = f; bestAt = i; }
                }
                int current = open[bestAt]; open.RemoveAt(bestAt);
                if (current == goal) { found = true; break; }
                if (closed[current]) continue;
                closed[current] = true;
                int cx = current % width, cz = current / width;
                for (int dz = -1; dz <= 1; dz++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        int x = cx + dx, z = cz + dz;
                        if (x < 0 || x >= width || z < 0 || z >= depth) continue;
                        int next = z * width + x;
                        if (closed[next] || !free(next)) continue;
                        if (dx != 0 && dz != 0 && (!free(cz * width + x) || !free(z * width + cx))) continue;
                        Vector3 a = point(current), b = point(next);
                        float run = new Vector2(a.x - b.x, a.z - b.z).magnitude;
                        if (Mathf.Abs(a.y - b.y) > run * .7f) continue;
                        float cost = costs[current] + Vector3.Distance(a, b);
                        if (cost >= costs[next]) continue;
                        parent[next] = current; costs[next] = cost;
                        if (!open.Contains(next)) open.Add(next);
                    }
            }
            if (!found) throw new Exception("No clear north-gate path within the bounded plaza; preserve buildings and inspect.");
            var raw = new List<Vector3>();
            for (int id = goal; id >= 0; id = parent[id]) raw.Add(point(id));
            raw.Reverse();
            var smooth = new List<Vector3> { raw[0] };
            for (int i = 0; i < raw.Count - 1;)
            {
                int next = raw.Count - 1;
                while (next > i + 1 && !ClearLine(raw[i], raw[next])) next--;
                smooth.Add(raw[next]); i = next;
            }
            return smooth;
        }

        static bool Clear(Vector3 p)
        {
            foreach (var c in Physics.OverlapCapsule(p + Vector3.up * .92f, p + Vector3.up * 2.88f, .78f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider || c is CharacterController || c.GetComponentInParent<Combatant>()) continue;
                if (c.bounds.max.y <= p.y + .2f) continue;
                return false;
            }
            return true;
        }

        static bool ClearLine(Vector3 a, Vector3 b)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / .25f));
            Vector3 previous = Ground(a);
            for (int i = 0; i <= steps; i++)
            {
                Vector3 p = Ground(Vector3.Lerp(a, b, i / (float)steps));
                if (!Clear(p)) return false;
                float run = new Vector2(p.x - previous.x, p.z - previous.z).magnitude;
                if (run > .01f && Mathf.Abs(p.y - previous.y) > run * .7f) return false;
                previous = p;
            }
            return true;
        }

        public static void ApplyRoutes()
        {
            try
            {
                Open();
                if (GameObject.Find("Route refinements")) throw new Exception("Route refinements already exist; inspect instead of reapplying.");
                var north = FindNorthGatePath();
                var riverJunction = Ground(new Vector3(-225, 0, 0));
                if (!ClearLine(north[north.Count - 1], riverJunction)) throw new Exception("North gate to river road is obstructed; inspect before connecting.");
                north.Add(riverJunction);
                var report = new StringBuilder($"SETTLEMENT ROUTES UTC={DateTime.UtcNow:O}\nNORTH GATE\n");
                foreach (var point in north) report.AppendLine(point.ToString("F2"));
                var village = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t => t.name == "Emberwatch Village Architecture");
                var fences = village.Cast<Transform>().Where(t => t.name.StartsWith("Prop_WoodenFence_Single") && t.position.x < 0)
                    .OrderBy(t => Mathf.Abs(t.position.z + 1)).Take(2).ToArray();
                if (fences.Length != 2 || fences.Any(t => Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2(-19, 0)) > 8))
                    throw new Exception("Unexpected fence layout; no bulk changes permitted.");
                Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
                var root = new GameObject("Route refinements").transform;
                foreach (var fence in fences)
                {
                    report.AppendLine($"Intentional west-exit opening: retained inactive fence segment at {fence.position}");
                    fence.gameObject.SetActive(false);
                }
                var road = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Road.mat");
                var earth = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Terrain/Earthen road.mat");
                BuildPath("Highspire north footway", north, 1.8f, road, root);
                BuildPath("Emberwatch west departure", new List<Vector3> { Ground(new Vector3(-15,0,0)), Ground(new Vector3(-25,0,-5)), Ground(new Vector3(-34,0,-11)) }, 2.4f, earth, root);
                BuildPath("Greenhollow arrival footway", new List<Vector3> { Ground(new Vector3(-17,0,368)), Ground(new Vector3(-27,0,386)), Ground(new Vector3(-36,0,395)) }, 2.5f, earth, root);
                report.AppendLine("Greenhollow arrival moved to clear space (-36,395); scout marker/discovery IDs untouched. No building, well or wall moved/deleted. Terrain assets unchanged.");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                File.WriteAllText(Evidence + "settlement-routes.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Fail("settlement-routes", e); }
        }

        static void BuildPath(string name, IList<Vector3> points, float width, Material material, Transform parent)
        {
            string path = Folder + "/" + name + ".asset";
            if (!material || File.Exists(path)) throw new Exception("Missing path material or existing path asset: " + name);
            var samples = new List<Vector3> { points[0] };
            for (int i = 1; i < points.Count; i++)
            {
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(points[i - 1], points[i])));
                for (int k = 1; k <= steps; k++) samples.Add(Vector3.Lerp(points[i - 1], points[i], k / (float)steps));
            }
            var vertices = new Vector3[samples.Count * 2]; var uv = new Vector2[vertices.Length];
            var triangles = new int[(samples.Count - 1) * 6];
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 direction = samples[Mathf.Min(i + 1, samples.Count - 1)] - samples[Mathf.Max(i - 1, 0)]; direction.y = 0;
                Vector3 side = Vector3.Cross(Vector3.up, direction.normalized) * (width * .5f);
                for (int j = 0; j < 2; j++)
                {
                    Vector3 p = Ground(samples[i] + side * (j == 0 ? -1 : 1)) + Vector3.up * .035f;
                    vertices[i * 2 + j] = p; uv[i * 2 + j] = new Vector2(p.x * .2f, p.z * .2f);
                }
                if (i == samples.Count - 1) continue;
                int a = i * 2, t = i * 6;
                triangles[t] = a; triangles[t + 1] = a + 2; triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1; triangles[t + 4] = a + 2; triangles[t + 5] = a + 3;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
        }

        internal static Vector3[] ReadPath(string name)
        {
            var go = GameObject.Find(name);
            if (!go) return null;
            // Play Mode static batching can replace sharedMesh with a combined multi-route buffer.
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Folder + "/" + name + ".asset");
            if (!mesh || mesh.vertexCount < 4 || mesh.vertexCount % 2 != 0)
                throw new InvalidOperationException("Reviewed source footway mesh missing/invalid: " + name);
            var vertices = mesh.vertices;
            var points = new Vector3[vertices.Length / 2];
            for (int i = 0; i < points.Length; i++) points[i] = go.transform.TransformPoint((vertices[i * 2] + vertices[i * 2 + 1]) * .5f);
            return points;
        }

        static void Fail(string packet, Exception e)
        {
            Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + packet + "-error.txt", e.ToString());
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}
