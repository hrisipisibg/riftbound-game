using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        int spellbookTab = 0; // 0: Class Spells, 1: Racials & General, 2: Spec Perks
        Vector2 spellbookScroll;

        public class SpellInfo
        {
            public string name;
            public string icon;
            public string rank;
            public string cost;
            public string castTime;
            public string range;
            public string cooldown;
            public string description;
            public Color iconColor;
            public int assignedSlot; // 1, 2, 3 or 0
            public Action onUse;
        }

        void DrawSpellbook(float width, float height)
        {
            float windowW = 680f, windowH = 510f;
            float x = width / 2f - windowW / 2f, y = height / 2f - windowH / 2f;

            // Outer dark gothic panel with gold border
            Fill(new Rect(x - 4f, y - 4f, windowW + 8f, windowH + 8f), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            Fill(new Rect(x, y, windowW, windowH), new Color(0.06f, 0.08f, 0.11f, 0.98f));

            // Header Title Bar
            Fill(new Rect(x, y, windowW, 38f), new Color(0.12f, 0.15f, 0.22f, 0.98f));
            Fill(new Rect(x, y + 37f, windowW, 1.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));

            string headerTitle = $"SPELLBOOK & ABILITIES — {player.CharacterRace.ToUpperInvariant()} {player.CharacterClass.ToUpperInvariant()} (LEVEL {player.Level})";
            var headerStyle = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.40f);
            GUI.Label(new Rect(x + 18f, y + 8f, windowW - 60f, 22f), headerTitle, headerStyle);

            // Close button [✕]
            if (GUI.Button(new Rect(x + windowW - 32f, y + 7f, 24f, 24f), "✕", microBtnStyle))
            {
                player.SpellbookOpen = false;
                AudioEffects.PlayClick();
            }

            // Navigation Tabs Bar
            float tabY = y + 42f;
            float tabH = 30f;
            float tabW = 205f;

            DrawSpellbookTab(x + 14f, tabY, tabW, tabH, 0, $"⚔ {player.CharacterClass} Spells");
            DrawSpellbookTab(x + 225f, tabY, tabW, tabH, 1, $"🌟 Racials & General");
            DrawSpellbookTab(x + 436f, tabY, tabW, tabH, 2, $"🔮 Specialization Perks");

            // Content Canvas (Aged dark slate parchment)
            float canvasX = x + 14f;
            float canvasY = tabY + tabH + 6f;
            float canvasW = windowW - 28f;
            float canvasH = windowH - (canvasY - y) - 14f;

            Fill(new Rect(canvasX, canvasY, canvasW, canvasH), new Color(0.04f, 0.05f, 0.07f, 0.92f));
            Fill(new Rect(canvasX, canvasY, canvasW, 1f), new Color(0.45f, 0.38f, 0.20f, 0.8f));

            var spells = GetSpellbookList();

            // Spell Cards Grid / List
            GUILayout.BeginArea(new Rect(canvasX + 10f, canvasY + 8f, canvasW - 20f, canvasH - 16f));
            spellbookScroll = GUILayout.BeginScrollView(spellbookScroll, GUILayout.Width(canvasW - 20f), GUILayout.Height(canvasH - 16f));

            for (int i = 0; i < spells.Count; i++)
            {
                DrawSpellCard(spells[i], canvasW - 40f);
                GUILayout.Space(6f);
            }

            if (spellbookTab == 2)
            {
                GUILayout.Space(12f);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("⚔ OPEN DUAL SPEC TALENT PANEL [N]", questButton, GUILayout.Width(340f), GUILayout.Height(36f)))
                {
                    player.TalentsOpen = true;
                    player.SpellbookOpen = false;
                    AudioEffects.PlayClick();
                }
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawSpellbookTab(float tx, float ty, float tw, float th, int index, string title)
        {
            bool isActive = spellbookTab == index;
            Color bgCol = isActive ? new Color(0.20f, 0.25f, 0.35f, 0.95f) : new Color(0.08f, 0.10f, 0.14f, 0.85f);
            Fill(new Rect(tx, ty, tw, th), bgCol);

            Color borderCol = isActive ? new Color(0.95f, 0.85f, 0.35f, 1f) : new Color(0.35f, 0.32f, 0.22f, 0.6f);
            Fill(new Rect(tx, ty + th - 2f, tw, 2f), borderCol);

            var tStyle = new GUIStyle(small)
            {
                fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            tStyle.normal.textColor = isActive ? new Color(1f, 0.95f, 0.6f) : new Color(0.7f, 0.7f, 0.75f);

            if (GUI.Button(new Rect(tx, ty, tw, th), title, tStyle))
            {
                spellbookTab = index;
                AudioEffects.PlayClick();
            }
        }

        void DrawSpellCard(SpellInfo s, float cardW)
        {
            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();

            // Spell Icon Badge (42x42px)
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = s.iconColor;
            GUILayout.Box(s.icon, GUILayout.Width(42f), GUILayout.Height(42f));
            GUI.backgroundColor = oldBg;

            GUILayout.Space(8f);

            // Title & Info Column
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            var titleStyle = new GUIStyle(label) { fontStyle = FontStyle.Bold, fontSize = 13 };
            titleStyle.normal.textColor = new Color(0.95f, 0.88f, 0.45f);
            GUILayout.Label(s.name, titleStyle);

            var rankStyle = new GUIStyle(small) { fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleRight };
            rankStyle.normal.textColor = new Color(0.6f, 0.8f, 1f);
            GUILayout.Label(s.rank, rankStyle);
            GUILayout.EndHorizontal();

            // Cost & Cast Properties Line
            var propStyle = new GUIStyle(small);
            propStyle.normal.textColor = new Color(0.75f, 0.78f, 0.85f);
            string props = $"{s.cost}   ·   {s.castTime}   ·   {s.range}   ·   {s.cooldown}";
            GUILayout.Label(props, propStyle);

            GUILayout.EndVertical();

            // Action Slot / Use Button Column
            GUILayout.BeginVertical(GUILayout.Width(130f));
            if (s.assignedSlot > 0)
            {
                var slotStyle = new GUIStyle(microBtnStyle) { fontStyle = FontStyle.Bold };
                slotStyle.normal.textColor = new Color(0.25f, 0.95f, 0.35f);
                GUILayout.Box($"✔ Slot [{s.assignedSlot}]", slotStyle, GUILayout.Height(24f));
            }
            else if (s.onUse != null)
            {
                if (GUILayout.Button("CAST / USE", questButton, GUILayout.Height(24f)))
                {
                    s.onUse.Invoke();
                    AudioEffects.PlayClick();
                }
            }
            else
            {
                var passStyle = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
                passStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
                GUILayout.Label("Passive Trait", passStyle);
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            // Description
            GUILayout.Space(4f);
            var descStyle = new GUIStyle(small) { wordWrap = true };
            descStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUILayout.Label(s.description, descStyle);

            GUILayout.EndVertical();
        }

        List<SpellInfo> GetSpellbookList()
        {
            var list = new List<SpellInfo>();
            string cls = player.CharacterClass;
            string race = player.CharacterRace;

            if (spellbookTab == 0)
            {
                // Active Class Spells
                if (cls == "Warrior")
                {
                    list.Add(new SpellInfo
                    {
                        name = "Devastating Strike",
                        icon = "⚔",
                        rank = "Rank 1",
                        cost = "Generates 15 Rage",
                        castTime = "Instant",
                        range = "Melee Range",
                        cooldown = "None",
                        description = "A vicious martial strike that rends the enemy for 24 Physical damage (scaling with weapon DPS). Builds primary combat Rage.",
                        iconColor = new Color(0.85f, 0.25f, 0.2f),
                        assignedSlot = 1,
                        onUse = () => player.CastAbility1Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Whirlwind Cleave",
                        icon = "🌪",
                        rank = "Rank 1",
                        cost = "25 Rage",
                        castTime = "Instant",
                        range = "5m Radius",
                        cooldown = "4.0s CD",
                        description = "In a whirlwind of steel, strike all nearby enemies in a 360-degree arc for heavy physical damage.",
                        iconColor = new Color(0.9f, 0.45f, 0.15f),
                        assignedSlot = 2,
                        onUse = () => player.CastAbility2Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Second Wind",
                        icon = "💨",
                        rank = "Rank 1",
                        cost = "Free",
                        castTime = "Instant",
                        range = "Self",
                        cooldown = "20s CD",
                        description = "Surges with adrenaline, instantly regenerating 35% of total maximum health over 6 seconds.",
                        iconColor = new Color(0.2f, 0.8f, 0.4f),
                        assignedSlot = 3
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Shield Wall / Guard",
                        icon = "🛡",
                        rank = "Rank 1",
                        cost = "Free",
                        castTime = "Channeled",
                        range = "Self",
                        cooldown = "3.0s CD",
                        description = "Raise shield or weapons in defensive posture [Q], absorbing 50% of all incoming physical and magical damage while held.",
                        iconColor = new Color(0.4f, 0.6f, 0.85f),
                        assignedSlot = 4
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Battle Presence",
                        icon = "🔥",
                        rank = "Passive Aura",
                        cost = "Passive",
                        castTime = "Always Active",
                        range = "Self",
                        cooldown = "None",
                        description = "Permanent warrior conditioning grants +10% Attack Power and +2 Armor rating in melee combat.",
                        iconColor = new Color(0.8f, 0.2f, 0.15f)
                    });
                }
                else if (cls == "Mage")
                {
                    list.Add(new SpellInfo
                    {
                        name = "Fireball",
                        icon = "🔥",
                        rank = "Rank 1",
                        cost = "20 Mana",
                        castTime = "1.2s Cast",
                        range = "25m Range",
                        cooldown = "None",
                        description = "Hurls a searing fiery sphere at the target for 32 Fire damage, with a chance to ignite the victim for burning damage over time.",
                        iconColor = new Color(1.0f, 0.45f, 0.1f),
                        assignedSlot = 1,
                        onUse = () => player.CastAbility1Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Frost Nova",
                        icon = "❄",
                        rank = "Rank 1",
                        cost = "25 Mana",
                        castTime = "Instant",
                        range = "8m Radius",
                        cooldown = "6.0s CD",
                        description = "Blasts a freezing wave of frost in an 8-meter ring around the mage, rooting all struck enemies in ice for 3.5 seconds.",
                        iconColor = new Color(0.25f, 0.75f, 1f),
                        assignedSlot = 2,
                        onUse = () => player.CastAbility2Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Arcane Ward & Blink",
                        icon = "🔮",
                        rank = "Rank 1",
                        cost = "30 Mana",
                        castTime = "Instant",
                        range = "10m Teleport",
                        cooldown = "15s CD",
                        description = "Wraps the caster in an arcane energy barrier absorbing 60 damage, while simultaneously teleporting 10 meters forward.",
                        iconColor = new Color(0.7f, 0.35f, 1f),
                        assignedSlot = 3
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Arcane Brilliance",
                        icon = "✨",
                        rank = "Passive Aura",
                        cost = "Passive",
                        castTime = "Always Active",
                        range = "Self",
                        cooldown = "None",
                        description = "Expands intellect, increasing maximum Mana pool by +15% and spell critical strike chance by +5%.",
                        iconColor = new Color(0.5f, 0.8f, 1f)
                    });
                }
                else if (cls == "Rogue")
                {
                    list.Add(new SpellInfo
                    {
                        name = "Sinister Strike",
                        icon = "🗡",
                        rank = "Rank 1",
                        cost = "40 Energy",
                        castTime = "Instant",
                        range = "Melee Range",
                        cooldown = "None",
                        description = "A vicious thrust dealing swift weapon damage and generating momentum for devastating finishing strikes.",
                        iconColor = new Color(0.9f, 0.75f, 0.15f),
                        assignedSlot = 1,
                        onUse = () => player.CastAbility1Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Eviscerate Finisher",
                        icon = "🩸",
                        rank = "Rank 1",
                        cost = "35 Energy",
                        castTime = "Instant",
                        range = "Melee Range",
                        cooldown = "3.0s CD",
                        description = "Finishing slice that disembowels the enemy for massive physical damage (+75% critical multiplier).",
                        iconColor = new Color(0.85f, 0.15f, 0.15f),
                        assignedSlot = 2,
                        onUse = () => player.CastAbility2Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Sprint Burst",
                        icon = "⚡",
                        rank = "Rank 1",
                        cost = "Free",
                        castTime = "Instant",
                        range = "Self",
                        cooldown = "15s CD",
                        description = "Surges with adrenaline, boosting movement speed by +60% for 8 seconds. Breaks movement impairing effects.",
                        iconColor = new Color(1f, 0.85f, 0.2f),
                        assignedSlot = 3
                    });
                }
                else // Paladin
                {
                    list.Add(new SpellInfo
                    {
                        name = "Crusader Strike",
                        icon = "⚔",
                        rank = "Rank 1",
                        cost = "15 Mana",
                        castTime = "Instant",
                        range = "Melee Range",
                        cooldown = "None",
                        description = "A righteous melee strike imbued with holy power, dealing weapon damage and generating 1 Holy Power charge.",
                        iconColor = new Color(1f, 0.85f, 0.3f),
                        assignedSlot = 1,
                        onUse = () => player.CastAbility1Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Consecration",
                        icon = "☀️",
                        rank = "Rank 1",
                        cost = "25 Mana",
                        castTime = "Instant",
                        range = "6m Ground",
                        cooldown = "8.0s CD",
                        description = "Consecrates the ground beneath the paladin's boots for 6 seconds, searing all unholy and hostile entities that stand upon it.",
                        iconColor = new Color(0.95f, 0.8f, 0.2f),
                        assignedSlot = 2,
                        onUse = () => player.CastAbility2Public()
                    });
                    list.Add(new SpellInfo
                    {
                        name = "Holy Light",
                        icon = "✝",
                        rank = "Rank 1",
                        cost = "25 Mana",
                        castTime = "1.5s Cast",
                        range = "Self",
                        cooldown = "None",
                        description = "Calls down divine radiance upon the paladin, restoring 80 Health points and purging harmful status ailments.",
                        iconColor = new Color(0.95f, 0.95f, 0.45f),
                        assignedSlot = 3
                    });
                }
            }
            else if (spellbookTab == 1)
            {
                // Racials & General
                list.Add(new SpellInfo
                {
                    name = GetRacialName(race),
                    icon = GetRacialIcon(race),
                    rank = "Racial Passive",
                    cost = "Passive",
                    castTime = "Permanent",
                    range = "Self",
                    cooldown = "None",
                    description = ClassSystem.GetRaceDescription(race),
                    iconColor = new Color(0.85f, 0.75f, 0.35f)
                });
                list.Add(new SpellInfo
                {
                    name = "Hearthstone",
                    icon = "🏛",
                    rank = "Innkeeper Stone",
                    cost = "Free",
                    castTime = "5.0s Cast",
                    range = "Continental",
                    cooldown = "60s CD",
                    description = "Communes with the home hearth at Emberwatch Outpost, teleporting the player safely back to the tavern [H].",
                    iconColor = new Color(0.35f, 0.75f, 1f),
                    assignedSlot = 7,
                    onUse = () => player.TriggerHearthstonePublic()
                });
                list.Add(new SpellInfo
                {
                    name = "Summon Mount",
                    icon = "🐎",
                    rank = "Ground / Flight",
                    cost = "Free",
                    castTime = "2.0s Cast",
                    range = "Self",
                    cooldown = "None",
                    description = "Summons the active steed (Ground Mount: +65% speed; Celestial Winged Steed: +145% speed & full aerial flight) [Z].",
                    iconColor = new Color(0.85f, 0.65f, 0.3f),
                    assignedSlot = 6,
                    onUse = () => player.ToggleMount()
                });
                list.Add(new SpellInfo
                {
                    name = "Basic Attack / Weapon Swing",
                    icon = "⚔",
                    rank = "General",
                    cost = "Free",
                    castTime = "Instant",
                    range = "Melee Range",
                    cooldown = "Weapon Speed",
                    description = "Strikes with the equipped weapon. Left-click or press [1] when targeting hostile enemies.",
                    iconColor = new Color(0.7f, 0.7f, 0.7f)
                });
            }
            else
            {
                // Specialization Perks
                var (specA, specB) = TalentSystem.GetTrees(cls);
                var activeTree = TalentSystem.ActiveSpecIndex == 0 ? specA : specB;
                list.Add(new SpellInfo
                {
                    name = $"Active Spec: {activeTree.specName} <{activeTree.roleTag}>",
                    icon = "⭐",
                    rank = "Specialization",
                    cost = "Active Role",
                    castTime = "Always Active",
                    range = "Self",
                    cooldown = "None",
                    description = activeTree.description,
                    iconColor = new Color(1f, 0.85f, 0.25f)
                });
                list.Add(new SpellInfo
                {
                    name = "Dual Specialization Mastery",
                    icon = "🔄",
                    rank = "Class Feature",
                    cost = "Free",
                    castTime = "2.0s Cast",
                    range = "Self",
                    cooldown = "Out of Combat",
                    description = "Allows instant role respecification between DPS, Tank, Healer, and Control builds via the Talent Window [N].",
                    iconColor = new Color(0.45f, 0.8f, 1f)
                });
            }

            return list;
        }

        string GetRacialName(string race)
        {
            switch (race)
            {
                case "Human": return "Will of the King (Alliance)";
                case "Elf":
                case "High Elf": return "Arcane Affinity (Alliance)";
                case "Dwarf": return "Stoneform Resilience (Alliance)";
                case "Undead": return "Touch of the Grave (Horde)";
                case "Goblin": return "Time is Money (Horde)";
                default: return "Blood Fury (Horde)";
            }
        }

        string GetRacialIcon(string race)
        {
            switch (race)
            {
                case "Human": return "👑";
                case "Elf":
                case "High Elf": return "💎";
                case "Dwarf": return "🛡";
                case "Undead": return "💀";
                case "Goblin": return "💰";
                default: return "🩸";
            }
        }
    }
}
