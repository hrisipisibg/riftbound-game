using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class BattlegroundSystem : MonoBehaviour
    {
        static BattlegroundSystem _instance;
        public static BattlegroundSystem Instance
        {
            get
            {
                if (!_instance) _instance = FindAnyObjectByType<BattlegroundSystem>();
                return _instance;
            }
            private set => _instance = value;
        }

        public enum BGState
        {
            Idle,
            Queued,
            MatchReady,
            Preparation,
            Fighting,
            MatchEnded
        }

        public BGState State { get; private set; } = BGState.Idle;
        public bool IsWindowOpen { get; set; }

        public string ActiveBattleground = "Crossroads Skirmish Arena (3v3)";
        public int AllianceScore { get; private set; }
        public int HordeScore { get; private set; }
        public const int ScoreToWin = 5;

        public int PlayerHonorPoints = 0;
        public int ArenaMatchesWon = 0;

        float queueTimer;
        float matchReadyTimer;
        float prepTimer;
        float matchEndedTimer;
        Vector3 preMatchPosition = new Vector3(0f, 0.5f, 0f);

        PlayerController player;
        GUIStyle titleStyle, headerStyle, scoreStyle, timerStyle, btnStyle;
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

            if (State == BGState.Queued)
            {
                queueTimer += Time.deltaTime;
                // Match pops after 4 seconds for instant responsiveness
                if (queueTimer >= 4.0f)
                {
                    State = BGState.MatchReady;
                    matchReadyTimer = 25.0f;
                    AudioEffects.PlayQuestComplete();
                    ChatSystem.LogCombat("<color=#ffd100>★ BATTLEGROUND READY! Crossroads Skirmish Arena has opened.</color>");
                    if (ZoneBanner.Instance) ZoneBanner.Show("BATTLEGROUND READY!", "Crossroads Arena");
                }
            }
            else if (State == BGState.MatchReady)
            {
                matchReadyTimer -= Time.deltaTime;
                if (matchReadyTimer <= 0f)
                {
                    LeaveQueue("Queue expired.");
                }
            }
            else if (State == BGState.Preparation)
            {
                prepTimer -= Time.deltaTime;
                if (prepTimer <= 0f)
                {
                    State = BGState.Fighting;
                    AudioEffects.PlayQuestComplete();
                    ChatSystem.LogCombat("<color=#ff4444>⚔ THE ARENA GATES HAVE OPENED! Fight for glory!</color>");
                    if (ZoneBanner.Instance) ZoneBanner.Show("ARENA COMMENCED!", "First to 5 kills wins!");
                }
            }
            else if (State == BGState.Fighting)
            {
                // In sparring simulation or combat:
                // Check if victory condition met
                if (AllianceScore >= ScoreToWin)
                {
                    EndMatch(true);
                }
                else if (HordeScore >= ScoreToWin)
                {
                    EndMatch(false);
                }
            }
            else if (State == BGState.MatchEnded)
            {
                matchEndedTimer -= Time.deltaTime;
                if (matchEndedTimer <= 0f)
                {
                    ExitArena();
                }
            }
        }

        public void QueueForBattleground(string bgName = "Crossroads Skirmish Arena (3v3)")
        {
            if (State != BGState.Idle) return;
            ActiveBattleground = bgName;
            State = BGState.Queued;
            queueTimer = 0f;
            AudioEffects.PlayClick();
            ChatSystem.LogSystem($"<color=#ffd100>[PvP] You are queued for {ActiveBattleground}. Estimated wait: 5s.</color>");
        }

        public void LeaveQueue(string reason = "Left queue.")
        {
            State = BGState.Idle;
            queueTimer = 0f;
            AudioEffects.PlayClick();
            ChatSystem.LogSystem($"<color=#aaaaaa>[PvP] {reason}</color>");
        }

        public void EnterBattleground()
        {
            if (State != BGState.MatchReady) return;

            if (player) preMatchPosition = player.transform.position;

            State = BGState.Preparation;
            prepTimer = 5.0f;
            AllianceScore = 0;
            HordeScore = 0;
            IsWindowOpen = false;

            // Teleport to Crossroads Arena coordinates
            if (player)
            {
                Vector3 arenaPos = PlayerController.ClampAboveGround(new Vector3(0f, 3.5f, 120.0f), 0.35f);
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.transform.position = arenaPos;
                if (cc) cc.enabled = true;
            }

            AudioEffects.PlayLootCoin();
            ChatSystem.LogCombat($"<color=#ffd100>[PvP] Entered {ActiveBattleground}! Gates open in 5 seconds...</color>");
        }

        public void RecordKill(bool allianceScored)
        {
            if (State != BGState.Fighting) return;

            if (allianceScored)
            {
                AllianceScore++;
                ChatSystem.LogCombat($"<color=#44aaff>🦁 Alliance scored a kill! ({AllianceScore}/{ScoreToWin})</color>");
            }
            else
            {
                HordeScore++;
                ChatSystem.LogCombat($"<color=#ff4444>🐺 Horde scored a kill! ({HordeScore}/{ScoreToWin})</color>");
            }
        }

        void EndMatch(bool allianceWon)
        {
            State = BGState.MatchEnded;
            matchEndedTimer = 5.0f;

            if (allianceWon)
            {
                ArenaMatchesWon++;
                PlayerHonorPoints += 150;
                if (player) player.AddGoldDirect(50);
                PlayerTitleSystem.SetActiveTitle("Champion of the Valley");
                AudioEffects.PlayQuestComplete();
                ChatSystem.LogSystem("<color=#ffd100>★ ARENA VICTORY! +150 Honor Points, +50 Gold, and unlocked title '<Champion of the Valley>'!</color>");
                if (ZoneBanner.Instance) ZoneBanner.Show("VICTORY IN THE ARENA!", "+150 Honor • Title Unlocked");
            }
            else
            {
                PlayerHonorPoints += 50;
                ChatSystem.LogSystem("<color=#ff6666>ARENA DEFEAT! +50 Honor Points.</color>");
                if (ZoneBanner.Instance) ZoneBanner.Show("ARENA DEFEAT", "+50 Honor Points");
            }
        }

        public void ExitArena()
        {
            State = BGState.Idle;
            if (player)
            {
                Vector3 safeReturn = PlayerController.ClampAboveGround(preMatchPosition, 0.30f);
                var cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;
                player.transform.position = safeReturn;
                if (cc) cc.enabled = true;
                if (player.Stats) player.Stats.Restore();
            }
            ChatSystem.LogSystem("<color=#ffd100>[PvP] Returned to the world.</color>");
        }

        public void DrawBattlegroundGUI(float screenWidth, float screenHeight)
        {
            InitStyles();

            // 1. Queue Status Widget at Top Right (below minimap)
            if (State == BGState.Queued)
            {
                float qw = 220f, qh = 42f;
                float qx = screenWidth - qw - 16f, qy = 120f;
                Fill(new Rect(qx, qy, qw, qh), new Color(0.06f, 0.08f, 0.12f, 0.92f));
                Fill(new Rect(qx, qy, qw, 1.5f), new Color(0.2f, 0.6f, 1.0f, 0.9f));

                int secs = Mathf.FloorToInt(queueTimer);
                GUI.Label(new Rect(qx + 8f, qy + 4f, qw - 16f, 18f), $"⚔ Queued: {ActiveBattleground}", timerStyle);
                GUI.Label(new Rect(qx + 8f, qy + 20f, 120f, 18f), $"Time: 0:{secs:D2} (Avg 0:05)", GUI.skin.label);

                if (GUI.Button(new Rect(qx + qw - 70f, qy + 19f, 62f, 18f), "LEAVE", GUI.skin.button))
                {
                    LeaveQueue();
                }
            }

            // 2. Match Ready Modal Dialog
            if (State == BGState.MatchReady)
            {
                float mw = 360f, mh = 120f;
                float mx = (screenWidth - mw) * 0.5f, my = screenHeight * 0.28f;
                Fill(new Rect(mx, my, mw, mh), new Color(0.06f, 0.08f, 0.12f, 0.96f));
                Fill(new Rect(mx, my, mw, 2f), new Color(1.0f, 0.82f, 0.25f, 0.95f));

                GUI.Label(new Rect(mx, my + 10f, mw, 24f), "⚔ BATTLEGROUND READY!", titleStyle);
                GUI.Label(new Rect(mx, my + 34f, mw, 20f), ActiveBattleground, headerStyle);

                int remain = Mathf.CeilToInt(matchReadyTimer);
                GUI.Label(new Rect(mx, my + 54f, mw, 18f), $"Time remaining to enter: {remain}s", timerStyle);

                if (GUI.Button(new Rect(mx + 30f, my + 80f, 140f, 28f), "ENTER BATTLE", btnStyle))
                {
                    EnterBattleground();
                }
                if (GUI.Button(new Rect(mx + 190f, my + 80f, 140f, 28f), "LEAVE QUEUE", GUI.skin.button))
                {
                    LeaveQueue();
                }
            }

            // 3. In-Arena Scoreboard Banner at Top-Center
            if (State == BGState.Fighting || State == BGState.Preparation || State == BGState.MatchEnded)
            {
                float sw = 380f, sh = 56f;
                float sx = (screenWidth - sw) * 0.5f, sy = 16f;
                Fill(new Rect(sx, sy, sw, sh), new Color(0.05f, 0.06f, 0.09f, 0.94f));
                Fill(new Rect(sx, sy, sw, 1.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));

                if (State == BGState.Preparation)
                {
                    int psec = Mathf.CeilToInt(prepTimer);
                    GUI.Label(new Rect(sx, sy + 6f, sw, 22f), "ARENA GATES OPENING...", headerStyle);
                    GUI.Label(new Rect(sx, sy + 28f, sw, 24f), $"STARTING IN {psec}s", titleStyle);
                }
                else
                {
                    // Scoreboard
                    GUI.Label(new Rect(sx + 16f, sy + 6f, 140f, 22f), $"🦁 ALLIANCE: {AllianceScore}", headerStyle);
                    GUI.Label(new Rect(sx + sw - 156f, sy + 6f, 140f, 22f), $"🐺 HORDE: {HordeScore}", timerStyle);
                    GUI.Label(new Rect(sx, sy + 30f, sw, 20f), $"First to {ScoreToWin} kills wins  •  Honor: {PlayerHonorPoints} ★", GUI.skin.label);
                }
            }

            // 4. Battleground Queue Window (Open via [H] or Menu)
            if (IsWindowOpen)
            {
                float ww = 480f, wh = 320f;
                float wx = (screenWidth - ww) * 0.5f, wy = (screenHeight - wh) * 0.5f;

                Fill(new Rect(wx, wy, ww, wh), new Color(0.06f, 0.08f, 0.12f, 0.96f));
                Fill(new Rect(wx, wy, ww, 2.5f), new Color(0.85f, 0.70f, 0.28f, 0.95f));

                GUI.Label(new Rect(wx + 20f, wy + 14f, 350f, 26f), "⚔ PLAYER VS PLAYER & BATTLEGROUNDS", titleStyle);
                if (GUI.Button(new Rect(wx + ww - 38f, wy + 12f, 26f, 24f), "✕", GUI.skin.button))
                {
                    IsWindowOpen = false;
                    AudioEffects.PlayClick();
                    return;
                }

                // Battleground 1: Crossroads Arena
                Fill(new Rect(wx + 20f, wy + 55f, ww - 40f, 95f), new Color(0.10f, 0.13f, 0.18f, 0.92f));
                GUI.Label(new Rect(wx + 32f, wy + 62f, 300f, 20f), "<b>Crossroads Skirmish Arena (3v3)</b>", headerStyle);
                GUI.Label(new Rect(wx + 32f, wy + 84f, ww - 64f, 36f), "Fast-paced 3v3 arena skirmish in the Contested Crossroads. Defeat the opposing faction squad to claim Honor Points and PvP Titles.", GUI.skin.label);

                bool inArenaQueue = (State == BGState.Queued && ActiveBattleground.Contains("Crossroads"));
                string arenaBtnText = inArenaQueue ? "IN QUEUE..." : "JOIN BATTLE";
                if (GUI.Button(new Rect(wx + ww - 145f, wy + 115f, 115f, 26f), arenaBtnText, btnStyle))
                {
                    if (State == BGState.Idle) QueueForBattleground("Crossroads Skirmish Arena (3v3)");
                    else if (inArenaQueue) LeaveQueue();
                }

                // Battleground 2: Warsong Gulch
                Fill(new Rect(wx + 20f, wy + 160f, ww - 40f, 95f), new Color(0.10f, 0.13f, 0.18f, 0.92f));
                GUI.Label(new Rect(wx + 32f, wy + 167f, 300f, 20f), "<b>Warsong Gulch (Flag War)</b>", headerStyle);
                GUI.Label(new Rect(wx + 32f, wy + 189f, ww - 64f, 36f), "Capture the enemy faction's war banner and return it to your base while defending your own flag.", GUI.skin.label);

                bool inWSGQueue = (State == BGState.Queued && ActiveBattleground.Contains("Warsong"));
                string wsgBtnText = inWSGQueue ? "IN QUEUE..." : "JOIN BATTLE";
                if (GUI.Button(new Rect(wx + ww - 145f, wy + 220f, 115f, 26f), wsgBtnText, btnStyle))
                {
                    if (State == BGState.Idle) QueueForBattleground("Warsong Gulch (Flag War)");
                    else if (inWSGQueue) LeaveQueue();
                }

                // Footer stats
                GUI.Label(new Rect(wx + 24f, wy + wh - 35f, 300f, 20f), $"Total Honor: <b>{PlayerHonorPoints}</b> ★  •  Victories: <b>{ArenaMatchesWon}</b>", GUI.skin.label);
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

            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 15,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = new Color(1.0f, 0.85f, 0.35f) }
                };
            }

            if (headerStyle == null)
            {
                headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(0.35f, 0.75f, 1.0f) }
                };
            }

            if (timerStyle == null)
            {
                timerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = new Color(1.0f, 0.45f, 0.35f) }
                };
            }

            if (btnStyle == null)
            {
                btnStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 11,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
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
