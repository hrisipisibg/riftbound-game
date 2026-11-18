using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Riftbound.Network
{
    public enum OnlineSessionState
    {
        Offline,
        Initializing,
        Authenticating,
        Creating,
        Joining,
        Connecting,
        InWorld,
        Leaving,
        Failed
    }

    public static class OnlineSessionService
    {
        public static OnlineSessionState State { get; private set; } = OnlineSessionState.Offline;
        public static string StatusMessage { get; private set; } = string.Empty;
        public static string LastError { get; private set; } = string.Empty;
        public static string ActiveJoinCode { get; private set; } = string.Empty;

        static IHostSession hostSession;
        static ISession clientSession;
        static Task pendingInitTask;
        static Task pendingSignInTask;
        static int attemptToken;
        static bool busy;

        public static bool IsBusy => busy || State == OnlineSessionState.Initializing || State == OnlineSessionState.Authenticating || State == OnlineSessionState.Creating || State == OnlineSessionState.Joining || State == OnlineSessionState.Connecting || State == OnlineSessionState.Leaving;
        public static bool HasActiveSession => hostSession != null || clientSession != null;

        public static string NormalizeJoinCode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            return raw.Trim().Replace(" ", "").Replace("-", "").ToUpperInvariant();
        }

        public static void CancelPendingAttempt()
        {
            attemptToken++;
            busy = false;
            if (State != OnlineSessionState.InWorld)
            {
                State = OnlineSessionState.Offline;
                StatusMessage = string.Empty;
            }
        }

        public static void ClearError()
        {
            LastError = string.Empty;
            if (State == OnlineSessionState.Failed)
            {
                State = OnlineSessionState.Offline;
                StatusMessage = string.Empty;
            }
        }

        static async Task<bool> EnsureServicesAndAuthAsync(int token)
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                State = OnlineSessionState.Initializing;
                StatusMessage = "Initializing online services...";
                Task initTask = pendingInitTask;
                if (initTask == null)
                {
                    var initOpts = new InitializationOptions();
                    if (!string.IsNullOrWhiteSpace(CoopSessionConfig.UgsProfile))
                    {
                        initOpts.SetProfile(CoopSessionConfig.UgsProfile.Trim());
                    }
                    initTask = UnityServices.InitializeAsync(initOpts);
                    pendingInitTask = initTask;
                }
                try { await initTask; }
                finally { if (ReferenceEquals(pendingInitTask, initTask)) pendingInitTask = null; }
                if (token != attemptToken) return false;
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                State = OnlineSessionState.Authenticating;
                StatusMessage = "Authenticating player session...";
                Task signInTask = pendingSignInTask;
                if (signInTask == null)
                {
                    signInTask = AuthenticationService.Instance.SignInAnonymouslyAsync();
                    pendingSignInTask = signInTask;
                }
                try { await signInTask; }
                finally { if (ReferenceEquals(pendingSignInTask, signInTask)) pendingSignInTask = null; }
                if (token != attemptToken) return false;
            }

            return true;
        }

        public static async Task<bool> StartHostOnlineAsync(Action configureNetworkBeforeStart)
        {
            if (IsBusy) return false;
            busy = true;
            int token = ++attemptToken;
            LastError = string.Empty;
            ActiveJoinCode = string.Empty;

            try
            {
                if (!await EnsureServicesAndAuthAsync(token)) return false;
                if (token != attemptToken) return false;

                State = OnlineSessionState.Creating;
                StatusMessage = "Creating private 4-player Relay session...";
                configureNetworkBeforeStart?.Invoke();

                var options = new SessionOptions
                {
                    MaxPlayers = CoopWorldManager.MaxPlayers,
                    IsPrivate = true,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { "proto", new SessionProperty(CoopSessionConfig.ProtocolVersion, VisibilityPropertyOptions.Public) }
                    }
                }.WithRelayNetwork();

                IHostSession created = await MultiplayerService.Instance.CreateSessionAsync(options);
                if (token != attemptToken)
                {
                    // Cancelled while creating — clean up late session
                    try { await created.DeleteAsync(); } catch { }
                    return false;
                }

                hostSession = created;
                ActiveJoinCode = created.Code ?? string.Empty;
                State = OnlineSessionState.InWorld;
                StatusMessage = $"Online Co-op Ready (Code: {ActiveJoinCode})";
                Debug.Log($"[OnlineSessionService] Host session created. Code={ActiveJoinCode}");
                return true;
            }
            catch (Exception ex)
            {
                if (token == attemptToken)
                {
                    State = OnlineSessionState.Failed;
                    LastError = FormatFriendlyError(ex, isJoin: false);
                    StatusMessage = LastError;
                    Debug.LogWarning($"[OnlineSessionService] Host failed: {ex.Message}");
                }
                return false;
            }
            finally
            {
                if (token == attemptToken) busy = false;
            }
        }

        public static async Task<bool> JoinOnlineByCodeAsync(string rawCode, Action configureNetworkBeforeStart)
        {
            if (IsBusy) return false;
            string code = NormalizeJoinCode(rawCode);
            if (code.Length < 4)
            {
                State = OnlineSessionState.Failed;
                LastError = "Please enter a valid join code.";
                StatusMessage = LastError;
                return false;
            }

            busy = true;
            int token = ++attemptToken;
            LastError = string.Empty;
            ActiveJoinCode = string.Empty;

            try
            {
                if (!await EnsureServicesAndAuthAsync(token)) return false;
                if (token != attemptToken) return false;

                State = OnlineSessionState.Joining;
                StatusMessage = $"Joining session {code}...";
                configureNetworkBeforeStart?.Invoke();

                ISession joined = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
                if (token != attemptToken)
                {
                    try { await joined.LeaveAsync(); } catch { }
                    return false;
                }

                if (joined.Properties != null && joined.Properties.TryGetValue("proto", out var protoProp))
                {
                    if (!string.Equals(protoProp.Value, CoopSessionConfig.ProtocolVersion, StringComparison.Ordinal))
                    {
                        try { await joined.LeaveAsync(); } catch { }
                        State = OnlineSessionState.Failed;
                        LastError = "Incompatible game version. Both players must use the same build.";
                        StatusMessage = LastError;
                        return false;
                    }
                }

                clientSession = joined;
                ActiveJoinCode = code;
                State = OnlineSessionState.InWorld;
                StatusMessage = $"Joined Online Co-op ({code})";
                Debug.Log($"[OnlineSessionService] Joined session {code}");
                return true;
            }
            catch (Exception ex)
            {
                if (token == attemptToken)
                {
                    State = OnlineSessionState.Failed;
                    LastError = FormatFriendlyError(ex, isJoin: true);
                    StatusMessage = LastError;
                    Debug.LogWarning($"[OnlineSessionService] Join failed: {ex.Message}");
                }
                return false;
            }
            finally
            {
                if (token == attemptToken) busy = false;
            }
        }

        public static async Task LeaveSessionAsync()
        {
            attemptToken++;
            State = OnlineSessionState.Leaving;
            StatusMessage = "Leaving online session...";

            try
            {
                if (hostSession != null)
                {
                    var hs = hostSession;
                    hostSession = null;
                    await hs.DeleteAsync();
                }
                else if (clientSession != null)
                {
                    var cs = clientSession;
                    clientSession = null;
                    await cs.LeaveAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OnlineSessionService] Leave cleanup warning: {ex.Message}");
            }
            finally
            {
                ActiveJoinCode = string.Empty;
                State = OnlineSessionState.Offline;
                StatusMessage = string.Empty;
                busy = false;
            }
        }

        static string FormatFriendlyError(Exception ex, bool isJoin)
        {
            string msg = ex?.Message ?? "Unknown error";
            string lower = msg.ToLowerInvariant();
            if (lower.Contains("not found") || lower.Contains("invalid") || lower.Contains("expired"))
                return isJoin ? "Join code is invalid or expired." : "Session configuration invalid.";
            if (lower.Contains("full") || lower.Contains("max players") || lower.Contains("capacity"))
                return "Session is full (4/4 players).";
            if (lower.Contains("rate") || lower.Contains("too many") || lower.Contains("429"))
                return "Too many requests. Wait a moment and retry.";
            if (lower.Contains("network") || lower.Contains("resolve") || lower.Contains("timeout") || lower.Contains("unavailable"))
                return "Online services unavailable. Check your internet connection or try Offline mode.";
            if (lower.Contains("auth") || lower.Contains("permission") || lower.Contains("forbidden") || lower.Contains("403"))
                return "Online authentication/service permission failed. Verify Unity Cloud dashboard settings.";
            return msg.Length > 110 ? msg.Substring(0, 110) + "..." : msg;
        }
    }
}
