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
    public static class SilverglenArrivalCourtPreview
    {
        const string Art = "Assets/RPG/Art/SilverglenArrivalCourt/";
        const string Output = WorldProductionAudit.Evidence + "silverglen-arrival/";
        const string Prefab = Art + "Silverglen Arrival Court.prefab";
        static readonly Vector3 Anchor = new Vector3(-130, 0, 55);
        const float LaneX = -128f;

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Silverglen arrival court fit on the existing trail shoulder.\n");
                var existing = Object.FindObjectsByType<Collider>()
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Silverglen Pearl and Teal.mat");
                if (!material || !material.mainTexture || material.mainTexture.width != 768)
                    throw new InvalidOperationException("Silverglen kit atlas missing.");

                var root = new GameObject("Silverglen Arrival Court");
                root.transform.position = Anchor;
                var colliders = new List<MeshCollider>();
                var paving = new List<MeshCollider>();

                MeshCollider Place(string part, float x, float z, float yaw = 0)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Silverglen_{part}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Silverglen {part}.");
                    var pivot = new GameObject(part).transform;
                    pivot.SetParent(root.transform);
                    pivot.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh for {part}.");
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var b = renderer.bounds;
                    var grade = new List<float>();
                    foreach (float px in new[] { b.min.x, x, b.max.x })
                    foreach (float pz in new[] { b.min.z, z, b.max.z })
                        grade.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));
                    if (grade.Max() - grade.Min() > .12f)
                        throw new InvalidOperationException($"{part} exceeds the grounding tolerance: {grade.Min():F2}..{grade.Max():F2}m.");
                    pivot.position += Vector3.up * (grade.Min() - .011f - b.min.y);
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    colliders.Add(collider);
                    report.AppendLine($"{part}: ({x:F2},{z:F2}), terrain {grade.Min():F2}..{grade.Max():F2}m, " +
                        $"{mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                    return collider;
                }

                var pavilion = Place("ReadingPavilion", -134.1f, 54.5f, 90);
                Place("HerbBasin", -123.6f, 55.9f);
                Place("CrescentBench", -123.6f, 54f);
                Place("TrailBeacon", -130.8f, 60.4f);
                Place("TrailBeacon", -125.25f, 60.4f);
                foreach (float z in new[] { 52.5f, 55.5f, 58.5f, 61.5f, 64.5f, 67.5f })
                    paving.Add(Place("InlayPaving", LaneX, z));

                Physics.SyncTransforms();
                foreach (var collider in colliders)
                    SettlementPlacementPreview.ReportOverlap(collider, existing, report);

                Vector3 Surface(Vector3 p)
                {
                    var ground = WorldTraversalVerification.Surface(p);
                    foreach (var piece in paving)
                        if (piece.Raycast(new Ray(ground + Vector3.up * 2, Vector3.down), out var hit, 4) && hit.point.y > ground.y)
                            ground = hit.point;
                    if (pavilion.Raycast(new Ray(ground + Vector3.up * 1.1f, Vector3.down), out var top, 2.2f) &&
                        top.point.y > ground.y)
                        ground = top.point;
                    return ground;
                }

                var player = Object.FindAnyObjectByType<PlayerController>().GetComponent<CharacterController>();
                var trail = new WorldTraversalVerification.Route("Silverglen trail-to-court",
                    new Vector3(-126.8f, 0, 72.4f), new Vector3(LaneX, 0, 65f), new Vector3(LaneX, 0, 58.5f),
                    new Vector3(LaneX, 0, 51.45f));
                var reading = new WorldTraversalVerification.Route("Silverglen pavilion threshold",
                    new Vector3(LaneX, 0, 54.5f), new Vector3(-130.1f, 0, 54.5f), new Vector3(-132.5f, 0, 54.5f));
                var garden = new WorldTraversalVerification.Route("Silverglen herb court access",
                    new Vector3(LaneX, 0, 57.8f), new Vector3(-124.6f, 0, 57.8f), new Vector3(-122.4f, 0, 57.8f));
                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(trail, false, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(trail, true, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(reading, false, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(garden, false, reverse, player, report, Surface);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportSilverglenArrivalCourt") >= 0)
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
                    SettlementPlacementPreview.Capture(Output + "silverglen-in-world.png",
                        new Vector3(-116.5f, 25.5f, 66.4f), new Vector3(-130, 16.3f, 56));
                    SettlementPlacementPreview.Capture(Output + "silverglen-approach.png",
                        new Vector3(-127.6f, 17.8f, 66), new Vector3(-130.1f, 16.5f, 54));
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
