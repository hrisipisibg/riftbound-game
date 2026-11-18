// Staged authoring/test code. Move into the own checkout's Editor folder after baseline import completes.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class WorldTraversalVerification
    {
        internal sealed class Route
        {
            internal string name;
            internal Vector3[] points;
            internal Route(string name, params Vector3[] points) { this.name = name; this.points = points; }
        }

        internal static Route[] Routes
        {
            get
            {
                var north = SettlementRoutePass.ReadPath("Highspire north footway") ?? new[] { new Vector3(-200,12,-60), new Vector3(-225,9,0) };
                var green = SettlementRoutePass.ReadPath("Greenhollow arrival footway") ?? new[] { new Vector3(-17,11,368), new Vector3(-40,13,400) };
                return new[]
                {
                    new Route("Emberwatch to Highspire", new Vector3(-18,0,0), new Vector3(-70,7,-35), new Vector3(-135,12,-55), new Vector3(-200,12,-60)),
                    new Route("Highspire to Westbank", north.Concat(new[] { new Vector3(-240,6,50), new Vector3(-260,6,50), new Vector3(-285,6,50) }).ToArray()),
                    new Route("Bridge crossing", new Vector3(-278,6,50), new Vector3(-235,6,50)),
                    new Route("Reaver approach", new Vector3(-285,6,50), RiverReaverCamp.Position),
                    new Route("Greenhollow arrival", green)
                };
            }
        }

        public static void Run()
        {
            try
            {
                if (!Application.isBatchMode || !File.Exists(".coordination/world-edit-context.md")) throw new Exception("Isolated batch with local edit authorization only.");
                EditorSceneManager.OpenScene(WorldRegions.ScenePath); Physics.SyncTransforms();
                var report = new StringBuilder($"WORLD TRAVERSAL GEOMETRY UTC={DateTime.UtcNow:O}\n");
                report.AppendLine("Real scene physics + CharacterController.Move; not actual input/animation/network/NavMesh acceptance. Mount includes current predictive box sweep.");
                var player = Object.FindFirstObjectByType<PlayerController>();
                var source = player ? player.GetComponent<CharacterController>() : null;
                report.AppendLine(source ? $"Source motor: height={source.height}, radius={source.radius}, slope={source.slopeLimit}, step={source.stepOffset}, skin={source.skinWidth}" : "Source motor absent; walk dimensions estimated.");
                foreach (var route in Routes)
                    foreach (bool mounted in new[] { false, true })
                        foreach (bool reverse in new[] { false, true })
                            Traverse(route, mounted, reverse, source, report);
                Directory.CreateDirectory(".code-check/overnight");
                File.WriteAllText(".code-check/overnight/traversal-geometry.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(".code-check/overnight");
                File.WriteAllText(".code-check/overnight/traversal-error.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        public static void CompareBridge()
        {
            try
            {
                if (!Application.isBatchMode || !File.Exists(".coordination/world-edit-context.md")) throw new Exception("Isolated batch with local edit authorization only.");
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var terrain = WorldRegions.TerrainAt(new Vector3(-255, 6, 50));
                var collider = terrain.GetComponent<TerrainCollider>();
                var current = terrain.terrainData;
                var baseline = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/RPG/Terrain/ReaverCamp/Reaver approach preserved final.asset");
                var source = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                var report = new StringBuilder("Bridge before/after real scene controller and mounted-box-sweep comparison. Temporary data assignment in memory only; no scene saved.\n");
                try
                {
                    foreach (bool repaired in new[] { false, true })
                    {
                        terrain.terrainData = repaired ? current : baseline;
                        collider.terrainData = terrain.terrainData;
                        terrain.Flush(); Physics.SyncTransforms();
                        var route = new Route(repaired ? "REPAIRED bridge" : "BASELINE bridge", new Vector3(-278,6,50), new Vector3(-235,6,50));
                        foreach (bool mounted in new[] { false, true })
                            foreach (bool reverse in new[] { false, true }) Traverse(route, mounted, reverse, source, report);
                    }
                }
                finally { terrain.terrainData = current; collider.terrainData = current; terrain.Flush(); }
                Directory.CreateDirectory(".code-check/overnight");
                File.WriteAllText(".code-check/overnight/bridge-controller-comparison.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(".code-check/overnight"); File.WriteAllText(".code-check/overnight/bridge-comparison-error.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        internal static void Traverse(Route route, bool mounted, bool reverse, CharacterController source, StringBuilder report)
        {
            var points = reverse ? route.points.Reverse().ToArray() : route.points;
            var go = new GameObject("Temporary route probe"); go.layer = 2;
            try
            {
                go.transform.position = Surface(points[0]) + Vector3.up * .12f;
                var cc = go.AddComponent<CharacterController>();
                cc.height = mounted ? 3.6f : (source ? source.height : 1.9f);
                cc.radius = mounted ? .62f : (source ? source.radius : .42f);
                cc.center = mounted ? Vector3.up * 1.8f : (source ? source.center : Vector3.up * .95f);
                cc.slopeLimit = source ? source.slopeLimit : 45;
                cc.stepOffset = source ? source.stepOffset : .3f;
                cc.skinWidth = source ? source.skinWidth : .08f;
                bool reached = true; string blocker = "none"; int stuck = 0; int target = 1;
                for (; target < points.Length; target++)
                {
                    int budget = Mathf.CeilToInt(Vector2.Distance(Flat(go.transform.position), Flat(points[target])) / .08f) + 150;
                    for (int i = 0; i < budget; i++)
                    {
                        Vector3 delta = points[target] - go.transform.position; delta.y = 0;
                        if (delta.magnitude < .4f) break;
                        go.transform.rotation = Quaternion.LookRotation(delta);
                        if (mounted && Physics.BoxCast(go.transform.position + Vector3.up * 1.3f, new Vector3(.58f, .5f, 1.25f), delta.normalized,
                                out RaycastHit hit, go.transform.rotation, .12f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        { blocker = PathOf(hit.collider.transform); reached = false; break; }
                        var before = go.transform.position;
                        cc.Move(delta.normalized * .08f + Vector3.down * .15f);
                        stuck = Vector2.Distance(Flat(before), Flat(go.transform.position)) < .005f ? stuck + 1 : 0;
                        if (stuck > 25)
                        {
                            var overlaps = Physics.OverlapCapsule(go.transform.position + Vector3.up * (cc.radius + .01f),
                                go.transform.position + Vector3.up * (cc.height - cc.radius), cc.radius + .15f, ~(1 << 2), QueryTriggerInteraction.Ignore);
                            blocker = string.Join(" | ", overlaps.Where(c => !(c is TerrainCollider)).Take(3).Select(c => PathOf(c.transform)));
                            if (blocker.Length == 0) blocker = "terrain slope/step or unsupported surface";
                            reached = false; break;
                        }
                    }
                    if (!reached || Vector2.Distance(Flat(go.transform.position), Flat(points[target])) >= .4f) { reached = false; break; }
                }
                report.AppendLine($"{(reached ? "REACHED" : "BLOCKED")} {route.name} mode={(mounted ? "mount" : "walk")} reverse={reverse} targetIndex={target} at={go.transform.position} blocker={blocker}");
            }
            finally { Object.DestroyImmediate(go); Physics.SyncTransforms(); }
        }

        static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);
        internal static Vector3 Surface(Vector3 p)
        {
            p.y = WorldRegions.GroundHeight(p);
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, p.y + 12, p.z), Vector3.down, 14, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (hit.collider.name == "Westbank bridge deck" && hit.point.y > p.y) p.y = hit.point.y;
            return p;
        }
        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            while (t) { parts.Add(t.name); t = t.parent; }
            parts.Reverse(); return string.Join("/", parts);
        }
    }
}
