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
    public static class VillageLandmarksPreview
    {
        const string Art = "Assets/RPG/Art/VillageLandmarks/";
        const string Civic = "Assets/RPG/Art/EmberwatchCivicSquare/";
        const string Output = WorldProductionAudit.Evidence + "village-landmarks/";
        static readonly Vector3 Emberwatch = new Vector3(43, 0, -40);
        static readonly Vector3 Greenhollow = new Vector3(-33, 0, 377);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Village landmarks on authored terrain. In-memory scene placement.\n");
                var square = AssetDatabase.LoadAssetAtPath<GameObject>(Civic + "Emberwatch Civic Square.prefab");
                if (!square) throw new InvalidOperationException("Emberwatch civic square prefab missing.");
                var squareInstance = (GameObject)PrefabUtility.InstantiatePrefab(square);
                squareInstance.transform.position = new Vector3(28, 0, -29);
                var squareColliders = squareInstance.GetComponentsInChildren<Collider>();
                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Civic + "Emberwatch Timber and Clay.mat");
                var stone = AssetDatabase.LoadAssetAtPath<Material>(Civic + "Emberwatch Stone.mat");
                if (!material || !stone || !material.mainTexture) throw new InvalidOperationException("Village materials missing.");

                var newParts = new List<Collider>();
                var ember = new GameObject("Emberwatch Supply Lane");
                ember.transform.position = Emberwatch;
                var green = new GameObject("Greenhollow Rest Stop");
                green.transform.position = Greenhollow;

                void Place(GameObject group, string name, float x, float z, float yaw, bool support = false)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx");
                    if (!model) throw new InvalidOperationException($"Missing village model {name}.");
                    var pivot = new GameObject(name).transform;
                    pivot.SetParent(group.transform);
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
                    float baseHeight = support ? ground.Max() - .015f : WorldRegions.GroundHeight(new Vector3(x, 0, z)) - .035f;
                    pivot.position += Vector3.up * (baseHeight - bounds.min.y);

                    if (support && baseHeight - ground.Min() > .045f)
                    {
                        float bottom = ground.Min() - .08f;
                        var baseObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        baseObject.name = "Stone ground support";
                        baseObject.transform.SetParent(group.transform);
                        baseObject.transform.position = new Vector3(x, (bottom + baseHeight) * .5f, z);
                        baseObject.transform.localScale = new Vector3(bounds.size.x * .70f, baseHeight - bottom, bounds.size.z * .67f);
                        baseObject.GetComponent<MeshRenderer>().sharedMaterial = stone;
                        newParts.Add(baseObject.GetComponent<BoxCollider>());
                    }

                    var collision = mesh.gameObject.AddComponent<MeshCollider>();
                    collision.sharedMesh = mesh.sharedMesh;
                    newParts.Add(collision);
                    report.AppendLine($"{name} x/z=({x:F2},{z:F2}) terrain={ground.Min():F2}..{ground.Max():F2}m " +
                        $"triangles={mesh.sharedMesh.GetIndexCount(0) / 3}");
                }

                Place(ember, "Emberwatch_LoadingShed", 44.1f, -37.9f, 180, support: true);
                Place(ember, "Emberwatch_SupplyWagon", 40.1f, -38.2f, -90);
                Place(ember, "Emberwatch_RoofedNoticeBoard", 39.5f, -43.8f, 0);
                Place(ember, "Emberwatch_HerbBeds", 45.1f, -44f, 10, support: true);
                Place(green, "Greenhollow_RestShelter", -33f, 377, 83, support: true);
                Place(green, "Greenhollow_HoneyStand", -36.8f, 375.8f, 65, support: true);
                Place(green, "Greenhollow_TrailLantern", -28.3f, 375.3f, 95);

                Physics.SyncTransforms();
                foreach (var collider in newParts)
                    SettlementPlacementPreview.ReportOverlap(collider, existing, report);

                var player = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                var supplyRoute = new WorldTraversalVerification.Route("Emberwatch square to supply yard",
                    new Vector3(29, 0, -40.9f), new Vector3(36, 0, -40.9f), new Vector3(43, 0, -40.9f));
                var greenRoute = new WorldTraversalVerification.Route("Greenhollow through road",
                    new Vector3(-17, 0, 368), new Vector3(-23, 0, 379), new Vector3(-27, 0, 386));
                var restApproach = new WorldTraversalVerification.Route("Greenhollow rest shelter entrance",
                    new Vector3(-25.6f, 0, 377.15f), new Vector3(-29, 0, 378.25f), new Vector3(-31.2f, 0, 377.7f));

                Vector3 SquareSurface(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    foreach (var collider in squareColliders)
                        if (collider.Raycast(new Ray(ground + Vector3.up * 3, Vector3.down), out var hit, 6) && hit.point.y > ground.y)
                            ground = hit.point;
                    return ground;
                }

                foreach (bool reverse in new[] { false, true })
                {
                    foreach (bool mounted in new[] { false, true })
                    {
                        WorldTraversalVerification.Traverse(supplyRoute, mounted, reverse, player, report, SquareSurface);
                        WorldTraversalVerification.Traverse(greenRoute, mounted, reverse, player, report);
                    }
                    WorldTraversalVerification.Traverse(restApproach, false, reverse, player, report);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportVillageLandmarkPrefabs") >= 0)
                {
                    foreach (var group in new[] { ember, green })
                    {
                        string path = Art + group.name + ".prefab";
                        if (File.Exists(path)) throw new IOException($"Prefab already exists: {path}");
                    }
                    foreach (var group in new[] { ember, green })
                    {
                        string path = Art + group.name + ".prefab";
                        var copy = Object.Instantiate(group);
                        copy.name = group.name;
                        copy.transform.position = Vector3.zero;
                        if (!PrefabUtility.SaveAsPrefabAsset(copy, path)) throw new IOException($"Could not save {path}");
                        Object.DestroyImmediate(copy);
                        report.AppendLine($"Created {path} at map anchor {group.transform.position.ToString("F2")}.");
                    }
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "emberwatch-supply-in-world.png",
                        new Vector3(52.7f, 10.1f, -50.6f), new Vector3(40.7f, 1.6f, -39.8f));
                    SettlementPlacementPreview.Capture(Output + "greenhollow-rest-in-world.png",
                        new Vector3(-22.8f, 18.2f, 370), new Vector3(-32.4f, 12.4f, 378));
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
