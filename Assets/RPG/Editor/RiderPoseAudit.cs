using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class RiderPoseAudit
    {
        const string Folder = ".coordination/rider-pose/";
        [Serializable] class Report
        {
            public string evidence = "Native sampled riding poses and optional temporary renders; NOT gameplay/online acceptance";
            public List<Row> rows = new List<Row>();
            public List<string> skipped = new List<string>();
            public List<string> errors = new List<string>();
        }
        [Serializable] class Row
        {
            public string character, avatar, state;
            public float phase, spineUpDot;
            public Vector3 modelEuler, hipsEuler, headFromHips;
        }

        public static void Run()
        {
            var report = new Report();
            try
            {
                if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Use an isolated batch outside Play Mode.");
                Directory.CreateDirectory(Folder);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/RPG/Resources/Characters" }).OrderBy(AssetDatabase.GUIDToAssetPath))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    Sample(path, report);
                }
                foreach (string character in new[] { "Human_Mage", "Human_Warrior" })
                    if (report.rows.Count(r => r.character == character) != 6)
                        report.errors.Add("Missing six reference pose samples for " + character);
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + "receipt.json", JsonUtility.ToJson(report, true));
            Debug.Log($"RIDER_POSE_AUDIT samples={report.rows.Count} errors={report.errors.Count}");
            if (Application.isBatchMode) EditorApplication.Exit(report.errors.Count == 0 ? 0 : 1);
        }

        static void Sample(string path, Report report)
        {
            var root = new GameObject("Disposable rider bench");
            try
            {
                var rider = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path), root.transform);
                var animator = rider.GetComponentInChildren<Animator>();
                if (!animator) throw new InvalidOperationException("Missing Animator");
                if (!animator.isHuman) { report.skipped.Add(path + ": non-Humanoid rig; use its dedicated pose audit"); return; }
                foreach (string state in new[] { "Idle", "Ride" })
                    if (!animator.HasState(0, Animator.StringToHash(state))) throw new InvalidOperationException("Missing state " + state);
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                var head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (!hips || !head) throw new InvalidOperationException("Missing body reference bones " + path);
                var mountAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Prefabs/Mount Horse.prefab");
                var mount = Object.Instantiate(mountAsset, root.transform).GetComponent<MountVisual>();
                mount.animator.Rebind(); mount.animator.Play("Idle", 0, 0); mount.animator.Update(0);
                foreach (string state in new[] { "Idle", "Ride" })
                foreach (float phase in new[] { 0f, .35f, .7f })
                {
                    animator.Rebind(); animator.Play(state, 0, phase); animator.Update(0);
                    animator.transform.position += mount.saddle.position - hips.position;
                    Vector3 spine = head.position - hips.position;
                    report.rows.Add(new Row { character = Path.GetFileNameWithoutExtension(path), avatar = AssetDatabase.GetAssetPath(animator.avatar),
                        state = state, phase = phase, spineUpDot = Vector3.Dot(spine.normalized, Vector3.up),
                        modelEuler = animator.transform.eulerAngles, hipsEuler = hips.eulerAngles, headFromHips = spine });
                    if (state == "Ride" && phase == .35f && path.Contains("Human_") && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        Capture(root, Path.GetFileNameWithoutExtension(path) + "-ride.png");
                }
            }
            catch (Exception e) { report.errors.Add(path + ": " + e.Message); }
            finally { Object.DestroyImmediate(root); }
        }

        static void Capture(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 30;
            var camera = new GameObject("Disposable rider camera").AddComponent<Camera>();
            var light = new GameObject("Disposable rider light").AddComponent<Light>();
            var target = new RenderTexture(900, 850, 24);
            var previous = RenderTexture.active; bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            Texture2D pixels = null;
            try
            {
                light.type = LightType.Directional; light.intensity = 2; light.cullingMask = 1 << 30; light.transform.rotation = Quaternion.Euler(35, -35, 0);
                camera.transform.position = new Vector3(5, 2.8f, 5); camera.transform.LookAt(Vector3.up * 1.8f);
                camera.orthographic = true; camera.orthographicSize = 2.5f; camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .17f);
                camera.targetTexture = target; ShaderUtil.allowAsyncCompilation = false;
                camera.Render(); camera.Render(); RenderTexture.active = target;
                pixels = new Texture2D(900, 850, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 900, 850), 0, 0); pixels.Apply();
                File.WriteAllBytes(Folder + name, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (pixels) Object.DestroyImmediate(pixels);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject);
            }
        }
    }
}
