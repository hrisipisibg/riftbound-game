using System;
using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        string selectedItem = "starter-axe";
        Vector2 bagScroll;

        void DrawEquipment(float width, float height)
        {
            float windowW = 820f, windowH = 490f;
            float x = width / 2f - windowW / 2f, y = height / 2f - windowH / 2f;

            // Outer dark gothic panel with gold border
            Fill(new Rect(x - 4f, y - 4f, windowW + 8f, windowH + 8f), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            Fill(new Rect(x, y, windowW, windowH), new Color(0.06f, 0.08f, 0.11f, 0.97f));

            // Header Title Bar
            Fill(new Rect(x, y, windowW, 36f), new Color(0.12f, 0.15f, 0.20f, 0.98f));
            Fill(new Rect(x, y + 35f, windowW, 1.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));

            string headerTitle = $"CHARACTER & INVENTORY — {PlayerTitleSystem.GetFormattedName(player)} (Lv {player.Level} {player.CharacterRace} {player.CharacterClass})";
            GUI.Label(new Rect(x + 18f, y + 6f, windowW - 60f, 26f), headerTitle, label);

            // Close button [✕]
            if (GUI.Button(new Rect(x + windowW - 32f, y + 6f, 24f, 24f), "✕", microBtnStyle))
            {
                player.InventoryOpen = false;
                AudioEffects.PlayClick();
            }

            // ==========================================
            // LEFT COLUMN: Paperdoll & Equipped Gear (400px)
            // ==========================================
            float leftW = 390f;
            float leftX = x + 16f;
            float leftY = y + 46f;

            GUI.Label(new Rect(leftX, leftY, leftW, 20f), "EQUIPPED GEAR", label);

            // Slot 1: Weapon Slot
            DrawEquippedSlot(leftX, leftY + 24f, leftW, 46f, "⚔", "Weapon", player.Weapon, () => {
                selectedItem = player.EquippedWeaponId;
            });

            // Slot 2: Chest Armor Slot
            DrawEquippedSlot(leftX, leftY + 76f, leftW, 46f, "🛡", "Chest Armor", player.Armor, () => {
                if (player.Armor != null) selectedItem = player.Armor.id;
            });

            // Slot 3: Trinket Slot
            DrawEquippedSlot(leftX, leftY + 128f, leftW, 46f, "💍", "Trinket", player.Trinket, () => {
                if (player.Trinket != null) selectedItem = player.Trinket.id;
            });

            // Character Combat Attributes Summary Box
            float statsY = leftY + 184f;
            float statsH = 175f;
            Fill(new Rect(leftX, statsY, leftW, statsH), new Color(0.04f, 0.05f, 0.07f, 0.85f));
            Fill(new Rect(leftX, statsY, leftW, 1f), new Color(0.35f, 0.30f, 0.15f, 0.7f));

            var statHeaderStyle = new GUIStyle(label) { fontStyle = FontStyle.Bold };
            statHeaderStyle.normal.textColor = new Color(0.95f, 0.85f, 0.40f);
            GUI.Label(new Rect(leftX + 12f, statsY + 6f, leftW - 24f, 20f), "CHARACTER ATTRIBUTES & RATINGS", statHeaderStyle);

            int baseDmg = 24 + (player.Level - 1) * 3 + Mathf.RoundToInt(player.WeaponBonus);
            int armor = player.ArmorRating;
            float armorReduc = (armor / (armor + 40f)) * 100f;
            int maxHp = player.Stats ? Mathf.RoundToInt(player.Stats.maxHealth) : 150;
            string mountSpeed = player.Mounted && player.SelectedMount ? (player.SelectedMount.isFlying ? "310% (Flight)" : "200% (Mount)") : "165% (Run)";

            string statsText =
                $"<b>Melee Weapon Damage:</b> {baseDmg} Physical DPS\n" +
                $"<b>Armor Rating:</b> {armor} ({armorReduc:0.0}% Physical Damage Reduction)\n" +
                $"<b>Maximum Health:</b> {maxHp} HP\n" +
                $"<b>Class Resource:</b> {player.MaxClassResource} {player.ClassResourceName}\n" +
                $"<b>Movement Speed:</b> {mountSpeed}\n" +
                $"<b>Race / Class:</b> {player.CharacterRace} {player.CharacterClass}\n" +
                $"<b>Active Title:</b> {(string.IsNullOrEmpty(PlayerTitleSystem.ActiveTitle) ? "(None)" : PlayerTitleSystem.ActiveTitle)}";

            GUI.Label(new Rect(leftX + 12f, statsY + 28f, leftW - 24f, statsH - 32f), statsText, small);

            // ==========================================
            // RIGHT COLUMN: 16-Slot WoW Backpack (390px)
            // ==========================================
            float rightX = x + 418f;
            float rightY = leftY;
            float rightW = 384f;

            GUI.Label(new Rect(rightX, rightY, rightW, 20f), "BACKPACK INVENTORY (16 SLOTS)", label);

            // 4x4 Grid of Item Slots
            float slotBoxSize = 44f;
            float slotGap = 6f;
            float gridStartX = rightX;
            float gridStartY = rightY + 24f;

            var items = player.OwnedItems;
            int totalSlots = 16;

            for (int i = 0; i < totalSlots; i++)
            {
                int col = i % 4;
                int row = i / 4;
                float sx = gridStartX + col * (slotBoxSize + slotGap);
                float sy = gridStartY + row * (slotBoxSize + slotGap);

                Rect slotRect = new Rect(sx, sy, slotBoxSize, slotBoxSize);
                bool hasItem = i < items.Count;
                string itemId = hasItem ? items[i] : null;
                var item = hasItem ? ItemCatalog.Get(itemId) : null;

                // Slot background
                Fill(slotRect, new Color(0.04f, 0.05f, 0.07f, 0.90f));

                if (item != null)
                {
                    bool isEquipped = player.IsEquipped(item);
                    bool isSelected = selectedItem == item.id;

                    // Border color by rarity (or selected gold)
                    Color borderCol = isSelected ? new Color(1f, 0.88f, 0.35f) : item.color;
                    Fill(new Rect(sx, sy, slotBoxSize, 2f), borderCol);
                    Fill(new Rect(sx, sy + slotBoxSize - 2f, slotBoxSize, 2f), borderCol);
                    Fill(new Rect(sx, sy, 2f, slotBoxSize), borderCol);
                    Fill(new Rect(sx + slotBoxSize - 2f, sy, 2f, slotBoxSize), borderCol);

                    // Item Icon
                    string icon = item.slot == GearSlot.Weapon ? "⚔" : item.slot == GearSlot.Armor ? "🛡" : "💍";
                    GUI.Label(new Rect(sx, sy + 6f, slotBoxSize, slotBoxSize - 6f), icon, questButton);

                    // Equipped badge [E]
                    if (isEquipped)
                    {
                        Fill(new Rect(sx + slotBoxSize - 14f, sy + 2f, 12f, 12f), new Color(0.18f, 0.75f, 0.25f, 0.95f));
                        GUI.Label(new Rect(sx + slotBoxSize - 14f, sy, 12f, 12f), "E", badgeStyle);
                    }

                    // Click Handling: Left Click to select, Right Click to equip instantly!
                    if (Event.current.type == EventType.MouseDown && slotRect.Contains(Event.current.mousePosition))
                    {
                        if (Event.current.button == 0) // Left click
                        {
                            selectedItem = item.id;
                            AudioEffects.PlayClick();
                            Event.current.Use();
                        }
                        else if (Event.current.button == 1) // Right click to equip
                        {
                            if (!isEquipped && !player.InCombat())
                            {
                                player.EquipItem(item.id);
                                AudioEffects.PlayBladeHit();
                            }
                            Event.current.Use();
                        }
                    }
                }
                else
                {
                    // Empty slot faint border
                    Fill(new Rect(sx, sy, slotBoxSize, 1f), new Color(0.2f, 0.2f, 0.25f, 0.6f));
                    Fill(new Rect(sx, sy + slotBoxSize - 1f, slotBoxSize, 1f), new Color(0.2f, 0.2f, 0.25f, 0.6f));
                    Fill(new Rect(sx, sy, 1f, slotBoxSize), new Color(0.2f, 0.2f, 0.25f, 0.6f));
                    Fill(new Rect(sx + slotBoxSize - 1f, sy, 1f, slotBoxSize), new Color(0.2f, 0.2f, 0.25f, 0.6f));
                }
            }

            // Selected Item Detailed Inspection Box (WoW-style Tooltip)
            float inspectY = gridStartY + 4 * (slotBoxSize + slotGap) + 4f;
            float inspectH = 148f;
            Fill(new Rect(rightX, inspectY, rightW, inspectH), new Color(0.04f, 0.05f, 0.07f, 0.94f));

            if (!player.OwnsItem(selectedItem)) selectedItem = player.EquippedWeaponId;
            var curSelected = ItemCatalog.Get(selectedItem);

            if (curSelected != null)
            {
                Fill(new Rect(rightX, inspectY, rightW, 2f), curSelected.color);

                var tipStyle = new GUIStyle(small) { richText = true, wordWrap = true, fontSize = 11 };
                GUI.Label(new Rect(rightX + 10f, inspectY + 6f, rightW - 20f, 106f), curSelected.FormattedTooltip, tipStyle);

                // Equip / Equipped Button
                bool isEquipped = player.IsEquipped(curSelected);
                GUI.enabled = !isEquipped && !player.InCombat();
                string equipBtnText = isEquipped ? "✔ EQUIPPED" : "EQUIP ITEM [RMB]";
                if (GUI.Button(new Rect(rightX + 10f, inspectY + 114f, 160f, 28f), equipBtnText, questButton))
                {
                    player.EquipItem(curSelected.id);
                    AudioEffects.PlayBladeHit();
                }
                GUI.enabled = true;

                if (player.InCombat())
                {
                    var redSmall = new GUIStyle(small);
                    redSmall.normal.textColor = new Color(1f, 0.4f, 0.4f);
                    GUI.Label(new Rect(rightX + 180f, inspectY + 118f, 190f, 24f), "Cannot equip in combat", redSmall);
                }
            }

            // ==========================================
            // BOTTOM BAR: Currencies & Quick Actions (width)
            // ==========================================
            float botY = y + windowH - 42f;
            Fill(new Rect(x, botY, windowW, 42f), new Color(0.08f, 0.10f, 0.14f, 0.98f));
            Fill(new Rect(x, botY, windowW, 1.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));

            string currencies = $"<b>Gold:</b> {player.Gold} 💰   <b>Bone Shards:</b> {player.BoneShards} 💀   <b>Potions:</b> {player.Potions}/10 🧪";
            GUI.Label(new Rect(x + 18f, botY + 10f, 450f, 24f), currencies, label);

            // Sell Shards button if holding shards
            if (player.BoneShards > 0)
            {
                if (GUI.Button(new Rect(x + 470f, botY + 7f, 160f, 28f), $"Sell Shards (+{player.BoneShards * 2}g)", questButton))
                {
                    player.SellShards();
                    AudioEffects.PlayLootCoin();
                }
            }

            // Close Button
            if (GUI.Button(new Rect(x + windowW - 130f, botY + 7f, 115f, 28f), "Close [C/I]", questButton))
            {
                player.InventoryOpen = false;
                AudioEffects.PlayClick();
            }
        }

        void DrawEquippedSlot(float x, float y, float w, float h, string icon, string slotName, GearItem item, Action onClick)
        {
            Fill(new Rect(x, y, w, h), new Color(0.04f, 0.05f, 0.07f, 0.88f));

            Color borderCol = item != null ? item.color : new Color(0.35f, 0.35f, 0.40f, 0.6f);
            Fill(new Rect(x, y, w, 1.5f), borderCol);
            Fill(new Rect(x, y + h - 1.5f, w, 1.5f), borderCol);
            Fill(new Rect(x, y, 1.5f, h), borderCol);
            Fill(new Rect(x + w - 1.5f, y, 1.5f, h), borderCol);

            // Slot Icon
            Fill(new Rect(x + 4f, y + 4f, 38f, 38f), new Color(0.12f, 0.14f, 0.18f, 0.95f));
            GUI.Label(new Rect(x + 4f, y + 4f, 38f, 38f), icon, questButton);

            if (item != null)
            {
                var nameStyle = new GUIStyle(label) { fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = item.color;
                GUI.Label(new Rect(x + 48f, y + 4f, w - 130f, 18f), item.name, nameStyle);
                GUI.Label(new Rect(x + 48f, y + 22f, w - 130f, 18f), $"{item.rarity} · {item.Stats}", small);

                if (GUI.Button(new Rect(x + w - 75f, y + 8f, 68f, 28f), "Inspect", microBtnStyle))
                {
                    onClick?.Invoke();
                    AudioEffects.PlayClick();
                }
            }
            else
            {
                var emptyStyle = new GUIStyle(small);
                emptyStyle.normal.textColor = new Color(0.6f, 0.6f, 0.65f);
                GUI.Label(new Rect(x + 48f, y + 14f, w - 60f, 18f), $"<Empty {slotName} Slot>", emptyStyle);
            }
        }
    }
}
