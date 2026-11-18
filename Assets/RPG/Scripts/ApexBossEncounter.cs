using System;
using System.Collections;
using UnityEngine;

namespace Riftbound
{
    public class ApexBossEncounter : MonoBehaviour
    {
        public static ApexBossEncounter Instance { get; private set; }

        public Combatant bossCombatant;
        public EnemyController bossController;
        public LootChest hoardChest;
        public Vector3 summitCenter = new Vector3(0f, 48f, 225f);

        public bool EncounterStarted { get; private set; }
        public bool EncounterCompleted { get; private set; }
        public bool Enraged { get; private set; }

        PlayerController player;
        float nextBreathTime;
        float nextMeteorTime;
        bool lootSpawned;

        GUIStyle nameStyle, titleStyle, pctStyle;
        Texture2D barBgTex, barFillTex, borderTex, crestTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            if (WorldRegions.Expanded) summitCenter = WorldRegions.Summit;
        }

        void Start()
        {
            FindBossReferences();
        }

        public void FindBossReferences()
        {
            if (bossCombatant == null)
            {
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (enemy.enemyId == "vermithrax-boss" || enemy.gameObject.name.Contains("Vermithrax"))
                    {
                        bossController = enemy;
                        bossCombatant = enemy.GetComponent<Combatant>();
                        break;
                    }
                }
            }

