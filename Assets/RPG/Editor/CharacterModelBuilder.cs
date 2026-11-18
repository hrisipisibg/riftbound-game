using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class CharacterModelBuilder
    {
        const string ArtDir = "Assets/RPG/Art/NPCs_WoW/";
        const string TexRootDir = "Assets/RPG/Art/NPCs_WoW/Textures/";
        const string SkeletonsDir = "Assets/RPG/Art/Skeletons/";
        const string AnimalsDir = "Assets/RPG/Art/Animals/";
        const string CharMatDir = "Assets/RPG/Materials/Characters/";
        const string AnimDir = "Assets/RPG/Animations/";
        const string TargetDir = "Assets/RPG/Prefabs/Characters/";
        const string ResourcesDir = "Assets/RPG/Resources/Characters/";
        const string PetResourcesDir = "Assets/RPG/Resources/Pets/";
        const string PropResourcesDir = "Assets/RPG/Resources/Props/";

        [MenuItem("Riftbound/🌟 Build Character Models (All Races & Classes)", priority = 5)]
        public static void BuildAll()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CharacterModelBuilder] Exit Play Mode before building character models.");
                return;
            }

            try
            {
                Directory.CreateDirectory(CharMatDir);
                Directory.CreateDirectory(TargetDir);
                Directory.CreateDirectory(ResourcesDir);
                Directory.CreateDirectory(PetResourcesDir);
                Directory.CreateDirectory(PropResourcesDir);
                AssetDatabase.Refresh();

                // 1. Repair shared NPC / Village / Props materials & ensure stylized textures/meshes exist
                RepairSharedMaterials();
                StylizedRacialModelSystem.EnsureUnifiedAssetsReady(false);

                // 2. Build rig-matched Player AnimatorControllers (Humanoid + Undead KayKit)
                var ctrlMelee = BuildHumanoidPlayerController("Player Humanoid Melee", "Melee");
                var ctrlCaster = BuildHumanoidPlayerController("Player Humanoid Caster", "Caster");
                var ctrlRogue = BuildHumanoidPlayerController("Player Humanoid Rogue", "Rogue");

                var ctrlUndeadWarrior = BuildUndeadPlayerController("Player Undead Warrior", "Skeleton Warrior");
                var ctrlUndeadMage = BuildUndeadPlayerController("Player Undead Mage", "Skeleton Mage");
                var ctrlUndeadRogue = BuildUndeadPlayerController("Player Undead Rogue", "Skeleton Rogue");

                // 3. Build all 24 Race × Class character prefabs with unified stylized textures & 3D racial features
                int count = 0;
                string[] races = { "Human", "Dwarf", "Elf", "Orc", "Undead", "Goblin" };
                string[] classes = { "Warrior", "Paladin", "Mage", "Rogue" };

                foreach (string race in races)
                {
                    foreach (string characterClass in classes)
                    {
                        RuntimeAnimatorController ctrl;
                        if (race == "Undead")
                        {
                            ctrl = characterClass == "Mage" ? ctrlUndeadMage
                                 : characterClass == "Rogue" ? ctrlUndeadRogue
                                 : ctrlUndeadWarrior;
                        }
                        else
                        {
                            ctrl = characterClass == "Mage" ? ctrlCaster
                                 : characterClass == "Rogue" ? ctrlRogue
                                 : ctrlMelee;
                        }

                        BuildPrefab(race, characterClass, ctrl);
                        count++;
                    }
                }

                // 4. Build normalized companion pet & prop prefabs in Resources/ and unify mount/animal fur textures
                BuildCompanionPetsAndProps();
                StylizedRacialModelSystem.UnifyAllAnimalMountAndPetMaterials();
                StylizedRacialModelSystem.MarkTextureStampComplete();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"<color=#70df80>[CharacterModelBuilder]</color> Built {count} textured, rig-matched character models + companion pets & props!");
                File.WriteAllText("CharacterModelBuildResult.txt", $"PASS: {count} character models + pets built at {DateTime.UtcNow:O}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                File.WriteAllText("CharacterModelBuildResult.txt", "FAILED: " + ex);
            }
        }

        [MenuItem("Riftbound/🛠 Repair All Models, Pets, Materials & Scene Visuals", priority = 6)]
        public static void RepairAllModelsAndSceneVisuals()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var sb = new StringBuilder();
            sb.AppendLine("=== MODEL, PET, MATERIAL & SCENE REPAIR REPORT ===");

            // 1. Rebuild all materials, controllers, 24 character prefabs, and pet/prop prefabs
            BuildAll();
            sb.AppendLine("  [OK] Rebuilt shared materials, 6 player AnimatorControllers, 24 Race/Class prefabs, and normalized Pet/Prop prefabs.");

            // 2. Repair active scene references (Well Water null material, Horde War Flame gray Lit.mat, floating NPCs)
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
            {
                Directory.CreateDirectory("Assets/RPG/Scenes/Backups");
                string backup = "Assets/RPG/Scenes/Backups/Outpost-Updated-before-visual-repair.unity";
                if (!File.Exists(backup))
                    File.Copy(scene.path, backup);

                bool sceneChanged = false;

                var wellWaterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/MedievalProps/Mat_Medieval_WellWater.mat");
                var flameMat = GetOrCreateUnlitColorMaterial("Assets/RPG/Materials/Mat_Horde_WarFlame.mat", new Color(1.0f, 0.38f, 0.06f, 1f));
                var crimsonPortalMat = GetOrCreateUnlitColorMaterial("Assets/RPG/Materials/Mat_Portal_Crimson.mat", new Color(0.92f, 0.14f, 0.24f, 1f));
                var arcanePortalMat = GetOrCreateUnlitColorMaterial("Assets/RPG/Materials/Mat_Portal_Arcane.mat", new Color(0.22f, 0.62f, 1.0f, 1f));
                var warmLanternMat = GetOrCreateUnlitColorMaterial("Assets/RPG/Materials/Mat_Lantern_WarmGold.mat", new Color(1.0f, 0.80f, 0.22f, 1f));
                var allianceBannerMat = GetOrCreateAllianceBannerMaterial();

                var stoneBrickMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_UnevenBrick.mat")
                                 ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Brick.mat");
                var rockTrimMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_RockTrim.mat")
                               ?? stoneBrickMat;
                var cryptPavingMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Crypt paving.mat") ?? rockTrimMat;
                var runeGlowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Runestone glow.mat") ?? arcanePortalMat;
                var goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Beacon gold.mat");
                var embersMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Camp embers.mat") ?? flameMat;

                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer || r.GetComponent<TextMesh>() != null)
                        continue;

                    string rName = r.name;
                    if (rName == "Well Water Surface" && wellWaterMat != null && r.sharedMaterial != wellWaterMat)
                    {
                        r.sharedMaterial = wellWaterMat;
                        EditorUtility.SetDirty(r);
                        sceneChanged = true;
                        sb.AppendLine($"  [SCENE FIX] Assigned Mat_Medieval_WellWater to '{r.transform.parent?.name}/{rName}'");
                        continue;
                    }

                    if (rName == "Royal Alliance Banner" && allianceBannerMat != null && r.sharedMaterial != allianceBannerMat)
                    {
                        r.sharedMaterial = allianceBannerMat;
                        EditorUtility.SetDirty(r);
                        sceneChanged = true;
                        sb.AppendLine($"  [SCENE FIX] Assigned Mat_Banner_AllianceBlue to '{rName}'");
                        continue;
                    }

                    bool isDefaultLit = r.sharedMaterial == null ||
                                        r.sharedMaterial.name == "Lit" ||
                                        AssetDatabase.GetAssetPath(r.sharedMaterial).StartsWith("Packages/com.unity.render-pipelines.universal");
                    if (!isDefaultLit) continue;

                    Material replacement = null;
                    string lower = rName.ToLowerInvariant();
                    if (lower.Contains("crimson portal")) replacement = crimsonPortalMat;
                    else if (lower.Contains("exit portal") || lower.Contains("portal")) replacement = arcanePortalMat;
                    else if (lower.Contains("sky lantern")) replacement = runeGlowMat;
                    else if (lower.Contains("oil lantern")) replacement = warmLanternMat;
                    else if (lower.Contains("molten roost pit")) replacement = embersMat;
                    else if (lower.Contains("flame") || lower.Contains("campfire") || lower.Contains("forge core") || lower.Contains("brazier")) replacement = flameMat;
                    else if (lower.Contains("hoard chest")) replacement = goldMat ?? rockTrimMat;
                    else if (lower.Contains("stone floor") || lower.Contains("dais") || lower.Contains("altar")) replacement = cryptPavingMat;
                    else if (lower.Contains("column") || lower.Contains("pillar") || lower.Contains("arch")) replacement = rockTrimMat;
                    else replacement = stoneBrickMat;

                    if (replacement != null && r.sharedMaterial != replacement)
                    {
                        r.sharedMaterial = replacement;
                        EditorUtility.SetDirty(r);
                        sceneChanged = true;
                        sb.AppendLine($"  [SCENE FIX] Replaced gray Lit.mat on '{r.transform.parent?.name}/{rName}' -> '{replacement.name}'");
                    }
                }

                // 2b. Sculpt northern/Horde plateaus & lift all buried world groups, NPCs, enemies, portals & nodes above terrain
                if (RepairWorldTerrainElevations(sb))
                    sceneChanged = true;

                // Ground any floating NPCs (TownNPC, FlightMaster, QuestGiver, Merchant)
                var npcTransforms = new HashSet<Transform>();
                foreach (var c in UnityEngine.Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None)) npcTransforms.Add(c.transform);
                foreach (var c in UnityEngine.Object.FindObjectsByType<FlightMaster>(FindObjectsInactive.Include, FindObjectsSortMode.None)) npcTransforms.Add(c.transform);
                foreach (var c in UnityEngine.Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Include, FindObjectsSortMode.None)) npcTransforms.Add(c.transform);
                foreach (var c in UnityEngine.Object.FindObjectsByType<Merchant>(FindObjectsInactive.Include, FindObjectsSortMode.None)) npcTransforms.Add(c.transform);

                var activeTerrain = Terrain.activeTerrain ?? UnityEngine.Object.FindFirstObjectByType<Terrain>();
                Physics.SyncTransforms();
                foreach (var t in npcTransforms)
                {
                    if (!t || !t.gameObject.activeInHierarchy) continue;
                    if (t.position.x < -200f || t.position.y < -20f || t.position.y >= 40f) continue;
                    var b = ComputeWorldBounds(t.gameObject);
                    if (b.size.y < 0.1f) continue;

                    float terrY = activeTerrain ? (activeTerrain.SampleHeight(t.position) + activeTerrain.transform.position.y) : b.min.y;
                    float rayTop = Mathf.Max(b.max.y, terrY) + 2.5f;
                    var hits = Physics.RaycastAll(new Vector3(b.center.x, rayTop, b.center.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
                    float bestY = activeTerrain ? terrY : float.NegativeInfinity;
                    foreach (var h in hits)
                    {
                        if (h.collider.transform.IsChildOf(t)) continue;
                        if (h.collider is CharacterController || h.collider is CapsuleCollider) continue;
                        if (h.point.y <= Mathf.Max(b.min.y + 0.8f, terrY + 1.5f) && h.point.y > bestY)
                            bestY = h.point.y;
                    }
                    if (!float.IsNegativeInfinity(bestY))
                    {
                        float delta = b.min.y - bestY;
                        if (Mathf.Abs(delta) > 0.25f)
                        {
                            Vector3 oldPos = t.position;
                            t.position = new Vector3(oldPos.x, oldPos.y - delta, oldPos.z);
                            if (t.TryGetComponent<FlightMaster>(out var fmComp)) fmComp.flightStationPos = t.position;
                            EditorUtility.SetDirty(t);
                            sceneChanged = true;
                            sb.AppendLine($"  [SCENE FIX] Grounded '{t.name}' from y={oldPos.y:F2} to y={t.position.y:F2} (delta was {delta:F2}m)");
                        }
                    }
                }

                if (sceneChanged)
                {
                    Physics.SyncTransforms();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    sb.AppendLine($"  [OK] Saved clean scene '{scene.path}'.");
                }
            }

            AssetDatabase.SaveAssets();
            File.WriteAllText("ModelVisualRepairResult.txt", sb.ToString());
            Debug.Log(sb.ToString());
        }

        static void RepairSharedMaterials()
        {
            // 1. Fix Ranger_Scout Body & EyeSpec textures (was mapped to Erika_Archer_Clothes_diffuse!)
            var rangerBodyTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRootDir + "Ranger_Scout/FemaleFitA_Body_diffuse.png");
            var rangerClothesTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRootDir + "Ranger_Scout/Erika_Archer_Clothes_diffuse.png");
            var rangerLashTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexRootDir + "Ranger_Scout/FemaleFitA_eyelash_diffuse.png");

            SetMatTexture("Assets/RPG/Materials/NPCs_WoW/Mat_Ranger_Scout_Body_MAT.mat", rangerBodyTex, Color.white, 0.06f, false);
            SetMatTexture("Assets/RPG/Materials/NPCs_WoW/Mat_Ranger_Scout_EyeSpec_MAT.mat", rangerBodyTex, Color.white, 0.25f, false);
            SetMatTexture("Assets/RPG/Materials/NPCs_WoW/Mat_Ranger_Scout_Akai_MAT.mat", rangerClothesTex, Color.white, 0.06f, false);
            SetMatTexture("Assets/RPG/Materials/NPCs_WoW/Mat_Ranger_Scout_eyelash_MAT.mat", rangerLashTex, Color.white, 0.04f, true);

            // 2. Fix untextured Village & FantasyProps pack materials (#CCCCCC gray -> proper diffuse textures)
            var windowTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/Village/Textures/T_WindowGradient.png");
            var bannerTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/FantasyProps/Textures/T_Trim_Cloth_BaseColor.png");
            var trimPropsTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/FantasyProps/Textures/T_Trim_Props_BaseColor.png");

            SetMatTexture("Assets/RPG/Materials/Village_MI_WindowGlass.mat", windowTex, Color.white, 0.65f, false);
            SetMatTexture("Assets/RPG/Materials/FantasyProps_MI_Banner.mat", bannerTex, Color.white, 0.12f, false);
            SetMatTexture("Assets/RPG/Materials/FantasyProps_MI_Trim_Props_Vertex.mat", trimPropsTex, Color.white, 0.15f, false);

            // 3. Create saved Mat_Medieval_WellWater.mat and repair Prefab_TownWell.prefab
            Directory.CreateDirectory("Assets/RPG/Materials/MedievalProps");
            string wellWaterMatPath = "Assets/RPG/Materials/MedievalProps/Mat_Medieval_WellWater.mat";
            var matWater = AssetDatabase.LoadAssetAtPath<Material>(wellWaterMatPath);
            if (!matWater)
            {
                matWater = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(matWater, wellWaterMatPath);
            }
            matWater.SetColor("_BaseColor", new Color(0.14f, 0.38f, 0.50f, 1f));
            matWater.SetFloat("_Smoothness", 0.90f);
            matWater.SetFloat("_Metallic", 0.08f);
            EditorUtility.SetDirty(matWater);

            string wellPrefabPath = "Assets/RPG/Prefabs/Props/Prefab_TownWell.prefab";
            if (File.Exists(wellPrefabPath))
            {
                var wellRoot = PrefabUtility.LoadPrefabContents(wellPrefabPath);
                bool modified = false;
                foreach (var r in wellRoot.GetComponentsInChildren<Renderer>(true))
                {
                    if (r.name == "Well Water Surface" && r.sharedMaterial != matWater)
                    {
                        r.sharedMaterial = matWater;
                        modified = true;
                    }
                }
                if (modified) PrefabUtility.SaveAsPrefabAsset(wellRoot, wellPrefabPath);
                PrefabUtility.UnloadPrefabContents(wellRoot);
            }

            GetOrCreateUnlitColorMaterial("Assets/RPG/Materials/Mat_Horde_WarFlame.mat", new Color(1.0f, 0.38f, 0.06f, 1f));
            GetOrCreateAllianceBannerMaterial();
            NormalizeLegacyNpcPrefabs();
        }

        static Material GetOrCreateUnlitColorMaterial(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f); // Double-sided for portal quads
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material GetOrCreateAllianceBannerMaterial()
        {
            string path = "Assets/RPG/Materials/Mat_Banner_AllianceBlue.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, path);
            }
            var clothTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/RPG/Art/FantasyProps/Textures/T_Trim_Cloth_BaseColor.png");
            if (clothTex) mat.SetTexture("_BaseMap", clothTex);
            mat.SetColor("_BaseColor", new Color(0.24f, 0.48f, 0.92f, 1f));
            mat.SetFloat("_Smoothness", 0.14f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void NormalizeLegacyNpcPrefabs()
        {
            Directory.CreateDirectory("Assets/RPG/Materials/NPCs");
            var legacyTargets = new (string path, float targetHeight)[]
            {
                ("Assets/RPG/Prefabs/NPCs/Prefab_NPC_Guard.prefab",         1.88f),
                ("Assets/RPG/Prefabs/NPCs/Prefab_NPC_Blacksmith.prefab",    1.85f),
                ("Assets/RPG/Prefabs/NPCs/Prefab_NPC_VillagerWoman.prefab", 1.72f),
                ("Assets/RPG/Prefabs/NPCs/Prefab_Enemy_Goblin.prefab",      1.38f),
                ("Assets/RPG/Prefabs/NPCs/Prefab_Enemy_Wolf.prefab",        1.05f)
            };

            foreach (var (prefabPath, targetHeight) in legacyTargets)
            {
                if (!File.Exists(prefabPath)) continue;
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                if (!root) continue;

                // Ensure any embedded FBX sub-asset materials are saved as real URP material assets
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                    var mats = r.sharedMaterials;
                    bool changed = false;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var orig = mats[i];
                        if (!orig || !AssetDatabase.IsSubAsset(orig)) continue;
                        string safe = new string(orig.name.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
                        string matPath = $"Assets/RPG/Materials/NPCs/Mat_Legacy_{root.name}_{safe}.mat";
                        var saved = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                        if (!saved)
                        {
                            saved = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                            Color col = orig.HasProperty("_BaseColor") ? orig.GetColor("_BaseColor") : (orig.HasProperty("_Color") ? orig.GetColor("_Color") : Color.gray);
                            saved.SetColor("_BaseColor", col);
                            saved.SetFloat("_Smoothness", 0.12f);
                            saved.SetFloat("_Metallic", 0f);
                            AssetDatabase.CreateAsset(saved, matPath);
                        }
                        mats[i] = saved;
                        changed = true;
                    }
                    if (changed) r.sharedMaterials = mats;
                }

                // Scale visual child (or root if single-object prefab) to realistic RPG height and ground feet at y=0
                root.transform.position = Vector3.zero;
                var anim = root.GetComponentInChildren<Animator>();
                if (anim) { anim.Rebind(); anim.Update(0f); }
                var b = ComputeWorldBounds(root);
                if (b.size.y > 0.05f && (Mathf.Abs(b.size.y - targetHeight) > 0.25f || Mathf.Abs(b.min.y) > 0.15f))
                {
                    Transform scaleTarget = root.transform.childCount == 1 ? root.transform.GetChild(0) : root.transform;
                    float factor = targetHeight / b.size.y;
                    scaleTarget.localScale *= factor;
                    if (anim) { anim.Rebind(); anim.Update(0f); }
                    b = ComputeWorldBounds(root);
                    if (scaleTarget != root.transform)
                        scaleTarget.position -= new Vector3(b.center.x, b.min.y, b.center.z);
                    else
                    {
                        foreach (Transform ch in root.transform)
                            ch.position -= new Vector3(b.center.x, b.min.y, b.center.z);
                    }
                }

                var colComp = root.GetComponent<CapsuleCollider>();
                if (colComp)
                {
                    colComp.height = targetHeight;
                    colComp.radius = 0.36f;
                    colComp.center = new Vector3(0f, targetHeight * 0.5f, 0f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void SetMatTexture(string matPath, Texture2D tex, Color color, float smoothness, bool alphaClip)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat || !tex) return;
            mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            if (alphaClip)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.35f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(mat);
        }

        static AnimatorController BuildHumanoidPlayerController(string controllerName, string archetype)
        {
            string path = AnimDir + controllerName + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var ualClips = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/UniversalAnimations/UAL1_Standard.fbx")
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();

            AnimationClip Clip(string suffix, string fallback = "Idle_Loop") =>
                ualClips.FirstOrDefault(c => c.name.EndsWith("|" + suffix, StringComparison.OrdinalIgnoreCase))
                ?? ualClips.FirstOrDefault(c => c.name.EndsWith("|" + fallback, StringComparison.OrdinalIgnoreCase))
                ?? ualClips.First();

            string idleSuffix = archetype == "Caster" ? "Spell_Simple_Idle_Loop"
                              : archetype == "Rogue"  ? "Idle_Loop"
                              : "Sword_Idle";
            string runSuffix  = archetype == "Rogue"  ? "Sprint_Loop" : "Jog_Fwd_Loop";
            float runSpeed    = archetype == "Rogue"  ? 0.90f : 1.0f;
            string atkSuffix  = archetype == "Caster" ? "Spell_Simple_Shoot" : "Sword_Attack";
            float atkSpeed    = archetype == "Rogue"  ? 1.45f : archetype == "Caster" ? 1.25f : 1.30f;
            string clvSuffix  = archetype == "Caster" ? "Spell_Simple_Shoot" : archetype == "Rogue" ? "Punch_Cross" : "Sword_Attack";

            var states = new (string name, string clipSuffix, float speed)[]
            {
                ("Idle",     idleSuffix,           1.0f),
                ("Walk",     "Walk_Loop",          1.0f),
                ("WalkBack", "Walk_Loop",         -1.0f),
                ("Run",      runSuffix,            runSpeed),
                ("RunBack",  "Jog_Fwd_Loop",      -0.85f),
                ("Attack",   atkSuffix,            atkSpeed),
                ("Cleave",   clvSuffix,            1.10f),
                ("Jump",     "Jump_Loop",          1.0f),
                ("Block",    "Crouch_Idle_Loop",   1.0f),
                ("Hit",      "Hit_Chest",          1.15f),
                ("Death",    "Death01",            1.0f),
                ("Ride",     "Sitting_Idle_Loop",  1.0f)
            };

            var sm = controller.layers[0].stateMachine;
            foreach (var entry in states)
            {
                var st = sm.states.FirstOrDefault(s => s.state.name == entry.name).state;
                if (!st) st = sm.AddState(entry.name);
                st.motion = Clip(entry.clipSuffix);
                st.speed = entry.speed;
                st.iKOnFeet = false;
                if (entry.name == "Idle") sm.defaultState = st;
                EditorUtility.SetDirty(st);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController BuildUndeadPlayerController(string controllerName, string sourceSkeletonController)
        {
            string path = AnimDir + controllerName + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var src = AssetDatabase.LoadAssetAtPath<AnimatorController>($"{AnimDir}{sourceSkeletonController}.controller");
            var srcMap = new Dictionary<string, Motion>();
            if (src && src.layers.Length > 0)
            {
                foreach (var s in src.layers[0].stateMachine.states)
                    if (s.state.motion != null) srcMap[s.state.name] = s.state.motion;
            }

            Motion GetM(string key, string fb = "Idle") =>
                srcMap.TryGetValue(key, out var m) ? m : (srcMap.TryGetValue(fb, out var f) ? f : null);

            var states = new (string name, Motion motion, float speed)[]
            {
                ("Idle",     GetM("Idle"),            1.0f),
                ("Walk",     GetM("Walk"),            1.0f),
                ("WalkBack", GetM("Walk"),           -1.0f),
                ("Run",      GetM("Run"),             1.0f),
                ("RunBack",  GetM("Run"),            -0.85f),
                ("Attack",   GetM("Attack"),          1.25f),
                ("Cleave",   GetM("Cleave", "Attack"),1.10f),
                ("Jump",     GetM("Run", "Idle"),     0.75f),
                ("Block",    GetM("Idle"),            1.0f),
                ("Hit",      GetM("Hit"),             1.1f),
                ("Death",    GetM("Death"),           1.0f),
                ("Ride",     GetM("Idle"),            1.0f)
            };

            var sm = controller.layers[0].stateMachine;
            foreach (var entry in states)
            {
                var st = sm.states.FirstOrDefault(s => s.state.name == entry.name).state;
                if (!st) st = sm.AddState(entry.name);
                st.motion = entry.motion;
                st.speed = entry.speed;
                st.iKOnFeet = false;
                if (entry.name == "Idle") sm.defaultState = st;
                EditorUtility.SetDirty(st);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildPrefab(string race, string characterClass, RuntimeAnimatorController controller)
        {
            var spec = StylizedRacialModelSystem.GetPlayableSpec(race, characterClass);

            var root = new GameObject($"{race}_{characterClass}");
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var visual = StylizedRacialModelSystem.BuildStyledRacialVisual(
                spec,
                controller,
                root.transform,
                $"{race}_{characterClass}_Model");

            if (!visual)
            {
                UnityEngine.Object.DestroyImmediate(root);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{TargetDir}{race}_{characterClass}.prefab");
            PrefabUtility.SaveAsPrefabAsset(root, $"{ResourcesDir}{race}_{characterClass}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static Color GetSubtleRacialMaterialTint(string race, string modelKey)
        {
            // Native Orc (Crag_Orc) and Goblin (Forest_Goblin) textures already have rich green/brown hues;
            // only apply subtle racial tinting when an archetype shares a Human mesh.
            if (race == "Orc" && modelKey != "Crag_Orc") return new Color(0.76f, 0.94f, 0.70f);
            if (race == "Undead") return new Color(0.76f, 0.82f, 0.86f);
            if (race == "Elf") return new Color(0.97f, 0.99f, 1.0f);
            if (race == "Dwarf") return new Color(1.0f, 0.95f, 0.90f);
            return Color.white;
        }

        static void ApplySavedCharacterMaterials(GameObject instance, string modelKey, string race, Color tint)
        {
            string texDir = TexRootDir + modelKey;
            var allTexPaths = Directory.Exists(texDir)
                ? Directory.GetFiles(texDir).Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Select(f => f.Replace('\\', '/')).ToArray()
                : Array.Empty<string>();

            var diffuseCandidates = allTexPaths
                .Where(p =>
                {
                    string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    return !n.Contains("normal") && !n.Contains("_nrm") && !n.Contains("spec") && !n.Contains("gloss") && !n.Contains("rough") && !n.Contains("metal") && !n.Contains("stdnm");
                })
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(t => t != null)
                .ToArray();

            Texture2D primaryDiffuse = diffuseCandidates
                .OrderByDescending(t => t.name.ToLowerInvariant().Contains("diffuse") || t.name.ToLowerInvariant().Contains("base"))
                .FirstOrDefault();

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                var origMats = r.sharedMaterials;
                var newMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    var orig = origMats[i];
                    string slotName = orig ? orig.name : $"Slot{i}";
                    Texture2D tex = ResolveDiffuseForSlot(slotName, diffuseCandidates, primaryDiffuse);
                    bool alphaClip = slotName.ToLowerInvariant().Contains("lash") || slotName.ToLowerInvariant().Contains("hair");

                    string safeSlot = new string(slotName.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
                    string raceSuffix = tint == Color.white ? "" : $"_{race}";
                    string matPath = $"{CharMatDir}Mat_Char_{modelKey}_{safeSlot}{raceSuffix}.mat";

                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (!mat)
                    {
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(mat, matPath);
                    }

                    if (tex)
                    {
                        mat.SetTexture("_BaseMap", tex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                    }
                    mat.SetColor("_BaseColor", tint);
                    mat.SetFloat("_Metallic", 0.0f);
                    mat.SetFloat("_Smoothness", slotName.ToLowerInvariant().Contains("eyespec") ? 0.25f : 0.07f);
                    mat.SetTexture("_BumpMap", null);
                    mat.DisableKeyword("_NORMALMAP");

                    if (alphaClip)
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.SetFloat("_Cutoff", 0.35f);
                        mat.EnableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    }
                    else
                    {
                        mat.SetFloat("_AlphaClip", 0f);
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                    }

                    EditorUtility.SetDirty(mat);
                    newMats[i] = mat;
                }
                r.sharedMaterials = newMats;
            }
        }

        static Texture2D ResolveDiffuseForSlot(string slotName, Texture2D[] candidates, Texture2D fallback)
        {
            string lower = slotName.ToLowerInvariant();
            if (candidates.Length > 1)
            {
                if (lower.Contains("lash") || lower.Contains("hair"))
                {
                    var m = candidates.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("lash") || t.name.ToLowerInvariant().Contains("hair"));
                    if (m) return m;
                }
                if (lower.Contains("body") || lower.Contains("eyespec") || lower.Contains("skin"))
                {
                    var m = candidates.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("body") || t.name.ToLowerInvariant().Contains("skin"));
                    if (m) return m;
                }
                if (lower.Contains("akai") || lower.Contains("cloth") || lower.Contains("armor"))
                {
                    var m = candidates.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("clothes") || t.name.ToLowerInvariant().Contains("armor"));
                    if (m) return m;
                }
            }
            return fallback;
        }

        static void ApplyUndeadSkeletonMaterialsAndWeapon(GameObject visual, string characterClass)
        {
            var skelTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SkeletonsDir + "skeleton_texture.png");

            string bodyMatPath = CharMatDir + "Mat_Char_UndeadSkeleton.mat";
            var bodyMat = AssetDatabase.LoadAssetAtPath<Material>(bodyMatPath);
            if (!bodyMat)
            {
                bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(bodyMat, bodyMatPath);
            }
            if (skelTex) bodyMat.SetTexture("_BaseMap", skelTex);
            bodyMat.SetColor("_BaseColor", new Color(0.92f, 0.96f, 0.94f));
            bodyMat.SetFloat("_Smoothness", 0.12f);
            bodyMat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(bodyMat);

            string glowMatPath = CharMatDir + "Mat_Char_UndeadEyes.mat";
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>(glowMatPath);
            if (!glowMat)
            {
                glowMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(glowMat, glowMatPath);
            }
            glowMat.SetColor("_BaseColor", new Color(0.25f, 0.95f, 0.72f));
            EditorUtility.SetDirty(glowMat);

            // Attach class weapon into handslot.r before material assignment
            string weaponFbx = characterClass == "Mage" ? "Skeleton_Staff.fbx"
                             : characterClass == "Rogue" ? "Skeleton_Blade.fbx"
                             : "Skeleton_Axe.fbx";
            var handSlot = visual.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "handslot.r");
            var weaponAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SkeletonsDir + weaponFbx);
            if (handSlot && weaponAsset)
            {
                var wpn = (GameObject)PrefabUtility.InstantiatePrefab(weaponAsset);
                wpn.name = Path.GetFileNameWithoutExtension(weaponFbx);
                wpn.transform.SetParent(handSlot, false);
                wpn.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                wpn.transform.localScale = Vector3.one;
                var wpnAnim = wpn.GetComponent<Animator>();
                if (wpnAnim) UnityEngine.Object.DestroyImmediate(wpnAnim);
            }

            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var origMats = r.sharedMaterials;
                var newMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    string slot = origMats[i] ? origMats[i].name.ToLowerInvariant() : "";
                    newMats[i] = slot.Contains("glow") ? glowMat : bodyMat;
                }
                r.sharedMaterials = newMats;
            }
        }

        static void BuildCompanionPetsAndProps()
        {
            BuildAnimalPetPrefab("Pug",   "Pet_Pug",   0.46f, "Animal Pug");
            BuildAnimalPetPrefab("Sheep", "Pet_Sheep", 0.50f, "Animal Sheep");
            BuildAnimalPetPrefab("Pig",   "Pet_Pig",   0.46f, "Animal Pug");
            BuildAnimalPetPrefab("Horse", "Pet_Steed", 0.58f, "Animal Horse");

            // Build normalized TreasureChest prefab in Resources/Props/TreasureChest.prefab
            var chestSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/FantasyProps/FBX/Chest_Wood.fbx");
            if (chestSource)
            {
                var root = new GameObject("TreasureChest");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(chestSource);
                inst.name = "Model";
                inst.transform.SetParent(root.transform, false);
                inst.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = r.sharedMaterials.Select(orig =>
                    {
                        if (!orig) return null;
                        var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/RPG/Materials/FantasyProps_{orig.name}.mat")
                             ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/FantasyProps_MI_Trim_Props.mat");
                        return m ? m : orig;
                    }).ToArray();
                }

                var b = ComputeWorldBounds(root);
                if (b.size.y > 0.05f) inst.transform.localScale = Vector3.one * (0.92f / b.size.y);
                b = ComputeWorldBounds(root);
                inst.transform.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);

                var box = root.AddComponent<BoxCollider>();
                box.size = new Vector3(1.1f, 0.92f, 0.75f);
                box.center = new Vector3(0f, 0.46f, 0f);

                PrefabUtility.SaveAsPrefabAsset(root, PropResourcesDir + "TreasureChest.prefab");
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        static void BuildAnimalPetPrefab(string animalType, string prefabName, float targetHeight, string controllerName)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>($"{AnimalsDir}{animalType}.fbx");
            if (!fbx) return;

            var root = new GameObject(prefabName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            model.name = animalType + "_Model";
            model.transform.SetParent(root.transform, false);
            // Animal FBXs face -Z natively; rotate 180° around Y so companion faces forward (+Z)
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));

            var anim = model.GetComponentInChildren<Animator>();
            if (!anim) anim = model.AddComponent<Animator>();
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>($"{AnimDir}{controllerName}.controller");
            if (ctrl) anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
                r.sharedMaterials = r.sharedMaterials.Select(orig =>
                {
                    if (!orig) return null;
                    string matPath = $"Assets/RPG/Materials/Animal {animalType} {orig.name}.mat";
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    var furTex = AssetDatabase.LoadAssetAtPath<Texture2D>(StylizedRacialModelSystem.StylizedTexDir + "Tex_Stylized_Fur.png");
                    if (!mat)
                    {
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        mat.SetColor("_BaseColor", orig.HasProperty("_BaseColor") ? orig.GetColor("_BaseColor") : orig.color);
                        mat.SetFloat("_Smoothness", 0.12f);
                        mat.SetFloat("_Metallic", 0f);
                        if (furTex) { mat.SetTexture("_BaseMap", furTex); mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f)); }
                        AssetDatabase.CreateAsset(mat, matPath);
                    }
                    else if (furTex)
                    {
                        mat.SetTexture("_BaseMap", furTex);
                        mat.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
                        mat.SetFloat("_Smoothness", 0.12f);
                        EditorUtility.SetDirty(mat);
                    }
                    return mat;
                }).ToArray();
            }

            anim.Rebind();
            if (anim.runtimeAnimatorController && anim.HasState(0, Animator.StringToHash("Idle")))
                anim.Play("Idle", 0, 0f);
            anim.Update(0f);

            var bounds = ComputeWorldBounds(root);
            if (bounds.size.y > 0.01f)
            {
                model.transform.localScale *= targetHeight / bounds.size.y;
                anim.Rebind();
                if (anim.runtimeAnimatorController && anim.HasState(0, Animator.StringToHash("Idle")))
                    anim.Play("Idle", 0, 0f);
                anim.Update(0f);
                bounds = ComputeWorldBounds(root);
                model.transform.position -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            foreach (var col in root.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(col);
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 2; // Ignore Raycast

            PrefabUtility.SaveAsPrefabAsset(root, $"{PetResourcesDir}{prefabName}.prefab");
            UnityEngine.Object.DestroyImmediate(root);
        }

        static string SelectSourceFbx(string race, string characterClass)
        {
            if (race == "Orc")
            {
                if (characterClass == "Warrior" || characterClass == "Paladin")
                    return ArtDir + "Crag_Orc.fbx";
                if (characterClass == "Mage")
                    return ArtDir + "Arcanist_Mage.fbx";
                return ArtDir + "Ranger_Scout.fbx";
            }

            if (race == "Goblin")
            {
                return ArtDir + "Forest_Goblin.fbx";
            }

            if (race == "Undead")
            {
                if (characterClass == "Warrior") return SkeletonsDir + "Skeleton_Warrior.fbx";
                if (characterClass == "Mage") return SkeletonsDir + "Skeleton_Mage.fbx";
                if (characterClass == "Rogue") return SkeletonsDir + "Skeleton_Rogue.fbx";
                return ArtDir + "Paladin_Guard.fbx";
            }

            if (race == "Dwarf")
            {
                if (characterClass == "Warrior") return ArtDir + "Blacksmith_Man.fbx";
                if (characterClass == "Paladin") return ArtDir + "Paladin_Guard.fbx";
                if (characterClass == "Mage") return ArtDir + "Arcanist_Mage.fbx";
                return ArtDir + "Ranger_Scout.fbx";
            }

            switch (characterClass)
            {
                case "Paladin": return ArtDir + "Paladin_Guard.fbx";
                case "Mage": return ArtDir + "Arcanist_Mage.fbx";
                case "Rogue": return ArtDir + "Ranger_Scout.fbx";
                case "Warrior":
                default: return ArtDir + "Knight_Arms.fbx";
            }
        }

        static bool RepairWorldTerrainElevations(StringBuilder sb)
        {
            var terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindFirstObjectByType<Terrain>();
            if (!terrain || !terrain.terrainData) return false;

            bool changed = false;
            var data = terrain.terrainData;
            Vector3 tPos = terrain.transform.position;
            Vector3 tSize = data.size;

            float SampleT(float wx, float wz) => terrain.SampleHeight(new Vector3(wx, 0f, wz)) + tPos.y;

            // 1. Record pre-existing grounded overworld objects outside '11 - New World NPCs' before updating terrain heightmap
            var preExistingGrounded = new List<(Transform tr, float oldTerrY)>();
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == "09 - Terrain and Atmosphere" || root.name == "11 - New World NPCs" || root.GetComponent<Light>() || root.GetComponent<Camera>())
                    continue;
                CollectPreExistingGrounded(root.transform, terrain, preExistingGrounded);
            }

            // 2. Refresh Frontier Terrain heightmap & splatmap from TerrainLayout (adds smooth plateaus & roads for Horde/Northern hubs)
            if (data.heightmapResolution == 513 && data.alphamapResolution == 256)
            {
                var heights = new float[513, 513];
                for (int z = 0; z < 513; z++)
                {
                    float wz = tPos.z + (z / 512f) * tSize.z;
                    for (int x = 0; x < 513; x++)
                    {
                        float wx = tPos.x + (x / 512f) * tSize.x;
                        heights[z, x] = TerrainLayout.Height(wx, wz) / tSize.y;
                    }
                }
                data.SetHeights(0, 0, heights);

                if (data.terrainLayers != null && data.terrainLayers.Length >= 3)
                {
                    var splat = new float[256, 256, 3];
                    for (int z = 0; z < 256; z++)
                    {
                        float v = z / 255f;
                        float wz = tPos.z + v * tSize.z;
                        for (int x = 0; x < 256; x++)
                        {
                            float u = x / 255f;
                            float wx = tPos.x + u * tSize.x;
                            float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 39f, data.GetSteepness(u, v)));
                            float dirt = TerrainLayout.Dirt(wx, wz) * (1f - rock);
                            splat[z, x, 0] = 1f - rock - dirt;
                            splat[z, x, 1] = dirt;
                            splat[z, x, 2] = rock;
                        }
                    }
                    data.SetAlphamaps(0, 0, splat);
                }

                EditorUtility.SetDirty(data);
                terrain.Flush();
                var tc = terrain.GetComponent<TerrainCollider>();
                if (tc) { tc.enabled = false; tc.enabled = true; }
                Physics.SyncTransforms();
                sb.AppendLine("  [TERRAIN FIX] Sculpted flat Horde/Northern settlement plateaus & connecting war-roads in Frontier Terrain.asset.");
            }

            // Keep pre-existing trees, rocks & enemies aligned if their terrain cell moved
            foreach (var (tr, oldTerrY) in preExistingGrounded)
            {
                if (!tr) continue;
                float newTerrY = SampleT(tr.position.x, tr.position.z);
                float diff = newTerrY - oldTerrY;
                if (Mathf.Abs(diff) > 0.015f)
                {
                    tr.position += Vector3.up * diff;
                    EditorUtility.SetDirty(tr);
                    changed = true;
                }
            }
            Physics.SyncTransforms();

            // 3. Lift all buried world groups, structures, NPCs, enemies, portals & gathering nodes under '11 - New World NPCs'
            var newWorld = GameObject.Find("11 - New World NPCs");
            if (newWorld != null)
            {
                foreach (Transform child in newWorld.transform)
                {
                    string name = child.name;
                    Vector3 cp = child.position;

                    // Subterranean Scarlet Crypts instance: keep at (-400, -40, 200), but fix its Exit Portal overworld destination!
                    if (name.StartsWith("12 -"))
                    {
                        foreach (var dp in child.GetComponentsInChildren<DungeonPortal>(true))
                        {
                            if (dp.isExitPortal)
                            {
                                float exitTerrY = SampleT(dp.destination.x, dp.destination.z);
                                Vector3 fixedDest = new Vector3(dp.destination.x, exitTerrY + 0.45f, dp.destination.z);
                                if (Vector3.Distance(dp.destination, fixedDest) > 0.05f)
                                {
                                    dp.destination = fixedDest;
                                    EditorUtility.SetDirty(dp);
                                    changed = true;
                                    sb.AppendLine($"  [PORTAL FIX] Updated '{dp.name}' destination -> {fixedDest}");
                                }
                            }
                        }
                        continue;
                    }

                    // Wyrmcrest Sky Summit Roost at y=48: intentional mountain peak
                    if (name.StartsWith("13 -") || cp.y >= 40f || cp.x < -200f)
                        continue;

                    // Flight Master container at (0,0,0) -> snap each overworld FlightMaster child
                    if (name.StartsWith("14 -"))
                    {
                        foreach (Transform fmTr in child)
                        {
                            if (fmTr.position.y >= 40f || fmTr.position.x < -200f) continue;
                            float surfaceY = SampleWalkableSurfaceOrTerrain(terrain, fmTr.position, fmTr);
                            if (Mathf.Abs(fmTr.position.y - surfaceY) > 0.05f)
                            {
                                Vector3 old = fmTr.position;
                                fmTr.position = new Vector3(old.x, surfaceY, old.z);
                                if (fmTr.TryGetComponent<FlightMaster>(out var fm))
                                {
                                    fm.flightStationPos = fmTr.position;
                                    EditorUtility.SetDirty(fm);
                                }
                                EditorUtility.SetDirty(fmTr);
                                changed = true;
                                sb.AppendLine($"  [FLIGHT MASTER FIX] Lifted '{fmTr.name}' y={old.y:F2} -> {surfaceY:F2}");
                            }
                        }
                        continue;
                    }

                    // World Gathering Nodes container at (0,0,0) -> snap each overworld herb/ore node
                    if (name.StartsWith("15 -"))
                    {
                        foreach (Transform nodeTr in child)
                        {
                            if (nodeTr.position.y >= 40f || nodeTr.position.x < -200f) continue;
                            float ty = SampleT(nodeTr.position.x, nodeTr.position.z);
                            float halfH = nodeTr.name.Contains("Vein") || nodeTr.name.Contains("Deposit") ? 0.32f : 0.22f;
                            float targetY = ty + halfH;
                            if (Mathf.Abs(nodeTr.position.y - targetY) > 0.08f)
                            {
                                Vector3 old = nodeTr.position;
                                nodeTr.position = new Vector3(old.x, targetY, old.z);
                                EditorUtility.SetDirty(nodeTr);
                                changed = true;
                                sb.AppendLine($"  [NODE FIX] Lifted '{nodeTr.name}' y={old.y:F2} -> {targetY:F2}");
                            }
                        }
                        continue;
                    }

                    // Structured landmark / capital / racial starter groups (06, 07, 08, 09, 16, 17, 18, 19, 20)
                    bool isStructuredHub = name.StartsWith("06 -") || name.StartsWith("07 -") || name.StartsWith("08 -") ||
                                           name.StartsWith("09 -") || name.StartsWith("16 -") || name.StartsWith("17 -") ||
                                           name.StartsWith("18 -") || name.StartsWith("19 -") || name.StartsWith("20 -");
                    if (isStructuredHub)
                    {
                        float hubTerrY = SampleT(cp.x, cp.z);
                        // Groups with a foundation slab at local y=-0.2..-0.4 sit cleanly at hubTerrY + 0.12f so the slab crowns above terrain
                        float slabOffset = (name.StartsWith("09 -") || name.StartsWith("18 -") || name.StartsWith("19 -") || name.StartsWith("20 -")) ? 0.15f : 0.0f;
                        float targetHubY = hubTerrY + slabOffset;
                        if (Mathf.Abs(cp.y - targetHubY) > 0.05f)
                        {
                            float oldY = cp.y;
                            child.position = new Vector3(cp.x, targetHubY, cp.z);
                            EditorUtility.SetDirty(child);
                            Physics.SyncTransforms();
                            changed = true;
                            sb.AppendLine($"  [HUB ELEVATION FIX] Lifted '{name}' y={oldY:F2} -> {targetHubY:F2} (terrainY={hubTerrY:F2})");
                        }

                        // Snap any NPCs, Enemies, Campfires, Spikes, Fissures, or Props inside the hub that extend onto sloped outskirts
                        foreach (Transform sub in child)
                        {
                            if (sub.name.Contains("Platform") || sub.name.Contains("Plaza") || sub.name.Contains("Ground") ||
                                sub.name.Contains("Deck") || sub.name.Contains("Dais") || sub.name.Contains("Bedrock") ||
                                sub.name.Contains("Header") || sub.name.Contains("Banner") || sub.name.Contains("Portal"))
                                continue;

                            bool isActor = sub.GetComponent<EnemyController>() || sub.GetComponent<TownNPC>() ||
                                           sub.GetComponent<QuestGiver>() || sub.GetComponent<Merchant>();
                            if (isActor)
                            {
                                float surfY = SampleWalkableSurfaceOrTerrain(terrain, sub.position, sub);
                                if (Mathf.Abs(sub.position.y - surfY) > 0.08f)
                                {
                                    sub.position = new Vector3(sub.position.x, surfY, sub.position.z);
                                    EditorUtility.SetDirty(sub);
                                    changed = true;
                                }
                            }
                            else
                            {
                                float subTerrY = SampleT(sub.position.x, sub.position.z);
                                if (sub.position.y < subTerrY - 0.10f)
                                {
                                    sub.position = new Vector3(sub.position.x, subTerrY + 0.08f, sub.position.z);
                                    EditorUtility.SetDirty(sub);
                                    changed = true;
                                }
                            }
                        }
                        continue;
                    }

                    // Loose top-level NPCs & Enemies directly under '11 - New World NPCs' (e.g. Outrider Lyra, Ashen Goblin Ambusher)
                    float looseSurfY = SampleWalkableSurfaceOrTerrain(terrain, cp, child);
                    if (Mathf.Abs(cp.y - looseSurfY) > 0.08f)
                    {
                        child.position = new Vector3(cp.x, looseSurfY, cp.z);
                        EditorUtility.SetDirty(child);
                        changed = true;
                        sb.AppendLine($"  [ACTOR FIX] Lifted '{name}' y={cp.y:F2} -> {looseSurfY:F2}");
                    }
                }
            }

            // Sync WorldBossEncounter spawnPosition if present in scene
            foreach (var wb in UnityEngine.Object.FindObjectsByType<WorldBossEncounter>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var gorgonax = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(e => e.enemyId == "worldboss-gorgonax");
                if (gorgonax && Vector3.Distance(wb.spawnPosition, gorgonax.transform.position) > 0.05f)
                {
                    wb.spawnPosition = gorgonax.transform.position;
                    EditorUtility.SetDirty(wb);
                    changed = true;
                }
            }

            Physics.SyncTransforms();
            return changed;
        }

        static void CollectPreExistingGrounded(Transform parent, Terrain terrain, List<(Transform tr, float oldTerrY)> list)
        {
            Vector3 tPos = terrain.transform.position, tSize = terrain.terrainData.size;
            foreach (Transform item in parent)
            {
                if (item.name == "Meadow ground" || item.name == "World boundary" || item.GetComponent<Camera>()) continue;
                if (item.position.sqrMagnitude < 0.001f && item.childCount > 0 && !item.GetComponent<Renderer>() && !item.GetComponent<Collider>())
                {
                    CollectPreExistingGrounded(item, terrain, list);
                    continue;
                }
                Vector3 p = item.position;
                if (p.x < tPos.x || p.x > tPos.x + tSize.x || p.z < tPos.z || p.z > tPos.z + tSize.z) continue;
                if (p.y < -20f || p.y >= 40f) continue;
                var mf = item.GetComponent<MeshFilter>();
                if (mf && mf.sharedMesh && mf.sharedMesh.name.Contains("terrain ribbon")) continue;

                float ty = terrain.SampleHeight(p) + tPos.y;
                if (Mathf.Abs(p.y - ty) < 2.2f)
                    list.Add((item, ty));
            }
        }

        static float SampleWalkableSurfaceOrTerrain(Terrain terrain, Vector3 pos, Transform ignoreSelf)
        {
            float terrY = terrain.SampleHeight(pos) + terrain.transform.position.y;
            var hits = Physics.RaycastAll(new Vector3(pos.x, terrY + 12f, pos.z), Vector3.down, 24f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = terrY;
            foreach (var h in hits)
            {
                if (ignoreSelf && h.collider.transform.IsChildOf(ignoreSelf)) continue;
                if (h.collider is CharacterController || h.collider is CapsuleCollider) continue;
                var tr = h.collider.transform;
                if (tr.GetComponentInParent<PlayerController>() || tr.GetComponentInParent<EnemyController>() ||
                    tr.GetComponentInParent<TownNPC>() || tr.GetComponentInParent<FlightMaster>() ||
                    tr.GetComponentInParent<QuestGiver>() || tr.GetComponentInParent<Merchant>() ||
                    tr.GetComponentInParent<GatheringNode>())
                    continue;
                if (h.point.y <= terrY + 1.6f && h.point.y > bestY)
                    bestY = h.point.y;
            }
            return bestY;
        }

        static Bounds ComputeWorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(false);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
