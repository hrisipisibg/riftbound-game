using System;
using UnityEngine;

namespace Riftbound
{
    public class DuelSystem : MonoBehaviour
    {
        static DuelSystem _instance;
        public static DuelSystem Instance
        {
            get
            {
                if (!_instance) _instance = FindAnyObjectByType<DuelSystem>();
                return _instance;
            }
            private set => _instance = value;
        }

        public enum DuelState
        {
            Inactive,
            Countdown,
            Sparring,
            Concluded
        }

        public DuelState State { get; private set; } = DuelState.Inactive;
        public Combatant Opponent { get; private set; }
        public Vector3 DuelCenter { get; private set; }
        public const float DuelRadius = 24.0f;
        public const float OutOfBoundsAllowance = 5.0f;

        float countdownTimer;
        float outOfBoundsTimer;
        float concludedTimer;
        GameObject bannerInstance;
        PlayerController player;

        GUIStyle bannerLabelStyle, countdownStyle, warningStyle;
        Texture2D solidTex;

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

        void Update()
        {
            if (!player) player = GetComponent<PlayerController>() ?? FindAnyObjectByType<PlayerController>();
            if (State == DuelState.Inactive) return;

            if (State == DuelState.Countdown)
            {
                countdownTimer -= Time.deltaTime;
                if (countdownTimer <= 0f)
                {
                    State = DuelState.Sparring;
                    outOfBoundsTimer = 0f;
                    AudioEffects.PlayQuestComplete();
                    ChatSystem.LogCombat("<color=#ffd100>⚔ DUEL HAS BEGUN! Fight to the surrender!</color>");
                    if (ZoneBanner.Instance) ZoneBanner.Show("DUEL!", "Fight!");
                }
            }
            else if (State == DuelState.Sparring)
            {
                if (!player || !player.Stats || !player.Stats.Alive || !Opponent || !Opponent.Alive)
                {
                    ConcludeDuel(null, "Opponent lost or unavailable.");
                    return;
                }

                // Check Boundary
                float dist = Vector3.Distance(player.transform.position, DuelCenter);
                if (dist > DuelRadius)
                {
                    outOfBoundsTimer += Time.deltaTime;
                    if (outOfBoundsTimer >= OutOfBoundsAllowance)
                    {
                        Forfeit("Fled outside the duel perimeter.");
                        return;
                    }
                }
                else
                {
                    outOfBoundsTimer = Mathf.Max(0f, outOfBoundsTimer - Time.deltaTime * 2f);
                }

                // Check Sparring Surrender at 1 HP threshold
                if (Opponent.Health <= 1.5f)
                {
                    ConcludeDuel(player.Stats, $"{player.CharacterName} is victorious!");
                }
                else if (player.Stats.Health <= 1.5f)
                {
                    ConcludeDuel(Opponent, $"{Opponent.displayName} is victorious!");
                }
            }
            else if (State == DuelState.Concluded)
            {
                concludedTimer -= Time.deltaTime;
                if (concludedTimer <= 0f)
                {
                    CleanupBanner();
                    State = DuelState.Inactive;
                    Opponent = null;
                }
            }
        }

        public void ResetDuel()
        {
            CleanupBanner();
            State = DuelState.Inactive;
            Opponent = null;
            countdownTimer = 0f;
            outOfBoundsTimer = 0f;
        }

        public bool Challenge(Combatant target, out string reason)
        {
            if (State == DuelState.Concluded)
            {
                ResetDuel();
            }

            if (State != DuelState.Inactive)
            {
                reason = "A duel is already in progress!";
                return false;
            }

            if (!target || !target.Alive)
            {
                reason = "You must select a living target to challenge!";
                return false;
            }

            if (player && target == player.Stats)
            {
                reason = "You cannot duel yourself!";
                return false;
            }

            Opponent = target;
            DuelCenter = (player ? player.transform.position : Vector3.zero) * 0.5f + target.transform.position * 0.5f;
            countdownTimer = 3.2f;
            outOfBoundsTimer = 0f;
            State = DuelState.Countdown;

            SpawnDuelBanner(DuelCenter);

            string oppName = target.displayName ?? "Opponent";
            reason = $"Challenged {oppName} to a duel!";
            ChatSystem.LogSystem($"<color=#ffd100>⚔ Duel challenge issued to {oppName}! Starting in 3 seconds...</color>");
            AudioEffects.PlayClick();
            if (ZoneBanner.Instance) ZoneBanner.Show("DUEL CHALLENGE", $"Versus {oppName}");

            return true;
        }

        public void Forfeit(string reason = "Conceded")
        {
            if (State == DuelState.Inactive) return;
            string oppName = Opponent ? Opponent.displayName : "Opponent";
            ChatSystem.LogCombat($"<color=#ff4444>Duel forfeited: {reason}. {oppName} wins.</color>");
            ConcludeDuel(Opponent, reason);
        }

        void ConcludeDuel(Combatant winner, string reason)
        {
            State = DuelState.Concluded;
            concludedTimer = 3.5f;

            // Restore both fighters
            if (player && player.Stats) player.Stats.Restore();
            if (Opponent) Opponent.Restore();

            bool playerWon = (winner != null && player && winner == player.Stats);
            string oppName = Opponent ? Opponent.displayName : "Opponent";

            if (playerWon)
            {
                AudioEffects.PlayQuestComplete();
                ChatSystem.LogSystem($"<color=#ffd100>★ VICTORY! You defeated {oppName} in the duel!</color>");
                if (ZoneBanner.Instance) ZoneBanner.Show("DUEL VICTORY!", $"Defeated {oppName}");
            }
            else
            {
                ChatSystem.LogSystem($"<color=#ff6666>DEFEAT! {oppName} won the duel.</color>");
                if (ZoneBanner.Instance) ZoneBanner.Show("DUEL DEFEAT", $"{oppName} prevailed.");
            }
        }

