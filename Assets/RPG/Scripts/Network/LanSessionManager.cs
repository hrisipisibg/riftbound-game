using System;
using System.Collections;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Riftbound.Networking
{
    public class LanSessionManager : MonoBehaviour
    {
        public static LanSessionManager Instance { get; private set; }
        public const int MaxPlayers = 4;

        string addressInput = "127.0.0.1";
        ushort port = 7777;
        string statusMessage = "Offline";
        Texture2D whiteTex;

        public string StatusMessage => statusMessage;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback += OnClientConnected;
                nm.OnClientDisconnectCallback += OnClientDisconnected;
            }
            TryAutoCliSession();
        }

        void TryAutoCliSession()
        {
            string[] args = Environment.GetCommandLineArgs();
            string role = null;
            string outFile = null;
            ushort cliPort = port;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-lanRole") role = args[i + 1].ToLowerInvariant();
                else if (args[i] == "-lanVerifyOut") outFile = args[i + 1];
                else if (args[i] == "-lanPort" && ushort.TryParse(args[i + 1], out ushort p)) cliPort = p;
            }
            if (string.IsNullOrEmpty(role)) return;
            StartCoroutine(RunCliVerification(role, cliPort, outFile));
        }

        IEnumerator RunCliVerification(string role, ushort cliPort, string outFile)
        {
            yield return new WaitForSeconds(0.25f);
            float deadline = Time.realtimeSinceStartup + 45f;

            if (role == "host")
            {
                if (!StartHostLan(cliPort))
                {
                    WriteVerify(outFile, $"FAIL: Host failed to start on port {cliPort}");
                    Application.Quit(1);
                    yield break;
                }

                while (Time.realtimeSinceStartup < deadline)
                {
                    var nm = NetworkManager.Singleton;
                    if (nm && nm.ConnectedClientsIds.Count >= 2)
                    {
                        var players = FindObjectsByType<RiftboundNetworkPlayer>(FindObjectsSortMode.None);
                        if (players.Length >= 2)
                        {
                            Vector3 p0Start = players[0].transform.position;
                            Vector3 p1Start = players[1].transform.position;
                            float sep = Vector3.Distance(p0Start, p1Start);
                            yield return new WaitForSeconds(1.0f);
                            float p1Moved = Vector3.Distance(p1Start, players[1].transform.position);
                            float p0Moved = Vector3.Distance(p0Start, players[0].transform.position);
                            WriteVerify(outFile, $"PASS Host: clients={nm.ConnectedClientsIds.Count}, avatars={players.Length}, spawnSep={sep:F2}m, p0Moved={p0Moved:F2}m, p1Moved={p1Moved:F2}m");
                            yield return new WaitForSeconds(0.5f);
                            DisconnectLan();
                            Application.Quit(0);
                            yield break;
                        }
                    }
                    yield return null;
                }
                WriteVerify(outFile, $"FAIL Host: timed out waiting for 2nd player (status={statusMessage})");
                DisconnectLan();
                Application.Quit(1);
            }
            else if (role == "client")
            {
                if (!JoinClientLan("127.0.0.1", cliPort))
                {
                    WriteVerify(outFile, $"FAIL Client: failed to start client");
                    Application.Quit(1);
                    yield break;
                }

                while (Time.realtimeSinceStartup < deadline)
                {
                    var nm = NetworkManager.Singleton;
                    if (nm && nm.IsConnectedClient)
                    {
                        var players = FindObjectsByType<RiftboundNetworkPlayer>(FindObjectsSortMode.None);
                        RiftboundNetworkPlayer owned = null;
                        int ownedCount = 0;
                        foreach (var p in players) { if (p.IsOwner) { owned = p; ownedCount++; } }

                        if (owned != null && players.Length >= 2)
                        {
                            Vector3 startPos = owned.transform.position;
                            for (int k = 0; k < 10; k++)
                            {
                                owned.SubmitMoveInputServerRpc(new Vector2(1f, 0f));
                                yield return new WaitForSeconds(0.06f);
                            }
                            float moved = Vector3.Distance(startPos, owned.transform.position);
                            WriteVerify(outFile, $"PASS Client: clientId={nm.LocalClientId}, avatars={players.Length}, ownedCount={ownedCount}, moved={moved:F2}m");
                            yield return new WaitForSeconds(0.4f);
                            DisconnectLan();
                            Application.Quit(0);
                            yield break;
                        }
                    }
                    yield return null;
                }
                WriteVerify(outFile, $"FAIL Client: timed out (status={statusMessage})");
                DisconnectLan();
                Application.Quit(1);
            }
        }

        static void WriteVerify(string path, string content)
        {
            if (string.IsNullOrEmpty(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content + "\n");
        }

        void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
                nm.ConnectionApprovalCallback = null;
            }
        }

        void OnClientConnected(ulong id)
        {
            var nm = NetworkManager.Singleton;
            int count = nm && nm.ConnectedClientsIds != null ? nm.ConnectedClientsIds.Count : 1;
            statusMessage = $"Connected ({count}/{MaxPlayers} players)";
        }

        void OnClientDisconnected(ulong id)
        {
            var nm = NetworkManager.Singleton;
            if (nm && nm.IsHost)
            {
                statusMessage = $"Client {id} disconnected";
            }
            else
            {
                string reason = nm && !string.IsNullOrEmpty(nm.DisconnectReason) ? nm.DisconnectReason : "Disconnected from host";
                statusMessage = reason;
            }
        }

        void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            int currentCount = nm && nm.ConnectedClientsIds != null ? nm.ConnectedClientsIds.Count : 0;
            if (currentCount >= MaxPlayers)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Reason = $"Session full (max {MaxPlayers} players).";
                return;
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
            int slot = currentCount;
            float angle = slot * (Mathf.PI * 0.5f);
            response.Position = new Vector3(Mathf.Cos(angle) * 2.5f, 1.0f, Mathf.Sin(angle) * 2.5f);
            response.Rotation = Quaternion.identity;
        }

        public bool StartHostLan(ushort customPort = 0)
        {
            if (customPort > 0) port = customPort;
            var nm = NetworkManager.Singleton;
            if (!nm) { statusMessage = "No NetworkManager in scene."; return false; }

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = ApprovalCheck;

            var utp = nm.GetComponent<UnityTransport>();
            if (utp) utp.SetConnectionData("127.0.0.1", port, "0.0.0.0");

            bool ok = nm.StartHost();
            statusMessage = ok ? $"Hosting LAN on port {port} (1/{MaxPlayers})" : "Failed to start Host";
            return ok;
        }

        public bool JoinClientLan(string ip, ushort customPort = 0)
        {
            if (customPort > 0) port = customPort;
            var nm = NetworkManager.Singleton;
            if (!nm) { statusMessage = "No NetworkManager in scene."; return false; }

            nm.NetworkConfig.ConnectionApproval = true;
            string targetIp = string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim();
            var utp = nm.GetComponent<UnityTransport>();
            if (utp) utp.SetConnectionData(targetIp, port);

            bool ok = nm.StartClient();
            statusMessage = ok ? $"Connecting to {targetIp}:{port}..." : "Failed to start Client";
            return ok;
        }

        public void DisconnectLan()
        {
            var nm = NetworkManager.Singleton;
            if (nm && (nm.IsHost || nm.IsClient || nm.IsServer))
            {
                nm.ConnectionApprovalCallback = null;
                nm.Shutdown();
                statusMessage = "Offline";
            }
        }

        void OnGUI()
        {
            if (whiteTex == null) whiteTex = Texture2D.whiteTexture;
            var nm = NetworkManager.Singleton;
            bool connected = nm && (nm.IsHost || nm.IsClient || nm.IsServer);

            Rect panel = new Rect(16f, 16f, 260f, connected ? 115f : 155f);
            var prev = GUI.color;
            GUI.color = new Color(0.08f, 0.07f, 0.06f, 0.9f);
            GUI.DrawTexture(panel, whiteTex);
            GUI.color = new Color(0.85f, 0.7f, 0.25f, 0.95f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 2f), whiteTex);
            GUI.color = Color.white;

            GUILayout.BeginArea(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, panel.height - 14f));
            GUILayout.Label($"<b>LAN Co-op</b>  ({statusMessage})");

            if (!connected)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP:", GUILayout.Width(24f));
                addressInput = GUILayout.TextField(addressInput);
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Host LAN (2–4 Players)")) StartHostLan();
                if (GUILayout.Button("Join LAN")) JoinClientLan(addressInput);
            }
            else
            {
                int count = nm.ConnectedClientsList != null ? nm.ConnectedClientsList.Count : 1;
                GUILayout.Label($"Players Connected: {count}/{MaxPlayers}");
                if (GUILayout.Button("Disconnect")) DisconnectLan();
            }

            GUILayout.EndArea();
            GUI.color = prev;
        }
    }
}
