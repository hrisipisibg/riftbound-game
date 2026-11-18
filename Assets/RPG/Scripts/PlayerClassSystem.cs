using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public enum RaceType
    {
        Brute,
        Human,
        Undead
    }

    public enum ClassType
    {
        Warrior,
        Mage,
        Rogue,
        Paladin
    }

    public static class ClassSystem
    {
        public static string GetRaceDescription(string race)
        {
            switch (race)
            {
                case "Human":
                    return "Will of the King (Alliance): +10% Movement Speed & +25% Gold gained from quests and enemies.";
                case "Elf":
                case "High Elf":
                    return "Arcane Affinity (Alliance): +15% Max Mana/Energy & +5% Spell Critical Chance.";
                case "Dwarf":
                    return "Stoneform (Alliance): +20% Armor Rating & +15 Max Health.";
                case "Undead":
                    return "Touch of the Grave (Horde): +3 Flat Damage Reduction & Melee attacks siphon 6 Health.";
                case "Goblin":
                    return "Time is Money (Horde): +15% Vendor discount & +10% Attack Speed.";
                case "Orc":
                case "Brute":
                default:
                    return "Blood Fury (Horde): +15% Physical & Spell Damage bonus and +25 Max Health.";
            }
        }

        public static string GetClassDescription(string cls)
        {
            switch (cls)
            {
                case "Mage":
                    return "Master of the Arcane. Uses Fireball [1/LMB] at range, Frost Nova [2] AOE freeze, and Arcane Ward [3] shield/teleport.";
                case "Rogue":
                    return "Agile Shadow Striker. Fast Sinister Strike [1/LMB], massive Eviscerate [2] finisher, and Sprint [3] speed burst.";
                case "Paladin":
                    return "Holy Crusader. Crusader Strike [1/LMB], Consecration [2] radiant ground burst, and Holy Light [3] powerful restoration.";
                case "Warrior":
                default:
                    return "Heavy Melee Juggernaut. Devastating Strike [1/LMB], Whirlwind [2] 360-degree cleave, and Second Wind [3] healing.";
            }
        }

        public static (string name, string desc) GetAbilityInfo(string cls, int slot)
        {
            switch (cls)
            {
                case "Mage":
                    if (slot == 1) return ("Fireball", "Launches blazing bolt (36 Fire Dmg)");
                    if (slot == 2) return ("Frost Nova", "Freezes nearby foes (32 Frost Dmg, 6s CD)");
                    return ("Arcane Ward", "Blink forward & shield 40 HP (10s CD)");

                case "Rogue":
                    if (slot == 1) return ("Sinister Strike", "Fast dual slice (20 Dmg, 0.5s CD)");
                    if (slot == 2) return ("Eviscerate", "Critical finisher (52 Dmg, 4s CD)");
                    return ("Sprint", "+65% Speed for 5s (10s CD)");

                case "Paladin":
                    if (slot == 1) return ("Crusader Strike", "Righteous blow (26 Holy Dmg)");
                    if (slot == 2) return ("Consecration", "Radiant holy ground (40 Holy Dmg, 6s CD)");
                    return ("Holy Light", "Restores 60 HP (12s CD)");

                case "Warrior":
                default:
                    if (slot == 1) return ("Strike", "Melee weapon strike (24 Dmg)");
                    if (slot == 2) return ("Whirlwind", "Cleaves all nearby foes (38 Dmg, 5s CD)");
                    return ("Second Wind", "Restores 45 HP (12s CD)");
            }
        }
    }
}
