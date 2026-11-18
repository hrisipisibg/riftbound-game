using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class CharacterSheetSystem : MonoBehaviour
    {
        public static CharacterSheetSystem Instance
        {
            get
            {
                if (_instance == null) _instance = UnityEngine.Object.FindAnyObjectByType<CharacterSheetSystem>();
                return _instance;
            }
            private set => _instance = value;
        }
        static CharacterSheetSystem _instance;

        public bool IsOpen { get; set; }

        public enum SheetTab
        {
            Character,
            Skills,
            Honor
        }

        public SheetTab CurrentTab { get; set; } = SheetTab.Character;

        PlayerController player;

        GUIStyle titleStyle, headerStyle, labelStyle, statLabelStyle, statValueStyle, tooltipTitleStyle, tooltipBodyStyle, closeBtnStyle;
        Texture2D bgTex, borderTex, slotBgTex, panelBgTex, barFillTex;

        // Hover tooltip state
        string hoveredSlotName = null;
        string hoveredItemName = null;
        string hoveredItemStats = null;
        string hoveredItemRarity = null;
        Rect hoveredRect;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
        }

        public void ToggleSheet()
        {
            IsOpen = !IsOpen;
            AudioEffects.PlayClick();
        }

        // --- ATTRIBUTE CALCULATIONS ---

        public int GetStrength(PlayerController p)
        {
            if (!p) return 20;
            int baseStr = p.CharacterClass == "Warrior" ? 28 : p.CharacterClass == "Paladin" ? 26 : p.CharacterClass == "Rogue" ? 18 : 14;
            int racialBonus = p.CharacterRace == "Orc" || p.CharacterRace == "Brute" ? 5 : p.CharacterRace == "Dwarf" ? 4 : 0;
            int gearBonus = p.Weapon != null ? p.Weapon.damage / 2 : 0;
            return baseStr + (p.Level - 1) * 3 + racialBonus + gearBonus;
        }

        public int GetAgility(PlayerController p)
        {
            if (!p) return 18;
            int baseAgi = p.CharacterClass == "Rogue" ? 30 : p.CharacterClass == "Warrior" ? 18 : 15;
            int racialBonus = p.CharacterRace == "Elf" || p.CharacterRace == "Goblin" ? 4 : 0;
            int gearBonus = p.Trinket != null ? p.Trinket.health / 10 : 0;
            return baseAgi + (p.Level - 1) * 2 + racialBonus + gearBonus;
        }

        public int GetStamina(PlayerController p)
        {
            if (!p) return 24;
            int baseSta = p.CharacterClass == "Warrior" || p.CharacterClass == "Paladin" ? 26 : 20;
            int racialBonus = p.CharacterRace == "Dwarf" ? 4 : p.CharacterRace == "Orc" ? 3 : 0;
            int gearBonus = (p.Armor != null ? p.Armor.health / 10 : 0) + (p.Trinket != null ? p.Trinket.health / 10 : 0);
            return baseSta + (p.Level - 1) * 3 + racialBonus + gearBonus;
        }

        public int GetIntellect(PlayerController p)
        {
            if (!p) return 16;
            int baseInt = p.CharacterClass == "Mage" ? 32 : p.CharacterClass == "Paladin" ? 20 : 12;
            int racialBonus = p.CharacterRace == "Elf" ? 4 : p.CharacterRace == "Undead" ? 2 : 0;
            return baseInt + (p.Level - 1) * 3 + racialBonus;
        }

        public int GetSpirit(PlayerController p)
        {
            if (!p) return 15;
            int baseSpi = p.CharacterClass == "Mage" || p.CharacterClass == "Paladin" ? 22 : 16;
            int racialBonus = p.CharacterRace == "Elf" ? 3 : p.CharacterRace == "Undead" ? 3 : 0;
            return baseSpi + (p.Level - 1) * 2 + racialBonus;
        }

        public int GetAttackPower(PlayerController p)
        {
            if (!p) return 40;
            if (p.CharacterClass == "Rogue")
                return GetStrength(p) + GetAgility(p) * 2;
            return GetStrength(p) * 2 + (p.Weapon != null ? p.Weapon.damage * 4 : 0);
        }

        public float GetCritChance(PlayerController p)
        {
            if (!p) return 5f;
            float baseCrit = 5.0f + GetAgility(p) * 0.08f;
            if (p.Weapon != null && p.Weapon.rarity == "Epic") baseCrit += 2.5f;
            if (p.Weapon != null && p.Weapon.rarity == "Legendary") baseCrit += 5.0f;
            return baseCrit;
        }

        public float GetDodgeChance(PlayerController p)
        {
            if (!p) return 4f;
            return 3.5f + GetAgility(p) * 0.06f;
        }

        public float GetBlockChance(PlayerController p)
        {
            if (!p) return 0f;
            return p.CharacterClass == "Warrior" || p.CharacterClass == "Paladin" ? 5.0f + GetStrength(p) * 0.04f : 0f;
        }

        // --- GUI DRAWING ---

        public void DrawCharacterSheetGUI(float screenW, float screenH)
        {
            if (!IsOpen || FrontendMenu.IsOpen) return;
            if (!player) player = GetComponent<PlayerController>() ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (!player) return;

            InitStyles();

            float windowW = 680f;
            float windowH = 510f;
            float winX = (screenW - windowW) * 0.5f;
            float winY = (screenH - windowH) * 0.5f;

            // Frame background & gold trim
            GUI.DrawTexture(new Rect(winX - 3f, winY - 3f, windowW + 6f, windowH + 6f), borderTex);
            GUI.DrawTexture(new Rect(winX, winY, windowW, windowH), bgTex);

            // Title Bar
            float headerH = 36f;
            GUI.DrawTexture(new Rect(winX, winY, windowW, headerH), borderTex);
            GUI.DrawTexture(new Rect(winX + 1f, winY + 1f, windowW - 2f, headerH - 2f), bgTex);

            string titleStr = $"{PlayerTitleSystem.GetFormattedName(player)} — LEVEL {player.Level} {player.CharacterRace.ToUpperInvariant()} {player.CharacterClass.ToUpperInvariant()}";
            GUI.Label(new Rect(winX + 16f, winY + 6f, windowW - 60f, 24f), titleStr, titleStyle);

            if (GUI.Button(new Rect(winX + windowW - 30f, winY + 6f, 22f, 22f), "✕", closeBtnStyle))
            {
                IsOpen = false;
                AudioEffects.PlayClick();
            }

            // Tabs Header
            float tabY = winY + headerH + 6f;
            float tabW = 120f, tabH = 24f;
            DrawSheetTab(winX + 16f + 0 * (tabW + 4f), tabY, tabW, tabH, "👤 Character", SheetTab.Character);
            DrawSheetTab(winX + 16f + 1 * (tabW + 4f), tabY, tabW, tabH, "⚔ Skills & Arms", SheetTab.Skills);
            DrawSheetTab(winX + 16f + 2 * (tabW + 4f), tabY, tabW, tabH, "🎖 Honor & PvP", SheetTab.Honor);

            float contentY = tabY + tabH + 8f;
            float contentH = windowH - (contentY - winY) - 14f;

            switch (CurrentTab)
            {
                case SheetTab.Character:
                    DrawCharacterTab(winX + 14f, contentY, windowW - 28f, contentH);
                    break;
                case SheetTab.Skills:
                    DrawSkillsTab(winX + 14f, contentY, windowW - 28f, contentH);
                    break;
                case SheetTab.Honor:
                    DrawHonorTab(winX + 14f, contentY, windowW - 28f, contentH);
                    break;
            }

            // Slot hover tooltip
            if (!string.IsNullOrEmpty(hoveredItemName))
            {
                DrawItemTooltip(hoveredRect);
            }
        }

        void DrawSheetTab(float x, float y, float w, float h, string label, SheetTab tab)
        {
            bool active = CurrentTab == tab;
            var oldCol = GUI.backgroundColor;
            if (active) GUI.backgroundColor = new Color(0.85f, 0.70f, 0.28f);
            if (GUI.Button(new Rect(x, y, w, h), label, closeBtnStyle))
            {
                CurrentTab = tab;
                AudioEffects.PlayClick();
            }
            GUI.backgroundColor = oldCol;
        }

        void DrawCharacterTab(float x, float y, float w, float h)
        {
            hoveredItemName = null;

            float colSlotSize = 42f;
            float slotGap = 6f;

            // ==========================================
            // 1. LEFT PAPERDOLL COLUMN (8 Equipment Slots)
            // ==========================================
            float leftX = x + 10f;
            float startY = y + 10f;

            DrawPaperdollSlot(leftX, startY + 0 * (colSlotSize + slotGap), colSlotSize, "👑", "Head", null);
            DrawPaperdollSlot(leftX, startY + 1 * (colSlotSize + slotGap), colSlotSize, "📿", "Neck", null);
            DrawPaperdollSlot(leftX, startY + 2 * (colSlotSize + slotGap), colSlotSize, "🥋", "Shoulders", null);
            DrawPaperdollSlot(leftX, startY + 3 * (colSlotSize + slotGap), colSlotSize, "🧣", "Back", null);
            DrawPaperdollSlot(leftX, startY + 4 * (colSlotSize + slotGap), colSlotSize, "🛡", "Chest", player.Armor);
            DrawPaperdollSlot(leftX, startY + 5 * (colSlotSize + slotGap), colSlotSize, "👕", "Shirt", null);
            
            // Tabard Slot linked to Guild System
            var guildSys = GuildSystem.Instance;
            string tabardCrest = guildSys != null ? guildSys.CrestEmblem : "⚜";
            DrawPaperdollSlot(leftX, startY + 6 * (colSlotSize + slotGap), colSlotSize, tabardCrest, "Tabard", null, guildSys != null ? $"Guild Tabard: <{guildSys.GuildName}>" : null);

            DrawPaperdollSlot(leftX, startY + 7 * (colSlotSize + slotGap), colSlotSize, "🧤", "Wrists", null);

            // ==========================================
            // 2. RIGHT PAPERDOLL COLUMN (8 Equipment Slots)
            // ==========================================
            float rightSlotX = leftX + 175f;

            DrawPaperdollSlot(rightSlotX, startY + 0 * (colSlotSize + slotGap), colSlotSize, "🥊", "Hands", null);
            DrawPaperdollSlot(rightSlotX, startY + 1 * (colSlotSize + slotGap), colSlotSize, "🥋", "Waist", null);
            DrawPaperdollSlot(rightSlotX, startY + 2 * (colSlotSize + slotGap), colSlotSize, "👖", "Legs", null);
            DrawPaperdollSlot(rightSlotX, startY + 3 * (colSlotSize + slotGap), colSlotSize, "👢", "Feet", null);
            DrawPaperdollSlot(rightSlotX, startY + 4 * (colSlotSize + slotGap), colSlotSize, "💍", "Finger 1", null);
            DrawPaperdollSlot(rightSlotX, startY + 5 * (colSlotSize + slotGap), colSlotSize, "💍", "Finger 2", null);
            DrawPaperdollSlot(rightSlotX, startY + 6 * (colSlotSize + slotGap), colSlotSize, "💎", "Trinket 1", player.Trinket);
            DrawPaperdollSlot(rightSlotX, startY + 7 * (colSlotSize + slotGap), colSlotSize, "🏆", "Trinket 2", null);

            // ==========================================
            // 3. CENTER MODEL PREVIEW BOX & BOTTOM WEAPONS
            // ==========================================
            float centerBoxX = leftX + colSlotSize + 12f;
            float centerBoxW = rightSlotX - centerBoxX - 12f;
            float centerBoxH = 7 * (colSlotSize + slotGap);

            GUI.DrawTexture(new Rect(centerBoxX - 1f, startY - 1f, centerBoxW + 2f, centerBoxH + 2f), borderTex);
            GUI.DrawTexture(new Rect(centerBoxX, startY, centerBoxW, centerBoxH), panelBgTex);

            // Model silhouette / class badge
            string classGlyph = player.CharacterClass == "Warrior" ? "⚔" :
                                player.CharacterClass == "Paladin" ? "✝" :
                                player.CharacterClass == "Mage" ? "🔥" : "🗡";
            var modelStyle = new GUIStyle(titleStyle) { fontSize = 56, alignment = TextAnchor.MiddleCenter };
            modelStyle.normal.textColor = new Color(0.85f, 0.70f, 0.28f, 0.85f);
            GUI.Label(new Rect(centerBoxX, startY + 40f, centerBoxW, 90f), classGlyph, modelStyle);

            var nameStyle = new GUIStyle(headerStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            GUI.Label(new Rect(centerBoxX + 4f, startY + 140f, centerBoxW - 8f, 20f), player.CharacterName, nameStyle);

            var guildStyle = new GUIStyle(statLabelStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
            guildStyle.normal.textColor = new Color(0.4f, 0.95f, 0.4f);
            string gName = guildSys != null ? $"<{guildSys.GuildName}>" : "<Unaffiliated>";
            GUI.Label(new Rect(centerBoxX + 4f, startY + 160f, centerBoxW - 8f, 18f), gName, guildStyle);

            // Bottom Weapon Slots (Main Hand, Off Hand, Ranged)
            float botSlotY = startY + centerBoxH + 8f;
            float wSlotSize = 44f;
            float botStartX = leftX + (rightSlotX + colSlotSize - leftX - (3 * wSlotSize + 2 * 14f)) * 0.5f;

            DrawPaperdollSlot(botStartX, botSlotY, wSlotSize, "⚔", "Main Hand", player.Weapon);
            DrawPaperdollSlot(botStartX + wSlotSize + 14f, botSlotY, wSlotSize, "🛡", "Off Hand", null);
            DrawPaperdollSlot(botStartX + 2 * (wSlotSize + 14f), botSlotY, wSlotSize, "🏹", "Ranged", null);

            // ==========================================
            // 4. RIGHT STATS & COMBAT RATINGS COLUMN (380px)
            // ==========================================
            float statsX = rightSlotX + colSlotSize + 22f;
            float statsW = w - (statsX - x) - 10f;
            float statsY = y + 8f;

            // Box 1: Core Base Attributes
            float attrBoxH = 120f;
            GUI.DrawTexture(new Rect(statsX - 1f, statsY - 1f, statsW + 2f, attrBoxH + 2f), borderTex);
            GUI.DrawTexture(new Rect(statsX, statsY, statsW, attrBoxH), panelBgTex);

            GUI.Label(new Rect(statsX + 10f, statsY + 4f, statsW - 20f, 18f), "BASE ATTRIBUTES", headerStyle);

            int str = GetStrength(player);
            int agi = GetAgility(player);
            int sta = GetStamina(player);
            int intel = GetIntellect(player);
            int spi = GetSpirit(player);

            DrawStatLine(statsX + 12f, statsY + 24f, statsW - 24f, "Strength", $"{str}", "(+AP & Block)");
            DrawStatLine(statsX + 12f, statsY + 42f, statsW - 24f, "Agility", $"{agi}", "(+Crit & Dodge)");
            DrawStatLine(statsX + 12f, statsY + 60f, statsW - 24f, "Stamina", $"{sta}", $"(+{sta * 10} Max HP)");
            DrawStatLine(statsX + 12f, statsY + 78f, statsW - 24f, "Intellect", $"{intel}", $"(+{intel * 15} Max Mana)");
            DrawStatLine(statsX + 12f, statsY + 96f, statsW - 24f, "Spirit", $"{spi}", "(+HP & MP Regen)");

            // Box 2: Melee / Ranged Combat Ratings
            float combatY = statsY + attrBoxH + 10f;
            float combatH = 108f;
            GUI.DrawTexture(new Rect(statsX - 1f, combatY - 1f, statsW + 2f, combatH + 2f), borderTex);
            GUI.DrawTexture(new Rect(statsX, combatY, statsW, combatH), panelBgTex);

            GUI.Label(new Rect(statsX + 10f, combatY + 4f, statsW - 20f, 18f), "MELEE & COMBAT RATINGS", headerStyle);

            int ap = GetAttackPower(player);
            float crit = GetCritChance(player);
            int baseDmg = 24 + (player.Level - 1) * 3 + (player.Weapon != null ? player.Weapon.damage : 0);
            float speed = 2.40f;
            float dps = baseDmg / speed;

            DrawStatLine(statsX + 12f, combatY + 24f, statsW - 24f, "Attack Power", $"{ap}", "AP");
            DrawStatLine(statsX + 12f, combatY + 42f, statsW - 24f, "Damage", $"{baseDmg - 4} - {baseDmg + 5}", $"{dps:0.0} DPS");
            DrawStatLine(statsX + 12f, combatY + 60f, statsW - 24f, "Attack Speed", $"{speed:0.00}s", "");
            DrawStatLine(statsX + 12f, combatY + 78f, statsW - 24f, "Critical Strike", $"{crit:0.1}%", "Crit");

            // Box 3: Defense & Resistances
            float defY = combatY + combatH + 10f;
            float defH = 135f;
            GUI.DrawTexture(new Rect(statsX - 1f, defY - 1f, statsW + 2f, defH + 2f), borderTex);
            GUI.DrawTexture(new Rect(statsX, defY, statsW, defH), panelBgTex);

            GUI.Label(new Rect(statsX + 10f, defY + 4f, statsW - 20f, 18f), "DEFENSE & RESISTANCES", headerStyle);

            int armor = player.ArmorRating;
            float dr = (armor / (armor + 400f + 85f * player.Level)) * 100f;
            float dodge = GetDodgeChance(player);
            float block = GetBlockChance(player);

            DrawStatLine(statsX + 12f, defY + 24f, statsW - 24f, "Armor Rating", $"{armor}", $"({dr:0.0}% Reduction)");
            DrawStatLine(statsX + 12f, defY + 42f, statsW - 24f, "Dodge Chance", $"{dodge:0.1}%", "");
            DrawStatLine(statsX + 12f, defY + 60f, statsW - 24f, "Block / Parry", $"{block:0.1}%", "");

            // Resistances row (Fire 🔥, Frost ❄, Nature 🌿, Shadow 💀, Arcane ✨)
            float resY = defY + 84f;
            GUI.Label(new Rect(statsX + 12f, resY, 80f, 18f), "Resistances:", statLabelStyle);

            DrawResistBadge(statsX + 85f, resY - 2f, "🔥", "15", new Color(0.95f, 0.35f, 0.2f));
            DrawResistBadge(statsX + 130f, resY - 2f, "❄", "10", new Color(0.35f, 0.75f, 1.0f));
            DrawResistBadge(statsX + 175f, resY - 2f, "🌿", "10", new Color(0.3f, 0.85f, 0.3f));
            DrawResistBadge(statsX + 220f, resY - 2f, "💀", "20", new Color(0.7f, 0.35f, 0.9f));

            // Currencies Footer Box
            float currY = defY + 110f;
            string curStr = $"💰 <b>{player.Gold}</b>g  ·  🦴 <b>{player.BoneShards}</b> Shards  ·  ★ <b>{(BattlegroundSystem.Instance ? BattlegroundSystem.Instance.PlayerHonorPoints : 0)}</b> Honor";
            GUI.Label(new Rect(statsX + 12f, currY, statsW - 24f, 20f), curStr, statLabelStyle);
        }

        void DrawStatLine(float x, float y, float w, string label, string value, string sub)
        {
            GUI.Label(new Rect(x, y, w * 0.45f, 18f), label, statLabelStyle);
            GUI.Label(new Rect(x + w * 0.45f, y, w * 0.25f, 18f), value, statValueStyle);
            if (!string.IsNullOrEmpty(sub))
            {
                var sStyle = new GUIStyle(statLabelStyle) { fontSize = 10, alignment = TextAnchor.MiddleRight };
                sStyle.normal.textColor = new Color(0.65f, 0.65f, 0.65f);
                GUI.Label(new Rect(x + w * 0.70f, y, w * 0.30f, 18f), sub, sStyle);
            }
        }

        void DrawResistBadge(float x, float y, string icon, string val, Color col)
        {
            GUI.DrawTexture(new Rect(x, y, 40f, 18f), slotBgTex);
            var style = new GUIStyle(statLabelStyle) { fontSize = 10, fontStyle = FontStyle.Bold };
            style.normal.textColor = col;
            GUI.Label(new Rect(x + 2f, y, 38f, 18f), $"{icon} {val}", style);
        }

        void DrawPaperdollSlot(float x, float y, float size, string defaultIcon, string slotName, GearItem equippedItem, string overrideDesc = null)
        {
            Rect r = new Rect(x, y, size, size);

            GUI.DrawTexture(new Rect(x - 1f, y - 1f, size + 2f, size + 2f), borderTex);
            GUI.DrawTexture(r, slotBgTex);

            if (equippedItem != null)
            {
                // Border rarity color
                Color rCol = equippedItem.color;
                GUI.DrawTexture(new Rect(x, y, size, 2f), MakeColorTex(rCol));
                GUI.DrawTexture(new Rect(x, y + size - 2f, size, 2f), MakeColorTex(rCol));
                GUI.DrawTexture(new Rect(x, y, 2f, size), MakeColorTex(rCol));
                GUI.DrawTexture(new Rect(x + size - 2f, y, 2f, size), MakeColorTex(rCol));

                var iconStyle = new GUIStyle(titleStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                string icon = equippedItem.slot == GearSlot.Weapon ? "⚔" : equippedItem.slot == GearSlot.Armor ? "🛡" : "💍";
                GUI.Label(r, icon, iconStyle);

                if (r.Contains(Event.current.mousePosition))
                {
                    hoveredSlotName = slotName;
                    hoveredItemName = equippedItem.name;
                    hoveredItemStats = equippedItem.Stats;
                    hoveredItemRarity = equippedItem.rarity;
                    hoveredRect = new Rect(Event.current.mousePosition.x + 12f, Event.current.mousePosition.y + 12f, 210f, 75f);
                }
            }
            else
            {
                // Empty slot faint icon
                var ghostStyle = new GUIStyle(titleStyle) { fontSize = 18, alignment = TextAnchor.MiddleCenter };
                ghostStyle.normal.textColor = new Color(0.4f, 0.45f, 0.55f, 0.5f);
                GUI.Label(r, defaultIcon, ghostStyle);

                if (r.Contains(Event.current.mousePosition))
                {
                    hoveredSlotName = slotName;
                    hoveredItemName = !string.IsNullOrEmpty(overrideDesc) ? overrideDesc : $"Empty {slotName} Slot";
                    hoveredItemStats = "No item equipped.";
                    hoveredItemRarity = "Common";
                    hoveredRect = new Rect(Event.current.mousePosition.x + 12f, Event.current.mousePosition.y + 12f, 190f, 50f);
                }
            }
        }

        void DrawItemTooltip(Rect rect)
        {
            var gear = ItemCatalog.Get(hoveredItemName);
            string tooltipText = gear != null ? gear.FormattedTooltip :
                $"<b>{hoveredItemName}</b>\nSlot: {hoveredSlotName} · {hoveredItemRarity}\n{hoveredItemStats}";

            float tipW = 240f;
            float tipH = 135f;
            Rect tipRect = new Rect(rect.x + rect.width + 6f, rect.y, tipW, tipH);
            if (tipRect.xMax > Screen.width - 20f) tipRect.x = rect.x - tipW - 6f;

            GUI.DrawTexture(new Rect(tipRect.x - 2f, tipRect.y - 2f, tipRect.width + 4f, tipRect.height + 4f), borderTex);
            GUI.DrawTexture(tipRect, bgTex);

            var tipStyle = new GUIStyle(tooltipBodyStyle) { richText = true, wordWrap = true, fontSize = 11 };
            GUI.Label(new Rect(tipRect.x + 8f, tipRect.y + 6f, tipRect.width - 16f, tipRect.height - 12f), tooltipText, tipStyle);
        }

        void DrawSkillsTab(float x, float y, float w, float h)
        {
            float colW = w * 0.48f;

            // Weapon Proficiencies
            GUI.Label(new Rect(x + 10f, y + 8f, colW, 20f), "WEAPON & COMBAT PROFICIENCIES", headerStyle);
            string[] weapons = { "Swords (Two-Handed)", "Axes (Two-Handed)", "Maces (One-Handed)", "Shield Defense", "Unarmed Combat" };
            int[] currentSkills = { 150, 150, 125, 140, 110 };

            for (int i = 0; i < weapons.Length; i++)
            {
                float sy = y + 32f + i * 36f;
                GUI.Label(new Rect(x + 12f, sy, colW - 24f, 16f), weapons[i], statLabelStyle);
                float r = (float)currentSkills[i] / 150f;
                GUI.DrawTexture(new Rect(x + 12f, sy + 18f, colW - 24f, 10f), slotBgTex);
                GUI.DrawTexture(new Rect(x + 12f, sy + 18f, (colW - 24f) * r, 10f), barFillTex);
                GUI.Label(new Rect(x + 12f, sy + 16f, colW - 24f, 14f), $"{currentSkills[i]} / 150", statValueStyle);
            }

            // Primary & Secondary Crafting Skills
            float rightX = x + colW + 20f;
            GUI.Label(new Rect(rightX, y + 8f, colW, 20f), "PROFESSIONS & REPUTATION RANKS", headerStyle);
            string[] profs = { "Mining (Apprentice)", "Blacksmithing (Journeyman)", "First Aid", "Cooking", "Riding (Artisan 310%)" };
            int[] profSkills = { 75, 120, 150, 45, 300 };
            int[] profMax = { 150, 150, 150, 150, 300 };

            for (int i = 0; i < profs.Length; i++)
            {
                float sy = y + 32f + i * 36f;
                GUI.Label(new Rect(rightX + 2f, sy, colW - 24f, 16f), profs[i], statLabelStyle);
                float r = (float)profSkills[i] / profMax[i];
                GUI.DrawTexture(new Rect(rightX + 2f, sy + 18f, colW - 24f, 10f), slotBgTex);
                GUI.DrawTexture(new Rect(rightX + 2f, sy + 18f, (colW - 24f) * r, 10f), barFillTex);
                GUI.Label(new Rect(rightX + 2f, sy + 16f, colW - 24f, 14f), $"{profSkills[i]} / {profMax[i]}", statValueStyle);
            }
        }

        void DrawHonorTab(float x, float y, float w, float h)
        {
            var bgSys = BattlegroundSystem.Instance;
            int honor = bgSys != null ? bgSys.PlayerHonorPoints : 0;

            GUI.Label(new Rect(x + 14f, y + 10f, w - 28f, 22f), "PLAYER VS. PLAYER STANDINGS & HONOR", headerStyle);

            string title = PlayerTitleSystem.ActiveTitle.Contains("Champion") ? "<Champion of the Valley>" : "<Combatant>";
            GUI.Label(new Rect(x + 14f, y + 36f, w - 28f, 20f), $"Active PvP Title: <b>{title}</b>", statLabelStyle);
            GUI.Label(new Rect(x + 14f, y + 58f, w - 28f, 20f), $"Lifetime Honor Points: <b>{honor:N0} ★</b>", statValueStyle);

            GUI.Label(new Rect(x + 14f, y + 90f, w - 28f, 20f), "BATTLEGROUND HISTORY:", headerStyle);
            string bgStats =
                "• <b>Crossroads Skirmish (3v3):</b> 12 Matches Played (9 Victories, 3 Defeats)\n" +
                "• <b>Warsong Gulch (10v10):</b> 4 Flag Captures · 35 Honorable Kills\n" +
                "• <b>Current PvP Standing:</b> Rank 4 (Master Sergeant)\n" +
                "• <b>Next Rank Threshold:</b> 450 / 1,000 Honor Points";
            GUI.Label(new Rect(x + 14f, y + 115f, w - 28f, 90f), bgStats, statLabelStyle);

            if (GUI.Button(new Rect(x + 14f, y + 215f, 180f, 28f), "Open Battlegrounds [Shift+H]", closeBtnStyle))
            {
                if (bgSys) bgSys.IsWindowOpen = true;
                IsOpen = false;
            }
        }

        Texture2D MakeColorTex(Color col)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, col);
            t.Apply();
            return t;
        }

        void InitStyles()
        {
            if (bgTex != null) return;

            bgTex = MakeColorTex(new Color(0.06f, 0.08f, 0.12f, 0.96f));
            borderTex = MakeColorTex(new Color(0.85f, 0.70f, 0.28f, 0.95f)); // Gold border
            panelBgTex = MakeColorTex(new Color(0.04f, 0.05f, 0.08f, 0.88f));
            slotBgTex = MakeColorTex(new Color(0.03f, 0.04f, 0.06f, 0.95f));
            barFillTex = MakeColorTex(new Color(0.2f, 0.65f, 0.95f));

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.40f);

            statLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            statLabelStyle.normal.textColor = Color.white;

            statValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            statValueStyle.normal.textColor = new Color(1f, 0.90f, 0.45f);

            tooltipTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            tooltipBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            tooltipBodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);

            closeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
