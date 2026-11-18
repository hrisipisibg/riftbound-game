using UnityEngine;

namespace Riftbound
{
    public class CombatStreakSystem : MonoBehaviour
    {
        public static CombatStreakSystem Instance { get; private set; }

        int streak;
        float streakExpires;
        const float StreakWindow = 10f;
        GUIStyle titleStyle;
        Texture2D whiteTex;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        public static void RegisterKill(Vector3 enemyPos)
        {
            if (!Instance) return;
            if (Time.time <= Instance.streakExpires) Instance.streak++;
            else Instance.streak = 1;
            Instance.streakExpires = Time.time + StreakWindow;

            if (Instance.streak >= 2)
            {
                string label = GetLabel(Instance.streak);
                Color col = Instance.streak >= 4 ? new Color(1f, 0.35f, 0.15f) : new Color(1f, 0.82f, 0.2f);
                CombatTextManager.Spawn(enemyPos + Vector3.up * 2.6f, label, col, true);
                if (Instance.streak >= 3)
                    SpellFX.CreateExpandingRing(enemyPos, 0.3f, 2.8f, 0.35f, col, 0.16f);
            }
        }

        static string GetLabel(int count) => count switch
        {
            2 => "DOUBLE KILL!",
            3 => "TRIPLE KILL!",
            4 => "RAMPAGE!",
            _ => $"{count}x UNSTOPPABLE!"
        };

        void OnGUI()
        {
            if (streak < 2 || Time.time >= streakExpires || FrontendMenu.IsOpen) return;

            if (whiteTex == null) whiteTex = Texture2D.whiteTexture;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 13,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float w = Screen.width / scale;

            float bw = 176f, bh = 28f;
            float bx = (w - bw) * 0.5f;
            float by = 74f;

            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.06f, 0.05f, 0.85f);
            GUI.DrawTexture(new Rect(bx, by, bw, bh), whiteTex);

            GUI.color = streak >= 4 ? new Color(1f, 0.35f, 0.18f, 0.95f) : new Color(0.95f, 0.78f, 0.24f, 0.95f);
            GUI.DrawTexture(new Rect(bx, by, bw, 1.5f), whiteTex);

            titleStyle.normal.textColor = streak >= 4 ? new Color(1f, 0.45f, 0.2f) : new Color(1f, 0.88f, 0.35f);
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by + 2f, bw, 18f), $"{GetLabel(streak)}", titleStyle);

            float remaining = Mathf.Clamp01((streakExpires - Time.time) / StreakWindow);
            GUI.color = new Color(0.2f, 0.15f, 0.12f, 0.9f);
            GUI.DrawTexture(new Rect(bx + 8f, by + bh - 6f, bw - 16f, 3f), whiteTex);
            GUI.color = streak >= 4 ? new Color(1f, 0.3f, 0.15f, 0.95f) : new Color(1f, 0.8f, 0.2f, 0.95f);
            GUI.DrawTexture(new Rect(bx + 8f, by + bh - 6f, (bw - 16f) * remaining, 3f), whiteTex);

            GUI.color = prev;
            GUI.matrix = oldMatrix;
        }
    }
}