        void SpawnDuelBanner(Vector3 pos)
        {
            CleanupBanner();
            bannerInstance = new GameObject("DuelBanner");
            if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            bannerInstance.transform.position = pos;

            var woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            woodMat.SetColor("_BaseColor", new Color(0.28f, 0.17f, 0.09f));
            woodMat.SetFloat("_Smoothness", 0.15f);

            var clothMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            clothMat.SetColor("_BaseColor", new Color(0.78f, 0.14f, 0.12f));
            clothMat.SetFloat("_Smoothness", 0.12f);
            if (clothMat.HasProperty("_Cull")) clothMat.SetFloat("_Cull", 0f);

            // Flag pole 1
            var pole1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole1.transform.SetParent(bannerInstance.transform, false);
            pole1.transform.localScale = new Vector3(0.08f, 1.6f, 0.08f);
            pole1.transform.localPosition = new Vector3(-0.15f, 1.6f, 0f);
            pole1.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
            pole1.GetComponent<Renderer>().sharedMaterial = woodMat;
            var col1 = pole1.GetComponent<Collider>(); if (col1) SafeDestroy(col1);

            // Flag pole 2
            var pole2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole2.transform.SetParent(bannerInstance.transform, false);
            pole2.transform.localScale = new Vector3(0.08f, 1.6f, 0.08f);
            pole2.transform.localPosition = new Vector3(0.15f, 1.6f, 0f);
            pole2.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            pole2.GetComponent<Renderer>().sharedMaterial = woodMat;
            var col2 = pole2.GetComponent<Collider>(); if (col2) SafeDestroy(col2);

            // Crest Banner
            var pennant = GameObject.CreatePrimitive(PrimitiveType.Quad);
            pennant.transform.SetParent(bannerInstance.transform, false);
            pennant.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            pennant.transform.localScale = new Vector3(1.2f, 0.9f, 1f);
            pennant.GetComponent<Renderer>().sharedMaterial = clothMat;
            var col3 = pennant.GetComponent<Collider>(); if (col3) SafeDestroy(col3);

            // Ground perimeter light
            var lightObj = new GameObject("DuelLight");
            lightObj.transform.SetParent(bannerInstance.transform, false);
            lightObj.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1.0f, 0.65f, 0.2f);
            light.range = 8f;
            light.intensity = 2.5f;
        }

        void CleanupBanner()
        {
            if (bannerInstance)
            {
                SafeDestroy(bannerInstance);
                bannerInstance = null;
            }
        }

        static void SafeDestroy(UnityEngine.Object obj)
        {
            if (!obj) return;
            if (Application.isPlaying) Destroy(obj);
            else DestroyImmediate(obj);
        }

        void OnDestroy()
        {
            CleanupBanner();
        }

        public void DrawDuelGUI(float screenWidth, float screenHeight)
        {
            if (State == DuelState.Inactive) return;

            InitStyles();

            // 1. Countdown Display
            if (State == DuelState.Countdown)
            {
                int seconds = Mathf.Max(1, Mathf.CeilToInt(countdownTimer));
                float cw = 320f, ch = 80f;
                float cx = (screenWidth - cw) * 0.5f, cy = screenHeight * 0.28f;

                Fill(new Rect(cx, cy, cw, ch), new Color(0.05f, 0.07f, 0.10f, 0.85f));
                GUI.Label(new Rect(cx, cy + 8f, cw, 28f), $"DUEL WITH {Opponent?.displayName.ToUpperInvariant()}", bannerLabelStyle);
                GUI.Label(new Rect(cx, cy + 34f, cw, 42f), $"STARTING IN {seconds}...", countdownStyle);
            }
            // 2. Sparring Active & Boundary Warning
            else if (State == DuelState.Sparring)
            {
                float bw = 240f, bh = 42f;
                float bx = (screenWidth - bw) * 0.5f, by = 18f;

                Fill(new Rect(bx, by, bw, bh), new Color(0.12f, 0.06f, 0.06f, 0.90f));
                GUI.Label(new Rect(bx, by + 4f, bw, 18f), $"⚔ DUEL: {Opponent?.displayName}", bannerLabelStyle);

                if (GUI.Button(new Rect(bx + 140f, by + 18f, 90f, 18f), "FORFEIT", GUI.skin.button))
                {
                    Forfeit("Conceded by player");
                }

                // Boundary warning
                if (outOfBoundsTimer > 0.5f)
                {
                    float ww = 380f, wh = 46f;
                    float wx = (screenWidth - ww) * 0.5f, wy = screenHeight * 0.35f;
                    Fill(new Rect(wx, wy, ww, wh), new Color(0.4f, 0.05f, 0.05f, 0.92f));
                    int remaining = Mathf.Max(0, Mathf.CeilToInt(OutOfBoundsAllowance - outOfBoundsTimer));
                    GUI.Label(new Rect(wx, wy + 8f, ww, 30f), $"⚠️ OUT OF BOUNDS! FORFEIT IN {remaining}s", warningStyle);
                }
            }
        }

        void InitStyles()
        {
            if (solidTex == null)
            {
                solidTex = new Texture2D(1, 1);
                solidTex.SetPixel(0, 0, Color.white);
                solidTex.Apply();
            }

            if (bannerLabelStyle == null)
            {
                bannerLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1.0f, 0.85f, 0.3f) }
                };
            }

            if (countdownStyle == null)
            {
                countdownStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1.0f, 0.45f, 0.2f) }
                };
            }

            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.yellow }
                };
            }
        }

        void Fill(Rect rect, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, solidTex);
            GUI.color = old;
        }
    }
}
