using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Riftbound.Editor
{
    public static class DemoPassSetup
    {
        const string Root = "Assets/RPG/";
        public static void BatchAnimationAudit()
        {
            GameObject copy = null;
            try
            {
                EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity");
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                copy = UnityEngine.Object.Instantiate(player.animator.gameObject); copy.transform.position = new Vector3(0, -400, 0);
                copy.transform.localScale = player.animator.transform.lossyScale;
                var animator = copy.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); animator.Update(0);
                if (animator.isHuman || animator.applyRootMotion) throw new InvalidOperationException("Expected native rig and code-driven root movement.");
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                var bones = copy.GetComponentsInChildren<Transform>();
                var foot = bones.First(b => b.name.EndsWith(":LeftFoot")); var toe = bones.First(b => b.name.EndsWith(":LeftToeBase"));
                var report = new StringBuilder("Native rig audit, edit-mode pose sampling only.\n");
                foreach (string name in new[] { "Idle", "Walk", "WalkBack", "Run", "RunBack", "Attack", "Cleave", "Jump", "Hit", "Death", "Block" })
                {
                    var clip = (AnimationClip)controller.layers[0].stateMachine.states.First(s => s.state.name == name).state.motion;
                    bool expectedLoop = name == "Idle" || name == "Walk" || name == "WalkBack" || name == "Run" || name == "RunBack" || name == "Block" || name == "Jump";
                    if (!clip || clip.isHumanMotion || clip.isLooping != expectedLoop) throw new InvalidOperationException("Invalid native clip/loop: " + name);
                    Vector3 anchor = copy.transform.position; float drift = 0, low = 999, high = -999;
                    var points = new Vector3[61]; var headings = new float[61];
                    for (int i = 0; i <= 60; i++)
                    {
                        animator.Play(name, 0, Mathf.Lerp(.001f, .999f, i / 60f)); animator.Update(0);
                        points[i] = toe.position; low = Mathf.Min(low, toe.position.y); high = Mathf.Max(high, toe.position.y);
                        var direction = Vector3.ProjectOnPlane(toe.position - foot.position, Vector3.up);
                        headings[i] = direction.sqrMagnitude > .00001f ? Vector3.Dot(direction.normalized, copy.transform.forward) : 1;
                        drift = Mathf.Max(drift, Vector3.Distance(anchor, copy.transform.position));
                    }
                    if (drift > .005f) throw new InvalidOperationException("Animation moved code-driven root: " + name);
                    float minimumHeading = 1; int minimumFrame = 0;
                    for (int i = 0; i <= 60; i++) if (points[i].y <= low + (high - low) * .2f + .005f && headings[i] < minimumHeading) { minimumHeading = headings[i]; minimumFrame = i; }
                    report.AppendLine($"PASS {name}: native, loop={clip.isLooping}, root drift={drift:F4}m; left toe planted heading dot={minimumHeading:F3} at {minimumFrame / 60f:F3}; seam distance={Vector3.Distance(points[0], points[60]):F3}m; pose range={high - low:F3}m.");
                    if (name == "Run" || name == "WalkBack")
                    {
                        animator.Play(name, 0, .35f); animator.Update(0); CharacterArtSetup.RenderPose(copy, "Native-Audit-" + name + ".png");
                    }
                    if (name == "Run" || name == "Walk") { animator.Play(name, 0, minimumFrame / 60f); animator.Update(0); CharacterArtSetup.RenderPose(copy, "Native-Audit-" + name + "-Plant.png"); }
                }
                File.WriteAllText("AnimationAuditResult.txt", report + "Toe headings/seams are diagnostics, not a gameplay acceptance test.\n");
                UnityEngine.Object.DestroyImmediate(copy); copy = null; EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                if (copy) UnityEngine.Object.DestroyImmediate(copy);
                File.WriteAllText("AnimationAuditResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1);
            }
        }
        public static void BatchPreview()
        {
            try
            {
                EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity");
                Setup();
                foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (enemy.animator) { enemy.animator.Rebind(); enemy.animator.Play("Idle", 0, 0); enemy.animator.Update(0); }
                RenderWorld(new Vector3(-14, 18, 45), new Vector3(-34, 0, 68), "Western-Crypt-Overview.png");
                RenderWorld(new Vector3(-24, 4, 53), new Vector3(-34, 1.5f, 70), "Western-Crypt-Entrance.png");
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                var copy = UnityEngine.Object.Instantiate(player.animator.gameObject);
                copy.transform.position = new Vector3(0, -400, 0); copy.transform.localScale = player.animator.transform.lossyScale;
                var animator = copy.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); animator.Play("Block", 0, .4f); animator.Update(0);
                CharacterArtSetup.RenderPose(copy, "Native-Guard-Preview.png"); UnityEngine.Object.DestroyImmediate(copy);
                File.WriteAllText("DemoPreviewResult.txt", "Rendered crypt overview, entrance and native guard pose in edit mode.\n");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("DemoPreviewResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        internal static void RenderWorld(Vector3 position, Vector3 target, string filename)
        {
            var camera = new GameObject("Crypt preview camera").AddComponent<Camera>();
            var texture = new RenderTexture(1280, 720, 24); var previous = RenderTexture.active;
            bool previousAsync = ShaderUtil.allowAsyncCompilation;
            try
            {
                camera.transform.position = position; camera.transform.LookAt(target); camera.fieldOfView = 58; camera.farClipPlane = 250;
                camera.clearFlags = CameraClearFlags.Skybox; camera.cullingMask = ~(1 << 31); camera.targetTexture = texture;
                camera.allowHDR = true; camera.allowMSAA = true; camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                foreach (var label in UnityEngine.Object.FindObjectsByType<WorldLabel>(FindObjectsSortMode.None)) label.transform.rotation = camera.transform.rotation;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render(); camera.Render(); RenderTexture.active = texture;
                var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply(); File.WriteAllBytes(filename, image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            finally { ShaderUtil.allowAsyncCompilation = previousAsync; RenderTexture.active = previous; texture.Release(); UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(camera.gameObject); }
        }
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity");
                Setup(); Validate();
                PrototypeBuild.BuildWindows();
                if (!File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")) throw new Exception("Windows build failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("DemoPassResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [MenuItem("Riftbound/Add Western Crypt and guard")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player) throw new InvalidOperationException("Open Outpost-Updated first.");
            Directory.CreateDirectory(Root + "Scenes/Backups");
            string backup = Root + "Scenes/Backups/Outpost-before-western-crypt.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            string blockPath = Root + "Art/BruteNative/standing block idle.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(blockPath);
            PrototypeImport.ConfigureAnimations(importer, blockPath); importer.SaveAndReimport();
            var blockClip = AssetDatabase.LoadAllAssetsAtPath(blockPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            var controller = (AnimatorController)player.animator.runtimeAnimatorController;
            var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Block").state;
            if (!state) state = controller.layers[0].stateMachine.AddState("Block");
            state.motion = blockClip; state.speed = 1; state.iKOnFeet = false; EditorUtility.SetDirty(controller);
            if (!GameObject.Find("07 - Western Crypt")) CreateCrypt(player);
            var crypt = GameObject.Find("07 - Western Crypt").transform;
            foreach (var label in crypt.GetComponentsInChildren<TextMesh>())
                if (!label.GetComponent<WorldLabel>()) label.gameObject.AddComponent<WorldLabel>();
            var forest = GameObject.Find("03 - Forest Road");
            if (forest) foreach (Transform brush in forest.transform)
                foreach (var rune in crypt.GetComponentsInChildren<Runestone>())
                    if (Vector2.Distance(new Vector2(brush.position.x, brush.position.z), new Vector2(rune.transform.position.x, rune.transform.position.z)) < 2.2f) brush.gameObject.SetActive(false);
            if (!crypt.Find("Ruined stonework")) Stonework(crypt);
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }
        static void CreateCrypt(PlayerController player)
        {
            var root = new GameObject("07 - Western Crypt").transform;
            var forest = GameObject.Find("03 - Forest Road");
            if (forest) foreach (Transform tree in forest.transform)
            {
                Vector3 delta = tree.position - new Vector3(-34, 0, 68); delta.y = 0;
                if (delta.magnitude < 14 || DistanceToTrail(tree.position) < 3) tree.gameObject.SetActive(false);
            }
            var stone = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Foundation.mat");
            var floor = Material("Crypt paving", "Ground Village square", Color.gray);
            floor.SetTextureScale("_BaseMap", new Vector2(9, 9));
            Cube("Ruined courtyard", new Vector3(-34, -.14f, 68), new Vector3(20, .1f, 20), floor, root);
            Vector3 from = new Vector3(0, -.13f, 50), to = new Vector3(-32, -.13f, 61);
            var trailMaterial = Material("Western trail", "Ground Forest road", Color.gray);
            trailMaterial.SetTextureScale("_BaseMap", new Vector2(1.7f, Vector3.Distance(from, to) / 2));
            var trail = Cube("Western crypt trail", (from + to) / 2, new Vector3(3.5f, .08f, Vector3.Distance(from, to)), trailMaterial, root);
            trail.transform.rotation = Quaternion.LookRotation(to - from);
            foreach (var wall in new[] { new Vector3(-43, 0, 65), new Vector3(-43, 0, 73), new Vector3(-25, 0, 68), new Vector3(-39, 0, 77), new Vector3(-31, 0, 77) })
                AdventureExpansion.Model("Village/FBX/Wall_Arch.fbx", wall + Vector3.down * .1f, wall.x < -41 || wall.x > -27 ? 90 : 0, 3.5f, root, true);
            var crystalMaterial = Material("Runestone glow", null, new Color(.2f, .65f, .9f));
            crystalMaterial.EnableKeyword("_EMISSION"); crystalMaterial.SetColor("_EmissionColor", new Color(.15f, .7f, 1));
            int index = 0;
            foreach (Vector3 point in new[] { new Vector3(-8, -.1f, 35), new Vector3(-20, -.1f, 52), new Vector3(-37, -.1f, 59) })
            {
                var runeRoot = new GameObject("Western Runestone " + ++index); runeRoot.transform.SetParent(root); runeRoot.transform.position = point;
                Cube("Carved stone", point + Vector3.up * .45f, new Vector3(1.1f, 1, .8f), stone, runeRoot.transform);
                var crystal = Cube("Runic crystal", point + Vector3.up * 1.4f, new Vector3(.5f, .7f, .5f), crystalMaterial, runeRoot.transform);
                crystal.transform.rotation = Quaternion.Euler(0, 45, 15);
                var rune = runeRoot.AddComponent<Runestone>(); rune.runeId = "west-rune-" + index; rune.crystal = crystal.GetComponent<Renderer>();
                rune.label = Label("RUNESTONE", point + Vector3.up * 2.35f, root, new Color(.5f, .9f, 1));
            }
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var warrior = enemies.First(e => e.enemyId == "camp-guard-1");
            var mage = enemies.First(e => e.ranged && !e.trainingDummy);
            Enemy(warrior, "crypt-guard-1", "Crypt Sentinel", new Vector3(-27, 0, 60), 95, 13, root, player);
            Enemy(mage, "crypt-guard-2", "Crypt Hexer", new Vector3(-42, 0, 68), 80, 12, root, player);
            Enemy(warrior, "crypt-guard-3", "Crypt Sentinel", new Vector3(-27, 0, 74), 95, 13, root, player);
            var warden = Enemy(warrior, "crypt-warden", "The Crypt Warden", new Vector3(-35, 0, 70), 300, 18, root, player);
            warden.heavyAttacker = true; warden.requiresRunes = true; warden.movementMultiplier = .62f;
            warden.transform.localScale *= 1.2f; warden.GetComponent<Combatant>().flatDamageReduction = 4;
            // The optional Warden must never count as the captain from Abe's main quest.
            warden.isBoss = false;
            PolishSetup.Chest("crypt-reliquary", new Vector3(-35, -.1f, 75), 60, 110, root);
            UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).First(c => c.chestId == "crypt-reliquary").requiresWardenDefeated = true;
            Label("WESTERN CRYPT", new Vector3(-34, 4, 62), root, new Color(.55f, .85f, 1));
            Label("Three runestones awaken the Warden", new Vector3(-34, 3.3f, 62), root, Color.white).characterSize = .065f;
        }
        static float DistanceToTrail(Vector3 point)
        {
            Vector3 a = new Vector3(0, 0, 50), delta = new Vector3(-32, 0, 11); point.y = 0;
            return Vector3.Distance(point, a + delta * Mathf.Clamp01(Vector3.Dot(point - a, delta) / delta.sqrMagnitude));
        }
        static void Stonework(Transform parent)
        {
            var root = new GameObject("Ruined stonework").transform; root.SetParent(parent);
            var stone = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Foundation.mat");
            int index = 0;
            foreach (var point in new[] { new Vector3(-43, 0, 59), new Vector3(-40, 0, 59), new Vector3(-29, 0, 59), new Vector3(-25, 0, 59), new Vector3(-43, 0, 77), new Vector3(-35, 0, 77), new Vector3(-25, 0, 77) })
            {
                float height = index++ % 3 == 0 ? 2.9f : 1.8f;
                Cube("Stone pillar base", point + Vector3.up * .1f, new Vector3(1.3f, .35f, 1.3f), stone, root);
                Cube("Broken crypt pillar", point + Vector3.up * (height / 2), new Vector3(.85f, height, .85f), stone, root);
                Cube("Pillar cap", point + Vector3.up * height, new Vector3(1.05f, .25f, 1.05f), stone, root);
            }
            Cube("North ruined wall", new Vector3(-34, .4f, 78), new Vector3(18, 1, .65f), stone, root);
            Cube("West ruined wall", new Vector3(-44, .4f, 68), new Vector3(.65f, 1, 18), stone, root);
            Cube("East ruined wall", new Vector3(-24, .4f, 68), new Vector3(.65f, 1, 18), stone, root);
            for (int i = 0; i < 6; i++)
            {
                var rubble = Cube("Fallen masonry", new Vector3(-42 + i * 2.7f, .15f, 76), new Vector3(.8f, .5f, .6f), stone, root);
                rubble.transform.rotation = Quaternion.Euler(8 * (i % 2), i * 31, 12);
            }
        }
        static EnemyController Enemy(EnemyController template, string id, string name, Vector3 position, float health, float damage, Transform parent, PlayerController player)
        {
            var enemy = UnityEngine.Object.Instantiate(template, parent); enemy.name = name; enemy.transform.position = position;
            enemy.transform.rotation = Quaternion.Euler(0, 180, 0); enemy.enemyId = id; enemy.player = player;
            enemy.campGuard = true; enemy.isBoss = false; enemy.trainingDummy = false; enemy.requiresRunes = false; enemy.heavyAttacker = false;
            enemy.attackDamage = damage; enemy.movementMultiplier = .8f;
            var stats = enemy.GetComponent<Combatant>(); stats.maxHealth = health; stats.flatDamageReduction = 1; stats.displayName = name;
            return enemy;
        }
        static Material Material(string name, string source, Color color)
        {
            string path = Root + "Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            material = source != null ? new Material(AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + source + ".mat")) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (source == null) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path); return material;
        }
        static GameObject Cube(string name, Vector3 point, Vector3 size, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent); go.transform.position = point;
            go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static TextMesh Label(string text, Vector3 point, Transform parent, Color color)
        {
            var go = new GameObject(text); go.transform.SetParent(parent); go.transform.position = point; go.transform.rotation = Quaternion.Euler(0, 180, 0);
            var label = go.AddComponent<TextMesh>(); label.text = text; label.characterSize = .1f; label.fontSize = 64; label.anchor = TextAnchor.MiddleCenter; label.color = color;
            go.AddComponent<WorldLabel>(); return label;
        }
        static void Validate()
        {
            var report = new StringBuilder();
            void Check(bool valid, string message) { report.AppendLine((valid ? "PASS " : "FAIL ") + message); if (!valid) throw new InvalidOperationException(message); }
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Check(enemies.Select(e => e.enemyId).Distinct().Count() == enemies.Length, "Every enemy has a unique persistent ID");
            var warden = enemies.First(e => e.enemyId == "crypt-warden");
            Check(warden.requiresRunes && warden.heavyAttacker && warden.campGuard && !warden.isBoss, "Warden unlock and main quest isolation");
            var runes = UnityEngine.Object.FindObjectsByType<Runestone>(FindObjectsSortMode.None);
            Check(runes.Length == 3 && runes.Select(r => r.runeId).Distinct().Count() == 3 && runes.All(r => r.crystal && r.label), "Three unique interactive runestones");
            var chests = UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None);
            Check(chests.Length >= 4 && chests.Select(c => c.chestId).Distinct().Count() == chests.Length && chests.First(c => c.chestId == "crypt-reliquary").requiresWardenDefeated, "Persistent caches have unique IDs; reliquary gated by Warden");
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var controller = (AnimatorController)player.animator.runtimeAnimatorController;
            var block = (AnimationClip)controller.layers[0].stateMachine.states.First(s => s.state.name == "Block").state.motion;
            Check(block && block.isLooping && !block.isHumanMotion && block.length > .2f, "Looping guard uses native rig");
            var actor = new GameObject("Demo utility checks") { hideFlags = HideFlags.HideAndDontSave };
            string previousSave = PlayerController.SavePathOverride;
            PlayerController.SavePathOverride = Path.GetFullPath("Library/DemoChecks/adventure.json");
            try
            {
                var test = actor.AddComponent<PlayerController>();
                typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(test, null);
                test.Stats.Restore(); test.Stats.ConfigureVitals(100, 4); test.Stats.incomingDamageMultiplier = .5f; test.Stats.Damage(20);
                Check(test.Stats.Health == 92, "Guard halves damage after armor");
                test.Stats.incomingDamageMultiplier = 1; test.Stats.Damage(20);
                Check(test.Stats.Health == 76, "Normal damage resumes after guard");
                test.RecordEnemyDefeat(warden);
                Check(!test.BossDefeated && test.Kills == 0 && test.OwnsItem("warden-axe"), "Warden rewards gear without completing captain quest");
                test.SaveAdventure(false);
                Check(File.Exists(PlayerController.SavePathOverride), "New rewards save in isolated test profile");
                var legacy = JsonUtility.FromJson<PlayerController.AdventureSave>("{\"version\":1,\"gold\":12}");
                Check(legacy.gold == 12 && legacy.runes == null, "Earlier saves remain readable");
                var data = new PlayerController.AdventureSave { runes = new[] { "west-rune-1", "west-rune-2" } };
                Check(JsonUtility.FromJson<PlayerController.AdventureSave>(JsonUtility.ToJson(data)).runes.Length == 2, "Runestone progress JSON roundtrip");
            }
            finally { PlayerController.SavePathOverride = previousSave; UnityEngine.Object.DestroyImmediate(actor); }
            Check(ItemCatalog.All.Length == 11, "Eleven gear items available");
            typeof(EquipmentSetup).GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            ValidateDiscovery(report);
            File.WriteAllText("DemoPassResult.txt", report + "Isolated edit-mode checks. Gameplay testing left to user.\n");
        }
        static void ValidateDiscovery(StringBuilder report)
        {
            string previousSave = PlayerController.SavePathOverride;
            try
            {
                // Isolate load/refresh checks from the authored scene and the user's profiles.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PlayerController.SavePathOverride = Path.GetFullPath("Library/DemoChecks/discovery.json");
                var player = new GameObject("Discovery checks").AddComponent<PlayerController>();
                typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                player.Stats.Restore();
                var warden = new GameObject("Test Warden").AddComponent<EnemyController>();
                warden.enemyId = "crypt-warden"; warden.requiresRunes = true; warden.campGuard = true; warden.player = player;
                player.RefreshAdventureWorld();
                if (warden.gameObject.activeSelf) throw new InvalidOperationException("Warden unlocked without runes.");
                var rune = new GameObject("Test rune").AddComponent<Runestone>(); rune.runeId = "west-rune-1";
                rune.Activate(player); int experience = player.Experience; rune.Activate(player);
                if (player.RunesFound != 1 || player.Experience != experience) throw new InvalidOperationException("Duplicate rune rewarded twice.");
                report.AppendLine("PASS Runestone interaction rewards progress once");
                rune.runeId = "west-rune-2"; rune.Activate(player);
                string twoRunes = File.ReadAllText(PlayerController.SavePathOverride);
                if (warden.gameObject.activeSelf) throw new InvalidOperationException("Warden unlocked before third rune.");
                rune.runeId = "west-rune-3"; rune.Activate(player);
                if (player.RunesFound != 3 || !warden.gameObject.activeSelf) throw new InvalidOperationException("Third rune did not awaken Warden.");
                report.AppendLine("PASS Warden activates only after third runestone");
                File.WriteAllText(PlayerController.SavePathOverride, twoRunes); player.LoadAdventure();
                if (player.RunesFound != 2 || player.HasRune("west-rune-3") || warden.gameObject.activeSelf) throw new InvalidOperationException("Runestone load failed.");
                report.AppendLine("PASS Loading saved runes restores progress and Warden gate");
                int before = player.Experience; rune.Activate(player); player.RecordEnemyDefeat(warden);
                int reward = player.Experience; player.RecordEnemyDefeat(warden);
                if (player.Experience != reward || reward <= before || !player.OwnsItem("warden-axe")) throw new InvalidOperationException("Warden reward duplication.");
                player.LoadAdventure();
                if (!player.HasDefeated("crypt-warden") || warden.gameObject.activeSelf || !player.OwnsItem("warden-axe")) throw new InvalidOperationException("Warden persistence failed.");
                report.AppendLine("PASS Defeated Warden stays defeated with one saved gear reward");
            }
            finally
            {
                PlayerController.SavePathOverride = previousSave;
                EditorSceneManager.OpenScene(Root + "Scenes/Outpost-Updated.unity");
            }
        }
    }
}
