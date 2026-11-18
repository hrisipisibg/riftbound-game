using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class GuildSystem : MonoBehaviour
    {
        public static GuildSystem Instance
        {
            get
            {
                if (_instance == null) _instance = UnityEngine.Object.FindAnyObjectByType<GuildSystem>();
                return _instance;
            }
            private set => _instance = value;
        }
        static GuildSystem _instance;

        public enum GuildRank
        {
            GuildMaster = 0,
            Officer = 1,
            Veteran = 2,
            Member = 3,
            Initiate = 4
        }

        [System.Serializable]
        public class GuildMember
        {
            public string name;
            public string characterClass; // "Warrior", "Paladin", "Mage", "Priest", "Rogue"
            public int level;
            public GuildRank rank;
            public string zone;
            public bool isOnline = true;
            public string publicNote = "";
            public Color classColor;
        }

        [System.Serializable]
        public class GuildBankItem
        {
            public string id;
            public string name;
            public string rarity; // "Common", "Rare", "Epic", "Legendary"
            public int count;
            public string icon;
            public string description;
        }

        [System.Serializable]
        public class GuildLogEntry
        {
            public string timestamp;
            public string text;
        }

        public string GuildName { get; set; } = "Vanguard of the Silver Dawn";
        public string GuildMOTD { get; set; } = "Welcome to the Vanguard! Cathedral Heroic run scheduled at 20:00 server time. Free potions in Guild Bank Tab 1.";
        public int GuildLevel { get; set; } = 12;
        public int GuildXP { get; set; } = 48200;
        public int GuildXPNext { get; set; } = 65000;
        public int GuildGold { get; set; } = 250; // In guild vault

        // Tabard Crest
        public string CrestEmblem { get; set; } = "🦁"; // Lion Crest
        public Color CrestPrimaryColor { get; set; } = new Color(0.18f, 0.35f, 0.85f); // Deep Royal Blue
        public Color CrestBorderColor { get; set; } = new Color(0.95f, 0.80f, 0.25f);  // Gold Border

        // Roster
        List<GuildMember> members = new List<GuildMember>();
        public IReadOnlyList<GuildMember> Members => members;

        // Guild Bank (2 tabs)
        List<GuildBankItem> bankTab1 = new List<GuildBankItem>(); // Consumables & Supplies
        List<GuildBankItem> bankTab2 = new List<GuildBankItem>(); // Armory & Reagents
        List<GuildLogEntry> bankLogs = new List<GuildLogEntry>();

        public IReadOnlyList<GuildBankItem> CurrentTabItems => CurrentBankTab == 0 ? bankTab1 : bankTab2;
        public IReadOnlyList<GuildLogEntry> BankLogs => bankLogs;

        public int CurrentBankTab { get; set; } = 0; // 0 = Supplies, 1 = Armory
        public bool IsOpen { get; set; }

        public enum GuildTab { Roster, Bank, Perks, Tabard }
        public GuildTab ActiveTab { get; set; } = GuildTab.Roster;

        PlayerController player;
        Vector2 rosterScroll, bankScroll, logScroll;
        string inviteInput = "";
        string motdEdit = "";
        GuildBankItem selectedBankItem = null;

        GUIStyle titleStyle, headerStyle, subStyle, rowStyle, buttonStyle, rankStyle, itemBoxStyle, tooltipStyle;
        Texture2D bgTex, borderTex, slotTex, goldTex, barFillTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();

            SeedInitialGuild();
        }

        public void SeedInitialGuild(bool force = false)
        {
            if (members.Count > 0 && !force) return;

            members.Clear();
            bankTab1.Clear();
            bankTab2.Clear();
            bankLogs.Clear();

            string playerName = player ? player.CharacterName : "Alden";
            string playerClass = player ? player.CharacterClass : "Paladin";

            // Seeding classic WoW guild roster
            members.Add(new GuildMember { name = playerName, characterClass = playerClass, rank = GuildRank.GuildMaster, level = 5, zone = "Highspire Keep", isOnline = true, publicNote = "Guild Master & Champion", classColor = GetClassColor(playerClass) });
            members.Add(new GuildMember { name = "High Commander Abe", characterClass = "Warrior", rank = GuildRank.Officer, level = 6, zone = "Amber Vale", isOnline = true, publicNote = "Garrison Overseer", classColor = GetClassColor("Warrior") });
            members.Add(new GuildMember { name = "Archmage Karen", characterClass = "Mage", rank = GuildRank.Officer, level = 6, zone = "Highspire Keep", isOnline = true, publicNote = "Arcane Ley Line Research", classColor = GetClassColor("Mage") });
            members.Add(new GuildMember { name = "Grand Duke Ronald", characterClass = "Paladin", rank = GuildRank.Veteran, level = 5, zone = "Amber Vale", isOnline = true, publicNote = "Regent of Highspire", classColor = GetClassColor("Paladin") });
            members.Add(new GuildMember { name = "Valeria Brightwhisper", characterClass = "Priest", rank = GuildRank.Member, level = 5, zone = "Highspire Keep", isOnline = true, publicNote = "Lead Holy Healer", classColor = GetClassColor("Priest") });
            members.Add(new GuildMember { name = "Darien Sunstrider", characterClass = "Warrior", rank = GuildRank.Member, level = 5, zone = "Emberwatch Outpost", isOnline = true, publicNote = "Shield Vanguard Tank", classColor = GetClassColor("Warrior") });
            members.Add(new GuildMember { name = "Kelsey Sparks", characterClass = "Mage", rank = GuildRank.Member, level = 5, zone = "Amber Vale", isOnline = true, publicNote = "Pyromancer DPS", classColor = GetClassColor("Mage") });
            members.Add(new GuildMember { name = "Vark Swiftblade", characterClass = "Rogue", rank = GuildRank.Initiate, level = 4, zone = "Crossroads", isOnline = false, publicNote = "Outrunner Scout", classColor = GetClassColor("Rogue") });

            // Seeding Bank Tab 1: Consumables & Supplies
            bankTab1.Add(new GuildBankItem { id = "ember-potion", name = "Ember Healing Potion", rarity = "Rare", count = 12, icon = "🧪", description = "Restores 120 Health instantly." });
            bankTab1.Add(new GuildBankItem { id = "mana-biscuit", name = "Conjured Mana Biscuit", rarity = "Common", count = 20, icon = "🍞", description = "Restores 80 Mana over 15 seconds." });
            bankTab1.Add(new GuildBankItem { id = "elixir-mongoose", name = "Elixir of the Mongoose", rarity = "Rare", count = 4, icon = "⚗", description = "Increases Agility by 15 and Critical Strike by 2%." });
            bankTab1.Add(new GuildBankItem { id = "flask-titans", name = "Flask of the Titans", rarity = "Epic", count = 2, icon = "🏆", description = "Increases Maximum Health by 400 for 1 hour. Persists through death." });

            // Seeding Bank Tab 2: Armory & Reagents
            bankTab2.Add(new GuildBankItem { id = "truesilver-ingot", name = "Truesilver Ingot", rarity = "Rare", count = 16, icon = "⛏", description = "Refined rare ore used in masterwork armorsmithing." });
            bankTab2.Add(new GuildBankItem { id = "crimson-ruby", name = "Crimson Ruby", rarity = "Rare", count = 6, icon = "💎", description = "Glinting gem infused with primordial fire essence." });
            bankTab2.Add(new GuildBankItem { id = "sunforged-breastplate", name = "Sunforged Breastplate", rarity = "Epic", count = 1, icon = "🛡", description = "Heavy epic plate forged with radiant solar wards. (+12 Armor, +65 HP)" });
            bankTab2.Add(new GuildBankItem { id = "crypt-signet", name = "Ring of the Scarlet Crypt", rarity = "Epic", count = 1, icon = "💍", description = "Ancient crest recovered from Dread Abbot Vael. (+8 Dmg, +5 Crit)" });

            // Seeding Bank Activity Logs
            bankLogs.Add(new GuildLogEntry { timestamp = "Today 14:20", text = "[Archmage Karen] deposited 50 Gold into the treasury." });
            bankLogs.Add(new GuildLogEntry { timestamp = "Today 12:45", text = "[High Commander Abe] deposited [Sunforged Breastplate] into Tab 2." });
            bankLogs.Add(new GuildLogEntry { timestamp = "Yesterday 21:10", text = "[Valeria] deposited 10x [Ember Healing Potion] into Tab 1." });
            bankLogs.Add(new GuildLogEntry { timestamp = "Yesterday 19:30", text = "[Darien] withdrew 2x [Ember Healing Potion] from Tab 1." });

            motdEdit = GuildMOTD;
        }

        public static Color GetClassColor(string cls)
        {
            switch (cls)
            {
                case "Warrior": return new Color(0.78f, 0.61f, 0.43f); // Tan
                case "Mage": return new Color(0.25f, 0.78f, 0.92f);    // Cyan
                case "Rogue": return new Color(1.0f, 0.96f, 0.41f);    // Yellow
                case "Priest": return new Color(0.95f, 0.95f, 0.95f);  // White
                case "Paladin": return new Color(0.96f, 0.55f, 0.73f); // Pink
                default: return Color.white;
            }
        }

        public static Color GetRarityColor(string rarity)
        {
            switch (rarity?.ToLowerInvariant())
            {
                case "uncommon": return new Color(0.12f, 0.90f, 0.12f);
                case "rare": return new Color(0.12f, 0.55f, 1.0f);
                case "epic": return new Color(0.64f, 0.21f, 0.93f);
                case "legendary": return new Color(1.0f, 0.5f, 0.0f);
                default: return Color.white;
            }
        }

        // --- GUILD ROSTER & MEMBER MANAGEMENT API ---

        public bool AddMember(string name, string cls, GuildRank rank = GuildRank.Initiate, int level = 5, string zone = "Amber Vale", string note = "New recruit")
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (members.Exists(m => m.name.Equals(name, StringComparison.OrdinalIgnoreCase))) return false;

            var newMember = new GuildMember
            {
                name = name,
                characterClass = cls,
                rank = rank,
                level = level,
                zone = zone,
                isOnline = true,
                publicNote = note,
                classColor = GetClassColor(cls)
            };
            members.Add(newMember);

            ChatSystem.LogSystem($"[{name}] has joined <{GuildName}>!");
            ChatSystem.LogGuild("Guild", $"{name} has joined the guild.");
            AudioEffects.PlayQuestComplete();
            return true;
        }

        public bool KickMember(string name)
        {
            int idx = members.FindIndex(m => m.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                if (members[idx].rank == GuildRank.GuildMaster)
                {
                    ChatSystem.LogSystem("You cannot remove the Guild Master!");
                    return false;
                }
                string kickedName = members[idx].name;
                members.RemoveAt(idx);
                ChatSystem.LogSystem($"[{kickedName}] was removed from <{GuildName}>.");
                ChatSystem.LogGuild("Guild", $"{kickedName} was removed from the guild.");
                return true;
            }
            return false;
        }

        public bool PromoteMember(string name)
        {
            var m = members.Find(x => x.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (m != null && m.rank > GuildRank.Officer)
            {
                m.rank--;
                ChatSystem.LogSystem($"[{m.name}] promoted to [{m.rank}].");
                ChatSystem.LogGuild("Guild", $"{m.name} was promoted to {m.rank}.");
                AudioEffects.PlayClick();
                return true;
            }
            return false;
        }

        public bool DemoteMember(string name)
        {
            var m = members.Find(x => x.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (m != null && m.rank >= GuildRank.Officer && m.rank < GuildRank.Initiate)
            {
                m.rank++;
                ChatSystem.LogSystem($"[{m.name}] demoted to [{m.rank}].");
                ChatSystem.LogGuild("Guild", $"{m.name} was demoted to {m.rank}.");
                AudioEffects.PlayClick();
                return true;
            }
            return false;
        }

        public void SetMOTD(string newMotd)
        {
            if (string.IsNullOrEmpty(newMotd)) return;
            GuildMOTD = newMotd;
            motdEdit = newMotd;
            ChatSystem.LogGuild("Guild", $"New Message of the Day: {newMotd}");
            AudioEffects.PlayClick();
        }

        // --- GUILD BANK API ---

        public bool DepositGold(int amount)
        {
            if (amount <= 0) return false;
            if (!player) player = GetComponent<PlayerController>() ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player && player.Gold < amount)
            {
                ChatSystem.LogSystem("Not enough gold to deposit into the guild bank.");
                return false;
            }

            if (player) player.AddGoldDirect(-amount);
            GuildGold += amount;
            string actor = player ? player.CharacterName : "Player";
            bankLogs.Insert(0, new GuildLogEntry { timestamp = "Just now", text = $"[{actor}] deposited {amount} Gold into the treasury." });
            ChatSystem.LogSystem($"Deposited {amount} Gold into <{GuildName}> Treasury. Vault Total: {GuildGold} 💰.");
            AudioEffects.PlayClick();
            return true;
        }

        public bool WithdrawGold(int amount)
        {
            if (amount <= 0 || GuildGold < amount)
            {
                ChatSystem.LogSystem("Not enough gold in the guild vault.");
                return false;
            }

            GuildGold -= amount;
            if (!player) player = GetComponent<PlayerController>() ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player) player.AddGoldDirect(amount);
            string actor = player ? player.CharacterName : "Player";
            bankLogs.Insert(0, new GuildLogEntry { timestamp = "Just now", text = $"[{actor}] withdrew {amount} Gold from the treasury." });
            ChatSystem.LogSystem($"Withdrew {amount} Gold from <{GuildName}> Treasury. Remaining: {GuildGold} 💰.");
            AudioEffects.PlayClick();
            return true;
        }

        public bool WithdrawBankItem(GuildBankItem item)
        {
            if (item == null) return false;
            var targetList = CurrentBankTab == 0 ? bankTab1 : bankTab2;
            if (!targetList.Contains(item)) return false;

            if (!player) player = GetComponent<PlayerController>() ?? UnityEngine.Object.FindAnyObjectByType<PlayerController>();

            if (player)
            {
                player.GrantItem(item.id);
            }

            item.count--;
            string actor = player ? player.CharacterName : "Player";
            bankLogs.Insert(0, new GuildLogEntry { timestamp = "Just now", text = $"[{actor}] withdrew [{item.name}]." });
            ChatSystem.LogLoot(item.name, item.rarity, 1);

            if (item.count <= 0)
            {
                targetList.Remove(item);
                if (selectedBankItem == item) selectedBankItem = null;
            }

            AudioEffects.PlayClick();
            return true;
        }

        public void DepositBankItem(string id, string name, string rarity, int count, string icon, string desc, int tab = 0)
        {
            var targetList = tab == 0 ? bankTab1 : bankTab2;
            var existing = targetList.Find(x => x.id == id);
            if (existing != null)
            {
                existing.count += count;
            }
            else
            {
                targetList.Add(new GuildBankItem
                {
                    id = id,
                    name = name,
                    rarity = rarity,
                    count = count,
                    icon = icon,
                    description = desc
                });
            }
            string actor = player ? player.CharacterName : "Player";
            bankLogs.Insert(0, new GuildLogEntry { timestamp = "Just now", text = $"[{actor}] deposited [{name}] x{count}." });
            ChatSystem.LogSystem($"Deposited [{name}] x{count} into Guild Bank Tab {tab + 1}.");
            AudioEffects.PlayClick();
        }

        // --- GUI DRAWING ---

        public void DrawGuildGUI(float screenW, float screenH)
        {
            if (!IsOpen || FrontendMenu.IsOpen) return;

            InitStyles();

            float windowW = 660f;
            float windowH = 500f;
            float winX = (screenW - windowW) * 0.5f;
            float winY = (screenH - windowH) * 0.5f;

            // Outer Frame
            GUI.DrawTexture(new Rect(winX - 2f, winY - 2f, windowW + 4f, windowH + 4f), borderTex);
            GUI.DrawTexture(new Rect(winX, winY, windowW, windowH), bgTex);

            // Title Bar
            float headerH = 38f;
            GUI.DrawTexture(new Rect(winX, winY, windowW, headerH), borderTex);
            GUI.DrawTexture(new Rect(winX + 1f, winY + 1f, windowW - 2f, headerH - 2f), bgTex);

            string titleStr = $"{CrestEmblem} <{GuildName}>  ·  LEVEL {GuildLevel}";
            GUI.Label(new Rect(winX + 16f, winY + 7f, windowW - 60f, 24f), titleStr, titleStyle);

            if (GUI.Button(new Rect(winX + windowW - 32f, winY + 7f, 24f, 24f), "✕", buttonStyle))
            {
                IsOpen = false;
                AudioEffects.PlayClick();
            }

            // MOTD Banner
            float motdY = winY + headerH + 4f;
            GUI.DrawTexture(new Rect(winX + 12f, motdY, windowW - 24f, 26f), bgTex);
            var motdStyle = new GUIStyle(subStyle) { fontStyle = FontStyle.Italic, fontSize = 11 };
            motdStyle.normal.textColor = new Color(0.4f, 0.95f, 0.4f);
            GUI.Label(new Rect(winX + 18f, motdY + 3f, windowW - 36f, 20f), $"<b>MOTD:</b> {GuildMOTD}", motdStyle);

            // Navigation Tabs
            float tabY = motdY + 32f;
            float tabW = 120f;
            float tabH = 26f;

            DrawNavTab(winX + 14f + 0 * (tabW + 6f), tabY, tabW, tabH, "👥 Roster", GuildTab.Roster);
            DrawNavTab(winX + 14f + 1 * (tabW + 6f), tabY, tabW, tabH, "🏦 Guild Bank", GuildTab.Bank);
            DrawNavTab(winX + 14f + 2 * (tabW + 6f), tabY, tabW, tabH, "✨ Perks & Info", GuildTab.Perks);
            DrawNavTab(winX + 14f + 3 * (tabW + 6f), tabY, tabW, tabH, "🛡 Tabard", GuildTab.Tabard);

            // Content Area
            float contentX = winX + 14f;
            float contentY = tabY + tabH + 8f;
            float contentW = windowW - 28f;
            float contentH = windowH - (contentY - winY) - 14f;

            GUI.DrawTexture(new Rect(contentX - 1f, contentY - 1f, contentW + 2f, contentH + 2f), borderTex);
            GUI.DrawTexture(new Rect(contentX, contentY, contentW, contentH), bgTex);

            switch (ActiveTab)
            {
                case GuildTab.Roster:
                    DrawRosterTab(contentX, contentY, contentW, contentH);
                    break;
                case GuildTab.Bank:
                    DrawBankTab(contentX, contentY, contentW, contentH);
                    break;
                case GuildTab.Perks:
                    DrawPerksTab(contentX, contentY, contentW, contentH);
                    break;
                case GuildTab.Tabard:
                    DrawTabardTab(contentX, contentY, contentW, contentH);
                    break;
            }
        }

        void DrawNavTab(float x, float y, float w, float h, string label, GuildTab tab)
        {
            bool isActive = ActiveTab == tab;
            var oldCol = GUI.backgroundColor;
            if (isActive) GUI.backgroundColor = new Color(0.85f, 0.70f, 0.28f);

            if (GUI.Button(new Rect(x, y, w, h), label, buttonStyle))
            {
                ActiveTab = tab;
                AudioEffects.PlayClick();
            }
            GUI.backgroundColor = oldCol;
        }

        void DrawRosterTab(float x, float y, float w, float h)
        {
            // Table Header
            float headerH = 22f;
            GUI.DrawTexture(new Rect(x, y, w, headerH), borderTex);
            GUI.Label(new Rect(x + 10f, y + 2f, 150f, 18f), "Member Name", headerStyle);
            GUI.Label(new Rect(x + 170f, y + 2f, 75f, 18f), "Level / Class", headerStyle);
            GUI.Label(new Rect(x + 255f, y + 2f, 100f, 18f), "Guild Rank", headerStyle);
            GUI.Label(new Rect(x + 360f, y + 2f, 110f, 18f), "Zone Location", headerStyle);
            GUI.Label(new Rect(x + 480f, y + 2f, 130f, 18f), "Public Note", headerStyle);

            // Scrollable List
            float scrollH = h - headerH - 42f;
            Rect viewRect = new Rect(0, 0, w - 20f, members.Count * 28f + 10f);
            rosterScroll = GUI.BeginScrollView(new Rect(x, y + headerH, w, scrollH), rosterScroll, viewRect);

            for (int i = 0; i < members.Count; i++)
            {
                var m = members[i];
                float ry = i * 28f + 4f;

                if (i % 2 == 1)
                {
                    GUI.DrawTexture(new Rect(2f, ry - 2f, viewRect.width - 4f, 26f), borderTex);
                }

                // Online indicator & Name
                string dot = m.isOnline ? "🟢 " : "⚪ ";
                var nameColStyle = new GUIStyle(rowStyle);
                nameColStyle.normal.textColor = m.classColor;
                GUI.Label(new Rect(6f, ry, 160f, 22f), dot + m.name, nameColStyle);

                // Level & Class
                GUI.Label(new Rect(170f, ry, 80f, 22f), $"Lv {m.level} {m.characterClass}", rowStyle);

                // Rank
                rankStyle.normal.textColor = m.rank == GuildRank.GuildMaster ? new Color(1f, 0.85f, 0.2f) :
                                             m.rank == GuildRank.Officer ? new Color(0.4f, 0.8f, 1f) : Color.white;
                GUI.Label(new Rect(255f, ry, 100f, 22f), $"{m.rank}", rankStyle);

                // Zone
                GUI.Label(new Rect(360f, ry, 110f, 22f), m.zone, rowStyle);

                // Note
                GUI.Label(new Rect(480f, ry, 130f, 22f), m.publicNote, rowStyle);
            }

            GUI.EndScrollView();

            // Bottom Actions: Invite Member
            float botY = y + h - 36f;
            GUI.Label(new Rect(x + 10f, botY + 4f, 110f, 22f), "Invite Player:", subStyle);
            inviteInput = GUI.TextField(new Rect(x + 115f, botY + 3f, 140f, 24f), inviteInput);

            if (GUI.Button(new Rect(x + 265f, botY + 3f, 80f, 24f), "Invite", buttonStyle))
            {
                if (!string.IsNullOrEmpty(inviteInput))
                {
                    AddMember(inviteInput, "Warrior", GuildRank.Initiate, 5, "Amber Vale", "Recruit");
                    inviteInput = "";
                }
            }

            GUI.Label(new Rect(x + w - 190f, botY + 4f, 180f, 22f), $"Total Members: {members.Count} (7 Online)", subStyle);
        }

        void DrawBankTab(float x, float y, float w, float h)
        {
            // Bank Sub-Tabs & Vault Gold
            float barH = 34f;
            var tab1Col = GUI.backgroundColor;
            if (CurrentBankTab == 0) GUI.backgroundColor = new Color(0.85f, 0.70f, 0.28f);
            if (GUI.Button(new Rect(x + 8f, y + 6f, 160f, 26f), "📦 Tab 1: Consumables", buttonStyle))
            {
                CurrentBankTab = 0;
                AudioEffects.PlayClick();
            }
            GUI.backgroundColor = tab1Col;

            var tab2Col = GUI.backgroundColor;
            if (CurrentBankTab == 1) GUI.backgroundColor = new Color(0.85f, 0.70f, 0.28f);
            if (GUI.Button(new Rect(x + 175f, y + 6f, 160f, 26f), "⚔ Tab 2: Armory & Ores", buttonStyle))
            {
                CurrentBankTab = 1;
                AudioEffects.PlayClick();
            }
            GUI.backgroundColor = tab2Col;

            // Vault Gold
            string goldStr = $"Vault Gold: <b>{GuildGold}</b> 💰";
            GUI.Label(new Rect(x + w - 290f, y + 8f, 140f, 22f), goldStr, subStyle);

            if (GUI.Button(new Rect(x + w - 145f, y + 6f, 65f, 24f), "+10g", buttonStyle))
            {
                DepositGold(10);
            }
            if (GUI.Button(new Rect(x + w - 75f, y + 6f, 65f, 24f), "-10g", buttonStyle))
            {
                WithdrawGold(10);
            }

            // Left: Grid of 16 vault item slots (4x4)
            float gridX = x + 14f;
            float gridY = y + barH + 14f;
            float slotSize = 44f;
            float slotGap = 8f;

            var currentList = CurrentTabItems;

            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    int index = r * 4 + c;
                    float sx = gridX + c * (slotSize + slotGap);
                    float sy = gridY + r * (slotSize + slotGap);
                    Rect slotRect = new Rect(sx, sy, slotSize, slotSize);

                    GUI.DrawTexture(new Rect(sx - 1f, sy - 1f, slotSize + 2f, slotSize + 2f), borderTex);
                    GUI.DrawTexture(slotRect, slotTex);

                    if (index < currentList.Count)
                    {
                        var itm = currentList[index];
                        var rCol = GetRarityColor(itm.rarity);

                        // Icon
                        var iconStyle = new GUIStyle(titleStyle) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
                        GUI.Label(new Rect(sx, sy + 2f, slotSize, slotSize * 0.75f), itm.icon, iconStyle);

                        // Count
                        if (itm.count > 1)
                        {
                            var countStyle = new GUIStyle(subStyle) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerRight };
                            countStyle.normal.textColor = Color.yellow;
                            GUI.Label(new Rect(sx, sy, slotSize - 3f, slotSize - 2f), $"{itm.count}", countStyle);
                        }

                        // Selection
                        if (GUI.Button(slotRect, GUIContent.none, GUIStyle.none))
                        {
                            selectedBankItem = itm;
                            AudioEffects.PlayClick();
                        }
                    }
                }
            }

            // Right: Selected Item Details & Activity Log
            float rightX = gridX + 4 * (slotSize + slotGap) + 16f;
            float rightW = w - (rightX - x) - 12f;

            if (selectedBankItem != null)
            {
                float detailH = 105f;
                GUI.DrawTexture(new Rect(rightX - 1f, gridY - 1f, rightW + 2f, detailH + 2f), borderTex);
                GUI.DrawTexture(new Rect(rightX, gridY, rightW, detailH), bgTex);

                var rCol = GetRarityColor(selectedBankItem.rarity);
                var itemNameStyle = new GUIStyle(headerStyle) { fontSize = 12 };
                itemNameStyle.normal.textColor = rCol;

                GUI.Label(new Rect(rightX + 10f, gridY + 6f, rightW - 20f, 18f), $"{selectedBankItem.icon} {selectedBankItem.name} ({selectedBankItem.rarity})", itemNameStyle);
                GUI.Label(new Rect(rightX + 10f, gridY + 26f, rightW - 20f, 40f), selectedBankItem.description, subStyle);

                if (GUI.Button(new Rect(rightX + 10f, gridY + detailH - 30f, 140f, 24f), "Withdraw to Bags", buttonStyle))
                {
                    WithdrawBankItem(selectedBankItem);
                }
            }

            // Transaction Log
            float logY = gridY + 115f;
            float logH = h - (logY - y) - 12f;
            GUI.Label(new Rect(rightX, logY - 18f, rightW, 18f), "Recent Vault Activity:", headerStyle);

            GUI.DrawTexture(new Rect(rightX - 1f, logY - 1f, rightW + 2f, logH + 2f), borderTex);
            GUI.DrawTexture(new Rect(rightX, logY, rightW, logH), bgTex);

            Rect logView = new Rect(0, 0, rightW - 20f, bankLogs.Count * 22f + 10f);
            logScroll = GUI.BeginScrollView(new Rect(rightX, logY, rightW, logH), logScroll, logView);
            for (int i = 0; i < bankLogs.Count; i++)
            {
                var l = bankLogs[i];
                GUI.Label(new Rect(6f, i * 22f + 2f, logView.width, 20f), $"{l.timestamp} - {l.text}", subStyle);
            }
            GUI.EndScrollView();
        }

        void DrawPerksTab(float x, float y, float w, float h)
        {
            // Guild XP & Level
            float leftW = 280f;
            GUI.Label(new Rect(x + 14f, y + 10f, leftW, 22f), $"Guild Level: {GuildLevel} / 25", titleStyle);

            float xpRatio = Mathf.Clamp01((float)GuildXP / GuildXPNext);
            GUI.DrawTexture(new Rect(x + 14f, y + 36f, leftW, 14f), slotTex);
            GUI.DrawTexture(new Rect(x + 14f, y + 36f, leftW * xpRatio, 14f), barFillTex);
            GUI.Label(new Rect(x + 14f, y + 34f, leftW, 16f), $"XP: {GuildXP:N0} / {GuildXPNext:N0} ({xpRatio * 100f:0.0}%)", subStyle);

            // Active Guild Perks
            GUI.Label(new Rect(x + 14f, y + 64f, leftW, 20f), "ACTIVE GUILD PERKS:", headerStyle);
            string[] perks = {
                "⚡ <b>Fast Track:</b> +10% Experience from all quests & monsters.",
                "🏇 <b>Mount Up:</b> +10% Movement speed on mounts and flights.",
                "💰 <b>Bountiful Spoils:</b> +15% Gold looted from dungeon creatures.",
                "🕊 <b>Hasty Hearth:</b> Reduces Hearthstone cooldown by 50%."
            };

            for (int i = 0; i < perks.Length; i++)
            {
                GUI.Label(new Rect(x + 14f, y + 90f + i * 36f, leftW, 34f), perks[i], subStyle);
            }

            // Right: MOTD Editor
            float rightX = x + leftW + 30f;
            float rightW = w - leftW - 44f;

            GUI.Label(new Rect(rightX, y + 10f, rightW, 22f), "GUILD MESSAGE OF THE DAY (MOTD):", headerStyle);
            motdEdit = GUI.TextArea(new Rect(rightX, y + 36f, rightW, 90f), motdEdit, 240);

            if (GUI.Button(new Rect(rightX, y + 134f, 130f, 28f), "Save MOTD", buttonStyle))
            {
                SetMOTD(motdEdit);
            }

            // Guild Statistics
            float statY = y + 180f;
            GUI.Label(new Rect(rightX, statY, rightW, 20f), "GUILD RECORD & ACCOMPLISHMENTS:", headerStyle);
            string stats =
                "• <b>Founded:</b> Day of the Silver Vanguard\n" +
                "• <b>Dungeon Purges:</b> 14 Scarlet Crypt purges completed\n" +
                "• <b>Apex Dragons Slain:</b> 3 Vermithrax encounters vanquished\n" +
                "• <b>Total Guild Vault Wealth:</b> 250 Gold stored\n" +
                "• <b>Guild Master:</b> " + (player ? player.CharacterName : "Alden");
            GUI.Label(new Rect(rightX, statY + 24f, rightW, 110f), stats, subStyle);
        }

        void DrawTabardTab(float x, float y, float w, float h)
        {
            float previewSize = 130f;
            float prevX = x + 30f;
            float prevY = y + 30f;

            // Live Tabard Preview
            GUI.DrawTexture(new Rect(prevX - 3f, prevY - 3f, previewSize + 6f, previewSize * 1.3f + 6f), borderTex);
            var oldCol = GUI.color;
            GUI.color = CrestPrimaryColor;
            GUI.DrawTexture(new Rect(prevX, prevY, previewSize, previewSize * 1.3f), Texture2D.whiteTexture);
            GUI.color = oldCol;

            var crestStyle = new GUIStyle(titleStyle) { fontSize = 52, alignment = TextAnchor.MiddleCenter };
            crestStyle.normal.textColor = CrestBorderColor;
            GUI.Label(new Rect(prevX, prevY + 20f, previewSize, previewSize), CrestEmblem, crestStyle);

            GUI.Label(new Rect(prevX, prevY + previewSize * 1.3f + 10f, previewSize, 22f), $"<{GuildName}>", subStyle);

            // Crest Options
            float optX = prevX + previewSize + 40f;
            float optW = w - previewSize - 90f;

            GUI.Label(new Rect(optX, y + 20f, optW, 22f), "SELECT GUILD CREST EMBLEM:", headerStyle);
            string[] emblems = { "🦁", "🦅", "⚔", "🐉", "🐺", "⚡" };
            string[] emblemNames = { "Lion", "Gryphon", "Swords", "Dragon", "Wolf", "Lightning" };

            float btnW = 54f;
            for (int i = 0; i < emblems.Length; i++)
            {
                var ebCol = GUI.backgroundColor;
                if (CrestEmblem == emblems[i]) GUI.backgroundColor = new Color(0.85f, 0.70f, 0.28f);

                var eStyle = new GUIStyle(buttonStyle) { fontSize = 18 };
                if (GUI.Button(new Rect(optX + i * (btnW + 8f), y + 48f, btnW, 40f), emblems[i], eStyle))
                {
                    CrestEmblem = emblems[i];
                    AudioEffects.PlayClick();
                }
                GUI.backgroundColor = ebCol;
            }

            // Color Palette
            GUI.Label(new Rect(optX, y + 105f, optW, 22f), "SELECT TABARD BACKGROUND COLOR:", headerStyle);
            Color[] colors = {
                new Color(0.18f, 0.35f, 0.85f), // Royal Blue
                new Color(0.75f, 0.15f, 0.15f), // Crimson
                new Color(0.15f, 0.60f, 0.20f), // Forest Green
                new Color(0.55f, 0.20f, 0.75f), // Void Purple
                new Color(0.12f, 0.14f, 0.18f), // Shadow Black
                new Color(0.85f, 0.70f, 0.25f)  // Radiant Gold
            };

            for (int i = 0; i < colors.Length; i++)
            {
                var cbCol = GUI.backgroundColor;
                GUI.backgroundColor = colors[i];
                if (GUI.Button(new Rect(optX + i * 44f, y + 132f, 36f, 30f), GUIContent.none))
                {
                    CrestPrimaryColor = colors[i];
                    AudioEffects.PlayClick();
                }
                GUI.backgroundColor = cbCol;
            }

            GUI.Label(new Rect(optX, y + 185f, optW, 40f), "Guild Tabards can be equipped in your character paperdoll tabard slot or displayed over mail and plate armor.", subStyle);
        }

        void InitStyles()
        {
            if (bgTex != null) return;

            bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.95f));
            bgTex.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.85f, 0.70f, 0.28f, 0.95f)); // Gold WoW Border
            borderTex.Apply();

            slotTex = new Texture2D(1, 1);
            slotTex.SetPixel(0, 0, new Color(0.04f, 0.05f, 0.07f, 0.90f));
            slotTex.Apply();

            goldTex = new Texture2D(1, 1);
            goldTex.SetPixel(0, 0, new Color(0.95f, 0.82f, 0.25f));
            goldTex.Apply();

            barFillTex = new Texture2D(1, 1);
            barFillTex.SetPixel(0, 0, new Color(0.2f, 0.65f, 0.95f));
            barFillTex.Apply();

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
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

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = Color.white;

            rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
            rowStyle.normal.textColor = Color.white;

            rankStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
