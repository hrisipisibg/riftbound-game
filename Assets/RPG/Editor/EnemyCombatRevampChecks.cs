using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    /// <summary>
    /// Focused native PlayMode regressions for enemy combat commitment, remote action
    /// precedence (no locomotion overwrite), finite expiry, interrupt/cancel/death/leash
    /// teardown, duplicate/stale packet filtering, controller fallback, and LOS contact checks.
    /// </summary>
    [InitializeOnLoad]
    public static class EnemyCombatRevampChecks
    {
        const string Key = "Riftbound.EnemyCombatRevampChecks";
        const string Output = ".code-check/combat-revamp/enemy-combat-revamp-checks.txt";
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();
        static readonly List<GameObject> Tracked = new List<GameObject>();

        static GameObject floorGo, wallGo, enemyGo, playerGo;
        static EnemyController ec;
        static Combatant eStats, pStats;
        static PlayerController pc;
        static RuntimeAnimatorController bruteCtrl, wolfCtrl;
        static int stage, passed, frameBefore;
        static double due, timeout;
        static float actionExpiresBeforeDup;

        static EnemyCombatRevampChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        static string SaveFixturePath => Path.GetFullPath(".code-check/combat-revamp/unused-save.json");

        [MenuItem("Riftbound/Run Enemy Combat Revamp Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start EnemyCombatRevampChecks outside Play Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            if (File.Exists(SaveFixturePath)) File.Delete(SaveFixturePath);
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
            PlayerController.SavePathOverride = SaveFixturePath;
            Application.runInBackground = true;
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Lines.Clear(); Errors.Clear(); Tracked.Clear();
                Lines.Add("Native isolated-scene EnemyController action precedence & commitment fixtures; NOT rendered online or 2-PC acceptance.");
                stage = 0; passed = 0;
                timeout = EditorApplication.timeSinceStartup + 45.0;
                due = EditorApplication.timeSinceStartup + 0.2;
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
                (stack.Contains("EnemyController") || stack.Contains("CastBarSystem") || msg.Contains("Animator")))
                Errors.Add(msg + "\n" + stack);
        }

        static void Wait(double seconds = 0.25)
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
                    throw new TimeoutException("EnemyCombatRevampChecks timeout at stage " + stage);
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                Time.timeScale = 1f;
                typeof(FrontendMenu).GetProperty("IsOpen")?.SetValue(null, false);
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < due || Time.frameCount < frameBefore + 3)
                    return;

                if (stage == 0)
                {
                    Time.timeScale = 1f;
                    var entering = typeof(FrontendMenu).GetField("enteringCharacter", BindingFlags.NonPublic | BindingFlags.Static);
                    if (entering != null)
                        entering.SetValue(null, new FrontendMenu.Character { id = Guid.NewGuid().ToString("N"), name = "RevampCheck", faction = "Alliance", race = "Human", characterClass = "Warrior" });
                    typeof(FrontendMenu).GetProperty("IsOpen")?.SetValue(null, false);
                    floorGo = Track(new GameObject("Floor"));
                    floorGo.transform.position = new Vector3(0f, -0.1f, 0f);
                    floorGo.AddComponent<BoxCollider>().size = new Vector3(60f, 0.2f, 60f);

                    playerGo = Track(new GameObject("PlayerTarget"));
                    playerGo.transform.position = new Vector3(0f, 0.1f, 3.0f);
                    pStats = playerGo.AddComponent<Combatant>();
                    pStats.isPlayer = true;
                    pStats.maxHealth = 500f;
                    pStats.Restore();
                    pc = playerGo.AddComponent<PlayerController>();

                    // Allow PlayerController/FrontendMenu Start() to settle BEFORE spawning the enemy under test
                    Wait(0.25);
                }
                else if (stage == 1)
                {
                    foreach (var fm in Object.FindObjectsByType<FrontendMenu>(FindObjectsSortMode.None)) Object.Destroy(fm);
                    typeof(FrontendMenu).GetProperty("IsOpen")?.SetValue(null, false);
                    Time.timeScale = 1f;

                    enemyGo = Track(new GameObject("RemoteEnemy"));
                    enemyGo.transform.position = new Vector3(0f, 0.1f, 0f);
                    var cc = enemyGo.AddComponent<CharacterController>();
                    cc.height = 1.9f; cc.radius = 0.45f; cc.center = Vector3.up * 0.95f;
                    eStats = enemyGo.AddComponent<Combatant>();
                    eStats.maxHealth = 300f; eStats.Restore();
                    var visualPrefab = Resources.Load<GameObject>("Characters/Orc_Warrior");
                    Animator anim = null;
                    if (visualPrefab)
                    {
                        var vis = Object.Instantiate(visualPrefab, enemyGo.transform);
                        foreach (var col in vis.GetComponentsInChildren<Collider>(true)) col.enabled = false;
                        anim = vis.GetComponentInChildren<Animator>(true);
                    }
                    ec = enemyGo.AddComponent<EnemyController>();
                    ec.enemyId = "revamp-test-enemy";
                    ec.animator = anim;
                    ec.player = pc;
                    ec.heavyAttacker = true;
                    ec.attackDuration = 0.7f;
                    ec.cleaveDuration = 0.55f;
                    ec.SetNetClientSimulationForTest(true);

                    // Stage 1 -> 2: Remote enemy is moving (Walk/Run) while receiving ActionAttack -> Attack MUST NOT be overwritten
                    ec.ApplyReplicatedState(300f, 300f, true, new Vector3(3.2f, 0.1f, 0f), 90f);
                    ec.ApplyReplicatedAction(1u, EnemyController.ActionAttack, 0.75f, enemyGo.transform.position, "Shadow Bolt", "💀");
                    actionExpiresBeforeDup = ec.RemainingRemoteAction;
                    // Send duplicate seq=1 & stale seq=1; must ignore and not extend lock
                    ec.ApplyReplicatedAction(1u, EnemyController.ActionCleave, 2.5f, enemyGo.transform.position, "", "");
                    Physics.SyncTransforms();
                    Wait(0.25);
                }
                else if (stage == 2)
                {
                    bool movingHeldAttack = ec.ActiveRemoteActionKind == EnemyController.ActionAttack
                                         && ec.CurrentAnimState == "Attack"
                                         && ec.LastRemoteActionSeq == 1u
                                         && ec.RemainingRemoteAction <= actionExpiresBeforeDup
                                         && CastBarSystem.IsTargetCasting(eStats)
                                         && enemyGo.transform.position.x > 0.4f;
                    Check("client_moving_preserves_attack_and_ignores_dup_seq", movingHeldAttack,
                        $"kind={ec.ActiveRemoteActionKind} state={ec.CurrentAnimState} seq={ec.LastRemoteActionSeq} x={enemyGo.transform.position.x:F2} menuOpen={FrontendMenu.IsOpen} ts={Time.timeScale:F2} alive={eStats.Alive} active={enemyGo.activeInHierarchy} enabled={ec.enabled}");

                    // Hit while active Attack -> must NOT clobber active Attack window!
                    ec.ApplyReplicatedAction(2u, EnemyController.ActionHit, 0.28f, enemyGo.transform.position, "", "");
                    Check("hit_does_not_clobber_active_attack", ec.ActiveRemoteActionKind == EnemyController.ActionAttack && ec.CurrentAnimState == "Attack",
                        $"kindAfterHit={ec.ActiveRemoteActionKind} state={ec.CurrentAnimState}");

                    // Wait past finite 0.75s Attack window while still moving toward further position
                    ec.ApplyReplicatedState(300f, 300f, true, new Vector3(8.5f, 0.1f, 0f), 90f);
                    Wait(0.75);
                }
                else if (stage == 3)
                {
                    bool returnedToLocomotion = ec.ActiveRemoteActionKind == 0
                                             && (ec.CurrentAnimState == "Walk" || ec.CurrentAnimState == "Run" || ec.CurrentAnimState == "Idle");
                    Check("finite_action_expiry_returns_to_locomotion", returnedToLocomotion,
                        $"kind={ec.ActiveRemoteActionKind} state={ec.CurrentAnimState} posX={enemyGo.transform.position.x:F2}");

                    // Remote HeavyWindup -> warning + cast bar, then ActionInterrupt clears warning & interrupts cast bar cleanly
                    ec.ApplyReplicatedAction(3u, EnemyController.ActionHeavyWindup, 1.5f, enemyGo.transform.position, "Crushing Blow", "🔨");
                    Wait(0.2);
                }
                else if (stage == 4)
                {
                    bool windupActive = ec.IsWarningActive && CastBarSystem.IsTargetCasting(eStats) && ec.ActiveRemoteActionKind == EnemyController.ActionHeavyWindup;
                    Check("heavy_windup_shows_warning_and_castbar", windupActive,
                        $"warn={ec.IsWarningActive} casting={CastBarSystem.IsTargetCasting(eStats)}");

                    ec.ApplyReplicatedAction(4u, EnemyController.ActionInterrupt, 0.45f, enemyGo.transform.position, "", "");
                    Wait(0.15);
                }
                else if (stage == 5)
                {
                    var tCast = CastBarSystem.GetTargetCast(eStats);
                    bool intCleared = !ec.IsWarningActive && ec.ActiveRemoteActionKind == EnemyController.ActionInterrupt && tCast != null && tCast.isInterrupted;
                    Check("interrupt_clears_warning_and_marks_cast_interrupted", intCleared,
                        $"warn={ec.IsWarningActive} kind={ec.ActiveRemoteActionKind} castInt={(tCast != null && tCast.isInterrupted)}");
                    Wait(0.5);
                }
                else if (stage == 6)
                {
                    Check("interrupt_stagger_expires_cleanly", ec.ActiveRemoteActionKind == 0,
                        $"kindAfterInt={ec.ActiveRemoteActionKind} state={ec.CurrentAnimState}");

                    // Death mid-warning cancels ring/cast bar; Alive state recovers cleanly
                    ec.ApplyReplicatedAction(5u, EnemyController.ActionHeavyWindup, 1.8f, enemyGo.transform.position, "Crushing Blow", "🔨");
                    ec.ApplyReplicatedState(0f, 300f, false, enemyGo.transform.position, 0f);
                    ec.ApplyReplicatedAction(6u, EnemyController.ActionDeath, 0f, enemyGo.transform.position, "", "");
                    Wait(0.15);
                }
                else if (stage == 7)
                {
                    Check("death_overrides_action_and_clears_warning", !eStats.Alive && !ec.IsWarningActive && ec.ActiveRemoteActionKind == 0,
                        $"alive={eStats.Alive} warn={ec.IsWarningActive} kind={ec.ActiveRemoteActionKind}");

                    // Rig fallback test on controllers lacking Death/Cleave (Brute.controller / Wolf.controller) — requires real Animator & controllers
                    wolfCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/RPG/Animations/NPCs/Wolf.controller");
                    bruteCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/RPG/Animations/Brute.controller");
                    RuntimeAnimatorController origCtrl = ec.animator ? ec.animator.runtimeAnimatorController : null;
                    bool animReady = ec.animator != null && origCtrl != null && bruteCtrl != null && wolfCtrl != null;
                    string deathFallbackBrute = "missing";
                    string wolfFallbackIdle = "missing";
                    if (animReady)
                    {
                        ec.animator.runtimeAnimatorController = bruteCtrl;
                        deathFallbackBrute = ec.ResolveAnimState("Death", "Hit");
                        ec.animator.runtimeAnimatorController = wolfCtrl;
                        wolfFallbackIdle = ec.ResolveAnimState("Cleave", "Attack");
                        ec.animator.runtimeAnimatorController = origCtrl;
                    }
                    Check("rig_fallback_missing_states_safe",
                        animReady && deathFallbackBrute == "Hit" && wolfFallbackIdle == "Default",
                        $"animReady={animReady} bruteDeath={deathFallbackBrute} wolfCleave={wolfFallbackIdle}");

                    // Revive remote enemy before ranged late-join snapshot test
                    ec.ApplyReplicatedState(300f, 300f, true, enemyGo.transform.position, 0f);
                    CastBarSystem.CancelTargetCast(eStats);
                    ec.ranged = true;
                    ec.attackDuration = 1.2f;
                    typeof(EnemyController).GetField("pendingDamage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, true);
                    typeof(EnemyController).GetField("attackEnds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, Time.time + 0.30f);
                    typeof(EnemyController).GetField("damageAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, Time.time + 0.12f);
                    typeof(EnemyController).GetField("activeSpellName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, "Shadow Bolt");
                    typeof(EnemyController).GetField("activeSpellIcon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, "💀");
                    bool gotSnap = ec.TryGetActiveActionSnapshot(out byte snapKind, out float snapRem, out float snapCastRem, out Vector3 snapOrig, out string snapSpell, out string snapIcon, out uint snapSeq);
                    typeof(EnemyController).GetField("pendingDamage", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, false);
                    ec.ApplyReplicatedState(300f, 300f, true, enemyGo.transform.position, 0f);
                    ec.ApplyReplicatedAction(7u, snapKind, snapRem, snapOrig, snapSpell, snapIcon, snapCastRem);
                    var lateCast = CastBarSystem.GetTargetCast(eStats);
                    bool exactRangedCastOk = gotSnap
                                          && Mathf.Abs(snapRem - 0.30f) < 0.02f
                                          && Mathf.Abs(snapCastRem - 0.12f) < 0.02f
                                          && lateCast != null
                                          && Mathf.Abs(lateCast.castDuration - 0.12f) < 0.02f
                                          && Mathf.Abs(ec.RemainingRemoteAction - 0.30f) < 0.02f;
                    Check("ranged_late_join_cast_remaining_matches_damage_deadline", exactRangedCastOk,
                        $"got={gotSnap} holdRem={snapRem:F3} castRem={snapCastRem:F3} barDur={(lateCast != null ? lateCast.castDuration : -1f):F3}");

                    // Expired / zero castRemaining must NEVER recreate a target cast bar
                    CastBarSystem.CancelTargetCast(eStats);
                    ec.ApplyReplicatedAction(8u, EnemyController.ActionAttack, 0.20f, enemyGo.transform.position, "Shadow Bolt", "💀", 0f);
                    bool noExpiredBarZero = !CastBarSystem.IsTargetCasting(eStats) && CastBarSystem.GetTargetCast(eStats) == null;
                    ec.ApplyReplicatedAction(9u, EnemyController.ActionAttack, 0.20f, enemyGo.transform.position, "Shadow Bolt", "💀", 0.01f);
                    bool noExpiredBarTiny = !CastBarSystem.IsTargetCasting(eStats) && CastBarSystem.GetTargetCast(eStats) == null;
                    Check("expired_cast_remaining_never_recreates_cast_bar", noExpiredBarZero && noExpiredBarTiny,
                        $"noBarZero={noExpiredBarZero} noBarTiny={noExpiredBarTiny}");
                    ec.ranged = false;

                    // Switch to authoritative server mode: verify Heavy LOS block, Leash Cancel, Missing Target Cancel, & Single Melee Hit
                    ec.SetNetClientSimulationForTest(false);
                    ec.ResetForLoad();
                    enemyGo.transform.position = new Vector3(0f, 0.1f, 0f);
                    playerGo.transform.position = new Vector3(0f, 0.1f, 2.2f);
                    Physics.SyncTransforms();

                    // Place solid stone wall between heavy origin and player -> HeavyAttack contact must deal ZERO damage!
                    wallGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
                    wallGo.transform.position = new Vector3(0f, 1.2f, 1.1f);
                    wallGo.transform.localScale = new Vector3(5f, 3f, 0.4f);
                    Physics.SyncTransforms();

                    float hpBeforeWallHeavy = pStats.Health;
                    typeof(EnemyController).GetMethod("HeavyAttack", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ec, null);
                    typeof(EnemyController).GetField("heavyUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, Time.time - 0.01f);
                    Wait(0.15);
                    StageData = hpBeforeWallHeavy;
                }
                else if (stage == 8)
                {
                    bool wallBlockedHeavy = Mathf.Approximately(pStats.Health, StageData);
                    Check("server_heavy_los_blocks_through_solid_wall", wallBlockedHeavy,
                        $"hp={pStats.Health}/{StageData}");

                    if (wallGo) Object.DestroyImmediate(wallGo);
                    wallGo = null;

                    // Start HeavyAttack then move player beyond leash range -> CancelAttack fires automatically
                    typeof(EnemyController).GetMethod("HeavyAttack", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ec, null);
                    playerGo.transform.position = new Vector3(0f, 0.1f, 40f);
                    Wait(0.2);
                }
                else if (stage == 9)
                {
                    Check("server_leash_cancels_active_windup", !ec.HasPendingAttack && !ec.IsWarningActive && !CastBarSystem.IsTargetCasting(eStats),
                        $"pending={ec.HasPendingAttack} warn={ec.IsWarningActive}");

                    // Start attack then null out player target -> missing target cancels cleanly
                    playerGo.transform.position = new Vector3(0f, 0.1f, 1.6f);
                    typeof(EnemyController).GetMethod("HeavyAttack", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ec, null);
                    ec.player = null;
                    Wait(0.15);
                }
                else if (stage == 10)
                {
                    Check("server_missing_target_cancels_windup", !ec.HasPendingAttack && !ec.IsWarningActive,
                        $"pending={ec.HasPendingAttack} warn={ec.IsWarningActive}");

                    // Single melee commitment cycle within attackRange -> deals exact attackDamage once (no duplicate hit)
                    ec.player = pc;
                    ec.heavyAttacker = false;
                    ec.isBoss = false;
                    ec.attackDamage = 17f;
                    ec.attackDuration = 0.45f;
                    ec.attackInterval = 2.0f;
                    typeof(EnemyController).GetField("heavyUntil", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, 0f);
                    typeof(EnemyController).GetField("nextHeavy", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, Time.time + 99f);
                    typeof(EnemyController).GetField("nextAttack", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, 0f);
                    typeof(EnemyController).GetField("attackEnds", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(ec, 0f);
                    StageData = pStats.Health;
                    enemyGo.transform.LookAt(new Vector3(playerGo.transform.position.x, enemyGo.transform.position.y, playerGo.transform.position.z));
                    Wait(0.95);
                }
                else if (stage == 11)
                {
                    float dmgTaken = StageData - pStats.Health;
                    Check("server_melee_commitment_single_authoritative_hit", Mathf.Approximately(dmgTaken, 17f),
                        $"dmgTaken={dmgTaken:F1} expected=17.0");

                    bool authRulesOk = Riftbound.Network.RiftboundCoopPlayer.IsAuthorizedEnemySenderForIds(0UL, 0UL, 0UL)
                                    && !Riftbound.Network.RiftboundCoopPlayer.IsAuthorizedEnemySenderForIds(1UL, 0UL, 0UL)
                                    && !Riftbound.Network.RiftboundCoopPlayer.IsAuthorizedEnemySenderForIds(0UL, 1UL, 0UL)
                                    && !Riftbound.Network.RiftboundCoopPlayer.IsAuthorizedEnemySenderForIds(1UL, 1UL, 0UL);
                    Check("server_sender_and_owner_validation_rejects_spoof", authRulesOk,
                        $"authRulesOk={authRulesOk}");

                    bool canonicalSafeOffline = Riftbound.Network.RiftboundCoopPlayer.ResolveCanonicalHostChannel(null) == null
                                              && !Riftbound.Network.RiftboundCoopPlayer.IsAuthorizedEnemySenderForIds(2UL, 0UL, 0UL);
                    Check("canonical_host_channel_null_offline_and_rejects_guest_owner", canonicalSafeOffline,
                        $"canonicalSafeOffline={canonicalSafeOffline}");

                    ec.ResetForLoad();
                    playerGo.transform.position = new Vector3(0f, 0.1f, 7.0f);
                    wallGo = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
                    wallGo.transform.position = new Vector3(0f, 1.1f, 2.8f);
                    wallGo.transform.localScale = new Vector3(2.8f, 2.5f, .5f);
                    Physics.SyncTransforms();
                    Wait(.55);
                }
                else if (stage == 12)
                {
                    Check("server_does_not_aggro_through_wall", !ec.HasPendingAttack && enemyGo.transform.position.z < .25f,
                        $"position={enemyGo.transform.position} pending={ec.HasPendingAttack}");
                    eStats.Damage(3f);
                    Wait(.95);
                }
                else if (stage == 13)
                {
                    var position = enemyGo.transform.position;
                    Check("server_routes_around_blocking_cover", position.z > 1.25f && (Mathf.Abs(position.x) > .8f || position.z > 3.5f),
                        $"position={position}");
                    Object.DestroyImmediate(wallGo);
                    playerGo.transform.position = new Vector3(0f, .1f, 39f);
                    Wait(2.9);
                }
                else if (stage == 14)
                {
                    Check("server_returns_home_after_leash", enemyGo.transform.position.sqrMagnitude < 1.3f &&
                        !ec.HasPendingAttack && Mathf.Approximately(eStats.Health, eStats.maxHealth),
                        $"position={enemyGo.transform.position} health={eStats.Health}/{eStats.maxHealth}");
                    ec.SetNetClientSimulationForTest(true);
                    ec.ApplyReplicatedState(0f, eStats.maxHealth, false, enemyGo.transform.position, enemyGo.transform.eulerAngles.y);
                    Check("client_replicated_death_marks_enemy_dead_immediately", Mathf.Approximately(eStats.Health, 0f) && !eStats.Alive,
                        $"hp={eStats.Health:F1} alive={eStats.Alive}");
                    ec.SetNetClientSimulationForTest(false);
                    Finish(false);
                }
            }
            catch (Exception ex)
            {
                Lines.Add("FAIL [exception] " + ex);
                Finish(true);
            }
        }

        static float StageData;

        static GameObject Track(GameObject go)
        {
            Tracked.Add(go);
            return go;
        }

        static void Finish(bool failed)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            foreach (var go in Tracked) if (go) Object.DestroyImmediate(go);
            Tracked.Clear();
            if (File.Exists(SaveFixturePath)) File.Delete(SaveFixturePath);
            if (Errors.Count > 0) failed = true;
            Lines.Add($"TOTAL PASS={passed} ERRORS={Errors.Count} RESULT={(failed ? "FAIL" : "PASS")}");
            Lines.AddRange(Errors);
            File.WriteAllText(Output, string.Join("\n", Lines) + "\n");
            SessionState.SetBool(Key + ".Failed", failed);
            EditorApplication.isPlaying = false;
        }
    }
}
