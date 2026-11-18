using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class Gp01Diagnostics
    {
        const string PlayKey = "Riftbound.Gp01PlayVerify";
        static int playStage;
        static double dueAt, timeoutAt;
        static Keyboard kbd;
        static Mouse mouseDev;
        static Vector3 pStart;
        static float yawStart;
        static readonly List<string> PlayLog = new List<string>();
        static readonly List<string> PlayErrors = new List<string>();
        static Quaternion baseVisualRot;

        static Gp01Diagnostics()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        public static void RunPlayModeVerify()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run in batchmode.");
            Directory.CreateDirectory(".code-check");
            SessionState.SetBool(PlayKey, true);
            SessionState.SetBool(PlayKey + ".Failed", false);
            SessionState.SetString(PlayKey + ".SavePath", Path.GetFullPath(".code-check/gp01-play-save-" + Guid.NewGuid().ToString("N") + ".json"));
            Application.runInBackground = true;
            PlayerSettings.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(File.Exists(WorldRegions.ScenePath) ? WorldRegions.ScenePath : "Assets/RPG/Scenes/Outpost-Updated.unity");
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SetupIsolatedSave()
        {
            if (SessionState.GetBool(PlayKey, false))
            {
                Application.runInBackground = true;
                PlayerController.SavePathOverride = SessionState.GetString(PlayKey + ".SavePath", "");
            }
        }

        static void OnPlayStateChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(PlayKey, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                UIArtSetup.PrepareGameView();
                playStage = 0;
                dueAt = EditorApplication.timeSinceStartup + 2.0;
                timeoutAt = dueAt + 60.0;
                PlayLog.Clear();
                PlayErrors.Clear();
                Application.logMessageReceived += OnLog;
                EditorApplication.update += TickPlay;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(PlayKey, false);
                EditorApplication.Exit(SessionState.GetBool(PlayKey + ".Failed", false) ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Riftbound"))
                PlayErrors.Add(msg + "\n" + stack);
        }

        static bool AnimStateActive(Animator anim, string stateName)
        {
            if (!anim) return false;
            return anim.GetCurrentAnimatorStateInfo(0).IsName(stateName) || (anim.IsInTransition(0) && anim.GetNextAnimatorStateInfo(0).IsName(stateName));
        }

        static void Assert(bool cond, string label)
        {
            PlayLog.Add((cond ? "PASS " : "FAIL ") + label);
            if (!cond) throw new Exception("FAIL " + label);
        }

        static void SendKeys(params Key[] keys)
        {
            if (kbd == null) return;
            InputSystem.QueueStateEvent(kbd, new KeyboardState(keys));
            InputSystem.Update();
        }

        static void TapKey(Key key)
        {
            if (kbd == null) return;
            InputSystem.QueueStateEvent(kbd, new KeyboardState(key));
        }

        static void SendMouse(bool rightDown)
        {
            if (mouseDev == null) return;
            var ms = new MouseState { buttons = rightDown ? (ushort)2 : (ushort)0 };
            InputSystem.QueueStateEvent(mouseDev, ms);
            InputSystem.Update();
        }

        static void FinishPlay(bool failed)
        {
            EditorApplication.update -= TickPlay;
            Application.logMessageReceived -= OnLog;
            SendKeys();
            SendMouse(false);
            string sp = SessionState.GetString(PlayKey + ".SavePath", "");
            if (!string.IsNullOrEmpty(sp) && File.Exists(sp)) File.Delete(sp);
            string outPath = Path.Combine(Path.GetTempPath(), "gp01_03_play_verify.txt");
            if (PlayErrors.Count > 0) failed = true;
            PlayLog.Add($"ERRORS={PlayErrors.Count}");
            PlayLog.AddRange(PlayErrors);
            File.WriteAllText(outPath, string.Join("\n", PlayLog));
            SessionState.SetBool(PlayKey + ".Failed", failed);
            Time.timeScale = 1f;
            EditorApplication.isPlaying = false;
        }

        static void TickPlay()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeoutAt) throw new TimeoutException("Gp01PlayVerify timeout at stage " + playStage);
                if (EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < dueAt || !EditorApplication.isPlaying) return;

                var pc = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (playStage == 0)
                {
                    var menu = pc.GetComponent<FrontendMenu>();
                    var entering = typeof(FrontendMenu).GetField("enteringCharacter", BindingFlags.NonPublic | BindingFlags.Static);
                    entering.SetValue(null, new FrontendMenu.Character { id = Guid.NewGuid().ToString("N"), name = "GpVerify", faction = "Alliance", race = "Human", characterClass = "Warrior" });
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null, false);
                    pc.orbit.SetPaused(false);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    playStage = 1;
                    dueAt = EditorApplication.timeSinceStartup + 2.0;
                    return;
                }
                if (playStage == 1)
                {
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    Application.runInBackground = true;
                    kbd = InputSystem.AddDevice<Keyboard>("GpVerifyKeyboard");
                    mouseDev = InputSystem.AddDevice<Mouse>("GpVerifyMouse");
                    kbd.MakeCurrent();
                    mouseDev.MakeCurrent();
                    var cc0 = pc.GetComponent<CharacterController>();
                    if (cc0) cc0.enabled = false;
                    pc.transform.SetPositionAndRotation(new Vector3(0f, 0.25f, -6f), Quaternion.identity);
                    if (cc0) cc0.enabled = true;
                    baseVisualRot = pc.animator.transform.localRotation;
                    pStart = pc.transform.position;
                    SendKeys(Key.W);
                    playStage = 2;
                    dueAt = EditorApplication.timeSinceStartup + 0.7;
                    return;
                }
                if (playStage == 2)
                {
                    Vector3 delta = pc.transform.position - pStart; delta.y = 0f;
                    bool runActive = AnimStateActive(pc.animator, "Run");
                    float animSpd = pc.animator.speed;
                    SendKeys();
                    Assert(delta.magnitude > 1.2f && Vector3.Dot(delta.normalized, pc.transform.forward) > 0.9f, $"GP01 W forward run ({delta.magnitude:F2}m)");
                    Assert(animSpd > 0.9f && runActive, $"GP01 Run state active (animSpd={animSpd:F2})");
                    playStage = 25;
                    dueAt = EditorApplication.timeSinceStartup + 0.05;
                    return;
                }
                if (playStage == 25)
                {
                    TapKey(Key.Slash);
                    playStage = 3;
                    dueAt = EditorApplication.timeSinceStartup + 0.12;
                    return;
                }
                if (playStage == 3)
                {
                    Assert(pc.Walking, "GP02 Slash toggles Walking=true");
                    pStart = pc.transform.position;
                    SendKeys(Key.W);
                    playStage = 4;
                    dueAt = EditorApplication.timeSinceStartup + 0.65;
                    return;
                }
                if (playStage == 4)
                {
                    Vector3 walkDelta = pc.transform.position - pStart; walkDelta.y = 0f;
                    bool walkActive = AnimStateActive(pc.animator, "Walk");
                    float walkAnimSpd = pc.animator.speed;
                    SendKeys();
                    Assert(walkDelta.magnitude > 0.3f && walkDelta.magnitude < 2.2f, $"GP02 Walk displacement bounded ({walkDelta.magnitude:F2}m)");
                    Assert(walkActive && walkAnimSpd >= 1.2f, $"GP01 Calibrated Walk cadence (animSpd={walkAnimSpd:F2})");
                    playStage = 45;
                    dueAt = EditorApplication.timeSinceStartup + 0.05;
                    return;
                }
                if (playStage == 45)
                {
                    TapKey(Key.Slash);
                    playStage = 5;
                    dueAt = EditorApplication.timeSinceStartup + 0.12;
                    return;
                }
                if (playStage == 5)
                {
                    Assert(!pc.Walking, "GP02 Slash toggles Walking=false (back to Run)");
                    pStart = pc.transform.position;
                    SendKeys(Key.S);
                    playStage = 6;
                    dueAt = EditorApplication.timeSinceStartup + 0.55;
                    return;
                }
                if (playStage == 6)
                {
                    Vector3 backDelta = pc.transform.position - pStart; backDelta.y = 0f;
                    bool backActive = AnimStateActive(pc.animator, "RunBack");
                    SendKeys();
                    Assert(backDelta.magnitude > 0.4f && Vector3.Dot(backDelta.normalized, pc.transform.forward) < -0.9f, $"GP02 Backpedal S moves backward ({backDelta.magnitude:F2}m)");
                    Assert(backActive, "GP01 RunBack state active on backpedal");
                    // Test Strafe visual alignment (RMB + A)
                    pStart = pc.transform.position;
                    SendMouse(true);
                    SendKeys(Key.A);
                    playStage = 7;
                    dueAt = EditorApplication.timeSinceStartup + 0.5;
                    return;
                }
                if (playStage == 7)
                {
                    Vector3 strafeDelta = pc.transform.position - pStart; strafeDelta.y = 0f;
                    float visualAngle = Quaternion.Angle(baseVisualRot, pc.animator.transform.localRotation);
                    SendKeys();
                    SendMouse(false);
                    Assert(strafeDelta.magnitude > 0.6f && Vector3.Dot(strafeDelta.normalized, pc.transform.right) < -0.7f, $"GP02 RMB+A strafes left ({strafeDelta.magnitude:F2}m)");
                    Assert(visualAngle > 20f, $"GP01 Strafe aligns visual mesh into travel direction ({visualAngle:F1} deg)");
                    // Test Turn-in-place with A alone
                    yawStart = pc.transform.eulerAngles.y;
                    SendKeys(Key.A);
                    playStage = 8;
                    dueAt = EditorApplication.timeSinceStartup + 0.35;
                    return;
                }
                if (playStage == 8)
                {
                    float yawDelta = Mathf.Abs(Mathf.DeltaAngle(yawStart, pc.transform.eulerAngles.y));
                    bool walkActive = AnimStateActive(pc.animator, "Walk");
                    SendKeys();
                    Assert(yawDelta > 20f && walkActive, $"GP01 Turn-in-place plays Walk instead of frozen Idle (yawDelta={yawDelta:F1} deg)");
                    // Test ChatOpen gates Mount Z & Hearth H
                    pc.ChatOpen = true;
                    TapKey(Key.Z);
                    playStage = 9;
                    dueAt = EditorApplication.timeSinceStartup + 0.12;
                    return;
                }
                if (playStage == 9)
                {
                    bool mountBlocked = !pc.Mounting && !pc.Mounted;
                    SendKeys();
                    pc.ChatOpen = false;
                    Assert(mountBlocked, "GP02 ChatOpen blocks Z mount trigger");
                    playStage = 95;
                    dueAt = EditorApplication.timeSinceStartup + 0.05;
                    return;
                }
                if (playStage == 95)
                {
                    TapKey(Key.Z);
                    playStage = 10;
                    dueAt = EditorApplication.timeSinceStartup + 0.12;
                    return;
                }
                if (playStage == 10)
                {
                    Assert(pc.Mounting, "GP02 Z begins mount cast on clear ground");
                    SendKeys(Key.W);
                    playStage = 11;
                    dueAt = EditorApplication.timeSinceStartup + 0.12;
                    return;
                }
                if (playStage == 11)
                {
                    SendKeys();
                    Assert(!pc.Mounting, "GP02 W interrupts mount cast cleanly");
                    // Test GP03 Interaction Prompt near Abe + Out-of-range hint + Single press dialogue + Accept/Close
                    var abePos = pc.questGiver.position;
                    var cc = pc.GetComponent<CharacterController>();
                    cc.enabled = false;
                    pc.transform.position = abePos + new Vector3(0f, 0f, -4.8f);
                    cc.enabled = true;
                    bool inRangeFar = pc.TryGetBestInteraction(out _, out string farHint);
                    Assert(!inRangeFar && !string.IsNullOrEmpty(farHint), $"GP03 Out-of-range reports helpful hint ('{farHint}')");

                    cc.enabled = false;
                    pc.transform.position = abePos + new Vector3(0f, 0f, -2.0f);
                    cc.enabled = true;
                    bool hasAbe = pc.TryGetBestInteraction(out var abeCand, out _);
                    Assert(hasAbe && abeCand.text.Contains("Elder Abe"), $"GP03 Prompt resolves Elder Abe deterministically ('{abeCand.text}')");

                    playStage = 115;
                    dueAt = EditorApplication.timeSinceStartup + 0.05;
                    return;
                }
                if (playStage == 115)
                {
                    TapKey(Key.E);
                    playStage = 12;
                    dueAt = EditorApplication.timeSinceStartup + 0.15;
                    return;
                }
                if (playStage == 12)
                {
                    SendKeys();
                    Assert(pc.DialogueOpen && pc.ActiveQuestGiver != null, "GP03 E opens Abe dialogue");
                    pStart = pc.transform.position;
                    SendKeys(Key.W);
                    playStage = 13;
                    dueAt = EditorApplication.timeSinceStartup + 0.25;
                    return;
                }
                if (playStage == 13)
                {
                    Vector3 dDiag = pc.transform.position - pStart; dDiag.y = 0f;
                    SendKeys();
                    Assert(dDiag.magnitude < 0.05f, $"GP03 Dialogue blocks movement ({dDiag.magnitude:F3}m)");
                    pc.AcceptQuest();
                    Assert(pc.QuestAccepted && !pc.DialogueOpen && Cursor.lockState == CursorLockMode.None && Cursor.visible, "GP03 AcceptQuest completes cleanly with unlocked cursor");

                    // GP04: Second Wind rejects full health cleanly; Whirlwind spends 20 Rage cleanly
                    pc.CastAbility3Public();
                    Assert(pc.Message.Contains("full"), $"GP04 Full-HP Second Wind blocked cleanly ('{pc.Message}')");
                    float rageBefore = pc.ClassResource;
                    pc.CastAbility2Public();
                    Assert(pc.ClassResource <= rageBefore - 20f + 0.01f && pc.CleaveReady > Time.time, "GP04 Warrior Whirlwind consumes 20 Rage and starts cooldown");

                    // GP08: SettingsSystem SaveSettings / LoadSettings PlayerPrefs round-trip (restores prior prefs cleanly)
                    float oldVol = PlayerPrefs.GetFloat("Riftbound.Volume", 0.8f);
                    float oldSfx = PlayerPrefs.GetFloat("Riftbound.SfxVolume", 0.85f);
                    var ss = SettingsSystem.Instance;
                    Assert(ss != null, "GP08 SettingsSystem instance active");
                    ss.masterVolume = 0.42f;
                    ss.sfxVolume = 0.33f;
                    ss.SaveSettings();
                    ss.masterVolume = 1f;
                    ss.sfxVolume = 1f;
                    ss.LoadSettings();
                    Assert(Mathf.Abs(ss.masterVolume - 0.42f) < 0.01f && Mathf.Abs(AudioListener.volume - 0.42f) < 0.01f && Mathf.Abs(ss.sfxVolume - 0.33f) < 0.01f, "GP08 Audio settings persist and apply to AudioListener");
                    PlayerPrefs.SetFloat("Riftbound.Volume", oldVol);
                    PlayerPrefs.SetFloat("Riftbound.SfxVolume", oldSfx);
                    PlayerPrefs.Save();
                    ss.LoadSettings();

                    // GP05: MinimapRadar Main Quest tracking
                    Assert(MinimapRadar.Instance != null, "GP05 MinimapRadar active");
                    MinimapRadar.Instance.TrackMainQuest();
                    Assert(MinimapRadar.Instance.TrackingMainQuest && MinimapRadar.Instance.customWaypoint.HasValue, "GP05 TrackMainQuest sets active objective waypoint");
                    MinimapRadar.ClearWaypoint();

                    // GP09: PartySystem opt-in companion spawn & dismiss
                    Assert(PartySystem.Instance != null && PartySystem.Instance.Members.Count == 0, "GP09 PartySystem starts empty until opt-in");
                    PartySystem.Instance.SeedInitialParty(false);
                    bool compOk = PartySystem.Instance.Members.Count == 1 && PartySystem.Instance.Members[0].companionObject != null && PartySystem.Instance.Members[0].companionObject.GetComponentInChildren<Animator>() != null;
                    Assert(compOk, "GP09 Opt-in companion spawns real visual follower");
                    PartySystem.Instance.KickMember(PartySystem.Instance.Members[0].name);
                    Assert(PartySystem.Instance.Members.Count == 0, "GP09 KickMember cleanly removes companion");

                    // GP10: Class abilities, non-doubled scaling, dead-corpse guard, and Paladin Consecration debuff
                    var liveGo = new GameObject("Gp10TempLiveEnemy");
                    liveGo.transform.position = pc.transform.position + pc.transform.forward * 2.5f;
                    var liveCbt = liveGo.AddComponent<Combatant>();
                    liveCbt.displayName = "TestDummy";
                    liveCbt.ConfigureVitals(500f, 0f);
                    liveCbt.Restore();
                    var liveCtrl = liveGo.AddComponent<EnemyController>();
                    liveCtrl.trainingDummy = true;

                    var deadGo = new GameObject("Gp10TempDeadEnemy");
                    deadGo.transform.position = pc.transform.position + pc.transform.forward * 2.5f;
                    var deadCbt = deadGo.AddComponent<Combatant>();
                    deadCbt.displayName = "DeadDummy";
                    deadCbt.ConfigureVitals(100f, 0f);
                    deadCbt.Restore();
                    deadCbt.Damage(9999f);
                    deadGo.AddComponent<EnemyController>();

                    string origClass = pc.CharacterClass;
                    var resProp = typeof(PlayerController).GetProperty("ClassResource");
                    var cleaveProp = typeof(PlayerController).GetProperty("CleaveReady");
                    var atkReadyField = typeof(PlayerController).GetField("attackReady", BindingFlags.NonPublic | BindingFlags.Instance);

                    pc.CharacterClass = "Paladin";
                    resProp.SetValue(pc, 5f);
                    cleaveProp.SetValue(pc, 0f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility2Public();
                    float consDmg = 500f - liveCbt.Health;
                    Assert(BuffSystem.HasTargetDebuff(liveCbt, "consecration") && !BuffSystem.HasTargetDebuff(liveCbt, "frostbite"), "GP10 Paladin Consecration applies Holy Consecrated debuff (not Frostbite)");
                    Assert(!BuffSystem.HasTargetDebuff(deadCbt, "consecration"), "GP10 Dead enemy ignored by Consecration AOE");
                    Assert(consDmg >= 35f && consDmg <= 85f, $"GP10 Consecration avoids double Weapon/Level scaling (dmg={consDmg:F1})");

                    pc.CharacterClass = "Mage";
                    resProp.SetValue(pc, 100f);
                    cleaveProp.SetValue(pc, 0f);
                    atkReadyField.SetValue(pc, 0f);
                    pc.CastAbility2Public();
                    Assert(BuffSystem.HasTargetDebuff(liveCbt, "frostbite") && !BuffSystem.HasTargetDebuff(deadCbt, "frostbite"), "GP10 Mage Frost Nova hits live foe and skips dead foe");

                    var targetProp = typeof(PlayerController).GetProperty("Target");
                    pc.CharacterClass = "Rogue";
                    resProp.SetValue(pc, 100f);
                    cleaveProp.SetValue(pc, 0f);
                    atkReadyField.SetValue(pc, 0f);
                    targetProp.SetValue(pc, liveCbt);
                    float hpPreEvis = liveCbt.Health;
                    pc.CastAbility2Public();
                    Assert(pc.ClassResource <= 65.01f && liveCbt.Health < hpPreEvis, "GP10 Rogue Eviscerate consumes 35 Energy and damages target");

                    pc.CharacterClass = origClass;
                    targetProp.SetValue(pc, null);
                    UnityEngine.Object.Destroy(liveGo);
                    UnityEngine.Object.Destroy(deadGo);

                    // GP11: Shop purchase & Leather Vest equip
                    var merchant = UnityEngine.Object.FindFirstObjectByType<Merchant>();
                    Assert(merchant != null, "GP11 Scene Merchant present");
                    Vector3 savedPos = pc.transform.position;
                    var cc11 = pc.GetComponent<CharacterController>();
                    if (cc11) cc11.enabled = false;
                    pc.transform.position = merchant.transform.position + new Vector3(0f, 0f, -1.8f);
                    if (cc11) cc11.enabled = true;
                    bool gotMerch = pc.TryGetBestInteraction(out var merchCand, out _);
                    Assert(gotMerch && merchCand.text.Contains("Trade with"), $"GP11 Merchant prompt resolved ('{merchCand.text}')");
                    merchCand.execute?.Invoke();
                    pc.AddGoldDirect(50);
                    int potBefore = pc.Potions;
                    pc.BuyPotion();
                    pc.BuyVest();
                    pc.CloseShop();
                    pc.InventoryOpen = true;
                    pc.EquipItem("leather-vest");
                    pc.InventoryOpen = false;
                    Assert(pc.Potions == Mathf.Min(10, potBefore + 1) && pc.ArmorRating >= 2, $"GP11 Merchant buy + equip succeeded (pot={pc.Potions}, armor={pc.ArmorRating})");
                    if (cc11) cc11.enabled = false;
                    pc.transform.position = savedPos;
                    if (cc11) cc11.enabled = true;

                    // GP06: Boss encounter references & phase/alive cast guards
                    Assert(ApexBossEncounter.Instance != null && CathedralBossEncounter.Instance != null && WorldBossEncounter.Instance != null, "GP06 All 3 boss encounter managers initialized");
                    var cb = CathedralBossEncounter.Instance;
                    Assert(cb.malakar != null && cb.abbotVael != null && ApexBossEncounter.Instance.bossCombatant != null && WorldBossEncounter.Instance.bossCombatant != null, "GP06 Vermithrax, Malakar, Vael & Gorgonax scene refs bound");
                    typeof(CathedralBossEncounter).GetProperty("EncounterPhase").SetValue(cb, 2);
                    typeof(CathedralBossEncounter).GetMethod("CastMalakarHolyWrath", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(cb, null);
                    Assert(!CastBarSystem.IsTargetCasting(cb.malakar.GetComponent<Combatant>()), "GP06 Dead/wrong-phase boss cast safely blocked");
                    typeof(CathedralBossEncounter).GetProperty("EncounterPhase").SetValue(cb, 1);

                    // GP12: LivingWorldEvents Caravan trigger
                    Assert(LivingWorldEvents.Instance != null, "GP12 LivingWorldEvents active");
                    LivingWorldEvents.Instance.TriggerCaravanEvent();
                    Assert(LivingWorldEvents.Instance.CaravanState == LivingWorldEvents.EventState.Active && GameObject.Find("WorldEvent_SupplyCaravan") != null, "GP12 Supply Caravan event spawns cleanly");

                    FinishPlay(false);
                }
            }
            catch (Exception ex)
            {
                PlayLog.Add("EXCEPTION: " + ex);
                FinishPlay(true);
            }
        }

        public static void RunAudit()
        {
            var sb = new StringBuilder();
            try
            {
                var scene = EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Expanded.unity", OpenSceneMode.Single);
                sb.AppendLine($"SCENE: {scene.path}");

                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (player)
                {
                    sb.AppendLine("\n=== SCENE PLAYER ===");
                    InspectActor(sb, "ScenePlayer", player.gameObject, player.animator);
                }

                sb.AppendLine("\n=== RESOURCES CHARACTERS PREFABS ===");
                string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/RPG/Resources/Characters" });
                Array.Sort(guids);
                foreach (string g in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(g);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (!prefab) continue;
                    var wrapper = new GameObject("Wrap_" + prefab.name) { hideFlags = HideFlags.HideAndDontSave };
                    wrapper.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    inst.hideFlags = HideFlags.HideAndDontSave;
                    inst.transform.SetParent(wrapper.transform, false);
                    inst.transform.localPosition = Vector3.zero;
                    inst.transform.localRotation = prefab.transform.localRotation;
                    var anim = inst.GetComponentInChildren<Animator>();
                    InspectActor(sb, Path.GetFileNameWithoutExtension(path), wrapper, anim);
                    UnityEngine.Object.DestroyImmediate(inst);
                    UnityEngine.Object.DestroyImmediate(wrapper);
                }

                sb.AppendLine("\n=== ALL SCENE ENEMIES SUMMARY ===");
                var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var e in enemies.OrderBy(x => x.NetworkKey))
                {
                    var anim = e.animator ? e.animator : e.GetComponentInChildren<Animator>(true);
                    string ctrlName = anim && anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "NONE";
                    string animName = anim ? anim.gameObject.name : "NO_ANIM";
                    string euler = anim ? anim.transform.localEulerAngles.ToString("F0") : "NA";
                    string facing = MeasureAnatomicalFacing(e.transform, anim);
                    sb.AppendLine($"E[{e.NetworkKey}] go='{e.gameObject.name}' rootY={e.transform.eulerAngles.y:F0} anim='{animName}' animLocalE={euler} ctrl='{ctrlName}' human={(anim && anim.isHuman)} strides=({e.walkStrideSpeed:F2},{e.runStrideSpeed:F2}) | {facing}");
                }

                string outPath = Path.Combine(Path.GetTempPath(), "gp01_audit2.txt");
                File.WriteAllText(outPath, sb.ToString());
                Debug.Log("GP01_AUDIT2_OK: " + outPath);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                string outPath = Path.Combine(Path.GetTempPath(), "gp01_audit2.txt");
                File.WriteAllText(outPath, sb + "\nERROR:\n" + ex);
                Debug.LogException(ex);
                EditorApplication.Exit(1);
            }
        }

        static string MeasureAnatomicalFacing(Transform root, Animator anim)
        {
            if (!anim) return "noAnim";
            anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            anim.Rebind();
            anim.Update(0f);
            anim.Play("Idle", 0, 0f);
            anim.Update(0f);

            Transform lt = null, lf = null, rt = null, rf = null, head = null, hips = null, lArm = null, rArm = null;
            if (anim.isHuman)
            {
                lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                lt = anim.GetBoneTransform(HumanBodyBones.LeftToes);
                rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                rt = anim.GetBoneTransform(HumanBodyBones.RightToes);
                head = anim.GetBoneTransform(HumanBodyBones.Head);
                hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            else
            {
                foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (lt == null && (n.Contains("toes.l") || n.Contains("lefttoe"))) lt = t;
                    if (lf == null && (n.Contains("foot.l") || n.Contains("leftfoot"))) lf = t;
                    if (rt == null && (n.Contains("toes.r") || n.Contains("righttoe"))) rt = t;
                    if (rf == null && (n.Contains("foot.r") || n.Contains("rightfoot"))) rf = t;
                    if (head == null && n == "head") head = t;
                    if (hips == null && (n == "hips" || n == "pelvis" || n == "root")) hips = t;
                    if (lArm == null && (n == "upperarm.l" || n.Contains("leftshoulder") || n.Contains("leftarm"))) lArm = t;
                    if (rArm == null && (n == "upperarm.r" || n.Contains("rightshoulder") || n.Contains("rightarm"))) rArm = t;
                }
            }

            Vector3 invRight = lArm && rArm ? (rArm.position - lArm.position) : Vector3.zero;
            invRight.y = 0f;
            Vector3 torsoFwd = invRight.sqrMagnitude > 0.0001f ? Vector3.Cross(invRight.normalized, Vector3.up) : Vector3.zero; // Right x Up = Back? Wait: Left-handed Unity: right(X) x up(Y) = forward(+Z)? Wait: X=(1,0,0), Y=(0,1,0) -> Cross(X,Y)=(0,0,1)=Forward!
            Vector3 invLeftFwd = invRight.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, invRight.normalized) : Vector3.zero; // Wait: Cross(Up, Right) in Unity left-handed: Cross((0,1,0),(1,0,0)) = (0,0,-1)! Cross((1,0,0),(0,1,0)) = (0,0,1) = Forward! Wait let's double check empirically!
            Vector3 lFootVec = (lt && lf) ? (lt.position - lf.position) : Vector3.zero; lFootVec.y = 0f;
            Vector3 rFootVec = (rt && rf) ? (rt.position - rf.position) : Vector3.zero; rFootVec.y = 0f;
            Vector3 avgFoot = lFootVec + rFootVec;
            float footDot = avgFoot.sqrMagnitude > 0.0001f ? Vector3.Dot(root.forward, avgFoot.normalized) : float.NaN;
            float crossDot = torsoFwd.sqrMagnitude > 0.0001f ? Vector3.Dot(root.forward, torsoFwd.normalized) : float.NaN;
            return $"footFwdDot={footDot:F2} shoulderCrossDot={crossDot:F2}";
        }

        static void InspectActor(StringBuilder sb, string label, GameObject actorRoot, Animator anim)
        {
            if (!anim) { sb.AppendLine($"{label}: NO ANIM"); return; }
            string idleFace = MeasureAnatomicalFacing(actorRoot.transform, anim);
            string walkFace = "", runFace = "";
            if (anim.runtimeAnimatorController is AnimatorController ac && ac.layers.Length > 0)
            {
                var states = ac.layers[0].stateMachine.states;
                if (states.Any(s => s.state.name == "Walk"))
                {
                    anim.Play("Walk", 0, 0.25f); anim.Update(0f);
                    walkFace = SampleBonesLocal(actorRoot.transform, anim, "Walk");
                }
                if (states.Any(s => s.state.name == "Run"))
                {
                    anim.Play("Run", 0, 0.25f); anim.Update(0f);
                    runFace = SampleBonesLocal(actorRoot.transform, anim, "Run");
                }
            }
            sb.AppendLine($"{label}: animE={anim.transform.localEulerAngles:F0} ctrl='{(anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "NONE")}' | idle=[{idleFace}] | walk=[{walkFace}] | run=[{runFace}]");
        }

        static string SampleBonesLocal(Transform root, Animator anim, string st)
        {
            Transform lt = null, lf = null, rt = null, rf = null, lArm = null, rArm = null;
            if (anim.isHuman)
            {
                lf = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                lt = anim.GetBoneTransform(HumanBodyBones.LeftToes);
                rf = anim.GetBoneTransform(HumanBodyBones.RightFoot);
                rt = anim.GetBoneTransform(HumanBodyBones.RightToes);
                lArm = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rArm = anim.GetBoneTransform(HumanBodyBones.RightUpperArm);
            }
            else
            {
                foreach (var t in anim.GetComponentsInChildren<Transform>(true))
                {
                    string n = t.name.ToLowerInvariant();
                    if (lt == null && (n.Contains("toes.l") || n.Contains("lefttoe"))) lt = t;
                    if (lf == null && (n.Contains("foot.l") || n.Contains("leftfoot"))) lf = t;
                    if (rt == null && (n.Contains("toes.r") || n.Contains("righttoe"))) rt = t;
                    if (rf == null && (n.Contains("foot.r") || n.Contains("rightfoot"))) rf = t;
                    if (lArm == null && (n == "upperarm.l" || n.Contains("leftshoulder") || n.Contains("leftarm"))) lArm = t;
                    if (rArm == null && (n == "upperarm.r" || n.Contains("rightshoulder") || n.Contains("rightarm"))) rArm = t;
                }
            }
            anim.Play(st, 0, 0f); anim.Update(0f);
            float len = anim.GetCurrentAnimatorStateInfo(0).length;
            if (len <= 0.01f) return "0len";
            const int N = 24;
            Vector3[,] pos = new Vector3[2, N + 1];
            float minY = float.MaxValue, maxY = float.MinValue;
            float shDotSum = 0f;
            int shCount = 0;
            for (int i = 0; i <= N; i++)
            {
                anim.Play(st, 0, (float)i / N); anim.Update(0f);
                Vector3 lp = (lt ?? lf) ? (lt ?? lf).position : Vector3.zero;
                Vector3 rp = (rt ?? rf) ? (rt ?? rf).position : Vector3.zero;
                pos[0, i] = lp; pos[1, i] = rp;
                minY = Mathf.Min(minY, lp.y, rp.y);
                maxY = Mathf.Max(maxY, lp.y, rp.y);
                if (lArm && rArm)
                {
                    Vector3 sh = rArm.position - lArm.position; sh.y = 0f;
                    if (sh.sqrMagnitude > 0.0001f)
                    {
                        Vector3 tf = Vector3.Cross(sh.normalized, Vector3.up);
                        shDotSum += Vector3.Dot(root.forward, tf.normalized);
                        shCount++;
                    }
                }
            }
            float lowCut = minY + (maxY - minY) * 0.25f + 0.01f;
            float dt = len / N;
            float sumZ = 0f, sumX = 0f; int cnt = 0;
            for (int f = 0; f < 2; f++)
            {
                for (int i = 1; i <= N; i++)
                {
                    if (pos[f, i].y <= lowCut)
                    {
                        Vector3 v = root.InverseTransformDirection((pos[f, i - 1] - pos[f, i]) / dt);
                        v.y = 0f;
                        if (v.magnitude > 0.2f && v.magnitude < 15f) { sumZ += v.z; sumX += v.x; cnt++; }
                    }
                }
            }
            float mz = cnt > 0 ? sumZ / cnt : 0f;
            float mx = cnt > 0 ? sumX / cnt : 0f;
            float avgSh = shCount > 0 ? shDotSum / shCount : float.NaN;
            return $"shDot={avgSh:F2} footVel=(X={mx:F2},Z={mz:F2},n={cnt})";
        }
    }
}
