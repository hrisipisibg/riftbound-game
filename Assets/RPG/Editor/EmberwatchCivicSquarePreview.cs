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
    /// <summary>Fits the civic square in memory and preserves the authored Emberwatch scene.</summary>
    public static class EmberwatchCivicSquarePreview
    {
        const string Art = "Assets/RPG/Art/EmberwatchCivicSquare/";
        const string Output = WorldProductionAudit.Evidence + "emberwatch-civic-square/";
        const string Prefab = Art + "Emberwatch Civic Square.prefab";
        static readonly Vector3 Anchor = new Vector3(28, 0, -29);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Emberwatch Civic Square fit on the authored map. Scene preview only.\n");
                var before = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Emberwatch Timber and Clay.mat");
                var footing = AssetDatabase.LoadAssetAtPath<Material>(Art + "Emberwatch Stone.mat");
                if (!material || !footing || !material.mainTexture || material.mainTexture.width != 768)
                    throw new InvalidOperationException("Emberwatch materials or 768px atlas missing.");

                var root = new GameObject("Emberwatch Civic Square").transform;
                root.position = Anchor;
                var pieces = new List<Collider>();
                foreach (var terrain in Terrain.activeTerrains)
                foreach (var tree in terrain.terrainData.treeInstances)
                {
                    var world = terrain.transform.position + Vector3.Scale(terrain.terrainData.size, tree.position);
                    if (world.x >= 15 && world.x <= 43 && world.z >= -50 && world.z <= -22)
                        report.AppendLine($"Retained terrain tree {tree.prototypeIndex}: {world.ToString("F2")}, width scale={tree.widthScale:F2}.");
                }

                void Place(string part, float x, float z, float yaw, bool lane = false)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Emberwatch_{part}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Emberwatch {part}.");
                    var pivot = new GameObject(part).transform;
                    pivot.SetParent(root);
                    pivot.SetPositionAndRotation(new Vector3(x, 0, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured Emberwatch mesh: {part}.");
                    var renderer = mesh.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    var b = renderer.bounds;
                    var samples = new List<float>();
                    foreach (float px in new[] { b.min.x, b.center.x, b.max.x })
                    foreach (float pz in new[] { b.min.z, b.center.z, b.max.z })
                        samples.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));

                    if (lane)
                    {
                        float Height(float sx, float sz) => WorldRegions.GroundHeight(new Vector3(sx, 0, sz));
                        float rear = Height(x, z - 2);
                        float front = Height(x, z + 2);
                        var normal = new Vector3(0, 4, rear - front).normalized;
                        pivot.SetPositionAndRotation(new Vector3(x, (rear + front) * .5f - .015f, z),
                            Quaternion.FromToRotation(Vector3.up, normal) * pivot.rotation);
                    }
                    else
                    {
                        float top = samples.Max() - .025f;
                        float bottom = samples.Min() - .09f;
                        pivot.position += Vector3.up * (top - b.min.y);
                        var foundation = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        foundation.name = part + " stone footing";
                        foundation.transform.SetParent(root);
                        foundation.transform.position = new Vector3(b.center.x, (top + bottom) * .5f, b.center.z);
                        foundation.transform.localScale = new Vector3(b.size.x * .83f, top - bottom, b.size.z * .80f);
                        foundation.GetComponent<MeshRenderer>().sharedMaterial = footing;
                        pieces.Add(foundation.GetComponent<BoxCollider>());
                    }

                    var collision = mesh.gameObject.AddComponent<MeshCollider>();
                    collision.sharedMesh = mesh.sharedMesh;
                    pieces.Add(collision);
                    report.AppendLine($"{part} x/z=({x:F1},{z:F1}), grade={samples.Min():F2}..{samples.Max():F2}m, " +
                        $"{mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                }

                Place("HearthInn", 21.8f, -33.5f, 90);
                Place("OpenSmithy", 37.2f, -32.8f, -90);
                Place("MarketShelter", 28.2f, -44.2f, 0);
                Place("MarketWell", 32.7f, -37.8f, 0);
                foreach (float z in new[] { -27f, -31f, -35f, -39f })
                    Place("SquareLane4m", 28, z, 0, lane: true);

                Physics.SyncTransforms();
                foreach (var piece in pieces.OfType<MeshCollider>())
                    SettlementPlacementPreview.ReportOverlap(piece, before, report);

                Vector3 Surface(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    foreach (var piece in pieces)
                        if (piece.Raycast(new Ray(ground + Vector3.up * 4, Vector3.down), out var hit, 8) &&
                            hit.point.y > ground.y)
                            ground = hit.point;
                    return ground;
                }

                var player = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                var marketLane = new WorldTraversalVerification.Route("Emberwatch market lane",
                    new Vector3(28, 0, -24.3f), new Vector3(28, 0, -30.5f), new Vector3(28, 0, -38.5f));
                var forgeDoor = new WorldTraversalVerification.Route("Emberwatch smithy apron",
                    new Vector3(28, 0, -32.9f), new Vector3(31, 0, -32.9f), new Vector3(35, 0, -32.9f));
                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(marketLane, false, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(marketLane, true, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(forgeDoor, false, reverse, player, report, Surface);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportEmberwatchPrefab") >= 0)
                {
                    if (File.Exists(Prefab)) throw new IOException($"Already exists: {Prefab}.");
                    var copy = Object.Instantiate(root.gameObject);
                    copy.name = root.name;
                    copy.transform.position = Vector3.zero;
                    if (!PrefabUtility.SaveAsPrefabAsset(copy, Prefab)) throw new IOException($"Could not create {Prefab}.");
                    Object.DestroyImmediate(copy);
                    report.AppendLine($"Created {Prefab}; map anchor {Anchor.ToString("F2")}.");
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "emberwatch-unity-overview.png",
                        new Vector3(48, 18, -16), new Vector3(28, 1.2f, -34));
                    SettlementPlacementPreview.Capture(Output + "emberwatch-unity-street.png",
                        new Vector3(27.6f, 2.5f, -22), new Vector3(28.4f, 2.5f, -37));
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
