using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class BloodrockLookoutPreview
    {
        const string Art = "Assets/RPG/Art/BloodrockLookout/";
        const string Output = WorldProductionAudit.Evidence + "bloodrock-lookout/";
        const string Prefab = Art + "Bloodrock Trail Lookout.prefab";
        static readonly Vector3 Anchor = new Vector3(294, 0, 184.5f);
        static readonly Vector3 DeckCenter = new Vector3(299.1f, 0, 183.2f);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Bloodrock trail lookout on the southeast approach.\n");
                var existing = Object.FindObjectsByType<Collider>()
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Bloodrock Sandstone and Hide.mat");
                if (!material || !material.mainTexture || material.mainTexture.width != 768)
                    throw new InvalidOperationException("Bloodrock sandstone atlas missing.");

                var root = new GameObject("Bloodrock Trail Lookout");
                root.transform.position = Anchor;
                var solids = new List<MeshCollider>();

                MeshCollider Place(string part, float x, float z, float yaw = 0)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Bloodrock_{part}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Bloodrock model {part}.");
                    var pivot = new GameObject(part).transform;
                    pivot.SetParent(root.transform);
                    pivot.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh for {part}.");
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var bounds = renderer.bounds;
                    var heights = new List<float>();
                    foreach (float px in new[] { bounds.min.x, x, bounds.max.x })
                    foreach (float pz in new[] { bounds.min.z, z, bounds.max.z })
                        heights.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));
                    if (heights.Max() - heights.Min() > .22f)
                        throw new InvalidOperationException($"{part} requires terrain fitting: {heights.Min():F2}–{heights.Max():F2}m");
                    pivot.position += Vector3.up * (heights.Min() - .012f - bounds.min.y);
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    solids.Add(collider);
                    report.AppendLine($"{part}: ({x:F2},{z:F2}), terrain {heights.Min():F2}–{heights.Max():F2}m, " +
                        $"{mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                    return collider;
                }

                var lookout = Place("VistaLookout", DeckCenter.x, DeckCenter.z);
                Place("TrailSupplyStand", 290, 183.4f, 90);
                Place("RestBay", 289.95f, 188.5f, 95);
                Place("OutpostBanner", 291.7f, 189.9f, 0);
                Place("OutpostBanner", 296.05f, 189.6f, 0);

                Physics.SyncTransforms();
                foreach (var solid in solids)
                    SettlementPlacementPreview.ReportOverlap(solid, existing, report);

                Vector3 Surface(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    if (lookout.Raycast(new Ray(ground + Vector3.up * 3.55f, Vector3.down), out var hit, 6) &&
                        hit.point.y > ground.y)
                        return hit.point;
                    return ground;
                }

                float rise = Surface(DeckCenter).y - WorldTraversalVerification.Surface(DeckCenter).y;
                report.AppendLine(rise > 2.10f && rise < 2.22f ? $"Lookout deck height: {rise:F3}m." : $"BLOCKED deck height: {rise:F3}m.");
                var player = Object.FindAnyObjectByType<PlayerController>();
                var motor = player.GetComponent<CharacterController>();
                var lane = new WorldTraversalVerification.Route("Bloodrock outpost through lane",
                    new Vector3(294.7f, 0, 200.5f), new Vector3(294.7f, 0, 193f), new Vector3(294.25f, 0, 188.7f),
                    new Vector3(294f, 0, 180.5f));
                var climb = new WorldTraversalVerification.Route("Bloodrock lookout climb",
                    new Vector3(294.5f, 0, 193.5f), new Vector3(299.1f, 0, 191.3f), new Vector3(299.1f, 0, 187.6f),
                    new Vector3(299.1f, 0, 184.1f), DeckCenter);
                var vistaLink = new WorldTraversalVerification.Route("Bloodrock Vista foot link",
                    new Vector3(294, 0, 180.5f), new Vector3(293.4f, 0, 179.1f), new Vector3(288.1f, 0, 179.1f),
                    new Vector3(286, 0, 184), new Vector3(280, 0, 185), new Vector3(274, 0, 188),
                    new Vector3(272.8f, 0, 191), new Vector3(270, 0, 190), new Vector3(269.1f, 0, 191.2f));
                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(lane, false, reverse, motor, report);
                    WorldTraversalVerification.Traverse(lane, true, reverse, motor, report);
                    WorldTraversalVerification.Traverse(climb, false, reverse, motor, report, Surface);
                    WorldTraversalVerification.Traverse(vistaLink, false, reverse, motor, report);
                }
                ReportFollowCamera(lane, WorldTraversalVerification.Surface, 2.4f, report);
                ReportFollowCamera(climb, Surface, player.EyeHeight, report);

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportBloodrockLookout") >= 0)
                {
                    if (File.Exists(Prefab)) throw new IOException($"Already exists: {Prefab}.");
                    var copy = Object.Instantiate(root);
                    copy.name = root.name;
                    copy.transform.position = Vector3.zero;
                    if (!PrefabUtility.SaveAsPrefabAsset(copy, Prefab)) throw new IOException($"Could not save {Prefab}.");
                    Object.DestroyImmediate(copy);
                    report.AppendLine($"Created {Prefab} at {Anchor.ToString("F2")}.");
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "bloodrock-lookout-in-world.png",
                        new Vector3(305.5f, 28.5f, 194f), new Vector3(294.1f, 19.2f, 185.6f));
                    SettlementPlacementPreview.Capture(Output + "bloodrock-from-deck.png",
                        new Vector3(299.02f, 21.9f, 183.35f), new Vector3(302.4f, 20.3f, 170.5f));
                }
                EditorApplication.Exit(blocked ? 1 : 0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        // Mirrors the normal follow-camera cast with neutral pitch along the sampled route.
        static void ReportFollowCamera(WorldTraversalVerification.Route route, Func<Vector3, Vector3> ground,
            float eyeHeight, StringBuilder report)
        {
            float shortest = 7;
            int samples = 0;
            for (int i = 1; i < route.points.Length; i++)
            {
                var a = route.points[i - 1];
                var b = route.points[i];
                int steps = Mathf.CeilToInt(Vector3.Distance(a, b));
                float heading = Quaternion.LookRotation(b - a).eulerAngles.y;
                foreach (float yaw in new[] { heading, heading + 180 })
                for (int step = 0; step <= steps; step++)
                {
                    var pivot = ground(Vector3.Lerp(a, b, step / (float)steps)) + Vector3.up * eyeHeight;
                    var backwards = Quaternion.Euler(23, yaw, 0) * Vector3.back;
                    if (Physics.SphereCast(pivot, .2f, backwards, out var hit, 7, ~(1 << 2), QueryTriggerInteraction.Ignore))
                        shortest = Mathf.Min(shortest, Mathf.Max(.6f, hit.distance - .15f));
                    samples++;
                }
            }
            report.AppendLine($"{route.name} follow-camera geometry: {samples} samples, shortest offset {shortest:F2}m of 7m.");
        }
    }
}
