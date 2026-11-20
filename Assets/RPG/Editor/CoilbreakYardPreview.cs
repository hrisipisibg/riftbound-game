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
    public static class CoilbreakYardPreview
    {
        const string Output = WorldProductionAudit.Evidence + "coilbreak-preview/";
        static readonly Vector3 Yard = new Vector3(211, 0, 188);

        internal static WorldTraversalVerification.Route Approach => new WorldTraversalVerification.Route("Coilbreak service spur",
            new Vector3(205, 0, 162.73f), new Vector3(205, 0, 174), new Vector3(207, 0, 187));

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Coilbreak yard placement in memory. No scene or asset saved.\n");
                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var road = new WorldTraversalVerification.Route("Redridge through lane",
                    new Vector3(195, 0, 166.36f), new Vector3(205, 0, 162.73f), new Vector3(218, 0, 158));
                report.AppendLine("BEFORE PLACEMENT");
                Traverse(road, report);
                report.AppendLine("AFTER PLACEMENT");
                var parts = Create(report);
                foreach (var part in parts)
                    SettlementPlacementPreview.ReportOverlap(part, existing, report);
                Traverse(road, report);
                Traverse(Approach, report);

                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    var focus = Yard + Vector3.up * (WorldRegions.GroundHeight(Yard) + 1.2f);
                    SettlementPlacementPreview.Capture(Output + "overview.png", focus + new Vector3(-11, 9, -13), focus);
                    SettlementPlacementPreview.Capture(Output + "approach.png", focus + new Vector3(-6, 2.6f, -10), focus);
                }

                File.WriteAllText(Output + "review.txt", report.ToString());
                EditorApplication.Exit(report.ToString().Contains("BLOCKED") ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        internal static List<Collider> Create(StringBuilder report)
        {
            var root = new GameObject("Coilbreak Yard preview").transform;
            var surface = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            surface.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/SettlementKit/civil_surfaces.png"));
            surface.SetColor("_BaseColor", new Color(.78f, .66f, .52f));
            surface.SetFloat("_Smoothness", .15f);
            var stone = new Material(surface);
            stone.SetTextureScale("_BaseMap", new Vector2(.23f, .46f));
            stone.SetTextureOffset("_BaseMap", new Vector2(.01f, .52f));
            var metal = new Material(surface);
            metal.SetTexture("_BaseMap", null);
            metal.SetColor("_BaseColor", new Color(.27f, .14f, .07f));
            metal.SetFloat("_Metallic", .55f);

            var shed = SettlementPlacementPreview.Place("LanternRest_RoadShelter", root, surface,
                new Vector3(211.8f, 0, 190.2f), Quaternion.Euler(0, 180, 0));
            SettlementPlacementPreview.Ground(shed, stone, report, shelterFeet: true);
            var waypost = SettlementPlacementPreview.Place("LanternRest_Waypost", root, surface,
                new Vector3(202.9f, 0, 170.2f), Quaternion.Euler(0, -35, 0));
            SettlementPlacementPreview.Ground(waypost, stone, report, baseWidth: .50f);
            var parts = new List<Collider> { shed, waypost };
            parts.Add(Prop("Stall_Cart_Empty", root, new Vector3(210.25f, 0, 184.2f), 155));
            parts.Add(Prop("Anvil_Log", root, new Vector3(211.8f, 0, 189.65f), 20));
            parts.Add(Prop("Cauldron", root, new Vector3(215.6f, 0, 188.6f), 0));
            parts.Add(Prop("Barrel_Holder", root, new Vector3(213.9f, 0, 190.1f), 80));
            parts.Add(Prop("Crate_Wooden", root, new Vector3(213.1f, 0, 184.2f), 18));

            var stacked = new Vector3(215.1f, 0, 183.25f);
            stacked.y = WorldRegions.GroundHeight(stacked);
            AddCoils(root, metal, stacked, parts);

            Physics.SyncTransforms();
            report.AppendLine($"Yard: {parts.Count} solid placements. The road runs at z=162.73 near the entry.");
            return parts;
        }

        static Collider Prop(string name, Transform root, Vector3 point, float yaw)
        {
            var collider = SettlementPlacementPreview.PlaceProp(name, root, point, yaw);
            Physics.SyncTransforms();
            var bounds = collider.bounds;
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            foreach (float x in new[] { bounds.min.x, bounds.max.x })
            foreach (float z in new[] { bounds.min.z, bounds.max.z })
            {
                float height = WorldRegions.GroundHeight(new Vector3(x, 0, z));
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
            }
            var anchor = collider.transform;
            while (anchor.parent != root) anchor = anchor.parent;
            anchor.position += Vector3.up * (Mathf.Lerp(low, high, .5f) - bounds.min.y - .04f);
            return collider;
        }

        static void AddCoils(Transform root, Material metal, Vector3 baseAt, List<Collider> parts)
        {
            const int rings = 24, sides = 8;
            var vertices = new Vector3[rings * sides];
            var indices = new int[rings * sides * 6];
            for (int i = 0; i < rings; i++)
            for (int j = 0; j < sides; j++)
            {
                float angle = (Mathf.PI * 2 / rings) * i;
                float cross = (Mathf.PI * 2 / sides) * j;
                float radial = .49f + .06f * Mathf.Cos(cross);
                int a = i * sides + j;
                vertices[a] = new Vector3(radial * Mathf.Cos(angle), .06f * Mathf.Sin(cross), radial * Mathf.Sin(angle));
                int b = i * sides + (j + 1) % sides;
                int c = ((i + 1) % rings) * sides + j;
                int d = ((i + 1) % rings) * sides + (j + 1) % sides;
                int k = a * 6;
                indices[k] = a; indices[k + 1] = b; indices[k + 2] = c;
                indices[k + 3] = b; indices[k + 4] = d; indices[k + 5] = c;
            }

            var mesh = new Mesh { name = "Coilbreak salvage ring", vertices = vertices, triangles = indices };
            mesh.RecalculateNormals();
            var stack = new GameObject("Recovered copper coils").transform;
            stack.SetParent(root);
            stack.position = baseAt;
            for (int i = 0; i < 4; i++)
            {
                var coil = new GameObject("Salvage coil");
                coil.transform.SetParent(stack);
                coil.transform.localPosition = new Vector3(i * .04f, .09f + i * .13f, 0);
                coil.AddComponent<MeshFilter>().sharedMesh = mesh;
                coil.AddComponent<MeshRenderer>().sharedMaterial = metal;
            }
            var collision = stack.gameObject.AddComponent<BoxCollider>();
            collision.center = new Vector3(.06f, .28f, 0);
            collision.size = new Vector3(1.23f, .56f, 1.12f);
            parts.Add(collision);
        }

        static void Traverse(WorldTraversalVerification.Route route, StringBuilder report)
        {
            var controller = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
            foreach (bool mounted in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
                WorldTraversalVerification.Traverse(route, mounted, reverse, controller, report);
        }
    }
}
