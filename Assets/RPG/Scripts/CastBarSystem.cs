using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class CastBarSystem : MonoBehaviour
    {
        public static CastBarSystem Instance { get; private set; }

        public bool IsCasting { get; private set; }
        public string SpellName { get; private set; } = "";
        public float CastDuration { get; private set; } = 1.5f;
        public float CastStartTime { get; private set; } = 0f;
        public Action OnComplete { get; private set; }
        public bool InterruptibleOnMove { get; private set; } = true;
        public bool InterruptibleOnDamage { get; private set; } = true;

        public class TargetCast
        {
            public Combatant target;
            public string spellName;
            public string icon;
            public float castDuration;
            public float castStartTime;
            public bool isInterruptible;
            public bool isInterrupted;
            public float interruptEndTime;
            public Action onComplete;

            public float Elapsed => Time.time - castStartTime;
            public float Progress => Mathf.Clamp01(Elapsed / Mathf.Max(0.1f, castDuration));
            public float Remaining => Mathf.Max(0f, castDuration - Elapsed);
        }

        readonly Dictionary<Combatant, TargetCast> targetCasts = new Dictionary<Combatant, TargetCast>();
        readonly List<Combatant> targetCastsCleanup = new List<Combatant>();

        Vector3 startPosition;
        PlayerController player;
        GUIStyle textStyle, timerStyle, targetCastStyle, interruptedStyle;
        Texture2D bgTex, fillTex, borderTex, interruptFillTex, interruptBorderTex, shieldFillTex, shieldBorderTex;

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

        public static void StartCast(string spellName, float duration, Action onComplete, bool interruptOnMove = true, bool interruptOnDamage = true)
        {
            if (!Instance)
            {
                var go = new GameObject("CastBarSystem");
                Instance = go.AddComponent<CastBarSystem>();
            }

            Instance.SpellName = spellName;
            Instance.CastDuration = Mathf.Max(0.1f, duration);
            Instance.CastStartTime = Time.time;
            Instance.OnComplete = onComplete;
            Instance.InterruptibleOnMove = interruptOnMove;
            Instance.InterruptibleOnDamage = interruptOnDamage;
            Instance.IsCasting = true;

            if (Instance.player)
                Instance.startPosition = Instance.player.transform.position;
        }

        public static void CancelCast(string reason = "Interrupted")
        {
            if (!Instance || !Instance.IsCasting) return;
            Instance.IsCasting = false;
            Instance.OnComplete = null;
            if (Instance.player)
                Instance.player.Message = reason;
        }

        public static void StartTargetCast(Combatant target, string spellName, string icon, float duration, Action onComplete, bool interruptible = true)
        {
            if (!target) return;
            if (!Instance)
            {
                var go = new GameObject("CastBarSystem");
                Instance = go.AddComponent<CastBarSystem>();
            }

            Instance.targetCasts[target] = new TargetCast
            {
                target = target,
                spellName = spellName,
                icon = string.IsNullOrEmpty(icon) ? "✦" : icon,
                castDuration = Mathf.Max(0.1f, duration),
                castStartTime = Time.time,
                onComplete = onComplete,
                isInterruptible = interruptible,
                isInterrupted = false
            };
        }

        public static bool InterruptTarget(Combatant target, string interruptSource = "Interrupt")
        {
            if (!Instance || !target) return false;
            if (Instance.targetCasts.TryGetValue(target, out var cast) && !cast.isInterrupted)
            {
                if (!cast.isInterruptible)
                {
                    if (Instance.player) Instance.player.Message = "Target's cast cannot be interrupted! [Shielded]";
                    return false;
                }

                cast.isInterrupted = true;
                cast.interruptEndTime = Time.time + 0.85f;
                cast.onComplete = null;
                return true;
            }
            return false;
        }

        public static bool IsTargetCasting(Combatant target)
        {
            if (!Instance || !target) return false;
            return Instance.targetCasts.TryGetValue(target, out var cast) && !cast.isInterrupted && cast.Elapsed < cast.castDuration;
        }

        public static TargetCast GetTargetCast(Combatant target)
        {
            if (!Instance || !target) return null;
            Instance.targetCasts.TryGetValue(target, out var cast);
            return cast;
        }

        public static void CancelTargetCast(Combatant target)
        {
            if (!Instance || !target) return;
            if (Instance.targetCasts.ContainsKey(target))
                Instance.targetCasts.Remove(target);
        }

        void Update()
        {
            UpdatePlayerCast();
            UpdateTargetCasts();
        }

        void UpdatePlayerCast()
        {
            if (!IsCasting) return;
            if (!player) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

            // Check if player died
            if (player && (!player.Stats || !player.Stats.Alive))
            {
                CancelCast("Cast Interrupted: Fallen in battle");
                return;
            }

            // Check movement interrupt
            if (InterruptibleOnMove && player)
            {
                if (Vector3.Distance(player.transform.position, startPosition) > 0.15f)
                {
                    CancelCast("Cast Interrupted: Movement");
                    return;
                }
            }

            // Check damage interrupt
            if (InterruptibleOnDamage && player && player.Stats)
            {
                if (player.Stats.LastHit >= CastStartTime)
                {
                    CancelCast("Cast Interrupted: Damaged");
                    return;
                }
            }

            // Check cast completion
            if (Time.time - CastStartTime >= CastDuration)
            {
                IsCasting = false;
                var cb = OnComplete;
                OnComplete = null;
                cb?.Invoke();
            }
        }

        void UpdateTargetCasts()
        {
            if (targetCasts.Count == 0) return;
            targetCastsCleanup.Clear();

            foreach (var kvp in targetCasts)
            {
                var target = kvp.Key;
                var cast = kvp.Value;

                if (!target || !target.Alive)
                {
                    targetCastsCleanup.Add(target);
                    continue;
                }

                if (cast.isInterrupted)
                {
                    if (Time.time >= cast.interruptEndTime)
                        targetCastsCleanup.Add(target);
                    continue;
                }

                if (cast.Elapsed >= cast.castDuration)
                {
                    var cb = cast.onComplete;
                    cast.onComplete = null;
                    targetCastsCleanup.Add(target);
                    cb?.Invoke();
                }
            }

            for (int i = 0; i < targetCastsCleanup.Count; i++)
                targetCasts.Remove(targetCastsCleanup[i]);
        }

        public void DrawCastBarGUI(float screenW, float screenH)
        {
            if (FrontendMenu.IsOpen) return;
            InitStyles();

            if (!player) player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();

            // --- 1. Player Cast Bar (Centered above Action Bar) ---
            if (IsCasting)
            {
                float barW = 260f;
                float barH = 22f;
                float cx = screenW / 2f - barW / 2f;
                float cy = screenH - 145f;

                float elapsed = Time.time - CastStartTime;
                float progress = Mathf.Clamp01(elapsed / CastDuration);
                float remaining = Mathf.Max(0f, CastDuration - elapsed);

                // Dark border & backing
                GUI.DrawTexture(new Rect(cx - 2f, cy - 2f, barW + 4f, barH + 4f), borderTex);
                GUI.DrawTexture(new Rect(cx, cy, barW, barH), bgTex);

                // Glowing Amber Fill bar
                GUI.DrawTexture(new Rect(cx, cy, barW * progress, barH), fillTex);

                // Spark indicator
                if (progress > 0.02f && progress < 0.98f)
                {
                    var oldColor = GUI.color;
                    GUI.color = new Color(1f, 1f, 0.8f, 0.85f);
                    GUI.DrawTexture(new Rect(cx + barW * progress - 2f, cy, 4f, barH), fillTex);
                    GUI.color = oldColor;
                }

                // Spell Name Label on the left
                GUI.Label(new Rect(cx + 8f, cy - 1f, barW - 60f, barH), SpellName, textStyle);

                // Remaining seconds on the right
                GUI.Label(new Rect(cx + barW - 55f, cy - 1f, 50f, barH), $"{remaining:F1}s", timerStyle);
            }

            // --- 2. Target Enemy Cast Bar (Directly below Target Unit Frame) ---
            if (player && player.Target && player.Target.Alive)
            {
                var target = player.Target;
                if (targetCasts.TryGetValue(target, out var tCast))
                {
                    float tfx = 258f;
                    float tfy = 78f;
                    float tfw = 205f;
                    float tfh = 18f;

                    if (tCast.isInterrupted)
                    {
                        // Flashing Crimson Interrupted Bar
                        GUI.DrawTexture(new Rect(tfx - 1.5f, tfy - 1.5f, tfw + 3f, tfh + 3f), interruptBorderTex);
                        GUI.DrawTexture(new Rect(tfx, tfy, tfw, tfh), interruptFillTex);
                        GUI.Label(new Rect(tfx, tfy, tfw, tfh), "⚡ [INTERRUPTED!]", interruptedStyle);
                    }
                    else
                    {
                        float tProg = tCast.Progress;
                        float tRem = tCast.Remaining;

                        // Border & Backing
                        Texture2D bdr = tCast.isInterruptible ? borderTex : shieldBorderTex;
                        GUI.DrawTexture(new Rect(tfx - 1.5f, tfy - 1.5f, tfw + 3f, tfh + 3f), bdr);
                        GUI.DrawTexture(new Rect(tfx, tfy, tfw, tfh), bgTex);

                        // Fill Bar
                        Texture2D fill = tCast.isInterruptible ? fillTex : shieldFillTex;
                        GUI.DrawTexture(new Rect(tfx, tfy, tfw * tProg, tfh), fill);

                        // Spark indicator
                        if (tProg > 0.02f && tProg < 0.98f)
                        {
                            var oldColor = GUI.color;
                            GUI.color = new Color(1f, 1f, 0.85f, 0.9f);
                            GUI.DrawTexture(new Rect(tfx + tfw * tProg - 1.5f, tfy, 3f, tfh), fillTex);
                            GUI.color = oldColor;
                        }

                        // Icon + Spell Name
                        string iconPrefix = string.IsNullOrEmpty(tCast.icon) ? "" : tCast.icon + " ";
                        string shieldSuffix = tCast.isInterruptible ? "" : " 🛡";
                        GUI.Label(new Rect(tfx + 5f, tfy - 1f, tfw - 48f, tfh), iconPrefix + tCast.spellName + shieldSuffix, targetCastStyle);

                        // Remaining seconds
                        GUI.Label(new Rect(tfx + tfw - 44f, tfy - 1f, 40f, tfh), $"{tRem:F1}s", timerStyle);
                    }
                }
            }
        }

        void InitStyles()
        {
            if (textStyle != null) return;

            bgTex = MakeTex(1, 1, new Color(0.06f, 0.08f, 0.1f, 0.95f));
            fillTex = MakeTex(1, 1, new Color(0.95f, 0.65f, 0.12f, 0.95f));
            borderTex = MakeTex(1, 1, new Color(0.35f, 0.28f, 0.14f, 1f));

            interruptFillTex = MakeTex(1, 1, new Color(0.85f, 0.15f, 0.15f, 0.95f));
            interruptBorderTex = MakeTex(1, 1, new Color(1f, 0.35f, 0.35f, 1f));

            shieldFillTex = MakeTex(1, 1, new Color(0.30f, 0.55f, 0.85f, 0.95f));
            shieldBorderTex = MakeTex(1, 1, new Color(0.60f, 0.75f, 0.95f, 1f));

            textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            textStyle.normal.textColor = Color.white;

            timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight
            };
            timerStyle.normal.textColor = new Color(0.9f, 0.9f, 0.85f);

            targetCastStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            targetCastStyle.normal.textColor = Color.white;

            interruptedStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            interruptedStyle.normal.textColor = Color.white;
        }

        Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; ++i) pix[i] = col;
            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }
    }
}
