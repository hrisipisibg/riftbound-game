using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class AdventureExpansion
    {
        const string ScenePath = "Assets/RPG/Scenes/Outpost.unity";
        const string Root = "Assets/RPG/";
        static double nextPoll;
        static AdventureExpansion() { EditorApplication.update += Poll; }
        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            nextPoll = EditorApplication.timeSinceStartup + 2;
            const string request = "AdventureWork.request";
            if (!File.Exists(request)) return;
            string command = File.ReadAllText(request).Trim();
            if (command.Length == 0) return;
            File.WriteAllText(request, "");
            if (command == "setup") Setup();
            else if (command == "validate") Validate();
            else if (command == "build") { Validate(); PrototypeBuild.BuildWindows(); }
        }
        [MenuItem("Riftbound/Expand adventure")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) { Debug.LogError("Open Outpost before expanding the adventure."); return; }
            try
            {
                bool dirty = scene.isDirty;
                Directory.CreateDirectory(Root + "Scenes/Backups");
                string backup = Root + "Scenes/Backups/Outpost-before-expansion.unity";
                if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (!player) throw new InvalidOperationException("Player is missing.");
                var raiders = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None).Where(e => !e.campGuard && !e.isBoss).OrderBy(e => e.transform.position.z).ToArray();
                for (int i = 0; i < raiders.Length; i++)
                { Undo.RecordObject(raiders[i], "Identify raiders"); raiders[i].enemyId = "road-raider-" + (i + 1); EditorUtility.SetDirty(raiders[i]); }
                if (!GameObject.Find("05 - Ashwood Adventure"))
                {
                    if (raiders.Length == 0) throw new InvalidOperationException("A raider model is needed as the enemy template.");
                    var root = new GameObject("05 - Ashwood Adventure").transform;
                    Undo.RegisterCreatedObjectUndo(root.gameObject, "Expand Ashwood adventure");
                    var camp = new GameObject("Ashwood supply camp").transform; camp.SetParent(root);
                    var ruins = new GameObject("Captain's ruined watchtower").transform; ruins.SetParent(root);
                    ClearBrush(new Vector3(25, 0, 64), 14);
                    ClearBrush(new Vector3(35, 0, 90), 14);
                    Path("Camp approach", new Vector3(1, 0, 51), new Vector3(25, 0, 61), root);
                    Path("Watchtower trail", new Vector3(27, 0, 69), new Vector3(35, 0, 87), root);
                    var wood = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Dark timber.mat");
                    var stone = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Foundation.mat");
                    var red = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Faction crimson.mat");
                    for (int side = 0; side < 2; side++)
                    for (int i = 0; i < 4; i++)
                        Model("Village/FBX/Prop_WoodenFence_Single.fbx", new Vector3(16 + i * 5, -.15f, side == 0 ? 55 : 74), 0, 1.6f, camp, true);
                    for (int i = 0; i < 3; i++)
                    {
                        var crate = Model("Village/FBX/Prop_Crate.fbx", new Vector3(21 + i * 5, -.15f, 67 + (i % 2) * 3), i * 25, 1.1f, camp, true);
                        crate.name = "Stolen supplies " + (i + 1); crate.AddComponent<SupplyCrate>().supplyId = "camp-supply-" + (i + 1);
                    }
                    Model("Village/FBX/Prop_Wagon.fbx", new Vector3(33, -.15f, 60), 25, 2, camp, true);
                    foreach (float x in new[] { 17f, 36f })
                    {
                        Cube("Camp banner pole", new Vector3(x, 1.8f, 65), new Vector3(.2f, 4, .2f), wood, camp);
                        Cube("Raider banner", new Vector3(x, 2.5f, 65), new Vector3(1.4f, 1.6f, .12f), red, camp);
                    }
                    // Open-front shelters leave a clear, walkable camp interior.
                    for (int i = 0; i < 2; i++)
                    {
                        float x = i == 0 ? 18 : 33;
                        Cube("Shelter back wall", new Vector3(x, 1, 73), new Vector3(4, 2.3f, .3f), wood, camp);
                        var roof = Model("Village/FBX/Roof_RoundTiles_6x8.fbx", new Vector3(x, 2, 72), 0, 1.5f, camp, false);
                        foreach (float offset in new[] { -1.7f, 1.7f }) Cube("Shelter post", new Vector3(x + offset, 1, 70), new Vector3(.18f, 2.3f, .18f), wood, camp);
                    }
                    Fire(new Vector3(25, 0, 61), camp, stone, wood);
                    Enemy(raiders[0], player, new Vector3(20, .05f, 62), "camp-guard-1", false, camp);
                    Enemy(raiders[0], player, new Vector3(31, .05f, 69), "camp-guard-2", false, camp);
                    var arena = Cube("Watchtower courtyard", new Vector3(35, -.13f, 90), new Vector3(20, .1f, 20), stone, ruins);
                    for (int i = 0; i < 9; i++)
                    {
                        float angle = i * 40 * Mathf.Deg2Rad;
                        var position = new Vector3(35 + Mathf.Sin(angle) * 11, -.15f, 90 + Mathf.Cos(angle) * 11);
                        if (position.z < 83) continue;
                        Model("Village/FBX/Wall_Arch.fbx", position, -i * 40, i % 2 == 0 ? 4.3f : 3.4f, ruins, true);
                    }
                    foreach (float x in new[] { 29f, 41f }) Fire(new Vector3(x, .02f, 94), ruins, stone, wood);
                    Enemy(raiders[0], player, new Vector3(35, .05f, 90), "ashwood-captain", true, ruins);
                    Sign("ASHWOOD CAMP", new Vector3(10, 0, 55), camp, wood);
                    Sign("THE RUINED WATCHTOWER", new Vector3(33, 0, 80), ruins, wood);
                    foreach (var p in new[] { new Vector3(13, 0, 72), new Vector3(44, 0, 73), new Vector3(46, 0, 88), new Vector3(21, 0, 88) })
                        Model("Nature/DeadTree_1.fbx", p + Vector3.down * .15f, 55, 6, root, true);
                    Boundaries(root, stone);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!dirty) EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                File.WriteAllText("AdventureExpansionResult.txt", "PASS: Ashwood camp, 3 supply crates, 2 guards, captain arena, 3-quest campaign, XP, potions and saves.\nScene saved: " + !dirty + ". Pre-expansion scene backup retained.\n");
                Validate();
            }
            catch (Exception exception) { File.WriteAllText("AdventureExpansionResult.txt", "FAILED: " + exception); Debug.LogException(exception); }
        }
        static void Enemy(EnemyController source, PlayerController player, Vector3 position, string id, bool boss, Transform parent)
        {
            var go = UnityEngine.Object.Instantiate(source.gameObject, parent); go.name = boss ? "Ashwood Captain" : "Ashwood camp guard";
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0, 180, 0));
            var enemy = go.GetComponent<EnemyController>(); enemy.enemyId = id; enemy.campGuard = !boss; enemy.isBoss = boss; enemy.player = player;
            enemy.attackDamage = boss ? 16 : 12; enemy.movementMultiplier = boss ? .65f : .8f;
            var stats = go.GetComponent<Combatant>(); stats.maxHealth = boss ? 260 : 85; stats.displayName = go.name;
            if (boss)
            {
                go.transform.localScale *= 1.2f;
                foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(original =>
                    {
                        string path = Root + "Materials/Captain " + original.name + ".mat";
                        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (!mat) { mat = new Material(original); mat.SetColor("_BaseColor", new Color(.85f, .74f, .5f)); AssetDatabase.CreateAsset(mat, path); }
                        return mat;
                    }).ToArray();
            }
        }
        static void ClearBrush(Vector3 center, float radius)
        {
            var forest = GameObject.Find("03 - Forest Road"); if (!forest) return;
            foreach (Transform item in forest.transform)
            {
                Vector3 delta = item.position - center; delta.y = 0;
                if (delta.magnitude >= radius) continue;
                Undo.RecordObject(item.gameObject, "Clear camp space"); item.gameObject.SetActive(false);
            }
        }
        internal static GameObject Model(string relative, Vector3 position, float yaw, float height, Transform parent, bool collision)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/" + relative);
            if (!asset) throw new InvalidOperationException("Missing model: " + relative);
            var root = new GameObject(asset.name); root.transform.SetParent(parent, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset); model.transform.SetParent(root.transform, false);
            string family = relative.Split('/')[0];
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(original => original ? AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + family + "_" + original.name.Replace('/', '_') + ".mat") ?? AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + (family == "Nature" ? "Dark timber" : "Foundation") + ".mat") : null).ToArray();
            Bounds bounds = Bounds(root);
            root.transform.localScale *= height / Mathf.Max(.01f, bounds.size.y);
            bounds = Bounds(root); model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            if (collision)
            {
                bounds = Bounds(root); var box = root.AddComponent<BoxCollider>();
                box.center = root.transform.InverseTransformPoint(bounds.center); box.size = bounds.size / root.transform.localScale.x;
            }
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); return root;
        }
        static Bounds Bounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds); return bounds;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false);
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static void Path(string name, Vector3 from, Vector3 to, Transform parent)
        {
            var original = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/Ground Forest road.mat");
            string path = Root + "Materials/" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(original); mat.SetTextureScale("_BaseMap", new Vector2(1.75f, Vector3.Distance(from, to) / 2)); AssetDatabase.CreateAsset(mat, path); }
            var go = Cube(name, (from + to) * .5f + Vector3.down * .12f, new Vector3(3.5f, .1f, Vector3.Distance(from, to)), mat, parent);
            go.transform.rotation = Quaternion.LookRotation(to - from);
        }
        static void Fire(Vector3 position, Transform parent, Material stone, Material wood)
        {
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2 / 7;
                Cube("Hearth stone", position + new Vector3(Mathf.Sin(angle) * .65f, .12f, Mathf.Cos(angle) * .65f), new Vector3(.35f, .3f, .35f), stone, parent);
            }
            Cube("Hearth logs", position + Vector3.up * .1f, new Vector3(.9f, .2f, .35f), wood, parent);
            var light = new GameObject("Warm campfire glow").AddComponent<Light>(); light.transform.SetParent(parent); light.transform.position = position + Vector3.up * .6f;
            light.type = LightType.Point; light.range = 7; light.intensity = 3; light.color = new Color(1, .43f, .12f);
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere); flame.name = "Fire embers"; flame.transform.SetParent(parent); flame.transform.position = position + Vector3.up * .3f; flame.transform.localScale = new Vector3(.4f, .5f, .4f);
            UnityEngine.Object.DestroyImmediate(flame.GetComponent<Collider>());
            string path = Root + "Materials/Camp embers.mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); material.SetColor("_BaseColor", new Color(1, .3f, .025f)); AssetDatabase.CreateAsset(material, path); }
            flame.GetComponent<Renderer>().sharedMaterial = material;
        }
        static void Sign(string value, Vector3 position, Transform parent, Material wood)
        {
            Cube("Trail sign post", position + Vector3.up, new Vector3(.15f, 2, .15f), wood, parent);
            Cube("Trail sign board", position + Vector3.up * 1.9f, new Vector3(3.8f, .6f, .16f), wood, parent);
            var go = new GameObject(value); go.transform.SetParent(parent); go.transform.position = position + new Vector3(0, 1.9f, -.1f);
            var text = go.AddComponent<TextMesh>(); text.text = value; text.fontSize = 48; text.characterSize = .055f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(1, .85f, .55f);
        }
        static void Boundaries(Transform parent, Material stone)
        {
            // Invisible edge colliders prevent walking off the prototype's finite ground.
            foreach (var sizeAndPosition in new[] { (new Vector3(1, 10, 182), new Vector3(-79, 4, 15)), (new Vector3(1, 10, 182), new Vector3(79, 4, 15)), (new Vector3(158, 10, 1), new Vector3(0, 4, -74)), (new Vector3(158, 10, 1), new Vector3(0, 4, 104)) })
                Cube("World boundary", sizeAndPosition.Item2, sizeAndPosition.Item1, stone, parent).GetComponent<Renderer>().enabled = false;
        }
        [MenuItem("Riftbound/Validate adventure content")]
        public static void Validate()
        {
            var report = new StringBuilder();
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(e => !e.trainingDummy).ToArray();
            var supplies = UnityEngine.Object.FindObjectsByType<SupplyCrate>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            void Check(bool condition, string message) { report.AppendLine((condition ? "PASS " : "FAIL ") + message); }
            Check(enemies.Length == 7, "4 road raiders, 2 guards and 1 boss");
            Check(enemies.Select(e => e.enemyId).Distinct().Count() == 7 && enemies.All(e => !string.IsNullOrEmpty(e.enemyId)), "Unique enemy save IDs");
            Check(supplies.Length == 3 && supplies.Select(s => s.supplyId).Distinct().Count() == 3, "Three uniquely identified supply crates");
            Check(enemies.All(e => e.player && e.animator && e.animator.runtimeAnimatorController && e.animator.avatar && e.animator.avatar.isValid), "Enemy player references, controllers and valid animation rigs");
            Check(enemies.Count(e => e.isBoss && e.GetComponent<Combatant>().maxHealth == 260) == 1, "Captain configured with 260 HP");
            foreach (string name in new[] { "Village square", "Forest road", "West lane", "East lane", "Meadow ground" })
            {
                var go = GameObject.Find(name); var renderer = go ? go.GetComponent<Renderer>() : null;
                Check(renderer && renderer.sharedMaterial && renderer.sharedMaterial.GetTexture("_BaseMap"), name + " has a ground texture");
            }
            Check(UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled).All(r => r.sharedMaterials.All(m => m && m.shader && m.shader.isSupported)), "Scene renderers have supported materials");
            report.AppendLine("Static scene validation only. Gameplay testing is left to the user.");
            File.WriteAllText("AdventureValidation.txt", report.ToString()); Debug.Log(report.ToString());
        }
    }
}
