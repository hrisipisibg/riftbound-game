using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class ReputationSystem : MonoBehaviour
    {
        public static ReputationSystem Instance { get; private set; }

        public enum StandingLevel
        {
            Hated,
            Hostile,
            Unfriendly,
            Neutral,
            Friendly,
            Honored,
            Revered,
            Exalted
        }

        [Serializable]
        public class FactionDef
        {
            public string id;
            public string name;
            public string category; // "Alliance", "Horde", "Neutral", "Dungeons & Raids"
            public string description;
            public string location;
            public string leader;
            public string icon;
            public int rawReputation;
            public string rewardTitle;
            public bool isAtWar;
            public bool showAsBar;

            public StandingLevel Level
            {
                get
                {
                    if (rawReputation < -6000) return StandingLevel.Hated;
                    if (rawReputation < -3000) return StandingLevel.Hostile;
                    if (rawReputation < 0) return StandingLevel.Unfriendly;
                    if (rawReputation < 3000) return StandingLevel.Neutral;
                    if (rawReputation < 9000) return StandingLevel.Friendly;
                    if (rawReputation < 21000) return StandingLevel.Honored;
                    if (rawReputation < 42000) return StandingLevel.Revered;
                    return StandingLevel.Exalted;
                }
            }

            public string StandingName => Level.ToString();

            public Color StandingColor
            {
                get
                {
                    switch (Level)
                    {
                        case StandingLevel.Hated: return new Color(0.65f, 0.05f, 0.05f);
                        case StandingLevel.Hostile: return new Color(0.85f, 0.15f, 0.15f);
                        case StandingLevel.Unfriendly: return new Color(0.92f, 0.50f, 0.15f);
                        case StandingLevel.Neutral: return new Color(0.95f, 0.82f, 0.20f);
                        case StandingLevel.Friendly: return new Color(0.18f, 0.78f, 0.25f);
                        case StandingLevel.Honored: return new Color(0.12f, 0.75f, 0.65f);
                        case StandingLevel.Revered: return new Color(0.20f, 0.60f, 0.95f);
                        default: return new Color(0.68f, 0.35f, 0.95f);
                    }
                }
            }

            public int CurrentRangeMin
            {
                get
                {
                    switch (Level)
                    {
                        case StandingLevel.Hated: return -36000;
                        case StandingLevel.Hostile: return -6000;
                        case StandingLevel.Unfriendly: return -3000;
                        case StandingLevel.Neutral: return 0;
                        case StandingLevel.Friendly: return 3000;
                        case StandingLevel.Honored: return 9000;
                        case StandingLevel.Revered: return 21000;
                        default: return 42000;
                    }
                }
            }

            public int CurrentRangeMax
            {
                get
                {
                    switch (Level)
                    {
                        case StandingLevel.Hated: return -6000;
                        case StandingLevel.Hostile: return -3000;
                        case StandingLevel.Unfriendly: return 0;
                        case StandingLevel.Neutral: return 3000;
                        case StandingLevel.Friendly: return 9000;
                        case StandingLevel.Honored: return 21000;
                        case StandingLevel.Revered: return 42000;
                        default: return 43000;
                    }
                }
            }

            public int CurrentProgress => Mathf.Clamp(rawReputation - CurrentRangeMin, 0, CurrentRangeMax - CurrentRangeMin);
            public int RangeTotal => CurrentRangeMax - CurrentRangeMin;
            public float ProgressRatio => RangeTotal > 0 ? (float)CurrentProgress / RangeTotal : 1f;
        }

        readonly List<FactionDef> factions = new List<FactionDef>();
        readonly HashSet<string> categories = new HashSet<string>();
        readonly Dictionary<string, bool> categoryFoldouts = new Dictionary<string, bool>();

        public IReadOnlyList<FactionDef> AllFactions
        {
            get
            {
                if (factions.Count == 0) RegisterFactions();
                return factions;
            }
        }

        Vector2 scrollPos;
        PlayerController player;

        GUIStyle headerStyle, catHeaderStyle, nameStyle, standingStyle, tipStyle, smallStyle;
        Texture2D darkBgTex, barBgTex, fillTex, borderTex, catBgTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            RegisterFactions();
        }

        void RegisterFactions()
        {
            if (factions.Count > 0) return;

            bool isAlliance = player && (player.CharacterRace == "Human" || player.CharacterRace == "Dwarf" || player.CharacterRace == "Elf");

            // 1. ALLIANCE FORCES
            AddFaction(new FactionDef
            {
                id = "highspire",
                name = "Highspire Vanguard",
                category = "Alliance Forces",
                description = "The exalted royal army and knights defending the shining capital fortress of Highspire.",
                location = "Highspire Keep (-55, -20)",
                leader = "Grand Duke Ronald",
                icon = "🦁",
                rawReputation = isAlliance ? 3400 : 1200,
                rewardTitle = "Vanguard of Highspire"
            });

            AddFaction(new FactionDef
            {
                id = "silverglen",
                name = "Silverglen Sentry",
                category = "Alliance Forces",
                description = "Frontier rangers and scouts safeguarding the eastern groves and Willow Crossing borders.",
                location = "Silverglen Outpost (-15, 45)",
                leader = "Commander Justin",
                icon = "🏹",
                rawReputation = isAlliance ? 4200 : 800,
                rewardTitle = "Silverglen Scout"
            });

            // 2. HORDE DOMINANCE
            AddFaction(new FactionDef
            {
                id = "gorkresh",
                name = "Gor'Kresh Ashen Pact",
                category = "Horde Dominance",
                description = "The mighty iron warhost of the Horde ruling the blackened crags of the Eastern Highlands.",
                location = "Gor'Kresh Stronghold (75, 225)",
                leader = "Overlord Mor'gok",
                icon = "🐺",
                rawReputation = isAlliance ? 600 : 3600,
                rewardTitle = "Warlord of Gor'Kresh"
            });

            AddFaction(new FactionDef
            {
                id = "bloodrock",
                name = "Camp Bloodrock Warriors",
                category = "Horde Dominance",
                description = "Veteran battlemasters and blacksmiths guarding the jagged red cliffs of Redridge Canyons.",
                location = "Camp Bloodrock (50, 210)",
                leader = "Warlord Gorgash",
                icon = "⚒",
                rawReputation = isAlliance ? 400 : 4100,
                rewardTitle = "Bloodrock Champion"
            });

            // 3. NEUTRAL & SCHOLARS
            AddFaction(new FactionDef
            {
                id = "emberwatch",
                name = "Emberwatch Frontier",
                category = "Neutral & Frontier Leagues",
                description = "The frontier sanctuary garrison upholding trade, road security, and mail transit for all travelers.",
                location = "Emberwatch Outpost (0, 10)",
                leader = "Guard Captain Abe",
                icon = "🏰",
                rawReputation = 5200,
                rewardTitle = "Frontier Marshal",
                showAsBar = true
            });

            AddFaction(new FactionDef
            {
                id = "kirin_tor",
                name = "Kirin Tor Conclave",
                category = "Neutral & Frontier Leagues",
                description = "Arcane researchers and herbal alchemists investigating the ancient leylines across Azeboth.",
                location = "Ancient Grove (-10, 85)",
                leader = "Archmage Karen & Alchemist Kelsey",
                icon = "🔮",
                rawReputation = 2400,
                rewardTitle = "Arcanist of the Conclave"
            });

            // 4. DUNGEONS & ADVERSARIES
            AddFaction(new FactionDef
            {
                id = "scarlet_onslaught",
                name = "The Scarlet Onslaught",
                category = "Dungeons & Adversaries",
                description = "Fanatical inquisitors and corrupted crusaders entrenched inside the Scarlet Crypts.",
                location = "Duskwood Cathedral (-55, 215)",
                leader = "High Inquisitor Malakar & Dread Abbot Vael",
                icon = "✝",
                rawReputation = -12000,
                isAtWar = true
            });

            AddFaction(new FactionDef
            {
                id = "wyrmcrest_brood",
                name = "Wyrmcrest Ember Brood",
                category = "Dungeons & Adversaries",
                description = "Ancient pyroclastic drakes roosting atop the magma crags of Mount Wyrmcrest.",
                location = "Wyrmcrest Summit (0, 48, 225)",
                leader = "Vermithrax the Ember Sovereign",
                icon = "🐉",
                rawReputation = -2500,
                rewardTitle = "Brood Sovereign",
                isAtWar = true
            });

            for (int i = 0; i < factions.Count; i++)
            {
                string cat = factions[i].category;
                categories.Add(cat);
                if (!categoryFoldouts.ContainsKey(cat))
                    categoryFoldouts[cat] = true;
            }
        }

        void AddFaction(FactionDef def)
        {
            factions.Add(def);
        }

        public void AddReputation(string factionId, int amount)
        {
            if (factions.Count == 0) RegisterFactions();

            var fac = factions.Find(f => f.id == factionId);
            if (fac == null) return;

            StandingLevel oldLevel = fac.Level;
            fac.rawReputation = Mathf.Clamp(fac.rawReputation + amount, -36000, 42000);
            StandingLevel newLevel = fac.Level;

            string sign = amount >= 0 ? "+" : "";
            Color repCol = fac.StandingColor;

            if (player)
            {
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.2f, $"{sign}{amount} Rep: {fac.name}", repCol, true);
            }

            ChatSystem.LogSystem($"<color=#{ColorUtility.ToHtmlStringRGB(repCol)}>Reputation with [{fac.name}] changed by {sign}{amount} ({fac.StandingName} {fac.CurrentProgress}/{fac.RangeTotal}).</color>");
            AudioEffects.PlayQuestComplete();

            // Standing Promotion Celebration
            if (newLevel > oldLevel)
            {
                ChatSystem.LogSystem($"<color=#ffd700>★ PROMOTION: You are now <b>{newLevel}</b> with [{fac.name}]!</color>");
                if (ZoneBanner.Instance)
                {
                    ZoneBanner.Show($"{fac.name}: {newLevel}!", $"Your standing has risen to {newLevel}.");
                }

                if (newLevel >= StandingLevel.Exalted && !string.IsNullOrEmpty(fac.rewardTitle))
                {
                    PlayerTitleSystem.SetActiveTitle(fac.rewardTitle);
                    ChatSystem.LogSystem($"<color=#ffd700>★ Exalted Title Unlocked: <{fac.rewardTitle}>!</color>");
                }
            }
        }

        public FactionDef GetFaction(string id)
        {
            if (factions.Count == 0) RegisterFactions();
            return factions.Find(f => f.id == id);
        }

        public int GetReputation(string id)
        {
            var fac = GetFaction(id);
            return fac != null ? fac.rawReputation : 0;
        }

        public StandingLevel GetStanding(string id)
        {
            var fac = GetFaction(id);
            return fac != null ? fac.Level : StandingLevel.Neutral;
        }

        public void DrawReputationWindow(float screenW, float screenH)
        {
            if (FrontendMenu.IsOpen) return;
            InitStyles();

            if (!player) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

            float winW = 560f;
            float winH = 490f;
            float winX = screenW / 2f - winW / 2f;
            float winY = screenH / 2f - winH / 2f;

            // Outer Frame Border & Gothic Parchment Backing
            GUI.DrawTexture(new Rect(winX - 3f, winY - 3f, winW + 6f, winH + 6f), borderTex);
            GUI.DrawTexture(new Rect(winX, winY, winW, winH), darkBgTex);

            // Title Header Banner
            GUI.DrawTexture(new Rect(winX, winY, winW, 40f), catBgTex);
            GUI.Label(new Rect(winX + 16f, winY + 8f, winW - 60f, 24f), "⚔ REPUTATION & FACTION STANDINGS [U]", headerStyle);

            if (GUI.Button(new Rect(winX + winW - 32f, winY + 8f, 24f, 24f), "✕", smallStyle))
            {
                if (player) player.ReputationOpen = false;
                AudioEffects.PlayClick();
            }

            // Exalted Counter Badge
            int exaltedCount = 0;
            for (int i = 0; i < factions.Count; i++)
                if (factions[i].Level >= StandingLevel.Exalted) exaltedCount++;

            GUI.Label(new Rect(winX + winW - 190f, winY + 11f, 150f, 20f), $"Exalted Factions: {exaltedCount}", smallStyle);

            // Scrollable Faction Hierarchy
            float scrollW = winW - 32f;
            float scrollH = winH - 60f;
            Rect viewRect = new Rect(winX + 16f, winY + 46f, scrollW, scrollH);

            float totalContentH = 0f;
            foreach (var cat in categories)
            {
                totalContentH += 28f;
                if (categoryFoldouts[cat])
                {
                    int countInCat = factions.FindAll(f => f.category == cat).Count;
                    totalContentH += countInCat * 46f + 4f;
                }
            }

            scrollPos = GUI.BeginScrollView(viewRect, scrollPos, new Rect(0, 0, scrollW - 18f, totalContentH));
            float curY = 0f;

            Vector2 mousePos = Event.current.mousePosition;
            FactionDef hoveredFac = null;
            Rect hoveredRect = Rect.zero;

            foreach (var cat in categories)
            {
                // Category Header Foldout
                bool expanded = categoryFoldouts[cat];
                string foldIcon = expanded ? "▼ " : "► ";

                GUI.DrawTexture(new Rect(0, curY, scrollW - 20f, 24f), catBgTex);
                if (GUI.Button(new Rect(4f, curY, scrollW - 28f, 24f), foldIcon + cat.ToUpperInvariant(), catHeaderStyle))
                {
                    categoryFoldouts[cat] = !expanded;
                    AudioEffects.PlayClick();
                }
                curY += 28f;

                if (expanded)
                {
                    var catFactions = factions.FindAll(f => f.category == cat);
                    for (int i = 0; i < catFactions.Count; i++)
                    {
                        var fac = catFactions[i];
                        float rowY = curY;
                        float rowH = 42f;
                        Rect rowRect = new Rect(0, rowY, scrollW - 20f, rowH);

                        // Backing
                        GUI.DrawTexture(rowRect, darkBgTex);
                        GUI.DrawTexture(new Rect(0, rowY + rowH - 1f, scrollW - 20f, 1f), borderTex);

                        // Crest Icon & Name
                        GUI.Label(new Rect(6f, rowY + 3f, 28f, 28f), fac.icon, headerStyle);
                        GUI.Label(new Rect(38f, rowY + 4f, 220f, 20f), fac.name, nameStyle);

                        // At War status flag
                        if (fac.isAtWar)
                        {
                            GUI.Label(new Rect(38f, rowY + 22f, 90f, 16f), "[AT WAR]", smallStyle);
                        }
                        else
                        {
                            GUI.Label(new Rect(38f, rowY + 22f, 180f, 16f), fac.location, smallStyle);
                        }

                        // Standing Progress Bar
                        float barW = 210f;
                        float barH = 18f;
                        float barX = scrollW - 20f - barW - 10f;
                        float barY = rowY + 12f;

                        // Dark Bar Well
                        GUI.DrawTexture(new Rect(barX - 1f, barY - 1f, barW + 2f, barH + 2f), borderTex);
                        GUI.DrawTexture(new Rect(barX, barY, barW, barH), barBgTex);

                        // Colored Standing Fill
                        var oldCol = GUI.color;
                        GUI.color = fac.StandingColor;
                        GUI.DrawTexture(new Rect(barX, barY, barW * fac.ProgressRatio, barH), fillTex);
                        GUI.color = oldCol;

                        // Standing Label Overlay (e.g. "Honored 3,450 / 12,000")
                        string repText = fac.Level >= StandingLevel.Exalted ? "Exalted (MAX)" : $"{fac.StandingName} ({fac.CurrentProgress:#,0} / {fac.RangeTotal:#,0})";
                        GUI.Label(new Rect(barX, barY - 1f, barW, barH), repText, standingStyle);

                        // Check hover for tooltip
                        Rect localRow = new Rect(viewRect.x, viewRect.y + rowY - scrollPos.y, rowRect.width, rowRect.height);
                        if (localRow.Contains(Event.current.mousePosition) && viewRect.Contains(Event.current.mousePosition))
                        {
                            hoveredFac = fac;
                            hoveredRect = localRow;
                        }

                        curY += 46f;
                    }
                }
            }

            GUI.EndScrollView();

            // Hover Tooltip
            if (hoveredFac != null)
            {
                float tipW = 270f;
                float tipH = 110f;
                float tipX = Mathf.Clamp(hoveredRect.x + 30f, 10f, screenW - tipW - 10f);
                float tipY = Mathf.Clamp(hoveredRect.y + 20f, 10f, screenH - tipH - 10f);

                GUI.DrawTexture(new Rect(tipX - 2f, tipY - 2f, tipW + 4f, tipH + 4f), borderTex);
                GUI.DrawTexture(new Rect(tipX, tipY, tipW, tipH), darkBgTex);

                GUI.Label(new Rect(tipX + 8f, tipY + 6f, tipW - 16f, 20f), $"{hoveredFac.icon} {hoveredFac.name}", headerStyle);
                GUI.Label(new Rect(tipX + 8f, tipY + 26f, tipW - 16f, 16f), $"Leader: {hoveredFac.leader}", smallStyle);
                GUI.Label(new Rect(tipX + 8f, tipY + 44f, tipW - 16f, 36f), hoveredFac.description, tipStyle);

                string perk = !string.IsNullOrEmpty(hoveredFac.rewardTitle) ? $"Exalted Title: <{hoveredFac.rewardTitle}>" : "Discounts at regional vendors.";
                GUI.Label(new Rect(tipX + 8f, tipY + 82f, tipW - 16f, 20f), $"★ Standing Perk: {perk}", smallStyle);
            }
        }

        void InitStyles()
        {
            if (headerStyle != null) return;

            darkBgTex = MakeTex(1, 1, new Color(0.06f, 0.08f, 0.11f, 0.96f));
            barBgTex = MakeTex(1, 1, new Color(0.12f, 0.14f, 0.18f, 0.95f));
            fillTex = MakeTex(1, 1, Color.white);
            borderTex = MakeTex(1, 1, new Color(0.40f, 0.35f, 0.20f, 1f));
            catBgTex = MakeTex(1, 1, new Color(0.14f, 0.16f, 0.22f, 0.95f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.35f);

            catHeaderStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            catHeaderStyle.normal.textColor = new Color(0.85f, 0.85f, 0.90f);

            nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            nameStyle.normal.textColor = Color.white;

            standingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            standingStyle.normal.textColor = Color.white;

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft
            };
            smallStyle.normal.textColor = new Color(0.75f, 0.75f, 0.75f);

            tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            tipStyle.normal.textColor = new Color(0.88f, 0.88f, 0.88f);
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
