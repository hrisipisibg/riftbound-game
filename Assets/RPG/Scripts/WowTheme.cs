using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    /// <summary>WoW-style cursor and shared gold-trimmed IMGUI styling (buttons, panels).</summary>
    public static class WowTheme
    {
        public static readonly Color Gold = new Color(.86f, .68f, .27f);
        public static readonly Color GoldDark = new Color(.42f, .31f, .12f);
        public static readonly Color Parchment = new Color(.95f, .86f, .62f);

        static Texture2D cursorNormal, cursorPressed;
        static Texture2D btnNormal, btnHover, btnActive, btnPrimary, btnPrimaryHover, panelTex;
        static GUIStyle panelStyle;

        static readonly string[] Arrow =
        {
            "#           ",
            "##          ",
            "#o#         ",
            "#oo#        ",
            "#ooo#       ",
            "#oooo#      ",
            "#ooooo#     ",
            "#oooooo#    ",
            "#ooooooo#   ",
            "#oooooooo#  ",
            "#ooooo##### ",
            "#oo#oo#     ",
            "#o# #oo#    ",
            "##  #oo#    ",
            "#    #oo#   ",
            "     #oo#   ",
            "      ##    "
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            const int scale = 2;
            cursorNormal = BuildCursor(new Color(1f, .86f, .38f), scale);
            cursorPressed = BuildCursor(new Color(.78f, .55f, .16f), scale);
            Cursor.SetCursor(cursorNormal, new Vector2(1, 1) * scale, CursorMode.Auto);
            var go = new GameObject("WowCursor");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<CursorDriver>();
        }

        static Texture2D BuildCursor(Color fill, int scale)
        {
            int w = 12 * scale, h = Arrow.Length * scale;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int row = (h - 1 - y) / scale, col = x / scale;
                    char c = col < Arrow[row].Length ? Arrow[row][col] : ' ';
                    Color px = clear;
                    if (c == '#') px = new Color(.10f, .06f, .02f, 1f);
                    else if (c == 'o') px = Color.Lerp(fill, new Color(1f, .97f, .78f), 0.45f * (1f - (float)row / Arrow.Length));
                    tex.SetPixel(x, y, px);
                }
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            return tex;
        }

        class CursorDriver : MonoBehaviour
        {
            bool wasDown;
            void Update()
            {
                var m = Mouse.current;
                if (m == null) return;
                bool down = m.leftButton.isPressed || m.rightButton.isPressed;
                if (down == wasDown) return;
                wasDown = down;
                Cursor.SetCursor(down ? cursorPressed : cursorNormal, Vector2.one * 2f, CursorMode.Auto);
            }
        }

        // Gold-framed 9-slice texture: dark outline, gold trim, inner shadow, vertical gradient fill.
        static Texture2D Frame(Color top, Color bottom, Color trim)
        {
            const int n = 24;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, n - 1 - x), Mathf.Min(y, n - 1 - y));
                    Color c = Color.Lerp(bottom, top, y / (n - 1f));
                    if (d == 0) c = new Color(.04f, .025f, .01f);
                    else if (d == 1) c = trim;
                    else if (d == 2) c = Color.Lerp(trim, c, .7f) * .75f;
                    else if (d == 3) c *= .85f;
                    c.a = 1f;
                    t.SetPixel(x, y, c);
                }
            t.Apply();
            return t;
        }

        static void EnsureTextures()
        {
            if (btnNormal) return;
            btnNormal = Frame(new Color(.24f, .17f, .10f), new Color(.10f, .07f, .045f), GoldDark * 1.5f);
            btnHover = Frame(new Color(.38f, .27f, .13f), new Color(.17f, .11f, .06f), Gold);
            btnActive = Frame(new Color(.08f, .055f, .035f), new Color(.19f, .13f, .08f), Gold);
            btnPrimary = Frame(new Color(.50f, .13f, .08f), new Color(.22f, .05f, .03f), Gold);
            btnPrimaryHover = Frame(new Color(.68f, .20f, .10f), new Color(.30f, .08f, .04f), new Color(1f, .85f, .40f));
            panelTex = Frame(new Color(.10f, .09f, .08f, 1f), new Color(.045f, .04f, .035f, 1f), GoldDark * 1.4f);
        }

        /// <summary>Restyle a button style as a WoW stone/gold button. primary = red "Enter World" style.</summary>
        public static void StyleButton(GUIStyle s, bool primary = false)
        {
            EnsureTextures();
            var border = new RectOffset(6, 6, 6, 6);
            s.border = border;
            s.normal.background = primary ? btnPrimary : btnNormal;
            s.hover.background = primary ? btnPrimaryHover : btnHover;
            s.active.background = btnActive;
            s.focused.background = primary ? btnPrimaryHover : btnHover;
            s.normal.textColor = primary ? new Color(1f, .93f, .62f) : Parchment;
            s.hover.textColor = Color.white;
            s.active.textColor = Gold;
            s.focused.textColor = Color.white;
            s.fontStyle = FontStyle.Bold;
            s.alignment = TextAnchor.MiddleCenter;
        }

        /// <summary>Draws a gold-trimmed dark stone panel.</summary>
        public static void Panel(Rect r)
        {
            EnsureTextures();
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle { border = new RectOffset(6, 6, 6, 6) };
                panelStyle.normal.background = panelTex;
            }
            var prev = GUI.color;
            GUI.color = new Color(1, 1, 1, .96f);
            GUI.Box(r, GUIContent.none, panelStyle);
            GUI.color = prev;
        }
    }
}
