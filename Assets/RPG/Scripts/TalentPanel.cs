using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        int selectedSpecTab = 0;

        void DrawTalentWindow(float width, float height)
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

                GUI.Label(new Rect(x + 40, nodeY + 12, 380, 24), $"{(isCapstone ? "★ " : "")}{node.name}", rankStyle);
                GUI.Label(new Rect(x + 40, nodeY + 36, 470, 44), node.description, small);

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
            GUI.enabled = (specA.PointsSpent + specB.PointsSpent) > 0;
            if (GUI.Button(new Rect(x + 24, y + windowH - 52, 170, 36), "RESET TALENTS", questButton))
            {
                TalentSystem.ResetTalents(player);
            }
            GUI.enabled = true;

            if (GUI.Button(new Rect(x + windowW - 194, y + windowH - 52, 170, 36), "CLOSE  [ N ]", questButton))
            {
                player.TalentsOpen = false;
            }
        }
    }
}
