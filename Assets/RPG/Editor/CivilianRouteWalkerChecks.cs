using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Riftbound.WorldAmbient;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class CivilianRouteWalkerChecks
    {
        const string Key = "Riftbound.CivilianRouteWalkerChecks";
        const string Output = ".code-check/civilian-routes/civilian-route-checks.txt";
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();

        static int stage, passed;
        static double due, timeout, phaseDeadline;
        static CivilianRouteWalker mainWalker, emptyWalker, nullWalker, singleWalker, dupWalker, compactWalker, missingParamWalker;
        static AnimatorController tempController;
        static Animator mainAnim;
        static bool sawWalkAnimTrue, sawPause, sawPauseIdleAnim, sawSecondLegYaw;
        static float minObservedY, maxObservedY;
        static Vector3 disabledPosSnapshot;

        static CivilianRouteWalkerChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        [MenuItem("Riftbound/Run Civilian Route Walker Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start CivilianRouteWalkerChecks outside Play Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Failed", false);
            Application.runInBackground = true;
            PlayerSettings.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear();
                Errors.Clear();
                Lines.Add("CivilianRouteWalker isolated PlayMode fixture with temporary actors (empty scene; no authored NPCs, scenes, or prefabs touched).");
                stage = 0;
                passed = 0;
                sawWalkAnimTrue = sawPause = sawPauseIdleAnim = sawSecondLegYaw = false;
                minObservedY = float.MaxValue;
                maxObservedY = float.MinValue;
                timeout = EditorApplication.timeSinceStartup + 30.0;
                due = EditorApplication.timeSinceStartup + 0.1;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                if (tempController) { Object.DestroyImmediate(tempController); tempController = null; }
                bool failed = SessionState.GetBool(Key + ".Failed", true);
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) &&
                (stack.Contains("Riftbound") || msg.Contains("CivilianRouteWalker") || msg.Contains("Animator")))
                Errors.Add($"{type}: {msg}\n{stack}");
            else if (type == LogType.Warning && (msg.Contains("Animator") || msg.Contains("Look rotation")))
                Errors.Add($"{type}: {msg}\n{stack}");
        }

        static void Assert(bool cond, string label, string detail = "")
        {
            string line = (cond ? "PASS " : "FAIL ") + label + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail);
            Lines.Add(line);
            if (!cond) throw new Exception(line);
            passed++;
        }

        static Transform MakeWaypoint(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            return go.transform;
        }

        static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) throw new MissingFieldException(target.GetType().Name, name);
            f.SetValue(target, value);
        }

        static CivilianRouteWalker SpawnWalker(string name, Vector3 startPos, Transform[] wps, float speed = 6f, float stop = 0.25f, bool closed = true, Animator anim = null, string param = "")
        {
            var go = new GameObject(name);
            go.SetActive(false);
            go.transform.position = startPos;
            var w = go.AddComponent<CivilianRouteWalker>();
            SetField(w, "waypoints", wps);
            SetField(w, "walkSpeed", speed);
            SetField(w, "turnSpeed", 720f);
            SetField(w, "stopSeconds", stop);
            SetField(w, "arrivalThreshold", 0.08f);
            SetField(w, "closedLoop", closed);
            SetField(w, "faceWaypointDirection", true);
            SetField(w, "animator", anim);
            SetField(w, "locomotionParameter", param);
            go.SetActive(true);
            return w;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout)
                    throw new TimeoutException("CivilianRouteWalkerChecks timed out at stage " + stage);
                if (EditorApplication.isPlaying && EditorApplication.isPaused)
                    EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < due || !EditorApplication.isPlaying) return;

                if (stage == 0)
                {
                    // Build temporary in-memory AnimatorController with Bool parameter "IsWalking"
                    tempController = new AnimatorController();
                    tempController.AddParameter("IsWalking", AnimatorControllerParameterType.Bool);
                    tempController.AddLayer("Base Layer");

                    var wp0 = MakeWaypoint("WP_0", new Vector3(0f, 2.0f, 0f));
                    var wp1 = MakeWaypoint("WP_1", new Vector3(0f, 3.5f, 3.0f));
                    var wp2 = MakeWaypoint("WP_2", new Vector3(3.0f, 1.25f, 0f));

                    var animGo = new GameObject("TempCivilianActor");
                    animGo.SetActive(false);
                    animGo.transform.position = wp0.position;
                    mainAnim = animGo.AddComponent<Animator>();
                    mainAnim.runtimeAnimatorController = tempController;
                    mainWalker = animGo.AddComponent<CivilianRouteWalker>();
                    SetField(mainWalker, "waypoints", new[] { wp0, wp1, wp2 });
                    SetField(mainWalker, "walkSpeed", 6f);
                    SetField(mainWalker, "turnSpeed", 720f);
                    SetField(mainWalker, "stopSeconds", 0.25f);
                    SetField(mainWalker, "arrivalThreshold", 0.08f);
                    SetField(mainWalker, "closedLoop", true);
                    SetField(mainWalker, "animator", mainAnim);
                    SetField(mainWalker, "locomotionParameter", "IsWalking");
                    animGo.SetActive(true);

                    // Invalid & edge-case routes
                    emptyWalker = SpawnWalker("InvalidEmpty", new Vector3(10f, 0f, 0f), Array.Empty<Transform>());
                    nullWalker = SpawnWalker("InvalidNulls", new Vector3(12f, 0f, 0f), new Transform[] { null, null });
                    singleWalker = SpawnWalker("InvalidSingle", new Vector3(14f, 0f, 0f), new[] { wp0 });
                    dupWalker = SpawnWalker("InvalidRepeated", new Vector3(16f, 0f, 0f), new[] { wp0, wp0, wp0 });

                    var cw0 = MakeWaypoint("CWP_0", new Vector3(20f, 1.0f, 0f));
                    var cw1 = MakeWaypoint("CWP_1", new Vector3(20f, 2.5f, 2.5f));
                    compactWalker = SpawnWalker("CompactRoute", cw0.position, new[] { cw0, null, cw0, cw1, cw1, cw0 }, speed: 5f, stop: 0.1f);

                    var missingGo = new GameObject("MissingParamActor");
                    missingGo.SetActive(false);
                    missingGo.transform.position = cw0.position;
                    var missingAnim = missingGo.AddComponent<Animator>();
                    missingAnim.runtimeAnimatorController = tempController;
                    missingParamWalker = missingGo.AddComponent<CivilianRouteWalker>();
                    SetField(missingParamWalker, "waypoints", new[] { cw0, cw1 });
                    SetField(missingParamWalker, "locomotionParameter", "NonExistentParam");
                    missingGo.SetActive(true);

                    Assert(!emptyWalker.HasValidRoute && !emptyWalker.IsWalking, "Empty route rejected safely");
                    Assert(!nullWalker.HasValidRoute && !nullWalker.IsWalking, "All-null route rejected safely");
                    Assert(!singleWalker.HasValidRoute && !singleWalker.IsWalking, "Single-waypoint route rejected safely");
                    Assert(!dupWalker.HasValidRoute && dupWalker.ValidWaypointCount == 1, "All-repeated waypoint route rejected safely");
                    Assert(compactWalker.HasValidRoute && compactWalker.ValidWaypointCount == 2,
                        "Nulls and consecutive/closing duplicate waypoints deduplicated cleanly",
                        $"valid={compactWalker.ValidWaypointCount}");
                    Assert(mainWalker.HasValidRoute && mainWalker.ValidWaypointCount == 3 && mainWalker.CurrentWaypointIndex == 1,
                        "Valid 3-waypoint closed route initialized toward WP_1");

                    phaseDeadline = EditorApplication.timeSinceStartup + 6.0;
                    stage = 1;
                    due = EditorApplication.timeSinceStartup + 0.05;
                }
                else if (stage == 1)
                {
                    float curY = mainWalker.transform.position.y;
                    if (curY < minObservedY) minObservedY = curY;
                    if (curY > maxObservedY) maxObservedY = curY;
                    if (mainWalker.IsWalking && mainAnim.GetBool("IsWalking"))
                        sawWalkAnimTrue = true;
                    if (mainWalker.IsPaused)
                    {
                        sawPause = true;
                        if (!mainAnim.GetBool("IsWalking")) sawPauseIdleAnim = true;
                    }
                    // On leg 2 (toward WP_2 at x=3, z=0), yaw turns significantly away from 0 deg
                    if (mainWalker.CurrentWaypointIndex == 2 && mainWalker.transform.eulerAngles.y > 45f)
                        sawSecondLegYaw = true;

                    if (mainWalker.CompletedLoops >= 1 && mainWalker.CurrentWaypointIndex == 1 && mainWalker.IsWalking)
                    {
                        Assert(sawWalkAnimTrue, "Optional Animator Bool parameter enabled while walking");
                        Assert(sawPause && sawPauseIdleAnim, "Actor pauses at waypoints for stopSeconds and resets Animator Bool to false");
                        Assert(sawSecondLegYaw, "Actor turns smoothly toward waypoint travel direction");
                        Assert(maxObservedY >= 3.35f && minObservedY <= 1.35f,
                            "Explicit authored waypoint heights followed without flattening",
                            $"minY={minObservedY:F2} maxY={maxObservedY:F2}");
                        Assert(mainWalker.CompletedLoops >= 1 && mainWalker.CurrentWaypointIndex == 1,
                            "Closed-loop route completes loop and repeats cleanly",
                            $"loops={mainWalker.CompletedLoops} idx={mainWalker.CurrentWaypointIndex}");

                        // Disable mid-walk and confirm immediate stop + Animator reset + position freeze
                        mainWalker.enabled = false;
                        disabledPosSnapshot = mainWalker.transform.position;
                        Assert(!mainWalker.IsWalking && !mainWalker.IsPaused && !mainAnim.GetBool("IsWalking"),
                            "Disabling walker clears walking/paused state and Animator parameter");

                        stage = 2;
                        due = EditorApplication.timeSinceStartup + 0.25;
                        return;
                    }

                    if (EditorApplication.timeSinceStartup > phaseDeadline)
                        throw new TimeoutException($"Route traversal did not complete loop in time (idx={mainWalker.CurrentWaypointIndex}, loops={mainWalker.CompletedLoops}, pos={mainWalker.transform.position})");
                    due = EditorApplication.timeSinceStartup + 0.03;
                }
                else if (stage == 2)
                {
                    Assert(Vector3.Distance(mainWalker.transform.position, disabledPosSnapshot) < 0.001f,
                        "Disabled walker stops moving cleanly");
                    Assert(Vector3.Distance(emptyWalker.transform.position, new Vector3(10f, 0f, 0f)) < 0.001f &&
                           Vector3.Distance(nullWalker.transform.position, new Vector3(12f, 0f, 0f)) < 0.001f &&
                           Vector3.Distance(dupWalker.transform.position, new Vector3(16f, 0f, 0f)) < 0.001f,
                        "Invalid-route actors remain completely stationary");
                    Assert(Errors.Count == 0, "Zero runtime errors or Animator warnings", $"errors={Errors.Count}");

                    Lines.Add($"TOTAL PASS={passed} ERRORS={Errors.Count} RESULT={(Errors.Count == 0 ? "PASS" : "FAIL")}");
                    File.WriteAllLines(Output, Lines);
                    SessionState.SetBool(Key + ".Failed", Errors.Count > 0);
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                Lines.Add("FAIL EXCEPTION " + ex.Message);
                foreach (var e in Errors) Lines.Add("ERROR " + e);
                File.WriteAllLines(Output, Lines);
                SessionState.SetBool(Key + ".Failed", true);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
