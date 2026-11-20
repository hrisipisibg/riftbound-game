using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        int selectedSpecTab = 0;
        bool showSpecializations;

        void DrawTalentWindow(float width, float height)
        {
            if (showSpecializations)
            {
                DrawSpecializations(width, height);
                return;
            }

            var window = new Rect(width / 2f - 500f, height / 2f - 328f, 1000f, 656f);
            Panel(window);
            GUI.Label(new Rect(window.x + 24f, window.y + 12f, 390f, 36f), "MILESTONE TALENTS", title);
            GUI.Label(new Rect(window.x + 24f, window.y + 50f, 930f, 25f),
                player.MilestoneTalentsAvailable
                    ? $"Level {player.Level}  ·  Choose one talent every five levels. Change choices outside combat."
                    : "Milestone talents are available in solo play.", small);
            if (GUI.Button(new Rect(window.xMax - 330f, window.y + 17f, 205f, 29f), "Specializations", microBtnStyle))
                showSpecializations = true;
            if (GUI.Button(new Rect(window.xMax - 110f, window.y + 17f, 86f, 29f), "Close [N]", microBtnStyle))
                player.TalentsOpen = false;

            bool canChange = player.MilestoneTalentsAvailable && !player.InCombat();
            var cardName = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 15 };
            var stateStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };
            var subtitle = new GUIStyle(small) { fontSize = 12 };
            subtitle.normal.textColor = new Color(.72f, .83f, .91f);
            var border = new Color(.29f, .34f, .36f);
            var gold = new Color(.96f, .78f, .42f);
            foreach (var talent in MilestoneTalents.All)
            {
                int column = talent.Icon % MilestoneTalents.ChoicesPerTier;
                float rowY = window.y + 83f + talent.Tier * 106f;
                bool unlocked = player.Level >= talent.Level;
                cardName.normal.textColor = unlocked ? new Color(.98f, .94f, .84f) : new Color(.72f, .75f, .77f);
                if (column == 0)
                    GUI.Label(new Rect(window.x + 24f, rowY + 17f, 64f, 58f), $"LEVEL\n{talent.Level}", cardName);
                bool selected = player.ChosenMilestone(talent.Tier) == talent.Id;
                var card = new Rect(window.x + 90f + column * 297f, rowY, 286f, 96f);
                Fill(card, selected ? gold : border);
                Fill(new Rect(card.x + 1f, card.y + 1f, card.width - 2f, card.height - 2f),
                    selected ? new Color(.123f, .218f, .185f) : new Color(.061f, .088f, .112f));
                RiftboundAbilityArt.Draw(new Rect(card.x + 8f, card.y + 8f, 49f, 49f), talent.Icon, unlocked ? 1f : .46f);
                GUI.Label(new Rect(card.x + 64f, card.y + 6f, 214f, 21f), talent.Name, cardName);
                GUI.Label(new Rect(card.x + 64f, card.y + 28f, 214f, 43f), talent.Description, subtitle);
                GUI.Label(new Rect(card.x + 8f, card.y + 68f, 130f, 22f),
                    talent.Active ? $"Active · {talent.Cooldown:0}s cooldown" : "Passive", subtitle);
                if (unlocked && !selected && canChange)
                {
                    if (GUI.Button(new Rect(card.xMax - 105f, card.y + 70f, 97f, 20f),
                            string.IsNullOrEmpty(player.ChosenMilestone(talent.Tier)) ? "Learn" : "Switch", microBtnStyle) &&
                        player.ChooseMilestone(talent)) AudioEffects.PlayClick();
                }
                else
                {
                    stateStyle.normal.textColor = selected ? gold : new Color(.73f, .79f, .83f);
                    GUI.Label(new Rect(card.xMax - 125f, card.y + 67f, 116f, 23f),
                        selected ? "Selected" : unlocked ? "Unavailable" : "Locked", stateStyle);
                }
            }
            if (GUI.Button(new Rect(window.xMax - 216f, window.yMax - 42f, 192f, 28f), "Open Spellbook [P]", microBtnStyle))
            {
                spellbookTab = 2;
                player.TalentsOpen = false;
                player.SpellbookOpen = true;
            }
            GUI.Label(new Rect(window.x + 24f, window.yMax - 42f, 730f, 26f),
                "Equip learned actives in the spellbook. Passive talents apply automatically.", small);
        }

        void DrawSpecializations(float width, float height)
        {
            float windowW = 720f, windowH = 530f;
            float x = width / 2 - windowW / 2, y = height / 2 - windowH / 2;

            // Background panel with dark metallic border
            Fill(new Rect(x - 8, y - 8, windowW + 16, windowH + 16), new Color(0.015f, 0.035f, 0.045f, 0.95f));
            Panel(new Rect(x, y, windowW, windowH));

            // Gold header
            GUI.Label(new Rect(x + 24, y + 18, 500, 36), "SPECIALIZATION & TALENTS", title);

            int unspent = TalentSystem.GetUnspentTalentPoints(player);
            Color unspentColor = unspent > 0 ? new Color(1f, 0.85f, 0.25f) : new Color(0.6f, 0.65f, 0.65f);
            var unspentStyle = new GUIStyle(questHeading) { fontSize = 16, alignment = TextAnchor.MiddleRight };
            unspentStyle.normal.textColor = unspentColor;
            GUI.Label(new Rect(x + windowW - 290, y + 22, 260, 28), $"Unspent Points: {unspent}", unspentStyle);

            var (specA, specB) = TalentSystem.GetTrees(player.CharacterClass);
            TalentTree activeTree = selectedSpecTab == 0 ? specA : specB;

            // Spec tabs (WoW Classic style)
            float tabW = 210f, tabH = 38f;
            GUI.backgroundColor = selectedSpecTab == 0 ? new Color(0.85f, 0.65f, 0.3f) : new Color(0.35f, 0.4f, 0.45f);
            if (GUI.Button(new Rect(x + 24, y + 62, tabW, tabH), $"{specA.specName} ({specA.PointsSpent})", questButton))
            {
                selectedSpecTab = 0;
            }

            GUI.backgroundColor = selectedSpecTab == 1 ? new Color(0.85f, 0.65f, 0.3f) : new Color(0.35f, 0.4f, 0.45f);
            if (GUI.Button(new Rect(x + 24 + tabW + 12, y + 62, tabW, tabH), $"{specB.specName} ({specB.PointsSpent})", questButton))
            {
                selectedSpecTab = 1;
            }
            GUI.backgroundColor = Color.white;

            // Spec Info Sub-panel
            Panel(new Rect(x + 24, y + 108, windowW - 48, 62));
            var specTitleStyle = new GUIStyle(label) { fontSize = 17, fontStyle = FontStyle.Bold };
            specTitleStyle.normal.textColor = new Color(0.95f, 0.82f, 0.45f);
            GUI.Label(new Rect(x + 36, y + 114, 440, 24), $"{activeTree.specName} — <{activeTree.roleTag}>", specTitleStyle);
            GUI.Label(new Rect(x + 36, y + 138, 440, 24), activeTree.description, small);

            // Dual Spec Switch Button
            bool isCurrentActiveSpec = TalentSystem.ActiveSpecIndex == selectedSpecTab;
            if (isCurrentActiveSpec)
            {
                var activeBadgeStyle = new GUIStyle(label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                activeBadgeStyle.normal.textColor = new Color(0.4f, 1.0f, 0.5f);
                Fill(new Rect(x + windowW - 220, y + 118, 180, 40), new Color(0.12f, 0.35f, 0.16f, 0.9f));
                GUI.Label(new Rect(x + windowW - 220, y + 118, 180, 40), "✔ ACTIVE SPEC", activeBadgeStyle);
            }
            else
            {
                GUI.backgroundColor = new Color(1.0f, 0.85f, 0.35f);
                if (GUI.Button(new Rect(x + windowW - 220, y + 118, 180, 40), $"ACTIVATE {activeTree.specName.ToUpper()}", questButton))
                {
                    int targetSpec = selectedSpecTab;
                    string targetName = activeTree.specName;
                    string targetTag = activeTree.roleTag;
                    CastBarSystem.StartCast($"Activating {targetName} ({targetTag})...", 2.0f, () =>
                    {
                        TalentSystem.ActiveSpecIndex = targetSpec;
                        TalentSystem.ApplyTalentStats(player);
                        ChatSystem.LogSystem($"Specialization switched to [{targetName}] <{targetTag}>.");
                        AudioEffects.PlayClick();
                    }, true, true);
                    player.TalentsOpen = false;
                }
                GUI.backgroundColor = Color.white;
            }

            // Talent Nodes List
            float nodeY = y + 182;
            for (int i = 0; i < activeTree.talents.Count; i++)
            {
                var node = activeTree.talents[i];
                bool isCapstone = i == 2;
                bool tierUnlocked = !isCapstone || activeTree.PointsSpent >= 3;
                bool maxed = node.currentRank >= node.maxRank;
                bool canAllocate = unspent > 0 && !maxed && tierUnlocked;

                // Node container
                Color nodeBg = maxed ? new Color(0.12f, 0.28f, 0.18f, 0.85f) :
                               canAllocate ? new Color(0.14f, 0.20f, 0.30f, 0.85f) :
                               new Color(0.08f, 0.10f, 0.12f, 0.85f);
                Fill(new Rect(x + 24, nodeY, windowW - 48, 86), nodeBg);
                Panel(new Rect(x + 24, nodeY, windowW - 48, 86));

                // Node rank badge
                string rankText = isCapstone ? (node.currentRank > 0 ? "[ MASTERED ]" : "[ 0 / 1 ]") : $"[ {node.currentRank} / {node.maxRank} ]";
                Color rankColor = maxed ? new Color(0.4f, 1f, 0.5f) : tierUnlocked ? new Color(1f, 0.85f, 0.35f) : new Color(0.55f, 0.55f, 0.55f);
                var rankStyle = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
                rankStyle.normal.textColor = rankColor;

                int specArt = player.CharacterClass switch
                {
                    "Mage" => selectedSpecTab == 0 ? 35 : 43,
                    "Rogue" => selectedSpecTab == 0 ? 36 : 39,
                    "Paladin" => selectedSpecTab == 0 ? 42 : 44,
                    _ => selectedSpecTab == 0 ? 45 : 34
                };
                int art = i == 0 ? specArt : i == 1 ? (selectedSpecTab == 0 ? 33 : 40) : (selectedSpecTab == 0 ? 41 : 37);
                RiftboundAbilityArt.Draw(new Rect(x + 36, nodeY + 15, 54, 54), art);
                GUI.Label(new Rect(x + 100, nodeY + 12, 335, 24), node.name, rankStyle);
                GUI.Label(new Rect(x + 100, nodeY + 36, 365, 44), node.description, small);

                GUI.Label(new Rect(x + windowW - 250, nodeY + 14, 110, 24), rankText, rankStyle);

                // Allocate button
                GUI.enabled = canAllocate;
                GUI.backgroundColor = canAllocate ? new Color(0.3f, 0.85f, 0.4f) : Color.gray;
                if (GUI.Button(new Rect(x + windowW - 135, nodeY + 22, 95, 42), maxed ? "MAX" : "+ LEARN", questButton))
                {
                    TalentSystem.AllocatePoint(player, activeTree, i);
                }
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;

                nodeY += 94;
            }

            // Bottom control bar
            if (GUI.Button(new Rect(x + 224, y + windowH - 52, 255, 36), "MILESTONE TALENTS", questButton))
                showSpecializations = false;
            GUI.enabled = (specA.PointsSpent + specB.PointsSpent) > 0;
            if (GUI.Button(new Rect(x + 24, y + windowH - 52, 170, 36), "RESET TALENTS", questButton))
            {
                TalentSystem.ResetTalents(player);
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x + windowW - 194, y + windowH - 52, 170, 36), "CLOSE  [ N ]", questButton))
            {
                player.TalentsOpen = false;
                showSpecializations = false;
            }
        }
    }
}
