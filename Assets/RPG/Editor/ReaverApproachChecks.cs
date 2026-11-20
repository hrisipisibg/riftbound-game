using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class ReaverApproachChecks
    {
        const string SessionKey = "Riftbound.ReaverApproachChecks";
        const string Output = ".code-check/reaver-approach/reaver-approach-checks.txt";
        static readonly BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
        static readonly string[] ReaverIds = { "westbank-reaver-1", "westbank-reaver-2", RiverReaverCamp.CaptainId };

        static readonly Vector3 WestbankStart = new Vector3(-235f, 0f, 50f);
        static readonly Vector3 BridgeWest = new Vector3(-278f, 0f, 50f);
        static readonly Vector3 ReaverBase = new Vector3(-285f, 0f, 50f);
        static readonly Vector3 SafeStaging = new Vector3(-284.5f, 0f, 57.5f);

        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();

        static PlayerController player;
        static Keyboard keyboard;
        static int stage, wpIndex, passed;
        static double due, timeout, phaseDeadline, lastProgressAt;
        static Vector3 lastProgressPos;
        static Vector3[] mountedRoute;
        static float hpBeforeContact;
        static bool loggedOnFootAggro;
        static string folder;

        static ReaverApproachChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        static string SavePath => Path.GetFullPath(".code-check/reaver-approach/test-adventure.json");

        [MenuItem("Riftbound/Run Reaver Approach Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start ReaverApproachChecks outside Play Mode.");
            folder = Path.GetDirectoryName(Output);
            Directory.CreateDirectory(folder);
            if (File.Exists(SavePath)) File.Delete(SavePath);

            SessionState.SetBool(SessionKey + ".DirectPlay", EditorPrefs.GetBool("Riftbound.DirectPlayInEditor", false));
            EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", false);
            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(SessionKey + ".Failed", false);
            UIArtSetup.PrepareGameView();
            Application.runInBackground = true;
            PlayerSettings.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSaves()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            PlayerController.SavePathOverride = SavePath;
            Application.runInBackground = true;
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear();
                Errors.Clear();
                Lines.Add($"REAVER APPROACH INPUT CHECK UTC={DateTime.UtcNow:O}");
                Lines.Add("Queued InputSystem keyboard controls (Z mount/dismount, W forward) + OrbitCamera yaw alignment in Outpost-Expanded with isolated save fixture; NOT manual human play or online co-op.");
                stage = 0;
                wpIndex = 0;
                passed = 0;
                loggedOnFootAggro = false;
                due = EditorApplication.timeSinceStartup + 1.0;
                timeout = due + 120.0;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                SessionState.SetBool(SessionKey, false);
                EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", SessionState.GetBool(SessionKey + ".DirectPlay", false));
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(SessionKey + ".Failed", true) ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Riftbound"))
                Errors.Add($"{type}: {msg}\n{stack}");
        }

        static void Assert(bool cond, string label, string detail = "")
        {
            string line = (cond ? "PASS " : "FAIL ") + label + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail);
            Lines.Add(line);
            if (!cond) throw new Exception(line);
            passed++;
        }

        static void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));

        static float FlatDist(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static Vector3 Surface(Vector3 p)
        {
            p.y = WorldRegions.GroundHeight(p);
            foreach (var hit in Physics.RaycastAll(new Vector3(p.x, p.y + 12f, p.z), Vector3.down, 14f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                if (hit.collider.name == "Westbank bridge deck" && hit.point.y > p.y) p.y = hit.point.y;
            return p;
        }

        static void PlaceAndFace(Vector3 pos, Vector3 lookTarget)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            player.transform.position = Surface(pos) + Vector3.up * 0.12f;
            Vector3 dir = lookTarget - player.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                player.transform.rotation = Quaternion.LookRotation(dir);
            motor.enabled = true;
            Physics.SyncTransforms();
            player.orbit.SetPaused(false);
            typeof(OrbitCamera).GetField("yaw", Hidden).SetValue(player.orbit, player.transform.eulerAngles.y);
            typeof(OrbitCamera).GetField("pitch", Hidden).SetValue(player.orbit, 23f);
        }

        static void AimToward(Vector3 target)
        {
            Vector3 dir = target - player.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                player.transform.rotation = Quaternion.LookRotation(dir);
                typeof(OrbitCamera).GetField("yaw", Hidden).SetValue(player.orbit, player.transform.eulerAngles.y);
            }
        }

        static string DescribeNearestBlocker()
        {
            Vector3 origin = player.transform.position + Vector3.up * 1.1f;
            if (Physics.SphereCast(origin, 0.45f, player.transform.forward, out var hit, 2.0f, ~(1 << 2), QueryTriggerInteraction.Ignore))
            {
                var parts = new List<string>();
                for (var t = hit.collider.transform; t; t = t.parent) parts.Add(t.name);
                parts.Reverse();
                return $"{string.Join("/", parts)} (dist={hit.distance:F2}m)";
            }
            return "none";
        }

        static string FormatEnemyDistances(Vector3 fromPos, out float minDist3D)
        {
            minDist3D = float.MaxValue;
            var entries = new List<string>();
            foreach (var id in ReaverIds)
            {
                var ec = EnemyController.FindByKey(id);
                if (!ec) { entries.Add($"{id}=missing"); continue; }
                float d3 = Vector3.Distance(fromPos, ec.transform.position);
                float d2 = FlatDist(fromPos, ec.transform.position);
                if (d3 < minDist3D) minDist3D = d3;
                entries.Add($"{id}=3D:{d3:F2}m/XZ:{d2:F2}m@{ec.transform.position:F2}");
            }
            return string.Join(", ", entries);
        }

        static void ResetReaverCamp()
        {
            foreach (var id in ReaverIds)
            {
                var ec = EnemyController.FindByKey(id);
                if (ec)
                {
                    ec.gameObject.SetActive(true);
                    ec.ResetForLoad();
                }
            }
        }

        static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > timeout) throw new TimeoutException($"ReaverApproachChecks timed out at stage {stage}");
                if (EditorApplication.isPlaying && EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || now < due) return;

                if (stage == 0)
                {
                    player = Object.FindFirstObjectByType<PlayerController>();
                    var frontend = player ? player.GetComponent<FrontendMenu>() : null;
                    if (!frontend || !frontend.TitleScreenOpen || !FrontendMenu.IsOpen || Time.frameCount < 5)
                    {
                        due = now + 0.5;
                        return;
                    }
                    PlayerController.SavePathOverride = SavePath;
                    typeof(FrontendMenu).GetField("enteringCharacter", BindingFlags.NonPublic | BindingFlags.Static)
                        .SetValue(null, new FrontendMenu.Character { id = Guid.NewGuid().ToString("N"), name = "ReaverCheck", faction = "Alliance", race = "Human", characterClass = "Warrior" });
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null, false);
                    player.orbit.SetPaused(false);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    stage = 1;
                    due = now + 0.5;
                    phaseDeadline = now + 20.0;
                }
                else if (stage == 1)
                {
                    player = Object.FindFirstObjectByType<PlayerController>();
                    if (!player || player.CharacterName != "ReaverCheck" || FrontendMenu.IsOpen)
                    {
                        if (now > phaseDeadline) throw new Exception("Isolated character entry failed.");
                        due = now + 0.5;
                        return;
                    }
                    RiverReaverCamp.Ensure();
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    keyboard = InputSystem.AddDevice<Keyboard>("ReaverApproachKeyboard");
                    keyboard.MakeCurrent();

                    Assert(player.Stats && player.Stats.Alive && player.orbit, "Isolated player initialized in Outpost-Expanded",
                        $"save={player.SavePath}");
                    foreach (var id in ReaverIds)
                    {
                        var ec = EnemyController.FindByKey(id);
                        Assert(ec && ec.gameObject.activeInHierarchy && ec.GetComponent<Combatant>().Alive,
                            $"Reaver enemy active: {id}", ec ? $"pos={ec.transform.position:F2}" : "null");
                    }

                    mountedRoute = new[] { WestbankStart, BridgeWest, ReaverBase, SafeStaging };
                    Lines.Add($"WAYPOINTS mounted Westbank->Staging: WP0={WestbankStart:F2} -> WP1={BridgeWest:F2} -> WP2={ReaverBase:F2} -> WP3={SafeStaging:F2}");
                    PlaceAndFace(WestbankStart, BridgeWest);
                    stage = 2;
                    due = now + 0.4;
                }
                else if (stage == 2)
                {
                    Keys(Key.Z); // Queued Z summons mount
                    stage = 3;
                    due = now + 0.15;
                }
                else if (stage == 3)
                {
                    Keys();
                    stage = 4;
                    due = now + 1.8;
                }
                else if (stage == 4)
                {
                    Assert(player.Mounted, "Queued Z summoned mount at Westbank", $"mount={player.SelectedMountId} msg={player.Message}");
                    wpIndex = 1;
                    lastProgressPos = player.transform.position;
                    lastProgressAt = now;
                    phaseDeadline = now + 25.0;
                    stage = 5;
                }
                else if (stage == 5)
                {
                    // Mounted travel Westbank -> Bridge -> Reaver base -> Safe staging point
                    if (!player.Stats.Alive || !player.Mounted)
                        throw new Exception($"Unexpected dismount/death during mounted travel to staging at {player.transform.position:F2} hp={player.Stats.Health} msg={player.Message} blocker={DescribeNearestBlocker()}");

                    while (wpIndex < mountedRoute.Length && FlatDist(player.transform.position, mountedRoute[wpIndex]) < 0.55f)
                    {
                        Lines.Add($"INFO reached WP{wpIndex} pos={player.transform.position:F2} hp={player.Stats.Health}/{player.Stats.maxHealth} mounted={player.Mounted}");
                        wpIndex++;
                    }
                    if (wpIndex >= mountedRoute.Length)
                    {
                        Keys();
                        stage = 6;
                        due = now + 0.6;
                        return;
                    }
                    if (FlatDist(player.transform.position, lastProgressPos) > 0.03f)
                    {
                        lastProgressPos = player.transform.position;
                        lastProgressAt = now;
                    }
                    if (now > phaseDeadline || now - lastProgressAt > 3.0)
                        throw new Exception($"Mounted route stalled at {player.transform.position:F2} toward WP{wpIndex}={mountedRoute[wpIndex]:F2} blocker={DescribeNearestBlocker()}");

                    AimToward(mountedRoute[wpIndex]);
                    Keys(Key.W);
                }
                else if (stage == 6)
                {
                    string distSummary = FormatEnemyDistances(player.transform.position, out float minDist);
                    Assert(player.Mounted && player.Stats.Alive && player.Stats.Health == player.Stats.maxHealth && !player.InCombat() && minDist > 14.0f,
                        "Mounted arrival at safe staging point stayed mounted, unharmed, and outside aggro",
                        $"pos={player.transform.position:F2} minEnemyDist={minDist:F2}m ({distSummary})");

                    // Phase 2: Explicit dismount with Z, then on-foot advance to hostile contact
                    Keys(Key.Z);
                    stage = 7;
                    due = now + 0.15;
                }
                else if (stage == 7)
                {
                    Keys();
                    Assert(!player.Mounted && player.Stats.Alive, "Queued Z dismounted cleanly at safe staging point",
                        $"pos={player.transform.position:F2}");
                    hpBeforeContact = player.Stats.Health;
                    lastProgressPos = player.transform.position;
                    lastProgressAt = now;
                    phaseDeadline = now + 15.0;
                    Lines.Add($"WAYPOINT on-foot advance: {player.transform.position:F2} -> {RiverReaverCamp.Position:F2} (input=Key.W)");
                    stage = 8;
                }
                else if (stage == 8)
                {
                    if (!loggedOnFootAggro && player.InCombat())
                    {
                        loggedOnFootAggro = true;
                        string distSummary = FormatEnemyDistances(player.transform.position, out _);
                        Lines.Add($"INFO on-foot aggro triggered at pos={player.transform.position:F2} ({distSummary})");
                    }
                    if (player.Stats.Health < hpBeforeContact)
                    {
                        Keys();
                        string distSummary = FormatEnemyDistances(player.transform.position, out float minDist);
                        Assert(!player.Mounted && player.Stats.Health < hpBeforeContact,
                            "On-foot advance reached hostile contact and took real enemy damage",
                            $"pos={player.transform.position:F2} hp={hpBeforeContact}->{player.Stats.Health} lastHit={player.Stats.LastHit:F2} minEnemyDist={minDist:F2}m ({distSummary})");

                        // Prepare Phase 3: Reset back to SafeStaging, restore & wait for combat clear, then test mounted damage-dismount
                        player.Stats.Restore();
                        ResetReaverCamp();
                        PlaceAndFace(SafeStaging, RiverReaverCamp.Position);
                        stage = 9;
                        due = now + 5.2; // allow >5s so PlayerController.InCombat() and LastHit expire cleanly
                        return;
                    }
                    if (FlatDist(player.transform.position, lastProgressPos) > 0.03f)
                    {
                        lastProgressPos = player.transform.position;
                        lastProgressAt = now;
                    }
                    if (now > phaseDeadline || now - lastProgressAt > 4.0)
                        throw new Exception($"On-foot advance did not reach hostile contact in time at {player.transform.position:F2} inCombat={player.InCombat()} hp={player.Stats.Health} blocker={DescribeNearestBlocker()}");

                    if (FlatDist(player.transform.position, RiverReaverCamp.Position) > 1.2f)
                    {
                        AimToward(RiverReaverCamp.Position);
                        Keys(Key.W);
                    }
                    else
                    {
                        Keys();
                    }
                }
                else if (stage == 9)
                {
                    Assert(!player.InCombat() && player.Stats.Health == player.Stats.maxHealth,
                        "Staging reset cleared combat before mounted contact test",
                        $"inCombat={player.InCombat()} pos={player.transform.position:F2}");
                    Keys(Key.Z);
                    stage = 10;
                    due = now + 0.15;
                }
                else if (stage == 10)
                {
                    Keys();
                    stage = 11;
                    due = now + 1.8;
                }
                else if (stage == 11)
                {
                    Assert(player.Mounted, "Queued Z remounted at safe staging point for damage-dismount check",
                        $"pos={player.transform.position:F2} msg={player.Message}");
                    hpBeforeContact = player.Stats.Health;
                    lastProgressPos = player.transform.position;
                    lastProgressAt = now;
                    phaseDeadline = now + 15.0;
                    Lines.Add($"WAYPOINT mounted hostile test: {player.transform.position:F2} -> {RiverReaverCamp.Position:F2} (input=Key.W)");
                    stage = 12;
                }
                else if (stage == 12)
                {
                    if (player.Stats.Health < hpBeforeContact)
                    {
                        Keys();
                        string distSummary = FormatEnemyDistances(player.transform.position, out _);
                        Assert(!player.Mounted,
                            "Mounted advance into Reaver camp preserved real damage-triggered dismount",
                            $"dismountPos={player.transform.position:F2} hp={hpBeforeContact}->{player.Stats.Health} lastHit={player.Stats.LastHit:F2} ({distSummary})");
                        Assert(Errors.Count == 0, "Zero Riftbound runtime errors", $"errors={Errors.Count}");

                        Lines.Add($"TOTAL PASS={passed} ERRORS={Errors.Count} RESULT=PASS");
                        File.WriteAllLines(Output, Lines);
                        SessionState.SetBool(SessionKey + ".Failed", false);
                        EditorApplication.isPlaying = false;
                        return;
                    }
                    if (!player.Mounted)
                        throw new Exception($"Dismounted before taking damage at {player.transform.position:F2} hp={player.Stats.Health}/{hpBeforeContact} msg={player.Message}");
                    if (FlatDist(player.transform.position, lastProgressPos) > 0.03f)
                    {
                        lastProgressPos = player.transform.position;
                        lastProgressAt = now;
                    }
                    if (now > phaseDeadline || now - lastProgressAt > 4.0)
                        throw new Exception($"Mounted hostile advance stalled/timed out at {player.transform.position:F2} hp={player.Stats.Health} blocker={DescribeNearestBlocker()}");

                    if (FlatDist(player.transform.position, RiverReaverCamp.Position) > 1.2f)
                    {
                        AimToward(RiverReaverCamp.Position);
                        Keys(Key.W);
                    }
                    else
                    {
                        Keys();
                    }
                }
            }
            catch (Exception ex)
            {
                if (keyboard != null) Keys();
                Lines.Add("FAIL EXCEPTION " + ex.Message);
                foreach (var e in Errors) Lines.Add("ERROR " + e);
                File.WriteAllLines(Output, Lines);
                SessionState.SetBool(SessionKey + ".Failed", true);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
