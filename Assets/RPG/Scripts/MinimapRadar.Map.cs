using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class MinimapRadar
    {
        const float PanelWidth = 244f, PanelHeight = 312f, Diameter = 204f;
        const float PanelTop = 52f;
        internal const float TrackerTop = PanelTop + PanelHeight + 12f;
        static readonly Color Copper = new(.66f, .53f, .34f);
        static readonly Color Paper = new(.96f, .90f, .76f);
        static readonly Color Muted = new(.73f, .76f, .72f);
        static readonly Color PlayerColor = new(.50f, 1f, .96f);
        static readonly Color PinColor = new(1f, .89f, .52f);
        static readonly Color Shadow = new(.035f, .049f, .055f, .96f);

        Texture2D mapDisc, terrainTex;
        GUIStyle header, caption, centered, button, glyph;
        bool filtersOpen;
        readonly List<MapPoint> mapPoints = new();
        readonly List<Vector2> drawnPins = new();

        readonly struct MapPoint
        {
            public readonly Vector3 position;
            public readonly Color color;
            public readonly float size;

            public MapPoint(Vector3 position, Color color, float size)
            {
                this.position = position;
                this.color = color;
                this.size = size;
            }
        }

        static Rect PanelBounds(float width) => new(width - PanelWidth - 16, PanelTop, PanelWidth, PanelHeight);

        internal static bool TakesMouse(PlayerController source, Vector2 screenPoint)
        {
            var radar = Instance;
            if (!radar || radar.player != source || !radar.IsVisible) return false;
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            if (scale <= 0f) return false;
            Vector2 point = new(screenPoint.x / scale, (Screen.height - screenPoint.y) / scale);
            var panel = PanelBounds(Screen.width / scale);
            return panel.Contains(point) || radar.filtersOpen && FilterBounds(panel).Contains(point);
        }

        bool IsVisible => showMinimap && player && !FrontendMenu.IsOpen && !player.MapOpen;

        public void DrawRadarGUI(float screenW, float screenH)
        {
            if (!IsVisible) return;
            PrepareMapStyle();
            var panel = PanelBounds(screenW);
            Vector2 center = new(panel.center.x, panel.y + 132);
            var map = Centered(center, Diameter);
            Vector3 position = player.transform.position;
            int floor = MapFloor(position);
            float radius = Diameter * .5f;
            float yaw = rotateWithCamera && orbitCam ? orbitCam.transform.eulerAngles.y : 0;
            float range = floor == 0 ? radarRange : Mathf.Min(radarRange, 41f);
            string zone = ZoneManager.GetZoneName(position);
            Color biome = ZoneColor(zone);

            Fill(new Rect(panel.x + 3, panel.y + 4, panel.width, panel.height), new Color(0, 0, 0, .43f));
            Fill(panel, Copper * new Color(.8f, .82f, .82f, 1f));
            Fill(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, panel.height - 2), Shadow);
            Fill(new Rect(panel.x + 1, panel.y + 1, panel.width - 2, 28), Color.Lerp(Shadow, biome, .43f));
            Label(new Rect(panel.x + 7, panel.y + 2, panel.width - 14, 24), zone, header, Paper);
            Dot(center, Diameter + 8, Copper);
            Dot(center, Diameter + 5, new Color(.085f, .12f, .12f));
            if (Event.current.type == EventType.Repaint)
            {
                UpdateTerrainTexture(position, range, floor);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(-yaw, center);
                GUI.DrawTexture(map, terrainTex);
                GUI.matrix = matrix;
            }
            if (showCompass)
            {
                DrawCardinal(center, radius, "N", 0f - yaw);
                DrawCardinal(center, radius, "E", 90f - yaw);
                DrawCardinal(center, radius, "S", 180f - yaw);
                DrawCardinal(center, radius, "W", 270f - yaw);
            }

            foreach (var point in mapPoints)
            {
                if (MapFloor(point.position) != floor) continue;
                var delta = Project(point.position - position, yaw) * (radius / range);
                if (delta.sqrMagnitude > (radius - 10) * (radius - 10)) continue;
                Dot(center + delta, point.size + 2, Shadow);
                Dot(center + delta, point.size, point.color);
            }

            string hovered = DrawQuestPins(center, radius, range, position, floor, yaw);
            string routeCaption = null;
            if ((trackQuests || customWaypoint.HasValue) && TryGetNavigationTarget(out var destination, out var title))
            {
                Vector3 route = LocalPosition(destination, floor) - position;
                Vector2 direction = Project(route, yaw);
                float distance = direction.magnitude;
                Vector2 offset = direction * (radius / range);
                Color ink = customWaypoint.HasValue ? PlayerColor : PinColor;
                if (offset.magnitude > radius - 13f)
                    DrawNeedle(center + offset.normalized * (radius - 16), Vector2.SignedAngle(Vector2.up, -offset), 15, ink);
                else if (offset.sqrMagnitude > 36f)
                    Outline(Centered(center + offset, 22), ink);
                routeCaption = $"{title}  ·  {Mathf.RoundToInt(distance)} m";
            }
            DrawNeedle(center, player.transform.eulerAngles.y - yaw, 23f, PlayerColor);

            float footer = panel.y + 237;
            string location = floor == 0 ? $"{position.x:0} E / {position.z:0} N" : "Underground";
            Label(new Rect(panel.x + 9, footer, 114, 23), location, caption, Muted);
            if (GUI.Button(new Rect(panel.xMax - 127, footer + 1, 26, 22), "−", button)) ChangeRange(-1);
            if (GUI.Button(new Rect(panel.xMax - 99, footer + 1, 26, 22), "+", button)) ChangeRange(1);
            if (GUI.Button(new Rect(panel.xMax - 69, footer + 1, 60, 22), "Map [M]", button)) player.MapOpen = true;
            Fill(new Rect(panel.x + 8, footer + 28, panel.width - 16, 1), Copper * .65f);
            Label(new Rect(panel.x + 10, footer + 33, panel.width - 20, 20),
                hovered ?? routeCaption ?? "! Quest     ◆ Objective     ? Turn in", centered, Paper);
            if (GUI.Button(new Rect(panel.x + 9, footer + 53, 61, 17), filtersOpen ? "Close" : "Filters", button)) filtersOpen = !filtersOpen;
            if (customWaypoint.HasValue && GUI.Button(new Rect(panel.xMax - 70, footer + 53, 61, 17), "Clear pin", button)) ClearWaypoint();
            if (filtersOpen) DrawFilters(FilterBounds(panel));

            var input = Event.current;
            Vector2 clickOffset = input.mousePosition - center;
            if (input.type == EventType.MouseUp && clickOffset.sqrMagnitude < radius * radius)
            {
                if (input.button == 1) ClearWaypoint();
                else if (input.button == 0 && string.IsNullOrEmpty(hovered))
                {
                    var offset = Rotate(clickOffset, yaw) * (range / radius);
                    Vector3 target = position + new Vector3(offset.x, 0, -offset.y);
                    if (floor == 0) target.y = WorldRegions.GroundHeight(target, Terrain.activeTerrains);
                    SetWaypoint(target);
                }
                input.Use();
            }
        }

        string DrawQuestPins(Vector2 center, float radius, float range, Vector3 position, int floor, float yaw)
        {
            if (!trackQuests) return null;
            string hovered = null;
            drawnPins.Clear();
            foreach (var pin in questPins)
            {
                if (MapFloor(pin.position) != floor && floor != 0) continue;
                var offset = Project(LocalPosition(pin.position, floor) - position, yaw) * (radius / range);
                if (offset.sqrMagnitude > (radius - 17) * (radius - 17)) continue;
                var point = center + offset;
                bool stacked = false;
                foreach (var drawn in drawnPins)
                    if ((drawn - point).sqrMagnitude < 17 * 17) { stacked = true; break; }
                if (stacked) continue;
                drawnPins.Add(point);
                var hit = Centered(point, 21f);
                if (hit.Contains(Event.current.mousePosition)) hovered = pin.caption;
                if (DrawQuestMapPin(hit, pin)) SelectQuestPin(pin);
            }
            return hovered;
        }

        internal bool DrawQuestMapPin(Rect hit, QuestPin pin)
        {
            PrepareMapStyle();
            Dot(hit.center, hit.width - 1f, pin.Ink);
            Dot(hit.center, hit.width - 4f, Shadow);
            Label(hit, pin.Icon, glyph, pin.Ink);
            return GUI.Button(hit, GUIContent.none, GUIStyle.none);
        }

        void DrawCardinal(Vector2 center, float radius, string name, float angle)
        {
            var point = center + Rotate(Vector2.down, angle) * (radius - 8f);
            Dot(point, 16f, Shadow);
            Label(Centered(point, 17f), name, centered, name == "N" ? PinColor : Paper);
        }

        static void DrawNeedle(Vector2 point, float rotation, float size, Color color)
        {
            var original = GUI.matrix;
            GUIUtility.RotateAroundPivot(rotation, point);
            Fill(new Rect(point.x - 1, point.y - size / 2, 2, size), Shadow);
            for (int row = 0; row < 10; row++)
            {
                float width = size * (.14f + row * .07f);
                var rect = new Rect(point.x - width / 2, point.y - size / 2 + row * size / 14f, width, size / 13f);
                Fill(new Rect(rect.x - 1, rect.y, rect.width + 2, rect.height + 1), Shadow);
                Fill(rect, color);
            }
            GUI.matrix = original;
        }

        void ChangeRange(int zoom) => radarRange = Mathf.Clamp(radarRange / (zoom > 0 ? 1.35f : 1f / 1.35f), 22f, 180f);
        static Rect FilterBounds(Rect panel) => new(panel.x - 155, panel.yMax - 222, 149, 222);
        void DrawFilters(Rect panel)
        {
            Fill(panel, Shadow);
            Outline(panel, Copper);
            float x = panel.x + 6, y = panel.y + 6;
            trackQuests = GUI.Toggle(new Rect(x, y, 135, 23), trackQuests, "Quests");
            trackEnemies = GUI.Toggle(new Rect(x, y + 23, 135, 23), trackEnemies, "Hostiles");
            trackLoot = GUI.Toggle(new Rect(x, y + 46, 135, 23), trackLoot, "Loot");
            trackChests = GUI.Toggle(new Rect(x, y + 69, 135, 23), trackChests, "Treasure sites");
            trackNPCs = GUI.Toggle(new Rect(x, y + 92, 135, 23), trackNPCs, "Town services");
            trackHerbs = GUI.Toggle(new Rect(x, y + 115, 135, 23), trackHerbs, "Herbs");
            trackMinerals = GUI.Toggle(new Rect(x, y + 138, 135, 23), trackMinerals, "Ore");
            rotateWithCamera = GUI.Toggle(new Rect(x, y + 161, 135, 23), rotateWithCamera, "Rotate with camera");
            showCompass = GUI.Toggle(new Rect(x, y + 184, 135, 23), showCompass, "Compass");
        }

        void RefreshMapPoints()
        {
            mapPoints.Clear();
            if (trackEnemies)
                foreach (var enemy in EnemyController.AllByKey.Values)
                    if (enemy && !enemy.trainingDummy && enemy.isActiveAndEnabled && enemy.GetComponent<Combatant>().Alive)
                        mapPoints.Add(new MapPoint(enemy.transform.position, new Color(.92f, .43f, .35f), enemy.isBoss ? 7 : 4));
            if (trackHerbs || trackMinerals)
                foreach (var node in FindObjectsByType<GatheringNode>(FindObjectsSortMode.None))
                {
                    bool herb = node.gatheringType == GatheringType.Herbalism;
                    if (herb ? trackHerbs : trackMinerals)
                        mapPoints.Add(new MapPoint(node.transform.position, herb ? new Color(.61f, 1, .49f) : new Color(.79f, .82f, .9f), 5));
                }
            if (trackLoot && LootDropSystem.Instance)
                foreach (var corpse in LootDropSystem.Instance.ActiveCorpses)
                    mapPoints.Add(new MapPoint(corpse.position, PinColor, 5));
            if (trackChests)
            {
                foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
                    if (!player.HasOpenedChest(chest.chestId)) mapPoints.Add(new MapPoint(chest.transform.position, PinColor, 6));
                if (ArchaeologySystem.Instance)
                    foreach (var site in ArchaeologySystem.Instance.digSites)
                        mapPoints.Add(new MapPoint(site.center, Copper, 7));
            }
            if (trackNPCs)
            {
                foreach (var npc in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                    mapPoints.Add(new MapPoint(npc.transform.position, new Color(.65f, .82f, .63f), 5));
                foreach (var fm in FindObjectsByType<FlightMaster>(FindObjectsSortMode.None))
                    mapPoints.Add(new MapPoint(fm.transform.position, PlayerColor, 7));
            }
            foreach (var portal in FindObjectsByType<DungeonPortal>(FindObjectsSortMode.None))
            {
                if (portal.destinationZoneName == "The Scarlet Crypts" && !portal.isExitPortal) cryptEntrance = portal;
                if (portal.isExitPortal && MapFloor(portal.transform.position) == 1) cryptExit = portal;
                mapPoints.Add(new MapPoint(portal.transform.position, new Color(.83f, .63f, 1f), 7));
            }
        }

        void PrepareMapStyle()
        {
            if (mapDisc) return;
            const int size = 64;
            mapDisc = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Map marker", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(size * .5f - new Vector2(x + .5f - size * .5f, y + .5f - size * .5f).magnitude));
            mapDisc.SetPixels(pixels);
            mapDisc.Apply(false, true);
            header = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            caption = new GUIStyle(GUI.skin.label) { fontSize = 11, clipping = TextClipping.Clip };
            centered = new GUIStyle(caption) { alignment = TextAnchor.MiddleCenter };
            glyph = new GUIStyle(centered) { fontSize = 14, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.button) { fontSize = 11, fontStyle = FontStyle.Bold, padding = new RectOffset(0, 0, 0, 0) };
        }

        static Vector2 Project(Vector3 offset, float yaw) => Rotate(new Vector2(offset.x, -offset.z), -yaw);
        static Vector2 Rotate(Vector2 direction, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad, c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            return new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c);
        }

        static Rect Centered(Vector2 p, float size) => new(p.x - size * .5f, p.y - size * .5f, size, size);
        void Dot(Vector2 p, float size, Color color) => Tint(Centered(p, size), mapDisc, color);
        static void Fill(Rect r, Color color) => Tint(r, Texture2D.whiteTexture, color);
        static void Outline(Rect r, Color color)
        {
            Fill(new Rect(r.x, r.y, r.width, 1), color);
            Fill(new Rect(r.x, r.yMax - 1, r.width, 1), color);
            Fill(new Rect(r.x, r.y, 1, r.height), color);
            Fill(new Rect(r.xMax - 1, r.y, 1, r.height), color);
        }

        static void Tint(Rect rect, Texture texture, Color color)
        {
            var old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texture);
            GUI.color = old;
        }

        static void Label(Rect rect, string text, GUIStyle style, Color ink)
        {
            style.normal.textColor = ink;
            GUI.Label(rect, text, style);
        }
    }
}
