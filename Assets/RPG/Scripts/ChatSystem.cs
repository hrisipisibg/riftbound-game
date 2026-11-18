using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class ChatSystem : MonoBehaviour
    {
        public static ChatSystem Instance { get; private set; }

        public enum Channel
        {
            System,
            Say,
            Yell,
            Party,
            Guild,
            Emote,
            Combat,
            Loot
        }

        public struct ChatEntry
        {
            public DateTime timestamp;
            public Channel channel;
            public string sender;
            public string text;
            public Color color;

            public string FormattedTime => timestamp.ToString("HH:mm");
        }

        public enum ChatTab
        {
            All,
            General,
            Combat
        }

        List<ChatEntry> messages = new List<ChatEntry>();
        const int MaxMessages = 120;

        public ChatTab CurrentTab { get; set; } = ChatTab.All;
        public bool InputActive { get; private set; }
        public bool HistoryVisible { get; private set; }
        public string InputText { get; set; } = "";

        Vector2 scrollPos = Vector2.zero;
        PlayerController player;

        GUIStyle boxStyle, tabStyle, activeTabStyle, msgStyle, inputStyle, prefixStyle;
        Texture2D bgTex, tabActiveTex;
        bool focusRequested = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();

            // Welcome message in classic WoW style
            LogSystem("Welcome to Riftbound: Battle for the Highlands.");
            LogSystem("Type /help for chat commands. Press [Enter] to chat.");
        }

        public static void LogSystem(string text)
        {
            if (Instance) Instance.AddMessage(Channel.System, "System", text, new Color(1.0f, 0.82f, 0.2f));
        }

        public static void LogCombat(string text)
        {
            if (Instance) Instance.AddMessage(Channel.Combat, "", text, new Color(1.0f, 0.65f, 0.35f));
        }

        public static void LogGuild(string sender, string text)
        {
            if (Instance) Instance.AddMessage(Channel.Guild, sender, text, new Color(0.25f, 1.0f, 0.25f));
        }

        public static void LogLoot(string itemName, string rarity = "common", int count = 1)
        {
            Color lootColor = rarity switch
            {
                "legendary" => new Color(1.0f, 0.5f, 0.0f),
                "epic" => new Color(0.64f, 0.21f, 0.93f),
                "rare" => new Color(0.0f, 0.44f, 0.87f),
                "uncommon" => new Color(0.12f, 1.0f, 0.0f),
                _ => Color.white
            };
            string qty = count > 1 ? $" x{count}" : "";
            if (Instance) Instance.AddMessage(Channel.Loot, "", $"You receive loot: [{itemName}]{qty}.", lootColor);
        }

        public static void LogQuest(string text)
        {
            if (Instance) Instance.AddMessage(Channel.System, "Quest", text, new Color(0.95f, 0.85f, 0.35f));
        }

        public static void LogZone(string zoneName)
        {
            if (Instance) Instance.AddMessage(Channel.System, "", $"Discovered: {zoneName}.", new Color(1f, 0.78f, 0.28f));
        }

        public void AddMessage(Channel ch, string sender, string text, Color color)
        {
            var entry = new ChatEntry
            {
                timestamp = DateTime.Now,
                channel = ch,
                sender = sender,
                text = text,
                color = color
            };
            messages.Add(entry);
            if (messages.Count > MaxMessages) messages.RemoveAt(0);
            scrollPos.y = float.MaxValue; // Auto scroll to bottom
        }

        void Update()
        {
            if (FrontendMenu.IsOpen) return;

            var keys = Keyboard.current;
            if (keys == null) return;

            bool enterPressed = keys.enterKey.wasPressedThisFrame || (keys.numpadEnterKey != null && keys.numpadEnterKey.wasPressedThisFrame);
            bool escPressed = keys.escapeKey.wasPressedThisFrame;

            if (enterPressed)
            {
                if (!InputActive)
                {
                    OpenChat();
                }
                else
                {
                    SubmitChat();
                }
            }
            else if (escPressed && InputActive)
            {
                CancelChat();
            }
        }

        public void OpenChat()
        {
            HistoryVisible = true;
            InputActive = true;
            focusRequested = true;
            if (player) player.ChatOpen = true;
        }

        public void CancelChat()
        {
            InputActive = false;
            InputText = "";
            if (player) player.ChatOpen = false;
            HistoryVisible = false;
        }

        public void SubmitChat()
        {
            string raw = InputText.Trim();
            InputActive = false;
            InputText = "";
            if (player) player.ChatOpen = false;
            HistoryVisible = false;

            if (string.IsNullOrEmpty(raw)) return;

            ProcessInputString(raw);
        }

        public void ProcessInputString(string raw)
        {
            string playerName = player != null ? player.CharacterName : "Hero";

            if (raw.StartsWith("/"))
            {
                int spaceIndex = raw.IndexOf(' ');
                string cmd = (spaceIndex > 0 ? raw.Substring(1, spaceIndex - 1) : raw.Substring(1)).ToLowerInvariant();
                string args = spaceIndex > 0 ? raw.Substring(spaceIndex + 1).Trim() : "";

                switch (cmd)
                {
                    case "s":
                    case "say":
                        if (!string.IsNullOrEmpty(args))
                            AddMessage(Channel.Say, playerName, args, Color.white);
                        break;

                    case "y":
                    case "yell":
                        if (!string.IsNullOrEmpty(args))
                            AddMessage(Channel.Yell, playerName, args.ToUpperInvariant() + "!!", new Color(1.0f, 0.25f, 0.25f));
                        break;

                    case "p":
                    case "party":
                        if (!string.IsNullOrEmpty(args))
                            AddMessage(Channel.Party, playerName, args, new Color(0.44f, 0.76f, 1.0f));
                        else if (PartySystem.Instance) PartySystem.Instance.ToggleCompanionMenu();
                        break;

                    case "crew":
                    case "companions":
                        if (PartySystem.Instance) PartySystem.Instance.ToggleCompanionMenu();
                        break;

                    case "g":
                    case "guild":
                        if (!string.IsNullOrEmpty(args))
                        {
                            AddMessage(Channel.Guild, playerName, args, new Color(0.25f, 1.0f, 0.25f));
                        }
                        else
                        {
                            var gSys = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                            if (gSys) gSys.IsOpen = !gSys.IsOpen;
                        }
                        break;

                    case "c":
                    case "character":
                    case "sheet":
                        var csCmd = CharacterSheetSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<CharacterSheetSystem>();
                        if (csCmd) csCmd.ToggleSheet();
                        break;

                    case "gmotd":
                        var gMotd = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                        if (gMotd)
                        {
                            if (!string.IsNullOrEmpty(args))
                            {
                                gMotd.SetMOTD(args);
                            }
                            else
                            {
                                AddMessage(Channel.Guild, "MOTD", gMotd.GuildMOTD, new Color(0.25f, 1.0f, 0.25f));
                            }
                        }
                        break;

                    case "ginvite":
                        var gInv = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                        if (gInv)
                        {
                            if (!string.IsNullOrEmpty(args))
                            {
                                gInv.AddMember(args, "Warrior", GuildSystem.GuildRank.Initiate, 5, "Amber Vale", "Recruit");
                            }
                            else
                            {
                                AddMessage(Channel.System, "Guild", "Usage: /ginvite [name]", new Color(1f, 0.82f, 0.2f));
                            }
                        }
                        break;

                    case "gkick":
                        var gKick = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                        if (gKick)
                        {
                            if (!string.IsNullOrEmpty(args))
                            {
                                gKick.KickMember(args);
                            }
                            else
                            {
                                AddMessage(Channel.System, "Guild", "Usage: /gkick [name]", new Color(1f, 0.82f, 0.2f));
                            }
                        }
                        break;

                    case "gpromote":
                        var gPromote = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                        if (gPromote && !string.IsNullOrEmpty(args))
                            gPromote.PromoteMember(args);
                        break;

                    case "gdemote":
                        var gDemote = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                        if (gDemote && !string.IsNullOrEmpty(args))
                            gDemote.DemoteMember(args);
                        break;

                    case "gbank":
                    case "gvault":
                        var gBank = GuildSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                        if (gBank)
                        {
                            gBank.IsOpen = true;
                            gBank.ActiveTab = GuildSystem.GuildTab.Bank;
                        }
                        break;

                    case "e":
                    case "me":
                    case "emote":
                        if (!string.IsNullOrEmpty(args))
                            AddMessage(Channel.Emote, "", $"{playerName} {args}", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "dance":
                        AddMessage(Channel.Emote, "", $"{playerName} bursts into an energetic dance!", new Color(1.0f, 0.55f, 0.25f));
                        if (player) player.Play("Idle"); // Can trigger dance emote
                        break;

                    case "cheer":
                        AddMessage(Channel.Emote, "", $"{playerName} cheers enthusiastically!", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "wave":
                        AddMessage(Channel.Emote, "", $"{playerName} waves hello to everyone nearby.", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "bow":
                        AddMessage(Channel.Emote, "", $"{playerName} bows respectfully.", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "salute":
                        AddMessage(Channel.Emote, "", $"{playerName} stands at attention and salutes with honor.", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "kneel":
                        AddMessage(Channel.Emote, "", $"{playerName} drops to one knee.", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "sit":
                        AddMessage(Channel.Emote, "", $"{playerName} sits down on the ground.", new Color(1.0f, 0.55f, 0.25f));
                        break;

                    case "roll":
                        int maxRoll = 100;
                        if (!string.IsNullOrEmpty(args) && int.TryParse(args, out int parsedMax) && parsedMax > 1)
                            maxRoll = parsedMax;
                        int rollVal = UnityEngine.Random.Range(1, maxRoll + 1);
                        AddMessage(Channel.System, "Roll", $"{playerName} rolls {rollVal} (1-{maxRoll})", new Color(1f, 0.85f, 0.25f));
                        break;

                    case "music":
                        if (string.IsNullOrEmpty(args))
                        {
                            float current = ZoneAudioAmbience.MasterMusicVolume;
                            float next = current > 0.05f ? 0f : 0.35f;
                            ZoneAudioAmbience.MasterMusicVolume = next;
                            AddMessage(Channel.System, "Music", $"Atmospheric Music: {(next > 0.05f ? $"ON ({Mathf.RoundToInt(next * 100)}%)" : "OFF")}", new Color(0.4f, 1f, 0.4f));
                        }
                        else if (float.TryParse(args, out float vol))
                        {
                            if (vol > 1f) vol /= 100f;
                            ZoneAudioAmbience.MasterMusicVolume = vol;
                            AddMessage(Channel.System, "Music", $"Atmospheric Music Volume set to {Mathf.RoundToInt(vol * 100)}%.", new Color(0.4f, 1f, 0.4f));
                        }
                        break;

                    case "titles":
                        if (player)
                        {
                            var unlocked = PlayerTitleSystem.GetUnlockedTitles(player);
                            string titleList = "";
                            foreach (var t in unlocked)
                            {
                                bool isCur = t.name.Equals(PlayerTitleSystem.ActiveTitle, System.StringComparison.OrdinalIgnoreCase);
                                titleList += (titleList.Length > 0 ? ", " : "") + (isCur ? $"[<b>{t.name}</b>]" : t.name);
                            }
                            AddMessage(Channel.System, "Titles", $"Unlocked Titles: {titleList}. Use /title [name] to switch.", new Color(1f, 0.85f, 0.25f));
                        }
                        break;

                    case "title":
                        if (!string.IsNullOrEmpty(args))
                        {
                            if (args.Equals("none", System.StringComparison.OrdinalIgnoreCase) || args.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
                            {
                                PlayerTitleSystem.SetActiveTitle("");
                                AddMessage(Channel.System, "Titles", "Title removed.", new Color(0.4f, 1f, 0.4f));
                            }
                            else if (PlayerTitleSystem.SetActiveTitle(args))
                            {
                                AddMessage(Channel.System, "Titles", $"Active title set to: '{PlayerTitleSystem.ActiveTitle}'", new Color(0.4f, 1f, 0.4f));
                            }
                            else
                            {
                                AddMessage(Channel.System, "Titles", $"Unknown or locked title '{args}'. Type /titles to see available titles.", new Color(1f, 0.4f, 0.4f));
                            }
                        }
                        else
                        {
                            AddMessage(Channel.System, "Titles", $"Current Title: '{(string.IsNullOrEmpty(PlayerTitleSystem.ActiveTitle) ? "(None)" : PlayerTitleSystem.ActiveTitle)}'. Type /titles to list, or /title [name].", new Color(1f, 0.85f, 0.25f));
                        }
                        break;

                    case "who":
                        string zone = ZoneManager.GetZoneName(player ? player.transform.position : Vector3.zero);
                        int lvl = player ? player.Level : 1;
                        string cls = player ? player.CharacterClass : "Warrior";
                        string race = player ? player.CharacterRace : "Human";
                        AddMessage(Channel.System, "Who", $"1 player online: {playerName} (Level {lvl} {race} {cls}) - {zone}", new Color(1f, 0.85f, 0.3f));
                        break;

                    case "played":
                        int minutes = Mathf.RoundToInt(Time.time / 60f);
                        AddMessage(Channel.System, "Time", $"Total time played this session: {minutes} minutes.", new Color(1f, 0.85f, 0.3f));
                        break;

                    case "macro":
                    case "macros":
                    case "m":
                        if (MacroSystem.Instance) MacroSystem.Instance.ToggleWindow();
                        break;

                    case "bind":
                    case "sethome":
                        if (RestedXPSystem.Instance && player)
                        {
                            string curZone = ZoneManager.GetZoneName(player.transform.position);
                            RestedXPSystem.Instance.BindHearthstone(curZone, player.transform.position);
                        }
                        break;

                    case "stuck":
                        if (player)
                        {
                            Vector3 safeHome = player.GetRacialSpawnPosition();
                            AddMessage(Channel.System, "Unstuck", $"Teleporting to safety in {ZoneManager.GetZoneName(safeHome)}...", new Color(0.4f, 1f, 0.4f));
                            var cc = player.GetComponent<CharacterController>();
                            if (cc) cc.enabled = false;
                            player.transform.position = safeHome;
                            if (cc) cc.enabled = true;
                        }
                        break;

                    case "flight":
                    case "mount":
                        if (player) player.ToggleMount();
                        break;

                    case "camp":
                    case "logout":
                        var menu = player ? player.GetComponent<FrontendMenu>() : UnityEngine.Object.FindAnyObjectByType<FrontendMenu>();
                        if (menu) menu.ReturnToMenu();
                        break;

                    case "clear":
                        messages.Clear();
                        break;

                    case "duel":
                        var duelSys = DuelSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<DuelSystem>();
                        if (duelSys)
                        {
                            if (player && player.Target)
                            {
                                if (!duelSys.Challenge(player.Target, out string reason))
                                    AddMessage(Channel.System, "Duel", reason, new Color(1f, 0.4f, 0.4f));
                            }
                            else
                            {
                                AddMessage(Channel.System, "Duel", "You must target a combatant to challenge them to a duel! (Tab to target)", new Color(1f, 0.82f, 0.2f));
                            }
                        }
                        break;

                    case "forfeit":
                    case "yield":
                        var duelF = DuelSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<DuelSystem>();
                        if (duelF && duelF.State != DuelSystem.DuelState.Inactive)
                            duelF.Forfeit("Conceded via command");
                        else
                            AddMessage(Channel.System, "Duel", "You are not currently in a duel.", new Color(1f, 0.4f, 0.4f));
                        break;

                    case "resetinstances":
                    case "reset":
                        var dim = DungeonInstanceManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<DungeonInstanceManager>();
                        if (dim)
                        {
                            dim.ResetAllInstances(player, out string reason);
                        }
                        else
                        {
                            AddMessage(Channel.System, "Dungeon", "Dungeon instance system not initialized.", new Color(1f, 0.4f, 0.4f));
                        }
                        break;

                    case "lfg":
                    case "lfd":
                    case "dungeon":
                    case "dungeons":
                        if (DungeonFinderSystem.Instance) DungeonFinderSystem.Instance.ToggleWindow();
                        break;

                    case "survey":
                    case "archaeology":
                    case "dig":
                        if (ArchaeologySystem.Instance)
                        {
                            if (cmd == "archaeology")
                                ArchaeologySystem.Instance.ToggleWindow();
                            else
                                ArchaeologySystem.Instance.PerformSurvey(player, out _);
                        }
                        break;

                    case "transmog":
                    case "tmog":
                    case "wardrobe":
                    case "appearance":
                        if (TransmogSystem.Instance) TransmogSystem.Instance.ToggleWindow();
                        break;

                    case "fish":
                    case "fishing":
                    case "castfish":
                        if (FishingSystem.Instance)
                        {
                            if (cmd == "fish" && !FishingSystem.Instance.IsWindowOpen && FishingSystem.Instance.State == FishingSystem.FishingState.Idle)
                                FishingSystem.Instance.StartCast(player, out _);
                            else
                                FishingSystem.Instance.ToggleWindow();
                        }
                        break;

                    case "compass":
                    case "minimap":
                    case "radar":
                        if (MinimapRadar.Instance)
                        {
                            MinimapRadar.Instance.showMinimap = !MinimapRadar.Instance.showMinimap;
                            AddMessage(Channel.System, "HUD", $"Minimap Radar HUD: {(MinimapRadar.Instance.showMinimap ? "Enabled" : "Disabled")}", new Color(1f, 0.85f, 0.4f));
                        }
                        break;

                    case "calendar":
                    case "events":
                    case "cal":
                        if (CalendarEventSystem.Instance) CalendarEventSystem.Instance.ToggleWindow();
                        break;

                    case "pets":
                    case "pet":
                    case "petbattle":
                        if (PetBattleSystem.Instance)
                        {
                            if (cmd == "petbattle")
                                PetBattleSystem.Instance.StartWildPetBattle();
                            else
                                PetBattleSystem.Instance.ToggleJournal();
                        }
                        break;

                    case "barber":
                    case "haircut":
                    case "salon":
                        if (BarberShopSystem.Instance) BarberShopSystem.Instance.ToggleWindow();
                        break;

                    case "wpvp":
                    case "worldpvp":
                    case "towers":
                        if (WorldPvPSystem.Instance) WorldPvPSystem.Instance.ToggleOverview();
                        break;

                    case "equipset":
                    case "gearset":
                    case "gear":
                    case "sets":
                        if (EquipmentSetSystem.Instance)
                        {
                            if (!string.IsNullOrEmpty(args))
                                EquipmentSetSystem.Instance.EquipSet(args.Trim(), out _);
                            else
                                EquipmentSetSystem.Instance.ToggleWindow();
                        }
                        break;

                    case "meter":
                    case "dps":
                    case "recount":
                    case "details":
                        if (DamageMeterSystem.Instance) DamageMeterSystem.Instance.ToggleMeter();
                        break;

                    case "resetmeter":
                    case "resetdps":
                        if (DamageMeterSystem.Instance) DamageMeterSystem.Instance.ResetMeter();
                        break;

                    case "reportdps":
                    case "reportmeter":
                        if (DamageMeterSystem.Instance) DamageMeterSystem.Instance.ReportToChat();
                        break;

                    case "glyph":
                    case "glyphs":
                    case "inscription":
                        if (GlyphSystem.Instance) GlyphSystem.Instance.ToggleWindow();
                        break;

                    case "gems":
                    case "gem":
                    case "socket":
                    case "sockets":
                    case "gemming":
                        if (GemSocketSystem.Instance) GemSocketSystem.Instance.ToggleWindow();
                        break;

                    case "prospect":
                        if (GemSocketSystem.Instance) GemSocketSystem.Instance.ProspectOre("Ore", out _);
                        break;

                    case "weather":
                    case "setweather":
                        if (WeatherSystem.Instance)
                        {
                            if (string.IsNullOrEmpty(args))
                            {
                                WeatherSystem.Instance.EvaluateZoneWeather(false);
                            }
                            else if (System.Enum.TryParse(args, true, out WeatherSystem.WeatherType wt))
                            {
                                WeatherSystem.Instance.SetWeather(wt);
                            }
                            else
                            {
                                AddMessage(Channel.System, "Weather", $"Unknown weather '{args}'. Valid: Clear, Rain, Storm, Snow, DenseFog, Sandstorm.", new Color(1f, 0.4f, 0.4f));
                            }
                        }
                        break;

                    case "time":
                    case "settime":
                        if (WeatherSystem.Instance && float.TryParse(args, out float hrs))
                        {
                            WeatherSystem.Instance.SetTimeOfDay(hrs);
                            AddMessage(Channel.System, "Time", $"Time of day set to {WeatherSystem.Instance.FormattedTime}.", new Color(0.4f, 1f, 0.4f));
                        }
                        else
                        {
                            AddMessage(Channel.System, "Time", $"Current Realm Time: {WeatherSystem.Instance.FormattedTime} ({WeatherSystem.Instance.CurrentWeather}). Usage: /time [0-23]", new Color(1f, 0.85f, 0.3f));
                        }
                        break;

                    case "heroic":
                    case "difficulty":
                        var dimDiff = DungeonInstanceManager.Instance ?? UnityEngine.Object.FindAnyObjectByType<DungeonInstanceManager>();
                        if (dimDiff)
                        {
                            var targetDiff = dimDiff.CurrentDifficulty == DungeonInstanceManager.DungeonDifficulty.Normal 
                                ? DungeonInstanceManager.DungeonDifficulty.Heroic 
                                : DungeonInstanceManager.DungeonDifficulty.Normal;
                            dimDiff.SetDifficulty(targetDiff, player, out string reason);
                        }
                        break;

                    case "bg":
                    case "pvp":
                    case "arena":
                        var bgSys = BattlegroundSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<BattlegroundSystem>();
                        if (bgSys)
                        {
                            bgSys.IsWindowOpen = !bgSys.IsWindowOpen;
                            AddMessage(Channel.System, "PvP", $"Battleground window toggled. Honor: {bgSys.PlayerHonorPoints} ★", new Color(0.4f, 0.75f, 1f));
                        }
                        break;

                    case "mail":
                    case "mailbox":
                    case "post":
                        var mbSys = MailboxSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<MailboxSystem>();
                        if (mbSys)
                        {
                            mbSys.IsOpen = !mbSys.IsOpen;
                            AddMessage(Channel.System, "Mail", $"Mailbox toggled. {mbSys.Inbox.Count} letters in inbox ({mbSys.UnreadCount()} unread).", new Color(1f, 0.85f, 0.25f));
                        }
                        break;

                    case "readycheck":
                    case "rc":
                        var partyRc = PartySystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<PartySystem>();
                        if (partyRc)
                        {
                            partyRc.StartReadyCheck(playerName);
                        }
                        else
                        {
                            AddMessage(Channel.System, "Party", "Party system not active.", new Color(1f, 0.4f, 0.4f));
                        }
                        break;

                    case "mark":
                    case "rtm":
                    case "targetmark":
                        var partyMark = PartySystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<PartySystem>();
                        if (partyMark)
                        {
                            if (player && player.Target)
                            {
                                var marker = PartySystem.ParseMarker(args);
                                PartySystem.SetMarker(player.Target, marker);
                            }
                            else
                            {
                                AddMessage(Channel.System, "Mark", "You must target a unit to apply a raid marker. (Usage: /mark skull, star, circle, etc.)", new Color(1f, 0.82f, 0.2f));
                            }
                        }
                        break;

                    case "clearallmarks":
                    case "clearmarks":
                        PartySystem.ClearAllMarkers();
                        break;

                    case "invite":
                    case "inv":
                        var partyInv = PartySystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<PartySystem>();
                        if (partyInv)
                        {
                            if (!string.IsNullOrEmpty(args))
                            {
                                partyInv.SpawnCompanion(args, "Mage", "DPS", new Color(0.25f, 0.78f, 0.92f));
                            }
                            else
                            {
                                AddMessage(Channel.System, "Party", "Usage: /invite [name]", new Color(1f, 0.82f, 0.2f));
                            }
                        }
                        break;

                    case "kick":
                    case "uninvite":
                        var partyKick = PartySystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<PartySystem>();
                        if (partyKick)
                        {
                            if (!string.IsNullOrEmpty(args))
                            {
                                if (!partyKick.KickMember(args))
                                    AddMessage(Channel.System, "Party", $"No party member named '{args}' found.", new Color(1f, 0.4f, 0.4f));
                            }
                            else
                            {
                                AddMessage(Channel.System, "Party", "Usage: /kick [name]", new Color(1f, 0.82f, 0.2f));
                            }
                        }
                        break;

                    case "help":
                        AddMessage(Channel.System, "Commands", "/say, /yell, /party, /guild (/g), /gmotd, /ginvite, /gkick, /gbank, /readycheck, /mark [skull/...], /clearallmarks, /invite, /kick, /dance, /duel, /forfeit, /heroic, /resetinstances, /pvp, /mail, /roll, /titles, /music, /who, /played, /mount, /camp", new Color(1f, 0.82f, 0.2f));
                        break;

                    default:
                        AddMessage(Channel.System, "Error", $"Unknown command: /{cmd}. Type /help for assistance.", new Color(1f, 0.4f, 0.4f));
                        break;
                }
            }
            else
            {
                // Default is Say
                AddMessage(Channel.Say, playerName, raw, Color.white);
            }
        }

        public void DrawChatGUI(float screenW, float screenH)
        {
            if (FrontendMenu.IsOpen) return;

            InitStyles();

            if (!HistoryVisible && !InputActive)
            {
                if (GUI.Button(new Rect(24f, screenH - 35f, 112f, 27f), "Chat [Enter]", tabStyle)) OpenChat();
                return;
            }

            float chatW = 320f;
            float chatH = 110f;
            float chatX = 24f;
            float chatY = screenH - 235f;

            if (GUI.Button(new Rect(chatX + chatW - 25f, chatY - 20f, 25f, 20f), "X", tabStyle)) CancelChat();

            // Chat frame background
            GUI.Box(new Rect(chatX, chatY, chatW, chatH), GUIContent.none, boxStyle);

            // Tabs
            float tabW = 68f;
            float tabH = 22f;
            float tx = chatX + 6f;
            float ty = chatY - tabH + 2f;

            if (GUI.Button(new Rect(tx, ty, tabW, tabH), "All", CurrentTab == ChatTab.All ? activeTabStyle : tabStyle))
            {
                CurrentTab = ChatTab.All;
                AudioEffects.PlayClick();
            }
            tx += tabW + 4f;

            if (GUI.Button(new Rect(tx, ty, tabW, tabH), "General", CurrentTab == ChatTab.General ? activeTabStyle : tabStyle))
            {
                CurrentTab = ChatTab.General;
                AudioEffects.PlayClick();
            }
            tx += tabW + 4f;

            if (GUI.Button(new Rect(tx, ty, tabW, tabH), "Combat", CurrentTab == ChatTab.Combat ? activeTabStyle : tabStyle))
            {
                CurrentTab = ChatTab.Combat;
                AudioEffects.PlayClick();
            }

            // Chat message log area
            Rect logArea = new Rect(chatX + 8f, chatY + 6f, chatW - 16f, chatH - 12f);
            List<ChatEntry> filtered = GetFilteredMessages();

            float contentH = Mathf.Max(logArea.height, filtered.Count * 18f + 10f);
            Rect viewRect = new Rect(0, 0, logArea.width - 16f, contentH);

            scrollPos = GUI.BeginScrollView(logArea, scrollPos, viewRect, false, true);
            float curY = 4f;
            foreach (var m in filtered)
            {
                string line = FormatMessage(m);
                msgStyle.normal.textColor = m.color;
                GUI.Label(new Rect(2f, curY, viewRect.width, 18f), line, msgStyle);
                curY += 18f;
            }
            GUI.EndScrollView();

            // Chat input bar at bottom when active
            if (InputActive)
            {
                float inputY = chatY + chatH + 4f;
                float inputH = 26f;

                GUI.Box(new Rect(chatX, inputY, chatW, inputH), GUIContent.none, boxStyle);
                GUI.Label(new Rect(chatX + 6f, inputY + 3f, 48f, 20f), "[Say]:", prefixStyle);

                GUI.SetNextControlName("ChatInputField");
                InputText = GUI.TextField(new Rect(chatX + 54f, inputY + 2f, chatW - 110f, 22f), InputText, 120, inputStyle);

                if (focusRequested)
                {
                    GUI.FocusControl("ChatInputField");
                    focusRequested = false;
                }

                if (GUI.Button(new Rect(chatX + chatW - 52f, inputY + 2f, 48f, 22f), "Send", activeTabStyle))
                {
                    SubmitChat();
                }
            }
        }

        public void ToggleHistory()
        {
            HistoryVisible = !HistoryVisible;
            if (!HistoryVisible && InputActive) CancelChat();
        }

        List<ChatEntry> GetFilteredMessages()
        {
            if (CurrentTab == ChatTab.All) return messages;

            var list = new List<ChatEntry>();
            foreach (var m in messages)
            {
                if (CurrentTab == ChatTab.Combat && (m.channel == Channel.Combat || m.channel == Channel.Loot))
                    list.Add(m);
                else if (CurrentTab == ChatTab.General && m.channel != Channel.Combat)
                    list.Add(m);
            }
            return list;
        }

        string FormatMessage(ChatEntry m)
        {
            string timeStr = $"[{m.FormattedTime}] ";
            return m.channel switch
            {
                Channel.Say => $"{timeStr}[Say] [{m.sender}]: {m.text}",
                Channel.Yell => $"{timeStr}[Yell] [{m.sender}]: {m.text}",
                Channel.Party => $"{timeStr}[Party] [{m.sender}]: {m.text}",
                Channel.Guild => $"{timeStr}[Guild] [{m.sender}]: {m.text}",
                Channel.Emote => $"{timeStr}* {m.text}",
                Channel.Combat => $"{timeStr}{m.text}",
                Channel.Loot => $"{timeStr}{m.text}",
                _ => $"{timeStr}[System]: {m.text}"
            };
        }

        void InitStyles()
        {
            if (boxStyle != null) return;

            bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.07f, 0.72f));
            bgTex.Apply();

            tabActiveTex = new Texture2D(1, 1);
            tabActiveTex.SetPixel(0, 0, new Color(0.24f, 0.20f, 0.12f, 0.95f));
            tabActiveTex.Apply();

            boxStyle = new GUIStyle();
            boxStyle.normal.background = bgTex;

            tabStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            tabStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            activeTabStyle = new GUIStyle(tabStyle)
            {
                fontStyle = FontStyle.Bold
            };
            activeTabStyle.normal.background = tabActiveTex;
            activeTabStyle.normal.textColor = new Color(1.0f, 0.82f, 0.25f);

            msgStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = false,
                alignment = TextAnchor.MiddleLeft
            };

            prefixStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            prefixStyle.normal.textColor = new Color(1.0f, 0.85f, 0.35f);

            inputStyle = new GUIStyle(GUI.skin.textField)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            inputStyle.normal.textColor = Color.white;
        }
    }
}
