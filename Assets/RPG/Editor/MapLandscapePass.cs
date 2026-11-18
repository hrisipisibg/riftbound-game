using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Riftbound.Editor
{
    public static class MapLandscapePass
    {
        const string Assets = "Assets/RPG/Terrain/FrontierArt";
        internal const string Dressing = "Frontier landscape and settlement dressing";
        const string Results = ".code-check/frontier-art";
        static readonly string[] Surfaces = { "Ashwood meadow", "Packed trail", "Fractured shale", "Moss and needles", "Alpine scree", "Wyrmcrest snow", "Redridge sandstone", "Hewn cobbles" };
        static readonly string[] OriginalSurfaces = { "Painted meadow", "Earthen trail", "Weathered stone", "Forest floor", "Rocky highland", "Summit snow", "Redridge canyon", "Cobblestone highway" };
        static readonly string[] Dark = { "384329", "68503A", "414747", "283427", "535652", "838E96", "644136", "4F4B44" };
        static readonly string[] Light = { "7D824D", "AA8962", "8F8B80", "686447", "9C9C8D", "E2E0CE", "AC7250", "959083" };
        static readonly float[] RepeatMetres = { 7, 6, 9, 7, 6, 9, 12, 4.5f };
        static Terrain[] tiles;

        public static void Run()
        {
            try
            {
                Directory.CreateDirectory(Results);
                Directory.CreateDirectory(Assets);
                AssetDatabase.Refresh();
                var scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                tiles = Terrain.activeTerrains.Where(t => t.name.StartsWith("Expanded terrain ", StringComparison.Ordinal)).ToArray();
                if (tiles.Length != 4) throw new InvalidOperationException($"Expected four expanded tiles, got {tiles.Length}.");
                if (GameObject.Find(Dressing)) throw new InvalidOperationException("This scene already contains the landscape pass.");
                var before = new StringBuilder();
                CheckRoutes(before);
                File.WriteAllText(Results + "/routes-before.txt", before.ToString());
                var layers = Enumerable.Range(0, Surfaces.Length).Select(CreateLayer).ToArray();
                foreach (var terrain in tiles) Paint(terrain, layers);
                BlendFootways();
                Physics.SyncTransforms();
                var root = new GameObject(Dressing).transform;
                var report = new StringBuilder("Outpost-Expanded landscape validation\n");
                DressWorld(root, report);
                FinishArt(root, report);
                var after = new StringBuilder();
                CheckRoutes(after);
                report.AppendLine(before.ToString() == after.ToString() ? "Route probe output matches the original scene." : "Route probe output differs from the original scene.");
                report.Append(after);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the landscape scene.");
                AssetDatabase.SaveAssets();
                RenderViews();
                File.WriteAllText(Results + "/validation.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                File.WriteAllText(Results + "/error.txt", error.ToString());
                EditorApplication.Exit(1);
            }
        }

        static Color Hex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString("#" + hex, out var color)) throw new ArgumentException(hex);
            return color;
        }

        static float Grain(int x, int y, int salt)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + salt * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177;
                return ((n ^ (n >> 16)) & 65535) / 65535f;
            }
        }

        static float Noise(float u, float v, int cells, int salt)
        {
            float x = u * cells, y = v * cells;
            int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float a = Mathf.SmoothStep(0, 1, x - ix), b = Mathf.SmoothStep(0, 1, y - iy);
            int x0 = ix % cells, x1 = (ix + 1) % cells, y0 = iy % cells, y1 = (iy + 1) % cells;
            return Mathf.Lerp(Mathf.Lerp(Grain(x0, y0, salt), Grain(x1, y0, salt), a),
                Mathf.Lerp(Grain(x0, y1, salt), Grain(x1, y1, salt), a), b);
        }

        static TerrainLayer CreateLayer(int index)
        {
            const int size = 512;
            int salt = 32 + index * 17;
            Color dark = Hex(Dark[index]), light = Hex(Light[index]);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float broad = Noise(u, v, 8, salt), grain = Noise(u, v, 116, salt + 2);
                float fine = Grain(x, y, salt + 3), flecks = Noise(u, v, 45, salt + 9);
                float tone = .20f + .29f * broad + .27f * grain + .12f * fine;
                if (index == 0 || index == 3) tone += .22f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .85f, flecks));
                if (index == 2 || index == 4 || index == 6)
                {
                    float band = 2 * Mathf.PI * (v * 16 + .45f * broad + .095f * grain);
                    tone += .23f * Mathf.Abs(Mathf.Sin(band)) - .23f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.66f, .9f, flecks));
                }
                if (index == 5) tone = .60f + .20f * broad + .12f * grain + .07f * fine;
                if (index == 7)
                {
                    float row = v * 14;
                    float col = u * 11 + (Mathf.FloorToInt(row) % 2) * .5f;
                    float dx = Mathf.Min(Mathf.Repeat(col, 1), 1 - Mathf.Repeat(col, 1));
                    float dy = Mathf.Min(Mathf.Repeat(row, 1), 1 - Mathf.Repeat(row, 1));
                    float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.027f, .105f, Mathf.Min(dx, dy) + (grain - .5f) * .05f));
                    var shade = Grain(Mathf.FloorToInt(col) % 11, Mathf.FloorToInt(row), salt);
                    tone = Mathf.Lerp(.045f + fine * .12f, .38f + .33f * shade + .21f * grain, edge);
                }
                pixels[y * size + x] = Color.Lerp(dark, light, tone);
            }

            string texturePath = $"{Assets}/{Surfaces[index]}.png";
            var image = new Texture2D(size, size, TextureFormat.RGB24, false);
            image.SetPixels32(pixels);
            image.Apply();
            File.WriteAllBytes(texturePath, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 8;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            string layerPath = $"{Assets}/{Surfaces[index]}.terrainlayer";
            var source = AssetDatabase.FindAssets($"{OriginalSurfaces[index]} t:TerrainLayer", new[] { "Assets/RPG/Terrain" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(path => Path.GetFileNameWithoutExtension(path) == OriginalSurfaces[index])
                .Select(AssetDatabase.LoadAssetAtPath<TerrainLayer>).First();
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            if (!layer) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, layerPath); }
            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.normalMapTexture = source.normalMapTexture;
            layer.normalScale = Mathf.Min(.85f, source.normalScale);
            layer.tileSize = Vector2.one * RepeatMetres[index];
            layer.smoothness = index == 5 ? .16f : .09f;
            SetTerrainRoughness(layer);
            return layer;
        }

        static void SetTerrainRoughness(TerrainLayer layer)
        {
            var serialized = new SerializedObject(layer);
            serialized.FindProperty("m_SmoothnessSource").intValue = 2; // Use the authored constant. RGB terrain textures have no smoothness alpha.
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(layer);
        }

        static float Smooth(float start, float end, float value) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(start, end, value));

        static float Area(float x, float z, float cx, float cz, float rx, float rz)
        {
            float distance = new Vector2((x - cx) / rx, (z - cz) / rz).magnitude;
            return 1 - Smooth(.56f, 1.18f, distance);
        }

        static void Paint(Terrain terrain, TerrainLayer[] layers)
        {
            var data = terrain.terrainData;
            var origin = terrain.GetPosition();
            int w = data.alphamapWidth, h = data.alphamapHeight;
            var existing = data.GetAlphamaps(0, 0, w, h);
            var oldNames = data.terrainLayers.Select(l => l ? l.name : "").ToArray();
            int trailIndex = Array.IndexOf(oldNames, OriginalSurfaces[1]);
            int pavingIndex = Array.IndexOf(oldNames, OriginalSurfaces[7]);
            if (trailIndex < 0 || pavingIndex < 0) throw new InvalidOperationException(terrain.name + " has unexpected source terrain layers: " + string.Join(", ", oldNames));
            var paint = new float[h, w, layers.Length];
            for (int j = 0; j < h; j++) for (int i = 0; i < w; i++)
            {
                float u = i / (float)(w - 1), v = j / (float)(h - 1);
                float x = origin.x + u * data.size.x, z = origin.z + v * data.size.z;
                float elevation = origin.y + data.GetInterpolatedHeight(u, v);
                float slope = data.GetSteepness(u, v);
                float variation = Mathf.PerlinNoise((x + 800) * .037f, (z + 800) * .037f);
                float trail = existing[j, i, trailIndex];
                float paving = existing[j, i, pavingIndex];
                if (i == 0 || j == 0 || i == w - 1 || j == h - 1)
                {
                    // Paint the shared edge from world coordinates on both tiles.
                    paving = 0;
                    trail = (1 - Smooth(2.8f, 6.5f, WorldExpansionLayout.RoadDistance(x, z))) * .76f;
                }
                float open = Mathf.Max(0, 1 - trail - paving);
                float woodland = Mathf.Max(Area(x, z, -204, 216, 139, 178), Area(x, z, -32, 437, 129, 140));
                float canyon = Mathf.Max(Area(x, z, 285, 227, 170, 150), Area(x, z, 195, 293, 102, 89));
                float snow = Smooth(57, 80, elevation + 9 * (variation - .5f));
                float scree = Smooth(17, 57, elevation) * (.45f + .4f * variation) * (1 - snow);
                float rock = Smooth(21, 48, slope + 9 * (variation - .5f));
                float moss = woodland * (.5f + .42f * variation);
                canyon *= .72f + .27f * variation;
                paint[j, i, 7] = paving;
                paint[j, i, 1] = trail;
                paint[j, i, 5] = open * snow * (1 - rock * .63f);
                open -= paint[j, i, 5];
                paint[j, i, 2] = open * rock * (1 - canyon * .52f);
                open -= paint[j, i, 2];
                paint[j, i, 6] = open * canyon;
                open -= paint[j, i, 6];
                paint[j, i, 4] = open * scree;
                open -= paint[j, i, 4];
                paint[j, i, 3] = open * moss;
                paint[j, i, 0] = open * (1 - moss);
            }
            data.terrainLayers = layers;
            data.SetAlphamaps(0, 0, paint);
            terrain.Flush();
            EditorUtility.SetDirty(data);
        }

        static void BlendFootways()
        {
            foreach (string name in new[] { "Greenhollow arrival footway", "Highspire north footway", "Emberwatch west departure" })
            {
                var points = SettlementRoutePass.ReadPath(name);
                if (points == null) continue;
                float left = points.Min(p => p.x) - 4, right = points.Max(p => p.x) + 4;
                float near = points.Min(p => p.z) - 4, far = points.Max(p => p.z) + 4;
                foreach (var tile in tiles)
                {
                    var data = tile.terrainData;
                    var origin = tile.GetPosition();
                    if (origin.x > right || origin.x + data.size.x < left || origin.z > far || origin.z + data.size.z < near) continue;
                    int width = data.alphamapWidth, height = data.alphamapHeight;
                    var splat = data.GetAlphamaps(0, 0, width, height);
                    for (int j = 0; j < height; j++)
                    {
                        float z = origin.z + j * data.size.z / (height - 1);
                        if (z < near || z > far) continue;
                        for (int i = 0; i < width; i++)
                        {
                            float x = origin.x + i * data.size.x / (width - 1);
                            if (x < left || x > right) continue;
                            var point = new Vector2(x, z);
                            float distance = 5;
                            for (int k = 1; k < points.Length; k++)
                                distance = Mathf.Min(distance, WorldExpansionLayout.SegmentDistance(point, points[k - 1], points[k], out _));
                            float weight = 1 - Smooth(.85f, 2.9f, distance);
                            if (weight <= 0) continue;
                            float cobble = splat[j, i, 7];
                            weight *= 1 - cobble * .82f;
                            for (int k = 0; k < splat.GetLength(2); k++) splat[j, i, k] *= 1 - weight;
                            splat[j, i, 1] += weight;
                        }
                    }
                    data.SetAlphamaps(0, 0, splat);
                    tile.Flush();
                    EditorUtility.SetDirty(data);
                }
                var renderer = GameObject.Find(name).GetComponent<MeshRenderer>();
                if (renderer) renderer.enabled = false;
            }
        }

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static bool Ground(Vector2 point, float maxSlope, out Vector3 position)
        {
            position = default;
            foreach (var tile in tiles)
            {
                var p = tile.GetPosition();
                var size = tile.terrainData.size;
                float u = (point.x - p.x) / size.x, v = (point.y - p.z) / size.z;
                if (u < 0 || u > 1 || v < 0 || v > 1) continue;
                if (tile.terrainData.GetSteepness(u, v) > maxSlope) return false;
                position = new Vector3(point.x, p.y + tile.terrainData.GetInterpolatedHeight(u, v), point.y);
                return true;
            }
            return false;
        }

        static void DressWorld(Transform root, StringBuilder report)
        {
            Cluster(root, report, "Ashwood hedgerows", 0, 87, 103, 114, 180, 1, "T_BirchTree_1", "T_Bush", "T_Flower_1_Clump", "T_Rock_2");
            Cluster(root, report, "Highspire alpine gardens", -206, -71, 64, 53, 77, 2, "T_PineTree_2", "T_Rock_1", "T_Bush_Large", "T_Flower_3_Clump");
            Cluster(root, report, "Frostpeak scree and pine", -101, -108, 55, 46, 67, 3, "T_PineTree_1", "T_Rock_1", "T_Rock_2", "T_Rock_3");
            Cluster(root, report, "Silverglen goldleaf groves", -144, 105, 71, 60, 109, 4, "T_MapleTree_1", "T_BirchTree_1", "T_Flower_3_Clump", "T_Bush");
            Cluster(root, report, "Duskwood and Cathedral margins", -218, 244, 92, 118, 160, 5, "T_DeadTree_1", "T_PineTree_2", "T_Rock_2", "T_Bush_Large");
            Cluster(root, report, "Greenhollow orchard and pasture", -27, 417, 103, 80, 158, 6, "T_NormalTree_2", "T_MapleTree_2", "T_Flower_1_Clump", "T_Bush");
            Cluster(root, report, "Westbank willow embankment", -267, 12, 26, 100, 54, 7, "T_BirchTree_2", "T_Bush_Large", "T_Flower_3_Clump", "T_Rock_2");
            Cluster(root, report, "Redridge outcrops and scrub", 263, 209, 131, 122, 178, 8, "T_Rock_3", "T_Rock_1", "T_DeadTree_2", "T_Bush");
            Cluster(root, report, "Wyrmcrest pine line", 274, 449, 94, 78, 81, 9, "T_PineTree_1", "T_Rock_1", "T_Rock_2", "T_DeadTree_2");
            DressSettlements(root, report);
        }

        static void Cluster(Transform root, StringBuilder report, string name, float x, float z, float sx, float sz, int count, int seed, params string[] prefabs)
        {
            var group = Group(name, root);
            var assets = prefabs.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/RPG/Prefabs/Nature/{p}.prefab")).ToArray();
            if (assets.Any(a => !a)) throw new IOException("Missing landscape prefab for " + name);
            var rng = new System.Random(seed * 571);
            var positions = new System.Collections.Generic.List<Vector3>();
            for (int attempt = 0; attempt < count * 8 && positions.Count < count; attempt++)
            {
                float angle = (float)rng.NextDouble() * Mathf.PI * 2;
                float radius = Mathf.Sqrt((float)rng.NextDouble());
                var point = new Vector2(x + sx * Mathf.Cos(angle) * radius, z + sz * Mathf.Sin(angle) * radius);
                if (WorldExpansionLayout.RoadDistance(point.x, point.y) < 10) continue;
                if (point.y > -125 && point.y < 135 && Mathf.Abs(point.x - WorldExpansionLayout.RiverX(point.y)) < 8) continue;
                if (Mathf.PerlinNoise((point.x + 500) * .052f, (point.y + 500) * .052f) < .38f) continue;
                if (!Ground(point, 29, out var p) || positions.Any(other => (other - p).sqrMagnitude < 12.25f)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 1.05f, .84f, ~0, QueryTriggerInteraction.Ignore)) continue;
                int variant = rng.Next(assets.Length);
                float scale = Mathf.Lerp(.74f, 1.36f, (float)rng.NextDouble());
                Place(assets[variant], group, p, scale, (float)rng.NextDouble() * 360);
                positions.Add(p);
            }
            report.AppendLine($"{name}: {positions.Count} placements.");
        }

        static void Place(GameObject asset, Transform parent, Vector3 point, float scale, float yaw)
        {
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            prop.transform.SetPositionAndRotation(point, Quaternion.Euler(0, yaw, 0));
            prop.transform.localScale *= scale;
            foreach (var collider in prop.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var renderer in prop.GetComponentsInChildren<MeshRenderer>()) renderer.receiveShadows = true;
        }

        static void GradeLighting()
        {
            RenderSettings.ambientSkyColor = Hex("B3B9BF");
            RenderSettings.ambientEquatorColor = Hex("9A9C8E");
            RenderSettings.ambientGroundColor = Hex("595147");
            RenderSettings.fogColor = Hex("9FAEB1");
            var sun = RenderSettings.sun ?? UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(light => light.type == LightType.Directional);
            if (!sun) return;
            sun.color = Hex("FFF1DA");
            sun.shadowStrength = .73f;
        }

        static void FinishArt(Transform root, StringBuilder report)
        {
            GradeLighting();
            StyleTown(root, report, "Reaver camp river pickets", -283, 78, 13, "663C3A", "8F7C61");
            BuildShelter(root, report, "Emberwatch road market", new Vector2(0, 4), 35, 125, "8B493B", "AD8956", .96f);
            var water = GameObject.Find("Westbank flowing river")?.GetComponent<MeshRenderer>();
            if (water)
            {
                var surface = Variant(water.sharedMaterial, "Westbank flowing water");
                surface.color = new Color(.18f, .43f, .50f, .91f);
                surface.SetFloat("_Smoothness", .26f);
                water.sharedMaterial = surface;
                EditorUtility.SetDirty(surface);
            }

            var palette = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source || !source.name.StartsWith("Nature_") || !source.name.EndsWith("_Leaves")) continue;
                    if (!palette.TryGetValue(source, out var leaf))
                    {
                        leaf = Variant(source, source.name + " frontier");
                        Color tinted = Color.Lerp(source.color, Hex(source.name.Contains("Birch") ? "A38045" : "4E6A46"), .53f);
                        leaf.color = tinted;
                        leaf.SetFloat("_Smoothness", .10f);
                        EditorUtility.SetDirty(leaf);
                        palette.Add(source, leaf);
                    }
                    materials[i] = leaf;
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }

        static Material Variant(Material source, string name)
        {
            string path = $"{Assets}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            var material = new Material(source);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void CheckRoutes(StringBuilder report)
        {
            Physics.SyncTransforms();
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var motor = player ? player.GetComponent<CharacterController>() : null;
            foreach (var route in WorldTraversalVerification.Routes)
                foreach (bool mounted in new[] { false, true })
                    foreach (bool reverse in new[] { false, true })
                        WorldTraversalVerification.Traverse(route, mounted, reverse, motor, report);
        }

        static void RenderViews()
        {
            Directory.CreateDirectory(".code-check");
            Render("world-overview", new Vector3(64, 650, 224), new Vector3(64, 0, 224), true);
            Render("emberwatch", new Vector3(-18, 19, -28), new Vector3(0, 4, 20));
            Render("highspire", new Vector3(-163, 37, -27), new Vector3(-203, 19, -61));
            Render("greenhollow", new Vector3(-18, 28, 365), new Vector3(-35, 15, 403));
            Render("redridge", new Vector3(325, 58, 145), new Vector3(280, 20, 215));
            Render("wyrmcrest", new Vector3(333, 114, 429), new Vector3(249, 79, 517));
            Render("westbank", new Vector3(-240, 28, 28), new Vector3(-271, 6, 53));
        }

        static void Render(string name, Vector3 position, Vector3 target, bool overhead = false)
        {
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                WorldPresentationSetup.RenderPreview(name, position, target, overhead);
            }
            finally { ShaderUtil.allowAsyncCompilation = asyncShaders; }
            File.Copy($".code-check/{name}.png", $"{Results}/{name}.png", true);
        }

        static void DressSettlements(Transform root, StringBuilder report)
        {
            StyleTown(root, report, "Emberwatch market gardens", 0, 4, 14, "A04331", "A69463");
            StyleTown(root, report, "Highspire heraldic court", -200, -60, 22, "3C596D", "AE996B");
            StyleTown(root, report, "Silverglen autumn court", -150, 75, 17, "536953", "BA9752");
            StyleTown(root, report, "Greenhollow trading post", -40, 400, 15, "316863", "C39E59");
            StyleTown(root, report, "Bloodrock frontier camp", 280, 210, 21, "85433C", "D1A160");
            StyleTown(root, report, "Gor'Kresh stronghold grounds", 340, 290, 27, "6F3036", "A98960");
            StyleTown(root, report, "Westbank river rest", -285, 59, 10, "355D5A", "C09E67");
            StyleTown(root, report, "Duskwood cathedral approach", -220, 260, 17, "4F495D", "B9A687");
            BuildShelter(root, report, "Emberwatch market pavilion", new Vector2(0, 4), 17, 118, "8B493B", "AD8956", 1.12f);
            BuildShelter(root, report, "Greenhollow orchard shelter", new Vector2(-40, 400), 21, 205, "4D6555", "AF9565", .98f);
            BuildShelter(root, report, "Bloodrock hide market", new Vector2(280, 210), 26, 98, "8B5143", "685649", 1.05f);
            BuildStandingStones(root, report, "Silverglen ward circle", new Vector2(-150, 75), 27, 70, "8F8C78", "557F75");
            BuildStandingStones(root, report, "Wyrmcrest trail cairns", new Vector2(250, 510), 27, 215, "777E83", "7C9CA7");
        }

        static bool FindFeatureSite(Vector2 center, float distance, float bearing, float clearance, out Vector3 point)
        {
            for (int i = 0; i < 24; i++)
            {
                float angle = (bearing + i * 137.5f) * Mathf.Deg2Rad;
                var target = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (distance + i / 5 * 2);
                if (WorldExpansionLayout.RoadDistance(target.x, target.y) < clearance + 7 || !Ground(target, 9f, out point)) continue;
                if (target.y > -125 && target.y < 135 && Mathf.Abs(target.x - WorldExpansionLayout.RiverX(target.y)) < clearance + 9) continue;
                var half = new Vector3(clearance, 1.95f, clearance);
                if (!Physics.CheckBox(point + Vector3.up * 2.65f, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) return true;
            }
            point = default;
            return false;
        }

        static void BuildShelter(Transform root, StringBuilder report, string name, Vector2 center, float distance, float bearing, string roofColor, string trimColor, float scale)
        {
            if (!FindFeatureSite(center, distance, bearing, 3.25f * scale, out var point))
            {
                report.AppendLine(name + ": no clear site.");
                return;
            }
            var shelter = Group(name, root);
            shelter.position = point;
            shelter.localScale = Vector3.one * scale;
            shelter.rotation = Quaternion.LookRotation(new Vector3(center.x - point.x, 0, center.y - point.z));
            var wood = Material("Ironwood posts", "514337");
            var stone = Material("Weathered stone bases", "847F70");
            var roof = Material(name + " canopy", roofColor);
            var trim = Material(name + " canopy ribs", trimColor);
            Part(shelter, "Raised stone terrace", PrimitiveType.Cube, new Vector3(0, -.04f, 0), new Vector3(5, .34f, 3.85f), stone);
            foreach (float x in new[] { -2.1f, 2.1f }) foreach (float z in new[] { -1.53f, 1.53f })
            {
                Part(shelter, "Stone pier", PrimitiveType.Cube, new Vector3(x, .25f, z), new Vector3(.44f, .42f, .44f), stone);
                Part(shelter, "Timber pillar", PrimitiveType.Cube, new Vector3(x, 1.5f, z), new Vector3(.25f, 2.6f, .25f), wood);
            }
            foreach (float sign in new[] { -1f, 1f })
            {
                var plane = Part(shelter, "Pitched roof panel", PrimitiveType.Cube, new Vector3(0, 3.23f, sign * .96f), new Vector3(5.45f, .15f, 2.27f), roof);
                plane.localRotation = Quaternion.Euler(sign * 28f, 0, 0);
                Part(shelter, "Eave beam", PrimitiveType.Cube, new Vector3(0, 2.75f, sign * 1.95f), new Vector3(5.62f, .23f, .16f), wood);
                Part(shelter, "Eave trim", PrimitiveType.Cube, new Vector3(0, 2.67f, sign * 2.02f), new Vector3(5.39f, .065f, .04f), trim);
                for (int rib = -4; rib <= 4; rib++)
                {
                    var strip = Part(shelter, "Canopy seam", PrimitiveType.Cube, new Vector3(rib * .58f, 3.32f, sign * .96f), new Vector3(.028f, .035f, 2.19f), trim);
                    strip.localRotation = plane.localRotation;
                }
            }
            Part(shelter, "Roof ridge cap", PrimitiveType.Cube, new Vector3(0, 3.80f, 0), new Vector3(5.6f, .15f, .2f), wood);
            Part(shelter, "Market counter", PrimitiveType.Cube, new Vector3(0, .94f, -.84f), new Vector3(2.85f, .15f, .69f), trim);
            for (int leg = -1; leg <= 1; leg += 2)
                Part(shelter, "Counter leg", PrimitiveType.Cube, new Vector3(leg * 1.18f, .49f, -.84f), new Vector3(.18f, .85f, .54f), wood);
            report.AppendLine($"{name}: {point}.");
        }

        static void BuildStandingStones(Transform root, StringBuilder report, string name, Vector2 center, float distance, float bearing, string rock, string inlay)
        {
            if (!FindFeatureSite(center, distance, bearing, 3.6f, out var point))
            {
                report.AppendLine(name + ": no clear site.");
                return;
            }
            var circle = Group(name, root);
            circle.position = point;
            var stone = Material(name + " stone", rock);
            var inset = Material(name + " inlay", inlay, .28f);
            Part(circle, "Central medallion", PrimitiveType.Cylinder, new Vector3(0, .1f, 0), new Vector3(2.2f, .13f, 2.2f), stone);
            Part(circle, "Medallion cap", PrimitiveType.Cylinder, new Vector3(0, .25f, 0), new Vector3(1.89f, .046f, 1.89f), inset);
            for (int i = 0; i < 6; i++)
            {
                float angle = i * Mathf.PI / 3;
                var offset = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * 2.72f;
                if (!Ground(new Vector2(point.x + offset.x, point.z + offset.z), 18, out var foot)) continue;
                var pillar = Group("Weathered standing stone", circle);
                pillar.position = foot;
                pillar.rotation = Quaternion.Euler(1.5f * (i % 3 - 1), i * 60f, 4 * (i % 2 == 0 ? -1 : 1));
                Part(pillar, "Stone foot", PrimitiveType.Cylinder, new Vector3(0, .11f, 0), new Vector3(.72f, .16f, .65f), stone);
                Part(pillar, "Upright", PrimitiveType.Cube, new Vector3(0, .9f, 0), new Vector3(.48f, 1.67f + .18f * (i % 3), .43f), stone);
                var mark = Part(pillar, "Diamond inlay", PrimitiveType.Cube, new Vector3(0, 1.34f, -.221f), new Vector3(.24f, .24f, .018f), inset);
                mark.localRotation = Quaternion.Euler(0, 0, 45);
            }
            report.AppendLine($"{name}: {point}.");
        }

        static Material Material(string name, string color, float metallic = 0)
        {
            string path = $"{Assets}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.color = Hex(color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", .18f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void StyleTown(Transform root, StringBuilder report, string name, float x, float z, float radius, string clothHex, string trimHex)
        {
            var group = Group(name, root);
            var cloth = Material(name + " cloth", clothHex);
            var trim = Material(name + " trim", trimHex, .2f);
            var oak = Material("Ironwood posts", "514337");
            var stone = Material("Weathered stone bases", "847F70");
            var bush = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Prefabs/Nature/T_Bush.prefab");
            var flowers = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Prefabs/Nature/T_Flower_3_Clump.prefab");
            int placed = 0;
            for (int i = 0; i < 18 && placed < 5; i++)
            {
                float angle = i * 2.399963f;
                float offset = radius + i * .55f;
                var point = new Vector2(x + Mathf.Cos(angle) * offset, z + Mathf.Sin(angle) * offset);
                if (WorldExpansionLayout.RoadDistance(point.x, point.y) < 5.5f || !Ground(point, 14, out var p)) continue;
                if (Physics.CheckCapsule(p + Vector3.up * .65f, p + Vector3.up * 3.3f, .63f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var accent = Group(placed % 2 == 0 ? "Heraldic lantern and planting" : "Planted stone bed", group);
                accent.position = p;
                accent.rotation = Quaternion.LookRotation(new Vector3(x - p.x, 0, z - p.z));
                if (placed % 2 == 0) Lantern(accent, oak, stone, cloth, trim);
                else Planter(accent, stone, bush, flowers);
                placed++;
            }
            report.AppendLine($"{name}: {placed} town or landmark accents.");
        }

        static void Lantern(Transform root, Material oak, Material stone, Material cloth, Material trim)
        {
            Part(root, "Plinth", PrimitiveType.Cylinder, new Vector3(0, .14f, 0), new Vector3(.64f, .14f, .64f), stone);
            Part(root, "Post", PrimitiveType.Cube, new Vector3(0, 1.33f, 0), new Vector3(.17f, 2.55f, .17f), oak);
            Part(root, "Arm", PrimitiveType.Cube, new Vector3(.33f, 2.54f, 0), new Vector3(.92f, .13f, .13f), oak);
            Part(root, "Lantern frame", PrimitiveType.Cube, new Vector3(.66f, 2.7f, 0), new Vector3(.26f, .36f, .26f), trim);
            Part(root, "Lantern glass", PrimitiveType.Cube, new Vector3(.66f, 2.7f, 0), new Vector3(.275f, .23f, .275f), Material("Honey lantern glass", "E6B568"));
            Part(root, "Banner", PrimitiveType.Cube, new Vector3(.39f, 2.04f, .035f), new Vector3(.56f, .72f, .027f), cloth);
            Part(root, "Banner piping", PrimitiveType.Cube, new Vector3(.39f, 1.72f, .055f), new Vector3(.56f, .045f, .016f), trim);
            var emblem = Part(root, "Banner diamond", PrimitiveType.Cube, new Vector3(.39f, 2.1f, .057f), new Vector3(.17f, .17f, .015f), trim);
            emblem.localRotation = Quaternion.Euler(0, 0, 45);
        }

        static void Planter(Transform root, Material stone, GameObject bush, GameObject flowers)
        {
            Part(root, "Stone flower bed", PrimitiveType.Cylinder, new Vector3(0, .14f, 0), new Vector3(1.16f, .14f, .93f), stone);
            Part(root, "Bed inset", PrimitiveType.Cylinder, new Vector3(0, .285f, 0), new Vector3(.94f, .025f, .74f), Material("Planter soil", "4F4032"));
            Place(bush, root, root.TransformPoint(new Vector3(0, .28f, -.1f)), .42f, 45);
            Place(flowers, root, root.TransformPoint(new Vector3(.27f, .28f, .19f)), .7f, 140);
            Place(flowers, root, root.TransformPoint(new Vector3(-.27f, .28f, .18f)), .57f, 220);
        }

        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var prop = GameObject.CreatePrimitive(type);
            prop.name = name;
            UnityEngine.Object.DestroyImmediate(prop.GetComponent<Collider>());
            prop.GetComponent<MeshRenderer>().sharedMaterial = material;
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = position;
            prop.transform.localScale = scale;
            return prop.transform;
        }
    }
}
