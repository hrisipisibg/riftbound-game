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
    // The batch preview places the kit in memory on the authored Highspire map.
    public static class HighspireDistrictPreview
    {
        const string Art = "Assets/RPG/Art/HighspireDistrict/";
        const string Output = WorldProductionAudit.Evidence + "highspire-district/";
        const string Prefab = Art + "Highspire East Ward.prefab";
        const float Street = 12.30f;
        const float Deck = Street + 1.244f;

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Highspire east ward. Temporary models on the authored map.\n");
                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "highspire_atlas.png");
                if (!atlas || atlas.width != 1536 || atlas.height != 1536)
                    throw new InvalidOperationException($"Highspire atlas dimensions: {atlas?.width} x {atlas?.height}.");
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Highspire District.mat");
                var masonry = AssetDatabase.LoadAssetAtPath<Material>(Art + "Highspire Foundation.mat");
                if (!material || material.mainTexture != atlas || !masonry || masonry.mainTexture != atlas)
                    throw new InvalidOperationException("Highspire's shared materials must reference the district atlas.");

                var root = new GameObject("Highspire east ward preview").transform;
                root.position = new Vector3(-138, Street, -49);
                var models = new List<MeshCollider>();
                MeshCollider Place(string part, float x, float y, float z, float yaw = 180)
                {
                    var path = Art + "Highspire_" + part + ".fbx";
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!model) throw new InvalidOperationException($"Missing model {path}.");
                    var anchor = new GameObject(part).transform;
                    anchor.SetParent(root);
                    anchor.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, anchor);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh at {path}.");
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    var bounds = renderer.bounds;
                    var ground = new List<float>();
                    foreach (float px in new[] { bounds.min.x, bounds.center.x, bounds.max.x })
                    foreach (float pz in new[] { bounds.min.z, bounds.center.z, bounds.max.z })
                        ground.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));
                    report.AppendLine($"{part}: {mesh.sharedMesh.GetIndexCount(0) / 3} triangles; " +
                        $"bounds {bounds.min.ToString("F2")}–{bounds.max.ToString("F2")}; ground {ground.Min():F2}–{ground.Max():F2}m");
                    models.Add(collider);
                    return collider;
                }

                void Foundation(string name, float x, float z, float width, float length, float bottom, float top)
                {
                    var footing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    footing.name = name;
                    footing.transform.SetParent(root);
                    footing.transform.position = new Vector3(x, (bottom + top) * .5f, z);
                    footing.transform.localScale = new Vector3(width, top - bottom, length);
                    footing.GetComponent<MeshRenderer>().sharedMaterial = masonry;
                }

                Place("Gatehouse", -138, Street, -49);
                for (int i = 0; i < 3; i++)
                    Place("CobbledLane4m", -138, Street, -55.5f + 4 * i);
                var ramp = Place("CartRamp", -130.6f, Street, -38.4f);
                var platform = Place("MarketTerrace", -130.6f, Street, -38.4f);
                Place("GuildHall", -130.6f, Deck, -31.6f);
                Place("MarketLoggia", -140.3f, 13.50f, -31.9f);
                Place("Workshop", -148.0f, 12.48f, -45.0f, 270);
                Place("Fountain", -142.2f, 12.95f, -38.3f);
                Foundation("West gatehouse footing", -142.58f, -49, 4.02f, 4.05f, 11.62f, Street - .006f);
                Foundation("East gatehouse footing", -133.42f, -49, 4.02f, 4.05f, 11.62f, Street - .006f);
                Foundation("Gate lane stone subgrade", -138f, -49.5f, 4.36f, 11.98f, 11.70f, Street + .018f);
                Foundation("Guild hall rear retaining wall", -130.6f, -30.75f, 8.23f, 4.75f, 11.60f, Deck - .01f);
                Foundation("Market loggia plinth", -140.3f, -31.85f, 8.8f, 2.68f, 12.32f, 13.488f);
                Foundation("Workshop foundation", -148.0f, -45.0f, 5.25f, 6.3f, 11.64f, 12.465f);
                Foundation("Fountain footing", -142.2f, -38.3f, 2.17f, 2.17f, 12.38f, 12.94f);

                Physics.SyncTransforms();
                foreach (var model in models)
                    SettlementPlacementPreview.ReportOverlap(model, existing, report);

                Vector3 Surface(Vector3 point)
                {
                    var surface = WorldTraversalVerification.Surface(point);
                    foreach (var model in models)
                    {
                        if (model.Raycast(new Ray(new Vector3(point.x, point.y + 30, point.z), Vector3.down), out var hit, 100)
                            && hit.point.y > surface.y)
                            surface = hit.point;
                    }
                    return surface;
                }

                var rampTop = new Vector3(-130.6f, Deck + 5, -38.4f);
                bool rampHit = ramp.Raycast(new Ray(rampTop + Vector3.back * .05f, Vector3.down), out var rampEnd, 10);
                bool deckHit = platform.Raycast(new Ray(rampTop + Vector3.forward * .05f, Vector3.down), out var deckStart, 10);
                bool seamOk = rampHit && deckHit && Mathf.Abs(rampEnd.point.y - deckStart.point.y) < .03f;
                report.AppendLine($"Ramp seam: {(seamOk ? "PASS" : "BLOCKED")} (ramp={rampEnd.point.y:F3}, deck={deckStart.point.y:F3})");

                var player = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                var gateRoute = new WorldTraversalVerification.Route("Gate lane",
                    new Vector3(-138, 0, -55.0f), new Vector3(-138, 0, -49), new Vector3(-138, 0, -44.1f));
                var rampRoute = new WorldTraversalVerification.Route("Market ramp",
                    new Vector3(-130.6f, 0, -44.8f), new Vector3(-130.6f, 0, -38.4f), new Vector3(-130.6f, 0, -36.7f));
                foreach (var route in new[] { gateRoute, rampRoute })
                foreach (bool mounted in new[] { false, true })
                foreach (bool reverse in new[] { false, true })
                    WorldTraversalVerification.Traverse(route, mounted, reverse, player, report, Surface);

                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-exportHighspirePrefab") >= 0 &&
                    !report.ToString().Contains("BLOCKED"))
                {
                    if (File.Exists(Prefab)) throw new InvalidOperationException("The Highspire prefab already exists.");
                    // Save an origin-relative copy. The authored map scene stays unsaved.
                    var kit = Object.Instantiate(root.gameObject);
                    kit.name = "Highspire East Ward";
                    kit.transform.position = Vector3.zero;
                    if (!PrefabUtility.SaveAsPrefabAsset(kit, Prefab)) throw new IOException($"Could not create {Prefab}.");
                    Object.DestroyImmediate(kit);
                    report.AppendLine($"Created {Prefab}; authored map anchor {root.position.ToString("F2")}.");
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "highspire-overview.png",
                        new Vector3(-108, 33, -62), new Vector3(-137, 14, -41));
                    SettlementPlacementPreview.Capture(Output + "highspire-gate.png",
                        new Vector3(-139, 15.3f, -60), new Vector3(-138, 15.5f, -46));
                }
                EditorApplication.Exit(report.ToString().Contains("BLOCKED") ? 1 : 0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
