using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    /// <summary>
    /// Factory and coordinator for visual 3D models across all 6 races and 4 classes.
    /// Handles runtime player model swapping and character preview generation.
    /// </summary>
    public static class CharacterVisualFactory
    {
        public static readonly string[] AllianceRaces = { "Human", "Dwarf", "Elf" };
        public static readonly string[] HordeRaces = { "Orc", "Undead", "Goblin" };
        public static readonly string[] AllClasses = { "Warrior", "Paladin", "Mage", "Rogue" };

        public struct RacialProfile
        {
            public float height;
            public Vector3 bodyScale;
            public Color skinTint;
            public float controllerHeight;
            public float controllerRadius;
            public Vector3 controllerCenter;
            public float walkStride;
            public float runStride;
        }

        public static RacialProfile GetProfile(string race)
        {
            switch (race)
            {
                case "Dwarf":
                    return new RacialProfile
                    {
                        height = 1.42f,
                        bodyScale = new Vector3(1.32f, 0.75f, 1.30f),
                        skinTint = Color.white,
                        controllerHeight = 1.50f,
                        controllerRadius = 0.44f,
                        controllerCenter = new Vector3(0f, 0.75f, 0f),
                        walkStride = 1.25f,
                        runStride = 2.45f
                    };
                case "Elf":
                case "High Elf":
                    return new RacialProfile
                    {
                        height = 1.92f,
                        bodyScale = new Vector3(0.88f, 1.05f, 0.88f),
                        skinTint = Color.white,
                        controllerHeight = 1.98f,
                        controllerRadius = 0.44f,
                        controllerCenter = new Vector3(0f, 0.99f, 0f),
                        walkStride = 1.52f,
                        runStride = 2.88f
                    };
                case "Orc":
                case "Brute":
                    return new RacialProfile
                    {
                        height = 2.08f,
                        bodyScale = new Vector3(1.22f, 1.08f, 1.20f),
                        skinTint = Color.white,
                        controllerHeight = 2.20f,
                        controllerRadius = 0.55f,
                        controllerCenter = new Vector3(0f, 1.10f, 0f),
                        walkStride = 1.65f,
                        runStride = 3.00f
                    };
                case "Undead":
                    return new RacialProfile
                    {
                        height = 1.80f,
                        bodyScale = new Vector3(0.94f, 0.98f, 0.94f),
                        skinTint = Color.white,
                        controllerHeight = 1.90f,
                        controllerRadius = 0.45f,
                        controllerCenter = new Vector3(0f, 0.95f, 0f),
                        walkStride = 1.45f,
                        runStride = 2.75f
                    };
                case "Goblin":
                    return new RacialProfile
                    {
                        height = 1.32f,
                        bodyScale = new Vector3(1.04f, 0.72f, 1.02f),
                        skinTint = Color.white,
                        controllerHeight = 1.40f,
                        controllerRadius = 0.38f,
                        controllerCenter = new Vector3(0f, 0.70f, 0f),
                        walkStride = 1.20f,
                        runStride = 2.35f
                    };
                case "Human":
                default:
                    return new RacialProfile
                    {
                        height = 1.88f,
                        bodyScale = Vector3.one,
                        skinTint = Color.white,
                        controllerHeight = 1.95f,
                        controllerRadius = 0.48f,
                        controllerCenter = new Vector3(0f, 0.98f, 0f),
                        walkStride = 1.50f,
                        runStride = 2.85f
                    };
            }
        }

        public static string NormalizeRace(string race)
        {
            if (string.IsNullOrEmpty(race)) return "Human";
            string r = race.Trim();
            if (r.Equals("High Elf", StringComparison.OrdinalIgnoreCase) || r.Equals("Elf", StringComparison.OrdinalIgnoreCase)) return "Elf";
            if (r.Equals("Brute", StringComparison.OrdinalIgnoreCase) || r.Equals("Orc", StringComparison.OrdinalIgnoreCase)) return "Orc";
            if (r.Equals("Dwarf", StringComparison.OrdinalIgnoreCase)) return "Dwarf";
            if (r.Equals("Undead", StringComparison.OrdinalIgnoreCase) || r.Equals("Forsaken", StringComparison.OrdinalIgnoreCase)) return "Undead";
            if (r.Equals("Goblin", StringComparison.OrdinalIgnoreCase)) return "Goblin";
            return "Human";
        }

        public static string NormalizeClass(string characterClass)
        {
            if (string.IsNullOrEmpty(characterClass)) return "Warrior";
            string c = characterClass.Trim();
            if (c.Equals("Paladin", StringComparison.OrdinalIgnoreCase)) return "Paladin";
            if (c.Equals("Mage", StringComparison.OrdinalIgnoreCase)) return "Mage";
            if (c.Equals("Rogue", StringComparison.OrdinalIgnoreCase)) return "Rogue";
            return "Warrior";
        }

        /// <summary>
        /// Loads or instantiates the visual prefab for a given race and class combination.
        /// </summary>
        public static GameObject CreateVisualInstance(string race, string characterClass, Transform parent = null, bool isPreview = false, bool hostile = false)
        {
            race = NormalizeRace(race);
            characterClass = NormalizeClass(characterClass);
            var profile = GetProfile(race);

            GameObject template = null;

            // 1. Check Resources folder for prebaked combination
            string resourceKey = $"Characters/{race}_{characterClass}";
            template = Resources.Load<GameObject>(resourceKey);

            // 2. Editor AssetDatabase fallback if not in Resources
#if UNITY_EDITOR
            if (!template)
            {
                string editorPrefabPath = $"Assets/RPG/Prefabs/Characters/{race}_{characterClass}.prefab";
                template = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(editorPrefabPath);
            }

            if (!template)
            {
                // Select archetype template by race/class
                string archetypePath = GetArchetypePrefabPath(race, characterClass);
                if (!string.IsNullOrEmpty(archetypePath))
                    template = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(archetypePath);
            }
#endif

            // 3. Fallback: Search existing scene NPCs or Brute as template
            if (!template)
            {
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (player && player.animator) template = player.animator.gameObject;
            }

            if (!template)
            {
                Debug.LogWarning($"[CharacterVisualFactory] No template found for {race} {characterClass}");
                return null;
            }

            var instance = UnityEngine.Object.Instantiate(template, parent);
            instance.name = $"{race}_{characterClass}_Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = template.transform.localRotation;

            // Apply body scale if instance is an archetype rather than a prebaked combination
            if (!template.name.StartsWith($"{race}_{characterClass}"))
            {
                instance.transform.localScale = profile.bodyScale;
                ApplyMaterialTint(instance, profile.skinTint);
            }

            // In preview mode or player mode, ensure colliders are managed cleanly
            if (isPreview)
            {
                foreach (var col in instance.GetComponentsInChildren<Collider>()) col.enabled = false;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
            }
            else
            {
                // Player visual: colliders on visual should be disabled to avoid fighting CharacterController
                foreach (var col in instance.GetComponentsInChildren<Collider>()) col.enabled = false;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 2; // Ignore Raycast
            }

            var kit = ProceduralActorCatalog.Current;
            if (kit)
                ProceduralActorVisual.Attach(instance, instance.GetComponentInChildren<Animator>(),
                    kit.ForClass(race, characterClass, hostile), profile.height / 1.9f);

            if (Application.isPlaying && !isPreview && parent)
                GroundedVisual.EnsureFor(parent.gameObject, instance.GetComponentInChildren<Animator>());
            return instance;
        }

        static string GetArchetypePrefabPath(string race, string characterClass)
        {
            if (race == "Orc")
            {
                if (characterClass == "Warrior") return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_CragOrc.prefab";
                if (characterClass == "Paladin") return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_CragOrc.prefab";
                if (characterClass == "Mage") return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_Arcanist.prefab";
                return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_RangerScout.prefab";
            }
            if (race == "Goblin")
            {
                return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_ForestGoblin.prefab";
            }
            if (race == "Dwarf" && characterClass == "Warrior")
            {
                return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_Blacksmith.prefab";
            }

            // Human / Elf / Undead defaults by class
            switch (characterClass)
            {
                case "Paladin": return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_PaladinGuard.prefab";
                case "Mage": return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_Arcanist.prefab";
                case "Rogue": return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_RangerScout.prefab";
                case "Warrior":
                default: return "Assets/RPG/Prefabs/NPCs_WoW/Prefab_WoW_KnightArms.prefab";
            }
        }

        static void ApplyMaterialTint(GameObject go, Color tint)
        {
            if (tint == Color.white) return;
            var block = new MaterialPropertyBlock();
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                for (int i = 0; i < r.sharedMaterials.Length; i++)
                {
                    r.GetPropertyBlock(block, i);
                    block.SetColor("_BaseColor", tint);
                    r.SetPropertyBlock(block, i);
                }
            }
        }

        /// <summary>
        /// Creates a clean 3D preview model parented to previewParent for character UI.
        /// </summary>
        public static GameObject SpawnPreview(string race, string characterClass, Transform previewParent, float yaw = 180f)
        {
            var visual = CreateVisualInstance(race, characterClass, previewParent, isPreview: true);
            if (!visual) return null;

            visual.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.Euler(0f, yaw, 0f) * visual.transform.localRotation);

            var anim = visual.GetComponentInChildren<Animator>();
            if (anim)
            {
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Rebind();
                anim.Play("Idle", 0, 0f);
                anim.Update(0f);
            }

            return visual;
        }

        /// <summary>
        /// Applies the exact 3D race and class model to the active PlayerController at runtime.
        /// </summary>
        public static void ApplyToPlayer(PlayerController player, string race, string characterClass)
        {
            if (!player) return;

            race = NormalizeRace(race);
            characterClass = NormalizeClass(characterClass);
            var profile = GetProfile(race);

            // 1. Remove existing character visual hierarchy under player
            var oldVisuals = new List<GameObject>();
            for (int i = 0; i < player.transform.childCount; i++)
            {
                var child = player.transform.GetChild(i).gameObject;
                string cName = child.name.ToLowerInvariant();
                if (cName.Contains("visual") || cName.Contains("brute") || cName.Contains("classicwow") || cName.Contains("model") || cName.Contains("knight") || cName.Contains("paladin") || cName.Contains("arcanist") || cName.Contains("goblin") || cName.Contains("orc") || cName.Contains("skeleton"))
                {
                    oldVisuals.Add(child);
                }
            }

            // 2. Instantiate new visual under player
            var newVisual = CreateVisualInstance(race, characterClass, player.transform, isPreview: false);
            if (!newVisual)
            {
                Debug.LogWarning($"[CharacterVisualFactory] Could not spawn {race} {characterClass} for player.");
                return;
            }

            // Safely destroy previous visuals now that new one exists
            foreach (var old in oldVisuals)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(old);
                else UnityEngine.Object.DestroyImmediate(old);
            }

            newVisual.name = "Player_ActiveVisual";
            newVisual.transform.localPosition = Vector3.zero;

            // 3. Connect and configure Animator (preserve prefab's rig-matched controller)
            var anim = newVisual.GetComponentInChildren<Animator>();
            if (anim)
            {
                if (!anim.runtimeAnimatorController)
                {
                    RuntimeAnimatorController targetController = null;
#if UNITY_EDITOR
                    string fallbackCtrl = anim.isHuman
                        ? "Assets/RPG/Animations/Player Humanoid Melee.controller"
                        : "Assets/RPG/Animations/Brute Native.controller";
                    targetController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(fallbackCtrl)
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/RPG/Animations/Brute Native.controller");
#endif
                    if (!targetController && player.animator && player.animator.isHuman == anim.isHuman)
                        targetController = player.animator.runtimeAnimatorController;

                    if (targetController) anim.runtimeAnimatorController = targetController;
                }
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Rebind();
                anim.Play("Idle", 0, 0f);

                player.animator = anim;

                GroundedVisual.EnsureFor(player.gameObject, anim);
            }

            // 4. Update CharacterController to racial proportions
            var motor = player.GetComponent<CharacterController>();
            if (motor)
            {
                motor.height = profile.controllerHeight;
                motor.radius = profile.controllerRadius;
                motor.center = profile.controllerCenter;
            }

            // 5. Update calibrated stride speeds matching measured clip foot travel
            GetCalibratedStrides(race, characterClass, out float walkStride, out float runStride);
            player.walkStrideSpeed = walkStride;
            player.runStrideSpeed = runStride;
            player.backWalkStrideSpeed = walkStride * 0.92f;
            player.backRunStrideSpeed = runStride * 0.85f;

            player.OnVisualApplied(anim);
            player.Play("Idle", true);
            Debug.Log($"<color=#70df80>[CharacterVisualFactory]</color> Applied {race} {characterClass} to player avatar successfully.");
        }

        public static void GetCalibratedStrides(string race, string characterClass, out float walkStride, out float runStride)
        {
            race = NormalizeRace(race);
            characterClass = NormalizeClass(characterClass);

            if (race == "Undead")
            {
                walkStride = 0.72f;
                runStride = 1.00f;
                return;
            }

            bool isRogue = characterClass == "Rogue";
            switch (race)
            {
                case "Orc":
                    walkStride = 0.77f;
                    runStride = isRogue ? 4.39f : 4.10f;
                    break;
                case "Dwarf":
                    walkStride = isRogue ? 1.20f : 1.02f;
                    runStride = isRogue ? 6.19f : 5.50f;
                    break;
                case "Elf":
                    walkStride = 0.80f;
                    runStride = isRogue ? 4.05f : 4.25f;
                    break;
                case "Goblin":
                    walkStride = 0.65f;
                    runStride = isRogue ? 3.76f : 3.30f;
                    break;
                case "Human":
                default:
                    walkStride = 0.93f;
                    runStride = isRogue ? 4.73f : 5.00f;
                    break;
            }
        }
    }
}
