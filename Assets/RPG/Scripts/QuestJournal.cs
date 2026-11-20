using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        enum JournalFilter
        {
            All,
            InProgress,
            Completed
        }

        AdventureGuideTab guideTab = AdventureGuideTab.Overview;
        JournalFilter journalFilter = JournalFilter.All;
        int journalSelection;
        int exploreSelection;
        Vector2 overviewScroll;
        Vector2 journalListScroll;
        Vector2 journalDetailScroll;
        Vector2 exploreListScroll;
        Vector2 exploreDetailScroll;
        GUIStyle guideCardStyle, guideMutedStyle, guideBadgeStyle, guideWrapButton, guideActionButton, guideHeadingStyle, guideCompactButton;

        void EnsureGuideStyles()
        {
            if (guideCardStyle != null) return;
            guideCardStyle = new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            guideCardStyle.normal.textColor = new Color(.90f, .92f, .89f);

            guideMutedStyle = new GUIStyle(small) { wordWrap = true, alignment = TextAnchor.UpperLeft };
            guideMutedStyle.normal.textColor = new Color(.68f, .72f, .68f);

            guideBadgeStyle = new GUIStyle(small) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };

            guideHeadingStyle = new GUIStyle(title) { fontSize = 21, fontStyle = FontStyle.Bold, wordWrap = true };
            guideHeadingStyle.normal.textColor = WowTheme.Parchment;

            guideWrapButton = new GUIStyle(questButton) { fontSize = 14, wordWrap = true, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 10, 6, 6) };
            guideActionButton = new GUIStyle(questButton) { fontSize = 13, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(8, 8, 4, 4) };
            guideCompactButton = new GUIStyle(questButton) { fontSize = 11, fontStyle = FontStyle.Bold, wordWrap = false, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(6, 6, 4, 4) };
        }

        void DrawQuestJournal(float width, float height)
        {
            EnsureGuideStyles();

            float winW = Mathf.Clamp(width - 48f, 760f, 920f);
            float winH = Mathf.Clamp(height - 110f, 480f, 566f);
            float x = (width - winW) * 0.5f;
            float y = Mathf.Max(22f, (height - winH) * 0.5f - 18f);

            Panel(new Rect(x, y, winW, winH));

            // Header bar
            GUI.Label(new Rect(x + 22f, y + 14f, 380f, 34f), "ADVENTURE GUIDE", title);
            string zoneTag = ZoneManager.GetZoneName(player.transform.position);
            var rightAlign = new GUIStyle(guideMutedStyle) { alignment = TextAnchor.MiddleRight };
            GUI.Label(new Rect(x + winW - 360f, y + 18f, 336f, 24f), $"{player.CharacterName}  ·  {zoneTag}", rightAlign);

            // Top tab strip: Overview / Journal / Explore
            float tabY = y + 52f;
            float tabW = 148f;
            DrawGuideTabButton(new Rect(x + 22f, tabY, tabW, 32f), "OVERVIEW", AdventureGuideTab.Overview);
            DrawGuideTabButton(new Rect(x + 22f + tabW + 8f, tabY, tabW, 32f), "JOURNAL", AdventureGuideTab.Journal);
            DrawGuideTabButton(new Rect(x + 22f + (tabW + 8f) * 2f, tabY, tabW, 32f), "EXPLORE", AdventureGuideTab.Explore);

            var radar = MinimapRadar.Instance;
            bool hasPin = radar && radar.customWaypoint.HasValue;
            string pinCaption = null;
            if (hasPin) radar.TryGetNavigationTarget(out _, out pinCaption);

            float bodyTop = y + 94f;
            if (hasPin)
            {
                Rect pinBar = new Rect(x + 22f, bodyTop, winW - 44f, 24f);
                Fill(pinBar, new Color(.08f, .20f, .24f, .92f));
                Fill(new Rect(pinBar.x, pinBar.y, 3f, pinBar.height), new Color(.25f, .90f, 1f));
                var pinStyle = new GUIStyle(small) { fontSize = 12, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                pinStyle.normal.textColor = new Color(.70f, .95f, 1f);
                GUI.Label(new Rect(pinBar.x + 10f, pinBar.y, pinBar.width - 16f, pinBar.height),
                    $"Active Map Pin: {(string.IsNullOrEmpty(pinCaption) ? "Travel waypoint" : pinCaption)}  —  kept until replaced or cleared.", pinStyle);
                bodyTop += 30f;
            }

            float footerH = 44f;
            float bodyH = y + winH - footerH - 12f - bodyTop;
            Rect bodyRect = new Rect(x + 22f, bodyTop, winW - 44f, bodyH);

            var entries = AdventureGuideCatalog.BuildJournalEntries(player);
            var exploreCards = AdventureGuideCatalog.BuildExploreCards(player);

            switch (guideTab)
            {
                case AdventureGuideTab.Overview:
                    DrawOverviewTab(bodyRect, entries, hasPin);
                    break;
                case AdventureGuideTab.Journal:
                    DrawJournalTab(bodyRect, entries, hasPin);
                    break;
                case AdventureGuideTab.Explore:
                    DrawExploreTab(bodyRect, exploreCards, hasPin);
                    break;
            }

            // Footer navigation bar (preserves World Map [M], Encounters [Shift+J], and Close [J / ESC])
            float footY = y + winH - footerH - 8f;
            if (GUI.Button(new Rect(x + 22f, footY, 176f, 36f), "WORLD MAP [M]", questButton))
            {
                AudioEffects.PlayClick();
                player.JournalOpen = false;
                player.MapOpen = true;
            }
            if (GUI.Button(new Rect(x + 208f, footY, 214f, 36f), "ENCOUNTERS [Shift+J]", questButton))
            {
                AudioEffects.PlayClick();
                player.JournalOpen = false;
                player.EncounterJournalOpen = true;
            }
            if (GUI.Button(new Rect(x + winW - 202f, footY, 180f, 36f), "CLOSE [J / ESC]", questButton))
            {
                AudioEffects.PlayClick();
                player.JournalOpen = false;
            }
        }

        void DrawGuideTabButton(Rect rect, string text, AdventureGuideTab tab)
        {
            bool active = guideTab == tab;
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(.92f, .76f, .38f) : new Color(.75f, .78f, .80f);
            if (GUI.Button(rect, text, questButton))
            {
                if (guideTab != tab) AudioEffects.PlayClick();
                guideTab = tab;
            }
            GUI.backgroundColor = prev;
            if (active) Fill(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), WowTheme.Gold);
        }

        void DrawOverviewTab(Rect area, List<JournalChapterView> entries, bool hasPin)
        {
            int goalIdx = AdventureGuideCatalog.GetCurrentGoalChapterIndex(player, entries);
            var sideIndices = AdventureGuideCatalog.GetRecommendedSideIndices(entries, goalIdx);

            float innerW = area.width - 18f;
            float leftW = Mathf.Floor(innerW * 0.56f);
            float rightW = innerW - leftW - 14f;

            var goal = entries.Count > 0 ? entries[Mathf.Clamp(goalIdx, 0, entries.Count - 1)] : null;

            float goalSummaryH = goal != null ? guideCardStyle.CalcHeight(new GUIContent(goal.summary), leftW - 28f) : 24f;
            float goalObjH = goal != null ? label.CalcHeight(new GUIContent(goal.objective), leftW - 28f) : 24f;
            float goalCardH = goal != null ? Mathf.Max(220f, 144f + goalSummaryH + goalObjH) : 160f;

            float sideTotalH = 34f;
            for (int i = 0; i < sideIndices.Count; i++)
            {
                var side = entries[sideIndices[i]];
                float sObjH = guideMutedStyle.CalcHeight(new GUIContent(side.objective), rightW - 24f);
                sideTotalH += Mathf.Max(124f, 98f + sObjH) + 10f;
            }
            if (sideIndices.Count == 0) sideTotalH += 76f;

            float contentH = Mathf.Max(area.height, Mathf.Max(goalCardH + 106f, sideTotalH));
            overviewScroll = GUI.BeginScrollView(area, overviewScroll, new Rect(0f, 0f, innerW, contentH));

            // Left: Current Goal + Frontier Snapshot
            GUI.Label(new Rect(0f, 0f, leftW, 22f), "CURRENT GOAL", small);
            if (goal != null)
            {
                Rect card = new Rect(0f, 24f, leftW, goalCardH);
                Fill(card, new Color(.08f, .10f, .12f, .92f));
                Fill(new Rect(card.x, card.y, 4f, card.height), StatusColor(goal.status));

                float cy = card.y + 10f;
                guideBadgeStyle.normal.textColor = StatusColor(goal.status);
                GUI.Label(new Rect(card.x + 14f, cy, leftW - 28f, 18f), $"{goal.statusLabel}  ·  {goal.regionTag}", guideBadgeStyle);
                cy += 20f;

                GUI.Label(new Rect(card.x + 14f, cy, leftW - 28f, 30f), goal.title, guideHeadingStyle);
                cy += 32f;

                GUI.Label(new Rect(card.x + 14f, cy, leftW - 28f, goalSummaryH + 4f), goal.summary, guideCardStyle);
                cy += goalSummaryH + 10f;

                Fill(new Rect(card.x + 14f, cy, leftW - 28f, 1f), WowTheme.GoldDark);
                cy += 8f;

                GUI.Label(new Rect(card.x + 14f, cy, leftW - 28f, goalObjH + 4f), goal.objective, label);
                cy += goalObjH + 12f;

                float btnY = card.yMax - 44f;
                float halfBtn = (leftW - 36f) * 0.5f;
                DrawPrimaryChapterAction(new Rect(card.x + 14f, btnY, halfBtn, 32f), goal, hasPin, closeOnTrack: true);
                if (GUI.Button(new Rect(card.x + 22f + halfBtn, btnY, halfBtn, 32f), "OPEN IN JOURNAL", guideActionButton))
                {
                    AudioEffects.PlayClick();
                    journalSelection = goal.index;
                    journalFilter = JournalFilter.All;
                    guideTab = AdventureGuideTab.Journal;
                }
            }

            // Frontier Snapshot beneath Current Goal
            float snapY = 24f + goalCardH + 12f;
            Rect snapRect = new Rect(0f, snapY, leftW, 74f);
            Fill(snapRect, new Color(.07f, .09f, .11f, .86f));
            int greenFound = WorldRegions.Expanded ? player.GreenhollowLandmarksFound : player.LandmarksFound;
            string coopNote = Network.CoopSessionConfig.IsMultiplayer
                ? "Multiplayer session: solo frontier quests are hidden from active recommendations."
                : WorldRegions.Expanded
                    ? "Solo Connected Frontier: Abe's campaign, Western Road, and Brass Hollows are active."
                    : "Campaign active in Emberwatch & Amber Vale.";
            GUI.Label(new Rect(snapRect.x + 12f, snapRect.y + 8f, snapRect.width - 24f, 20f),
                $"Campaign: {Mathf.Min(player.QuestIndex, 3)}/3   ·   Greenhollow: {greenFound}/3   ·   Crypt Runes: {player.RunesFound}/3", small);
            GUI.Label(new Rect(snapRect.x + 12f, snapRect.y + 32f, snapRect.width - 24f, 36f), coopNote, guideMutedStyle);

            // Right: Recommended Next (up to 2 feasible side routes)
            float rx = leftW + 14f;
            GUI.Label(new Rect(rx, 0f, rightW, 22f), "RECOMMENDED SIDE ROUTES", small);
            float sy = 24f;
            if (sideIndices.Count == 0)
            {
                Rect emptyCard = new Rect(rx, sy, rightW, 72f);
                Fill(emptyCard, new Color(.08f, .10f, .12f, .86f));
                GUI.Label(new Rect(emptyCard.x + 12f, emptyCard.y + 14f, emptyCard.width - 24f, 44f),
                    "No open side routes right now. Check Explore for regional travel notes or the Encounter Journal for boss tactics.", guideMutedStyle);
            }
            else
            {
                for (int i = 0; i < sideIndices.Count; i++)
                {
                    var side = entries[sideIndices[i]];
                    float sObjH = guideMutedStyle.CalcHeight(new GUIContent(side.objective), rightW - 24f);
                    float sCardH = Mathf.Max(124f, 98f + sObjH);
                    Rect sCard = new Rect(rx, sy, rightW, sCardH);
                    Fill(sCard, new Color(.08f, .10f, .12f, .90f));
                    Fill(new Rect(sCard.x, sCard.y, 3f, sCard.height), StatusColor(side.status));

                    guideBadgeStyle.normal.textColor = StatusColor(side.status);
                    GUI.Label(new Rect(sCard.x + 12f, sCard.y + 8f, rightW - 24f, 18f), $"{side.statusLabel}  ·  {side.regionTag}", guideBadgeStyle);
                    GUI.Label(new Rect(sCard.x + 12f, sCard.y + 26f, rightW - 24f, 24f), side.title, label);
                    GUI.Label(new Rect(sCard.x + 12f, sCard.y + 50f, rightW - 24f, sObjH + 4f), side.objective, guideMutedStyle);

                    float bY = sCard.yMax - 38f;
                    float detailsW = 104f;
                    float actionW = rightW - 30f - detailsW;
                    if (GUI.Button(new Rect(sCard.x + 12f, bY, detailsW, 28f), "DETAILS", guideCompactButton))
                    {
                        AudioEffects.PlayClick();
                        journalSelection = side.index;
                        journalFilter = JournalFilter.All;
                        guideTab = AdventureGuideTab.Journal;
                    }
                    DrawPrimaryChapterAction(new Rect(sCard.x + 18f + detailsW, bY, actionW, 28f), side, hasPin, closeOnTrack: true, compact: true);

                    sy += sCardH + 10f;
                }
            }

            GUI.EndScrollView();
        }

        void DrawJournalTab(Rect area, List<JournalChapterView> entries, bool hasPin)
        {
            float listW = Mathf.Clamp(area.width * 0.36f, 250f, 320f);
            float detailX = area.x + listW + 14f;
            float detailW = area.width - listW - 14f;

            // Filter row above list
            float filterW = (listW - 8f) / 3f;
            DrawFilterButton(new Rect(area.x, area.y, filterW, 26f), "ALL", JournalFilter.All);
            DrawFilterButton(new Rect(area.x + filterW + 4f, area.y, filterW, 26f), "ACTIVE", JournalFilter.InProgress);
            DrawFilterButton(new Rect(area.x + (filterW + 4f) * 2f, area.y, filterW, 26f), "DONE", JournalFilter.Completed);

            var filtered = new List<JournalChapterView>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (journalFilter == JournalFilter.InProgress &&
                    e.status != JournalEntryStatus.Active &&
                    e.status != JournalEntryStatus.ReadyToTurnIn &&
                    e.status != JournalEntryStatus.Available)
                    continue;
                if (journalFilter == JournalFilter.Completed && e.status != JournalEntryStatus.Completed)
                    continue;
                filtered.Add(e);
            }

            Rect listRect = new Rect(area.x, area.y + 34f, listW, area.height - 34f);
            float rowH = 52f;
            journalListScroll = GUI.BeginScrollView(listRect, journalListScroll,
                new Rect(0f, 0f, listW - 18f, Mathf.Max(listRect.height, filtered.Count * (rowH + 4f))));

            if (filtered.Count == 0)
            {
                GUI.Label(new Rect(6f, 10f, listW - 26f, 40f), "No entries match this filter.", guideMutedStyle);
            }
            else
            {
                for (int i = 0; i < filtered.Count; i++)
                {
                    var item = filtered[i];
                    bool selected = journalSelection == item.index;
                    float ry = i * (rowH + 4f);
                    Rect rowRect = new Rect(0f, ry, listW - 18f, rowH);

                    var prevBg = GUI.backgroundColor;
                    GUI.backgroundColor = selected ? new Color(.90f, .75f, .40f) : Color.white;
                    if (GUI.Button(rowRect, "", guideWrapButton))
                    {
                        journalSelection = item.index;
                        AudioEffects.PlayClick();
                    }
                    GUI.backgroundColor = prevBg;

                    Fill(new Rect(rowRect.x + 2f, rowRect.y + 4f, 3f, rowRect.height - 8f), StatusColor(item.status));
                    GUI.Label(new Rect(rowRect.x + 10f, rowRect.y + 5f, rowRect.width - 16f, 22f), item.title, label);
                    guideBadgeStyle.normal.textColor = StatusColor(item.status);
                    GUI.Label(new Rect(rowRect.x + 10f, rowRect.y + 27f, rowRect.width - 16f, 18f), item.statusLabel, guideBadgeStyle);
                }
            }
            GUI.EndScrollView();

            journalSelection = Mathf.Clamp(journalSelection, 0, Mathf.Max(0, entries.Count - 1));
            var cur = entries.Count > 0 ? entries[journalSelection] : null;
            if (cur == null) return;

            Rect detailBox = new Rect(detailX, area.y, detailW, area.height);
            Fill(detailBox, new Color(.07f, .09f, .11f, .88f));

            float innerW = detailW - 28f;
            float sumH = guideCardStyle.CalcHeight(new GUIContent(cur.summary), innerW);
            float objH = label.CalcHeight(new GUIContent(cur.objective), innerW - 16f);
            float noteH = string.IsNullOrEmpty(cur.availabilityNote) ? 0f : guideMutedStyle.CalcHeight(new GUIContent(cur.availabilityNote), innerW) + 8f;
            float detailContentH = Mathf.Max(detailBox.height - 16f, 176f + sumH + objH + noteH);

            journalDetailScroll = GUI.BeginScrollView(
                new Rect(detailBox.x + 8f, detailBox.y + 8f, detailBox.width - 12f, detailBox.height - 16f),
                journalDetailScroll,
                new Rect(0f, 0f, innerW, detailContentH));

            float dy = 4f;
            guideBadgeStyle.normal.textColor = StatusColor(cur.status);
            GUI.Label(new Rect(4f, dy, innerW, 18f), $"{cur.statusLabel}  ·  {cur.regionTag}", guideBadgeStyle);
            dy += 20f;

            GUI.Label(new Rect(4f, dy, innerW, 34f), cur.title, title);
            dy += 36f;

            GUI.Label(new Rect(4f, dy, innerW, sumH + 4f), cur.summary, guideCardStyle);
            dy += sumH + 12f;

            GUI.Label(new Rect(4f, dy, innerW, 20f), "CURRENT OBJECTIVE", small);
            dy += 22f;

            Rect objBox = new Rect(4f, dy, innerW - 4f, Mathf.Max(48f, objH + 16f));
            Fill(objBox, new Color(.05f, .07f, .09f, .95f));
            Fill(new Rect(objBox.x, objBox.y, 3f, objBox.height), StatusColor(cur.status));
            GUI.Label(new Rect(objBox.x + 12f, objBox.y + 8f, objBox.width - 20f, objH + 4f), cur.objective, label);
            dy = objBox.yMax + 12f;

            if (!string.IsNullOrEmpty(cur.availabilityNote))
            {
                GUI.Label(new Rect(4f, dy, innerW, noteH), cur.availabilityNote, guideMutedStyle);
                dy += noteH + 6f;
            }

            // One primary action for the selected chapter
            DrawPrimaryChapterAction(new Rect(4f, dy, Mathf.Min(280f, innerW - 4f), 36f), cur, hasPin, closeOnTrack: true);

            GUI.EndScrollView();
        }

        void DrawExploreTab(Rect area, List<ExploreCardView> cards, bool hasPin)
        {
            float listW = Mathf.Clamp(area.width * 0.34f, 240f, 300f);
            float detailX = area.x + listW + 14f;
            float detailW = area.width - listW - 14f;

            Rect listRect = new Rect(area.x, area.y, listW, area.height);
            float rowH = 62f;
            exploreListScroll = GUI.BeginScrollView(listRect, exploreListScroll,
                new Rect(0f, 0f, listW - 18f, Mathf.Max(listRect.height, cards.Count * (rowH + 6f))));

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                bool selected = exploreSelection == i;
                Rect row = new Rect(0f, i * (rowH + 6f), listW - 18f, rowH);

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = selected ? new Color(.90f, .75f, .40f) : Color.white;
                if (GUI.Button(row, "", guideWrapButton))
                {
                    exploreSelection = i;
                    AudioEffects.PlayClick();
                }
                GUI.backgroundColor = prevBg;

                Color accent = card.questUnavailableInCoop ? new Color(.55f, .58f, .62f) : WowTheme.Gold;
                Fill(new Rect(row.x + 2f, row.y + 4f, 3f, row.height - 8f), accent);
                GUI.Label(new Rect(row.x + 10f, row.y + 6f, row.width - 16f, 22f), card.title, label);
                guideBadgeStyle.normal.textColor = accent;
                GUI.Label(new Rect(row.x + 10f, row.y + 32f, row.width - 16f, 18f), card.availabilityBadge, guideBadgeStyle);
            }
            GUI.EndScrollView();

            exploreSelection = Mathf.Clamp(exploreSelection, 0, Mathf.Max(0, cards.Count - 1));
            var cur = cards.Count > 0 ? cards[exploreSelection] : null;
            if (cur == null) return;

            Rect detailBox = new Rect(detailX, area.y, detailW, area.height);
            Fill(detailBox, new Color(.07f, .09f, .11f, .88f));

            float innerW = detailW - 30f;
            float purposeH = label.CalcHeight(new GUIContent(cur.purpose), innerW);
            float startH = guideCardStyle.CalcHeight(new GUIContent(cur.howToStart), innerW);
            float destH = guideCardStyle.CalcHeight(new GUIContent(cur.namedDestination), innerW);
            float hintH = guideCardStyle.CalcHeight(new GUIContent(cur.practicalHint), innerW);
            float liveH = string.IsNullOrEmpty(cur.liveObjectiveLine) ? 0f : label.CalcHeight(new GUIContent(cur.liveObjectiveLine), innerW - 16f) + 28f;

            int actionCount = cur.liveStops.Count + (cur.allowTrackWestern ? 1 : 0) + (cur.allowTrackHollows ? 1 : 0) + (cur.showEncountersButton ? 1 : 0);
            float actionsH = actionCount * 38f + 16f;
            float totalH = Mathf.Max(detailBox.height - 16f, 210f + purposeH + startH + destH + hintH + liveH + actionsH);

            exploreDetailScroll = GUI.BeginScrollView(
                new Rect(detailBox.x + 8f, detailBox.y + 8f, detailBox.width - 12f, detailBox.height - 16f),
                exploreDetailScroll,
                new Rect(0f, 0f, innerW, totalH));

            float dy = 4f;
            guideBadgeStyle.normal.textColor = cur.questUnavailableInCoop ? new Color(.65f, .68f, .70f) : WowTheme.Gold;
            GUI.Label(new Rect(4f, dy, innerW, 18f), $"{cur.subtitle.ToUpperInvariant()}  ·  {cur.availabilityBadge}", guideBadgeStyle);
            dy += 20f;

            GUI.Label(new Rect(4f, dy, innerW, 34f), cur.title, title);
            dy += 36f;

            GUI.Label(new Rect(4f, dy, innerW, purposeH + 4f), cur.purpose, label);
            dy += purposeH + 6f;

            dy = DrawLabeledSection(4f, dy, innerW, "HOW TO START", cur.howToStart, guideCardStyle);
            dy = DrawLabeledSection(4f, dy, innerW, "NAMED DESTINATION", cur.namedDestination, guideCardStyle);
            dy = DrawLabeledSection(4f, dy, innerW, "TRAVEL HINT", cur.practicalHint, guideCardStyle);

            if (!string.IsNullOrEmpty(cur.liveObjectiveLine))
            {
                float lObjH = label.CalcHeight(new GUIContent(cur.liveObjectiveLine), innerW - 20f);
                Rect liveBox = new Rect(4f, dy, innerW - 4f, lObjH + 16f);
                Fill(liveBox, new Color(.05f, .07f, .09f, .95f));
                Fill(new Rect(liveBox.x, liveBox.y, 3f, liveBox.height), WowTheme.Gold);
                GUI.Label(new Rect(liveBox.x + 12f, liveBox.y + 8f, liveBox.width - 20f, lObjH + 4f), cur.liveObjectiveLine, label);
                dy = liveBox.yMax + 12f;
            }

            // Explicit route/stop actions (omits unavailable sites automatically; labels pin replacement)
            GUI.Label(new Rect(4f, dy, innerW, 20f), "ROUTE ACTIONS", small);
            dy += 24f;

            float actionW = Mathf.Min(360f, innerW - 8f);
            int slot = 0;

            void PlaceButton(string text, bool enabled, System.Action onClick)
            {
                Rect r = new Rect(4f, dy + slot * 38f, actionW, 32f);
                bool prevEnabled = GUI.enabled;
                GUI.enabled = enabled;
                if (GUI.Button(r, text, guideActionButton))
                {
                    AudioEffects.PlayClick();
                    onClick?.Invoke();
                }
                GUI.enabled = prevEnabled;
                slot++;
            }

            if (cur.allowTrackWestern)
            {
                bool canTrack = MinimapRadar.Instance != null;
                string lbl = hasPin ? "REPLACE PIN & TRACK WESTERN" : "TRACK WESTERN QUEST";
                PlaceButton(lbl, canTrack, () =>
                {
                    MinimapRadar.Instance.FollowWesternRoad();
                    player.JournalOpen = false;
                });
            }
            if (cur.allowTrackHollows)
            {
                bool canTrack = MinimapRadar.Instance != null;
                string lbl = hasPin ? "REPLACE PIN & TRACK CAVE" : "TRACK CAVE QUEST";
                PlaceButton(lbl, canTrack, () =>
                {
                    MinimapRadar.Instance.FollowBrassHollows();
                    player.JournalOpen = false;
                });
            }
            for (int s = 0; s < cur.liveStops.Count; s++)
            {
                var stop = cur.liveStops[s];
                if (!stop.available) continue;
                string verb = hasPin ? "REPLACE PIN: " : "PIN: ";
                PlaceButton(verb + stop.label, MinimapRadar.Instance != null, () =>
                {
                    MinimapRadar.SetWaypoint(stop.position, stop.label);
                });
            }
            if (cur.showEncountersButton)
            {
                PlaceButton("OPEN ENCOUNTERS [Shift+J]", true, () =>
                {
                    player.JournalOpen = false;
                    player.EncounterJournalOpen = true;
                });
            }

            GUI.EndScrollView();
        }

        float DrawLabeledSection(float x, float y, float w, string heading, string body, GUIStyle bodyStyle)
        {
            GUI.Label(new Rect(x, y, w, 18f), heading, small);
            y += 18f;
            float h = bodyStyle.CalcHeight(new GUIContent(body), w);
            GUI.Label(new Rect(x, y, w, h + 4f), body, bodyStyle);
            return y + h + 7f;
        }

        void DrawFilterButton(Rect rect, string text, JournalFilter filter)
        {
            bool active = journalFilter == filter;
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(.90f, .75f, .40f) : new Color(.78f, .80f, .82f);
            if (GUI.Button(rect, text, questButton))
            {
                if (journalFilter != filter) AudioEffects.PlayClick();
                journalFilter = filter;
            }
            GUI.backgroundColor = prev;
        }

        void DrawPrimaryChapterAction(Rect rect, JournalChapterView chapter, bool hasPin, bool closeOnTrack, bool compact = false)
        {
            bool radarReady = MinimapRadar.Instance != null;
            string btnLabel = chapter.actionLabel;
            bool enabled = true;

            switch (chapter.actionKind)
            {
                case GuideActionKind.TrackMain:
                    btnLabel = hasPin ? "REPLACE PIN & TRACK MAIN" : "TRACK MAIN QUEST";
                    enabled = radarReady && !player.QuestComplete;
                    break;
                case GuideActionKind.TrackWestern:
                    btnLabel = hasPin ? "REPLACE PIN & TRACK WESTERN" : "TRACK WESTERN QUEST";
                    enabled = radarReady && player.WesternRoadActive;
                    break;
                case GuideActionKind.TrackHollows:
                    btnLabel = hasPin ? "REPLACE PIN & TRACK CAVE" : "TRACK CAVE QUEST";
                    enabled = radarReady && player.BrassHollowsQuestActive;
                    break;
                case GuideActionKind.PinSite:
                    enabled = radarReady && chapter.destination.available;
                    if (hasPin && !string.IsNullOrEmpty(btnLabel) && btnLabel.StartsWith("PIN "))
                        btnLabel = "REPLACE " + btnLabel;
                    break;
                case GuideActionKind.OpenEncounters:
                    enabled = true;
                    break;
                default:
                    btnLabel = chapter.status == JournalEntryStatus.Completed ? "COMPLETED" : "UNAVAILABLE";
                    enabled = false;
                    break;
            }

            bool prevEnabled = GUI.enabled;
            GUI.enabled = enabled;
            if (GUI.Button(rect, btnLabel, compact ? guideCompactButton : guideActionButton))
            {
                AudioEffects.PlayClick();
                switch (chapter.actionKind)
                {
                    case GuideActionKind.TrackMain:
                        MinimapRadar.Instance.TrackMainQuest();
                        if (closeOnTrack) player.JournalOpen = false;
                        break;
                    case GuideActionKind.TrackWestern:
                        MinimapRadar.Instance.FollowWesternRoad();
                        if (closeOnTrack) player.JournalOpen = false;
                        break;
                    case GuideActionKind.TrackHollows:
                        MinimapRadar.Instance.FollowBrassHollows();
                        if (closeOnTrack) player.JournalOpen = false;
                        break;
                    case GuideActionKind.PinSite:
                        MinimapRadar.SetWaypoint(chapter.destination.position, chapter.destination.label);
                        break;
                    case GuideActionKind.OpenEncounters:
                        player.JournalOpen = false;
                        player.EncounterJournalOpen = true;
                        break;
                }
            }
            GUI.enabled = prevEnabled;
        }

        static Color StatusColor(JournalEntryStatus status) => status switch
        {
            JournalEntryStatus.ReadyToTurnIn => new Color(.35f, .92f, .45f),
            JournalEntryStatus.Active => new Color(1f, .82f, .28f),
            JournalEntryStatus.Available => new Color(.55f, .82f, 1f),
            JournalEntryStatus.Completed => new Color(.62f, .80f, .64f),
            _ => new Color(.60f, .62f, .64f)
        };
    }
}
