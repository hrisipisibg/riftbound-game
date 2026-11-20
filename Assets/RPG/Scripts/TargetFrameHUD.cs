using UnityEngine;

namespace Riftbound
{
    public class TargetFrameHUD : MonoBehaviour
    {
        PlayerController player;
        Texture2D whiteTex;
        GUIStyle nameStyle, subStyle, hpStyle, bossStyle;

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        void EnsureStyles()
        {
            if (whiteTex == null) whiteTex = Texture2D.whiteTexture;
            if (nameStyle == null)
            {
                nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
                nameStyle.normal.textColor = new Color(0.96f, 0.94f, 0.88f);
                subStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold };
                subStyle.normal.textColor = new Color(0.9f, 0.76f, 0.32f);
                hpStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                hpStyle.normal.textColor = Color.white;
                bossStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                bossStyle.normal.textColor = new Color(1f, 0.86f, 0.42f);
            }
        }

        void OnGUI()
        {
            if (FrontendMenu.IsOpen) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || !player.Stats) return;

            EnsureStyles();

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float w = Screen.width / scale;

            // --- 1. Dodge [V] Readiness Pip ---
            DrawDodgePip(24f, 108f);

            // --- 2. Selected Target Unit Frame (Next to Player Frame) ---
            var target = player.Target;
            if (target && target.Alive)
            {
                DrawTargetFrame(target, 264f, 20f);
                var ec = target.GetComponent<EnemyController>();
                if (ec && ec.isBoss)
                    DrawBossBar(target, ec, w);
            }

            GUI.matrix = oldMatrix;
        }

        void DrawDodgePip(float x, float y)
        {
            float cd = player.DodgeCooldownRemaining;
            float w = 132f, h = 20f;
            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, w, h), whiteTex);

            float fill = cd <= 0.01f ? 1f : Mathf.Clamp01(1f - cd / 3.5f);
            GUI.color = cd <= 0.01f ? new Color(0.25f, 0.78f, 0.95f, 0.9f) : new Color(0.35f, 0.45f, 0.55f, 0.65f);
            GUI.DrawTexture(new Rect(x + 2f, y + h - 4f, (w - 4f) * fill, 2.5f), whiteTex);

            GUI.color = Color.white;
            subStyle.normal.textColor = cd <= 0.01f ? new Color(0.55f, 0.95f, 1f) : new Color(0.75f, 0.75f, 0.78f);
            GUI.Label(new Rect(x + 6f, y + 1f, w - 12f, 16f), cd <= 0.01f ? "[Alt] DODGE READY" : $"[Alt] DODGE ({cd:F1}s)", subStyle);
            GUI.color = prev;
        }

        void DrawTargetFrame(Combatant target, float x, float y)
        {
            float w = 224f, h = 52f;
            WowTheme.Panel(new Rect(x - 3f, y - 3f, w + 6f, h + 6f));

            var ec = target.GetComponent<EnemyController>();
            Color trim = (ec && ec.isBoss) ? new Color(1f, 0.75f, 0.18f, 0.95f) : new Color(0.85f, 0.25f, 0.2f, 0.9f);
            var prev = GUI.color;
            GUI.color = trim;
            GUI.DrawTexture(new Rect(x, y, w, 2f), whiteTex);

            string tag = "";
            if (ec)
            {
                int lvl = ec.isBoss ? 5 : ec.heavyAttacker ? 4 : ec.campGuard ? 3 : 2;
                tag = ec.isBoss ? $"Lv{lvl} BOSS" : (ec.Affix != EnemyController.EnemyAffix.None ? $"Lv{lvl} {ec.Affix}" : $"Lv{lvl}");
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 8f, y + 4f, w - 16f, 18f), target.displayName, nameStyle);
            subStyle.normal.textColor = (ec && ec.Affix != EnemyController.EnemyAffix.None) ? new Color(1f, 0.72f, 0.25f) : new Color(0.88f, 0.82f, 0.65f);
            if (!string.IsNullOrEmpty(tag))
            {
                subStyle.alignment = TextAnchor.MiddleRight;
                GUI.Label(new Rect(x + 8f, y + 5f, w - 16f, 16f), tag, subStyle);
                subStyle.alignment = TextAnchor.UpperLeft;
            }

            float pct = target.maxHealth > 0f ? Mathf.Clamp01(target.Health / target.maxHealth) : 0f;
            Rect bar = new Rect(x + 8f, y + 25f, w - 16f, 16f);
            GUI.color = new Color(0.14f, 0.06f, 0.06f, 0.95f);
            GUI.DrawTexture(bar, whiteTex);
            GUI.color = new Color(0.82f, 0.18f, 0.15f, 0.95f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * pct, bar.height), whiteTex);

            GUI.color = Color.white;
            GUI.Label(bar, $"{Mathf.CeilToInt(target.Health)} / {Mathf.CeilToInt(target.maxHealth)} ({Mathf.RoundToInt(pct * 100f)}%)", hpStyle);
            GUI.color = prev;
        }

        void DrawBossBar(Combatant target, EnemyController ec, float screenW)
        {
            float bw = 440f, bh = 36f;
            float bx = (screenW - bw) * 0.5f;
            float by = 18f;

            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.05f, 0.05f, 0.9f);
            GUI.DrawTexture(new Rect(bx - 3f, by - 3f, bw + 6f, bh + 6f), whiteTex);
            GUI.color = new Color(0.92f, 0.72f, 0.22f, 0.95f);
            GUI.DrawTexture(new Rect(bx - 3f, by - 3f, bw + 6f, 2f), whiteTex);

            string title = ec.Affix != EnemyController.EnemyAffix.None
                ? $"☠ {target.displayName.ToUpperInvariant()}  [{ec.Affix.ToString().ToUpperInvariant()}] ☠"
                : $"☠ {target.displayName.ToUpperInvariant()} ☠";
            GUI.color = Color.white;
            GUI.Label(new Rect(bx, by + 1f, bw, 18f), title, bossStyle);

            float pct = target.maxHealth > 0f ? Mathf.Clamp01(target.Health / target.maxHealth) : 0f;
            Rect bar = new Rect(bx + 10f, by + 20f, bw - 20f, 12f);
            GUI.color = new Color(0.18f, 0.06f, 0.06f, 1f);
            GUI.DrawTexture(bar, whiteTex);
            GUI.color = new Color(0.88f, 0.16f, 0.14f, 1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * pct, bar.height), whiteTex);
            GUI.color = prev;
        }
    }
}
