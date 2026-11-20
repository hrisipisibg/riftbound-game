using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
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
    public static class QuestRouteSmokeTest
    {
        const string Running = "Riftbound.QuestRouteSmokeTest";
        static readonly List<string> Results = new List<string>();
        static readonly List<string> Errors = new List<string>();
        static double due, timeout;
        static int stage, frontPhase;
        static Keyboard keyboard;
        static Vector3 start;

        static QuestRouteSmokeTest()
        {
            EditorApplication.playModeStateChanged += Changed;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run this check in an isolated Unity batch process.");
            Directory.CreateDirectory(".code-check");
            UIArtSetup.Configure();
            UIArtSetup.PrepareGameView();
            SessionState.SetBool(Running + ".DirectPlay", EditorPrefs.GetBool("Riftbound.DirectPlayInEditor", false));
            EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", false);
            SessionState.SetBool(Running, true);
            SessionState.SetBool(Running + ".Failed", false);
            SessionState.SetString(Running + ".SavePath", Path.GetFullPath(".code-check/quest-route-save-" + Guid.NewGuid().ToString("N") + ".json"));
            Application.runInBackground = true;
            PlayerSettings.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorSceneManager.OpenScene(File.Exists(WorldRegions.ScenePath) ? WorldRegions.ScenePath : "Assets/RPG/Scenes/Outpost-Updated.unity");
            EditorApplication.isPlaying = true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void IsolateSaves()
        {
            if (SessionState.GetBool(Running, false))
            {
                Application.runInBackground = true;
                PlayerController.SavePathOverride = SessionState.GetString(Running + ".SavePath", "");
            }
        }

        static void Changed(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(Running, false)) return;
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                due = EditorApplication.timeSinceStartup + 3;
                timeout = due + 90;
                stage = 0;
                frontPhase = 0;
                Application.logMessageReceived += Log;
                EditorApplication.update += Tick;
            }
            if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(Running, false);
                EditorPrefs.SetBool("Riftbound.DirectPlayInEditor", SessionState.GetBool(Running + ".DirectPlay", false));
                EditorApplication.Exit(SessionState.GetBool(Running + ".Failed", false) ? 1 : 0);
            }
        }

        static void Log(string message, string stack, LogType type)
        {
            if ((type == LogType.Exception || type == LogType.Error) && stack.Contains("Riftbound"))
                Errors.Add(message + "\n" + stack);
        }

        static void Check(bool passed, string label)
        {
            Results.Add((passed ? "PASS " : "FAIL ") + label);
            if (!passed) throw new Exception(label);
        }

        static PlayerController Player()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            Check(player && player.Stats && player.animator, "Player, health and animator survive startup");
            Check(player.orbit && player.orbit.target == player.transform, "Camera follows the player");
            var hud = Object.FindFirstObjectByType<PrototypeHUD>();
            Check(hud && hud.player == player, "HUD references the live player");
            return player;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new TimeoutException("Play Mode check timed out");
                if (EditorApplication.isPlaying && EditorApplication.isPaused)
                    EditorApplication.isPaused = false;
                EditorApplication.QueuePlayerLoopUpdate();
                if (EditorApplication.timeSinceStartup < due || !EditorApplication.isPlaying) return;
                if (stage == 0)
                {
                    var player = Player();
                    Results.Add($"Initial frame={Time.frameCount}, background={Application.runInBackground}, focused={Application.isFocused}, editorPaused={EditorApplication.isPaused}");
                    var menu = player.GetComponent<FrontendMenu>();
                    Check(menu, "Frontend exists");
                    if (frontPhase == 0)
                    {
                        Check(menu.TitleScreenOpen, "Startup opens the title screen");
                        var atlas = Resources.Load<Texture2D>("UI/AbilityIcons");
                        Check(atlas && atlas.width == atlas.height && atlas.width >= 512, "Painted ability atlas is packaged as a square texture");
                        var cells = new HashSet<int>();
                        foreach (string cls in new[] { "Warrior", "Mage", "Rogue", "Paladin" })
                            foreach (string key in new[] { "1", "2", "3" }) cells.Add(ActionBarSystem.IconCell(cls, key));
                        foreach (string key in new[] { "4", "F", "Q", "H" }) cells.Add(ActionBarSystem.IconCell("Warrior", key));
                        Check(cells.Count == 16 && cells.Min() == 0 && cells.Max() == 15, "Class and shared actions map to sixteen distinct icon cells");
                        CaptureUI("Title"); frontPhase = 1; due = EditorApplication.timeSinceStartup + 1; return;
                    }
                    if (frontPhase == 1)
                    {
                        menu.OpenCharacterSelect();
                        Check(!menu.TitleScreenOpen && FrontendMenu.IsOpen, "Play opens character selection while gameplay stays gated");
                        CaptureUI("Characters"); frontPhase = 2; due = EditorApplication.timeSinceStartup + 1; return;
                    }
                    Check(FrontendMenu.IsOpen || (!player.orbit.Paused && Time.timeScale > 0), "Menu or direct-play controls are available");
                    Results.Add("Initial character selection shown: " + FrontendMenu.IsOpen);
                    PlayerController.SavePathOverride = SessionState.GetString(Running + ".SavePath", "");
                    var entering = typeof(FrontendMenu).GetField("enteringCharacter", BindingFlags.NonPublic | BindingFlags.Static);
                    Check(entering != null, "Character-entry handoff is available");
                    entering.SetValue(null, new FrontendMenu.Character { id = Guid.NewGuid().ToString("N"), name = "BuildCheck", faction = "Alliance", race = "Human", characterClass = "Warrior" });
                    typeof(FrontendMenu).GetProperty("IsOpen").SetValue(null, false);
                    player.orbit.SetPaused(false);
                    SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                    stage = 1;
                    due = EditorApplication.timeSinceStartup + 3;
                }
                else if (stage == 1)
                {
                    var player = Player();
                    if (frontPhase == 2)
                    {
                        CaptureUI("HUD"); frontPhase = 3; due = EditorApplication.timeSinceStartup + 1; return;
                    }
                    if (frontPhase == 3)
                    {
                        player.MapOpen = true;
                        frontPhase = 4;
                        due = EditorApplication.timeSinceStartup + .65;
                        return;
                    }
                    if (frontPhase == 4)
                    {
                        CaptureUI("QuestAtlas"); frontPhase = 5; due = EditorApplication.timeSinceStartup + 1; return;
                    }
                    player.MapOpen = false;
                    Results.Add($"World state: menu={FrontendMenu.IsOpen}, paused={player.orbit.Paused}, timeScale={Time.timeScale}, name={player.CharacterName}, frames={Time.frameCount}");
                    Check(!FrontendMenu.IsOpen && !player.orbit.Paused && Time.timeScale > 0, "Entering the world restores gameplay time and camera control");
                    Check(player.CharacterName == "BuildCheck", "Selected character is applied after scene reload");
                    Check(player.CharacterRace == "Human", "Fresh test character retains its selected race");
                    CheckQuestMapStart(player);
                    if (WorldRegions.Expanded) CheckExpandedWorld(player);
                    Check(player.GetComponentsInChildren<Renderer>().Length > 0, "Character has a visible model");
                    InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                    InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                    Application.runInBackground = true;
                    keyboard = InputSystem.AddDevice<Keyboard>("QuestRouteSmokeTestKeyboard");
                    keyboard.MakeCurrent();
                    start = player.transform.position;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                    stage = 2;
                    due = EditorApplication.timeSinceStartup + 2;
                }
                else if (stage == 2)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    var player = Player();
                    var travel = player.transform.position - start;
                    travel.y = 0;
                    Check(travel.magnitude > .5f, $"W moves the player ({travel.magnitude:F2}m)");
                    Check(Errors.Count == 0, "No RPG runtime exceptions during startup and movement");
                    Time.timeScale = 0;
                    QuestRegression(player);
                    stage = 3;
                    due = EditorApplication.timeSinceStartup + .8;
                }
                else if (stage == 3)
                {
                    var player = Player();
                    var radar = MinimapRadar.Instance;
                    Check(radar.TrackingMainQuest && radar.TryGetNavigationTarget(out var destination, out _) &&
                        Vector3.Distance(destination, player.questGiver.position) < .1f, "Live quest tracking returns to Abe when the objective is ready");
                    Check(RefreshMapAndHas(radar, "abe", MinimapRadar.QuestMark.TurnIn), "Abe's available marker changes to turn-in");
                    TurnIn(player);
                    Check(player.QuestIndex == 1 && !player.QuestAccepted, "Road quest turns in and advances exactly once");
                    OpenAbe(player); player.AcceptQuest();
                    radar.RefreshQuestPins();
                    Check(radar.QuestPins.Count(pin => pin.id.StartsWith("supply:")) == 3, "The map shows all three live supply crates");
                    foreach (var crate in Object.FindObjectsByType<SupplyCrate>(FindObjectsSortMode.None))
                    {
                        Move(player, crate.transform.position);
                        crate.Collect(player);
                    }
                    Check(player.QuestReady && player.Supplies >= 3, "Supply quest collects the three actual crates");
                    TurnIn(player);
                    Check(player.QuestIndex == 2, "Supply hand-in unlocks the captain mission");
                    if (WorldRegions.Expanded)
                        Check(RefreshMapAndHas(radar, "western", MinimapRadar.QuestMark.Available), "Rowan appears when the Western Road quest unlocks");
                    var captain = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(e => e.IsMainQuestCaptain);
                    Check(!captain.gameObject.activeSelf, "Captain remains locked until his quest is accepted");
                    OpenAbe(player); player.AcceptQuest();
                    Check(captain.gameObject.activeSelf, "Accepting the captain quest activates him");
                    captain.GetComponent<Combatant>().Damage(100000);
                    Check(player.QuestReady && player.BossDefeated, "Only the captain completes the captain objective");
                    TurnIn(player);
                    Check(player.QuestComplete && player.QuestIndex == 3 && player.OwnsItem("emberwatch-axe") && player.OwnsItem("emberwatch-sigil"), "Full campaign grants the final equipment");
                    int gold = player.Gold;
                    player.CompleteQuest();
                    Check(player.Gold == gold && player.QuestIndex == 3, "Repeated hand-in cannot duplicate rewards");
                    player.SaveAdventure(false); player.LoadAdventure();
                    Check(player.QuestComplete && player.OwnsItem("emberwatch-sigil"), "Campaign completion survives save/load");
                    Check(Errors.Count == 0, "Quest and loot route produces no RPG runtime exceptions");
                    stage = 4;
                    due = EditorApplication.timeSinceStartup + .8;
                }
                else if (stage == 4)
                {
                    Check(!MinimapRadar.Instance.TrackingMainQuest && !MinimapRadar.Instance.TryGetNavigationTarget(out _, out _),
                        "Quest navigation clears after campaign completion");
                    CheckWesternRoad(Player());
                    Finish(false);
                }
            }
            catch (Exception exception)
            {
                Errors.Add(exception.ToString());
                Finish(true);
            }
        }

        static bool RefreshMapAndHas(MinimapRadar radar, string id, MinimapRadar.QuestMark kind)
        {
            radar.RefreshQuestPins();
            return radar.QuestPins.Any(pin => pin.id == id && pin.kind == kind);
        }

        static void CheckQuestMapStart(PlayerController player)
        {
            var radar = MinimapRadar.Instance;
            Check(radar && radar.showMinimap && radar.showCompass, "Minimap and compass are visible by default");
            Check(RefreshMapAndHas(radar, "abe", MinimapRadar.QuestMark.Available), "The map offers the starting quest at Abe");
            var abe = radar.QuestPins.First(pin => pin.id == "abe");
            radar.SelectQuestPin(abe);
            Check(radar.TryGetNavigationTarget(out var point, out _) && point == player.questGiver.position, "Choosing a quest pin sets a waypoint at its real position");
            MinimapRadar.ClearWaypoint();
            if (WorldRegions.Expanded)
                Check(RefreshMapAndHas(radar, "hollows", MinimapRadar.QuestMark.Available), "The mine quest appears at Fizzlebang before acceptance");
        }

        static void CheckWesternRoad(PlayerController player)
        {
            var camps = WesternRoadCamps.Current;
            Check(camps && camps.Layby && camps.Rest && camps.Waybill && camps.Ferrymaster, "Western road sites and quest actors load");
            var cargo = camps.Layby.Find("Spilled shipment");
            var crateVisual = cargo ? cargo.Find("Timber supply crate") : null;
            var art = crateVisual ? crateVisual.GetComponentInChildren<Renderer>() : null;
            Check(art && art.sharedMaterial && art.sharedMaterial.mainTexture && cargo.Find("Grain sack"),
                "Western cargo uses the imported meshes and palette");
            Check(cargo.Find("Supply crate").GetComponent<Collider>().enabled, "Cargo art preserves the original collision");

            var rowan = WesternRoadCamps.FindRowan();
            Check(rowan, "Quartermaster Rowan is available for western quest turn-in");
            var interact = typeof(PlayerController).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic);
            Move(player, rowan.transform.position);
            interact.Invoke(player, null);
            player.HandleWesternRoadAtRowan();
            Check(player.WesternRoadStage == 1 && player.TryGetWesternRoadDestination(out var site, out _) && site == camps.Layby.position,
                "Rowan starts the western quest and marks the layby");
            player.CloseShop();

            var engineer = Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None)
                .Single(npc => npc.npcName == BrassHollows.Engineer);
            Move(player, engineer.transform.position);
            player.OpenTownNPCDialogue(engineer);
            player.HandleBrassHollowsQuest();
            player.CloseDialogue();
            var radar = MinimapRadar.Instance;
            var cave = BrassHollows.Current;
            Check(player.BrassHollowsQuestActive && cave &&
                radar.TryGetNavigationTarget(out var entrance, out _) && entrance == cave.Entrance,
                "Explicit cave tracking selects the mine entrance");
            Move(player, new Vector3(642, 36.5f, 167));
            Check(BrassHollows.Inside(player.transform.position), "Navigation fixture enters cave bounds");
            radar.FollowWesternRoad();
            var pin = cave.Entrance + Vector3.forward * 5;
            MinimapRadar.SetWaypoint(pin, "Personal map pin");
            Check(radar.TryGetNavigationTarget(out var selected, out var caption) && selected == pin && caption == "Personal map pin",
                "Manual pin overrides the tracked quest");
            MinimapRadar.ClearWaypoint();
            Check(radar.TryGetNavigationTarget(out var western, out _) && western == camps.Layby.position,
                "Clearing a pin inside the cave resumes the explicitly tracked western quest");

            Move(player, camps.Waybill.position);
            interact.Invoke(player, null);
            Check(player.WesternRoadStage == 1, "Guarded waybill cannot be recovered early");

            var guards = camps.Layby.GetComponentsInChildren<EnemyController>();
            Check(guards.Count(enemy => enemy.enemyId.StartsWith("western-waybill-")) == 2, "Western quest spawns exactly two guards");
            foreach (var guard in guards.Where(enemy => enemy.enemyId.StartsWith("western-waybill-")))
                guard.GetComponent<Combatant>().Damage(100000);
            interact.Invoke(player, null);
            Check(player.WesternRoadStage == 2 && player.TryGetWesternRoadDestination(out var witness, out _) &&
                  witness == camps.Ferrymaster.transform.position, "Waybill advances the objective to Sera");
            Move(player, camps.Ferrymaster.transform.position);
            player.OpenTownNPCDialogue(camps.Ferrymaster);
            player.ReadFerryTally();
            player.CloseDialogue();
            Check(player.WesternRoadStage == 3, "Sera supplies the ferry tally");

            Move(player, rowan.transform.position);
            interact.Invoke(player, null);
            int before = player.Gold;
            player.HandleWesternRoadAtRowan();
            player.HandleWesternRoadAtRowan();
            Check(player.WesternRoadStage == 4 && player.Gold == before + 35, "Western turn-in rewards exactly once");
            player.CloseShop();
            player.SaveAdventure(false);
            player.LoadAdventure();
            Check(player.WesternRoadStage == 4 && !player.TryGetWesternRoadDestination(out _, out _),
                "Western quest completion survives reload and clears its objective");
            Check(radar.TryGetNavigationTarget(out var next, out _) && next == cave.Entrance,
                "Completing the tracked western quest falls back to the active cave quest");
        }

        static void Move(PlayerController player, Vector3 position)
        {
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
        }

        static void CheckExpandedWorld(PlayerController player)
        {
            Check(Terrain.activeTerrains.Count(t=>t.isActiveAndEnabled)==4,"Expanded world has four active terrain tiles");
            Check(WorldRegions.Bounds.width==768 && WorldRegions.Bounds.height==768,"World footprint is 768 by 768 metres");
            Check(PartySystem.Instance && PartySystem.Instance.Members.Count==0,"Characters start solo without companions");
            var party=PartySystem.Instance;
            party.SpawnCompanion("Validation ally","Warrior","Tank",Color.white);
            Check(party.Members.Count==1 && party.Members[0].companionObject,"An ally can be called explicitly");
            party.KickMember("Validation ally");
            Check(party.Members.Count==0,"Called allies can be dismissed");
            Check(AudioEffects.LoadRecording("mouseclick1") && AudioEffects.LoadRecording("footstep00"),"Recorded UI and footstep audio is packaged");
            foreach(var point in new[]{WorldRegions.Cathedral,WorldRegions.Bloodrock,WorldRegions.Summit,WorldRegions.Greenhollow,WorldRegions.Deathknell})
            {
                Check(WorldRegions.TerrainAt(point),"Terrain exists below region "+point);
                var grounded=PlayerController.ClampAboveGround(new Vector3(point.x,-2,point.z),.3f);
                Check(grounded.y>=WorldRegions.GroundHeight(point),"Grounding supports all tiles and high-elevation regions");
            }
            Check(WorldRegions.LegacyPosition(new Vector3(50,9.2f,210),"Brute").x>200,"Legacy Horde save positions migrate to the expanded eastern region");
            Check(WorldRegions.LegacyPosition(new Vector3(-400,-40,200))==new Vector3(-400,-40,200),"Underground save positions stay in the dungeon");
            Check(FlightMaster.WorldFlightNodes.Any(n=>n.targetPos.x>200)&&FlightMaster.WorldFlightNodes.Any(n=>n.targetPos.z>450),"Flight destinations use separated eastern and northern zones");
            CheckExploration(player);
            CheckFactionBossObjectives(player);
            CheckCartography(player);
            var restoredCaptain = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(e => e.enemyId == RiverReaverCamp.CaptainId);
            Check(restoredCaptain.gameObject.activeSelf && restoredCaptain.GetComponent<Combatant>().Alive && restoredCaptain.GetComponent<CharacterController>().enabled, "Loading an earlier character restores enemies it has not defeated");
        }

        static void CheckCartography(PlayerController player)
        {
            var system = FactionQuestSystem.Instance;
            player.SaveAdventure(false);
            string adventure = File.ReadAllText(PlayerController.SavePathOverride);
            string path = Path.ChangeExtension(PlayerController.SavePathOverride, ".faction.json");
            string faction = File.ReadAllText(path);
            try
            {
                var npc = Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None).Single(n => n.npcName == ExplorationSites.CartographerName);
                Check(npc.GetComponentInChildren<Animator>(), "Cartographer has an animated character model");
                Move(player, npc.transform.position + Vector3.forward * 1.5f);
                typeof(PlayerController).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                Check(player.DialogueOpen && player.ActiveTownNPC == npc, "Cartographer opens the existing NPC dialogue");
                var quest = system.GetAvailableQuestForNPC(npc.npcName, player);
                Check(quest != null && quest.id == "frontier_cartography", "Cartographer offers the survey quest");
                system.AcceptQuest(quest, player); player.CloseDialogue();
                player.DiscoverLandmark(ExplorationSites.SurveyIds[0]);
                Check(system.CurrentProgress == 1, "Survey advances when a site is discovered");
                player.DiscoverLandmark(ExplorationSites.SurveyIds[0]);
                Check(system.CurrentProgress == 1, "Survey ignores repeated discoveries");
                player.LoadAdventure(); system.LoadData();
                Check(system.CurrentProgress == 1, "Survey progress survives reload");
                Vector3 personalPin = WorldRegions.Highspire;
                MinimapRadar.SetWaypoint(personalPin, "Keep this pin");
                player.DiscoverLandmark(ExplorationSites.SurveyIds[1]); player.DiscoverLandmark(ExplorationSites.SurveyIds[2]);
                Check(system.HasActiveQuestToComplete(npc.npcName, player), "All sites make the survey ready for hand-in");
                var radar = MinimapRadar.Instance;
                Check(radar.TryGetNavigationTarget(out var pinned, out _) && pinned == personalPin,
                    "Survey discoveries preserve a manual pin");
                MinimapRadar.ClearWaypoint();
                Check(radar.TryGetNavigationTarget(out var turnIn, out _) && Vector3.Distance(turnIn, npc.transform.position) < .1f,
                    "Removing the pin resumes the survey turn-in objective");
                MinimapRadar.SetWaypoint(personalPin, "Keep this pin");
                Move(player, npc.transform.position + Vector3.forward);
                system.CompleteQuest(player);
                Check(system.IsQuestCompleted("frontier_cartography"), "Survey completion is recorded");
                Check(radar.TryGetNavigationTarget(out var afterTurnIn, out _) && afterTurnIn == personalPin,
                    "Faction turn-in preserves a manual pin");
                MinimapRadar.ClearWaypoint();
                int gold = player.Gold, xp = player.Experience;
                system.CompleteQuest(player);
                Check(player.Gold == gold && player.Experience == xp, "Survey hand-in rewards cannot repeat");
                system.LoadData();
                Check(system.IsQuestCompleted("frontier_cartography"), "Survey completion survives reload");
                CheckRiverReavers(player, system, npc);
                File.WriteAllText(path, JsonUtility.ToJson(new FactionQuestSystem.FactionQuestSaveData())); system.LoadData();
                system.AcceptQuest(quest, player);
                Check(system.CurrentProgress == 3, "Sites discovered before quest acceptance count toward the survey");
            }
            finally
            {
                player.CloseDialogue(); File.WriteAllText(PlayerController.SavePathOverride, adventure); player.LoadAdventure();
                File.WriteAllText(path, faction); system.LoadData(); MinimapRadar.ClearWaypoint();
            }
        }

        static void CheckRiverReavers(PlayerController player, FactionQuestSystem system, TownNPC giver)
        {
            var actors = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var captain = actors.Single(e => e.enemyId == RiverReaverCamp.CaptainId);
            var stats = captain.GetComponent<Combatant>();
            Check(captain.animator && captain.isBoss && stats.maxHealth == 260, "River Reaver captain has a complete boss rig and vitals");
            var tile = WorldRegions.TerrainAt(RiverReaverCamp.Position);
            Check(tile && tile.GetComponent<TerrainCollider>().terrainData == tile.terrainData &&
                Terrain.activeTerrains.Count(other => other.terrainData == tile.terrainData) == 1,
                "Camp uses its own terrain data for render and collision");
            Check(ReaverCampTerrainSetup.CheckApproach() <= 35, "Camp approach supports walking and mounted travel below35 degrees");
            Check(actors.Count(e => e.enemyId.StartsWith("westbank-reaver-")) == 3, "River Reaver camp has exactly one captain and two guards");
            CheckEnemyDisengagement(player, captain);
            var quest = system.GetAvailableQuestForNPC(giver.npcName, player);
            Check(quest != null && quest.id == "river_reaver_bounty", "Cartography unlocks the River Reaver bounty");
            system.AcceptQuest(quest, player);
            var chest = Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).Single(c => c.chestId == RiverReaverCamp.CacheId);
            Move(player, chest.transform.position + Vector3.forward);
            Check(!player.OpenChest(chest), "Reaver lockbox is unavailable before the captain's defeat");
            system.OnEnemyDefeated(actors.Single(e => e.enemyId == "westbank-reaver-1"));
            Check(system.CurrentProgress == 0, "A guard does not satisfy the captain bounty");
            stats.Damage(10000);
            Check(player.HasDefeated(RiverReaverCamp.CaptainId) && system.IsActiveQuestReady(), "Captain death advances the bounty through the real damage event");
            Check(!player.BossDefeated && player.Kills == 0, "Optional captain does not alter Abe's campaign");
            Check(player.OpenChest(chest) && !player.OpenChest(chest), "Captain unlocks a one-time camp lockbox");
            player.LoadAdventure(); system.LoadData();
            Check(!captain.gameObject.activeSelf && player.HasDefeated(RiverReaverCamp.CaptainId), "Defeated captain stays defeated and is hidden after reload");
            Check(player.HasOpenedChest(RiverReaverCamp.CacheId), "Claimed lockbox survives reload");
            Move(player, giver.transform.position + Vector3.forward);
            system.CompleteQuest(player);
            Check(player.OwnsItem("riverwarden-signet") && system.IsQuestCompleted("river_reaver_bounty"), "Bounty awards Riverwarden's Signet and completes");
            int gold = player.Gold;
            system.CompleteQuest(player);
            Check(player.Gold == gold, "Bounty hand-in cannot award twice");
            File.WriteAllText(Path.ChangeExtension(PlayerController.SavePathOverride, ".faction.json"), JsonUtility.ToJson(new FactionQuestSystem.FactionQuestSaveData()));
            system.LoadData(); system.AcceptQuest(quest, player);
            Check(system.IsActiveQuestReady(), "Defeating the captain before accepting the bounty still counts");
        }

        static void CheckEnemyDisengagement(PlayerController player, EnemyController actor)
        {
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(EnemyController);
            var motor = actor.GetComponent<CharacterController>();
            var stats = actor.GetComponent<Combatant>();
            Vector3 home = actor.transform.position;
            var update = type.GetMethod("Update", hidden);
            void Set(string name, object value) => type.GetField(name, hidden).SetValue(actor, value);
            void Position(Vector3 value) { motor.enabled = false; actor.transform.position = value; motor.enabled = true; Physics.SyncTransforms(); }
            try
            {
                Position(home + Vector3.right * 29);
                Move(player, home + Vector3.right * 30);
                Set("pendingDamage", true); Set("damageAt", Time.time - 1); Set("attackEnds", Time.time + 3);
                float health = player.Stats.Health;
                update.Invoke(actor, null);
                Check(player.Stats.Health == health && !(bool)type.GetField("pendingDamage", hidden).GetValue(actor), "Leashing cancels a queued melee hit without damaging the player");
                Move(player, home + Vector3.right * 24);
                update.Invoke(actor, null);
                Check((bool)type.GetField("evading", hidden).GetValue(actor), "Enemy cannot reaggro midway through its return home");
                Move(player, home + Vector3.right * 30);
                type.GetMethod("HeavyAttack", hidden).Invoke(actor, null);
                update.Invoke(actor, null);
                Check((float)type.GetField("heavyUntil", hidden).GetValue(actor) == 0 && !CastBarSystem.IsTargetCasting(stats), "Leashing cancels the heavy telegraph and cast bar");
                stats.Damage(20); Position(home + Vector3.right * .5f);
                update.Invoke(actor, null);
                Check(stats.Health == stats.maxHealth, "An evading enemy fully recovers on reaching home");
                actor.ResetForLoad();
                Check(Vector3.Distance(actor.transform.position, home) < .01f && motor.enabled && stats.Alive, "Reload reset restores enemy position, health and motor");
                Move(player, home + Vector3.forward * 4);
                actor.transform.rotation = Quaternion.identity;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    wall.name = "Isolated enemy sight test wall";
                    wall.transform.position = home + Vector3.forward * 2 + Vector3.up;
                    wall.transform.localScale = new Vector3(3, 3, .25f); Physics.SyncTransforms();
                    var sight = type.GetMethod("ClearSightForTarget", hidden);
                    var sightTarget = new object[] { player.transform };
                    Check(!(bool)sight.Invoke(actor, sightTarget), "A solid wall blocks enemy line of sight");
                    wall.SetActive(false); Physics.SyncTransforms();
                    Check((bool)sight.Invoke(actor, sightTarget), "Enemy sight accepts the player collider when the path is clear");
                }
                finally { Object.Destroy(wall); }
            }
            finally { actor.ResetForLoad(); player.Stats.Restore(); }
        }

        static void CheckFactionBossObjectives(PlayerController player)
        {
            FactionQuestSystem.EnsureSystem(player);
            var system = FactionQuestSystem.Instance;
            string path = Path.ChangeExtension(PlayerController.SavePathOverride, ".faction.json");
            string saved = File.Exists(path) ? File.ReadAllText(path) : JsonUtility.ToJson(new FactionQuestSystem.FactionQuestSaveData());
            string originalOverride = PlayerController.SavePathOverride;
            try
            {
                var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var captain = enemies.Single(e => e.enemyId == "ashwood-captain");
                foreach (string id in new[] { "bloodrock_warlord", "gorkresh_apex_clash", "alliance_wyrmcrest_summit" })
                {
                    var quest = FactionQuestSystem.GetQuest(id);
                    system.AcceptQuest(quest, player);
                    system.OnEnemyDefeated(captain);
                    Check(system.CurrentProgress == 0, "Unrelated captain cannot complete " + id);
                    system.OnEnemyDefeated(enemies.Single(e => e.enemyId == quest.targetId));
                    Check(system.IsActiveQuestReady(), "Correct regional boss completes " + id);
                }
                PlayerController.SavePathOverride = Path.GetFullPath(".code-check/no-faction-save-" + Guid.NewGuid().ToString("N") + ".json");
                system.LoadData();
                Check(system.ActiveQuestId == "" && system.CurrentProgress == 0, "Character without a faction save starts with empty quest progress");
            }
            finally
            {
                PlayerController.SavePathOverride = originalOverride;
                File.WriteAllText(path, saved); system.LoadData(); MinimapRadar.ClearWaypoint();
            }
        }

        static void CheckExploration(PlayerController player)
        {
            player.SaveAdventure(false);
            string saved = File.ReadAllText(PlayerController.SavePathOverride);
            try
            {
                var zebra = player.mounts.First(m => m.mountId == "zebra");
                foreach (string id in new[] { "westbank-overlook", "bloodrock-vista", "wyrmcrest-lookout" })
                {
                    var marker = Object.FindObjectsByType<Landmark>(FindObjectsSortMode.None).Single(m => m.landmarkId == id);
                    Check(Mathf.Abs(marker.transform.position.y - WorldRegions.GroundHeight(marker.transform.position)) < .15f, "Exploration site grounded: " + id);
                    player.DiscoverLandmark(id);
                    int gold = player.Gold, xp = player.Experience;
                    player.DiscoverLandmark(id);
                    Check(player.Gold == gold && player.Experience == xp, "Exploration rewards pay once: " + id);
                }
                Check(player.GreenhollowLandmarksFound == 0 && !player.MountUnlocked(zebra), "Other discoveries cannot unlock the Greenhollow mount");
                player.DiscoverLandmark("willow-crossing"); player.DiscoverLandmark("greenhollow-post");
                Check(!player.MountUnlocked(zebra), "Two Greenhollow sites keep zebra locked");
                player.DiscoverLandmark("ancient-grove");
                Check(player.MountUnlocked(zebra), "All three Greenhollow sites unlock zebra");
                player.LoadAdventure();
                Check(player.HasLandmark("wyrmcrest-lookout") && player.MountUnlocked(zebra), "New discoveries and mount unlock survive reload");
                var cache = Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).Single(c => c.chestId == "westbank-overlook-cache");
                Move(player, cache.transform.position + Vector3.forward);
                Check(player.OpenChest(cache), "New exploration supply cache can be collected");
                Check(!player.OpenChest(cache), "Exploration cache cannot pay twice");
            }
            finally { File.WriteAllText(PlayerController.SavePathOverride, saved); player.LoadAdventure(); }
        }

        static void OpenAbe(PlayerController player)
        {
            Move(player, player.questGiver.position);
            typeof(PlayerController).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            Check(player.DialogueOpen && player.ActiveQuestGiver, $"Abe dialogue opens through interaction (current loot: {LootDropSystem.Instance.CurrentLooting?.name ?? "none"})");
        }

        static void TurnIn(PlayerController player)
        {
            OpenAbe(player);
            string title = player.QuestName;
            int experience = player.QuestExperience;
            player.CompleteQuest();
            var messages = (List<ChatSystem.ChatEntry>)typeof(ChatSystem).GetField("messages", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ChatSystem.Instance);
            Check(messages.Any(m => m.text.Contains("Quest Completed: " + title) && m.text.Contains(experience + " XP")), "Hand-in reports the completed quest and its reward");
        }

        static void DefeatTestEnemy(PlayerController player, string id, bool boss = false)
        {
            var go = new GameObject("Quest regression " + id);
            go.transform.position = player.transform.position + Vector3.right * 80;
            var enemy = go.AddComponent<EnemyController>();
            enemy.enabled = false;
            enemy.player = player;
            enemy.enemyId = id;
            enemy.isBoss = boss;
            go.GetComponent<Combatant>().Damage(100000);
        }

        static void QuestRegression(PlayerController player)
        {
            var all = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var worldBoss = all.First(e => e.enemyId == "worldboss-gorgonax");
            Check(worldBoss.gameObject.activeSelf, "World boss is available independently of Abe's quest");
            OpenAbe(player); player.AcceptQuest();
            Check(player.QuestAccepted, "Abe's road mission is accepted");
            MinimapRadar.Instance.TrackMainQuest();
            Check(MinimapRadar.Instance.TrackingMainQuest, "Journal navigation can start live tracking");
            Check(all.Any(e => e.IsRoadRaider && e.transform.position == player.ObjectivePosition), "Road objective targets a real raider instead of a dummy or unrelated enemy");
            DefeatTestEnemy(player, "regression-side-boss", true);
            DefeatTestEnemy(player, "regression-rat");
            Check(player.Kills == 0 && !player.BossDefeated, "Unrelated mobs and bosses grant no main-quest credit");
            var raiders = all.Where(e => e.IsRoadRaider).OrderBy(e => e.enemyId).Take(3).ToArray();
            int gold = player.Gold, shards = player.BoneShards;
            foreach (var raider in raiders) raider.GetComponent<Combatant>().Damage(100000);
            Check(player.Kills == 3 && player.QuestReady, "Three road raiders complete the road objective");
            Check(player.Gold == gold && player.BoneShards == shards && !player.OwnsItem("raider-cleaver"), "Enemy loot is not awarded before collection");
            Check(player.SaveAdventure(false), "Unclaimed loot can be saved");
            player.LoadAdventure();
            Check(!player.OwnsItem("raider-cleaver"), "Reloading cannot bypass corpse looting");
            var loot = LootDropSystem.Instance;
            var corpse = loot.ActiveCorpses.First(c => c.items.Contains("raider-cleaver"));
            loot.LootCoins(corpse);
            int afterCoins = player.Gold;
            Check(afterCoins - gold == 4, $"Human gold bonus applies exactly once (received {afterCoins - gold}, race {player.CharacterRace})");
            player.LoadAdventure();
            corpse = loot.ActiveCorpses.First(c => c.items.Contains("raider-cleaver"));
            Check(corpse.gold == 0, "Claimed coins stay claimed after reload");
            loot.LootAll(corpse);
            loot.LootAll(corpse);
            Check(player.Gold == afterCoins && player.BoneShards == shards + 1 && player.OwnsItem("raider-cleaver"), "Looting grants valid gear and shards once");
            player.LoadAdventure();
            Check(player.OwnsItem("raider-cleaver") && !loot.ActiveCorpses.Any(c => c.items.Contains("raider-cleaver")), "Claimed item and corpse removal persist");
            var data = JsonUtility.FromJson<PlayerController.AdventureSave>(File.ReadAllText(player.SavePath));
            data.lootOnCorpses = false; data.items = new[] { "starter-axe" };
            File.WriteAllText(player.SavePath, JsonUtility.ToJson(data));
            player.LoadAdventure();
            Check(player.OwnsItem("raider-cleaver"), "Legacy automatic-loot saves retain earned equipment");
            data.lootOnCorpses = true;
            File.WriteAllText(player.SavePath, JsonUtility.ToJson(data));
            player.LoadAdventure();
            Check(!player.OwnsItem("raider-cleaver"), "New-format saves do not auto-grant enemy equipment");
        }

        static void CaptureUI(string name)
        {
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                ScreenCapture.CaptureScreenshot(".code-check/UI-" + name + ".png");
        }

        static void Finish(bool failed)
        {
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= Log;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            string report = string.Join("\n", Results) + "\n" + string.Join("\n", Errors);
            File.WriteAllText("QuestRouteSmokeResult.txt", report);
            SessionState.SetBool(Running + ".Failed", failed);
            if (failed) File.WriteAllText(".code-check/startup-smoke-failed.txt", report);
            EditorApplication.isPlaying = false;
        }
    }
}
