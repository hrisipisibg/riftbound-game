using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class MovementSetup
    {
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity"); Setup();
                PrototypeBuild.BuildWindows();
                if (!File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")) throw new Exception("Build failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("MovementResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [MenuItem("Riftbound/Calibrate contact and add training dummy")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player || player.animator.isHuman) throw new InvalidOperationException("Open Outpost-Updated with its native rig.");
            const string backup = "Assets/RPG/Scenes/Backups/Outpost-before-contact-calibration.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            var copy = UnityEngine.Object.Instantiate(player.animator.gameObject); copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); copy.transform.localScale = player.animator.transform.lossyScale;
            var animator = copy.GetComponent<Animator>(); animator.speed = 1; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Update(0);
            Transform Bone(string name) => copy.GetComponentsInChildren<Transform>().First(t => t.name.EndsWith(":" + name));
            var left = Bone("LeftToeBase"); var right = Bone("RightToeBase"); var hips = Bone("Hips");
            var report = new StringBuilder();
            try
            {
                animator.Play("Idle", 0, 0); animator.Update(0); float idle = Mathf.Min(left.position.y, right.position.y);
                var grounding = player.GetComponent<GroundedVisual>();
                foreach (string state in new[] { "Walk", "Run", "WalkBack", "RunBack" })
                {
                    var positions = new Vector3[2, 121]; float low = 999, high = -999, peak = -999, phase = 0;
                    animator.Play(state, 0, 0); animator.Update(0); float duration = animator.GetCurrentAnimatorStateInfo(0).length;
                    for (int i = 0; i <= 120; i++)
                    {
                        animator.Play(state, 0, i / 120f); animator.Update(0);
                        positions[0, i] = left.position; positions[1, i] = right.position;
                        float forward = left.position.z - hips.position.z;
                        if (forward > peak) { peak = forward; phase = i / 120f; }
                        low = Mathf.Min(low, left.position.y, right.position.y); high = Mathf.Max(high, left.position.y, right.position.y);
                    }
                    var speeds = new List<float>();
                    for (int foot = 0; foot < 2; foot++)
                    for (int i = 1; i < 120; i++)
                    {
                        float velocity = (positions[foot, i - 1].z - positions[foot, i].z) * 120 / duration * (state.EndsWith("Back") ? -1 : 1);
                        if (positions[foot, i].y < low + (high - low) * .2f + .005f && velocity > .2f && velocity < 8) speeds.Add(velocity);
                    }
                    speeds.Sort();
                    if (speeds.Count < 5) throw new InvalidOperationException("Too few planted-foot samples: " + state);
                    float speed = speeds[(int)((speeds.Count - 1) * .75f)]; float offset = Mathf.Clamp(idle - low, -.18f, .18f);
                    report.AppendLine($"PASS {state}: contact speed {speed:F3}m/s; phase {phase:F3}; vertical offset {offset:F3}m; {speeds.Count} stance samples.");
                    if (state == "Walk") { player.walkStrideSpeed = speed; player.walkPhase = phase; grounding.walkOffset = offset; }
                    if (state == "Run") { player.runStrideSpeed = speed; player.runPhase = phase; grounding.runOffset = offset; }
                    if (state == "WalkBack") { player.backWalkStrideSpeed = speed; player.backWalkPhase = phase; grounding.backWalkOffset = offset; }
                    if (state == "RunBack") { player.backRunStrideSpeed = speed; player.backRunPhase = phase; grounding.backRunOffset = offset; }
                }
                EditorUtility.SetDirty(player); EditorUtility.SetDirty(grounding);
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            if (!GameObject.Find("Village training dummy"))
            {
                var root = new GameObject("Village training dummy"); root.transform.position = new Vector3(7, -.06f, -6);
                var motor = root.AddComponent<CharacterController>(); motor.height = 2.3f; motor.center = Vector3.up * 1.15f; motor.radius = .4f;
                var stats = root.AddComponent<Combatant>(); stats.maxHealth = 500; stats.displayName = "Training Dummy";
                var dummy = root.AddComponent<EnemyController>(); dummy.trainingDummy = true; dummy.campGuard = true; dummy.enemyId = "village-training"; dummy.player = player;
                var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
                var stone = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Foundation.mat");
                Part(root.transform, new Vector3(0, 1.1f, 0), new Vector3(.25f, 2.2f, .25f), wood);
                Part(root.transform, new Vector3(0, 1.6f, 0), new Vector3(1.5f, .2f, .2f), wood);
                Part(root.transform, new Vector3(0, 1.3f, -.15f), new Vector3(.8f, .9f, .25f), stone);
                var label = new GameObject("Training sign"); label.transform.SetParent(root.transform, false); label.transform.localPosition = new Vector3(0, 2.7f, -.1f);
                var text = label.AddComponent<TextMesh>(); text.text = "TRAINING\nResets health / no rewards"; text.characterSize = .045f; text.fontSize = 48; text.anchor = TextAnchor.MiddleCenter;
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            report.AppendLine("PASS Training dummy configured without quest/reward credit.\nPlay from legacy Outpost routes to Outpost-Updated. Gameplay testing left to user.");
            File.WriteAllText("MovementResult.txt", report.ToString());
        }
        static void Part(Transform parent, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = size; part.GetComponent<Renderer>().sharedMaterial = material; UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }
    }
}
