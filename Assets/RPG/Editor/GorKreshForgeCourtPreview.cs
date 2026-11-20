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
    public static class GorKreshForgeCourtPreview
    {
        const string Art = "Assets/RPG/Art/GorKreshForgeCourt/";
        const string Output = WorldProductionAudit.Evidence + "gorkresh-forge-court/";
        const string Prefab = Art + "GorKresh Forge Court.prefab";
        static readonly Vector3 Anchor = new Vector3(340, 0, 269);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Gor'Kresh forge court on the existing south apron.\n");
                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "GorKresh Charcoal and Rust.mat");
                if (!material || !material.mainTexture || material.mainTexture.width != 768)
                    throw new InvalidOperationException("Gor'Kresh atlas material missing.");

                var root = new GameObject("Gor'Kresh Forge Court");
                root.transform.position = Anchor;
                var parts = new List<MeshCollider>();

                void Place(string name, float x, float z, float yaw)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + "GorKresh_" + name + ".fbx");
                    if (!model) throw new InvalidOperationException($"Missing forge model {name}.");
                    var pivot = new GameObject(name).transform;
                    pivot.SetParent(root.transform);
                    pivot.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh for {name}.");
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var bounds = renderer.bounds;
                    var ground = new List<float>();
                    foreach (float px in new[] { bounds.min.x, x, bounds.max.x })
                    foreach (float pz in new[] { bounds.min.z, z, bounds.max.z })
                        ground.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));
                    if (ground.Max() - ground.Min() > .12f)
                        throw new InvalidOperationException($"{name} needs a separate terrain fitting at {x},{z}.");
                    pivot.position += Vector3.up * (ground.Max() - bounds.min.y - .018f);
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    parts.Add(collider);
                    report.AppendLine($"{name}: x/z=({x:F1},{z:F1}), bounds x={bounds.min.x:F2}..{bounds.max.x:F2} " +
                        $"z={bounds.min.z:F2}..{bounds.max.z:F2}, grade={ground.Min():F2}..{ground.Max():F2}m, {mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                }

                Place("ForgeArch", 340, 263, 180);
                Place("KilnFurnace", 333.4f, 270.5f, 90);
                Place("QuartermasterShed", 347, 271, -90);
                Place("AnvilStation", 334.2f, 264.6f, 90);
                Place("CharcoalBin", 329.1f, 271, 10);

                Physics.SyncTransforms();
                foreach (var part in parts)
                    SettlementPlacementPreview.ReportOverlap(part, existing, report);

                var player = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                var lane = new WorldTraversalVerification.Route("Gor'Kresh forge court lane",
                    new Vector3(340, 0, 257), new Vector3(340, 0, 264), new Vector3(340, 0, 275.5f));
                var shed = new WorldTraversalVerification.Route("Gor'Kresh supply shed approach",
                    new Vector3(340, 0, 270.6f), new Vector3(342, 0, 270.6f), new Vector3(345.5f, 0, 270.6f));
                var kiln = new WorldTraversalVerification.Route("Gor'Kresh kiln apron",
                    new Vector3(340, 0, 271.4f), new Vector3(338, 0, 271.4f), new Vector3(336.5f, 0, 271.4f));

                Vector3 Surface(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    foreach (var collider in parts)
                        if (collider.Raycast(new Ray(ground + Vector3.up * 1.5f, Vector3.down), out var hit, 3) &&
                            hit.point.y > ground.y)
                            ground = hit.point;
                    return ground;
                }

                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(lane, false, reverse, player, report);
                    WorldTraversalVerification.Traverse(lane, true, reverse, player, report);
                    WorldTraversalVerification.Traverse(shed, false, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(kiln, false, reverse, player, report, Surface);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportGorKreshForgeCourt") >= 0)
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
                    SettlementPlacementPreview.Capture(Output + "gorkresh-court-in-world.png",
                        new Vector3(354, 38, 256), new Vector3(339.3f, 27.0f, 268));
                    SettlementPlacementPreview.Capture(Output + "gorkresh-gate-in-world.png",
                        new Vector3(342.6f, 27.8f, 257.5f), new Vector3(338.1f, 27.1f, 271.5f));
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
