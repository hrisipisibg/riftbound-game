using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public enum CraftCategory
    {
        Alchemy,
        Blacksmithing,
        Engineering
    }

    [Serializable]
    public class CraftRecipe
    {
        public string id;
        public string displayName;
        public CraftCategory category;
        public int requiredSkill;
        public string description;
        public string outputItemId;
        public int outputCount;
        [NonSerialized] public (string matId, int count)[] reagents;
        public Color rarityColor;
    }

    public class ProfessionSystem : MonoBehaviour
    {
        public static ProfessionSystem Instance { get; private set; }

        public static bool IsOpen { get; set; } = false;

        public int alchemySkill = 15;
        public int blacksmithingSkill = 12;
        public int engineeringSkill = 8;
        public const int MaxSkill = 75; // Apprentice tier

        // Reagents inventory
        [NonSerialized] public Dictionary<string, int> reagents = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Peacebloom", 6 },
            { "Silverleaf", 4 },
            { "Kingsblood", 2 },
            { "Copper Ore", 6 },
            { "Tin Ore", 4 },
            { "Silver Ore", 2 }
        };

        CraftCategory selectedCategory = CraftCategory.Alchemy;
        CraftRecipe selectedRecipe = null;
        Vector2 recipeScroll;

        PlayerController player;
        List<CraftRecipe> allRecipes = new List<CraftRecipe>();
        public IReadOnlyList<CraftRecipe> Recipes => allRecipes;

        GUIStyle windowTitle, categoryTab, recipeItemStyle, sectionHeading, descStyle, craftBtnStyle, reagentStyle;
        Texture2D bgTex, borderTex, headerTex, selectTex, goldBarTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            BuildRecipeDatabase();
            if (allRecipes.Count > 0) selectedRecipe = allRecipes[0];
        }

        void BuildRecipeDatabase()
        {
            allRecipes.Clear();

            // ALCHEMY RECIPES
            allRecipes.Add(new CraftRecipe
            {
                id = "minor-healing-potion",
                displayName = "Minor Healing Potion",
                category = CraftCategory.Alchemy,
                requiredSkill = 1,
                description = "Restores 55 Health immediately. Essential survival draught for all adventurers.",
                outputItemId = "potion",
                outputCount = 2,
                reagents = new[] { ("Peacebloom", 2), ("Silverleaf", 1) },
                rarityColor = new Color(0.9f, 0.9f, 0.9f)
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "elixir-lions-strength",
                displayName = "Elixir of Lion's Strength",
                category = CraftCategory.Alchemy,
                requiredSkill = 10,
                description = "Imbues the drinker with the fury of a lion, granting +8 Attack Damage for 60 seconds.",
                outputItemId = "buff-strength",
                outputCount = 1,
                reagents = new[] { ("Peacebloom", 2), ("Kingsblood", 1) },
                rarityColor = new Color(0.12f, 1.0f, 0.0f) // Uncommon
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "draught-of-swiftness",
                displayName = "Draught of Swiftness",
                category = CraftCategory.Alchemy,
                requiredSkill = 20,
                description = "Greatly stimulates reflexes, increasing movement speed by +35% for 20 seconds.",
                outputItemId = "buff-swiftness",
                outputCount = 1,
                reagents = new[] { ("Silverleaf", 2), ("Kingsblood", 1) },
                rarityColor = new Color(0.0f, 0.44f, 0.87f) // Rare
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "flask-of-the-titans",
                displayName = "Flask of the Titans",
                category = CraftCategory.Alchemy,
                requiredSkill = 35,
                description = "An ancient draught that increases Maximum Health by +80 for 5 minutes.",
                outputItemId = "buff-titans",
                outputCount = 1,
                reagents = new[] { ("Peacebloom", 3), ("Kingsblood", 2) },
                rarityColor = new Color(0.64f, 0.21f, 0.93f) // Epic
            });

            // BLACKSMITHING RECIPES
            allRecipes.Add(new CraftRecipe
            {
                id = "sharpened-copper-dagger",
                displayName = "Sharpened Copper Dagger",
                category = CraftCategory.Blacksmithing,
                requiredSkill = 1,
                description = "Forged from smelted copper bars. Fast blade granting +8 Weapon Damage.",
                outputItemId = "copper-dagger",
                outputCount = 1,
                reagents = new[] { ("Copper Ore", 2) },
                rarityColor = new Color(0.9f, 0.9f, 0.9f)
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "tempered-bronze-broadsword",
                displayName = "Tempered Bronze Broadsword",
                category = CraftCategory.Blacksmithing,
                requiredSkill = 15,
                description = "Heavy martial blade folded at the forge. Grants +16 Weapon Damage and +15 Maximum Health.",
                outputItemId = "bronze-broadsword",
                outputCount = 1,
                reagents = new[] { ("Copper Ore", 3), ("Tin Ore", 2) },
                rarityColor = new Color(0.12f, 1.0f, 0.0f)
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "heavy-iron-buckler",
                displayName = "Heavy Iron Buckler",
                category = CraftCategory.Blacksmithing,
                requiredSkill = 25,
                description = "Reinforced shield with hardened boss. Grants +8 Armor and +35 Maximum Health.",
                outputItemId = "iron-buckler",
                outputCount = 1,
                reagents = new[] { ("Tin Ore", 2), ("Silver Ore", 1) },
                rarityColor = new Color(0.0f, 0.44f, 0.87f)
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "truesilver-executioner",
                displayName = "Truesilver Executioner",
                category = CraftCategory.Blacksmithing,
                requiredSkill = 40,
                description = "Masterwork 2H Greatsword gleaming with truesilver sheen. Grants +24 Weapon Damage and +5% Crit Chance.",
                outputItemId = "truesilver-blade",
                outputCount = 1,
                reagents = new[] { ("Tin Ore", 3), ("Silver Ore", 3) },
                rarityColor = new Color(0.64f, 0.21f, 0.93f)
            });

            // ENGINEERING RECIPES
            allRecipes.Add(new CraftRecipe
            {
                id = "goblin-boom-powder-bomb",
                displayName = "Goblin Boom Powder Bomb",
                category = CraftCategory.Engineering,
                requiredSkill = 1,
                description = "Volatile explosive concoction. Hurls a bomb dealing 45 Fire Damage in a 7-meter radius.",
                outputItemId = "boom-bomb",
                outputCount = 3,
                reagents = new[] { ("Copper Ore", 1), ("Kingsblood", 1) },
                rarityColor = new Color(0.9f, 0.9f, 0.9f)
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "gnomish-target-dummy",
                displayName = "Gnomish Target Dummy",
                category = CraftCategory.Engineering,
                requiredSkill = 15,
                description = "Deploys a mechanized decoy dummy that pulses a high-pitched siren, distracting nearby enemies for 10 seconds.",
                outputItemId = "target-dummy",
                outputCount = 1,
                reagents = new[] { ("Tin Ore", 2), ("Silver Ore", 1) },
                rarityColor = new Color(0.12f, 1.0f, 0.0f)
            });

            allRecipes.Add(new CraftRecipe
            {
                id = "rocket-boots-xtreme",
                displayName = "Rocket Boots Xtreme",
                category = CraftCategory.Engineering,
                requiredSkill = 30,
                description = "Equips volatile turbo-jets beneath your boots, providing a supersonic +100% sprint burst for 12 seconds!",
                outputItemId = "buff-rocket",
                outputCount = 1,
                reagents = new[] { ("Copper Ore", 2), ("Tin Ore", 2), ("Silver Ore", 1) },
                rarityColor = new Color(0.0f, 0.44f, 0.87f)
            });
        }

        public void AddReagent(string name, int count)
        {
            if (string.IsNullOrEmpty(name) || count <= 0) return;
            // Normalize herb and ore names
            string clean = name.Replace(" Leaf", "").Replace(" Ore", "").Replace(" Vein", "").Trim();
            if (clean.StartsWith("Peacebloom")) clean = "Peacebloom";
            else if (clean.StartsWith("Silverleaf")) clean = "Silverleaf";
            else if (clean.StartsWith("Kingsblood")) clean = "Kingsblood";
            else if (clean.StartsWith("Copper")) clean = "Copper Ore";
            else if (clean.StartsWith("Tin")) clean = "Tin Ore";
            else if (clean.StartsWith("Silver")) clean = "Silver Ore";

            if (!reagents.ContainsKey(clean)) reagents[clean] = 0;
            reagents[clean] += count;

            CombatTextManager.Spawn(transform.position + Vector3.up * 1.5f, $"+{count} {clean}", new Color(0.5f, 1f, 0.5f), false);
            ChatSystem.LogLoot($"Received reagent: [{clean}] x{count}.");
        }

        public int GetReagentCount(string name)
        {
            return reagents.TryGetValue(name, out int count) ? count : 0;
        }

        public bool CanCraft(CraftRecipe recipe)
        {
            if (recipe == null) return false;
            int skill = GetSkill(recipe.category);
            if (skill < recipe.requiredSkill) return false;

            foreach (var r in recipe.reagents)
            {
                if (GetReagentCount(r.matId) < r.count) return false;
            }
            return true;
        }

        public int GetSkill(CraftCategory cat)
        {
            switch (cat)
            {
                case CraftCategory.Alchemy: return alchemySkill;
                case CraftCategory.Blacksmithing: return blacksmithingSkill;
                case CraftCategory.Engineering: return engineeringSkill;
                default: return 0;
            }
        }

        public void StartCraft(CraftRecipe recipe)
        {
            if (!CanCraft(recipe) || !player || !player.Stats.Alive) return;

            string action = recipe.category == CraftCategory.Alchemy ? "Brewing" :
                            recipe.category == CraftCategory.Blacksmithing ? "Forging" : "Tinkering";

            IsOpen = false; // close window so player sees cast bar
            CastBarSystem.StartCast($"{action} {recipe.displayName}...", 2.2f, () =>
            {
                FinishCraft(recipe);
            }, true, true);
        }

        void FinishCraft(CraftRecipe recipe)
        {
            if (recipe == null || !player) return;

            // Consume reagents
            foreach (var r in recipe.reagents)
            {
                if (reagents.ContainsKey(r.matId))
                    reagents[r.matId] = Mathf.Max(0, reagents[r.matId] - r.count);
            }

            // Award skill point (if not capped)
            int curSkill = GetSkill(recipe.category);
            if (curSkill < MaxSkill)
            {
                int gain = (recipe.requiredSkill + 15 >= curSkill) ? 2 : 1;
                SetSkill(recipe.category, Mathf.Min(MaxSkill, curSkill + gain));
                CombatTextManager.Spawn(transform.position + Vector3.up * 2.2f, $"+{gain} {recipe.category} Skill ({GetSkill(recipe.category)}/{MaxSkill})!", new Color(1f, 0.85f, 0.2f), true);
            }

            // Grant rewards
            if (recipe.outputItemId == "potion")
            {
                player.AddPotionsDirect(recipe.outputCount);
            }
            else if (recipe.outputItemId == "buff-strength")
            {
                BuffSystem.ApplyBuff(new BuffSystem.AuraBuff
                {
                    id = "elixir-strength",
                    name = "Lion's Strength",
                    description = "Increases Attack Damage by +8.",
                    tag = "LS",
                    iconColor = new Color(1f, 0.45f, 0.15f),
                    duration = 60f,
                    bonusDamage = 8f
                });
            }
            else if (recipe.outputItemId == "buff-swiftness")
            {
                BuffSystem.ApplyBuff(new BuffSystem.AuraBuff
                {
                    id = "swiftness-draught",
                    name = "Swiftness Draught",
                    description = "Increases Movement Speed by +35%.",
                    tag = "SW",
                    iconColor = new Color(0.2f, 0.9f, 0.35f),
                    duration = 20f,
                    bonusSpeedMultiplier = 1.35f
                });
            }
            else if (recipe.outputItemId == "buff-titans")
            {
                BuffSystem.ApplyBuff(new BuffSystem.AuraBuff
                {
                    id = "flask-titans",
                    name = "Flask of the Titans",
                    description = "Increases Maximum Health by +80.",
                    tag = "FT",
                    iconColor = new Color(0.65f, 0.25f, 0.95f),
                    duration = 300f
                });
                player.Stats.maxHealth += 80;
                player.Stats.Heal(80);
            }
            else if (recipe.outputItemId == "buff-rocket")
            {
                BuffSystem.ApplyBuff(new BuffSystem.AuraBuff
                {
                    id = "rocket-boots",
                    name = "Rocket Boots Xtreme",
                    description = "Supersonic propulsion active (+80% Speed)!",
                    tag = "RB",
                    iconColor = new Color(1.0f, 0.35f, 0.1f),
                    duration = 12f,
                    bonusSpeedMultiplier = 1.80f
                });
            }
            else if (recipe.outputItemId == "copper-dagger")
            {
                player.GrantItem("copper-dagger");
                ChatSystem.LogLoot($"You forged: [Sharpened Copper Dagger]! Open Equipment [I] to equip.");
            }
            else if (recipe.outputItemId == "bronze-broadsword")
            {
                player.GrantItem("bronze-broadsword");
                ChatSystem.LogLoot($"You forged: [Tempered Bronze Broadsword]! Open Equipment [I] to equip.");
            }
            else if (recipe.outputItemId == "iron-buckler")
            {
                player.GrantItem("iron-buckler");
                ChatSystem.LogLoot($"You forged: [Heavy Iron Buckler]! Open Equipment [I] to equip.");
            }
            else if (recipe.outputItemId == "truesilver-blade")
            {
                player.GrantItem("truesilver-blade");
                ChatSystem.LogLoot($"You forged: [Truesilver Executioner]! Open Equipment [I] to equip.");
            }
            else
            {
                ChatSystem.LogLoot($"Created: [{recipe.displayName}] x{recipe.outputCount}!");
            }

            CombatTextManager.Spawn(transform.position + Vector3.up * 1.8f, $"Created {recipe.displayName}!", recipe.rarityColor, false);
            ChatSystem.LogSystem($"Crafting Complete: [{recipe.displayName}].");
        }

        void SetSkill(CraftCategory cat, int val)
        {
            switch (cat)
            {
                case CraftCategory.Alchemy: alchemySkill = val; break;
                case CraftCategory.Blacksmithing: blacksmithingSkill = val; break;
                case CraftCategory.Engineering: engineeringSkill = val; break;
            }
        }

        void InitTextures()
        {
            if (bgTex != null) return;
            bgTex = MakeTex(new Color(0.04f, 0.05f, 0.07f, 0.96f));
            borderTex = MakeTex(new Color(0.58f, 0.46f, 0.28f, 0.95f));
            headerTex = MakeTex(new Color(0.09f, 0.12f, 0.17f, 0.95f));
            selectTex = MakeTex(new Color(0.25f, 0.35f, 0.55f, 0.45f));
            goldBarTex = MakeTex(new Color(0.85f, 0.70f, 0.25f, 1f));

            windowTitle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            windowTitle.normal.textColor = new Color(1f, 0.85f, 0.3f);

            categoryTab = new GUIStyle(GUI.skin.button) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            recipeItemStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(10, 8, 4, 4) };

            sectionHeading = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            sectionHeading.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            descStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, wordWrap = true };
            descStyle.normal.textColor = new Color(0.78f, 0.80f, 0.84f);

            craftBtnStyle = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            reagentStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleLeft };
        }

        Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public void DrawProfessionsGUI(float screenW, float screenH)
        {
            if (!IsOpen || !player) return;

            InitTextures();

            float winW = 760f;
            float winH = 490f;
            float x = (screenW - winW) * 0.5f;
            float y = (screenH - winH) * 0.5f;

            // Outer Frame
            GUI.DrawTexture(new Rect(x - 3, y - 3, winW + 6, winH + 6), borderTex);
            GUI.DrawTexture(new Rect(x, y, winW, winH), bgTex);

            // Header Bar
            GUI.DrawTexture(new Rect(x, y, winW, 46), headerTex);
            GUI.Label(new Rect(x + 16, y + 8, 380, 30), "PROFESSIONS & TRADE SKILLS", windowTitle);

            // Skill Progress Bar in header
            int curSkill = GetSkill(selectedCategory);
            float barW = 200f;
            float barH = 14f;
            float barX = x + winW - barW - 65f;
            float barY = y + 16f;
            GUI.DrawTexture(new Rect(barX, barY, barW, barH), MakeTex(new Color(0.1f, 0.12f, 0.15f)));
            float pct = (float)curSkill / MaxSkill;
            GUI.DrawTexture(new Rect(barX, barY, barW * pct, barH), goldBarTex);
            var centerLabel = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            centerLabel.normal.textColor = Color.black;
            GUI.Label(new Rect(barX, barY - 1, barW, barH), $"{selectedCategory}: {curSkill} / {MaxSkill} (Apprentice)", centerLabel);

            // Close [X] Button
            if (GUI.Button(new Rect(x + winW - 38, y + 8, 28, 28), "X", categoryTab))
            {
                IsOpen = false;
            }

            // Category Selection Tabs
            float tabY = y + 52;
            CraftCategory[] categories = { CraftCategory.Alchemy, CraftCategory.Blacksmithing, CraftCategory.Engineering };
            float tabW = 160f;
            for (int i = 0; i < categories.Length; i++)
            {
                var cat = categories[i];
                bool isSel = cat == selectedCategory;
                GUI.backgroundColor = isSel ? new Color(0.95f, 0.78f, 0.25f) : new Color(0.35f, 0.40f, 0.45f);
                if (GUI.Button(new Rect(x + 16 + i * (tabW + 8), tabY, tabW, 30), $"{cat} ({GetSkill(cat)}/{MaxSkill})", categoryTab))
                {
                    selectedCategory = cat;
                    // Auto select first recipe in category
                    selectedRecipe = allRecipes.Find(r => r.category == cat);
                }
            }
            GUI.backgroundColor = Color.white;

            // Split Panels: Left = Recipe List (320w), Right = Recipe Details (390w)
            float contentY = tabY + 40;
            float contentH = winH - (contentY - y) - 18;

            // Left Pane (Recipe List)
            float leftW = 310f;
            GUI.DrawTexture(new Rect(x + 16, contentY, leftW, contentH), MakeTex(new Color(0.06f, 0.08f, 0.11f, 0.9f)));
            GUI.DrawTexture(new Rect(x + 16, contentY, leftW, 1), borderTex);

            var categoryRecipes = allRecipes.FindAll(r => r.category == selectedCategory);
            recipeScroll = GUI.BeginScrollView(new Rect(x + 18, contentY + 4, leftW - 4, contentH - 8), recipeScroll, new Rect(0, 0, leftW - 24, categoryRecipes.Count * 44));

            for (int r = 0; r < categoryRecipes.Count; r++)
            {
                var rec = categoryRecipes[r];
                bool isSel = selectedRecipe == rec;
                bool canMake = CanCraft(rec);

                GUI.backgroundColor = isSel ? new Color(0.85f, 0.70f, 0.25f) : (canMake ? new Color(0.3f, 0.45f, 0.35f) : new Color(0.2f, 0.22f, 0.25f));
                recipeItemStyle.normal.textColor = rec.rarityColor;

                string statusPrefix = canMake ? "â— " : "â—‹ ";
                if (GUI.Button(new Rect(2, r * 44, leftW - 26, 40), $"{statusPrefix}{rec.displayName}", recipeItemStyle))
                {
                    selectedRecipe = rec;
                }
            }
            GUI.EndScrollView();
            GUI.backgroundColor = Color.white;

            // Right Pane (Recipe Details & Materials)
            float rightX = x + 16 + leftW + 12;
            float rightW = winW - (rightX - x) - 16;
            GUI.DrawTexture(new Rect(rightX, contentY, rightW, contentH), MakeTex(new Color(0.06f, 0.08f, 0.11f, 0.9f)));
            GUI.DrawTexture(new Rect(rightX, contentY, rightW, 1), borderTex);

            if (selectedRecipe != null)
            {
                float dy = contentY + 12;

                // Title
                var itemTitle = new GUIStyle(windowTitle) { fontSize = 16 };
                itemTitle.normal.textColor = selectedRecipe.rarityColor;
                GUI.Label(new Rect(rightX + 14, dy, rightW - 28, 24), selectedRecipe.displayName, itemTitle);
                dy += 28;

                // Subhead (Category + Req)
                GUI.Label(new Rect(rightX + 14, dy, rightW - 28, 18), $"Requires {selectedRecipe.category} ({selectedRecipe.requiredSkill})", descStyle);
                dy += 24;

                // Description
                GUI.Label(new Rect(rightX + 14, dy, rightW - 28, 55), selectedRecipe.description, descStyle);
                dy += 62;

                // Separator
                GUI.DrawTexture(new Rect(rightX + 14, dy, rightW - 28, 1), borderTex);
                dy += 10;

                // Required Reagents Header
                GUI.Label(new Rect(rightX + 14, dy, rightW - 28, 20), "REAGENTS REQUIRED:", sectionHeading);
                dy += 24;

                // Reagent Rows
                foreach (var reagent in selectedRecipe.reagents)
                {
                    int owned = GetReagentCount(reagent.matId);
                    bool hasEnough = owned >= reagent.count;
                    reagentStyle.normal.textColor = hasEnough ? new Color(0.4f, 0.95f, 0.4f) : new Color(0.95f, 0.4f, 0.4f);

                    GUI.Label(new Rect(rightX + 24, dy, rightW - 48, 22), $"â€¢ {reagent.matId}  ({owned} / {reagent.count})", reagentStyle);
                    dy += 24;
                }

                // Craft Button at bottom
                float craftBtnY = contentY + contentH - 52;
                bool canCraft = CanCraft(selectedRecipe);
                GUI.backgroundColor = canCraft ? new Color(0.95f, 0.75f, 0.18f) : new Color(0.35f, 0.35f, 0.35f);
                GUI.enabled = canCraft;

                if (GUI.Button(new Rect(rightX + 14, craftBtnY, rightW - 28, 40), canCraft ? "CREATE ITEM (2.2s CAST)" : "INSUFFICIENT REAGENTS", craftBtnStyle))
                {
                    StartCraft(selectedRecipe);
                }

                GUI.enabled = true;
                GUI.backgroundColor = Color.white;
            }
            else
            {
                GUI.Label(new Rect(rightX + 20, contentY + 40, rightW - 40, 30), "Select a recipe from the list to view requirements.", descStyle);
            }
        }
    }
}
