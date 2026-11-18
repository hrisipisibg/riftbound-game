using System;
using UnityEngine;

namespace Riftbound.Network
{
    public enum CoopSessionMode
    {
        Offline = 0,
        HostOnline = 1,
        JoinOnline = 2,
        // Dev-only localhost/LAN fallback (hidden from release UI)
        HostLan = 3,
        JoinLan = 4
    }

    public static class CoopSessionConfig
    {
        public const string ProtocolVersion = "riftbound-coop-v1";

        public static CoopSessionMode Mode = CoopSessionMode.Offline;
        public static string JoinCode = string.Empty;
        public static string JoinIp = "127.0.0.1";
        public static ushort Port = 7777;
        public static string UgsProfile = string.Empty;
        public static string LastDisconnectReason = string.Empty;
        public static float AutoSmokeExitSeconds = 0f;
        public static bool CombatSmoke = false;
        public static bool InvalidCodeSmoke = false;
        public static bool ProgressionSmoke = false;
        public static bool LateJoinSmoke = false;
        public static bool ResilienceSmoke = false;
        public static bool ClientLeaveSmoke = false;
        public static bool FullAcceptanceSmoke = false;
        public static bool UiSmoke = false;
        public static bool AuthoritySmoke = false;
        public static bool VisualSmoke = false;
        public static bool CombatVisualSmoke = false;
        public static string CaptureDir = string.Empty;
        public static int ExpectedMinPlayers = 2;
        public static bool CliParsed { get; private set; }

        public static bool IsMultiplayer => Mode != CoopSessionMode.Offline;
        public static bool IsOnlineMode => Mode == CoopSessionMode.HostOnline || Mode == CoopSessionMode.JoinOnline;
        public static bool IsLanDevMode => Mode == CoopSessionMode.HostLan || Mode == CoopSessionMode.JoinLan;

        public static void ResetToOffline()
        {
            Mode = CoopSessionMode.Offline;
            LastDisconnectReason = string.Empty;
        }

        public static void ParseCommandLineOnce()
        {
            if (CliParsed) return;
            CliParsed = true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-coopOnlineHost")
                {
                    Mode = CoopSessionMode.HostOnline;
                }
                else if (args[i] == "-coopOnlineJoin")
                {
                    Mode = CoopSessionMode.JoinOnline;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        JoinCode = args[++i];
                }
                else if (args[i] == "-ugsProfile" && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    UgsProfile = args[++i];
                }
                else if (args[i] == "-coopHost")
                {
                    Mode = CoopSessionMode.HostLan;
                }
                else if (args[i] == "-coopJoin")
                {
                    Mode = CoopSessionMode.JoinLan;
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                        JoinIp = args[++i];
                }
                else if (args[i] == "-coopPort" && i + 1 < args.Length && ushort.TryParse(args[i + 1], out ushort p))
                {
                    Port = p;
                    i++;
                }
                else if (args[i] == "-coopSmoke" && i + 1 < args.Length && float.TryParse(args[i + 1], out float sec))
                {
                    AutoSmokeExitSeconds = Mathf.Max(2f, sec);
                    i++;
                }
                else if (args[i] == "-coopCombatSmoke")
                {
                    CombatSmoke = true;
                }
                else if (args[i] == "-coopInvalidCodeSmoke")
                {
                    InvalidCodeSmoke = true;
                    Mode = CoopSessionMode.JoinOnline;
                    JoinCode = "ZZZZ99";
                    AutoSmokeExitSeconds = 20f;
                }
                else if (args[i] == "-coopProgressionSmoke")
                {
                    ProgressionSmoke = true;
                }
                else if (args[i] == "-coopLateJoinSmoke")
                {
                    LateJoinSmoke = true;
                }
                else if (args[i] == "-coopResilienceSmoke")
                {
                    ResilienceSmoke = true;
                    Mode = CoopSessionMode.HostOnline;
                    AutoSmokeExitSeconds = 60f;
                }
                else if (args[i] == "-coopClientLeaveSmoke")
                {
                    ClientLeaveSmoke = true;
                }
                else if (args[i] == "-coopFullAcceptanceSmoke")
                {
                    FullAcceptanceSmoke = true;
                    if (AutoSmokeExitSeconds <= 0f) AutoSmokeExitSeconds = 75f;
                }
                else if (args[i] == "-coopUiSmoke")
                {
                    UiSmoke = true;
                }
                else if (args[i] == "-coopAuthoritySmoke")
                {
                    AuthoritySmoke = true;
                    if (AutoSmokeExitSeconds <= 0f) AutoSmokeExitSeconds = 70f;
                }
                else if (args[i] == "-coopVisualSmoke")
                {
                    VisualSmoke = true;
                    if (AutoSmokeExitSeconds <= 0f) AutoSmokeExitSeconds = 35f;
                }
                else if (args[i] == "-coopCombatVisualSmoke")
                {
                    CombatVisualSmoke = true;
                    if (AutoSmokeExitSeconds <= 0f) AutoSmokeExitSeconds = 40f;
                }
                else if (args[i] == "-coopCaptureDir" && i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    CaptureDir = args[++i];
                }
                else if (args[i] == "-coopMinPlayers" && i + 1 < args.Length && int.TryParse(args[i + 1], out int mp))
                {
                    ExpectedMinPlayers = Mathf.Clamp(mp, 1, 4);
                    i++;
                }
            }
        }
    }
}
