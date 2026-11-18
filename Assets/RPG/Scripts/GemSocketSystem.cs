using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class GemSocketSystem : MonoBehaviour
    {
        public static GemSocketSystem Instance { get; private set; }

        public enum SocketColor { Red, Blue, Yellow, Meta, Prismatic }

        [Serializable]
        public class GemDefinition
        {
            public string id;
            public string name;
            public SocketColor color;
            public string rarity; // Uncommon, Rare, Epic
            public int bonusAttackPower;
            public int bonusSpellPower;
            public int bonusHealth;
            public int bonusArmor;
            public int bonusCrit;
            public string specialEffect;
            public string icon;
            public string description;

            public GemDefinition(string id, string name, SocketColor color, string rarity,
                int ap, int sp, int hp, int armor, int crit, string icon, string desc, string special = "")
            {
                this.id = id;
                this.name = name;
                this.color = color;
                this.rarity = rarity;
                this.bonusAttackPower = ap;
                this.bonusSpellPower = sp;
                this.bonusHealth = hp;
                this.bonusArmor = armor;
                this.bonusCrit = crit;
                this.icon = icon;
                this.description = desc;
                this.specialEffect = special;
            }

            public Color GetRarityColor()
            {
                switch (rarity)
                {
                    case "Epic": return new Color(0.64f, 0.21f, 0.93f);
                    case "Rare": return new Color(0.0f, 0.44f, 0.87f);
                    case "Uncommon": return new Color(0.12f, 1.0f, 0.0f);
                    default: return Color.white;
                }
            }
        }

        [Serializable]
        public class ItemSocketProfile
        {
            public string itemId;
            public string itemName;
            public List<SocketColor> sockets = new List<SocketColor>();
            public string socketBonusDesc;
            public int bonusAttackPower;
            public int bonusSpellPower;
            public int bonusHealth;
            public int bonusArmor;
            public int bonusCrit;

            public ItemSocketProfile(string id, string name, SocketColor[] colors, string bonusDesc,
                int ap = 0, int sp = 0, int hp = 0, int armor = 0, int crit = 0)
            {
                itemId = id;
                itemName = name;
                if (colors != null) sockets.AddRange(colors);
                socketBonusDesc = bonusDesc;
                bonusAttackPower = ap;
                bonusSpellPower = sp;
                bonusHealth = hp;
                bonusArmor = armor;
                bonusCrit = crit;
            }
        }

        [Serializable]
        public class ItemSocketsState
        {
            public string itemId;
            public List<string> socketedGemIds = new List<string>();

            public ItemSocketsState(string id, int socketCount)
            {
                itemId = id;
                for (int i = 0; i < socketCount; i++) socketedGemIds.Add("");
            }
        }

        public List<GemDefinition> gemCatalog = new List<GemDefinition>();
        [NonSerialized] public Dictionary<string, ItemSocketProfile> socketProfiles = new Dictionary<string, ItemSocketProfile>();
        [NonSerialized] public Dictionary<string, ItemSocketsState> itemSocketStates = new Dictionary<string, ItemSocketsState>();
        [NonSerialized] public Dictionary<string, int> playerGemInventory = new Dictionary<string, int>();

        public bool IsWindowOpen { get; set; } = false;
        public string selectedItemId = "wyrmtooth-edge";
        public int selectedSocketIndex = 0;
        [NonSerialized] public SocketColor? currentFilterColor = null;

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

            SeedGemsCatalog();
            SeedSocketProfiles();
            SeedStarterGemInventory();
        }

        public void SeedGemsCatalog()
        {
            gemCatalog.Clear();

            // Red Gems (+AP, +Spell Power)
            gemCatalog.Add(new GemDefinition("gem_bold_ruby", "Bold Blood Garnet", SocketColor.Red, "Rare",
                16, 0, 0, 0, 0, "🔴", "+16 Attack Power"));
            gemCatalog.Add(new GemDefinition("gem_crimson_spinel", "Bright Crimson Spinel", SocketColor.Red, "Epic",
                24, 0, 0, 0, 0, "🔴", "+24 Attack Power"));
            gemCatalog.Add(new GemDefinition("gem_runed_ruby", "Runed Scarlet Ruby", SocketColor.Red, "Rare",
                0, 14, 0, 0, 0, "🔴", "+14 Spell Power"));

            // Blue Gems (+Health, +Armor)
            gemCatalog.Add(new GemDefinition("gem_solid_zircon", "Solid Majestic Zircon", SocketColor.Blue, "Rare",
                0, 0, 30, 0, 0, "🔵", "+30 Maximum Health"));
            gemCatalog.Add(new GemDefinition("gem_lustrous_zircon", "Lustrous Star Sapphire", SocketColor.Blue, "Epic",
                0, 0, 45, 0, 0, "🔵", "+45 Maximum Health"));
            gemCatalog.Add(new GemDefinition("gem_sparkling_zircon", "Sparkling Ocean Sapphire", SocketColor.Blue, "Rare",
                0, 0, 0, 15, 0, "🔵", "+15 Armor"));

            // Yellow Gems (+Crit)
            gemCatalog.Add(new GemDefinition("gem_quick_amber", "Quick King's Amber", SocketColor.Yellow, "Rare",
                0, 0, 0, 0, 12, "🟡", "+12 Critical Strike"));
            gemCatalog.Add(new GemDefinition("gem_smooth_amber", "Smooth Autumn's Glow", SocketColor.Yellow, "Epic",
                0, 0, 0, 0, 18, "🟡", "+18 Critical Strike"));
            gemCatalog.Add(new GemDefinition("gem_gleaming_amber", "Gleaming Sun Crystal", SocketColor.Yellow, "Rare",
                0, 0, 0, 8, 8, "🟡", "+8 Armor & +8 Critical Strike"));

            // Prismatic Gems (Fits any non-meta socket, grants multi-stats)
            gemCatalog.Add(new GemDefinition("gem_prismatic_tear", "Enchanted Prismatic Tear", SocketColor.Prismatic, "Epic",
                8, 6, 15, 5, 5, "💠", "+15 Health, +8 AP, +6 SP, +5 Armor, +5 Crit (Matches Any Socket)"));

            // Meta Gems (Fits only Meta sockets)
            gemCatalog.Add(new GemDefinition("gem_chaotic_skyflare", "Chaotic Skyflare Diamond", SocketColor.Meta, "Epic",
                0, 0, 25, 0, 14, "💎", "+25 Health, +14 Crit", "+3% Critical Strike Damage"));
            gemCatalog.Add(new GemDefinition("gem_relentless_earthsiege", "Relentless Earthsiege Diamond", SocketColor.Meta, "Epic",
                18, 0, 0, 12, 0, "💎", "+18 Attack Power, +12 Armor", "+2% Reduced Physical Damage Taken"));
        }

        public void SeedSocketProfiles()
        {
            socketProfiles.Clear();

            RegisterSocketProfile(new ItemSocketProfile("wyrmtooth-edge", "Wyrmtooth Cleaver",
                new[] { SocketColor.Red, SocketColor.Red }, "+12 Attack Power", ap: 12));

            RegisterSocketProfile(new ItemSocketProfile("scarlet-aegis", "Aegis of the Scarlet Dawn",
                new[] { SocketColor.Blue, SocketColor.Yellow }, "+25 Maximum Health", hp: 25));

            RegisterSocketProfile(new ItemSocketProfile("ember-drake-scale", "Heart of the Ember Drake",
                new[] { SocketColor.Prismatic }, "+15 Maximum Health", hp: 15));

            RegisterSocketProfile(new ItemSocketProfile("scarlet-blade", "Inquisitor's Sunblade",
                new[] { SocketColor.Red }, "+8 Attack Power", ap: 8));

            RegisterSocketProfile(new ItemSocketProfile("truesilver-blade", "Truesilver Executioner",
                new[] { SocketColor.Red, SocketColor.Yellow }, "+10 Attack Power", ap: 10));

            RegisterSocketProfile(new ItemSocketProfile("watchtower-cuirass", "Watchtower Cuirass",
                new[] { SocketColor.Blue, SocketColor.Blue }, "+15 Armor", armor: 15));

            RegisterSocketProfile(new ItemSocketProfile("cathedral-relic", "Censer of the Pure Light",
                new[] { SocketColor.Meta }, "+20 Maximum Health", hp: 20));

            RegisterSocketProfile(new ItemSocketProfile("duskwood-shadow-dagger", "Shadowfang Stiletto",
                new[] { SocketColor.Red, SocketColor.Blue }, "+8 Critical Strike", crit: 8));
        }

        public void RegisterSocketProfile(ItemSocketProfile profile)
        {
            socketProfiles[profile.itemId] = profile;
            if (!itemSocketStates.ContainsKey(profile.itemId))
            {
                itemSocketStates[profile.itemId] = new ItemSocketsState(profile.itemId, profile.sockets.Count);
            }
        }

        public void SeedStarterGemInventory()
        {
            playerGemInventory.Clear();
            AddGemsToInventory("gem_bold_ruby", 2);
            AddGemsToInventory("gem_solid_zircon", 2);
            AddGemsToInventory("gem_quick_amber", 2);
            AddGemsToInventory("gem_prismatic_tear", 1);
            AddGemsToInventory("gem_chaotic_skyflare", 1);
        }

        public void AddGemsToInventory(string gemId, int count)
        {
            if (playerGemInventory.ContainsKey(gemId)) playerGemInventory[gemId] += count;
            else playerGemInventory[gemId] = count;
        }

        public bool RemoveGemFromInventory(string gemId)
        {
            if (playerGemInventory.TryGetValue(gemId, out int count) && count > 0)
            {
                playerGemInventory[gemId] = count - 1;
                if (playerGemInventory[gemId] <= 0) playerGemInventory.Remove(gemId);
                return true;
            }
            return false;
        }

        public GemDefinition GetGem(string gemId)
        {
            if (string.IsNullOrEmpty(gemId)) return null;
            return gemCatalog.Find(g => g.id == gemId);
        }

        public ItemSocketProfile GetProfile(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            socketProfiles.TryGetValue(itemId, out var p);
            return p;
        }

        public ItemSocketsState GetState(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            if (!itemSocketStates.TryGetValue(itemId, out var s))
            {
                var profile = GetProfile(itemId);
                if (profile != null)
                {
                    s = new ItemSocketsState(itemId, profile.sockets.Count);
                    itemSocketStates[itemId] = s;
                }
            }
            return s;
        }

        // Validity check: Can gem physically be placed into this socket?
        public bool CanSocketGemIntoSlot(SocketColor socketColor, GemDefinition gem, out string reason)
        {
            reason = "";
            if (gem == null)
            {
                reason = "Invalid gem.";
                return false;
            }

            if (socketColor == SocketColor.Meta)
            {
                if (gem.color != SocketColor.Meta)
                {
                    reason = "Meta sockets can only accept Meta gems!";
                    return false;
                }
                return true;
            }

            // Non-meta socket
            if (gem.color == SocketColor.Meta)
            {
                reason = "Meta gems can only be placed into Meta sockets!";
                return false;
            }

            return true;
        }

        // Color Match check: Does gem match the socket color for activating Socket Bonus?
        public bool MatchesSocketColor(SocketColor socketColor, GemDefinition gem)
        {
            if (gem == null) return false;
            if (socketColor == SocketColor.Prismatic || gem.color == SocketColor.Prismatic) return true;
            return socketColor == gem.color;
        }

        public bool IsSocketBonusActive(string itemId)
        {
            var profile = GetProfile(itemId);
            var state = GetState(itemId);
            if (profile == null || state == null || profile.sockets.Count == 0) return false;

            for (int i = 0; i < profile.sockets.Count; i++)
            {
                if (i >= state.socketedGemIds.Count) return false;
                string gemId = state.socketedGemIds[i];
                if (string.IsNullOrEmpty(gemId)) return false; // unfilled socket

                var gem = GetGem(gemId);
                if (gem == null || !MatchesSocketColor(profile.sockets[i], gem)) return false;
            }
            return true;
        }

        public bool SocketGem(string itemId, int socketIndex, string gemId, out string message, bool ignoreInventory = false)
        {
            message = "";
            var profile = GetProfile(itemId);
            if (profile == null)
            {
                message = "This item has no gem sockets.";
                return false;
            }

            if (socketIndex < 0 || socketIndex >= profile.sockets.Count)
            {
                message = "Invalid socket slot.";
                return false;
            }

            var gem = GetGem(gemId);
            if (gem == null)
            {
                message = "Selected gem does not exist in catalog.";
                return false;
            }

            if (!ignoreInventory && (!playerGemInventory.TryGetValue(gemId, out int c) || c <= 0))
            {
                message = $"You do not have any [{gem.name}] in your inventory.";
                return false;
            }

            if (!CanSocketGemIntoSlot(profile.sockets[socketIndex], gem, out string rejectReason))
            {
                message = rejectReason;
                AudioEffects.PlayClick();
                return false;
            }

            var state = GetState(itemId);
            // If already filled, return existing gem to player
            string oldGemId = state.socketedGemIds[socketIndex];
            if (!string.IsNullOrEmpty(oldGemId) && !ignoreInventory)
            {
                AddGemsToInventory(oldGemId, 1);
            }

            // Socket new gem
            state.socketedGemIds[socketIndex] = gemId;
            if (!ignoreInventory) RemoveGemFromInventory(gemId);

            if (player != null) player.RefreshEquipmentStats();

            AudioEffects.PlayBladeHit();

            bool bonusActive = IsSocketBonusActive(itemId);
            message = $"Socketed [{gem.name}] into [{profile.itemName}] (Slot {socketIndex + 1})!";
            if (bonusActive) message += $" Socket Bonus Activated: {profile.socketBonusDesc}!";

            ChatSystem.LogSystem($"<color=#ffd700>💎 <b>[Jewelcrafting]</b> {message}</color>");

            return true;
        }

        public bool UnsocketGem(string itemId, int socketIndex, out string message, bool returnToInventory = true)
        {
            message = "";
            var profile = GetProfile(itemId);
            var state = GetState(itemId);
            if (profile == null || state == null || socketIndex < 0 || socketIndex >= state.socketedGemIds.Count)
            {
                message = "Invalid socket reference.";
                return false;
            }

            string gemId = state.socketedGemIds[socketIndex];
            if (string.IsNullOrEmpty(gemId))
            {
                message = "Socket slot is already empty.";
                return false;
            }

            var gem = GetGem(gemId);
            state.socketedGemIds[socketIndex] = "";
            if (returnToInventory && gem != null) AddGemsToInventory(gemId, 1);
            if (player != null) player.RefreshEquipmentStats();

            AudioEffects.PlayClick();
            message = $"Extracted [{gem?.name ?? "Gem"}] from [{profile.itemName}] (Slot {socketIndex + 1}).";
            ChatSystem.LogSystem($"<color=#aaaaaa>💎 [Jewelcrafting] {message}</color>");
            return true;
        }

        public void GetItemGemStats(string itemId, out int ap, out int sp, out int hp, out int armor, out int crit)
        {
            ap = sp = hp = armor = crit = 0;
            var profile = GetProfile(itemId);
            var state = GetState(itemId);
            if (profile == null || state == null) return;

            foreach (var gId in state.socketedGemIds)
            {
                var gem = GetGem(gId);
                if (gem != null)
                {
                    ap += gem.bonusAttackPower;
                    sp += gem.bonusSpellPower;
                    hp += gem.bonusHealth;
                    armor += gem.bonusArmor;
                    crit += gem.bonusCrit;
                }
            }

            if (IsSocketBonusActive(itemId))
            {
                ap += profile.bonusAttackPower;
                sp += profile.bonusSpellPower;
                hp += profile.bonusHealth;
                armor += profile.bonusArmor;
                crit += profile.bonusCrit;
            }
        }

        public void GetTotalEquippedGemStats(out int ap, out int sp, out int hp, out int armor, out int crit)
        {
            ap = sp = hp = armor = crit = 0;
            if (player == null) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

            var checkedItems = new HashSet<string>();
            if (player != null)
            {
                if (!string.IsNullOrEmpty(player.EquippedWeaponId)) checkedItems.Add(player.EquippedWeaponId);
                if (!string.IsNullOrEmpty(player.EquippedArmorId)) checkedItems.Add(player.EquippedArmorId);
                if (!string.IsNullOrEmpty(player.EquippedTrinketId)) checkedItems.Add(player.EquippedTrinketId);
            }

            foreach (var id in checkedItems)
            {
                GetItemGemStats(id, out int itemAp, out int itemSp, out int itemHp, out int itemArm, out int itemCrit);
                ap += itemAp;
                sp += itemSp;
                hp += itemHp;
                armor += itemArm;
                crit += itemCrit;
            }
        }

        public bool ProspectOre(string oreType, out string message)
        {
            message = "";
            // Prospect 5 ore chunks to produce uncut/cut gems
            int roll = UnityEngine.Random.Range(0, gemCatalog.Count);
            var gem = gemCatalog[roll];
            AddGemsToInventory(gem.id, 1);
            AudioEffects.PlaySpellCast();
            message = $"[Jewelcrafting Prospecting] You sifted through raw minerals and discovered 1x [{gem.name}]!";
            ChatSystem.LogSystem($"<color=#aaaaaa>💎 [Jewelcrafting] {message}</color>");
            return true;
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            if (IsWindowOpen) AudioEffects.PlaySpellCast();
            else AudioEffects.PlayClick();
        }

        public void DrawGemSocketGUI(float screenWidth, float screenHeight)
        {
            if (!IsWindowOpen) return;

            GUI.depth = -10;
            float winWidth = 640;
            float winHeight = 500;
            float winX = (screenWidth - winWidth) * 0.5f;
            float winY = (screenHeight - winHeight) * 0.5f;

            Rect winRect = new Rect(winX, winY, winWidth, winHeight);
            GUI.Box(winRect, GUIContent.none);

            // Title Bar
            GUI.Box(new Rect(winX, winY, winWidth, 36), GUIContent.none);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.82f, 0.0f);
            GUI.Label(new Rect(winX + 40, winY + 6, winWidth - 80, 24), "💎 JEWELCRAFTING & PRISMATIC GEM SOCKETING", titleStyle);

            if (GUI.Button(new Rect(winX + winWidth - 32, winY + 6, 26, 24), "✕"))
            {
                ToggleWindow();
                return;
            }

            // Left Pane: Socketable Gear Items
            float leftW = 190;
            Rect leftRect = new Rect(winX + 10, winY + 44, leftW, winHeight - 54);
            GUI.Box(leftRect, GUIContent.none);

            var subHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 11 };
            subHeaderStyle.normal.textColor = new Color(0.9f, 0.85f, 0.7f);
            GUI.Label(new Rect(leftRect.x + 8, leftRect.y + 6, leftW - 16, 20), "SOCKETABLE GEAR", subHeaderStyle);

            float itemY = leftRect.y + 28;
            foreach (var kvp in socketProfiles)
            {
                var profile = kvp.Value;
                bool isSelected = (selectedItemId == profile.itemId);
                bool bonusOn = IsSocketBonusActive(profile.itemId);

                string bonusIndicator = bonusOn ? "<color=#00ff00>✔</color>" : "<color=#888888>○</color>";
                string socketDots = "";
                foreach (var sc in profile.sockets)
                {
                    switch (sc)
                    {
                        case SocketColor.Red: socketDots += "<color=#ff4444>●</color>"; break;
                        case SocketColor.Blue: socketDots += "<color=#4488ff>●</color>"; break;
                        case SocketColor.Yellow: socketDots += "<color=#ffff44>●</color>"; break;
                        case SocketColor.Meta: socketDots += "<color=#00ffff>◆</color>"; break;
                        case SocketColor.Prismatic: socketDots += "<color=#ffffff>◇</color>"; break;
                    }
                }

                string btnText = $"{profile.itemName}\n{socketDots} {bonusIndicator}";
                if (GUI.Button(new Rect(leftRect.x + 6, itemY, leftW - 12, 42), btnText))
                {
                    selectedItemId = profile.itemId;
                    selectedSocketIndex = 0;
                    AudioEffects.PlayClick();
                }
                itemY += 46;
            }

            // Center Pane: Selected Item Socket Chamber
            float centerW = 210;
            Rect centerRect = new Rect(leftRect.xMax + 10, winY + 44, centerW, winHeight - 54);
            GUI.Box(centerRect, GUIContent.none);

            var curProfile = GetProfile(selectedItemId);
            var curState = GetState(selectedItemId);

            if (curProfile != null && curState != null)
            {
                GUI.Label(new Rect(centerRect.x + 8, centerRect.y + 6, centerW - 16, 20), "SOCKET CHAMBER", subHeaderStyle);

                var itemTitleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 12 };
                itemTitleStyle.normal.textColor = new Color(1f, 0.75f, 0.1f);
                GUI.Label(new Rect(centerRect.x + 8, centerRect.y + 26, centerW - 16, 22), curProfile.itemName, itemTitleStyle);

                // Render each socket
                float sockY = centerRect.y + 54;
                for (int i = 0; i < curProfile.sockets.Count; i++)
                {
                    var sColor = curProfile.sockets[i];
                    string gemId = i < curState.socketedGemIds.Count ? curState.socketedGemIds[i] : "";
                    var slottedGem = GetGem(gemId);

                    bool isSlotSelected = (selectedSocketIndex == i);
                    Rect sockRect = new Rect(centerRect.x + 8, sockY, centerW - 16, 68);
                    GUI.Box(sockRect, GUIContent.none);

                    string sockTitle = $"Slot {i + 1}: [{sColor} Socket]";
                    var sockHeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 10 };
                    switch (sColor)
                    {
                        case SocketColor.Red: sockHeaderStyle.normal.textColor = new Color(1f, 0.4f, 0.4f); break;
                        case SocketColor.Blue: sockHeaderStyle.normal.textColor = new Color(0.4f, 0.7f, 1f); break;
                        case SocketColor.Yellow: sockHeaderStyle.normal.textColor = new Color(1f, 1f, 0.4f); break;
                        case SocketColor.Meta: sockHeaderStyle.normal.textColor = new Color(0.3f, 1f, 1f); break;
                        case SocketColor.Prismatic: sockHeaderStyle.normal.textColor = Color.white; break;
                    }
                    GUI.Label(new Rect(sockRect.x + 4, sockRect.y + 2, sockRect.width - 8, 18), sockTitle, sockHeaderStyle);

                    if (slottedGem != null)
                    {
                        var gemStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                        gemStyle.normal.textColor = slottedGem.GetRarityColor();
                        GUI.Label(new Rect(sockRect.x + 4, sockRect.y + 20, sockRect.width - 8, 18),
                            $"{slottedGem.icon} {slottedGem.name}", gemStyle);

                        var statStyle = new GUIStyle(GUI.skin.label) { fontSize = 9 };
                        statStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                        GUI.Label(new Rect(sockRect.x + 4, sockRect.y + 36, sockRect.width - 8, 16),
                            slottedGem.description, statStyle);

                        if (GUI.Button(new Rect(sockRect.xMax - 54, sockRect.y + 48, 50, 16), "Remove"))
                        {
                            UnsocketGem(selectedItemId, i, out _);
                        }
                    }
                    else
                    {
                        var emptyStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic };
                        emptyStyle.normal.textColor = new Color(0.6f, 0.6f, 0.6f);
                        GUI.Label(new Rect(sockRect.x + 4, sockRect.y + 24, sockRect.width - 8, 20),
                            "— Empty Socket —", emptyStyle);

                        if (isSlotSelected)
                        {
                            GUI.Label(new Rect(sockRect.x + 4, sockRect.y + 44, sockRect.width - 8, 18),
                                "<color=#ffff00>▶ Selected for socketing</color>");
                        }
                        else
                        {
                            if (GUI.Button(new Rect(sockRect.x + 4, sockRect.y + 44, sockRect.width - 8, 18), "Select Slot"))
                            {
                                selectedSocketIndex = i;
                                AudioEffects.PlayClick();
                            }
                        }
                    }

                    sockY += 72;
                }

                // Socket Bonus Box
                bool bonusActive = IsSocketBonusActive(selectedItemId);
                Rect bonusRect = new Rect(centerRect.x + 8, centerRect.yMax - 95, centerW - 16, 42);
                GUI.Box(bonusRect, GUIContent.none);

                var bonusTitleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 10 };
                bonusTitleStyle.normal.textColor = bonusActive ? new Color(0.3f, 1.0f, 0.3f) : new Color(0.6f, 0.6f, 0.6f);
                string bonusStatus = bonusActive ? "SOCKET BONUS ACTIVE!" : "SOCKET BONUS (Locked)";
                GUI.Label(new Rect(bonusRect.x + 4, bonusRect.y + 2, bonusRect.width - 8, 16), bonusStatus, bonusTitleStyle);

                var bonusDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 10 };
                bonusDescStyle.normal.textColor = bonusActive ? new Color(1.0f, 0.84f, 0.0f) : new Color(0.6f, 0.6f, 0.6f);
                GUI.Label(new Rect(bonusRect.x + 4, bonusRect.y + 18, bonusRect.width - 8, 20), curProfile.socketBonusDesc, bonusDescStyle);

                // Clear All Button
                if (GUI.Button(new Rect(centerRect.x + 8, centerRect.yMax - 45, centerW - 16, 26), "Extract All Gems"))
                {
                    for (int s = 0; s < curProfile.sockets.Count; s++)
                    {
                        UnsocketGem(selectedItemId, s, out _);
                    }
                }
            }

            // Right Pane: Player's Gem Inventory
            float rightW = winWidth - (leftW + centerW + 40);
            Rect rightRect = new Rect(centerRect.xMax + 10, winY + 44, rightW, winHeight - 54);
            GUI.Box(rightRect, GUIContent.none);

            GUI.Label(new Rect(rightRect.x + 8, rightRect.y + 6, rightW - 16, 20), "GEM SATCHEL", subHeaderStyle);

            // Filter Buttons
            float fX = rightRect.x + 6;
            if (GUI.Button(new Rect(fX, rightRect.y + 26, 28, 20), "All")) currentFilterColor = null;
            if (GUI.Button(new Rect(fX + 30, rightRect.y + 26, 26, 20), "🔴")) currentFilterColor = SocketColor.Red;
            if (GUI.Button(new Rect(fX + 58, rightRect.y + 26, 26, 20), "🔵")) currentFilterColor = SocketColor.Blue;
            if (GUI.Button(new Rect(fX + 86, rightRect.y + 26, 26, 20), "🟡")) currentFilterColor = SocketColor.Yellow;
            if (GUI.Button(new Rect(fX + 114, rightRect.y + 26, 26, 20), "💎")) currentFilterColor = SocketColor.Meta;
            if (GUI.Button(new Rect(fX + 142, rightRect.y + 26, 26, 20), "💠")) currentFilterColor = SocketColor.Prismatic;

            // List gems in player inventory
            float gemListY = rightRect.y + 52;
            int drawn = 0;
            foreach (var kvp in playerGemInventory)
            {
                string gemId = kvp.Key;
                int count = kvp.Value;
                if (count <= 0) continue;

                var g = GetGem(gemId);
                if (g == null) continue;
                if (currentFilterColor.HasValue && g.color != currentFilterColor.Value) continue;

                Rect gBox = new Rect(rightRect.x + 6, gemListY, rightW - 12, 48);
                GUI.Box(gBox, GUIContent.none);

                var gNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
                gNameStyle.normal.textColor = g.GetRarityColor();
                GUI.Label(new Rect(gBox.x + 4, gBox.y + 2, gBox.width - 55, 16), $"{g.icon} {g.name} x{count}", gNameStyle);

                var gDescStyle = new GUIStyle(GUI.skin.label) { fontSize = 9 };
                gDescStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f);
                GUI.Label(new Rect(gBox.x + 4, gBox.y + 18, gBox.width - 55, 26), g.description, gDescStyle);

                if (GUI.Button(new Rect(gBox.xMax - 50, gBox.y + 12, 46, 26), "Insert"))
                {
                    SocketGem(selectedItemId, selectedSocketIndex, g.id, out _);
                }

                gemListY += 52;
                drawn++;
                if (drawn >= 6) break;
            }

            if (drawn == 0)
            {
                var noGemsStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, fontSize = 10 };
                noGemsStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
                GUI.Label(new Rect(rightRect.x + 10, gemListY + 10, rightW - 20, 40),
                    "No cut gems of this color in your satchel.", noGemsStyle);
            }

            // Prospect Button at Bottom Right
            if (GUI.Button(new Rect(rightRect.x + 6, rightRect.yMax - 36, rightW - 12, 28), "⚒ Prospect 5x Raw Ore"))
            {
                ProspectOre("Copper", out _);
            }
        }
    }
}
