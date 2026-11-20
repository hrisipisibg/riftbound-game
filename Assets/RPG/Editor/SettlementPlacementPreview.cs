using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class SettlementPlacementPreview
    {
        const string Kit = "Assets/RPG/Art/SettlementKit/";
        const string Output = WorldProductionAudit.Evidence + "settlement-preview/";

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Temporary edit-mode placement. No scene or terrain saves.\n");
                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var preview = Create(report);
                foreach (var collider in preview.Models)
                    ReportOverlap(collider, existing, report);
                var terrace = preview.Terrace;
                var toe = preview.Toe;
                var deck = preview.Deck;
                var uphill = preview.Uphill;
                var controller = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                foreach (bool mounted in new[] { false, true })
                foreach (bool reverse in new[] { false, true })
                    WorldTraversalVerification.Traverse(preview.Ramp, mounted, reverse, controller, report, preview.RampSurface);

                foreach (bool mounted in new[] { false, true })
                foreach (bool reverse in new[] { false, true })
                    WorldTraversalVerification.Traverse(preview.CampApproach, mounted, reverse, controller, report);

                float toeGap = Top(terrace, toe).y - WorldRegions.GroundHeight(toe);
                float rise = Top(terrace, deck).y - Top(terrace, toe).y;
                report.AppendLine($"Ramp toe={toe:F2} deck={deck:F2} toe/terrain delta={toeGap:F2}m sampled rise={rise:F2}m");
                if (!SystemInfo.graphicsDeviceType.Equals(UnityEngine.Rendering.GraphicsDeviceType.Null))
                {
                    Capture(Output + "emberwatch-overview.png", terrace.bounds.center + new Vector3(12, 11, -14),
                        Vector3.Lerp(terrace.bounds.center, preview.House.bounds.center, .4f));
                    Capture(Output + "emberwatch-approach.png", toe - uphill * 4f + Vector3.up * 2.7f,
                        terrace.bounds.center + Vector3.up * .4f);
                    Capture(Output + "lantern-rest.png", preview.Shelter.bounds.center + new Vector3(-9, 3, -6), preview.Shelter.bounds.center);
                }

                File.WriteAllText(Output + "placement.txt", report.ToString());
                EditorApplication.Exit(report.ToString().Contains("BLOCKED") ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        internal sealed class Placement
        {
            internal MeshCollider[] Models;
            internal MeshCollider Terrace => Models[0];
            internal MeshCollider House => Models[1];
            internal MeshCollider Shelter => Models[2];
            internal Vector3 Toe, Deck;
            internal Vector3 Uphill => (Deck - Toe).normalized;

            internal WorldTraversalVerification.Route Ramp => new WorldTraversalVerification.Route("Emberwatch cart ramp",
                Toe - Uphill * 1.4f, Toe, Deck - Uphill * 1.3f);
            internal WorldTraversalVerification.Route CampApproach => new WorldTraversalVerification.Route("Lantern Rest road pull-off",
                new Vector3(-16.14f, 0, 300), new Vector3(-7, 0, 300), new Vector3(-.8f, 0, 300));

            internal Vector3 RampSurface(Vector3 point)
            {
                var surface = WorldTraversalVerification.Surface(point);
                return Terrace.Raycast(new Ray(point + Vector3.up * 5, Vector3.down), out var hit, 10)
                    && hit.point.y > surface.y ? hit.point : surface;
            }
        }

        internal static Placement Create(StringBuilder report, bool replaceLanternRest = false)
        {
            var atlas = Load<Texture2D>("civil_surfaces.png");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", atlas);
            material.SetFloat("_Smoothness", .12f);
            var stone = new Material(material);
            stone.SetTextureScale("_BaseMap", new Vector2(.23f, .46f));
            stone.SetTextureOffset("_BaseMap", new Vector2(.01f, .52f));
            var court = new GameObject("Emberwatch east court preview");

            var terrace = Place("Emberwatch_ServiceTerrace", court.transform, material, new Vector3(28, 0, -22), Quaternion.Euler(0, 180, 0));
            Physics.SyncTransforms();
            var local = terrace.bounds;
            Vector3 near = new Vector3(local.center.x, 0, local.min.z + .12f);
            Vector3 far = new Vector3(local.center.x, 0, local.max.z - .12f);
            bool nearLow = Top(terrace, near).y < Top(terrace, far).y;
            Vector3 toe = nearLow ? near : far;
            Vector3 deck = nearLow ? far : near;
            Vector3 uphill = (deck - toe).normalized;
            var terraceRoot = terrace.transform.parent;
            terraceRoot.position += Vector3.up * WorldRegions.GroundHeight(toe);
            var houseAt = deck + uphill * 3.15f + Vector3.right * 2.55f;
            var house = Place("Emberwatch_Weighhouse", court.transform, material, houseAt,
                Quaternion.LookRotation(-uphill));
            Ground(house, stone, report);

            var crates = PlaceProp("Crate_Wooden", court.transform, new Vector3(29.45f, Top(terrace, deck).y, -19.88f), 12);
            var models = new List<MeshCollider> { terrace, house };
            if (!replaceLanternRest)
            {
                var camp = new GameObject("Lantern Rest preview");
                var shelter = Place("LanternRest_RoadShelter", camp.transform, material, new Vector3(2, 0, 300),
                    Quaternion.Euler(0, -90, 0));
                Ground(shelter, stone, report, shelterFeet: true);
                var lantern = Place("LanternRest_Waypost", camp.transform, material, new Vector3(-2, 0, 298));
                Ground(lantern, stone, report, baseWidth: .50f);
                var bench = PlaceProp("Bench", camp.transform, new Vector3(.15f, WorldRegions.GroundHeight(new Vector3(.15f, 0, 302.5f)), 302.5f), 10);
                var barrel = PlaceProp("Barrel", camp.transform, new Vector3(3.2f, WorldRegions.GroundHeight(new Vector3(3.2f, 0, 302.2f)), 302.2f), 0);
                models.AddRange(new[] { shelter, lantern, bench, barrel });
            }
            models.Add(crates);
            Physics.SyncTransforms();
            return new Placement { Models = models.ToArray(), Toe = toe, Deck = deck };
        }

        static T Load<T>(string name) where T : Object =>
            AssetDatabase.LoadAssetAtPath<T>(Kit + name) ?? throw new InvalidOperationException("Missing " + name);

        internal static MeshCollider Place(string name, Transform root, Material material, Vector3 position, Quaternion? rotation = null)
        {
            var model = Load<GameObject>(name + ".fbx");
            var anchor = new GameObject(name).transform;
            anchor.SetParent(root);
            anchor.SetPositionAndRotation(position, rotation ?? Quaternion.identity);
            var collision = PlaceModel(model, anchor);
            collision.GetComponent<MeshRenderer>().sharedMaterial = material;
            return collision;
        }

        internal static MeshCollider PlaceProp(string name, Transform root, Vector3 position, float yaw)
        {
            const string folder = "Assets/RPG/Art/FantasyProps/FBX/";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + name + ".fbx");
            if (!prefab) throw new InvalidOperationException("Missing prop " + name);
            var anchor = new GameObject(name).transform;
            anchor.SetParent(root);
            anchor.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0));
            return PlaceModel(prefab, anchor);
        }

        static MeshCollider PlaceModel(GameObject prefab, Transform anchor)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
            var mesh = instance.GetComponentInChildren<MeshFilter>();
            var collision = mesh.gameObject.AddComponent<MeshCollider>();
            collision.sharedMesh = mesh.sharedMesh;
            return collision;
        }

        internal static void Ground(MeshCollider model, Material stone, StringBuilder report, bool shelterFeet = false, float baseWidth = 0f)
        {
            Physics.SyncTransforms();
            var bounds = model.bounds;
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            foreach (float x in new[] { bounds.min.x, bounds.center.x, bounds.max.x })
            foreach (float z in new[] { bounds.min.z, bounds.center.z, bounds.max.z })
            {
                float y = WorldRegions.GroundHeight(new Vector3(x, 0, z));
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            var instance = model.transform.parent;
            instance.position += Vector3.up * (high - bounds.min.y);
            if (shelterFeet)
            {
                foreach (float x in new[] { -1.57f, 1.57f })
                foreach (float z in new[] { -1.05f, 1.05f })
                {
                    var foot = instance.TransformPoint(new Vector3(x, 0, z));
                    AddFooting(instance, stone, foot, .36f, .36f, WorldRegions.GroundHeight(foot), high);
                }
            }
            else
            {
                if (baseWidth > 0)
                {
                    var centre = instance.position;
                    AddFooting(instance, stone, centre, baseWidth, baseWidth, WorldRegions.GroundHeight(centre), high);
                }
                else
                {
                    AddFooting(instance, stone, bounds.center, bounds.size.x * .93f, bounds.size.z * .86f, low, high);
                }
            }
            report.AppendLine($"{instance.name} pivot={instance.position:F2} terrain range={low:F2}..{high:F2}m footing allowance={high-low:F2}m");
        }

        static void AddFooting(Transform root, Material stone, Vector3 centre, float width, float depth, float low, float high)
        {
            if (high - low < .02f) return;
            var footing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            footing.name = "Stone footing";
            footing.transform.SetParent(root, true);
            footing.transform.SetPositionAndRotation(new Vector3(centre.x, (low + high - .08f) * .5f, centre.z), Quaternion.identity);
            footing.transform.localScale = new Vector3(width, high - low + .08f, depth);
            footing.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }

        static Vector3 Top(Collider collider, Vector3 point)
        {
            if (!collider.Raycast(new Ray(point + Vector3.up * 10, Vector3.down), out var hit, 15))
                throw new InvalidOperationException($"No surface on {collider.name} at {point}");
            return hit.point;
        }

        internal static void ReportOverlap(Collider placed, Collider[] existing, StringBuilder report)
        {
            var bounds = placed.bounds;
            var overlaps = existing.Where(c => bounds.Intersects(c.bounds)).Select(c => c.name).Distinct().ToArray();
            report.AppendLine($"{placed.name} AABB overlap candidates: {(overlaps.Length == 0 ? "none" : string.Join(", ", overlaps))}");
        }

        internal static void Capture(string name, Vector3 position, Vector3 focus)
        {
            var camera = new GameObject("Temporary placement camera").AddComponent<Camera>();
            camera.transform.position = position;
            camera.transform.LookAt(focus);
            camera.fieldOfView = 50;
            var target = new RenderTexture(1280, 720, 24);
            var previous = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(name, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                ShaderUtil.allowAsyncCompilation = async;
                Object.DestroyImmediate(camera.gameObject);
                Object.DestroyImmediate(pixels);
                target.Release();
                Object.DestroyImmediate(target);
            }
        }
    }
}
