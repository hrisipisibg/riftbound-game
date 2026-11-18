using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class NPCIntegrationSetup
    {
        const string NPCsDir = "Assets/RPG/Art/NPCs/";
        const string MatDir = "Assets/RPG/Materials/NPCs/";
        const string PrefabDir = "Assets/RPG/Prefabs/NPCs/";
        const string AnimDir = "Assets/RPG/Animations/NPCs/";

        [MenuItem("Riftbound/Integrate Downloaded NPCs")]
        public static void Setup()
        {
            try
            {
                Directory.CreateDirectory(MatDir);
                Directory.CreateDirectory(PrefabDir);
                Directory.CreateDirectory(AnimDir);
                AssetDatabase.Refresh();

                // 1. Configure clip looping on all NPC models
                ConfigureModelClips(NPCsDir + "KnightCharacter.fbx");
                ConfigureModelClips(NPCsDir + "Villager_Man.fbx");
                ConfigureModelClips(NPCsDir + "Villager_Woman.fbx");
                ConfigureModelClips(NPCsDir + "Wolf.fbx");
                ConfigureModelClips(NPCsDir + "Goblin.fbx");
                ConfigureModelClips(NPCsDir + "Zombie.fbx");
                AssetDatabase.Refresh();

                // 2. Create Materials
                var matKnightArmor = CreateColorLit("Mat_Knight_Armor", new Color(0.38f, 0.40f, 0.45f), 0.85f, 0.55f);
                var matKnightBoots = CreateColorLit("Mat_Knight_Boots", new Color(0.12f, 0.08f, 0.06f), 0.05f, 0.20f);
                var matKnightSkin = CreateColorLit("Mat_Knight_Skin", new Color(0.85f, 0.68f, 0.54f), 0.0f, 0.25f);

                var matVillagerMan = CreateTextureLit("Mat_Villager_Man", NPCsDir + "ClothedLightSkin.png", 0.0f, 0.2f);
                var matVillagerWoman = CreateTextureLit("Mat_Villager_Woman", NPCsDir + "LightSkin.png", 0.0f, 0.2f);
                var matWolf = CreateColorLit("Mat_Wolf", new Color(0.40f, 0.36f, 0.32f), 0.0f, 0.15f);

                var matGoblinSkin = CreateColorLit("Mat_Goblin_Skin", new Color(0.32f, 0.52f, 0.22f), 0.0f, 0.25f);
                var matGoblinShirt = CreateColorLit("Mat_Goblin_Shirt", new Color(0.48f, 0.32f, 0.18f), 0.0f, 0.15f);
                var matGoblinBoots = CreateColorLit("Mat_Goblin_Boots", new Color(0.15f, 0.10f, 0.06f), 0.0f, 0.20f);
                var matGoblinPants = CreateColorLit("Mat_Goblin_Pants", new Color(0.22f, 0.18f, 0.14f), 0.0f, 0.15f);

                // 3. Build Animation Controllers
                var knightCtrl = CreateSimpleController("KnightGuard", NPCsDir + "KnightCharacter.fbx", "HumanArmature|Idle_swordRight");
                var blacksmithCtrl = CreateSimpleController("Blacksmith", NPCsDir + "Villager_Man.fbx", "Human Armature|Working");
                var womanCtrl = CreateSimpleController("VillagerWoman", NPCsDir + "Villager_Woman.fbx", "Armature|SitIdle");
                var wolfCtrl = CreateSimpleController("Wolf", NPCsDir + "Wolf.fbx", "WolfArmature|Walking");
                var goblinCtrl = CreateSimpleController("Goblin", NPCsDir + "Goblin.fbx", "GoblinArmature|Goblin_Idle");

                // 4. Create Prefabs
                BuildGuardPrefab(knightCtrl, matKnightArmor, matKnightBoots, matKnightSkin);
                BuildBlacksmithPrefab(blacksmithCtrl, matVillagerMan);
                BuildVillagerWomanPrefab(womanCtrl, matVillagerWoman);
                BuildWolfPrefab(wolfCtrl, matWolf);
                BuildGoblinPrefab(goblinCtrl, matGoblinSkin, matGoblinShirt, matGoblinBoots, matGoblinPants);

                // 5. Populate Outpost-Updated.unity
                PopulateNPCsInScene();

                AssetDatabase.SaveAssets();
                File.WriteAllText("NPCSetupResult.txt", "PASS: Rigged NPCs and creatures imported, controllers generated, prefabs built, and scene populated.");
                Debug.Log("NPC Integration completed successfully!");
            }
            catch (Exception ex)
            {
                File.WriteAllText("NPCSetupResult.txt", "FAILED: " + ex);
                Debug.LogException(ex);
            }
        }

        static void ConfigureModelClips(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (!importer) return;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.isReadable = true;

            var defaultClips = importer.defaultClipAnimations;
            if (defaultClips != null && defaultClips.Length > 0)
            {
                foreach (var c in defaultClips)
                {
                    string n = c.name.ToLowerInvariant();
                    c.loopTime = n.Contains("idle") || n.Contains("walk") || n.Contains("run") || n.Contains("working") || n.Contains("sit");
                    c.loopPose = c.loopTime;
                }
                importer.clipAnimations = defaultClips;
            }
            importer.SaveAndReimport();
        }

        static Material CreateColorLit(string name, Color color, float metallic, float smoothness)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material CreateTextureLit(string name, string texPath, float metallic, float smoothness)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
            if (tex) mat.SetTexture("_BaseMap", tex);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static AnimatorController CreateSimpleController(string name, string fbxPath, string defaultClipName)
        {
            string path = AnimDir + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var clips = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            var clip = clips.FirstOrDefault(c => c.name.Equals(defaultClipName, StringComparison.OrdinalIgnoreCase)) ?? clips.FirstOrDefault(c => c.name.Contains("Idle")) ?? clips.FirstOrDefault();

            if (clip)
            {
                var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == "Default").state;
                if (!state) state = controller.layers[0].stateMachine.AddState("Default");
                state.motion = clip;
                controller.layers[0].stateMachine.defaultState = state;
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildGuardPrefab(AnimatorController ctrl, Material armor, Material boots, Material skin)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "KnightCharacter.fbx");
            var root = new GameObject("Prefab_NPC_Guard");
            var inst = UnityEngine.Object.Instantiate(source, root.transform);
            inst.name = "KnightVisual";

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr) smr.sharedMaterials = new Material[] { armor, boots, skin };

            var anim = inst.GetComponentInChildren<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            // Attach Helmet and Sword
            var helmetSource = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "Helmet1.fbx");
            var swordSource = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "KnightSword.fbx");
            var shoulderSource = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "ShoulderPads.fbx");

            var bones = inst.GetComponentsInChildren<Transform>();
            var head = bones.FirstOrDefault(b => b.name.ToLowerInvariant().Contains("head"));
            var rightHand = bones.FirstOrDefault(b => b.name.ToLowerInvariant().Contains("hand.r") || b.name.ToLowerInvariant().Contains("r_hand") || b.name.ToLowerInvariant().Contains("r.hand"));
            var chest = bones.FirstOrDefault(b => b.name.ToLowerInvariant().Contains("chest") || b.name.ToLowerInvariant().Contains("spine"));

            if (helmetSource && head)
            {
                var h = UnityEngine.Object.Instantiate(helmetSource, head);
                h.name = "Helmet";
                foreach (var r in h.GetComponentsInChildren<Renderer>()) r.sharedMaterial = armor;
            }
            if (swordSource && rightHand)
            {
                var s = UnityEngine.Object.Instantiate(swordSource, rightHand);
                s.name = "Sword";
                foreach (var r in s.GetComponentsInChildren<Renderer>()) r.sharedMaterial = armor;
            }
            if (shoulderSource && chest)
            {
                var sp = UnityEngine.Object.Instantiate(shoulderSource, chest);
                sp.name = "ShoulderPads";
                foreach (var r in sp.GetComponentsInChildren<Renderer>()) r.sharedMaterial = armor;
            }

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.radius = 0.35f;
            col.height = 1.8f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Prefab_NPC_Guard.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void BuildBlacksmithPrefab(AnimatorController ctrl, Material mat)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "Villager_Man.fbx");
            var root = new GameObject("Prefab_NPC_Blacksmith");
            var inst = UnityEngine.Object.Instantiate(source, root.transform);
            inst.name = "BlacksmithVisual";

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr) smr.sharedMaterial = mat;

            var anim = inst.GetComponentInChildren<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.radius = 0.35f;
            col.height = 1.8f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Prefab_NPC_Blacksmith.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void BuildVillagerWomanPrefab(AnimatorController ctrl, Material mat)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "Villager_Woman.fbx");
            var root = new GameObject("Prefab_NPC_VillagerWoman");
            var inst = UnityEngine.Object.Instantiate(source, root.transform);
            inst.name = "VillagerWomanVisual";

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr) smr.sharedMaterial = mat;

            var anim = inst.GetComponentInChildren<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.85f, 0);
            col.radius = 0.32f;
            col.height = 1.7f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Prefab_NPC_VillagerWoman.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void BuildWolfPrefab(AnimatorController ctrl, Material mat)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "Wolf.fbx");
            var root = new GameObject("Prefab_Enemy_Wolf");
            var inst = UnityEngine.Object.Instantiate(source, root.transform);
            inst.name = "WolfVisual";

            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr) smr.sharedMaterial = mat;

            var anim = inst.GetComponentInChildren<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            var col = root.AddComponent<CapsuleCollider>();
            col.direction = 2; // Z-axis for quadruped
            col.center = new Vector3(0, 0.5f, 0);
            col.radius = 0.35f;
            col.height = 1.3f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Prefab_Enemy_Wolf.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void BuildGoblinPrefab(AnimatorController ctrl, Material skin, Material shirt, Material boots, Material pants)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(NPCsDir + "Goblin.fbx");
            var root = new GameObject("Prefab_Enemy_Goblin");
            var inst = UnityEngine.Object.Instantiate(source, root.transform);
            inst.name = "GoblinVisual";

            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (smr.name.Contains("GoblinFace"))
                {
                    smr.sharedMaterials = new Material[] { shirt, boots };
                }
                else
                {
                    smr.sharedMaterials = new Material[] { skin, shirt, boots, pants };
                }
            }

            var anim = inst.GetComponentInChildren<Animator>();
            if (!anim) anim = inst.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;

            var col = root.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.65f, 0);
            col.radius = 0.32f;
            col.height = 1.3f;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Prefab_Enemy_Goblin.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void PopulateNPCsInScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity");
            var existingGroup = GameObject.Find("11 - New World NPCs");
            if (existingGroup) UnityEngine.Object.DestroyImmediate(existingGroup);

            var root = new GameObject("11 - New World NPCs");

            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_NPC_Guard.prefab");
            var smithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_NPC_Blacksmith.prefab");
            var womanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_NPC_VillagerWoman.prefab");
            var wolfPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_Enemy_Wolf.prefab");
            var goblinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_Enemy_Goblin.prefab");

            // 1. Gate Sentries (armored knights at the Emberwatch outpost gate)
            if (guardPrefab)
            {
                var guard1 = (GameObject)PrefabUtility.InstantiatePrefab(guardPrefab, root.transform);
                guard1.name = "Emberwatch Sentry Justin";
                guard1.transform.position = new Vector3(-2.8f, -0.11f, 17.6f);
                guard1.transform.rotation = Quaternion.Euler(0, 180, 0);

                var guard2 = (GameObject)PrefabUtility.InstantiatePrefab(guardPrefab, root.transform);
                guard2.name = "Emberwatch Guard Halford";
                guard2.transform.position = new Vector3(2.8f, -0.11f, 17.6f);
                guard2.transform.rotation = Quaternion.Euler(0, 180, 0);
            }

            // 2. Village Blacksmith hammering at the forge/anvil
            if (smithPrefab)
            {
                var smith = (GameObject)PrefabUtility.InstantiatePrefab(smithPrefab, root.transform);
                smith.name = "Blacksmith Torvald";
                smith.transform.position = new Vector3(8.5f, -0.11f, 7.8f);
                smith.transform.rotation = Quaternion.Euler(0, 45, 0);
            }

            // 3. Villager Maiden sitting on the tavern bench
            if (womanPrefab)
            {
                var woman = (GameObject)PrefabUtility.InstantiatePrefab(womanPrefab, root.transform);
                woman.name = "Tavern Hostess Cheryl";
                woman.transform.position = new Vector3(-7.5f, -0.11f, 6.5f);
                woman.transform.rotation = Quaternion.Euler(0, 90, 0);
            }

            // 4. Wild Timber Wolves prowling the forest road borders
            if (wolfPrefab)
            {
                var wolf1 = (GameObject)PrefabUtility.InstantiatePrefab(wolfPrefab, root.transform);
                wolf1.name = "Timber Wolf Stalker";
                wolf1.transform.position = new Vector3(15.5f, -0.15f, 52.0f);
                wolf1.transform.rotation = Quaternion.Euler(0, -60, 0);

                var wolf2 = (GameObject)PrefabUtility.InstantiatePrefab(wolfPrefab, root.transform);
                wolf2.name = "Timber Wolf";
                wolf2.transform.position = new Vector3(-16.0f, -0.15f, 68.0f);
                wolf2.transform.rotation = Quaternion.Euler(0, 40, 0);
            }

            // 5. Goblin Scout lurking by the ruined watchtower path
            if (goblinPrefab)
            {
                var gob1 = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab, root.transform);
                gob1.name = "Ashen Goblin Ambusher";
                gob1.transform.position = new Vector3(14.0f, -0.15f, 92.0f);
                gob1.transform.rotation = Quaternion.Euler(0, -45, 0);

                var gob2 = (GameObject)PrefabUtility.InstantiatePrefab(goblinPrefab, root.transform);
                gob2.name = "Ashen Goblin Sneak";
                gob2.transform.position = new Vector3(11.5f, -0.15f, 96.0f);
                gob2.transform.rotation = Quaternion.Euler(0, -110, 0);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
