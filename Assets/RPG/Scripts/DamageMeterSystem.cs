using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class DamageMeterSystem : MonoBehaviour
    {
        public static DamageMeterSystem Instance { get; private set; }

        public enum MeterDisplayMode
        {
            DamageDone,
            DPS,
            HealingDone,
            DamageTaken
        }

        [Serializable]
        public class CombatantMeterData
        {
            public string name;
            public string characterClass;
            public int totalDamage;
            public int totalHealing;
            public int damageTaken;
            public float combatActiveTime;

            public CombatantMeterData(string name, string characterClass)
            {
                this.name = name;
                this.characterClass = characterClass;
                this.totalDamage = 0;
                this.totalHealing = 0;
                this.damageTaken = 0;
                this.combatActiveTime = 0f;
            }

            public float DPS => combatActiveTime > 0.5f ? (float)totalDamage / combatActiveTime : totalDamage;
            public float HPS => combatActiveTime > 0.5f ? (float)totalHealing / combatActiveTime : totalHealing;
        }

        public List<CombatantMeterData> combatants = new List<CombatantMeterData>();
        public MeterDisplayMode displayMode = MeterDisplayMode.DamageDone;
        public bool IsMeterOpen { get; set; } = false;
        public float combatDuration = 0f;
        public bool IsCombatActive { get; private set; } = false;

        PlayerController player;
        float combatEndTimer = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            SeedInitialData();
        }

        void Update()
        {
            if (IsCombatActive)
            {
                combatDuration += Time.deltaTime;
                foreach (var c in combatants)
                {
                    c.combatActiveTime += Time.deltaTime;
                }

                // Check if player left combat
                if (player != null && !player.InCombat())
                {
                    combatEndTimer += Time.deltaTime;
                    if (combatEndTimer >= 4f)
                    {
                        IsCombatActive = false;
                        combatEndTimer = 0f;
                    }
                }
                else
                {
                    combatEndTimer = 0f;
                }
            }
        }

        public void SeedInitialData()
        {
            if (combatants.Count > 0) return;

            string pName = player ? player.Stats?.displayName ?? "Garrick" : "Garrick";
            string pClass = player ? player.CharacterClass : "Warrior";

            var pData = new CombatantMeterData(pName, pClass) { totalDamage = 840, combatActiveTime = 10f };
            var ally1 = new CombatantMeterData("Darien Silverguard", "Paladin") { totalDamage = 420, totalHealing = 280, combatActiveTime = 10f };
            var ally2 = new CombatantMeterData("Elaria Moonfall", "Mage") { totalDamage = 350, combatActiveTime = 10f };

            combatants.Add(pData);
            combatants.Add(ally1);
            combatants.Add(ally2);
            combatDuration = 10f;
        }

        public void RecordDamage(string sourceName, string sourceClass, int amount, string targetName = "")
        {
            if (amount <= 0 || string.IsNullOrEmpty(sourceName)) return;

            IsCombatActive = true;
            combatEndTimer = 0f;

            var entry = combatants.Find(c => c.name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new CombatantMeterData(sourceName, sourceClass);
                combatants.Add(entry);
            }

            entry.totalDamage += amount;

            if (!string.IsNullOrEmpty(targetName))
            {
                RecordDamageTaken(targetName, "Target", amount);
            }
        }

        public void RecordHealing(string sourceName, string sourceClass, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(sourceName)) return;

            IsCombatActive = true;
            combatEndTimer = 0f;

            var entry = combatants.Find(c => c.name.Equals(sourceName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new CombatantMeterData(sourceName, sourceClass);
                combatants.Add(entry);
            }

            entry.totalHealing += amount;
        }

        public void RecordDamageTaken(string targetName, string targetClass, int amount)
        {
            if (amount <= 0 || string.IsNullOrEmpty(targetName)) return;

            var entry = combatants.Find(c => c.name.Equals(targetName, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                entry = new CombatantMeterData(targetName, targetClass);
                combatants.Add(entry);
            }

            entry.damageTaken += amount;
        }

        public void ResetMeter()
        {
            combatants.Clear();
            combatDuration = 0f;
            IsCombatActive = false;
            ChatSystem.LogSystem("<color=#aaa>[Damage Meter] Combat data reset.</color>");
            AudioEffects.PlayClick();
        }

        public void ToggleMeter()
        {
            IsMeterOpen = !IsMeterOpen;
            AudioEffects.PlayClick();
        }

        public void CycleDisplayMode()
        {
            displayMode = (MeterDisplayMode)(((int)displayMode + 1) % 4);
            AudioEffects.PlayClick();
        }

        public void ReportToChat()
        {
            if (combatants.Count == 0)
            {
                ChatSystem.LogSystem("[Damage Meter] No combat data to report.");
                return;
            }

            var sorted = GetSortedEntries();
            int total = GetTotalMetric();
            string modeName = displayMode switch
            {
                MeterDisplayMode.DamageDone => "Damage Done",
                MeterDisplayMode.DPS => "DPS",
                MeterDisplayMode.HealingDone => "Healing Done",
                _ => "Damage Taken"
            };

            ChatSystem.LogSystem($"<color=#ffd700><b>[Meter Report: {modeName}]</b> (Fight Duration: {combatDuration:0.0}s)</color>");
            int rank = 1;
            foreach (var c in sorted)
            {
                if (rank > 5) break;
                int val = GetCombatantMetric(c);
                float pct = total > 0 ? ((float)val / total) * 100f : 0f;
                string line = $"{rank}. {c.name} ({c.characterClass}) - {val:N0} ({c.DPS:0} DPS, {pct:0.0}%)";
                ChatSystem.LogSystem($"<color=#{ColorUtility.ToHtmlStringRGB(GetClassColor(c.characterClass))}>{line}</color>");
                rank++;
            }
        }

        public List<CombatantMeterData> GetSortedEntries()
        {
            var list = new List<CombatantMeterData>(combatants);
            list.Sort((a, b) => GetCombatantMetric(b).CompareTo(GetCombatantMetric(a)));
            return list;
        }

        public int GetCombatantMetric(CombatantMeterData c)
        {
            return displayMode switch
            {
                MeterDisplayMode.DamageDone => c.totalDamage,
                MeterDisplayMode.DPS => Mathf.RoundToInt(c.DPS),
                MeterDisplayMode.HealingDone => c.totalHealing,
                _ => c.damageTaken
            };
        }

        public int GetTotalMetric()
        {
            int sum = 0;
            foreach (var c in combatants) sum += GetCombatantMetric(c);
            return Mathf.Max(1, sum);
        }

        public static Color GetClassColor(string cls)
        {
            return cls?.ToLowerInvariant() switch
            {
                "warrior" => new Color(0.78f, 0.61f, 0.43f), // Warrior Brown
                "paladin" => new Color(0.96f, 0.55f, 0.73f), // Paladin Pink
                "mage" => new Color(0.25f, 0.78f, 0.92f),    // Mage Light Blue
                "rogue" => new Color(1.0f, 0.96f, 0.41f),    // Rogue Yellow
                "priest" => Color.white,
                "hunter" => new Color(0.67f, 0.83f, 0.45f),  // Hunter Green
                "warlock" => new Color(0.53f, 0.53f, 0.93f), // Warlock Purple
                "druid" => new Color(1.0f, 0.49f, 0.04f),    // Druid Orange
                _ => new Color(0.85f, 0.25f, 0.25f)          // Red for monsters
            };
        }

        // GUI Rendering
        public void DrawDamageMeterGUI(float screenWidth, float screenHeight)
        {
            if (!IsMeterOpen) return;

            // Docked low-profile window on middle-right side
            float winW = 240f;
            float winH = 175f;
            float winX = screenWidth - winW - 14f;
            float winY = screenHeight * 0.42f;

            // Border & Backing
            DrawRect(new Rect(winX - 2, winY - 2, winW + 4, winH + 4), new Color(0.85f, 0.70f, 0.28f, 0.9f));
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.05f, 0.07f, 0.10f, 0.92f));

            // Header Banner
            DrawRect(new Rect(winX, winY, winW, 26f), new Color(0.12f, 0.15f, 0.20f, 1f));

            string modeTitle = displayMode switch
            {
                MeterDisplayMode.DamageDone => "DAMAGE DONE (DPS)",
                MeterDisplayMode.DPS => "DPS METRIC",
                MeterDisplayMode.HealingDone => "HEALING DONE",
                _ => "DAMAGE TAKEN"
            };

            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 6, winY + 3, winW - 85, 20), $"⚔ {modeTitle}", headStyle);

            // Mini controls
            if (GUI.Button(new Rect(winX + winW - 74, winY + 3, 22, 20), "🔄"))
            {
                ResetMeter();
            }
            if (GUI.Button(new Rect(winX + winW - 50, winY + 3, 22, 20), "📢"))
            {
                ReportToChat();
            }
            if (GUI.Button(new Rect(winX + winW - 26, winY + 3, 22, 20), "✕"))
            {
                IsMeterOpen = false;
                AudioEffects.PlayClick();
            }

            // Mode switcher button on click header
            if (GUI.Button(new Rect(winX + 6, winY + 3, winW - 85, 20), GUIContent.none, GUIStyle.none))
            {
                CycleDisplayMode();
            }

            // Ranked List
            var sorted = GetSortedEntries();
            int total = GetTotalMetric();
            int topVal = sorted.Count > 0 ? GetCombatantMetric(sorted[0]) : 1;
            if (topVal <= 0) topVal = 1;

            float rowY = winY + 30;
            float rowH = 22f;
            int maxRows = Mathf.Min(5, sorted.Count);

            for (int r = 0; r < maxRows; r++)
            {
                var entry = sorted[r];
                int val = GetCombatantMetric(entry);
                float ratio = Mathf.Clamp01((float)val / topVal);

                // Row backing & class color fill
                DrawRect(new Rect(winX + 4, rowY, winW - 8, rowH), new Color(0.08f, 0.10f, 0.14f, 0.8f));
                Color classCol = GetClassColor(entry.characterClass);
                DrawRect(new Rect(winX + 4, rowY, (winW - 8) * ratio, rowH), classCol * 0.75f);

                // Text: Left = Rank. Name
                var lStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
                lStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(winX + 8, rowY + 1, 120, 20), $"{r + 1}. {entry.name}", lStyle);

                // Text: Right = Value (DPS, %)
                var rStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleRight };
                rStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f);
                float pct = ((float)val / total) * 100f;
                string statStr = displayMode == MeterDisplayMode.DPS ? $"{val:N0}/s ({pct:0}%)" : $"{val:N0} ({pct:0}%)";
                GUI.Label(new Rect(winX + winW - 110, rowY + 1, 104, 20), statStr, rStyle);

                rowY += rowH + 2f;
            }

            // Bottom Footer Status
            float footY = winY + winH - 22;
            DrawRect(new Rect(winX, footY, winW, 22), new Color(0.03f, 0.04f, 0.06f, 1f));
            var footStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, alignment = TextAnchor.MiddleCenter };
            footStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);
            GUI.Label(new Rect(winX, footY, winW, 20), $"⏱ {combatDuration:0.0}s  •  Total: {total:N0}  •  [Click Header to Cycle]", footStyle);
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
