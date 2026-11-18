using System.Collections;
using UnityEngine;

namespace Riftbound
{
    public class CathedralBossEncounter : MonoBehaviour
    {
        public static CathedralBossEncounter Instance { get; private set; }

        public EnemyController malakar;
        public EnemyController abbotVael;
        public LootChest altarChest;

        public bool EncounterStarted { get; private set; }
        public bool EncounterCompleted { get; private set; }
        public int EncounterPhase { get; private set; } = 1; // 1 = Malakar, 2 = Both / Abbot, 3 = Defeated

        PlayerController player;
        float nextSpecialAbilityTime;
        bool vaelActivated;

        GUIStyle bossNameStyle, bossHpStyle;
        Texture2D bossBarBg, bossBarFill, borderTex;

        void Awake()
        {
            if (Instance == null) Instance = this;
            player = FindFirstObjectByType<PlayerController>();
        }

        void Start()
        {
            FindBossReferences();
        }

        public void FindBossReferences()
        {
            if (!malakar || !abbotVael)
            {
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (enemy.gameObject.name.Contains("Malakar")) malakar = enemy;
                    if (enemy.gameObject.name.Contains("Vael")) abbotVael = enemy;
                }
            }

            if (!altarChest)
            {
                foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
                {
                    if (chest.chestId == "cathedral-altar-chest") altarChest = chest;
                }
            }

            // Put Abbot in dormant ritual meditation until Phase 2
            if (abbotVael && !vaelActivated)
            {
                var combatant = abbotVael.GetComponent<Combatant>();
                if (combatant) combatant.incomingDamageMultiplier = 0f;
            }
        }

        void Update()
        {
            if (EncounterCompleted) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            // Check if player is inside Cathedral Dungeon (Z > 190, X < -370, Y < -20)
            bool inCathedral = player.transform.position.x < -350f && player.transform.position.y < -20f;
            if (!inCathedral) return;

            if (!EncounterStarted)
            {
                // Player approaches altar
                float distToMalakar = malakar ? Vector3.Distance(player.transform.position, malakar.transform.position) : 999f;
                if (distToMalakar < 18f)
                {
                    StartEncounter();
                }
                return;
            }

            // Encounter logic
            if (EncounterPhase == 1)
            {
                var malakarStats = malakar ? malakar.GetComponent<Combatant>() : null;
                if (malakarStats && malakarStats.Health <= 1f)
                {
                    // Transition to Phase 2
                    TransitionToPhase2();
                }
                else
                {
                    // Periodic Holy Wrath cast
                    if (Time.time >= nextSpecialAbilityTime)
                    {
                        nextSpecialAbilityTime = Time.time + 7f;
                        CastMalakarHolyWrath();
                    }
                }
            }
            else if (EncounterPhase == 2)
            {
                var vaelStats = abbotVael ? abbotVael.GetComponent<Combatant>() : null;
                if (vaelStats && !vaelStats.Alive)
                {
                    CompleteEncounter();
                }
                else
                {
                    // Periodic Shadow Bolt Volley
                    if (Time.time >= nextSpecialAbilityTime)
                    {
                        nextSpecialAbilityTime = Time.time + 6f;
                        CastVaelShadowVolley();
                    }
                }
            }
        }

        void StartEncounter()
        {
            EncounterStarted = true;
            nextSpecialAbilityTime = Time.time + 4f;

            ChatSystem.LogSystem("High Inquisitor Malakar shouts: \"Intruders! You shall not desecrate the sacred halls of the Scarlet Crusade!\"");
            AudioEffects.PlayClick();
            ZoneBanner.Show("The Scarlet Crypts", "Boss Encounter: High Inquisitor Malakar");
        }

        void TransitionToPhase2()
        {
            EncounterPhase = 2;
            vaelActivated = true;

            if (malakar)
            {
                var mStats = malakar.GetComponent<Combatant>();
                if (mStats) mStats.incomingDamageMultiplier = 0f;
                // Golden shield VFX
                SpellFX.CreateConsecration(malakar.transform.position, 2.5f);
            }

            if (abbotVael)
            {
                var vStats = abbotVael.GetComponent<Combatant>();
                if (vStats) vStats.incomingDamageMultiplier = 1f;
            }

            ChatSystem.LogSystem("Dread Abbot Vael shouts: \"Arise, my champion! Cleanse the infidels with the sacred flame!\"");
            ZoneBanner.Show("The Scarlet Crypts", "Phase 2: Dread Abbot Vael Awakens!");
            AudioEffects.PlaySpellCast();
            nextSpecialAbilityTime = Time.time + 3f;
        }

