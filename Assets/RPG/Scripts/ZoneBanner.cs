using UnityEngine;

namespace Riftbound
{
    public class ZoneBanner : MonoBehaviour
    {
        public static ZoneBanner Instance { get; private set; }

        string zoneTitle = "";
        string zoneSub = "";
        float showUntil = 0f;
        float fadeStartTime = 0f;
        const float DisplayDuration = 2.8f;
        const float FadeDuration = 0.8f;

        GUIStyle titleStyle, subStyle;
        Texture2D lineTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        public static void Show(string title, string subtitle)
        {
            if (!Instance)
            {
                Instance = UnityEngine.Object.FindAnyObjectByType<ZoneBanner>();
                if (!Instance)
                {
                    var go = new GameObject("ZoneBanner");
                    Instance = go.AddComponent<ZoneBanner>();
                }
            }
            if (Instance)
            {
                Instance.zoneTitle = title != null ? title.ToUpperInvariant() : "";
                Instance.zoneSub = subtitle ?? "";
                Instance.fadeStartTime = Time.time;
                Instance.showUntil = Time.time + DisplayDuration;
            }
        }

        public void DrawBannerGUI(float screenW, float screenH)
        {
            if (Time.time >= showUntil || FrontendMenu.IsOpen) return;

            InitStyles();

            float elapsed = Time.time - fadeStartTime;
            float remaining = showUntil - Time.time;

            float alpha = 1f;
            if (elapsed < FadeDuration)
                alpha = Mathf.Clamp01(elapsed / FadeDuration);
            else if (remaining < FadeDuration)
                alpha = Mathf.Clamp01(remaining / FadeDuration);

            Color oldColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);

            float cx = screenW / 2f;
            float cy = 150f;

            // Filigree top line

            // Grand zone title with shadow
            titleStyle.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.9f);
            GUI.Label(new Rect(cx - 280f + 1f, cy - 18f + 1f, 560f, 30f), zoneTitle, titleStyle);

            titleStyle.normal.textColor = new Color(.86f, .78f, .60f, alpha);
            GUI.Label(new Rect(cx - 280f, cy - 18f, 560f, 30f), zoneTitle, titleStyle);

            // Subtitle
            Color subColor = zoneSub.Contains("Alliance") ? new Color(0.35f, 0.75f, 1.0f, alpha) :
                             zoneSub.Contains("Horde") ? new Color(1.0f, 0.35f, 0.25f, alpha) :
                             zoneSub.Contains("Dungeon") ? new Color(1.0f, 0.2f, 0.2f, alpha) :
                             zoneSub.Contains("Apex") ? new Color(1.0f, 0.6f, 0.1f, alpha) :
                             new Color(0.9f, 0.9f, 0.5f, alpha);

            subStyle.normal.textColor = subColor;
            GUI.Label(new Rect(cx - 280f, cy + 11f, 560f, 20f), zoneSub, subStyle);

            // Filigree bottom line

            GUI.color = oldColor;
        }

        void InitStyles()
        {
            if (titleStyle != null) return;

            lineTex = new Texture2D(1, 1);
            lineTex.SetPixel(0, 0, new Color(0.9f, 0.75f, 0.35f, 0.8f));
            lineTex.Apply();

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
