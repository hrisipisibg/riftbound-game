using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    // Empty-scene native fixtures. These are presentation safety checks, not gameplay.
    [InitializeOnLoad]
    public static class GroundedVisualChecks
    {
        const string Key = "Riftbound.GroundedVisualChecks";
        const string Output = ".code-check/presentation/grounded-visual-checks.txt";
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();
        static readonly List<GameObject> Objects = new List<GameObject>();
        static GameObject actor, visual;
        static Animator animator;
        static GroundedVisual grounding;
        static Vector3 expectedRoot, settledLocal, sentinel;
        static bool mounted, alive, airborne;
        static int stage, passed, frameBefore;
        static double due, timeout;

        static GroundedVisualChecks() { EditorApplication.playModeStateChanged += Changed; }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Isolated batch outside Play Mode required.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Failed", false);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSave()
        {
            if (!SessionState.GetBool(Key, false)) return;
            PlayerController.SavePathOverride = Path.GetFullPath(".code-check/presentation/grounding-fixture-unused-save.json");
            Application.runInBackground = true;
        }

        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear(); Errors.Clear(); Objects.Clear();
                Lines.Add("Native empty-scene presentation/physics fixtures; NOT gameplay, rendered or online acceptance.");
                stage = 0; passed = 0; timeout = EditorApplication.timeSinceStartup + 45;
                due = EditorApplication.timeSinceStartup + .3;
                frameBefore = Time.frameCount;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetBool(Key + ".Failed", true) ? 1 : 0);
            }
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) &&
                (stack.Contains("GroundedVisual") || message.Contains("GroundedVisual"))) Errors.Add(message + "\n" + stack);
        }

        static void Wait(double seconds = .8)
        {
            stage++;
            due = EditorApplication.timeSinceStartup + seconds;
            frameBefore = Time.frameCount;
        }

        static void Check(string label, bool valid, string evidence)
        {
            Lines.Add($"{(valid ? "PASS" : "FAIL")} [{label}] {evidence}");
            if (!valid) throw new InvalidOperationException(label + ": " + evidence);
            passed++;
        }

        static void Contact(string label, float supportY)
        {
            float sole = Sole();
            Check(label, Mathf.Abs(sole - (supportY + .025f)) <= .025f,
                $"baked-foot-region sole={sole:F4} support={supportY:F4} expected gap=.025±.025m");
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new TimeoutException("GroundedVisualChecks stage " + stage);
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due || Time.frameCount < frameBefore + 3) return;
                if (stage == 0)
                {
                    if (Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length != 0)
                        throw new InvalidOperationException("Fixture scene unexpectedly contains a PlayerController.");
                    Fixture(.15f, true); Wait();
                }
                else if (stage == 1)
                {
                    Contact("floor_contact", 0);
                    Check("actor_root_unchanged", Near(actor.transform.position, expectedRoot), actor.transform.position.ToString("F5"));
                    Check("visual_wrapper_unchanged", Near(visual.transform.localPosition, Vector3.zero), visual.transform.localPosition.ToString("F5"));
                    settledLocal = animator.transform.localPosition;
                    grounding.BindObserver(animator, () => mounted, () => alive, () => airborne);
                    Wait(.5);
                }
                else if (stage == 2)
                {
                    Check("same_binding_no_drift", Near(animator.transform.localPosition, settledLocal), $"before={settledLocal:F5} after={animator.transform.localPosition:F5}");
                    airborne = true;
                    actor.transform.position += Vector3.up;
                    Wait(.5);
                }
                else if (stage == 3)
                {
                    Check("airborne_keeps_visual_baseline", Near(animator.transform.localPosition, settledLocal), animator.transform.localPosition.ToString("F5"));
                    Check("airborne_root_not_snapped", Near(actor.transform.position, expectedRoot + Vector3.up), actor.transform.position.ToString("F5"));
                    mounted = true; sentinel = new Vector3(.12f, .42f, -.08f); animator.transform.localPosition = sentinel;
                    Wait(.4);
                }
                else if (stage == 4)
                {
                    Check("mounted_seat_owned_by_caller", Near(animator.transform.localPosition, sentinel), animator.transform.localPosition.ToString("F5"));
                    mounted = false; alive = false; sentinel = new Vector3(-.1f, .5f, .04f); animator.transform.localPosition = sentinel;
                    Wait(.4);
                }
                else if (stage == 5)
                {
                    Check("dead_pose_owned_by_caller", Near(animator.transform.localPosition, sentinel), animator.transform.localPosition.ToString("F5"));
                    alive = true; airborne = false; grounding.soleHeight = .08f; actor.transform.position = expectedRoot; Wait();
                }
                else if (stage == 6)
                {
                    Contact("grounding_resumes", 0);
                    Check("legacy_clearance_does_not_preserve_hover", Sole() <= .045f, $"legacy soleHeight=.08 observed sole={Sole():F4}");
                    var self = new GameObject("Self collider"); self.transform.SetParent(actor.transform, false);
                    self.transform.localPosition = Vector3.down * .05f;
                    self.AddComponent<BoxCollider>().size = new Vector3(.4f, .1f, .4f);
                    var other = Track(new GameObject("Other actor collider"));
                    other.transform.position = new Vector3(0, -.1f, 0);
                    var motor = other.AddComponent<CharacterController>(); motor.height = .4f; motor.radius = .15f; motor.center = Vector3.up * .1f;
                    Physics.SyncTransforms(); Wait(.5);
                }
                else if (stage == 7)
                {
                    Contact("self_and_other_actor_excluded", 0);
                    Fixture(2.15f, true); Box("Bridge deck", new Vector3(0, 1.9f, 0), new Vector3(4, .2f, 4));
                    Physics.SyncTransforms(); Wait();
                }
                else if (stage == 8)
                {
                    Contact("deck_not_lower_floor", 2);
                    Check("deck_actor_root_unchanged", Near(actor.transform.position, expectedRoot), actor.transform.position.ToString("F5"));
                    var old = visual;
                    Visual("Dwarf_Warrior"); grounding.BindObserver(animator, () => mounted, () => alive, () => airborne);
                    Object.DestroyImmediate(old); Wait();
                }
                else if (stage == 9)
                {
                    Contact("new_visual_rebind", 2);
                    Fixture(.15f, true, false); Wait();
                }
                else if (stage == 10)
                {
                    Contact("stationary_without_motor_or_stats", 0);
                    Fixture(.15f, false); sentinel = animator.transform.localPosition; Wait();
                }
                else if (stage == 11)
                {
                    Check("missing_support_fails_safe", Near(animator.transform.localPosition, sentinel), animator.transform.localPosition.ToString("F5"));
                    Fixture(.15f, false);
                    for (int i = 0; i < 128; i++) Box("Saturated support " + i, new Vector3(0, -.002f * i, 0), new Vector3(2, .001f, 2));
                    sentinel = animator.transform.localPosition; Physics.SyncTransforms(); Wait();
                }
                else if (stage == 12)
                {
                    Check("saturated_support_fails_safe", Near(animator.transform.localPosition, sentinel), animator.transform.localPosition.ToString("F5"));
                    Clear(); Box("Floor", new Vector3(0, -.1f, 0), new Vector3(5, .2f, 5));
                    var container = Track(Object.Instantiate(Resources.Load<GameObject>("Characters/Human_Warrior")));
                    container.transform.position = new Vector3(0, .15f, 0);
                    animator = container.GetComponentInChildren<Animator>();
                    actor = animator.gameObject; expectedRoot = actor.transform.position;
                    animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0); animator.speed = 0;
                    grounding = actor.AddComponent<GroundedVisual>(); grounding.BindObserver(animator, () => false, () => true, () => false);
                    Physics.SyncTransforms(); Wait();
                }
                else if (stage == 13)
                {
                    Check("animator_on_actor_root_never_moves_authority", Near(actor.transform.position, expectedRoot), actor.transform.position.ToString("F5"));
                    Fixture(.15f, true, false, true); Wait();
                }
                else if (stage == 14)
                {
                    var motor = actor.GetComponent<CharacterController>();
                    float sole = Sole();
                    Check("stationary_service_motor_without_move", !motor.isGrounded && Near(actor.transform.position, expectedRoot) && Mathf.Abs(sole - .025f) <= .025f,
                        $"motor.isGrounded={motor.isGrounded} root={actor.transform.position:F5} sole={sole:F4} expected=.025±.025m; no Move called");
                    Finish(false);
                }
            }
            catch (Exception e) { Lines.Add("FAIL [exception] " + e); Finish(true); }
        }

        static GameObject Track(GameObject go) { Objects.Add(go); return go; }
        static void Box(string name, Vector3 position, Vector3 size)
        {
            var go = Track(new GameObject(name)); go.transform.position = position; go.AddComponent<BoxCollider>().size = size;
        }
        static void Fixture(float y, bool floor, bool observer = true, bool serviceWithMotor = false)
        {
            Clear(); mounted = false; alive = true; airborne = false;
            if (floor) Box("Floor", new Vector3(0, -.1f, 0), new Vector3(5, .2f, 5));
            actor = Track(new GameObject("Fixture actor")); actor.transform.position = new Vector3(0, y, 0); expectedRoot = actor.transform.position;
            if (serviceWithMotor)
            {
                actor.AddComponent<TownNPC>().npcName = "Fixture stationary service";
                var motor = actor.AddComponent<CharacterController>();
                motor.height = 1.95f; motor.radius = .48f; motor.center = Vector3.up * .975f;
            }
            Visual("Human_Warrior"); grounding = actor.AddComponent<GroundedVisual>();
            if (observer) grounding.BindObserver(animator, () => mounted, () => alive, () => airborne);
            else grounding.BindAnimator(animator);
            Physics.SyncTransforms();
        }
        static void Visual(string name)
        {
            var prefab = Resources.Load<GameObject>("Characters/" + name);
            if (!prefab || prefab.GetComponentInChildren<PlayerController>()) throw new InvalidOperationException("Invalid visual-only fixture prefab " + name);
            visual = Object.Instantiate(prefab, actor.transform); visual.transform.localPosition = Vector3.zero;
            foreach (var col in visual.GetComponentsInChildren<Collider>()) col.enabled = false;
            animator = visual.GetComponentInChildren<Animator>(); animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; animator.Rebind(); animator.Play("Idle", 0, 0); animator.Update(0); animator.speed = 0;
        }
        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .003f;

        static float Sole()
        {
            var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot); var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            float min = float.PositiveInfinity, limit = Mathf.Min(left.position.y, right.position.y) + .08f;
            foreach (var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                try
                {
                    renderer.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        var p = renderer.transform.TransformPoint(vertex);
                        if (p.y > limit) continue;
                        float leftDistance = new Vector2(p.x - left.position.x, p.z - left.position.z).sqrMagnitude;
                        float rightDistance = new Vector2(p.x - right.position.x, p.z - right.position.z).sqrMagnitude;
                        if (Mathf.Min(leftDistance, rightDistance) < .0625f) min = Mathf.Min(min, p.y);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            if (float.IsPositiveInfinity(min)) throw new InvalidOperationException("No native baked foot-region geometry.");
            return min;
        }

        static void Clear()
        {
            foreach (var go in Objects) if (go) Object.DestroyImmediate(go);
            Objects.Clear(); actor = null; visual = null; animator = null; grounding = null;
        }
        static void Finish(bool failed)
        {
            EditorApplication.update -= Tick; Application.logMessageReceived -= OnLog;
            Clear();
            if (Errors.Count != 0) failed = true;
            Lines.Add($"TOTAL PASS={passed} ERRORS={Errors.Count} RESULT={(failed ? "FAIL" : "PASS")}");
            Lines.AddRange(Errors); File.WriteAllText(Output, string.Join("\n", Lines) + "\n");
            SessionState.SetBool(Key + ".Failed", failed);
            EditorApplication.isPlaying = false;
        }
    }
}