        void CompleteEncounter()
        {
            EncounterPhase = 3;
            EncounterCompleted = true;

            if (malakar)
            {
                var mStats = malakar.GetComponent<Combatant>();
                if (mStats) { mStats.incomingDamageMultiplier = 1f; mStats.Damage(9999f); }
            }

            AudioEffects.PlayBossVictory();
            ChatSystem.LogSystem("*** DUNGEON COMPLETE: High Inquisitor Malakar & Dread Abbot Vael have been defeated! ***");
            ZoneBanner.Show("Cathedral Purged!", "Dungeon Victory! Claim your rewards from the Altar Chest.");

            // Spawn boss loot drops
            if (abbotVael) LootDropSystem.SpawnLoot(abbotVael.transform.position, "Dread Abbot Vael", 45, 8, "scarlet-aegis");
            if (malakar) LootDropSystem.SpawnLoot(malakar.transform.position, "High Inquisitor Malakar", 35, 6, "cathedral-relic");

            if (player && !Network.CoopSessionConfig.IsMultiplayer)
            {
                player.AddExperience(250);
                player.AddGoldDirect(80);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2f, "+250 XP (DUNGEON BOSS)", new Color(0.4f, 1f, 0.5f), true);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.5f, "+80 Gold", new Color(1f, 0.85f, 0.2f), false);

                // Award Scourge Hunter title
                PlayerTitleSystem.SetActiveTitle("Scourge Hunter");
                ChatSystem.LogSystem("<color=#e0b034>★ Title Unlocked: 'Scourge Hunter'!</color>");
            }
        }

        void CastMalakarHolyWrath()
        {
            if (!malakar || !player || !player.Stats.Alive || EncounterPhase != 1) return;
            var mStats = malakar.GetComponent<Combatant>();
            if (!mStats || !mStats.Alive) return;
            ChatSystem.LogCombat("High Inquisitor Malakar begins casting [Holy Wrath]...");
            CastBarSystem.StartTargetCast(mStats, "Holy Wrath", "☼", 2.2f, () =>
            {
                if (malakar && mStats && mStats.Alive && EncounterPhase == 1 && player && player.Stats.Alive)
                {
                    player.Stats.Damage(24f);
                    CombatTextManager.Spawn(player.transform.position, "-24 HOLY", new Color(1f, 0.85f, 0.3f), false);
                    SpellFX.CreateConsecration(player.transform.position, 1.5f);
                    AudioEffects.PlayBladeHit();
                }
            }, interruptible: true);
        }

        void CastVaelShadowVolley()
        {
            if (!abbotVael || !player || !player.Stats.Alive || EncounterPhase != 2) return;
            var vStats = abbotVael.GetComponent<Combatant>();
            if (!vStats || !vStats.Alive) return;
            ChatSystem.LogCombat("Dread Abbot Vael begins casting [Shadow Bolt Volley]...");
            CastBarSystem.StartTargetCast(vStats, "Shadow Bolt Volley", "💀", 2.4f, () =>
            {
                if (abbotVael && vStats && vStats.Alive && EncounterPhase == 2 && player && player.Stats.Alive)
                {
                    player.Stats.Damage(28f);
                    CombatTextManager.Spawn(player.transform.position, "-28 SHADOW", new Color(0.7f, 0.2f, 0.95f), false);
                    AudioEffects.PlaySpellCast();
                }
            }, interruptible: true);
        }

        public void DrawBossFrameGUI(float screenW, float screenH)
        {
            if (!EncounterStarted || EncounterCompleted || FrontendMenu.IsOpen) return;
            if (!player || player.transform.position.x > -350f) return;

            InitStyles();

            Combatant activeBoss = (EncounterPhase == 1 && malakar) ? malakar.GetComponent<Combatant>() :
                                  (abbotVael ? abbotVael.GetComponent<Combatant>() : null);
            if (!activeBoss) return;

            string bossName = (EncounterPhase == 1) ? "☠ High Inquisitor Malakar [Phase 1]" : "☠ Dread Abbot Vael [Phase 2]";
            float barW = 340f;
            float barH = 22f;
            float x = screenW / 2f - barW / 2f;
            float y = 52f;

            // Background & Border
            GUI.DrawTexture(new Rect(x - 2, y - 2, barW + 4, barH + 4), borderTex);
            GUI.DrawTexture(new Rect(x, y, barW, barH), bossBarBg);

            // HP Fill
            float pct = Mathf.Clamp01(activeBoss.maxHealth > 0 ? activeBoss.Health / activeBoss.maxHealth : 0f);
            if (pct > 0.001f)
            {
                GUI.DrawTexture(new Rect(x, y, barW * pct, barH), bossBarFill);
            }

            // Labels
            GUI.Label(new Rect(x, y - 24f, barW, 22f), bossName, bossNameStyle);
            GUI.Label(new Rect(x, y, barW, barH), $"{Mathf.CeilToInt(activeBoss.Health)} / {Mathf.CeilToInt(activeBoss.maxHealth)} ({(int)(pct * 100)}%)", bossHpStyle);
        }

        void InitStyles()
        {
            if (bossNameStyle != null) return;

            bossBarBg = new Texture2D(1, 1);
            bossBarBg.SetPixel(0, 0, new Color(0.12f, 0.02f, 0.02f, 0.95f));
            bossBarBg.Apply();

            bossBarFill = new Texture2D(1, 1);
            bossBarFill.SetPixel(0, 0, new Color(0.85f, 0.15f, 0.18f, 1f));
            bossBarFill.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.95f, 0.8f, 0.25f, 1f));
            borderTex.Apply();

            bossNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bossNameStyle.normal.textColor = new Color(1.0f, 0.88f, 0.35f);

            bossHpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bossHpStyle.normal.textColor = Color.white;
        }
    }
}
