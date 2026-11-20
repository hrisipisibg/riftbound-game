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
    public static class FrostpeakTrailStationPreview
    {
        const string Art = "Assets/RPG/Art/FrostpeakTrailStation/";
        const string Output = WorldProductionAudit.Evidence + "frostpeak-trail-station/";
        const string Prefab = Art + "Frostpeak Trail Station.prefab";
        static readonly Vector3 Anchor = new Vector3(-112, 0, -109);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Frostpeak trail station on the authored west spur.\n");
                var existing = Object.FindObjectsByType<Collider>()
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var stone = AssetDatabase.LoadAssetAtPath<Material>(Art + "Frostpeak Granite and Copper.mat");
                if (!stone || !stone.mainTexture || stone.mainTexture.width != 768)
                    throw new InvalidOperationException("Frostpeak's granite atlas is missing.");

                var root = new GameObject("Frostpeak Trail Station");
                root.transform.position = Anchor;
                var parts = new List<MeshCollider>();

                MeshCollider Place(string name, float x, float z, float yaw = 0)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Frostpeak_{name}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Frostpeak model {name}.");
                    var pivot = new GameObject(name).transform;
                    pivot.SetParent(root.transform);
                    pivot.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh for {name}.");
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = stone;
                    var bounds = renderer.bounds;
                    var heights = new List<float>();
                    foreach (float px in new[] { bounds.min.x, x, bounds.max.x })
                    foreach (float pz in new[] { bounds.min.z, z, bounds.max.z })
                        heights.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));
                    if (heights.Max() - heights.Min() > .12f)
                        throw new InvalidOperationException($"{name} needs individual grade fitting.");
                    pivot.position += Vector3.up * (heights.Min() - .014f - bounds.min.y);
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    parts.Add(collider);
                    report.AppendLine($"{name}: ({x:F2},{z:F2}) terrain {heights.Min():F2}..{heights.Max():F2}m, " +
                        $"{mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                    return collider;
                }

                var forge = Place("PassForge", -118.05f, -110.5f, 90);
                var shelter = Place("WindbreakShelter", -106.3f, -110.75f, -90);
                Place("PackedSupplies", -105.8f, -106.45f, -80);
                Place("PassMilepost", -114.9f, -103.5f, 172);
                Place("PassMilepost", -109.2f, -104.2f, 190);
                var terrace = Place("TrailTerrace", Anchor.x, Anchor.z);

                Physics.SyncTransforms();
                foreach (var part in parts)
                    SettlementPlacementPreview.ReportOverlap(part, existing, report);

                var floors = new[] { terrace, forge, shelter };
                Vector3 Surface(Vector3 p)
                {
                    var ground = WorldTraversalVerification.Surface(p);
                    foreach (var support in floors)
                        if (support.Raycast(new Ray(ground + Vector3.up * 1.05f, Vector3.down), out var hit, 2.10f) &&
                            hit.point.y > ground.y)
                            ground = hit.point;
                    return ground;
                }

                float rise = Surface(Anchor).y - Surface(Anchor + Vector3.forward * 6.25f).y;
                report.AppendLine(rise > .79f && rise < .90f ? $"Trail rise: {rise:F3}m." : $"BLOCKED trail rise: {rise:F3}m.");
                var player = Object.FindAnyObjectByType<PlayerController>().GetComponent<CharacterController>();
                var approach = new WorldTraversalVerification.Route("Frostpeak spur and station terrace",
                    new Vector3(-123.7f, 0, -104.45f), new Vector3(-119.2f, 0, -102.1f), new Vector3(-112, 0, -101.9f),
                    new Vector3(-112, 0, -110), new Vector3(-112, 0, -115.7f));
                var workBay = new WorldTraversalVerification.Route("Frostpeak forge apron",
                    new Vector3(-119, 0, -102.3f), new Vector3(-115.3f, 0, -106.3f),
                    new Vector3(-115.2f, 0, -110.1f), new Vector3(-116.15f, 0, -110.1f));
                var restBay = new WorldTraversalVerification.Route("Frostpeak windbreak entrance",
                    new Vector3(-108.85f, 0, -107.3f), new Vector3(-108.8f, 0, -110.75f),
                    new Vector3(-107.3f, 0, -110.75f));
                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(approach, false, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(approach, true, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(workBay, false, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(restBay, false, reverse, player, report, Surface);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportFrostpeakTrailStation") >= 0)
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
                    SettlementPlacementPreview.Capture(Output + "frostpeak-in-world.png",
                        new Vector3(-101.2f, 27.5f, -99.4f), new Vector3(-112.4f, 17.6f, -109.6f));
                    SettlementPlacementPreview.Capture(Output + "frostpeak-approach.png",
                        new Vector3(-112.35f, 18.65f, -100.3f), new Vector3(-112.9f, 18.25f, -110.0f));
                }
                EditorApplication.Exit(blocked ? 1 : 0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
