using System;
using System.Collections;
using UnityEngine;

namespace Riftbound
{
    public class WorldBossEncounter : MonoBehaviour
    {
        public static WorldBossEncounter Instance { get; private set; }

        public Combatant bossCombatant;
        public EnemyController bossController;
        public Vector3 spawnPosition = new Vector3(10f, 0f, 185f);

        bool enraged = false;
        bool meteorActive = false;
        float nextStompTime = 0f;
        float nextMeteorTime = 0f;
        bool lootSpawned = false;

        PlayerController player;

        GUIStyle bossNameStyle, skullStyle, subTitleStyle, pctStyle;
        Texture2D barBgTex, hpTex, borderTex, skullBgTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>();
            if (WorldRegions.Expanded) spawnPosition = WorldRegions.WorldBoss;
        }

        void Start()
        {
            FindBossReference();
        }

        void FindBossReference()
        {
            if (bossCombatant != null) return;
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy.enemyId == "worldboss-gorgonax")
                {
                    bossController = enemy;
                    bossCombatant = enemy.GetComponent<Combatant>();
                    spawnPosition = enemy.transform.position;
                    break;
                }
            }
        }

        void Update()
        {
            if (bossCombatant == null)
            {
                if (Time.frameCount % 90 == 0) FindBossReference();
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
            if (dist > 35f) return;

            // Phase 1 / Recurring: Seismic Stomp (Every 14s)
            if (Time.time >= nextStompTime)
            {
                nextStompTime = Time.time + 14f;
                TriggerSeismicStomp();
            }

            // Phase 2: Meteor Strike (Every 22s when in range)
            if (Time.time >= nextMeteorTime && dist < 25f && !meteorActive)
            {
                nextMeteorTime = Time.time + 22f;
                StartCoroutine(MeteorStrikeRoutine());
            }

            // Phase 3: Molten Enrage below 35% HP
            float hpPct = (float)bossCombatant.Health / bossCombatant.maxHealth;
            if (hpPct <= 0.35f && !enraged)
            {
                enraged = true;
                TriggerEnrage();
            }
        }

        void TriggerSeismicStomp()
        {
            ChatSystem.LogCombat("<color=#ff6633>[Gorgonax]</color> slams the earth! The ground violently shakes!");
            CombatTextManager.Spawn(bossCombatant.transform.position + Vector3.up * 4.2f, "★ SEISMIC STOMP! ★", new Color(1f, 0.45f, 0.1f), true);

            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            float d = Vector3.Distance(player.transform.position, bossCombatant.transform.position);
            if (d < 14f && player.Stats.Alive)
            {
                player.Stats.Damage(28f);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.5f, "-28 Fire (Stomp)", new Color(1f, 0.25f, 0.2f), false);
            }
        }

        IEnumerator MeteorStrikeRoutine()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) yield break;

            meteorActive = true;
            Vector3 targetPos = player.transform.position;
            ChatSystem.LogCombat("<color=#ff4422>[Gorgonax]</color> summons a Molten Meteor! MOVE!");
            CombatTextManager.Spawn(targetPos + Vector3.up * 0.5f, "⚠ METEOR IMPACT ZONE ⚠", new Color(1f, 0.2f, 0.2f), true);

            // Warning delay
            yield return new WaitForSeconds(2.4f);
            if (!bossCombatant || !bossCombatant.Alive) { meteorActive = false; yield break; }

            // Impact
            float distToImpact = Vector3.Distance(player.transform.position, targetPos);
            if (distToImpact < 6.0f && player.Stats.Alive)
            {
                player.Stats.Damage(45f);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.0f, "-45 METEOR IMPACT!", new Color(1f, 0.1f, 0.1f), true);
            }
            else
            {
                CombatTextManager.Spawn(targetPos + Vector3.up * 1.0f, "Meteor Evaded!", new Color(0.4f, 1f, 0.4f), false);
            }
            meteorActive = false;
        }

        void TriggerEnrage()
        {
            ChatSystem.LogCombat("<color=#ff2222><b>[Gorgonax the Worldbreaker]</b></color> enters a MOLTEN FRENZY! Attack speed increased!");
            CombatTextManager.Spawn(bossCombatant.transform.position + Vector3.up * 4.5f, "🔥 MOLTEN ENRAGE! 🔥", Color.red, true);
            if (bossController != null)
            {
                bossController.attackInterval = 1.05f; // Rapid strikes
                bossController.attackDamage = 32f;
            }
        }

        void OnBossDefeated()
        {
            ChatSystem.LogSystem("<color=#ffd700><b>[WORLD EVENT]</b></color> Gorgonax the Worldbreaker has been vanquished!");
            CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.5f, "WORLD BOSS DEFEATED! +150 XP", new Color(1f, 0.85f, 0.2f), true);
            if (player && !Network.CoopSessionConfig.IsMultiplayer)
            {
                player.AddExperience(150);
                player.AddGoldDirect(65);
            }
            AudioEffects.PlayLootCoin();

            // Spawn World Boss Hoard Chest (prefer 3D TreasureChest prefab over raw primitive)
            var chestPrefab = Resources.Load<GameObject>("Props/TreasureChest");
            GameObject chestObj;
            Vector3 chestPos = spawnPosition + new Vector3(0f, 0.6f, 3.5f);
            if (Physics.Raycast(chestPos + Vector3.up * 5f, Vector3.down, out var gHit, 15f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                chestPos.y = gHit.point.y;

            if (chestPrefab != null)
            {
                chestObj = Instantiate(chestPrefab, chestPos, Quaternion.identity);
                chestObj.name = "Molten Worldbreaker Hoard Chest";
                chestObj.transform.localScale = Vector3.one * 1.35f;
            }
            else
            {
                chestObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chestObj.name = "Molten Worldbreaker Hoard Chest";
                chestObj.transform.position = chestPos + Vector3.up * 0.55f;
                chestObj.transform.localScale = new Vector3(1.8f, 1.1f, 1.2f);
                var chestMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                chestMat.SetColor("_BaseColor", new Color(0.55f, 0.35f, 0.12f));
                chestMat.SetFloat("_Metallic", 0.35f);
                chestMat.SetFloat("_Smoothness", 0.45f);
                chestObj.GetComponent<Renderer>().sharedMaterial = chestMat;
            }

            var chest = chestObj.AddComponent<LootChest>();
            chest.chestId = "worldboss-gorgonax-chest";

            var lt = chestObj.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(1.0f, 0.45f, 0.05f);
            lt.range = 14f;
            lt.intensity = 4.2f;

            // Grant guaranteed Epic Purple Loot
            string[] epicDrops = { "worldbreaker-maul", "crown-of-molten-ruin", "cinderfury-signet" };
            string chosen = epicDrops[UnityEngine.Random.Range(0, epicDrops.Length)];
            player.GrantItem(chosen);
            var item = ItemCatalog.Get(chosen);
            if (item != null)
            {
                ChatSystem.LogLoot($"World Boss Loot: Received [{item.name}] ({item.rarity} {item.slot})!");
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 3.0f, $"Looted: {item.name}!", item.color, true);
            }

            // Award Titanbreaker Title
            PlayerTitleSystem.SetActiveTitle("Titanbreaker");
            ChatSystem.LogSystem("<color=#e0b034>★ Title Unlocked: 'Titanbreaker'!</color>");
        }

        void InitTextures()
        {
            if (barBgTex != null) return;
            barBgTex = MakeTex(new Color(0.04f, 0.05f, 0.07f, 0.95f));
            borderTex = MakeTex(new Color(0.85f, 0.65f, 0.22f, 0.98f)); // Ornate gold
            hpTex = MakeTex(new Color(0.85f, 0.15f, 0.15f, 1f));       // Crimson
            skullBgTex = MakeTex(new Color(0.18f, 0.04f, 0.04f, 0.98f));

            bossNameStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bossNameStyle.normal.textColor = new Color(1f, 0.9f, 0.6f);

            skullStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            skullStyle.normal.textColor = new Color(1f, 0.2f, 0.2f);

            subTitleStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            subTitleStyle.normal.textColor = new Color(0.95f, 0.55f, 0.25f);

            pctStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            pctStyle.normal.textColor = Color.white;
        }

        Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        public void DrawBossHealthBar(float screenW, float screenH)
        {
            if (bossCombatant == null || !bossCombatant.Alive || FrontendMenu.IsOpen) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;
            float dist = Vector3.Distance(player.transform.position, bossCombatant.transform.position);
            if (dist > 35f) return;

            InitTextures();

            float barW = 440f;
            float barH = 34f;
            float x = (screenW - barW) * 0.5f;
            float y = 55f;

            // Gold Dragon Ornate Outer Border
            GUI.DrawTexture(new Rect(x - 3, y - 3, barW + 6, barH + 6), borderTex);
            GUI.DrawTexture(new Rect(x, y, barW, barH), barBgTex);

            // Skull Level Badge on the left
            float skullSize = barH + 8;
            GUI.DrawTexture(new Rect(x - skullSize - 4, y - 4, skullSize, skullSize), borderTex);
            GUI.DrawTexture(new Rect(x - skullSize - 2, y - 2, skullSize - 4, skullSize - 4), skullBgTex);
            GUI.Label(new Rect(x - skullSize - 2, y + 2, skullSize - 4, skullSize - 8), "☠ 10", skullStyle);

            // Health Bar Fill
            float pct = Mathf.Clamp01((float)bossCombatant.Health / bossCombatant.maxHealth);
            GUI.DrawTexture(new Rect(x + 2, y + 2, (barW - 4) * pct, barH - 4), hpTex);

            // Boss Title & Subtitle
            GUI.Label(new Rect(x, y - 24, barW, 22), "GORGONAX THE WORLDBREAKER", bossNameStyle);
            GUI.Label(new Rect(x, y + 2, barW, 16), "World Boss — Magma Colossus", subTitleStyle);
            GUI.Label(new Rect(x, y + 16, barW, 16), $"{Mathf.CeilToInt(bossCombatant.Health)} / {bossCombatant.maxHealth} ({Mathf.RoundToInt(pct * 100)}%)", pctStyle);
        }
    }
}
