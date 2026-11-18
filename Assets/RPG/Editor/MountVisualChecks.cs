using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class MountVisualChecks
    {
        public static void Run()
        {
            var report = new StringBuilder("Native disposable mount animation checks; NOT gameplay, rendered hoof contact or online verification.\n");
            var temporary = new List<Object>();
            var scene = EditorSceneManager.NewPreviewScene();
            int passed = 0, failed = 0;
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Prefabs/Mount Horse.prefab");
                if (!prefab) throw new InvalidOperationException("Horse prefab missing.");
                var horse = Clone(prefab);
                var mount = horse.GetComponent<MountVisual>();
                var firstAnimator = mount.animator;
                var originalController = firstAnimator.runtimeAnimatorController;
                var idle = ((AnimatorController)originalController).layers[0].stateMachine.states.Single(s => s.state.name == "Idle").state.motion as AnimationClip;

                mount.Animate(3, false, true);
                Record(Near(firstAnimator.speed, 3 / mount.walkStride), "Walking uses prefab walkStride at3m/s, above old1.25 cadence cap");
                mount.Animate(8, false);
                Record(Near(firstAnimator.speed, 8 / mount.runStride), "Running uses prefab runStride at8m/s");
                mount.walkStride = 100; mount.Animate(1, false, true);
                Record(Near(firstAnimator.speed, .65f), "Walking cadence has finite0.65 lower bound");
                mount.walkStride = 1; mount.Animate(20, false, true);
                Record(Near(firstAnimator.speed, 2.25f), "Walking cadence has2.25 upper bound");
                mount.walkStride = float.NaN; mount.Animate(3, false, true);
                Record(Near(firstAnimator.speed, 1), "Invalid stride uses neutral finite cadence");
                mount.walkStride = prefab.GetComponent<MountVisual>().walkStride;
                mount.Animate(0, false);
                Record(Near(firstAnimator.speed, 1), "Idle stays at authored playback speed");
                mount.Animate(8, true);
                Record(Near(firstAnimator.speed, 1), "Airborne Jump stays at authored playback speed");
                mount.Animate(float.NaN, false);
                firstAnimator.Update(.3f);
                Record(Near(firstAnimator.speed, 1) && firstAnimator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Nonfinite movement input safely selects Idle");

                mount.Animate(3, false, true);
                firstAnimator.Update(.3f);
                firstAnimator.Play("Walk", 0, .45f); firstAnimator.Update(0);
                float before = firstAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime;
                mount.Animate(3, false, true); firstAnimator.Update(0);
                Record(!firstAnimator.IsInTransition(0) && Near(firstAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime, before), "Repeated walking does not restart normalized phase or transition");

                mount.Animate(8, false); firstAnimator.Update(.3f);
                var other = Clone(prefab).GetComponent<MountVisual>().animator;
                other.Play("Idle", 0, 0); other.Update(0);
                mount.animator = other; mount.Animate(8, false); other.Update(.3f);
                Record(other.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Replacing Animator invalidates state cache even when desired state is unchanged");

                var overrides = new AnimatorOverrideController(other.runtimeAnimatorController);
                temporary.Add(overrides);
                other.runtimeAnimatorController = overrides; other.Rebind(); other.Play("Idle", 0, 0); other.Update(0);
                mount.Animate(8, false); other.Update(.3f);
                Record(other.GetCurrentAnimatorStateInfo(0).IsName("Run"), "Replacing controller with override invalidates state cache");

                other.runtimeAnimatorController = OnlyState("Idle", idle); other.Rebind(); other.Update(0);
                mount.Animate(8, false); other.Update(.3f);
                Record(other.GetCurrentAnimatorStateInfo(0).IsName("Idle") && Near(other.speed, 1), "Missing Run falls back to existing Idle at neutral speed");
                mount.Animate(8, true); other.Update(.3f);
                Record(other.GetCurrentAnimatorStateInfo(0).IsName("Idle") && Near(other.speed, 1), "Missing Jump falls back safely without a missing-state transition");

                other.runtimeAnimatorController = OnlyState("Unrelated", idle); other.Rebind(); other.Update(0);
                mount.Animate(8, false);
                Record(Near(other.speed, 1), "Controller with no supported state is safe and neutral");
                other.runtimeAnimatorController = null;
                mount.Animate(8, false);
                Record(true, "Missing controller returns without exception");
                mount.animator = null; mount.Animate(8, false);
                Record(true, "Missing Animator returns without exception");
            }
            catch (Exception e) { failed++; report.AppendLine("FAIL " + e); }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach (var value in temporary) if (value) Object.DestroyImmediate(value);
                Directory.CreateDirectory(".code-check/presentation");
                report.AppendLine($"TOTAL PASS={passed} FAIL={failed}");
                File.WriteAllText(".code-check/presentation/mount-visual-checks.txt", report.ToString());
            }
            Debug.Log($"MOUNT_VISUAL_CHECKS PASS={passed} FAIL={failed}");
            if (Application.isBatchMode) EditorApplication.Exit(failed == 0 ? 0 : 1);

            GameObject Clone(GameObject prefab)
            {
                var copy = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var anim = copy.GetComponentInChildren<Animator>();
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.applyRootMotion = false; anim.Rebind(); anim.Update(0);
                return copy;
            }
            AnimatorController OnlyState(string name, AnimationClip clip)
            {
                var machine = new AnimatorStateMachine { name = "Transient mount check" };
                var state = machine.AddState(name); state.motion = clip; machine.defaultState = state;
                var controller = new AnimatorController { name = "Transient mount check", layers = new[] { new AnimatorControllerLayer { name = "Base Layer", stateMachine = machine, defaultWeight = 1 } } };
                temporary.Add(controller); temporary.Add(machine); temporary.Add(state);
                return controller;
            }
            void Record(bool valid, string label)
            {
                if (valid) passed++; else failed++;
                report.AppendLine((valid ? "PASS " : "FAIL ") + label);
            }
        }
        static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    }
}
