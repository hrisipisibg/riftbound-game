using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class PresentationPosePreview
    {
        static bool Approved => Environment.GetCommandLineArgs().Contains("-presentationApproved");
        static string Folder => Approved ? ".code-check/presentation/approved-previews/" : ".code-check/presentation/previews/";
        [Serializable] class Receipt
        {
            public string evidence = "Rendered disposable editor poses and candidate clip overrides; NOT applied assets, animation playback, gameplay or online verification";
            public string graphicsDevice;
            public string candidateLimit = "Static normalized phase only; real backward state replacement requires positive controller speed and full-cycle validation. Sole-normalized captures move disposable visual roots only, not production actors.";
            public List<string> images = new List<string>();
            public List<string> errors = new List<string>();
        }

        // No shared controller is changed: overrides exist on disposable visual instances only.
        public static void Run()
        {
            var receipt = new Receipt { graphicsDevice = SystemInfo.graphicsDeviceType.ToString() };
            try
            {
                if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Isolated batch outside Play Mode only.");
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    throw new InvalidOperationException("Graphics device unavailable; run without -nographics.");
                Directory.CreateDirectory(Folder);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                if (Approved)
                {
                    foreach (string model in new[] { "Undead_Warrior", "Undead_Rogue", "Undead_Mage", "Undead_Paladin" })
                    foreach (string state in new[] { "WalkBack", "Jump", "Ride" })
                        Capture(model, state, null, receipt);
                }
                else
                {
                    foreach (string state in new[] { "Idle", "Walk", "Run", "WalkBack" }) Capture("Human_Warrior", state, null, receipt);
                    Capture("Human_Warrior", "Idle", null, receipt, true);
                    Capture("Orc_Warrior", "Idle", null, receipt);
                    Capture("Orc_Warrior", "Idle", null, receipt, true);
                    foreach (string model in new[] { "Undead_Warrior", "Undead_Rogue", "Undead_Mage" })
                    foreach (var pair in new[] { ("Ride", "Sit_Chair_Idle"), ("Block", "Block"), ("Jump", "Jump_Idle"), ("WalkBack", "Walking_Backwards") })
                    {
                        Capture(model, pair.Item1, null, receipt);
                        Capture(model, pair.Item1, pair.Item2, receipt);
                    }
                }
            }
            catch (Exception e) { receipt.errors.Add(e.ToString()); }
            finally
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Folder + "receipt.json", JsonUtility.ToJson(receipt, true));
            }
            Debug.Log($"PRESENTATION_POSE_PREVIEW images={receipt.images.Count} errors={receipt.errors.Count}");
            if (Application.isBatchMode) EditorApplication.Exit(receipt.errors.Count == 0 ? 0 : 1);
        }

        static void Capture(string model, string state, string candidate, Receipt receipt, bool normalizeSole = false)
        {
            GameObject copy = null, floor = null;
            Material material = null;
            AnimatorOverrideController overrides = null;
            try
            {
                var prefab = Resources.Load<GameObject>("Characters/" + model);
                if (!prefab) throw new InvalidOperationException("Missing preview prefab " + model);
                copy = UnityEngine.Object.Instantiate(prefab);
                var animator = copy.GetComponentInChildren<Animator>();
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (!animator.HasState(0, Animator.StringToHash(state))) throw new InvalidOperationException("Missing state " + state);
                if (candidate != null)
                {
                    string family = model.EndsWith("Mage", StringComparison.Ordinal) ? "Mage" : model.EndsWith("Rogue", StringComparison.Ordinal) ? "Rogue" : "Warrior";
                    var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/Skeletons/Skeleton_" + family + ".fbx")
                        .OfType<AnimationClip>().Single(c => c.name == candidate);
                    animator.Play(state, 0, 0); animator.Update(0);
                    var current = animator.GetCurrentAnimatorClipInfo(0);
                    if (current.Length != 1) throw new InvalidOperationException("Ambiguous current clip " + state);
                    overrides = new AnimatorOverrideController(animator.runtimeAnimatorController);
                    overrides[current[0].clip.name] = clip;
                    animator.runtimeAnimatorController = overrides;
                }
                animator.Rebind(); animator.Play(state, 0, .35f); animator.Update(0);
                if (normalizeSole)
                {
                    if (!animator.isHuman || !PresentationGroundingAudit.TryEstimateSole(animator,
                        animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot), out float sole))
                        throw new InvalidOperationException("No geometric sole estimate for normalized preview " + model);
                    copy.transform.position += Vector3.up * (.025f - sole);
                }
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Temporary zero-height reference floor";
                floor.transform.position = new Vector3(0, -.025f, 0);
                floor.transform.localScale = new Vector3(4, .05f, 4);
                UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.color = new Color(.24f, .30f, .33f);
                floor.GetComponent<Renderer>().sharedMaterial = material;
                floor.layer = 30;
                string filename = model + "-" + state + (normalizeSole ? "-sole-normalized-candidate" : candidate == null ? "-current" : "-candidate-" + candidate) + ".png";
                CharacterArtSetup.RenderPose(copy, Folder + filename);
                if (!File.Exists(Folder + filename)) throw new InvalidOperationException("No rendered output " + filename);
                receipt.images.Add(filename);
            }
            finally
            {
                if (copy) UnityEngine.Object.DestroyImmediate(copy);
                if (floor) UnityEngine.Object.DestroyImmediate(floor);
                if (material) UnityEngine.Object.DestroyImmediate(material);
                if (overrides) UnityEngine.Object.DestroyImmediate(overrides);
            }
        }
    }
}
