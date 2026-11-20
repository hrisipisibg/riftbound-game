using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD : MonoBehaviour
    {
        public PlayerController player;
        public static bool MenuOpen { get; set; }
        string previousMessage;
        float messageUntil;
        GUIStyle title, label, small, nameplate;
        GUIStyle questBody, questHeading, questButton;
        GUIStyle microBtnStyle, tooltipStyle, badgeStyle;
        static bool questTrackerCollapsed = false;
        static bool showHelpOverlay = false;
        string activeMicroTip = "";
        Rect activeMicroTipRect;
        Texture2D white;
        const string PointerXPref = "Riftbound.QuestPointerX";
        const string PointerYPref = "Riftbound.QuestPointerY";
        const float PointerWidth = 242f, PointerHeight = 120f, PointerMargin = 8f;
        static PrototypeHUD pointerOwner;
        Texture2D questArrow;
        GUIStyle pointerCaptionStyle, pointerDetailStyle;
        Vector2 pointerAnchor, pointerDragOffset;
        bool pointerPositionLoaded;
        int pointerDragControl;
        void Start()
        {
            // Region discovery has a top-screen ZoneBanner; legacy 3D floating captions duplicate it.
            foreach (var text in FindObjectsByType<TextMesh>(FindObjectsSortMode.None))
            {
                if (text.GetComponentInParent<TownNPC>() || text.GetComponentInParent<Merchant>() || text.GetComponentInParent<QuestGiver>() || text.GetComponent<FloatingCombatNumber>())
                    continue;
                var renderer = text.GetComponent<MeshRenderer>(); if (renderer) renderer.enabled = false;
                var billboard = text.GetComponent<WorldLabel>(); if (billboard) billboard.enabled = false;
                text.gameObject.SetActive(false);
            }
            ExplorationSites.Ensure();
            RiverReaverCamp.Ensure();
            if (player) WesternRoadCamps.Ensure(player);
            BrassHollows.Ensure();
            RoadPatrolGuard.Ensure();
            if (!ActionBarSystem.Instance && !GetComponent<ActionBarSystem>()) gameObject.AddComponent<ActionBarSystem>();
            if (!CathedralBossEncounter.Instance && !GetComponent<CathedralBossEncounter>()) gameObject.AddComponent<CathedralBossEncounter>();
            if (!ApexBossEncounter.Instance && !GetComponent<ApexBossEncounter>()) gameObject.AddComponent<ApexBossEncounter>();
            if (!ZoneAudioAmbience.Instance && !GetComponent<ZoneAudioAmbience>()) gameObject.AddComponent<ZoneAudioAmbience>();
            if (!PetCompanionSystem.Instance && !GetComponent<PetCompanionSystem>()) gameObject.AddComponent<PetCompanionSystem>();
            if (!ProfessionSystem.Instance && !GetComponent<ProfessionSystem>()) gameObject.AddComponent<ProfessionSystem>();
            if (!AuctionHouseSystem.Instance && !GetComponent<AuctionHouseSystem>()) gameObject.AddComponent<AuctionHouseSystem>();
            if (!WorldBossEncounter.Instance && !GetComponent<WorldBossEncounter>()) gameObject.AddComponent<WorldBossEncounter>();
            if (!MacroSystem.Instance && !GetComponent<MacroSystem>()) gameObject.AddComponent<MacroSystem>();
            if (!SettingsSystem.Instance && !GetComponent<SettingsSystem>()) gameObject.AddComponent<SettingsSystem>();
            if (!RestedXPSystem.Instance && !GetComponent<RestedXPSystem>()) gameObject.AddComponent<RestedXPSystem>();
            if (!DungeonFinderSystem.Instance && !GetComponent<DungeonFinderSystem>()) gameObject.AddComponent<DungeonFinderSystem>();
            if (!ArchaeologySystem.Instance && !GetComponent<ArchaeologySystem>()) gameObject.AddComponent<ArchaeologySystem>();
            if (!TransmogSystem.Instance && !GetComponent<TransmogSystem>()) gameObject.AddComponent<TransmogSystem>();
            if (!FishingSystem.Instance && !GetComponent<FishingSystem>()) gameObject.AddComponent<FishingSystem>();
            if (!CalendarEventSystem.Instance && !GetComponent<CalendarEventSystem>()) gameObject.AddComponent<CalendarEventSystem>();
            if (!WeatherSystem.Instance && !GetComponent<WeatherSystem>()) gameObject.AddComponent<WeatherSystem>();
            if (!PetBattleSystem.Instance && !GetComponent<PetBattleSystem>()) gameObject.AddComponent<PetBattleSystem>();
            if (!BarberShopSystem.Instance && !GetComponent<BarberShopSystem>()) gameObject.AddComponent<BarberShopSystem>();
            if (!WorldPvPSystem.Instance && !GetComponent<WorldPvPSystem>()) gameObject.AddComponent<WorldPvPSystem>();
            if (!EquipmentSetSystem.Instance && !GetComponent<EquipmentSetSystem>()) gameObject.AddComponent<EquipmentSetSystem>();
            if (!DamageMeterSystem.Instance && !GetComponent<DamageMeterSystem>()) gameObject.AddComponent<DamageMeterSystem>();
            if (!GlyphSystem.Instance && !GetComponent<GlyphSystem>()) gameObject.AddComponent<GlyphSystem>();
            if (!GemSocketSystem.Instance && !GetComponent<GemSocketSystem>()) gameObject.AddComponent<GemSocketSystem>();
            if (!GetComponent<TargetSelectionRing>()) gameObject.AddComponent<TargetSelectionRing>();
            if (!GetComponent<AmbientParticles>()) gameObject.AddComponent<AmbientParticles>();
            if (!GetComponent<CombatStreakSystem>()) gameObject.AddComponent<CombatStreakSystem>();
            if (!GetComponent<WorldShrineSystem>()) gameObject.AddComponent<WorldShrineSystem>();
            if (!GetComponent<TargetFrameHUD>()) gameObject.AddComponent<TargetFrameHUD>();
            if (!GetComponent<LivingWorldEvents>()) gameObject.AddComponent<LivingWorldEvents>();
        }
        void OnGUI()
        {
            if (!player || !player.Stats || FrontendMenu.IsOpen)
            {
                FinishPointerDrag();
                return;
            }
            if (white == null)
            {
                white = Texture2D.whiteTexture;
                title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
                title.normal.textColor = new Color(.78f, .69f, .49f);
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                label.normal.textColor = new Color(.92f, .94f, .91f);
                small = new GUIStyle(label) { fontSize = 13 };
                nameplate = new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                questBody = new GUIStyle(label) { fontSize = 17 };
                questBody.normal.textColor = new Color(.19f, .12f, .065f);
                questHeading = new GUIStyle(questBody) { fontSize = 22, fontStyle = FontStyle.Bold };
                WowTheme.StyleButton(GUI.skin.button);
                questButton = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
                microBtnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(0, 0, 0, 0)
                };
                tooltipStyle = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                tooltipStyle.normal.textColor = new Color(1f, 0.88f, 0.35f);
                badgeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 9,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                badgeStyle.normal.textColor = new Color(0.1f, 0.1f, 0.1f);
            }
            var oldMatrix = GUI.matrix;
            float lowHpRatio = player.Stats.maxHealth > 0f ? player.Stats.Health / player.Stats.maxHealth : 1f;
            if (player.Stats.Alive && lowHpRatio < 0.30f)
            {
                float severity = 1f - (lowHpRatio / 0.30f);
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 5.5f);
                float alpha = Mathf.Clamp01((0.12f + 0.24f * severity) * pulse);
                var prevColor = GUI.color;
                GUI.color = new Color(0.75f, 0.04f, 0.04f, alpha);
                float edge = 28f;
                GUI.DrawTexture(new Rect(0, 0, Screen.width, edge), white);
                GUI.DrawTexture(new Rect(0, Screen.height - edge, Screen.width, edge), white);
                GUI.DrawTexture(new Rect(0, edge, edge, Screen.height - edge * 2f), white);
                GUI.DrawTexture(new Rect(Screen.width - edge, edge, edge, Screen.height - edge * 2f), white);
                GUI.color = prevColor;
            }
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                var stats = enemy.GetComponent<Combatant>();
                if (!stats.Alive || !Camera.main || Vector3.Distance(player.transform.position, enemy.transform.position) > 16) continue;
                if (player.Target != stats && (stats.Health >= stats.maxHealth || Time.time - stats.LastHit > 6)) continue;
                var point = Camera.main.WorldToScreenPoint(enemy.transform.position + Vector3.up * 2.6f);
                if (point.z <= 0) continue;
                var rect = new Rect(point.x - 45, Screen.height - point.y, 90, 7);
                Bar(rect, stats.Health / stats.maxHealth, new Color(.8f, .23f, .12f));
                if (!enemy.trainingDummy && (player.Target == stats || Vector3.Distance(player.transform.position, enemy.transform.position) < 10))
                {
                    int enemyLevel = enemy.isBoss ? 5 : enemy.heavyAttacker ? 4 : enemy.campGuard ? 3 : 2;
                    int levelDiff = enemyLevel - player.Level;
                    Color tagColor = levelDiff <= -3 ? new Color(0.65f, 0.65f, 0.65f) :
                                     levelDiff <= -1 ? new Color(0.2f, 0.85f, 0.25f) :
                                     levelDiff == 0 ? new Color(0.95f, 0.90f, 0.2f) :
                                     levelDiff <= 2 ? new Color(1f, 0.55f, 0.1f) :
                                     new Color(0.9f, 0.15f, 0.15f);
                    nameplate.normal.textColor = tagColor;
                    string affixTag = enemy.Affix != EnemyController.EnemyAffix.None ? $" · {enemy.Affix}" : "";
                    string tagText = enemy.isBoss ? $"[Lv {enemyLevel} Boss{affixTag}] {stats.displayName}" : $"[Lv {enemyLevel}{affixTag}] {stats.displayName}";
                    GUI.Label(new Rect(point.x - 100, rect.y - 25, 200, 23), tagText, nameplate);
                }
            }
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var hudMatrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            GUI.matrix = hudMatrix;
            float width = Screen.width / scale, height = Screen.height / scale;
            int pointerId = GUIUtility.GetControlID(FocusType.Passive);
            if (Event.current.type != EventType.Repaint) DrawQuestPointer(width, height, pointerId);
            activeMicroTip = "";

            // --- 1. Low-Profile Player Unit Frame (WoW Style: Portrait ring + Name/Title + HP + Resource + XP) ---
            float pfx = 24f, pfy = 20f, pfw = 224f, pfh = 52f;
            WowTheme.Panel(new Rect(pfx - 3, pfy - 3, pfw + 6, pfh + 6));
            Fill(new Rect(pfx, pfy, pfw, 1.5f), new Color(.66f, .55f, .33f, .85f)); // Gold top trim
            Fill(new Rect(pfx, pfy + pfh - 1f, pfw, 1f), new Color(0.35f, 0.30f, 0.15f, 0.8f));

            // Class Icon Portrait Badge
            float portSize = 36f;
            Fill(new Rect(pfx + 6f, pfy + 8f, portSize, portSize), new Color(0.12f, 0.15f, 0.20f, 0.95f));
            string classIcon = string.IsNullOrEmpty(player.CharacterClass) ? "?" : player.CharacterClass.Substring(0, 1);
            var portStyle = new GUIStyle(title) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(pfx + 6f, pfy + 7f, portSize, portSize), classIcon, portStyle);

            // Level Circle Badge (Overlapping bottom-right of portrait)
            Fill(new Rect(pfx + portSize - 4f, pfy + portSize - 4f, 16f, 15f), new Color(0.88f, 0.72f, 0.25f, 1f));
            GUI.Label(new Rect(pfx + portSize - 4f, pfy + portSize - 5f, 16f, 15f), $"{player.Level}", badgeStyle);

            // Formatted Name & Title
            float textX = pfx + portSize + 12f;
            float barW = pfw - portSize - 18f;
            string charName = player.CharacterName;
            GUI.Label(new Rect(textX, pfy + 3f, barW, 16f), charName, label);

            // Heroic Difficulty Badge on Player Frame
            if (DungeonInstanceManager.Instance && DungeonInstanceManager.Instance.CurrentDifficulty == DungeonInstanceManager.DungeonDifficulty.Heroic)
            {
                Fill(new Rect(pfx + pfw - 62f, pfy - 14f, 62f, 13f), new Color(0.6f, 0.12f, 0.12f, 0.95f));
                GUI.Label(new Rect(pfx + pfw - 62f, pfy - 15f, 62f, 13f), "HEROIC", badgeStyle);
            }

            // Health Bar (13px)
            float hpPct = player.Stats.Health / player.Stats.maxHealth;
            Bar(new Rect(textX, pfy + 21f, barW, 13f), hpPct, new Color(.30f, .57f, .43f));
            GUI.Label(new Rect(textX, pfy + 19f, barW, 15f), $"{Mathf.CeilToInt(player.Stats.Health)} / {player.Stats.maxHealth}", small);

            // Resource Bar (9px)
            Color resColor = player.CharacterClass == "Warrior" ? new Color(0.85f, 0.18f, 0.12f) :
                             player.CharacterClass == "Mage" ? new Color(0.18f, 0.48f, 0.95f) :
                             player.CharacterClass == "Rogue" ? new Color(0.95f, 0.85f, 0.15f) :
                             new Color(1f, 0.85f, 0.25f);
            float resRatio = Mathf.Clamp01(player.ClassResource / Mathf.Max(1f, player.MaxClassResource));
            Bar(new Rect(textX, pfy + 36f, barW, 9f), resRatio, resColor);

            // Tooltip on Player Frame Hover (Shows Gold & XP Details)
            if (new Rect(pfx, pfy, pfw, pfh).Contains(Event.current.mousePosition))
            {
                activeMicroTip = $"<b>{charName}</b>\nLevel {player.Level} {player.CharacterRace} {player.CharacterClass}\nGold: {player.Gold}  ·  XP: {player.Experience}/{player.NextLevelExperience}";
                activeMicroTipRect = new Rect(Event.current.mousePosition.x + 12f, Event.current.mousePosition.y + 12f, 210f, 48f);
            }

            // --- 2. Low-Profile Target Unit Frame (Directly adjacent to Player Frame) ---
            if (player.Target)
            {
                float tfx = pfx + pfw + 10f, tfy = pfy, tfw = 205f, tfh = 52f;
                WowTheme.Panel(new Rect(tfx - 3, tfy - 3, tfw + 6, tfh + 6));
                Fill(new Rect(tfx, tfy, tfw, 1.5f), new Color(0.85f, 0.35f, 0.28f, 0.95f)); // Crimson top trim
                Fill(new Rect(tfx, tfy + tfh - 1f, tfw, 1f), new Color(0.35f, 0.15f, 0.15f, 0.8f));

                var enemyComp = player.Target.GetComponentInParent<EnemyController>();
                int targetLvl = enemyComp ? (enemyComp.isBoss ? 5 : enemyComp.heavyAttacker ? 4 : enemyComp.campGuard ? 3 : 2) : 1;
                string targetIcon = enemyComp && enemyComp.isBoss ? "B" : "·";

                // Target Portrait Icon
                Fill(new Rect(tfx + 6f, tfy + 8f, portSize, portSize), new Color(0.22f, 0.10f, 0.10f, 0.95f));
                GUI.Label(new Rect(tfx + 6f, tfy + 7f, portSize, portSize), targetIcon, portStyle);

                // Target Level Badge
                Fill(new Rect(tfx + portSize - 4f, tfy + portSize - 4f, 16f, 15f), new Color(0.85f, 0.22f, 0.18f, 1f));
                GUI.Label(new Rect(tfx + portSize - 4f, tfy + portSize - 5f, 16f, 15f), $"{targetLvl}", badgeStyle);

                // Target Name & Raid Marker
                float ttextX = tfx + portSize + 12f;
                float tbarW = tfw - portSize - 18f;
                var tMarker = PartySystem.GetMarker(player.Target);
                string tNameText = tMarker != RaidTargetMarker.None ? $"{PartySystem.GetMarkerGlyph(tMarker)} {player.Target.displayName}" : player.Target.displayName;
                GUI.Label(new Rect(ttextX, tfy + 3f, tbarW - 24f, 16f), tNameText, label);

                // Target Health Bar
                float thpPct = player.Target.Health / player.Target.maxHealth;
                Bar(new Rect(ttextX, tfy + 21f, tbarW, 13f), thpPct, new Color(0.85f, 0.22f, 0.16f));
                GUI.Label(new Rect(ttextX, tfy + 19f, tbarW, 15f), $"{Mathf.CeilToInt(player.Target.Health)} / {player.Target.maxHealth}", small);

                // Target Debuffs (Auras)
                if (BuffSystem.Instance)
                {
                    float debuffY = (CastBarSystem.Instance && CastBarSystem.IsTargetCasting(player.Target)) ? tfy + tfh + 24f : tfy + tfh + 6f;
                    BuffSystem.Instance.DrawTargetDebuffsGUI(player.Target, tfx, debuffY);
                }

                // Target Duel Button
                var duelSys = DuelSystem.Instance ?? UnityEngine.Object.FindAnyObjectByType<DuelSystem>();
                if (duelSys && duelSys.State == DuelSystem.DuelState.Inactive && player.Target.Alive && player.Target != player.Stats)
                {
                    if (GUI.Button(new Rect(tfx + tfw + 4f, tfy + 12f, 54f, 26f), "DUEL", small))
                    {
                        duelSys.Challenge(player.Target, out _);
                    }
                }
            }

            // Keep the world visible: show only active objectives, with details in the journal.
            float qtw = 236f, qtx = width - qtw - 20f;
            float qty = (MinimapRadar.Instance != null && MinimapRadar.Instance.showMinimap) ? MinimapRadar.TrackerTop : 20f;
            var fqs = FactionQuestSystem.Instance;
            var fq = fqs != null ? fqs.GetActiveQuest() : null;
            bool hasObjective = fq != null || player.QuestAccepted;
            if (hasObjective)
            {
                Fill(new Rect(qtx, qty, qtw, 28f), new Color(.055f, .065f, .08f, .84f));
                Fill(new Rect(qtx, qty, 2f, 28f), new Color(.66f, .55f, .33f, .85f));
                if (GUI.Button(new Rect(qtx + 10f, qty + 3f, qtw - 18f, 23f), questTrackerCollapsed ? "OBJECTIVES  +" : "OBJECTIVES  -", small))
                {
                    questTrackerCollapsed = !questTrackerCollapsed;
                    AudioEffects.PlayClick();
                }
                if (!questTrackerCollapsed)
                {
                    float rows = fq != null && player.QuestAccepted ? 2f : 1f;
                    Fill(new Rect(qtx, qty + 28f, qtw, rows * 58f + 8f), new Color(.045f, .055f, .07f, .72f));
                    float objectiveY = qty + 35f;
                    if (fq != null)
                    {
                        GUI.Label(new Rect(qtx + 12f, objectiveY, qtw - 24f, 22f), fq.title, label);
                        string objective = fqs.IsActiveQuestReady() ? "Return to " + fq.giverNpc : $"{fq.targetName}: {fqs.CurrentProgress}/{fq.requiredCount}";
                        GUI.Label(new Rect(qtx + 12f, objectiveY + 23f, qtw - 24f, 32f), objective, small);
                        objectiveY += 58f;
                    }
                    if (player.QuestAccepted)
                    {
                        GUI.Label(new Rect(qtx + 12f, objectiveY, qtw - 24f, 22f), player.QuestName, label);
                        GUI.Label(new Rect(qtx + 12f, objectiveY + 23f, qtw - 24f, 32f), player.QuestObjective, small);
                    }
                }
            }

            if (!player.DialogueOpen)
            {
                if (ActionBarSystem.Instance)
                {
                    ActionBarSystem.Instance.DrawActionBar(player, width, height);
                }
                else
                {
                    Panel(new Rect(width / 2 - 300, height - 105, 600, 85));
                    var ab1 = ClassSystem.GetAbilityInfo(player.CharacterClass, 1);
                    var ab2 = ClassSystem.GetAbilityInfo(player.CharacterClass, 2);
                    var ab3 = ClassSystem.GetAbilityInfo(player.CharacterClass, 3);
                    GUI.Label(new Rect(width / 2 - 280, height - 98, 560, 24), $"[1 / LMB] {ab1.name}   [2] {ab2.name}   [3] {ab3.name}", label);
                    GUI.Label(new Rect(width / 2 - 280, height - 70, 560, 25), $"[2] {Cooldown(player.CleaveReady)}   [3] {Cooldown(player.HealReady)}   [4] Potion x{player.Potions}   ·   {player.ClassResourceName}: {Mathf.RoundToInt(player.ClassResource)}", small);
                }

                // Centered Notification Toast
                if (previousMessage != player.Message)
                {
                    previousMessage = player.Message;
                    messageUntil = Time.unscaledTime + 4f;
                }
                if (!string.IsNullOrEmpty(player.Message) && Time.unscaledTime < messageUntil)
                {
                    float msgW = Mathf.Min(580f, width - 120f);
                    float msgX = width / 2f - msgW / 2f;
                    float msgY = height - 124f;
                    Fill(new Rect(msgX - 6f, msgY - 3f, msgW + 12f, 42f), new Color(0.04f, 0.06f, 0.08f, .65f));
                    var centerMsg = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
                    GUI.Label(new Rect(msgX, msgY, msgW, 36f), player.Message, centerMsg);
                }

                DrawUtilityMenu(width, height);
            }
            if (!player.DialogueOpen && !player.ShopOpen && !player.AnyBlockingWindowOpen())
            {
                if (player.TryGetBestInteraction(out var interaction, out _))
                    DrawInteractionBadge("E", interaction.text, width, height);
            }
            if (player.DialogueOpen)
            {
                if (player.ActiveQuestGiver) DrawQuestWindow(height);
                else if (player.ActiveTownNPC) DrawTownNPCGossip(width, height);
            }
            if (player.InventoryOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
            {
                DrawEquipment(width, height);
            }
            if (player.TalentsOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
            {
                DrawTalentWindow(width, height);
            }
            if (player.SpellbookOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
            {
                DrawSpellbook(width, height);
            }
            if (AchievementSystem.Instance)
            {
                AchievementSystem.Instance.DrawToastGUI(width, height);
                if (player.AchievementOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
                {
                    AchievementSystem.Instance.DrawAchievementsWindow(width, height);
                }
            }
            if (ReputationSystem.Instance && player.ReputationOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
            {
                ReputationSystem.Instance.DrawReputationWindow(width, height);
            }
            if (EncounterJournal.Instance && player.EncounterJournalOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
            {
                EncounterJournal.Instance.DrawEncounterJournalGUI(width, height);
            }
            if (ProfessionSystem.Instance && ProfessionSystem.IsOpen && !player.DialogueOpen && !(player.orbit && player.orbit.Paused))
            {
                ProfessionSystem.Instance.DrawProfessionsGUI(width, height);
            }
            if (AuctionHouseSystem.Instance && !(player.orbit && player.orbit.Paused))
            {
                AuctionHouseSystem.Instance.DrawAuctionGUI(width, height);
                AuctionHouseSystem.Instance.DrawMailboxGUI(width, height);
            }
            if (WorldBossEncounter.Instance)
            {
                WorldBossEncounter.Instance.DrawBossHealthBar(width, height);
            }
            if (AuctionHouseSystem.Instance && AuctionHouseSystem.Instance.HasUnreadMail())
            {
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(1.0f, 0.85f, 0.2f, 0.95f);
                if (GUI.Button(new Rect(width - 282, 20, 60, 24), "Mail", small))
                {
                    AuctionHouseSystem.MailboxOpen = !AuctionHouseSystem.MailboxOpen;
                }
                GUI.backgroundColor = prevColor;
            }
            DrawAdventurePanels(width, height);
            if (!player.Stats.Alive || (player.orbit && player.orbit.Paused))
            {
                Panel(new Rect(width / 2 - 230, height / 2 - 70, 460, 240));
                GUI.Label(new Rect(width / 2 - 205, height / 2 - 50, 410, 40), player.Stats.Alive ? "PAUSED" : "YOU HAVE FALLEN", title);
                GUI.Label(new Rect(width / 2 - 205, height / 2, 410, 50), player.Stats.Alive ? "Press ESC to resume." : "Press R to recover at the outpost.", label);
                if (player.Stats.Alive && GUI.Button(new Rect(width / 2 - 205, height / 2 + 53, 195, 35), "Save Progress", questButton)) player.SaveAdventure();
                if (player.Stats.Alive && GUI.Button(new Rect(width / 2 + 10, height / 2 + 53, 195, 35), "Resume", questButton)) player.orbit.SetPaused(false);
                if (player.Stats.Alive && GUI.Button(new Rect(width / 2 - 205, height / 2 + 100, 410, 35), "Save & Return to Main Menu", questButton)) player.GetComponent<FrontendMenu>().ReturnToMenu();
            }

            if (MinimapRadar.Instance) MinimapRadar.Instance.DrawRadarGUI(width, height);
            if (ZoneBanner.Instance) ZoneBanner.Instance.DrawBannerGUI(width, height);
            if (ChatSystem.Instance) ChatSystem.Instance.DrawChatGUI(width, height);
            if (LootDropSystem.Instance) LootDropSystem.Instance.DrawLootGUI(width, height);
            if (CombatTextManager.Instance) CombatTextManager.Instance.DrawCombatTextGUI();
            if (PartySystem.Instance) PartySystem.Instance.DrawPartyGUI(width, height);
            if (BuffSystem.Instance) BuffSystem.Instance.DrawBuffsGUI(width, height);
            if (CastBarSystem.Instance) CastBarSystem.Instance.DrawCastBarGUI(width, height);
            if (DuelSystem.Instance) DuelSystem.Instance.DrawDuelGUI(width, height);
            if (LootRollSystem.Instance) LootRollSystem.Instance.DrawLootRollGUI(width, height);
            if (MailboxSystem.Instance) MailboxSystem.Instance.DrawMailboxGUI(width, height);
            if (BattlegroundSystem.Instance) BattlegroundSystem.Instance.DrawBattlegroundGUI(width, height);
            if (GuildSystem.Instance) GuildSystem.Instance.DrawGuildGUI(width, height);
            if (CharacterSheetSystem.Instance) CharacterSheetSystem.Instance.DrawCharacterSheetGUI(width, height);
            if (MacroSystem.Instance) MacroSystem.Instance.DrawMacroGUI(width, height);
            if (SettingsSystem.Instance) SettingsSystem.Instance.DrawSettingsGUI(width, height);
            if (DungeonFinderSystem.Instance) DungeonFinderSystem.Instance.DrawDungeonFinderGUI(width, height);
            if (ArchaeologySystem.Instance) ArchaeologySystem.Instance.DrawArchaeologyGUI(width, height);
            if (TransmogSystem.Instance) TransmogSystem.Instance.DrawTransmogGUI(width, height);
            if (FishingSystem.Instance) FishingSystem.Instance.DrawFishingGUI(width, height);
            if (CalendarEventSystem.Instance) CalendarEventSystem.Instance.DrawCalendarGUI(width, height);
            if (PetBattleSystem.Instance) PetBattleSystem.Instance.DrawPetSystemGUI(width, height);
            if (BarberShopSystem.Instance) BarberShopSystem.Instance.DrawBarberShopGUI(width, height);
            if (WorldPvPSystem.Instance) WorldPvPSystem.Instance.DrawWorldPvPGUI(width, height);
            if (EquipmentSetSystem.Instance) EquipmentSetSystem.Instance.DrawEquipmentSetGUI(width, height);
            if (DamageMeterSystem.Instance) DamageMeterSystem.Instance.DrawDamageMeterGUI(width, height);
            if (GlyphSystem.Instance) GlyphSystem.Instance.DrawGlyphGUI(width, height);
            if (GemSocketSystem.Instance) GemSocketSystem.Instance.DrawGemSocketGUI(width, height);

            if (FlightMaster.ActiveFlightMaster != null && FlightMaster.ActiveFlightMaster.TaxiMapOpen)
                FlightMaster.ActiveFlightMaster.DrawTaxiMapGUI(width, height);
            else if (FlightMaster.Instance != null && FlightMaster.Instance.TaxiMapOpen)
                FlightMaster.Instance.DrawTaxiMapGUI(width, height);
            if (CathedralBossEncounter.Instance) CathedralBossEncounter.Instance.DrawBossFrameGUI(width, height);
            if (ApexBossEncounter.Instance) ApexBossEncounter.Instance.DrawBossFrameGUI(width, height);
            if (PetCompanionSystem.Instance) PetCompanionSystem.Instance.DrawPetFrameGUI(width, height);

            if (Event.current.type == EventType.Repaint)
            {
                GUI.matrix = hudMatrix;
                DrawQuestPointer(width, height, pointerId);
            }
            GUI.matrix = oldMatrix;
        }
        void OnDestroy()
        {
            MenuOpen = false;
            if (pointerOwner == this) pointerOwner = null;
            if (questArrow) Destroy(questArrow);
            if (atlasRelief) Destroy(atlasRelief);
        }

        static bool PointerContains(Rect rect, Vector2 point) =>
            rect.Contains(point) && (point.y >= rect.y + 76f || Mathf.Abs(point.x - rect.center.x) <= 38f);

        bool TryGetPointerDestination(out Vector3 target, out string caption)
        {
            target = default;
            caption = null;
            var radar = MinimapRadar.Instance;
            return player && player.Stats && player.Stats.Alive && !FrontendMenu.IsOpen && !MenuOpen &&
                !player.AnyBlockingWindowOpen() && !(player.orbit && player.orbit.Paused) &&
                radar && radar.TryGetNavigationTarget(out target, out caption);
        }

        Rect QuestPointerRect(float width, float height)
        {
            if (!pointerPositionLoaded)
            {
                pointerAnchor = new Vector2(Mathf.Clamp01(PlayerPrefs.GetFloat(PointerXPref, .5f)),
                    Mathf.Clamp01(PlayerPrefs.GetFloat(PointerYPref, .035f)));
                pointerPositionLoaded = true;
            }
            return new Rect(PointerMargin + pointerAnchor.x * (width - PointerWidth - 2 * PointerMargin),
                PointerMargin + pointerAnchor.y * (height - PointerHeight - 2 * PointerMargin), PointerWidth, PointerHeight);
        }

        // The camera and target selection read mouse input independently of IMGUI.
        internal static bool QuestPointerTakesMouse(PlayerController source, Vector2 screenPoint, bool leftPress)
        {
            if (!pointerOwner || pointerOwner.player != source ||
                (!leftPress && pointerOwner.pointerDragControl == 0) ||
                !pointerOwner.TryGetPointerDestination(out _, out _))
                return false;
            if (pointerOwner.pointerDragControl != 0) return true;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0f) return false;
            var guiPoint = new Vector2(screenPoint.x, Screen.height - screenPoint.y) / scale;
            return PointerContains(pointerOwner.QuestPointerRect(Screen.width / scale, Screen.height / scale), guiPoint);
        }

        void FinishPointerDrag()
        {
            if (pointerDragControl == 0) return;
            if (GUIUtility.hotControl == pointerDragControl) GUIUtility.hotControl = 0;
            pointerDragControl = 0;
            PlayerPrefs.SetFloat(PointerXPref, pointerAnchor.x);
            PlayerPrefs.SetFloat(PointerYPref, pointerAnchor.y);
            PlayerPrefs.Save();
        }

        void DrawQuestPointer(float width, float height, int id)
        {
            if (!TryGetPointerDestination(out var target, out var caption))
            {
                FinishPointerDrag();
                return;
            }

            pointerOwner = this;
            var rect = QuestPointerRect(width, height);
            var input = Event.current;
            if (pointerDragControl != 0 && (GUIUtility.hotControl != pointerDragControl || !Application.isFocused))
                FinishPointerDrag();

            if (pointerDragControl != 0)
            {
                if (input.type == EventType.MouseDrag)
                {
                    Vector2 origin = input.mousePosition - pointerDragOffset;
                    pointerAnchor = new Vector2(
                        Mathf.InverseLerp(PointerMargin, width - PointerWidth - PointerMargin, origin.x),
                        Mathf.InverseLerp(PointerMargin, height - PointerHeight - PointerMargin, origin.y));
                    input.Use();
                }
                else if (input.type == EventType.MouseUp && input.button == 0)
                {
                    FinishPointerDrag();
                    input.Use();
                }
                else if (input.type == EventType.Ignore) FinishPointerDrag();
            }
            else if (input.type == EventType.MouseDown && input.button == 0 &&
                GUIUtility.hotControl == 0 && PointerContains(rect, input.mousePosition))
            {
                pointerDragOffset = input.mousePosition - rect.position;
                pointerDragControl = GUIUtility.hotControl = id;
                input.Use();
            }
            if (input.type != EventType.Repaint) return;

            if (WorldRegions.Expanded && WorldRegions.Underground(target) && !WorldRegions.Underground(player.transform.position))
                target = WorldRegions.Cathedral;
            Vector3 offset = target - player.transform.position;
            offset.y = 0;
            float distance = offset.magnitude;
            var camera = Camera.main;
            float heading = camera ? camera.transform.eulerAngles.y : player.transform.eulerAngles.y;
            float bearing = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg - heading;
            bool arrived = distance <= 3f;
            Color accent = arrived ? new Color(.57f, .98f, .81f) : Color.Lerp(
                new Color(.51f, .96f, .77f), new Color(1f, .72f, .34f),
                Mathf.InverseLerp(20f, 150f, Mathf.Abs(Mathf.DeltaAngle(0f, bearing))));
            var icon = new Vector2(rect.center.x, rect.y + 38f);
            var matrix = GUI.matrix;
            // Draw at the local origin so the rotation cannot move the HUD anchor.
            GUI.matrix = matrix * Matrix4x4.TRS(new Vector3(icon.x, icon.y, 0f),
                Quaternion.Euler(0f, 0f, arrived ? 45f : bearing), Vector3.one);
            if (arrived)
            {
                Fill(new Rect(-12f, -12f, 24f, 24f), new Color(.05f, .12f, .13f));
                Fill(new Rect(-9f, -9f, 18f, 18f), accent);
            }
            else
            {
                if (!questArrow) questArrow = CreateQuestArrow();
                var tint = GUI.color;
                GUI.color = accent;
                GUI.DrawTexture(new Rect(-35f, -35f, 70f, 70f), questArrow);
                GUI.color = tint;
            }
            GUI.matrix = matrix;

            var plate = new Rect(rect.x, rect.y + 76f, rect.width, 44f);
            bool hover = pointerDragControl != 0 || PointerContains(rect, input.mousePosition);
            Fill(plate, new Color(.027f, .043f, .056f, hover ? .96f : .82f));
            if (hover) Fill(new Rect(plate.center.x - 14f, plate.y, 28f, 2f), accent);
            pointerCaptionStyle ??= new GUIStyle(label)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip, fontStyle = FontStyle.Bold
            };
            pointerDetailStyle ??= new GUIStyle(small)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip
            };
            pointerCaptionStyle.normal.textColor = accent;
            GUI.Label(new Rect(plate.x + 5f, plate.y + 2f, plate.width - 10f, 21f), caption, pointerCaptionStyle);
            GUI.Label(new Rect(plate.x + 5f, plate.y + 22f, plate.width - 10f, 19f),
                arrived ? "At destination" : $"{distance:0} m · {WorldRegions.ZoneName(target)}", pointerDetailStyle);
        }

        static Texture2D CreateQuestArrow()
        {
            const int size = 96;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float lateral = Mathf.Abs((x + .5f) / size - .5f);
                // Center the painted silhouette between its tip and wing corners.
                float height = (y + .5f) / size + .036f;
                // Swept wings meet at the tip and the tail has a shallow V cut.
                float side = (.88f - height) * .55f - lateral;
                float tail = height - (.37f - lateral * .47f);
                float edge = Mathf.Min(side, tail);
                float alpha = Mathf.Clamp01(edge * size + .5f);
                float bevel = Mathf.Clamp01((edge - .022f) * size);
                float facet = (x < size / 2 ? .96f : .68f) + (height - .5f) * .35f;
                float light = Mathf.Lerp(.085f, facet, bevel);
                pixels[y * size + x] = new Color(light, light, light, alpha);
            }
            var arrow = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            arrow.SetPixels32(pixels);
            arrow.Apply(false, true);
            return arrow;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMenu() { MenuOpen = false; }

        void DrawInteractionBadge(string key, string promptText, float width, float height)
        {
            float badgeW = Mathf.Max(220f, promptText.Length * 8.5f + 64f);
            float badgeH = 34f;
            float badgeX = (width - badgeW) * 0.5f;
            float badgeY = height * 0.5f + 78f;

            // Sleek dark panel with warm gold border
            Fill(new Rect(badgeX - 1f, badgeY - 1f, badgeW + 2f, badgeH + 2f), new Color(0.35f, 0.28f, 0.14f, 0.90f));
            Fill(new Rect(badgeX, badgeY, badgeW, badgeH), new Color(0.04f, 0.055f, 0.08f, 0.99f));

            // Gold key badge
            float keyW = 28f, keyH = 22f;
            float keyX = badgeX + 6f, keyY = badgeY + 6f;
            Fill(new Rect(keyX, keyY, keyW, keyH), new Color(1f, 0.81f, 0.43f, 1f));
            var keyStyle = new GUIStyle(small) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            keyStyle.normal.textColor = new Color(0.08f, 0.06f, 0.02f);
            GUI.Label(new Rect(keyX, keyY - 1f, keyW, keyH), key, keyStyle);

            // Action label
            var textStyle = new GUIStyle(label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            textStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(keyX + keyW + 8f, badgeY + 4f, badgeW - keyW - 14f, 26f), promptText, textStyle);
        }

        void DrawUtilityMenu(float width, float height)
        {
            if (GUI.Button(new Rect(width - 98f, height - 34f, 84f, 26f), "MENU", questButton))
            {
                if (!MenuOpen) showHelpOverlay = false;
                MenuOpen = !MenuOpen;
                if (PartySystem.Instance) PartySystem.Instance.CloseCompanionMenu();
                AudioEffects.PlayClick();
            }
            if (MenuOpen && !showHelpOverlay)
            {
                float w = 420f, h = 470f;
                float x = width - w - 16f, y = height - h - 42f;
                Panel(new Rect(x, y, w, h));
                GUI.Label(new Rect(x + 18f, y + 12f, 300f, 26f), "GAME MENU", title);
                if (GUI.Button(new Rect(x + w - 38f, y + 10f, 26f, 24f), "X", questButton)) { MenuOpen = false; AudioEffects.PlayClick(); }

                var headerStyle = new GUIStyle(small) { fontStyle = FontStyle.Bold, fontSize = 11 };
                headerStyle.normal.textColor = new Color(0.86f, 0.68f, 0.27f);

                float curY = y + 42f;
                void Section(string header, (string title, System.Action action)[] items)
                {
                    GUI.Label(new Rect(x + 18f, curY, w - 36f, 16f), header.ToUpperInvariant(), headerStyle);
                    Fill(new Rect(x + 18f, curY + 16f, w - 36f, 1f), new Color(0.42f, 0.31f, 0.12f, 0.6f));
                    curY += 20f;
                    for (int i = 0; i < items.Length; i++)
                    {
                        float btnX = x + 18f + (i % 2) * 194f;
                        float btnY = curY + (i / 2) * 32f;
                        if (GUI.Button(new Rect(btnX, btnY, 190f, 28f), items[i].title, questButton))
                        {
                            MenuOpen = false;
                            items[i].action();
                            AudioEffects.PlayClick();
                        }
                    }
                    curY += Mathf.CeilToInt(items.Length / 2f) * 32f + 6f;
                }

                int talentPoints = TalentSystem.GetUnspentTalentPoints(player);
                string talentLabel = talentPoints > 0 ? $"Talents [N] ({talentPoints})" : "Talents [N]";

                Section("Character & Abilities", new[]
                {
                    ("Character [C]", (System.Action)(() => { if (CharacterSheetSystem.Instance) CharacterSheetSystem.Instance.ToggleSheet(); })),
                    ("Bags [B / I]", () => player.InventoryOpen = !player.InventoryOpen),
                    (talentLabel, () => player.TalentsOpen = !player.TalentsOpen),
                    ("Spellbook [P]", () => player.SpellbookOpen = !player.SpellbookOpen),
                });

                Section("World & Adventure", new[]
                {
                    ("World Map [M]", (System.Action)(() => player.MapOpen = !player.MapOpen)),
                    ("Quests [J]", () => player.JournalOpen = !player.JournalOpen),
                    ("Dungeon Finder", () => { if (DungeonFinderSystem.Instance) DungeonFinderSystem.Instance.ToggleWindow(); }),
                    ("Companions", () => { if (PartySystem.Instance) PartySystem.Instance.IsCompanionMenuOpen = true; }),
                });

                Section("Social & Economy", new[]
                {
                    ("Guild [G]", (System.Action)(() => { if (GuildSystem.Instance) GuildSystem.Instance.IsOpen = !GuildSystem.Instance.IsOpen; })),
                    ("Auction House", () => AuctionHouseSystem.AuctionOpen = !AuctionHouseSystem.AuctionOpen),
                    ("Mailbox", () => { if (MailboxSystem.Instance) MailboxSystem.Instance.IsOpen = !MailboxSystem.Instance.IsOpen; }),
                    ("Achievements [Y]", () => player.AchievementOpen = !player.AchievementOpen),
                });

                Section("System", new[]
                {
                    ("Settings [Esc]", (System.Action)(() => { if (SettingsSystem.Instance) SettingsSystem.Instance.OpenMenu(); })),
                    ("Controls & Hotkeys", () => { showHelpOverlay = true; MenuOpen = true; }),
                });
            }
            if (showHelpOverlay && MenuOpen)
            {
                float x = (width - 460f) * .5f, y = (height - 240f) * .5f;
                Panel(new Rect(x, y, 460f, 240f));
                GUI.Label(new Rect(x + 20f, y + 14f, 380f, 28f), "GAME CONTROLS & SHORTCUTS", title);
                GUI.Label(new Rect(x + 20f, y + 50f, 420f, 130f),
                    "<b>Movement:</b> WASD relative to camera · Left/Right arrows to turn · Space to jump · C to slow walk\n" +
                    "<b>Camera:</b> Hold Right-Click to steer · Left-Click to orbit · Wheel to zoom\n" +
                    "<b>Combat:</b> 1-3 Abilities · 4 Health Potion · F Interrupt · Q Shield Block\n" +
                    "<b>World:</b> E to Interact/Talk · Z Mount · V Cycle Mount · H Hearthstone\n" +
                    "<b>Panels:</b> C Character · B/I Bags · J Quests · M Map · N Talents · P Spells", small);
                if (GUI.Button(new Rect(x + 170f, y + 192f, 120f, 30f), "Close", questButton)) { showHelpOverlay = false; MenuOpen = false; AudioEffects.PlayClick(); }
            }
        }

        void DrawQuestWindow(float height)
        {
            var npc = player.ActiveQuestGiver;
            float x = 24, y = 150, w = 450, h = Mathf.Min(530, height - 174);
            Fill(new Rect(x, y, w, h), new Color(.45f, .31f, .13f));
            Fill(new Rect(x + 3, y + 3, w - 6, h - 6), new Color(.86f, .79f, .62f));
            Fill(new Rect(x + 3, y + 3, w - 6, 45), new Color(.12f, .14f, .11f));
            GUI.Label(new Rect(x + 19, y + 8, w - 70, 34), npc.npcName.ToUpperInvariant() + "  ·  QUESTS", title);
            if (GUI.Button(new Rect(x + w - 37, y + 11, 24, 25), "X", questButton)) player.CloseDialogue();
            var mainProfile = Narrative.NarrativeCatalog.ResolveMainQuestGiver(npc);
            var titleSel = Narrative.NarrativeCatalog.SelectText(mainProfile, "main_title", "", player);
            var bodySel = Narrative.NarrativeCatalog.SelectText(mainProfile, "main_body", "", player);
            string displayTitle = titleSel.matched && !string.IsNullOrEmpty(titleSel.text) ? titleSel.text : player.QuestName;
            string displayBody = bodySel.matched && !string.IsNullOrEmpty(bodySel.text) ? bodySel.text : player.QuestDescription;
            GUI.Label(new Rect(x + 22, y + 64, w - 44, 38), displayTitle, questHeading);
            GUI.Label(new Rect(x + 22, y + 110, w - 44, 166), displayBody, questBody);
            Fill(new Rect(x + 22, y + 286, w - 44, 1), new Color(.55f, .43f, .25f));
            GUI.Label(new Rect(x + 22, y + 301, w - 44, 30), "Quest Objectives", questHeading);
            GUI.Label(new Rect(x + 22, y + 337, w - 44, 52), player.QuestObjective, questBody);
            GUI.Label(new Rect(x + 22, y + 397, w - 44, 30), "Rewards", questHeading);
            GUI.Label(new Rect(x + 22, y + 433, w - 44, 30), player.QuestComplete ? "Rewards: Emberwatch Axe & Sigil" : $"{player.QuestGold} gold / {player.QuestExperience} XP / 1 potion", questBody);
            float bottom = y + h - 47;
            if (!player.QuestAccepted && !player.QuestComplete)
            {
                if (GUI.Button(new Rect(x + 22, bottom, 190, 32), "Accept Quest", questButton)) player.AcceptQuest();
                if (GUI.Button(new Rect(x + 235, bottom, 190, 32), "Not Now", questButton)) player.CloseDialogue();
            }
            else if (!player.QuestComplete && player.QuestReady)
            {
                if (GUI.Button(new Rect(x + 22, bottom, 240, 32), "Complete Quest", questButton)) player.CompleteQuest();
                if (GUI.Button(new Rect(x + 280, bottom, 145, 32), "Close", questButton)) player.CloseDialogue();
            }
            else if (GUI.Button(new Rect(x + 22, bottom, w - 44, 32), player.QuestComplete ? "Farewell" : "I'll return", questButton)) player.CloseDialogue();
        }
        void Fill(Rect rect, Color color) { GUI.color = color; GUI.DrawTexture(rect, white); GUI.color = Color.white; }
        string Cooldown(float end) => end <= Time.time ? "READY" : $"{Mathf.CeilToInt(end - Time.time)}s";
        void Panel(Rect rect) { WowTheme.Panel(rect); }
        void Bar(Rect rect, float fill, Color color)
        {
            GUI.color = new Color(.1f, .12f, .12f); GUI.DrawTexture(rect, white);
            rect.width *= Mathf.Clamp01(fill); GUI.color = color; GUI.DrawTexture(rect, white); GUI.color = Color.white;
        }
    }
}


