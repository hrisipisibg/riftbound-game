using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        public class AchievementDef
        {
            public string id;
            public string title;
            public string category; // "Exploration", "Dungeons & Bosses", "Character", "Professions", "Feats"
            public string description;
            public string rewardTitle;
            public int points;
            public string icon;
            public Func<PlayerController, bool> isComplete;
            public Func<PlayerController, string> getProgressText;
            public Func<PlayerController, float> getProgressRatio;
        }

        readonly List<AchievementDef> allAchievements = new List<AchievementDef>();
        readonly HashSet<string> unlockedIds = new HashSet<string>();

        public IReadOnlyList<AchievementDef> AllAchievements
        {
            get
            {
                if (allAchievements.Count == 0) RegisterAchievements();
                return allAchievements;
            }
        }
        public int TotalPoints { get; private set; }

        // Banner Toast state
        bool bannerActive = false;
        float bannerDismissTime = 0f;
        AchievementDef bannerAch;

        // Window state
        int selectedCategoryTab = 0; // 0: All, 1: Exploration, 2: Dungeons & Bosses, 3: Character, 4: Professions & Feats
        Vector2 scrollPos;
        PlayerController player;

        GUIStyle headerStyle, catBtnStyle, achTitleStyle, achDescStyle, badgeStyle, progressStyle;
        Texture2D barBgTex, barFillTex, goldBorderTex, darkBgTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            RegisterAchievements();
        }

        void Start()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
        }

        void RegisterAchievements()
        {
            allAchievements.Clear();

            // ==========================================
            // 1. EXPLORATION
            // ==========================================
            allAchievements.Add(new AchievementDef
            {
                id = "exp_amber_vale",
                title = "Scout of Amber Vale",
                category = "Exploration",
                description = "Scout the ancient frontier: Willow Crossing, Greenhollow Post, and the Ancient Grove.",
                rewardTitle = "the Explorer",
                points = 10,
                icon = "🗺",
                isComplete = p => p && p.LandmarksFound >= 3,
                getProgressText = p => p ? $"{p.LandmarksFound} / 3 Landmarks Scouted" : "0 / 3",
                getProgressRatio = p => p ? Mathf.Clamp01(p.LandmarksFound / 3f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "exp_highspire",
                title = "Bastion of the Lion",
                category = "Exploration",
                description = "Visit Highspire Keep, the soaring capital fortress of the Alliance.",
                rewardTitle = "",
                points = 10,
                icon = "🏰",
                isComplete = p => p && p.transform.position.x < -30f && p.transform.position.z < 0f,
                getProgressText = p => (p && p.transform.position.x < -30f && p.transform.position.z < 0f) ? "Discovered" : "Undiscovered",
                getProgressRatio = p => (p && p.transform.position.x < -30f && p.transform.position.z < 0f) ? 1f : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "exp_gorkresh",
                title = "Fortress of the Ashen Pact",
                category = "Exploration",
                description = "Venture into Gor'Kresh Stronghold, the iron stronghold of the Horde.",
                rewardTitle = "",
                points = 10,
                icon = "⛩",
                isComplete = p => p && p.transform.position.x > 60f && p.transform.position.z > 210f,
                getProgressText = p => (p && p.transform.position.x > 60f && p.transform.position.z > 210f) ? "Discovered" : "Undiscovered",
                getProgressRatio = p => (p && p.transform.position.x > 60f && p.transform.position.z > 210f) ? 1f : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "exp_wyrmcrest",
                title = "Summit of the Dragon",
                category = "Exploration",
                description = "Ascend the snowy crags of Mount Wyrmcrest above 40 meters altitude.",
                rewardTitle = "Skywarden",
                points = 15,
                icon = "🌋",
                isComplete = p => p && p.transform.position.y >= 38f && p.transform.position.z > 190f,
                getProgressText = p => p ? $"Altitude: {Mathf.Max(0, (int)p.transform.position.y)} / 40m" : "0 / 40m",
                getProgressRatio = p => p ? Mathf.Clamp01(p.transform.position.y / 40f) : 0f
            });

            // ==========================================
            // 2. DUNGEONS & BOSSES
            // ==========================================
            allAchievements.Add(new AchievementDef
            {
                id = "boss_crypt_warden",
                title = "Awaken the Dead",
                category = "Dungeons & Bosses",
                description = "Activate the three ancient runestones and defeat the Crypt Warden.",
                rewardTitle = "the Undaunted",
                points = 15,
                icon = "💀",
                isComplete = p => p && (p.HasDefeated("crypt-warden") || p.RunesFound >= 3),
                getProgressText = p => p ? $"{p.RunesFound} / 3 Runestones Awakened" : "0 / 3",
                getProgressRatio = p => p ? Mathf.Clamp01(p.RunesFound / 3f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "boss_scarlet_crypts",
                title = "Purge the Scarlet Crypts",
                category = "Dungeons & Bosses",
                description = "Enter the subterranean cathedral and vanquish High Inquisitor Malakar.",
                rewardTitle = "Scourge Hunter",
                points = 20,
                icon = "⚔",
                isComplete = p => p && (p.OwnsItem("scarlet-blade") || p.OwnsItem("scarlet-aegis")),
                getProgressText = p => (p && (p.OwnsItem("scarlet-blade") || p.OwnsItem("scarlet-aegis"))) ? "Completed" : "Cathedral Gate (-55, 215)",
                getProgressRatio = p => (p && (p.OwnsItem("scarlet-blade") || p.OwnsItem("scarlet-aegis"))) ? 1f : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "boss_gorgonax",
                title = "Titanbreaker",
                category = "Dungeons & Bosses",
                description = "Conquer Gorgonax the Worldbreaker in the Redridge Impact Crater.",
                rewardTitle = "Titanbreaker",
                points = 25,
                icon = "👹",
                isComplete = p => p && (p.HasDefeated("gorgonax") || p.Kills >= 8),
                getProgressText = p => p ? $"{Mathf.Min(p.Kills, 8)} / 8 Conquest Progress" : "0 / 8",
                getProgressRatio = p => p ? Mathf.Clamp01(p.Kills / 8f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "boss_vermithrax",
                title = "Sovereign of the Flames",
                category = "Dungeons & Bosses",
                description = "Slay Vermithrax the Ember Sovereign atop the peak of Wyrmcrest Summit.",
                rewardTitle = "Dragonslayer",
                points = 30,
                icon = "🐲",
                isComplete = p => p && (p.OwnsItem("wyrmtooth-edge") || p.OwnsItem("ember-drake-scale")),
                getProgressText = p => (p && (p.OwnsItem("wyrmtooth-edge") || p.OwnsItem("ember-drake-scale"))) ? "Dragon Hoard Claimed" : "Summit Dais (0, 48, 225)",
                getProgressRatio = p => (p && (p.OwnsItem("wyrmtooth-edge") || p.OwnsItem("ember-drake-scale"))) ? 1f : 0f
            });

            // ==========================================
            // 3. CHARACTER & COMBAT
            // ==========================================
            allAchievements.Add(new AchievementDef
            {
                id = "char_journey_begins",
                title = "The Journey Begins",
                category = "Character",
                description = "Attain Level 2 and embark upon your journey through Aethelgard.",
                rewardTitle = "",
                points = 5,
                icon = "⭐",
                isComplete = p => p && p.Level >= 2,
                getProgressText = p => p ? $"Level {p.Level} / 2" : "Lv 1 / 2",
                getProgressRatio = p => p ? Mathf.Clamp01(p.Level / 2f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "char_champion",
                title = "Champion of the Realm",
                category = "Character",
                description = "Advance your character prowess and battle experience to Level 5.",
                rewardTitle = "Champion",
                points = 15,
                icon = "👑",
                isComplete = p => p && p.Level >= 5,
                getProgressText = p => p ? $"Level {p.Level} / 5" : "Lv 1 / 5",
                getProgressRatio = p => p ? Mathf.Clamp01(p.Level / 5f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "char_road_cleanse",
                title = "Cleansing the Roads",
                category = "Character",
                description = "Defeat 5 forest raiders, crag orcs, or hostile skeletons in combat.",
                rewardTitle = "",
                points = 10,
                icon = "🗡",
                isComplete = p => p && p.Kills >= 5,
                getProgressText = p => p ? $"{Mathf.Min(p.Kills, 5)} / 5 Foes Defeated" : "0 / 5",
                getProgressRatio = p => p ? Mathf.Clamp01(p.Kills / 5f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "char_purse",
                title = "Gilded Pockets",
                category = "Character",
                description = "Accumulate 40 or more Gold coins in your adventure backpack.",
                rewardTitle = "",
                points = 10,
                icon = "💰",
                isComplete = p => p && p.Gold >= 40,
                getProgressText = p => p ? $"{p.Gold} / 40 Gold" : "0 / 40",
                getProgressRatio = p => p ? Mathf.Clamp01(p.Gold / 40f) : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "char_epic_gear",
                title = "Armed for Glory",
                category = "Character",
                description = "Acquire and equip any Epic or Legendary tier weapon, armor, or trinket.",
                rewardTitle = "",
                points = 20,
                icon = "🛡",
                isComplete = p => p && (p.OwnsItem("scarlet-blade") || p.OwnsItem("scarlet-aegis") || p.OwnsItem("wyrmtooth-edge") || p.OwnsItem("ember-drake-scale")),
                getProgressText = p => (p && (p.OwnsItem("scarlet-blade") || p.OwnsItem("scarlet-aegis") || p.OwnsItem("wyrmtooth-edge") || p.OwnsItem("ember-drake-scale"))) ? "Acquired" : "Epic / Legendary Gear Needed",
                getProgressRatio = p => (p && (p.OwnsItem("scarlet-blade") || p.OwnsItem("scarlet-aegis") || p.OwnsItem("wyrmtooth-edge") || p.OwnsItem("ember-drake-scale"))) ? 1f : 0f
            });

            // ==========================================
            // 4. PROFESSIONS & FEATS
            // ==========================================
            allAchievements.Add(new AchievementDef
            {
                id = "prof_craftsman",
                title = "Craftsman's Calling",
                category = "Professions",
                description = "Advance a crafting profession: Alchemy, Blacksmithing, or Engineering.",
                rewardTitle = "Master Artisan",
                points = 10,
                icon = "⚒",
                isComplete = p => ProfessionSystem.Instance != null && (ProfessionSystem.Instance.alchemySkill > 0 || ProfessionSystem.Instance.blacksmithingSkill > 0 || ProfessionSystem.Instance.engineeringSkill > 0),
                getProgressText = p => "Skill Active",
                getProgressRatio = p => 1f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "feat_skies",
                title = "Skies of Aethelgard",
                category = "Feats",
                description = "Take flight on the Celestial Winged Steed and master 310% speed flight.",
                rewardTitle = "Skywarden",
                points = 25,
                icon = "🦅",
                isComplete = p => p && p.Mounted && p.SelectedMount != null && p.SelectedMount.isFlying,
                getProgressText = p => (p && p.Mounted && p.SelectedMount != null && p.SelectedMount.isFlying) ? "Active Flight" : "Summon Flying Mount [Z]",
                getProgressRatio = p => (p && p.Mounted && p.SelectedMount != null && p.SelectedMount.isFlying) ? 1f : 0f
            });

            allAchievements.Add(new AchievementDef
            {
                id = "feat_dual_spec",
                title = "Master of Roles",
                category = "Feats",
                description = "Activate your secondary specialization using the Dual Spec Talent panel [N].",
                rewardTitle = "",
                points = 15,
                icon = "🔄",
                isComplete = p => TalentSystem.ActiveSpecIndex > 0,
                getProgressText = p => TalentSystem.ActiveSpecIndex > 0 ? "Dual Spec Active" : "Activate via Talent Window [N]",
                getProgressRatio = p => TalentSystem.ActiveSpecIndex > 0 ? 1f : 0f
            });
        }

        void Update()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || FrontendMenu.IsOpen) return;

            // Live check for achievement completions
            for (int i = 0; i < allAchievements.Count; i++)
            {
                var ach = allAchievements[i];
                if (!unlockedIds.Contains(ach.id) && ach.isComplete != null && ach.isComplete(player))
                {
                    UnlockAchievement(ach);
                }
            }

            // Dismiss banner after timeout
            if (bannerActive && Time.time >= bannerDismissTime)
            {
                bannerActive = false;
            }
        }

        public void UnlockAchievement(AchievementDef ach)
        {
            if (ach == null || unlockedIds.Contains(ach.id)) return;

            unlockedIds.Add(ach.id);
            TotalPoints += ach.points;

            // Trigger golden toast banner
            bannerActive = true;
            bannerDismissTime = Time.time + 4.5f;
            bannerAch = ach;

            // Unlock reward title if present
            if (!string.IsNullOrEmpty(ach.rewardTitle))
            {
                PlayerTitleSystem.SetActiveTitle(ach.rewardTitle);
                ChatSystem.LogSystem($"<color=#ffd700>★ Title Unlocked: <{ach.rewardTitle}>!</color>");
            }

            AudioEffects.PlayQuestComplete();
            ChatSystem.LogSystem($"<color=#ffd700>🏆 Achievement Earned: [{ach.title}] (+{ach.points} pts)!</color>");
        }

        public bool IsUnlocked(string id) => unlockedIds.Contains(id);

        void InitStyles()
        {
            if (headerStyle != null) return;

            barBgTex = MakeTex(1, 1, new Color(0.12f, 0.14f, 0.18f, 0.95f));
            barFillTex = MakeTex(1, 1, new Color(0.18f, 0.72f, 0.28f, 1f));
            goldBorderTex = MakeTex(1, 1, new Color(0.85f, 0.72f, 0.28f, 1f));
            darkBgTex = MakeTex(1, 1, new Color(0.06f, 0.08f, 0.11f, 0.97f));

            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.95f, 0.85f, 0.40f);

            catBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 8, 4, 4)
            };

            achTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            achTitleStyle.normal.textColor = new Color(0.95f, 0.88f, 0.45f);

            achDescStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true
            };
            achDescStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            badgeStyle.normal.textColor = Color.white;

            progressStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            progressStyle.normal.textColor = new Color(0.75f, 0.85f, 1f);
        }

        Texture2D MakeTex(int w, int h, Color col)
        {
            Color[] pix = new Color[w * h];
            for (int i = 0; i < pix.Length; i++) pix[i] = col;
            Texture2D result = new Texture2D(w, h);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        public void DrawToastGUI(float screenW, float screenH)
        {
            if (!bannerActive || bannerAch == null || FrontendMenu.IsOpen) return;

            InitStyles();

            float bannerW = 380f;
            float bannerH = 56f;
            float bx = screenW / 2f - bannerW / 2f;
            float by = 60f;

            // Outer gold border
            GUI.DrawTexture(new Rect(bx - 1f, by - 1f, bannerW + 2f, bannerH + 2f), goldBorderTex);
            // Dark gothic panel
            GUI.DrawTexture(new Rect(bx, by, bannerW, bannerH), darkBgTex);

            // Shield / Trophy Badge
            GUI.DrawTexture(new Rect(bx + 8f, by + 8f, 40f, 40f), goldBorderTex);
            GUI.DrawTexture(new Rect(bx + 10f, by + 10f, 36f, 36f), darkBgTex);
            GUI.Label(new Rect(bx + 10f, by + 10f, 36f, 36f), bannerAch.icon, badgeStyle);

            // Header line
            var tStyle = new GUIStyle(headerStyle) { fontSize = 11 };
            tStyle.normal.textColor = new Color(1f, 0.85f, 0.25f);
            GUI.Label(new Rect(bx + 54f, by + 6f, bannerW - 60f, 16f), $"Achievement unlocked  +{bannerAch.points}", tStyle);

            // Title
            GUI.Label(new Rect(bx + 54f, by + 22f, bannerW - 60f, 18f), bannerAch.title, achTitleStyle);

            // Subtitle / reward
            var subStyle = new GUIStyle(achDescStyle) { fontSize = 10, fontStyle = FontStyle.Italic };
            subStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);
            string sub = !string.IsNullOrEmpty(bannerAch.rewardTitle) ? $"Reward Title: <{bannerAch.rewardTitle}>" : bannerAch.category;
            GUI.Label(new Rect(bx + 54f, by + 38f, bannerW - 60f, 14f), sub, subStyle);
        }

        public void DrawAchievementsWindow(float screenW, float screenH)
        {
            InitStyles();

            float windowW = 720f, windowH = 520f;
            float x = screenW / 2f - windowW / 2f, y = screenH / 2f - windowH / 2f;

            // Outer dark gothic panel with gold border
            GUI.DrawTexture(new Rect(x - 4f, y - 4f, windowW + 8f, windowH + 8f), goldBorderTex);
            GUI.DrawTexture(new Rect(x, y, windowW, windowH), darkBgTex);

            // Header Title Bar
            var topBar = new Rect(x, y, windowW, 38f);
            GUI.DrawTexture(topBar, darkBgTex);
            GUI.DrawTexture(new Rect(x, y + 37f, windowW, 1.5f), goldBorderTex);

            string titleText = $"ACHIEVEMENTS & MILESTONES — 🏆 TOTAL: {TotalPoints} POINTS";
            GUI.Label(new Rect(x + 18f, y + 8f, windowW - 60f, 22f), titleText, headerStyle);

            // Close button [✕]
            var microBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold };
            if (GUI.Button(new Rect(x + windowW - 32f, y + 7f, 24f, 24f), "✕", microBtn))
            {
                if (player) player.AchievementOpen = false;
                AudioEffects.PlayClick();
            }

            // Left Category Sidebar (180px)
            float leftW = 180f;
            float contentY = y + 44f;
            float contentH = windowH - 52f;

            GUI.DrawTexture(new Rect(x + 10f, contentY, leftW, contentH), darkBgTex);
            GUI.DrawTexture(new Rect(x + 10f + leftW, contentY, 1.5f, contentH), goldBorderTex);

            string[] categories = { "★ All Summary", "🗺 Exploration", "💀 Dungeons & Bosses", "⚔ Character & Combat", "⚒ Professions & Feats" };
            float btnH = 34f;
            for (int i = 0; i < categories.Length; i++)
            {
                bool isSel = selectedCategoryTab == i;
                var oldCol = GUI.backgroundColor;
                GUI.backgroundColor = isSel ? new Color(0.85f, 0.70f, 0.25f) : new Color(0.2f, 0.22f, 0.28f);
                if (GUI.Button(new Rect(x + 14f, contentY + 8f + i * (btnH + 4f), leftW - 8f, btnH), categories[i], catBtnStyle))
                {
                    selectedCategoryTab = i;
                    AudioEffects.PlayClick();
                }
                GUI.backgroundColor = oldCol;
            }

            // Right Scrollable Content Pane
            float rightX = x + leftW + 18f;
            float rightW = windowW - leftW - 28f;

            var filtered = GetFilteredAchievements();

            GUILayout.BeginArea(new Rect(rightX, contentY + 4f, rightW, contentH - 8f));
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Width(rightW), GUILayout.Height(contentH - 8f));

            for (int i = 0; i < filtered.Count; i++)
            {
                DrawAchievementCard(filtered[i], rightW - 24f);
                GUILayout.Space(6f);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        List<AchievementDef> GetFilteredAchievements()
        {
            if (selectedCategoryTab == 0) return allAchievements;
            string matchCat = selectedCategoryTab == 1 ? "Exploration" :
                              selectedCategoryTab == 2 ? "Dungeons & Bosses" :
                              selectedCategoryTab == 3 ? "Character" : "Professions";

            var list = new List<AchievementDef>();
            for (int i = 0; i < allAchievements.Count; i++)
            {
                if (selectedCategoryTab == 4)
                {
                    if (allAchievements[i].category == "Professions" || allAchievements[i].category == "Feats")
                        list.Add(allAchievements[i]);
                }
                else if (allAchievements[i].category == matchCat)
                {
                    list.Add(allAchievements[i]);
                }
            }
            return list;
        }

        void DrawAchievementCard(AchievementDef ach, float cardW)
        {
            bool isUnlocked = unlockedIds.Contains(ach.id);

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            // Shield Icon & Points Badge (46x46px)
            var oldBg = GUI.backgroundColor;
            GUI.backgroundColor = isUnlocked ? new Color(0.2f, 0.8f, 0.3f) : new Color(0.35f, 0.38f, 0.45f);
            GUILayout.Box($"{ach.icon}\n{ach.points}p", GUILayout.Width(46f), GUILayout.Height(46f));
            GUI.backgroundColor = oldBg;

            GUILayout.Space(8f);

            // Title & Info
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label(ach.title, achTitleStyle);

            if (isUnlocked)
            {
                var compStyle = new GUIStyle(badgeStyle) { alignment = TextAnchor.MiddleRight };
                compStyle.normal.textColor = new Color(0.25f, 0.95f, 0.35f);
                GUILayout.Label("✔ COMPLETED", compStyle);
            }
            else
            {
                string prog = ach.getProgressText != null ? ach.getProgressText(player) : "In Progress";
                GUILayout.Label(prog, progressStyle);
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(ach.description, achDescStyle);

            if (!string.IsNullOrEmpty(ach.rewardTitle))
            {
                var rewStyle = new GUIStyle(achDescStyle) { fontStyle = FontStyle.Italic };
                rewStyle.normal.textColor = new Color(0.65f, 0.85f, 1f);
                GUILayout.Label($"★ Reward Title: <{ach.rewardTitle}>", rewStyle);
            }

            // Progress Bar
            float ratio = isUnlocked ? 1f : (ach.getProgressRatio != null ? ach.getProgressRatio(player) : 0f);
            var barRect = GUILayoutUtility.GetRect(cardW, 6f);
            GUI.DrawTexture(barRect, barBgTex);
            if (ratio > 0f)
            {
                var fillCol = isUnlocked ? new Color(0.18f, 0.85f, 0.30f) : new Color(0.85f, 0.70f, 0.25f);
                var prevCol = GUI.color;
                GUI.color = fillCol;
                GUI.DrawTexture(new Rect(barRect.x, barRect.y, barRect.width * ratio, barRect.height), barFillTex);
                GUI.color = prevCol;
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
