using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class PlayerMovementChecks
    {
        const string Key = "Riftbound.PlayerMovementChecks", Output = ".code-check/movement/checks.txt";
        static readonly List<string> lines = new List<string>();
        static PlayerController player;
        static CharacterController motor;
        static OrbitCamera camera;
        static Combatant victim;
        static Keyboard keyboard;
        static Mouse mouse;
        static int stage, passed, frame, hits;
        static double deadline;
        static float due, cameraYaw, ready;
        static bool ceilingHit;
        static float ceilingDeadline;

        static PlayerMovementChecks() { EditorApplication.playModeStateChanged += Changed; }

        public static void Run()
        {
            if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use an isolated batch outside Play Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Key, true);
            SessionState.SetBool(Key + ".Failed", true);
            EditorApplication.isPaused = false;
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Isolate()
        {
            if (SessionState.GetBool(Key, false))
                PlayerController.SavePathOverride = Path.GetFullPath(".code-check/movement/unused-save-" + Guid.NewGuid().ToString("N") + ".json");
        }

        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                Application.runInBackground = true;
                Time.timeScale = 1;
                Time.captureDeltaTime = 1f / 60;
                lines.Clear(); passed = hits = stage = 0; frame = -1;
                deadline = EditorApplication.timeSinceStartup + 35;
                lines.Add("Native empty-scene fixtures at fixed 60Hz simulation with synthetic input; player Update invoked from an actual Unity Update driver. NOT normal-input gameplay or online acceptance.");
                PlayerMovementFixtureDriver.Step = Tick;
                new GameObject("Disposable movement fixture driver").AddComponent<PlayerMovementFixtureDriver>();
                EditorApplication.update += Watchdog;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Key, false);
                EditorApplication.Exit(SessionState.GetBool(Key + ".Failed", true) ? 1 : 0);
            }
        }

        static void Watchdog()
        {
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup > deadline)
                Finish(new TimeoutException($"Movement fixture stage {stage}, frame={Time.frameCount}, scale={Time.timeScale}"));
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException($"Movement fixture stage {stage}, frame={Time.frameCount}, scale={Time.timeScale}, paused={EditorApplication.isPaused}");
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
                frame = Time.frameCount;
                if (Time.time < due)
                {
                    if (player) Invoke(player, "Update");
                    return;
                }
                if (stage == 0)
                {
                    JumpChecks();
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null, false);
                    PrototypeHUD.MenuOpen = false;
                    var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(100, 1, 100);
                    var actor = new GameObject("Disposable movement actor");
                    actor.AddComponent<CharacterController>(); actor.AddComponent<Combatant>();
                    player = actor.AddComponent<PlayerController>(); player.enabled = false;
                    player.Stats.isPlayer = true;
                    typeof(PlayerController).GetProperty("CharacterClass").SetValue(player, "Warrior");
                    motor = actor.GetComponent<CharacterController>(); motor.height = 1.8f; motor.center = Vector3.up * .9f; motor.radius = .35f;
                    camera = new GameObject("Disposable movement camera").AddComponent<OrbitCamera>();
                    camera.enabled = false; camera.target = actor.transform; Set(camera, "player", player); player.orbit = camera;
                    keyboard = InputSystem.AddDevice<Keyboard>(); keyboard.MakeCurrent();
                    mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
                    Settle();
                    stage++; return;
                }
                if (stage == 1)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 3, delta = new Vector2(20, 0) });
                    InputSystem.Update();
                    float before = camera.Yaw;
                    camera.RefreshInput(); cameraYaw = camera.Yaw;
                    Check("current_frame_mouse_steering", camera.IsSteering && camera.BothMouseButtonsPressed && Mathf.Abs(cameraYaw - before - 4.4f) < .001f);
                    camera.RefreshInput();
                    Check("mouse_delta_consumed_once", camera.Yaw == cameraYaw);
                    player.InventoryOpen = true; camera.RefreshInput();
                    Check("modal_clears_steering_same_frame", !camera.IsSteering && !camera.IsFreeLooking && !camera.BothMouseButtonsPressed && camera.Yaw == cameraYaw);
                    player.InventoryOpen = false;
                    stage++; return;
                }
                if (stage == 2)
                {
                    InputSystem.QueueStateEvent(mouse, new MouseState());
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Space)); InputSystem.Update();
                    Settle(); Invoke(player, "Update");
                    Check("actual_player_update_starts_jump", Get<float>(player, "vertical") > 0);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                    Settle(); Set(player, "vertical", 0f);
                    var target = new GameObject("Disposable queue target"); target.transform.position = player.transform.position + Vector3.forward * 1.8f;
                    victim = target.AddComponent<Combatant>(); victim.maxHealth = 1000; victim.Restore(); victim.Damaged += amount => hits++;
                    player.SetTarget(victim);
                    ready = Time.time + .30f; Set(player, "attackReady", ready);
                    player.RequestPrimaryAttackInput();
                    Check("early_primary_accepted_through_ready", player.BufferedAbilitySlot == 1 && Get<float>(player, "bufferedAbilityExpiresAt") >= ready + .159f);
                    due = Time.time + .21f; stage++; return;
                }
                if (stage == 3)
                {
                    Check("accepted_queue_survives_old_expiry", player.BufferedAbilitySlot == 1 && Time.time < ready);
                    player.InventoryOpen = true; Invoke(player, "Update");
                    Check("modal_cancels_queued_action", player.BufferedAbilitySlot == 0);
                    player.InventoryOpen = false;
                    Settle();
                    Check("ready_queue_fixture_grounded", motor.isGrounded);
                    ready = Time.time + .30f; Set(player, "attackReady", ready); player.RequestPrimaryAttackInput();
                    Check("second_primary_accepted", player.BufferedAbilitySlot == 1);
                    due = ready + .10f; stage++; return;
                }
                if (stage == 4)
                {
                    Invoke(player, "Update");
                    float accepted = Get<float>(player, "attackReady");
                    lines.Add($"Dispatch observation: t={Time.time:F4} dt={Time.deltaTime:F5} ready={ready:F4} actualReady={accepted:F4} queued={player.BufferedAbilitySlot} grounded={motor.isGrounded} target={player.Target != null} alive={player.Stats.Alive} blocked={player.AnyBlockingWindowOpen()} message={player.Message}");
                    Check("queued_action_dispatches_when_ready", player.BufferedAbilitySlot == 0 && accepted > Time.time + .5f);
                    Invoke(player, "Update");
                    Check("queue_dispatch_not_repeated", Get<float>(player, "attackReady") == accepted);
                    due = Time.time + .65f; stage++; return;
                }
                if (stage == 5)
                {
                    Check("one_authoritative_offline_impact", hits == 1);
                    Set(player, "attackReady", Time.time + .5f); player.RequestPrimaryAttackInput();
                    Check("outside_queue_window_rejected", player.BufferedAbilitySlot == 0);
                    var mount = new GameObject("Disposable mount").AddComponent<MountVisual>(); mount.transform.SetParent(player.transform);
                    Set(player, "mountInstance", mount); Set(player, "normalHeight", motor.height); Set(player, "normalRadius", motor.radius); Set(player, "normalCenter", motor.center);
                    var position = player.transform.position; player.transform.rotation = Quaternion.Euler(35, 70, 15);
                    player.Dismount();
                    Check("dismount_restores_upright_root", !player.Mounted && Vector3.Dot(player.transform.up, Vector3.up) > .9999f && Mathf.Abs(Mathf.DeltaAngle(player.transform.eulerAngles.y, 70)) < .01f && player.transform.position == position);
                    Settle(); Set(player, "busyUntil", 0f); Set(player, "vertical", -2f);
                    Get<JumpInputBuffer>(player, "jumpInput").Reset();
                    var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ceiling.transform.position = player.transform.position + Vector3.up * (motor.height + .12f);
                    ceiling.transform.localScale = new Vector3(4, .16f, 4); Physics.SyncTransforms();
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(UnityEngine.InputSystem.Key.Space)); InputSystem.Update();
                    Invoke(player, "Update"); ceilingHit = (motor.collisionFlags & CollisionFlags.Above) != 0;
                    ceilingDeadline = Time.time + .35f; due = 0; stage++; return;
                }
                if (stage == 6)
                {
                    if (!ceilingHit)
                    {
                        Invoke(player, "Update"); ceilingHit = (motor.collisionFlags & CollisionFlags.Above) != 0;
                        if (!ceilingHit && Time.time < ceilingDeadline) return;
                    }
                    Check("low_ceiling_contact_observed", ceilingHit);
                    Check("ceiling_cancels_upward_velocity", Get<float>(player, "vertical") <= 0);
                    Finish(null);
                }
            }
            catch (Exception e) { Finish(e); }
        }

        static void JumpChecks()
        {
            var input = new JumpInputBuffer();
            Check("no_jump_without_press", !input.TryJump(10, true, false, true));
            Check("coyote_press_80ms", input.TryJump(10.08f, false, true, true));
            Check("coyote_consumed_once", !input.TryJump(10.09f, false, true, true));
            input.Reset(); input.TryJump(20, true, false, true);
            Check("coyote_expired_110ms", !input.TryJump(20.11f, false, true, true));
            input.Reset(); input.TryJump(30, false, true, true);
            Check("landing_buffer_100ms", input.TryJump(30.10f, true, false, true));
            Check("landing_press_not_repeated_airborne", !input.TryJump(30.11f, false, false, true));
            input.Reset(); input.TryJump(40, false, true, true);
            Check("landing_buffer_expired_130ms", !input.TryJump(40.13f, true, false, true));
            input.Reset(); input.TryJump(50, false, true, true); input.TryJump(50.01f, true, false, false);
            Check("blocked_state_drops_jump", !input.TryJump(50.02f, true, false, true));
            input.TryJump(60, false, true, true); input.Reset();
            Check("reset_drops_pending_jump", !input.TryJump(60.01f, true, false, true));
        }

        static void Settle()
        {
            motor.enabled = false; player.transform.SetPositionAndRotation(Vector3.up * .15f, Quaternion.identity); motor.enabled = true;
            Physics.SyncTransforms(); motor.Move(Vector3.down * .4f);
        }
        static void Check(string name, bool ok)
        {
            lines.Add((ok ? "PASS " : "FAIL ") + name);
            if (!ok) throw new InvalidOperationException(name);
            passed++;
        }
        static FieldInfo Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static T Get<T>(object target, string name) => (T)Field(target, name).GetValue(target);
        static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
        static void Invoke(object target, string name) => target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        static void Finish(Exception error)
        {
            EditorApplication.update -= Watchdog;
            PlayerMovementFixtureDriver.Step = null;
            Time.captureDeltaTime = 0;
            if (error != null) lines.Add(error.ToString());
            lines.Add($"TOTAL PASS={passed} ERRORS={(error == null ? 0 : 1)}");
            File.WriteAllLines(Output, lines);
            if (player) player.StopAllCoroutines();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            SessionState.SetBool(Key + ".Failed", error != null);
            EditorApplication.isPlaying = false;
        }
    }
}
