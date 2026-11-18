using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class LootDropSystem : MonoBehaviour
    {
        public static LootDropSystem Instance { get; private set; }

        public class LootCorpse
        {
            public string id;
            public string name;
            public Vector3 position;
            public int gold;
            public int boneShards;
            public List<string> items = new List<string>();
            public float spawnTime;
            public GameObject markerObject;
        }

        [System.Serializable]
        public class SavedCorpse
        {
            public string name;
            public Vector3 position;
            public int gold, boneShards;
            public string[] items;
            public float remainingSeconds;
        }

        public SavedCorpse[] ExportCorpses()
        {
            var saved = new List<SavedCorpse>();
            foreach (var corpse in activeCorpses)
            {
                float remaining = 120f - (Time.time - corpse.spawnTime);
                if (remaining <= 0 || (corpse.gold <= 0 && corpse.boneShards <= 0 && corpse.items.Count == 0)) continue;
                saved.Add(new SavedCorpse { name = corpse.name, position = corpse.position, gold = corpse.gold,
                    boneShards = corpse.boneShards, items = corpse.items.ToArray(), remainingSeconds = remaining });
            }
            return saved.ToArray();
        }

        public void RestoreCorpses(SavedCorpse[] saved)
        {
            foreach (var corpse in activeCorpses) if (corpse.markerObject) Destroy(corpse.markerObject);
            activeCorpses.Clear();
            CurrentLooting = null;
            if (saved == null) return;
            foreach (var corpse in saved)
            {
                if (corpse == null || corpse.remainingSeconds <= 0) continue;
                SpawnLoot(corpse.position, corpse.name, Mathf.Max(0, corpse.gold), Mathf.Max(0, corpse.boneShards), "");
                var restored = activeCorpses[activeCorpses.Count - 1];
                restored.spawnTime = Time.time - (120f - Mathf.Min(120f, corpse.remainingSeconds));
                if (corpse.items != null) foreach (string item in corpse.items)
                    if (ItemCatalog.Get(item) != null) restored.items.Add(item);
            }
        }

        List<LootCorpse> activeCorpses = new List<LootCorpse>();
        public List<LootCorpse> ActiveCorpses => activeCorpses;
        public LootCorpse CurrentLooting { get; private set; }

        PlayerController player;
        GUIStyle windowStyle, titleStyle, itemStyle, coinStyle, btnStyle;
        Texture2D winBgTex, borderTex;

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

        public static void SpawnLoot(Vector3 pos, string enemyName, int gold, int boneShards, string itemId)
        {
            if (!Instance)
            {
                var go = new GameObject("LootDropSystem");
                Instance = go.AddComponent<LootDropSystem>();
            }

            var corpse = new LootCorpse
            {
                id = System.Guid.NewGuid().ToString(),
                name = enemyName,
                position = pos,
                gold = gold,
                boneShards = boneShards,
                spawnTime = Time.time
            };

            if (!string.IsNullOrEmpty(itemId))
            {
                corpse.items.Add(itemId);
            }

            // Create sparkling visual diamond beacon
            var marker = new GameObject($"LootSparkle_{enemyName}");
            marker.transform.position = pos + Vector3.up * 0.45f;
            var mf = marker.AddComponent<MeshFilter>();
            mf.sharedMesh = GetDiamondMesh();
            var mr = marker.AddComponent<MeshRenderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.SetColor("_BaseColor", new Color(1f, 0.88f, 0.35f));
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.2f) * 2.2f);
                mr.sharedMaterial = mat;
            }

            var light = marker.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.85f, 0.25f);
            light.range = 3.2f;
            light.intensity = 2.0f;

            var col = marker.AddComponent<SphereCollider>();
            col.radius = 0.65f;
            col.isTrigger = true;
            var clickTarget = marker.AddComponent<LootClickTarget>();
            clickTarget.corpse = corpse;

            marker.AddComponent<LootSparkleAnim>();
            corpse.markerObject = marker;

            Instance.activeCorpses.Add(corpse);
        }

        void Update()
        {
            if (!player) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            if (!player || FrontendMenu.IsOpen) return;

            // Despawn old unlooted corpses after 120s
            bool expired = false;
            for (int i = activeCorpses.Count - 1; i >= 0; i--)
            {
                var c = activeCorpses[i];
                if (Time.time - c.spawnTime > 120f)
                {
                    if (c.markerObject) Destroy(c.markerObject);
                    activeCorpses.RemoveAt(i);
                    expired = true;
                }
            }
            if (expired) player.SaveAdventure(false);

            // Close looting if moved away
            if (CurrentLooting != null)
            {
                if (Vector3.Distance(player.transform.position, CurrentLooting.position) > 4.2f || !player.Stats.Alive)
                {
                    CloseLoot();
                }
                else if (Time.time > openedAt + 0.25f && Keyboard.current != null)
                {
                    if (Keyboard.current.escapeKey.wasPressedThisFrame)
                        CloseLoot();
                    else if (Keyboard.current.eKey.wasPressedThisFrame)
                        LootAll(CurrentLooting);
                }
            }
        }

        public LootCorpse GetNearbyCorpse(Vector3 playerPos, float maxDist = 3.2f)
        {
            LootCorpse closest = null;
            float bestDist = maxDist;
            for (int i = 0; i < activeCorpses.Count; i++)
            {
                float d = Vector3.Distance(playerPos, activeCorpses[i].position);
                if (d < bestDist)
                {
                    bestDist = d;
                    closest = activeCorpses[i];
                }
            }
            return closest;
        }

        float openedAt;
        public void OpenLoot(LootCorpse corpse)
        {
            CurrentLooting = corpse;
            openedAt = Time.time;
            AudioEffects.PlayClick();
        }

        public void CloseLoot()
        {
            CurrentLooting = null;
        }

        public void LootAll(LootCorpse corpse)
        {
            if (corpse == null || !activeCorpses.Contains(corpse)) return;
            if (!player) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            if (!player) return;

            string summary = "";
            if (corpse.gold > 0)
            {
                int g = corpse.gold;
                if (player.CharacterRace == "Human") g = Mathf.RoundToInt(g * 1.25f);
                corpse.gold = 0;
                player.AddGoldDirect(g);
                ChatSystem.LogLoot($"{g} Gold", "common");
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.8f, $"+{g} Gold", new Color(1f, 0.88f, 0.25f), false);
                summary += $"{g} Gold";
            }

            if (corpse.boneShards > 0)
            {
                int shards = corpse.boneShards;
                corpse.boneShards = 0;
                player.AddBoneShardsDirect(shards);
                ChatSystem.LogLoot($"{shards} Bone Shards", "common");
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.1f, $"+{shards} Bone Shards", new Color(0.85f, 0.85f, 0.8f), false);
                if (summary.Length > 0) summary += ", ";
                summary += $"{shards} Bone Shards";
            }

            for (int i = corpse.items.Count - 1; i >= 0; i--)
            {
                string id = corpse.items[i];
                corpse.items.RemoveAt(i);
                AwardItem(id);
                var gear = ItemCatalog.Get(id);
                if (gear != null)
                {
                    CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.4f, $"★ {gear.name} ★", gear.color, true);
                    if (summary.Length > 0) summary += ", ";
                    summary += gear.name;
                }
            }

            if (!string.IsNullOrEmpty(summary))
                player.Message = $"Looted: {summary}";

            AudioEffects.PlayLootCoin();

            // Cleanup corpse
            if (corpse.markerObject) Destroy(corpse.markerObject);
            activeCorpses.Remove(corpse);
            CurrentLooting = null;
            player.SaveAdventure(false);
        }

        void AwardItem(string id)
        {
            var gear = ItemCatalog.Get(id);
            if (gear == null) return;
            if ((gear.rarity == "Epic" || gear.rarity == "Legendary") && PartySystem.Instance && PartySystem.Instance.Members.Count > 0)
                LootRollSystem.PromptRoll(gear.id, gear.name, gear.rarity, gear.Stats);
            else
            {
                player.GrantItem(id);
                ChatSystem.LogLoot(gear.name, gear.rarity.ToLowerInvariant());
            }
        }

        public void LootItem(LootCorpse corpse, string itemId)
        {
            if (corpse == null || !player || !activeCorpses.Contains(corpse) || !corpse.items.Contains(itemId)) return;
            corpse.items.Remove(itemId);
            AwardItem(itemId);
            player.SaveAdventure(false);
            AudioEffects.PlayLootCoin();

            if (corpse.gold <= 0 && corpse.boneShards <= 0 && corpse.items.Count == 0)
            {
                if (corpse.markerObject) Destroy(corpse.markerObject);
                activeCorpses.Remove(corpse);
                CurrentLooting = null;
            }
        }

        public void LootCoins(LootCorpse corpse)
        {
            if (corpse == null || !player || !activeCorpses.Contains(corpse) || corpse.gold <= 0) return;
            int g = corpse.gold;
            if (player.CharacterRace == "Human") g = Mathf.RoundToInt(g * 1.25f);
            corpse.gold = 0;
            player.AddGoldDirect(g);
            ChatSystem.LogLoot($"{g} Gold", "common");
            AudioEffects.PlayLootCoin();

            if (corpse.boneShards <= 0 && corpse.items.Count == 0)
            {
                if (corpse.markerObject) Destroy(corpse.markerObject);
                activeCorpses.Remove(corpse);
                CurrentLooting = null;
            }
        }

        public void DrawLootGUI(float screenW, float screenH)
        {
            if (CurrentLooting == null || FrontendMenu.IsOpen) return;

            InitStyles();

            float winW = 280f;
            float winH = 200f;
            float x = screenW / 2f - winW / 2f;
            float y = screenH / 2f - winH / 2f;

            // Background & border
            GUI.Box(new Rect(x, y, winW, winH), GUIContent.none, windowStyle);

            // Title
            GUI.Label(new Rect(x + 12f, y + 8f, winW - 50f, 24f), CurrentLooting.name.ToUpperInvariant() + " - LOOT", titleStyle);
            if (GUI.Button(new Rect(x + winW - 32f, y + 8f, 22f, 22f), "X", btnStyle))
            {
                CloseLoot();
                return;
            }

            float rowY = y + 38f;

            // Gold Coins Row
            if (CurrentLooting.gold > 0)
            {
                GUI.Label(new Rect(x + 14f, rowY, winW - 90f, 24f), $"● {CurrentLooting.gold} Gold Coins", coinStyle);
                if (GUI.Button(new Rect(x + winW - 74f, rowY, 60f, 22f), "Take", btnStyle))
                {
                    LootCoins(CurrentLooting);
                }
                rowY += 28f;
            }

            // Bone Shards Row
            if (CurrentLooting.boneShards > 0)
            {
                GUI.Label(new Rect(x + 14f, rowY, winW - 90f, 24f), $"● {CurrentLooting.boneShards} Bone Shards", itemStyle);
                rowY += 28f;
            }

            // Items Rows
            for (int i = 0; i < CurrentLooting.items.Count; i++)
            {
                string id = CurrentLooting.items[i];
                var gear = ItemCatalog.Get(id);
                string itemName = gear != null ? gear.name : id;

                itemStyle.normal.textColor = gear != null ? gear.color : Color.white;
                GUI.Label(new Rect(x + 14f, rowY, winW - 90f, 24f), $"◆ {itemName}", itemStyle);
                if (GUI.Button(new Rect(x + winW - 74f, rowY, 60f, 22f), "Take", btnStyle))
                {
                    LootItem(CurrentLooting, id);
                    break;
                }
                rowY += 28f;
            }

            // [Loot All] button at bottom
            if (GUI.Button(new Rect(x + 14f, y + winH - 36f, winW - 28f, 28f), "LOOT ALL [E]", btnStyle))
            {
                LootAll(CurrentLooting);
            }
        }

        void InitStyles()
        {
            if (windowStyle != null) return;

            winBgTex = new Texture2D(1, 1);
            winBgTex.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.12f, 0.94f));
            winBgTex.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.35f, 0.28f, 0.15f, 0.95f));
            borderTex.Apply();

            windowStyle = new GUIStyle();
            windowStyle.normal.background = winBgTex;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            titleStyle.normal.textColor = new Color(1.0f, 0.85f, 0.35f);

            itemStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            itemStyle.normal.textColor = Color.white;

            coinStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            coinStyle.normal.textColor = new Color(1.0f, 0.88f, 0.25f);

            btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        static Mesh s_diamondMesh;
        public static Mesh GetDiamondMesh()
        {
            if (s_diamondMesh != null) return s_diamondMesh;
            var mesh = new Mesh { name = "LootSparkleDiamond" };
            Vector3 top = new Vector3(0f, 0.22f, 0f);
            Vector3 bottom = new Vector3(0f, -0.22f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 0.11f);
            Vector3 back = new Vector3(0f, 0f, -0.11f);
            Vector3 right = new Vector3(0.11f, 0f, 0f);
            Vector3 left = new Vector3(-0.11f, 0f, 0f);

            mesh.vertices = new Vector3[]
            {
                top, right, fwd,
                top, fwd, left,
                top, left, back,
                top, back, right,
                bottom, fwd, right,
                bottom, left, fwd,
                bottom, back, left,
                bottom, right, back
            };
            mesh.triangles = new int[]
            {
                0, 1, 2,
                3, 4, 5,
                6, 7, 8,
                9, 10, 11,
                12, 13, 14,
                15, 16, 17,
                18, 19, 20,
                21, 22, 23
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            s_diamondMesh = mesh;
            return mesh;
        }
    }

    public class LootSparkleAnim : MonoBehaviour
    {
        Vector3 basePos;
        Light ptLight;
        void Start()
        {
            basePos = transform.position;
            ptLight = GetComponent<Light>();
        }
        void Update()
        {
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 3.5f) * 0.08f);
            transform.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);
            if (ptLight != null)
            {
                ptLight.intensity = 1.8f + Mathf.Sin(Time.time * 5f) * 0.6f;
            }
        }
    }
    public class LootClickTarget : MonoBehaviour
    {
        public LootDropSystem.LootCorpse corpse;
    }
}
