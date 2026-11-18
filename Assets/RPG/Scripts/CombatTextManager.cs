using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class CombatTextManager : MonoBehaviour
    {
        public static CombatTextManager Instance { get; private set; }

        class CombatTextItem
        {
            public Vector3 worldPos;
            public Vector3 velocity;
            public string text;
            public Color color;
            public float spawnTime;
            public float duration;
            public bool isCrit;
        }

        List<CombatTextItem> items = new List<CombatTextItem>();
        GUIStyle textStyle, critStyle;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
        }

        public static void Spawn(Vector3 worldPos, string text, Color color, bool isCrit = false)
        {
            if (!Instance)
            {
                var go = new GameObject("CombatTextManager");
                go.AddComponent<CombatTextManager>();
            }

            Vector3 vel = new Vector3(
                UnityEngine.Random.Range(-0.35f, 0.35f),
                isCrit ? 1.8f : 1.2f,
                UnityEngine.Random.Range(-0.35f, 0.35f)
            );

            Instance.items.Add(new CombatTextItem
            {
                worldPos = worldPos + Vector3.up * 1.6f,
                velocity = vel,
                text = text,
                color = color,
                spawnTime = Time.time,
                duration = isCrit ? 1.4f : 1.1f,
                isCrit = isCrit
            });
        }

        public static void SpawnDamage(Vector3 worldPos, float amount, bool isCrit = false)
        {
            int val = Mathf.RoundToInt(amount);
            string str = isCrit ? $"{val}* CRIT!" : val.ToString();
            Color col = isCrit ? new Color(1.0f, 0.85f, 0.1f) : new Color(1.0f, 0.95f, 0.85f);
            Spawn(worldPos, str, col, isCrit);
        }

        public static void SpawnHeal(Vector3 worldPos, float amount)
        {
            int val = Mathf.RoundToInt(amount);
            Spawn(worldPos, $"+{val}", new Color(0.2f, 1.0f, 0.3f), false);
        }

        public static void SpawnMiss(Vector3 worldPos, string status = "MISS")
        {
            Spawn(worldPos, status, new Color(0.7f, 0.7f, 0.7f), false);
        }

        public static void SpawnBlock(Vector3 worldPos)
        {
            Spawn(worldPos, "BLOCKED", new Color(0.4f, 0.75f, 1.0f), true);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                item.worldPos += item.velocity * dt;
                item.velocity.y *= 0.96f; // slight deceleration
                if (Time.time - item.spawnTime >= item.duration)
                {
                    items.RemoveAt(i);
                }
            }
        }

        public void DrawCombatTextGUI()
        {
            if (FrontendMenu.IsOpen || !Camera.main || items.Count == 0) return;

            InitStyles();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                float age = Time.time - item.spawnTime;
                float progress = Mathf.Clamp01(age / item.duration);
                float alpha = progress > 0.65f ? Mathf.Clamp01(1f - (progress - 0.65f) / 0.35f) : 1f;

                Vector3 screenPoint = Camera.main.WorldToScreenPoint(item.worldPos);
                if (screenPoint.z <= 0.1f) continue;

                float screenX = screenPoint.x;
                float screenY = Screen.height - screenPoint.y;

                GUIStyle style = item.isCrit ? critStyle : textStyle;
                Color col = item.color;
                col.a = alpha;

                // Black outline / shadow for crisp readability
                Color shadowCol = new Color(0f, 0f, 0f, alpha * 0.9f);
                style.normal.textColor = shadowCol;
                GUI.Label(new Rect(screenX - 99f, screenY - 14f, 200f, 30f), item.text, style);
                GUI.Label(new Rect(screenX - 101f, screenY - 16f, 200f, 30f), item.text, style);

                // Main colored text
                style.normal.textColor = col;
                GUI.Label(new Rect(screenX - 100f, screenY - 15f, 200f, 30f), item.text, style);
            }
        }

        void InitStyles()
        {
            if (textStyle != null) return;

            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            critStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 23,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
