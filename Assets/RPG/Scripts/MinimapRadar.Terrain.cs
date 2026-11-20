using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class MinimapRadar
    {
        const int MapSize = 128;
        readonly float[] elevations = new float[MapSize * MapSize];
        readonly Color32[] mapPixels = new Color32[MapSize * MapSize];
        readonly List<MapLine> mapLines = new();
        readonly List<Bounds> dungeonFloors = new();
        Vector3 terrainOrigin;
        float lastRange, nextTerrainUpdate;
        int lastFloor = -1;
        bool hasRoutes;

        readonly struct MapLine
        {
            public readonly Vector3 from, to;
            public readonly Color color;
            public readonly float width;

            public MapLine(Vector3 from, Vector3 to, Color color, float width)
            {
                this.from = from;
                this.to = to;
                this.color = color;
                this.width = width;
            }
        }

        void UpdateTerrainTexture(Vector3 position, float range, int floor)
        {
            if (!terrainTex)
                terrainTex = new Texture2D(MapSize, MapSize, TextureFormat.RGBA32, false)
                    { name = "Local map relief", wrapMode = TextureWrapMode.Clamp };
            bool scaleChanged = lastRange != range || lastFloor != floor;
            float movement = (terrainOrigin - position).sqrMagnitude;
            if (!scaleChanged && (movement < .8f || movement < 900f && Time.unscaledTime < nextTerrainUpdate)) return;
            if (!hasRoutes && floor == 0) CacheRoutes();
            if (floor > 0 && lastFloor != floor) CacheDungeonFloors(floor);
            lastRange = range;
            lastFloor = floor;
            nextTerrainUpdate = Time.unscaledTime + .35f;
            terrainOrigin = position;
            var terrains = Terrain.activeTerrains;
            float half = MapSize * .5f;
            float metersPerPixel = range / half;
            Vector3 start = position - new Vector3(range, 0, range);
            for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
            {
                int i = y * MapSize + x;
                var point = start + new Vector3((x + .5f) * metersPerPixel, 0f, (y + .5f) * metersPerPixel);
                elevations[i] = floor == 0 ? WorldRegions.GroundHeight(point, terrains) : point.y;
            }
            for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
            {
                int i = y * MapSize + x;
                float edge = Mathf.Clamp01(half - new Vector2(x + .5f - half, y + .5f - half).magnitude);
                if (edge <= 0)
                {
                    mapPixels[i] = new Color32(0, 0, 0, 0);
                    continue;
                }
                var point = start + new Vector3((x + .5f) * metersPerPixel, 0f, (y + .5f) * metersPerPixel);
                point.y = elevations[i];
                Color color = floor == 0 ? TerrainColor(point) : DungeonColor(point, floor);
                float high = elevations[y * MapSize + Mathf.Min(MapSize - 1, x + 1)];
                float north = elevations[Mathf.Min(MapSize - 1, y + 1) * MapSize + x];
                float grade = Mathf.Clamp((high - point.y) * -.18f + (north - point.y) * .22f, -.30f, .25f);
                float contour = floor == 0 && Mathf.Floor(point.y / 4f) != Mathf.Floor(high / 4f) ? -.09f : 0f;
                float noise = floor == 0 ? (Mathf.PerlinNoise(point.x * .11f, point.z * .11f) - .5f) * .15f : 0f;
                color *= 1f + grade + contour + noise;
                color.a = edge;
                mapPixels[i] = color;
            }
            if (floor == 0)
                foreach (var line in mapLines) PaintLine(line, start, metersPerPixel);
            terrainTex.SetPixels32(mapPixels);
            terrainTex.Apply(false);
        }

        static Color ZoneColor(string zone) => zone switch
        {
            "Wyrmcrest Summit" => new(.58f, .70f, .75f),
            "Wyrmcrest Crags" => new(.43f, .53f, .57f),
            "Highspire Keep" => new(.50f, .57f, .60f),
            "Frostpeak Foothills" => new(.42f, .56f, .60f),
            "Greenhollow Valley" => new(.38f, .56f, .29f),
            "Whispering Woods" => new(.22f, .40f, .31f),
            "Ashwood Ridge" => new(.40f, .47f, .27f),
            "Willowmere Wilds" => new(.28f, .49f, .45f),
            "Silverglen Sanctum" => new(.42f, .43f, .56f),
            "Duskwood Cathedral" => new(.33f, .34f, .45f),
            "Deathknell Hollow" => new(.39f, .36f, .47f),
            "Duskwood Wilds" => new(.26f, .34f, .35f),
            "Gor'Kresh Stronghold" => new(.51f, .29f, .23f),
            "Redridge Volcanic Crater" => new(.52f, .28f, .21f),
            "Redridge Canyons" => new(.57f, .38f, .27f),
            "Rustwater Scrapworks" => new(.49f, .39f, .30f),
            "Camp Bloodrock" => new(.56f, .34f, .25f),
            "The Brass Hollows" => new(.49f, .40f, .29f),
            "The Scarlet Crypts" => new(.47f, .39f, .55f),
            _ => new(.34f, .49f, .32f)
        };

        static Color TerrainColor(Vector3 point)
        {
            // Interpolate the biome inks across boundaries so they follow the relief softly.
            const float grid = 13f;
            float x = Mathf.Floor(point.x / grid) * grid;
            float z = Mathf.Floor(point.z / grid) * grid;
            float u = Mathf.SmoothStep(0, 1, (point.x - x) / grid);
            float v = Mathf.SmoothStep(0, 1, (point.z - z) / grid);
            Color Sample(float east, float north) => ZoneColor(ZoneManager.GetZoneName(new Vector3(east, point.y, north)));
            return Color.Lerp(Color.Lerp(Sample(x, z), Sample(x + grid, z), u),
                Color.Lerp(Sample(x, z + grid), Sample(x + grid, z + grid), u), v);
        }

        void CacheRoutes()
        {
            hasRoutes = true;
            var trail = new Color(.80f, .68f, .46f);
            var network = FindFirstObjectByType<WorldMapRoutes>();
            if (network && network.routes != null)
                foreach (var route in network.routes)
                    for (int i = 1; route.points != null && i < route.points.Length; i++)
                        mapLines.Add(new MapLine(route.points[i - 1], route.points[i], trail, 2.5f));
            mapLines.Add(new MapLine(new Vector3(0, 0, 2), new Vector3(0, 0, 101), trail, 4.5f));
            mapLines.Add(new MapLine(new Vector3(0, 0, 51), new Vector3(25, 0, 64), trail, 3.5f));
            mapLines.Add(new MapLine(new Vector3(25, 0, 64), new Vector3(35, 0, 91), trail, 3.5f));
            mapLines.Add(new MapLine(new Vector3(0, 0, 50), new Vector3(-34, 0, 70), trail, 3.5f));
            // Width is derived from the same bank vertices that place the rendered water.
            CacheRiver("Westbank flowing river", new Color(.30f, .68f, .82f));
            CacheRiver("Shallow Willow Brook", new Color(.35f, .72f, .82f));
            if (!WorldRegions.Expanded)
                mapLines.Add(new MapLine(new Vector3(2.4f, 0, -45), new Vector3(2.4f, 0, 55),
                    new Color(.32f, .69f, .82f), 3.2f));
        }

        void CacheRiver(string name, Color color)
        {
            var river = GameObject.Find(name);
            var filter = river ? river.GetComponent<MeshFilter>() : null;
            if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return;
            var banks = filter.sharedMesh.vertices;
            for (int i = 2; i + 1 < banks.Length; i += 2)
            {
                var from = filter.transform.TransformPoint((banks[i - 2] + banks[i - 1]) * .5f);
                var to = filter.transform.TransformPoint((banks[i] + banks[i + 1]) * .5f);
                float width = filter.transform.TransformVector(banks[i] - banks[i + 1]).magnitude;
                mapLines.Add(new MapLine(from, to, color, width));
            }
        }

        void CacheDungeonFloors(int floor)
        {
            dungeonFloors.Clear();
            foreach (var box in FindObjectsByType<BoxCollider>(FindObjectsSortMode.None))
                if (!box.isTrigger && MapFloor(box.bounds.center) == floor &&
                    box.name.EndsWith("floor", System.StringComparison.OrdinalIgnoreCase))
                    dungeonFloors.Add(box.bounds);
        }

        Color DungeonColor(Vector3 point, int floor)
        {
            foreach (var room in dungeonFloors)
            {
                if (point.x < room.min.x || point.x > room.max.x || point.z < room.min.z || point.z > room.max.z) continue;
                float distanceToWall = Mathf.Min(Mathf.Min(point.x - room.min.x, room.max.x - point.x),
                    Mathf.Min(point.z - room.min.z, room.max.z - point.z));
                Color inside = ZoneColor(floor == 2 ? "The Brass Hollows" : "The Scarlet Crypts");
                return distanceToWall < .6f ? Color.Lerp(inside, Paper, .38f) : inside;
            }
            return new Color(.14f, .15f, .18f);
        }

        void PaintLine(MapLine line, Vector3 start, float metersPerPixel)
        {
            Vector2 a = new Vector2(line.from.x - start.x, line.from.z - start.z) / metersPerPixel;
            Vector2 b = new Vector2(line.to.x - start.x, line.to.z - start.z) / metersPerPixel;
            Vector2 offset = b - a;
            float width = Mathf.Max(.7f, line.width / metersPerPixel * .5f);
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - width - 1));
            int x1 = Mathf.Min(MapSize - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + width + 1));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - width - 1));
            int y1 = Mathf.Min(MapSize - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + width + 1));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * MapSize + x;
                if (mapPixels[i].a == 0) continue;
                Vector2 relative = new Vector2(x + .5f, y + .5f) - a;
                float t = Mathf.Clamp01(Vector2.Dot(relative, offset) / Mathf.Max(.0001f, offset.sqrMagnitude));
                float coverage = Mathf.Clamp01(width + .5f - (relative - offset * t).magnitude);
                var color = Color32.Lerp(mapPixels[i], line.color, coverage);
                color.a = mapPixels[i].a;
                mapPixels[i] = color;
            }
        }
    }
}
