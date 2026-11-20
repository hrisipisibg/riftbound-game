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
    public static class RustwaterSalvageYardPreview
    {
        const string Art = "Assets/RPG/Art/RustwaterSalvageYard/";
        const string Output = WorldProductionAudit.Evidence + "rustwater-salvage-yard/";
        const string Prefab = Art + "Rustwater Salvage Yard.prefab";
        static readonly Vector3 Anchor = new Vector3(224, 0, 171);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Rustwater salvage yard fit on authored terrain.\n");
                var existing = Object.FindObjectsByType<Collider>()
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Rustwater Zinc and Verdigris.mat");
                if (!material || !material.mainTexture || material.mainTexture.width != 768)
                    throw new InvalidOperationException("Rustwater materials or atlas missing.");

                var root = new GameObject("Rustwater Salvage Yard").transform;
                root.position = Anchor;
                var blockers = new List<Collider>();

                MeshFilter Place(string name, Transform pivot, float x, float y, float z, float yaw)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Rustwater_{name}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Rustwater model {name}.");
                    pivot.SetPositionAndRotation(new Vector3(x, y, z), Quaternion.Euler(0, yaw, 0));
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, pivot);
                    var mesh = instance.GetComponentsInChildren<MeshFilter>().Single();
                    if (!mesh.sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0) || mesh.sharedMesh.subMeshCount != 1)
                        throw new InvalidOperationException($"Expected one textured mesh for {name}.");
                    mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
                    return mesh;
                }

                Transform Ground(string name, float x, float z, float yaw = 0)
                {
                    var pivot = new GameObject(name).transform;
                    pivot.SetParent(root);
                    var mesh = Place(name, pivot, x, 0, z, yaw);
                    var bounds = mesh.GetComponent<MeshRenderer>().bounds;
                    var ground = new List<float>();
                    foreach (float px in new[] { bounds.min.x, x, bounds.max.x })
                    foreach (float pz in new[] { bounds.min.z, z, bounds.max.z })
                        ground.Add(WorldRegions.GroundHeight(new Vector3(px, 0, pz)));
                    if (ground.Max() - ground.Min() > .12f)
                        throw new InvalidOperationException($"{name} exceeds local grounding tolerance: {ground.Min():F2}..{ground.Max():F2}m.");
                    pivot.position += Vector3.up * (ground.Min() - .009f);
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    blockers.Add(collider);
                    report.AppendLine($"{name}: ground {ground.Min():F2}..{ground.Max():F2}m, {mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                    return pivot;
                }

                Ground("SortingWorkshop", 218, 173, 90);
                var frame = Ground("DrumFrame", 219.3f, 166.6f);
                var axle = new GameObject("Sorting drum axle").transform;
                axle.SetParent(frame);
                var axlePosition = frame.position + Vector3.up * 1.47f;
                var drum = Place("SortingDrum", axle, axlePosition.x, axlePosition.y, axlePosition.z, 0);
                if (drum.GetComponent<MeshRenderer>().bounds.size.x < drum.GetComponent<MeshRenderer>().bounds.size.z)
                    throw new InvalidOperationException("Rustwater drum axle is off axis.");
                axle.gameObject.AddComponent<AmbientAxle>();
                var fixedDrumVolume = frame.gameObject.AddComponent<BoxCollider>();
                fixedDrumVolume.center = new Vector3(0, 1.47f, 0);
                fixedDrumVolume.size = new Vector3(1.57f, 1.58f, 1.58f);
                blockers.Add(fixedDrumVolume);

                Ground("SalvageCrane", 230.1f, 173.8f, 265);
                Ground("SortedScrapBins", 230, 167.9f, 265);
                var crossing = Ground("ServiceCrossing", 225, 171);
                Physics.SyncTransforms();
                foreach (var collider in blockers)
                    SettlementPlacementPreview.ReportOverlap(collider, existing, report);

                var surface = crossing.GetComponentInChildren<MeshCollider>();
                Vector3 Deck(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    if (surface.Raycast(new Ray(ground + Vector3.up * 2f, Vector3.down), out var hit, 4) && hit.point.y > ground.y)
                        return hit.point;
                    return ground;
                }

                float rise = Deck(new Vector3(225, 0, 171)).y - Deck(new Vector3(225, 0, 165)).y;
                report.AppendLine(rise > .67f && rise < .77f ? $"Crossing height: {rise:F3}m." : $"BLOCKED crossing height: {rise:F3}m.");

                var player = Object.FindAnyObjectByType<PlayerController>().GetComponent<CharacterController>();
                var lane = new WorldTraversalVerification.Route("Rustwater Redridge approach and crossing",
                    new Vector3(225, 0, 156), new Vector3(225, 0, 164.8f), new Vector3(225, 0, 171), new Vector3(225, 0, 177.4f));
                var workshop = new WorldTraversalVerification.Route("Rustwater workshop apron",
                    new Vector3(222.5f, 0, 173), new Vector3(220.55f, 0, 173));
                foreach (bool reverse in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(lane, false, reverse, player, report, Deck);
                    WorldTraversalVerification.Traverse(lane, true, reverse, player, report, Deck);
                    WorldTraversalVerification.Traverse(workshop, false, reverse, player, report);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportRustwaterSalvageYard") >= 0)
                {
                    if (File.Exists(Prefab)) throw new IOException($"Already exists: {Prefab}.");
                    var copy = Object.Instantiate(root.gameObject);
                    copy.name = root.name;
                    copy.transform.position = Vector3.zero;
                    if (!PrefabUtility.SaveAsPrefabAsset(copy, Prefab)) throw new IOException($"Could not save {Prefab}.");
                    Object.DestroyImmediate(copy);
                    report.AppendLine($"Created {Prefab} at {Anchor.ToString("F2")}.");
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "rustwater-in-world.png",
                        new Vector3(239, 24, 160), new Vector3(224, 15.7f, 171));
                    SettlementPlacementPreview.Capture(Output + "rustwater-crossing.png",
                        new Vector3(225, 16.1f, 161), new Vector3(224.8f, 15.4f, 173));
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
