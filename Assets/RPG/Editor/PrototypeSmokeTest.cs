using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class PrototypeSmokeTest
    {
        static Keyboard keyboard;
        static PlayerController player;
        static EnemyController[] enemies;
        static readonly List<string> Results = new List<string>();
        static int step, enemyIndex, attacks;
        static double due;
        static Vector3 start;
        static Vector3 dialoguePosition;
        static bool release;
        static int releaseAfterFrame;
        static InputSettings.EditorInputBehaviorInPlayMode previousInputBehavior;
        static InputSettings.BackgroundBehavior previousBackgroundBehavior;
        static PrototypeSmokeTest() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Riftbound/Run gameplay smoke test")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) return;
            if (EditorSceneManagerPath() != "Assets/RPG/Scenes/Outpost.unity")
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost.unity");
            SessionState.SetBool("Riftbound.SmokeTest", true);
            EditorApplication.isPlaying = true;
        }
        static string EditorSceneManagerPath() => UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        static void Changed(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Riftbound.SmokeTest", false))
            {
                Results.Clear(); step = 0; enemyIndex = 0; attacks = 0;
                keyboard = InputSystem.AddDevice<Keyboard>("PrototypeSmokeKeyboard");
                keyboard.MakeCurrent();
                previousInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
                previousBackgroundBehavior = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Application.runInBackground = true;
                due = EditorApplication.timeSinceStartup + 2;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                keyboard = null;
                InputSystem.settings.editorInputBehaviorInPlayMode = previousInputBehavior;
                InputSystem.settings.backgroundBehavior = previousBackgroundBehavior;
                SessionState.SetBool("Riftbound.SmokeTest", false);
            }
        }
        static void Pulse(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); release = true; releaseAfterFrame = Time.frameCount + 2;
        }
        static void Check(bool condition, string message)
        {
            Results.Add((condition ? "PASS " : "FAIL ") + message);
            if (!condition) throw new Exception(message);
        }
        static void Move(Vector3 position)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false; player.transform.position = position; motor.enabled = true;
        }
        static void Tick()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (release)
            {
                if (Time.frameCount < releaseAfterFrame) return;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); release = false; return;
            }
            if (EditorApplication.timeSinceStartup < due) return;
            due = EditorApplication.timeSinceStartup + .4;
            try
            {
                switch (step)
                {
                    case 0:
                        player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                        enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
                        Check(player && enemies.Length == 4, "Player and four raiders exist");
                        Check(player.animator && player.animator.avatar && player.animator.avatar.isValid, "Brute humanoid avatar is valid");
                        start = player.transform.position;
                        Check(!player.Walking, "Running is the default movement mode");
                        Pulse(Key.C); step = 102; break;
                    case 102:
                        Check(player.Walking, "C enables slow walking");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); due += 4; step = 1; break;
                    case 1:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Check(Vector3.Distance(start, player.transform.position) > 1, "WASD moves the player");
                        var walking = player.animator.GetCurrentAnimatorStateInfo(0);
                        Check(walking.IsName("Walk") && walking.loop && walking.normalizedTime > 2, "Walking keeps animating beyond two full cycles");
                        var leftFoot = player.animator.GetBoneTransform(HumanBodyBones.LeftToes);
                        var rightFoot = player.animator.GetBoneTransform(HumanBodyBones.RightToes);
                        bool groundHit = Physics.Raycast(player.transform.position + Vector3.up * .4f, Vector3.down, out var ground, 1, ~(1 << 2));
                        float footHeight = Mathf.Min(leftFoot.position.y, rightFoot.position.y) - ground.point.y;
                        Check(groundHit && footHeight > -.05f && footHeight < .3f, "Walking feet stay near ground while retaining natural gait bounce");
                        Pulse(Key.C); step = 103; break;
                    case 103:
                        Check(!player.Walking, "C toggles back to running");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); due += 2.6; step = 100; break;
                    case 100:
                        var running = player.animator.GetCurrentAnimatorStateInfo(0);
                        Check(running.IsName("Run") && running.loop && running.normalizedTime > 2, "Running keeps animating beyond two full cycles");
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        Move(player.questGiver.position + Vector3.back * 2); step = 2; break;
                    case 2: Pulse(Key.E); step++; break;
                    case 3:
                        Check(player.DialogueOpen && !player.QuestAccepted, "E opens Abe dialogue without auto-accepting");
                        Pulse(Key.Escape); step = 105; break;
                    case 105:
                        Check(!player.DialogueOpen && !player.orbit.Paused, "Escape closes dialogue without pausing");
                        Pulse(Key.E); step = 106; break;
                    case 106:
                        Check(player.DialogueOpen && player.ActiveQuestGiver.npcName == "Abe", "Abe dialogue reopens");
                        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                            ScreenCapture.CaptureScreenshot(Path.GetFullPath("AbeDialoguePreview.png"));
                        dialoguePosition = player.transform.position;
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Digit1)); step = 104; break;
                    case 104:
                        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        var dialogueTravel = player.transform.position - dialoguePosition; dialogueTravel.y = 0;
                        Check(dialogueTravel.magnitude < .05f, "Dialogue blocks movement and combat input");
                        player.CompleteQuest(); Check(!player.QuestComplete && player.Gold == 0, "Incomplete quest cannot grant rewards");
                        player.AcceptQuest();
                        Check(player.QuestAccepted && !player.DialogueOpen, "Accept button accepts quest and closes window");
                        player.Stats.Damage(60); Pulse(Key.Digit3); step = 4; break;
                    case 4:
                        Check(player.Stats.Health > 120 && player.HealReady > Time.time, "Second Wind heals and starts cooldown");
                        Move(enemies[0].transform.position + Vector3.back * 1.8f); Pulse(Key.Tab); step++; break;
                    case 5: Pulse(Key.Digit2); due += .4; step = 101; break;
                    case 101:
                        var cleaving = player.animator.GetCurrentAnimatorStateInfo(0);
                        Check(cleaving.IsName("Cleave") && cleaving.normalizedTime > .5f && cleaving.normalizedTime < 1, "Combat animation continues through its second half");
                        due += .6; step = 6; break;
                    case 6:
                        Check(enemies[0].GetComponent<Combatant>().Health < 70 && player.CleaveReady > Time.time, "Whirlwind damages enemy and starts cooldown");
                        step++; break;
                    case 7:
                        if (enemyIndex >= 3) { step++; break; }
                        var enemy = enemies[enemyIndex];
                        if (!enemy.GetComponent<Combatant>().Alive) { enemyIndex++; attacks = 0; break; }
                        Move(enemy.transform.position + Vector3.back * 1.8f);
                        player.SelectTarget(); Pulse(Key.Digit1); due += 1; attacks++;
                        Check(attacks < 8, "Strike applied to raider " + (enemyIndex + 1));
                        player.Stats.Heal(30); break;
                    case 8:
                        Check(player.Kills >= 3, "Three kills advance mission objective");
                        player.Stats.Damage(1000); step++; break;
                    case 9: Pulse(Key.R); step++; break;
                    case 10:
                        Check(player.Stats.Alive && player.Stats.Health == player.Stats.maxHealth, "R restores player after defeat");
                        Move(player.questGiver.position + Vector3.back * 2); step++; break;
                    case 11: Pulse(Key.E); step++; break;
                    case 12:
                        Check(player.DialogueOpen && !player.QuestComplete, "Returning to Abe opens completion dialogue");
                        player.CompleteQuest();
                        Check(player.QuestComplete && player.Gold == 25 && !player.DialogueOpen, "Complete Quest grants 25 gold and closes window");
                        player.CompleteQuest(); Check(player.Gold == 25, "Quest reward cannot be claimed twice");
                        Pulse(Key.Escape); step++; break;
                    case 13:
                        Check(Time.timeScale == 0, "Escape pauses gameplay"); Pulse(Key.Escape); step++; break;
                    case 14:
                        Check(Time.timeScale == 1, "Escape resumes gameplay");
                        Move(start); if (!Application.isBatchMode) ScreenCapture.CaptureScreenshot(Path.GetFullPath("PrototypePreview.png")); step++; due += 1; break;
                    default: Finish(); break;
                }
            }
            catch (Exception exception) { Results.Add("ERROR " + exception); Finish(); }
        }
        static void Finish()
        {
            File.WriteAllLines("PrototypeSmokeResults.txt", Results);
            EditorApplication.update -= Tick;
            EditorApplication.isPlaying = false;
            if (Application.isBatchMode) EditorApplication.Exit(Results.Any(r => r.StartsWith("FAIL") || r.StartsWith("ERROR")) ? 1 : 0);
        }
    }
}
