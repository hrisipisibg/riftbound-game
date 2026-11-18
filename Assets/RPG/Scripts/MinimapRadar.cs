using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class MinimapRadar : MonoBehaviour
    {
        public static MinimapRadar Instance { get; private set; }

        public float radarRange = 60f;
        public bool rotateWithCamera = false;
        public bool showCompass = false; // Disabled by default: removes cardinal indicators (N, S, E, W) and compass button from HUD
        public bool showMinimap = false; // Master toggle for minimap radar on HUD (disabled by default: removes circle radar/compass)

        // Tracking Filter Toggles
        public bool trackQuests = true;
        public bool trackEnemies = true;
        public bool trackHerbs = true;
        public bool trackMinerals = true;
        public bool trackLoot = true;
        public bool trackChests = true;
        public bool trackNPCs = true;

        // Custom Waypoint
        [NonSerialized] public Vector3? customWaypoint = null;
        public bool TrackingMainQuest { get; private set; }
        public bool RadarVisible => showMinimap || TrackingMainQuest;
        float nextQuestUpdate;

        public void TrackMainQuest()
        {
            if (!player || player.QuestComplete) return;
            SetWaypoint(player.ObjectivePosition, player.QuestName);
            TrackingMainQuest = true;
        }

        public static void SetWaypoint(Vector3 pos, string label = null)
        {
            var radar = Instance ?? FindFirstObjectByType<MinimapRadar>();
            if (radar)
            {
                radar.customWaypoint = pos;
                radar.TrackingMainQuest = false;
                if (!string.IsNullOrEmpty(label))
                    ChatSystem.LogSystem($"<color=#00e5ff>★ Waypoint Set: {label} ({pos.x:0.0}, {pos.z:0.0})</color>");
                AudioEffects.PlayClick();
            }
        }

        public static void ClearWaypoint()
        {
            var radar = Instance ?? FindFirstObjectByType<MinimapRadar>();
            if (radar && radar.customWaypoint.HasValue)
            {
                radar.customWaypoint = null;
                radar.TrackingMainQuest = false;
                ChatSystem.LogSystem("<color=#aaa>Waypoint Cleared.</color>");
                AudioEffects.PlayClick();
            }
        }

        PlayerController player;
        OrbitCamera orbitCam;

        // Dynamic Topographic Underlay
        Texture2D terrainTex;
        Color32[] terrainPixels;
        Vector3 lastTerrainUpdatePos = new Vector3(-999f, 0, -999f);
        float nextTerrainUpdateTime = 0f;

        // Texture Cache
        Texture2D circleMaskTex, bezelTex, rivetTex, playerNeedleTex;
        Texture2D blipRedTex, blipGoldTex, blipGreenTex, blipPurpleTex, blipCyanTex, blipOrangeTex;
        Texture2D menuBgTex, menuBorderTex;

        // Styles
        GUIStyle zoneTitleStyle, coordsStyle, clockStyle, cardinalStyle, rimBtnStyle, blipLabelStyle, tooltipStyle, menuStyle, menuHeaderStyle;
        bool trackingMenuOpen = false;
        string activeTooltip = "";
        Rect activeTooltipRect;

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

        void Start()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            orbitCam = FindFirstObjectByType<OrbitCamera>();
        }

        void Update()
        {
            if (!TrackingMainQuest || !player || FrontendMenu.IsOpen || Time.unscaledTime < nextQuestUpdate) return;
            nextQuestUpdate = Time.unscaledTime + .5f;
            if (player.QuestComplete)
            {
                TrackingMainQuest = false;
                customWaypoint = null;
            }
            else customWaypoint = player.ObjectivePosition;
        }

        public void DrawRadarGUI(float screenW, float screenH)
        {
            if (!RadarVisible || !player || FrontendMenu.IsOpen) return;
            if (!orbitCam) orbitCam = FindFirstObjectByType<OrbitCamera>();

            InitTextures();

            float radarDiameter = 132f;
            float radarRadius = radarDiameter / 2f;
            float cx = screenW - 88f;
            float cy = 92f;

            activeTooltip = "";

            // Handle minimap mouse click for waypoints / menu
            HandleMinimapInput(cx, cy, radarRadius);

            // Update procedural topographic terrain underlay
            UpdateTerrainTexture(player.transform.position);

            // 1. Zone Name Banner (Top Pill with gold filigree)
            DrawZonePill(cx, cy - radarRadius - 14f);

            // 2. Terrain Underlay & Radar Content (Rendered with rotation support)
            float viewRotation = rotateWithCamera ? (orbitCam ? orbitCam.transform.eulerAngles.y : player.transform.eulerAngles.y) : 0f;

            var oldMatrix = GUI.matrix;

            // Rotate radar interior if rotating mode is enabled
            if (rotateWithCamera && Mathf.Abs(viewRotation) > 0.01f)
            {
                GUIUtility.RotateAroundPivot(-viewRotation, new Vector2(cx, cy));
            }

            // Draw Topographic Terrain Texture
            Rect radarRect = new Rect(cx - radarRadius, cy - radarRadius, radarDiameter, radarDiameter);
            if (terrainTex != null)
            {
                GUI.DrawTexture(radarRect, terrainTex);
            }

            // Draw World Tracking Blips
            DrawTrackingBlips(cx, cy, radarRadius, player.transform.position);

            // Custom Waypoint Blip inside radar
            if (customWaypoint.HasValue)
            {
                Vector2 wpPos = WorldToRadar(player.transform.position, customWaypoint.Value, cx, cy, radarRadius);
                if (wpPos.x > 0)
                {
                    float pulse = 8f + Mathf.PingPong(Time.time * 6f, 3f);
                    GUI.DrawTexture(new Rect(wpPos.x - pulse / 2f, wpPos.y - pulse / 2f, pulse, pulse), blipCyanTex);
                    GUI.Label(new Rect(wpPos.x - 7f, wpPos.y - 8f, 16f, 16f), "", blipLabelStyle);
                }
            }

            // Restore GUI Matrix for compass rim, bezel, and UI buttons
            GUI.matrix = oldMatrix;

            // 3. Central Player Needle & Vision Cone
            DrawPlayerIcon(cx, cy, viewRotation);

            // 4. Off-Screen Objective / Waypoint Navigation Arrows (Pinned to Perimeter)
            DrawOffScreenArrows(cx, cy, radarRadius, viewRotation);

            // 5. Heavy Sculpted Ornate Bezel, Cardinal Ticks, & Compass
            DrawOrnateBezel(cx, cy, radarRadius, viewRotation);

            // 6. Coordinates & Local Time Bar at bottom
            bool radarHovered = new Rect(cx - radarRadius - 20f, cy - radarRadius - 24f, radarDiameter + 40f, radarDiameter + 56f).Contains(Event.current.mousePosition);
            if (radarHovered || trackingMenuOpen) DrawBottomCoordsBar(cx, cy + radarRadius + 4f);

            // 7. Interactive Rim Buttons (+, -, 🔍, 🧭, M)
            if (radarHovered || trackingMenuOpen) DrawRimButtons(cx, cy, radarRadius);

            // 8. Tracking Dropdown Menu (If Open)
            if (trackingMenuOpen)
            {
                DrawTrackingDropdownMenu(cx, cy, radarRadius);
            }

            // 9. Floating Tooltip (If hovering an icon or button)
            if (!string.IsNullOrEmpty(activeTooltip))
            {
                DrawActiveTooltip();
            }
        }

        void DrawZonePill(float cx, float y)
        {
            string zone = ZoneManager.GetZoneName(player.transform.position);
            string sub = ZoneManager.GetSubZoneName(player.transform.position);
            bool isAlliance = sub.Contains("Alliance");
            bool isHorde = sub.Contains("Horde");

            float pillW = 152f;
            float pillH = 18f;
            float px = cx - pillW / 2f;

            // Dark embossed box with border
            GUI.DrawTexture(new Rect(px - 1, y - 1, pillW + 2, pillH + 2), bezelTex);
            GUI.DrawTexture(new Rect(px, y, pillW, pillH), menuBgTex);

            Color tagColor = isAlliance ? new Color(0.4f, 0.75f, 1f) :
                             isHorde ? new Color(1f, 0.4f, 0.35f) :
                             new Color(.78f, .73f, .60f);

            zoneTitleStyle.normal.textColor = tagColor;
            GUI.Label(new Rect(px, y - 1f, pillW, pillH), zone, zoneTitleStyle);

            // Tooltip on hover
            if (new Rect(px, y, pillW, pillH).Contains(Event.current.mousePosition))
            {
                activeTooltip = $"<b>{zone}</b>\n{sub}";
                activeTooltipRect = new Rect(Event.current.mousePosition.x - 70f, y - 36f, 140f, 34f);
            }
        }

        void DrawBottomCoordsBar(float cx, float y)
        {
            float barW = 146f;
            float barH = 16f;
            float bx = cx - barW / 2f;

            GUI.DrawTexture(new Rect(bx, y, barW, barH), menuBgTex);
            GUI.DrawTexture(new Rect(bx, y, barW, 1f), bezelTex);

            Vector3 pos = player.transform.position;
            string coords = $"{pos.x:0.0}, {pos.z:0.0}";
            string clock = DateTime.Now.ToString("HH:mm");

            coordsStyle.normal.textColor = new Color(0.95f, 0.85f, 0.45f);
            GUI.Label(new Rect(bx + 4f, y - 1f, 85f, barH), coords, coordsStyle);

            clockStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);
            GUI.Label(new Rect(bx + barW - 54f, y - 1f, 50f, barH), clock, clockStyle);
        }

        void DrawOrnateBezel(float cx, float cy, float radius, float viewRot)
        {
            // Outer golden ring
            int segments = 40;
            var matrix = GUI.matrix;
            float outerR = radius + 2f;

            for (int i = 0; i < segments; i++)
            {
                float a1 = i * Mathf.PI * 2f / segments;
                float a2 = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 p1 = new Vector2(cx + Mathf.Cos(a1) * outerR, cy + Mathf.Sin(a1) * outerR);
                Vector2 p2 = new Vector2(cx + Mathf.Cos(a2) * outerR, cy + Mathf.Sin(a2) * outerR);

                float len = Vector2.Distance(p1, p2);
                float rot = Mathf.Atan2(p2.y - p1.y, p2.x - p1.x) * Mathf.Rad2Deg;

                GUIUtility.RotateAroundPivot(rot, p1);
                GUI.DrawTexture(new Rect(p1.x, p1.y - .5f, len, 1.25f), menuBorderTex != null ? menuBorderTex : bezelTex);
                GUI.matrix = matrix;
            }

            // Cardinal Letters (N, S, E, W) - only drawn if showCompass is true
            if (showCompass)
            {
                float cardinalDist = radius - 11f;
                float northAngle = rotateWithCamera ? -viewRot : 0f;

                DrawCardinal("N", cx, cy, cardinalDist, northAngle, new Color(1f, 0.35f, 0.35f));
                DrawCardinal("E", cx, cy, cardinalDist, northAngle + 90f, new Color(1f, 0.85f, 0.4f));
                DrawCardinal("S", cx, cy, cardinalDist, northAngle + 180f, new Color(0.85f, 0.85f, 0.85f));
                DrawCardinal("W", cx, cy, cardinalDist, northAngle + 270f, new Color(0.85f, 0.85f, 0.85f));
            }
        }

        void DrawCardinal(string text, float cx, float cy, float dist, float degAngle, Color col)
        {
            float rad = degAngle * Mathf.Deg2Rad;
            // 0 deg is North (-Y in screen space)
            float x = cx + Mathf.Sin(rad) * dist;
            float y = cy - Mathf.Cos(rad) * dist;

            cardinalStyle.normal.textColor = col;
            GUI.Label(new Rect(x - 9f, y - 8f, 18f, 16f), text, cardinalStyle);
        }

        void DrawPlayerIcon(float cx, float cy, float viewRot)
        {
            float playerYaw = player.transform.eulerAngles.y;
            float needleRot = rotateWithCamera ? (playerYaw - viewRot) : playerYaw;

            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(needleRot, new Vector2(cx, cy));

            // Sleek player arrow
            var prevCol = GUI.color;
            GUI.color = new Color(1f, 0.88f, 0.25f, 0.98f);
            GUI.DrawTexture(new Rect(cx - 5f, cy - 8f, 10f, 13f), playerNeedleTex);

            GUI.color = prevCol;
            GUI.matrix = matrix;
        }

        void DrawTrackingBlips(float cx, float cy, float radius, Vector3 playerPos)
        {
            // 1. Hostile Enemies & World Bosses
            if (trackEnemies)
            {
                var enemies = UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < enemies.Length; i++)
                {
                    var e = enemies[i];
                    if (!e) continue;
                    var combatant = e.GetComponent<Combatant>();
                    if (!combatant || !combatant.Alive) continue;

                    Vector2 blip = WorldToRadar(playerPos, e.transform.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        if (e.isBoss)
                        {
                            GUI.DrawTexture(new Rect(blip.x - 5f, blip.y - 5f, 10f, 10f), blipOrangeTex);
                            GUI.Label(new Rect(blip.x - 6f, blip.y - 7f, 14f, 14f), "", blipLabelStyle);
                        }
                        else if (e.heavyAttacker)
                        {
                            GUI.DrawTexture(new Rect(blip.x - 4f, blip.y - 4f, 8f, 8f), blipRedTex);
                        }
                        else
                        {
                            GUI.DrawTexture(new Rect(blip.x - 3f, blip.y - 3f, 6f, 6f), blipRedTex);
                        }
                    }
                }
            }

            // 2. Herbalism & Mining Nodes
            if (trackHerbs || trackMinerals)
            {
                var nodes = UnityEngine.Object.FindObjectsByType<GatheringNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    if (!n || !n.gameObject.activeInHierarchy) continue;

                    Vector2 blip = WorldToRadar(playerPos, n.transform.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        if (n.gatheringType == GatheringType.Herbalism && trackHerbs)
                        {
                            GUI.DrawTexture(new Rect(blip.x - 3f, blip.y - 3f, 6f, 6f), blipGreenTex);
                            GUI.Label(new Rect(blip.x - 5f, blip.y - 6f, 12f, 12f), "", blipLabelStyle);
                        }
                        else if (n.gatheringType == GatheringType.Mining && trackMinerals)
                        {
                            GUI.DrawTexture(new Rect(blip.x - 3f, blip.y - 3f, 6f, 6f), blipGoldTex);
                            GUI.Label(new Rect(blip.x - 5f, blip.y - 6f, 12f, 12f), "", blipLabelStyle);
                        }
                    }
                }
            }

            // 3. Loot Sparklers & Defeated Mob Corpses
            if (trackLoot && LootDropSystem.Instance != null && LootDropSystem.Instance.ActiveCorpses != null)
            {
                var corpses = LootDropSystem.Instance.ActiveCorpses;
                for (int i = 0; i < corpses.Count; i++)
                {
                    var c = corpses[i];
                    if (c == null) continue;

                    Vector2 blip = WorldToRadar(playerPos, c.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        float sparkle = 6f + Mathf.PingPong(Time.time * 4f, 2.5f);
                        GUI.DrawTexture(new Rect(blip.x - sparkle / 2f, blip.y - sparkle / 2f, sparkle, sparkle), blipGoldTex);
                        GUI.Label(new Rect(blip.x - 5f, blip.y - 6f, 12f, 12f), "✨", blipLabelStyle);
                    }
                }
            }

            // 4. Treasure Chests & Reliquaries
            if (trackChests)
            {
                var chests = UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < chests.Length; i++)
                {
                    var ch = chests[i];
                    if (!ch || player.HasOpenedChest(ch.chestId)) continue;

                    Vector2 blip = WorldToRadar(playerPos, ch.transform.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        GUI.DrawTexture(new Rect(blip.x - 3.5f, blip.y - 3.5f, 7f, 7f), blipGoldTex);
                        GUI.Label(new Rect(blip.x - 5f, blip.y - 6f, 12f, 12f), "", blipLabelStyle);
                    }
                }

                // 4b. Archaeology Dig Sites
                if (ArchaeologySystem.Instance != null)
                {
                    foreach (var site in ArchaeologySystem.Instance.digSites)
                    {
                        Vector2 siteBlip = WorldToRadar(playerPos, site.center, cx, cy, radius);
                        if (siteBlip.x > 0)
                        {
                            GUI.Label(new Rect(siteBlip.x - 6f, siteBlip.y - 6f, 14f, 14f), "🏺", blipLabelStyle);
                        }
                    }
                }
            }

            // 5. Town NPCs, Quest Givers, & Flight Masters
            if (trackNPCs)
            {
                var townNPCs = UnityEngine.Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < townNPCs.Length; i++)
                {
                    var npc = townNPCs[i];
                    if (!npc) continue;

                    Vector2 blip = WorldToRadar(playerPos, npc.transform.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        GUI.DrawTexture(new Rect(blip.x - 3f, blip.y - 3f, 6f, 6f), blipGreenTex);
                    }
                }

                // Flight Masters
                var flightMasters = UnityEngine.Object.FindObjectsByType<FlightMaster>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < flightMasters.Length; i++)
                {
                    var fm = flightMasters[i];
                    if (!fm) continue;

                    Vector2 blip = WorldToRadar(playerPos, fm.transform.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        GUI.DrawTexture(new Rect(blip.x - 4f, blip.y - 4f, 8f, 8f), blipCyanTex);
                        GUI.Label(new Rect(blip.x - 5f, blip.y - 7f, 12f, 12f), "🦅", blipLabelStyle);
                    }
                }
            }

            // 6. Dungeon Portals
            var portals = UnityEngine.Object.FindObjectsByType<DungeonPortal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < portals.Length; i++)
            {
                var p = portals[i];
                if (!p) continue;

                Vector2 blip = WorldToRadar(playerPos, p.transform.position, cx, cy, radius);
                if (blip.x > 0)
                {
                    GUI.DrawTexture(new Rect(blip.x - 4.5f, blip.y - 4.5f, 9f, 9f), blipPurpleTex);
                    GUI.Label(new Rect(blip.x - 5f, blip.y - 7f, 12f, 12f), "🌀", blipLabelStyle);
                }
            }

            // 7. Quest Givers & Objectives inside radar
            if (trackQuests)
            {
                var qg = UnityEngine.Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                for (int i = 0; i < qg.Length; i++)
                {
                    var q = qg[i];
                    if (!q) continue;

                    Vector2 blip = WorldToRadar(playerPos, q.transform.position, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        string icon = player.QuestReady ? "?" : "!";
                        blipLabelStyle.normal.textColor = new Color(1f, 0.88f, 0.15f);
                        GUI.Label(new Rect(blip.x - 7f, blip.y - 8f, 15f, 16f), icon, blipLabelStyle);
                    }
                }

                // Active Quest Objective Beacon if within range
                if (player.QuestAccepted && !player.QuestComplete)
                {
                    Vector3 objPos = player.ObjectivePosition;
                    Vector2 blip = WorldToRadar(playerPos, objPos, cx, cy, radius);
                    if (blip.x > 0)
                    {
                        float ringSize = 12f + Mathf.PingPong(Time.time * 5f, 4f);
                        GUI.DrawTexture(new Rect(blip.x - ringSize / 2f, blip.y - ringSize / 2f, ringSize, ringSize), blipGoldTex);
                        blipLabelStyle.normal.textColor = new Color(1f, 0.95f, 0.25f);
                        GUI.Label(new Rect(blip.x - 6f, blip.y - 8f, 14f, 16f), "★", blipLabelStyle);
                    }
                }
            }
        }

        void DrawOffScreenArrows(float cx, float cy, float radius, float viewRot)
        {
            Vector3 playerPos = player.transform.position;

            // 1. Off-screen Quest Objective Chevron
            if (trackQuests && player.QuestAccepted && !player.QuestComplete)
            {
                Vector3 objPos = player.ObjectivePosition;
                Vector3 diff = objPos - playerPos;
                Vector2 horiz = new Vector2(diff.x, diff.z);
                float dist = horiz.magnitude;

                if (dist > radarRange)
                {
                    float angleDeg = Mathf.Atan2(horiz.x, horiz.y) * Mathf.Rad2Deg;
                    if (rotateWithCamera) angleDeg -= viewRot;

                    float rad = angleDeg * Mathf.Deg2Rad;
                    float rimDist = radius - 8f;
                    float ax = cx + Mathf.Sin(rad) * rimDist;
                    float ay = cy - Mathf.Cos(rad) * rimDist;

                    var matrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angleDeg, new Vector2(ax, ay));

                    GUI.DrawTexture(new Rect(ax - 6f, ay - 8f, 12f, 12f), blipGoldTex);
                    GUI.matrix = matrix;

                    // Hover tooltip
                    if (new Rect(ax - 10f, ay - 10f, 20f, 20f).Contains(Event.current.mousePosition))
                    {
                        activeTooltip = $"<b>Quest Objective</b>\n{player.QuestName}\nDistance: {dist:0}m";
                        activeTooltipRect = new Rect(Event.current.mousePosition.x - 85f, Event.current.mousePosition.y - 42f, 170f, 38f);
                    }
                }
            }

            // 2. Off-screen Custom Waypoint
            if (customWaypoint.HasValue)
            {
                Vector3 diff = customWaypoint.Value - playerPos;
                Vector2 horiz = new Vector2(diff.x, diff.z);
                float dist = horiz.magnitude;

                if (dist > radarRange)
                {
                    float angleDeg = Mathf.Atan2(horiz.x, horiz.y) * Mathf.Rad2Deg;
                    if (rotateWithCamera) angleDeg -= viewRot;

                    float rad = angleDeg * Mathf.Deg2Rad;
                    float rimDist = radius - 8f;
                    float ax = cx + Mathf.Sin(rad) * rimDist;
                    float ay = cy - Mathf.Cos(rad) * rimDist;

                    var matrix = GUI.matrix;
                    GUIUtility.RotateAroundPivot(angleDeg, new Vector2(ax, ay));

                    GUI.DrawTexture(new Rect(ax - 5f, ay - 7f, 10f, 10f), blipCyanTex);
                    GUI.matrix = matrix;

                    if (new Rect(ax - 10f, ay - 10f, 20f, 20f).Contains(Event.current.mousePosition))
                    {
                        activeTooltip = $"<b>Custom Waypoint</b>\nDistance: {dist:0}m\n(Right-click radar to clear)";
                        activeTooltipRect = new Rect(Event.current.mousePosition.x - 85f, Event.current.mousePosition.y - 42f, 170f, 38f);
                    }
                }
            }
        }

        void DrawRimButtons(float cx, float cy, float radius)
        {
            // Tracking Magnifying Glass Button (Top-Left of Rim)
            Rect trackRect = new Rect(cx - radius - 10f, cy - radius + 12f, 20f, 20f);
            if (GUI.Button(trackRect, "...", rimBtnStyle))
            {
                trackingMenuOpen = !trackingMenuOpen;
                AudioEffects.PlayClick();
            }
            if (trackRect.Contains(Event.current.mousePosition))
            {
                activeTooltip = "<b>Tracking Options</b>\nConfigure minimap blips & compass";
                activeTooltipRect = new Rect(Event.current.mousePosition.x - 120f, Event.current.mousePosition.y - 36f, 140f, 32f);
            }

            // Compass Mode Button (North-Up vs Rotating) - only shown when compass is enabled
            if (showCompass)
            {
                Rect rotRect = new Rect(cx - radius - 12f, cy - 8f, 22f, 20f);
                string rotIcon = rotateWithCamera ? "R" : "N";
                if (GUI.Button(rotRect, rotIcon, rimBtnStyle))
                {
                    rotateWithCamera = !rotateWithCamera;
                    AudioEffects.PlayClick();
                    ChatSystem.LogSystem($"Minimap Compass: {(rotateWithCamera ? "Rotating (Camera-Facing)" : "Fixed (North-Up)")}");
                }
                if (rotRect.Contains(Event.current.mousePosition))
                {
                    activeTooltip = $"<b>Minimap Mode: {(rotateWithCamera ? "Rotating" : "Fixed North")}</b>\nClick to toggle compass alignment";
                    activeTooltipRect = new Rect(Event.current.mousePosition.x - 145f, Event.current.mousePosition.y - 36f, 160f, 32f);
                }
            }

            // World Map Button (Bottom-Left of Rim)
            Rect mapRect = new Rect(cx - radius - 8f, cy + radius - 24f, 20f, 20f);
            if (GUI.Button(mapRect, "M", rimBtnStyle))
            {
                player.MapOpen = !player.MapOpen;
                AudioEffects.PlayClick();
            }
            if (mapRect.Contains(Event.current.mousePosition))
            {
                activeTooltip = "<b>World Map [M]</b>\nView continental map";
                activeTooltipRect = new Rect(Event.current.mousePosition.x - 110f, Event.current.mousePosition.y - 36f, 130f, 32f);
            }

            // Zoom In (+) Button (Top-Right of Rim)
            Rect zoomInRect = new Rect(cx + radius - 8f, cy - 20f, 19f, 19f);
            if (GUI.Button(zoomInRect, "+", rimBtnStyle))
            {
                radarRange = Mathf.Max(30f, radarRange - 15f);
                AudioEffects.PlayClick();
            }
            if (zoomInRect.Contains(Event.current.mousePosition))
            {
                activeTooltip = $"<b>Zoom In</b>\nRadar Range: {radarRange:0}m";
                activeTooltipRect = new Rect(Event.current.mousePosition.x - 110f, Event.current.mousePosition.y - 36f, 120f, 32f);
            }

            // Zoom Out (-) Button (Right of Rim)
            Rect zoomOutRect = new Rect(cx + radius - 8f, cy + 5f, 19f, 19f);
            if (GUI.Button(zoomOutRect, "-", rimBtnStyle))
            {
                radarRange = Mathf.Min(120f, radarRange + 15f);
                AudioEffects.PlayClick();
            }
            if (zoomOutRect.Contains(Event.current.mousePosition))
            {
                activeTooltip = $"<b>Zoom Out</b>\nRadar Range: {radarRange:0}m";
                activeTooltipRect = new Rect(Event.current.mousePosition.x - 110f, Event.current.mousePosition.y - 36f, 120f, 32f);
            }
        }

        void DrawTrackingDropdownMenu(float cx, float cy, float radius)
        {
            float menuW = 165f;
            float menuH = showCompass ? 215f : 195f;
            float mx = cx - radius - menuW - 14f;
            float my = cy - radius;

            GUI.DrawTexture(new Rect(mx - 2, my - 2, menuW + 4, menuH + 4), bezelTex);
            GUI.DrawTexture(new Rect(mx, my, menuW, menuH), menuBgTex);

            GUI.Label(new Rect(mx + 8f, my + 4f, menuW - 16f, 18f), "TRACKING FILTERS", menuHeaderStyle);

            float rowY = my + 24f;
            float rowH = 20f;

            trackQuests = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "★ Quests & NPCs", trackQuests); rowY += rowH;
            trackEnemies = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "💀 Enemies & Bosses", trackEnemies); rowY += rowH;
            trackHerbs = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "🌿 Herbalism", trackHerbs); rowY += rowH;
            trackMinerals = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "⛏ Mining Deposits", trackMinerals); rowY += rowH;
            trackLoot = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "✨ Corpse Loot", trackLoot); rowY += rowH;
            trackChests = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "📦 Treasure Chests", trackChests); rowY += rowH;
            trackNPCs = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "🦅 Towns & Taxis", trackNPCs); rowY += rowH;
            showCompass = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "🧭 Show Compass", showCompass); rowY += rowH;
            if (showCompass)
            {
                rotateWithCamera = DrawToggleRow(mx + 6f, rowY, menuW - 12f, rowH, "🔄 Rotating Compass", rotateWithCamera);
            }
        }

        bool DrawToggleRow(float x, float y, float w, float h, string label, bool val)
        {
            string box = val ? "☑ " : "☐ ";
            menuStyle.normal.textColor = val ? new Color(1f, 0.9f, 0.45f) : new Color(0.65f, 0.65f, 0.65f);
            if (GUI.Button(new Rect(x, y, w, h), box + label, menuStyle))
            {
                val = !val;
                AudioEffects.PlayClick();
            }
            return val;
        }

        void HandleMinimapInput(float cx, float cy, float radius)
        {
            Event e = Event.current;
            if (e == null || !e.isMouse) return;

            Vector2 mousePos = e.mousePosition;
            float distToCenter = Vector2.Distance(mousePos, new Vector2(cx, cy));

            if (distToCenter <= radius)
            {
                // Left Click to set custom waypoint
                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    Vector2 localOffset = mousePos - new Vector2(cx, cy);
                    float ratio = (distToCenter / (radius - 6f)) * radarRange;
                    Vector2 dir = localOffset.normalized;

                    float viewRotation = rotateWithCamera ? (orbitCam ? orbitCam.transform.eulerAngles.y : player.transform.eulerAngles.y) : 0f;
                    Vector2 worldDir = rotateWithCamera ? RotateVec2(dir, viewRotation) : dir;

                    Vector3 targetPos = player.transform.position + new Vector3(worldDir.x * ratio, 0f, -worldDir.y * ratio);
                    customWaypoint = targetPos;
                    TrackingMainQuest = false;
                    AudioEffects.PlayClick();
                    ChatSystem.LogSystem($"<color=#00e5ff>★ Waypoint Set: ({targetPos.x:0.0}, {targetPos.z:0.0})</color>");
                    e.Use();
                }
                // Right Click to clear waypoint
                else if (e.type == EventType.MouseDown && e.button == 1)
                {
                    if (customWaypoint.HasValue)
                    {
                        customWaypoint = null;
                        TrackingMainQuest = false;
                        AudioEffects.PlayClick();
                        ChatSystem.LogSystem("<color=#aaa>Waypoint Cleared.</color>");
                        e.Use();
                    }
                }
            }
        }

        Vector2 RotateVec2(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }

        Vector2 WorldToRadar(Vector3 playerPos, Vector3 targetPos, float cx, float cy, float radius)
        {
            Vector3 diff = targetPos - playerPos;
            Vector2 horiz = new Vector2(diff.x, diff.z);
            float dist = horiz.magnitude;

            if (dist > radarRange) return Vector2.zero;

            float ratio = (dist / radarRange) * (radius - 6f);
            Vector2 dir = horiz.normalized;

            // X is +X, Y is -Z in screen space
            float screenX = cx + dir.x * ratio;
            float screenY = cy - dir.y * ratio;

            return new Vector2(screenX, screenY);
        }

        void UpdateTerrainTexture(Vector3 playerPos)
        {
            if (terrainTex == null) return;
            if (Time.time < nextTerrainUpdateTime && Vector3.Distance(playerPos, lastTerrainUpdatePos) < 2.0f)
                return;

            nextTerrainUpdateTime = Time.time + 0.35f;
            lastTerrainUpdatePos = playerPos;

            int size = 128;
            float r = size / 2f;
            var terrains = Terrain.activeTerrains;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int idx = y * size + x;
                    float distFromCenter = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));

                    if (distFromCenter > r)
                    {
                        terrainPixels[idx] = new Color32(0, 0, 0, 0); // Transparent outside circle
                        continue;
                    }

                    // Map texture coordinate to world coordinate
                    float wx = playerPos.x + ((x - r) / r) * radarRange;
                    float wz = playerPos.z + ((y - r) / r) * radarRange;

                    // Sample Height
                    float h = WorldRegions.GroundHeight(new Vector3(wx, 0, wz), terrains);
                    float hEast = WorldRegions.GroundHeight(new Vector3(wx + 2f, 0, wz), terrains);
                    float hNorth = WorldRegions.GroundHeight(new Vector3(wx, 0, wz + 2f), terrains);
                    float slopeShade = Mathf.Clamp((hEast - h) * 0.12f + (hNorth - h) * 0.08f, -0.25f, 0.25f);

                    // Base Biome Color
                    Color baseCol;

                    if (WorldRegions.Expanded)
                    {
                        string zone = WorldRegions.ZoneName(new Vector3(wx, playerPos.y < -20 ? playerPos.y : h, wz));
                        baseCol = zone == "Wyrmcrest Summit" ? new Color(.55f, .61f, .63f) :
                            zone == "Duskwood Cathedral" || zone == "The Scarlet Crypts" ? new Color(.17f, .20f, .22f) :
                            zone == "Camp Bloodrock" || zone == "Gor'Kresh Stronghold" ? new Color(.43f, .31f, .19f) :
                            zone == "Greenhollow Valley" ? new Color(.24f, .37f, .21f) : new Color(.29f, .39f, .22f);
                    }
                    else if (playerPos.y < -20f)
                    {
                        // Subterranean Scarlet Crypts: ancient cathedral stonework
                        baseCol = new Color(0.18f, 0.15f, 0.22f);
                    }
                    else if (wz > 200f && Mathf.Abs(wx) < 25f && h > 20f)
                    {
                        // Wyrmcrest Summit Peak: Snow & alpine crag stone
                        baseCol = new Color(0.68f, 0.72f, 0.78f);
                    }
                    else if (wx > 30f && wz > 165f)
                    {
                        // Camp Bloodrock: Ochre desert & red sandstone
                        baseCol = new Color(0.58f, 0.28f, 0.15f);
                    }
                    else if (wx < -30f && wz > 140f)
                    {
                        // Duskwood: Haunted twilight forest & dark moss
                        baseCol = new Color(0.12f, 0.18f, 0.14f);
                    }
                    else
                    {
                        // Amber Vale / Emberwatch: Lush meadows and farmland
                        baseCol = new Color(0.24f, 0.42f, 0.18f);

                        // River Brook (Blue water)
                        if (Mathf.Abs(wx - 2f) < 3.2f && wz < 55f)
                        {
                            baseCol = new Color(0.15f, 0.42f, 0.72f);
                        }
                        // Highway Road (Packed dirt)
                        else if (Mathf.Abs(wx) < 3.0f && wz < 160f)
                        {
                            baseCol = new Color(0.48f, 0.42f, 0.30f);
                        }
                    }

                    // Apply relief shading & soft edge antialiasing
                    float edgeAlpha = Mathf.Clamp01((r - distFromCenter) / 1.5f);
                    float light = Mathf.Clamp01(1f + slopeShade);

                    Color finalCol = baseCol * light;
                    terrainPixels[idx] = new Color32(
                        (byte)(Mathf.Clamp01(finalCol.r) * 255),
                        (byte)(Mathf.Clamp01(finalCol.g) * 255),
                        (byte)(Mathf.Clamp01(finalCol.b) * 255),
                        (byte)(edgeAlpha * 230)
                    );
                }
            }

            terrainTex.SetPixels32(terrainPixels);
            terrainTex.Apply(false);
        }

        void DrawActiveTooltip()
        {
            GUI.DrawTexture(new Rect(activeTooltipRect.x - 2, activeTooltipRect.y - 2, activeTooltipRect.width + 4, activeTooltipRect.height + 4), bezelTex);
            GUI.DrawTexture(activeTooltipRect, menuBgTex);
            GUI.Label(new Rect(activeTooltipRect.x + 6, activeTooltipRect.y + 4, activeTooltipRect.width - 12, activeTooltipRect.height - 8), activeTooltip, tooltipStyle);
        }

        void InitTextures()
        {
            if (terrainTex != null) return;

            int size = 128;
            terrainTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            terrainPixels = new Color32[size * size];

            bezelTex = MakeTex(new Color(.55f, .49f, .36f, .9f)); // Polished WoW Gold
            rivetTex = MakeTex(new Color(0.45f, 0.35f, 0.15f, 1f));
            menuBgTex = MakeTex(new Color(0.06f, 0.08f, 0.11f, 0.94f));
            menuBorderTex = MakeTex(new Color(.48f, .43f, .33f, .9f));

            blipRedTex = MakeTex(new Color(1f, 0.18f, 0.18f));
            blipGoldTex = MakeTex(new Color(1f, 0.88f, 0.22f));
            blipGreenTex = MakeTex(new Color(0.2f, 0.92f, 0.35f));
            blipPurpleTex = MakeTex(new Color(0.75f, 0.35f, 1f));
            blipCyanTex = MakeTex(new Color(0.05f, 0.90f, 1f));
            blipOrangeTex = MakeTex(new Color(1f, 0.45f, 0.10f));

            // Procedural Player Arrow Texture (sharp triangle)
            playerNeedleTex = new Texture2D(10, 14, TextureFormat.RGBA32, false);
            Color gold = new Color(.82f, .74f, .52f);
            Color darkGold = new Color(0.75f, 0.55f, 0.12f);
            Color empty = new Color(0, 0, 0, 0);

            for (int y = 0; y < 14; y++)
            {
                float frac = 1f - (float)y / 14f;
                float halfW = frac * 5f;
                for (int x = 0; x < 10; x++)
                {
                    float dist = Mathf.Abs(x - 4.5f);
                    if (dist <= halfW)
                        playerNeedleTex.SetPixel(x, y, x == 4 || x == 5 ? gold : darkGold);
                    else
                        playerNeedleTex.SetPixel(x, y, empty);
                }
            }
            playerNeedleTex.Apply();

            // Styles (Only initialize when inside OnGUI)
            if (Event.current == null) return;

            zoneTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            coordsStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            clockStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleRight
            };

            cardinalStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            rimBtnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            blipLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            tooltipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft
            };
            tooltipStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f);

            menuStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(6, 2, 0, 0)
            };

            menuHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            menuHeaderStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);
        }

        Texture2D MakeTex(Color col)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, col);
            tex.Apply();
            return tex;
        }
    }
}
