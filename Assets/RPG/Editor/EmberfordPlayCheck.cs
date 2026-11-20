using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    internal static class EmberfordPlayCheck
    {
        const string SessionKey = "Riftbound.Emberford.PlayCheck";
        static readonly string Report = Path.GetFullPath("../emberford-playcheck.txt");
        static PlayerController player;
        static Transform rightHand;
        static Keyboard keyboard;
        static Vector3 start, handBefore;
        static Quaternion wristBefore;
        static float handTravel, wristTravel;
        static int stage, samples;
        static bool preparedPlayer;
        static double due, timeout;

        static EmberfordPlayCheck() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void RunWithPreviews()
        {
            OriginalWorldRemodel.Run();
            EmberfordPresentation.Capture();
            Run();
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run the isolated Emberford smoke check in a Unity batch.");
            File.WriteAllText(Report, "Riftbound campaign Play Mode smoke check\n");
            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(SessionKey + ".DirectPlay", EditorPrefs.GetBool("Riftbound.DirectPlayInEditor", false));
            EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", true);
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSave()
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            PlayerController.SavePathOverride = Path.GetFullPath("../emberford-smoke-" + Guid.NewGuid().ToString("N") + ".json");
            Application.runInBackground = true;
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(SessionKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                due = EditorApplication.timeSinceStartup + 1;
                timeout = due + 75;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(SessionKey, false);
                EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", SessionState.GetBool(SessionKey + ".DirectPlay", false));
                EditorApplication.Exit(SessionState.GetInt(SessionKey + ".Exit", 1));
            }
        }

        static void Tick()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
                double now = EditorApplication.timeSinceStartup;
                if (now > timeout) throw new TimeoutException("The Emberford play test did not finish.");
                if (keyboard != null && (stage == 1 || stage == 2) && now < due)
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                if (now < due) return;
                if (stage == 0)
                {
                    player = Object.FindAnyObjectByType<PlayerController>();
                    if (!player || FrontendMenu.IsOpen || Time.frameCount < 10) return;
                    if (!preparedPlayer)
                    {
                        CharacterVisualFactory.ApplyToPlayer(player, "Human", "Warrior");
                        preparedPlayer = true;
                        due = now + .30;
                        return;
                    }
                    var body = player.GetComponentsInChildren<ProceduralActorVisual>().SingleOrDefault(v => v.Driver == player.animator);
                    Require(body && body.Driver == player.animator && player.animator.isHuman, "Player uses the campaign animator.");
                    var skins = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    var replacement = skins.Single(s => s.name == "Emberwatch_Knight_LOD0");
                    rightHand = replacement.bones.Single(t => t && t.name == "hand.R");
                    Require(skins.Where(s => s != replacement && !s.name.Contains("_LOD")).All(s => s.forceRenderingOff || !s.enabled),
                        "Legacy skinned renderers are hidden.");
                    int previews = 0;
                    foreach (string race in CharacterVisualFactory.AllianceRaces.Concat(CharacterVisualFactory.HordeRaces))
                    foreach (string playerClass in CharacterVisualFactory.AllClasses)
                    {
                        var preview = CharacterVisualFactory.SpawnPreview(race, playerClass, null);
                        var kit = preview ? preview.GetComponent<ProceduralActorVisual>() : null;
                        Require(kit && kit.Driver && preview.GetComponentsInChildren<SkinnedMeshRenderer>().Any(
                            s => s.name.EndsWith("_LOD0", StringComparison.Ordinal) && s.enabled && s.gameObject.activeInHierarchy),
                            $"Class preview {race} {playerClass}.");
                        Object.Destroy(preview);
                        previews++;
                    }
                    Log($"PASS {previews} faction, race, and class previews render the replacement skins.");
                    Require(EnemyController.FindByKey("camp-guard-1") &&
                        EnemyController.FindByKey("ashwood-captain"), "Both existing Ashwood encounters register in the rebuilt locations.");
                    Require(Object.FindObjectsByType<TownNPC>().Count(n => n.npcName == "Blacksmith Torvald") == 1,
                        "Emberwatch smith is present with one dialogue component.");
                    Require(Object.FindObjectsByType<ProceduralActorVisual>(FindObjectsInactive.Include).Count(v => v.StagedDisplay) >= 80,
                        "Campaign actors have serialized replacement models.");
                    MovePlayer(0f, 13f);
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    keyboard = InputSystem.AddDevice<Keyboard>("EmberfordSmokeKeyboard");
                    keyboard.MakeCurrent();
                    start = player.transform.position;
                    handBefore = player.transform.InverseTransformPoint(rightHand.position);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    stage = 1;
                    due = now + 1.15;
                    return;
                }
                if (stage == 1)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    float distance = Vector3.ProjectOnPlane(player.transform.position - start, Vector3.up).magnitude;
                    Require(distance > .7f, $"Keyboard movement through the original Emberwatch north gate ({distance:F2} m).");
                    Require(Vector3.Distance(handBefore, player.transform.InverseTransformPoint(rightHand.position)) > .015f,
                        "The replacement weapon hand moves with the player's gait.");
                    MovePlayer(-266f, 50f, 90);
                    start = player.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    stage = 2;
                    due = now + 3.3;
                    return;
                }
                if (stage == 2)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    Require(player.transform.position.x - start.x > 11f && player.transform.position.y > 5f,
                        $"Keyboard traversal across Westbank's timber deck ({player.transform.position.x - start.x:F2} m).");
                    var enemy = EnemyController.FindByKey("camp-guard-1");
                    var position = enemy.transform.position - Vector3.forward * 1.85f;
                    MovePlayer(position.x, position.z);
                    player.SetTarget(enemy.GetComponent<Combatant>());
                    handBefore = player.transform.InverseTransformPoint(rightHand.position);
                    wristBefore = rightHand.localRotation;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Digit1));
                    stage = 3;
                    due = now + .08;
                    return;
                }
                if (stage == 3)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    samples++;
                    handTravel = Mathf.Max(handTravel,
                        Vector3.Distance(handBefore, player.transform.InverseTransformPoint(rightHand.position)));
                    wristTravel = Mathf.Max(wristTravel, Quaternion.Angle(wristBefore, rightHand.localRotation));
                    if (samples < 12)
                    {
                        due = now + .065;
                        return;
                    }
                    Require(handTravel > .05f, $"The right-hand grip follows the attack ({handTravel:F2} m).");
                    Require(wristTravel > 3f, $"The weapon inherits the animated wrist rotation ({wristTravel:F1}°).");
                    Log("PASS Emberford campaign Play Mode smoke check completed.");
                    Finish(true);
                }
            }
            catch (Exception e)
            {
                Log("FAIL " + e.Message);
                Debug.LogException(e);
                Finish(false);
            }
        }

        static void MovePlayer(float x, float z, float yaw = 0)
        {
            var motor = player.GetComponent<CharacterController>();
            motor.enabled = false;
            var point = new Vector3(x, 0, z);
            point.y = WorldRegions.GroundHeight(point) + .1f;
            if (Physics.Raycast(new Vector3(x, point.y + 5, z), Vector3.down, out var hit, 8f))
                point.y = Mathf.Max(point.y, hit.point.y + .08f);
            player.transform.SetPositionAndRotation(point, Quaternion.Euler(0, yaw, 0));
            motor.enabled = true;
            Physics.SyncTransforms();
            if (player.orbit)
            {
                player.orbit.SetPaused(false);
                player.orbit.FaceTravel(yaw);
            }
        }

        static void Require(bool result, string message)
        {
            if (!result) throw new InvalidOperationException(message);
            if (!message.StartsWith("Class preview", StringComparison.Ordinal)) Log("PASS " + message);
        }

        static void Log(string message)
        {
            File.AppendAllText(Report, message + Environment.NewLine);
            Debug.Log(message);
        }

        static void Finish(bool passed)
        {
            EditorApplication.update -= Tick;
            SessionState.SetInt(SessionKey + ".Exit", passed ? 0 : 1);
            EditorApplication.isPlaying = false;
        }
    }
}
