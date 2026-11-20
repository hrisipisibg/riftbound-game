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
    public static class DeathknellCloisterPreview
    {
        const string Art = "Assets/RPG/Art/DeathknellCloister/";
        const string Output = WorldProductionAudit.Evidence + "deathknell-cloister/";
        const string Prefab = Art + "Deathknell Cloister.prefab";
        const float LaneZ = 156.8f;
        static readonly Vector3 Anchor = new Vector3(-242.5f, 0, LaneZ);

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Deathknell south cloister preview on the authored map.\n");
                var existing = Object.FindObjectsByType<Collider>()
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var material = AssetDatabase.LoadAssetAtPath<Material>(Art + "Deathknell Slate and Moss.mat");
                if (!material || !material.mainTexture || material.mainTexture.width != 768)
                    throw new InvalidOperationException("Deathknell kit atlas is missing.");

                var root = new GameObject("Deathknell Cloister").transform;
                root.position = Anchor;
                var models = new List<MeshCollider>();

                Transform Place(string part, float x, float z, float yaw = 0)
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Art + $"Deathknell_{part}.fbx");
                    if (!model) throw new InvalidOperationException($"Missing Deathknell part {part}.");
                    var pivot = new GameObject(part).transform;
                    pivot.SetParent(root);
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
                    pivot.position += Vector3.up * (heights.Min() - .012f - bounds.min.y);
                    var collider = mesh.gameObject.AddComponent<MeshCollider>();
                    collider.sharedMesh = mesh.sharedMesh;
                    models.Add(collider);
                    report.AppendLine($"{part}: ({x:F2},{z:F2}), terrain {heights.Min():F2}..{heights.Max():F2}m, " +
                        $"{mesh.sharedMesh.GetIndexCount(0) / 3} triangles.");
                    return pivot;
                }

                void Lantern(float x, float z, float yaw)
                {
                    var pivot = Place("CloisterLantern", x, z, yaw);
                    var light = new GameObject("Cloister pool of light").AddComponent<Light>();
                    light.transform.SetParent(pivot, false);
                    light.transform.localPosition = new Vector3(0, 2.10f, .72f);
                    light.type = LightType.Point;
                    light.color = new Color(.60f, .83f, .43f);
                    light.intensity = .65f;
                    light.range = 3.15f;
                    light.shadows = LightShadows.None;
                }

                Place("WatchArch", -249.1f, 157.65f, -90);
                Place("BrokenArcade", -251.25f, 161.75f, 12);
                Place("Cenotaph", -232.4f, 156.55f, -65);
                var ramp = Place("CloisterRamp", -242.5f, LaneZ, 90);
                Lantern(-247.65f, 159.9f, 0);
                Lantern(-233.35f, 160.55f, 180);

                Physics.SyncTransforms();
                foreach (var model in models)
                    SettlementPlacementPreview.ReportOverlap(model, existing, report);

                var grade = ramp.GetComponentInChildren<MeshCollider>();
                Vector3 Surface(Vector3 point)
                {
                    var ground = WorldTraversalVerification.Surface(point);
                    if (grade.Raycast(new Ray(ground + Vector3.up * 2f, Vector3.down), out var hit, 4f) && hit.point.y > ground.y)
                        return hit.point;
                    return ground;
                }

                float rise = Surface(new Vector3(-242.5f, 0, LaneZ)).y - Surface(new Vector3(-248, 0, LaneZ)).y;
                report.AppendLine(rise > .64f && rise < .77f ? $"Ramp rise: {rise:F3}m." : $"BLOCKED ramp rise: {rise:F3}m.");
                var player = Object.FindAnyObjectByType<PlayerController>().GetComponent<CharacterController>();
                var passage = new WorldTraversalVerification.Route("Deathknell cloister arch and ramp",
                    new Vector3(-253, 0, LaneZ), new Vector3(-249.1f, 0, LaneZ),
                    new Vector3(-242.5f, 0, LaneZ), new Vector3(-235.85f, 0, LaneZ));
                var entrance = new WorldTraversalVerification.Route("Deathknell approach from starter square",
                    new Vector3(-236.8f, 0, 175.15f), new Vector3(-234.4f, 0, 165.8f),
                    new Vector3(-235.85f, 0, 162.4f), new Vector3(-235.85f, 0, LaneZ));
                foreach (bool reverse in new[] { false, true })
                foreach (bool mounted in new[] { false, true })
                {
                    WorldTraversalVerification.Traverse(passage, mounted, reverse, player, report, Surface);
                    WorldTraversalVerification.Traverse(entrance, mounted, reverse, player, report, Surface);
                }

                bool blocked = report.ToString().Contains("BLOCKED");
                if (!blocked && Array.IndexOf(Environment.GetCommandLineArgs(), "-exportDeathknellCloister") >= 0)
                {
                    if (File.Exists(Prefab)) throw new IOException($"Already exists: {Prefab}.");
                    var copy = Object.Instantiate(root.gameObject);
                    copy.name = root.name;
                    copy.transform.position = Vector3.zero;
                    if (!PrefabUtility.SaveAsPrefabAsset(copy, Prefab)) throw new IOException($"Could not create {Prefab}.");
                    Object.DestroyImmediate(copy);
                    report.AppendLine($"Created {Prefab} at {Anchor.ToString("F2")}.");
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "deathknell-in-world.png",
                        new Vector3(-254, 21.2f, 144.5f), new Vector3(-242.8f, 13.9f, 159.5f));
                    SettlementPlacementPreview.Capture(Output + "deathknell-approach.png",
                        new Vector3(-254.5f, 14.8f, LaneZ - 1.25f), new Vector3(-240, 14.5f, LaneZ));
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
