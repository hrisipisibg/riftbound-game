using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class WorldShrineSystem : MonoBehaviour
    {
        public static WorldShrineSystem Instance { get; private set; }

        public enum ShrineType { Fury, Haste, Fortitude, Fortune }

        class ShrineNode
        {
            public ShrineType type;
            public string name;
            public string desc;
            public Color color;
            public Vector3 position;
            public GameObject root;
            public Transform crystal;
            public float respawnAt;
        }

        readonly List<ShrineNode> shrines = new List<ShrineNode>();
        PlayerController player;
        ShrineNode nearbyShrine;

        static ShrineType activeBuff;
        static float buffExpires;

        GUIStyle promptStyle, buffStyle;
        Texture2D whiteTex;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            Vector3 anchor = player ? player.transform.position : Vector3.zero;
            SpawnShrine(ShrineType.Fury, "Shrine of Fury", "+35% Damage & +20% Crit (45s)", new Color(1f, 0.32f, 0.18f), anchor + new Vector3(18f, 0f, 14f));
            SpawnShrine(ShrineType.Haste, "Shrine of Haste", "+35% Move Speed (45s)", new Color(0.25f, 0.9f, 1f), anchor + new Vector3(-22f, 0f, 18f));
            SpawnShrine(ShrineType.Fortitude, "Shrine of Fortitude", "Full Heal & +35% Armor (45s)", new Color(0.3f, 0.95f, 0.45f), anchor + new Vector3(26f, 0f, -20f));
            SpawnShrine(ShrineType.Fortune, "Shrine of Fortune", "+50% Bonus XP & Gold (60s)", new Color(0.78f, 0.45f, 1f), anchor + new Vector3(-24f, 0f, -24f));
        }

        void SpawnShrine(ShrineType type, string shrineName, string desc, Color col, Vector3 xzPos)
        {
            float y = WorldRegions.GroundHeight(xzPos);
            Vector3 pos = new Vector3(xzPos.x, y, xzPos.z);

            var root = new GameObject($"WorldShrine_{type}");
            root.transform.position = pos;

            var basePedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            basePedestal.name = "Pedestal";
            basePedestal.transform.SetParent(root.transform, false);
            basePedestal.transform.localPosition = new Vector3(0f, 0.35f, 0f);
            basePedestal.transform.localScale = new Vector3(1.2f, 0.35f, 1.2f);
            basePedestal.GetComponent<Renderer>().sharedMaterial = SpellFX.CreateUnlitMat(new Color(0.22f, 0.2f, 0.24f, 1f));

            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Crystal";
            var cCol = crystal.GetComponent<Collider>();
            if (cCol) Destroy(cCol);
            crystal.transform.SetParent(root.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 1.45f, 0f);
            crystal.transform.localScale = new Vector3(0.45f, 0.85f, 0.45f);
            crystal.transform.localRotation = Quaternion.Euler(25f, 45f, 25f);
            crystal.GetComponent<Renderer>().sharedMaterial = SpellFX.CreateUnlitMat(col);

            var light = crystal.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = col;
            light.range = 7f;
            light.intensity = 2.8f;

            shrines.Add(new ShrineNode
            {
                type = type,
                name = shrineName,
                desc = desc,
                color = col,
                position = pos,
                root = root,
                crystal = crystal.transform
            });
        }

        void Update()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || FrontendMenu.IsOpen) return;

            if (Time.time >= buffExpires && activeBuff == ShrineType.Fortitude && player.Stats)
                player.Stats.incomingDamageMultiplier = 1f;

            nearbyShrine = null;
            float bestDist = 4.2f;

            for (int i = 0; i < shrines.Count; i++)
            {
                var s = shrines[i];
                bool ready = Time.time >= s.respawnAt;
                if (s.crystal)
                {
                    s.crystal.gameObject.SetActive(ready);
                    if (ready)
                    {
                        s.crystal.Rotate(Vector3.up * (55f * Time.deltaTime), Space.World);
                        s.crystal.localPosition = new Vector3(0f, 1.45f + Mathf.Sin(Time.time * 2.5f + i) * 0.14f, 0f);
                    }
                }
                if (!ready) continue;

                float d = Vector3.Distance(player.transform.position, s.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    nearbyShrine = s;
                }
            }

            if (nearbyShrine != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                ActivateShrine(nearbyShrine);
        }

        void ActivateShrine(ShrineNode s)
        {
            s.respawnAt = Time.time + 90f;
            activeBuff = s.type;
            float dur = s.type == ShrineType.Fortune ? 60f : 45f;
            buffExpires = Time.time + dur;

            if (player.Stats)
            {
                player.Stats.incomingDamageMultiplier = s.type == ShrineType.Fortitude ? 0.65f : 1f;
                if (s.type == ShrineType.Fortitude) player.Stats.Heal(player.Stats.maxHealth);
            }

            SpellFX.CreateExpandingRing(s.position, 0.4f, 4.5f, 0.6f, s.color, 0.25f);
            SpellFX.SpawnBurst(s.position + Vector3.up * 1.4f, s.color, Color.white, 30, 2.5f, 6.5f, 0.12f, 0.28f, 0.8f);
            OrbitCamera.Shake(0.14f, 0.18f);
            AudioEffects.PlayLevelUp();
            ZoneBanner.Show(s.name.ToUpperInvariant(), s.desc);
            ChatSystem.LogSystem($"Activated {s.name}: {s.desc}");
        }

        public static float GetDamageMultiplier() => (Time.time < buffExpires && activeBuff == ShrineType.Fury) ? 1.35f : 1f;
        public static float GetCritBonus() => (Time.time < buffExpires && activeBuff == ShrineType.Fury) ? 0.20f : 0f;
        public static float GetSpeedMultiplier() => (Time.time < buffExpires && activeBuff == ShrineType.Haste) ? 1.35f : 1f;
        public static float GetRewardMultiplier() => (Time.time < buffExpires && activeBuff == ShrineType.Fortune) ? 1.50f : 1f;

        void OnGUI()
        {
            if (FrontendMenu.IsOpen || !player) return;
            if (whiteTex == null) whiteTex = Texture2D.whiteTexture;

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float w = Screen.width / scale;
            float h = Screen.height / scale;

            if (nearbyShrine != null)
            {
                if (promptStyle == null)
                {
                    promptStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 14,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    promptStyle.normal.textColor = new Color(1f, 0.92f, 0.6f);
                }
                float pw = 290f, ph = 32f;
                Rect r = new Rect((w - pw) * 0.5f, h * 0.64f, pw, ph);
                var prev = GUI.color;
                GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.88f);
                GUI.DrawTexture(r, whiteTex);
                GUI.color = nearbyShrine.color;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), whiteTex);
                GUI.color = Color.white;
                GUI.Label(r, $"[E] Activate {nearbyShrine.name}", promptStyle);
                GUI.color = prev;
            }

            if (Time.time < buffExpires)
            {
                if (buffStyle == null)
                {
                    buffStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 12,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    buffStyle.normal.textColor = Color.white;
                }
                float rem = buffExpires - Time.time;
                Rect br = new Rect(24f, 80f, 180f, 22f);
                var prev = GUI.color;
                GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.84f);
                GUI.DrawTexture(br, whiteTex);
                GUI.color = new Color(0.9f, 0.75f, 0.25f, 0.9f);
                GUI.DrawTexture(new Rect(br.x, br.y, br.width, 1.5f), whiteTex);
                GUI.color = Color.white;
                GUI.Label(br, $"✦ Shrine of {activeBuff} ({Mathf.CeilToInt(rem)}s)", buffStyle);
                GUI.color = prev;
            }

            GUI.matrix = oldMatrix;
        }
    }
}
