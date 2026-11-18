using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class TransmogSystem : MonoBehaviour
    {
        public static TransmogSystem Instance { get; private set; }

        public enum TransmogSlot
        {
            Head,
            Shoulders,
            Chest,
            MainHand,
            OffHand
        }

        [Serializable]
        public class AppearanceModel
        {
            public string id;
            public string name;
            public TransmogSlot slot;
            public string rarity;
            public string icon;
            public Color modelColor;
            public string sourceZone;
            public bool isUnlocked;

            public AppearanceModel(string id, string name, TransmogSlot slot, string rarity, string icon, Color modelColor, string sourceZone, bool isUnlocked = true)
            {
                this.id = id;
                this.name = name;
                this.slot = slot;
                this.rarity = rarity;
                this.icon = icon;
                this.modelColor = modelColor;
                this.sourceZone = sourceZone;
                this.isUnlocked = isUnlocked;
            }
        }

        // Active transmog overrides for each slot
        [NonSerialized] public Dictionary<TransmogSlot, string> activeAppearances = new Dictionary<TransmogSlot, string>();

        // Catalog of collectible appearances
        public List<AppearanceModel> wardrobeCatalog = new List<AppearanceModel>();

        public TransmogSlot selectedSlot = TransmogSlot.MainHand;
        public int selectedAppearanceIndex = 0;
        public bool IsWindowOpen { get; set; } = false;

        // Cost per transmog modification
        public const int TransmogCostGold = 5;

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
            SeedWardrobeCatalog();
        }

        public void SeedWardrobeCatalog()
        {
            if (wardrobeCatalog.Count > 0) return;

            // Weapons
            wardrobeCatalog.Add(new AppearanceModel("app_brute_axe", "Brute Great-Axe", TransmogSlot.MainHand, "Common", "🪓", new Color(0.7f, 0.7f, 0.7f), "Emberwatch Outpost"));
            wardrobeCatalog.Add(new AppearanceModel("app_captain_greataxe", "Captain's Greataxe", TransmogSlot.MainHand, "Rare", "⚔", new Color(0.2f, 0.6f, 1f), "Ashwood Camp"));
            wardrobeCatalog.Add(new AppearanceModel("app_sunblade", "Inquisitor's Sunblade", TransmogSlot.MainHand, "Epic", "🗡", new Color(0.85f, 0.7f, 0.2f), "Scarlet Crypts"));
            wardrobeCatalog.Add(new AppearanceModel("app_shadowfang", "Shadowfang Stiletto", TransmogSlot.MainHand, "Epic", "🗡", new Color(0.6f, 0.2f, 0.9f), "Duskwood Cathedral"));
            wardrobeCatalog.Add(new AppearanceModel("app_wyrmtooth", "Wyrmtooth Cleaver", TransmogSlot.MainHand, "Legendary", "🪓", new Color(1f, 0.5f, 0.1f), "Wyrmcrest Summit"));

            // Helmets / Head
            wardrobeCatalog.Add(new AppearanceModel("app_iron_helm", "Reinforced Iron Greathelm", TransmogSlot.Head, "Uncommon", "🪖", new Color(0.6f, 0.65f, 0.7f), "Blacksmithing"));
            wardrobeCatalog.Add(new AppearanceModel("app_scarlet_coif", "Scarlet Crusader's Coif", TransmogSlot.Head, "Rare", "🔴", new Color(0.9f, 0.2f, 0.2f), "Scarlet Crypts"));
            wardrobeCatalog.Add(new AppearanceModel("app_crown_molten", "Crown of Molten Ruin", TransmogSlot.Head, "Epic", "👑", new Color(1f, 0.4f, 0.1f), "Gorgonax World Boss"));

            // Shoulders
            wardrobeCatalog.Add(new AppearanceModel("app_rugged_spaulders", "Rugged Leather Spaulders", TransmogSlot.Shoulders, "Common", "🛡", new Color(0.5f, 0.35f, 0.2f), "Ember Vale"));
            wardrobeCatalog.Add(new AppearanceModel("app_silver_mantle", "Mantle of the Silver Dawn", TransmogSlot.Shoulders, "Epic", "✨", new Color(0.9f, 0.9f, 1f), "Highspire Keep"));
            wardrobeCatalog.Add(new AppearanceModel("app_bloodrock_pauldrons", "Bloodrock Spiked Pauldrons", TransmogSlot.Shoulders, "Rare", "🩸", new Color(0.7f, 0.1f, 0.1f), "Camp Bloodrock"));

            // Chest Armor
            wardrobeCatalog.Add(new AppearanceModel("app_scout_vest", "Scout's Leather Vest", TransmogSlot.Chest, "Common", "🥋", new Color(0.4f, 0.3f, 0.15f), "Ember Vale"));
            wardrobeCatalog.Add(new AppearanceModel("app_boneguard_plate", "Boneguard Plate Cuirass", TransmogSlot.Chest, "Uncommon", "🦴", new Color(0.85f, 0.85f, 0.8f), "Ashwood Ridge"));
            wardrobeCatalog.Add(new AppearanceModel("app_aegis_hauberk", "Hauberk of the Scarlet Dawn", TransmogSlot.Chest, "Epic", "⚜", new Color(0.8f, 0.1f, 0.1f), "Scarlet Crypts"));

            // Off-Hand / Shields
            wardrobeCatalog.Add(new AppearanceModel("app_wooden_buckler", "Reinforced Oak Buckler", TransmogSlot.OffHand, "Common", "🛡", new Color(0.5f, 0.4f, 0.25f), "Ember Vale"));
            wardrobeCatalog.Add(new AppearanceModel("app_scarlet_aegis", "Aegis of the Scarlet Dawn", TransmogSlot.OffHand, "Epic", "🛡", new Color(0.85f, 0.75f, 0.2f), "Scarlet Crypts"));
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        public List<AppearanceModel> GetAppearancesForSlot(TransmogSlot slot)
        {
            return wardrobeCatalog.FindAll(a => a.slot == slot);
        }

        public string GetActiveAppearance(TransmogSlot slot)
        {
            if (activeAppearances.TryGetValue(slot, out string modelId))
                return modelId;
            return "Default";
        }

        public bool ApplyTransmog(TransmogSlot slot, string appearanceId, out string reason)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();

            var app = wardrobeCatalog.Find(a => a.id == appearanceId && a.slot == slot);
            if (app == null)
            {
                reason = "Appearance model not found in wardrobe collection!";
                return false;
            }

            if (!app.isUnlocked)
            {
                reason = "You have not collected this appearance yet!";
                return false;
            }

            if (player && player.Gold < TransmogCostGold)
            {
                reason = $"Not enough gold! Transmogrification costs {TransmogCostGold} gold (You have {player.Gold}g).";
                return false;
            }

            if (player)
            {
                player.SpendGoldDirect(TransmogCostGold);
            }

            activeAppearances[slot] = appearanceId;
            reason = $"Transmogrified {slot} to {app.name}!";

            ZoneBanner.Show("APPEARANCE UPDATED", $"{app.name} ({slot})");
            ChatSystem.LogSystem($"<color=#d580ff>[Transmogrification] Your {slot} appearance has been bound to {app.name}!</color>");
            AudioEffects.PlayQuestComplete();

            return true;
        }

        public void RevertTransmog(TransmogSlot slot)
        {
            if (activeAppearances.ContainsKey(slot))
            {
                activeAppearances.Remove(slot);
                ZoneBanner.Show("APPEARANCE REVERTED", $"{slot} restored to default item visual.");
                ChatSystem.LogSystem($"<color=#d580ff>[Transmogrification] Reverted {slot} to original look.</color>");
                AudioEffects.PlayClick();
            }
        }

        // GUI Rendering
        public void DrawTransmogGUI(float screenWidth, float screenHeight)
        {
            if (!IsWindowOpen) return;

            float winW = 600f;
            float winH = 470f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.50f, 0.95f, 0.95f)); // Ethereal purple trim
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.97f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 36f), new Color(0.14f, 0.12f, 0.20f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(0.95f, 0.70f, 1f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 24), "🔮 ETHEREAL TRANSMOGRIFICATION & WARDROBE", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Top Slot Navigation (Head, Shoulders, Chest, MainHand, OffHand)
            float slotY = winY + 44f;
            string[] slotNames = new string[] { "Head 🪖", "Shoulders ✨", "Chest 🥋", "Main Hand ⚔", "Off Hand 🛡" };
            selectedSlot = (TransmogSlot)GUI.Toolbar(new Rect(winX + 16, slotY, winW - 32, 28), (int)selectedSlot, slotNames);

            // Left Pane: Available appearances for selected slot
            var slotApps = GetAppearancesForSlot(selectedSlot);
            float listW = 240f;
            float listH = winH - 130f;
            DrawRect(new Rect(winX + 16, winY + 80, listW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            var itemBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 4, 4) };
            for (int i = 0; i < slotApps.Count; i++)
            {
                var app = slotApps[i];
                bool isSel = (i == selectedAppearanceIndex);
                if (isSel) GUI.color = new Color(0.95f, 0.70f, 1f);
                string activeTag = (activeAppearances.TryGetValue(selectedSlot, out string curId) && curId == app.id) ? " ★" : "";
                if (GUI.Button(new Rect(winX + 20, winY + 84 + i * 38, listW - 8, 34), $"{app.icon}  {app.name}{activeTag}", itemBtn))
                {
                    selectedAppearanceIndex = i;
                    AudioEffects.PlayClick();
                }
                GUI.color = Color.white;
            }

            // Right Pane: Preview & Apply
            float prevX = winX + listW + 28;
            float prevW = winW - listW - 44;
            if (selectedAppearanceIndex >= 0 && selectedAppearanceIndex < slotApps.Count)
            {
                var sel = slotApps[selectedAppearanceIndex];

                var pTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                pTitle.normal.textColor = sel.rarity == "Legendary" ? new Color(1f, 0.5f, 0.1f) :
                                          sel.rarity == "Epic" ? new Color(0.75f, 0.35f, 1f) :
                                          sel.rarity == "Rare" ? new Color(0.2f, 0.6f, 1f) : Color.white;
                GUI.Label(new Rect(prevX, winY + 84, prevW, 24), $"{sel.icon} {sel.name}", pTitle);

                GUI.Label(new Rect(prevX, winY + 110, prevW, 20), $"Slot: <b>{sel.slot}</b>  ·  Rarity: <b>{sel.rarity}</b>");
                GUI.Label(new Rect(prevX, winY + 130, prevW, 20), $"Collected from: <i>{sel.sourceZone}</i>");

                // Preview Model Swatch Box
                DrawRect(new Rect(prevX, winY + 160, prevW, 110), new Color(0.10f, 0.12f, 0.16f, 1f));
                DrawRect(new Rect(prevX + 10, winY + 170, 90, 90), sel.modelColor);

                var swatchStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
                swatchStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
                string activeStatus = (activeAppearances.TryGetValue(selectedSlot, out string cur) && cur == sel.id) ?
                    "<color=#40ff40><b>Currently Transmogrified</b></color>" : "Original Item Visual Active";
                GUI.Label(new Rect(prevX + 110, winY + 175, prevW - 120, 80), $"Wardrobe Status:\n{activeStatus}\n\nCost to Apply: <b>{TransmogCostGold} Gold</b>", swatchStyle);

                // Action Buttons
                float btnY = winY + winH - 46f;
                GUI.color = new Color(0.85f, 0.50f, 0.95f);
                if (GUI.Button(new Rect(prevX, btnY, 150, 36), "Apply Transmog"))
                {
                    ApplyTransmog(selectedSlot, sel.id, out _);
                }
                GUI.color = Color.white;

                if (activeAppearances.ContainsKey(selectedSlot))
                {
                    if (GUI.Button(new Rect(prevX + 160, btnY, 100, 36), "Revert"))
                    {
                        RevertTransmog(selectedSlot);
                    }
                }
            }
        }

        void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
