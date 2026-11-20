using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static partial class RiverTexturePolish
    {
        const string SurfaceFolder = "Assets/RPG/ProceduralKit/World Surfaces";
        const string Kit = "Assets/RPG/ProceduralKit";
        static readonly Dictionary<string, Texture2D> textures = new();
        static readonly Dictionary<string, Material> surfaceMaterials = new();

        static readonly (string layer, string texture, float tile, float normal)[] TerrainFinishes = {
            ("Painted meadow", "meadow", 7.5f, .31f), ("Earthen trail", "trail", 6.8f, .24f),
            ("Weathered stone", "bedrock", 11.8f, .34f), ("Forest floor", "forest", 7.8f, .29f),
            ("Rocky highland", "highland", 10.5f, .32f), ("Summit snow", "snow", 9.7f, .21f),
            ("Redridge canyon", "canyon", 10.4f, .27f), ("Cobblestone highway", "cobbles", 6.3f, .28f)
        };

        [MenuItem("Riftbound/World/Refinish terrain and rivers")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                !Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            textures.Clear();
            surfaceMaterials.Clear();
            EnsureFolder("Terrain");
            EnsureFolder("Materials");
            ImportTextures();
            var activeTiles = Terrain.activeTerrains;
            if (activeTiles.Length != 4) throw new InvalidOperationException("Expected the four expanded campaign terrain tiles.");
            var westbank = activeTiles.Single(t => t.name == "Expanded terrain 0-0");
            var willow = activeTiles.Single(t => t.name == "Expanded terrain 0-1");
            var foliage = RecordBankDressing();
            var interactions = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include)
                .Where(c => c is PlayerController || c is EnemyController || c is TownNPC || c is LootChest ||
                    c is Runestone || c is DungeonPortal || c is QuestGiver || c is SupplyCrate || c is FlightMaster)
                .ToDictionary(c => c, c => c.transform.position);

            var layers = activeTiles.SelectMany(t => SourceTerrain(t).terrainLayers).Distinct().ToArray();
            foreach (var (label, texture, tile, normal) in TerrainFinishes)
                ApplyLayer(layers.Single(l => l.name == label), texture, tile, normal);
            string siltPath = SurfaceFolder + "/Terrain/Wet shoreline.terrainlayer";
            var silt = AssetDatabase.LoadAssetAtPath<TerrainLayer>(siltPath);
            if (!silt)
            {
                silt = new TerrainLayer { name = "Wet shoreline" };
                AssetDatabase.CreateAsset(silt, siltPath);
            }
            ApplyLayer(silt, "silt", 7.2f, .22f);

            GroundChannel(westbank, silt, false);
            GroundChannel(willow, silt, true);
            ReseatDressing(foliage);
            var river = ReplaceWater("Westbank flowing river", false,
                WaterMaterial("Westbank moving water", new Color(.365f, .60f, .52f), new Color(.10f, .29f, .32f), .72f));
            var brook = ReplaceWater("Shallow Willow Brook", true,
                WaterMaterial("Willow Brook moving water", new Color(.40f, .61f, .50f), new Color(.16f, .34f, .31f), .41f));
            DressBanks();
            int surfaces = RefinishMeshSurfaces();
            int finishes = TuneKitAndVegetation();
            Physics.SyncTransforms();

            foreach (var pair in interactions)
                if (!pair.Key || Vector3.SqrMagnitude(pair.Key.transform.position - pair.Value) > .000001f)
                    throw new InvalidOperationException("A player, quest anchor, or actor moved during terrain authoring.");
            float minClearance = CheckRiver("Westbank flowing river", river, false);
            float brookClearance = CheckRiver("Shallow Willow Brook", brook, true);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the river and surface pass.");
            AssetDatabase.SaveAssets();
            int corrected = foliage.Count(item => Mathf.Abs(item.transform.position.y - item.before) > .025f);
            string report = $"PASS {TerrainFinishes.Length} existing terrain layers plus a wet shoreline layer across {activeTiles.Length} campaign tiles.\n" +
                $"PASS {surfaces} named ground, path, dungeon, and landmark renderers refinished.\n" +
                $"PASS {finishes} shared architecture, prop and vegetation finishes adjusted.\n" +
                $"PASS Westbank runs {Westbank[^1].along - Westbank[0].along:F0} m from its high spring cascade to the woodland pool. " +
                $"Minimum centerline water depth {minClearance:F2} m outside endpoint shallows.\n" +
                $"PASS Willow Brook runs {Willow[^1].along - Willow[0].along:F0} m through Greenhollow. Minimum centerline depth {brookClearance:F2} m.\n" +
                $"PASS {corrected} shoreline scenery anchors re-grounded. {interactions.Count} gameplay transforms preserved.\n";
            File.WriteAllText("../riftbound-river-material-checks.txt", report);
            Debug.Log(report);
        }

        static void EnsureFolder(string name)
        {
            if (!AssetDatabase.IsValidFolder(SurfaceFolder + "/" + name))
                AssetDatabase.CreateFolder(SurfaceFolder, name);
        }

        static void ImportTextures()
        {
            foreach (var key in TerrainFinishes.Select(f => f.texture).Concat(new[] { "silt", "vault" }))
            foreach (var channel in new[] { "albedo", "normal" })
            {
                string name = $"{key}_{channel}";
                string path = $"{SurfaceFolder}/Textures/{name}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (!importer) throw new InvalidOperationException("Blender surface texture missing: " + path);
                var type = channel == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
                if (importer.textureType != type || importer.wrapMode != TextureWrapMode.Repeat || importer.anisoLevel != 6)
                {
                    importer.textureType = type;
                    importer.sRGBTexture = channel != "normal";
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.anisoLevel = 6;
                    importer.mipmapEnabled = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.SaveAndReimport();
                }
                textures[name] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        static void ApplyLayer(TerrainLayer layer, string key, float tile, float normal)
        {
            layer.diffuseTexture = textures[key + "_albedo"];
            layer.normalMapTexture = textures[key + "_normal"];
            layer.tileSize = new Vector2(tile, tile);
            layer.normalScale = normal;
            layer.metallic = 0;
            layer.smoothness = key == "silt" ? .22f : key == "snow" ? .11f : .055f;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = Vector4.one;
            EditorUtility.SetDirty(layer);
        }

        static Material LoadOrCreate(string name, Shader shader)
        {
            string path = $"{SurfaceFolder}/Materials/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            return material;
        }

        static Material WaterMaterial(string label, Color shallows, Color channel, float speed)
        {
            var shader = Shader.Find("Riftbound/Flowing River");
            if (!shader || !shader.isSupported) throw new InvalidOperationException("The river shader could not be loaded.");
            var material = LoadOrCreate(label, shader);
            material.SetTexture("_RippleTex", AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "/Textures/river_albedo.png"));
            material.SetColor("_Shallows", shallows);
            material.SetColor("_Channel", channel);
            material.SetColor("_Foam", new Color(.73f, .83f, .75f));
            material.SetFloat("_Current", speed);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material SurfaceMaterial(string key, Vector2 scale)
        {
            string label = $"{key} {scale.x:0}x{scale.y:0}";
            if (surfaceMaterials.TryGetValue(label, out var saved)) return saved;
            var material = LoadOrCreate(label, Shader.Find("Universal Render Pipeline/Lit"));
            UsePattern(material, key, scale);
            surfaceMaterials.Add(label, material);
            return material;
        }

        static void UsePattern(Material material, string key, Vector2 scale)
        {
            material.SetTexture("_BaseMap", textures[key + "_albedo"]);
            material.SetTextureScale("_BaseMap", scale);
            material.SetTexture("_BumpMap", textures[key + "_normal"]);
            material.SetTextureScale("_BumpMap", scale);
            material.SetFloat("_BumpScale", .24f);
            material.EnableKeyword("_NORMALMAP");
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0);
            material.SetFloat("_Smoothness", .075f);
            EditorUtility.SetDirty(material);
        }

        static int RefinishMeshSurfaces()
        {
            var exact = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
                ["Watchtower courtyard"] = "cobbles", ["Ruined courtyard"] = "vault",
                ["Greenhollow village square"] = "cobbles", ["Willow Rest cache footing"] = "bedrock",
                ["Summit Dais Platform"] = "vault", ["Crater Bedrock"] = "canyon",
                ["Cathedral Stone Floor"] = "vault", ["Cathedral Vaulted Ceiling"] = "vault",
                ["Cathedral Left Wall"] = "vault", ["Cathedral Right Wall"] = "vault",
                ["Cathedral Entrance Back Wall"] = "vault", ["Cathedral Altar Back Wall"] = "vault",
                ["Altar Dais"] = "vault", ["Moonwell Stone Basin"] = "cobbles",
                ["Ancient grove trail"] = "forest", ["Northern frontier trail"] = "trail",
                ["Greenhollow approach"] = "trail", ["Camp approach"] = "trail",
                ["Watchtower trail"] = "trail", ["Western crypt trail"] = "trail",
                ["Forest road"] = "trail"
            };
            int count = 0;
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                string name = renderer.name;
                if (!exact.TryGetValue(name, out string key))
                {
                    if (!name.Contains("footway", StringComparison.OrdinalIgnoreCase) &&
                        !name.Contains("road surface", StringComparison.OrdinalIgnoreCase)) continue;
                    key = name.Contains("Highspire", StringComparison.OrdinalIgnoreCase) ? "cobbles" : "trail";
                }
                Vector3 size = renderer.bounds.size;
                // Broad slabs use repeat units in meters rather than stretching a single patch across the whole floor.
                var mesh = renderer.GetComponent<MeshFilter>()?.sharedMesh;
                Vector2 uvSpan = Vector2.one;
                if (mesh && mesh.isReadable && mesh.uv.Length > 0)
                {
                    var uv = mesh.uv;
                    uvSpan = new Vector2(Mathf.Max(.5f, uv.Max(p => p.x) - uv.Min(p => p.x)),
                        Mathf.Max(.5f, uv.Max(p => p.y) - uv.Min(p => p.y)));
                }
                float unit = key == "vault" ? 5.0f : 6.4f;
                var scale = new Vector2(
                    Mathf.Clamp(Mathf.Round(Mathf.Max(size.x, .4f) / unit / uvSpan.x), 1, 24),
                    Mathf.Clamp(Mathf.Round(Mathf.Max(size.z, size.y) / unit / uvSpan.y), 1, 40));
                var replacement = SurfaceMaterial(key, scale);
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => replacement).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                count++;
            }
            return count;
        }

        static int TuneKitAndVegetation()
        {
            string[] folders = { Kit + "/Regional Finishes", Kit + "/Materials", "Assets/RPG/Materials" };
            var visible = Object.FindObjectsByType<MeshRenderer>().Where(r => r.enabled)
                .SelectMany(r => r.sharedMaterials).Where(m => m).Distinct().ToArray();
            var stone = AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "/Textures/stone_albedo.png");
            var wood = AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "/Textures/wood_albedo.png");
            int adjusted = 0;
            foreach (var material in visible)
            {
                string path = AssetDatabase.GetAssetPath(material);
                if (!folders.Any(f => path.StartsWith(f + "/", StringComparison.Ordinal)) ||
                    !material.HasProperty("_BaseMap") || !material.HasProperty("_BaseColor")) continue;
                string name = material.name.Replace('_', ' ');
                if (name == "Nature Rock" || name == "Foundation")
                {
                    UsePattern(material, "bedrock", Vector2.one);
                    adjusted++;
                    continue;
                }

                bool foliage = name.StartsWith("Nature ", StringComparison.Ordinal) &&
                    (name.EndsWith("Leaves", StringComparison.Ordinal) || name.EndsWith("Grass", StringComparison.Ordinal));
                if (foliage)
                {
                    material.SetColor("_BaseColor", new Color(.71f, .81f, .65f));
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .07f);
                    EditorUtility.SetDirty(material);
                    adjusted++;
                    continue;
                }

                bool kit = path.StartsWith(Kit, StringComparison.Ordinal);
                string[] timber = { "Oak", "Oak Edge", "Dark Wood", "Leather" };
                string[] masonry = { "Limestone", "Pale Stone", "Basalt", "Slate", "Slate Edge", "Plaster", "Ember Edge", "Ember Tile" };
                if (!kit || !timber.Concat(masonry).Any(swatch =>
                    name == swatch || name.EndsWith(" " + swatch, StringComparison.Ordinal))) continue;
                if (!material.GetTexture("_BaseMap"))
                    material.SetTexture("_BaseMap", timber.Any(swatch => name.EndsWith(swatch, StringComparison.Ordinal)) ? wood : stone);
                if (material.HasProperty("_Smoothness"))
                    material.SetFloat("_Smoothness", Mathf.Min(.18f, material.GetFloat("_Smoothness")));
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
                EditorUtility.SetDirty(material);
                adjusted++;
            }
            return adjusted;
        }
    }
}
