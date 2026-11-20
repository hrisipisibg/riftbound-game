using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    /// <summary>
    /// Fills the generated expanded world with biome-aware GPU-instanced terrain trees, rocks, grass and flowers.
    /// Only writes to the four "World tile" TerrainData assets and new prefab wrappers; scene objects are untouched.
    /// Safe to re-run (it replaces previous trees/details). Run it with Outpost-Expanded.unity open.
    /// </summary>
    public static class WorldDressingPass
    {
        const string Marker = "WorldDressingResult.txt";
        const string NatureDir = "Assets/RPG/Art/Nature/";
        const string MatDir = "Assets/RPG/Materials/";
        const string PrefabDir = "Assets/RPG/Prefabs/Nature/";
        const int DetailRes = 384, DetailPatch = 16;

        // Tree prototype indices
        static readonly string[] TreeFbx =
        {
            "NormalTree_1","NormalTree_2","PineTree_1","PineTree_2","BirchTree_1","BirchTree_2",
            "MapleTree_1","MapleTree_2","DeadTree_1","DeadTree_2","Bush","Bush_Large","Rock_1","Rock_2","Rock_3"
        };
        static readonly int[] Temperate = { 0, 1, 4, 5, 6, 7, 0, 4, 10, 11 };
        static readonly int[] Haunted = { 8, 9, 2, 3, 8, 2, 10 };
        static readonly int[] Willow = { 6, 7, 4, 5, 6, 1, 10 };
        static readonly int[] Highland = { 2, 3, 2, 3, 2, 12, 13 };
        static readonly int[] Canyon = { 8, 9, 8, 12, 13, 14, 12 };
        static readonly int[] Rocks = { 12, 13, 14 };

        static readonly string[] DetailFbx = { "Grass_Large", "Grass_Small", "Flower_1_Clump", "Flower_3_Clump" };

        public static void BatchRun()
        {
            try
            {
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                Run(true);
                EditorSceneManager.SaveOpenScenes();
                File.WriteAllText(Marker, "Dressed (batch) " + DateTime.Now);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); File.WriteAllText(Marker, "FAILED: " + e); EditorApplication.Exit(1); }
        }

        [MenuItem("Riftbound/World/Dress terrain (trees, rocks, grass, flowers)")]
        public static void Run() { Run(false); }
        public static void Run(bool silent)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Riftbound] Exit Play Mode first."); return; }
            var terrains = new List<Terrain>();
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t.name.StartsWith("Expanded terrain") && t.terrainData) terrains.Add(t);
            if (terrains.Count == 0)
            {
                if (!silent) EditorUtility.DisplayDialog("World dressing", "Open Outpost-Expanded.unity first (Riftbound > Generate & Open Big Zone World).", "OK");
                return;
            }

            Directory.CreateDirectory(PrefabDir.TrimEnd('/'));
            AssetDatabase.Refresh();
            foreach (string matName in new[] { "Nature_Bush_Leaves", "Nature_Flowers", "Nature_NormalTree_Bark", "Nature_NormalTree_Leaves", "Nature_PineTree_Bark", "Nature_PineTree_Leaves", "Nature_BirchTree_Bark", "Nature_BirchTree_Leaves", "Nature_Rock" })
                EnableInstancing(matName);

            var treePrefabs = new List<GameObject>();
            foreach (var n in TreeFbx) { var p = MakePrefab(n); if (!p) { Debug.LogError("[Riftbound] Missing nature model " + n); return; } treePrefabs.Add(p); }
            var detailPrefabs = new List<GameObject>();
            foreach (var n in DetailFbx) { var p = MakePrefab(n); if (p) detailPrefabs.Add(p); }

            int totalTrees = 0, totalDetail = 0;
            try
            {
                for (int i = 0; i < terrains.Count; i++)
                {
                    EditorUtility.DisplayProgressBar("Dressing world", terrains[i].name, i / (float)terrains.Count);
                    Dress(terrains[i], treePrefabs, detailPrefabs, 9137 + i * 31, out int trees, out int detail);
                    totalTrees += trees; totalDetail += detail;
                }
            }
            finally { EditorUtility.ClearProgressBar(); }

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(terrains[0].gameObject.scene);
            string msg = $"Planted {totalTrees} terrain trees/rocks and {totalDetail} grass/flower cells over {terrains.Count} tiles. Save the scene (Ctrl+S) to keep the terrain settings.";
            Debug.Log("<color=#70df80>[Riftbound]</color> " + msg);
            if (!silent) EditorUtility.DisplayDialog("World dressing complete", msg, "OK");
        }

        [MenuItem("Riftbound/World/Remove terrain dressing")]
        public static void Remove()
        {
            foreach (var t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Expanded terrain") || !t.terrainData) continue;
                t.terrainData.SetTreeInstances(new TreeInstance[0], false);
                t.terrainData.treePrototypes = new TreePrototype[0];
                t.terrainData.detailPrototypes = new DetailPrototype[0];
                EditorUtility.SetDirty(t.terrainData);
            }
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- prefabs / materials
        static void EnableInstancing(string name)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");
            if (m && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
        }

        static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");

        static GameObject MakePrefab(string fbxName)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(NatureDir + fbxName + ".fbx");
            if (!fbx) return null;
            var copy = Object.Instantiate(fbx);
            copy.name = "T_" + fbxName;
            AssignMaterials(copy, fbxName, fbx);
            var prefab = PrefabUtility.SaveAsPrefabAsset(copy, PrefabDir + "T_" + fbxName + ".prefab");
            Object.DestroyImmediate(copy);
            return prefab;
        }

        static void AssignMaterials(GameObject go, string fbxName, GameObject fbxSource = null)
        {
            string family = fbxName.StartsWith("PineTree") ? "PineTree" : fbxName.StartsWith("BirchTree") ? "BirchTree" :
                            fbxName.Contains("Tree") ? "NormalTree" : "";
            bool dead = fbxName.StartsWith("DeadTree"), rock = fbxName.StartsWith("Rock"), bush = fbxName.StartsWith("Bush"),
                 grass = fbxName.StartsWith("Grass"), flower = fbxName.StartsWith("Flower");
            var srcRenderers = fbxSource ? fbxSource.GetComponentsInChildren<Renderer>(true) : null;
            var dstRenderers = go.GetComponentsInChildren<Renderer>(true);
            for (int rIdx = 0; rIdx < dstRenderers.Length; rIdx++)
            {
                var r = dstRenderers[rIdx];
                var srcMats = (srcRenderers != null && rIdx < srcRenderers.Length) ? srcRenderers[rIdx].sharedMaterials : null;
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material refMat = (srcMats != null && i < srcMats.Length && srcMats[i]) ? srcMats[i] : mats[i];
                    string n = refMat ? refMat.name.ToLowerInvariant() : "";
                    bool isBark = n.Contains("bark") || n.Contains("trunk") || n.Contains("wood") || n.Contains("brown");
                    bool isLeaf = n.Contains("leaf") || n.Contains("leav") || n.Contains("green") || n.Contains("foliage") || n.Contains("orange") || n.Contains("yellow") || n.Contains("autumn");
                    if (!isBark && !isLeaf && refMat && refMat.HasProperty("_Color"))
                    {
                        Color c = refMat.color;
                        // Birch bark in the FBX is white/grey (high R,G,B, low saturation); leaves are green/yellow/orange (G > B + 0.15)
                        if (c.g > c.b + 0.12f || (c.r > 0.6f && c.g > 0.4f && c.b < 0.3f)) isLeaf = true;
                        else isBark = true;
                    }
                    Material m = null;
                    if (rock) m = Mat("Nature_Rock");
                    else if (grass) m = Mat("Nature_Bush_Leaves");
                    else if (flower) m = (isLeaf || n.Contains("stem")) ? Mat("Nature_Bush_Leaves") : Mat("Nature_Flowers");
                    else if (bush) m = (n.Contains("flower") || n.Contains("petal") || n.Contains("pink") || n.Contains("red") || n.Contains("yellow") || n.Contains("white") || n.Contains("purple")) ? Mat("Nature_Flowers") : Mat("Nature_Bush_Leaves");
                    else if (dead) m = Mat("Nature_NormalTree_Bark");
                    else if (family != "") m = Mat("Nature_" + family + (isBark ? "_Bark" : isLeaf ? "_Leaves" : (i == 0 ? "_Bark" : "_Leaves")));
                    if (m) mats[i] = m;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = grass || flower ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        // ---------------------------------------------------------------- placement rules
        static bool InBox(float x, float z, Vector2 c, Vector2 half, float margin) =>
            Mathf.Abs(x - c.x) <= half.x + margin && Mathf.Abs(z - c.y) <= half.y + margin;

        /// <summary>True where nothing natural should be planted (villages, capitals, roads, river, original core).</summary>
        static bool Blocked(float x, float z, float roadClear, float riverClear)
        {
            if (InBox(x, z, new Vector2(0, 18), new Vector2(83, 90), 0)) return true; // hand-built Emberwatch core
            if (InBox(x, z, new Vector2(-40, 400), new Vector2(28, 28), 0)) return true; // Greenhollow village square & buildings
            foreach (var r in WorldExpansionLayout.Regions)
            {
                var c = r.center; if (r.group == "08 - Greenhollow Frontier") c += new Vector2(23, 9);
                if (r.group == "08 - Greenhollow Frontier") continue; // big natural valley: trees allowed
                if (InBox(x, z, c, r.halfSize, 7f)) return true;
            }
            if (WorldExpansionLayout.RoadDistance(x, z) < roadClear) return true;
            if (z > -130 && z < 140 && Mathf.Abs(x - WorldExpansionLayout.RiverX(z)) < riverClear) return true;
            return false;
        }

        static float Grove(float x, float z) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, .66f, Mathf.PerlinNoise(x * .017f + 31, z * .017f + 7)));

        static int[] BiomeFamily(float x, float z, float h, out float density)
        {
            float dusk = WorldExpansionLayout.BoxWeight(x, z, new Vector2(-220, 235), new Vector2(75, 85), 30);
            float green = WorldExpansionLayout.BoxWeight(x, z, new Vector2(-40, 410), new Vector2(95, 70), 30);
            float canyon = WorldExpansionLayout.BoxWeight(x, z, new Vector2(270, 230), new Vector2(120, 110), 30);
            float willow = WorldExpansionLayout.BoxWeight(x, z, new Vector2(-268, 50), new Vector2(48, 150), 30);
            float grove = Grove(x, z);
            if (h > 34) { density = 3.2f * (1 - Mathf.InverseLerp(34, 50, h)) * (.4f + grove); return Highland; }
            if (dusk > .35f) { density = 5.5f * dusk * (.55f + grove * .6f); return Haunted; }
            if (green > .35f) { density = 6.5f * green * (.55f + grove * .6f); return Temperate; }
            if (willow > .35f) { density = 3.8f * willow * (.4f + grove); return Willow; }
            if (canyon > .35f) { density = .55f * canyon * (.3f + grove); return Canyon; }
            density = 1.1f * grove; return Temperate; // open meadow: groves and clearings
        }

        // ---------------------------------------------------------------- per-tile dressing
        static void Dress(Terrain terrain, List<GameObject> treePrefabs, List<GameObject> detailPrefabs, int seed, out int treeCount, out int detailCells)
        {
            var data = terrain.terrainData; var origin = terrain.transform.position; var size = data.size;
            var rng = new System.Random(seed);

            // trees
            var protos = new TreePrototype[treePrefabs.Count];
            for (int i = 0; i < protos.Length; i++) protos[i] = new TreePrototype { prefab = treePrefabs[i] };
            data.treePrototypes = protos;

            var trees = new List<TreeInstance>();
            const float cell = 4f;
            for (float gx = 0; gx < size.x && trees.Count < 9000; gx += cell)
            for (float gz = 0; gz < size.z; gz += cell)
            {
                float x = origin.x + gx + (float)rng.NextDouble() * cell, z = origin.z + gz + (float)rng.NextDouble() * cell;
                float u = (x - origin.x) / size.x, v = (z - origin.z) / size.z;
                float h = data.GetInterpolatedHeight(u, v) + origin.y;
                float slope = data.GetSteepness(u, v);
                if (h < 1.9f) continue;
                var fam = BiomeFamily(x, z, h, out float density);
                bool steepRockOnly = slope > 30f && slope < 52f;
                if (slope > 52f) continue;
                if (steepRockOnly) { fam = Rocks; density = .9f; }
                if (Blocked(x, z, 9f, 14f)) continue;
                if (rng.NextDouble() > density / 100f * cell * cell) continue;
                int idx = fam[rng.Next(fam.Length)];
                bool isRock = idx >= 12;
                float s = isRock ? Mathf.Lerp(.7f, 1.8f, (float)rng.NextDouble()) : Mathf.Lerp(.8f, 1.3f, (float)rng.NextDouble());
                float tint = Mathf.Lerp(.82f, 1.05f, (float)rng.NextDouble());
                trees.Add(new TreeInstance
                {
                    prototypeIndex = idx, position = new Vector3(u, 0, v), widthScale = s, heightScale = s * Mathf.Lerp(.9f, 1.15f, (float)rng.NextDouble()),
                    rotation = (float)rng.NextDouble() * Mathf.PI * 2, color = new Color(tint, tint, tint, 1), lightmapColor = Color.white
                });
            }
            data.SetTreeInstances(trees.ToArray(), true);
            treeCount = trees.Count;

            // details (grass + flowers)
            var dp = new DetailPrototype[detailPrefabs.Count];
            for (int i = 0; i < dp.Length; i++)
            {
                bool flower = i >= 2;
                dp[i] = new DetailPrototype
                {
                    prototype = detailPrefabs[i], usePrototypeMesh = true, useInstancing = true, renderMode = DetailRenderMode.VertexLit,
                    minWidth = flower ? .8f : .9f, maxWidth = flower ? 1.3f : 1.5f, minHeight = flower ? .8f : .8f, maxHeight = flower ? 1.2f : 1.4f,
                    noiseSpread = flower ? 6f : 2.5f, holeEdgePadding = .2f,
                    healthyColor = flower ? Color.white : new Color(.62f, .86f, .42f), dryColor = flower ? Color.white : new Color(.78f, .74f, .38f)
                };
            }
            data.detailPrototypes = dp;
            data.SetDetailResolution(DetailRes, DetailPatch);
            var layers = new int[dp.Length][,];
            for (int i = 0; i < dp.Length; i++) layers[i] = new int[DetailRes, DetailRes];
            detailCells = 0;
            for (int py = 0; py < DetailRes; py++)
            for (int px = 0; px < DetailRes; px++)
            {
                float u = px / (DetailRes - 1f), v = py / (DetailRes - 1f);
                float x = origin.x + u * size.x, z = origin.z + v * size.z;
                float h = data.GetInterpolatedHeight(u, v) + origin.y;
                if (h < 2.2f || h > 44f || data.GetSteepness(u, v) > 34f) continue;
                if (Blocked(x, z, 4.8f, 7f)) continue;
                float dusk = WorldExpansionLayout.BoxWeight(x, z, new Vector2(-220, 235), new Vector2(75, 85), 20);
                float canyon = WorldExpansionLayout.BoxWeight(x, z, new Vector2(270, 230), new Vector2(120, 110), 20);
                float lush = Mathf.Clamp01(1f - canyon * .95f - dusk * .55f);
                float n = Mathf.PerlinNoise(x * .045f + 3, z * .045f + 9);
                int g = Mathf.RoundToInt(lush * (3f + 5f * n) * Mathf.Clamp01(1.3f - Mathf.InverseLerp(30, 44, h)));
                if (g <= 0) continue;
                layers[0][py, px] = g; detailCells++;
                if (dp.Length > 1 && ((px + py) & 1) == 0) layers[1][py, px] = Mathf.Max(0, g - 2);
                float fl = Mathf.PerlinNoise(x * .06f + 70, z * .06f + 20);
                if (dp.Length > 3 && fl > .72f && lush > .5f)
                {
                    if (((px * 7 + py * 13) % 5) == 0) layers[2][py, px] = 2;
                    if (((px * 11 + py * 3) % 7) == 0) layers[3][py, px] = 2;
                }
            }
            for (int i = 0; i < dp.Length; i++) data.SetDetailLayer(0, 0, i, layers[i]);

            terrain.detailObjectDistance = 85; terrain.detailObjectDensity = 1f;
            terrain.treeDistance = 450; terrain.treeBillboardDistance = 140; terrain.treeCrossFadeLength = 30; terrain.treeMaximumFullLODCount = 500;
            EditorUtility.SetDirty(data); EditorUtility.SetDirty(terrain);
        }
    }
}


