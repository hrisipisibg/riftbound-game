using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    // Samples disposable instances in a preview scene; never saves or reimports assets.
    public static class PresentationAssetAudit
    {
        const string Output = ".code-check/presentation/asset-audit.json";
        const int Samples = 60;

        [Serializable] public class Report
        {
            public string evidence = "Native editor asset/pose sampling; NOT rendered, grounding, gameplay or online verification";
            public string unityVersion;
            public int approvedControllerChecks;
            public List<Model> models = new List<Model>();
            public List<string> errors = new List<string>();
        }

        [Serializable] public class Model
        {
            public string path, mountId, controller, avatar;
            public bool human, validAvatar, applyRootMotion, hasSaddle;
            public Vector3 rootScale, saddlePosition;
            public float height, idleMeshBottom, idleFootBottom, idleEstimatedSoleY, configuredWalkStride, configuredRunStride;
            public bool hasFeet, hasEstimatedSole;
            public string[] flags, genericBones;
            public List<State> states = new List<State>();
        }

        [Serializable] public class State
        {
            public string name, clip, motionName;
            public bool looping, humanMotion, sampled;
            public float stateSpeed, duration, footMin, footMax, hipsLateralRange, estimatedContactStride;
            public int contactSamples;
        }

        [MenuItem("Riftbound/Presentation/Read-only asset and pose audit")]
        public static void Run()
        {
            var report = new Report { unityVersion = Application.unityVersion };
            Scene preview = default;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Run outside Play Mode.");
                preview = EditorSceneManager.NewPreviewScene();
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/RPG/Resources/Characters" }).OrderBy(x => AssetDatabase.GUIDToAssetPath(x), StringComparer.Ordinal))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Inspect(AssetDatabase.LoadAssetAtPath<GameObject>(path), path, null, preview, report);
                }
                foreach (string guid in AssetDatabase.FindAssets("t:MountDefinition", new[] { "Assets/RPG/Data/Mounts" }).OrderBy(x => AssetDatabase.GUIDToAssetPath(x), StringComparer.Ordinal))
                {
                    var definition = AssetDatabase.LoadAssetAtPath<MountDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                    if (!definition || !definition.prefab)
                        report.errors.Add("Missing mount definition/prefab: " + AssetDatabase.GUIDToAssetPath(guid));
                    else
                        Inspect(definition.prefab.gameObject, AssetDatabase.GetAssetPath(definition.prefab), definition, preview, report);
                }
                if (report.models.Count == 0) report.errors.Add("No models inspected.");
                if (Environment.GetCommandLineArgs().Contains("-presentationApproved")) VerifyApprovedControllers(report);
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                Directory.CreateDirectory(Path.GetDirectoryName(Output));
                File.WriteAllText(Output, JsonUtility.ToJson(report, true));
            }
            Debug.Log($"PRESENTATION_ASSET_AUDIT models={report.models.Count} errors={report.errors.Count} report={Output}");
            if (Application.isBatchMode) EditorApplication.Exit(report.errors.Count == 0 ? 0 : 1);
        }

        static void VerifyApprovedControllers(Report report)
        {
            foreach (string family in new[] { "Warrior", "Mage", "Rogue", "Paladin" })
            {
                var model = report.models.Single(m => m.path.EndsWith("/Undead_" + family + ".prefab", StringComparison.Ordinal));
                foreach (var expected in new[] { ("WalkBack", "Walking_Backwards"), ("Jump", "Jump_Idle"), ("Ride", "Sit_Chair_Idle") })
                {
                    var state = model.states.Single(s => s.name == expected.Item1);
                    if (state.motionName != expected.Item2 || state.stateSpeed != 1 || !state.looping || !state.sampled)
                        report.errors.Add($"Approved controller binding failed: Undead_{family}/{expected.Item1}");
                    else report.approvedControllerChecks++;
                }
            }
            // Paladin intentionally shares Warrior's controller: 9 bindings, 12 consumer checks.
            if (report.approvedControllerChecks != 12) report.errors.Add("Expected all 12 approved Undead consumer checks.");
        }

        static void Inspect(GameObject prefab, string path, MountDefinition definition, Scene scene, Report report)
        {
            GameObject instance = null;
            var model = new Model { path = path, mountId = definition ? definition.mountId : "" };
            var flags = new List<string>();
            report.models.Add(model);
            try
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                model.rootScale = instance.transform.localScale;
                var animator = instance.GetComponentInChildren<Animator>(true);
                if (!animator) { flags.Add("No Animator"); return; }
                model.applyRootMotion = animator.applyRootMotion;
                model.controller = AssetDatabase.GetAssetPath(animator.runtimeAnimatorController);
                model.avatar = AssetDatabase.GetAssetPath(animator.avatar);
                model.validAvatar = animator.avatar && animator.avatar.isValid;
                model.human = animator.isHuman;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                animator.Rebind();
                animator.Update(0);
                Transform left = null, right = null, hips = null;
                if (animator.isHuman)
                {
                    left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                }
                else
                {
                    model.genericBones = animator.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
                    foreach (var bone in animator.GetComponentsInChildren<Transform>(true))
                    {
                        if (bone.name == "foot.l" || bone.name == "FrontFoot.L" || bone.name.EndsWith(":LeftFoot", StringComparison.Ordinal)) left = bone;
                        if (bone.name == "foot.r" || bone.name == "FrontFoot.R" || bone.name.EndsWith(":RightFoot", StringComparison.Ordinal)) right = bone;
                        if (bone.name == "pelvis" || bone.name == "hips" || bone.name == "Hips" || bone.name.EndsWith(":Hips", StringComparison.Ordinal)) hips = bone;
                    }
                }
                model.hasFeet = left && right;
                var mount = instance.GetComponent<MountVisual>();
                if (mount)
                {
                    model.configuredWalkStride = mount.walkStride;
                    model.configuredRunStride = mount.runStride;
                    model.hasSaddle = mount.saddle;
                    if (mount.saddle) model.saddlePosition = instance.transform.InverseTransformPoint(mount.saddle.position);
                }
                if (animator.runtimeAnimatorController is AnimatorController controller && controller.layers.Length > 0)
                    InspectStates(controller.layers[0].stateMachine, controller.layers[0].name, animator, instance.transform, left, right, hips, model, flags);
                else flags.Add("Controller missing or unsupported by this audit");
                if (animator.HasState(0, Animator.StringToHash("Idle")))
                {
                    animator.Play("Idle", 0, 0); animator.Update(0);
                }
                bool hasBounds = false;
                Bounds bounds = default;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer is ParticleSystemRenderer || renderer is TrailRenderer || renderer is LineRenderer) continue;
                    if (renderer.sharedMaterials.Any(m => !m || !m.shader)) flags.Add("Missing material/shader: " + renderer.name);
                    if (renderer is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true;
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                model.height = hasBounds ? bounds.size.y : 0;
                model.idleMeshBottom = hasBounds ? bounds.min.y : 0;
                if (model.hasFeet) model.idleFootBottom = Mathf.Min(left.position.y, right.position.y);
                model.hasEstimatedSole = PresentationGroundingAudit.TryEstimateSole(animator, left, right, out float sole);
                if (model.hasEstimatedSole) model.idleEstimatedSoleY = sole;
                if (model.applyRootMotion) flags.Add("Root motion enabled on source prefab");
            }
            catch (Exception e) { report.errors.Add(path + ": " + e.Message); }
            finally
            {
                model.flags = flags.Distinct().ToArray();
                if (instance) Object.DestroyImmediate(instance);
            }
        }

        static void InspectStates(AnimatorStateMachine machine, string prefix, Animator animator, Transform root,
            Transform left, Transform right, Transform hips, Model model, List<string> flags)
        {
            foreach (var child in machine.states)
            {
                var source = child.state;
                var clip = source.motion as AnimationClip;
                var row = new State { name = source.name, stateSpeed = source.speed, motionName = clip ? clip.name : "",
                    clip = AssetDatabase.GetAssetPath(clip), duration = clip ? clip.length : 0,
                    looping = clip && clip.isLooping, humanMotion = clip && clip.humanMotion };
                model.states.Add(row);
                if (!clip) { flags.Add("No single clip: " + source.name); continue; }
                if (clip.humanMotion != animator.isHuman) flags.Add("Rig/clip mismatch: " + source.name);
                if (source.speed < 0) flags.Add("Reverse-played forward clip: " + source.name);
                if (!left || !right || !hips || source.speed == 0 || clip.length <= 0) continue;
                var feet = new Vector3[2, Samples + 1];
                float minHipX = float.PositiveInfinity, maxHipX = float.NegativeInfinity;
                float minFoot = float.PositiveInfinity, maxFoot = float.NegativeInfinity;
                for (int sample = 0; sample <= Samples; sample++)
                {
                    float phase = Mathf.Min(.9999f, (float)sample / Samples);
                    animator.Play(prefix + "." + source.name, 0, source.speed < 0 ? .9999f - phase : phase);
                    animator.Update(0);
                    feet[0, sample] = root.InverseTransformPoint(left.position);
                    feet[1, sample] = root.InverseTransformPoint(right.position);
                    float x = root.InverseTransformPoint(hips.position).x;
                    minHipX = Mathf.Min(minHipX, x); maxHipX = Mathf.Max(maxHipX, x);
                    minFoot = Mathf.Min(minFoot, feet[0, sample].y, feet[1, sample].y);
                    maxFoot = Mathf.Max(maxFoot, feet[0, sample].y, feet[1, sample].y);
                }
                row.sampled = true;
                row.footMin = minFoot; row.footMax = maxFoot;
                row.hipsLateralRange = maxHipX - minHipX;
                var velocities = new List<float>();
                for (int foot = 0; foot < 2; foot++)
                {
                    float low = float.PositiveInfinity, high = float.NegativeInfinity;
                    for (int i = 0; i <= Samples; i++) { low = Mathf.Min(low, feet[foot, i].y); high = Mathf.Max(high, feet[foot, i].y); }
                    float contactHeight = low + .3f * (high - low) + .01f;
                    for (int i = 1; i <= Samples; i++)
                    {
                        float velocity = (feet[foot, i - 1].z - feet[foot, i].z) * Samples * Mathf.Abs(source.speed) / clip.length;
                        if (source.name.EndsWith("Back", StringComparison.Ordinal)) velocity = -velocity;
                        if (feet[foot, i].y <= contactHeight && velocity > .1f) velocities.Add(velocity);
                    }
                }
                velocities.Sort(); row.contactSamples = velocities.Count;
                row.estimatedContactStride = velocities.Count > 0 ? velocities[velocities.Count / 2] : 0;
            }
            foreach (var child in machine.stateMachines)
                InspectStates(child.stateMachine, prefix + "." + child.stateMachine.name, animator, root, left, right, hips, model, flags);
        }
    }
}
