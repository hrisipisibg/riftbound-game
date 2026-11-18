using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class EncounterJournal : MonoBehaviour
    {
        public static EncounterJournal Instance { get; private set; }

        public class BossAbility
        {
            public string name;
            public string icon;
            public string castTime;
            public string description;
            public bool isInterruptible;
        }

        public class BossLoot
        {
            public string itemId;
            public string dropChance;
        }

        public class BossEncounterDef
        {
            public string id;
            public string name;
            public string title;
            public string zone;
            public string roleTag; // "Dungeon Boss", "World Boss", "Apex Sovereign"
            public string icon;
            public int health;
            public string lore;
            public string location;
            public string tankTip;
            public string healTip;
            public string dpsTip;
            public List<BossAbility> abilities = new List<BossAbility>();
            public List<BossLoot> lootTable = new List<BossLoot>();
        }

        readonly List<BossEncounterDef> encounters = new List<BossEncounterDef>();
        int selectedEncounterIndex = 0;
        int selectedTab = 0; // 0: Overview & Lore, 1: Abilities & Tactics, 2: Loot Table

        Vector2 leftScrollPos, rightScrollPos;
        PlayerController player;

        GUIStyle headerStyle, subHeaderStyle, bossTitleStyle, tabStyle, textStyle, abilityTitleStyle, roleTipStyle, smallStyle;
        Texture2D darkBgTex, cardBgTex, borderTex, goldTex, tabActiveTex, tabInactiveTex;

        public IReadOnlyList<BossEncounterDef> Encounters
        {
            get
            {
                if (encounters.Count == 0) RegisterEncounters();
                return encounters;
            }
        }

        public BossEncounterDef GetEncounter(string id)
        {
            if (encounters.Count == 0) RegisterEncounters();
            return encounters.Find(e => e.id == id);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            RegisterEncounters();
        }

        void RegisterEncounters()
        {
            if (encounters.Count > 0) return;

            // 1. High Inquisitor Malakar
            var malakar = new BossEncounterDef
            {
                id = "malakar",
                name = "High Inquisitor Malakar",
                title = "The Scarlet Inquisitor",
                zone = "The Scarlet Crypts (Duskwood)",
                roleTag = "Dungeon Boss",
                icon = "✝",
                health = 450,
                location = "Cathedral Grand Altar (-55, -20, 215)",
                lore = "A zealous commander of the Scarlet Crusade who retreated into the cathedral crypts. Wielding holy vengeance, Malakar sanctifies the ground with righteous fury to incinerate all who trespass.",
                tankTip = "Face Malakar away from the cathedral dais and step out of golden Consecration zones.",
                healTip = "Save high-output heals for Holy Wrath bursts. Cleanse inquisitor debuffs rapidly.",
                dpsTip = "Use your Spell Interrupt [F] (Pummel / Kick / Counterspell / Rebuke) on Holy Wrath (2.2s cast) to prevent lethal group damage!"
            };
            malakar.abilities.Add(new BossAbility
            {
                name = "Holy Wrath",
                icon = "☼",
                castTime = "2.2s Cast (Interruptible)",
                description = "Channels concentrated holy radiance dealing 24 Holy AoE damage to all nearby champions. Can and MUST be interrupted with [F]!",
                isInterruptible = true
            });
            malakar.abilities.Add(new BossAbility
            {
                name = "Consecrated Ground",
                icon = "🔥",
                castTime = "Instant",
                description = "Sanctifies the cathedral stone in a glowing golden ring, burning intruders standing within.",
                isInterruptible = false
            });
            malakar.abilities.Add(new BossAbility
            {
                name = "Scarlet Sanctuary",
                icon = "🛡",
                castTime = "Phase 2 Transition",
                description = "Upon reaching 1 HP, Malakar seals himself inside an impervious divine barrier while Dread Abbot Vael awakens.",
                isInterruptible = false
            });
            malakar.lootTable.Add(new BossLoot { itemId = "scarlet-blade", dropChance = "100%" });
            malakar.lootTable.Add(new BossLoot { itemId = "cathedral-relic", dropChance = "65%" });
            encounters.Add(malakar);

            // 2. Dread Abbot Vael
            var vael = new BossEncounterDef
            {
                id = "vael",
                name = "Dread Abbot Vael",
                title = "The Fallen Abbot",
                zone = "The Scarlet Crypts (Duskwood)",
                roleTag = "Dungeon Final Boss",
                icon = "💀",
                health = 520,
                location = "Cathedral Grand Altar (-55, -20, 215)",
                lore = "The corrupted high abbot of the Scarlet Crusade. When Malakar falls, Vael breaks his meditative trance and unleashes devastating shadow volleys.",
                tankTip = "Interrupt Shadow Bolt Volley and hold Vael centered in the grand altar hall.",
                healTip = "Triage shadow burst damage. Anticipate dark projectile bursts when lockout expires.",
                dpsTip = "Coordinate interrupts on Shadow Bolt Volley (2.4s cast). Burst Abbot Vael to purge the crypts permanently!"
            };
            vael.abilities.Add(new BossAbility
            {
                name = "Shadow Bolt Volley",
                icon = "💀",
                castTime = "2.4s Cast (Interruptible)",
                description = "Unleashes necrotic shadow bolts striking all party members for 28 Shadow damage. Interrupt with [F] to lock shadow magic for 4s!",
                isInterruptible = true
            });
            vael.abilities.Add(new BossAbility
            {
                name = "Dark Empowerment",
                icon = "⚡",
                castTime = "Passive",
                description = "Amplifies spell projectile speed and inflicts shadow vulnerability upon targets.",
                isInterruptible = false
            });
            vael.lootTable.Add(new BossLoot { itemId = "scarlet-aegis", dropChance = "100%" });
            vael.lootTable.Add(new BossLoot { itemId = "duskwood-shadow-dagger", dropChance = "50%" });
            encounters.Add(vael);

            // 3. Sandscourge Rex
            var rex = new BossEncounterDef
            {
                id = "rex",
                name = "Sandscourge Rex",
                title = "The Desert Colossus",
                zone = "Redridge Canyons (Barrens)",
                roleTag = "World Boss",
                icon = "🌪",
                health = 1200,
                location = "Shifting Dunes (45, 185)",
                lore = "An ancient armored dread-beast burrowing beneath the red sands of the canyon frontier. Capable of triggering localized sandstorms and devastating earth tremors.",
                tankTip = "Face Rex facing north away from open roads. Keep boss grounded during submerge phase.",
                healTip = "Dispel bleeding sands and maintain high defensive shields on the primary tank.",
                dpsTip = "Attack from the flanks; avoid tail sweep knockbacks and focus during sand-burrow breaches!"
            };
            rex.abilities.Add(new BossAbility
            {
                name = "Sandstorm Burrow",
                icon = "🌪",
                castTime = "Channeling",
                description = "Submerges beneath the earth, whipping up choking sand before erupting under unsuspecting adventurers.",
                isInterruptible = false
            });
            rex.abilities.Add(new BossAbility
            {
                name = "Crushing Cleave",
                icon = "🔨",
                castTime = "1.8s Cast (Interruptible)",
                description = "Heavy frontal slam that fractures armor. Interrupt with [F] to avoid heavy damage.",
                isInterruptible = true
            });
            rex.lootTable.Add(new BossLoot { itemId = "worldbreaker-maul", dropChance = "45%" });
            rex.lootTable.Add(new BossLoot { itemId = "crown-of-molten-ruin", dropChance = "40%" });
            encounters.Add(rex);

            // 4. Vermithrax the Ember Sovereign
            var vermithrax = new BossEncounterDef
            {
                id = "vermithrax",
                name = "Vermithrax the Ember Sovereign",
                title = "Apex of Mount Wyrmcrest",
                zone = "Wyrmcrest Summit (Mountain Peak)",
                roleTag = "Apex Sovereign",
                icon = "🐉",
                health = 2500,
                location = "Summit Dais (0, 48, 225)",
                lore = "The primordial elder drake whose molten breath reshaped the continental crags. Roosting atop the highest elevation of Azeboth, only flying mounts can reach his sovereign dais.",
                tankTip = "Position Vermithrax sideways along the caldera edge. Stack defensive mitigation during Enrage!",
                healTip = "High continuous AoE triage during Molten Meteor bombardment. Keep everyone above 50% HP.",
                dpsTip = "Interrupt Pyroclastic Surge (2.6s) during Phase 1. When Enrage hits at 50% HP, the cast becomes UNINTERRUPTIBLE (Shield 🛡)—move out of the cone instantly!"
            };
            vermithrax.abilities.Add(new BossAbility
            {
                name = "Pyroclastic Surge",
                icon = "🔥",
                castTime = "2.6s Cast (Interruptible in P1)",
                description = "Channels a massive 65-degree cone of primordial fire dealing 36 Fire damage. Interruptible during Phase 1; becomes non-interruptible with a Silver Shield during Enrage!",
                isInterruptible = true
            });
            vermithrax.abilities.Add(new BossAbility
            {
                name = "Molten Meteors",
                icon = "☄",
                castTime = "Periodic (9s)",
                description = "Calls down 3 fiery meteors from the heavens, blasting 28 Fire damage across 4.5m impact craters.",
                isInterruptible = false
            });
            vermithrax.abilities.Add(new BossAbility
            {
                name = "Primordial Flame Enrage",
                icon = "💥",
                castTime = "Phase 2 (50% HP)",
                description = "At 50% HP, Vermithrax erupts into molten fury: attack speed increases by 40%, attack damage increases, and all breath casts become shielded and non-interruptible!",
                isInterruptible = false
            });
            vermithrax.lootTable.Add(new BossLoot { itemId = "wyrmtooth-edge", dropChance = "100%" });
            vermithrax.lootTable.Add(new BossLoot { itemId = "ember-drake-scale", dropChance = "100%" });
            encounters.Add(vermithrax);
        }

        public void DrawEncounterJournalGUI(float screenW, float screenH)
        {
            if (FrontendMenu.IsOpen) return;
            InitStyles();

            if (!player) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

            float winW = 760f;
            float winH = 510f;
            float winX = screenW / 2f - winW / 2f;
            float winY = screenH / 2f - winH / 2f;

            // Outer Frame Border & Gothic Texture
            GUI.DrawTexture(new Rect(winX - 3f, winY - 3f, winW + 6f, winH + 6f), borderTex);
            GUI.DrawTexture(new Rect(winX, winY, winW, winH), darkBgTex);

            // Title Header Bar
            GUI.DrawTexture(new Rect(winX, winY, winW, 40f), cardBgTex);
            GUI.Label(new Rect(winX + 16f, winY + 8f, winW - 60f, 24f), "📖 ENCOUNTER JOURNAL: DUNGEONS & WORLD BOSSES [Shift+J]", headerStyle);

            if (GUI.Button(new Rect(winX + winW - 32f, winY + 8f, 24f, 24f), "✕", smallStyle))
            {
                if (player) player.EncounterJournalOpen = false;
                AudioEffects.PlayClick();
            }

            // ==========================================
            // LEFT COLUMN: Boss & Dungeon Selector (220px)
            // ==========================================
            float leftW = 215f;
            float leftH = winH - 52f;
            Rect leftView = new Rect(winX + 12f, winY + 44f, leftW, leftH);

            GUI.DrawTexture(leftView, cardBgTex);
            leftScrollPos = GUI.BeginScrollView(leftView, leftScrollPos, new Rect(0, 0, leftW - 16f, encounters.Count * 54f));

            for (int i = 0; i < encounters.Count; i++)
            {
                var enc = encounters[i];
                float btnY = i * 54f;
                Rect btnRect = new Rect(2f, btnY + 2f, leftW - 20f, 50f);

                bool isSelected = selectedEncounterIndex == i;
                GUI.DrawTexture(btnRect, isSelected ? tabActiveTex : darkBgTex);
                if (isSelected)
                {
                    GUI.DrawTexture(new Rect(btnRect.x, btnRect.y, 3f, btnRect.height), goldTex);
                }

                if (GUI.Button(btnRect, GUIContent.none, GUIStyle.none))
                {
                    selectedEncounterIndex = i;
                    AudioEffects.PlayClick();
                }

                GUI.Label(new Rect(btnRect.x + 6f, btnRect.y + 6f, 24f, 24f), enc.icon, headerStyle);
                GUI.Label(new Rect(btnRect.x + 32f, btnRect.y + 6f, leftW - 56f, 20f), enc.name, isSelected ? headerStyle : bossTitleStyle);
                GUI.Label(new Rect(btnRect.x + 32f, btnRect.y + 26f, leftW - 56f, 18f), enc.roleTag, smallStyle);
            }

            GUI.EndScrollView();

            // ==========================================
            // RIGHT PANEL: Selected Boss Details (515px)
            // ==========================================
            var curEnc = encounters[selectedEncounterIndex];
            float rightX = winX + leftW + 20f;
            float rightY = winY + 44f;
            float rightW = winW - leftW - 32f;

            // Boss Title Banner
            GUI.DrawTexture(new Rect(rightX, rightY, rightW, 46f), cardBgTex);
            GUI.Label(new Rect(rightX + 12f, rightY + 4f, rightW - 24f, 22f), $"{curEnc.name}  ·  {curEnc.title}", headerStyle);
            GUI.Label(new Rect(rightX + 12f, rightY + 26f, rightW - 24f, 16f), $"Location: {curEnc.zone}  ·  Max Health: {curEnc.health} HP  ·  {curEnc.roleTag}", subHeaderStyle);

            // Tab Navigation Bar
            float tabY = rightY + 52f;
            float tabW = rightW / 3f - 4f;

            string[] tabs = { "📜 Overview & Lore", "⚔ Abilities & Tactics", "💎 Loot Table" };
            for (int t = 0; t < 3; t++)
            {
                Rect tabRect = new Rect(rightX + t * (tabW + 6f), tabY, tabW, 28f);
                bool isTabActive = selectedTab == t;
                GUI.DrawTexture(tabRect, isTabActive ? tabActiveTex : tabInactiveTex);
                if (isTabActive)
                {
                    GUI.DrawTexture(new Rect(tabRect.x, tabRect.yMax - 2f, tabRect.width, 2f), goldTex);
                }

                if (GUI.Button(tabRect, tabs[t], tabStyle))
                {
                    selectedTab = t;
                    AudioEffects.PlayClick();
                }
            }

            // Tab Content Body
            float bodyY = tabY + 34f;
            float bodyH = winH - (bodyY - winY) - 12f;
            Rect rightView = new Rect(rightX, bodyY, rightW, bodyH);

            rightScrollPos = GUI.BeginScrollView(rightView, rightScrollPos, new Rect(0, 0, rightW - 16f, 360f));

            if (selectedTab == 0)
            {
                // OVERVIEW & LORE
                GUI.Label(new Rect(8f, 6f, rightW - 24f, 20f), "ENCOUNTER NARRATIVE", headerStyle);
                GUI.Label(new Rect(8f, 28f, rightW - 24f, 75f), curEnc.lore, textStyle);

                GUI.DrawTexture(new Rect(8f, 108f, rightW - 24f, 1f), borderTex);

                GUI.Label(new Rect(8f, 116f, rightW - 24f, 20f), "REGIONAL LOCATION & SANCTUARY", headerStyle);
                GUI.Label(new Rect(8f, 138f, rightW - 24f, 40f), $"Headquarters: {curEnc.location}\nReach this encounter via world exploration, dungeon portals, or aerial taxi.", textStyle);

                GUI.DrawTexture(new Rect(8f, 185f, rightW - 24f, 1f), borderTex);

                GUI.Label(new Rect(8f, 195f, rightW - 24f, 20f), "KEY MECHANICS SNAPSHOT", headerStyle);
                GUI.Label(new Rect(8f, 218f, rightW - 24f, 80f),
                    $"• Total Boss Abilities: {curEnc.abilities.Count}\n" +
                    $"• Interruptible Spells: Available via [F] (Pummel / Kick / Counterspell / Rebuke)\n" +
                    $"• Epic & Legendary Gear Drops: {curEnc.lootTable.Count} unique items\n" +
                    $"• Faction Renown: Grants major reputation upon victory", textStyle);
            }
            else if (selectedTab == 1)
            {
                // ABILITIES & TACTICS
                float aY = 4f;

                // Role Tactical Cards
                GUI.Label(new Rect(8f, aY, rightW - 24f, 18f), "ROLE GUIDANCE & STRATEGY", headerStyle);
                aY += 22f;

                // Tank Tip
                GUI.Label(new Rect(8f, aY, rightW - 24f, 26f), $"<color=#5ba3e0>🛡 Tank:</color> {curEnc.tankTip}", roleTipStyle);
                aY += 28f;
                // Healer Tip
                GUI.Label(new Rect(8f, aY, rightW - 24f, 26f), $"<color=#4ade80>✚ Healer:</color> {curEnc.healTip}", roleTipStyle);
                aY += 28f;
                // DPS Tip
                GUI.Label(new Rect(8f, aY, rightW - 24f, 26f), $"<color=#f87171>⚔ DPS:</color> {curEnc.dpsTip}", roleTipStyle);
                aY += 34f;

                GUI.DrawTexture(new Rect(8f, aY, rightW - 24f, 1f), borderTex);
                aY += 8f;

                GUI.Label(new Rect(8f, aY, rightW - 24f, 18f), "BOSS ABILITIES & CAST TIMERS", headerStyle);
                aY += 22f;

                for (int a = 0; a < curEnc.abilities.Count; a++)
                {
                    var ab = curEnc.abilities[a];
                    Rect aCard = new Rect(6f, aY, rightW - 28f, 52f);
                    GUI.DrawTexture(aCard, cardBgTex);

                    Color borderCol = ab.isInterruptible ? new Color(0.95f, 0.70f, 0.25f, 0.9f) : new Color(0.4f, 0.6f, 0.85f, 0.8f);
                    GUI.DrawTexture(new Rect(aCard.x, aCard.y, 3f, aCard.height), MakeTex(1, 1, borderCol));

                    GUI.Label(new Rect(aCard.x + 8f, aCard.y + 4f, 22f, 22f), ab.icon, headerStyle);
                    GUI.Label(new Rect(aCard.x + 32f, aCard.y + 4f, 220f, 18f), ab.name, abilityTitleStyle);

                    string badge = ab.isInterruptible ? "⚡ INTERRUPTIBLE [F]" : "🛡 SHIELDED";
                    Color badgeCol = ab.isInterruptible ? new Color(0.95f, 0.75f, 0.25f) : new Color(0.6f, 0.75f, 0.95f);
                    var bStyle = new GUIStyle(smallStyle);
                    bStyle.normal.textColor = badgeCol;
                    GUI.Label(new Rect(aCard.xMax - 145f, aCard.y + 4f, 140f, 18f), badge, bStyle);

                    GUI.Label(new Rect(aCard.x + 32f, aCard.y + 22f, aCard.width - 36f, 28f), ab.description, textStyle);

                    aY += 58f;
                }
            }
            else
            {
                // LOOT TABLE
                GUI.Label(new Rect(8f, 4f, rightW - 24f, 18f), "GUARANTEED & EPIC LOOT DROPS", headerStyle);
                float lY = 26f;

                for (int l = 0; l < curEnc.lootTable.Count; l++)
                {
                    var drop = curEnc.lootTable[l];
                    var item = ItemCatalog.Get(drop.itemId);
                    if (item == null) continue;

                    Rect lCard = new Rect(6f, lY, rightW - 28f, 48f);
                    GUI.DrawTexture(lCard, cardBgTex);

                    // Left colored accent line matching item rarity
                    GUI.DrawTexture(new Rect(lCard.x, lCard.y, 3.5f, lCard.height), MakeTex(1, 1, item.color));

                    string slotIcon = item.slot == GearSlot.Weapon ? "⚔" : item.slot == GearSlot.Armor ? "🛡" : "💍";
                    GUI.Label(new Rect(lCard.x + 10f, lCard.y + 6f, 24f, 24f), slotIcon, headerStyle);

                    var nameColStyle = new GUIStyle(abilityTitleStyle);
                    nameColStyle.normal.textColor = item.color;
                    GUI.Label(new Rect(lCard.x + 38f, lCard.y + 5f, 240f, 18f), item.name, nameColStyle);

                    GUI.Label(new Rect(lCard.x + 38f, lCard.y + 24f, 260f, 18f), item.Stats, textStyle);

                    // Drop chance badge
                    GUI.Label(new Rect(lCard.xMax - 85f, lCard.y + 14f, 80f, 20f), drop.dropChance, headerStyle);

                    lY += 54f;
                }
            }

            GUI.EndScrollView();
        }

        void InitStyles()
        {
            if (headerStyle != null) return;

            darkBgTex = MakeTex(1, 1, new Color(0.06f, 0.08f, 0.11f, 0.96f));
            cardBgTex = MakeTex(1, 1, new Color(0.12f, 0.14f, 0.19f, 0.95f));
            borderTex = MakeTex(1, 1, new Color(0.40f, 0.35f, 0.20f, 1f));
            goldTex = MakeTex(1, 1, new Color(0.95f, 0.85f, 0.35f, 1f));
            tabActiveTex = MakeTex(1, 1, new Color(0.20f, 0.23f, 0.30f, 0.98f));
            tabInactiveTex = MakeTex(1, 1, new Color(0.09f, 0.11f, 0.15f, 0.85f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.35f);

            subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal
            };
            subHeaderStyle.normal.textColor = new Color(0.80f, 0.80f, 0.85f);

            bossTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            bossTitleStyle.normal.textColor = Color.white;

            tabStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            tabStyle.normal.textColor = Color.white;

            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            textStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);

            abilityTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };
            abilityTitleStyle.normal.textColor = Color.white;

            roleTipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            roleTipStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            smallStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
