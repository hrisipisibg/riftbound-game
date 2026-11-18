using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class AnimationRepair
    {
        static AnimationRepair()
        {
            EditorApplication.delayCall += FirstRepair;
            EditorApplication.delayCall += FirstLocomotionCalibration;
            EditorApplication.delayCall += FirstRootMotionRepair;
        }
        static void FirstRootMotionRepair()
        {
            if (Application.isBatchMode || File.Exists("RootMotionFix.txt")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            { EditorApplication.delayCall += FirstRootMotionRepair; return; }
            if (EditorSceneManager.GetActiveScene().isDirty || EditorSceneManager.GetActiveScene().path != "Assets/RPG/Scenes/Outpost.unity") return;
            FixRootMotion();
        }
        [MenuItem("Riftbound/Fix locomotion root motion")]
        public static void FixRootMotion()
        {
            if (EditorApplication.isPlaying) return;
            int count = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art/Brute" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string lower = path.ToLowerInvariant();
                if (!lower.Contains("walk") && !lower.Contains("run")) continue;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                PrototypeImport.ConfigureAnimations(importer, path); importer.SaveAndReimport(); count++;
            }
            CalibrateLocomotion();
            File.WriteAllText("RootMotionFix.txt", $"Reimported {count} locomotion models with root XZ/rotation extracted, not baked into pose. Animator root motion stays disabled.\n" + File.ReadAllText("LocomotionCalibration-v3.txt"));
        }
        static void FirstLocomotionCalibration()
        {
            if (Application.isBatchMode) return;
            if (File.Exists("LocomotionCalibration-v3.txt")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            { EditorApplication.delayCall += FirstLocomotionCalibration; return; }
            if (EditorSceneManager.GetActiveScene().isDirty) return;
            CalibrateLocomotion();
        }
        [MenuItem("Riftbound/Calibrate walking and running")]
        public static void CalibrateLocomotion()
        {
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost.unity");
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/RPG/Scenes/Outpost.unity") return;
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player || !player.animator) return;
            foreach (string file in new[] { "Brute@Unarmed Run Forward", "Brute@Unarmed Run Back" })
            {
                string path = "Assets/RPG/Art/Brute/" + file + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) continue;
                PrototypeImport.ConfigureAnimations(importer, path); importer.SaveAndReimport();
            }
            ConfigureController(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/RPG/Animations/Brute.controller"));
            var copy = UnityEngine.Object.Instantiate(player.animator.gameObject);
            copy.name = "Temporary stride measurement"; copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = player.animator.transform.lossyScale;
            try
            {
                var animator = copy.GetComponent<Animator>();
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind(); animator.Update(0);
                float walk = MeasureStride(animator, "Walk", out _);
                var controller = (AnimatorController)animator.runtimeAnimatorController;
                var runState = controller.layers[0].stateMachine.states.First(s => s.state.name == "Run").state;
                AnimationClip bestClip = null;
                float run = 0, bestSway = float.MaxValue;
                var comparison = new System.Text.StringBuilder();
                bool suppliedRun = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/Brute/Brute@Unarmed Run Forward.fbx");
                foreach (string candidate in suppliedRun ? new[] { "Brute@Unarmed Run Forward" } : new[] { "standing run forward", "unarmed run forward" })
                {
                    var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/Brute/" + candidate + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                    runState.motion = clip;
                    animator.runtimeAnimatorController = null; animator.runtimeAnimatorController = controller;
                    animator.Rebind(); animator.Update(0);
                    float stride = MeasureStride(animator, "Run", out float sway);
                    comparison.AppendLine($"{candidate}: stride {stride:F3} m/s; hip lateral range {sway:F3} m");
                    if (stride > walk && sway < bestSway) { bestClip = clip; bestSway = sway; run = stride; }
                }
                runState.motion = bestClip; EditorUtility.SetDirty(runState);
                if (walk < .3f || walk > 4 || run < walk || run > 8)
                    throw new InvalidOperationException($"Unexpected measured stride speeds: walk {walk}, run {run}");
                player.walkStrideSpeed = walk; player.runStrideSpeed = run;
                if (suppliedRun)
                {
                    animator.runtimeAnimatorController = null; animator.runtimeAnimatorController = controller; animator.Rebind(); animator.Update(0);
                    float backRun = MeasureStride(animator, "RunBack", out _);
                    float backWalk = MeasureStride(animator, "WalkBack", out _);
                    if (backRun < .3f || backWalk < .3f) throw new InvalidOperationException("Backward clips have no usable stride.");
                    player.backRunStrideSpeed = backRun; player.backWalkStrideSpeed = backWalk;
                    comparison.AppendLine($"Backward run stride {backRun:F3}; backward walk stride {backWalk:F3} m/s.");
                }
                player.walkSpeed = 1.5f; player.runSpeed = 5; EditorUtility.SetDirty(player);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                File.WriteAllText("LocomotionCalibration-v3.txt", $"PASS: 60 sampled poses per clip.\n{comparison}\nSelected: {AssetDatabase.GetAssetPath(bestClip)}\nWalk travel 1.5 m/s; run travel 5 m/s.\nNative walk stride: {walk:F3}; run stride: {run:F3}. Cadence follows travel speed.");
            }
            catch (Exception exception) { File.WriteAllText("LocomotionCalibration-v3.txt", "FAILED: " + exception); Debug.LogException(exception); }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static float MeasureStride(Animator animator, string state, out float sway)
        {
            const int samples = 60;
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            var positions = new Vector3[2, samples + 1];
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            float minX = float.MaxValue, maxX = float.MinValue;
            animator.Play(state, 0, 0); animator.Update(0);
            float duration = animator.GetCurrentAnimatorStateInfo(0).length;
            for (int i = 0; i <= samples; i++)
            {
                animator.Play(state, 0, (float)i / samples); animator.Update(0);
                positions[0, i] = left.position; positions[1, i] = right.position;
                minX = Mathf.Min(minX, hips.position.x); maxX = Mathf.Max(maxX, hips.position.x);
            }
            var speeds = new List<float>();
            for (int foot = 0; foot < 2; foot++)
            {
                float low = float.MaxValue, high = float.MinValue;
                for (int i = 0; i < samples; i++) { low = Mathf.Min(low, positions[foot, i].y); high = Mathf.Max(high, positions[foot, i].y); }
                float contactHeight = low + (high - low) * .3f + .01f;
                for (int i = 1; i < samples; i++)
                {
                    float backwardSpeed = (positions[foot, i - 1].z - positions[foot, i].z) * samples / duration * (state.EndsWith("Back") ? -1 : 1);
                    if (positions[foot, i].y <= contactHeight && backwardSpeed > .1f) speeds.Add(backwardSpeed);
                }
            }
            speeds.Sort();
            sway = maxX - minX;
            return speeds.Count > 0 ? speeds[speeds.Count / 2] : 0;
        }
        static void FirstRepair()
        {
            if (File.Exists("AnimationFixResult.txt")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            { EditorApplication.delayCall += FirstRepair; return; }
            if (EditorSceneManager.GetActiveScene().isDirty) return;
            Repair();
        }
        [MenuItem("Riftbound/Fix animation imports and grounding")]
        public static void Repair()
        {
            if (EditorApplication.isPlaying) return;
            try
            {
                int count = 0;
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art/Brute" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                    PrototypeImport.ConfigureAnimations(importer, path);
                    importer.SaveAndReimport(); count++;
                }
                ConfigureController(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/RPG/Animations/Brute.controller"));
                if (EditorSceneManager.GetActiveScene().path == "Assets/RPG/Scenes/Outpost.unity")
                {
                    foreach (var player in UnityEngine.Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                    { AddGrounding(player.gameObject, player.animator); player.attackDuration = 1.2f; player.cleaveDuration = 2.433333f / 2; EditorUtility.SetDirty(player); }
                    foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    { AddGrounding(enemy.gameObject, enemy.animator); enemy.attackDuration = 1.2f; EditorUtility.SetDirty(enemy); }
                    EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
                    EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                }
                AssetDatabase.SaveAssets();
                File.WriteAllText("AnimationFixResult.txt", "Reimported " + count + " models; locomotion loops, full attacks at 2x speed, foot grounding installed.");
            }
            catch (Exception exception) { File.WriteAllText("AnimationFixResult.txt", exception.ToString()); Debug.LogException(exception); }
        }
        public static void ConfigureController(AnimatorController controller)
        {
            if (!controller) return;
            var machine = controller.layers[0].stateMachine;
            foreach (string stateName in new[] { "RunBack", "WalkBack" })
                if (!machine.states.Any(s => s.state.name == stateName)) machine.AddState(stateName);
            foreach (var child in controller.layers[0].stateMachine.states)
            {
                string clipName = child.state.name == "Walk" ? "unarmed walk forward" : child.state.name == "Run" ? "Brute@Unarmed Run Forward" : child.state.name == "RunBack" ? "Brute@Unarmed Run Back" : child.state.name == "WalkBack" ? "unarmed walk back" : null;
                if (clipName != null)
                    child.state.motion = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/Brute/" + clipName + ".fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__"));
                child.state.speed = child.state.name == "Attack" || child.state.name == "Cleave" ? 2 : 1;
                child.state.iKOnFeet = !child.state.name.StartsWith("Run");
                EditorUtility.SetDirty(child.state);
            }
            EditorUtility.SetDirty(controller);
        }
        static void AddGrounding(GameObject actor, Animator animator)
        {
            var grounding = actor.GetComponent<GroundedVisual>();
            if (!grounding) grounding = actor.AddComponent<GroundedVisual>();
            grounding.animator = animator; EditorUtility.SetDirty(grounding);
        }
    }
}
