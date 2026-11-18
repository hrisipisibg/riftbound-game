using System;
using System.Collections.Generic;
using System.IO;
using Riftbound.Network;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    /// <summary>
    /// Native PlayMode regression suite covering remote GroundedVisual.EnsureFor / BindObserver
    /// lifecycle invariants (no-context fail-safe, per-peer state gates, mount/dismount wrapper
    /// restoration, model replacement, death/revive, and despawn/rejoin cleanup).
    /// </summary>
    [InitializeOnLoad]
    public static class RemoteObserverGroundingChecks
    {
        const string Key = "Riftbound.RemoteObserverGroundingChecks";
        const string Output = ".code-check/presentation/remote-observer-grounding-checks.txt";
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();
        static readonly List<GameObject> Tracked = new List<GameObject>();

        static GameObject peerA, peerB, visualA, visualB, mountA, dummyLocalPlayer;
        static Animator animA, animB;
        static GroundedVisual gvA, gvB;
        static bool mountedA, aliveA, airborneA, mountedB, aliveB, airborneB;
        static Vector3 rootA, rootB, nestedBaselineA, nestedBaselineB;
        static int stage, passed, frameBefore;
        static double due, timeout;

        static RemoteObserverGroundingChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        [MenuItem("Riftbound/Run Remote Observer Grounding Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start RemoteObserverGroundingChecks outside Play Mode.");
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
            PlayerController.SavePathOverride = Path.GetFullPath(".code-check/presentation/remote-observer-unused-save.json");
            Application.runInBackground = true;
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear();
                Errors.Clear();
                Tracked.Clear();
                Lines.Add("Native empty-scene remote observer GroundedVisual lifecycle checks; NOT full online WAN acceptance.");
                stage = 0;
                passed = 0;
                timeout = EditorApplication.timeSinceStartup + 50.0;
                due = EditorApplication.timeSinceStartup + 0.3;
                frameBefore = Time.frameCount;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                bool failed = SessionState.GetBool(Key + ".Failed", true);
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) &&
                (stack.Contains("GroundedVisual") || stack.Contains("RiftboundCoopPlayer") || msg.Contains("GroundedVisual")))
                Errors.Add(msg + "\n" + stack);
        }

        static void Wait(double seconds = 0.7)
        {
            stage++;
            due = EditorApplication.timeSinceStartup + seconds;
            frameBefore = Time.frameCount;
        }

        static void Check(string label, bool ok, string detail)
        {
            Lines.Add($"{(ok ? "PASS" : "FAIL")} [{label}] {detail}");
            if (!ok) throw new InvalidOperationException($"{label}: {detail}");
            passed++;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout)
                    throw new TimeoutException($"RemoteObserverGroundingChecks timeout at stage {stage}");
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due || Time.frameCount < frameBefore + 3)
                    return;

                if (stage == 0)
                {
                    BuildBaseFloor();
                    // Stage 0: NetworkObject peer created via EnsureFor WITHOUT BindObserver must NOT borrow local state
                    dummyLocalPlayer = Track(new GameObject("LocalContextDecoy"));
                    dummyLocalPlayer.transform.position = Vector3.zero;
                    dummyLocalPlayer.AddComponent<CharacterController>();

                    peerA = CreateNetworkPeer("RemotePeerA", new Vector3(0f, 0.18f, 0f), out rootA);
                    visualA = CharacterVisualFactory.CreateVisualInstance("Human", "Warrior", peerA.transform);
                    animA = visualA.GetComponentInChildren<Animator>(true);
                    PrepareAnimIdle(animA);
                    gvA = GroundedVisual.EnsureFor(peerA, animA);
                    nestedBaselineA = animA.transform.localPosition;
                    Physics.SyncTransforms();
                    Wait(0.6);
                }
                else if (stage == 1)
                {
                    Check("no_context_peer_fails_safe",
                        gvA != null
                        && Near(animA.transform.localPosition, nestedBaselineA)
                        && Near(peerA.transform.position, rootA),
                        $"no-context animLocal={animA.transform.localPosition:F4} expected={nestedBaselineA:F4} root={peerA.transform.position:F4}");

                    // Now bind Peer A's own observer context + spawn an independent Peer B on the same floor
                    mountedA = false; aliveA = true; airborneA = false;
                    gvA = GroundedVisual.EnsureFor(peerA, animA);
                    gvA.BindObserver(animA, () => mountedA, () => aliveA, () => airborneA);

                    peerB = CreateNetworkPeer("RemotePeerB", new Vector3(3f, 0.18f, 0f), out rootB);
                    visualB = CharacterVisualFactory.CreateVisualInstance("Orc", "Warrior", peerB.transform);
                    animB = visualB.GetComponentInChildren<Animator>(true);
                    PrepareAnimIdle(animB);
                    mountedB = false; aliveB = true; airborneB = false;
                    gvB = GroundedVisual.EnsureFor(peerB, animB);
                    gvB.BindObserver(animB, () => mountedB, () => aliveB, () => airborneB);
                    Physics.SyncTransforms();
                    Wait(0.8);
                }
                else if (stage == 2)
                {
                    float soleA = Sole(visualA, animA);
                    float soleB = Sole(visualB, animB);
                    Check("bound_observers_ground_both_peers_and_preserve_roots",
                        peerA.GetComponents<GroundedVisual>().Length == 1
                        && peerB.GetComponents<GroundedVisual>().Length == 1
                        && soleA <= 0.055f && soleB <= 0.055f
                        && Near(visualA.transform.localPosition, Vector3.zero)
                        && Near(peerA.transform.position, rootA)
                        && Near(peerB.transform.position, rootB),
                        $"gvA={peerA.GetComponents<GroundedVisual>().Length} soleA={soleA:F4} soleB={soleB:F4}");

                    nestedBaselineA = animA.transform.localPosition;
                    nestedBaselineB = animB.transform.localPosition;

                    // Toggle Peer A airborne while moving root +1m; Peer B stays grounded on floor
                    airborneA = true;
                    peerA.transform.position = rootA + Vector3.up;
                    Wait(0.5);
                }
                else if (stage == 3)
                {
                    Check("airborne_gate_isolated_per_peer_and_no_root_snap",
                        Near(peerA.transform.position, rootA + Vector3.up)
                        && Near(animA.transform.localPosition, nestedBaselineA)
                        && Sole(visualB, animB) <= 0.055f,
                        $"peerA.rootY={peerA.transform.position.y:F3} peerB.sole={Sole(visualB, animB):F4}");

                    // Land Peer A, then mount Horse + shift wrapper visualA
                    peerA.transform.position = rootA;
                    airborneA = false;
                    mountedA = true;
                    var def = RiftboundCoopPlayer.ResolveMountDefinition("horse");
                    if (def && def.prefab)
                    {
                        mountA = Track(Object.Instantiate(def.prefab.gameObject, peerA.transform));
                        mountA.name = "TestMount_Horse";
                    }
                    // Shift wrapper as PositionRemoteRider() does, leaving nested animA untouched
                    visualA.transform.position += new Vector3(0.05f, 0.95f, -0.04f);
                    Wait(0.5);
                }
                else if (stage == 4)
                {
                    Vector3 expectedWrapperMount = new Vector3(0.05f, 0.95f, -0.04f);
                    Check("mounted_wrapper_moves_nested_preserved_and_peerB_unaffected",
                        Near(visualA.transform.localPosition, expectedWrapperMount)
                        && Near(animA.transform.localPosition, nestedBaselineA)
                        && Near(peerA.transform.position, rootA)
                        && Sole(visualB, animB) <= 0.055f,
                        $"wrapper={visualA.transform.localPosition:F4} nested={animA.transform.localPosition:F4}");

                    // Dismount Peer A & restore authored wrapper
                    mountedA = false;
                    if (mountA) Object.Destroy(mountA);
                    mountA = null;
                    visualA.transform.localPosition = Vector3.zero;
                    Wait(0.7);
                }
                else if (stage == 5)
                {
                    Check("dismount_restores_wrapper_and_resumes_grounding",
                        Near(visualA.transform.localPosition, Vector3.zero)
                        && Sole(visualA, animA) <= 0.055f
                        && Near(peerA.transform.position, rootA),
                        $"wrapper={visualA.transform.localPosition:F4} soleA={Sole(visualA, animA):F4}");

                    // Death gate pauses without corrupting root/wrapper; Revive resumes
                    aliveA = false;
                    Wait(0.4);
                }
                else if (stage == 6)
                {
                    Check("death_gate_safe_and_wrapper_restored",
                        Near(visualA.transform.localPosition, Vector3.zero)
                        && Near(peerA.transform.position, rootA),
                        $"deadWrapper={visualA.transform.localPosition:F4}");
                    aliveA = true;

                    // Race/model replacement on an elevated deck (y=2.0)
                    var deck = Track(new GameObject("ElevatedDeck"));
                    deck.transform.position = new Vector3(0f, 1.9f, 0f);
                    deck.AddComponent<BoxCollider>().size = new Vector3(4f, 0.2f, 4f);
                    rootA = new Vector3(0f, 2.18f, 0f);
                    peerA.transform.position = rootA;
                    Physics.SyncTransforms();

                    gvA.BindAnimator(null);
                    visualA.SetActive(false);
                    Object.Destroy(visualA);
                    visualA = CharacterVisualFactory.CreateVisualInstance("Dwarf", "Warrior", peerA.transform);
                    animA = visualA.GetComponentInChildren<Animator>(true);
                    PrepareAnimIdle(animA);
                    gvA = GroundedVisual.EnsureFor(peerA, animA);
                    gvA.BindObserver(animA, () => mountedA, () => aliveA, () => airborneA);
                    Wait(0.9);
                }
                else if (stage == 7)
                {
                    float deckSole = Sole(visualA, animA);
                    Check("race_rebind_single_component_and_deck_grounding",
                        peerA.GetComponents<GroundedVisual>().Length == 1
                        && gvA.animator == animA
                        && Mathf.Abs(deckSole - 2.025f) <= 0.03f
                        && Near(peerA.transform.position, rootA),
                        $"gvCount={peerA.GetComponents<GroundedVisual>().Length} deckSole={deckSole:F4}");

                    // Despawn cleanup & Rejoin fresh instance
                    gvA.BindAnimator(null);
                    visualA.transform.localPosition = Vector3.zero;
                    visualA.SetActive(false);
                    Object.Destroy(visualA);
                    Object.Destroy(peerA);
                    peerA = null; gvA = null; visualA = null; animA = null;

                    peerA = CreateNetworkPeer("RejoinedPeerA", new Vector3(-2.5f, 0.18f, 0f), out rootA);
                    visualA = CharacterVisualFactory.CreateVisualInstance("Orc", "Mage", peerA.transform);
                    animA = visualA.GetComponentInChildren<Animator>(true);
                    PrepareAnimIdle(animA);
                    mountedA = false; aliveA = true; airborneA = false;
                    gvA = GroundedVisual.EnsureFor(peerA, animA);
                    gvA.BindObserver(animA, () => mountedA, () => aliveA, () => airborneA);
                    Physics.SyncTransforms();
                    Wait(0.9);
                }
                else if (stage == 8)
                {
                    float rejoinSole = Sole(visualA, animA);
                    Check("despawn_and_rejoin_rebinds_cleanly",
                        peerA.GetComponents<GroundedVisual>().Length == 1
                        && rejoinSole <= 0.055f
                        && Near(peerA.transform.position, rootA),
                        $"rejoinGv={peerA.GetComponents<GroundedVisual>().Length} rejoinSole={rejoinSole:F4}");
                    Finish(false);
                }
            }
            catch (Exception ex)
            {
                Lines.Add("FAIL [exception] " + ex);
                Finish(true);
            }
        }

        static GameObject Track(GameObject go)
        {
            Tracked.Add(go);
            return go;
        }

        static void BuildBaseFloor()
        {
            var floor = Track(new GameObject("TestFloor"));
            floor.transform.position = new Vector3(0f, -0.1f, 0f);
            floor.AddComponent<BoxCollider>().size = new Vector3(20f, 0.2f, 20f);
            Physics.SyncTransforms();
        }

        static GameObject CreateNetworkPeer(string name, Vector3 pos, out Vector3 rootPos)
        {
            var go = Track(new GameObject(name));
            go.transform.position = pos;
            go.AddComponent<NetworkObject>();
            rootPos = pos;
            return go;
        }

        static void PrepareAnimIdle(Animator anim)
        {
            if (!anim) return;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            if (anim.HasState(0, Animator.StringToHash("Idle")))
                anim.Play("Idle", 0, 0f);
            anim.Update(0f);
            anim.speed = 0f;
        }

        static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < 0.005f;

        static float Sole(GameObject root, Animator anim)
        {
            Transform left = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform right = anim.GetBoneTransform(HumanBodyBones.RightFoot);
            if (!left || !right)
            {
                foreach (var bone in anim.GetComponentsInChildren<Transform>(true))
                {
                    if (bone.name.EndsWith(":LeftFoot", StringComparison.Ordinal) || bone.name == "LeftFoot" || bone.name == "foot.l") left = bone;
                    if (bone.name.EndsWith(":RightFoot", StringComparison.Ordinal) || bone.name == "RightFoot" || bone.name == "foot.r") right = bone;
                }
            }
            float min = float.PositiveInfinity;
            float limit = Mathf.Min(left.position.y, right.position.y) + 0.08f;
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.sharedMesh) continue;
                var mesh = new Mesh();
                try
                {
                    r.BakeMesh(mesh);
                    foreach (var v in mesh.vertices)
                    {
                        Vector3 p = r.transform.TransformPoint(v);
                        if (p.y > limit) continue;
                        float dl = new Vector2(p.x - left.position.x, p.z - left.position.z).sqrMagnitude;
                        float dr = new Vector2(p.x - right.position.x, p.z - right.position.z).sqrMagnitude;
                        if (Mathf.Min(dl, dr) < 0.0625f) min = Mathf.Min(min, p.y);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            if (float.IsPositiveInfinity(min))
                throw new InvalidOperationException("No baked foot geometry found.");
            return min;
        }

        static void Finish(bool failed)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            foreach (var go in Tracked) if (go) Object.DestroyImmediate(go);
            Tracked.Clear();
            if (Errors.Count > 0) failed = true;
            Lines.Add($"TOTAL PASS={passed} ERRORS={Errors.Count} RESULT={(failed ? "FAIL" : "PASS")}");
            Lines.AddRange(Errors);
            File.WriteAllText(Output, string.Join("\n", Lines) + "\n");
            SessionState.SetBool(Key + ".Failed", failed);
            EditorApplication.isPlaying = false;
        }
    }
}
