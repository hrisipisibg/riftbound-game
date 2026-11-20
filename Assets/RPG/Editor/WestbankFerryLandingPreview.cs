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
    public static class WestbankFerryLandingPreview
    {
        const string Art = "Assets/RPG/Art/WestbankFerryLanding/";
        const string Output = WorldProductionAudit.Evidence + "westbank-ferry-landing/";
        const string Prefab = Art + "Westbank Ferry Landing.prefab";
        internal static readonly Vector3 Anchor = new Vector3(-270, 0, 41);
        const float DockX = -261.4f;
        const float DockY = 3.90f;
        const float RampTop = 5.70f;

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Westbank south-bank ferry landing on the authored shoreline.\n");
                var existing = Object.FindObjectsByType<Collider>()
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var cedar = AssetDatabase.LoadAssetAtPath<Material>(Art + "Westbank Cedar and Willow.mat");
                if (!cedar || !cedar.mainTexture || cedar.mainTexture.width != 768)
                    throw new InvalidOperationException("Westbank atlas material missing.");

                var river = GameObject.Find("Westbank flowing river");
                if (!river || Mathf.Abs(river.GetComponent<Renderer>().bounds.center.y - 3.25f) > .01f)
                    throw new InvalidOperationException("The ferry landing expects the authored 3.25m river surface.");

                var root = new GameObject("Westbank Ferry Landing");
                root.transform.position = Anchor;
                var solids = new List<MeshCollider>();

                MeshFilter Place(string name, Vector3 at, Vector3 scale, float yaw)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Westbank_{name}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Westbank model {name}.");
                    var pivot = new GameObject(name).transform;
                    pivot.SetParent(root.transform);
                    pivot.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
                    pivot.localScale = scale;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh for {name}.");
                    mesh.GetComponent<MeshRenderer>().sharedMaterial = cedar;
                    report.AppendLine($"{name}: origin {at.ToString("F2")}, {mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                    return mesh;
                }

                MeshCollider Collision(MeshFilter mesh)
                {
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    solids.Add(collider);
                    return collider;
                }

                void PlaceOnBank(string name, float x, float z, float yaw = 0)
                {
                    var at = new Vector3(x, 0, z);
                    at.y = WorldRegions.GroundHeight(at) - .015f;
                    var mesh = Place(name, at, Vector3.one, yaw);
                    var b = mesh.GetComponent<Renderer>().bounds;
                    foreach (float px in new[] { b.min.x, x, b.max.x })
                    foreach (float pz in new[] { b.min.z, z, b.max.z })
                        if (Mathf.Abs(WorldRegions.GroundHeight(new Vector3(px, 0, pz)) - at.y) > .21f)
                            throw new InvalidOperationException($"{name} needs a separate bank footing at ({px:F2},{pz:F2}).");
                    Collision(mesh);
                }

                PlaceOnBank("RopewayShelter", -279.8f, 46.8f);
                PlaceOnBank("CargoHoist", -274.5f, 47f, -12);
                var dock = Collision(Place("LandingDock", new Vector3(DockX, DockY, 30.6f), new Vector3(.72f, 1, 1), 0));
                var ramp = Collision(Place("ShoreRamp", new Vector3(DockX, DockY, 37), new Vector3(.72f, (RampTop - DockY) / 1.54f, 1), 0));
                var skiff = Place("WillowSkiff", new Vector3(-258.6f, 3.25f, 30.8f), Vector3.one, -10);
                var rope = new GameObject("Skiff mooring line").AddComponent<LineRenderer>();
                rope.transform.SetParent(root.transform, false);
                rope.useWorldSpace = false;
                rope.widthMultiplier = .032f;
                rope.shadowCastingMode = ShadowCastingMode.Off;
                rope.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
                if (!rope.sharedMaterial) throw new InvalidOperationException("Westbank mooring material missing.");
                var tie = new[] { new Vector3(-260.16f, 4.18f, 28.88f), new Vector3(-259.73f, 3.73f, 28.79f),
                    new Vector3(-259.20f, 3.54f, 28.75f), new Vector3(-258.95f, 3.60f, 28.78f) };
                rope.positionCount = tie.Length;
                rope.SetPositions(tie.Select(root.transform.InverseTransformPoint).ToArray());

                Physics.SyncTransforms();
                foreach (var solid in solids)
                    SettlementPlacementPreview.ReportOverlap(solid, existing, report);

                var waterline = skiff.GetComponent<MeshRenderer>().bounds;
                float bed = WorldRegions.GroundHeight(new Vector3(waterline.center.x, 0, waterline.center.z));
                if (!(bed < waterline.min.y - .25f && waterline.min.y < 3.25f && waterline.max.y > 3.25f))
                    throw new InvalidOperationException("The skiff does not sit clear of the riverbed at the current waterline.");
                report.AppendLine($"Skiff center terrain: {bed:F2}m, hull from {waterline.min.y:F2} to {waterline.max.y:F2}m.");

                var surfaces = new[] { ramp, dock };
                Vector3 Surface(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    foreach (var surface in surfaces)
                        if (surface.Raycast(new Ray(new Vector3(point.x, 9, point.z), Vector3.down), out var hit, 9) &&
                            hit.point.y > ground.y)
                            ground = hit.point;
                    return ground;
                }

                float deckSide = Surface(new Vector3(DockX, 0, 33.36f)).y;
                float rampSide = Surface(new Vector3(DockX, 0, 33.44f)).y;
                bool seam = Mathf.Abs(deckSide - rampSide) < .035f && Mathf.Abs(deckSide - DockY) < .03f;
                report.AppendLine($"{(seam ? "PASS" : "BLOCKED")} ramp/deck seam: {rampSide:F3}m to {deckSide:F3}m.");

                var player = Object.FindAnyObjectByType<PlayerController>().GetComponent<CharacterController>();
                var road = new WorldTraversalVerification.Route("Westbank bridge bypassing landing",
                    new Vector3(-282, 0, 50.1f), new Vector3(-268, 0, 50.1f), new Vector3(-255, 0, 50.1f),
                    new Vector3(-239, 0, 50.1f));
                var dockWay = new WorldTraversalVerification.Route("Westbank road to dock",
                    new Vector3(-279.7f, 0, 50.1f), new Vector3(-272, 0, 50.1f), new Vector3(-272, 0, 47.2f),
                    new Vector3(-267.2f, 0, 42.4f), new Vector3(-265.9f, 0, 40.45f), new Vector3(DockX, 0, 40.6f),
                    new Vector3(DockX, 0, 36.1f), new Vector3(DockX, 0, 30));
                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(road, false, reverse, player, report);
                    WorldTraversalVerification.Traverse(road, true, reverse, player, report);
                    WorldTraversalVerification.Traverse(dockWay, false, reverse, player, report, Surface);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportWestbankFerryLanding") >= 0)
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
                    SettlementPlacementPreview.Capture(Output + "westbank-landing-in-world.png",
                        new Vector3(-247.5f, 14.8f, 25.8f), new Vector3(-263.2f, 4.9f, 37.9f));
                    SettlementPlacementPreview.Capture(Output + "westbank-shelter-in-world.png",
                        new Vector3(-273, 11.5f, 55.2f), new Vector3(-278.2f, 6.8f, 46.7f));
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
