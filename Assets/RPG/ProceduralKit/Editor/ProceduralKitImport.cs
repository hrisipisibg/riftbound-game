using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    internal static class ProceduralKitImport
    {
        const string Root = "Assets/RPG/ProceduralKit";
        const string ScenePath = Root + "/Emberford.unity";
        const string Request = "Library/RiftboundProceduralKit.request";
        const string Report = "Library/RiftboundProceduralKit.result";

        [Serializable] sealed class Manifest
        {
            public Asset[] assets;
            public Placement[] instances;
            public Swatch[] palette;
        }

        [Serializable] sealed class Asset { public string name, file, kind; public int[] triangles; }
        [Serializable] sealed class Placement { public string asset; public float[] position; public float yaw, scale; }
        [Serializable] sealed class Swatch { public string name, color, surface; }

        static readonly HashSet<string> Architecture = new HashSet<string>
        {
            "Timber Cottage", "Wayfarer Inn", "Emberford Smithy", "Bell Chapel", "Briar Camp Hut",
            "Briar Watchtower", "Highspire Masonry Tower", "Highspire Citadel Hall", "Briar War Hall", "Palisade", "Ashwater Bridge", "Stone Wall", "Ruined Wall",
            "Stone Arch", "Broken Arch", "Collapsed Shed", "Timber Door", "Broken Door", "Paving Tile", "Moss Rock"
        };

        [InitializeOnLoadMethod]
        static void QueueRequestedBuild()
        {
            if (!File.Exists(Request)) return;
            EditorApplication.delayCall += RunRequestedBuild;
        }

        static void RunRequestedBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += RunRequestedBuild;
                return;
            }
            File.Delete(Request);
            try { Build(); }
            catch (Exception ex) { File.WriteAllText(Report, "FAIL " + ex); Debug.LogException(ex); }
        }

        [MenuItem("Riftbound/Procedural Kit/Build Emberford Showcase")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode to build Emberford.");
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/KitManifest.json"));
            foreach (var folder in new[] { "Prefabs", "Materials", "Animations" })
                if (!AssetDatabase.IsValidFolder(Root + "/" + folder)) AssetDatabase.CreateFolder(Root, folder);
            var materials = CreateMaterials(manifest.palette);
            bool additive = !Application.isBatchMode && !string.IsNullOrEmpty(SceneManager.GetActiveScene().path);
            if (!additive && !Application.isBatchMode && SceneManager.GetActiveScene().isDirty)
                throw new InvalidOperationException("Save the current scene before building Emberford.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, additive ? NewSceneMode.Additive : NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            try
            {
                var prefabs = new Dictionary<string, GameObject>();
                int actors = 0, staticModels = 0, clips = 0;
                foreach (Asset asset in manifest.assets)
                {
                    var prefab = BuildPrefab(asset, materials, out int clipCount);
                    prefabs.Add(asset.name, prefab);
                    clips += clipCount;
                    if (IsActor(asset)) actors++;
                    else staticModels++;
                }

                var terrainModel = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/FBX/Emberford_Terrain.fbx");
                if (!terrainModel) throw new InvalidOperationException("Missing environment terrain FBX.");
                var terrain = (GameObject)PrefabUtility.InstantiatePrefab(terrainModel, scene);
                terrain.name = "Emberford terrain, river, and paving";
                ApplyMaterials(terrain, materials);
                int groundColliders = 0;
                foreach (var filter in terrain.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.name.Contains("River")) continue;
                    filter.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    groundColliders++;
                }
                if (groundColliders != 2) throw new InvalidOperationException("Expected separate ground and paving colliders.");

                var dressed = new GameObject("Emberford district, ruins, and Briar encampment");
                foreach (Placement item in manifest.instances)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[item.asset], scene);
                    instance.transform.SetParent(dressed.transform, false);
                    instance.transform.SetLocalPositionAndRotation(
                        new Vector3(item.position[0], item.position[1], item.position[2]), Quaternion.Euler(0, item.yaw, 0));
                    instance.transform.localScale = Vector3.one * item.scale;
                }
                var hero = new GameObject("Showcase camera").AddComponent<Camera>();
                hero.tag = "MainCamera";
                hero.transform.position = new Vector3(31, 39, 53);
                hero.transform.LookAt(new Vector3(-1, 0, -4));
                hero.orthographic = true;
                hero.orthographicSize = 29;
                hero.farClipPlane = 300;
                hero.clearFlags = CameraClearFlags.SolidColor;
                hero.backgroundColor = new Color(.25f, .34f, .43f);
                Light sun = new GameObject("Warm sky key").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = new Color(1f, .90f, .79f);
                sun.intensity = 1.65f;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(49, -29, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.55f, .62f, .70f);
                if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new InvalidOperationException("Could not save Emberford.");
                AssetDatabase.SaveAssets();
                string report = $"PASS {prefabs.Count} Unity prefabs, {staticModels} static and {actors} rigged.\n" +
                    $"PASS Three validated renderer LODs per prefab and {clips} imported animation clips.\n" +
                    $"PASS {manifest.instances.Length} prefab instances, collidable terrain, and a separate Emberford scene.\n";
                File.WriteAllText(Report, report);
                Debug.Log(report);
            }
            finally
            {
                if (additive && scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        internal static void ImportArchitectureUpgrades()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/KitManifest.json"));
            var materials = manifest.palette.ToDictionary(swatch => swatch.name, swatch =>
                AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/" + swatch.name.Replace(' ', '_') + ".mat"));
            foreach (Asset asset in manifest.assets.Where(a => a.name == "Briar Watchtower" ||
                a.name == "Highspire Masonry Tower" || a.name == "Highspire Citadel Hall" || a.name == "Briar War Hall"))
                BuildPrefab(asset, materials, out _);
            AssetDatabase.SaveAssets();
        }

        static bool IsActor(Asset asset) => asset.kind != "static" && asset.kind != "weapon";

        static GameObject BuildPrefab(Asset asset, Dictionary<string, Material> materials, out int clips)
        {
            string sourcePath = Root + "/FBX/" + asset.file;
            var importer = AssetImporter.GetAtPath(sourcePath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing " + sourcePath);
            bool actor = IsActor(asset);
            var animationType = actor ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
            var avatarMode = actor ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.NoAvatar;
            if (importer.animationType != animationType || importer.importAnimation != actor || importer.avatarSetup != avatarMode)
            {
                importer.animationType = animationType;
                importer.importAnimation = actor;
                importer.avatarSetup = avatarMode;
                importer.SaveAndReimport();
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            var prefabRoot = new GameObject(asset.name);
            try
            {
                var display = (GameObject)PrefabUtility.InstantiatePrefab(model);
                display.transform.SetParent(prefabRoot.transform, false);
                ApplyMaterials(display, materials);
                var lods = new LOD[3];
                var renderers = display.GetComponentsInChildren<Renderer>();
                float[] thresholds = { .24f, .102f, .017f };
                for (int level = 0; level < 3; level++)
                {
                    Renderer[] renderLevel = renderers.Where(r => r.name.EndsWith("_LOD" + level, StringComparison.Ordinal)).ToArray();
                    if (renderLevel.Length == 0) throw new InvalidOperationException($"{asset.name} has no LOD{level} renderer.");
                    lods[level] = new LOD(thresholds[level], renderLevel);
                }
                foreach (var importedGroup in display.GetComponentsInChildren<LODGroup>(true))
                    UnityEngine.Object.DestroyImmediate(importedGroup);
                var lodGroup = prefabRoot.AddComponent<LODGroup>();
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();
                clips = 0;
                if (actor)
                {
                    if (display.GetComponentsInChildren<SkinnedMeshRenderer>().Length != 3)
                        throw new InvalidOperationException(asset.name + " is missing skinned LODs.");
                    var available = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>()
                        .Where(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal)).ToArray();
                    var idle = available.FirstOrDefault(c => c.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0);
                    var walk = available.FirstOrDefault(c => c.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (!idle || !walk) throw new InvalidOperationException(asset.name + " is missing Idle or Walk.");
                    clips = available.Length;
                    var animator = display.GetComponent<Animator>() ?? display.AddComponent<Animator>();
                    var avatar = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<Avatar>().FirstOrDefault(a => a.isValid);
                    if (!avatar) throw new InvalidOperationException(asset.name + " has no valid Generic avatar.");
                    animator.avatar = avatar;
                    animator.runtimeAnimatorController = MakeController(asset.file, idle, walk);
                    animator.applyRootMotion = false;
                    if (asset.kind == "npc") AddTownNpc(prefabRoot, asset.name);
                }
                if (Architecture.Contains(asset.name))
                {
                    // The low-poly collision stays stable while the visible mesh changes LOD.
                    foreach (Renderer modelRenderer in lods[2].renderers)
                    {
                        var filter = modelRenderer.GetComponent<MeshFilter>();
                        if (filter) modelRenderer.gameObject.AddComponent<MeshCollider>().sharedMesh = filter.sharedMesh;
                    }
                }
                string dest = Root + "/Prefabs/" + Path.GetFileNameWithoutExtension(asset.file) + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(prefabRoot, dest);
            }
            finally { UnityEngine.Object.DestroyImmediate(prefabRoot); }
        }

        static AnimatorController MakeController(string file, AnimationClip idle, AnimationClip walk)
        {
            string path = Root + "/Animations/" + Path.GetFileNameWithoutExtension(file) + ".controller";
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (ctrl) return ctrl;
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
            ctrl.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            var machine = ctrl.layers[0].stateMachine;
            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            var walkState = machine.AddState("Walk");
            walkState.motion = walk;
            machine.defaultState = idleState;
            var enter = idleState.AddTransition(walkState);
            enter.hasExitTime = false;
            enter.duration = .12f;
            enter.AddCondition(AnimatorConditionMode.If, 0, "Moving");
            var exit = walkState.AddTransition(idleState);
            exit.hasExitTime = false;
            exit.duration = .12f;
            exit.AddCondition(AnimatorConditionMode.IfNot, 0, "Moving");
            return ctrl;
        }

        static void AddTownNpc(GameObject obj, string model)
        {
            // Keeping this optional lets the asset kit import into clean Unity projects too.
            var type = Type.GetType("Riftbound.TownNPC, Assembly-CSharp");
            if (type == null) return;
            var npc = obj.AddComponent(type);
            type.GetField("npcName").SetValue(npc, model);
            type.GetField("roleTag").SetValue(npc, model.Replace("Town ", ""));
            type.GetField("greeting").SetValue(npc, model switch
            {
                "Town Blacksmith" => "The Briar Watch crossed the river at dusk. Bring your gear by before you head north.",
                "Town Merchant" => "Travellers have been avoiding the old stone arches. Take the bridge road after dark.",
                _ => "The low trail reaches the ruins. The lantern road takes you straight to the watchtower."
            });
        }

        static Dictionary<string, Material> CreateMaterials(Swatch[] palette)
        {
            var result = new Dictionary<string, Material>();
            bool srp = GraphicsSettings.currentRenderPipeline != null;
            Shader shader = Shader.Find(srp ? "Universal Render Pipeline/Lit" : "Standard");
            if (!shader) throw new InvalidOperationException("A Unity Lit shader is required.");
            foreach (Swatch swatch in palette)
            {
                string path = Root + "/Materials/" + swatch.name.Replace(' ', '_') + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!material)
                {
                    material = new Material(shader) { name = swatch.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                material.shader = shader;
                if (!ColorUtility.TryParseHtmlString("#" + swatch.color, out Color color))
                    throw new InvalidOperationException("Invalid palette color " + swatch.color);
                material.SetColor(srp ? "_BaseColor" : "_Color", color);
                var texture = string.IsNullOrEmpty(swatch.surface) ? null :
                    AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/" + swatch.surface + "_albedo.png");
                material.SetTexture(srp ? "_BaseMap" : "_MainTex", texture);
                material.SetFloat("_Metallic", swatch.surface == "metal" ? .54f : 0);
                material.SetFloat(srp ? "_Smoothness" : "_Glossiness", swatch.name == "River" ? .66f : .26f);
                if (swatch.surface == "glow")
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * .55f);
                }
                EditorUtility.SetDirty(material);
                result.Add(Key(swatch.name), material);
            }
            return result;
        }

        static string Key(string name) => string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();

        static void ApplyMaterials(GameObject obj, Dictionary<string, Material> materials)
        {
            foreach (var renderer in obj.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
                    m && materials.TryGetValue(Key(m.name), out var replacement) ? replacement : m).ToArray();
        }
    }
}
