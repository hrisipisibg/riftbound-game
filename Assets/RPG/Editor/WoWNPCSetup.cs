using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Riftbound;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class WoWNPCSetup
    {
        const string ArtDir = "Assets/RPG/Art/NPCs_WoW/";
        const string TexRootDir = "Assets/RPG/Art/NPCs_WoW/Textures/";
        const string MatDir = "Assets/RPG/Materials/NPCs_WoW/";
        const string AnimDir = "Assets/RPG/Animations/NPCs_WoW/";
        const string PrefabDir = "Assets/RPG/Prefabs/NPCs_WoW/";
        const string ScenePath = "Assets/RPG/Scenes/Outpost-Updated.unity";
        const string AutoMarkerFile = "WoWNPCSetup_v2.done";

        static readonly string[] ModelFiles =
        {
            "Paladin_Guard.fbx",
            "Castle_Guard.fbx",
            "Knight_Arms.fbx",
            "Blacksmith_Man.fbx",
            "Tavern_Maiden.fbx",
            "Arcanist_Mage.fbx",
            "Ranger_Scout.fbx",
            "Forest_Goblin.fbx",
            "Crag_Orc.fbx"
        };

        static WoWNPCSetup()
        {
            EditorApplication.delayCall += TryAutoSetup;
        }

        static void TryAutoSetup()
        {
            if (Application.isBatchMode || File.Exists(WorldRegions.ScenePath)) return;
            if (File.Exists(AutoMarkerFile)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += TryAutoSetup;
                return;
            }

            if (!Directory.Exists(ArtDir) || Directory.GetFiles(ArtDir, "*.fbx").Length < ModelFiles.Length)
                return;

            Setup();
        }

        [MenuItem("Riftbound/ðŸŒŸ Rebuild Classic WoW NPCs (Fix Scale & Textures)", priority = 4)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Riftbound] Exit Play Mode before rebuilding NPCs.");
                return;
            }

            try
            {
                Directory.CreateDirectory(TexRootDir);
                Directory.CreateDirectory(MatDir);
                Directory.CreateDirectory(AnimDir);
                Directory.CreateDirectory(PrefabDir);
                Directory.CreateDirectory("Assets/RPG/Scenes/Backups");
                AssetDatabase.Refresh();

                // 1. Import all smooth-shaded fantasy FBX models as Humanoid & extract embedded diffuse textures
                foreach (string file in ModelFiles)
                {
                    string fbxPath = ArtDir + file;
                    if (!File.Exists(fbxPath)) continue;
                    ConfigureHumanoidAndExtractTextures(fbxPath);
                }
                AssetDatabase.Refresh();

                // 2. Apply Classic WoW Old-School Low-Res Diffuse Texture Settings (256x256 crisp matte diffuse)
                ConfigureAllExtractedTexturesClassicWoW();
                AssetDatabase.Refresh();

                // 3. Build Humanoid AnimatorControllers from UAL1_Standard.fbx
                var ctrlGuard = BuildHumanoidSingleLoopController("WoW_Guard_Idle", "Sword_Idle");
                var ctrlSmith = BuildHumanoidSingleLoopController("WoW_Blacksmith_Loop", "Idle_Torch_Loop");
                var ctrlTalk = BuildHumanoidSingleLoopController("WoW_TownTalk_Idle", "Idle_Talking_Loop");
                var ctrlMage = BuildHumanoidSingleLoopController("WoW_Mage_Idle", "Spell_Simple_Idle_Loop");
                var ctrlScout = BuildHumanoidSingleLoopController("WoW_Scout_Idle", "Idle_Loop");
                var ctrlMeleeEnemy = BuildHumanoidCombatController("WoW_Enemy_Melee", rangedCaster: false);
                var ctrlCasterEnemy = BuildHumanoidCombatController("WoW_Enemy_Caster", rangedCaster: true);

                // 4. Build properly scaled, grounded, multi-material URP matte diffuse prefabs
                BuildTownNPCPrefab("Prefab_WoW_PaladinGuard", "Paladin_Guard.fbx", ctrlGuard, 1.92f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_CastleGuard", "Castle_Guard.fbx", ctrlGuard, 1.88f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_KnightArms", "Knight_Arms.fbx", ctrlGuard, 1.90f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_Blacksmith", "Blacksmith_Man.fbx", ctrlSmith, 1.85f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_TavernMaiden", "Tavern_Maiden.fbx", ctrlTalk, 1.73f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_Arcanist", "Arcanist_Mage.fbx", ctrlMage, 1.86f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_RangerScout", "Ranger_Scout.fbx", ctrlScout, 1.76f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_CragOrc", "Crag_Orc.fbx", ctrlGuard, 2.06f, Color.white);
                BuildTownNPCPrefab("Prefab_WoW_ForestGoblin", "Forest_Goblin.fbx", ctrlScout, 1.34f, Color.white);

                // 5. Open Outpost-Updated.unity, save safety backup, replace unscaled low-poly NPCs & upgrade world enemies
                var scene = EditorSceneManager.GetActiveScene();
                if (scene.path != ScenePath)
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

                string backupPath = "Assets/RPG/Scenes/Backups/Outpost-Updated-before-wow-npcs.unity";
                if (!File.Exists(backupPath))
                    EditorSceneManager.SaveScene(scene, backupPath, true);

                var report = new StringBuilder();
                report.AppendLine("=== CLASSIC WOW NPC & ENEMY OVERHAUL REPORT ===");

                PopulateSceneWithClassicWoWNPCs(ctrlGuard, ctrlMeleeEnemy, ctrlCasterEnemy, report);

                // 6. Expand Mount System (Celestial Winged Steed flying mount)
                MountExpansionSetup.Setup();
                report.AppendLine("  [Mounts] Celestial Winged Steed (Flying) and mount definitions configured on player.");

                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                SceneQuickOpener.FocusPlayerInSceneView();

                File.WriteAllText("WoWNPCSetupResult.txt", "PASS\n" + report);
                File.WriteAllText(AutoMarkerFile, DateTime.UtcNow.ToString("O"));
                Debug.Log("<color=#70df80>[Riftbound]</color> Classic WoW NPCs rebuilt with smooth 3D meshes, 256x256 old-school diffuse textures, and exact human scale!\n" + report);
            }
            catch (Exception ex)
            {
                File.WriteAllText("WoWNPCSetupResult.txt", "FAILED: " + ex);
                Debug.LogException(ex);
            }
        }

        static void ConfigureHumanoidAndExtractTextures(string fbxPath)
        {
            string key = Path.GetFileNameWithoutExtension(fbxPath);
            string texDir = TexRootDir + key;
            Directory.CreateDirectory(texDir);

            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (!importer) return;

            bool needsReimport = false;
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                needsReimport = true;
            }
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                needsReimport = true;
            }
            if (importer.importNormals != ModelImporterNormals.Import)
            {
                importer.importNormals = ModelImporterNormals.Import;
                needsReimport = true;
            }

            if (Directory.GetFiles(texDir).Length == 0)
            {
                importer.ExtractTextures(texDir);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                needsReimport = true;
            }

            if (needsReimport)
                importer.SaveAndReimport();

            // Verify Humanoid avatar validity; if a monster rig is non-humanoid, fall back cleanly to Generic
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<Avatar>().FirstOrDefault();
            if (importer.animationType == ModelImporterAnimationType.Human && (!avatar || !avatar.isValid))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
        }

        static void ConfigureAllExtractedTexturesClassicWoW()
        {
            if (!Directory.Exists(TexRootDir)) return;
            foreach (string file in Directory.GetFiles(TexRootDir, "*.*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) continue;
                string assetPath = file.Replace('\\', '/');
                var texImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (!texImporter) continue;

                string lower = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
                bool isNormalOrSpec = lower.Contains("normal") || lower.Contains("_nrm") || lower.Contains("specular") || lower.Contains("gloss") || lower.Contains("rough") || lower.Contains("metallic");
                if (isNormalOrSpec) continue;

                // Old-school Classic WoW character skin spec: crisp 256x256 diffuse, uncompressed RGB/RGBA, bilinear filter
                texImporter.textureType = TextureImporterType.Default;
                texImporter.sRGBTexture = true;
                texImporter.isReadable = true;
                texImporter.alphaIsTransparency = texImporter.DoesSourceTextureHaveAlpha();
                texImporter.maxTextureSize = 256;
                texImporter.textureCompression = TextureImporterCompression.Uncompressed;
                texImporter.filterMode = FilterMode.Bilinear;
                texImporter.mipmapEnabled = true;
                texImporter.SaveAndReimport();
            }
        }

        static AnimatorController BuildHumanoidSingleLoopController(string controllerName, string clipSuffix)
        {
            string path = AnimDir + controllerName + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var ualClips = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/UniversalAnimations/UAL1_Standard.fbx")
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();

            var clip = ualClips.FirstOrDefault(c => c.name.EndsWith("|" + clipSuffix, StringComparison.OrdinalIgnoreCase))
                    ?? ualClips.FirstOrDefault(c => c.name.EndsWith("|Idle_Loop", StringComparison.OrdinalIgnoreCase))
                    ?? ualClips.First();

            var sm = controller.layers[0].stateMachine;
            var state = sm.states.FirstOrDefault(s => s.state.name == "Idle").state;
            if (!state) state = sm.AddState("Idle");
            state.motion = clip;
            state.iKOnFeet = false;
            sm.defaultState = state;
            EditorUtility.SetDirty(state);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorController BuildHumanoidCombatController(string controllerName, bool rangedCaster)
        {
            string path = AnimDir + controllerName + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            var ualClips = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/UniversalAnimations/UAL1_Standard.fbx")
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();

            AnimationClip FindClip(string suffix, string fallback) =>
                ualClips.FirstOrDefault(c => c.name.EndsWith("|" + suffix, StringComparison.OrdinalIgnoreCase))
                ?? ualClips.FirstOrDefault(c => c.name.EndsWith("|" + fallback, StringComparison.OrdinalIgnoreCase))
                ?? ualClips.First();

            var entries = new (string stateName, string clipSuffix, float speed)[]
            {
                ("Idle", rangedCaster ? "Spell_Simple_Idle_Loop" : "Sword_Idle", 1f),
                ("Walk", "Walk_Loop", 1f),
                ("Run", "Jog_Fwd_Loop", 1f),
                ("Attack", rangedCaster ? "Spell_Simple_Shoot" : "Sword_Attack", rangedCaster ? 1.0f : 1.25f),
                ("Cleave", "Sword_Attack", 0.9f),
                ("Hit", "Hit_Chest", 1.15f),
                ("Death", "Death01", 1f)
            };

            var sm = controller.layers[0].stateMachine;
            foreach (var entry in entries)
            {
                var state = sm.states.FirstOrDefault(s => s.state.name == entry.stateName).state;
                if (!state) state = sm.AddState(entry.stateName);
                state.motion = FindClip(entry.clipSuffix, "Idle_Loop");
                state.speed = entry.speed;
                state.iKOnFeet = false;
                if (entry.stateName == "Idle") sm.defaultState = state;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static GameObject BuildTownNPCPrefab(string prefabName, string fbxFile, AnimatorController ctrl, float targetHeight, Color tint)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + fbxFile);
            if (!source) throw new FileNotFoundException("Missing NPC FBX: " + ArtDir + fbxFile);

            string modelKey = Path.GetFileNameWithoutExtension(fbxFile);
            var root = new GameObject(prefabName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            var visual = CreateNormalizedCharacterVisual(source, modelKey, ctrl, targetHeight, tint, root.transform);

            var col = root.AddComponent<CapsuleCollider>();
            col.height = targetHeight;
            col.radius = 0.34f;
            col.center = new Vector3(0f, targetHeight * 0.5f, 0f);

            string savePath = PrefabDir + prefabName + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject CreateNormalizedCharacterVisual(GameObject sourceFbx, string modelKey, AnimatorController ctrl, float targetHeight, Color tint, Transform parent)
        {
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(sourceFbx);
            visual.name = "ClassicWoWVisual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            ApplyClassicWoWMaterials(visual, modelKey, tint);

            var anim = visual.GetComponentInChildren<Animator>();
            if (!anim) anim = visual.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            anim.Play("Idle", 0, 0f);
            anim.Update(0f);

            // Measure exact posed world-space bounds and normalize height strictly
            var bounds = ComputeWorldBounds(visual);
            if (bounds.size.y < 0.001f)
                throw new InvalidOperationException($"Zero-height bounds on {sourceFbx.name}");

            float scaleFactor = targetHeight / bounds.size.y;
            visual.transform.localScale *= scaleFactor;
            anim.Update(0f);
            Physics.SyncTransforms();

            bounds = ComputeWorldBounds(visual);
            float footOffset = parent.position.y - bounds.min.y;
            visual.transform.position += new Vector3(0f, footOffset, 0f);
            anim.Update(0f);

            bounds = ComputeWorldBounds(visual);
            float maxAllowedHeight = Mathf.Max(2.75f, targetHeight * 1.25f);
            if (bounds.size.y < 1.05f || bounds.size.y > maxAllowedHeight || bounds.size.x > 4.5f || bounds.size.z > 4.5f)
                throw new InvalidOperationException($"Scale validation failed on {sourceFbx.name}: resulting bounds {bounds.size}");

            return visual;
        }

        static void ApplyClassicWoWMaterials(GameObject instance, string modelKey, Color tint)
        {
            string texDir = TexRootDir + modelKey;
            var allTexPaths = Directory.Exists(texDir)
                ? Directory.GetFiles(texDir).Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)).Select(f => f.Replace('\\', '/')).ToArray()
                : Array.Empty<string>();

            var diffuseCandidates = allTexPaths
                .Where(p =>
                {
                    string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    return !n.Contains("normal") && !n.Contains("_nrm") && !n.Contains("spec") && !n.Contains("gloss") && !n.Contains("rough") && !n.Contains("metal");
                })
                .Select(AssetDatabase.LoadAssetAtPath<Texture2D>)
                .Where(t => t != null)
                .ToArray();

            Texture2D primaryDiffuse = diffuseCandidates
                .OrderByDescending(t => t.name.ToLowerInvariant().Contains("diffuse") || t.name.ToLowerInvariant().Contains("base") || t.name.ToLowerInvariant().Contains("1001"))
                .FirstOrDefault();

            string tintSuffix = tint == Color.white ? "" : $"_{ColorUtility.ToHtmlStringRGB(tint)}";

            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;

                var origMats = r.sharedMaterials;
                var newMats = new Material[origMats.Length];
                for (int i = 0; i < origMats.Length; i++)
                {
                    var orig = origMats[i];
                    string slotName = orig ? orig.name : $"Slot{i}";
                    Texture2D tex = ResolveSlotDiffuseTexture(orig, slotName, diffuseCandidates, primaryDiffuse);

                    bool needsAlphaClip = ShouldUseAlphaClip(orig, slotName, tex);
                    string safeSlot = SanitizeFileName(slotName);
                    string matPath = $"{MatDir}Mat_{modelKey}_{safeSlot}{tintSuffix}.mat";

                    var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                    if (!mat)
                    {
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(mat, matPath);
                    }

                    if (tex) mat.SetTexture("_BaseMap", tex);
                    mat.SetColor("_BaseColor", tint);
                    // Classic WoW matte diffuse look: zero metallic sheen, very low smoothness, no glossy plastic normal map
                    mat.SetFloat("_Metallic", 0.0f);
                    mat.SetFloat("_Smoothness", 0.06f);
                    mat.SetTexture("_BumpMap", null);
                    mat.DisableKeyword("_NORMALMAP");

                    if (needsAlphaClip)
                    {
                        mat.SetFloat("_AlphaClip", 1f);
                        mat.SetFloat("_Cutoff", 0.38f);
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

        static Texture2D ResolveSlotDiffuseTexture(Material orig, string slotName, Texture2D[] candidates, Texture2D fallback)
        {
            if (orig)
            {
                if (orig.HasProperty("_BaseMap") && orig.GetTexture("_BaseMap") is Texture2D bm) return bm;
                if (orig.HasProperty("_MainTex") && orig.GetTexture("_MainTex") is Texture2D mt) return mt;
                if (orig.mainTexture is Texture2D mainT) return mainT;
            }

            string lowerSlot = slotName.ToLowerInvariant();
            if (candidates.Length > 1)
            {
                if (lowerSlot.Contains("1002") || lowerSlot.Contains("hair") || lowerSlot.Contains("lash") || lowerSlot.Contains("body1"))
                {
                    var match = candidates.FirstOrDefault(t =>
                    {
                        string tn = t.name.ToLowerInvariant();
                        return tn.Contains("1002") || tn.Contains("hair") || tn.Contains("lash");
                    });
                    if (match) return match;
                }
                if (lowerSlot.Contains("prop") || lowerSlot.Contains("sword") || lowerSlot.Contains("shield") || lowerSlot.Contains("bow") || lowerSlot.Contains("weapon"))
                {
                    var match = candidates.FirstOrDefault(t =>
                    {
                        string tn = t.name.ToLowerInvariant();
                        return tn.Contains("prop") || tn.Contains("sword") || tn.Contains("shield") || tn.Contains("bow") || tn.Contains("weapon");
                    });
                    if (match) return match;
                }
                var prefixMatch = candidates.FirstOrDefault(t => lowerSlot.Contains(t.name.ToLowerInvariant().Replace("_diffuse", "")));
                if (prefixMatch) return prefixMatch;
            }
            return fallback;
        }

        static bool ShouldUseAlphaClip(Material orig, string slotName, Texture2D tex)
        {
            if (!tex) return false;
            string s = (slotName + "_" + tex.name).ToLowerInvariant();
            bool nameSuggestsCutout = s.Contains("hair") || s.Contains("lash") || s.Contains("1002") || s.Contains("feather") || s.Contains("alpha") || s.Contains("trans") || s.Contains("cloth") || s.Contains("skirt");
            if (orig && orig.HasProperty("_Mode") && orig.GetFloat("_Mode") >= 1f)
                nameSuggestsCutout = true;

            if (!nameSuggestsCutout) return false;

            try
            {
                var pixels = tex.GetPixels32();
                int transparentCount = 0;
                int step = Mathf.Max(1, pixels.Length / 2048);
                int sampled = 0;
                for (int i = 0; i < pixels.Length; i += step)
                {
                    sampled++;
                    if (pixels[i].a < 30) transparentCount++;
                }
                return sampled > 0 && transparentCount > sampled / 50; // >2% truly transparent cutout pixels
            }
            catch
            {
                return false;
            }
        }

        static void PopulateSceneWithClassicWoWNPCs(AnimatorController ctrlGuard, AnimatorController ctrlMeleeEnemy, AnimatorController ctrlCasterEnemy, StringBuilder report)
        {
            Physics.SyncTransforms();

            // Remove old unscaled low-poly group in scene
            var oldGroup = GameObject.Find("11 - New World NPCs");
            if (oldGroup) UnityEngine.Object.DestroyImmediate(oldGroup);

            var root = new GameObject("11 - New World NPCs");

            var paladinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_PaladinGuard.prefab");
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_CastleGuard.prefab");
            var knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_KnightArms.prefab");
            var smithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Blacksmith.prefab");
            var maidenPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_TavernMaiden.prefab");
            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Arcanist.prefab");
            var scoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_RangerScout.prefab");

            // 1. Outpost Gate Guards (1.88m - 1.92m tall, flanking gate cleanly clear of weapon racks)
            SpawnTownNPC(paladinPrefab, root.transform, "Captain Halford", "Emberwatch Guard",
                "Light guide your blade! Beyond this gate lie Goblin ambushers and Crag Orc raiders.",
                new Vector3(2.25f, -0.11f, 18.25f), -162f, 2.28f, report);

            SpawnTownNPC(guardPrefab, root.transform, "Sentry Justin", "Gate Watch",
                "Stay on the cobbled road if you value your hide, traveler.",
                new Vector3(-2.25f, -0.11f, 18.25f), 162f, 2.22f, report);

            // 2. Blacksmith & Weaponsmaster at the Armory / Anvil
            SpawnTownNPC(smithPrefab, root.transform, "Blacksmith Torvald", "Weapons & Armor",
                "Good steel takes patience and fire. Speak with Rowan if you need supplies!",
                new Vector3(8.95f, -0.11f, 7.85f), -42f, 2.18f, report);

            SpawnTownNPC(knightPrefab, root.transform, "Weaponsmaster Vaelen", "Warrior Trainer",
                "Build your Rage in the heat of melee, then unleash Whirlwind to cleave whole packs!",
                new Vector3(6.15f, -0.11f, 7.10f), -125f, 2.24f, report);

            // 3. Tavern Hostess & Town Well Arcanist
            SpawnTownNPC(maidenPrefab, root.transform, "Innkeeper Cheryl", "Tavern Hostess",
                "Welcome to Emberwatch! Rest your weary feet by the hearth whenever you return.",
                new Vector3(-6.25f, -0.11f, 5.05f), 85f, 2.08f, report);

            SpawnTownNPC(magePrefab, root.transform, "Arcanist Aldous", "Mage Trainer",
                "Watch your Mana reserves, apprentice. Frost Nova will root raiders before they reach you.",
                new Vector3(4.45f, -0.11f, 3.75f), -130f, 2.20f, report);

            // 4. Outpost Road Ranger & Greenhollow Post Ranger
            SpawnTownNPC(scoutPrefab, root.transform, "Scout Sylvan", "Ashwood Ranger",
                "I've tracked Crag Orc outriders and Goblin sneaks all the way to the ruined watchtower.",
                new Vector3(4.75f, -0.11f, 22.8f), -25f, 2.10f, report);

            SpawnTownNPC(scoutPrefab, root.transform, "Outrider Lyra", "Greenhollow Scout",
                "Greenhollow pastures are peaceful for now, though dark riders prowl the southern woods.",
                new Vector3(-22.5f, -0.11f, 159.5f), 135f, 2.10f, report);

            // 5. Upgrade Quartermaster Rowan's visual so he is an armored Quartermaster instead of a clone of Abe
            UpgradeMerchantVisual(ctrlGuard, report);

            // 6. Upgrade existing Road / Camp / Frontier EnemyControllers from low-poly skeletons to smooth Classic WoW Raiders
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            UpgradeExistingEnemiesToClassicWoW(player, ctrlMeleeEnemy, ctrlCasterEnemy, report);

            // 7. Spawn 4 additional smooth-shaded Forest Combat Enemies (Crag Orcs & Watchtower Goblins)
            if (player)
            {
                SpawnWorldEnemy(root.transform, player, "Crag_Orc.fbx", ctrlMeleeEnemy,
                    "forest-orc-1", "Ashen Crag Stalker", new Vector3(15.5f, -0.15f, 52.0f), -60f, 2.04f, 85f, 11f, Color.white, report);
                SpawnWorldEnemy(root.transform, player, "Crag_Orc.fbx", ctrlMeleeEnemy,
                    "forest-orc-2", "Ashen Crag Raider", new Vector3(-16.0f, -0.15f, 68.0f), 40f, 2.04f, 85f, 11f, Color.white, report);
                SpawnWorldEnemy(root.transform, player, "Forest_Goblin.fbx", ctrlMeleeEnemy,
                    "tower-goblin-1", "Ashen Goblin Ambusher", new Vector3(14.0f, -0.15f, 92.0f), -45f, 1.38f, 65f, 9f, Color.white, report);
                SpawnWorldEnemy(root.transform, player, "Forest_Goblin.fbx", ctrlMeleeEnemy,
                    "tower-goblin-2", "Ashen Goblin Sneak", new Vector3(11.5f, -0.15f, 96.0f), -110f, 1.38f, 65f, 9f, Color.white, report);
            }

            // 8. Build Scarlet Crypts Cathedral Portal & Redridge Frontier Horde Border Landmarks
            BuildFrontierLandmarks(root.transform, player, ctrlGuard, ctrlMeleeEnemy, ctrlCasterEnemy, report);
        }

        static void BuildFrontierLandmarks(Transform root, PlayerController player, AnimatorController ctrlGuard, AnimatorController ctrlMelee, AnimatorController ctrlCaster, StringBuilder report)
        {
            // --- 1. Scarlet Crypts Landmark (Cathedral Dungeon Portal) ---
            var oldCrypt = GameObject.Find("06 - Scarlet Crypts Landmark");
            if (oldCrypt) UnityEngine.Object.DestroyImmediate(oldCrypt);

            var cryptRoot = new GameObject("06 - Scarlet Crypts Landmark");
            cryptRoot.transform.SetParent(root, false);
            Vector3 cryptPos = new Vector3(-55.0f, 0f, 215.0f);
            float cryptGroundY = SampleEnvironmentGroundY(cryptPos, -0.1f);
            cryptRoot.transform.position = new Vector3(cryptPos.x, cryptGroundY, cryptPos.z);

            var cryptLandmark = cryptRoot.AddComponent<Landmark>();
            cryptLandmark.landmarkId = "scarlet-crypts";
            cryptLandmark.displayName = "Scarlet Crypts (Cathedral Dungeon)";

            var stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Plaster.mat");
            var cryptArchL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cryptArchL.name = "Cathedral Arch Left Pillar";
            cryptArchL.transform.SetParent(cryptRoot.transform, false);
            cryptArchL.transform.localPosition = new Vector3(-2.6f, 2.5f, 0f);
            cryptArchL.transform.localScale = new Vector3(0.9f, 5.0f, 0.9f);
            if (stoneMat) cryptArchL.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var cryptArchR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cryptArchR.name = "Cathedral Arch Right Pillar";
            cryptArchR.transform.SetParent(cryptRoot.transform, false);
            cryptArchR.transform.localPosition = new Vector3(2.6f, 2.5f, 0f);
            cryptArchR.transform.localScale = new Vector3(0.9f, 5.0f, 0.9f);
            if (stoneMat) cryptArchR.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var cryptArchTop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cryptArchTop.name = "Cathedral Arch Header";
            cryptArchTop.transform.SetParent(cryptRoot.transform, false);
            cryptArchTop.transform.localPosition = new Vector3(0f, 5.2f, 0f);
            cryptArchTop.transform.localScale = new Vector3(6.1f, 0.8f, 1.1f);
            if (stoneMat) cryptArchTop.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var portalQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            portalQuad.name = "Cathedral Crimson Portal";
            portalQuad.transform.SetParent(cryptRoot.transform, false);
            portalQuad.transform.localPosition = new Vector3(0f, 2.4f, 0.05f);
            portalQuad.transform.localScale = new Vector3(4.2f, 4.6f, 1f);
            var portalCol = portalQuad.GetComponent<Collider>();
            if (portalCol) portalCol.isTrigger = true;
            var portalLight = portalQuad.AddComponent<Light>();
            portalLight.type = LightType.Point;
            portalLight.color = new Color(1.0f, 0.18f, 0.22f);
            portalLight.range = 14f;
            portalLight.intensity = 3.5f;

            var entryPortal = portalQuad.AddComponent<DungeonPortal>();
            entryPortal.destination = new Vector3(-400f, -39.0f, 196.0f);
            entryPortal.destinationZoneName = "The Scarlet Crypts";
            entryPortal.isExitPortal = false;

            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Arcanist.prefab");
            SpawnTownNPC(magePrefab, cryptRoot.transform, "Inquisitor Lucian", "Scarlet Guardian",
                "The Cathedral crypts harbor unspeakable horrors. Step through the portal only if your blade is ready!",
                new Vector3(-52.5f, cryptGroundY, 212.5f), -145f, 2.2f, report);

            // --- 2. Redridge Frontier Landmark & Camp Bloodrock (Horde Territory) ---
            var oldRedridge = GameObject.Find("07 - Redridge Frontier Landmark");
            if (oldRedridge) UnityEngine.Object.DestroyImmediate(oldRedridge);

            var redridgeRoot = new GameObject("07 - Redridge Frontier Landmark");
            redridgeRoot.transform.SetParent(root, false);
            Vector3 redridgePos = new Vector3(55.0f, 0f, 215.0f);
            float redGroundY = SampleEnvironmentGroundY(redridgePos, -0.1f);
            redridgeRoot.transform.position = new Vector3(redridgePos.x, redGroundY, redridgePos.z);

            var redridgeLandmark = redridgeRoot.AddComponent<Landmark>();
            redridgeLandmark.landmarkId = "redridge-frontier";
            redridgeLandmark.displayName = "Redridge Frontier (Horde Border)";

            var woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            for (int p = -2; p <= 2; p++)
            {
                if (p == 0) continue;
                var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = $"Horde Palisade Spike {p}";
                spike.transform.SetParent(redridgeRoot.transform, false);
                spike.transform.localPosition = new Vector3(p * 2.2f, 1.6f, 0f);
                spike.transform.localScale = new Vector3(0.35f, 1.8f, 0.35f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, p > 0 ? -12f : 12f);
                if (woodMat) spike.GetComponent<Renderer>().sharedMaterial = woodMat;
            }

            var fireObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fireObj.name = "Frontier Campfire";
            fireObj.transform.SetParent(redridgeRoot.transform, false);
            fireObj.transform.localPosition = new Vector3(-3.2f, 0.25f, 2.0f);
            fireObj.transform.localScale = new Vector3(0.8f, 0.4f, 0.8f);
            var fireLight = fireObj.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1.0f, 0.58f, 0.12f);
            fireLight.range = 10f;
            fireLight.intensity = 2.8f;

            var knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_KnightArms.prefab");
            SpawnTownNPC(knightPrefab, redridgeRoot.transform, "Scout Grimjaw", "Horde Outrunner",
                "Halt, Alliance! These red canyons lead into Durotar. Keep your weapons sheathed if you wish to pass.",
                new Vector3(53.0f, redGroundY, 213.0f), 135f, 2.25f, report);

            // Warlord Gorgash (Bloodrock Chieftain)
            SpawnTownNPC(knightPrefab, redridgeRoot.transform, "Warlord Gorgash", "Bloodrock Chieftain",
                "Victory or death! The Ashen Pact will claim these red canyons. Sharpen your blade, warrior!",
                new Vector3(56.0f, redGroundY, 218.0f), -45f, 2.30f, report);

            // Horde Armorer Thrak
            var smithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Blacksmith.prefab");
            SpawnTownNPC(smithPrefab, redridgeRoot.transform, "Thrak", "Horde Armorer",
                "Lok'tar! I forge steel strong enough to withstand Alliance blades.",
                new Vector3(50.5f, redGroundY, 217.5f), 70f, 2.15f, report);

            if (player)
            {
                SpawnWorldEnemy(redridgeRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "redridge-orc-sentry", "Crag Orc Frontier Guard", new Vector3(58.8f, redGroundY, 216.0f), -135f, 2.12f, 95f, 12f, Color.white, report);
            }

            report.AppendLine("  [Landmarks] Built Scarlet Crypts Cathedral Portal (-55, 215) and Redridge Frontier Camp Bloodrock (55, 215).");

            // --- 3. Build Subterranean Scarlet Crypts Cathedral Dungeon Interior ---
            BuildCathedralDungeonInterior(root, player, ctrlMelee, ctrlCaster, report);

            // --- 4. Build High-Altitude Wyrmcrest Roost (Apex Mountain Peak) ---
            BuildWyrmcrestSummitRoost(root, player, ctrlMelee, ctrlCaster, report);

            // --- 5. Build Highspire Keep (Alliance Capital City) ---
            BuildHighspireKeepCapital(root, player, ctrlGuard, report);

            // --- 6. Build Gor'Kresh Stronghold (Horde Capital City) ---
            BuildGorkreshStrongholdCapital(root, player, ctrlGuard, report);

            // --- 7. Build Racial Starting Precincts (Dwarf, High Elf, Undead, Goblin) ---
            BuildRacialStartingPrecincts(root, player, ctrlGuard, ctrlMelee, ctrlCaster, report);

            // --- 8. Configure 6-Node World Flight Master Network ---
            BuildFlightMasterNetwork(root, report);

            // --- 9. Spawn Herbalism & Mining Gathering Nodes across the 6 zones ---
            BuildGatheringNodes(root, report);

            // --- 10. Build Volcanic Crater & World Boss Gorgonax the Worldbreaker ---
            BuildWorldBossArena(root, player, ctrlMelee, report);
        }

        static void BuildCathedralDungeonInterior(Transform root, PlayerController player, AnimatorController ctrlMelee, AnimatorController ctrlCaster, StringBuilder report)
        {
            var oldInterior = GameObject.Find("12 - The Scarlet Crypts Dungeon");
            if (oldInterior) UnityEngine.Object.DestroyImmediate(oldInterior);

            var dungeonRoot = new GameObject("12 - The Scarlet Crypts Dungeon");
            dungeonRoot.transform.SetParent(root, false);
            Vector3 origin = new Vector3(-400f, -40f, 200f);
            dungeonRoot.transform.position = origin;

            var stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Plaster.mat");
            var woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");

            // 1. Grand Cathedral Floor (50m long x 20m wide)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Cathedral Stone Floor";
            floor.transform.SetParent(dungeonRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.5f, 20f);
            floor.transform.localScale = new Vector3(20f, 1f, 52f);
            if (stoneMat) floor.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 2. Cathedral Vaulted Ceiling (11m high)
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.name = "Cathedral Vaulted Ceiling";
            ceiling.transform.SetParent(dungeonRoot.transform, false);
            ceiling.transform.localPosition = new Vector3(0f, 11.5f, 20f);
            ceiling.transform.localScale = new Vector3(20f, 1f, 52f);
            if (woodMat) ceiling.GetComponent<Renderer>().sharedMaterial = woodMat;

            // 3. Side Walls
            var wallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallL.name = "Cathedral Left Wall";
            wallL.transform.SetParent(dungeonRoot.transform, false);
            wallL.transform.localPosition = new Vector3(-10f, 5.5f, 20f);
            wallL.transform.localScale = new Vector3(1f, 11f, 52f);
            if (stoneMat) wallL.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var wallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallR.name = "Cathedral Right Wall";
            wallR.transform.SetParent(dungeonRoot.transform, false);
            wallR.transform.localPosition = new Vector3(10f, 5.5f, 20f);
            wallR.transform.localScale = new Vector3(1f, 11f, 52f);
            if (stoneMat) wallR.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var wallBack = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallBack.name = "Cathedral Entrance Back Wall";
            wallBack.transform.SetParent(dungeonRoot.transform, false);
            wallBack.transform.localPosition = new Vector3(0f, 5.5f, -6f);
            wallBack.transform.localScale = new Vector3(20f, 11f, 1f);
            if (stoneMat) wallBack.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var wallFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallFront.name = "Cathedral Altar Back Wall";
            wallFront.transform.SetParent(dungeonRoot.transform, false);
            wallFront.transform.localPosition = new Vector3(0f, 5.5f, 46f);
            wallFront.transform.localScale = new Vector3(20f, 11f, 1f);
            if (stoneMat) wallFront.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 4. Gothic Crypt Nave Columns (4 pairs = 8 pillars)
            for (int i = 0; i < 4; i++)
            {
                float pz = 4f + i * 9.5f;
                var colL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                colL.name = $"Cathedral Column L{i}";
                colL.transform.SetParent(dungeonRoot.transform, false);
                colL.transform.localPosition = new Vector3(-5.5f, 5.5f, pz);
                colL.transform.localScale = new Vector3(1.2f, 5.5f, 1.2f);
                if (stoneMat) colL.GetComponent<Renderer>().sharedMaterial = stoneMat;

                var colR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                colR.name = $"Cathedral Column R{i}";
                colR.transform.SetParent(dungeonRoot.transform, false);
                colR.transform.localPosition = new Vector3(5.5f, 5.5f, pz);
                colR.transform.localScale = new Vector3(1.2f, 5.5f, 1.2f);
                if (stoneMat) colR.GetComponent<Renderer>().sharedMaterial = stoneMat;

                var torchL = new GameObject($"Cathedral Torch L{i}");
                torchL.transform.SetParent(dungeonRoot.transform, false);
                torchL.transform.localPosition = new Vector3(-4.6f, 3.2f, pz);
                var ltL = torchL.AddComponent<Light>();
                ltL.type = LightType.Point;
                ltL.color = new Color(1f, 0.55f, 0.18f);
                ltL.range = 10f;
                ltL.intensity = 2.5f;

                var torchR = new GameObject($"Cathedral Torch R{i}");
                torchR.transform.SetParent(dungeonRoot.transform, false);
                torchR.transform.localPosition = new Vector3(4.6f, 3.2f, pz);
                var ltR = torchR.AddComponent<Light>();
                ltR.type = LightType.Point;
                ltR.color = new Color(1f, 0.55f, 0.18f);
                ltR.range = 10f;
                ltR.intensity = 2.5f;
            }

            // 5. Elevated Altar Dais & Table
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dais.name = "Altar Dais";
            dais.transform.SetParent(dungeonRoot.transform, false);
            dais.transform.localPosition = new Vector3(0f, 0.4f, 38f);
            dais.transform.localScale = new Vector3(12f, 0.8f, 10f);
            if (stoneMat) dais.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var altar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            altar.name = "Scarlet Altar Table";
            altar.transform.SetParent(dungeonRoot.transform, false);
            altar.transform.localPosition = new Vector3(0f, 1.3f, 40f);
            altar.transform.localScale = new Vector3(4.5f, 1.0f, 1.8f);
            if (stoneMat) altar.GetComponent<Renderer>().sharedMaterial = stoneMat;

            var altarLight = new GameObject("Altar Crimson Light");
            altarLight.transform.SetParent(dungeonRoot.transform, false);
            altarLight.transform.localPosition = new Vector3(0f, 4.5f, 40f);
            var aL = altarLight.AddComponent<Light>();
            aL.type = LightType.Point;
            aL.color = new Color(1.0f, 0.22f, 0.30f);
            aL.range = 16f;
            aL.intensity = 3.8f;

            // 6. Altar Dungeon Treasure Chest (Awards the Epic Inquisitor's Sunblade!)
            var chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestObj.name = "Cathedral Altar Treasure Chest";
            chestObj.transform.SetParent(dungeonRoot.transform, false);
            chestObj.transform.localPosition = new Vector3(0f, 1.1f, 42.5f);
            chestObj.transform.localScale = new Vector3(1.3f, 0.9f, 0.9f);
            if (woodMat) chestObj.GetComponent<Renderer>().sharedMaterial = woodMat;
            var lootComp = chestObj.AddComponent<LootChest>();
            lootComp.chestId = "cathedral-altar-chest";
            lootComp.gold = 75;
            lootComp.experience = 150;

            // 7. Exit Portal (Blue Swirling Gateway back to Duskwood)
            var exitQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            exitQuad.name = "Cathedral Exit Portal";
            exitQuad.transform.SetParent(dungeonRoot.transform, false);
            exitQuad.transform.localPosition = new Vector3(0f, 2.5f, -5.2f);
            exitQuad.transform.localScale = new Vector3(3.6f, 4.4f, 1f);
            var exitCol = exitQuad.GetComponent<Collider>();
            if (exitCol) exitCol.isTrigger = true;
            var exitLight = exitQuad.AddComponent<Light>();
            exitLight.type = LightType.Point;
            exitLight.color = new Color(0.25f, 0.65f, 1.0f);
            exitLight.range = 12f;
            exitLight.intensity = 3.2f;

            var exitPortal = exitQuad.AddComponent<DungeonPortal>();
            exitPortal.destination = new Vector3(-55.0f, -0.1f, 208.0f);
            exitPortal.isExitPortal = true;
            exitPortal.returnZoneName = "Duskwood Forest";

            // 8. Dungeon Bosses & Mobs
            if (player)
            {
                // Boss 1: High Inquisitor Malakar (Level 6 Boss, 280 HP)
                SpawnWorldEnemy(dungeonRoot.transform, player, "Knight_Arms.fbx", ctrlMelee,
                    "boss-malakar", "High Inquisitor Malakar", origin + new Vector3(-2.8f, 0.8f, 36f), 180f, 2.35f, 280f, 18f, new Color(0.95f, 0.45f, 0.45f), report);

                // Boss 2: Dread Abbot Vael (Level 7 Boss, 350 HP)
                SpawnWorldEnemy(dungeonRoot.transform, player, "Arcanist_Mage.fbx", ctrlCaster,
                    "boss-abbot-vael", "Dread Abbot Vael", origin + new Vector3(2.8f, 0.8f, 36f), 180f, 2.10f, 350f, 22f, new Color(0.65f, 0.35f, 0.75f), report);

                // Trash Mobs along the cathedral nave
                SpawnWorldEnemy(dungeonRoot.transform, player, "Knight_Arms.fbx", ctrlMelee,
                    "dungeon-sentry-1", "Scarlet Inquisitor Sentry", origin + new Vector3(-3.2f, 0f, 12f), 140f, 1.95f, 95f, 12f, Color.white, report);
                SpawnWorldEnemy(dungeonRoot.transform, player, "Arcanist_Mage.fbx", ctrlCaster,
                    "dungeon-hexer-1", "Scarlet Blood Cleric", origin + new Vector3(3.2f, 0f, 12f), -140f, 1.88f, 85f, 14f, Color.white, report);
                SpawnWorldEnemy(dungeonRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "dungeon-sentry-2", "Scarlet Crypt Defiler", origin + new Vector3(-3.2f, 0f, 25f), 160f, 2.05f, 110f, 13f, Color.white, report);
                SpawnWorldEnemy(dungeonRoot.transform, player, "Forest_Goblin.fbx", ctrlMelee,
                    "dungeon-ghoul-1", "Crypt Scavenger", origin + new Vector3(3.2f, 0f, 25f), -160f, 1.40f, 75f, 10f, Color.white, report);
            }

            report.AppendLine("  [Cathedral Dungeon] Built The Scarlet Crypts interior instance at (-400, -40, 200) with High Inquisitor Malakar & Dread Abbot Vael!");
        }

        static void BuildWyrmcrestSummitRoost(Transform root, PlayerController player, AnimatorController ctrlMelee, AnimatorController ctrlCaster, StringBuilder report)
        {
            var oldRoost = GameObject.Find("13 - Wyrmcrest Summit Roost");
            if (oldRoost) UnityEngine.Object.DestroyImmediate(oldRoost);

            var roostRoot = new GameObject("13 - Wyrmcrest Summit Roost");
            roostRoot.transform.SetParent(root, false);
            Vector3 origin = new Vector3(0f, 48.0f, 225.0f);
            roostRoot.transform.position = origin;

            var landmark = roostRoot.AddComponent<Landmark>();
            landmark.landmarkId = "wyrmcrest-summit";
            landmark.displayName = "Wyrmcrest Summit (Apex Dragon Roost)";

            var stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Plaster.mat");
            var darkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");

            // Grand Summit Altar Platform
            var dais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dais.name = "Summit Dais Platform";
            dais.transform.SetParent(roostRoot.transform, false);
            dais.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            dais.transform.localScale = new Vector3(32f, 1.2f, 32f);
            if (stoneMat) dais.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 8 Obsidian Pillars around perimeter
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI * 2f / 8f;
                float px = Mathf.Sin(ang) * 14f;
                float pz = Mathf.Cos(ang) * 14f;

                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = $"Summit Pillar {i}";
                pillar.transform.SetParent(roostRoot.transform, false);
                pillar.transform.localPosition = new Vector3(px, 3.5f, pz);
                pillar.transform.localScale = new Vector3(1.2f, 3.5f, 1.2f);
                if (darkMat) pillar.GetComponent<Renderer>().sharedMaterial = darkMat;

                // Fire brazier atop pillar
                var brazier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                brazier.name = $"Brazier Flame {i}";
                brazier.transform.SetParent(pillar.transform, false);
                brazier.transform.localPosition = new Vector3(0f, 1.1f, 0f);
                brazier.transform.localScale = new Vector3(0.9f, 0.5f, 0.9f);
                var bLight = brazier.AddComponent<Light>();
                bLight.type = LightType.Point;
                bLight.color = new Color(1.0f, 0.45f, 0.12f);
                bLight.range = 10f;
                bLight.intensity = 2.2f;
            }

            // Central Magma Rift / Roost Nest
            var rift = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rift.name = "Molten Roost Pit";
            rift.transform.SetParent(roostRoot.transform, false);
            rift.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            rift.transform.localScale = new Vector3(9f, 0.15f, 9f);
            var riftLight = rift.AddComponent<Light>();
            riftLight.type = LightType.Point;
            riftLight.color = new Color(1.0f, 0.25f, 0.05f);
            riftLight.range = 22f;
            riftLight.intensity = 4.5f;

            // Dragon Hoard Treasure Cache
            var chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chestObj.name = "Wyrmcrest Hoard Chest";
            chestObj.transform.SetParent(roostRoot.transform, false);
            chestObj.transform.localPosition = new Vector3(0f, 0.6f, 8.5f);
            chestObj.transform.localScale = new Vector3(2.2f, 1.2f, 1.4f);
            var chest = chestObj.AddComponent<LootChest>();
            chest.chestId = "wyrmcrest-hoard-chest";
            var chestLight = chestObj.AddComponent<Light>();
            chestLight.type = LightType.Point;
            chestLight.color = new Color(1f, 0.8f, 0.2f);
            chestLight.range = 8f;
            chestLight.intensity = 2.5f;

            // Spawn Apex Boss: Vermithrax the Ember Sovereign (Lv 8 Boss, 650 HP)
            if (player)
            {
                SpawnWorldEnemy(roostRoot.transform, player, "Knight_Arms.fbx", ctrlMelee,
                    "vermithrax-boss", "Vermithrax the Ember Sovereign", origin + new Vector3(0f, 0.1f, 2.5f), 180f, 3.2f, 650f, 24f, new Color(1f, 0.35f, 0.1f), report);

                // 2 Hatchling Drakes (Lv 5)
                SpawnWorldEnemy(roostRoot.transform, player, "Forest_Goblin.fbx", ctrlMelee,
                    "wyrmcrest-hatchling-1", "Ember Hatchling", origin + new Vector3(-6.5f, 0.1f, -1.0f), 120f, 1.55f, 160f, 13f, new Color(1f, 0.5f, 0.15f), report);
                SpawnWorldEnemy(roostRoot.transform, player, "Forest_Goblin.fbx", ctrlMelee,
                    "wyrmcrest-hatchling-2", "Ember Hatchling", origin + new Vector3(6.5f, 0.1f, -1.0f), -120f, 1.55f, 160f, 13f, new Color(1f, 0.5f, 0.15f), report);
            }

            report.AppendLine("  [Apex Roost] Built Wyrmcrest Summit Roost at (0, 48, 225) with Apex Boss Vermithrax the Ember Sovereign & Dragon Hoard Chest!");
        }

        static void BuildHighspireKeepCapital(Transform root, PlayerController player, AnimatorController ctrlGuard, StringBuilder report)
        {
            var oldKeep = GameObject.Find("08 - Highspire Keep Alliance Capital");
            if (oldKeep) UnityEngine.Object.DestroyImmediate(oldKeep);

            var keepRoot = new GameObject("08 - Highspire Keep Alliance Capital");
            keepRoot.transform.SetParent(root, false);
            Vector3 origin = new Vector3(-45.0f, 0f, -20.0f);
            float groundY = SampleEnvironmentGroundY(origin, -0.1f);
            keepRoot.transform.position = new Vector3(origin.x, groundY, origin.z);

            var landmark = keepRoot.AddComponent<Landmark>();
            landmark.landmarkId = "highspire-keep";
            landmark.displayName = "Highspire Keep (Alliance Capital)";

            var stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Plaster.mat") ??
                           AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_Plaster.mat");
            var brickMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Brick.mat");
            var goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Beacon gold.mat");

            // 1. Keep Courtyard Stone Base (32m x 1m x 32m)
            var courtyard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            courtyard.name = "Highspire Courtyard Plaza";
            courtyard.transform.SetParent(keepRoot.transform, false);
            courtyard.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            courtyard.transform.localScale = new Vector3(32f, 0.8f, 32f);
            if (stoneMat) courtyard.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // 2. Twin Bastion Towers flanking North Gate
            for (int side = -1; side <= 1; side += 2)
            {
                var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tower.name = $"Bastion Tower {(side < 0 ? "West" : "East")}";
                tower.transform.SetParent(keepRoot.transform, false);
                tower.transform.localPosition = new Vector3(side * 11f, 6.0f, 12f);
                tower.transform.localScale = new Vector3(4.2f, 6.0f, 4.2f);
                if (brickMat) tower.GetComponent<Renderer>().sharedMaterial = brickMat;

                // Crenellation parapet ring
                var parapet = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                parapet.name = "Parapet Crown";
                parapet.transform.SetParent(tower.transform, false);
                parapet.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                parapet.transform.localScale = new Vector3(1.15f, 0.15f, 1.15f);
                if (stoneMat) parapet.GetComponent<Renderer>().sharedMaterial = stoneMat;

                // Lantern
                var lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                lantern.name = "Alliance Sky Lantern";
                lantern.transform.SetParent(tower.transform, false);
                lantern.transform.localPosition = new Vector3(0f, 1.35f, 0f);
                lantern.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var lt = lantern.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(0.35f, 0.65f, 1.0f);
                lt.range = 16f;
                lt.intensity = 3.2f;
            }

            // 3. Grand Gate Archway Header
            var gateHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateHeader.name = "Highspire Gate Header";
            gateHeader.transform.SetParent(keepRoot.transform, false);
            gateHeader.transform.localPosition = new Vector3(0f, 9.5f, 12f);
            gateHeader.transform.localScale = new Vector3(18f, 1.6f, 2.5f);
            if (brickMat) gateHeader.GetComponent<Renderer>().sharedMaterial = brickMat;

            // 4. Grand Alliance Blue Banner
            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Royal Alliance Banner";
            banner.transform.SetParent(keepRoot.transform, false);
            banner.transform.localPosition = new Vector3(0f, 6.5f, 12.8f);
            banner.transform.localScale = new Vector3(3.2f, 5.0f, 0.15f);
            var bannerRend = banner.GetComponent<Renderer>();
            if (bannerRend) bannerRend.sharedMaterial = stoneMat;

            // 5. Highspire Alliance Lion Altar in Center Courtyard
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            altar.name = "Lion Crest Pedestal";
            altar.transform.SetParent(keepRoot.transform, false);
            altar.transform.localPosition = new Vector3(0f, 0.6f, -2f);
            altar.transform.localScale = new Vector3(4.5f, 0.6f, 4.5f);
            if (goldMat) altar.GetComponent<Renderer>().sharedMaterial = goldMat;

            // 6. Spawn Highspire Capital NPCs
            var paladinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_PaladinGuard.prefab");
            var knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_KnightArms.prefab");
            var guardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_CastleGuard.prefab");
            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Arcanist.prefab");

            // Grand Duke Ronald (Alliance High Commander)
            SpawnTownNPC(paladinPrefab, keepRoot.transform, "Grand Duke Ronald", "High Commander of the Alliance",
                "For the Alliance! Highspire Keep stands eternal as a bastion of honor and sacred light.",
                new Vector3(-45.0f, groundY, -24.0f), 0f, 2.30f, report);

            // Archmage Karen (Royal Arcanist)
            SpawnTownNPC(magePrefab, keepRoot.transform, "Archmage Karen", "Royal Ley Arcanist",
                "The leylines converge at Highspire. Arcane brilliance shields our kingdom from corruption.",
                new Vector3(-39.0f, groundY, -22.0f), -40f, 2.22f, report);

            // Captain Brian (Royal Guard Commander)
            SpawnTownNPC(knightPrefab, keepRoot.transform, "Captain Brian", "Royal Guard Commander",
                "Keep your weapons primed, recruit. Highspire's defenders never rest.",
                new Vector3(-48.5f, groundY, -14.0f), 150f, 2.25f, report);

            // Gate Sentries
            SpawnTownNPC(guardPrefab, keepRoot.transform, "Highspire Sentry Donald", "Capital Guard",
                "All who enter Highspire Keep are safe under Alliance law.",
                new Vector3(-51.5f, groundY, -8.5f), 170f, 2.20f, report);

            SpawnTownNPC(guardPrefab, keepRoot.transform, "Highspire Sentry Kenneth", "Capital Guard",
                "Honor to you, traveler. Speak with Gryphon Master Darien if you require swift transport.",
                new Vector3(-38.5f, groundY, -8.5f), -170f, 2.20f, report);

            // Auctioneer Fitch (Capital Auction House)
            SpawnTownNPC(guardPrefab, keepRoot.transform, "Auctioneer Fitch", "Auctioneer",
                "Welcome to the Highspire Royal Exchange! All wares guaranteed authentic under Alliance charter.",
                new Vector3(-42.0f, groundY, -21.0f), -35f, 2.20f, report);

            // Highspire Royal Brass Mailbox
            var mailboxObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mailboxObj.name = "Highspire Royal Mailbox";
            mailboxObj.transform.SetParent(keepRoot.transform, false);
            mailboxObj.transform.localPosition = new Vector3(-3.2f, 0.6f, 2.0f);
            mailboxObj.transform.localScale = new Vector3(0.4f, 0.6f, 0.4f);
            if (goldMat) mailboxObj.GetComponent<Renderer>().sharedMaterial = goldMat;
            var boxHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            boxHead.name = "Mailbox Head";
            boxHead.transform.SetParent(mailboxObj.transform, false);
            boxHead.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            boxHead.transform.localScale = new Vector3(1.6f, 1.2f, 2.2f);
            if (goldMat) boxHead.GetComponent<Renderer>().sharedMaterial = goldMat;
            mailboxObj.AddComponent<Mailbox>();

            report.AppendLine("  [Alliance Capital] Highspire Keep constructed at (-45, -20) with Grand Duke Ronald, Archmage Karen, Auctioneer Fitch, Brass Mailbox, and Royal Garrison.");
        }

        static void BuildGorkreshStrongholdCapital(Transform root, PlayerController player, AnimatorController ctrlGuard, StringBuilder report)
        {
            var oldStronghold = GameObject.Find("09 - Gor'Kresh Stronghold Horde Capital");
            if (oldStronghold) UnityEngine.Object.DestroyImmediate(oldStronghold);

            var hordeRoot = new GameObject("09 - Gor'Kresh Stronghold Horde Capital");
            hordeRoot.transform.SetParent(root, false);
            Vector3 origin = new Vector3(85.0f, 0f, 235.0f);
            float groundY = SampleEnvironmentGroundY(origin, 0.5f);
            hordeRoot.transform.position = new Vector3(origin.x, groundY, origin.z);

            var landmark = hordeRoot.AddComponent<Landmark>();
            landmark.landmarkId = "gorkresh-stronghold";
            landmark.displayName = "Gor'Kresh Stronghold (Horde Capital)";

            var timberMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            var redMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Faction crimson.mat");
            var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Nature_Rock.mat");

            // 1. Heavy Bastion Dirt & Timber Base (32m x 1m x 32m)
            var baseFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseFloor.name = "Gor'Kresh War Ground";
            baseFloor.transform.SetParent(hordeRoot.transform, false);
            baseFloor.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            baseFloor.transform.localScale = new Vector3(32f, 0.8f, 32f);
            if (timberMat) baseFloor.GetComponent<Renderer>().sharedMaterial = timberMat;

            // 2. Heavy Spiked War Towers flanking South Gate
            for (int side = -1; side <= 1; side += 2)
            {
                var tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tower.name = $"Horde War Tower {(side < 0 ? "West" : "East")}";
                tower.transform.SetParent(hordeRoot.transform, false);
                tower.transform.localPosition = new Vector3(side * 11f, 5.5f, -11f);
                tower.transform.localScale = new Vector3(3.8f, 5.5f, 3.8f);
                if (rockMat) tower.GetComponent<Renderer>().sharedMaterial = rockMat;

                // Spikes atop tower
                for (int sp = 0; sp < 4; sp++)
                {
                    float a = sp * Mathf.PI * 0.5f;
                    var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    spike.name = "Tower Spike";
                    spike.transform.SetParent(tower.transform, false);
                    spike.transform.localPosition = new Vector3(Mathf.Sin(a) * 0.45f, 1.1f, Mathf.Cos(a) * 0.45f);
                    spike.transform.localScale = new Vector3(0.12f, 0.4f, 0.12f);
                    spike.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 15f, 0f, Mathf.Cos(a) * 15f);
                    if (timberMat) spike.GetComponent<Renderer>().sharedMaterial = timberMat;
                }

                // Fire brazier
                var brazier = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                brazier.name = "Horde War Flame";
                brazier.transform.SetParent(tower.transform, false);
                brazier.transform.localPosition = new Vector3(0f, 1.25f, 0f);
                brazier.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
                var lt = brazier.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1.0f, 0.35f, 0.08f);
                lt.range = 18f;
                lt.intensity = 3.8f;
            }

            // 3. Heavy Timber Gate Header
            var gateLog = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gateLog.name = "Horde Gate Log Header";
            gateLog.transform.SetParent(hordeRoot.transform, false);
            gateLog.transform.localPosition = new Vector3(0f, 8.5f, -11f);
            gateLog.transform.localScale = new Vector3(18f, 1.8f, 2.2f);
            if (timberMat) gateLog.GetComponent<Renderer>().sharedMaterial = timberMat;

            // 4. Grand Crimson Horde War Banner
            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Horde War Banner";
            banner.transform.SetParent(hordeRoot.transform, false);
            banner.transform.localPosition = new Vector3(0f, 5.5f, -11.8f);
            banner.transform.localScale = new Vector3(3.2f, 4.8f, 0.15f);
            var bannerRend = banner.GetComponent<Renderer>();
            if (bannerRend && redMat) bannerRend.sharedMaterial = redMat;

            // 5. Central Warchief Fire Pit
            var firePit = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            firePit.name = "Warchief Great Hearth";
            firePit.transform.SetParent(hordeRoot.transform, false);
            firePit.transform.localPosition = new Vector3(0f, 0.4f, 2f);
            firePit.transform.localScale = new Vector3(4.5f, 0.4f, 4.5f);
            if (rockMat) firePit.GetComponent<Renderer>().sharedMaterial = rockMat;

            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Ancestral Flame Core";
            flame.transform.SetParent(firePit.transform, false);
            flame.transform.localPosition = new Vector3(0f, 0.7f, 0f);
            flame.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            var flameLight = flame.AddComponent<Light>();
            flameLight.type = LightType.Point;
            flameLight.color = new Color(1.0f, 0.42f, 0.10f);
            flameLight.range = 20f;
            flameLight.intensity = 4.5f;

            // 6. Spawn Gor'Kresh Capital NPCs
            var knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_KnightArms.prefab");
            var smithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Blacksmith.prefab");
            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Arcanist.prefab");

            // Overlord Mor'gok (Horde High Warlord)
            SpawnTownNPC(knightPrefab, hordeRoot.transform, "Overlord Mor'gok", "High Warlord of the Horde",
                "Blood and Thunder! Gor'Kresh will crush all who dare trespass on Horde territory! Lok'tar Ogar!",
                new Vector3(85.0f, groundY, 239.0f), 180f, 2.32f, report);

            // Shaman Kelsey (Ancestral Flame Seer)
            SpawnTownNPC(magePrefab, hordeRoot.transform, "Shaman Kelsey", "Ancestral Flame Seer",
                "The spirits of the red canyons burn fiercely beneath our feet. Walk with the earth and sky, warrior.",
                new Vector3(79.0f, groundY, 237.0f), 40f, 2.22f, report);

            // Korgath the Brute (Horde Warmaster)
            SpawnTownNPC(knightPrefab, hordeRoot.transform, "Korgath the Brute", "Horde Warmaster",
                "Only the strong endure these canyons. Test your mettle against the beasts of the desert!",
                new Vector3(81.0f, groundY, 228.0f), 30f, 2.28f, report);

            // Iron Blacksmith Garok
            SpawnTownNPC(smithPrefab, hordeRoot.transform, "Blacksmith Garok", "Horde Weaponsmith",
                "A blunt axe is as good as dead. Keep your edge razor-sharp!",
                new Vector3(91.0f, groundY, 238.0f), -50f, 2.20f, report);

            // Auctioneer Drazzle (Capital Auction House)
            SpawnTownNPC(smithPrefab, hordeRoot.transform, "Auctioneer Drazzle", "Auctioneer",
                "Looking to buy, looking to sell? The Bilgewater Cartel takes only a modest cut, friend!",
                new Vector3(88.0f, groundY, 236.0f), -45f, 2.15f, report);

            // Gor'Kresh Spiked War Mailbox
            var hordeMailbox = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            hordeMailbox.name = "Gor'Kresh War Mailbox";
            hordeMailbox.transform.SetParent(hordeRoot.transform, false);
            hordeMailbox.transform.localPosition = new Vector3(-3.2f, 0.6f, -3.0f);
            hordeMailbox.transform.localScale = new Vector3(0.45f, 0.6f, 0.45f);
            if (timberMat) hordeMailbox.GetComponent<Renderer>().sharedMaterial = timberMat;
            var hordeBoxHead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hordeBoxHead.name = "Mailbox Head";
            hordeBoxHead.transform.SetParent(hordeMailbox.transform, false);
            hordeBoxHead.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            hordeBoxHead.transform.localScale = new Vector3(1.7f, 1.2f, 2.2f);
            if (redMat) hordeBoxHead.GetComponent<Renderer>().sharedMaterial = redMat;
            hordeMailbox.AddComponent<Mailbox>();

            report.AppendLine("  [Horde Capital] Gor'Kresh Stronghold constructed at (85, 235) with Overlord Mor'gok, Shaman Kelsey, Auctioneer Drazzle, War Mailbox, and Warchief War Hearth.");
        }

        static void BuildRacialStartingPrecincts(Transform root, PlayerController player, AnimatorController ctrlGuard, AnimatorController ctrlMelee, AnimatorController ctrlCaster, StringBuilder report)
        {
            // Destroy legacy roots if present
            var oldDwarf = GameObject.Find("16 - Dwarf Starter Frostpeak Foothills");
            if (oldDwarf) UnityEngine.Object.DestroyImmediate(oldDwarf);

            var oldElf = GameObject.Find("17 - High Elf Starter Silverglen Sanctum");
            if (oldElf) UnityEngine.Object.DestroyImmediate(oldElf);

            var oldUndead = GameObject.Find("18 - Undead Starter Deathknell Hollow");
            if (oldUndead) UnityEngine.Object.DestroyImmediate(oldUndead);

            var oldGoblin = GameObject.Find("19 - Goblin Starter Rustwater Scrapworks");
            if (oldGoblin) UnityEngine.Object.DestroyImmediate(oldGoblin);

            // Common materials
            var stoneMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Plaster.mat") ??
                           AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_Plaster.mat");
            var brickMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_Brick.mat");
            var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Nature_Rock.mat");
            var timberMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            var glowMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Runestone glow.mat");
            var cryptMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Crypt paving.mat") ?? stoneMat;
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Willow brook water.mat") ?? glowMat;
            var redMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Faction crimson.mat");
            var metalMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/FantasyProps_MI_Trim_Metal.mat") ?? rockMat;

            // Common NPC prefabs
            var smithPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Blacksmith.prefab");
            var knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_KnightArms.prefab");
            var scoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_RangerScout.prefab");
            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Arcanist.prefab");

            // =========================================================================
            // 1. DWARF STARTER: Frostpeak Foothills (20, 2, -15)
            // =========================================================================
            var dwarfRoot = new GameObject("16 - Dwarf Starter Frostpeak Foothills");
            dwarfRoot.transform.SetParent(root, false);
            Vector3 dwarfPos = new Vector3(20.0f, 0f, -15.0f);
            float dwarfGroundY = SampleEnvironmentGroundY(dwarfPos, -0.1f);
            dwarfRoot.transform.position = new Vector3(dwarfPos.x, dwarfGroundY, dwarfPos.z);

            var dwarfLandmark = dwarfRoot.AddComponent<Landmark>();
            dwarfLandmark.landmarkId = "dwarf-starter-frostpeak";
            dwarfLandmark.displayName = "Frostpeak Foothills (Dwarven Haven)";

            // Dwarven Forge Stone Platform
            var dwarfFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dwarfFloor.name = "Dwarven Forge Platform";
            dwarfFloor.transform.SetParent(dwarfRoot.transform, false);
            dwarfFloor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            dwarfFloor.transform.localScale = new Vector3(12f, 0.4f, 12f);
            if (rockMat) dwarfFloor.GetComponent<Renderer>().sharedMaterial = rockMat;

            // Great Stone Hearth & Forge Furnace
            var dwarfForge = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dwarfForge.name = "Great Dwarven Furnace";
            dwarfForge.transform.SetParent(dwarfRoot.transform, false);
            dwarfForge.transform.localPosition = new Vector3(0f, 1.4f, 4.0f);
            dwarfForge.transform.localScale = new Vector3(3.2f, 2.8f, 2.2f);
            if (brickMat) dwarfForge.GetComponent<Renderer>().sharedMaterial = brickMat;

            // Forge Coals & Fire Glow
            var forgeCoals = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            forgeCoals.name = "Molten Forge Core";
            forgeCoals.transform.SetParent(dwarfForge.transform, false);
            forgeCoals.transform.localPosition = new Vector3(0f, -0.2f, 0.45f);
            forgeCoals.transform.localScale = new Vector3(0.7f, 0.5f, 0.5f);
            var forgeLight = forgeCoals.AddComponent<Light>();
            forgeLight.type = LightType.Point;
            forgeLight.color = new Color(1.0f, 0.52f, 0.12f);
            forgeLight.range = 14f;
            forgeLight.intensity = 3.6f;

            // Iron Anvil
            var dwarfAnvil = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dwarfAnvil.name = "Mountain Great Anvil";
            dwarfAnvil.transform.SetParent(dwarfRoot.transform, false);
            dwarfAnvil.transform.localPosition = new Vector3(0f, 0.45f, 1.8f);
            dwarfAnvil.transform.localScale = new Vector3(1.1f, 0.9f, 1.6f);
            if (metalMat) dwarfAnvil.GetComponent<Renderer>().sharedMaterial = metalMat;

            // Dwarven Ale Barrels
            for (int b = 0; b < 3; b++)
            {
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                barrel.name = $"Dwarven Ale Cask {b + 1}";
                barrel.transform.SetParent(dwarfRoot.transform, false);
                barrel.transform.localPosition = new Vector3(-3.4f + (b * 1.0f), 0.5f, 3.2f);
                barrel.transform.localScale = new Vector3(0.7f, 0.5f, 0.7f);
                if (timberMat) barrel.GetComponent<Renderer>().sharedMaterial = timberMat;
            }

            // Dwarven NPCs
            SpawnTownNPC(knightPrefab, dwarfRoot.transform, "Thane Baelor Ironbeard", "Dwarven Thane",
                "Khaz Modan! Keep your beard out of the forge fire, recruit, and let's see if your hammer arm is true.",
                new Vector3(20.0f, dwarfGroundY, -13.2f), 180f, 2.15f, report);

            SpawnTownNPC(smithPrefab, dwarfRoot.transform, "Gundrak Stonehammer", "Master Armorer",
                "Cold iron and mountain ale make a dwarf stout. Need your battle armor reinforced?",
                new Vector3(17.5f, dwarfGroundY, -15.5f), 65f, 2.10f, report);

            if (player)
            {
                SpawnWorldEnemy(dwarfRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "frostpeak-trogg-1", "Frostpeak Trogg Scout", new Vector3(28.0f, dwarfGroundY, -22.0f), -45f, 1.75f, 75f, 7f, new Color(0.8f, 0.9f, 1.0f), report);
                SpawnWorldEnemy(dwarfRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "frostpeak-trogg-2", "Frostpeak Trogg Scout", new Vector3(24.5f, dwarfGroundY, -26.0f), 30f, 1.75f, 75f, 7f, new Color(0.8f, 0.9f, 1.0f), report);
            }

            // =========================================================================
            // 2. HIGH ELF STARTER: Silverglen Sanctum (-25, 0, 15)
            // =========================================================================
            var elfRoot = new GameObject("17 - High Elf Starter Silverglen Sanctum");
            elfRoot.transform.SetParent(root, false);
            Vector3 elfPos = new Vector3(-25.0f, 0f, 15.0f);
            float elfGroundY = SampleEnvironmentGroundY(elfPos, -0.1f);
            elfRoot.transform.position = new Vector3(elfPos.x, elfGroundY, elfPos.z);

            var elfLandmark = elfRoot.AddComponent<Landmark>();
            elfLandmark.landmarkId = "elf-starter-silverglen";
            elfLandmark.displayName = "Silverglen Sanctum (Elven Grove)";

            // Elven Circular Moonstone Dais
            var elfDais = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            elfDais.name = "Moonstone Dais";
            elfDais.transform.SetParent(elfRoot.transform, false);
            elfDais.transform.localPosition = new Vector3(0f, -0.18f, 0f);
            elfDais.transform.localScale = new Vector3(12f, 0.36f, 12f);
            if (stoneMat) elfDais.GetComponent<Renderer>().sharedMaterial = stoneMat;

            // Central Moonwell Fountain Ring & Pool
            var moonwellRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            moonwellRing.name = "Moonwell Stone Basin";
            moonwellRing.transform.SetParent(elfRoot.transform, false);
            moonwellRing.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            moonwellRing.transform.localScale = new Vector3(4.4f, 0.6f, 4.4f);
            if (brickMat) moonwellRing.GetComponent<Renderer>().sharedMaterial = brickMat;

            var moonwellWater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            moonwellWater.name = "Moonwell Shimmering Water";
            moonwellWater.transform.SetParent(moonwellRing.transform, false);
            moonwellWater.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            moonwellWater.transform.localScale = new Vector3(0.82f, 0.2f, 0.82f);
            if (waterMat) moonwellWater.GetComponent<Renderer>().sharedMaterial = waterMat;

            var moonLight = moonwellWater.AddComponent<Light>();
            moonLight.type = LightType.Point;
            moonLight.color = new Color(0.25f, 0.78f, 1.0f);
            moonLight.range = 15f;
            moonLight.intensity = 3.4f;

            // 4 Glowing Arcane Obelisks
            Vector2[] obeliskCorners = { new Vector2(4.2f, 4.2f), new Vector2(-4.2f, 4.2f), new Vector2(4.2f, -4.2f), new Vector2(-4.2f, -4.2f) };
            for (int i = 0; i < obeliskCorners.Length; i++)
            {
                var obelisk = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obelisk.name = $"Moonstone Obelisk {i + 1}";
                obelisk.transform.SetParent(elfRoot.transform, false);
                obelisk.transform.localPosition = new Vector3(obeliskCorners[i].x, 2.2f, obeliskCorners[i].y);
                obelisk.transform.localScale = new Vector3(0.55f, 4.4f, 0.55f);
                if (glowMat) obelisk.GetComponent<Renderer>().sharedMaterial = glowMat;

                var obLight = obelisk.AddComponent<Light>();
                obLight.type = LightType.Point;
                obLight.color = new Color(0.35f, 0.85f, 1.0f);
                obLight.range = 6.0f;
                obLight.intensity = 1.8f;
            }

            // High Elf NPCs
            SpawnTownNPC(scoutPrefab, elfRoot.transform, "Sunwarden Thalorien", "Silverglen Overseer",
                "Bal'a dash, malanore. Welcome to Silverglen Sanctum. May the eternal light guide your path.",
                new Vector3(-25.0f, elfGroundY, 11.8f), 0f, 2.22f, report);

            SpawnTownNPC(magePrefab, elfRoot.transform, "Magister Kaelen", "Arcane Scholar",
                "The arcane energies of the Sunwell still whisper through these stones. Seek knowledge, initiate.",
                new Vector3(-28.2f, elfGroundY, 16.5f), 125f, 2.25f, report);

            if (player)
            {
                SpawnWorldEnemy(elfRoot.transform, player, "Forest_Goblin.fbx", ctrlCaster,
                    "silverglen-mana-sprite-1", "Corrupted Mana Sprite", new Vector3(-34.0f, elfGroundY, 18.0f), 85f, 1.55f, 70f, 6f, new Color(0.4f, 0.8f, 1.0f), report);
                SpawnWorldEnemy(elfRoot.transform, player, "Forest_Goblin.fbx", ctrlCaster,
                    "silverglen-mana-sprite-2", "Corrupted Mana Sprite", new Vector3(-31.0f, elfGroundY, 23.0f), 140f, 1.55f, 70f, 6f, new Color(0.4f, 0.8f, 1.0f), report);
            }

            // =========================================================================
            // 3. UNDEAD STARTER: Deathknell Hollow (-28, 0, 160)
            // =========================================================================
            var undeadRoot = new GameObject("18 - Undead Starter Deathknell Hollow");
            undeadRoot.transform.SetParent(root, false);
            Vector3 undeadPos = new Vector3(-28.0f, 0f, 160.0f);
            float undeadGroundY = SampleEnvironmentGroundY(undeadPos, -0.1f);
            undeadRoot.transform.position = new Vector3(undeadPos.x, undeadGroundY, undeadPos.z);

            var undeadLandmark = undeadRoot.AddComponent<Landmark>();
            undeadLandmark.landmarkId = "undead-starter-deathknell";
            undeadLandmark.displayName = "Deathknell Hollow (Forsaken Crypts)";

            // Weathered Mausoleum Platform
            var cryptFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cryptFloor.name = "Deathknell Crypt Platform";
            cryptFloor.transform.SetParent(undeadRoot.transform, false);
            cryptFloor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            cryptFloor.transform.localScale = new Vector3(13f, 0.4f, 13f);
            if (cryptMat) cryptFloor.GetComponent<Renderer>().sharedMaterial = cryptMat;

            // Crypt Archway Gate
            for (int side = -1; side <= 1; side += 2)
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pillar.name = $"Mausoleum Pillar {(side < 0 ? "West" : "East")}";
                pillar.transform.SetParent(undeadRoot.transform, false);
                pillar.transform.localPosition = new Vector3(side * 2.8f, 2.2f, 4.5f);
                pillar.transform.localScale = new Vector3(0.85f, 4.4f, 0.85f);
                if (brickMat) pillar.GetComponent<Renderer>().sharedMaterial = brickMat;
            }
            var archHeader = GameObject.CreatePrimitive(PrimitiveType.Cube);
            archHeader.name = "Mausoleum Gate Header";
            archHeader.transform.SetParent(undeadRoot.transform, false);
            archHeader.transform.localPosition = new Vector3(0f, 4.5f, 4.5f);
            archHeader.transform.localScale = new Vector3(6.6f, 0.7f, 0.95f);
            if (brickMat) archHeader.GetComponent<Renderer>().sharedMaterial = brickMat;

            // Gravestones
            Vector3[] gravePositions = { new Vector3(-3.8f, 0.55f, -2.5f), new Vector3(3.5f, 0.6f, -1.8f), new Vector3(-2.2f, 0.5f, 2.8f), new Vector3(3.6f, 0.65f, 2.2f) };
            for (int g = 0; g < gravePositions.Length; g++)
            {
                var grave = GameObject.CreatePrimitive(PrimitiveType.Cube);
                grave.name = $"Weathered Gravestone {g + 1}";
                grave.transform.SetParent(undeadRoot.transform, false);
                grave.transform.localPosition = gravePositions[g];
                grave.transform.localScale = new Vector3(0.65f, 1.1f, 0.25f);
                grave.transform.localRotation = Quaternion.Euler(5f, g * 25f, -3f);
                if (rockMat) grave.GetComponent<Renderer>().sharedMaterial = rockMat;
            }

            // Blight Cauldron & Toxic Emerald Light
            var cauldron = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cauldron.name = "Forsaken Blight Cauldron";
            cauldron.transform.SetParent(undeadRoot.transform, false);
            cauldron.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            cauldron.transform.localScale = new Vector3(1.5f, 0.9f, 1.5f);
            if (rockMat) cauldron.GetComponent<Renderer>().sharedMaterial = rockMat;

            var blightLight = cauldron.AddComponent<Light>();
            blightLight.type = LightType.Point;
            blightLight.color = new Color(0.22f, 0.98f, 0.28f);
            blightLight.range = 14f;
            blightLight.intensity = 3.5f;

            // Undead NPCs
            SpawnTownNPC(knightPrefab, undeadRoot.transform, "Deathstalker Morzan", "Forsaken Emissary",
                "We are the Forsaken. We will slaughter anyone who stands in our way. Trust no one, brother.",
                new Vector3(-28.0f, undeadGroundY, 157.5f), 0f, 2.25f, report);

            SpawnTownNPC(magePrefab, undeadRoot.transform, "Shadow Priestess Alyssa", "Cult of the Forgotten Shadow",
                "Light and Shadow are one. Embrace the darkness that gave you second life.",
                new Vector3(-25.2f, undeadGroundY, 161.0f), -110f, 2.20f, report);

            if (player)
            {
                SpawnWorldEnemy(undeadRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "deathknell-ghoul-1", "Wandering Ghoul", new Vector3(-35.0f, undeadGroundY, 166.0f), 70f, 1.85f, 80f, 8f, new Color(0.6f, 0.7f, 0.5f), report);
                SpawnWorldEnemy(undeadRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "deathknell-ghoul-2", "Wandering Ghoul", new Vector3(-22.0f, undeadGroundY, 168.0f), -90f, 1.85f, 80f, 8f, new Color(0.6f, 0.7f, 0.5f), report);
            }

            // =========================================================================
            // 4. GOBLIN STARTER: Rustwater Scrapworks (38, 0, 175)
            // =========================================================================
            var goblinRoot = new GameObject("19 - Goblin Starter Rustwater Scrapworks");
            goblinRoot.transform.SetParent(root, false);
            Vector3 goblinPos = new Vector3(38.0f, 0f, 175.0f);
            float goblinGroundY = SampleEnvironmentGroundY(goblinPos, -0.1f);
            goblinRoot.transform.position = new Vector3(goblinPos.x, goblinGroundY, goblinPos.z);

            var goblinLandmark = goblinRoot.AddComponent<Landmark>();
            goblinLandmark.landmarkId = "goblin-starter-rustwater";
            goblinLandmark.displayName = "Rustwater Scrapworks (Cartel Basin)";

            // Timber Workshop Deck
            var deckFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deckFloor.name = "Rustwater Workshop Deck";
            deckFloor.transform.SetParent(goblinRoot.transform, false);
            deckFloor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            deckFloor.transform.localScale = new Vector3(13f, 0.4f, 13f);
            if (timberMat) deckFloor.GetComponent<Renderer>().sharedMaterial = timberMat;

            // Steam Boiler Engine
            var boiler = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            boiler.name = "Steam Boiler Engine";
            boiler.transform.SetParent(goblinRoot.transform, false);
            boiler.transform.localPosition = new Vector3(0f, 1.4f, 3.8f);
            boiler.transform.localScale = new Vector3(2.4f, 2.8f, 2.4f);
            if (metalMat) boiler.GetComponent<Renderer>().sharedMaterial = metalMat;

            // Exhaust Stack
            var exhaust = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            exhaust.name = "Boiler Exhaust Stack";
            exhaust.transform.SetParent(boiler.transform, false);
            exhaust.transform.localPosition = new Vector3(0.5f, 1.3f, 0f);
            exhaust.transform.localScale = new Vector3(0.35f, 0.8f, 0.35f);
            if (metalMat) exhaust.GetComponent<Renderer>().sharedMaterial = metalMat;

            // TNT Barrels
            for (int t = 0; t < 3; t++)
            {
                var tnt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                tnt.name = $"TNT Barrel {t + 1}";
                tnt.transform.SetParent(goblinRoot.transform, false);
                tnt.transform.localPosition = new Vector3(-3.5f + (t * 0.9f), 0.5f, 2.2f);
                tnt.transform.localScale = new Vector3(0.65f, 0.5f, 0.65f);
                if (redMat) tnt.GetComponent<Renderer>().sharedMaterial = redMat;
            }

            // Goblin Lantern Post
            var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Lantern Post";
            post.transform.SetParent(goblinRoot.transform, false);
            post.transform.localPosition = new Vector3(0f, 1.5f, -4.5f);
            post.transform.localScale = new Vector3(0.2f, 1.5f, 0.2f);
            if (timberMat) post.GetComponent<Renderer>().sharedMaterial = timberMat;

            var lantern = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lantern.name = "Goblin Oil Lantern";
            lantern.transform.SetParent(post.transform, false);
            lantern.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            lantern.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            var lanternLight = lantern.AddComponent<Light>();
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1.0f, 0.82f, 0.18f);
            lanternLight.range = 14f;
            lanternLight.intensity = 3.2f;

            // Goblin NPCs
            SpawnTownNPC(smithPrefab, goblinRoot.transform, "Boss Griznak", "Bilgewater Cartel Foreman",
                "Time is money, friend! If you're not generating profit, you're costing me gold! Let's get to work.",
                new Vector3(38.0f, goblinGroundY, 172.5f), 0f, 2.15f, report);

            SpawnTownNPC(magePrefab, goblinRoot.transform, "Tinker Fizzlebang", "Chief Engineer",
                "Stand back! This turbo-thruster only explodes forty percent of the time! Standard goblin safety margins!",
                new Vector3(35.5f, goblinGroundY, 176.0f), 110f, 2.10f, report);

            if (player)
            {
                SpawnWorldEnemy(goblinRoot.transform, player, "Forest_Goblin.fbx", ctrlMelee,
                    "rustwater-scavenger-1", "Rustwater Scavenger", new Vector3(45.0f, goblinGroundY, 179.0f), -120f, 1.6f, 75f, 7f, new Color(0.9f, 0.8f, 0.4f), report);
                SpawnWorldEnemy(goblinRoot.transform, player, "Forest_Goblin.fbx", ctrlMelee,
                    "rustwater-scavenger-2", "Rustwater Scavenger", new Vector3(42.0f, goblinGroundY, 183.0f), -160f, 1.6f, 75f, 7f, new Color(0.9f, 0.8f, 0.4f), report);
            }

            report.AppendLine("  [Racial Starting Precincts] Built Frostpeak Foothills (Dwarf), Silverglen Sanctum (High Elf), Deathknell Hollow (Undead), and Rustwater Scrapworks (Goblin) with thematic architecture, starter NPCs, and wild creatures!");
        }

        static void BuildFlightMasterNetwork(Transform root, StringBuilder report)
        {
            var oldNetwork = GameObject.Find("14 - Flight Master Network");
            if (oldNetwork) UnityEngine.Object.DestroyImmediate(oldNetwork);

            var flightRoot = new GameObject("14 - Flight Master Network");
            flightRoot.transform.SetParent(root, false);

            var paladinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_PaladinGuard.prefab");
            var knightPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_KnightArms.prefab");
            var scoutPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_RangerScout.prefab");
            var magePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Prefab_WoW_Arcanist.prefab");

            // 1. Highspire Keep Gryphon Master Darien (Alliance Capital)
            SpawnFlightMasterNPC(paladinPrefab, flightRoot.transform, "Gryphon Master Darien", "Flight Master",
                "Highspire Keep (Alliance Capital)", "Alliance", new Vector3(-52.0f, 1.5f, -16.0f), 65f, 2.25f, report);

            // 2. Emberwatch Outpost Gryphon Master Ryan (Amber Vale Plains)
            SpawnFlightMasterNPC(paladinPrefab, flightRoot.transform, "Gryphon Master Ryan", "Flight Master",
                "Emberwatch Outpost", "Alliance", new Vector3(2.2f, 0.5f, 10.0f), -135f, 2.25f, report);

            // 3. Duskwood Cathedral Grounds Skywatcher (Duskwood / Crypts)
            SpawnFlightMasterNPC(magePrefab, flightRoot.transform, "Duskwood Skywatcher", "Flight Master",
                "Duskwood Cathedral Grounds", "Neutral", new Vector3(-50.0f, 0.5f, 205.0f), -120f, 2.20f, report);

            // 4. Camp Bloodrock Outrider (Redridge Canyons)
            SpawnFlightMasterNPC(knightPrefab, flightRoot.transform, "Bloodrock Outrider", "Flight Master",
                "Camp Bloodrock", "Horde", new Vector3(50.0f, 0.5f, 210.0f), 45f, 2.25f, report);

            // 5. Gor'Kresh Stronghold Wind Rider Vark (Horde Capital)
            SpawnFlightMasterNPC(knightPrefab, flightRoot.transform, "Wind Rider Vark", "Flight Master",
                "Gor'Kresh Stronghold (Horde Capital)", "Horde", new Vector3(92.0f, 2.5f, 231.0f), -60f, 2.25f, report);

            // 6. Wyrmcrest Summit Skywarden (Mountain Peak)
            SpawnFlightMasterNPC(scoutPrefab, flightRoot.transform, "Wyrmcrest Skywarden", "Flight Master",
                "Wyrmcrest Summit (Mountain Peak)", "Neutral", new Vector3(0.0f, 49.0f, 215.0f), 180f, 2.20f, report);

            report.AppendLine("  [Flight Taxi Network] 6 World Flight Masters spawned across Highspire Keep, Emberwatch, Duskwood, Redridge, Gor'Kresh, and Wyrmcrest!");
        }

        static GameObject SpawnFlightMasterNPC(GameObject prefab, Transform parent, string name, string role, string nodeName, string faction, Vector3 approxPos, float yaw, float labelHeight, StringBuilder report)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            float groundY = SampleEnvironmentGroundY(approxPos, approxPos.y);
            inst.transform.SetPositionAndRotation(new Vector3(approxPos.x, groundY, approxPos.z), Quaternion.Euler(0f, yaw, 0f));
            inst.transform.localScale = Vector3.one;

            var npc = inst.GetComponent<TownNPC>() ?? inst.AddComponent<TownNPC>();
            npc.npcName = name;
            npc.roleTag = role;
            npc.greeting = $"Where shall the winds carry you, traveler? Speak to me to take flight.";
            npc.nameplateHeight = labelHeight;

            var fm = inst.GetComponent<FlightMaster>() ?? inst.AddComponent<FlightMaster>();
            fm.nodeName = nodeName;
            fm.faction = faction;
            fm.flightStationPos = inst.transform.position;

            report.AppendLine($"  [FlightMaster] {name} ({nodeName}): pos=({inst.transform.position.x:F1}, {inst.transform.position.y:F2}, {inst.transform.position.z:F1})");
            return inst;
        }

        static void SpawnTownNPC(GameObject prefab, Transform parent, string name, string role, string greeting, Vector3 approxPos, float yaw, float labelHeight, StringBuilder report)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            inst.name = name;
            float groundY = SampleEnvironmentGroundY(approxPos, approxPos.y);
            inst.transform.SetPositionAndRotation(new Vector3(approxPos.x, groundY, approxPos.z), Quaternion.Euler(0f, yaw, 0f));
            inst.transform.localScale = Vector3.one;

            var npc = inst.GetComponent<TownNPC>() ?? inst.AddComponent<TownNPC>();
            npc.npcName = name;
            npc.roleTag = role;
            npc.greeting = greeting;
            npc.nameplateHeight = labelHeight;

            var b = ComputeWorldBounds(inst);
            report.AppendLine($"  [NPC] {name} <{role}>: pos=({inst.transform.position.x:F1}, {inst.transform.position.y:F2}, {inst.transform.position.z:F1}) | height={b.size.y:F2}m | width={b.size.x:F2}m");
        }

        static void UpgradeMerchantVisual(AnimatorController ctrlGuard, StringBuilder report)
        {
            var merchant = UnityEngine.Object.FindFirstObjectByType<Merchant>();
            if (!merchant) return;

            var existingWoW = merchant.transform.Find("ClassicWoWVisual");
            if (existingWoW) UnityEngine.Object.DestroyImmediate(existingWoW.gameObject);

            // Deactivate old duplicate Abe mesh cleanly without deleting serialized children
            foreach (Transform child in merchant.transform)
            {
                if (child.GetComponentInChildren<Renderer>(true) && child.name != "Merchant name")
                    child.gameObject.SetActive(false);
            }

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + "Castle_Guard.fbx");
            if (!source) return;

            float groundY = SampleEnvironmentGroundY(merchant.transform.position, merchant.transform.position.y);
            merchant.transform.position = new Vector3(merchant.transform.position.x, groundY, merchant.transform.position.z);

            CreateNormalizedCharacterVisual(source, "Castle_Guard", ctrlGuard, 1.86f, new Color(0.95f, 0.90f, 0.80f), merchant.transform);
            EditorUtility.SetDirty(merchant);

            var b = ComputeWorldBounds(merchant.gameObject);
            report.AppendLine($"  [Merchant] {merchant.npcName}: upgraded to smooth Castle Guard | height={b.size.y:F2}m");
        }

        static void UpgradeExistingEnemiesToClassicWoW(PlayerController player, AnimatorController ctrlMelee, AnimatorController ctrlCaster, StringBuilder report)
        {
            var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var enemy in enemies)
            {
                if (enemy.trainingDummy) continue;

                // Deactivate previous low-poly visuals cleanly (preserving serialized hierarchy)
                foreach (Transform child in enemy.transform)
                {
                    if (child.name == "ClassicWoWVisual")
                        UnityEngine.Object.DestroyImmediate(child.gameObject);
                    else if (child.GetComponentInChildren<SkinnedMeshRenderer>(true) || child.name == "Skeleton visual")
                        child.gameObject.SetActive(false);
                }

                string fbxFile;
                float targetHeight;
                Color tint = Color.white;
                string displayTitle;

                if (enemy.isBoss)
                {
                    fbxFile = "Knight_Arms.fbx";
                    targetHeight = 2.35f;
                    tint = new Color(0.72f, 0.62f, 0.68f); // Dark dread-iron plate
                    displayTitle = "Ashwood Dread Captain";
                }
                else if (enemy.ranged)
                {
                    fbxFile = "Arcanist_Mage.fbx";
                    targetHeight = 1.85f;
                    tint = new Color(0.78f, 0.55f, 0.82f); // Shadow cultist robes
                    displayTitle = "Ashwood Shadow Hexer";
                }
                else if (enemy.campGuard || enemy.heavyAttacker)
                {
                    fbxFile = "Crag_Orc.fbx";
                    targetHeight = 2.06f;
                    displayTitle = "Ashen Crag Enforcer";
                }
                else
                {
                    fbxFile = "Forest_Goblin.fbx";
                    targetHeight = 1.40f;
                    displayTitle = "Ashwood Goblin Raider";
                }

                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + fbxFile);
                string modelKey = Path.GetFileNameWithoutExtension(fbxFile);
                var ctrl = enemy.ranged ? ctrlCaster : ctrlMelee;

                var visual = CreateNormalizedCharacterVisual(source, modelKey, ctrl, targetHeight, tint, enemy.transform);
                var anim = visual.GetComponentInChildren<Animator>();

                enemy.animator = anim;
                enemy.nativeDeathAnimation = true;
                enemy.walkStrideSpeed = 1.55f;
                enemy.runStrideSpeed = 3.1f;

                var stats = enemy.GetComponent<Combatant>();
                if (stats) stats.displayName = displayTitle;

                var motor = enemy.GetComponent<CharacterController>();
                if (motor)
                {
                    motor.height = targetHeight;
                    motor.radius = enemy.isBoss ? 0.45f : 0.35f;
                    motor.center = Vector3.up * (targetHeight * 0.5f);
                }

                var grounding = enemy.GetComponent<GroundedVisual>();
                if (grounding)
                {
                    grounding.animator = anim;
                    EditorUtility.SetDirty(grounding);
                }

                EditorUtility.SetDirty(enemy);
                if (stats) EditorUtility.SetDirty(stats);
                if (motor) EditorUtility.SetDirty(motor);

                var b = ComputeWorldBounds(enemy.gameObject);
                report.AppendLine($"  [Enemy] {enemy.enemyId} -> {displayTitle} ({modelKey}): height={b.size.y:F2}m");
            }
        }

        static GameObject SpawnWorldEnemy(Transform parent, PlayerController player, string fbxFile, AnimatorController ctrl,
            string id, string title, Vector3 approxPos, float yaw, float height, float maxHp, float dmg, Color tint, StringBuilder report)
        {
            var go = new GameObject(title);
            go.transform.SetParent(parent, false);
            float groundY = SampleEnvironmentGroundY(approxPos, approxPos.y);
            go.transform.SetPositionAndRotation(new Vector3(approxPos.x, groundY, approxPos.z), Quaternion.Euler(0f, yaw, 0f));

            var motor = go.AddComponent<CharacterController>();
            motor.height = height;
            motor.radius = 0.35f;
            motor.center = Vector3.up * (height * 0.5f);

            var stats = go.AddComponent<Combatant>();
            stats.displayName = title;
            stats.maxHealth = maxHp;

            var enemy = go.AddComponent<EnemyController>();
            enemy.player = player;
            enemy.enemyId = id;
            enemy.attackDamage = dmg;
            enemy.attackRange = 2.1f;
            enemy.attackInterval = 1.7f;
            enemy.movementMultiplier = 0.72f;
            enemy.nativeDeathAnimation = true;
            enemy.walkStrideSpeed = 1.55f;
            enemy.runStrideSpeed = 3.1f;

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ArtDir + fbxFile);
            string modelKey = Path.GetFileNameWithoutExtension(fbxFile);
            var visual = CreateNormalizedCharacterVisual(source, modelKey, ctrl, height, tint, go.transform);
            enemy.animator = visual.GetComponentInChildren<Animator>();

            var b = ComputeWorldBounds(go);
            report.AppendLine($"  [WorldEnemy] {title} ({modelKey}): pos=({go.transform.position.x:F1}, {go.transform.position.y:F2}, {go.transform.position.z:F1}) | height={b.size.y:F2}m");
            return go;
        }

        static float SampleEnvironmentGroundY(Vector3 pos, float fallbackY)
        {
            if (pos.x < -200f || pos.y < -20f || pos.y >= 40f)
                return fallbackY;

            var terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindFirstObjectByType<Terrain>();
            float terrainY = float.MinValue;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 tp = terrain.transform.position;
                Vector3 ts = terrain.terrainData.size;
                if (pos.x >= tp.x && pos.x <= tp.x + ts.x && pos.z >= tp.z && pos.z <= tp.z + ts.z)
                    terrainY = terrain.SampleHeight(pos) + tp.y;
            }

            float rayStartY = (terrainY > -100f ? terrainY : fallbackY) + 12f;
            var hits = Physics.RaycastAll(new Vector3(pos.x, rayStartY, pos.z), Vector3.down, 24f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = terrainY;
            foreach (var hit in hits)
            {
                if (terrainY > -100f && hit.point.y > terrainY + 1.8f) continue; // Ignore roofs/canopies
                var tr = hit.collider.transform;
                if (tr.GetComponentInParent<PlayerController>() ||
                    tr.GetComponentInParent<EnemyController>() ||
                    tr.GetComponentInParent<TownNPC>() ||
                    tr.GetComponentInParent<QuestGiver>() ||
                    tr.GetComponentInParent<Merchant>() ||
                    tr.GetComponentInParent<Wildlife>() ||
                    tr.GetComponentInParent<LootChest>())
                    continue;

                string rootName = tr.root.name.ToLowerInvariant();
                if (rootName.Contains("npc") || rootName.Contains("props")) continue;

                if (hit.point.y > bestY)
                    bestY = hit.point.y;
            }
            return bestY > -5f ? bestY : fallbackY;
        }

        static Bounds ComputeWorldBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(false)
                .Where(r => !(r is LineRenderer) && !(r is ParticleSystemRenderer) && !r.GetComponent<TextMesh>())
                .ToArray();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.one);

            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                smr.updateWhenOffscreen = true;

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void BuildGatheringNodes(Transform root, StringBuilder report)
        {
            var oldNodes = GameObject.Find("15 - World Gathering Nodes");
            if (oldNodes) UnityEngine.Object.DestroyImmediate(oldNodes);

            var nodesRoot = new GameObject("15 - World Gathering Nodes");
            nodesRoot.transform.SetParent(root, false);

            var herbMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Nature_Flowers.mat") ??
                          AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Runestone glow.mat");
            var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Nature_Rock.mat");
            var goldMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Beacon gold.mat");

            // 1. Amber Vale Herbs
            CreateHerbNode(nodesRoot.transform, "Peacebloom", new Vector3(6.5f, 0f, 14.5f), new Color(1f, 0.9f, 0.4f), herbMat, 25, 2, report);
            CreateHerbNode(nodesRoot.transform, "Silverleaf", new Vector3(-8.5f, 0f, 21.0f), new Color(0.7f, 1f, 0.7f), herbMat, 25, 2, report);
            CreateHerbNode(nodesRoot.transform, "Earthroot", new Vector3(18.0f, 0f, 48.0f), new Color(0.85f, 0.65f, 0.35f), herbMat, 30, 3, report);

            // 2. Redridge Canyons Ores & Herbs
            CreateHerbNode(nodesRoot.transform, "Bloodthorn", new Vector3(45.0f, 0f, 206.0f), new Color(1f, 0.25f, 0.25f), herbMat, 35, 4, report);
            CreateOreNode(nodesRoot.transform, "Copper Ore Vein", new Vector3(62.0f, 0f, 220.0f), new Color(0.9f, 0.55f, 0.25f), rockMat, goldMat, 40, 5, report);
            CreateOreNode(nodesRoot.transform, "Dark Iron Deposit", new Vector3(76.0f, 0f, 232.0f), new Color(0.6f, 0.2f, 0.2f), rockMat, goldMat, 50, 7, report);

            // 3. Duskwood Herbs
            CreateHerbNode(nodesRoot.transform, "Ghost Mushroom", new Vector3(-48.0f, 0f, 198.0f), new Color(0.6f, 0.9f, 1f), herbMat, 45, 6, report);
            CreateHerbNode(nodesRoot.transform, "Grave Moss", new Vector3(-58.0f, 0f, 222.0f), new Color(0.5f, 0.7f, 0.5f), herbMat, 45, 6, report);

            // 4. Wyrmcrest Summit Nodes
            CreateHerbNode(nodesRoot.transform, "Dragonfire Lotus", new Vector3(4.5f, 48.0f, 221.0f), new Color(1f, 0.4f, 0.1f), herbMat, 65, 10, report);
            CreateOreNode(nodesRoot.transform, "Obsidian Core Vein", new Vector3(-5.5f, 48.0f, 227.0f), new Color(0.3f, 0.15f, 0.45f), rockMat, goldMat, 75, 12, report);

            report.AppendLine("  [Gathering Nodes] Spawned 9 interactive Herbalism & Mining gathering nodes across all zones.");
        }

        static void CreateHerbNode(Transform parent, string name, Vector3 approxPos, Color flowerColor, Material mat, int xp, int gold, StringBuilder report)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            float gy = SampleEnvironmentGroundY(approxPos, approxPos.y);
            go.transform.position = new Vector3(approxPos.x, gy + 0.25f, approxPos.z);
            go.transform.localScale = new Vector3(0.55f, 0.45f, 0.55f);

            var r = go.GetComponent<Renderer>();
            if (mat) r.sharedMaterial = mat;

            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = flowerColor;
            lt.range = 3.5f;
            lt.intensity = 1.8f;

            var gn = go.AddComponent<GatheringNode>();
            gn.nodeName = name;
            gn.gatheringType = GatheringType.Herbalism;
            gn.rewardXp = xp;
            gn.rewardGold = gold;
            gn.rewardItem = name;

            var col = go.GetComponent<Collider>();
            if (col) col.isTrigger = false;
        }

        static void CreateOreNode(Transform parent, string name, Vector3 approxPos, Color oreColor, Material baseMat, Material veinMat, int xp, int gold, StringBuilder report)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            float gy = SampleEnvironmentGroundY(approxPos, approxPos.y);
            go.transform.position = new Vector3(approxPos.x, gy + 0.35f, approxPos.z);
            go.transform.localScale = new Vector3(1.1f, 0.7f, 1.1f);
            go.transform.localRotation = Quaternion.Euler(15f, 35f, 12f);

            var r = go.GetComponent<Renderer>();
            if (baseMat) r.sharedMaterial = baseMat;

            // Shiny mineral crystals on top
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crystal.name = "Vein Mineral";
            crystal.transform.SetParent(go.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            crystal.transform.localScale = new Vector3(0.45f, 0.4f, 0.45f);
            var cr = crystal.GetComponent<Renderer>();
            if (veinMat) cr.sharedMaterial = veinMat;

            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = oreColor;
            lt.range = 4.5f;
            lt.intensity = 2.2f;

            var gn = go.AddComponent<GatheringNode>();
            gn.nodeName = name;
            gn.gatheringType = GatheringType.Mining;
            gn.rewardXp = xp;
            gn.rewardGold = gold;
            gn.rewardItem = name;
        }

        static void BuildWorldBossArena(Transform root, PlayerController player, AnimatorController ctrlMelee, StringBuilder report)
        {
            var oldCrater = GameObject.Find("20 - Redridge Volcanic Crater World Boss");
            if (oldCrater) UnityEngine.Object.DestroyImmediate(oldCrater);

            var craterRoot = new GameObject("20 - Redridge Volcanic Crater World Boss");
            craterRoot.transform.SetParent(root, false);
            Vector3 origin = new Vector3(10.0f, 0f, 185.0f);
            float groundY = SampleEnvironmentGroundY(origin, -0.1f);
            craterRoot.transform.position = new Vector3(origin.x, groundY, origin.z);

            var landmark = craterRoot.AddComponent<Landmark>();
            landmark.landmarkId = "worldboss-crater";
            landmark.displayName = "Volcanic Crater (Gorgonax World Boss)";

            var rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Nature_Rock.mat");
            var fireMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Runestone glow.mat");

            // Scorched Volcanic Bedrock (18m x 0.4m x 18m)
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Crater Bedrock";
            floor.transform.SetParent(craterRoot.transform, false);
            floor.transform.localPosition = new Vector3(0f, -0.2f, 0f);
            floor.transform.localScale = new Vector3(18f, 0.4f, 18f);
            if (rockMat) floor.GetComponent<Renderer>().sharedMaterial = rockMat;

            // Molten Fissures flanking the arena
            for (int f = -1; f <= 1; f += 2)
            {
                var fissure = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fissure.name = $"Molten Magma Fissure {(f < 0 ? "West" : "East")}";
                fissure.transform.SetParent(craterRoot.transform, false);
                fissure.transform.localPosition = new Vector3(f * 7.5f, 0.1f, f * 2.0f);
                fissure.transform.localScale = new Vector3(3.5f, 0.2f, 3.5f);
                if (fireMat) fissure.GetComponent<Renderer>().sharedMaterial = fireMat;

                var fLight = fissure.AddComponent<Light>();
                fLight.type = LightType.Point;
                fLight.color = new Color(1.0f, 0.35f, 0.05f);
                fLight.range = 16f;
                fLight.intensity = 4.2f;
            }

            if (player)
            {
                var bossGo = SpawnWorldEnemy(craterRoot.transform, player, "Crag_Orc.fbx", ctrlMelee,
                    "worldboss-gorgonax", "Gorgonax the Worldbreaker", origin + new Vector3(0f, 0.1f, 2.0f), 180f, 4.2f, 1600f, 28f, new Color(1.0f, 0.38f, 0.12f), report);
                if (bossGo)
                {
                    var enemy = bossGo.GetComponent<EnemyController>();
                    if (enemy) enemy.isBoss = true;
                }
            }

            report.AppendLine("  [World Boss] Built Redridge Volcanic Crater at (10, 185) with Gorgonax the Worldbreaker (Level 10 World Boss, 1600 HP, 4.2m tall)!");
        }

        static string SanitizeFileName(string value)
        {
            var chars = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
                sb.Append(chars.Contains(c) || c == ' ' || c == ':' ? '_' : c);
            return sb.ToString();
        }
    }
}
