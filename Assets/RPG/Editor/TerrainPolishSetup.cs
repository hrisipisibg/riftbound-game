using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Riftbound.Editor
{
    public static class TerrainPolishSetup
    {
        const string Root = "Assets/RPG/";
        const string Folder = Root + "Terrain/";
        const string ScenePath = Root + "Scenes/Outpost-Updated.unity";
        static readonly HashSet<string> Roads = new HashSet<string> { "Forest road", "Camp approach", "Watchtower trail", "Western crypt trail", "Northern frontier trail", "Greenhollow approach", "Ancient grove trail", "Watchtower courtyard" };
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath); Setup(); Validate();
                typeof(MountExpansionSetup).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                typeof(DemoPassSetup).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                PrototypeBuild.BuildWindows();
                if (!File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")) throw new Exception("Windows build failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("TerrainPolishResult.txt", "FAILED: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        [MenuItem("Riftbound/Add terrain and atmosphere")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) throw new InvalidOperationException("Open Outpost-Updated first.");
            Directory.CreateDirectory(Root + "Scenes/Backups"); Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            string backup = Root + "Scenes/Backups/Outpost-before-terrain.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var visual = enemy.transform.Find("Skeleton visual"); if (visual) visual.localRotation = Quaternion.Euler(0, 180, 0);
            }
            if (!GameObject.Find("09 - Terrain and Atmosphere"))
            {
                var root = new GameObject("09 - Terrain and Atmosphere").transform;
                var terrain = CreateTerrain(root); ProjectScene(terrain);
            }
            ConfigureGraphics(GameObject.Find("09 - Terrain and Atmosphere").transform);
            FinishSurfaces();
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        static Terrain CreateTerrain(Transform parent)
        {
            var layers = new TerrainLayer[3];
            for (int i = 0; i < 3; i++) layers[i] = Layer(i);
            string dataPath = Folder + "Frontier Terrain.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if (!data) { data = new TerrainData(); AssetDatabase.CreateAsset(data, dataPath); }
            data.heightmapResolution = 513; data.alphamapResolution = 256; data.baseMapResolution = 1024; data.size = new Vector3(240, 32, 380);
            var heights = new float[513, 513];
            for (int z = 0; z < 513; z++) for (int x = 0; x < 513; x++) heights[z, x] = TerrainLayout.Height(-120 + x / 512f * 240, -115 + z / 512f * 380) / 32;
            data.SetHeights(0, 0, heights); data.terrainLayers = layers;
            var splat = new float[256, 256, 3];
            for (int z = 0; z < 256; z++) for (int x = 0; x < 256; x++)
            {
                float u = x / 255f, v = z / 255f, wx = -120 + u * 240, wz = -115 + v * 380;
                float rock = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(18, 39, data.GetSteepness(u, v)));
                float dirt = TerrainLayout.Dirt(wx, wz) * (1 - rock);
                splat[z, x, 0] = 1 - rock - dirt; splat[z, x, 1] = dirt; splat[z, x, 2] = rock;
            }
            data.SetAlphamaps(0, 0, splat); EditorUtility.SetDirty(data);
            var go = Terrain.CreateTerrainGameObject(data); go.name = "Riftbound sculpted terrain"; go.transform.SetParent(parent); go.transform.position = new Vector3(-120, -.15f, -115);
            var terrain = go.GetComponent<Terrain>(); terrain.drawInstanced = true; terrain.heightmapPixelError = 4; terrain.basemapDistance = 220; terrain.shadowCastingMode = ShadowCastingMode.On;
            string materialPath = Folder + "Frontier Terrain.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); material.enableInstancing = true; AssetDatabase.CreateAsset(material, materialPath); }
            terrain.materialTemplate = material; terrain.Flush();
            var ground = GameObject.Find("Meadow ground"); ground.GetComponent<Renderer>().enabled = false; ground.GetComponent<Collider>().enabled = false;
            return terrain;
        }
        static TerrainLayer Layer(int type)
        {
            string name = new[] { "Painted meadow", "Earthen trail", "Weathered stone" }[type];
            string path = Folder + name + ".terrainlayer"; var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer) return layer;
            const int size = 512;
            var field = new float[size * size]; var pixels = new Color[size * size]; var normals = new Color[size * size];
            Color dark = type == 0 ? new Color(.22f, .31f, .11f) : type == 1 ? new Color(.29f, .22f, .14f) : new Color(.30f, .33f, .30f);
            Color light = type == 0 ? new Color(.40f, .50f, .21f) : type == 1 ? new Color(.51f, .42f, .28f) : new Color(.57f, .58f, .52f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float broad = Noise(u, v, 4, type * 31 + 7), fine = Noise(u, v, 42, type * 17 + 23);
                float value = broad * .65f + fine * .35f;
                field[y * size + x] = value; pixels[y * size + x] = Color.Lerp(dark, light, Mathf.Clamp01(value * 1.3f - .12f));
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = field[y * size + (x + size - 1) % size] - field[y * size + (x + 1) % size];
                float dy = field[((y + size - 1) % size) * size + x] - field[((y + 1) % size) * size + x];
                Vector3 n = new Vector3(dx * 3, dy * 3, .5f).normalized; normals[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f);
            }
            var diffuse = SaveTexture(name, pixels, size, false); var normal = SaveTexture(name + " Normal", normals, size, true);
            layer = new TerrainLayer { diffuseTexture = diffuse, normalMapTexture = normal, tileSize = Vector2.one * (type == 2 ? 8 : 5), smoothness = .08f, metallic = 0, normalScale = type == 0 ? .4f : .7f };
            AssetDatabase.CreateAsset(layer, path); return layer;
        }
        static float Noise(float u, float v, float frequency, float seed)
        {
            float a = Mathf.Lerp(Mathf.PerlinNoise(u * frequency + seed, v * frequency + seed), Mathf.PerlinNoise((u - 1) * frequency + seed, v * frequency + seed), Mathf.SmoothStep(0, 1, u));
            float b = Mathf.Lerp(Mathf.PerlinNoise(u * frequency + seed, (v - 1) * frequency + seed), Mathf.PerlinNoise((u - 1) * frequency + seed, (v - 1) * frequency + seed), Mathf.SmoothStep(0, 1, u));
            return Mathf.Lerp(a, b, Mathf.SmoothStep(0, 1, v));
        }
        static Texture2D SaveTexture(string name, Color[] pixels, int size, bool normal)
        {
            string path = Folder + name + ".png"; var texture = new Texture2D(size, size, TextureFormat.RGB24, false); texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static float Elevation(Terrain terrain, Vector3 point) => terrain.SampleHeight(point) + terrain.transform.position.y + .15f;
        static void ProjectScene(Terrain terrain)
        {
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "09 - Terrain and Atmosphere" || root.GetComponent<Light>() || root.GetComponent<Camera>()) continue;
                ProjectChildren(root.transform, terrain);
            }
            Physics.SyncTransforms();
        }
        static void ProjectChildren(Transform parent, Terrain terrain)
        {
            foreach (Transform item in parent)
            {
                if (item.name == "Meadow ground" || item.GetComponent<Camera>()) continue;
                if (item.name == "Distant ridge" || item.name == "Greenhollow distant ridge") { item.gameObject.SetActive(false); continue; }
                if (item.name == "World boundary") { item.position = new Vector3(item.position.x, 25, item.position.z); item.localScale = new Vector3(item.localScale.x, 60, item.localScale.z); continue; }
                if (Roads.Contains(item.name)) { ConformRoad(item.gameObject, terrain); continue; }
                if (item.position.sqrMagnitude < .001f && item.childCount > 0 && !item.GetComponent<Renderer>() && !item.GetComponent<Collider>()) { ProjectChildren(item, terrain); continue; }
                item.position += Vector3.up * Elevation(terrain, item.position);
            }
        }
        static void ConformRoad(GameObject go, Terrain terrain)
        {
            float width = go.transform.lossyScale.x, length = go.transform.lossyScale.z; var position = go.transform.position;
            go.transform.position = new Vector3(position.x, 0, position.z); go.transform.localScale = Vector3.one;
            int segments = Mathf.CeilToInt(length), columns = Mathf.CeilToInt(width / 1.5f) + 1;
            var vertices = new Vector3[(segments + 1) * columns]; var uv = new Vector2[vertices.Length]; var triangles = new int[segments * (columns - 1) * 6];
            for (int z = 0; z <= segments; z++) for (int x = 0; x < columns; x++)
            {
                int index = z * columns + x; var local = new Vector3((x / (float)(columns - 1) - .5f) * width, 0, (z / (float)segments - .5f) * length); var world = go.transform.TransformPoint(local);
                local.y = terrain.SampleHeight(world) + terrain.transform.position.y + .025f; vertices[index] = local; uv[index] = new Vector2(x / (float)(columns - 1) * width / 5, z / (float)segments * length / 5);
            }
            int triangle = 0;
            for (int z = 0; z < segments; z++) for (int x = 0; x < columns - 1; x++)
            {
                int a = z * columns + x, b = a + columns; triangles[triangle++] = a; triangles[triangle++] = b; triangles[triangle++] = a + 1; triangles[triangle++] = a + 1; triangles[triangle++] = b; triangles[triangle++] = b + 1;
            }
            var mesh = new Mesh { name = go.name + " terrain ribbon", vertices = vertices, uv = uv, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = Folder + go.name + ".asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; } else AssetDatabase.CreateAsset(mesh, path);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            foreach (var collider in go.GetComponents<Collider>()) collider.enabled = false;
            string materialPath = Folder + "Earthen road.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Earthen trail.png")); material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Earthen trail Normal.png")); material.EnableKeyword("_NORMALMAP"); material.SetFloat("_BumpScale", .55f); material.SetFloat("_Smoothness", .07f); AssetDatabase.CreateAsset(material, materialPath); }
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (go.name == "Watchtower courtyard")
            {
                string stonePath = Folder + "Watchtower paving.mat"; var stone = AssetDatabase.LoadAssetAtPath<Material>(stonePath);
                if (!stone) { stone = new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground Village square.mat")); stone.SetTextureScale("_BaseMap", Vector2.one); stone.SetTextureScale("_BumpMap", Vector2.one); AssetDatabase.CreateAsset(stone, stonePath); }
                go.GetComponent<Renderer>().sharedMaterial = stone;
            }
        }
        static void ConfigureGraphics(Transform root)
        {
            const string pipelinePath = Folder + "Frontier RP.asset", rendererPath = Folder + "Frontier Renderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (!renderer)
            {
                renderer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset")); renderer.name = "Frontier Renderer"; AssetDatabase.CreateAsset(renderer, rendererPath);
                var feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>(); feature.name = "Frontier contact shading"; AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
                var settings = new SerializedObject(feature); var options = settings.FindProperty("m_Settings");
                options.FindPropertyRelative("Downsample").boolValue = true; options.FindPropertyRelative("Intensity").floatValue = .75f; options.FindPropertyRelative("Radius").floatValue = .3f; options.FindPropertyRelative("DirectLightingStrength").floatValue = .15f;
                settings.ApplyModifiedPropertiesWithoutUndo(); feature.SetActive(true); EditorUtility.SetDirty(renderer);
            }
            renderer.rendererFeatures.RemoveAll(feature => feature is ScreenSpaceAmbientOcclusion && AssetDatabase.GetAssetPath(feature) != rendererPath);
            var rendererSettings = new SerializedObject(renderer); var legacyProbes = rendererSettings.FindProperty("probeVolumeResources");
            if (legacyProbes != null)
            {
                foreach (string name in new[] { "probeVolumeDebugShader", "probeVolumeFragmentationDebugShader", "probeVolumeOffsetDebugShader", "probeVolumeSamplingDebugShader", "probeSamplingDebugMesh", "probeSamplingDebugTexture", "probeVolumeBlendStatesCS" })
                { var property = legacyProbes.FindPropertyRelative(name); if (property != null) property.objectReferenceValue = null; }
                rendererSettings.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(renderer);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (!pipeline) { pipeline = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset")); pipeline.name = "Frontier RP"; AssetDatabase.CreateAsset(pipeline, pipelinePath); }
            var serialized = new SerializedObject(pipeline); serialized.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = renderer; serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.msaaSampleCount = 4; pipeline.supportsHDR = true; pipeline.shadowDistance = 100; pipeline.mainLightShadowmapResolution = 2048; EditorUtility.SetDirty(pipeline);
            GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline; QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Folder + "Frontier grading.asset");
            if (!profile)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, Folder + "Frontier grading.asset");
                var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
                var color = profile.Add<ColorAdjustments>(true); color.postExposure.Override(.15f); color.contrast.Override(9); color.saturation.Override(2);
                var white = profile.Add<WhiteBalance>(true); white.temperature.Override(5);
                var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(.1f); bloom.threshold.Override(1.15f); bloom.scatter.Override(.45f);
                var vignette = profile.Add<Vignette>(true); vignette.intensity.Override(.1f); vignette.smoothness.Override(.35f);
                foreach (var component in profile.components) AssetDatabase.AddObjectToAsset(component, profile); EditorUtility.SetDirty(profile);
            }
            var volume = root.GetComponent<Volume>(); if (!volume) volume = root.gameObject.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
            foreach (var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) { camera.allowHDR = true; camera.allowMSAA = true; camera.GetUniversalAdditionalCameraData().renderPostProcessing = true; EditorUtility.SetDirty(camera); }
            RenderSettings.ambientMode = AmbientMode.Trilight; RenderSettings.ambientSkyColor = new Color(.55f, .65f, .73f); RenderSettings.ambientEquatorColor = new Color(.43f, .49f, .42f); RenderSettings.ambientGroundColor = new Color(.22f, .26f, .20f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 65; RenderSettings.fogEndDistance = 265; RenderSettings.fogColor = new Color(.60f, .69f, .72f);
            if (RenderSettings.sun) { RenderSettings.sun.intensity = 1.25f; RenderSettings.sun.color = new Color(1, .92f, .80f); RenderSettings.sun.transform.rotation = Quaternion.Euler(42, -32, 0); RenderSettings.sun.shadowBias = .04f; RenderSettings.sun.shadowNormalBias = .35f; RenderSettings.sun.shadows = LightShadows.Soft; EditorUtility.SetDirty(RenderSettings.sun); }
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Frontier sky.mat");
            if (!sky) { sky = new Material(RenderSettings.skybox); AssetDatabase.CreateAsset(sky, Folder + "Frontier sky.mat"); }
            sky.SetColor("_SkyTint", new Color(.5f, .5f, .5f)); sky.SetColor("_GroundColor", RenderSettings.fogColor); sky.SetFloat("_AtmosphereThickness", .9f); sky.SetFloat("_Exposure", 1);
            EditorUtility.SetDirty(sky); RenderSettings.skybox = sky;
        }
        static void FinishSurfaces()
        {
            foreach (string name in new[] { "Village square", "West lane", "East lane" })
            {
                var go = GameObject.Find(name); if (!go) continue;
                var pavingMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground " + name + ".mat");
                if (pavingMaterial) go.GetComponent<Renderer>().sharedMaterial = pavingMaterial;
            }
            var water = GameObject.Find("Shallow Willow Brook"); if (!water) return;
            var renderer = water.GetComponent<Renderer>(); var original = renderer.sharedMaterial;
            string materialPath = Folder + "Flowing brook.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material) { material = new Material(original); AssetDatabase.CreateAsset(material, materialPath); }
            material.SetColor("_BaseColor", new Color(.15f, .43f, .56f)); material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "Weathered stone Normal.png")); material.SetFloat("_BumpScale", .35f); material.SetFloat("_Smoothness", .85f); material.SetFloat("_Metallic", .12f); material.EnableKeyword("_NORMALMAP"); EditorUtility.SetDirty(material);
            renderer.sharedMaterial = material;
            if (!water.GetComponent<FlowingWater>()) water.AddComponent<FlowingWater>();
            if (water.GetComponent<MeshFilter>().sharedMesh.name == "Curved Willow Brook") return;
            water.transform.localScale = Vector3.one;
            const int segments = 96; var vertices = new Vector3[(segments + 1) * 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++)
            {
                float x = Mathf.Lerp(-85, 85, i / (float)segments), bend = Mathf.Sin(x * .075f) * .9f + Mathf.Sin(x * .21f) * .3f;
                vertices[i * 2] = new Vector3(x, 0, -6 + bend); vertices[i * 2 + 1] = new Vector3(x, 0, 6 + bend);
                uv[i * 2] = new Vector2(i / (float)segments * 14, 0); uv[i * 2 + 1] = new Vector2(i / (float)segments * 14, 1);
                if (i == segments) continue;
                int t = i * 6, a = i * 2; triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = a + 2; triangles[t + 3] = a + 1; triangles[t + 4] = a + 3; triangles[t + 5] = a + 2;
            }
            var mesh = new Mesh { name = "Curved Willow Brook", vertices = vertices, uv = uv, triangles = triangles }; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path = Folder + "Willow brook surface.asset"; var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); mesh = existing; } else AssetDatabase.CreateAsset(mesh, path);
            water.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        static void Validate()
        {
            var report = new StringBuilder();
            void Check(bool valid, string message)
            {
                report.AppendLine((valid ? "PASS " : "FAIL ") + message); File.WriteAllText("TerrainPolishResult.txt", report.ToString());
                if (!valid) throw new InvalidOperationException(message);
            }
            var terrain = Terrain.activeTerrain;
            Check(terrain && terrain.GetComponent<TerrainCollider>().enabled && terrain.terrainData.heightmapResolution == 513, "Native terrain and matching height collider exist");
            Check(!GameObject.Find("Meadow ground").GetComponent<Renderer>().enabled && !GameObject.Find("Meadow ground").GetComponent<Collider>().enabled, "Old flat ground no longer renders or collides");
            var data = terrain.terrainData; var heights = data.GetHeights(0, 0, 513, 513); float low = 99, high = -99;
            for (int z = 0; z < 513; z += 8) for (int x = 0; x < 513; x += 8) { low = Mathf.Min(low, heights[z, x] * 32); high = Mathf.Max(high, heights[z, x] * 32); }
            Check(high - low > 18, "Terrain includes real hills and ridges above 18 metres");
            foreach (var point in new[] { new Vector3(0, 0, -6), new Vector3(-34, 3.5f, 68), new Vector3(35, 6, 90), new Vector3(-25, 5.5f, 157), new Vector3(22, 10, 201) })
                Check(Mathf.Abs(Elevation(terrain, point) - point.y) < .12f, $"Landmark platform at ({point.x},{point.z}) retains intended {point.y}m elevation");
            Check(data.terrainLayers.Length == 3 && data.terrainLayers.All(l => l.diffuseTexture && l.normalMapTexture), "Grass, earth and rock layers include normal maps");
            var splat = data.GetAlphamaps(0, 0, 256, 256); bool normalized = true; int grass = 0, dirt = 0, rock = 0;
            for (int z = 0; z < 256; z += 4) for (int x = 0; x < 256; x += 4)
            {
                float sum = splat[z, x, 0] + splat[z, x, 1] + splat[z, x, 2]; normalized &= Mathf.Abs(sum - 1) < .02f;
                if (splat[z, x, 0] > .7f) grass++; if (splat[z, x, 1] > .7f) dirt++; if (splat[z, x, 2] > .7f) rock++;
            }
            Check(normalized && grass > 50 && dirt > 10 && rock > 10, "Terrain weights are normalized and all three surfaces appear");
            foreach (string name in Roads)
            {
                var go = GameObject.Find(name); var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                Check(mesh.vertexCount > 24 && go.GetComponents<Collider>().All(c => !c.enabled), name + " follows terrain without a flat collision slab");
                float slope = MaximumRoadSlope(go, terrain);
                Check(slope < 34, name + $" stays traversable: maximum grade {slope:F1} degrees");
            }
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(enemies.Where(e => !e.trainingDummy).All(e => Mathf.Abs(Mathf.DeltaAngle(e.transform.Find("Skeleton visual").localEulerAngles.y, 180)) < .1f), "All Skeleton visuals face the controller's +Z aiming direction");
            Check(enemies.All(e => { float clearance = e.transform.position.y - Elevation(terrain, e.transform.position) + .15f; return clearance > -.03f && clearance < .45f; }), "Enemy and dummy roots are grounded at the new elevations");
            var actor = new GameObject("Facing checks").AddComponent<EnemyController>();
            var target = new GameObject("Facing check target").AddComponent<PlayerController>(); actor.player = target; actor.transform.position = new Vector3(1000, 0, 1000);
            try
            {
                var face = typeof(EnemyController).GetMethod("FacePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    target.transform.position = actor.transform.position + direction * 2 + Vector3.up * 3; face.Invoke(actor, null);
                    Check(Vector3.Dot(actor.transform.forward, direction) > .999f && Mathf.Abs(actor.transform.forward.y) < .001f, "Attack tracking faces moving target horizontally: " + direction);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(actor.gameObject); UnityEngine.Object.DestroyImmediate(target.gameObject); }
            var pipeline = QualitySettings.renderPipeline as UniversalRenderPipelineAsset;
            Check(pipeline && pipeline.msaaSampleCount == 4 && pipeline.mainLightShadowmapResolution >= 2048, "PC rendering uses 4x MSAA and 2048px sun shadows");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Folder + "Frontier Renderer.asset");
            Check(renderer.rendererFeatures.Count(f => f is ScreenSpaceAmbientOcclusion && f.isActive) == 1, "One contact ambient-occlusion pass enabled without inherited duplicates");
            var volume = GameObject.Find("09 - Terrain and Atmosphere").GetComponent<Volume>();
            Check(volume && volume.isGlobal && volume.sharedProfile.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.ACES, "Global ACES grading profile configured");
            Check(RenderSettings.fog && RenderSettings.sun && RenderSettings.sun.shadows == LightShadows.Soft, "Atmospheric fog and soft sunlight configured");
            report.AppendLine("Edit-mode geometry, facing, terrain and render-data checks. Gameplay testing remains with the user."); File.WriteAllText("TerrainPolishResult.txt", report.ToString());
        }
        static float MaximumRoadSlope(GameObject go, Terrain terrain)
        {
            var bounds = go.GetComponent<MeshFilter>().sharedMesh.bounds;
            var from = go.transform.TransformPoint(new Vector3(0, 0, bounds.min.z)); var to = go.transform.TransformPoint(new Vector3(0, 0, bounds.max.z));
            float length = Vector3.Distance(from, to), previous = Elevation(terrain, from), maximum = 0; int steps = Mathf.CeilToInt(length * 2);
            for (int i = 1; i <= steps; i++) { float height = Elevation(terrain, Vector3.Lerp(from, to, i / (float)steps)); maximum = Mathf.Max(maximum, Mathf.Atan2(Mathf.Abs(height - previous), length / steps) * Mathf.Rad2Deg); previous = height; }
            return maximum;
        }
        public static void BatchPreview()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath);
                var terrain = Terrain.activeTerrain; var report = new StringBuilder();
                foreach (string name in Roads)
                {
                    var go = GameObject.Find(name); var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                    Vector3 a = go.transform.TransformPoint(new Vector3(0, 0, mesh.bounds.min.z)), b = go.transform.TransformPoint(new Vector3(0, 0, mesh.bounds.max.z));
                    float length = Vector3.Distance(a, b), grade = 0; Vector3 worst = a;
                    int steps = Mathf.CeilToInt(length * 2); float previous = Elevation(terrain, a);
                    for (int i = 1; i <= steps; i++)
                    {
                        Vector3 point = Vector3.Lerp(a, b, i / (float)steps); float height = Elevation(terrain, point);
                        float slope = Mathf.Atan2(Mathf.Abs(height - previous), length / steps) * Mathf.Rad2Deg;
                        if (slope > grade) { grade = slope; worst = point; } previous = height;
                    }
                    report.AppendLine($"{name}: max longitudinal slope {grade:F1}deg at {worst}; elevation {Elevation(terrain, a):F1} to {Elevation(terrain, b):F1}m");
                }
                foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (enemy.animator) { enemy.animator.Rebind(); enemy.animator.Play("Idle", 0, 0); enemy.animator.Update(0); }
                    report.AppendLine($"{enemy.enemyId}: root clearance {enemy.transform.position.y - (Elevation(terrain, enemy.transform.position) - .15f):F3}m");
                }
                DemoPassSetup.RenderWorld(new Vector3(24, 17, -24), new Vector3(0, 1, 12), "Terrain-Emberwatch.png");
                DemoPassSetup.RenderWorld(new Vector3(-19, 14, 30), new Vector3(2, 3, 66), "Terrain-Forest-Road.png");
                DemoPassSetup.RenderWorld(new Vector3(35, 25, 134), new Vector3(-22, 7, 159), "Terrain-Greenhollow.png");
                DemoPassSetup.RenderWorld(new Vector3(92, 86, 18), new Vector3(0, 6, 105), "Terrain-World-Overview.png");
                foreach (string id in new[] { "camp-guard-1", "camp-guard-2", "road-raider-2" })
                {
                    var enemy = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(e => e.enemyId == id);
                    var copy = UnityEngine.Object.Instantiate(enemy.gameObject); copy.transform.SetPositionAndRotation(new Vector3(0, -400, 0), Quaternion.identity);
                    var animator = copy.GetComponent<EnemyController>().animator; animator.Rebind(); animator.Play("Attack", 0, .4f); animator.Update(0);
                    CharacterArtSetup.RenderPose(copy, "Enemy-Fixed-" + id + ".png"); UnityEngine.Object.DestroyImmediate(copy);
                }
                File.WriteAllText("TerrainSurvey.txt", report.ToString()); EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("TerrainSurvey.txt", "FAILED: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BatchEnemyInspect()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity");
                var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var report = new StringBuilder();
                foreach (string id in new[] { "road-raider-1", "road-raider-2", "camp-guard-1", "camp-guard-2" })
                {
                    var enemy = enemies.First(e => e.enemyId == id); var copy = UnityEngine.Object.Instantiate(enemy.gameObject);
                    copy.transform.SetPositionAndRotation(new Vector3(0, -400, 0), Quaternion.identity);
                    var actor = copy.GetComponent<EnemyController>(); actor.animator.Rebind();
                    report.AppendLine(id + " visual rotation " + actor.transform.Find("Skeleton visual").localEulerAngles);
                    foreach (string state in new[] { "Idle", "Attack" })
                    {
                        actor.animator.Play(state, 0, .4f); actor.animator.Update(0);
                        CharacterArtSetup.RenderPose(copy, "Enemy-Before-" + id + "-" + state + ".png");
                        foreach (var bone in actor.animator.GetComponentsInChildren<Transform>().Where(t => t.name == "head" || t.name == "handslot.r" || t.name == "root")) report.AppendLine(state + " " + bone.name + " local=" + bone.localEulerAngles + " forward=" + bone.forward);
                    }
                    UnityEngine.Object.DestroyImmediate(copy);
                }
                File.WriteAllText("EnemyFacingInspection.txt", report.ToString()); EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("EnemyFacingInspection.txt", "FAILED: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
