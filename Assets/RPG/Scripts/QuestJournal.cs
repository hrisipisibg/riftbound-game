using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        int journalSelection;
        Vector2 journalScroll;
        static readonly string[] ChapterNames = {
            "1. The Forest Road",
            "2. Stolen Supplies",
            "3. The Ashwood Captain",
            "4. The Western Crypt",
            "5. Scout Greenhollow",
            "6. The Scarlet Crypts",
            "7. Redridge Frontier",
            "8. Apex of Wyrmcrest"
        };
        static readonly string[] ChapterText =
        {
            "Abe needs the road outside Emberwatch secured. Defeat three road raiders, then return to him for your reward.",
            "The raiders took supplies to their camp. Recover three crates from Ashwood and bring news back to Abe.",
            "Challenge the captain at the northern watchtower. Accept Abe's mission first, defeat the captain and report back to claim your Emberwatch gear.",
            "Ancient stones mark a trail west of the forest road. Activate all three to awaken the Crypt Warden. Defeat him, then open the reliquary at the rear of the ruins. Recommended: level 3+ with armor and potions.",
            "Follow the north road beyond the watchtower. Scout Willow Crossing, Greenhollow Post and the Ancient Grove. Press E at each marker to earn gold and XP, then unlock the Frontier Zebra. Z summons your mount; V changes your selection.",
            "Beneath the ruins in northwest Duskwood lies the Scarlet Crypts Cathedral. Enter through the crimson portal (-55, 215), purge the corrupted zealots, and claim the Inquisitor's Sunblade from the grand altar.",
            "To the northeast lies the Redridge Frontier and Camp Bloodrock (55, 215). Warlord Gorgash and Armorer Thrak hold the frontier. Challenge the crag orcs and claim the Bloodrock Decapitator.",
            "Mount your Celestial Winged Steed (Z) and soar to the highest peak at Wyrmcrest Summit (0, 48, 225). Defeat the apex boss Vermithrax the Ember Sovereign to claim the legendary Heart of the Ember Drake."
        };
        void DrawQuestJournal(float width, float height)
        {
            float x = width / 2 - 410, y = height / 2 - 245;
            Panel(new Rect(x, y, 820, 490));
            GUI.Label(new Rect(x + 24, y + 18, 750, 38), "QUEST JOURNAL & CAMPAIGN", title);
            GUI.Label(new Rect(x + 24, y + 66, 290, 24), "WORLD CHAPTERS", small);

            journalScroll = GUI.BeginScrollView(new Rect(x + 24, y + 96, 295, 325), journalScroll, new Rect(0, 0, 275, ChapterNames.Length * 46));
            for (int i = 0; i < ChapterNames.Length; i++)
            {
                string status = i == 7 ? (player.OwnsItem("ember-drake-scale") ? "DONE" : "APEX BOSS") :
                                i == 6 ? (player.OwnsItem("bloodrock-waraxe") ? "DONE" : "HORDE CAMP") :
                                i == 5 ? (player.OwnsItem("scarlet-blade") ? "DONE" : "DUNGEON") :
                                i == 4 ? (player.LandmarksFound >= 3 ? "DONE" : "OPTIONAL") :
                                i == 3 ? (player.HasOpenedChest("crypt-reliquary") ? "DONE" : "OPTIONAL") :
                                player.QuestIndex > i ? "DONE" :
                                player.QuestIndex == i ? (player.QuestAccepted ? "ACTIVE" : "SPEAK TO ABE") : "LOCKED";

                var color = GUI.backgroundColor;
                GUI.backgroundColor = journalSelection == i ? new Color(.9f, .75f, .4f) : Color.white;
                if (GUI.Button(new Rect(0, i * 46, 275, 42), ChapterNames[i] + " [" + status + "]", questButton))
                    journalSelection = i;
                GUI.backgroundColor = color;
            }
            GUI.EndScrollView();

            GUI.Label(new Rect(x + 338, y + 96, 450, 40), ChapterNames[journalSelection], title);
            string chapterText = ChapterText[journalSelection];
            if (WorldRegions.Expanded)
                chapterText = chapterText.Replace("(-55, 215)", "(-220, 260)").Replace("(55, 215)", "(280, 210)").Replace("(0, 48, 225)", "(250, 75, 510)");
            GUI.Label(new Rect(x + 338, y + 144, 450, 160), chapterText, label);

            string objective = "";
            if (journalSelection == 7)
                objective = $"Mount: {(player.SelectedMount && player.SelectedMount.isFlying ? "WINGED STEED READY" : "SUMMON MOUNT [Z]")}\nVermithrax: {(player.OwnsItem("ember-drake-scale") ? "DEFEATED" : "ALIVE AT PEAK (Lv 8)")}\nLegendary Heart: {(player.OwnsItem("ember-drake-scale") ? "RECOVERED" : "SEALED IN HOARD")}";
            else if (journalSelection == 6)
            {
                var fqs = FactionQuestSystem.Instance;
                var fq = fqs != null ? fqs.GetActiveQuest() : null;
                string fqLine = fq != null ? $"\nActive Mission: {fq.title} ({fqs.CurrentProgress}/{fq.requiredCount})" : "";
                objective = $"Camp Bloodrock: DISCOVERED\nBloodrock Decapitator: {(player.OwnsItem("bloodrock-waraxe") ? "ACQUIRED" : "UNCLAIMED")}\nTerritory: Horde Outpost{fqLine}";
            }
            else if (journalSelection == 5)
                objective = $"Cathedral Portal: FOUND (-55, 215)\nInquisitor's Sunblade: {(player.OwnsItem("scarlet-blade") ? "ACQUIRED" : "UNCLAIMED")}\nDungeon Bosses: High Inquisitor Malakar & Dread Abbot Vael";
            else if (journalSelection == 4)
                objective = $"Landmarks: {player.LandmarksFound}/3\nHorse: AVAILABLE\nFrontier Zebra: {(player.LandmarksFound >= 3 ? "UNLOCKED" : "SCOUT ALL THREE LANDMARKS")}";
            else if (journalSelection == 3)
                objective = $"Runestones: {player.RunesFound}/3\nWarden: {(player.HasDefeated("crypt-warden") ? "DEFEATED" : player.RunesFound >= 3 ? "AWAKENED" : "DORMANT")}\nReliquary: {(player.HasOpenedChest("crypt-reliquary") ? "RECOVERED" : "SEALED")}";
            else
                objective = player.QuestIndex > journalSelection ? "Chapter completed. Rewards claimed." : player.QuestIndex == journalSelection ? player.QuestObjective : "Complete the earlier chapters with Abe.";

            if (WorldRegions.Expanded) objective = objective.Replace("(-55, 215)", "(-220, 260)");
            GUI.Label(new Rect(x + 338, y + 312, 450, 105), objective, label);

            if (GUI.Button(new Rect(x + 24, y + 432, 170, 36), "WORLD MAP [M]", questButton)) { player.JournalOpen = false; player.MapOpen = true; }
            GUI.enabled = !player.QuestComplete && MinimapRadar.Instance;
            if (GUI.Button(new Rect(x + 202, y + 432, 194, 36), "TRACK MAIN QUEST", questButton))
            {
                MinimapRadar.Instance.TrackMainQuest();
                player.JournalOpen = false;
            }
            GUI.enabled = true;
            if (GUI.Button(new Rect(x + 404, y + 432, 206, 36), "ENCOUNTERS [Shift+J]", questButton)) { player.JournalOpen = false; player.EncounterJournalOpen = true; }
            if (GUI.Button(new Rect(x + 618, y + 432, 177, 36), "CLOSE [J / ESC]", questButton)) player.JournalOpen = false;
        }
    }
}
