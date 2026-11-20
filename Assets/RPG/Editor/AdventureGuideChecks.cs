using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class AdventureGuideChecks
    {
        const string SessionKey = "Riftbound.AdventureGuideChecks";
        const string Output = ".code-check/adventure-guide/adventure-guide-checks.txt";
        static readonly List<string> Lines = new List<string>();
        static readonly List<string> Errors = new List<string>();
        static readonly BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        static int stage, passed;
        static double due, timeout;
        static PrototypeHUD hud;
        static PlayerController player;

        static AdventureGuideChecks()
        {
            EditorApplication.playModeStateChanged += OnPlayStateChanged;
        }

        static string SavePath => Path.GetFullPath(".code-check/adventure-guide/save-fixture.json");

        [MenuItem("Riftbound/Run Adventure Guide Checks")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start AdventureGuideChecks outside Play Mode.");
            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            if (File.Exists(SavePath)) File.Delete(SavePath);

            SessionState.SetBool(SessionKey + ".DirectPlay", EditorPrefs.GetBool("Riftbound.DirectPlayInEditor", false));
            EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", true);
            SessionState.SetBool(SessionKey, true);
            SessionState.SetBool(SessionKey + ".Failed", false);
            UIArtSetup.Configure();
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
                Lines.Add("Adventure Guide PlayMode verification in Outpost-Expanded (isolated save fixture; NOT manual human play).");
                stage = 0;
                passed = 0;
                due = EditorApplication.timeSinceStartup + 1.0;
                timeout = due + 60.0;
                Application.logMessageReceived += OnLog;
                EditorApplication.update += Tick;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.update -= Tick;
                Application.logMessageReceived -= OnLog;
                Network.CoopSessionConfig.ResetToOffline();
                SessionState.SetBool(SessionKey, false);
                EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", SessionState.GetBool(SessionKey + ".DirectPlay", false));
                if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool(SessionKey + ".Failed", true) ? 1 : 0);
            }
        }

        static void OnLog(string msg, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) &&
                (stack.Contains("Riftbound") || msg.Contains("QuestJournal") || msg.Contains("AdventureGuide")))
                Errors.Add($"{type}: {msg}\n{stack}");
        }

        static void Assert(bool cond, string label, string detail = "")
        {
            string line = (cond ? "PASS " : "FAIL ") + label + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail);
            Lines.Add(line);
            if (!cond) throw new Exception(line);
            passed++;
        }

        static void Teleport(Vector3 pos)
        {
            var motor = player.GetComponent<CharacterController>();
            if (motor) motor.enabled = false;
            player.transform.position = pos;
            if (motor) motor.enabled = true;
        }

        static void OpenAbe()
        {
            Teleport(player.questGiver.position - Vector3.forward * 1.2f);
            typeof(PlayerController).GetMethod("Interact", Hidden).Invoke(player, null);
            Assert(player.DialogueOpen && player.ActiveQuestGiver, "Abe dialogue opened via Interact");
        }

        static void Tick()
        {
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (now > timeout) throw new TimeoutException("AdventureGuideChecks timed out at stage " + stage);
                if (EditorApplication.isPlaying && EditorApplication.isPaused) EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (!EditorApplication.isPlaying || now < due) return;

                if (stage == 0)
                {
                    player = Object.FindAnyObjectByType<PlayerController>();
                    hud = Object.FindAnyObjectByType<PrototypeHUD>();
                    Assert(player && player.Stats && hud && MinimapRadar.Instance, "Player, HUD, and MinimapRadar initialized");
                    WesternRoadCamps.Ensure(player);
                    BrassHollows.Ensure();

                    // 1. Fresh character checks
                    var entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries.Count == 10, "Journal lists all 10 world chapters", $"count={entries.Count}");
                    Assert(entries[0].status == JournalEntryStatus.Available &&
                           entries[1].status == JournalEntryStatus.Unavailable &&
                           entries[2].status == JournalEntryStatus.Unavailable,
                        "Fresh character shows Ch.1 Available and Ch.2-3 Unavailable",
                        $"statuses={entries[0].status}/{entries[1].status}/{entries[2].status}");
                    int goal = AdventureGuideCatalog.GetCurrentGoalChapterIndex(player, entries);
                    Assert(goal == 0, "Overview Current Goal points to Ch.1 on fresh profile", $"goal={goal}");
                    var side = AdventureGuideCatalog.GetRecommendedSideIndices(entries, goal);
                    Assert(side.Count > 0 && side.Count <= 2 && side.All(idx => entries[idx].status != JournalEntryStatus.Completed && entries[idx].status != JournalEntryStatus.Unavailable),
                        "Overview recommends up to 2 feasible side routes", $"sideCount={side.Count}");

                    foreach (var e in entries)
                    {
                        string combined = (e.summary ?? "") + " " + (e.objective ?? "");
                        Assert(!combined.Contains("(-55, 215)") && !combined.Contains("(55, 215)") && !combined.Contains("(0, 48, 225)"),
                            $"Chapter '{e.title}' omits legacy coordinates");
                    }

                    // 2. Abe Ch.1 Active -> ReadyToTurnIn -> Completed & Ch.2 Available
                    OpenAbe();
                    player.AcceptQuest();
                    player.CloseDialogue();
                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries[0].status == JournalEntryStatus.Active && entries[0].objective == player.QuestObjective,
                        "Accepting Ch.1 transitions entry to Active with live QuestObjective");

                    var raiders = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude)
                        .Where(e => e.IsRoadRaider && e.GetComponent<Combatant>().Alive).Take(3).ToList();
                    Assert(raiders.Count == 3, "Found 3 live road raiders for Ch.1 progression");
                    foreach (var r in raiders) r.GetComponent<Combatant>().Damage(100000f);

                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries[0].status == JournalEntryStatus.ReadyToTurnIn,
                        "Defeating 3 raiders marks Ch.1 ReadyToTurnIn", entries[0].objective);

                    OpenAbe();
                    player.CompleteQuest();
                    player.CloseDialogue();
                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries[0].status == JournalEntryStatus.Completed && entries[1].status == JournalEntryStatus.Available &&
                           AdventureGuideCatalog.GetCurrentGoalChapterIndex(player, entries) == 1,
                        "Turning in Ch.1 marks it Completed and advances Current Goal to Ch.2");

                    // 3. Complete Ch.2 to unlock Western Road
                    OpenAbe();
                    player.AcceptQuest();
                    player.CloseDialogue();
                    foreach (var crate in Object.FindObjectsByType<SupplyCrate>().Take(3))
                    {
                        Teleport(crate.transform.position);
                        crate.Collect(player);
                    }
                    OpenAbe();
                    player.CompleteQuest();
                    player.CloseDialogue();

                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(player.QuestIndex == 2 && entries[8].status == JournalEntryStatus.Available,
                        "Completing Ch.2 unlocks Western Road entry (Available)");

                    // 4. Western Road live progression + Brass Hollows live progression
                    var rowan = WesternRoadCamps.FindRowan();
                    Assert(rowan != null, "Quartermaster Rowan located");
                    Teleport(rowan.transform.position);
                    player.GetType().GetProperty("ActiveMerchant").SetValue(player, rowan);
                    player.HandleWesternRoadAtRowan();
                    player.CloseShop();

                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries[8].status == JournalEntryStatus.Active && entries[8].objective == player.WesternRoadObjective,
                        "Western Road shows Active with live WesternRoadObjective");

                    var fizz = Object.FindObjectsByType<TownNPC>().FirstOrDefault(n => n.npcName == BrassHollows.Engineer);
                    Assert(fizz != null, "Tinker Fizzlebang located");
                    Teleport(fizz.transform.position - Vector3.forward * 1.2f);
                    player.OpenTownNPCDialogue(fizz);
                    player.HandleBrassHollowsQuest();
                    player.CloseDialogue();
                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries[9].status == JournalEntryStatus.Active && entries[9].objective == player.BrassHollowsObjective,
                        "Brass Hollows shows Active with live BrassHollowsObjective");

                    player.ShutDownBrassSiphon();
                    entries = AdventureGuideCatalog.BuildJournalEntries(player);
                    Assert(entries[9].status == JournalEntryStatus.ReadyToTurnIn,
                        "Shutting down siphon marks Brass Hollows ReadyToTurnIn");

                    // 5. Explore cards + conditional SettlementWaystop anchors + conditional Brass Hollows hint
                    var cards = AdventureGuideCatalog.BuildExploreCards(player);
                    Assert(cards.Count == 4, "Explore view contains 4 route cards");
                    foreach (var c in cards)
                    {
                        string all = $"{c.purpose} {c.howToStart} {c.namedDestination} {c.practicalHint}";
                        Assert(!all.Contains("(-55, 215)") && !all.Contains("(55, 215)") && !all.Contains("(0, 48, 225)"),
                            $"Explore card '{c.id}' omits legacy coordinates");
                        Assert(!string.IsNullOrEmpty(c.purpose) && !string.IsNullOrEmpty(c.howToStart) &&
                               !string.IsNullOrEmpty(c.namedDestination) && !string.IsNullOrEmpty(c.practicalHint),
                            $"Explore card '{c.id}' has purpose/start/destination/hint");
                    }
                    var north0 = cards.First(c => c.id == "north-road");
                    var caves0 = cards.First(c => c.id == "brass-hollows");
                    var summit0 = cards.First(c => c.id == "wyrmcrest-climb");
                    Assert(!north0.liveStops.Any(s => s.label == "Three-Chimes Relay") &&
                           !caves0.liveStops.Any(s => s.label == "Coilbreak Yard") &&
                           !summit0.liveStops.Any(s => s.label == "Gor'Kresh Trailhead Shelter"),
                        "Optional preview stops are omitted when SettlementWaystop sites are absent");
                    Assert(caves0.practicalHint.Contains("recovery window") && caves0.practicalHint.Contains("red warning ring"),
                        "Brass Hollows hint includes steam/Veyl tactical note once BrassHollowsStage >= 1");

                    // Add temporary SettlementWaystop anchors -> verify dynamic resolution -> destroy
                    var tempStops = new List<GameObject>();
                    foreach (var pair in new[]
                    {
                        ("Three-Chimes Relay", new Vector3(-1f, 15f, 300f)),
                        ("Coilbreak Yard", new Vector3(207f, 15f, 187f)),
                        ("Gor'Kresh Trailhead Shelter", new Vector3(367f, 25f, 314f))
                    })
                    {
                        var go = new GameObject("Temp_" + pair.Item1);
                        go.transform.position = pair.Item2;
                        var ws = go.AddComponent<SettlementWaystop>();
                        ws.displayName = pair.Item1;
                        tempStops.Add(go);
                    }
                    var cardsWithStops = AdventureGuideCatalog.BuildExploreCards(player);
                    Assert(cardsWithStops.First(c => c.id == "north-road").liveStops.Any(s => s.label == "Three-Chimes Relay") &&
                           cardsWithStops.First(c => c.id == "brass-hollows").liveStops.Any(s => s.label == "Coilbreak Yard") &&
                           cardsWithStops.First(c => c.id == "wyrmcrest-climb").liveStops.Any(s => s.label == "Gor'Kresh Trailhead Shelter"),
                        "Explore cards dynamically include live SettlementWaystop anchors when present");
                    foreach (var go in tempStops) Object.DestroyImmediate(go);

                    // 6. Co-op boundary check
                    try
                    {
                        Network.CoopSessionConfig.Mode = Network.CoopSessionMode.HostOnline;
                        var coopEntries = AdventureGuideCatalog.BuildJournalEntries(player);
                        var coopSide = AdventureGuideCatalog.GetRecommendedSideIndices(coopEntries, 0);
                        var coopCards = AdventureGuideCatalog.BuildExploreCards(player);
                        Assert(coopEntries[8].status == JournalEntryStatus.Unavailable &&
                               coopEntries[9].status == JournalEntryStatus.Unavailable &&
                               !coopSide.Contains(8) && !coopSide.Contains(9),
                            "Co-op mode marks Western Road and Brass Hollows Unavailable and excludes them from recommendations");
                        Assert(coopCards.First(c => c.id == "western-road").questUnavailableInCoop &&
                               coopCards.First(c => c.id == "brass-hollows").questUnavailableInCoop,
                            "Co-op mode flags solo Explore quest routes accurately");
                    }
                    finally
                    {
                        Network.CoopSessionConfig.ResetToOffline();
                    }

                    // 7. Manual Pin preservation + Journal/Map/Encounter toggling across OnGUI frames
                    MinimapRadar.SetWaypoint(new Vector3(12f, 0f, 34f), "Manual Test Pin");
                    Assert(MinimapRadar.Instance.customWaypoint.HasValue, "Manual map pin set cleanly");
                    player.JournalOpen = true;
                    typeof(PrototypeHUD).GetField("guideTab", Hidden).SetValue(hud, AdventureGuideTab.Overview);
                    stage = 1;
                    due = now + 0.35;
                }
                else if (stage == 1)
                {
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        ScreenCapture.CaptureScreenshot(".code-check/adventure-guide/guide-overview.png");
                    stage = 2;
                    due = now + 0.25;
                }
                else if (stage == 2)
                {
                    typeof(PrototypeHUD).GetField("guideTab", Hidden).SetValue(hud, AdventureGuideTab.Journal);
                    stage = 3;
                    due = now + 0.35;
                }
                else if (stage == 3)
                {
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        ScreenCapture.CaptureScreenshot(".code-check/adventure-guide/guide-journal.png");
                    stage = 4;
                    due = now + 0.25;
                }
                else if (stage == 4)
                {
                    typeof(PrototypeHUD).GetField("guideTab", Hidden).SetValue(hud, AdventureGuideTab.Explore);
                    stage = 5;
                    due = now + 0.35;
                }
                else if (stage == 5)
                {
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                        ScreenCapture.CaptureScreenshot(".code-check/adventure-guide/guide-explore.png");
                    stage = 6;
                    due = now + 0.25;
                }
                else if (stage == 6)
                {
                    Assert(MinimapRadar.Instance.customWaypoint.HasValue,
                        "Opening J and cycling Overview/Journal/Explore preserves existing manual map pin");

                    MinimapRadar.Instance.TrackMainQuest();
                    Assert(!MinimapRadar.Instance.customWaypoint.HasValue && MinimapRadar.Instance.TrackingMainQuest,
                        "Explicit TrackMainQuest replaces manual pin and tracks main quest");

                    player.JournalOpen = false;
                    player.MapOpen = true;
                    Assert(player.MapOpen && !player.JournalOpen, "World Map screen reachable from Adventure Guide");
                    player.MapOpen = false;
                    player.EncounterJournalOpen = true;
                    Assert(player.EncounterJournalOpen && !player.JournalOpen, "Encounter Journal reachable from Adventure Guide");
                    player.EncounterJournalOpen = false;

                    Assert(Errors.Count == 0, "Zero Riftbound/AdventureGuide runtime errors", $"errors={Errors.Count}");
                    Lines.Add($"TOTAL PASS={passed} ERRORS={Errors.Count} RESULT=PASS");
                    File.WriteAllLines(Output, Lines);
                    SessionState.SetBool(SessionKey + ".Failed", false);
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception ex)
            {
                Lines.Add("FAIL EXCEPTION " + ex.Message);
                foreach (var e in Errors) Lines.Add("ERROR " + e);
                File.WriteAllLines(Output, Lines);
                SessionState.SetBool(SessionKey + ".Failed", true);
                EditorApplication.isPlaying = false;
            }
        }
    }
}
