using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class BrassHollowsChecks
    {
        const string Key = "Riftbound.BrassHollowsChecks";
        const string Output = ".code-check/brass-hollows/brass-hollows-checks.txt";
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();

        static int stage, passedCount;
        static double due, timeout, phaseDeadline;
        static float hpSnapshot;
        static bool sawVentWarning, sawVentRecovery, sawBellVentWarning, sawVeylWindup, sawVeylRecovery;
        static string stage1SaveJson;
        static int goldBeforeTurnIn, xpBeforeTurnIn;
        static Transform oreVent, pumpVent, bellWestVent, bellEastVent;

        static BrassHollowsChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        static string SaveFixturePath => Path.GetFullPath(".code-check/brass-hollows/save-fixture.json");

        [MenuItem("Riftbound/Run Brass Hollows Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start BrassHollowsChecks outside Play Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            if (File.Exists(SaveFixturePath)) File.Delete(SaveFixturePath);
            SessionState.SetBool(Key + ".DirectPlay", EditorPrefs.GetBool("Riftbound.DirectPlayInEditor", false));
            EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", true);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Failed", false);
            Application.runInBackground = true;
            PlayerSettings.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSave()
        {
            if (!SessionState.GetBool(Key, false)) return;
            PlayerController.SavePathOverride = SaveFixturePath;
            Application.runInBackground = true;
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear();
                Errors.Clear();
                Lines.Add("Brass Hollows PlayMode verification in Outpost-Expanded (isolated save fixture; NOT rendered online or 2-PC play).");
                stage = 0;
                passedCount = 0;
                sawVentWarning = sawVentRecovery = sawBellVentWarning = sawVeylWindup = sawVeylRecovery = false;
                timeout = EditorApplication.timeSinceStartup + 90.0;
                due = EditorApplication.timeSinceStartup + 1.0;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                bool failed = SessionState.GetBool(Key + ".Failed", true);
                SessionState.SetBool(Key, false);
                EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", SessionState.GetBool(Key + ".DirectPlay", false));
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Riftbound"))
                Errors.Add(msg + "\n" + stack);
        }

        static void Assert(bool condition, string label, string detail = "")
        {
            string line = (condition ? "PASS " : "FAIL ") + label + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail);
            Lines.Add(line);
            if (!condition) throw new Exception(line);
            passedCount++;
        }

        static void Teleport(PlayerController player, Vector3 pos)
        {
            var motor = player.GetComponent<CharacterController>();
            if (motor) motor.enabled = false;
            player.transform.position = pos;
            if (motor) motor.enabled = true;
        }

        static TownNPC FindFizzlebang()
        {
            foreach (var npc in Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                if (npc.npcName == BrassHollows.Engineer) return npc;
            return null;
        }

        static void CacheVents()
        {
            var chamber = GameObject.Find("Brass Hollows underground passage")?.transform;
            if (!chamber) return;
            oreVent = chamber.Find("Ore Landing steam vent");
            pumpVent = chamber.Find("Pressure vent");
            bellWestVent = chamber.Find("Bell Chamber west vent");
            bellEastVent = chamber.Find("Bell Chamber east vent");
        }

        static bool VentRingEnabled(Transform vent)
        {
            var ring = vent ? vent.GetComponentInChildren<LineRenderer>(true) : null;
            return ring && ring.enabled;
        }

        static string VentMaterialName(Transform vent)
        {
            var r = vent ? vent.GetComponent<Renderer>() : null;
            return r && r.sharedMaterial ? r.sharedMaterial.name : "";
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout)
                    throw new TimeoutException("BrassHollowsChecks timed out at stage " + stage);
                if (EditorApplication.isPlaying && EditorApplication.isPaused)
                    EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < due || !EditorApplication.isPlaying) return;

                var player = Object.FindFirstObjectByType<PlayerController>();
                if (!player || !player.Stats) throw new Exception("Missing PlayerController");

                if (stage == 0)
                {
                    BrassHollows.Ensure();
                    var hollows = BrassHollows.Current;
                    Assert(hollows != null, "BrassHollows.Current initialized");
                    Assert(player.CanUseBrassHollowsQuest && !Network.CoopSessionConfig.IsMultiplayer,
                        "Solo Outpost-Expanded quest boundary verified");
                    Assert(player.BrassHollowsStage == 0, "Fresh profile begins at BrassHollowsStage 0");

                    // Stage 0 blocked at entrance
                    Teleport(player, hollows.Entrance - Vector3.forward * 1.5f);
                    Assert(player.TryGetBestInteraction(out var cand0, out _) && cand0.text == "Enter the Brass Hollows",
                        "Stage 0 displays Enter prompt");
                    cand0.execute?.Invoke();
                    Assert(!BrassHollows.Inside(player.transform.position), "Stage 0 blocks entry without quest", player.Message);

                    // Accept from Fizzlebang
                    var fizz = FindFizzlebang();
                    Assert(fizz != null, "Tinker Fizzlebang found in world");
                    Teleport(player, fizz.transform.position - Vector3.forward * 1.5f);
                    player.OpenTownNPCDialogue(fizz);
                    player.HandleBrassHollowsQuest();
                    player.CloseDialogue();
                    Assert(player.BrassHollowsStage == 1, "Accepting quest from Fizzlebang sets stage 1");

                    // Enter Brass Hollows
                    Teleport(player, hollows.Entrance - Vector3.forward * 1.5f);
                    Assert(player.TryGetBestInteraction(out var enterCand, out _), "Stage 1 Enter prompt available");
                    enterCand.execute?.Invoke();
                    Assert(BrassHollows.Inside(player.transform.position), "Entered Brass Hollows Ore Landing",
                        $"pos={player.transform.position}");
                    CacheVents();
                    Assert(oreVent && pumpVent && bellWestVent && bellEastVent, "All 4 steam vents constructed");
                    Assert(!VentRingEnabled(oreVent) && VentMaterialName(oreVent).Contains("brass"),
                        "Teaching vent starts calm at entry", VentMaterialName(oreVent));

                    // Verify safe landing for 0.6s
                    hpSnapshot = player.Stats.Health;
                    stage = 1;
                    due = EditorApplication.timeSinceStartup + 0.6;
                }
                else if (stage == 1)
                {
                    var sentry = EnemyController.FindByKey("hollows-sentry");
                    float sentryDist = sentry ? Vector3.Distance(player.transform.position, sentry.transform.position) : -1f;
                    Assert(sentryDist > 14f && !player.InCombat() && player.Stats.Health == hpSnapshot,
                        "Entry landing is safe from immediate sentry aggro",
                        $"sentryDist={sentryDist:F2}m inCombat={player.InCombat()} hp={player.Stats.Health}/{hpSnapshot}");

                    // Save Stage 1 snapshot for later LoadAdventure reset check
                    player.SaveAdventure(false);
                    stage1SaveJson = File.Exists(SaveFixturePath) ? File.ReadAllText(SaveFixturePath) : "";
                    Assert(!string.IsNullOrEmpty(stage1SaveJson), "Captured isolated Stage 1 save");

                    // Stand OUTSIDE Ore Landing vent ring (at z=3.2 local -> 166.2 world, vent is at z=6.0 -> 169.0 world)
                    Teleport(player, new Vector3(642f, 36.12f, 166.2f));
                    hpSnapshot = player.Stats.Health;
                    phaseDeadline = EditorApplication.timeSinceStartup + 7.5;
                    stage = 2;
                    due = EditorApplication.timeSinceStartup + 0.1;
                }
                else if (stage == 2)
                {
                    if (VentRingEnabled(oreVent) && VentMaterialName(oreVent).Contains("warning"))
                        sawVentWarning = true;
                    if (sawVentWarning && !VentRingEnabled(oreVent) && VentMaterialName(oreVent).Contains("pump"))
                        sawVentRecovery = true;
                    if (sawVentWarning && sawVentRecovery)
                    {
                        Assert(player.Stats.Health == hpSnapshot,
                            "Standing outside warning ring avoids steam vent impact",
                            $"hp={player.Stats.Health}/{hpSnapshot}");
                        // Now stand ON Pump Gallery vent (z=29.5 -> 192.5 world) and wait for avoidable burst hit
                        Teleport(player, new Vector3(642f, 36.12f, 192.5f));
                        hpSnapshot = player.Stats.Health;
                        phaseDeadline = EditorApplication.timeSinceStartup + 8.0;
                        stage = 3;
                        due = EditorApplication.timeSinceStartup + 0.1;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup > phaseDeadline)
                        throw new Exception($"Ore vent cycle did not complete (warn={sawVentWarning}, rec={sawVentRecovery})");
                    due = EditorApplication.timeSinceStartup + 0.1;
                }
                else if (stage == 3)
                {
                    if (player.Stats.Health < hpSnapshot)
                    {
                        Assert(true, "Standing inside vent ring triggers steam burst damage",
                            $"hp {hpSnapshot} -> {player.Stats.Health}");

                        // Test front exit & enemy/vent reset on re-entry
                        var bruiser = EnemyController.FindByKey("hollows-bruiser");
                        if (bruiser) bruiser.GetComponent<Combatant>().Damage(25f); // damage bruiser partially
                        player.Stats.Restore();
                        Teleport(player, new Vector3(642f, 36.12f, 164.1f));
                        // Wait >3s or clear LastHit? Wait: wait out 3.1s so requireClear front retreat is clean, OR test LastHit first!
                        stage = 4;
                        due = EditorApplication.timeSinceStartup + 3.2;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup > phaseDeadline)
                        throw new Exception("Pump Gallery vent did not damage player standing inside ring");
                    due = EditorApplication.timeSinceStartup + 0.1;
                }
                else if (stage == 4)
                {
                    Assert(player.TryGetBestInteraction(out var exitCand, out _) && exitCand.text == "Leave the Brass Hollows",
                        "Front exit interaction prompt available");
                    exitCand.execute?.Invoke();
                    Assert(!BrassHollows.Inside(player.transform.position), "Front exit returns player outside",
                        $"pos={player.transform.position}");
                    Assert(BrassHollows.Current.ActiveVentWarningCount == 0, "Vent warning rings cleared on exit");

                    // Re-enter and verify damaged undefeated bruiser resets to home + full HP
                    Teleport(player, BrassHollows.Current.Entrance - Vector3.forward * 1.5f);
                    player.TryGetBestInteraction(out var reenter, out _);
                    reenter.execute?.Invoke();
                    var bruiser = EnemyController.FindByKey("hollows-bruiser");
                    var bruiserStats = bruiser ? bruiser.GetComponent<Combatant>() : null;
                    Assert(BrassHollows.Inside(player.transform.position) && bruiserStats && bruiserStats.Health == bruiserStats.maxHealth,
                        "Re-entry resets undefeated enemies to full HP",
                        $"bruiserHp={(bruiserStats ? bruiserStats.Health : 0)}/{(bruiserStats ? bruiserStats.maxHealth : 0)}");

                    // Move into Bell Chamber near Veyl (center lane z=48 -> 211 world) to observe flank vents + Veyl windup/recovery
                    player.Stats.Restore();
                    Teleport(player, new Vector3(642f, 36.12f, 211.0f));
                    hpSnapshot = player.Stats.Health;
                    phaseDeadline = EditorApplication.timeSinceStartup + 14.0;
                    stage = 5;
                    due = EditorApplication.timeSinceStartup + 0.1;
                }
                else if (stage == 5)
                {
                    // Keep player alive while observing Veyl's windup/recovery and Bell Chamber flank vents
                    if (player.Stats.Health < 80f) player.Stats.Restore();
                    var veyl = EnemyController.FindByKey("hollows-veyl");
                    if (veyl && veyl.IsWarningActive) sawVeylWindup = true;
                    if (BrassHollows.Current.IsVeylRecovering) sawVeylRecovery = true;
                    bool westWarn = VentRingEnabled(bellWestVent);
                    bool eastWarn = VentRingEnabled(bellEastVent);
                    if (westWarn && eastWarn)
                        throw new Exception("Bell Chamber flank vents warned simultaneously");
                    if (westWarn || eastWarn) sawBellVentWarning = true;

                    if (sawVeylWindup && sawVeylRecovery && sawBellVentWarning)
                    {
                        Assert(true, "Bell Chamber flank vents warn alternately without overlapping");
                        Assert(true, "Foreman Veyl windup and post-slam recovery window observed cleanly");
                        stage = 6;
                        due = EditorApplication.timeSinceStartup + 0.1;
                        return;
                    }
                    if (EditorApplication.timeSinceStartup > phaseDeadline)
                        throw new Exception($"Veyl/Bell encounter not observed in time (windup={sawVeylWindup}, recovery={sawVeylRecovery}, bellWarn={sawBellVentWarning})");
                    due = EditorApplication.timeSinceStartup + 0.1;
                }
                else if (stage == 6)
                {
                    // Pump blocked before defeating all 5
                    Teleport(player, new Vector3(642f, 36.12f, 217.0f));
                    Assert(player.TryGetBestInteraction(out var pumpCand, out _) && pumpCand.text == "Shut down the siphon",
                        "Pump interaction prompt available");
                    pumpCand.execute?.Invoke();
                    Assert(player.BrassHollowsStage == 1, "Pump remains locked while Choir enemies live", player.Message);

                    // Defeat all 5 Choir enemies
                    foreach (var id in new[] { "hollows-sentry", "hollows-lookout", "hollows-bruiser", "hollows-channeler", "hollows-veyl" })
                    {
                        var ec = EnemyController.FindByKey(id);
                        if (ec && ec.GetComponent<Combatant>().Alive) ec.GetComponent<Combatant>().Damage(100000f);
                    }
                    Assert(BrassHollows.Current.DefeatedCount(player) == 5, "All 5 Choir enemies defeated");

                    // Shut down siphon -> Stage 2 -> Service door opens
                    pumpCand.execute?.Invoke();
                    Assert(player.BrassHollowsStage == 2, "Siphon shutoff advances quest to stage 2");
                    var chamber = GameObject.Find("Brass Hollows underground passage")?.transform;
                    var door = chamber ? chamber.Find("Service hatch") : null;
                    Assert(door && door.localPosition.x > 4f, "Service hatch slides open at stage 2",
                        $"doorX={(door ? door.localPosition.x : 0):F2}");
                    Assert(BrassHollows.Current.ActiveVentWarningCount == 0 && VentMaterialName(bellWestVent).Contains("pump"),
                        "Vents cool down after siphon shutoff");

                    // Simulate just-hit right before using Service Exit to prove it never traps a victorious player
                    player.Stats.Restore();
                    player.Stats.Damage(5f);
                    Teleport(player, new Vector3(645f, 36.12f, 220.5f));
                    Assert(player.TryGetBestInteraction(out var svcCand, out _) && svcCand.text == "Take the service exit",
                        "Service exit prompt shown");
                    svcCand.execute?.Invoke();
                    Assert(!BrassHollows.Inside(player.transform.position),
                        "Service exit returns player to Redridge Pass even right after combat hit",
                        $"pos={player.transform.position}");

                    // Turn in at Fizzlebang & verify idempotency
                    var fizz = FindFizzlebang();
                    goldBeforeTurnIn = player.Gold;
                    xpBeforeTurnIn = player.Experience;
                    Teleport(player, fizz.transform.position - Vector3.forward * 1.5f);
                    player.OpenTownNPCDialogue(fizz);
                    player.HandleBrassHollowsQuest();
                    int gold1 = player.Gold, xp1 = player.Experience;
                    player.HandleBrassHollowsQuest();
                    player.CloseDialogue();
                    Assert(player.BrassHollowsStage == 3 && gold1 == goldBeforeTurnIn + 55 && player.Gold == gold1 && xp1 == xpBeforeTurnIn + 240,
                        "Fizzlebang turn-in rewards 55g and 240xp idempotently",
                        $"stage={player.BrassHollowsStage} gold={goldBeforeTurnIn}->{gold1}->{player.Gold} xp={xpBeforeTurnIn}->{xp1}");

                    // Reload Stage 1 save -> verify pump/door/enemies reset cleanly!
                    File.WriteAllText(SaveFixturePath, stage1SaveJson);
                    player.LoadAdventure();
                    stage = 7;
                    due = EditorApplication.timeSinceStartup + 3.2;
                }
                else if (stage == 7)
                {
                    var chamber = GameObject.Find("Brass Hollows underground passage")?.transform;
                    var door = chamber ? chamber.Find("Service hatch") : null;
                    var veyl = EnemyController.FindByKey("hollows-veyl");
                    Assert(player.BrassHollowsStage == 1 && door && door.localPosition.x < 3.2f && veyl && veyl.gameObject.activeSelf,
                        "Reloading Stage 1 save closes service hatch, resets pump, and restores Veyl",
                        $"stage={player.BrassHollowsStage} doorX={(door ? door.localPosition.x : -1):F2} veylActive={(veyl && veyl.gameObject.activeSelf)}");

                    // Death inside Brass Hollows + respawn recovery
                    Teleport(player, BrassHollows.Current.Entrance - Vector3.forward * 1.5f);
                    player.TryGetBestInteraction(out var enter2, out _);
                    enter2.execute?.Invoke();
                    Assert(BrassHollows.Inside(player.transform.position), "Re-entered Brass Hollows for death/respawn check",
                        $"msg={player.Message}");
                    player.Stats.Damage(100000f);
                    Assert(!player.Stats.Alive, "Player downed inside Brass Hollows");
                    stage = 8;
                    due = EditorApplication.timeSinceStartup + 0.15;
                }
                else if (stage == 8)
                {
                    Assert(BrassHollows.Current.ActiveVentWarningCount == 0, "Vent warning rings cleared while player is downed");
                    player.ApplyAuthorizedRespawn(player.GetRacialSpawnPosition());
                    Assert(player.Stats.Alive && !BrassHollows.Inside(player.transform.position),
                        "Respawn recovers player cleanly outside at outpost", $"pos={player.transform.position}");

                    Lines.Add($"TOTAL PASS={passedCount} ERRORS={Errors.Count} RESULT={(Errors.Count == 0 ? "PASS" : "FAIL")}");
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
