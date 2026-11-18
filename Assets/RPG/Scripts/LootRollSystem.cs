using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class LootRollSystem : MonoBehaviour
    {
        static LootRollSystem _instance;
        public static LootRollSystem Instance
        {
            get
            {
                if (!_instance) _instance = FindAnyObjectByType<LootRollSystem>();
                return _instance;
            }
            private set => _instance = value;
        }

        public class LootRollEntry
        {
            public string id;
            public string name;
            public string rarity; // "Epic", "Rare", "Legendary", "Uncommon"
            public string description;
            public string icon;
            public float timer = 22f;
            public float maxTimer = 22f;
        }

        List<LootRollEntry> pendingRolls = new List<LootRollEntry>();
        public IReadOnlyList<LootRollEntry> PendingRolls => pendingRolls;

        PlayerController player;
        GUIStyle windowStyle, titleStyle, nameStyle, descStyle, btnStyle;
        Texture2D solidTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
        }

        void Update()
        {
            if (!player) player = GetComponent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();

            for (int i = pendingRolls.Count - 1; i >= 0; i--)
            {
                pendingRolls[i].timer -= Time.deltaTime;
                if (pendingRolls[i].timer <= 0f)
                {
                    // Default to Greed on timeout
                    ResolveRoll(pendingRolls[i], RollChoice.Greed);
                }
            }
        }

        public enum RollChoice
        {
            Pass,
            Greed,
            Need
        }

        public static void PromptRoll(string itemId, string itemName, string rarity, string desc, string icon = "💎")
        {
            if (!Instance)
            {
                var go = new GameObject("LootRollSystem");
                Instance = go.AddComponent<LootRollSystem>();
            }

            // Check if already in queue
            if (Instance.pendingRolls.Exists(r => r.id == itemId)) return;

            var entry = new LootRollEntry
            {
                id = itemId,
                name = itemName,
                rarity = rarity,
                description = desc,
                icon = icon,
                timer = 22f,
                maxTimer = 22f
            };

            Instance.pendingRolls.Add(entry);
            AudioEffects.PlayLootCoin();
            ChatSystem.LogCombat($"<color=#ffd100>★ Group Loot Roll started for [{itemName}]! (20s to roll)</color>");
        }

        public void ChooseRoll(string itemId, RollChoice choice)
        {
            var entry = pendingRolls.Find(r => r.id == itemId);
            if (entry != null)
            {
                ResolveRoll(entry, choice);
            }
        }

        void ResolveRoll(LootRollEntry entry, RollChoice playerChoice)
        {
            pendingRolls.Remove(entry);

            if (playerChoice == RollChoice.Pass)
            {
                ChatSystem.LogCombat($"[Loot] You passed on [{entry.name}].");
                return;
            }

            int playerRoll = UnityEngine.Random.Range(1, 101);
            string choiceStr = playerChoice == RollChoice.Need ? "Need" : "Greed";
            ChatSystem.LogCombat($"[Loot] You rolled <b>{playerRoll}</b> ({choiceStr}) for [{entry.name}].");

            // Simulate party member rolls
            string[] companions = { "Gryphon Master Darien", "Shaman Kelsey", "Wind Rider Vark" };
            int highestCompRoll = UnityEngine.Random.Range(20, 95);
            string winningCompanion = companions[UnityEngine.Random.Range(0, companions.Length)];
            bool compChoseNeed = (entry.rarity == "Epic" || entry.rarity == "Legendary") && (UnityEngine.Random.value > 0.45f);

            bool playerWins = false;
            if (playerChoice == RollChoice.Need)
            {
                if (!compChoseNeed || playerRoll >= highestCompRoll) playerWins = true;
            }
            else // player chose Greed
            {
                if (!compChoseNeed && playerRoll >= highestCompRoll) playerWins = true;
            }

            if (playerWins)
            {
                AudioEffects.PlayQuestComplete();
                ChatSystem.LogCombat($"<color=#ffd100>★ You won the roll for [{entry.name}] with a {playerRoll}!</color>");
                if (ZoneBanner.Instance) ZoneBanner.Show("LOOT WON!", entry.name);
                if (player)
                {
                    player.GrantItem(entry.id);
                    player.SaveAdventure(false);
                }
            }
            else
            {
                string compChoiceStr = compChoseNeed ? "Need" : "Greed";
                ChatSystem.LogCombat($"[Loot] {winningCompanion} rolled {highestCompRoll} ({compChoiceStr}) and won [{entry.name}].");
            }
        }

        public void DrawLootRollGUI(float screenWidth, float screenHeight)
        {
            if (pendingRolls.Count == 0) return;

            InitStyles();

            // Draw up to 2 active roll prompts stacked above action bar
            float rw = 360f, rh = 64f;
            float rx = (screenWidth - rw) * 0.5f;
            float startY = screenHeight - 160f;

            for (int i = 0; i < Mathf.Min(2, pendingRolls.Count); i++)
            {
                var roll = pendingRolls[i];
                float ry = startY - (i * (rh + 8f));

                // Background
                Fill(new Rect(rx, ry, rw, rh), new Color(0.06f, 0.08f, 0.11f, 0.94f));

                // Quality border
                Color rarityCol = GetRarityColor(roll.rarity);
                Fill(new Rect(rx, ry, rw, 1.5f), rarityCol);
                Fill(new Rect(rx, ry + rh - 1.5f, rw, 1.5f), rarityCol);
                Fill(new Rect(rx, ry, 1.5f, rh), rarityCol);
                Fill(new Rect(rx + rw - 1.5f, ry, 1.5f, rh), rarityCol);

                // Icon
                Fill(new Rect(rx + 6f, ry + 6f, 38f, 38f), new Color(0.12f, 0.15f, 0.20f, 0.95f));
                GUI.Label(new Rect(rx + 6f, ry + 4f, 38f, 38f), roll.icon, titleStyle);

                // Item Name
                nameStyle.normal.textColor = rarityCol;
                GUI.Label(new Rect(rx + 50f, ry + 5f, 210f, 20f), roll.name, nameStyle);

                // Description
                GUI.Label(new Rect(rx + 50f, ry + 24f, 210f, 18f), roll.description, descStyle);

                // Action Buttons: Need [🎲], Greed [🪙], Pass [✖]
                if (GUI.Button(new Rect(rx + 266f, ry + 6f, 28f, 28f), "🎲", btnStyle))
                {
                    ChooseRoll(roll.id, RollChoice.Need);
                    break;
                }
                if (GUI.Button(new Rect(rx + 298f, ry + 6f, 28f, 28f), "🪙", btnStyle))
                {
                    ChooseRoll(roll.id, RollChoice.Greed);
                    break;
                }
                if (GUI.Button(new Rect(rx + 330f, ry + 6f, 24f, 28f), "✖", btnStyle))
                {
                    ChooseRoll(roll.id, RollChoice.Pass);
                    break;
                }

                // Rolling Timer Bar
                float timePct = Mathf.Clamp01(roll.timer / roll.maxTimer);
                Fill(new Rect(rx + 50f, ry + 46f, 304f, 8f), new Color(0.15f, 0.12f, 0.08f, 0.9f));
                Fill(new Rect(rx + 50f, ry + 46f, 304f * timePct, 8f), new Color(1.0f, 0.75f, 0.20f, 0.95f));
            }
        }

        Color GetRarityColor(string rarity)
        {
            switch (rarity)
            {
                case "Legendary": return new Color(1.0f, 0.50f, 0.0f); // Orange
                case "Epic": return new Color(0.64f, 0.21f, 0.93f);    // Purple
                case "Rare": return new Color(0.0f, 0.44f, 0.87f);     // Blue
                case "Uncommon": return new Color(0.12f, 1.0f, 0.0f);  // Green
                default: return Color.white;
            }
        }

        void InitStyles()
        {
            if (solidTex == null)
            {
                solidTex = new Texture2D(1, 1);
                solidTex.SetPixel(0, 0, Color.white);
                solidTex.Apply();
            }

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
            }

            if (descStyle == null)
            {
                descStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 10,
                    normal = { textColor = new Color(0.75f, 0.75f, 0.75f) }
                };
            }

            if (btnStyle == null)
            {
                btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 13,
                    alignment = TextAnchor.MiddleCenter
                };
            }
        }

        void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, solidTex);
            GUI.color = old;
        }
    }
}
