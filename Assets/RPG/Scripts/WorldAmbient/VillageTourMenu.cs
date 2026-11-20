using Riftbound.Network;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    /// <summary>Added to the map by the isolated village-tour build.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class VillageTourMenu : MonoBehaviour
    {
        public const string Product = "Riftbound Village Tour";

        internal readonly struct Stop
        {
            public readonly string Name, Details;
            public readonly Vector3 Position;
            public readonly float Heading;

            public Stop(string name, string details, float x, float z, float heading)
            {
                Name = name;
                Details = details;
                Position = new Vector3(x, 0, z);
                Heading = heading;
            }
        }

        internal static readonly Stop[] Stops =
        {
            new Stop("Highspire East Ward", "Gate arch and raised guild terrace", -138, -55, 0),
            new Stop("Emberwatch Civic Square", "Market lane and open smithy", 28, -24.3f, 180),
            new Stop("Emberwatch Supply Lane", "Loaded wagon and herb beds", 36, -40.9f, 90),
            new Stop("Greenhollow Rest Stop", "Shelter and honey stand west of the road", -25.6f, 377.15f, 270),
            new Stop("Gor’Kresh Forge Court", "Wide arch and kiln apron", 340, 257, 0),
            new Stop("Rustwater Salvage Yard", "Ore drum and raised service crossing", 225, 156, 0),
            new Stop("Deathknell Cloister", "Lanterns and low stone ramp", -253, 156.8f, 90),
            new Stop("Silverglen Arrival Court", "Reading pavilion and forest approach", -128, 65, 180),
            new Stop("Frostpeak Trail Station", "Granite terrace and windbreak", -112, -101.9f, 180),
            new Stop("Westbank Ferry Landing", "Follow the west bank around the bridge rail", -279.7f, 50.1f, 95),
            new Stop("Bloodrock Trail Lookout", "Ramp to the lookout and mounted bypass", 294.7f, 193, 180),
        };

        PlayerController player;
        OrbitCamera pausedCamera;
        bool hasReturnPoint;
        Vector3 returnPoint;
        float returnHeading, previousTimeScale;
        CursorLockMode previousCursorLock;
        bool previousCursorVisibility;
        Vector2 scroll;
        string status;
        GUIStyle header, subhead, cardTitle, cardDescription, button, card;

        internal static bool IsOffline =>
            !CoopSessionConfig.IsMultiplayer && OnlineSessionService.State == OnlineSessionState.Offline &&
            !(NetworkManager.Singleton && NetworkManager.Singleton.IsListening);

        void Update()
        {
            if (Application.productName != Product) return;
            var keys = Keyboard.current;
            if (pausedCamera)
            {
                if (!player || FrontendMenu.IsOpen || !IsOffline || (keys != null && keys.f8Key.wasPressedThisFrame))
                    Close();
                return;
            }
            if (keys == null || !keys.f8Key.wasPressedThisFrame || FrontendMenu.IsOpen || !IsOffline || !WorldRegions.Expanded)
                return;

            if (!player) player = Object.FindAnyObjectByType<PlayerController>();
            if (!player) return;
            if (!player.CanUseVillageTour || !player.orbit || !player.orbit.enabled || player.orbit.Paused)
            {
                player.Message = "Use the village tour on foot, outside combat, with other menus closed.";
                return;
            }
            Open();
        }

        void LateUpdate()
        {
            // Keep the camera disabled through its Update so Escape cannot open the settings menu underneath.
            if (pausedCamera && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        void Open()
        {
            pausedCamera = player.orbit;
            previousTimeScale = Time.timeScale;
            previousCursorLock = Cursor.lockState;
            previousCursorVisibility = Cursor.visible;
            pausedCamera.enabled = false;
            pausedCamera.SetPaused(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            status = null;
        }

        void Close()
        {
            if (pausedCamera)
            {
                pausedCamera.enabled = true;
                pausedCamera.SetPaused(false);
            }
            pausedCamera = null;
            Time.timeScale = previousTimeScale;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisibility;
        }

        void OnDisable()
        {
            if (pausedCamera) Close();
        }

        void Travel(Stop stop)
        {
            var departure = player.transform.position;
            float heading = player.transform.eulerAngles.y;
            if (!player.TryVillageTourTravel(stop.Position, stop.Heading, true))
            {
                status = "This arrival point is blocked. Choose another destination.";
                return;
            }
            if (!hasReturnPoint)
            {
                returnPoint = departure;
                returnHeading = heading;
                hasReturnPoint = true;
            }
            Close();
            player.Message = stop.Name;
        }

        void TravelBack()
        {
            if (!player.TryVillageTourTravel(returnPoint, returnHeading, false))
            {
                status = "Your original arrival point is blocked.";
                return;
            }
            hasReturnPoint = false;
            Close();
            player.Message = "Returned to your tour starting point.";
        }

        void OnGUI()
        {
            if (!pausedCamera || !player) return;
            if (header == null) PrepareStyles();
            int previousDepth = GUI.depth;
            GUI.depth = -1000;
            GUI.ModalWindow(741809, new Rect(0, 0, Screen.width, Screen.height), DrawTour,
                GUIContent.none, GUIStyle.none);
            GUI.depth = previousDepth;
        }

        void DrawTour(int id)
        {
            Color previousColor = GUI.color;
            GUI.color = new Color(0, 0, 0, .62f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            float width = Mathf.Min(760, Screen.width - 28);
            float height = Mathf.Min(578, Screen.height - 28);
            var panel = new Rect((Screen.width - width) * .5f, (Screen.height - height) * .5f, width, height);
            WowTheme.Panel(panel);
            GUI.BeginGroup(panel);
            GUI.Label(new Rect(24, 17, width - 120, 31), "Village tour", header);
            GUI.Label(new Rect(24, 51, width - 48, 24), "Pick an arrival point, then explore on foot or mount.", subhead);
            if (GUI.Button(new Rect(width - 91, 21, 66, 28), "Close", button))
            {
                GUI.EndGroup();
                Close();
                return;
            }

            int columns = width >= 600 ? 2 : 1;
            int rows = (Stops.Length + columns - 1) / columns;
            const float rowHeight = 64;
            float viewportHeight = height - 138;
            float contentWidth = width - 48 - (rows * rowHeight > viewportHeight ? 18 : 0);
            float cardWidth = (contentWidth - (columns - 1) * 10) / columns;
            scroll = GUI.BeginScrollView(new Rect(24, 82, width - 48, viewportHeight), scroll,
                new Rect(0, 0, contentWidth, rows * rowHeight));
            Stop? pressed = null;
            for (int i = 0; i < Stops.Length; i++)
            {
                Stop stop = Stops[i];
                float x = (i % columns) * (cardWidth + 10);
                float y = (i / columns) * rowHeight;
                if (GUI.Button(new Rect(x, y, cardWidth, rowHeight - 9), GUIContent.none, card)) pressed = stop;
                GUI.Label(new Rect(x + 12, y + 6, cardWidth - 24, 21), stop.Name, cardTitle);
                GUI.Label(new Rect(x + 12, y + 28, cardWidth - 24, 19), stop.Details, cardDescription);
            }
            GUI.EndScrollView();
            GUI.Label(new Rect(25, height - 46, width - 218, 34), status ?? "F8 or Esc to close", subhead);
            bool beforeEnabled = GUI.enabled;
            GUI.enabled = beforeEnabled && hasReturnPoint;
            bool goBack = GUI.Button(new Rect(width - 186, height - 43, 160, 28), "Return to start", button);
            GUI.enabled = beforeEnabled;
            GUI.EndGroup();
            if (pressed.HasValue) Travel(pressed.Value);
            else if (goBack) TravelBack();
        }

        void PrepareStyles()
        {
            header = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
            header.normal.textColor = WowTheme.Parchment;
            subhead = new GUIStyle(GUI.skin.label) { fontSize = 13, wordWrap = true };
            subhead.normal.textColor = new Color(.76f, .72f, .61f);
            cardTitle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, clipping = TextClipping.Clip };
            cardTitle.normal.textColor = WowTheme.Parchment;
            cardDescription = new GUIStyle(subhead) { wordWrap = false, clipping = TextClipping.Clip };
            button = new GUIStyle(GUI.skin.button);
            card = new GUIStyle(GUI.skin.button);
            WowTheme.StyleButton(button);
            WowTheme.StyleButton(card);
        }
    }
}
