using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    // Observes normal scene startup with an isolated progress path; never enters a roster/session UI.
    [InitializeOnLoad]
    public static class PresentationWorldProbe
    {
        const string Key = "Riftbound.PresentationWorldProbe";
        const string Folder = ".code-check/presentation/world-probe/";
        static double due, deadline;
        static int frameAtStart;
        static bool fixtureEntered, sceneReloaded;
        static Keyboard fixtureKeyboard;
        [Serializable] class Receipt
        {
            public string evidence = "Native running-world contact observation with test-only title gate bypass and optional renders; NOT title/login UI, input traversal, gameplay or multiplayer acceptance";
            public int activeGroundedComponents;
            public int framesObserved;
            public float timeScale;
            public bool editorPaused, frontendOpen;
            public bool fixtureBypassedTitleWithoutRosterSave;
            public bool syntheticIdleKeyboard;
            public bool observedAfterSceneReload;
            public List<Row> actors = new List<Row>();
            public List<string> captures = new List<string>();
            public string error;
        }
        [Serializable] class Row
        {
            public string actor, state, support;
            public bool hasFeet, hasSoleEstimate, hasSupport;
            public bool hasMotor, motorGrounded;
            public Vector3 root;
            public float soleY, supportY, estimatedGap;
        }

        static PresentationWorldProbe() { EditorApplication.playModeStateChanged += Changed; }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Isolated batch outside Play Mode required.");
            Directory.CreateDirectory(Folder);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Failed", true);
            SessionState.SetString(Key + ".Save", Path.GetFullPath(Folder + "isolated-progress-" + Guid.NewGuid().ToString("N") + ".json"));
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSave()
        {
            if (!SessionState.GetBool(Key, false)) return;
            PlayerController.SavePathOverride = SessionState.GetString(Key + ".Save", "");
            Application.runInBackground = true;
        }

        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                due = EditorApplication.timeSinceStartup + 5;
                deadline = due + 20;
                frameAtStart = Time.frameCount;
                fixtureEntered = false;
                sceneReloaded = false;
                fixtureKeyboard = InputSystem.AddDevice<Keyboard>();
                fixtureKeyboard.MakeCurrent();
                EditorApplication.update += Observe;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                if (fixtureKeyboard != null && fixtureKeyboard.added) InputSystem.RemoveDevice(fixtureKeyboard);
                EditorApplication.Exit(SessionState.GetBool(Key + ".Failed", true) ? 1 : 0);
            }
        }

        static void Observe()
        {
            if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due) return;
            if (!fixtureEntered)
            {
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (!player && EditorApplication.timeSinceStartup < deadline) return;
                if (player)
                {
                    // Same isolated-world test gate as existing route checks; never call EnterWorld/SaveRoster.
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null, false);
                    if (player.orbit) player.orbit.SetPaused(false);
                    EditorApplication.isPaused = false;
                    fixtureEntered = true;
                    frameAtStart = Time.frameCount;
                    due = EditorApplication.timeSinceStartup + 5;
                    deadline = due + 20;
                    return;
                }
            }
            if (!sceneReloaded && fixtureEntered)
            {
                sceneReloaded = true;
                EditorSceneManager.LoadSceneInPlayMode(WorldRegions.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                fixtureEntered = false;
                due = EditorApplication.timeSinceStartup + 2;
                deadline = due + 20;
                return;
            }
            if ((Time.frameCount < frameAtStart + 60 || Time.timeScale <= 0 || EditorApplication.isPaused) && EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Observe;
            var receipt = new Receipt { framesObserved = Time.frameCount - frameAtStart, timeScale = Time.timeScale,
                editorPaused = EditorApplication.isPaused, frontendOpen = FrontendMenu.IsOpen,
                fixtureBypassedTitleWithoutRosterSave = fixtureEntered, syntheticIdleKeyboard = fixtureKeyboard != null,
                observedAfterSceneReload = sceneReloaded };
            try
            {
                // Editor update deltaTime can be zero outside the player loop; require advancing, unpaused game frames.
                if (receipt.framesObserved < 60 || receipt.timeScale <= 0 || receipt.editorPaused)
                    throw new InvalidOperationException("No advancing unpaused game frames; contact probe NOT RUN.");
                var groundings = Object.FindObjectsByType<GroundedVisual>(FindObjectsSortMode.None);
                receipt.activeGroundedComponents = groundings.Length;
                Physics.SyncTransforms();
                foreach (var grounding in groundings.OrderBy(g => g.name, StringComparer.Ordinal))
                {
                    var animator = grounding.animator;
                    if (!animator) continue;
                    var motor = grounding.GetComponent<CharacterController>();
                    var row = new Row { actor = grounding.name, root = grounding.transform.position,
                        hasMotor = motor, motorGrounded = motor && motor.isGrounded,
                        state = animator.GetCurrentAnimatorStateInfo(0).IsName("Idle") ? "Idle" : "non_idle" };
                    receipt.actors.Add(row);
                    Transform left = null, right = null;
                    if (animator.isHuman)
                    {
                        left = animator.GetBoneTransform(HumanBodyBones.LeftFoot); right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    }
                    else foreach (var bone in animator.GetComponentsInChildren<Transform>())
                    {
                        if (bone.name == "foot.l" || bone.name.EndsWith(":LeftFoot", StringComparison.Ordinal)) left = bone;
                        if (bone.name == "foot.r" || bone.name.EndsWith(":RightFoot", StringComparison.Ordinal)) right = bone;
                    }
                    row.hasFeet = left && right;
                    row.hasSoleEstimate = PresentationGroundingAudit.TryEstimateSole(animator, left, right, out float sole);
                    if (row.hasSoleEstimate) row.soleY = sole;
                    foreach (var hit in Physics.RaycastAll(grounding.transform.position + Vector3.up * .4f, Vector3.down, 1.6f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                    {
                        var t = hit.collider.transform;
                        if (t.IsChildOf(grounding.transform) || hit.normal.y < .45f || hit.point.y > grounding.transform.position.y + .35f ||
                            t.GetComponentInParent<CharacterController>() || t.GetComponentInParent<Combatant>() ||
                            t.GetComponentInParent<TownNPC>() || t.GetComponentInParent<Merchant>() || t.GetComponentInParent<QuestGiver>()) continue;
                        row.hasSupport = true; row.supportY = hit.point.y; row.support = hit.collider.name; break;
                    }
                    if (row.hasSoleEstimate && row.hasSupport) row.estimatedGap = row.soleY - row.supportY;
                    bool repairedActor = row.actor == "Ember Hatchling" || row.actor == "Outrider Lyra" || row.actor == "Vermithrax the Ember Sovereign";
                    if ((!row.hasSupport || repairedActor) && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        string name = "contact-" + receipt.actors.Count + ".png";
                        Capture(grounding.transform, name, repairedActor); receipt.captures.Add(name);
                    }
                }
                var player = Object.FindAnyObjectByType<PlayerController>();
                if (!player || groundings.Length < 1) throw new InvalidOperationException("Expected running world actors are missing.");
                if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                {
                    Capture(player.transform, "player-world.png"); receipt.captures.Add("player-world.png");
                    var abe = Object.FindAnyObjectByType<QuestGiver>();
                    if (abe) { Capture(abe.transform, "quest-giver-world.png"); receipt.captures.Add("quest-giver-world.png"); }
                }
            }
            catch (Exception e) { receipt.error = e.ToString(); }
            File.WriteAllText(Folder + "receipt.json", JsonUtility.ToJson(receipt, true));
            Debug.Log($"PRESENTATION_WORLD_PROBE actors={receipt.actors.Count} components={receipt.activeGroundedComponents} error={receipt.error}");
            SessionState.SetBool(Key + ".Failed", !string.IsNullOrEmpty(receipt.error));
            if (fixtureKeyboard != null && fixtureKeyboard.added) InputSystem.RemoveDevice(fixtureKeyboard);
            EditorApplication.isPlaying = false;
        }

        static void Capture(Transform target, string filename, bool frameActor = false)
        {
            var camera = new GameObject("Disposable contact probe camera").AddComponent<Camera>();
            var texture = new RenderTexture(900, 800, 24);
            var previous = RenderTexture.active;
            bool asyncShaders = ShaderUtil.allowAsyncCompilation;
            Texture2D pixels = null;
            try
            {
                camera.transform.position = target.position + new Vector3(2.7f, 1.3f, 2.5f);
                camera.transform.LookAt(target.position + Vector3.up * .8f);
                camera.fieldOfView = 38; camera.nearClipPlane = .05f; camera.farClipPlane = 80;
                if (frameActor)
                {
                    var bounds = new Bounds(target.position + Vector3.up, Vector3.zero);
                    foreach (var renderer in target.GetComponentsInChildren<SkinnedMeshRenderer>())
                        if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                    float radius = Mathf.Max(1, bounds.extents.magnitude);
                    float distance = radius / Mathf.Sin(camera.fieldOfView * .5f * Mathf.Deg2Rad) * 1.15f;
                    camera.transform.position = bounds.center + new Vector3(1, .6f, 1).normalized * distance;
                    camera.transform.LookAt(bounds.center);
                }
                camera.targetTexture = texture;
                ShaderUtil.allowAsyncCompilation = false;
                camera.Render(); camera.Render(); RenderTexture.active = texture;
                pixels = new Texture2D(900, 800, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 900, 800), 0, 0); pixels.Apply();
                File.WriteAllBytes(Folder + filename, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = asyncShaders;
                if (pixels) Object.DestroyImmediate(pixels);
                texture.Release(); Object.DestroyImmediate(texture); Object.DestroyImmediate(camera.gameObject);
            }
        }
    }
}
