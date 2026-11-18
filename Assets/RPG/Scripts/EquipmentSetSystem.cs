using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class EquipmentSetSystem : MonoBehaviour
    {
        public static EquipmentSetSystem Instance { get; private set; }

        [Serializable]
        public class EquipmentSet
        {
            public string name;
            public string icon;
            public string weaponId;
            public string armorId;
            public string trinketId;

            public EquipmentSet(string name, string icon, string weaponId, string armorId, string trinketId)
            {
                this.name = name;
                this.icon = icon;
                this.weaponId = weaponId;
                this.armorId = armorId;
                this.trinketId = trinketId;
            }
        }

        public List<EquipmentSet> savedSets = new List<EquipmentSet>();
        public int selectedSetIndex = 0;
        public bool IsWindowOpen { get; set; } = false;

        public static readonly string[] SetIcons = new[] { "🛡", "⚔", "🏹", "🔥", "❄", "⚡", "💚", "💀" };

        PlayerController player;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            SeedInitialSets();
        }

        public void SeedInitialSets()
        {
            if (savedSets.Count > 0) return;

            savedSets.Add(new EquipmentSet("Vanguard Tank", "🛡", "bloodrock-waraxe", "iron-plate-mail", "vitality-talisman"));
            savedSets.Add(new EquipmentSet("Sovereign Striker", "⚔", "wyrmtooth-edge", "runed-scout-vest", "dragonslayer-medal"));
            savedSets.Add(new EquipmentSet("Wanderer Explorer", "🏹", "ashwood-greatsword", "scout-leather-tunic", "lucky-adventurer-stone"));
        }

        public bool SaveCurrentEquipment(string setName, string icon = "⚔")
        {
            if (string.IsNullOrWhiteSpace(setName)) return false;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return false;

            // Remove existing with same name
            savedSets.RemoveAll(s => s.name.Equals(setName, StringComparison.OrdinalIgnoreCase));

            var newSet = new EquipmentSet(
                setName.Trim(),
                icon,
                player.EquippedWeaponId,
                player.EquippedArmorId,
                player.EquippedTrinketId
            );

            savedSets.Add(newSet);
            ChatSystem.LogSystem($"<color=#ffd700>🛡 <b>[Equipment Manager]</b> Saved equipment set '{newSet.name}' ({newSet.icon}).</color>");
            AudioEffects.PlayQuestComplete();
            return true;
        }

        public bool EquipSet(string setName, out string feedback)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player)
            {
                feedback = "Player not found.";
                return false;
            }

            var set = savedSets.Find(s => s.name.Equals(setName, StringComparison.OrdinalIgnoreCase));
            if (set == null)
            {
                feedback = $"Equipment set '{setName}' does not exist.";
                return false;
            }

            bool inCombat = player.InCombat();

            // In Classic WoW: Weapons can be swapped in combat, but Armor and Trinkets CANNOT!
            if (inCombat)
            {
                if (!string.IsNullOrEmpty(set.weaponId))
                {
                    player.EquipDirect(set.weaponId);
                }
                feedback = $"Swapped weapon to '{set.name}', but Armor and Trinkets cannot be changed during combat!";
                ChatSystem.LogSystem($"<color=#ffaa00>⚔ <b>[Equipment Manager]</b> {feedback}</color>");
                AudioEffects.PlayBladeHit();
                return true;
            }

            // Out of combat: Full set swap
            if (!string.IsNullOrEmpty(set.weaponId)) player.EquipDirect(set.weaponId);
            if (!string.IsNullOrEmpty(set.armorId)) player.EquipDirect(set.armorId);
            if (!string.IsNullOrEmpty(set.trinketId)) player.EquipDirect(set.trinketId);

            feedback = $"Equipped set '{set.name}' ({set.icon})!";
            ChatSystem.LogSystem($"<color=#00ff88>🛡 <b>[Equipment Manager]</b> {feedback}</color>");
            ZoneBanner.Show("EQUIPMENT SET APPLIED", $"{set.icon} {set.name}");
            AudioEffects.PlayQuestComplete();
            return true;
        }

        public bool DeleteSet(string setName)
        {
            int removed = savedSets.RemoveAll(s => s.name.Equals(setName, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                ChatSystem.LogSystem($"<color=#aaa>Equipment set '{setName}' deleted.</color>");
                AudioEffects.PlayClick();
                return true;
            }
            return false;
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        // GUI Rendering
        public void DrawEquipmentSetGUI(float screenWidth, float screenHeight)
        {
            if (!IsWindowOpen) return;

            float winW = 540f;
            float winH = 420f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Border & Backing
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Header Banner
            DrawRect(new Rect(winX, winY, winW, 38f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 26), "🛡 EQUIPMENT SET MANAGER (GEAR SWITCHER)", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Left Pane: Set List
            float listW = 230f;
            float listH = winH - 95f;
            DrawRect(new Rect(winX + 14, winY + 48, listW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            for (int i = 0; i < savedSets.Count; i++)
            {
                var s = savedSets[i];
                float rY = winY + 52 + (i * 44);
                bool isSel = (i == selectedSetIndex);

                DrawRect(new Rect(winX + 18, rY, listW - 8, 40), isSel ? new Color(0.22f, 0.35f, 0.55f, 0.9f) : new Color(0.09f, 0.11f, 0.15f, 0.8f));

                var icStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                GUI.Label(new Rect(winX + 20, rY + 4, 32, 32), s.icon, icStyle);

                var nmStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = isSel ? FontStyle.Bold : FontStyle.Normal };
                nmStyle.normal.textColor = isSel ? Color.white : new Color(0.85f, 0.85f, 0.85f);
                GUI.Label(new Rect(winX + 56, rY + 10, listW - 65, 24), s.name, nmStyle);

                if (GUI.Button(new Rect(winX + 18, rY, listW - 8, 40), GUIContent.none, GUIStyle.none))
                {
                    selectedSetIndex = i;
                    AudioEffects.PlayClick();
                }
            }

            // Right Pane: Selected Set Details
            float detX = winX + listW + 24;
            float detW = winW - listW - 38;
            DrawRect(new Rect(detX, winY + 48, detW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            if (selectedSetIndex >= 0 && selectedSetIndex < savedSets.Count)
            {
                var sel = savedSets[selectedSetIndex];

                var titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                titleStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(detX + 14, winY + 58, detW - 28, 24), $"{sel.icon} {sel.name}", titleStyle);

                float itmY = winY + 92;
                DrawItemSlotPreview(detX + 14, itmY, "⚔ Weapon:", sel.weaponId);
                DrawItemSlotPreview(detX + 14, itmY + 48, "🛡 Armor:", sel.armorId);
                DrawItemSlotPreview(detX + 14, itmY + 96, "💎 Trinket:", sel.trinketId);

                // Action buttons inside details
                if (GUI.Button(new Rect(detX + 14, itmY + 160, 120, 32), "⚡ Equip Set"))
                {
                    EquipSet(sel.name, out _);
                }

                if (GUI.Button(new Rect(detX + 144, itmY + 160, 110, 32), "🗑 Delete Set"))
                {
                    DeleteSet(sel.name);
                    selectedSetIndex = Mathf.Clamp(selectedSetIndex, 0, Mathf.Max(0, savedSets.Count - 1));
                }
            }

            // Bottom Bar: Save Current Setup
            float barY = winY + winH - 42;
            if (GUI.Button(new Rect(winX + 14, barY, 180, 30), "💾 Save Current Gear As New Set"))
            {
                SaveCurrentEquipment($"Set #{savedSets.Count + 1}", SetIcons[savedSets.Count % SetIcons.Length]);
            }

            var noteStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, alignment = TextAnchor.MiddleRight };
            noteStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
            GUI.Label(new Rect(winX + 200, barY, winW - 216, 30), "Use /equipset [Name] in chat or macros for instant gear swapping.", noteStyle);
        }

        void DrawItemSlotPreview(float x, float y, string slotLabel, string itemId)
        {
            var item = ItemCatalog.Get(itemId);
            string itemName = item != null ? item.name : (!string.IsNullOrEmpty(itemId) ? itemId : "(None)");
            Color qualityCol = item != null ? item.color : new Color(0.6f, 0.6f, 0.6f);

            var lblStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold };
            lblStyle.normal.textColor = new Color(0.85f, 0.70f, 0.28f);
            GUI.Label(new Rect(x, y, 90, 20), slotLabel, lblStyle);

            var valStyle = new GUIStyle(GUI.skin.label) { fontSize = 12 };
            valStyle.normal.textColor = qualityCol;
            GUI.Label(new Rect(x + 95, y, 160, 20), itemName, valStyle);
        }

        static Texture2D _flatTex;
        static void DrawRect(Rect rect, Color col)
        {
            if (_flatTex == null)
            {
                _flatTex = new Texture2D(1, 1);
                _flatTex.SetPixel(0, 0, Color.white);
                _flatTex.Apply();
            }
            var prev = GUI.color;
            GUI.color = col;
            GUI.DrawTexture(rect, _flatTex);
            GUI.color = prev;
        }
    }
}
