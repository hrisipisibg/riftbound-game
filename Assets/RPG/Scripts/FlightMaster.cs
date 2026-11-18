using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class FlightMaster : MonoBehaviour
    {
        public static FlightMaster Instance { get; private set; }
        public static FlightMaster ActiveFlightMaster { get; private set; }

        public string nodeName = "Emberwatch Gryphon Master";
        public string faction = "Alliance"; // "Alliance", "Horde", "Neutral"
        public Vector3 flightStationPos;

        public struct FlightPathDestination
        {
            public string name;
            public string zone;
            public string faction;
            public Vector3 targetPos;
            public int costGold;
        }

        public static FlightPathDestination[] WorldFlightNodes => WorldRegions.Expanded ? ExpandedFlightNodes : LegacyFlightNodes;
        static readonly FlightPathDestination[] ExpandedFlightNodes = {
            new FlightPathDestination { name = "Highspire Keep", zone = "Highspire Keep", faction = "Alliance", targetPos = WorldRegions.Highspire, costGold = 5 },
            new FlightPathDestination { name = "Emberwatch Outpost", zone = "Amber Vale", faction = "Alliance", targetPos = new Vector3(2, .5f, 10), costGold = 0 },
            new FlightPathDestination { name = "Duskwood Cathedral Grounds", zone = "Duskwood", faction = "Neutral", targetPos = WorldRegions.Cathedral + new Vector3(5, 0, -10), costGold = 5 },
            new FlightPathDestination { name = "Camp Bloodrock", zone = "Redridge Canyons", faction = "Horde", targetPos = WorldRegions.Bloodrock + new Vector3(-5, 0, -5), costGold = 5 },
            new FlightPathDestination { name = "Gor'Kresh Stronghold", zone = "Gor'Kresh", faction = "Horde", targetPos = WorldRegions.Stronghold, costGold = 5 },
            new FlightPathDestination { name = "Wyrmcrest Summit", zone = "Ironfang Peaks", faction = "Neutral", targetPos = WorldRegions.Summit + new Vector3(0, 1, -10), costGold = 10 },
            new FlightPathDestination { name = "Greenhollow Post", zone = "Greenhollow Valley", faction = "Neutral", targetPos = WorldRegions.Greenhollow + new Vector3(5, 0, -8), costGold = 5 }
        };
        static readonly FlightPathDestination[] LegacyFlightNodes = new FlightPathDestination[]
        {
            new FlightPathDestination { name = "Highspire Keep (Alliance Capital)", zone = "Highspire Keep", faction = "Alliance", targetPos = new Vector3(-45f, 1.2f, -20f), costGold = 5 },
            new FlightPathDestination { name = "Emberwatch Outpost", zone = "Amber Vale", faction = "Alliance", targetPos = new Vector3(2f, 0.5f, 10f), costGold = 0 },
            new FlightPathDestination { name = "Duskwood Cathedral Grounds", zone = "Duskwood", faction = "Neutral", targetPos = new Vector3(-50f, 12.8f, 205f), costGold = 5 },
            new FlightPathDestination { name = "Camp Bloodrock", zone = "Redridge Canyons", faction = "Horde", targetPos = new Vector3(50f, 9.2f, 210f), costGold = 5 },
            new FlightPathDestination { name = "Gor'Kresh Stronghold (Horde Capital)", zone = "Gor'Kresh", faction = "Horde", targetPos = new Vector3(85f, 14.0f, 235f), costGold = 5 },
            new FlightPathDestination { name = "Wyrmcrest Summit (Mountain Peak)", zone = "Ironfang Peaks", faction = "Neutral", targetPos = new Vector3(0f, 49f, 215f), costGold = 10 }
        };

        public bool TaxiMapOpen { get; private set; }
        public bool InFlight { get; private set; }
        public static bool IsAnyTaxiInFlight { get; private set; }
        public static bool IsAnyTaxiMapOpen =>
            (ActiveFlightMaster != null && ActiveFlightMaster.TaxiMapOpen) ||
            (Instance != null && Instance.TaxiMapOpen);

        PlayerController player;
        GUIStyle winTitleStyle, nodeBtnStyle, subStyle, costStyle;
        Texture2D mapBgTex, borderTex;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            ActiveFlightMaster = null;
            IsAnyTaxiInFlight = false;
        }

        void Awake()
        {
            flightStationPos = transform.position;
            if (!Instance) Instance = this;
            player = FindAnyObjectByType<PlayerController>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (ActiveFlightMaster == this) ActiveFlightMaster = null;
            if (InFlight) IsAnyTaxiInFlight = false;
            if (mapBgTex) Destroy(mapBgTex);
            if (borderTex) Destroy(borderTex);
        }

        public static void CloseActiveTaxiWindow()
        {
            if (ActiveFlightMaster != null) ActiveFlightMaster.CloseTaxiWindow();
            if (Instance != null) Instance.CloseTaxiWindow();
        }

        public static FlightMaster GetNearby(Vector3 pos, float radius = 3.5f)
        {
            FlightMaster nearest = null;
            float minDst = radius;
            foreach (var fm in FindObjectsByType<FlightMaster>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(pos, fm.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    nearest = fm;
                }
            }
            return nearest;
        }

        public static void OpenTaxiWindow(FlightMaster fm)
        {
            if (!fm) return;
            ActiveFlightMaster = fm;
            Instance = fm;
            fm.TaxiMapOpen = true;
            AudioEffects.PlayClick();
            if (fm.player == null) fm.player = FindFirstObjectByType<PlayerController>();
            if (fm.player)
            {
                fm.player.ChatOpen = false;
                fm.player.Message = $"[{fm.nodeName}] Select a flight taxi destination.";
            }
        }

        public void CloseTaxiWindow()
        {
            TaxiMapOpen = false;
            if (ActiveFlightMaster == this) ActiveFlightMaster = null;
        }

        public void TakeFlight(FlightPathDestination dest)
        {
            if (!player) player = FindAnyObjectByType<PlayerController>();
            if (!player || InFlight || IsAnyTaxiInFlight) return;

            if (player.Gold < dest.costGold)
            {
                player.Message = "Not enough gold for flight taxi!";
                return;
            }

            if (dest.costGold > 0)
                if (!player.SpendGoldDirect(dest.costGold)) return;

            CloseTaxiWindow();
            StartCoroutine(FlightTaxiRoutine(dest));
        }

        IEnumerator FlightTaxiRoutine(FlightPathDestination dest)
        {
            InFlight = true;
            IsAnyTaxiInFlight = true;
            ChatSystem.LogSystem($"Taking flight taxi to [{dest.name}]...");
            AudioEffects.PlayClick();

            bool wasAlreadyMounted = player != null && player.Mounted;
            if (player != null && !wasAlreadyMounted)
                player.MountForFlightTaxi();

            var cc = player ? player.GetComponent<CharacterController>() : null;
            if (cc) cc.enabled = false;

            Vector3 startPos = player ? player.transform.position : transform.position;
            Vector3 endPos = PlayerController.ClampAboveGround(dest.targetPos, 0.35f);
            Vector3 midPoint = (startPos + endPos) / 2f + Vector3.up * 45f; // soaring flight arc

            float duration = Mathf.Clamp(Vector3.Distance(startPos, endPos) / 24f, 4.5f, 32f);
            float elapsed = 0f;

            CastBarSystem.StartCast($"Flying to {dest.name}...", duration, null, false, false);

            try
            {
                while (elapsed < duration)
                {
                    if (!player) yield break;

                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    // Quadratic Bezier arc
                    Vector3 p1 = Vector3.Lerp(startPos, midPoint, t);
                    Vector3 p2 = Vector3.Lerp(midPoint, endPos, t);
                    Vector3 cur = Vector3.Lerp(p1, p2, t);

                    Vector3 forward = p2 - p1;
                    if (forward.sqrMagnitude > 0.0001f)
                        player.transform.rotation = Quaternion.Slerp(player.transform.rotation, Quaternion.LookRotation(forward.normalized), Time.deltaTime * 6f);

                    player.transform.position = cur;
                    player.AnimateFlightTaxi();
                    yield return null;
                }

                if (player)
                {
                    Vector3 flatFwd = endPos - startPos;
                    flatFwd.y = 0f;
                    if (flatFwd.sqrMagnitude > 0.01f)
                        player.transform.rotation = Quaternion.LookRotation(flatFwd.normalized);
                    else
                        player.transform.rotation = Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f);

                    player.transform.position = endPos;
                }
            }
            finally
            {
                if (cc) cc.enabled = true;
                if (player) player.EndFlightTaxiMount(wasAlreadyMounted);
                InFlight = false;
                IsAnyTaxiInFlight = false;
                CastBarSystem.CancelCast("");
            }

            if (player)
            {
                ChatSystem.LogSystem($"Arrived at [{dest.name}].");
                ZoneBanner.Show(dest.zone, dest.faction + " Territory");
                AudioEffects.PlayZoneDiscover();
            }
        }

        public void DrawTaxiMapGUI(float screenW, float screenH)
        {
            if (!TaxiMapOpen || FrontendMenu.IsOpen) return;

            InitStyles();

            float winW = 460f;
            float winH = 430f;
            float x = screenW / 2f - winW / 2f;
            float y = screenH / 2f - winH / 2f;

            // Background & Border
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, winW + 4f, winH + 4f), borderTex);
            GUI.DrawTexture(new Rect(x, y, winW, winH), mapBgTex);

            // Title
            GUI.Label(new Rect(x + 20f, y + 16f, winW - 80f, 28f), "FLIGHT MASTER TAXI NETWORK", winTitleStyle);
            GUI.Label(new Rect(x + 20f, y + 46f, winW - 80f, 20f), "Select your destination across the 6 zones:", subStyle);

            if (GUI.Button(new Rect(x + winW - 42f, y + 14f, 26f, 26f), "X", nodeBtnStyle))
            {
                CloseTaxiWindow();
                return;
            }

            float rowY = y + 78f;
            for (int i = 0; i < WorldFlightNodes.Length; i++)
            {
                var node = WorldFlightNodes[i];
                Vector3 pPos = player ? player.transform.position : transform.position;
                float dist = Vector2.Distance(new Vector2(pPos.x, pPos.z), new Vector2(node.targetPos.x, node.targetPos.z));
                bool isCurrent = dist < 16f && Mathf.Abs(pPos.y - node.targetPos.y) < 12f;

                Rect rowRect = new Rect(x + 20f, rowY, winW - 40f, 48f);
                Color btnBg = isCurrent ? new Color(0.2f, 0.45f, 0.2f) :
                              node.faction == "Alliance" ? new Color(0.18f, 0.24f, 0.38f) :
                              node.faction == "Horde" ? new Color(0.38f, 0.18f, 0.18f) :
                              new Color(0.25f, 0.25f, 0.28f);

                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = btnBg;

                GUI.enabled = !isCurrent && !InFlight && !IsAnyTaxiInFlight;
                string label = isCurrent ? $"[CURRENT LOCATION] {node.name}" : $"✈ {node.name}\nZone: {node.zone}  •  Cost: {node.costGold} Gold";
                if (GUI.Button(rowRect, label, nodeBtnStyle))
                {
                    GUI.enabled = true;
                    GUI.backgroundColor = oldBg;
                    TakeFlight(node);
                    return;
                }
                GUI.enabled = true;
                GUI.backgroundColor = oldBg;

                rowY += 54f;
            }

            if (GUI.Button(new Rect(x + 20f, y + winH - 42f, winW - 40f, 30f), "CLOSE [ESC]", nodeBtnStyle))
            {
                CloseTaxiWindow();
            }
        }

        void InitStyles()
        {
            if (winTitleStyle != null && subStyle != null && nodeBtnStyle != null && mapBgTex != null && borderTex != null) return;

            mapBgTex = new Texture2D(1, 1);
            mapBgTex.SetPixel(0, 0, new Color(0.06f, 0.08f, 0.12f, 0.96f));
            mapBgTex.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.85f, 0.72f, 0.32f, 1f));
            borderTex.Apply();

            winTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            winTitleStyle.normal.textColor = new Color(1.0f, 0.85f, 0.35f);

            subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            subStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            nodeBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
}
