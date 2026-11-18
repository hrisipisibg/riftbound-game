using System;
using UnityEngine;

namespace Riftbound
{
    public class ZoneManager : MonoBehaviour
    {
        public static ZoneManager Instance { get; private set; }

        public string CurrentZone { get; private set; } = "Emberwatch Outpost";
        public string CurrentSubZone { get; private set; } = "Alliance Territory";

        string lastLoggedZone = "";
        PlayerController player;

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
            if (player)
            {
                CurrentZone = GetZoneName(player.transform.position);
                CurrentSubZone = GetSubZoneName(player.transform.position);
                lastLoggedZone = CurrentZone;
            }
        }

        void Update()
        {
            if (!player || FrontendMenu.IsOpen) return;

            string zone = GetZoneName(player.transform.position);
            string sub = GetSubZoneName(player.transform.position);

            if (zone != CurrentZone)
            {
                CurrentZone = zone;
                CurrentSubZone = sub;

                if (zone != lastLoggedZone)
                {
                    lastLoggedZone = zone;
                    ZoneBanner.Show(zone, sub);
                    AudioEffects.PlayZoneDiscover();
                    ChatSystem.LogZone(zone);
                    if (WeatherSystem.Instance) WeatherSystem.Instance.EvaluateZoneWeather(false);
                }
            }
        }

        public static string GetZoneName(Vector3 pos)
        {
            if (WorldRegions.Expanded) return WorldRegions.ZoneName(pos);
            // Underground Cathedral Dungeon
            if (pos.y < -20f)
                return "The Scarlet Crypts";

            // High Mountain Aerie / Peak
            if (pos.y > 40f || (pos.z > 210f && Mathf.Abs(pos.x) < 25f && pos.y > 25f))
                return "Wyrmcrest Summit";

            // Horde Frontier Camp
            if (pos.x > 35f && pos.z > 175f)
                return "Camp Bloodrock";

            // Cathedral Courtyard Entrance
            if (pos.x < -35f && pos.z > 175f)
                return "Duskwood Cathedral";

            // Northern Deep Woods
            if (pos.z > 130f)
                return "Whispering Woods";

            // Central Ashwood Ridge
            if (pos.z > 50f)
                return "Ashwood Ridge";

            // Starting Town
            return "Emberwatch Outpost";
        }

        public static string GetSubZoneName(Vector3 pos)
        {
            if (WorldRegions.Expanded)
            {
                string zone = WorldRegions.ZoneName(pos);
                if (WorldRegions.Underground(pos)) return "Cathedral Instance (Dungeon)";
                if (zone == "Camp Bloodrock" || zone == "Gor'Kresh Stronghold" || zone == "Deathknell Hollow" || zone == "Rustwater Scrapworks") return "Horde Territory";
                if (zone == "Emberwatch Outpost" || zone == "Highspire Keep" || zone == "Frostpeak Foothills" || zone == "Silverglen Sanctum") return "Alliance Territory";
                if (zone == "Wyrmcrest Summit" || zone == "Wyrmcrest Crags") return "Dragon Territory (Dangerous)";
                if (zone == "Duskwood Cathedral" || zone == "Duskwood Wilds") return "Contested Haunted Lands";
                if (zone == "Redridge Canyons" || zone == "Redridge Volcanic Crater") return "Contested Badlands";
                return "Contested Wilderness";
            }
            if (pos.y < -20f) return "Cathedral Instance (Dungeon)";
            if (pos.y > 40f || (pos.z > 210f && Mathf.Abs(pos.x) < 25f && pos.y > 25f)) return "Dragon's Roost (Apex Boss)";
            if (pos.x > 35f && pos.z > 175f) return "Horde Frontier Outpost";
            if (pos.x < -35f && pos.z > 175f) return "Contested Territory";
            if (pos.z > 130f) return "Contested Territory";
            if (pos.z > 50f) return "Contested Territory";
            return "Alliance Territory";
        }
    }
}
