using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class PlayerTitleSystem : MonoBehaviour
    {
        public static PlayerTitleSystem Instance { get; private set; }

        public static string ActiveTitle = "The Undaunted";

        public struct TitleDefinition
        {
            public string id;
            public string name;
            public string description;
            public bool isPrefix; // e.g. "Champion Garrick" vs "Garrick the Undaunted"
        }

        public static readonly TitleDefinition[] AllTitles =
        {
            new TitleDefinition { id = "undaunted", name = "the Undaunted", description = "Defended Emberwatch from forest raiders", isPrefix = false },
            new TitleDefinition { id = "scourge_hunter", name = "Scourge Hunter", description = "Breached the subterranean Scarlet Crypts", isPrefix = true },
            new TitleDefinition { id = "dragonslayer", name = "Dragonslayer", description = "Conquered the summit of Mount Wyrmcrest", isPrefix = true },
            new TitleDefinition { id = "titanbreaker", name = "Titanbreaker", description = "Defeated Gorgonax the Worldbreaker", isPrefix = true },
            new TitleDefinition { id = "master_artisan", name = "Master Artisan", description = "Achieved 30+ skill in a primary crafting profession", isPrefix = true },
            new TitleDefinition { id = "skywarden", name = "Skywarden", description = "Mastered 310% speed flight on a Celestial Winged Steed", isPrefix = true },
            new TitleDefinition { id = "pvp_champion", name = "Champion of the Valley", description = "Claimed victory in the Crossroads Skirmish Arena", isPrefix = true }
        };

        PlayerController player;

        void Awake()
        {
            Instance = this;
            player = GetComponent<PlayerController>();
        }

        public static bool IsTitleUnlocked(string titleId, PlayerController p)
        {
            if (!p) return false;
            switch (titleId)
            {
                case "undaunted":
                    return p.QuestIndex >= 1 || p.Kills >= 3;
                case "scourge_hunter":
                    return p.RunesFound >= 1 || p.Level >= 2;
                case "dragonslayer":
                    return p.BossDefeated || p.Level >= 4;
                case "titanbreaker":
                    return p.Kills >= 8 || p.Level >= 5;
                case "master_artisan":
                    return ProfessionSystem.Instance != null && (ProfessionSystem.Instance.alchemySkill >= 30 || ProfessionSystem.Instance.blacksmithingSkill >= 30 || ProfessionSystem.Instance.engineeringSkill >= 30);
                case "skywarden":
                    return p.Mounted && p.SelectedMount != null && p.SelectedMount.isFlying;
                case "pvp_champion":
                    return BattlegroundSystem.Instance != null && BattlegroundSystem.Instance.ArenaMatchesWon > 0;
                default:
                    return false;
            }
        }

        public static List<TitleDefinition> GetUnlockedTitles(PlayerController p)
        {
            var list = new List<TitleDefinition>();
            if (!p) return list;
            foreach (var t in AllTitles)
            {
                if (IsTitleUnlocked(t.id, p)) list.Add(t);
            }
            return list;
        }

        public static string GetFormattedName(PlayerController p)
        {
            if (!p) return "Hero";
            string baseName = p.CharacterName;
            if (string.IsNullOrEmpty(ActiveTitle) || ActiveTitle == "none") return baseName;

            foreach (var t in AllTitles)
            {
                if (t.name.Equals(ActiveTitle, StringComparison.OrdinalIgnoreCase) || t.id.Equals(ActiveTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return t.isPrefix ? $"{t.name} {baseName}" : $"{baseName} {t.name}";
                }
            }

            return $"{baseName} {ActiveTitle}";
        }

        public static bool SetActiveTitle(string title)
        {
            if (string.IsNullOrEmpty(title) || title.Equals("none", StringComparison.OrdinalIgnoreCase))
            {
                ActiveTitle = "";
                return true;
            }
            foreach (var t in AllTitles)
            {
                if (t.name.Equals(title, StringComparison.OrdinalIgnoreCase) || t.id.Equals(title, StringComparison.OrdinalIgnoreCase))
                {
                    ActiveTitle = t.name;
                    return true;
                }
            }
            ActiveTitle = title;
            return true;
        }
    }
}
