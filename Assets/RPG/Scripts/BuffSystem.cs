using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class BuffSystem : MonoBehaviour
    {
        public static BuffSystem Instance { get; private set; }

        [Serializable]
        public class AuraBuff
        {
            public string id;
            public string name;
            public string description;
            public string tag; // 2-letter abbreviation
            public Color iconColor;
            public float duration; // -1 for permanent
            public float startTime;

            public float bonusDamage;
            public float bonusArmor;
            public float bonusSpeedMultiplier; // 1.0 = normal, 1.6 = +60%
            public float bonusHpRegen;

            public float Remaining => duration <= 0 ? 9999f : Mathf.Max(0f, (startTime + duration) - Time.time);
            public bool IsExpired => duration > 0 && Time.time >= startTime + duration;
        }

        [Serializable]
        public class TargetDebuff
        {
            public string id;
            public string name;
            public string description;
            public string icon;
            public Color color;
            public float duration;
            public float startTime;
            public int stacks = 1;
            public float tickInterval = 1f;
            public float nextTickTime;
            public float damagePerTick;

            public float Remaining => duration <= 0 ? 999f : Mathf.Max(0f, (startTime + duration) - Time.time);
            public bool IsExpired => duration > 0 && Time.time >= startTime + duration;
        }

        List<AuraBuff> activeBuffs = new List<AuraBuff>();
        readonly Dictionary<Combatant, List<TargetDebuff>> targetDebuffs = new Dictionary<Combatant, List<TargetDebuff>>();
        readonly List<Combatant> deadTargets = new List<Combatant>();
        PlayerController player;

        GUIStyle buffBoxStyle, buffTagStyle, buffTimeStyle, tooltipStyle;
        Texture2D buffBgTex, borderTex, tooltipBgTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();

            // Apply starting racial / class passive buffs
            ApplyClassBuffs();
        }

        public void ApplyClassBuffs()
        {
            if (!player) player = GetComponent<PlayerController>();
            if (!player) return;

            if (player.CharacterClass == "Warrior")
            {
                ApplyBuff(new AuraBuff
                {
                    id = "warrior-shout",
                    name = "Battle Presence",
                    description = "+10% increased attack power and resilience in melee combat.",
                    tag = "BP",
                    iconColor = new Color(0.85f, 0.2f, 0.15f),
                    duration = -1,
                    bonusDamage = 3f,
                    bonusArmor = 2f
                });
            }
            else if (player.CharacterClass == "Mage")
            {
                ApplyBuff(new AuraBuff
                {
                    id = "mage-intellect",
                    name = "Arcane Brilliance",
                    description = "+8 Mana regeneration per second and amplified spell critical strike.",
                    tag = "AB",
                    iconColor = new Color(0.2f, 0.5f, 0.95f),
                    duration = -1,
                    bonusDamage = 4f
                });
            }
            else if (player.CharacterClass == "Rogue")
            {
                ApplyBuff(new AuraBuff
                {
                    id = "rogue-fleet",
                    name = "Fleet Footed",
                    description = "Permanent +10% movement speed and increased agility.",
                    tag = "FF",
                    iconColor = new Color(0.95f, 0.85f, 0.15f),
                    duration = -1,
                    bonusSpeedMultiplier = 0.10f
                });
            }
            else if (player.CharacterClass == "Paladin")
            {
                ApplyBuff(new AuraBuff
                {
                    id = "paladin-aura",
                    name = "Devotion Aura",
                    description = "Holy aura providing +4 armor and health regeneration to the champion.",
                    tag = "DA",
                    iconColor = new Color(1.0f, 0.88f, 0.35f),
                    duration = -1,
                    bonusArmor = 4f,
                    bonusHpRegen = 2f
                });
            }
        }

        public static void ApplyBuff(AuraBuff buff)
        {
            if (!Instance)
            {
                var go = new GameObject("BuffSystem");
                Instance = go.AddComponent<BuffSystem>();
            }

            buff.startTime = Time.time;
            for (int i = 0; i < Instance.activeBuffs.Count; i++)
            {
                if (Instance.activeBuffs[i].id == buff.id)
                {
                    Instance.activeBuffs[i] = buff; // refresh
                    return;
                }
            }

            Instance.activeBuffs.Add(buff);
            ChatSystem.LogSystem($"Aura applied: [{buff.name}]");
        }

        public static void RemoveBuff(string id)
        {
            if (!Instance) return;
            for (int i = Instance.activeBuffs.Count - 1; i >= 0; i--)
            {
                if (Instance.activeBuffs[i].id == id)
                {
                    ChatSystem.LogSystem($"Aura faded: [{Instance.activeBuffs[i].name}]");
                    Instance.activeBuffs.RemoveAt(i);
                }
            }
        }

        public static bool HasBuff(string id)
        {
            if (!Instance) return false;
            for (int i = 0; i < Instance.activeBuffs.Count; i++)
                if (Instance.activeBuffs[i].id == id) return true;
            return false;
        }

        public static void ApplyTargetDebuff(Combatant target, string id, string name, string description, string icon, Color color, float duration, float damagePerTick = 0f, float tickInterval = 1f)
        {
            if (!target) return;
            if (!Instance)
            {
                Instance = UnityEngine.Object.FindAnyObjectByType<BuffSystem>();
                if (!Instance)
                {
                    var go = new GameObject("BuffSystem");
                    Instance = go.AddComponent<BuffSystem>();
                }
            }

            if (!Instance.targetDebuffs.TryGetValue(target, out var list))
            {
                list = new List<TargetDebuff>();
                Instance.targetDebuffs[target] = list;
            }

            var existing = list.Find(d => d.id == id);
            if (existing != null)
            {
                existing.startTime = Time.time;
                existing.duration = duration;
                existing.stacks = Mathf.Min(existing.stacks + 1, 5);
                existing.damagePerTick = damagePerTick;
            }
            else
            {
                list.Add(new TargetDebuff
                {
                    id = id,
                    name = name,
                    description = description,
                    icon = icon,
                    color = color,
                    duration = duration,
                    startTime = Time.time,
                    stacks = 1,
                    damagePerTick = damagePerTick,
                    tickInterval = tickInterval,
                    nextTickTime = Time.time + tickInterval
                });
            }
        }

        public static IReadOnlyList<TargetDebuff> GetTargetDebuffs(Combatant target)
        {
            if (!Instance || !target) return null;
            Instance.targetDebuffs.TryGetValue(target, out var list);
            return list;
        }

        public static bool HasTargetDebuff(Combatant target, string id)
        {
            if (!Instance || !target) return false;
            if (Instance.targetDebuffs.TryGetValue(target, out var list))
                return list.Exists(d => d.id == id);
            return false;
        }

        public static float GetTotalBonusDamage()
        {
            if (!Instance) return 0f;
            float sum = 0f;
            for (int i = 0; i < Instance.activeBuffs.Count; i++)
                sum += Instance.activeBuffs[i].bonusDamage;
            return sum;
        }

        public static float GetTotalBonusArmor()
        {
            if (!Instance) return 0f;
            float sum = 0f;
            for (int i = 0; i < Instance.activeBuffs.Count; i++)
                sum += Instance.activeBuffs[i].bonusArmor;
            return sum;
        }

        public static float GetTotalBonusSpeedMultiplier()
        {
            if (!Instance) return 0f;
            float sum = 0f;
            for (int i = 0; i < Instance.activeBuffs.Count; i++)
                sum += Instance.activeBuffs[i].bonusSpeedMultiplier;
            return sum;
        }

        void Update()
        {
            if (!player) player = GetComponent<PlayerController>();

            // Check mount flight buff
            if (player && player.Mounted && player.SelectedMount && player.SelectedMount.isFlying)
            {
                if (!HasBuff("steed-flight"))
                {
                    ApplyBuff(new AuraBuff
                    {
                        id = "steed-flight",
                        name = "Flight of the Steed",
                        description = "Soaring through the skies. Shift for max speed boost.",
                        tag = "FL",
                        iconColor = new Color(0.4f, 0.75f, 1.0f),
                        duration = -1
                    });
                }
            }
            else
            {
                if (HasBuff("steed-flight")) RemoveBuff("steed-flight");
            }

            // Expire timed buffs
            for (int i = activeBuffs.Count - 1; i >= 0; i--)
            {
                if (activeBuffs[i].IsExpired)
                {
                    ChatSystem.LogSystem($"Aura faded: [{activeBuffs[i].name}]");
                    activeBuffs.RemoveAt(i);
                }
            }

            // Health regen from buffs
            if (player && player.Stats && player.Stats.Alive)
            {
                float regen = 0f;
                for (int i = 0; i < activeBuffs.Count; i++)
                    regen += activeBuffs[i].bonusHpRegen;

                if (regen > 0f)
                    player.Stats.Heal(regen * Time.deltaTime);
            }

            // Update target debuffs & periodic damage ticks
            if (targetDebuffs.Count > 0)
            {
                deadTargets.Clear();
                foreach (var kvp in targetDebuffs)
                {
                    var target = kvp.Key;
                    var debuffs = kvp.Value;

                    if (!target || !target.Alive)
                    {
                        deadTargets.Add(target);
                        continue;
                    }

                    for (int i = debuffs.Count - 1; i >= 0; i--)
                    {
                        var d = debuffs[i];
                        if (d.IsExpired)
                        {
                            debuffs.RemoveAt(i);
                            continue;
                        }

                        // Periodic tick (host-authoritative in multiplayer)
                        bool authoritativeTick = !Network.CoopSessionConfig.IsMultiplayer || (Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsServer);
                        if (d.damagePerTick > 0f && Time.time >= d.nextTickTime)
                        {
                            d.nextTickTime = Time.time + d.tickInterval;
                            if (authoritativeTick)
                            {
                                float dmg = d.damagePerTick * d.stacks;
                                target.Damage(dmg);
                                CombatTextManager.Spawn(target.transform.position, $"-{Mathf.RoundToInt(dmg)} {d.icon}", d.color, false);
                            }
                        }
                    }

                    if (debuffs.Count == 0) deadTargets.Add(target);
                }

                for (int i = 0; i < deadTargets.Count; i++)
                    targetDebuffs.Remove(deadTargets[i]);
            }
        }

        public void DrawBuffsGUI(float screenW, float screenH)
        {
            if (FrontendMenu.IsOpen || activeBuffs.Count == 0) return;

            InitStyles();

            float buffSize = 30f;
            float spacing = 6f;
            // Place buff row to the left of the radar minimap (which ends at screenW - 15)
            float startX = screenW - 170f - (activeBuffs.Count * (buffSize + spacing));
            float startY = 16f;

            Vector2 mousePos = Event.current.mousePosition;
            AuraBuff hovered = null;
            Rect hoverRect = Rect.zero;

            for (int i = 0; i < activeBuffs.Count; i++)
            {
                var b = activeBuffs[i];
                float bx = startX + i * (buffSize + spacing);
                float by = startY;
                Rect bRect = new Rect(bx, by, buffSize, buffSize);

                // Border
                GUI.DrawTexture(new Rect(bx - 1f, by - 1f, buffSize + 2f, buffSize + 2f), borderTex);

                // Tinted Box
                var oldCol = GUI.color;
                GUI.color = b.iconColor;
                GUI.DrawTexture(bRect, buffBgTex);
                GUI.color = oldCol;

                // Tag
                GUI.Label(bRect, b.tag, buffTagStyle);

                // Time remaining label below badge
                if (b.duration > 0)
                {
                    string timeStr = b.Remaining < 60f ? $"{Mathf.CeilToInt(b.Remaining)}s" : $"{Mathf.CeilToInt(b.Remaining / 60f)}m";
                    GUI.Label(new Rect(bx - 5f, by + buffSize + 1f, buffSize + 10f, 14f), timeStr, buffTimeStyle);
                }

                if (bRect.Contains(mousePos))
                {
                    hovered = b;
                    hoverRect = bRect;
                }
            }

            // Draw Tooltip on hover
            if (hovered != null)
            {
                float tipW = 210f;
                float tipH = 75f;
                float tx = Mathf.Clamp(hoverRect.x - tipW / 2f + buffSize / 2f, 10f, screenW - tipW - 10f);
                float ty = hoverRect.yMax + 18f;

                GUI.DrawTexture(new Rect(tx - 2f, ty - 2f, tipW + 4f, tipH + 4f), borderTex);
                GUI.DrawTexture(new Rect(tx, ty, tipW, tipH), tooltipBgTex);

                GUI.Label(new Rect(tx + 8f, ty + 6f, tipW - 16f, 20f), hovered.name, buffTagStyle);
                GUI.Label(new Rect(tx + 8f, ty + 26f, tipW - 16f, 44f), hovered.description, tooltipStyle);
            }
        }

        public void DrawTargetDebuffsGUI(Combatant target, float startX, float startY)
        {
            if (!target || !target.Alive || FrontendMenu.IsOpen) return;
            if (!targetDebuffs.TryGetValue(target, out var list) || list.Count == 0) return;

            InitStyles();

            float dSize = 22f;
            float spacing = 4f;
            Vector2 mousePos = Event.current.mousePosition;
            TargetDebuff hovered = null;
            Rect hoverRect = Rect.zero;

            for (int i = 0; i < list.Count; i++)
            {
                var d = list[i];
                float dx = startX + i * (dSize + spacing);
                float dy = startY;
                Rect dRect = new Rect(dx, dy, dSize, dSize);

                // Border
                var oldCol = GUI.color;
                GUI.color = d.color;
                GUI.DrawTexture(new Rect(dx - 1f, dy - 1f, dSize + 2f, dSize + 2f), borderTex);
                GUI.color = oldCol;

                // Dark backing
                GUI.DrawTexture(dRect, buffBgTex);

                // Icon
                GUI.Label(dRect, d.icon, buffTagStyle);

                // Stacks badge in top-right
                if (d.stacks > 1)
                {
                    GUI.Label(new Rect(dx + dSize - 10f, dy - 3f, 12f, 12f), d.stacks.ToString(), buffTimeStyle);
                }

                // Countdown below
                if (d.duration > 0)
                {
                    GUI.Label(new Rect(dx - 4f, dy + dSize, dSize + 8f, 12f), $"{Mathf.CeilToInt(d.Remaining)}s", buffTimeStyle);
                }

                if (dRect.Contains(mousePos))
                {
                    hovered = d;
                    hoverRect = dRect;
                }
            }

            // Tooltip
            if (hovered != null)
            {
                float tipW = 190f;
                float tipH = 55f;
                float tx = Mathf.Clamp(hoverRect.x, 10f, Screen.width - tipW - 10f);
                float ty = hoverRect.yMax + 14f;

                GUI.DrawTexture(new Rect(tx - 1.5f, ty - 1.5f, tipW + 3f, tipH + 3f), borderTex);
                GUI.DrawTexture(new Rect(tx, ty, tipW, tipH), tooltipBgTex);

                string title = hovered.stacks > 1 ? $"{hovered.name} ({hovered.stacks})" : hovered.name;
                GUI.Label(new Rect(tx + 6f, ty + 4f, tipW - 12f, 18f), title, buffTagStyle);
                GUI.Label(new Rect(tx + 6f, ty + 22f, tipW - 12f, 30f), hovered.description, tooltipStyle);
            }
        }

        void InitStyles()
        {
            if (buffBoxStyle != null) return;

            buffBgTex = new Texture2D(1, 1);
            buffBgTex.SetPixel(0, 0, new Color(0.12f, 0.14f, 0.18f, 0.95f));
            buffBgTex.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.75f, 0.62f, 0.25f, 1f));
            borderTex.Apply();

            tooltipBgTex = new Texture2D(1, 1);
            tooltipBgTex.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.09f, 0.96f));
            tooltipBgTex.Apply();

            buffBoxStyle = new GUIStyle();
            buffBoxStyle.normal.background = buffBgTex;

            buffTagStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buffTagStyle.normal.textColor = Color.white;

            buffTimeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            buffTimeStyle.normal.textColor = new Color(0.92f, 0.92f, 0.85f);

            tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            tooltipStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
        }
    }
}
