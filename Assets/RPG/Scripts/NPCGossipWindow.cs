using System;
using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        static PrototypeHUD activeGossipHud;
        int gossipMenuState = 0; // 0: Main, 1: Lore, 2: Threats, 3: Directions, 4: Offer, 5: TurnIn, 6: PublicTopic/Aftermath
        string gossipFeedback = "";
        string activeNarrativeTopicId = "";
        string activeNarrativeTopicChannel = "";
        string activeNarrativeTopicTitle = "";
        Vector2 gossipScroll = Vector2.zero;

        public static void ResetGossipState()
        {
            if (!activeGossipHud) activeGossipHud = FindFirstObjectByType<PrototypeHUD>();
            if (!activeGossipHud) return;
            activeGossipHud.gossipMenuState = 0;
            activeGossipHud.gossipFeedback = "";
            activeGossipHud.activeNarrativeTopicId = "";
            activeGossipHud.activeNarrativeTopicChannel = "";
            activeGossipHud.activeNarrativeTopicTitle = "";
            activeGossipHud.gossipScroll = Vector2.zero;
        }

        void DrawTownNPCGossip(float width, float height)
        {
            activeGossipHud = this;
            var npc = player.ActiveTownNPC;
            if (!npc) return;

            float windowW = 460f, windowH = 500f;
            float x = 24f, y = 140f;

            // Outer dark gothic panel with gold border
            Fill(new Rect(x - 4f, y - 4f, windowW + 8f, windowH + 8f), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            Fill(new Rect(x, y, windowW, windowH), new Color(0.06f, 0.08f, 0.11f, 0.98f));

            // Header Title Bar
            Fill(new Rect(x, y, windowW, 40f), new Color(0.12f, 0.15f, 0.22f, 0.98f));
            Fill(new Rect(x, y + 39f, windowW, 1.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));

            // Determine Crest Icon
            string crest = GetNPCCrest(npc);
            string npcTitle = $"{crest}  {npc.npcName.ToUpperInvariant()}";
            var headerStyle = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.40f);
            GUI.Label(new Rect(x + 14f, y + 5f, windowW - 60f, 20f), npcTitle, headerStyle);

            var subStyle = new GUIStyle(small) { fontStyle = FontStyle.Italic };
            subStyle.normal.textColor = new Color(0.65f, 0.80f, 0.95f);
            GUI.Label(new Rect(x + 14f, y + 22f, windowW - 60f, 16f), $"<{npc.roleTag}>", subStyle);

            // Close button [✕]
            if (GUI.Button(new Rect(x + windowW - 32f, y + 8f, 24f, 24f), "✕", microBtnStyle))
            {
                player.CloseDialogue();
                gossipMenuState = 0;
                gossipFeedback = "";
                AudioEffects.PlayClick();
            }

            // Inner Aged Parchment Page
            float parchX = x + 12f;
            float parchY = y + 46f;
            float parchW = windowW - 24f;
            float parchH = windowH - 58f;

            Fill(new Rect(parchX - 2f, parchY - 2f, parchW + 4f, parchH + 4f), new Color(0.35f, 0.26f, 0.12f, 0.95f));
            Fill(new Rect(parchX, parchY, parchW, parchH), new Color(0.88f, 0.81f, 0.65f, 0.97f));

            // Parchment Typography Styles
            var quoteStyle = new GUIStyle(label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Italic,
                wordWrap = true
            };
            quoteStyle.normal.textColor = new Color(0.25f, 0.16f, 0.08f);

            var bodyStyle = new GUIStyle(label)
            {
                fontSize = 12,
                wordWrap = true
            };
            bodyStyle.normal.textColor = new Color(0.12f, 0.08f, 0.04f);

            var optionStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(10, 10, 4, 4)
            };
            optionStyle.normal.textColor = new Color(1f, .95f, .81f);
            optionStyle.hover.textColor = optionStyle.focused.textColor = Color.white;
            optionStyle.active.textColor = new Color(1f, .87f, .53f);

            var feedbackStyle = new GUIStyle(small) { fontStyle = FontStyle.Bold };
            feedbackStyle.normal.textColor = new Color(0.05f, 0.45f, 0.15f);

            // Content Area
            GUILayout.BeginArea(new Rect(parchX + 14f, parchY + 12f, parchW - 28f, parchH - 24f));
            gossipScroll = GUILayout.BeginScrollView(gossipScroll, GUILayout.Width(parchW - 28f), GUILayout.Height(parchH - 24f));

            if (gossipMenuState == 0)
            {
                // Main Greeting & Dialogue Options
                var npcProfile = Narrative.NarrativeCatalog.ResolveTownNPC(npc);
                var greetingSel = Narrative.NarrativeCatalog.SelectText(npcProfile, "greeting", "", player);
                string greetingLine = !string.IsNullOrEmpty(greetingSel.text) ? greetingSel.text : npc.greeting;
                GUILayout.Label($"\"{greetingLine}\"", quoteStyle);
                GUILayout.Space(10f);

                if (npc.npcName == WesternRoadCamps.Witness && player.CanUseWesternRoadQuest)
                {
                    if (player.WesternRoadStage == 2)
                    {
                        GUILayout.Label("I tallied one wagon with one broken wheel. I’ll give you my crossing record. It says what I saw, nothing about who signed your waybill.", bodyStyle);
                        if (GUILayout.Button("Take a copy of Sera's tally", optionStyle, GUILayout.Height(32f)))
                            player.ReadFerryTally();
                    }
                    else if (player.WesternRoadStage >= 3)
                        GUILayout.Label("Tell Rowan the wagon had one broken wheel. That’s the crossing I can vouch for.", bodyStyle);
                    GUILayout.Space(6f);
                }
                if (npc.npcName == BrassHollows.Engineer && player.CanUseBrassHollowsQuest)
                {
                    if (player.BrassHollowsStage == 0)
                        GUILayout.Label("An old quarry on Redridge Pass is ringing like a bell. The Hollow Choir took our regulator. Clear the mine, shut down their siphon and bring it home.", bodyStyle);
                    else GUILayout.Label(player.BrassHollowsObjective, bodyStyle);
                    if ((player.BrassHollowsStage == 0 || player.BrassHollowsStage == 2) &&
                        GUILayout.Button(player.BrassHollowsStage == 0 ? "Accept: Cut the Siphon" : "Return the regulator", optionStyle, GUILayout.Height(32f)))
                        player.HandleBrassHollowsQuest();
                    GUILayout.Space(8f);
                }

                // Divider line
                DrawParchmentDivider();
                GUILayout.Space(8f);

                if (!string.IsNullOrEmpty(gossipFeedback))
                {
                    GUILayout.Label(gossipFeedback, feedbackStyle);
                    GUILayout.Space(8f);
                }

                GUILayout.Label("Choose a topic of inquiry:", bodyStyle);
                GUILayout.Space(6f);

                var fqs = player.GetComponent<FactionQuestSystem>();
                if (fqs != null && fqs.HasActiveQuestToComplete(npc.npcName, player))
                {
                    var q = fqs.GetActiveQuest();
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.35f, 0.95f, 0.45f);
                    if (GUILayout.Button($"❓  \"I have completed the mission: [{q.title}] (Turn In)\"", optionStyle, GUILayout.Height(32f)))
                    {
                        gossipMenuState = 5;
                        AudioEffects.PlayClick();
                    }
                    GUI.backgroundColor = oldBg;
                    GUILayout.Space(6f);
                }
                else if (fqs != null && fqs.GetActiveQuest() is var activeQ && activeQ != null && (string.Equals(activeQ.giverNpc, npc.npcName, StringComparison.OrdinalIgnoreCase) || string.Equals(activeQ.targetId, npc.npcName, StringComparison.OrdinalIgnoreCase)))
                {
                    GUILayout.Label($"<b>Active Mission:</b> {activeQ.title} ({fqs.CurrentProgress}/{activeQ.requiredCount} {activeQ.targetName})", feedbackStyle);
                    var progSel = Narrative.NarrativeCatalog.SelectText(npcProfile, "quest_progress", activeQ.id, player);
                    if (progSel.matched && !string.IsNullOrEmpty(progSel.text))
                        GUILayout.Label(progSel.text, bodyStyle);
                    GUILayout.Space(6f);
                }
                else if (fqs != null && fqs.HasAvailableQuest(npc.npcName, player))
                {
                    var q = fqs.GetAvailableQuestForNPC(npc.npcName, player);
                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1.0f, 0.85f, 0.25f);
                    if (GUILayout.Button($"❗  \"I seek a task, commander: [{q.title}]\"", optionStyle, GUILayout.Height(32f)))
                    {
                        gossipMenuState = 4;
                        AudioEffects.PlayClick();
                    }
                    GUI.backgroundColor = oldBg;
                    GUILayout.Space(6f);
                }
                else if (fqs != null && npcProfile != null && npcProfile.entries != null)
                {
                    var seenAfter = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in npcProfile.entries)
                    {
                        if (entry == null || !string.Equals(entry.channel, "quest_after", StringComparison.Ordinal) || string.IsNullOrEmpty(entry.topicId) || seenAfter.Contains(entry.topicId))
                            continue;
                        seenAfter.Add(entry.topicId);
                        var afterSel = Narrative.NarrativeCatalog.SelectText(npcProfile, "quest_after", entry.topicId, player);
                        if (!afterSel.matched) continue;
                        string qTitle = FactionQuestSystem.GetQuest(entry.topicId)?.title ?? entry.topicId;
                        if (GUILayout.Button($"📜 \"Discuss aftermath: [{qTitle}]\"", optionStyle, GUILayout.Height(28f)))
                        {
                            activeNarrativeTopicChannel = "quest_after";
                            activeNarrativeTopicId = entry.topicId;
                            activeNarrativeTopicTitle = $"AFTERWARDS: {qTitle.ToUpperInvariant()}";
                            gossipMenuState = 6;
                            AudioEffects.PlayClick();
                        }
                    }
                }

                if (npcProfile != null)
                {
                    foreach (var topic in Narrative.NarrativeCatalog.GetPublicTopics(npcProfile, player))
                    {
                        if (GUILayout.Button($"💬 \"{topic.label}\"", optionStyle, GUILayout.Height(28f)))
                        {
                            activeNarrativeTopicChannel = "public_topic";
                            activeNarrativeTopicId = topic.topicId;
                            activeNarrativeTopicTitle = npc.npcName.ToUpperInvariant();
                            gossipMenuState = 6;
                            AudioEffects.PlayClick();
                        }
                    }
                }

                if (GUILayout.Button("💬 \"Tell me about this settlement.\"", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 1;
                    AudioEffects.PlayClick();
                }

                if (GUILayout.Button("📜 \"What threats lurk in this realm?\"", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 2;
                    AudioEffects.PlayClick();
                }

                if (GUILayout.Button("📍 \"Where can I find town services?\"", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 3;
                    AudioEffects.PlayClick();
                }

                // Contextual Option: Flight Master
                if (npc.roleTag.Contains("Flight Master") || npc.roleTag.Contains("Gryphon") || npc.roleTag.Contains("Wind Rider"))
                {
                    if (GUILayout.Button("🦅 \"I require flight transportation.\"", optionStyle, GUILayout.Height(28f)))
                    {
                        var fm = FlightMaster.GetNearby(player.transform.position, 6f);
                        if (fm) FlightMaster.OpenTaxiWindow(fm);
                        player.CloseDialogue();
                        AudioEffects.PlayClick();
                    }
                }

                // Contextual Option: Innkeeper Hearthstone Binding
                if (npc.roleTag.Contains("Innkeeper") || npc.npcName.Contains("Innkeeper"))
                {
                    if (GUILayout.Button("🍺 \"Make this inn my home. (Bind Hearthstone)\"", optionStyle, GUILayout.Height(28f)))
                    {
                        if (RestedXPSystem.Instance)
                        {
                            string curZone = ZoneManager.GetZoneName(player.transform.position);
                            RestedXPSystem.Instance.BindHearthstone(curZone + " Tavern", player.transform.position);
                        }
                        gossipFeedback = $"Your Hearthstone has been set to {npc.npcName}'s Inn!";
                        AudioEffects.PlayClick();
                    }

                    if (GUILayout.Button("✂ \"I'd like to get a haircut and makeover. (Barber Shop)\"", optionStyle, GUILayout.Height(28f)))
                    {
                        if (BarberShopSystem.Instance) BarberShopSystem.Instance.OpenBarber();
                        player.CloseDialogue();
                        AudioEffects.PlayClick();
                    }
                }

                // Contextual Option: Trainer
                if (npc.roleTag.Contains("Trainer"))
                {
                    if (GUILayout.Button("⚔ \"I wish to review my Class Talents [N].\"", optionStyle, GUILayout.Height(28f)))
                    {
                        player.TalentsOpen = true;
                        player.CloseDialogue();
                        AudioEffects.PlayClick();
                    }
                }

                GUILayout.Space(12f);
                DrawParchmentDivider();
                GUILayout.Space(6f);

                if (GUILayout.Button("👋 \"Farewell.\"", optionStyle, GUILayout.Height(26f)))
                {
                    player.CloseDialogue();
                    AudioEffects.PlayClick();
                }
            }
            else if (gossipMenuState == 1)
            {
                // Lore Sub-view
                GUILayout.Label($"<b>THE REALM & SETTLEMENT</b>", bodyStyle);
                GUILayout.Space(6f);
                string lore = GetNPCLore(npc);
                GUILayout.Label(lore, bodyStyle);

                GUILayout.Space(14f);
                DrawParchmentDivider();
                GUILayout.Space(8f);

                if (GUILayout.Button("◄ Back to Inquiries", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 0;
                    AudioEffects.PlayClick();
                }
            }
            else if (gossipMenuState == 2)
            {
                // Threats Sub-view
                GUILayout.Label($"<b>REGIONAL THREATS & BOUNTIES</b>", bodyStyle);
                GUILayout.Space(6f);
                string threats = GetNPCThreats(npc);
                GUILayout.Label(threats, bodyStyle);

                GUILayout.Space(14f);
                DrawParchmentDivider();
                GUILayout.Space(8f);

                if (GUILayout.Button("◄ Back to Inquiries", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 0;
                    AudioEffects.PlayClick();
                }
            }
            else if (gossipMenuState == 3)
            {
                // Directions & Waypoints Sub-view
                GUILayout.Label("<b>TOWN SERVICES & WAYPOINTS</b>", bodyStyle);
                GUILayout.Label("Select a service to drop a waypoint beacon on your Minimap Radar:", bodyStyle);
                GUILayout.Space(8f);

                if (GUILayout.Button("✈ Flight Master Taxi Post", optionStyle, GUILayout.Height(28f)))
                {
                    var fm = FlightMaster.GetNearby(player.transform.position, 160f) ?? FindNearestComponent<FlightMaster>(player.transform.position);
                    if (fm)
                    {
                        MinimapRadar.SetWaypoint(fm.transform.position, $"Flight Master ({fm.nodeName})");
                        gossipFeedback = $"✔ Waypoint pinned: Flight Master ({fm.nodeName})";
                    }
                    else
                    {
                        gossipFeedback = "No Flight Master post located nearby.";
                    }
                    gossipMenuState = 0;
                }

                if (GUILayout.Button("🏛 Auction House & Mailbox", optionStyle, GUILayout.Height(28f)))
                {
                    var mb = Mailbox.GetNearby(player.transform.position, 160f) ?? FindNearestComponent<Mailbox>(player.transform.position);
                    if (mb)
                    {
                        MinimapRadar.SetWaypoint(mb.transform.position, "Auction House & Mailbox");
                        gossipFeedback = "✔ Waypoint pinned: Auctioneer & Mailbox";
                    }
                    else
                    {
                        gossipFeedback = "No Auction House or Mailbox located nearby.";
                    }
                    gossipMenuState = 0;
                }

                if (GUILayout.Button("⚔ The Scarlet Crypts Dungeon Portal", optionStyle, GUILayout.Height(28f)))
                {
                    Vector3 cryptPos = WorldRegions.Expanded ? WorldRegions.Cathedral : new Vector3(-55f, 0f, 215f);
                    MinimapRadar.SetWaypoint(cryptPos, "The Scarlet Crypts Portal");
                    gossipFeedback = $"✔ Waypoint pinned: Scarlet Crypts Portal ({cryptPos.x:F0}, {cryptPos.z:F0})";
                    gossipMenuState = 0;
                }

                if (GUILayout.Button("🌋 Mount Wyrmcrest Summit Dais", optionStyle, GUILayout.Height(28f)))
                {
                    Vector3 summitPos = WorldRegions.Expanded ? WorldRegions.Summit : new Vector3(0f, 48f, 225f);
                    MinimapRadar.SetWaypoint(summitPos, "Wyrmcrest Summit (Apex Dragon)");
                    gossipFeedback = $"✔ Waypoint pinned: Mount Wyrmcrest ({summitPos.x:F0}, {summitPos.z:F0})";
                    gossipMenuState = 0;
                }

                if (GUILayout.Button("🍺 Innkeeper & Traveling Merchant", optionStyle, GUILayout.Height(28f)))
                {
                    var merchant = player.NearbyMerchant() ?? FindNearestComponent<Merchant>(player.transform.position);
                    if (merchant)
                    {
                        MinimapRadar.SetWaypoint(merchant.transform.position, merchant.npcName);
                        gossipFeedback = $"✔ Waypoint pinned: {merchant.npcName}";
                    }
                    else
                    {
                        gossipFeedback = "No merchant post located nearby.";
                    }
                    gossipMenuState = 0;
                }

                GUILayout.Space(12f);
                DrawParchmentDivider();
                GUILayout.Space(8f);

                if (GUILayout.Button("◄ Back to Inquiries", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 0;
                    AudioEffects.PlayClick();
                }
            }
            else if (gossipMenuState == 4)
            {
                // Quest Offer View
                var fqsOffer = player.GetComponent<FactionQuestSystem>();
                var q = fqsOffer != null ? fqsOffer.GetAvailableQuestForNPC(npc.npcName, player) : null;
                if (q != null)
                {
                    var npcProfile = Narrative.NarrativeCatalog.ResolveTownNPC(npc);
                    var offerSel = Narrative.NarrativeCatalog.SelectText(npcProfile, "quest_offer", q.id, player);
                    string offerBody = offerSel.matched && !string.IsNullOrEmpty(offerSel.text) ? offerSel.text : q.description;

                    GUILayout.Label($"<b>MISSION: {q.title.ToUpperInvariant()}</b>", bodyStyle);
                    GUILayout.Label($"<i>Zone: {q.zone}  •  Faction: {q.faction}</i>", subStyle);
                    GUILayout.Space(8f);
                    DrawParchmentDivider();
                    GUILayout.Space(8f);

                    GUILayout.Label(offerBody, bodyStyle);
                    GUILayout.Space(12f);

                    GUILayout.Label($"<b>OBJECTIVE:</b>\n• {q.targetName}: 0 / {q.requiredCount}", bodyStyle);
                    GUILayout.Space(8f);

                    string rewardsText = $"<b>REWARDS:</b>\n• {q.rewardGold} Gold\n• {q.rewardXP} Experience";
                    if (!string.IsNullOrEmpty(q.rewardItem))
                    {
                        var itemDef = ItemCatalog.Get(q.rewardItem);
                        rewardsText += $"\n• Equipment: [{itemDef?.name ?? q.rewardItem}] ({itemDef?.Stats ?? ""})";
                    }
                    GUILayout.Label(rewardsText, bodyStyle);
                    GUILayout.Space(14f);
                    DrawParchmentDivider();
                    GUILayout.Space(10f);

                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(1.0f, 0.85f, 0.25f);
                    if (GUILayout.Button("✔  ACCEPT QUEST", optionStyle, GUILayout.Height(32f)))
                    {
                        fqsOffer.AcceptQuest(q, player);
                        gossipFeedback = $"Quest Accepted: {q.title}!";
                        gossipMenuState = 0;
                    }
                    GUI.backgroundColor = oldBg;
                    GUILayout.Space(6f);

                    if (GUILayout.Button("◄ Decline & Return", optionStyle, GUILayout.Height(28f)))
                    {
                        gossipMenuState = 0;
                        AudioEffects.PlayClick();
                    }
                }
                else
                {
                    gossipMenuState = 0;
                }
            }
            else if (gossipMenuState == 5)
            {
                // Quest Turn-in View
                var fqsTurnIn = player.GetComponent<FactionQuestSystem>();
                var q = fqsTurnIn != null ? fqsTurnIn.GetActiveQuest() : null;
                if (q != null)
                {
                    var npcProfile = Narrative.NarrativeCatalog.ResolveTownNPC(npc);
                    var readySel = Narrative.NarrativeCatalog.SelectText(npcProfile, "quest_ready", q.id, player);
                    string readyBody = readySel.matched && !string.IsNullOrEmpty(readySel.text)
                        ? readySel.text
                        : "Outstanding work, traveler. You met the mark and brought safe passage back to our frontier post. Take these spoils with our gratitude.";

                    GUILayout.Label($"<b>COMPLETE MISSION: {q.title.ToUpperInvariant()}</b>", bodyStyle);
                    GUILayout.Label($"<i>Zone: {q.zone}</i>", subStyle);
                    GUILayout.Space(8f);
                    DrawParchmentDivider();
                    GUILayout.Space(8f);

                    GUILayout.Label(readyBody, bodyStyle);
                    GUILayout.Space(12f);

                    string rewardsText = $"<b>REWARDS EARNED:</b>\n• +{q.rewardGold} Gold\n• +{q.rewardXP} Experience";
                    if (!string.IsNullOrEmpty(q.rewardItem))
                    {
                        var itemDef = ItemCatalog.Get(q.rewardItem);
                        rewardsText += $"\n• Equipment Reward: [{itemDef?.name ?? q.rewardItem}]";
                    }
                    GUILayout.Label(rewardsText, feedbackStyle);
                    GUILayout.Space(14f);
                    DrawParchmentDivider();
                    GUILayout.Space(10f);

                    var oldBg = GUI.backgroundColor;
                    GUI.backgroundColor = new Color(0.35f, 0.95f, 0.45f);
                    if (GUILayout.Button("★  COMPLETE QUEST & CLAIM REWARDS  ★", optionStyle, GUILayout.Height(34f)))
                    {
                        fqsTurnIn.CompleteQuest(player);
                        gossipFeedback = $"Completed: {q.title}! (+{q.rewardGold} Gold, +{q.rewardXP} XP)";
                        gossipMenuState = 0;
                    }
                    GUI.backgroundColor = oldBg;
                }
                else
                {
                    gossipMenuState = 0;
                }
            }
            else if (gossipMenuState == 6)
            {
                var npcProfile = Narrative.NarrativeCatalog.ResolveTownNPC(npc);
                var topicSel = Narrative.NarrativeCatalog.SelectText(npcProfile, activeNarrativeTopicChannel, activeNarrativeTopicId, player);
                string heading = string.IsNullOrEmpty(activeNarrativeTopicTitle) ? npc.npcName.ToUpperInvariant() : activeNarrativeTopicTitle;
                GUILayout.Label($"<b>{heading}</b>", bodyStyle);
                GUILayout.Space(6f);
                GUILayout.Label(!string.IsNullOrEmpty(topicSel.text) ? topicSel.text : (npcProfile?.fallbackText ?? npc.greeting), bodyStyle);
                GUILayout.Space(14f);
                DrawParchmentDivider();
                GUILayout.Space(8f);
                if (GUILayout.Button("◄ Back to Inquiries", optionStyle, GUILayout.Height(28f)))
                {
                    gossipMenuState = 0;
                    AudioEffects.PlayClick();
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawParchmentDivider()
        {
            var rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(2f));
            Fill(rect, new Color(0.55f, 0.45f, 0.28f, 0.7f));
        }

        string GetNPCCrest(TownNPC npc)
        {
            string name = npc.npcName;
            string role = npc.roleTag;

            if (role.Contains("Flight Master") || role.Contains("Gryphon") || role.Contains("Wind Rider")) return "🦅";
            if (role.Contains("Mage") || role.Contains("Arcane") || role.Contains("Scholar") || role.Contains("Priest")) return "🔮";
            if (role.Contains("Smith") || role.Contains("Armor") || role.Contains("Engineer")) return "⚒";
            if (role.Contains("Innkeeper") || role.Contains("Tavern")) return "🍺";
            if (role.Contains("Auctioneer") || role.Contains("Cartel")) return "💰";
            if (name.Contains("Ronald") || name.Contains("Halford") || name.Contains("Justin") || name.Contains("Thalorien") || name.Contains("Ironbrow")) return "🦁";
            if (name.Contains("Mor'gok") || name.Contains("Gorgash") || name.Contains("Grimjaw") || name.Contains("Morzan") || name.Contains("Griznak")) return "🐺";
            return "🛡";
        }

        static T FindNearestComponent<T>(Vector3 origin) where T : Component
        {
            T best = null;
            float bestDist = float.MaxValue;
            foreach (var comp in FindObjectsByType<T>(FindObjectsSortMode.None))
            {
                if (!comp) continue;
                float d = Vector3.Distance(origin, comp.transform.position);
                if (d < bestDist) { bestDist = d; best = comp; }
            }
            return best;
        }

        string GetNPCLore(TownNPC npc)
        {
            var profile = Narrative.NarrativeCatalog.ResolveTownNPC(npc);
            if (profile != null)
            {
                var topics = Narrative.NarrativeCatalog.GetPublicTopics(profile, player);
                if (topics.Count > 0)
                {
                    var firstTopic = Narrative.NarrativeCatalog.SelectText(profile, "public_topic", topics[0].topicId, player);
                    if (!string.IsNullOrEmpty(firstTopic.text)) return firstTopic.text;
                }
                if (!string.IsNullOrEmpty(profile.fallbackText)) return profile.fallbackText;
            }

            string role = npc.roleTag ?? "";
            if (role.Contains("Highspire") || role.Contains("Alliance"))
                return "Highspire Keep commands the western crags above Willowmere, sheltering caravans that brave the fractured frontier roads.";
            if (role.Contains("Gor'Kresh") || role.Contains("Horde"))
                return "Gor'Kresh rises along the red canyon approaches of Bloodrock, where vanguard scouts hold the passes against beasts and raiders.";
            if (role.Contains("Smith") || role.Contains("Provision"))
                return "Stamped steel and dry rations keep the frontier relays open when storm and raiding parties cut the supply trails.";
            return !string.IsNullOrEmpty(npc.greeting)
                ? $"{npc.greeting}\n\nEmberwatch Outpost shelters travelers holding the roads between Greenhollow, Highspire, and Bloodrock."
                : "Emberwatch Outpost shelters travelers holding the roads between Greenhollow, Highspire, and Bloodrock.";
        }

        string GetNPCThreats(TownNPC npc)
        {
            var profile = Narrative.NarrativeCatalog.ResolveTownNPC(npc);
            if (profile != null)
            {
                var topics = Narrative.NarrativeCatalog.GetPublicTopics(profile, player);
                if (topics.Count > 1)
                {
                    var secondTopic = Narrative.NarrativeCatalog.SelectText(profile, "public_topic", topics[1].topicId, player);
                    if (!string.IsNullOrEmpty(secondTopic.text)) return secondTopic.text;
                }
            }

            string name = npc.npcName ?? "";
            if (string.Equals(name, "Commander Ronald", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Scout Brian", StringComparison.OrdinalIgnoreCase))
                return "Fanatic forces gather within The Scarlet Crypts beyond Highspire. Travel prepared before challenging the crypt locks.";
            if (string.Equals(name, "Warlord Mor'gok", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "Vindicator Gorgash", StringComparison.OrdinalIgnoreCase))
                return "Mount Wyrmcrest rises high above the eastern crags, where Vermithrax holds the frostbound summit dais.";

            return "Watch your step along the Ashwood north road and the Westbank crossings near Willowmere. Raiders and hexers patrol off the main flagstones.";
        }
    }
}
