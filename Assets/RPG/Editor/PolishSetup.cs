using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Riftbound.Editor
{
    public static class PolishSetup
    {
        public static void BatchCheck()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity");
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(); Validate(player);
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    var copy = UnityEngine.Object.Instantiate(player.animator.gameObject); copy.hideFlags = HideFlags.HideAndDontSave;
                    copy.transform.SetPositionAndRotation(new Vector3(0, -400, 0), Quaternion.identity); copy.transform.localScale = player.animator.transform.lossyScale;
                    try
                    {
                        var animator = copy.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                        foreach (string state in new[] { "Jump", "Hit", "Death" })
                        { animator.Play(state, 0, state == "Death" ? .95f : .5f); animator.Update(0); CharacterArtSetup.RenderPose(copy, "Polished-" + state + ".png"); }
                    }
                    finally { UnityEngine.Object.DestroyImmediate(copy); }
                }
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("PolishResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity");
                Setup();
                PrototypeBuild.BuildWindows();
                if (!File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")) throw new Exception("Windows build failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("PolishResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [MenuItem("Riftbound/Add merchant, loot and animation polish")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            if (!player) throw new InvalidOperationException("Open an Outpost scene first.");
            if (player.animator.isHuman) CharacterArtSetup.ApplyNativePlayer();
            Directory.CreateDirectory("Assets/RPG/Scenes/Backups");
            string backup = "Assets/RPG/Scenes/Backups/Outpost-before-merchant-polish.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            ConfigureAnimations(player);
            if (!UnityEngine.Object.FindFirstObjectByType<Merchant>())
            {
                var npc = UnityEngine.Object.Instantiate(player.questGiver.gameObject); npc.name = "Quartermaster Rowan";
                npc.transform.position = new Vector3(-7, -.05f, -2); npc.transform.rotation = Quaternion.Euler(0, 120, 0);
                var giver = npc.GetComponent<QuestGiver>(); if (giver) UnityEngine.Object.DestroyImmediate(giver);
                npc.AddComponent<Merchant>();
            }
            if (!GameObject.Find("06 - Adventure discoveries"))
            {
                var root = new GameObject("06 - Adventure discoveries").transform;
                Chest("village-cache", new Vector3(7, -.05f, 6), 10, 15, root);
                Chest("camp-cache", new Vector3(35, -.15f, 72), 25, 40, root);
                Chest("tower-cache", new Vector3(42, -.15f, 97), 40, 80, root);
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Validate(player);
        }
        [MenuItem("Riftbound/Rebake mounted riding pose")]
        public static void RebakeRide()
        {
            const string Root = "Assets/RPG/";
            string ridePath = Root + "Animations/Player Ride.anim";
            var source = AssetDatabase.LoadAllAssetsAtPath(Root + "Art/UniversalAnimations/UAL1_Standard.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__") && c.name.EndsWith("|Sitting_Idle_Loop"));
            Bake(source, ridePath, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Rebaked Player Ride.anim with natural leg spread (22 degrees).");
        }
        public static void ConfigureAnimations(PlayerController player)
        {
            var grounding = player.GetComponent<GroundedVisual>();
            if (grounding) { grounding.soleHeight = .025f; EditorUtility.SetDirty(grounding); }
            const string sourcePath = "Assets/RPG/Art/UniversalAnimations/UAL1_Standard.fbx";
            var sourceClips = AssetDatabase.LoadAllAssetsAtPath(sourcePath).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
            var controller = (AnimatorController)player.animator.runtimeAnimatorController;
            foreach (var item in new[] { ("Death", "Death01", false), ("Hit", "Hit_Chest", false), ("Jump", "Jump_Loop", true) })
            {
                string path = "Assets/RPG/Animations/Player " + item.Item1 + ".anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (!clip || Environment.GetCommandLineArgs().Contains("-rebake")) clip = Bake(sourceClips.First(c => c.name.EndsWith("|" + item.Item2)), path, item.Item3);
                var state = controller.layers[0].stateMachine.states.FirstOrDefault(s => s.state.name == item.Item1).state;
                if (!state) state = controller.layers[0].stateMachine.AddState(item.Item1);
                state.motion = clip; state.speed = item.Item1 == "Hit" ? clip.length / .28f : 1; state.iKOnFeet = false;
                EditorUtility.SetDirty(state);
            }
            EditorUtility.SetDirty(controller);
        }
        internal static AnimationClip Bake(AnimationClip source, string path, bool airPose, bool mounted = false)
        {
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/Brute/Brute.fbx"));
            model.hideFlags = HideFlags.HideAndDontSave; model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var animator = model.GetComponent<Animator>(); animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.runtimeAnimatorController = null;
            var graph = PlayableGraph.Create("Bake Brute native reaction");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, source); playable.SetApplyFootIK(false);
            var output = AnimationPlayableOutput.Create(graph, "Brute pose", animator); output.SetSourcePlayable(playable); graph.Play();
            var bones = model.GetComponentsInChildren<Transform>().Where(t => t.name.StartsWith("mixamorig:")).ToArray();
            var hips = bones.First(t => t.name == "mixamorig:Hips");
            var leftLeg = bones.First(t => t.name == "mixamorig:LeftUpLeg");
            var rightLeg = bones.First(t => t.name == "mixamorig:RightUpLeg");
            var referenceHips = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/Brute/Brute.fbx").GetComponentsInChildren<Transform>().First(t => t.name == "mixamorig:Hips").localPosition;
            int frames = Mathf.CeilToInt(source.length * 30);
            var curves = bones.Select(_ => Enumerable.Range(0, 7).Select(i => new AnimationCurve()).ToArray()).ToArray();
            try
            {
                for (int frame = 0; frame <= frames; frame++)
                {
                    float time = Mathf.Min(source.length, frame / 30f);
                    playable.SetTime(time); graph.Evaluate(0);
                    if (airPose) hips.localPosition = referenceHips;
                    if (mounted)
                    {
                        float leftSide = Mathf.Sign(Vector3.Dot(leftLeg.position - hips.position, model.transform.right));
                        float rightSide = Mathf.Sign(Vector3.Dot(rightLeg.position - hips.position, model.transform.right));
                        // Natural riding flare (22 degrees instead of extreme 65 split)
                        leftLeg.rotation = Quaternion.AngleAxis(leftSide * 22, model.transform.up) * leftLeg.rotation;
                        rightLeg.rotation = Quaternion.AngleAxis(rightSide * 22, model.transform.up) * rightLeg.rotation;
                    }
                    for (int i = 0; i < bones.Length; i++)
                    {
                        Vector3 p = bones[i].localPosition; Quaternion q = bones[i].localRotation;
                        float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                        for (int j = 0; j < 7; j++) curves[i][j].AddKey(time, values[j]);
                    }
                }
                var result = new AnimationClip { name = "Brute " + source.name, frameRate = 30 };
                string[] properties = { "localPosition.x", "localPosition.y", "localPosition.z", "localRotation.x", "localRotation.y", "localRotation.z", "localRotation.w" };
                for (int i = 0; i < bones.Length; i++)
                for (int j = 0; j < 7; j++) result.SetCurve(AnimationUtility.CalculateTransformPath(bones[i], model.transform), typeof(Transform), properties[j], curves[i][j]);
                result.EnsureQuaternionContinuity();
                var settings = AnimationUtility.GetAnimationClipSettings(result); settings.loopTime = airPose; settings.loopBlend = airPose; AnimationUtility.SetAnimationClipSettings(result, settings);
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing) { EditorUtility.CopySerialized(result, existing); UnityEngine.Object.DestroyImmediate(result); EditorUtility.SetDirty(existing); return existing; }
                AssetDatabase.CreateAsset(result, path); return result;
            }
            finally { graph.Destroy(); UnityEngine.Object.DestroyImmediate(model); }
        }
        internal static void Chest(string id, Vector3 position, int gold, int xp, Transform parent)
        {
            var root = new GameObject(id); root.transform.SetParent(parent); root.transform.position = position;
            var wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            var metal = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Foundation.mat");
            Part("Chest body", new Vector3(0, .4f, 0), new Vector3(1.2f, .8f, .9f), wood, root.transform);
            var hinge = new GameObject("Lid hinge").transform; hinge.SetParent(root.transform, false); hinge.localPosition = new Vector3(0, .85f, .45f);
            Part("Chest lid", new Vector3(0, 0, -.45f), new Vector3(1.25f, .16f, .95f), wood, hinge).GetComponent<Collider>().enabled = false;
            foreach (float x in new[] { -.4f, .4f }) Part("Iron chest band", new Vector3(x, .42f, -.465f), new Vector3(.1f, .85f, .045f), metal, root.transform);
            var chest = root.AddComponent<LootChest>(); chest.chestId = id; chest.gold = gold; chest.experience = xp; chest.lid = hinge;
        }
        static GameObject Part(string name, Vector3 position, Vector3 size, Material material, Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        static void Validate(PlayerController player)
        {
            var report = new StringBuilder();
            void Check(bool valid, string message) { report.AppendLine((valid ? "PASS " : "FAIL ") + message); if (!valid) throw new InvalidOperationException(message); }
            Check(UnityEngine.Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None).Length == 1, "Merchant exists");
            var chests = UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None);
            Check(chests.Length == 3 && chests.Select(c => c.chestId).Distinct().Count() == 3, "Three persistent caches with unique IDs");
            var controller = (AnimatorController)player.animator.runtimeAnimatorController;
            foreach (string name in new[] { "Hit", "Death", "Jump" })
            {
                var clip = (AnimationClip)controller.layers[0].stateMachine.states.First(s => s.state.name == name).state.motion;
                Check(clip && clip.length > .2f && !clip.isHumanMotion && AnimationUtility.GetCurveBindings(clip).Length > 100, name + " baked to native bone curves");
                if (name == "Death" || name == "Jump")
                {
                    var binding = AnimationUtility.GetCurveBindings(clip).First(b => b.path.EndsWith("mixamorig:Hips") && b.propertyName.EndsWith("Position.y"));
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float range = curve.keys.Max(k => k.value) - curve.keys.Min(k => k.value);
                    Check(name == "Death" ? range > .2f : range < .001f, name == "Death" ? "Death has a real body collapse" : "Jump height is driven once by character physics");
                }
            }
            var old = JsonUtility.FromJson<PlayerController.AdventureSave>("{\"version\":1,\"gold\":25,\"questIndex\":1}");
            Check(old.version == 1 && old.gold == 25 && old.chests == null && !old.temperedWeapon, "Old saves remain compatible");
            var data = new PlayerController.AdventureSave { temperedWeapon = true, chests = new[] { "camp-cache" } };
            var roundtrip = JsonUtility.FromJson<PlayerController.AdventureSave>(JsonUtility.ToJson(data));
            Check(roundtrip.temperedWeapon && roundtrip.chests.Length == 1, "Upgrade and loot save roundtrip");
            Directory.CreateDirectory("Library/PolishChecks");
            const string testFile = "Library/PolishChecks/save.json";
            File.WriteAllText(testFile, "1"); File.WriteAllText(testFile + ".tmp", "2"); File.Replace(testFile + ".tmp", testFile, testFile + ".bak");
            File.WriteAllText(testFile + ".tmp", "3"); File.Replace(testFile + ".tmp", testFile, testFile + ".bak");
            Check(File.ReadAllText(testFile) == "3" && File.ReadAllText(testFile + ".bak") == "2", "Repeated atomic saves retain the previous version");
            File.WriteAllText("PolishResult.txt", report + "Gameplay tests left to user.\n");
        }
    }
}
