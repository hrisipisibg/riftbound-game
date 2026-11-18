using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class MountExpansionSetup
    {
        const string Root = "Assets/RPG/";
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity");
                Setup(); Validate();
                typeof(DemoPassSetup).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
                PrototypeBuild.BuildWindows();
                if (!File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")) throw new InvalidOperationException("Windows build failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("MountExpansionResult.txt", "FAILED: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        [MenuItem("Riftbound/Add mounts and Greenhollow Valley")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player) throw new InvalidOperationException("Open Outpost-Updated first.");
            var scene = EditorSceneManager.GetActiveScene();
            Directory.CreateDirectory(Root + "Scenes/Backups");
            string backup = Root + "Scenes/Backups/Outpost-before-mounts.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            PrepareAnimals(); PrepareProps();
            Directory.CreateDirectory(Root + "Prefabs"); Directory.CreateDirectory(Root + "Data/Mounts");
            AssetDatabase.Refresh();
            var mounts = new List<MountDefinition>();
            foreach (var item in new[] { ("Horse", "horse", "Emberwatch Horse", false, false), ("Zebra", "zebra", "Frontier Zebra", true, false), ("Horse", "gryphon", "Celestial Winged Steed (Flying)", false, true) })
            {
                string path = Root + "Prefabs/Mount " + item.Item2 + ".prefab";
                var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var prefab = prefabAsset ? prefabAsset.GetComponent<MountVisual>() : null;
                if (!prefab || prefab.artVersion < 2)
                {
                    var mount = Animal(item.Item1, 2.4f, Vector3.zero, null);
                    var visual = mount.AddComponent<MountVisual>(); visual.animator = mount.GetComponentInChildren<Animator>();
                    var body = mount.GetComponentsInChildren<Transform>().First(t => t.name == "Body");
                    var saddle = new GameObject("Rider saddle").transform; saddle.SetParent(body, false);
                    saddle.position = new Vector3(0, 1.8f, -.3f); visual.saddle = saddle; visual.artVersion = 2;
                    var pad = Cube("Leather saddle", saddle.position - Vector3.up * .12f, new Vector3(.75f, .12f, .65f), LoadMaterial("Dark timber"), saddle);
                    UnityEngine.Object.DestroyImmediate(pad.GetComponent<Collider>()); SetLayer(mount.transform, 2);
                    MeasureStride(visual);
                    prefab = PrefabUtility.SaveAsPrefabAsset(mount, path).GetComponent<MountVisual>(); UnityEngine.Object.DestroyImmediate(mount);
                }
                string dataPath = Root + "Data/Mounts/" + item.Item2 + ".asset";
                var definition = AssetDatabase.LoadAssetAtPath<MountDefinition>(dataPath);
                if (!definition) { definition = ScriptableObject.CreateInstance<MountDefinition>(); AssetDatabase.CreateAsset(definition, dataPath); }
                definition.mountId = item.Item2; definition.displayName = item.Item3; definition.requiresExploration = item.Item4;
                definition.prefab = prefab; definition.runSpeed = item.Item4 ? 9 : 8; definition.walkSpeed = 3;
                definition.isFlying = item.Item5; definition.flySpeed = item.Item5 ? 16f : 8f;
                EditorUtility.SetDirty(definition); mounts.Add(definition);
            }
            player.mounts = mounts.ToArray(); EditorUtility.SetDirty(player);
            var controller = (AnimatorController)player.animator.runtimeAnimatorController;
            string ridePath = Root + "Animations/Player Ride.anim";
            var ride = AssetDatabase.LoadAssetAtPath<AnimationClip>(ridePath);
            if (!ride || Environment.GetCommandLineArgs().Contains("-rebakeRider"))
            {
                var source = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/UniversalAnimations/UAL1_Standard.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__") && c.name.EndsWith("|Sitting_Idle_Loop"));
                ride = PolishSetup.Bake(source, ridePath, true, true);
            }
            var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Ride").state;
            if (!state) state = controller.layers[0].stateMachine.AddState("Ride"); state.motion = ride; state.iKOnFeet = false; state.speed = 1; EditorUtility.SetDirty(controller);
            if (!GameObject.Find("09 - Terrain and Atmosphere"))
            {
                ExpandBounds();
                if (!GameObject.Find("08 - Greenhollow Frontier")) CreateValley(player);
                PolishValley();
            }
            var camera = UnityEngine.Object.FindFirstObjectByType<OrbitCamera>(); if (camera) { camera.GetComponent<Camera>().farClipPlane = 350; EditorUtility.SetDirty(camera.GetComponent<Camera>()); }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        static void PrepareAnimals()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "Art/Animals" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid); var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                importer.animationType = ModelImporterAnimationType.Generic; importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; importer.bakeAxisConversion = true;
                PrototypeImport.ConfigurePackClips(importer);
                var clips = importer.clipAnimations;
                foreach (var clip in clips) { string name = clip.name.ToLowerInvariant(); clip.loopTime = name.Contains("idle") || name.Contains("walk") || name.Contains("run"); clip.loopPose = clip.loopTime; }
                importer.clipAnimations = clips; importer.SaveAndReimport();
            }
        }
        static void PrepareProps()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "Art/FantasyProps" }))
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var renderer in model.GetComponentsInChildren<Renderer>()) foreach (var original in renderer.sharedMaterials)
                {
                    if (!original) continue;
                    string path = Root + "Materials/FantasyProps_" + original.name + ".mat";
                    if (AssetDatabase.LoadAssetAtPath<Material>(path)) continue;
                    var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    string stem = original.name.Replace("MI_", "T_") + "_BaseColor";
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/FantasyProps/Textures/" + stem + ".png");
                    material.SetTexture("_BaseMap", texture); material.SetColor("_BaseColor", texture ? Color.white : original.color); material.SetFloat("_Smoothness", .15f);
                    AssetDatabase.CreateAsset(material, path);
                }
            }
        }
        static GameObject Animal(string type, float height, Vector3 position, Transform parent)
        {
            var root = new GameObject(type); root.transform.SetParent(parent);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Animals/" + type + ".fbx"));
            model.transform.SetParent(root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var animator = model.GetComponentInChildren<Animator>(); animator.runtimeAnimatorController = AnimalController(type); animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SkinnedMeshRenderer skin) skin.updateWhenOffscreen = true;
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                {
                    string path = Root + "Materials/Animal " + type + " " + original.name + ".mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", original.color); material.SetFloat("_Smoothness", .08f); AssetDatabase.CreateAsset(material, path); }
                    return material;
                }).ToArray();
            }
            animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0);
            Bounds bounds = BoundsOf(root); model.transform.localScale *= height / Mathf.Max(.01f, bounds.size.y);
            bounds = BoundsOf(root); model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            root.transform.position = position; SetLayer(root.transform, 2); return root;
        }
        static AnimatorController AnimalController(string type)
        {
            string path = Root + "Animations/Animal " + type + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/Animals/" + type + ".fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            foreach (string name in new[] { "Idle", "Walk", "Run", "Jump", "Death" })
            {
                var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == name).state;
                if (!state) state = controller.layers[0].stateMachine.AddState(name);
                state.motion = clips.FirstOrDefault(c => c.name.EndsWith("|" + name)) ?? clips.First(c => c.name.EndsWith("|Idle"));
                state.speed = 1; if (name == "Idle") controller.layers[0].stateMachine.defaultState = state;
            }
            EditorUtility.SetDirty(controller); return controller;
        }
        static void MeasureStride(MountVisual mount)
        {
            var foot = mount.GetComponentsInChildren<Transform>().First(t => t.name == "FrontFoot.L");
            foreach (string state in new[] { "Walk", "Run" })
            {
                mount.animator.Play(state, 0, 0); mount.animator.Update(0); float duration = mount.animator.GetCurrentAnimatorStateInfo(0).length;
                var points = new Vector3[121]; float low = 999, high = -999;
                for (int i = 0; i <= 120; i++) { mount.animator.Play(state, 0, i / 120f); mount.animator.Update(0); points[i] = foot.position; low = Mathf.Min(low, points[i].y); high = Mathf.Max(high, points[i].y); }
                var speeds = new List<float>();
                for (int i = 1; i < 120; i++) if (points[i].y < low + (high - low) * .3f + .005f)
                { float speed = Mathf.Abs(points[i].z - points[i - 1].z) * 120 / duration; if (speed > .2f && speed < 12) speeds.Add(speed); }
                speeds.Sort(); float stride = speeds.Count > 0 ? speeds[(speeds.Count - 1) * 3 / 4] : state == "Run" ? 4 : 1.5f;
                if (state == "Run") mount.runStride = stride; else mount.walkStride = stride;
            }
            mount.animator.Play("Idle", 0, 0); mount.animator.Update(0);
        }
        static void ExpandBounds()
        {
            var ground = GameObject.Find("Meadow ground"); ground.transform.position = new Vector3(0, -.65f, 75); ground.transform.localScale = new Vector3(160, 1, 300);
            string path = Root + "Materials/Expanded meadow.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(ground.GetComponent<Renderer>().sharedMaterial); var tile = material.GetTextureScale("_BaseMap"); material.SetTextureScale("_BaseMap", new Vector2(tile.x, tile.y * 300 / 180)); AssetDatabase.CreateAsset(material, path); }
            ground.GetComponent<Renderer>().sharedMaterial = material;
            foreach (var transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == "World boundary")
                {
                    if (Mathf.Abs(transform.position.x) > 70) { transform.position = new Vector3(transform.position.x, 4, 75); transform.localScale = new Vector3(1, 10, 300); }
                    else if (transform.position.z > 90) transform.position = new Vector3(0, 4, 224);
                }
                if (transform.name == "Distant ridge" && transform.position.z > 95 && Mathf.Abs(transform.position.x) < 65) transform.gameObject.SetActive(false);
            }
        }
        static void CreateValley(PlayerController player)
        {
            var root = new GameObject("08 - Greenhollow Frontier").transform;
            var wood = LoadMaterial("Dark timber"); var stone = LoadMaterial("Foundation");
            Trail("Northern frontier trail", new Vector3(0, 0, 66), new Vector3(0, 0, 156), root);
            Trail("Greenhollow approach", new Vector3(0, 0, 153), new Vector3(-26, 0, 157), root);
            Trail("Ancient grove trail", new Vector3(1, 0, 155), new Vector3(22, 0, 198), root);
            var waterMat = Solid("Willow brook water", new Color(.15f, .48f, .57f)); waterMat.SetFloat("_Smoothness", .75f);
            var water = Cube("Shallow Willow Brook", new Vector3(0, -.105f, 124), new Vector3(126, .025f, 12), waterMat, root); UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
            Cube("Willow crossing deck", new Vector3(0, -.075f, 124), new Vector3(5.2f, .25f, 16), wood, root);
            foreach (float x in new[] { -2.7f, 2.7f })
            {
                Cube("Bridge handrail", new Vector3(x, .85f, 124), new Vector3(.14f, .14f, 16), wood, root);
                for (int i = 0; i < 5; i++) Cube("Bridge post", new Vector3(x, .45f, 116 + i * 4), new Vector3(.22f, 1.1f, .22f), wood, root);
            }
            for (int i = 0; i < 9; i++)
            {
                float x = -60 + i * 15;
                if (Mathf.Abs(x) > 7) AdventureExpansion.Model("Nature/Rock_1.fbx", new Vector3(x, -.15f, 117), i * 47, .7f + i % 3 * .2f, root, true);
                if (Mathf.Abs(x + 4) > 7) AdventureExpansion.Model("Nature/Rock_1.fbx", new Vector3(x + 4, -.15f, 132), i * 37, .8f, root, true);
            }
            Cube("Greenhollow village square", new Vector3(-25, -.13f, 157), new Vector3(24, .1f, 21), LoadMaterial("Ground Village square"), root);
            foreach (float x in new[] { -34f, -17f })
            {
                Cube("Open lodge rear wall", new Vector3(x, 1.25f, 165), new Vector3(6, 2.8f, .3f), wood, root);
                foreach (float offset in new[] { -2.5f, 2.5f }) Cube("Lodge support", new Vector3(x + offset, 1.3f, 159), new Vector3(.25f, 2.9f, .25f), wood, root);
                AdventureExpansion.Model("Village/FBX/Roof_RoundTiles_6x8.fbx", new Vector3(x, 2.6f, 162), 0, 1.5f, root, false);
            }
            Props("Stall_Cart_Empty", new Vector3(-26, -.1f, 164), 180, 1.6f, root);
            Props("FarmCrate_Apple", new Vector3(-28, -.1f, 161), 25, .7f, root);
            Props("FarmCrate_Carrot", new Vector3(-29, -.1f, 162), 0, .7f, root);
            Props("Barrel_Apples", new Vector3(-23, -.1f, 164), 0, 1.1f, root);
            Props("Anvil_Log", new Vector3(-16, -.1f, 163), 30, 1.1f, root);
            Props("Table_Large", new Vector3(-33, -.1f, 162), 90, 1, root);
            Props("Bag", new Vector3(-32, .9f, 162), 25, .5f, root);
            Props("Bench", new Vector3(-31, -.1f, 159), 90, .7f, root);
            Props("Cauldron", new Vector3(-27, -.1f, 152), 0, .8f, root);
            Props("Bucket_Wooden_1", new Vector3(-35, -.1f, 158), 0, .55f, root);
            var traderTemplate = UnityEngine.Object.FindFirstObjectByType<Merchant>();
            var trader = UnityEngine.Object.Instantiate(traderTemplate, root); trader.transform.SetPositionAndRotation(new Vector3(-25, -.1f, 160), Quaternion.Euler(0, 180, 0)); trader.name = "Provisioner Mira"; trader.npcName = "Provisioner Mira";
            foreach (float x in new[] { -37f, -13f }) AdventureExpansion.Model("Village/FBX/Prop_WoodenFence_Single.fbx", new Vector3(x, -.15f, 153), 90, 1.3f, root, true);
            foreach (var mount in player.mounts)
            {
                var display = UnityEngine.Object.Instantiate(mount.prefab, root); display.name = mount.displayName + " at riding post";
                display.transform.SetPositionAndRotation(new Vector3(mount.mountId == "horse" ? -33 : -18, -.12f, 153), Quaternion.Euler(0, 120, 0));
                display.gameObject.layer = 0; var box = display.gameObject.AddComponent<BoxCollider>(); box.center = new Vector3(0, .9f, -.1f); box.size = new Vector3(1, 1.5f, 2.4f);
            }
            Label("GREENHOLLOW RIDING POST", new Vector3(-25, 3.6f, 150), root, .065f);
            Label("[Z] Summon horse  /  [V] Change mount", new Vector3(-25, 3.1f, 150), root, .045f);
            Landmark("willow-crossing", "Willow Crossing", new Vector3(5, -.15f, 133), root);
            Landmark("greenhollow-post", "Greenhollow Post", new Vector3(-23, -.1f, 156), root);
            Landmark("ancient-grove", "Ancient Grove", new Vector3(22, -.15f, 201), root);
            for (int i = 0; i < 8; i++)
            {
                if (i == 6) continue;
                float angle = i * Mathf.PI / 4;
                AdventureExpansion.Model("Nature/Rock_1.fbx", new Vector3(22 + Mathf.Cos(angle) * 7, -.15f, 201 + Mathf.Sin(angle) * 7), i * 37, 2.2f + i % 2, root, true);
            }
            var rogue = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(e => e.enemyId == "road-raider-2");
            for (int i = 0; i < 3; i++)
            {
                var scout = UnityEngine.Object.Instantiate(rogue, root); scout.name = "Ashen Frontier Scout"; scout.enemyId = "frontier-scout-" + (i + 1); scout.campGuard = true; scout.isBoss = false; scout.requiresRunes = false; scout.heavyAttacker = false;
                scout.transform.position = new Vector3(16 + i * 8, 0, 178 + i % 2 * 11); scout.player = player; scout.attackDamage = 13;
                var stats = scout.GetComponent<Combatant>(); stats.displayName = "Ashen Frontier Scout"; stats.maxHealth = 100; stats.flatDamageReduction = 2;
                Props("Crate_Wooden", scout.transform.position + new Vector3(3, -.15f, 3), 30, 1.2f, root);
            }
            PolishSetup.Chest("valley-cache", new Vector3(-36, -.1f, 161), 25, 40, root);
            PolishSetup.Chest("grove-cache", new Vector3(22, -.15f, 204.5f), 40, 70, root);
            foreach (var item in new[] { ("Cow", new Vector3(-42, -.15f, 151), 1.65f), ("Sheep", new Vector3(-39, -.15f, 143), 1f), ("Sheep", new Vector3(-44, -.15f, 145), .9f), ("Llama", new Vector3(-11, -.15f, 154), 1.8f), ("Pug", new Vector3(-26, -.15f, 153), .55f) })
            {
                var animal = Animal(item.Item1, item.Item3, item.Item2, root); var wildlife = animal.AddComponent<Wildlife>(); wildlife.animator = animal.GetComponentInChildren<Animator>(); wildlife.wander = item.Item1 == "Cow";
            }
            var random = new System.Random(43019);
            for (int i = 0; i < 75; i++)
            {
                float x = (float)random.NextDouble() * 138 - 69, z = 110 + (float)random.NextDouble() * 108;
                if (Mathf.Abs(x) < 7 || (x > -41 && x < -9 && z > 137 && z < 173) || (x > 9 && x < 40 && z > 172) || (z > 115 && z < 133)) continue;
                AdventureExpansion.Model("Nature/BirchTree_" + (i % 5 + 1) + ".fbx", new Vector3(x, -.15f, z), i * 41, 5 + i % 4, root, true);
                if (i % 2 == 0) AdventureExpansion.Model("Nature/Bush_Small.fbx", new Vector3(x + 2, -.15f, z + 1), i * 27, .7f, root, false);
            }
            for (int i = 0; i < 8; i++)
            {
                var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere); hill.name = "Greenhollow distant ridge"; hill.transform.SetParent(root);
                hill.transform.position = new Vector3(-100 + i * 29, -9, i % 2 == 0 ? 240 : 252); hill.transform.localScale = new Vector3(58, 40 + i % 3 * 7, 48); hill.GetComponent<Renderer>().sharedMaterial = LoadMaterial("Distant hills"); UnityEngine.Object.DestroyImmediate(hill.GetComponent<Collider>());
            }
        }
        static void Landmark(string id, string name, Vector3 point, Transform parent)
        {
            var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = point;
            var landmark = root.AddComponent<Riftbound.Landmark>(); landmark.landmarkId = id; landmark.displayName = name;
            Cube("Scout marker post", point + Vector3.up * .85f, new Vector3(.2f, 1.8f, .2f), LoadMaterial("Dark timber"), root.transform);
            Cube("Scout marker board", point + Vector3.up * 1.65f, new Vector3(1.25f, .55f, .12f), LoadMaterial("Foundation"), root.transform);
            Label("[E] " + name, point + Vector3.up * 2.4f, root.transform, .04f);
        }
        static void Props(string name, Vector3 point, float yaw, float height, Transform parent) => AdventureExpansion.Model("FantasyProps/FBX/" + name + ".fbx", point, yaw, height, parent, true);
        static void PolishValley()
        {
            var root = GameObject.Find("08 - Greenhollow Frontier").transform;
            var trail = root.Find("Northern frontier trail");
            if (trail)
            {
                trail.position = new Vector3(0, -.13f, 111); trail.localScale = new Vector3(5, .08f, 90);
                var material = trail.GetComponent<Renderer>().sharedMaterial; material.SetTextureScale("_BaseMap", new Vector2(2.5f, 45)); EditorUtility.SetDirty(material);
            }
            foreach (var chest in root.GetComponentsInChildren<LootChest>()) if (chest.chestId == "grove-cache") chest.transform.position = new Vector3(22, -.15f, 204.5f);
            foreach (var display in root.GetComponentsInChildren<MountVisual>())
            {
                if (display.artVersion >= 2) continue;
                display.animator.transform.localRotation = Quaternion.Euler(0, 180, 0); display.animator.Rebind(); display.animator.Play("Idle", 0, 0); display.animator.Update(0);
                display.saddle.position = display.transform.TransformPoint(new Vector3(0, 1.8f, -.3f));
                var pad = display.saddle.Find("Leather saddle"); if (pad) pad.position = display.saddle.position - Vector3.up * .12f;
                display.artVersion = 2;
            }
            foreach (var animal in root.GetComponentsInChildren<Wildlife>()) animal.animator.transform.localRotation = Quaternion.Euler(0, 180, 0);
            foreach (Transform item in root)
            {
                if (item.name == "Rock_1" && ((Mathf.Abs(item.position.x) < 7 && item.position.z >= 115 && item.position.z <= 133) || Vector3.Distance(item.position, new Vector3(22, -.15f, 194)) < .2f)) item.gameObject.SetActive(false);
                if (item.name == "Roof_RoundTiles_6x8")
                {
                    var bounds = BoundsOf(item.gameObject); var scale = item.localScale;
                    item.localScale = new Vector3(scale.x * 7 / bounds.size.x, scale.y, scale.z * 8 / bounds.size.z);
                }
                if (!item.name.StartsWith("BirchTree_")) continue;
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Nature/" + item.name + ".fbx");
                var sourceRenderers = source.GetComponentsInChildren<Renderer>();
                foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                {
                    var original = sourceRenderers.FirstOrDefault(r => r.name == renderer.name); if (!original) continue;
                    renderer.sharedMaterials = original.sharedMaterials.Select(mat =>
                    {
                        string path = Root + "Materials/Nature_" + mat.name + ".mat"; var converted = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!converted)
                        {
                            bool leaf = mat.name.ToLowerInvariant().Contains("leave"); converted = new Material(LoadMaterial(leaf ? "Nature_NormalTree_Leaves" : "Nature_NormalTree_Bark"));
                            if (mat.mainTexture) { converted.SetTexture("_BaseMap", mat.mainTexture); converted.SetColor("_BaseColor", Color.white); }
                            AssetDatabase.CreateAsset(converted, path);
                        }
                        return converted;
                    }).ToArray();
                }
            }
        }
        static void Trail(string name, Vector3 from, Vector3 to, Transform parent)
        {
            string path = Root + "Materials/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(LoadMaterial("Ground Forest road")); mat.SetTextureScale("_BaseMap", new Vector2(2.5f, Vector3.Distance(from, to) / 2)); AssetDatabase.CreateAsset(mat, path); }
            var go = Cube(name, (from + to) / 2 + Vector3.down * .13f, new Vector3(5, .08f, Vector3.Distance(from, to)), mat, parent); go.transform.rotation = Quaternion.LookRotation(to - from);
        }
        static void Label(string text, Vector3 position, Transform parent, float size)
        {
            var label = new GameObject(text); label.transform.SetParent(parent); label.transform.position = position;
            var mesh = label.AddComponent<TextMesh>(); mesh.text = text; mesh.characterSize = size; mesh.fontSize = 64; mesh.anchor = TextAnchor.MiddleCenter; mesh.color = new Color(.75f, 1, .8f); label.AddComponent<WorldLabel>();
        }
        static Material LoadMaterial(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");
        static Material Solid(string name, Color color)
        {
            string path = Root + "Materials/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.SetColor("_BaseColor", color); AssetDatabase.CreateAsset(mat, path); } return mat;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 size, Material mat, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent); go.transform.position = position;
            var scale = parent.lossyScale; go.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z); go.GetComponent<Renderer>().sharedMaterial = mat; return go;
        }
        static Bounds BoundsOf(GameObject model)
        {
            var renderers = model.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static void SetLayer(Transform root, int layer) { root.gameObject.layer = layer; foreach (Transform child in root) SetLayer(child, layer); }
        public static void BatchPreview()
        {
            try
            {
                EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity");
                foreach (var mount in UnityEngine.Object.FindObjectsByType<MountVisual>(FindObjectsSortMode.None)) { mount.animator.Rebind(); mount.animator.Play("Idle", 0, .4f); mount.animator.Update(0); }
                DemoPassSetup.RenderWorld(new Vector3(53, 65, 112), new Vector3(-3, 0, 164), "Greenhollow-Overview.png");
                DemoPassSetup.RenderWorld(new Vector3(-3, 8, 140), new Vector3(-25, 1, 159), "Greenhollow-Riding-Post.png");
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                foreach (var definition in player.mounts)
                {
                    var copy = UnityEngine.Object.Instantiate(player.gameObject); copy.transform.position = new Vector3(0, -400, 0);
                    var rider = copy.GetComponent<PlayerController>(); var mount = UnityEngine.Object.Instantiate(definition.prefab, copy.transform);
                    mount.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var hips = rider.animator.GetComponentsInChildren<Transform>().First(t => t.name.EndsWith(":Hips"));
                    foreach (var renderer in rider.animator.GetComponentsInChildren<Renderer>()) if (renderer.name.ToLowerInvariant().Contains("axe")) renderer.enabled = false;
                    rider.animator.Rebind(); mount.animator.Rebind();
                    foreach (string state in new[] { "Idle", "Run" })
                    {
                        mount.animator.Play(state, 0, .35f); mount.animator.Update(0); rider.animator.Play("Ride", 0, .35f); rider.animator.Update(0);
                        rider.animator.transform.position += mount.saddle.position - hips.position;
                        RenderMount(copy, "Mount-" + definition.mountId + "-" + state + ".png");
                    }
                    UnityEngine.Object.DestroyImmediate(copy);
                }
                File.WriteAllText("MountPreviewResult.txt", "Edit-mode renders: Greenhollow overview/post; horse and zebra with native seated rider in Idle/Run.\n"); EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("MountPreviewResult.txt", "FAILED: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static void RenderMount(GameObject model, string filename)
        {
            SetLayer(model.transform, 30);
            var camera = new GameObject("Mount pose camera").AddComponent<Camera>(); var light = new GameObject("Mount pose light").AddComponent<Light>();
            var texture = new RenderTexture(900, 900, 24); var previous = RenderTexture.active; bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                camera.cullingMask = 1 << 30; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f, .14f, .16f);
                camera.transform.position = model.transform.position + new Vector3(5.5f, 2.8f, 5.5f); camera.transform.LookAt(model.transform.position + Vector3.up * 1.7f); camera.fieldOfView = 42; camera.nearClipPlane = .05f; camera.farClipPlane = 30; camera.targetTexture = texture;
                light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(35, -35, 0);
                ShaderUtil.allowAsyncCompilation = false; camera.Render(); camera.Render(); RenderTexture.active = texture;
                var image = new Texture2D(900, 900, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); image.Apply(); File.WriteAllBytes(filename, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(light.gameObject); }
        }
        static void Validate()
        {
            var report = new StringBuilder();
            void Check(bool valid, string text) { report.AppendLine((valid ? "PASS " : "FAIL ") + text); if (!valid) throw new InvalidOperationException(text); }
            var ground = GameObject.Find("Meadow ground");
            Check(ground && ground.transform.localScale.z == 300 && ground.transform.position.z == 75, "Playable ground expanded from 160x180 to 160x300 metres");
            Check(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Any(t => t.name == "World boundary" && t.position.z == 224), "Northern world boundary moved to new edge");
            Physics.SyncTransforms();
            float bridgeElevation = Terrain.activeTerrain ? Terrain.activeTerrain.SampleHeight(new Vector3(0, 0, 114)) + Terrain.activeTerrain.transform.position.y + .15f : 0;
            Check(!Physics.BoxCast(new Vector3(0, 1.1f + bridgeElevation, 114), new Vector3(.58f, .7f, 1.25f), Vector3.forward, out _, Quaternion.identity, 21, ~(1 << 2), QueryTriggerInteraction.Ignore), "Bridge route is clear for the mounted body");
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(); var definitions = player.mounts;
            Check(definitions.Length == 2 && definitions.Select(m => m.mountId).Distinct().Count() == 2, "Two unique mount definitions assigned to player");
            foreach (var mount in definitions)
            {
                Check(mount.prefab && mount.prefab.saddle && mount.prefab.animator.avatar.isValid && !mount.prefab.animator.isHuman, mount.displayName + " has a native rig and saddle");
                var controller = (AnimatorController)mount.prefab.animator.runtimeAnimatorController;
                foreach (string state in new[] { "Idle", "Walk", "Run" })
                {
                    var clip = (AnimationClip)controller.layers[0].stateMachine.states.First(s => s.state.name == state).state.motion;
                    Check(clip && !clip.isHumanMotion && clip.isLooping, mount.displayName + " / " + state + " uses own looping animation");
                }
                Check(mount.prefab.walkStride > .2f && mount.prefab.runStride > 1, mount.displayName + " stride measured from its hoof motion");
            }
            var riderController = (AnimatorController)player.animator.runtimeAnimatorController;
            var ride = (AnimationClip)riderController.layers[0].stateMachine.states.First(s => s.state.name == "Ride").state.motion;
            Check(ride && !ride.isHumanMotion && ride.isLooping && AnimationUtility.GetCurveBindings(ride).Length > 100, "Seated rider is baked to native Brute curves");
            var landmarks = UnityEngine.Object.FindObjectsByType<Riftbound.Landmark>(FindObjectsSortMode.None);
            Check(landmarks.Length == 3 && landmarks.Select(l => l.landmarkId).Distinct().Count() == 3, "Three distinct Greenhollow scouting markers");
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(enemies.Select(e => e.enemyId).Distinct().Count() == enemies.Length && enemies.All(e => e.player == player), "Expanded enemies have unique IDs and correct player references");
            Check(enemies.Count(e => e.isBoss) == 1 && enemies.Where(e => e.enemyId.StartsWith("frontier-")).All(e => e.campGuard && !e.isBoss), "Frontier scouts do not affect Abe's raider/captain counters");
            var chests = UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None);
            Check(chests.Length == 6 && chests.Select(c => c.chestId).Distinct().Count() == 6, "Six distinct persistent caches");
            Check(UnityEngine.Object.FindObjectsByType<Wildlife>(FindObjectsSortMode.None).Length == 5, "Five harmless animated wildlife actors");
            Check(UnityEngine.Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None).Length == 2, "Village and Greenhollow both have provisioners");
            foreach (string name in new[] { "MI_Trim_Cloth", "MI_Trim_Furniture", "MI_Trim_Metal", "MI_Trim_Props" })
            {
                var mat = LoadMaterial("FantasyProps_" + name); Check(mat && mat.GetTexture("_BaseMap"), "Fantasy prop atlas assigned: " + name);
            }
            string previousSave = PlayerController.SavePathOverride;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                definitions = new[] { AssetDatabase.LoadAssetAtPath<MountDefinition>(Root + "Data/Mounts/Horse.asset"), AssetDatabase.LoadAssetAtPath<MountDefinition>(Root + "Data/Mounts/Zebra.asset") };
                PlayerController.SavePathOverride = Path.GetFullPath("Library/MountChecks/adventure.json");
                var actor = new GameObject("Mount progress checks").AddComponent<PlayerController>();
                typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, null); actor.Stats.Restore(); actor.mounts = definitions;
                Check(actor.MountUnlocked(definitions[0]) && !actor.MountUnlocked(definitions[1]), "Horse available immediately; zebra requires exploration");
                actor.CycleMount(); Check(actor.SelectedMountId == "horse", "Cycling cannot select a locked mount");
                actor.DiscoverLandmark("not-a-landmark"); Check(actor.LandmarksFound == 0, "Unknown landmark IDs cannot unlock mounts");
                actor.DiscoverLandmark("willow-crossing"); int xp = actor.Experience, gold = actor.Gold;
                actor.DiscoverLandmark("willow-crossing"); Check(actor.LandmarksFound == 1 && actor.Experience == xp && actor.Gold == gold, "Landmark gold and XP pay once");
                actor.DiscoverLandmark("greenhollow-post"); Check(!actor.MountUnlocked(definitions[1]), "Two landmarks do not unlock zebra");
                actor.DiscoverLandmark("ancient-grove"); Check(actor.MountUnlocked(definitions[1]) && actor.Experience == 125 && actor.Gold == 30, "Third landmark unlocks zebra with exploration rewards");
                actor.CycleMount(); Check(actor.SelectedMountId == "zebra", "Unlocked zebra can be selected");
                actor.LoadAdventure(); Check(actor.SelectedMountId == "zebra" && actor.LandmarksFound == 3 && actor.Experience == 125, "Scouting and selected mount survive save/load");
                Check(actor.Kills == 0 && !actor.BossDefeated, "Exploration rewards do not complete combat quests");
                File.WriteAllText(PlayerController.SavePathOverride, "{\"version\":1,\"gold\":17,\"mount\":\"zebra\"}"); actor.LoadAdventure();
                Check(actor.Gold == 17 && actor.LandmarksFound == 0 && actor.SelectedMountId == "horse", "Older/incomplete saves load safely and cannot bypass mount unlock");
            }
            finally { PlayerController.SavePathOverride = previousSave; EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity"); }
            File.WriteAllText("MountExpansionResult.txt", report + "Edit-mode data/rig checks only. Mounted gameplay and encounter balance remain for user testing.\n");
        }
        public static void BatchInspect()
        {
            try
            {
                var report = new StringBuilder();
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art/Animals" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    importer.SaveAndReimport();
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    report.AppendLine(path);
                    foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__"))) report.AppendLine($"  {clip.name}: {clip.length:F2}s loop={clip.isLooping}");
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>()) report.AppendLine("  mesh " + renderer.name + " bounds " + renderer.bounds.size + " mats " + string.Join(",", renderer.sharedMaterials.Select(m => m ? m.name + ":" + m.color : "null")));
                    report.AppendLine("  bones " + string.Join(",", model.GetComponentsInChildren<Transform>().Select(t => t.name)));
                }
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity");
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                report.AppendLine("PLAYER animator=" + player.animator.name + " scale=" + player.animator.transform.localScale + " parent=" + player.animator.transform.parent.name);
                foreach (var renderer in player.animator.GetComponentsInChildren<SkinnedMeshRenderer>()) report.AppendLine("PLAYER mesh=" + renderer.name + " materials=" + string.Join(",", renderer.sharedMaterials.Select(m => m.name)));
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art/FantasyProps" }).Take(4))
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                    foreach (var renderer in model.GetComponentsInChildren<Renderer>()) report.AppendLine("PROP " + model.name + " mats=" + string.Join(",", renderer.sharedMaterials.Select(m => m ? m.name : "null")));
                }
                File.WriteAllText("MountAssetInspection.txt", report.ToString()); EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("MountAssetInspection.txt", "FAILED: " + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