            if (!hoardChest)
            {
                foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
                {
                    if (chest.chestId == "wyrmcrest-hoard-chest")
                    {
                        hoardChest = chest;
                        break;
                    }
                }
            }
        }

        void Update()
        {
            if (FrontendMenu.IsOpen || EncounterCompleted) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            if (bossCombatant == null)
            {
                if (Time.frameCount % 120 == 0) FindBossReferences();
                return;
            }

            if (!bossCombatant.Alive)
            {
                if (!lootSpawned)
                {
                    lootSpawned = true;
                    OnBossDefeated();
                }
                return;
            }

            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            float dist = Vector3.Distance(player.transform.position, bossCombatant.transform.position);
            bool inRoost = dist < 42f && player.transform.position.y > (summitCenter.y - 10f);

            if (!EncounterStarted)
            {
                if (inRoost && dist < 22f)
                {
                    StartEncounter();
                }
                return;
            }

            if (!inRoost) return;

            // Phase 1: Flame Breath (every 11s)
            if (Time.time >= nextBreathTime)
            {
                nextBreathTime = Time.time + (Enraged ? 8f : 11f);
                CastFlameBreath();
            }

            // Phase 2: Enrage check at 50% HP
            float hpPct = bossCombatant.Health / bossCombatant.maxHealth;
            if (!Enraged && hpPct <= 0.50f)
            {
                TriggerEnrage();
            }

            // Enraged Meteor Volley (every 9s)
            if (Enraged && Time.time >= nextMeteorTime)
            {
                nextMeteorTime = Time.time + 9f;
                CastMoltenMeteors();
            }
        }

        void StartEncounter()
        {
            EncounterStarted = true;
            nextBreathTime = Time.time + 5f;
            nextMeteorTime = Time.time + 10f;

            ChatSystem.LogSystem("<color=#ff4500><b>[APEX ROOST]</b></color> Vermithrax the Ember Sovereign lets out an earth-shattering roar!");
            ZoneBanner.Show("Wyrmcrest Summit", "Apex Boss: Vermithrax the Ember Sovereign");
            AudioEffects.PlaySpellCast();
        }

        void TriggerEnrage()
        {
            Enraged = true;
            ChatSystem.LogSystem("<color=#ff2000><b>[BOSS ENRAGE]</b></color> Vermithrax unleashes the Primordial Flame! Molten fury consumes the summit!");
            CombatTextManager.Spawn(bossCombatant.transform.position + Vector3.up * 5.5f, "🔥 PRIMORDIAL FLAME ENRAGE! 🔥", new Color(1f, 0.25f, 0.05f), true);

            if (bossController)
            {
                bossController.attackInterval = 1.15f;
                bossController.attackDamage = 35f;
            }
            SpellFX.CreateConsecration(bossCombatant.transform.position, 4.0f);
        }

        void CastFlameBreath()
        {
            if (!bossCombatant || !bossCombatant.Alive || !player || !player.Stats.Alive) return;
            ChatSystem.LogCombat("Vermithrax begins channeling [Ember Breath]...");

            CastBarSystem.StartTargetCast(bossCombatant, "Pyroclastic Surge", "🔥", 2.6f, () =>
            {
                if (bossCombatant && bossCombatant.Alive && player && player.Stats.Alive)
                {
                    Vector3 toPlayer = (player.transform.position - bossCombatant.transform.position).normalized;
                    float angle = Vector3.Angle(bossCombatant.transform.forward, toPlayer);
                    float dist = Vector3.Distance(bossCombatant.transform.position, player.transform.position);

                    if (angle < 65f && dist < 18f)
                    {
                        float dmg = Enraged ? 36f : 26f;
                        player.Stats.Damage(dmg);
                        CombatTextManager.Spawn(player.transform.position, $"-{dmg:0} FIRE BREATH", new Color(1f, 0.4f, 0.1f), true);
                        SpellFX.CreateConsecration(player.transform.position, 2.0f);
                        AudioEffects.PlayBladeHit();
                    }
                }
            }, interruptible: !Enraged);
        }

        void CastMoltenMeteors()
        {
            if (!bossCombatant || !bossCombatant.Alive || !player || !player.Stats.Alive) return;
            ChatSystem.LogCombat("Volcanic fire rains down from the Wyrmcrest heavens!");

            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = UnityEngine.Random.insideUnitSphere * 12f;
                offset.y = 0f;
                Vector3 impactPos = player.transform.position + offset;
                StartCoroutine(DropMeteor(impactPos, 1.2f + i * 0.5f));
            }
        }

        IEnumerator DropMeteor(Vector3 targetPos, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!bossCombatant || !bossCombatant.Alive) yield break;
            SpellFX.CreateConsecration(targetPos, 2.5f);
            AudioEffects.PlayBladeHit();

            if (player && player.Stats.Alive && Vector3.Distance(player.transform.position, targetPos) < 4.5f)
            {
                player.Stats.Damage(28f);
                CombatTextManager.Spawn(player.transform.position, "-28 METEOR STRIKE", new Color(1f, 0.2f, 0.1f), true);
            }
        }

        void OnBossDefeated()
        {
            EncounterCompleted = true;
            ChatSystem.LogSystem("<color=#ffd700><b>*** APEX APOTHEOSIS ***</b></color> Vermithrax the Ember Sovereign has been slain!");
            ZoneBanner.Show("Wyrmcrest Conquered!", "Apex Sovereign Purged. The Skies of Aethelgard are Free.");
            AudioEffects.PlayBossVictory();

            if (player && !Network.CoopSessionConfig.IsMultiplayer)
            {
                player.AddExperience(400);
                player.AddGoldDirect(120);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.5f, "+400 XP (APEX SOVEREIGN)", new Color(0.4f, 1f, 0.5f), true);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.8f, "+120 Gold", new Color(1f, 0.85f, 0.2f), false);

                // Spawn sparkler corpse loot
                Vector3 corpsePos = bossCombatant ? bossCombatant.transform.position : summitCenter;
                LootDropSystem.SpawnLoot(corpsePos, "Vermithrax the Ember Sovereign", 85, 25, "wyrmtooth-edge");

                // Guaranteed legendary heart
                player.GrantItem("ember-drake-scale");
                var drakeScale = ItemCatalog.Get("ember-drake-scale");
                if (drakeScale != null)
                {
                    ChatSystem.LogLoot($"Apex Trophy: Received [{drakeScale.name}] (Legendary Trinket)!");
                    CombatTextManager.Spawn(player.transform.position + Vector3.up * 3.2f, $"Looted: {drakeScale.name}!", drakeScale.color, true);
                }

                // Award Dragonslayer title
                PlayerTitleSystem.SetActiveTitle("Dragonslayer");
                ChatSystem.LogSystem("<color=#ffd700>★ Title Unlocked: 'Dragonslayer'!</color>");
            }
        }

        public void DrawBossFrameGUI(float screenW, float screenH)
        {
            if (!EncounterStarted || EncounterCompleted || FrontendMenu.IsOpen) return;
            if (!bossCombatant || !player || player.transform.position.y < 35f) return;

            InitTextures();

            float barW = 420f;
            float barH = 34f;
            float x = (screenW - barW) * 0.5f;
            float y = 38f;

            // Background & Border
            GUI.DrawTexture(new Rect(x - 3, y - 3, barW + 6, barH + 6), borderTex);
            GUI.DrawTexture(new Rect(x, y, barW, barH), barBgTex);

            // Fill
            float pct = Mathf.Clamp01(bossCombatant.Health / bossCombatant.maxHealth);
            GUI.DrawTexture(new Rect(x + 2, y + 2, (barW - 4) * pct, barH - 4), barFillTex);

            // Dragon Crest / Skull Box
            float skullSize = 44f;
            GUI.DrawTexture(new Rect(x - skullSize + 4, y - 5, skullSize, skullSize), borderTex);
            GUI.DrawTexture(new Rect(x - skullSize + 6, y - 3, skullSize - 4, skullSize - 4), crestTex);

            var skullLabel = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold
            };
            skullLabel.normal.textColor = new Color(1f, 0.85f, 0.2f);
            GUI.Label(new Rect(x - skullSize + 4, y - 5, skullSize, skullSize), "🐉", skullLabel);

            // Boss Name & Phase Label
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
                nameStyle.normal.textColor = new Color(1f, 0.95f, 0.85f);
            }

            string title = Enraged ? "Vermithrax the Ember Sovereign [🔥 ENRAGED]" : "Vermithrax the Ember Sovereign [Lv 8 Apex Boss]";
            GUI.Label(new Rect(x + 12, y + 1, barW - 120, barH - 2), title, nameStyle);

            // HP text
            if (pctStyle == null)
            {
                pctStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 13,
                    fontStyle = FontStyle.Bold
                };
                pctStyle.normal.textColor = Color.white;
            }

            string hpText = $"{Mathf.CeilToInt(bossCombatant.Health)} / {bossCombatant.maxHealth:0}  ({pct * 100f:0}%)";
            GUI.Label(new Rect(x + barW - 140, y + 1, 130, barH - 2), hpText, pctStyle);
        }

        void InitTextures()
        {
            if (barBgTex != null) return;
            barBgTex = MakeTex(new Color(0.08f, 0.05f, 0.03f, 0.96f));
            borderTex = MakeTex(new Color(0.95f, 0.65f, 0.15f, 1f)); // Ornate dragon gold
            barFillTex = MakeTex(new Color(0.92f, 0.32f, 0.08f, 1f)); // Molten ember flame
            crestTex = MakeTex(new Color(0.22f, 0.06f, 0.04f, 0.98f));
        }

        Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
