using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class WyrmcrestRefugePreview
    {
        const string Output = WorldProductionAudit.Evidence + "wyrmcrest-preview/";
        static readonly Vector3 Site = new Vector3(371, 0, 314);

        internal static WorldTraversalVerification.Route Approach => new WorldTraversalVerification.Route("Gor'Kresh trailhead spur",
            new Vector3(352, 0, 314), new Vector3(355, 0, 312), new Vector3(362, 0, 313), new Vector3(367.5f, 0, 314));

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Gor'Kresh trailhead shelter in-memory preview. No scene, asset or terrain saved.\n");
                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var road = new WorldTraversalVerification.Route("Lower Wyrmcrest switchback",
                    new Vector3(345.625f, 0, 308), new Vector3(347.5f, 0, 314), new Vector3(349.375f, 0, 320));
                var controller = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                report.AppendLine("BASELINE CENTRELINE DIAGNOSTIC");
                Traverse(road, controller, report);
                report.AppendLine("AFTER PLACEMENT");
                var refuge = Create(report);
                foreach (var collider in refuge.GetComponentsInChildren<Collider>())
                    SettlementPlacementPreview.ReportOverlap(collider, existing, report);
                var candidate = new StringBuilder();
                var bypass = new WorldTraversalVerification.Route("East bypass beside the War-Hall",
                    new Vector3(351, 0, 308), new Vector3(352, 0, 314), new Vector3(352, 0, 320));
                Traverse(bypass, controller, candidate);
                Traverse(Approach, controller, candidate);
                report.Append(candidate);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    var focus = refuge.position + Vector3.up * 1.3f;
                    SettlementPlacementPreview.Capture(Output + "approach.png", focus + new Vector3(-8, 2.6f, -7), focus);
                    SettlementPlacementPreview.Capture(Output + "overview.png", focus + new Vector3(-12, 12, 8), focus);
                }
                File.WriteAllText(Output + "review.txt", report.ToString());
                EditorApplication.Exit(candidate.ToString().Contains("BLOCKED") ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        internal static Transform Create(StringBuilder report)
        {
            var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/SettlementKit/civil_surfaces.png");
            if (!atlas) throw new InvalidOperationException("Settlement surface atlas missing.");
            var stone = Surface(atlas, 0, new Color(.74f, .80f, .81f));
            var slate = Surface(atlas, 3, new Color(.75f, .82f, .88f));
            var timber = Surface(atlas, 2, Color.white);
            var cloth = Surface(atlas, 7, new Color(1f, .72f, .50f));
            var root = new GameObject("Gor'Kresh Trailhead Shelter preview").transform;
            root.position = SettlementPoint(Site);

            // Ground the visible foundation separately from the surrounding grade.
            var floor = Piece(root, "Stone refuge floor", new Vector3(0, -.09f, 0), new Vector3(4.5f, .25f, 3.35f), stone);
            Piece(root, "East wind wall", new Vector3(2.02f, 1.15f, 0), new Vector3(.39f, 2.34f, 3.2f), stone);
            Piece(root, "North wind wall", new Vector3(.12f, .83f, 1.45f), new Vector3(4.05f, 1.68f, .36f), stone);
            foreach (float z in new[] { -1.40f, 1.30f })
                Piece(root, "Oak roof support", new Vector3(-1.9f, 1.38f, z), new Vector3(.21f, 2.76f, .21f), timber);
            var roof = Piece(root, "Sloping slate roof", new Vector3(0, 2.69f, 0), new Vector3(5.12f, .19f, 3.72f), slate);
            roof.transform.localRotation = Quaternion.Euler(0, 0, -7f);
            Piece(root, "Survey rest bench", new Vector3(1.48f, .25f, .55f), new Vector3(.64f, .50f, 1.4f), timber);

            var flag = SettlementPoint(new Vector3(365.2f, 0, 316.6f)) - root.position;
            Piece(root, "Survey flag pole", flag + Vector3.up * 1.3f, new Vector3(.15f, 2.60f, .15f), timber);
            Piece(root, "Red survey pennant", flag + new Vector3(.49f, 2.23f, 0), new Vector3(.97f, .50f, .035f), cloth, false);
            Physics.SyncTransforms();
            report.AppendLine($"Refuge base={root.position:F2}, floor top={floor.bounds.max.y:F2}. Roof clearance estimated from prefab dimensions; spur ends outside the shelter.");
            return root;
        }

        static Material Surface(Texture2D atlas, int tile, Color tint)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", atlas);
            material.SetTextureScale("_BaseMap", new Vector2(.23f, .46f));
            material.SetTextureOffset("_BaseMap", new Vector2((tile % 4) * .25f + .01f, (1 - tile / 4) * .5f + .02f));
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", .09f);
            return material;
        }

        static Vector3 SettlementPoint(Vector3 point)
        {
            point.y = WorldRegions.GroundHeight(point);
            return point;
        }

        static Collider Piece(Transform parent, string name, Vector3 position, Vector3 dimensions, Material material, bool solid = true)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = dimensions;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = part.GetComponent<Collider>();
            if (!solid) Object.DestroyImmediate(collider);
            return collider;
        }

        static void Traverse(WorldTraversalVerification.Route route, CharacterController controller, StringBuilder report)
        {
            foreach (bool mounted in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
                WorldTraversalVerification.Traverse(route, mounted, reverse, controller, report);
        }
    }
}
