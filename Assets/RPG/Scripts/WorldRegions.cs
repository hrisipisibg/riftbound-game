using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound
{
    public static class WorldRegions
    {
        public const string ScenePath = "Assets/RPG/Scenes/Outpost-Expanded.unity";
        public static bool Expanded => SceneManager.GetActiveScene().name == "Outpost-Expanded";
        public static Rect Bounds => Expanded ? new Rect(-320, -160, 768, 768) : new Rect(-120, -115, 240, 380);
        public static readonly Vector3 Emberford = new Vector3(72, 1.2f, -39);
        public static readonly Vector3 AshwoodWatchChapel = new Vector3(69, 5.3f, 105);
        public static readonly Vector3 BriarStockade = new Vector3(149, 11.8f, 174);
        public static readonly Vector3 Highspire = new Vector3(-200, 12, -60);
        public static readonly Vector3 Frostpeak = new Vector3(-95, 16, -90);
        public static readonly Vector3 Silverglen = new Vector3(-150, 15, 75);
        public static readonly Vector3 Deathknell = new Vector3(-240, 12, 180);
        public static readonly Vector3 Rustwater = new Vector3(240, 14, 150);
        public static readonly Vector3 Bloodrock = new Vector3(280, 18, 210);
        public static readonly Vector3 Stronghold = new Vector3(340, 25, 290);
        public static readonly Vector3 Cathedral = new Vector3(-220, 18, 260);
        public static readonly Vector3 Summit = new Vector3(250, 75, 510);
        public static readonly Vector3 Greenhollow = new Vector3(-40, 13, 400);
        public static readonly Vector3 WorldBoss = new Vector3(180, 22, 300);

        public static Terrain TerrainAt(Vector3 point) => TerrainAt(point, Terrain.activeTerrains);
        public static Terrain TerrainAt(Vector3 point, Terrain[] terrains)
        {
            foreach (var terrain in terrains)
            {
                var origin = terrain.transform.position;
                var size = terrain.terrainData.size;
                if (point.x >= origin.x && point.x <= origin.x + size.x && point.z >= origin.z && point.z <= origin.z + size.z)
                    return terrain;
            }
            return null;
        }

        public static float GroundHeight(Vector3 point, Terrain[] terrains = null)
        {
            var terrain = TerrainAt(point, terrains ?? Terrain.activeTerrains);
            return terrain ? terrain.SampleHeight(point) + terrain.transform.position.y : point.y;
        }

        public static bool Underground(Vector3 point) => point.y < -20 && Vector2.Distance(new Vector2(point.x, point.z), new Vector2(-400, 200)) < 140;

        public static Vector3 LegacyPosition(Vector3 point, string race = null)
        {
            if (!Expanded || point.y < -20) return point;
            if ((race == "Dwarf") && Near(point, new Vector3(20, 0, -15), 18)) return Shift(point, new Vector3(20, .5f, -15), Frostpeak);
            if ((race == "Elf" || race == "High Elf") && Near(point, new Vector3(-25, 0, 15), 18)) return Shift(point, new Vector3(-25, .5f, 15), Silverglen);
            if (race == "Undead" && Near(point, new Vector3(-28, 0, 160), 20)) return Shift(point, new Vector3(-28, 5.7f, 160), Deathknell);
            if (race == "Goblin" && Near(point, new Vector3(38, 0, 175), 20)) return Shift(point, new Vector3(38, 9, 175), Rustwater);
            if (point.y > 30 && Near(point, new Vector3(0, 0, 225), 35)) return Shift(point, new Vector3(0, 48, 225), Summit);
            if (Near(point, new Vector3(85, 0, 235), 24)) return Shift(point, new Vector3(85, 14, 235), Stronghold);
            if (Near(point, new Vector3(55, 0, 215), 20)) return Shift(point, new Vector3(55, 9, 215), Bloodrock);
            if (Near(point, new Vector3(-55, 0, 215), 30)) return Shift(point, new Vector3(-55, 12.5f, 215), Cathedral);
            if (Near(point, new Vector3(10, 0, 185), 17)) return Shift(point, new Vector3(10, 7.5f, 185), WorldBoss);
            if (Near(point, new Vector3(38, 0, 175), 20)) return Shift(point, new Vector3(38, 9, 175), Rustwater);
            if (Near(point, new Vector3(-45, 0, -20), 22)) return Shift(point, new Vector3(-45, .87f, -20), Highspire);
            if (point.z > 139 && point.z < 205 && point.x > -48 && point.x < 38) return point + new Vector3(-17, 8, 244);
            return point;
        }

        static bool Near(Vector3 a, Vector3 b, float radius) => new Vector2(a.x - b.x, a.z - b.z).sqrMagnitude <= radius * radius;
        static Vector3 Shift(Vector3 point, Vector3 oldCenter, Vector3 newCenter) => point + newCenter - oldCenter;

        public static string ZoneName(Vector3 point)
        {
            if (BrassHollows.Inside(point)) return "The Brass Hollows";
            if (Underground(point)) return "The Scarlet Crypts";
            if (point.z > 430 && point.x > 95) return "Wyrmcrest Summit";
            if (point.z > 330 && point.x >= 110) return "Wyrmcrest Crags";
            if (point.z > 320 && point.x < 110) return "Greenhollow Valley";
            if (point.x < -180 && point.z > 210) return "Duskwood Cathedral";
            if (point.x < -180 && point.z > 140) return "Deathknell Hollow";
            if (point.x < -120 && point.z > 100) return "Duskwood Wilds";
            if (point.x > 290 && point.z > 245) return "Gor'Kresh Stronghold";
            if (point.x >= 140 && point.x <= 230 && point.z >= 260 && point.z <= 340) return "Redridge Volcanic Crater";
            if (point.x >= 205 && point.x <= 275 && point.z >= 115 && point.z <= 180) return "Rustwater Scrapworks";
            if (point.x > 210 && point.z > 180) return "Camp Bloodrock";
            if (point.x > 105 && point.z > 80) return "Redridge Canyons";
            if (point.x >= -190 && point.x <= -110 && point.z >= 40 && point.z <= 110) return "Silverglen Sanctum";
            if (point.x < -140 && point.z < -20) return "Highspire Keep";
            if (point.x >= -140 && point.x < 0 && point.z < -20) return "Frostpeak Foothills";
            if (point.x < -120 && point.z < 100) return "Willowmere Wilds";
            if (point.z > 160 && Mathf.Abs(point.x) <= 105) return "Whispering Woods";
            if (point.z > 60 && Mathf.Abs(point.x) <= 105) return "Ashwood Ridge";
            return "Emberwatch Outpost";
        }
    }
}
