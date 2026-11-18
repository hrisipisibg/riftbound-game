using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class PrototypeBuilder
    {
        const string Root = "Assets/RPG/";
        const string ScenePath = Root + "Scenes/Outpost.unity";
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static PrototypeBuilder() { EditorApplication.delayCall += FirstSetup; }
        static void FirstSetup()
        {
            if (File.Exists(ScenePath)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += FirstSetup; return; }
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Brute/Brute.fbx")) return;
            // Never replace an unsaved scene during the automatic first setup.
            if (EditorSceneManager.GetActiveScene().isDirty) return;
            Build();
        }

        [MenuItem("Riftbound/Create prototype scene")]
        public static void Build()
        {
            if (File.Exists(ScenePath)) { EditorSceneManager.OpenScene(ScenePath); return; }
            BuildScene();
        }
        [MenuItem("Riftbound/Rebuild generated prototype scene")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlaying) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScene();
        }
        static void BuildScene()
        {
            try
            {
                Materials.Clear();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                UnityEngine.Random.InitState(621);
                SetupLighting();
                var terrain = new GameObject("01 - Landscape").transform;
                var village = new GameObject("02 - Emberwatch Outpost").transform;
                var forest = new GameObject("03 - Forest Road").transform;
                var gameplay = new GameObject("04 - Gameplay").transform;
                var grass = Solid("Meadow", new Color(.25f, .37f, .19f));
                var dirt = Solid("Road", new Color(.43f, .36f, .25f));
                var stone = Solid("Foundation", new Color(.33f, .37f, .35f));
                var wood = Solid("Dark timber", new Color(.24f, .14f, .085f));
                var plaster = Solid("Warm plaster", new Color(.74f, .67f, .49f));
                var red = Solid("Faction crimson", new Color(.47f, .12f, .09f));
                var gold = Solid("Beacon gold", new Color(1, .66f, .15f), true);
                Cube("Meadow ground", new Vector3(0, -.65f, 15), new Vector3(160, 1, 180), grass, terrain);
                Cube("Village square", new Vector3(0, -.11f, 0), new Vector3(24, .1f, 27), dirt, terrain);
                Cube("Forest road", new Vector3(0, -.09f, 35), new Vector3(6, .1f, 65), dirt, terrain);
                Cube("West lane", new Vector3(-14, -.09f, 2), new Vector3(19, .1f, 4), dirt, terrain);
                Cube("East lane", new Vector3(14, -.09f, 2), new Vector3(19, .1f, 4), dirt, terrain);
                House(new Vector3(-12, -.15f, -10), -90, village, plaster, wood, stone);
                House(new Vector3(12, -.15f, -10), 90, village, plaster, wood, stone);
                House(new Vector3(-16, -.15f, 8), -90, village, plaster, wood, stone);
                House(new Vector3(16, -.15f, 8), 90, village, plaster, wood, stone);
                House(new Vector3(-12, -.15f, 24), -90, village, plaster, wood, stone);
                House(new Vector3(12, -.15f, 24), 90, village, plaster, wood, stone);
                // Gate, banners and a visible destination orient the player along the road.
                foreach (int side in new[] { -1, 1 })
                {
                    Cube("Gate post", new Vector3(side * 4, 2.4f, 18), new Vector3(.65f, 5.2f, .65f), wood, village);
                    Cube("Crimson banner", new Vector3(side * 4, 3, 17.6f), new Vector3(1.4f, 2, .1f), red, village);
                }
                Cube("Gate crossbeam", new Vector3(0, 4.8f, 18), new Vector3(9, .5f, .6f), wood, village);
                Text("Village sign", "EMBERWATCH OUTPOST", new Vector3(0, 4.2f, 17.6f), .18f, Color.white, village, Quaternion.identity);
                foreach (var p in new[] { new Vector3(-6, 0, -4), new Vector3(7, 0, 10), new Vector3(-8, 0, 17) })
                    Place("Village/FBX/Prop_Crate.fbx", p, 0, 1, village, true);
                Place("Village/FBX/Prop_Wagon.fbx", new Vector3(8, 0, -1), 25, 1, village, true);

                for (int i = 0; i < 160; i++)
                {
                    float x = UnityEngine.Random.Range(-65f, 65f), z = UnityEngine.Random.Range(-45f, 90f);
                    if (Mathf.Abs(x) < 25 && z < 32 && z > -21) continue;
                    if (Mathf.Abs(x) < 7 && z > -5 && z < 72) continue;
                    string species = i % 3 == 0 ? "PineTree_" : "NormalTree_";
                    var tree = Place("Nature/" + species + (i % 5 + 1) + ".fbx", new Vector3(x, -.15f, z), UnityEngine.Random.Range(0, 360), 1, forest, false);
                    if (tree)
                    {
                        FitHeight(tree, UnityEngine.Random.Range(6f, 10f)); Ground(tree, -.15f);
                        var collider = tree.AddComponent<CapsuleCollider>();
                        collider.center = new Vector3(0, 1, 0); collider.radius = .13f; collider.height = 2;
                    }
                }
                for (int i = 0; i < 100; i++)
                {
                    float x = UnityEngine.Random.Range(-45f, 45f), z = UnityEngine.Random.Range(-30f, 78f);
                    if (Mathf.Abs(x) < 6 || (Mathf.Abs(x) < 24 && z < 30 && z > -20)) continue;
                    bool rock = i % 4 == 0;
                    var item = Place("Nature/" + (rock ? "Rock_" + (i % 5 + 1) : "Bush") + ".fbx", new Vector3(x, -.15f, z), i * 37, 1, forest, false);
                    if (item) { FitHeight(item, rock ? UnityEngine.Random.Range(1, 3f) : .8f); Ground(item, -.15f); }
                }
                for (int i = 0; i < 100; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * UnityEngine.Random.Range(4.2f, 7f);
                    float z = UnityEngine.Random.Range(28, 74);
                    var clump = Place("Nature/" + (i % 5 == 0 ? "Flower_1_Clump" : "Grass_Small") + ".fbx", new Vector3(x, -.15f, z), i * 31, 1, forest, false);
                    if (clump) { FitHeight(clump, UnityEngine.Random.Range(.25f, .55f)); Ground(clump, -.15f); }
                }
                // Hills frame the playable area without requiring terrain packages.
                for (int i = 0; i < 20; i++)
                {
                    float angle = i * Mathf.PI * 2 / 20;
                    var hill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    hill.name = "Distant ridge"; hill.transform.SetParent(terrain);
                    hill.transform.position = new Vector3(Mathf.Sin(angle) * 85, -7, 20 + Mathf.Cos(angle) * 100);
                    hill.transform.localScale = new Vector3(45, UnityEngine.Random.Range(30, 55), 43);
                    hill.GetComponent<Renderer>().sharedMaterial = Solid("Distant hills", new Color(.20f, .29f, .23f));
                    UnityEngine.Object.DestroyImmediate(hill.GetComponent<Collider>());
                }
                var beacon = new GameObject("Patrol mission - press E nearby").transform;
                beacon.SetParent(gameplay); beacon.position = new Vector3(-3, 0, 2);
                Cube("Beacon pedestal", beacon.position + Vector3.up * .35f, new Vector3(1.4f, .7f, 1.4f), stone, beacon);
                var crystal = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crystal.name = "Golden mission beacon"; crystal.transform.SetParent(beacon);
                crystal.transform.position = beacon.position + Vector3.up * 1.7f; crystal.transform.localScale = new Vector3(.55f, 1.2f, .55f);
                crystal.GetComponent<Renderer>().sharedMaterial = gold;
                Text("Mission marker", "!", beacon.position + Vector3.up * 3.5f, .8f, new Color(1, .8f, .25f), beacon, Quaternion.Euler(0, 180, 0));
                var beaconLight = new GameObject("Beacon glow").AddComponent<Light>();
                beaconLight.transform.SetParent(beacon); beaconLight.transform.position = beacon.position + Vector3.up * 2;
                beaconLight.type = LightType.Point; beaconLight.color = new Color(1, .65f, .2f); beaconLight.intensity = 3; beaconLight.range = 7;

                var controller = CreateAnimator();
                var playerGO = Character("Brute - Player", new Vector3(0, .05f, -6), gameplay, controller, false);
                var playerStats = playerGO.AddComponent<Combatant>(); playerStats.isPlayer = true; playerStats.displayName = "Brute"; playerStats.maxHealth = 150;
                var player = playerGO.AddComponent<PlayerController>(); player.animator = playerGO.GetComponentInChildren<Animator>(); player.questGiver = beacon;
                player.walkSpeed = 1.5f; player.runSpeed = 5;
                SetLayer(playerGO.transform, 2);
                var cameraGO = new GameObject("Main Camera"); cameraGO.tag = "MainCamera";
                var camera = cameraGO.AddComponent<Camera>(); camera.fieldOfView = 62; camera.farClipPlane = 220;
                cameraGO.AddComponent<AudioListener>(); cameraGO.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
                var orbit = cameraGO.AddComponent<OrbitCamera>(); orbit.target = player.transform; player.orbit = orbit;
                cameraGO.transform.position = player.transform.position + new Vector3(0, 4, -7); cameraGO.transform.LookAt(player.transform.position + Vector3.up * 1.5f);
                cameraGO.AddComponent<PrototypeHUD>().player = player;
                var positions = new[] { new Vector3(-3, .05f, 36), new Vector3(4, .05f, 46), new Vector3(-4, .05f, 57), new Vector3(3, .05f, 65) };
                for (int i = 0; i < positions.Length; i++)
                {
                    var enemyGO = Character("Forest raider " + (i + 1), positions[i], gameplay, controller, true);
                    var stats = enemyGO.AddComponent<Combatant>(); stats.maxHealth = 70; stats.displayName = "Forest raider";
                    var enemy = enemyGO.AddComponent<EnemyController>(); enemy.player = player; enemy.animator = enemyGO.GetComponentInChildren<Animator>();
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/Abe/Ch39_nonPBR@Old Man Idle.fbx")) AbeSetup.Setup();
                if (AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/Ground/bricks_wall_14_basecolor_1k.png")) GroundTextureSetup.Apply();
                var builds = EditorBuildSettings.scenes.ToList();
                if (!builds.Any(s => s.path == ScenePath)) builds.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = builds.ToArray();
                AssetDatabase.SaveAssets();
                File.WriteAllText("PrototypeSetupResult.txt", "SUCCESS: " + ScenePath + "\n" + DateTime.UtcNow.ToString("O"));
                Debug.Log("Riftbound prototype ready. Open Assets/RPG/Scenes/Outpost and press Play.");
            }
            catch (Exception e) { File.WriteAllText("PrototypeSetupResult.txt", e.ToString()); Debug.LogException(e); }
        }

        static void SetupLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.58f, .69f, .77f);
            RenderSettings.ambientEquatorColor = new Color(.43f, .50f, .43f);
            RenderSettings.ambientGroundColor = new Color(.24f, .26f, .21f);
            RenderSettings.fog = true; RenderSettings.fogColor = new Color(.56f, .68f, .68f);
            RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 55; RenderSettings.fogEndDistance = 160;
            var sky = new Material(Shader.Find("Skybox/Procedural")); sky.SetColor("_SkyTint", new Color(.48f, .65f, .8f));
            sky.SetFloat("_AtmosphereThickness", .8f); RenderSettings.skybox = sky;
            RenderSettings.skybox = PersistMaterial(sky, Root + "Materials/Outpost sky.mat");
            var sun = new GameObject("Late afternoon sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.intensity = 1.6f; sun.color = new Color(1, .89f, .72f); sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38, -35, 0); RenderSettings.sun = sun;
        }

        static void House(Vector3 position, float yaw, Transform parent, Material plaster, Material wood, Material stone)
        {
            var house = new GameObject("Modular village house").transform; house.SetParent(parent);
            Cube("Stone foundation", new Vector3(0, .1f, 0), new Vector3(6.3f, .5f, 6.3f), stone, house);
            // The imported wall pieces retain their own texture mapping; bounds fitting makes their grid explicit.
            for (int level = 0; level < 2; level++)
                for (int side = 0; side < 4; side++)
                    for (int section = 0; section < 2; section++)
                    {
                        string file = side == 0 && section == 0 && level == 0 ? "Wall_Plaster_Door_Round" : (level == 1 ? "Wall_Plaster_Window_Wide_Flat" : "Wall_UnevenBrick_Straight");
                        var piece = Place("Village/FBX/" + file + ".fbx", Vector3.zero, 0, 1, house, false);
                        if (!piece) continue;
                        FitBox(piece, new Vector3(3, 2.5f, .28f));
                        var rotation = Quaternion.Euler(0, side * 90, 0);
                        piece.transform.localPosition = rotation * new Vector3(section == 0 ? -1.5f : 1.5f, .35f + level * 2.5f, -3);
                        piece.transform.localRotation = rotation;
                    }
            Cube("Interior wall backing", new Vector3(0, 2.8f, 0), new Vector3(5.7f, 4.9f, 5.7f), plaster, house);
            for (int side = 0; side < 4; side++)
            {
                var face = new GameObject("Exterior timber and windows").transform; face.SetParent(house, false);
                foreach (float x in new[] { -3.05f, 0, 3.05f })
                    Cube("Timber frame", new Vector3(x, 2.8f, -3.18f), new Vector3(.17f, 5, .18f), wood, face);
                Cube("Timber belt", new Vector3(0, 2.85f, -3.2f), new Vector3(6.2f, .2f, .2f), wood, face);
                for (int window = 0; window < 2; window++)
                {
                    var pane = Place("Village/FBX/Window_Wide_Flat1.fbx", Vector3.zero, 0, 1, face, false);
                    if (pane) { FitBox(pane, new Vector3(1.1f, 1.3f, .2f)); pane.transform.localPosition = new Vector3(window == 0 ? -1.5f : 1.5f, 3.3f, -3.3f); }
                }
                face.localRotation = Quaternion.Euler(0, side * 90, 0);
            }
            var roof = Place("Village/FBX/Roof_RoundTiles_6x8.fbx", Vector3.zero, 0, 1, house, false);
            if (roof) { FitBox(roof, new Vector3(7.2f, 2.6f, 7.5f)); roof.transform.localPosition = new Vector3(0, 5.3f, 0); }
            var door = Place("Village/FBX/Door_1_Round.fbx", Vector3.zero, 0, 1, house, false);
            if (door) { FitBox(door, new Vector3(1, 2.1f, .15f)); door.transform.localPosition = new Vector3(-1.5f, .35f, -3.25f); }
            var chimney = Place("Village/FBX/Prop_Chimney.fbx", new Vector3(2, 6.6f, 1), 0, 1, house, false);
            if (chimney) FitHeight(chimney, 1.8f);
            Cube("Building collision", new Vector3(0, 2.8f, 0), new Vector3(6.2f, 5.5f, 6.2f), plaster, house).GetComponent<Renderer>().enabled = false;
            house.position = position; house.rotation = Quaternion.Euler(0, yaw, 0);
        }

        static GameObject Character(string name, Vector3 position, Transform parent, RuntimeAnimatorController controller, bool enemy)
        {
            var root = new GameObject(name); root.transform.SetParent(parent); root.transform.position = position;
            var motor = root.AddComponent<CharacterController>(); motor.height = 2.1f; motor.radius = .42f; motor.center = Vector3.up * 1.05f; motor.stepOffset = .35f;
            var visual = Place("Brute/Brute.fbx", Vector3.zero, 0, 1, root.transform, false);
            if (visual)
            {
                FitHeight(visual, 2.1f); Ground(visual, position.y);
                var animator = visual.GetComponentInChildren<Animator>(); if (!animator) animator = visual.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<GroundedVisual>().animator = animator;
                if (enemy) foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var source = materials[i]; if (!source) continue;
                        string key = "Raider " + source.name;
                        if (!Materials.TryGetValue(key, out var replacement))
                        {
                            replacement = new Material(source) { name = key };
                            replacement.SetColor("_BaseColor", source.GetColor("_BaseColor") * new Color(.82f, .57f, .52f));
                            replacement = PersistMaterial(replacement, Root + "Materials/" + key + ".mat"); Materials[key] = replacement;
                        }
                        materials[i] = replacement;
                    }
                    renderer.sharedMaterials = materials;
                }
            }
            return root;
        }

        static AnimatorController CreateAnimator()
        {
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(Root + "Animations/Brute.controller");
            if (existing) { AnimationRepair.ConfigureController(existing); return existing; }
            var controller = AnimatorController.CreateAnimatorControllerAtPath(Root + "Animations/Brute.controller");
            var sm = controller.layers[0].stateMachine;
            string[] states = { "Idle", "Walk", "Run", "Attack", "Cleave", "Hit" };
            string[] files = { "standing idle", "unarmed walk forward", "standing run forward", "standing melee attack horizontal", "standing melee attack 360 low", "standing react large gut" };
            for (int i = 0; i < states.Length; i++)
            {
                var clip = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/Brute/" + files[i] + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
                var state = sm.AddState(states[i], new Vector3(i % 3 * 230, i / 3 * 100)); state.motion = clip;
                if (i == 0) sm.defaultState = state;
                if (!clip) Debug.LogWarning("Missing animation: " + files[i]);
            }
            AnimationRepair.ConfigureController(controller);
            return controller;
        }

        static GameObject Place(string relative, Vector3 localPosition, float yaw, float scale, Transform parent, bool collider)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/" + relative);
            if (!asset) { Debug.LogWarning("Missing model " + relative); return null; }
            var go = new GameObject(asset.name);
            go.transform.SetParent(parent, false); go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0); go.transform.localScale = Vector3.one * scale;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(go.transform, false);
            foreach (var renderer in go.GetComponentsInChildren<Renderer>())
            {
                var list = renderer.sharedMaterials;
                for (int i = 0; i < list.Length; i++) if (list[i]) list[i] = ConvertMaterial(list[i], relative);
                renderer.sharedMaterials = list;
            }
            if (collider) { var bounds = BoundsOf(go); var c = go.AddComponent<BoxCollider>(); c.center = go.transform.InverseTransformPoint(bounds.center); c.size = bounds.size / scale; }
            return go;
        }

        static Material ConvertMaterial(Material source, string relative)
        {
            string family = relative.Split('/')[0]; string key = family + "_" + source.name.Replace('/', '_');
            if (Materials.TryGetValue(key, out var known)) return known;
            var result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
            result.SetFloat("_Smoothness", .15f);
            var color = source.HasProperty("_Color") ? source.color : Color.white;
            result.SetColor("_BaseColor", color);
            if (source.mainTexture) result.SetTexture("_BaseMap", source.mainTexture);
            if (family == "Nature" && !source.mainTexture)
            {
                string name = source.name.ToLowerInvariant();
                var tint = name.Contains("bark") ? new Color(.28f, .19f, .12f) : name.Contains("rock") ? new Color(.40f, .44f, .40f) : name.Contains("flower") ? new Color(.72f, .43f, .56f) : new Color(.22f, .42f, .16f);
                result.SetColor("_BaseColor", tint);
            }
            if (family == "Brute" && !source.mainTexture)
            {
                string name = source.name.ToLowerInvariant();
                var tint = name.Contains("body") ? new Color(.56f, .38f, .25f) : name.Contains("hair") || name.Contains("moustache") ? new Color(.16f, .10f, .055f) : name.Contains("bottom") ? new Color(.23f, .27f, .30f) : name.Contains("shoes") ? new Color(.18f, .12f, .08f) : name.Contains("eye") ? new Color(.8f, .85f, .75f) : new Color(.32f, .33f, .29f);
                result.SetColor("_BaseColor", tint);
            }
            if (family == "Brute")
            {
                string name = source.name.ToLowerInvariant();
                string file = name.Contains("body") || name.Contains("eye") ? "MaleBruteA_Body_diffuse1.jpg" : name.Contains("bottom") ? "MaleBruteA_Bottom_diffuse1.jpg" : name.Contains("moustache") ? "MaleBruteA_Moustache_diffuse.png" : name.Contains("hair") ? "MaleBruteA_Hair_diffuse.png" : name.Contains("shoes") ? "MaleBruteA_Shoes_diffuse1.jpg" : name.Contains("blinn") ? "axe_DIF2.png" : null;
                if (file != null)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Brute/Textures/" + file);
                    if (texture) { result.SetTexture("_BaseMap", texture); result.SetColor("_BaseColor", Color.white); }
                }
                if (name.Contains("hair") || name.Contains("moustache"))
                { result.SetFloat("_AlphaClip", 1); result.SetFloat("_Cutoff", .4f); result.EnableKeyword("_ALPHATEST_ON"); }
            }
            if (family == "Village")
            {
                string lower = source.name.ToLowerInvariant();
                string stem = lower.Contains("wood") ? "WoodTrim" : lower.Contains("roof") || lower.Contains("tile") ? "RoundTiles" : lower.Contains("uneven") ? "UnevenBrick" : lower.Contains("plaster") ? "Plaster" : lower.Contains("rock") ? "RockTrim" : lower.Contains("brick") ? "Brick" : null;
                if (stem != null)
                {
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Village/Textures/T_" + stem + "_BaseColor.png");
                    if (texture) { result.SetTexture("_BaseMap", texture); result.SetColor("_BaseColor", Color.white); }
                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "Art/Village/Textures/Normals Godot-Unity/T_" + stem + "_Normal.png");
                    if (normal) { result.SetTexture("_BumpMap", normal); result.EnableKeyword("_NORMALMAP"); }
                }
            }
            result = PersistMaterial(result, Root + "Materials/" + key + ".mat"); Materials[key] = result; return result;
        }

        static Material Solid(string name, Color color, bool glow = false)
        {
            if (Materials.TryGetValue(name, out var known)) return known;
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .1f);
            if (glow) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * 2); }
            material = PersistMaterial(material, Root + "Materials/" + name + ".mat"); Materials[name] = material; return material;
        }
        static Material PersistMaterial(Material value, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!existing) { AssetDatabase.CreateAsset(value, path); return value; }
            EditorUtility.CopySerialized(value, existing);
            UnityEngine.Object.DestroyImmediate(value);
            EditorUtility.SetDirty(existing); return existing;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); cube.name = name; cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position; cube.transform.localScale = size; cube.GetComponent<Renderer>().sharedMaterial = material; return cube;
        }
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds); return bounds;
        }
        static void FitHeight(GameObject go, float height)
        { float actual = BoundsOf(go).size.y; if (actual > .001f) go.transform.localScale *= height / actual; }
        static void Ground(GameObject go, float y) { var p = go.transform.position; p.y += y - BoundsOf(go).min.y; go.transform.position = p; }
        static void FitBox(GameObject go, Vector3 size)
        {
            var bounds = BoundsOf(go); var s = go.transform.localScale;
            go.transform.localScale = Vector3.Scale(s, new Vector3(size.x / Mathf.Max(.001f, bounds.size.x), size.y / Mathf.Max(.001f, bounds.size.y), size.z / Mathf.Max(.001f, bounds.size.z)));
            bounds = BoundsOf(go);
            var offset = go.transform.position + Vector3.up * bounds.extents.y - bounds.center;
            foreach (Transform child in go.transform) child.position += offset;
        }
        static void Text(string name, string value, Vector3 position, float size, Color color, Transform parent, Quaternion rotation)
        {
            var go = new GameObject(name); go.transform.SetParent(parent); go.transform.position = position; go.transform.rotation = rotation;
            var text = go.AddComponent<TextMesh>(); text.text = value; text.characterSize = size; text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter; text.color = color;
        }
        static void SetLayer(Transform transform, int layer) { transform.gameObject.layer = layer; foreach (Transform child in transform) SetLayer(child, layer); }
    }
}
