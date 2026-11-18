using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Riftbound.Network
{
    public class CoopWorldManager : MonoBehaviour
    {
        public const int MaxPlayers = 4;
        public static CoopWorldManager Instance { get; private set; }

        NetworkManager nm;
        UnityTransport utp;
        string statusText = "";
        bool shuttingDown;

        // Deliverables #5: Party Frames, Map Ping, Ready Check, Group Menu
        bool readyCheckOpen;
        string readyCheckInitiator = "";
        float readyCheckExpires;
        float readySummaryUntil;
        string activePingSender = "";
        Vector3 activePingWorldPos;
        float activePingExpires;
        bool hostDisconnected;
        string hostDisconnectMessage = "";
        bool groupMenuOpen;
        bool revealJoinCode;
        ulong selectedPeerClientId = ulong.MaxValue;
        readonly List<RiftboundCoopPlayer> sortedPeers = new List<RiftboundCoopPlayer>();
        GUIStyle rosterHeaderStyle, rosterSubStyle, rosterRightStyle;

        public static bool IsGroupMenuOpen => Instance && Instance.groupMenuOpen;
        public void SetGroupMenuOpenForTest(bool open) { groupMenuOpen = open; if (!open) revealJoinCode = false; }
        public void CloseGroupMenu() { groupMenuOpen = false; revealJoinCode = false; }

        public bool HasActivePingForTest => Time.time < activePingExpires;
        public bool IsReadyCheckOpenForTest => readyCheckOpen && Time.time < readyCheckExpires;
        public bool IsHostDisconnectedForTest => hostDisconnected;

        public bool TryGetActiveReadyCheck(out string initiator)
        {
            if (readyCheckOpen && Time.time < readyCheckExpires && !string.IsNullOrEmpty(readyCheckInitiator))
            {
                initiator = readyCheckInitiator;
                return true;
            }
            initiator = "";
            return false;
        }

        public void ShowMapPing(string sender, Vector3 pos)
        {
            activePingSender = sender;
            activePingWorldPos = pos;
            activePingExpires = Time.time + 8f;
            SpellFX.CreateArcaneBurst(pos + Vector3.up * 0.4f);
        }

        public void OpenReadyCheck(string initiator)
        {
            readyCheckInitiator = initiator;
            readyCheckOpen = true;
            readyCheckExpires = Time.time + 25f;
            readySummaryUntil = Time.time + 30f;
        }

        void Update()
        {
            if (!CoopSessionConfig.IsMultiplayer || FrontendMenu.IsOpen || RiftboundCoopPlayer.LocalOwner == null) return;
            if (readyCheckOpen && Time.time > readyCheckExpires) readyCheckOpen = false;
            var keys = Keyboard.current;
            if (keys == null) return;

            var localPc = FindFirstObjectByType<PlayerController>();
            bool chatOrTextOpen = localPc != null && (localPc.ChatOpen || localPc.DialogueOpen || localPc.ShopOpen);
            if (!chatOrTextOpen && keys.oKey.wasPressedThisFrame)
            {
                groupMenuOpen = !groupMenuOpen;
                if (!groupMenuOpen) revealJoinCode = false;
            }
            if (groupMenuOpen && keys.escapeKey.wasPressedThisFrame)
            {
                CloseGroupMenu();
                return;
            }

            bool uiBlocking = groupMenuOpen || (localPc != null && (localPc.ChatOpen || localPc.DialogueOpen || localPc.ShopOpen || localPc.InventoryOpen || localPc.MapOpen || localPc.JournalOpen || localPc.TalentsOpen || localPc.SpellbookOpen || localPc.AchievementOpen || localPc.ReputationOpen));
            if (uiBlocking || RiftboundCoopPlayer.LocalOwner.Health.Value <= 0) return;

            if (keys.f6Key.wasPressedThisFrame)
            {
                RiftboundCoopPlayer.LocalOwner.RequestMapPing(RiftboundCoopPlayer.LocalOwner.transform.position);
            }
            if (keys.f7Key.wasPressedThisFrame)
            {
                RiftboundCoopPlayer.LocalOwner.RequestReadyCheck();
            }
            // Note: [E] Ally Revive is handled cleanly through PlayerServices.TryGetBestInteraction / Interact() so [E] never double-fires alongside quests/NPCs/chests.
        }

        readonly Dictionary<ulong, HashSet<string>> claimedChestsByClient = new Dictionary<ulong, HashSet<string>>();
        readonly Dictionary<ulong, HashSet<int>> claimedQuestsByClient = new Dictionary<ulong, HashSet<int>>();
        readonly Dictionary<ulong, HashSet<string>> creditedKillsByClient = new Dictionary<ulong, HashSet<string>>();

        public bool TryClaimChestForClient(ulong clientId, string chestId)
        {
            if (string.IsNullOrEmpty(chestId)) return false;
            if (!claimedChestsByClient.TryGetValue(clientId, out var set))
                claimedChestsByClient[clientId] = set = new HashSet<string>();
            return set.Add(chestId);
        }

        public bool TryClaimQuestForClient(ulong clientId, int questIndex)
        {
            if (!claimedQuestsByClient.TryGetValue(clientId, out var set))
                claimedQuestsByClient[clientId] = set = new HashSet<int>();
            return set.Add(questIndex);
        }

        public bool TryCreditKillForClient(ulong clientId, string enemyKey)
        {
            if (string.IsNullOrEmpty(enemyKey)) return false;
            if (!creditedKillsByClient.TryGetValue(clientId, out var set))
                creditedKillsByClient[clientId] = set = new HashSet<string>();
            return set.Add(enemyKey);
        }

        static bool sceneHookRegistered = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnSceneLoaded()
        {
            if (!sceneHookRegistered)
            {
                sceneHookRegistered = true;
                SceneManager.sceneLoaded += (_, _) => EnsureManagerForActiveScene();
            }
            EnsureManagerForActiveScene();
        }

        static int clientLeaveStage = 0;
        static bool clientLeaveQuestAccepted = false;
        static bool clientLeaveMounted = false;
        static string clientLeaveRejoinCode = "";
        static bool clientLeaveRejoinOk = false;

        static void EnsureManagerForActiveScene()
        {
            CoopSessionConfig.ParseCommandLineOnce();
            string sceneName = SceneManager.GetActiveScene().name;
            if (!sceneName.Contains("Outpost")) return;
            bool resilienceActive = CoopSessionConfig.ResilienceSmoke && (resilienceStage == -2 || resilienceStage == 1 || resilienceStage == 3);
            bool clientLeaveActive = CoopSessionConfig.ClientLeaveSmoke && (clientLeaveStage == 1 || clientLeaveStage == 3);
            bool uiSmokeActive = CoopSessionConfig.UiSmoke && UiSmokeStage == 2;
            if (!CoopSessionConfig.IsMultiplayer && !resilienceActive && !clientLeaveActive && !uiSmokeActive) return;
            if (Instance != null) return;

            var go = new GameObject("CoopWorldManager");
            go.AddComponent<CoopWorldManager>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static int UiSmokeStage = 0;

        static int resilienceStage = -1;
        static string resilienceCode1 = "";
        static int resilienceNmCount = 0;
        static bool resilienceCancelOk = false;

        void Start()
        {
            if (CoopSessionConfig.UiSmoke)
            {
                if (UiSmokeStage == 1)
                {
                    Time.timeScale = 1f;
                    StartSelectedSessionMode();
                    StartCoroutine(RunUiSmokeWorldRoutine());
                    return;
                }
                if (UiSmokeStage == 2)
                {
                    StartCoroutine(FinishUiSmokeRoutine());
                    return;
                }
            }

            if (CoopSessionConfig.ClientLeaveSmoke && (clientLeaveStage == 1 || clientLeaveStage == 3))
            {
                StartCoroutine(FinishClientLeaveSmokeRoutine());
                return;
            }

            if (CoopSessionConfig.ResilienceSmoke)
            {
                if (resilienceStage == -2)
                {
                    resilienceCancelOk = OnlineSessionService.State == OnlineSessionState.Offline &&
                                         !OnlineSessionService.IsBusy &&
                                         FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length == 0;
                    resilienceStage = 0;
                    CoopSessionConfig.Mode = CoopSessionMode.HostOnline;
                }
                else if (resilienceStage == 1)
                {
                    resilienceStage = 2;
                    CoopSessionConfig.Mode = CoopSessionMode.HostOnline;
                }
                else if (resilienceStage == 3)
                {
                    StartCoroutine(FinishResilienceSmokeRoutine());
                    return;
                }
            }

            if (!CoopSessionConfig.IsMultiplayer)
            {
                Destroy(gameObject);
                return;
            }

            if (CoopSessionConfig.AutoSmokeExitSeconds > 0f)
            {
                Time.timeScale = 1f;
            }

            StartSelectedSessionMode();

            if (CoopSessionConfig.AutoSmokeExitSeconds > 0f)
            {
                StartCoroutine(RunCoopSmokeRoutine(CoopSessionConfig.AutoSmokeExitSeconds));
            }
        }

        void StartSelectedSessionMode()
        {
            if (CoopSessionConfig.IsOnlineMode)
            {
                StartOnlineFlow();
            }
            else if (CoopSessionConfig.IsLanDevMode)
            {
                StartLanDevFlow();
            }
        }

        void EnsureNetworkManagerPrepared()
        {
            GameObject avatarPrefab = Resources.Load<GameObject>("CoopPlayerAvatar");
            if (!avatarPrefab)
            {
                statusText = "Missing Resources/CoopPlayerAvatar prefab!";
                Debug.LogError("[CoopWorldManager] " + statusText);
                return;
            }

            nm = NetworkManager.Singleton;
            if (nm == null)
            {
                var nmGo = new GameObject("CoopNetworkManager");
                nm = nmGo.AddComponent<NetworkManager>();
                utp = nmGo.AddComponent<UnityTransport>();
                nm.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = utp,
                    PlayerPrefab = avatarPrefab,
                    ConnectionApproval = CoopSessionConfig.IsLanDevMode
                };
                nm.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = avatarPrefab });
            }
            else
            {
                utp = nm.GetComponent<UnityTransport>() ?? nm.gameObject.AddComponent<UnityTransport>();
                if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();
                nm.NetworkConfig.NetworkTransport = utp;
                nm.NetworkConfig.PlayerPrefab = avatarPrefab;
                nm.NetworkConfig.ConnectionApproval = CoopSessionConfig.IsLanDevMode;
            }

            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        async void StartOnlineFlow()
        {
            if (CoopSessionConfig.Mode == CoopSessionMode.HostOnline)
            {
                bool ok = await OnlineSessionService.StartHostOnlineAsync(EnsureNetworkManagerPrepared);
                statusText = ok ? $"Online Host ({OnlineSessionService.ActiveJoinCode})" : OnlineSessionService.LastError;
            }
            else if (CoopSessionConfig.Mode == CoopSessionMode.JoinOnline)
            {
                bool ok = await OnlineSessionService.JoinOnlineByCodeAsync(CoopSessionConfig.JoinCode, EnsureNetworkManagerPrepared);
                statusText = ok ? $"Online Co-op ({OnlineSessionService.ActiveJoinCode})" : OnlineSessionService.LastError;
            }
        }

        void StartLanDevFlow()
        {
            EnsureNetworkManagerPrepared();
            if (nm == null) return;

            if (CoopSessionConfig.Mode == CoopSessionMode.HostLan)
            {
                nm.ConnectionApprovalCallback = ApprovalCheck;
                utp.SetConnectionData("127.0.0.1", CoopSessionConfig.Port, "0.0.0.0");
                bool ok = nm.StartHost();
                statusText = ok ? $"Dev LAN Host :{CoopSessionConfig.Port}" : "Failed to start Host";
                Debug.Log($"[CoopWorldManager] {statusText}");
            }
            else if (CoopSessionConfig.Mode == CoopSessionMode.JoinLan)
            {
                string ip = string.IsNullOrWhiteSpace(CoopSessionConfig.JoinIp) ? "127.0.0.1" : CoopSessionConfig.JoinIp.Trim();
                utp.SetConnectionData(ip, CoopSessionConfig.Port);
                bool ok = nm.StartClient();
                statusText = ok ? $"Connecting to {ip}:{CoopSessionConfig.Port}..." : "Failed to start Client";
                Debug.Log($"[CoopWorldManager] {statusText}");
            }
        }

        void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            if (nm.ConnectedClientsIds.Count >= MaxPlayers)
            {
                response.Approved = false;
                response.Reason = $"Server full ({MaxPlayers}/{MaxPlayers})";
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            int slot = nm.ConnectedClientsIds.Count;
            Vector3 basePos = new Vector3(0f, 1.0f, 10f);
            var pc = FindFirstObjectByType<PlayerController>();
            if (pc) basePos = pc.transform.position;
            response.Position = RiftboundCoopPlayer.ComputeSpawnPosition(basePos, slot);
            response.Rotation = Quaternion.identity;
            response.Pending = false;
        }

        void OnClientConnected(ulong clientId)
        {
            if (nm != null && nm.IsServer && clientId != nm.LocalClientId && RiftboundCoopPlayer.LocalOwner != null)
            {
                RiftboundCoopPlayer.LocalOwner.SendWorldSnapshotToClient(clientId);
            }
            if (nm != null && clientId == nm.LocalClientId)
            {
                if (CoopSessionConfig.IsOnlineMode)
                    statusText = nm.IsHost ? $"Online Host ({OnlineSessionService.ActiveJoinCode})" : $"Online Co-op ({OnlineSessionService.ActiveJoinCode})";
                else
                    statusText = nm.IsHost ? $"Dev LAN Host :{CoopSessionConfig.Port}" : $"Connected to {CoopSessionConfig.JoinIp}:{CoopSessionConfig.Port}";
            }
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (shuttingDown || nm == null) return;

            if (clientId == nm.LocalClientId && !nm.IsServer)
            {
                string reason = string.IsNullOrEmpty(nm.DisconnectReason) ? "Host closed the session" : nm.DisconnectReason;
                CoopSessionConfig.LastDisconnectReason = reason;
                statusText = $"Disconnected: {reason}";
                hostDisconnected = true;
                hostDisconnectMessage = $"{reason}. Host exit ends the session (no host migration).";
                readyCheckOpen = false;
                groupMenuOpen = false;
                revealJoinCode = false;
                selectedPeerClientId = ulong.MaxValue;
                activePingExpires = 0f;
                Debug.LogWarning($"[CoopWorldManager] {statusText}");
            }
        }

        public async void DisconnectAndReturnToMenu()
        {
            if (shuttingDown) return;
            shuttingDown = true;

            OnlineSessionService.CancelPendingAttempt();
            if (OnlineSessionService.HasActiveSession)
            {
                await OnlineSessionService.LeaveSessionAsync();
            }

            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
                if (nm.IsListening) nm.Shutdown();
                Destroy(nm.gameObject);
                nm = null;
            }

            CoopSessionConfig.ResetToOffline();
            groupMenuOpen = false;
            revealJoinCode = false;
            selectedPeerClientId = ulong.MaxValue;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        void OnGUI()
        {
            if (!CoopSessionConfig.IsMultiplayer || FrontendMenu.IsOpen) return;

            if (CoopSessionConfig.IsOnlineMode && OnlineSessionService.IsBusy)
            {
                DrawCenteredStatusBox("CONNECTING TO ONLINE CO-OP", OnlineSessionService.StatusMessage, showRetry: false);
                return;
            }

            if (CoopSessionConfig.IsOnlineMode && OnlineSessionService.State == OnlineSessionState.Failed)
            {
                DrawCenteredStatusBox("ONLINE CO-OP ERROR", OnlineSessionService.LastError, showRetry: true);
                return;
            }

            if (hostDisconnected)
            {
                DrawCenteredStatusBox("SESSION ENDED", hostDisconnectMessage, showRetry: false, cancelLabel: "Back to Menu");
                return;
            }

            if (rosterHeaderStyle == null)
            {
                rosterHeaderStyle = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 0) };
                rosterHeaderStyle.normal.textColor = new Color(0.95f, 0.94f, 0.88f);
                rosterSubStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, padding = new RectOffset(0, 0, 0, 0) };
                rosterSubStyle.normal.textColor = new Color(0.84f, 0.86f, 0.88f);
                rosterRightStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, padding = new RectOffset(0, 0, 0, 0) };
            }

            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);
            float uiW = Screen.width / scale;
            float uiH = Screen.height / scale;

            int count = CountSpawnedPeers();
            // Compact group pill placed cleanly left of the Mail/minimap controls; join code stays tucked inside the Group Menu [O]
            float pillW = 144f, pillH = 24f;
            float pillX = Mathf.Max(480f, uiW - 436f), pillY = 20f;
            GUI.color = new Color(0.06f, 0.08f, 0.11f, 0.88f);
            GUI.DrawTexture(new Rect(pillX, pillY, pillW, pillH), Texture2D.whiteTexture);
            GUI.color = new Color(0.66f, 0.55f, 0.33f, 0.85f);
            GUI.DrawTexture(new Rect(pillX, pillY, pillW, 1.5f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            string pillText = groupMenuOpen ? $"Close Group [{count}/{MaxPlayers}]" : $"Group {count}/{MaxPlayers} [O]";
            if (GUI.Button(new Rect(pillX + 2f, pillY + 2f, pillW - 4f, pillH - 4f), pillText, rosterHeaderStyle))
            {
                groupMenuOpen = !groupMenuOpen;
                if (!groupMenuOpen) revealJoinCode = false;
            }

            DrawPartyFramesAndCoopExtras(uiW, uiH);
            if (groupMenuOpen) DrawGroupSessionMenu(uiW, uiH);

            GUI.matrix = oldMatrix;
        }

        int CountSpawnedPeers()
        {
            int n = 0;
            foreach (var p in RiftboundCoopPlayer.AllPlayers)
                if (p && p.IsSpawned) n++;
            return n;
        }

        void RefreshSortedPeers()
        {
            sortedPeers.Clear();
            foreach (var p in RiftboundCoopPlayer.AllPlayers)
                if (p && p.IsSpawned) sortedPeers.Add(p);
            sortedPeers.Sort((a, b) =>
            {
                int slotCmp = a.SpawnSlot.Value.CompareTo(b.SpawnSlot.Value);
                return slotCmp != 0 ? slotCmp : a.OwnerClientId.CompareTo(b.OwnerClientId);
            });
        }

        static Color GetClassTint(string cls)
        {
            switch (cls)
            {
                case "Warrior": return new Color(0.78f, 0.61f, 0.43f);
                case "Paladin": return new Color(0.96f, 0.55f, 0.73f);
                case "Mage": return new Color(0.25f, 0.78f, 0.92f);
                case "Rogue": return new Color(1.0f, 0.94f, 0.36f);
                default: return new Color(0.85f, 0.85f, 0.85f);
            }
        }

        static Color GetResourceTint(string cls)
        {
            switch (cls)
            {
                case "Warrior": return new Color(0.85f, 0.18f, 0.12f);
                case "Mage": return new Color(0.18f, 0.48f, 0.95f);
                case "Rogue": return new Color(0.95f, 0.85f, 0.15f);
                case "Paladin": return new Color(1.0f, 0.82f, 0.22f);
                default: return new Color(0.5f, 0.5f, 0.55f);
            }
        }

        static bool HasSupportedResource(string cls) =>
            cls == "Warrior" || cls == "Mage" || cls == "Rogue" || cls == "Paladin";

        void DrawPartyFramesAndCoopExtras(float uiW, float uiH)
        {
            RefreshSortedPeers();
            bool showReadyBadge = readyCheckOpen || Time.time < readySummaryUntil;

            // Stack cleanly below top-left self Player Frame (y=20..92), Dodge pip (y=108..128), and any opt-in NPC crew
            int companionCount = PartySystem.Instance != null ? PartySystem.Instance.Members.Count : 0;
            float startX = 24f;
            float py = 134f + companionCount * 38f;
            float cardW = 208f;
            float cardH = 44f;

            var localOwner = RiftboundCoopPlayer.LocalOwner;
            bool selectedStillPresent = false;

            foreach (var p in sortedPeers)
            {
                if (!p || !p.IsSpawned || p.IsOwner) continue;
                if (p.OwnerClientId == selectedPeerClientId) selectedStillPresent = true;

                string cls = p.ClassName.Value.ToString();
                Color clsCol = GetClassTint(cls);
                Rect cardRect = new Rect(startX, py, cardW, cardH);

                GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.86f);
                GUI.DrawTexture(cardRect, Texture2D.whiteTexture);
                GUI.color = clsCol;
                GUI.DrawTexture(new Rect(startX, py, 3f, cardH), Texture2D.whiteTexture);
                GUI.color = p.OwnerClientId == selectedPeerClientId ? new Color(0.88f, 0.72f, 0.25f, 0.95f) : new Color(0.32f, 0.28f, 0.20f, 0.75f);
                GUI.DrawTexture(new Rect(startX + 3f, py, cardW - 3f, 1f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(startX + 3f, py + cardH - 1f, cardW - 3f, 1f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                string leaderTag = p.IsLeader.Value ? "★ " : "";
                string roleTag = RiftboundCoopPlayer.GetSocialRoleTag(p.SocialRole.Value);
                string roleSuffix = roleTag.Length > 0 ? $" {roleTag}" : "";
                string combatTag = p.IsInCombat.Value ? " [CBT]" : "";
                string mountTag = p.IsMounted.Value ? " [M]" : "";
                string downTag = p.Health.Value <= 0 ? " [DOWN]" : "";
                rosterHeaderStyle.normal.textColor = p.Health.Value <= 0 ? new Color(0.82f, 0.35f, 0.35f) : clsCol;
                GUI.Label(new Rect(startX + 7f, py + 2f, cardW - 34f, 15f), $"{leaderTag}{p.PlayerName.Value} Lv{p.Level.Value}{roleSuffix}{combatTag}{mountTag}{downTag}", rosterHeaderStyle);
                rosterHeaderStyle.normal.textColor = new Color(0.95f, 0.94f, 0.88f);

                if (showReadyBadge)
                {
                    string rMark = p.ReadyStatus.Value == 1 ? "✔" : (p.ReadyStatus.Value == 2 ? "✖" : "?");
                    rosterRightStyle.normal.textColor = p.ReadyStatus.Value == 1 ? new Color(0.3f, 0.92f, 0.38f) :
                                                        p.ReadyStatus.Value == 2 ? new Color(0.92f, 0.28f, 0.28f) :
                                                        new Color(1f, 0.78f, 0.25f);
                    GUI.Label(new Rect(startX + cardW - 24f, py + 2f, 19f, 15f), rMark, rosterRightStyle);
                }

                // HP bar (13px)
                float hpPct = p.MaxHealth.Value > 0 ? Mathf.Clamp01((float)p.Health.Value / p.MaxHealth.Value) : 1f;
                float barX = startX + 7f, barW = cardW - 13f;
                GUI.color = new Color(0.13f, 0.14f, 0.16f, 0.95f);
                GUI.DrawTexture(new Rect(barX, py + 19f, barW, 13f), Texture2D.whiteTexture);
                GUI.color = p.Health.Value <= 0 ? new Color(0.55f, 0.18f, 0.18f) : new Color(0.25f, 0.78f, 0.36f);
                GUI.DrawTexture(new Rect(barX + 1f, py + 20f, (barW - 2f) * hpPct, 11f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(barX + 4f, py + 17f, barW - 8f, 15f), p.Health.Value <= 0 ? "DOWN (0)" : $"{p.Health.Value}/{p.MaxHealth.Value}", rosterSubStyle);

                // Supported resource bar (5px)
                GUI.color = new Color(0.10f, 0.11f, 0.13f, 0.95f);
                GUI.DrawTexture(new Rect(barX, py + 35f, barW, 5f), Texture2D.whiteTexture);
                if (HasSupportedResource(cls))
                {
                    float resPct = Mathf.Clamp01(p.ResourcePct.Value / 100f);
                    GUI.color = GetResourceTint(cls);
                    GUI.DrawTexture(new Rect(barX, py + 35f, barW * resPct, 5f), Texture2D.whiteTexture);
                }
                GUI.color = Color.white;

                if (!groupMenuOpen && Event.current.type == EventType.MouseDown && Event.current.button == 0 && cardRect.Contains(Event.current.mousePosition))
                {
                    selectedPeerClientId = selectedPeerClientId == p.OwnerClientId ? ulong.MaxValue : p.OwnerClientId;
                    Event.current.Use();
                }

                // Compact peer context actions (only real implemented actions)
                if (!groupMenuOpen && selectedPeerClientId == p.OwnerClientId && localOwner != null)
                {
                    float ctxX = startX + cardW + 6f;
                    bool canRevive = p.Health.Value <= 0 && localOwner.Health.Value > 0 && Vector3.Distance(localOwner.transform.position, p.transform.position) <= 5.0f;
                    bool canPromote = localOwner.IsLeader.Value && !p.IsLeader.Value;
                    float ctxH = 54f + (canPromote ? 24f : 0f);
                    GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.94f);
                    GUI.DrawTexture(new Rect(ctxX, py, 134f, ctxH), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                    float cy = py + 4f;
                    if (GUI.Button(new Rect(ctxX + 4f, cy, 126f, 21f), "Ping Location", rosterSubStyle))
                    {
                        localOwner.RequestMapPing(p.transform.position);
                        selectedPeerClientId = ulong.MaxValue;
                    }
                    cy += 24f;
                    GUI.enabled = canRevive;
                    if (GUI.Button(new Rect(ctxX + 4f, cy, 126f, 21f), canRevive ? "Revive Ally" : "Revive (<5m)", rosterSubStyle))
                    {
                        localOwner.RequestReviveAlly(p.OwnerClientId);
                        selectedPeerClientId = ulong.MaxValue;
                    }
                    GUI.enabled = true;
                    if (canPromote)
                    {
                        cy += 24f;
                        if (GUI.Button(new Rect(ctxX + 4f, cy, 126f, 21f), "Make Leader", rosterSubStyle))
                        {
                            localOwner.RequestPromoteLeader(p.OwnerClientId);
                            selectedPeerClientId = ulong.MaxValue;
                        }
                    }
                }

                py += cardH + 4f;
            }
            if (!selectedStillPresent && selectedPeerClientId != ulong.MaxValue)
                selectedPeerClientId = ulong.MaxValue;

            if (Time.time < activePingExpires && Camera.main)
            {
                Vector3 sp = Camera.main.WorldToScreenPoint(activePingWorldPos + Vector3.up * 2.5f);
                if (sp.z > 0.1f)
                {
                    float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
                    float gx = sp.x / scale;
                    float gy = (Screen.height - sp.y) / scale;
                    GUI.color = new Color(1f, 0.82f, 0.2f, 0.95f);
                    GUI.Label(new Rect(gx - 80f, gy - 20f, 160f, 24f), $"★ PING ({activePingSender})");
                    GUI.color = Color.white;
                }
            }

            if (readyCheckOpen)
            {
                float rw = 300f;
                float rx = (uiW - rw) * 0.5f;
                float ry = 88f;
                GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
                GUI.DrawTexture(new Rect(rx, ry, rw, 84f), Texture2D.whiteTexture);
                GUI.color = new Color(0.66f, 0.55f, 0.33f, 0.9f);
                GUI.DrawTexture(new Rect(rx, ry, rw, 2f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(rx + 14f, ry + 8f, rw - 28f, 22f), $"Ready Check from {readyCheckInitiator}");
                if (GUI.Button(new Rect(rx + 14f, ry + 38f, 128f, 30f), "Ready"))
                {
                    RiftboundCoopPlayer.LocalOwner?.RespondReadyCheck(true);
                    readyCheckOpen = false;
                }
                if (GUI.Button(new Rect(rx + 158f, ry + 38f, 128f, 30f), "Not Ready"))
                {
                    RiftboundCoopPlayer.LocalOwner?.RespondReadyCheck(false);
                    readyCheckOpen = false;
                }
            }
        }

        void DrawGroupSessionMenu(float uiW, float uiH)
        {
            float mw = 470f, mh = 365f;
            float mx = (uiW - mw) * 0.5f, my = (uiH - mh) * 0.5f;

            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.96f);
            GUI.DrawTexture(new Rect(mx, my, mw, mh), Texture2D.whiteTexture);
            GUI.color = new Color(0.66f, 0.55f, 0.33f, 0.92f);
            GUI.DrawTexture(new Rect(mx, my, mw, 2f), Texture2D.whiteTexture);
            GUI.color = Color.white;

            int count = sortedPeers.Count;
            GUI.Label(new Rect(mx + 16f, my + 12f, mw - 70f, 24f), $"GROUP & SESSION  ({count}/{MaxPlayers} Players)");
            if (GUI.Button(new Rect(mx + mw - 38f, my + 10f, 24f, 24f), "X"))
                CloseGroupMenu();

            string cleanStatus = System.Text.RegularExpressions.Regex.Replace(statusText ?? "", @"\s*\([A-Z0-9]{4,8}\)", "");
            GUI.Label(new Rect(mx + 16f, my + 36f, mw - 32f, 20f), $"{cleanStatus} · Session-only progress · Host exit ends session", rosterSubStyle);

            float rowY = my + 64f;
            if (CoopSessionConfig.IsOnlineMode && !string.IsNullOrEmpty(OnlineSessionService.ActiveJoinCode))
            {
                string displayedCode = revealJoinCode ? OnlineSessionService.ActiveJoinCode : "••••••";
                GUI.Label(new Rect(mx + 16f, rowY + 2f, 185f, 22f), $"Private Invite Code: {displayedCode}", rosterSubStyle);
                if (GUI.Button(new Rect(mx + 206f, rowY, 74f, 24f), revealJoinCode ? "Hide" : "Reveal"))
                    revealJoinCode = !revealJoinCode;
                if (GUI.Button(new Rect(mx + 286f, rowY, 166f, 24f), "Copy Invite Code"))
                    GUIUtility.systemCopyBuffer = OnlineSessionService.ActiveJoinCode;
                rowY += 34f;
            }

            var localOwner = RiftboundCoopPlayer.LocalOwner;
            GUI.Label(new Rect(mx + 16f, rowY, mw - 32f, 18f), "PARTY MEMBERS", rosterSubStyle);
            rowY += 22f;

            foreach (var p in sortedPeers)
            {
                if (!p || !p.IsSpawned) continue;
                GUI.color = new Color(0.09f, 0.11f, 0.15f, 0.9f);
                GUI.DrawTexture(new Rect(mx + 16f, rowY, mw - 32f, 38f), Texture2D.whiteTexture);
                GUI.color = Color.white;

                string leaderMark = p.IsLeader.Value ? "★ Leader · " : "";
                string selfMark = p.IsOwner ? " (You)" : "";
                string readyStr = p.ReadyStatus.Value == 1 ? "Ready" : (p.ReadyStatus.Value == 2 ? "Not Ready" : "Idle");
                string stateStr = p.Health.Value <= 0 ? "DOWN" : (p.IsMounted.Value ? $"Mounted ({p.MountId.Value})" : (p.IsInCombat.Value ? "Combat" : readyStr));
                string roleStr = RiftboundCoopPlayer.GetSocialRoleName(p.SocialRole.Value);
                GUI.Label(new Rect(mx + 24f, rowY + 2f, 260f, 18f), $"{leaderMark}{p.PlayerName.Value}{selfMark} · Lv{p.Level.Value} {p.ClassName.Value}");
                GUI.Label(new Rect(mx + 24f, rowY + 19f, 260f, 17f), $"HP {p.Health.Value}/{p.MaxHealth.Value} · {roleStr} · {stateStr}", rosterSubStyle);

                if (p.IsOwner)
                {
                    if (GUI.Button(new Rect(mx + mw - 132f, rowY + 8f, 108f, 22f), $"Role: {roleStr}"))
                        localOwner.CycleSocialRole();
                }
                else if (localOwner != null && localOwner.IsLeader.Value && !p.IsLeader.Value)
                {
                    if (GUI.Button(new Rect(mx + mw - 132f, rowY + 8f, 108f, 22f), "Make Leader"))
                        localOwner.RequestPromoteLeader(p.OwnerClientId);
                }
                rowY += 42f;
            }

            float btnY = my + mh - 74f;
            if (GUI.Button(new Rect(mx + 16f, btnY, 138f, 28f), "Map Ping [F6]"))
                localOwner?.RequestMapPing(localOwner.transform.position);
            if (GUI.Button(new Rect(mx + 162f, btnY, 146f, 28f), "Ready Check [F7]"))
                localOwner?.RequestReadyCheck();
            if (GUI.Button(new Rect(mx + 316f, btnY, 138f, 28f), "Companions [G]"))
            {
                CloseGroupMenu();
                PartySystem.Instance?.ToggleCompanionMenu();
            }

            if (GUI.Button(new Rect(mx + 16f, my + mh - 38f, mw - 32f, 28f), "Leave Session & Return to Menu"))
            {
                CloseGroupMenu();
                DisconnectAndReturnToMenu();
            }
        }

        void DrawCenteredStatusBox(string title, string message, bool showRetry, string cancelLabel = "Cancel")
        {
            float w = 440f;
            float h = 150f;
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.42f;

            GUI.color = new Color(0.05f, 0.06f, 0.09f, 0.94f);
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 18f, y + 14f, w - 36f, 24f), title);
            GUI.Label(new Rect(x + 18f, y + 44f, w - 36f, 48f), message);

            if (showRetry)
            {
                if (GUI.Button(new Rect(x + 18f, y + 104f, 180f, 30f), "Retry"))
                {
                    OnlineSessionService.ClearError();
                    StartSelectedSessionMode();
                }
                if (GUI.Button(new Rect(x + 242f, y + 104f, 180f, 30f), "Back to Menu"))
                {
                    DisconnectAndReturnToMenu();
                }
            }
            else
            {
                if (GUI.Button(new Rect(x + 130f, y + 104f, 180f, 30f), cancelLabel))
                {
                    DisconnectAndReturnToMenu();
                }
            }
        }

        static RiftboundCoopPlayer GetFirstRemotePlayer()
        {
            foreach (var p in RiftboundCoopPlayer.AllPlayers)
                if (p && p.IsSpawned && p != RiftboundCoopPlayer.LocalOwner)
                    return p;
            return null;
        }

        IEnumerator RunUiSmokeWorldRoutine()
        {
            float t = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t < 25f &&
                   (OnlineSessionService.State != OnlineSessionState.InWorld || string.IsNullOrEmpty(OnlineSessionService.ActiveJoinCode) || RiftboundCoopPlayer.LocalOwner == null))
                yield return new WaitForSeconds(0.25f);

            bool worldEntered = !FrontendMenu.IsOpen &&
                                OnlineSessionService.State == OnlineSessionState.InWorld &&
                                !string.IsNullOrEmpty(OnlineSessionService.ActiveJoinCode) &&
                                RiftboundCoopPlayer.LocalOwner != null;
            if (!worldEntered)
            {
                string failMsg = $"FAIL UiFlow stage1: menu={FrontendMenu.IsOpen}, state={OnlineSessionService.State}, codeEmpty={string.IsNullOrEmpty(OnlineSessionService.ActiveJoinCode)}";
                Debug.Log(failMsg);
                System.Console.WriteLine(failMsg);
                Application.Quit(1);
                yield break;
            }
            UiSmokeStage = 2;
            DisconnectAndReturnToMenu();
        }

        IEnumerator FinishUiSmokeRoutine()
        {
            yield return new WaitForSecondsRealtime(0.6f);
            int remainingNm = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length;
            int cams = 0;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled && c.gameObject.activeInHierarchy) cams++;
            int listeners = 0;
            foreach (var al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (al.enabled && al.gameObject.activeInHierarchy) listeners++;
            bool pass = FrontendMenu.IsOpen &&
                        OnlineSessionService.State == OnlineSessionState.Offline &&
                        CoopSessionConfig.Mode == CoopSessionMode.Offline &&
                        remainingNm == 0 &&
                        cams >= 1 &&
                        listeners == 1;
            string msg = $"{(pass ? "PASS" : "FAIL")} UiFlow: menuOpen={FrontendMenu.IsOpen}, state={OnlineSessionService.State}, mode={CoopSessionConfig.Mode}, nm={remainingNm}, cams={cams}, listeners={listeners}";
            Debug.Log(msg);
            System.Console.WriteLine(msg);
            Time.timeScale = 1f;
            yield return new WaitForSecondsRealtime(0.2f);
            Application.Quit(pass ? 0 : 1);
        }

        IEnumerator FinishClientLeaveSmokeRoutine()
        {
            yield return new WaitForSeconds(0.5f);
            int remainingNm = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length;
            bool offlineClean = OnlineSessionService.State == OnlineSessionState.Offline &&
                                CoopSessionConfig.Mode == CoopSessionMode.Offline &&
                                remainingNm == 0;

            if (clientLeaveStage == 1)
            {
                if (!offlineClean || string.IsNullOrEmpty(clientLeaveRejoinCode))
                {
                    string failMsg = $"FAIL ClientLeave(stage1): questAccepted={clientLeaveQuestAccepted}, mounted={clientLeaveMounted}, nmAfterLeave={remainingNm}, finalState={OnlineSessionService.State}";
                    Debug.Log(failMsg);
                    System.Console.WriteLine(failMsg);
                    yield return new WaitForSeconds(0.2f);
                    Application.Quit(1);
                    yield break;
                }

                clientLeaveStage = 2;
                CoopSessionConfig.Mode = CoopSessionMode.JoinOnline;
                CoopSessionConfig.JoinCode = clientLeaveRejoinCode;
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                yield break;
            }

            bool pass = offlineClean &&
                        clientLeaveQuestAccepted &&
                        clientLeaveMounted &&
                        clientLeaveRejoinOk;
            string msg = $"{(pass ? "PASS" : "FAIL")} ClientLeave: questAccepted={clientLeaveQuestAccepted}, mounted={clientLeaveMounted}, rejoined={clientLeaveRejoinOk}, nmAfterLeave={remainingNm}, finalState={OnlineSessionService.State}";
            Debug.Log(msg);
            System.Console.WriteLine(msg);
            yield return new WaitForSeconds(0.2f);
            Application.Quit(pass ? 0 : 1);
        }

        IEnumerator FinishResilienceSmokeRoutine()
        {
            yield return new WaitForSeconds(0.5f);
            int remainingNm = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length;
            bool pass = OnlineSessionService.State == OnlineSessionState.Offline &&
                        CoopSessionConfig.Mode == CoopSessionMode.Offline &&
                        remainingNm == 0 &&
                        resilienceCancelOk;
            string msg = $"{(pass ? "PASS" : "FAIL")} Resilience: cancelOk={resilienceCancelOk}, codes={resilienceCode1}, nmDuringHost={resilienceNmCount}, nmAfterLeave={remainingNm}, finalState={OnlineSessionService.State}";
            Debug.Log(msg);
            System.Console.WriteLine(msg);
            yield return new WaitForSeconds(0.2f);
            Application.Quit(pass ? 0 : 1);
        }

        IEnumerator RunCoopSmokeRoutine(float timeoutSeconds)
        {
            float start = Time.realtimeSinceStartup;
            if (CoopSessionConfig.InvalidCodeSmoke)
            {
                while (Time.realtimeSinceStartup - start < timeoutSeconds && OnlineSessionService.State != OnlineSessionState.Failed)
                    yield return new WaitForSeconds(0.2f);

                bool invPass = OnlineSessionService.State == OnlineSessionState.Failed && !string.IsNullOrEmpty(OnlineSessionService.LastError);
                string invMsg = $"{(invPass ? "PASS" : "FAIL")} InvalidCode: state={OnlineSessionService.State}, err={OnlineSessionService.LastError}";
                Debug.Log(invMsg);
                System.Console.WriteLine(invMsg);
                yield return new WaitForSeconds(0.2f);
                Application.Quit(invPass ? 0 : 1);
                yield break;
            }

            if (CoopSessionConfig.ResilienceSmoke)
            {
                if (resilienceStage == -1)
                {
                    float waitBusy = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitBusy < 5f && !OnlineSessionService.IsBusy)
                        yield return null;
                    yield return new WaitForSeconds(0.05f);
                    resilienceStage = -2;
                    DisconnectAndReturnToMenu();
                    yield break;
                }
                else if (resilienceStage == 0)
                {
                    while (Time.realtimeSinceStartup - start < timeoutSeconds * 0.45f &&
                           (OnlineSessionService.State != OnlineSessionState.InWorld || string.IsNullOrEmpty(OnlineSessionService.ActiveJoinCode)))
                        yield return new WaitForSeconds(0.25f);
                    resilienceCode1 = OnlineSessionService.ActiveJoinCode;
                    resilienceStage = 1;
                    DisconnectAndReturnToMenu();
                    yield break;
                }
                else if (resilienceStage == 2)
                {
                    while (Time.realtimeSinceStartup - start < timeoutSeconds * 0.45f &&
                           (OnlineSessionService.State != OnlineSessionState.InWorld || string.IsNullOrEmpty(OnlineSessionService.ActiveJoinCode)))
                        yield return new WaitForSeconds(0.25f);
                    string code2 = OnlineSessionService.ActiveJoinCode;
                    resilienceNmCount = FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length;
                    bool stage2Ok = !string.IsNullOrEmpty(resilienceCode1) && !string.IsNullOrEmpty(code2) &&
                                    code2 != resilienceCode1 && resilienceNmCount == 1;
                    if (!stage2Ok)
                    {
                        string failMsg = $"FAIL Resilience stage2: code1={resilienceCode1}, code2={code2}, nmCount={resilienceNmCount}";
                        Debug.Log(failMsg);
                        System.Console.WriteLine(failMsg);
                        Application.Quit(1);
                        yield break;
                    }
                    resilienceCode1 = $"{resilienceCode1}->{code2}";
                    resilienceStage = 3;
                    DisconnectAndReturnToMenu();
                    yield break;
                }
            }

            bool isHost = CoopSessionConfig.Mode == CoopSessionMode.HostLan || CoopSessionConfig.Mode == CoopSessionMode.HostOnline;
            string role = isHost ? "CoopHost" : "CoopClient";
            int minPlayers = Mathf.Max(2, CoopSessionConfig.ExpectedMinPlayers);

            bool hostPreDamaged = false;
            if (CoopSessionConfig.LateJoinSmoke && isHost)
            {
                while (Time.realtimeSinceStartup - start < timeoutSeconds * 0.5f && (nm == null || !nm.IsListening || RiftboundCoopPlayer.LocalOwner == null))
                    yield return new WaitForSeconds(0.2f);
                var preEnemy = EnemyController.FindByKey("road-raider-1");
                if (preEnemy)
                {
                    var preStats = preEnemy.GetComponent<Combatant>();
                    if (preStats && preStats.Alive)
                    {
                        preStats.Damage(30f);
                        hostPreDamaged = preStats.Health < preStats.maxHealth;
                        Debug.Log($"[LateJoinSmoke] Host pre-damaged {preEnemy.NetworkKey} to {preStats.Health:F0}/{preStats.maxHealth:F0}");
                    }
                }
                if (LivingWorldEvents.Instance != null) LivingWorldEvents.Instance.TriggerCaravanEvent();
                if (RiftboundCoopPlayer.LocalOwner != null) RiftboundCoopPlayer.LocalOwner.RequestReadyCheck();
            }

            while (Time.realtimeSinceStartup - start < timeoutSeconds)
            {
                if (nm != null && nm.IsListening && RiftboundCoopPlayer.AllPlayers.Count >= minPlayers && RiftboundCoopPlayer.LocalOwner != null)
                {
                    break;
                }
                yield return new WaitForSeconds(0.25f);
            }

            yield return new WaitForSeconds(0.8f);

            int players = RiftboundCoopPlayer.AllPlayers.Count;
            int activeCams = 0;
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled && c.gameObject.activeInHierarchy) activeCams++;
            int activeListeners = 0;
            foreach (var al in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (al.enabled && al.gameObject.activeInHierarchy) activeListeners++;

            float sep = players >= 2
                ? Vector3.Distance(RiftboundCoopPlayer.AllPlayers[0].transform.position, RiftboundCoopPlayer.AllPlayers[1].transform.position)
                : 0f;
            string posInfo = players >= 2
                ? $"p0={RiftboundCoopPlayer.AllPlayers[0].transform.position}, p1={RiftboundCoopPlayer.AllPlayers[1].transform.position}"
                : "p<2";

            bool combatPass = true;
            string combatInfo = "";
            if (minPlayers >= 4 && players >= 4)
            {
                var frameSamples = new List<float>(30);
                for (int s = 0; s < 30; s++)
                {
                    yield return null;
                    frameSamples.Add(Time.unscaledDeltaTime * 1000f);
                }
                frameSamples.Sort();
                float sumMs = 0f;
                for (int s = 0; s < frameSamples.Count; s++) sumMs += frameSamples[s];
                float avgMs = frameSamples.Count > 0 ? sumMs / frameSamples.Count : 0f;
                float p95Ms = frameSamples.Count > 0 ? frameSamples[Mathf.Clamp((int)(frameSamples.Count * 0.95f), 0, frameSamples.Count - 1)] : 0f;
                int netCount = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None).Length;
                combatInfo += $", telemetry(avgMs={avgMs:F2}, p95Ms={p95Ms:F2}, enemies={EnemyController.AllByKey.Count}, netObjs={netCount})";
            }
            if (CoopSessionConfig.CombatSmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                EnemyController targetEnemy = null;
                float bestDist = float.MaxValue;
                Vector3 refPos = RiftboundCoopPlayer.AllPlayers[0].transform.position;
                foreach (var kv in EnemyController.AllByKey)
                {
                    if (kv.Value && !kv.Value.trainingDummy)
                    {
                        var st = kv.Value.GetComponent<Combatant>();
                        if (st && st.Alive)
                        {
                            float d = Vector3.Distance(refPos, kv.Value.transform.position);
                            if (d < bestDist) { bestDist = d; targetEnemy = kv.Value; }
                        }
                    }
                }

                if (targetEnemy)
                {
                    var enemyStats = targetEnemy.GetComponent<Combatant>();
                    float initialHp = enemyStats.Health;
                    Vector3 combatSpot = targetEnemy.transform.position + new Vector3(4f, 0f, 0f);
                    if (isHost)
                    {
                        var clientAvatar = RiftboundCoopPlayer.FindByClientId(1);
                        if (clientAvatar != null)
                        {
                            clientAvatar.AuthorizeHostTeleport(combatSpot);
                            RiftboundCoopPlayer.DispatchEnemyHitToPlayer(1, 10f, targetEnemy.NetworkKey);
                        }
                    }
                    else
                    {
                        yield return new WaitForSeconds(0.6f);
                        string ab = RiftboundCoopPlayer.LocalOwner.ClassName.Value.ToString() == "Mage" ? "fireball" : "melee";
                        RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(enemyStats, ab);
                    }

                    float waitStart = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitStart < 7f && enemyStats.Health >= initialHp)
                        yield return new WaitForSeconds(0.15f);

                    bool clientTookDamage = true;
                    if (!isHost)
                    {
                        var lp = FindFirstObjectByType<PlayerController>();
                        float waitDmg = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitDmg < 3f && lp && lp.Stats && lp.Stats.Health >= lp.Stats.maxHealth)
                            yield return new WaitForSeconds(0.15f);
                        clientTookDamage = lp && lp.Stats && lp.Stats.Health < lp.Stats.maxHealth;
                    }

                    if (isHost)
                        yield return new WaitForSeconds(1.0f);

                    combatPass = enemyStats.Health < initialHp && clientTookDamage;
                    combatInfo = $", enemy={targetEnemy.NetworkKey} hp={enemyStats.Health:F0}/{initialHp:F0}, clientDamaged={clientTookDamage}";
                }
                else
                {
                    combatPass = false;
                    combatInfo = ", enemy=none";
                }
            }

            bool lateJoinPass = true;
            if (CoopSessionConfig.LateJoinSmoke && players >= 2)
            {
                yield return new WaitForSeconds(1.8f);
                var ljEnemy = EnemyController.FindByKey("road-raider-1");
                var ljStats = ljEnemy ? ljEnemy.GetComponent<Combatant>() : null;
                bool evtActive = LivingWorldEvents.Instance != null && LivingWorldEvents.Instance.CaravanState == LivingWorldEvents.EventState.Active;
                if (isHost)
                {
                    var clientAvatar = GetFirstRemotePlayer();
                    float wDecline = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - wDecline < 4f && (clientAvatar == null || clientAvatar.ReadyStatus.Value != 2))
                        yield return new WaitForSeconds(0.15f);
                    bool rcDeclineOk = clientAvatar != null && clientAvatar.ReadyStatus.Value == 2;
                    bool obsGvOk = HasValidObserverGrounding(clientAvatar);
                    lateJoinPass = ljStats != null && ljStats.Health < ljStats.maxHealth && hostPreDamaged && evtActive && rcDeclineOk && obsGvOk;
                    combatInfo += $", lateJoin={(lateJoinPass ? "PASS" : "FAIL")}({(ljStats ? $"{ljStats.Health:F0}/{ljStats.maxHealth:F0}" : "none")},evt={evtActive},rcDecline={rcDeclineOk},obsGv={obsGvOk})";
                }
                else
                {
                    bool rcOpen = IsReadyCheckOpenForTest;
                    if (RiftboundCoopPlayer.LocalOwner != null)
                        RiftboundCoopPlayer.LocalOwner.RespondReadyCheck(false);
                    yield return new WaitForSeconds(0.6f);
                    var hostPeer = GetFirstRemotePlayer();
                    bool obsGvOk = HasValidObserverGrounding(hostPeer);
                    lateJoinPass = ljStats != null && ljStats.Health < ljStats.maxHealth && evtActive && rcOpen && obsGvOk;
                    combatInfo += $", lateJoin={(lateJoinPass ? "PASS" : "FAIL")}({(ljStats ? $"{ljStats.Health:F0}/{ljStats.maxHealth:F0}" : "none")},evt={evtActive},rcSnap={rcOpen},obsGv={obsGvOk})";
                }
            }

            bool progPass = true;
            if (CoopSessionConfig.ProgressionSmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                var targetEnemy = EnemyController.FindByKey("road-raider-1");
                LootChest targetChest = FindDeterministicTestChest(15, false);

                var enemyStats = targetEnemy ? targetEnemy.GetComponent<Combatant>() : null;
                var lp = FindFirstObjectByType<PlayerController>();
                TownNPC elara = null;
                foreach (var n in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                    if (n && n.npcName == ExplorationSites.CartographerName) { elara = n; break; }
                Landmark sv0 = null, sv1 = null, sv2 = null;
                foreach (var lm in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                {
                    if (!lm) continue;
                    if (lm.landmarkId == "westbank-overlook") sv0 = lm;
                    else if (lm.landmarkId == "bloodrock-vista") sv1 = lm;
                    else if (lm.landmarkId == "wyrmcrest-lookout") sv2 = lm;
                }
                if (targetEnemy && enemyStats && targetChest && lp && elara && sv0 && sv1 && sv2)
                {
                    if (isHost)
                    {
                        var clientAvatar = GetFirstRemotePlayer();
                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(targetEnemy.transform.position + new Vector3(4f, 0f, 0f));

                        float waitKill = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitKill < 15f && enemyStats.Alive)
                            yield return new WaitForSeconds(0.2f);

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(targetChest.transform.position + new Vector3(1.5f, 0f, 0f));

                        float waitLoot = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitLoot < 8f && (clientAvatar == null || clientAvatar.Gold.Value < 18))
                            yield return new WaitForSeconds(0.25f);
                        yield return new WaitForSeconds(1.8f);

                        if (clientAvatar != null)
                        {
                            int peakGold = clientAvatar.Gold.Value;
                            float waitBuy = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - waitBuy < 4f && clientAvatar.Gold.Value > peakGold - 10)
                                yield return new WaitForSeconds(0.15f);

                            RiftboundCoopPlayer.DispatchEnemyHitToPlayer(clientAvatar.OwnerClientId, 999f, "smoke-test");
                            yield return new WaitForSeconds(0.2f);
                            float waitRespH = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - waitRespH < 5f && clientAvatar.Health.Value < clientAvatar.MaxHealth.Value)
                                yield return new WaitForSeconds(0.15f);
                        }

                        // Teleport Client to Cartographer Elara -> 3 survey sites -> Elara for optional bounty test
                        Landmark[] svs = { sv0, sv1, sv2 };
                        if (clientAvatar != null)
                        {
                            clientAvatar.AuthorizeHostTeleport(elara.transform.position + new Vector3(1.5f, 0f, 0f));
                            yield return new WaitForSeconds(1.2f);
                            for (int s = 0; s < 3; s++)
                            {
                                int gBefore = clientAvatar.Gold.Value;
                                clientAvatar.AuthorizeHostTeleport(svs[s].transform.position + new Vector3(1.5f, 0f, 0f));
                                float wSv = Time.realtimeSinceStartup;
                                while (Time.realtimeSinceStartup - wSv < 6f && clientAvatar.Gold.Value < gBefore + 15)
                                    yield return new WaitForSeconds(0.2f);
                            }
                            int gBeforeTurnIn = clientAvatar.Gold.Value;
                            clientAvatar.AuthorizeHostTeleport(elara.transform.position + new Vector3(1.5f, 0f, 0f));
                            float wBounty = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wBounty < 8f && clientAvatar.Gold.Value < gBeforeTurnIn + 75)
                                yield return new WaitForSeconds(0.2f);
                            yield return new WaitForSeconds(0.8f);
                        }

                        Vector3 hostChestSpot = targetChest.transform.position + new Vector3(1.5f, 0f, 0f);
                        RiftboundCoopPlayer.LocalOwner.AuthorizeHostTeleport(hostChestSpot);
                        yield return new WaitForSeconds(0.25f);
                        bool hostChestOk = lp.OpenChest(targetChest);
                        progPass = !enemyStats.Alive && hostChestOk;
                        combatInfo += $", hostProg={(progPass ? "PASS" : "FAIL")}(dead={!enemyStats.Alive}, chest={hostChestOk})";
                    }
                    else
                    {
                        float waitTp = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitTp < 8f && Vector3.Distance(lp.transform.position, targetEnemy.transform.position) > 9f)
                            yield return new WaitForSeconds(0.2f);

                        float waitKill = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitKill < 14f && enemyStats.Alive && RiftboundCoopPlayer.LocalOwner != null)
                        {
                            string ab = RiftboundCoopPlayer.LocalOwner.ClassName.Value.ToString() == "Mage" ? "fireball" : "melee";
                            RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(enemyStats, ab);
                            yield return new WaitForSeconds(0.55f);
                        }
                        yield return new WaitForSeconds(0.8f);
                        if (RiftboundCoopPlayer.LocalOwner == null)
                        {
                            progPass = false;
                            combatInfo += ", clientProg=FAIL(disconnected)";
                        }
                        else
                        {
                            bool killCreditOk = !enemyStats.Alive && RiftboundCoopPlayer.LocalOwner.Gold.Value > 0 &&
                                                (RiftboundCoopPlayer.LocalOwner.Experience.Value > 0 || RiftboundCoopPlayer.LocalOwner.Level.Value > 1);

                            RiftboundCoopPlayer.LocalOwner.RequestCompleteQuestServerValidated(0);
                            yield return new WaitForSeconds(0.8f);
                            bool questRejectOk = RiftboundCoopPlayer.LocalOwner.QuestIndex.Value == 0;

                            float waitChestTp = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - waitChestTp < 8f && Vector3.Distance(lp.transform.position, targetChest.transform.position) > 4f)
                                yield return new WaitForSeconds(0.2f);

                            int goldBeforeChest = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                            lp.OpenChest(targetChest);
                            yield return new WaitForSeconds(1.2f);
                            bool chest1Ok = lp.HasOpenedChest(targetChest.chestId) && RiftboundCoopPlayer.LocalOwner.Gold.Value > goldBeforeChest;

                            int goldAfterChest1 = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                            RiftboundCoopPlayer.LocalOwner.RequestOpenChestServerValidated(targetChest);
                            yield return new WaitForSeconds(0.8f);
                            bool chestDupRejectOk = RiftboundCoopPlayer.LocalOwner.Gold.Value == goldAfterChest1;

                            RiftboundCoopPlayer.LocalOwner.RequestTransaction("temper_weapon");
                            yield return new WaitForSeconds(0.6f);
                            bool txRejectOk = !lp.TemperedWeapon && RiftboundCoopPlayer.LocalOwner.Gold.Value == goldAfterChest1;

                            int potsBefore = lp.Potions;
                            RiftboundCoopPlayer.LocalOwner.RequestTransaction("buy_potion");
                            yield return new WaitForSeconds(0.8f);
                            bool txBuyOk = RiftboundCoopPlayer.LocalOwner.Gold.Value == goldAfterChest1 - 10 && lp.Potions == Mathf.Min(10, potsBefore + 1);

                            float waitDie = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - waitDie < 6f && RiftboundCoopPlayer.LocalOwner.Health.Value > 0)
                                yield return new WaitForSeconds(0.15f);
                            bool didDie = RiftboundCoopPlayer.LocalOwner.Health.Value <= 0;
                            if (didDie)
                                RiftboundCoopPlayer.LocalOwner.RequestRespawn();
                            float waitResp = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - waitResp < 3f &&
                                   (RiftboundCoopPlayer.LocalOwner.Health.Value < RiftboundCoopPlayer.LocalOwner.MaxHealth.Value ||
                                    Vector3.Distance(lp.transform.position, targetChest.transform.position) <= 5f))
                                yield return new WaitForSeconds(0.15f);
                            bool respawnOk = didDie &&
                                             RiftboundCoopPlayer.LocalOwner.Health.Value == RiftboundCoopPlayer.LocalOwner.MaxHealth.Value &&
                                             Vector3.Distance(lp.transform.position, targetChest.transform.position) > 5f;

                            // Optional Bounty (Cartographer frontier_cartography): premature reject -> 3 surveys -> turn in once -> duplicate reject
                            bool bountyOk = false;
                            var fq = FactionQuestSystem.Instance;
                            if (fq != null)
                            {
                                float wElara = Time.realtimeSinceStartup;
                                while (Time.realtimeSinceStartup - wElara < 10f && Vector3.Distance(lp.transform.position, elara.transform.position) > 5f)
                                    yield return new WaitForSeconds(0.15f);
                                var qDef = fq.GetAvailableQuestForNPC(ExplorationSites.CartographerName, lp);
                                if (qDef != null && qDef.id == "frontier_cartography")
                                {
                                    fq.AcceptQuest(qDef, lp);
                                    int gPre = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                                    RiftboundCoopPlayer.LocalOwner.RequestCompleteFactionQuestServerValidated("frontier_cartography");
                                    yield return new WaitForSeconds(0.55f);
                                    bool preRej = !fq.IsQuestCompleted("frontier_cartography") && RiftboundCoopPlayer.LocalOwner.Gold.Value == gPre;

                                    Landmark[] svs = { sv0, sv1, sv2 };
                                    for (int s = 0; s < 3; s++)
                                    {
                                        float wS = Time.realtimeSinceStartup;
                                        while (Time.realtimeSinceStartup - wS < 8f && Vector3.Distance(lp.transform.position, svs[s].transform.position) > 5f)
                                            yield return new WaitForSeconds(0.15f);
                                        int gS = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                                        lp.DiscoverLandmark(svs[s].landmarkId);
                                        float wGs = Time.realtimeSinceStartup;
                                        while (Time.realtimeSinceStartup - wGs < 5f && RiftboundCoopPlayer.LocalOwner.Gold.Value < gS + 15)
                                            yield return new WaitForSeconds(0.15f);
                                    }

                                    float wRet = Time.realtimeSinceStartup;
                                    while (Time.realtimeSinceStartup - wRet < 8f && Vector3.Distance(lp.transform.position, elara.transform.position) > 5f)
                                        yield return new WaitForSeconds(0.15f);
                                    int gBeforeTurn = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                                    fq.CompleteQuest(lp);
                                    float wDone = Time.realtimeSinceStartup;
                                    while (Time.realtimeSinceStartup - wDone < 5f && !fq.IsQuestCompleted("frontier_cartography"))
                                        yield return new WaitForSeconds(0.15f);
                                    int gAfterTurn = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                                    RiftboundCoopPlayer.LocalOwner.RequestCompleteFactionQuestServerValidated("frontier_cartography");
                                    yield return new WaitForSeconds(0.55f);
                                    bool dupBountyRej = RiftboundCoopPlayer.LocalOwner.Gold.Value == gAfterTurn;
                                    bountyOk = preRej && fq.IsQuestCompleted("frontier_cartography") && gAfterTurn >= gBeforeTurn + 75 && dupBountyRej;
                                }
                            }

                            progPass = killCreditOk && questRejectOk && chest1Ok && chestDupRejectOk && txRejectOk && txBuyOk && respawnOk && bountyOk;
                            combatInfo += $", clientProg={(progPass ? "PASS" : "FAIL")}(kill={killCreditOk},qRej={questRejectOk},c1={chest1Ok},cDup={chestDupRejectOk},txRej={txRejectOk},txBuy={txBuyOk},resp={respawnOk},bounty={bountyOk})";
                        }
                    }
                }
                else
                {
                    progPass = false;
                    combatInfo += ", prog=FAIL(missingEntity)";
                }
            }

            bool clientLeavePass = true;
            if (CoopSessionConfig.ClientLeaveSmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                if (!isHost && clientLeaveStage == 2)
                {
                    yield return new WaitForSeconds(0.4f);
                    int rCams = 0;
                    foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) if (c.enabled) rCams++;
                    int rListeners = 0;
                    foreach (var l in FindObjectsByType<AudioListener>(FindObjectsSortMode.None)) if (l.enabled) rListeners++;
                    clientLeaveRejoinOk = RiftboundCoopPlayer.AllPlayers.Count >= 2 &&
                                          RiftboundCoopPlayer.LocalOwner != null &&
                                          rCams == 1 &&
                                          rListeners == 1 &&
                                          !FrontendMenu.IsOpen;
                    clientLeaveStage = 3;
                    DisconnectAndReturnToMenu();
                    yield break;
                }

                var giver = FindFirstObjectByType<TownNPC>();
                if (isHost)
                {
                    var clientAvatar = GetFirstRemotePlayer();
                    if (clientAvatar != null && giver != null)
                        clientAvatar.AuthorizeHostTeleport(giver.transform.position + new Vector3(1.5f, 0f, 0f));

                    float waitMount = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitMount < 10f && (clientAvatar == null || !clientAvatar.IsMounted.Value))
                        yield return new WaitForSeconds(0.2f);
                    bool sawMounted = clientAvatar != null && clientAvatar.IsMounted.Value;
                    bool sawQuest = clientAvatar != null && clientAvatar.QuestAccepted.Value;
                    bool sawObsGv = HasValidObserverGrounding(clientAvatar);

                    float waitDisc = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitDisc < 12f && RiftboundCoopPlayer.AllPlayers.Count > 1)
                        yield return new WaitForSeconds(0.2f);

                    int afterPlayers = RiftboundCoopPlayer.AllPlayers.Count;

                    float waitRejoin = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitRejoin < 18f && RiftboundCoopPlayer.AllPlayers.Count < 2)
                        yield return new WaitForSeconds(0.2f);
                    yield return new WaitForSeconds(0.4f);
                    var rejoinedAvatar = GetFirstRemotePlayer();
                    bool rejoined = RiftboundCoopPlayer.AllPlayers.Count >= 2;
                    bool rejoinObsGv = HasValidObserverGrounding(rejoinedAvatar);

                    float waitDisc2 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitDisc2 < 12f && RiftboundCoopPlayer.AllPlayers.Count > 1)
                        yield return new WaitForSeconds(0.2f);
                    int finalPlayers = RiftboundCoopPlayer.AllPlayers.Count;

                    clientLeavePass = sawMounted && sawQuest && sawObsGv && afterPlayers == 1 && rejoined && rejoinObsGv && finalPlayers == 1 && nm != null && nm.IsListening;
                    combatInfo += $", hostDisconnectRecovery={(clientLeavePass ? "PASS" : "FAIL")}(mounted={sawMounted},quest={sawQuest},obs={sawObsGv},playersAfter={afterPlayers},rejoined={rejoined},rejoinObs={rejoinObsGv},finalPlayers={finalPlayers})";
                }
                else
                {
                    if (giver != null)
                    {
                        float waitGiver = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitGiver < 8f &&
                               Vector3.Distance(RiftboundCoopPlayer.LocalOwner.transform.position, giver.transform.position) > 5.5f)
                            yield return new WaitForSeconds(0.2f);
                    }
                    RiftboundCoopPlayer.LocalOwner.RequestAcceptQuest();
                    RiftboundCoopPlayer.LocalOwner.RequestSetMount(true, "horse");
                    float waitSync = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - waitSync < 6f &&
                           (!RiftboundCoopPlayer.LocalOwner.QuestAccepted.Value || !RiftboundCoopPlayer.LocalOwner.IsMounted.Value))
                        yield return new WaitForSeconds(0.2f);

                    clientLeaveQuestAccepted = RiftboundCoopPlayer.LocalOwner.QuestAccepted.Value;
                    clientLeaveMounted = RiftboundCoopPlayer.LocalOwner.IsMounted.Value;
                    clientLeaveRejoinCode = OnlineSessionService.ActiveJoinCode;
                    clientLeaveStage = 1;
                    DisconnectAndReturnToMenu();
                    yield break;
                }
            }

            bool fullAcceptPass = true;
            if (CoopSessionConfig.FullAcceptanceSmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                var giver = FindFirstObjectByType<TownNPC>();
                var lp = FindFirstObjectByType<PlayerController>();
                var enemies = new List<EnemyController>();
                foreach (var kv in EnemyController.AllByKey)
                {
                    if (kv.Value && !kv.Value.trainingDummy)
                    {
                        var st = kv.Value.GetComponent<Combatant>();
                        if (st && st.Alive && st.maxHealth <= 110f && kv.Value.gameObject.activeInHierarchy) enemies.Add(kv.Value);
                    }
                }
                enemies.Sort((a, b) => Vector3.Distance(giver.transform.position, a.transform.position).CompareTo(Vector3.Distance(giver.transform.position, b.transform.position)));

                if (giver && enemies.Count >= 3 && lp)
                {
                    if (isHost)
                    {
                        var clientAvatar = GetFirstRemotePlayer();
                        ulong remoteCid = clientAvatar != null ? clientAvatar.OwnerClientId : 1UL;
                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(giver.transform.position + new Vector3(1.5f, 0f, 0f));

                        float waitAcc = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitAcc < 10f && (clientAvatar == null || !clientAvatar.QuestAccepted.Value))
                            yield return new WaitForSeconds(0.2f);

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(enemies[0].transform.position + new Vector3(2.2f, 0f, 0f));

                        yield return new WaitForSeconds(2.2f);
                        RiftboundCoopPlayer.DispatchEnemyHitToPlayer(remoteCid, 30f, enemies[0].NetworkKey);
                        yield return new WaitForSeconds(1.2f);

                        float wait3Kills = Time.realtimeSinceStartup;
                        float lastFollowSync = wait3Kills;
                        int lastIdx = 0;
                        while (Time.realtimeSinceStartup - wait3Kills < 26f && clientAvatar != null && clientAvatar.QuestKills.Value < 3)
                        {
                            if (lastIdx < 3)
                            {
                                var e = enemies[lastIdx];
                                var est = e ? e.GetComponent<Combatant>() : null;
                                if (est && !est.Alive)
                                {
                                    lastIdx++;
                                    if (lastIdx < 3)
                                    {
                                        clientAvatar.AuthorizeHostTeleport(enemies[lastIdx].transform.position + new Vector3(2.2f, 0f, 0f));
                                        lastFollowSync = Time.realtimeSinceStartup;
                                    }
                                }
                                else if (est && est.Alive && Time.realtimeSinceStartup - lastFollowSync >= 2.0f && Vector3.Distance(clientAvatar.transform.position, e.transform.position) > 4.6f)
                                {
                                    clientAvatar.AuthorizeHostTeleport(e.transform.position + new Vector3(2.2f, 0f, 0f));
                                    lastFollowSync = Time.realtimeSinceStartup;
                                }
                            }
                            yield return new WaitForSeconds(0.2f);
                        }

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(giver.transform.position + new Vector3(1.5f, 0f, 0f));

                        // Wait for QuestIndex == 1 + Ping + ReadyCheck (give generous synchronized window)
                        float waitSocial = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitSocial < 16f && (clientAvatar == null || clientAvatar.QuestIndex.Value < 1 || !HasActivePingForTest || !IsReadyCheckOpenForTest))
                            yield return new WaitForSeconds(0.2f);

                        bool pingOk = HasActivePingForTest;
                        bool rcOpen = IsReadyCheckOpenForTest;
                        RiftboundCoopPlayer.LocalOwner.RespondReadyCheck(true);
                        yield return new WaitForSeconds(0.4f);
                        bool rcResponded = RiftboundCoopPlayer.LocalOwner.ReadyStatus.Value == 1;

                        // Wait briefly so client reaches waitDown before downing
                        yield return new WaitForSeconds(0.8f);
                        RiftboundCoopPlayer.DispatchEnemyHitToPlayer(remoteCid, 500f, enemies[0].NetworkKey);
                        float waitDown = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitDown < 4f && clientAvatar != null && clientAvatar.Health.Value > 0)
                            yield return new WaitForSeconds(0.15f);

                        if (clientAvatar != null)
                        {
                            RiftboundCoopPlayer.LocalOwner.AuthorizeHostTeleport(clientAvatar.transform.position + new Vector3(1.5f, 0f, 0f));
                            yield return new WaitForSeconds(0.4f);
                            RiftboundCoopPlayer.LocalOwner.RequestReviveAlly(remoteCid);
                        }
                        float waitRev = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitRev < 4f && clientAvatar != null && clientAvatar.Health.Value <= 0)
                            yield return new WaitForSeconds(0.15f);
                        bool reviveOk = clientAvatar != null && clientAvatar.Health.Value > 0;

                        yield return new WaitForSeconds(0.6f);
                        var lwe = LivingWorldEvents.Instance ? LivingWorldEvents.Instance : FindFirstObjectByType<LivingWorldEvents>();
                        if (lwe)
                        {
                            lwe.TriggerCaravanEvent();
                            lwe.TriggerAmbushEvent();
                        }
                        yield return new WaitForSeconds(0.8f);
                        if (clientAvatar != null)
                            RiftboundCoopPlayer.BroadcastWorldEventReward("smoke_event_test", clientAvatar.transform.position, 40, 20);
                        yield return new WaitForSeconds(2.5f);

                        fullAcceptPass = pingOk && rcOpen && rcResponded && reviveOk && clientAvatar != null && clientAvatar.QuestIndex.Value == 1;
                        combatInfo += $", hostAccept={(fullAcceptPass ? "PASS" : "FAIL")}(cid={(clientAvatar ? (long)remoteCid : -1)},ping={pingOk},rc={rcOpen}/{rcResponded},revive={reviveOk},kills={(clientAvatar ? clientAvatar.QuestKills.Value : -1)},qIdx={(clientAvatar ? clientAvatar.QuestIndex.Value : -1)})";
                        string hostMsg = $"{(fullAcceptPass ? "PASS" : "FAIL")} {role}: players={players}, localOwner=True, cams={activeCams}, listeners={activeListeners}, sep={sep:F2}m{combatInfo}";
                        Debug.Log(hostMsg);
                        System.Console.WriteLine(hostMsg);

                        yield return new WaitForSeconds(0.6f);
                        if (OnlineSessionService.HasActiveSession)
                        {
                            var leaveTask = OnlineSessionService.LeaveSessionAsync();
                            float lw = Time.realtimeSinceStartup;
                            while (!leaveTask.IsCompleted && Time.realtimeSinceStartup - lw < 5f)
                                yield return new WaitForSeconds(0.1f);
                        }
                        if (nm != null && nm.IsListening) nm.Shutdown();
                        yield return new WaitForSeconds(0.3f);
                        Application.Quit(fullAcceptPass ? 0 : 1);
                        yield break;
                    }
                    else
                    {
                        var me = RiftboundCoopPlayer.LocalOwner;
                        string myClass = me.ClassName.Value.ToString();
                        string wrongAb = myClass == "Mage" ? "consecration" : "fireball";
                        string rightAb = myClass == "Mage" ? "fireball" : (myClass == "Warrior" ? "whirlwind" : (myClass == "Rogue" ? "eviscerate" : "consecration"));
                        string healAb = myClass == "Mage" ? "arcane_ward" : (myClass == "Paladin" ? "holy_light" : "second_wind");

                        float waitGiver1 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitGiver1 < 10f && Vector3.Distance(lp.transform.position, giver.transform.position) > 5.5f)
                            yield return new WaitForSeconds(0.2f);
                        me.RequestAcceptQuest();
                        float waitAcc = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitAcc < 5f && !me.QuestAccepted.Value)
                            yield return new WaitForSeconds(0.15f);
                        bool questAccepted = me.QuestAccepted.Value;

                        var e0Stats = enemies[0].GetComponent<Combatant>();
                        float waitTp0 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitTp0 < 10f && Vector3.Distance(lp.transform.position, enemies[0].transform.position) > 6f)
                            yield return new WaitForSeconds(0.2f);

                        float hpBeforeSpoof = e0Stats.Health;
                        me.RequestAbilityAttack(e0Stats, wrongAb);
                        yield return new WaitForSeconds(0.55f);
                        bool spoofBlocked = Mathf.Approximately(e0Stats.Health, hpBeforeSpoof);

                        me.RequestAbilityAttack(e0Stats, rightAb);
                        float waitHit = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitHit < 3f && e0Stats.Health >= hpBeforeSpoof)
                            yield return new WaitForSeconds(0.15f);
                        bool validAbHit = e0Stats.Health < hpBeforeSpoof;

                        float waitDmg = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitDmg < 8f && me.Health.Value >= me.MaxHealth.Value)
                            yield return new WaitForSeconds(0.15f);
                        int hpDamaged = me.Health.Value;
                        me.RequestAbilityHeal(40, healAb);
                        float waitHeal = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitHeal < 3f && me.Health.Value <= hpDamaged)
                            yield return new WaitForSeconds(0.15f);
                        bool abHealOk = me.Health.Value > hpDamaged;

                        me.RequestSetMount(true, "horse");
                        float waitMnt = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitMnt < 3f && !me.IsMounted.Value)
                            yield return new WaitForSeconds(0.15f);
                        bool mountedOk = me.IsMounted.Value;
                        me.RequestSetMount(false, "");
                        float waitDism = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitDism < 3f && me.IsMounted.Value)
                            yield return new WaitForSeconds(0.15f);
                        bool dismountOk = !me.IsMounted.Value;

                        for (int k = 0; k < 3; k++)
                        {
                            var ek = enemies[k];
                            var eks = ek.GetComponent<Combatant>();
                            float wt = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wt < 10f && Vector3.Distance(lp.transform.position, ek.transform.position) > 6f)
                                yield return new WaitForSeconds(0.15f);
                            float wk = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wk < 10f && eks.Alive && RiftboundCoopPlayer.LocalOwner != null && me.Health.Value > 0)
                            {
                                if (me.Health.Value < 35 && lp.Potions > 0)
                                    me.RequestUsePotion();
                                me.RequestAbilityAttack(eks, "melee");
                                yield return new WaitForSeconds(0.42f);
                            }
                        }

                        float waitGiver2 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitGiver2 < 10f && Vector3.Distance(lp.transform.position, giver.transform.position) > 5.5f)
                            yield return new WaitForSeconds(0.2f);
                        me.RequestCompleteQuestServerValidated(0);
                        float waitQ1 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitQ1 < 5f && me.QuestIndex.Value < 1)
                            yield return new WaitForSeconds(0.15f);
                        int goldAfterQuest = me.Gold.Value;
                        me.RequestCompleteQuestServerValidated(0); // Repeat claim must fail
                        yield return new WaitForSeconds(0.5f);
                        bool questCompletedOnce = me.QuestIndex.Value == 1 && me.Gold.Value == goldAfterQuest && goldAfterQuest >= 25;

                        me.RequestMapPing(lp.transform.position);
                        me.RequestReadyCheck();

                        float waitDownCli = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitDownCli < 10f && me.Health.Value > 0)
                            yield return new WaitForSeconds(0.15f);
                        float waitRevCli = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitRevCli < 8f && me.Health.Value <= 0)
                            yield return new WaitForSeconds(0.15f);
                        bool clientRevived = me.Health.Value > 0;

                        int goldBeforeEvt = me.Gold.Value;
                        float waitEvt = Time.realtimeSinceStartup;
                        var lweCli = LivingWorldEvents.Instance ? LivingWorldEvents.Instance : FindFirstObjectByType<LivingWorldEvents>();
                        while (Time.realtimeSinceStartup - waitEvt < 10f && (lweCli == null || lweCli.CaravanState != LivingWorldEvents.EventState.Active || lweCli.AmbushState != LivingWorldEvents.EventState.Active || lp.Gold <= goldBeforeEvt))
                        {
                            lweCli = LivingWorldEvents.Instance ? LivingWorldEvents.Instance : FindFirstObjectByType<LivingWorldEvents>();
                            yield return new WaitForSeconds(0.2f);
                        }
                        bool eventSeen = lweCli != null && lweCli.CaravanState == LivingWorldEvents.EventState.Active && lweCli.AmbushState == LivingWorldEvents.EventState.Active;
                        bool noDoubleGold = lp.Gold == goldBeforeEvt + 20;

                        float waitHostDisc = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitHostDisc < 12f && !hostDisconnected)
                            yield return new WaitForSeconds(0.2f);
                        bool hostExitDetected = hostDisconnected;

                        fullAcceptPass = spoofBlocked && validAbHit && abHealOk && mountedOk && dismountOk && questAccepted && questCompletedOnce && clientRevived && eventSeen && noDoubleGold && hostExitDetected;
                        string cliMsg = $"{(fullAcceptPass ? "PASS" : "FAIL")} {role}: clientAccept={(fullAcceptPass ? "PASS" : "FAIL")}(spoofRej={spoofBlocked},abHit={validAbHit},abHeal={abHealOk},dismount={dismountOk},qAcc={questAccepted},qComp={questCompletedOnce},rev={clientRevived},evt={eventSeen},noDblG={noDoubleGold},hostDisc={hostExitDetected})";
                        Debug.Log(cliMsg);
                        System.Console.WriteLine(cliMsg);
                        yield return new WaitForSeconds(0.2f);
                        Application.Quit(fullAcceptPass ? 0 : 1);
                        yield break;
                    }
                }
                else
                {
                    fullAcceptPass = false;
                    combatInfo += ", fullAccept=FAIL(missingEntity)";
                }
            }

            bool authorityPass = true;
            if (CoopSessionConfig.AuthoritySmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                var lp = FindFirstObjectByType<PlayerController>();
                var targetEnemy = EnemyController.FindByKey("road-raider-1");
                LootChest targetChest = FindDeterministicTestChest(20, true);
                Landmark lm0 = null, lm1 = null, lm2 = null;
                foreach (var lm in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                {
                    if (!lm) continue;
                    if (lm.landmarkId == "willow-crossing") lm0 = lm;
                    else if (lm.landmarkId == "greenhollow-post") lm1 = lm;
                    else if (lm.landmarkId == "ancient-grove") lm2 = lm;
                }

                if (lp && targetEnemy && targetChest && lm0 && lm1 && lm2)
                {
                    var eStats = targetEnemy.GetComponent<Combatant>();
                    if (isHost)
                    {
                        var clientAvatar = GetFirstRemotePlayer();
                        ulong remoteCid = clientAvatar != null ? clientAvatar.OwnerClientId : 1UL;
                        yield return new WaitForSeconds(1.2f);

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(targetEnemy.transform.position + new Vector3(15.5f, 0f, 0f));
                        yield return new WaitForSeconds(3.2f);

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(targetEnemy.transform.position + new Vector3(2.2f, 0f, 0f));
                        yield return new WaitForSeconds(2.2f);

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(targetChest.transform.position + new Vector3(1.5f, 0f, 0f));
                        float waitChestCli = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitChestCli < 10f && (clientAvatar == null || clientAvatar.Gold.Value <= 0))
                            yield return new WaitForSeconds(0.2f);
                        int hostGoldBefore = RiftboundCoopPlayer.LocalOwner.Gold.Value;
                        RiftboundCoopPlayer.LocalOwner.AuthorizeHostTeleport(targetChest.transform.position + new Vector3(1.5f, 0f, 0f));
                        yield return new WaitForSeconds(0.35f);
                        bool hostChestOk = lp.OpenChest(targetChest) && RiftboundCoopPlayer.LocalOwner.Gold.Value > hostGoldBefore;

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(lm0.transform.position + new Vector3(1.5f, 0f, 0f));
                        int gBeforeLm0 = clientAvatar != null ? clientAvatar.Gold.Value : 0;
                        float waitLm0 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitLm0 < 10f && (clientAvatar == null || clientAvatar.Gold.Value < gBeforeLm0 + 15))
                            yield return new WaitForSeconds(0.2f);
                        yield return new WaitForSeconds(1.2f); // allow early zebra check + dup check

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(lm1.transform.position + new Vector3(1.5f, 0f, 0f));
                        int gBeforeLm1 = clientAvatar != null ? clientAvatar.Gold.Value : 0;
                        float waitLm1 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitLm1 < 10f && (clientAvatar == null || clientAvatar.Gold.Value < gBeforeLm1 + 15))
                            yield return new WaitForSeconds(0.2f);

                        if (clientAvatar != null)
                            clientAvatar.AuthorizeHostTeleport(lm2.transform.position + new Vector3(1.5f, 0f, 0f));
                        float waitLm2 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitLm2 < 10f && (clientAvatar == null || !clientAvatar.IsMounted.Value))
                            yield return new WaitForSeconds(0.2f);
                        bool zebraSeenOnHost = clientAvatar != null && clientAvatar.IsMounted.Value && clientAvatar.MountId.Value.ToString() == "zebra";
                        bool realMountMeshOk = clientAvatar != null && clientAvatar.RemoteMountComponent != null && !clientAvatar.HasRemoteCapsuleFallback && clientAvatar.RemoteMountComponent.animator != null && HasValidObserverGrounding(clientAvatar);

                        int gBeforeTemper = clientAvatar != null ? clientAvatar.Gold.Value : 0;
                        float waitTemperH = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitTemperH < 4f && (clientAvatar == null || clientAvatar.Gold.Value > gBeforeTemper - 45))
                            yield return new WaitForSeconds(0.15f);
                        yield return new WaitForSeconds(0.7f);

                        if (clientAvatar != null)
                            RiftboundCoopPlayer.LocalOwner.AuthorizeHostTeleport(clientAvatar.transform.position + new Vector3(22f, 0f, 0f));
                        yield return new WaitForSeconds(0.4f);
                        RiftboundCoopPlayer.DispatchEnemyHitToPlayer(remoteCid, 500f, targetEnemy.NetworkKey);
                        float waitDownH = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitDownH < 6f && clientAvatar != null && clientAvatar.Health.Value > 0)
                            yield return new WaitForSeconds(0.15f);
                        yield return new WaitForSeconds(0.2f);
                        bool mountClearedOnDeath = clientAvatar != null && !clientAvatar.IsMounted.Value && clientAvatar.RemoteMountComponent == null && IsWrapperAtRest(clientAvatar);
                        yield return new WaitForSeconds(1.6f); // Let dead client fail invalid RPCs
                        RiftboundCoopPlayer.LocalOwner.RequestReviveAlly(remoteCid); // Must fail > 5m
                        yield return new WaitForSeconds(0.6f);
                        bool farRevBlocked = clientAvatar != null && clientAvatar.Health.Value <= 0;

                        if (clientAvatar != null)
                            RiftboundCoopPlayer.LocalOwner.AuthorizeHostTeleport(clientAvatar.transform.position + new Vector3(1.5f, 0f, 0f));
                        yield return new WaitForSeconds(0.5f);
                        RiftboundCoopPlayer.LocalOwner.RequestReviveAlly(remoteCid);
                        float waitRevH = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitRevH < 5f && clientAvatar != null && clientAvatar.Health.Value <= 0)
                            yield return new WaitForSeconds(0.15f);
                        int revHp = clientAvatar != null ? clientAvatar.Health.Value : -1;
                        RiftboundCoopPlayer.LocalOwner.RequestReviveAlly(remoteCid); // Duplicate revive must not overwrite
                        yield return new WaitForSeconds(0.5f);
                        bool nearRevOk = revHp > 0 && clientAvatar != null && clientAvatar.Health.Value == revHp;

                        // Client is at ~50% HP after revive; wait for alive-respawn check + rest + repeat-rest check, then deal 90 dmg for potion test
                        float waitRestH = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - waitRestH < 8f && clientAvatar != null && clientAvatar.Health.Value < clientAvatar.MaxHealth.Value)
                            yield return new WaitForSeconds(0.2f);
                        yield return new WaitForSeconds(0.9f);
                        if (clientAvatar != null)
                        {
                            RiftboundCoopPlayer.DispatchEnemyHitToPlayer(clientAvatar.OwnerClientId, 90f, "smoke-test");
                            float waitPotH = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - waitPotH < 6f && clientAvatar.Health.Value < 100)
                                yield return new WaitForSeconds(0.2f);
                            yield return new WaitForSeconds(0.8f);
                        }

                        bool obsRevOk = HasValidObserverGrounding(clientAvatar) && clientAvatar.IsRemoteObserverAlive && !clientAvatar.IsRemoteObserverMounted && IsWrapperAtRest(clientAvatar);
                        authorityPass = hostChestOk && zebraSeenOnHost && realMountMeshOk && mountClearedOnDeath && farRevBlocked && nearRevOk && obsRevOk;
                        combatInfo += $", hostAuth={(authorityPass ? "PASS" : "FAIL")}(chest={hostChestOk},zebra={zebraSeenOnHost},realMnt={realMountMeshOk},mntClr={mountClearedOnDeath},farRevRej={farRevBlocked},nearRev={nearRevOk},obsGv={obsRevOk})";
                    }
                    else
                    {
                        var me = RiftboundCoopPlayer.LocalOwner;
                        string rightAb = me.ClassName.Value.ToString() == "Mage" ? "fireball" : "whirlwind";

                        Vector3 prePos = lp.transform.position;
                        me.SendSpoofedPositionForTest(prePos + new Vector3(200f, 0f, 0f));
                        yield return new WaitForSeconds(0.75f);
                        bool posClampOk = Vector3.Distance(lp.transform.position, prePos) < 25f;

                        float wtFar = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtFar < 8f && Vector3.Distance(lp.transform.position, targetEnemy.transform.position) < 12f)
                            yield return new WaitForSeconds(0.15f);
                        float hpBeforeFar = eStats.Health;
                        me.RequestAbilityAttack(eStats, "melee");
                        yield return new WaitForSeconds(0.6f);
                        bool outRangeRej = Mathf.Approximately(eStats.Health, hpBeforeFar);

                        // Verify immediate enemy-state replication on an accepted guest hit while enemy is idle (>14m pursuit range, zero 0.1s chasing ticks)
                        bool enemyIdleHitOk = true;
                        if (rightAb == "fireball")
                        {
                            float hpBeforeIdleHit = eStats.Health;
                            me.RequestAbilityAttack(eStats, "fireball");
                            float wtIdleHit = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wtIdleHit < 1.2f && Mathf.Approximately(eStats.Health, hpBeforeIdleHit))
                                yield return new WaitForSeconds(0.05f);
                            enemyIdleHitOk = hpBeforeIdleHit - eStats.Health >= 30f && eStats.Health > 0f;
                        }

                        float wtNear = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtNear < 8f && Vector3.Distance(lp.transform.position, targetEnemy.transform.position) > 5f)
                            yield return new WaitForSeconds(0.15f);
                        float hpBeforeUnk = eStats.Health;
                        me.RequestAbilityAttack(eStats, "spoof_unknown_ab");
                        yield return new WaitForSeconds(0.45f);
                        bool unknownAtkRej = Mathf.Approximately(eStats.Health, hpBeforeUnk);

                        float hpBeforeCd = eStats.Health;
                        me.RequestAbilityAttack(eStats, rightAb);
                        yield return new WaitForSeconds(0.05f);
                        me.RequestAbilityAttack(eStats, rightAb);
                        yield return new WaitForSeconds(0.9f);
                        float dmgDealt = hpBeforeCd - eStats.Health;
                        bool cooldownRej = dmgDealt > 0f && dmgDealt <= 42f;

                        float wtChest = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtChest < 8f && Vector3.Distance(lp.transform.position, targetChest.transform.position) > 4f)
                            yield return new WaitForSeconds(0.15f);
                        lp.OpenChest(targetChest);
                        float wtGold1 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtGold1 < 5f && me.Gold.Value <= 0)
                            yield return new WaitForSeconds(0.15f);
                        int goldAfterChest = me.Gold.Value;

                        // Discover 3 Greenhollow landmarks server-authoritatively & check Zebra policy
                        Landmark[] lms = { lm0, lm1, lm2 };
                        bool zebraBlockedEarly = false;
                        bool dupLmRej = false;
                        for (int i = 0; i < 3; i++)
                        {
                            float wlm = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wlm < 8f && Vector3.Distance(lp.transform.position, lms[i].transform.position) > 5f)
                                yield return new WaitForSeconds(0.15f);
                            int gBefore = me.Gold.Value;
                            lp.DiscoverLandmark(lms[i].landmarkId);
                            float wg = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wg < 4f && me.Gold.Value <= gBefore)
                                yield return new WaitForSeconds(0.15f);
                            if (i == 0)
                            {
                                int gDupBefore = me.Gold.Value;
                                me.RequestDiscoverLandmark(lms[0].landmarkId);
                                yield return new WaitForSeconds(0.45f);
                                dupLmRej = me.Gold.Value == gDupBefore;

                                me.RequestSetMount(true, "zebra");
                                yield return new WaitForSeconds(0.45f);
                                zebraBlockedEarly = !me.IsMounted.Value;
                            }
                        }

                        me.RequestSetMount(true, "zebra");
                        float wmZebra = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wmZebra < 4f && !me.IsMounted.Value)
                            yield return new WaitForSeconds(0.15f);
                        bool zebraUnlockedOk = me.IsMounted.Value && me.MountId.Value.ToString() == "zebra";

                        // Temper weapon once + duplicate temper rejection
                        bool temperOk = false;
                        if (me.Gold.Value >= 45)
                        {
                            me.RequestTransaction("temper_weapon");
                            float wTemp = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wTemp < 3f && !lp.TemperedWeapon)
                                yield return new WaitForSeconds(0.15f);
                            int goldAfterT1 = me.Gold.Value;
                            me.RequestTransaction("temper_weapon");
                            yield return new WaitForSeconds(0.45f);
                            temperOk = lp.TemperedWeapon && me.Gold.Value == goldAfterT1;
                        }

                        // Wait down -> check dead rejections -> wait revive
                        float wtDownC = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtDownC < 12f && me.Health.Value > 0)
                            yield return new WaitForSeconds(0.15f);
                        float enemyHpDead = eStats.Health;
                        int goldDead = me.Gold.Value;
                        me.RequestAbilityAttack(eStats, rightAb);
                        me.RequestSetMount(true, "horse");
                        me.RequestTransaction("buy_potion");
                        yield return new WaitForSeconds(0.8f);
                        bool deadRejOk = Mathf.Approximately(eStats.Health, enemyHpDead) && !me.IsMounted.Value && me.Gold.Value == goldDead;

                        float wtRevC = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtRevC < 12f && me.Health.Value <= 0)
                            yield return new WaitForSeconds(0.15f);
                        bool revBackOk = me.Health.Value > 0;
                        yield return new WaitForSeconds(1.2f); // Let host verify duplicate revive check before healing via rest!

                        // Wait for -25 dmg -> test rest heals to MaxHealth and costs 5g
                        float wtDmgRest = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtDmgRest < 8f && me.Health.Value >= me.MaxHealth.Value)
                            yield return new WaitForSeconds(0.15f);
                        bool restDmgOk = false;
                        bool restFullRej = false;
                        bool aliveRespRej = false;
                        bool potOk = false;
                        bool potCdRej = false;
                        int restG0 = me.Gold.Value, restH0 = me.Health.Value;
                        if (me.Gold.Value >= 5 && me.Health.Value < me.MaxHealth.Value)
                        {
                            Vector3 preRespPos = lp.transform.position;
                            int preRespHp = me.Health.Value;
                            me.RequestRespawn();
                            yield return new WaitForSeconds(0.5f);
                            aliveRespRej = me.Health.Value == preRespHp && me.Health.Value < me.MaxHealth.Value && Vector3.Distance(lp.transform.position, preRespPos) < 5f;

                            int gRest = me.Gold.Value;
                            me.RequestTransaction("rest");
                            float wtR = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wtR < 4f && (me.Health.Value < me.MaxHealth.Value || me.Gold.Value > gRest - 5))
                                yield return new WaitForSeconds(0.15f);
                            restDmgOk = me.Health.Value == me.MaxHealth.Value && me.Gold.Value == gRest - 5;

                            // Now Client is guaranteed full HP -> repeat rest must be rejected
                            int gAtFull = me.Gold.Value;
                            me.RequestTransaction("rest");
                            yield return new WaitForSeconds(0.45f);
                            restFullRej = me.Gold.Value == gAtFull;
                        }

                        // Wait for Host's -90 dmg hit -> verify server potion heal & cooldown reject without client desync
                        float wtPotDmg = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wtPotDmg < 6f && me.Health.Value > 50)
                            yield return new WaitForSeconds(0.15f);
                        if (me.Health.Value <= 50 && lp.Potions > 0)
                        {
                            int potsBeforeUse = lp.Potions;
                            int hpLow = me.Health.Value;
                            me.RequestUsePotion();
                            float wtPotUse = Time.realtimeSinceStartup;
                            while (Time.realtimeSinceStartup - wtPotUse < 3f && (me.Health.Value < hpLow + 70 || lp.Potions != potsBeforeUse - 1))
                                yield return new WaitForSeconds(0.1f);
                            potOk = me.Health.Value >= hpLow + 70 && lp.Potions == potsBeforeUse - 1;

                            int hpAfterPot = me.Health.Value;
                            me.RequestUsePotion(); // Within 1.5s server cooldown -> must reject with zero local potion/HP desync
                            me.RequestAbilityHeal(60, "spoof_unknown_heal"); // Unknown heal ID must reject cleanly
                            me.RequestPromoteLeader(me.OwnerClientId); // Non-leader promote must reject cleanly
                            me.RespondReadyCheckForTest(true, 999999u); // Stale/wrong readyCheckId must reject cleanly
                            yield return new WaitForSeconds(0.5f);
                            potCdRej = lp.Potions == potsBeforeUse - 1 && me.Health.Value == hpAfterPot;
                        }

                        uint enemySeqBeforeSpoof = targetEnemy.LastRemoteActionSeq;
                        byte enemyKindBeforeSpoof = targetEnemy.ActiveRemoteActionKind;
                        bool rpcSpoofThrew = me.AttemptSpoofedEnemyActionFromClientForTest(targetEnemy.NetworkKey);
                        var hostPeer = GetFirstRemotePlayer();
                        bool peerObsGvOk = HasValidObserverGrounding(hostPeer) && IsWrapperAtRest(hostPeer);
                        bool leaderSpoofRej = !me.IsLeader.Value && me.ReadyStatus.Value == 0;
                        bool enemySpoofRej = rpcSpoofThrew && targetEnemy.LastRemoteActionSeq == enemySeqBeforeSpoof && targetEnemy.ActiveRemoteActionKind == enemyKindBeforeSpoof && !targetEnemy.IsWarningActive;
                        authorityPass = posClampOk && outRangeRej && enemyIdleHitOk && unknownAtkRej && cooldownRej && goldAfterChest > 0 && aliveRespRej && restFullRej && dupLmRej && zebraBlockedEarly && zebraUnlockedOk && temperOk && deadRejOk && revBackOk && restDmgOk && potOk && potCdRej && leaderSpoofRej && peerObsGvOk && enemySpoofRej;
                        combatInfo += $", clientAuth={(authorityPass ? "PASS" : "FAIL")}(clamp={posClampOk},rngRej={outRangeRej},idleHit={enemyIdleHitOk},unkAtkRej={unknownAtkRej},cdRej={cooldownRej},aliveRespRej={aliveRespRej},restRej={restFullRej},dupLmRej={dupLmRej},zEarly={zebraBlockedEarly},zOk={zebraUnlockedOk},temper={temperOk},deadRej={deadRejOk},rev={revBackOk},restOk={restDmgOk}[g={restG0}->{me.Gold.Value},h={restH0}->{me.Health.Value}/{me.MaxHealth.Value}],potOk={potOk},potCdRej={potCdRej},ldrSpoofRej={leaderSpoofRej},obsGv={peerObsGvOk},enemySpoofRej={enemySpoofRej})";
                    }
                }
                else
                {
                    authorityPass = false;
                    combatInfo += ", auth=FAIL(missingEntity)";
                }
            }

            bool visualPass = true;
            if (CoopSessionConfig.VisualSmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                var me = RiftboundCoopPlayer.LocalOwner;
                var peer = GetFirstRemotePlayer();
                var lp = FindFirstObjectByType<PlayerController>();
                string capDir = CoopSessionConfig.CaptureDir;
                if (!string.IsNullOrEmpty(capDir)) System.IO.Directory.CreateDirectory(capDir);

                // Phase 1: Both mount Horse and observe real MountVisual (never capsule)
                me.RequestSetMount(true, "horse");
                float wH = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - wH < 5f && (peer == null || !peer.IsMounted.Value || peer.RemoteMountComponent == null))
                {
                    if (peer == null) peer = GetFirstRemotePlayer();
                    yield return new WaitForSeconds(0.15f);
                }
                if (lp && peer && lp.orbit)
                {
                    Vector3 look = peer.transform.position - lp.transform.position; look.y = 0f;
                    if (look.sqrMagnitude > 0.01f) lp.transform.rotation = Quaternion.LookRotation(look);
                }
                yield return new WaitForSeconds(0.6f);
                bool horseReal = peer != null && peer.IsMounted.Value && peer.RemoteMountComponent != null && !peer.HasRemoteCapsuleFallback && peer.MountId.Value.ToString() == "horse";
                bool obsBefore = HasValidObserverGrounding(peer);
                if (!string.IsNullOrEmpty(capDir))
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, (isHost ? "host" : "guest") + "_horse_roster.png"));
                    yield return new WaitForSeconds(0.4f);
                }

                // Phase 2: Switch live mount to Gryphon without dismounting
                me.RequestSetMount(true, "gryphon");
                float wG = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - wG < 5f && (peer == null || peer.MountId.Value.ToString() != "gryphon" || peer.RemoteMountComponent == null))
                    yield return new WaitForSeconds(0.15f);
                yield return new WaitForSeconds(0.6f);
                bool gryphonReal = peer != null && peer.IsMounted.Value && peer.RemoteMountComponent != null && !peer.HasRemoteCapsuleFallback && peer.MountId.Value.ToString() == "gryphon";
                if (!string.IsNullOrEmpty(capDir))
                {
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, (isHost ? "host" : "guest") + "_gryphon.png"));
                    yield return new WaitForSeconds(0.4f);
                }

                // Phase 3: Group Menu [O] check + Dismount & teardown
                if (!isHost)
                {
                    SetGroupMenuOpenForTest(true);
                    yield return new WaitForSeconds(0.3f);
                    if (!string.IsNullOrEmpty(capDir))
                    {
                        ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, "guest_group_menu.png"));
                        yield return new WaitForSeconds(0.4f);
                    }
                    CloseGroupMenu();
                }

                me.RequestSetMount(false, "horse");
                float wD = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - wD < 5f && (peer == null || peer.IsMounted.Value || peer.RemoteMountComponent != null))
                    yield return new WaitForSeconds(0.15f);
                yield return new WaitForSeconds(0.3f);
                bool dismountClear = peer != null && !peer.IsMounted.Value && peer.RemoteMountComponent == null;
                bool obsAfter = HasValidObserverGrounding(peer) && IsWrapperAtRest(peer);

                me.TriggerActionAnim("Dodge", 0.32f);
                yield return new WaitForSeconds(0.45f);
                visualPass = horseReal && gryphonReal && dismountClear && obsBefore && obsAfter;
                combatInfo += $", visualSmoke={(visualPass ? "PASS" : "FAIL")}(horseReal={horseReal},gryphonReal={gryphonReal},dismountClear={dismountClear},obsBefore={obsBefore},obsAfter={obsAfter})";
            }

            bool combatVisPass = true;
            if (CoopSessionConfig.CombatVisualSmoke && players >= 2 && RiftboundCoopPlayer.LocalOwner != null)
            {
                var cap = EnemyController.FindByKey("ashwood-captain");
                var capStats = cap ? cap.GetComponent<Combatant>() : null;
                var lp = FindFirstObjectByType<PlayerController>();
                string capDir = CoopSessionConfig.CaptureDir;
                if (!string.IsNullOrEmpty(capDir)) System.IO.Directory.CreateDirectory(capDir);

                if (cap && capStats)
                {
                    if (isHost)
                    {
                        if (!cap.gameObject.activeInHierarchy || !capStats.Alive)
                        {
                            cap.gameObject.SetActive(true);
                            cap.ResetForLoad();
                            RiftboundCoopPlayer.LocalOwner.BroadcastEnemyState(cap);
                            yield return new WaitForSeconds(0.25f);
                        }
                        var cli = GetFirstRemotePlayer();
                        Vector3 hostSpot = cap.transform.position + new Vector3(-1.4f, 0f, -3.2f);
                        Vector3 guestSpot = cap.transform.position + new Vector3(1.4f, 0f, -3.2f);
                        RiftboundCoopPlayer.LocalOwner.AuthorizeHostTeleport(hostSpot);
                        if (cli != null) cli.AuthorizeHostTeleport(guestSpot);
                        yield return new WaitForSeconds(0.45f);
                        if (lp && lp.orbit)
                        {
                            Vector3 look = cap.transform.position - lp.transform.position; look.y = 0f;
                            if (look.sqrMagnitude > 0.01f) lp.transform.rotation = Quaternion.LookRotation(look);
                        }

                        float wWindupH = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wWindupH < 10f && !cap.IsWarningActive && !cap.HasPendingAttack)
                            yield return new WaitForSeconds(0.08f);
                        bool hostSawWindup = cap.IsWarningActive || cap.HasPendingAttack;
                        yield return new WaitForSeconds(0.25f);
                        if (!string.IsNullOrEmpty(capDir))
                        {
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, "host_combat_windup.png"));
                            yield return new WaitForSeconds(0.35f);
                        }

                        // Give guest time to see heavy windup, send interrupt, and confirm finite stagger expiry before finishing kill
                        float wKill = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wKill < 22f && capStats.Alive && RiftboundCoopPlayer.LocalOwner != null)
                        {
                            if (Time.realtimeSinceStartup - wKill > 6.5f && capStats.Alive)
                                RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(capStats, "melee");
                            yield return new WaitForSeconds(0.5f);
                        }
                        yield return new WaitForSeconds(2.0f);
                        bool hostDeadClean = !capStats.Alive && !cap.IsWarningActive;
                        combatVisPass = hostSawWindup && hostDeadClean;
                        combatInfo += $", combatVisH={(combatVisPass ? "PASS" : "FAIL")}(windup={hostSawWindup},deadClr={hostDeadClean})";
                    }
                    else
                    {
                        float wPos = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wPos < 8f && (!cap.gameObject.activeInHierarchy || Vector3.Distance(lp.transform.position, cap.transform.position) > 8f))
                            yield return new WaitForSeconds(0.15f);
                        if (lp && lp.orbit)
                        {
                            Vector3 look = cap.transform.position - lp.transform.position; look.y = 0f;
                            if (look.sqrMagnitude > 0.01f) lp.transform.rotation = Quaternion.LookRotation(look);
                        }

                        float wAction = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wAction < 12f && RiftboundCoopPlayer.LocalOwner != null && cap.ActiveRemoteActionKind == 0 && !cap.IsWarningActive)
                            yield return new WaitForSeconds(0.08f);
                        bool guestSawWindupOrAtk = cap.ActiveRemoteActionKind != 0 || cap.IsWarningActive;
                        yield return new WaitForSeconds(0.2f);
                        bool notOverwrittenMidAction = cap.ActiveRemoteActionKind != 0;
                        if (!string.IsNullOrEmpty(capDir))
                        {
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, "guest_combat_windup.png"));
                            yield return new WaitForSeconds(0.35f);
                        }

                        // Test remote interrupt clearing warning & cast bar cleanly
                        if (RiftboundCoopPlayer.LocalOwner != null)
                            RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(capStats, "interrupt");
                        float wInt = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wInt < 4f && RiftboundCoopPlayer.LocalOwner != null && cap.ActiveRemoteActionKind != EnemyController.ActionInterrupt && (CastBarSystem.GetTargetCast(capStats) == null || !CastBarSystem.GetTargetCast(capStats).isInterrupted))
                            yield return new WaitForSeconds(0.08f);
                        yield return new WaitForSeconds(0.15f);
                        var tCast = CastBarSystem.GetTargetCast(capStats);
                        bool guestInterrupted = !cap.IsWarningActive && (cap.ActiveRemoteActionKind == EnemyController.ActionInterrupt || (tCast != null && tCast.isInterrupted));
                        if (!string.IsNullOrEmpty(capDir))
                        {
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, "guest_combat_interrupt.png"));
                            yield return new WaitForSeconds(0.35f);
                        }

                        // Verify finite expiry (no lock) and assist kill for death cleanup check
                        float wExp = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wExp < 3.5f && RiftboundCoopPlayer.LocalOwner != null && cap.ActiveRemoteActionKind == EnemyController.ActionInterrupt)
                            yield return new WaitForSeconds(0.1f);
                        bool expiryReturned = cap.ActiveRemoteActionKind != EnemyController.ActionInterrupt;

                        float wDeath = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - wDeath < 15f && capStats.Alive && RiftboundCoopPlayer.LocalOwner != null)
                        {
                            if (Time.realtimeSinceStartup - wDeath > 1.5f)
                                RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(capStats, "melee");
                            yield return new WaitForSeconds(0.5f);
                        }
                        yield return new WaitForSeconds(0.45f);
                        bool guestDeadClean = !capStats.Alive && !cap.IsWarningActive && cap.ActiveRemoteActionKind == 0;
                        if (!string.IsNullOrEmpty(capDir))
                        {
                            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(capDir, "guest_combat_death.png"));
                            yield return new WaitForSeconds(0.35f);
                        }

                        combatVisPass = guestSawWindupOrAtk && notOverwrittenMidAction && guestInterrupted && expiryReturned && guestDeadClean;
                        combatInfo += $", combatVisG={(combatVisPass ? "PASS" : "FAIL")}(saw={guestSawWindupOrAtk},held={notOverwrittenMidAction},int={guestInterrupted},exp={expiryReturned},deadClr={guestDeadClean})";
                    }
                }
                else
                {
                    combatVisPass = false;
                    combatInfo += ", combatVis=FAIL(missingCaptain)";
                }
            }

            bool pass = players >= minPlayers && RiftboundCoopPlayer.LocalOwner != null && activeCams == 1 && activeListeners == 1 && sep >= 1.0f && combatPass && lateJoinPass && progPass && clientLeavePass && fullAcceptPass && authorityPass && visualPass && combatVisPass;
            string msg = $"{(pass ? "PASS" : "FAIL")} {role}: players={players}, localOwner={(RiftboundCoopPlayer.LocalOwner != null)}, cams={activeCams}, listeners={activeListeners}, sep={sep:F2}m ({posInfo}){combatInfo}";
            Debug.Log(msg);
            System.Console.WriteLine(msg);

            yield return new WaitForSeconds(0.5f);
            Application.Quit(pass ? 0 : 1);
        }

        static bool HasValidObserverGrounding(RiftboundCoopPlayer p) =>
            p != null
            && !p.IsOwner
            && p.RemoteVisualRoot != null
            && p.RemoteAnimator != null
            && p.IsObserverContextBound
            && p.GetComponents<GroundedVisual>().Length == 1;

        static bool IsWrapperAtRest(RiftboundCoopPlayer p) =>
            p != null
            && p.RemoteVisualRoot != null
            && p.RemoteVisualRoot.transform.localPosition.sqrMagnitude < 0.0001f;

        /// <summary>Picks an un-gated LootChest deterministically across Host and Client.</summary>
        static LootChest FindDeterministicTestChest(int minGold, bool pickHighest)
        {
            LootChest best = null;
            foreach (var c in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
            {
                if (!c || string.IsNullOrEmpty(c.chestId) || c.gold < minGold || c.requiresCaptainDefeated || c.requiresWardenDefeated || c.chestId == RiverReaverCamp.CacheId)
                    continue;
                bool better = best == null ||
                    (pickHighest ? c.gold > best.gold : c.gold < best.gold) ||
                    (c.gold == best.gold && string.CompareOrdinal(c.chestId, best.chestId) < 0);
                if (better) best = c;
            }
            return best;
        }
    }
}
