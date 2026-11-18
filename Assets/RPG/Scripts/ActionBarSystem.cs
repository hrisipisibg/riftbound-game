using System;
using UnityEngine;

namespace Riftbound
{
    public class ActionBarSystem : MonoBehaviour
    {
        public static ActionBarSystem Instance { get; private set; }

        GUIStyle slotStyle, keybindStyle, costStyle, cdStyle, labelStyle, tipTitleStyle, tipBodyStyle;
        Texture2D bgTex, borderTex, healthBgTex, healthFillTex, resourceFillTex, cdOverlayTex;
        Texture2D abilityAtlas;
        string activeClass;

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void InitStyles()
        {
            if (slotStyle != null) return;
            abilityAtlas = Resources.Load<Texture2D>("UI/AbilityIcons");

            bgTex = MakeTex(1, 1, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            borderTex = MakeTex(1, 1, new Color(0.30f, 0.29f, 0.25f, .85f));
            healthBgTex = MakeTex(1, 1, new Color(0.12f, 0.04f, 0.04f, 0.95f));
            healthFillTex = MakeTex(1, 1, new Color(0.16f, 0.72f, 0.22f, 1f));
            resourceFillTex = MakeTex(1, 1, new Color(0.85f, 0.22f, 0.18f, 1f));
            cdOverlayTex = MakeTex(1, 1, new Color(0.0f, 0.0f, 0.0f, 0.72f));

            slotStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 20,
                fontStyle = FontStyle.Bold
            };
            slotStyle.normal.textColor = Color.white;

            keybindStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            keybindStyle.normal.textColor = new Color(1.0f, 0.88f, 0.32f);

            costStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.LowerRight
            };
            costStyle.normal.textColor = new Color(0.75f, 0.85f, 1.0f);

            cdStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            cdStyle.normal.textColor = new Color(1.0f, 0.95f, 0.5f);

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            labelStyle.normal.textColor = Color.white;

            tipTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            tipTitleStyle.normal.textColor = new Color(1.0f, 0.85f, 0.25f);

            tipBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            tipBodyStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f);
        }

        public void DrawActionBar(PlayerController player, float screenW, float screenH)
        {
            if (!player || !player.Stats || FrontendMenu.IsOpen) return;
            InitStyles();
            activeClass = player.CharacterClass;

            const int slotCount = 9;
            const float slotSize = 51f;
            const float spacing = 5f;
            float totalBarW = slotCount * slotSize + (slotCount - 1) * spacing;
            float barX = screenW / 2f - totalBarW / 2f;
            float barY = screenH - 71f;

            GUI.DrawTexture(new Rect(barX - 8f, barY - 10f, totalBarW + 16f, slotSize + 18f), bgTex);

            // Sleek, Low-Profile Experience Bar (3.5px) resting on top of Action Slots
            float xpRatio = player.Level == 5 ? 1f : Mathf.Clamp01((float)(player.Experience - player.LevelFloor) / Mathf.Max(1, player.NextLevelExperience - player.LevelFloor));
            GUI.DrawTexture(new Rect(barX, barY - 5f, totalBarW, 3.5f), borderTex);
            var prevGuiCol = GUI.color;
            GUI.color = new Color(0.55f, 0.25f, 0.90f);
            GUI.DrawTexture(new Rect(barX, barY - 5f, totalBarW * xpRatio, 3.5f), healthFillTex);
            GUI.color = prevGuiCol;

            // --- Action Slots (1 to 8) ---
            var ab1 = ClassSystem.GetAbilityInfo(player.CharacterClass, 1);
            var ab2 = ClassSystem.GetAbilityInfo(player.CharacterClass, 2);
            var ab3 = ClassSystem.GetAbilityInfo(player.CharacterClass, 3);

            string hoverTitle = null;
            string hoverSub = null;
            string hoverDesc = null;

            // Slot 1: Primary Strike
            string s1Icon = player.CharacterClass == "Mage" ? "🔥" : player.CharacterClass == "Rogue" ? "🗡" : player.CharacterClass == "Paladin" ? "✝" : "⚔";
            string s1Cost = player.CharacterClass == "Mage" ? "15 MP" : player.CharacterClass == "Rogue" ? "40 NR" : "";
            if (DrawSlot(new Rect(barX, barY, slotSize, slotSize), "1", s1Icon, s1Cost, 0f, () => player.CastAbility1Public()))
            {
                hoverTitle = ab1.name;
                hoverSub = $"{s1Cost}  •  Primary ability  •  Instant";
                hoverDesc = ab1.desc + "\nBuilds primary class resource when attacking.";
            }

            // Slot 2: Cleave / Burst / Nova
            float cd2 = Mathf.Max(0f, player.CleaveReady - Time.time);
            string s2Icon = player.CharacterClass == "Mage" ? "❄" : player.CharacterClass == "Rogue" ? "🩸" : player.CharacterClass == "Paladin" ? "☼" : "🌪";
            string s2Cost = player.CharacterClass == "Mage" ? "25 MP" : player.CharacterClass == "Rogue" ? "35 NR" : player.CharacterClass == "Paladin" ? "2 HP" : "20 RG";
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 1, barY, slotSize, slotSize), "2", s2Icon, s2Cost, cd2, () => player.CastAbility2Public()))
            {
                hoverTitle = ab2.name;
                hoverSub = $"{s2Cost}  •  Instant";
                hoverDesc = ab2.desc + "\nDevastating multi-target burst ability.";
            }

            // Slot 3: Utility / Healing / Shield
            float cd3 = Mathf.Max(0f, player.HealReady - Time.time);
            string s3Icon = player.CharacterClass == "Mage" ? "✦" : player.CharacterClass == "Rogue" ? "⚡" : player.CharacterClass == "Paladin" ? "✚" : "♥";
            string s3Cost = player.CharacterClass == "Mage" ? "20 MP" : player.CharacterClass == "Rogue" ? "20 NR" : player.CharacterClass == "Paladin" ? "3 HP" : "";
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 2, barY, slotSize, slotSize), "3", s3Icon, s3Cost, cd3, () => player.CastAbility3Public()))
            {
                hoverTitle = ab3.name;
                hoverSub = $"{s3Cost}  •  Instant";
                hoverDesc = ab3.desc + "\nDefensive recovery or mobility ability.";
            }

            // Slot 4: Health Potion
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 3, barY, slotSize, slotSize), "4", "🧪", $"x{player.Potions}", 0f, () => player.UsePotionPublic()))
            {
                hoverTitle = "Health Potion";
                hoverSub = "Consumable  •  Instant";
                hoverDesc = $"Restores 70 Health points instantly.\nYou currently hold {player.Potions} potion(s). Purchase more from town vendors.";
            }

            // Slot 5: Spell Interrupt [F]
            float cdInterrupt = player.InterruptCooldownRemaining;
            string intName = player.InterruptSpellName;
            string intIcon = player.InterruptIcon;
            Rect intRect = new Rect(barX + (slotSize + spacing) * 4, barY, slotSize, slotSize);
            if (DrawSlot(intRect, "F", intIcon, "", cdInterrupt, () => player.CastInterruptPublic()))
            {
                hoverTitle = $"{intName} (Interrupt)";
                hoverSub = $"Instant  •  12.0s Cooldown  •  {(player.CharacterClass == "Mage" ? "25m Range" : "Melee Range")}";
                hoverDesc = $"Interrupts the enemy's spellcast, locking out that school of magic for 4 seconds.\nPress [F] or click when target begins casting.";
            }
            if (player.Target != null && CastBarSystem.IsTargetCasting(player.Target) && cdInterrupt <= 0.05f)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 9f);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.82f, 0.18f, pulse);
                GUI.DrawTexture(new Rect(intRect.x - 3, intRect.y - 3, intRect.width + 6, 3), borderTex);
                GUI.DrawTexture(new Rect(intRect.x - 3, intRect.yMax, intRect.width + 6, 3), borderTex);
                GUI.DrawTexture(new Rect(intRect.x - 3, intRect.y, 3, intRect.height), borderTex);
                GUI.DrawTexture(new Rect(intRect.xMax, intRect.y, 3, intRect.height), borderTex);
                GUI.color = prev;
            }

            // Slot 6: Guard / Shield Block [Q]
            float cdGuard = Mathf.Max(0f, player.GuardReady - Time.time);
            string guardLabel = player.Blocking ? "ACTIVE" : "";
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 5, barY, slotSize, slotSize), "Q", "🛡", guardLabel, cdGuard, () => player.ToggleGuardPublic()))
            {
                hoverTitle = "Guard (Shield Block)";
                hoverSub = "Stance  •  Instant";
                hoverDesc = "Raise your guard to absorb 50% of incoming damage for up to 3 seconds. Recharges every 6 seconds.";
            }

            // Slot 7: Mount [Z]
            string mountIcon = (player.SelectedMount && player.SelectedMount.isFlying) ? "🦅" : "🐎";
            string mountStatus = player.Mounted ? "DISMOUNT" : "SUMMON";
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 6, barY, slotSize, slotSize), "Z", mountIcon, mountStatus, 0f, () => player.ToggleMountPublic()))
            {
                hoverTitle = player.Mounted ? "Dismount" : (player.SelectedMount ? player.SelectedMount.displayName : "Celestial Winged Steed");
                hoverSub = (player.SelectedMount && player.SelectedMount.isFlying) ? "Flying Mount  •  1.5s Cast" : "Ground Mount  •  1.5s Cast";
                hoverDesc = "Summons your noble steed to increase travel speed. Flying mounts allow soaring high above the 6 zones.";
            }

            // Slot 8: Hearthstone [H]
            float cdHearth = Mathf.Max(0f, player.HearthReady - Time.time);
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 7, barY, slotSize, slotSize), "H", "🌀", "", cdHearth, () => player.TriggerHearthstonePublic()))
            {
                hoverTitle = "Hearthstone";
                hoverSub = "5.0s Cast  •  60s Cooldown";
                hoverDesc = "Return to your home settlement. Moving or taking damage cancels the cast.";
            }

            // Slot 9: World Atlas Map [M]
            if (DrawSlot(new Rect(barX + (slotSize + spacing) * 8, barY, slotSize, slotSize), "M", "🗺", "MAP", 0f, () => player.ToggleMapPublic()))
            {
                hoverTitle = "World Map";
                hoverSub = "Continental Map  •  [M]";
                hoverDesc = "Explore the world and place a travel waypoint. Roads connect settlements and encounters.";
            }

            // --- 3. Mouseover Tooltip ---
            if (!string.IsNullOrEmpty(hoverTitle))
            {
                DrawTooltip(barX, barY - 105f, 290f, 96f, hoverTitle, hoverSub, hoverDesc);
            }
        }

        bool DrawSlot(Rect rect, string keybind, string icon, string cost, float cd, Action onClick)
        {
            // Slot background & border
            GUI.DrawTexture(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), borderTex);
            GUI.DrawTexture(rect, bgTex);

            // Icon symbol
            int cell = IconCell(activeClass, keybind);
            if (abilityAtlas && cell >= 0)
                GUI.DrawTextureWithTexCoords(new Rect(rect.x + 2, rect.y + 2, rect.width - 4, rect.height - 4),
                    abilityAtlas, new Rect((cell % 4) * .25f + .003f, (3 - cell / 4) * .25f + .003f, .244f, .244f));
            else
                GUI.Label(new Rect(rect.x, rect.y + 8, rect.width, rect.height - 16), keybind == "Z" ? "Ride" : keybind == "M" ? "Map" : keybind, labelStyle);
            bool hovered = rect.Contains(Event.current.mousePosition);
            if (hovered)
            {
                var tint = GUI.color; GUI.color = new Color(.9f, .78f, .50f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1), borderTex);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1, rect.width, 1), borderTex);
                GUI.color = tint;
            }

            // Cooldown overlay
            if (cd > 0.05f)
            {
                GUI.DrawTexture(rect, cdOverlayTex);
                GUI.Label(rect, $"{cd:F1}s", cdStyle);
            }

            // Keybind badge (Top-Left)
            GUI.DrawTexture(new Rect(rect.x + 2, rect.y + 2, 19, 18), bgTex);
            GUI.Label(new Rect(rect.x + 4, rect.y + 1, 24, 19), keybind, keybindStyle);

            // Cost or sub-label (Bottom-Right)
            if (!string.IsNullOrEmpty(cost) && (keybind == "4" || cost == "ACTIVE"))
            {
                GUI.Label(new Rect(rect.x, rect.yMax - 15, rect.width - 3, 14), cost, costStyle);
            }

            // Click handling
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hovered)
            {
                onClick?.Invoke();
                Event.current.Use();
            }

            return hovered;
        }

        public static int IconCell(string characterClass, string key)
        {
            int index = key == "1" ? 0 : key == "2" ? 1 : key == "3" ? 2 : -1;
            if (index >= 0)
                return (characterClass == "Mage" ? 3 : characterClass == "Rogue" ? 6 : characterClass == "Paladin" ? 9 : 0) + index;
            return key == "4" ? 12 : key == "F" ? 13 : key == "Q" ? 14 : key == "H" ? 15 : -1;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (var texture in new[] { bgTex, borderTex, healthBgTex, healthFillTex, resourceFillTex, cdOverlayTex })
                if (texture) Destroy(texture);
        }

        void DrawStatusBar(float x, float y, float width, float height, float current, float max, Color fillColor, string title, string text)
        {
            GUI.DrawTexture(new Rect(x - 1, y - 1, width + 2, height + 2), borderTex);
            GUI.DrawTexture(new Rect(x, y, width, height), healthBgTex);

            float pct = Mathf.Clamp01(max > 0 ? current / max : 0f);
            if (pct > 0.001f)
            {
                var oldBg = GUI.color;
                GUI.color = fillColor;
                GUI.DrawTexture(new Rect(x, y, width * pct, height), healthFillTex);
                GUI.color = oldBg;
            }

            GUI.Label(new Rect(x, y - 1, width, height), text, labelStyle);
        }

        void DrawTooltip(float x, float y, float width, float height, string title, string sub, string desc)
        {
            // Tooltip dark panel with golden trim
            GUI.DrawTexture(new Rect(x - 2, y - 2, width + 4, height + 4), borderTex);
            GUI.DrawTexture(new Rect(x, y, width, height), bgTex);

            GUI.Label(new Rect(x + 10, y + 8, width - 20, 20), title, tipTitleStyle);
            if (!string.IsNullOrEmpty(sub))
            {
                GUI.Label(new Rect(x + 10, y + 26, width - 20, 18), sub, keybindStyle);
            }
            GUI.Label(new Rect(x + 10, y + 46, width - 20, 44), desc, tipBodyStyle);
        }

        Texture2D MakeTex(int w, int h, Color col)
        {
            var pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            var t = new Texture2D(w, h);
            t.SetPixels(pix);
            t.Apply();
            return t;
        }
    }
}
