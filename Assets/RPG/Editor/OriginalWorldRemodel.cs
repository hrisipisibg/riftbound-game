using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Riftbound.Editor.OriginalSiteLayout;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static partial class OriginalWorldRemodel
    {
        const string Kit = "Assets/RPG/ProceduralKit/";
        const string FinishFolder = Kit + "Regional Finishes";
        const string Remodel = "Rebuilt from the Emberford kit";
        const string OldShowcase = "Emberford - Ashwood district, approach, and camps";
        const string BridgeGroup = "Westbank - ashwood bridge and approaches";
        static readonly Dictionary<string, GameObject> prefabs = new();
        static readonly Dictionary<(Region, Material), Material> finishes = new();
        static readonly StringBuilder report = new();
        static readonly List<Renderer> retired = new();
        static Scene scene;
        static string site;
        static int models, originals;

        [MenuItem("Riftbound/World/Rebuild original locations with the Emberford kit")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ProceduralKitImport.ImportArchitectureUpgrades();
            scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            report.Clear();
            prefabs.Clear();
            finishes.Clear();
            retired.Clear();
            models = originals = 0;

            if (!AssetDatabase.IsValidFolder(FinishFolder))
                AssetDatabase.CreateFolder(Kit.TrimEnd('/'), "Regional Finishes");

            SaveQuestProps();
            RemoveShowcase();
            var anchors = RecordAnchors();

            foreach (var layout in Sites) RebuildSite(layout);
            Ashwood();
            Greenhollow();
            Westbank();
            InteriorAndArenas();
            SecondaryLandmarks();
            Physics.SyncTransforms();
            Validate(anchors);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("The remodeled campaign scene could not be saved.");
            AssetDatabase.SaveAssets();
            string result = $"PASS {originals} original structures and props, {models} visible kit instances, {retired.Count} original renderers retired.\n";
            report.AppendLine(result);
            File.WriteAllText("../original-world-remodel-checks.txt", report.ToString());
            Debug.Log(result);
        }

        static void SaveQuestProps()
        {
            const string path = "Assets/RPG/Resources/OriginalLocationProps.asset";
            var props = AssetDatabase.LoadAssetAtPath<OriginalLocationProps>(path);
            if (!props)
            {
                props = ScriptableObject.CreateInstance<OriginalLocationProps>();
                AssetDatabase.CreateAsset(props, path);
            }
            props.crate = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/Supply_Crate.prefab");
            props.arch = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/Stone_Arch.prefab");
            props.rock = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/Moss_Rock.prefab");
            props.stoneTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "Textures/stone_albedo.png");
            props.woodTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "Textures/wood_albedo.png");
            EditorUtility.SetDirty(props);
        }

        static Transform Find(string name)
        {
            var matches = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(t => t.name == name && t.gameObject.scene == scene).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException($"Expected one authored location called {name}, found {matches.Length}.");
            return matches[0];
        }

        static void RemoveShowcase()
        {
            var showcase = scene.GetRootGameObjects().FirstOrDefault(g => g.name == OldShowcase);
            if (showcase)
            {
                var bridge = showcase.transform.Find(BridgeGroup);
                if (!bridge) throw new InvalidOperationException("The Westbank retrofit is missing from the previous integration.");
                bridge.SetParent(Find("Westbank River and Willow Rest"), true);
                Object.DestroyImmediate(showcase);
            }

            foreach (var terrain in Terrain.activeTerrains)
            {
                var data = terrain.terrainData;
                if (!AssetDatabase.GetAssetPath(data).StartsWith(Kit + "Landscape/", StringComparison.Ordinal)) continue;
                string originalName = data.name.Substring("Emberford ".Length);
                var original = AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets/RPG/Terrain" })
                    .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TerrainData>)
                    .Single(a => a && a.name == originalName);
                terrain.terrainData = original;
                terrain.GetComponent<TerrainCollider>().terrainData = original;
            }
            Physics.SyncTransforms();
        }

        static void RebuildSite(Site layout)
        {
            site = layout.label;
            var root = Find(layout.root);
            int count = originals;
            foreach (var (name, role) in layout.buildings)
            {
                var matches = root.Cast<Transform>().Where(t => t.name == name).ToArray();
                if (matches.Length == 0) throw new InvalidOperationException($"{site} is missing its original {name}.");
                foreach (var old in matches) Rebuild(old, role, GatewayRegion(old.name, layout.region));
            }
            foreach (var child in root.Cast<Transform>().Where(t => t.gameObject.activeSelf).ToArray())
            {
                if (layout.buildings.Any(entry => entry.name == child.name)) continue;
                if (PropRole(child.name) is Role prop) Rebuild(child, prop, layout.region);
            }
            report.AppendLine($"PASS {site} replaced {originals - count} authored objects.");
        }

        static void Rebuild(Transform original, Role role, Region region)
        {
            if (!original.gameObject.activeSelf || original.Find(Remodel)) return;

            if (role == Role.Wall && original.Cast<Transform>().Any(t => t.name.StartsWith("Wall Segment")))
            {
                foreach (var segment in original.Cast<Transform>().Where(t => t.name.StartsWith("Wall Segment")).ToArray())
                    Rebuild(segment, role, region);
                return;
            }

            Quaternion rotation = original.rotation;
            if (role == Role.Cottage || role == Role.Inn || role == Role.Forge ||
                role == Role.Hall || role == Role.Hut || role == Role.Citadel || role == Role.Tower)
                rotation *= Quaternion.Euler(0, 180, 0); // The Blender kit faces +Z while the old architecture used -Z.

            var art = OriginalArt(original);
            if (art.Length == 0 && role != Role.Barrel) throw new InvalidOperationException($"{site} / {original.name} has no original architecture mesh.");
            var bounds = art.Length == 0 ? new Bounds(new Vector3(0, -.03f, 0), new Vector3(.81f, .88f, .81f)) :
                BoundsInFrame(art, original.position, rotation);
            var replacement = CreateRoot(Remodel, original, original.position, rotation);

            switch (role)
            {
                case Role.Citadel:
                    Citadel(replacement, bounds, region);
                    break;
                case Role.Gate:
                    Gate(original, replacement, bounds, region);
                    break;
                case Role.Cargo when original.name == "Prop_Wagon" || original.name.StartsWith("Stall_Cart"):
                    CargoStack(replacement, bounds, region);
                    break;
                default:
                    var dimensions = bounds.size;
                    if (role == Role.Broken) dimensions.y *= .96f;
                    if (role == Role.Lamp) dimensions = new Vector3(.65f, Mathf.Clamp(dimensions.y, 2.3f, 4f), .65f);
                    Place(replacement, Asset(role, region), region, Base(bounds), dimensions);
                    break;
            }

            bool keepCollision = role == Role.Cargo || role == Role.Barrel || role == Role.Lamp;
            if (role == Role.Broken && original.parent.name == "Scarlet Cathedral Ruined Exterior")
                keepCollision = false;
            Retire(original, replacement, art, keepCollision);
            originals++;
        }

        static void Citadel(Transform placement, Bounds old, Region region)
        {
            var centre = Base(old);
            var size = old.size;
            bool fortifiedTimber = IsPalisade(region);
            Place(placement, fortifiedTimber ? "Briar_War_Hall" : "Highspire_Citadel_Hall", region,
                centre + new Vector3(0, 0, -size.z * .055f),
                new Vector3(size.x * .61f, size.y * .86f, size.z * .68f));
            foreach (float side in new[] { -1f, 1f })
            {
                Place(placement, fortifiedTimber ? "Briar_Camp_Hut" : "Timber_Cottage", region,
                    centre + new Vector3(side * size.x * .36f, 0, -size.z * .12f),
                    new Vector3(size.x * .27f, size.y * .44f, size.z * .56f));
                Place(placement, Tower(region), region,
                    centre + new Vector3(side * size.x * .35f, 0, size.z * .34f),
                    new Vector3(size.x * .19f, size.y * (fortifiedTimber ? .78f : .93f), size.z * .25f));
                Place(placement, "Briar_Banner", region,
                    centre + new Vector3(side * size.x * .125f, 0, size.z * .40f),
                    new Vector3(1.1f, 3.5f, .6f));
            }
        }

        static void Gate(Transform original, Transform placement, Bounds bounds, Region region)
        {
            var oldTowers = original.Cast<Transform>().Where(t => t.name.EndsWith("Tower L") || t.name.EndsWith("Tower R")).ToArray();
            if (oldTowers.Length == 2)
            {
                foreach (var tower in oldTowers)
                {
                    var b = BoundsInFrame(OriginalArt(tower), placement.position, placement.rotation);
                    Place(placement, Tower(region), region, Base(b), b.size);
                }
            }
            float span = original.name == "Emberwatch North Gateway Arch" ? 8.5f : original.name.Contains("Bloodrock") ? 8.5f : 9.5f;
            float width = span * 1.32f;
            // Scale the original sculpted arch opening to the road. Existing towers remain outside that opening.
            Place(placement, "Stone_Arch", region, new Vector3(0, Mathf.Min(0, bounds.min.y + .18f), 0),
                new Vector3(width, 7.15f, 1.9f));
            foreach (float side in new[] { -1f, 1f })
                Place(placement, "Street_Lantern", region, new Vector3(side * (span * .5f + 1.0f), 0, -1.35f),
                    new Vector3(.63f, 2.8f, .67f));
        }

        static void CargoStack(Transform parent, Bounds bounds, Region region)
        {
            var centre = Base(bounds);
            float width = Mathf.Clamp(bounds.size.x * .50f, .65f, 1.44f);
            float length = Mathf.Clamp(bounds.size.z * .42f, .67f, 1.13f);
            Place(parent, "Supply_Crate", region, centre + Vector3.left * width * .51f,
                new Vector3(width, .82f, length), -12f);
            Place(parent, "Barrel", region, centre + Vector3.right * width * .47f,
                new Vector3(width * .68f, 1.22f, width * .68f), 12f);
            Place(parent, "Supply_Crate", region, centre + new Vector3(-width * .50f, .81f, .09f),
                new Vector3(width * .65f, .52f, length * .75f), 22f);
        }

        static Renderer[] OriginalArt(Transform root) => root.GetComponentsInChildren<MeshRenderer>(false)
            .Where(r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh &&
                !r.GetComponent<TextMesh>()).Cast<Renderer>().ToArray();

        static void Retire(Transform original, Transform replacement, Renderer[] art, bool keepCollision)
        {
            foreach (var renderer in art)
            {
                renderer.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                retired.Add(renderer);
            }
            if (keepCollision) return;
            foreach (var collider in original.GetComponentsInChildren<Collider>(false))
            {
                if (collider.isTrigger || collider.transform.IsChildOf(replacement) ||
                    collider.GetComponentInParent<Combatant>() || collider.GetComponent<SupplyCrate>() ||
                    collider.GetComponent<Landmark>() || collider.GetComponent<DungeonPortal>()) continue;
                collider.enabled = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
        }

        static Transform CreateRoot(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.SetPositionAndRotation(position, rotation);
            return root;
        }

        static Vector3 Base(Bounds bounds) => new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

        static Bounds BoundsInFrame(IEnumerable<Renderer> renderers, Vector3 origin, Quaternion frame)
        {
            var inverse = Quaternion.Inverse(frame);
            bool seen = false;
            var bounds = new Bounds();
            foreach (var renderer in renderers)
            {
                var box = renderer.localBounds;
                var pose = renderer.transform;
                for (int i = 0; i < 8; i++)
                {
                    var local = box.center + Vector3.Scale(box.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = inverse * (pose.TransformPoint(local) - origin);
                    if (!seen) { bounds = new Bounds(point, Vector3.zero); seen = true; }
                    else bounds.Encapsulate(point);
                }
            }
            if (!seen) throw new InvalidOperationException($"{site}: cannot measure an empty mesh.");
            return bounds;
        }

        static Transform Place(Transform group, string asset, Region region, Vector3 baseline, Vector3 dimensions, float turn = 0f,
            bool collision = true)
        {
            if (!prefabs.TryGetValue(asset, out var prefab))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/" + asset + ".prefab");
                if (!prefab) throw new InvalidOperationException("Missing kit asset " + asset);
                prefabs.Add(asset, prefab);
            }

            var art = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var offset = CreateRoot(art.name + " footing", group,
                group.position + group.rotation * baseline, group.rotation * Quaternion.Euler(0, turn, 0));
            var mesh = art.transform;
            mesh.SetParent(offset, false);
            // The parent site can be a scaled prefab instance; normalize its inherited scale before fitting.
            var inherited = offset.lossyScale;
            offset.localScale = new Vector3(1 / Mathf.Abs(inherited.x), 1 / Mathf.Abs(inherited.y), 1 / Mathf.Abs(inherited.z));
            var visuals = OriginalArt(mesh);
            var lod = visuals.Where(r => r.name.EndsWith("_LOD0", StringComparison.Ordinal)).ToArray();
            var bounds = BoundsInFrame(lod.Length == 0 ? visuals : lod, offset.position, offset.rotation);
            var size = bounds.size;
            var scale = new Vector3(dimensions.x / Mathf.Max(.015f, size.x), dimensions.y / Mathf.Max(.015f, size.y),
                dimensions.z / Mathf.Max(.015f, size.z));
            mesh.localScale = new Vector3(Mathf.Clamp(scale.x, .11f, 8f), Mathf.Clamp(scale.y, .11f, 8f),
                Mathf.Clamp(scale.z, .11f, 8f));
            mesh.localPosition = -Vector3.Scale(Base(bounds), mesh.localScale);

            foreach (var renderer in visuals)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m => Finish(region, m)).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if (!collision)
                foreach (var col in art.GetComponentsInChildren<Collider>(true)) col.enabled = false;
            models++;
            return offset;
        }

        static Material Finish(Region region, Material original)
        {
            if (!original) return original;
            if (finishes.TryGetValue((region, original), out var existing)) return existing;
            var colors = Palette(region);
            string swatch = original.name.Replace('_', ' ');
            string hex = swatch switch
            {
                "Limestone" or "Pale Stone" or "Basalt" => colors.stone,
                "Oak" or "Oak Edge" or "Dark Wood" => colors.timber,
                "Slate" or "Slate Edge" or "Ember Tile" or "Ember Edge" => colors.roof,
                "Banner Teal" or "Banner Red" => colors.cloth,
                "Plaster" => colors.plaster,
                "Gold" or "Iron" or "Red Iron" => colors.metal,
                _ => null
            };
            if (hex == null) return original;
            var materialName = $"{region} {swatch}";
            string path = $"{FinishFolder}/{materialName}.mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!variant)
            {
                variant = new Material(original) { name = materialName };
                AssetDatabase.CreateAsset(variant, path);
            }
            ColorUtility.TryParseHtmlString("#" + hex, out var tint);
            float value = swatch.EndsWith("Edge", StringComparison.Ordinal) || swatch == "Pale Stone" ? 1.22f :
                swatch == "Dark Wood" || original.name == "Basalt" ? .75f : 1f;
            variant.SetColor("_BaseColor", new Color(tint.r * value, tint.g * value, tint.b * value, 1));
            EditorUtility.SetDirty(variant);
            finishes.Add((region, original), variant);
            return variant;
        }

        static Material MaterialFor(Region region, string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/" + name.Replace(' ', '_') + ".mat");
            if (!material) throw new InvalidOperationException("Missing kit material " + name);
            return Finish(region, material);
        }

        static Dictionary<Component, Vector3> RecordAnchors() => Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
            .Where(c => c is TownNPC || c is QuestGiver || c is EnemyController || c is DungeonPortal || c is LootChest ||
                c is SupplyCrate || c is Runestone || c is FlightMaster || c is Landmark || c is PlayerController)
            .ToDictionary(c => (Component)c, c => c.transform.position);

        static void Validate(Dictionary<Component, Vector3> anchors)
        {
            foreach (var pair in anchors)
                if (!pair.Key || Vector3.SqrMagnitude(pair.Key.transform.position - pair.Value) > .000001f)
                    throw new InvalidOperationException("A gameplay anchor changed while remodeling the map.");
            if (retired.Any(r => !r || r.enabled) || Object.FindObjectsByType<Transform>().Count(t => t.name == Remodel) < 135)
                throw new InvalidOperationException("A legacy visual survived or the planned location coverage was incomplete.");
            report.AppendLine($"PASS {anchors.Count} authored player, NPC, quest, enemy, and portal anchors kept their positions and components.");
        }
    }
}
