using System;
using UnityEngine;

namespace Riftbound
{
    public class WeatherSystem : MonoBehaviour
    {
        public static WeatherSystem Instance { get; private set; }

        public enum WeatherType
        {
            Clear,
            Rain,
            Storm,
            Snow,
            DenseFog,
            Sandstorm
        }

        public WeatherType CurrentWeather { get; private set; } = WeatherType.Clear;
        public float WeatherIntensity { get; private set; } = 0f; // 0.0 to 1.0

        // In-Game Day/Night Cycle (24 in-game hours per 24 minutes real-time)
        public float TimeOfDayHours { get; set; } = 14.5f; // 2:30 PM default
        public float DayCycleSpeed { get; set; } = .35f;   // 1 hour per minute real time
        public bool DayNightEnabled { get; set; } = true;

        public string FormattedTime
        {
            get
            {
                int h = Mathf.FloorToInt(TimeOfDayHours) % 24;
                int m = Mathf.FloorToInt((TimeOfDayHours - Mathf.Floor(TimeOfDayHours)) * 60f);
                return $"{h:D2}:{m:D2}";
            }
        }

        public bool IsNight => TimeOfDayHours < 6f || TimeOfDayHours > 20f;

        // Weather Transition timing
        float nextWeatherChangeTime;
        public float WeatherChangeInterval = 180f; // Shift weather every 90 seconds

        // Simulated Particle Precipitation & Fog State
        Color targetFogColor;
        float targetFogDensity;
        Light directionalSun;
        PlayerController player;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
            nextWeatherChangeTime = Time.time + WeatherChangeInterval;
        }

        void Start()
        {
            FindDirectionalSun();
            EvaluateZoneWeather(true);
        }

        void FindDirectionalSun()
        {
            var lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional && (l.cullingMask & 1) != 0)
                {
                    directionalSun = l;
                    break;
                }
            }
        }

        void Update()
        {
            // Advance Day/Night Clock
            if (DayNightEnabled)
            {
                TimeOfDayHours = (TimeOfDayHours + (Time.deltaTime / 60f) * DayCycleSpeed) % 24f;
            }

            // Periodic weather transition
            if (Time.time >= nextWeatherChangeTime)
            {
                nextWeatherChangeTime = Time.time + WeatherChangeInterval;
                EvaluateZoneWeather(false);
            }

            ConfigureFogAndLighting();
            // Smooth atmosphere lerp
            ApplyAtmosphereLighting();
        }

        public void SetWeather(WeatherType type, float intensity = 1.0f)
        {
            CurrentWeather = type;
            WeatherIntensity = Mathf.Clamp01(intensity);
            ConfigureFogAndLighting();

            string icon = GetWeatherIcon(type);
            ZoneBanner.Show("WEATHER CHANGE", $"{icon} {type.ToString().ToUpperInvariant()}");
            ChatSystem.LogSystem($"<color=#70d0ff>[Weather] The sky shifts into {type}!</color>");
        }

        public void SetTimeOfDay(float hours)
        {
            TimeOfDayHours = Mathf.Clamp(hours, 0f, 23.99f);
            ApplyAtmosphereLighting();
        }

        public void EvaluateZoneWeather(bool instant)
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            Vector3 pos = player ? player.transform.position : Vector3.zero;
            string zone = ZoneManager.GetZoneName(pos);

            WeatherType chosen = WeatherType.Clear;

            // Regional climatic rules
            if (zone == "Wyrmcrest Summit")
            {
                chosen = (UnityEngine.Random.value < 0.65f) ? WeatherType.Snow : WeatherType.DenseFog;
            }
            else if (zone == "Camp Bloodrock" || zone == "Redridge Canyons")
            {
                chosen = (UnityEngine.Random.value < 0.5f) ? WeatherType.Sandstorm : WeatherType.Clear;
            }
            else if (zone == "Duskwood Cathedral" || zone == "The Scarlet Crypts")
            {
                chosen = (UnityEngine.Random.value < 0.6f) ? WeatherType.DenseFog : WeatherType.Rain;
            }
            else if (zone == "Whispering Woods" || zone == "Ashwood Ridge")
            {
                float roll = UnityEngine.Random.value;
                chosen = roll < 0.45f ? WeatherType.Rain : (roll < 0.75f ? WeatherType.Storm : WeatherType.Clear);
            }
            else
            {
                // Amber Vale (Gentle temperate)
                chosen = instant ? WeatherType.Clear : ((UnityEngine.Random.value < .20f) ? WeatherType.Rain : WeatherType.Clear);
            }

            CurrentWeather = chosen;
            WeatherIntensity = UnityEngine.Random.Range(0.6f, 1.0f);
            ConfigureFogAndLighting();

            if (!instant)
            {
                string icon = GetWeatherIcon(chosen);
                ZoneBanner.Show("WEATHER CHANGE", $"{icon} {chosen.ToString().ToUpperInvariant()}");
                ChatSystem.LogSystem($"<color=#70d0ff>[Weather] Regional weather shifted to {chosen}.</color>");
            }
        }

        void ConfigureFogAndLighting()
        {
            switch (CurrentWeather)
            {
                case WeatherType.Clear:
                    targetFogColor = IsNight ? new Color(0.04f, 0.05f, 0.09f) : new Color(0.70f, 0.80f, 0.90f);
                    targetFogDensity = .0018f;
                    break;
                case WeatherType.Rain:
                    targetFogColor = new Color(0.35f, 0.40f, 0.45f);
                    targetFogDensity = .0045f;
                    break;
                case WeatherType.Storm:
                    targetFogColor = new Color(0.20f, 0.22f, 0.28f);
                    targetFogDensity = .007f;
                    break;
                case WeatherType.Snow:
                    targetFogColor = new Color(0.85f, 0.88f, 0.95f);
                    targetFogDensity = .004f;
                    break;
                case WeatherType.DenseFog:
                    targetFogColor = new Color(0.30f, 0.32f, 0.35f);
                    targetFogDensity = .008f;
                    break;
                case WeatherType.Sandstorm:
                    targetFogColor = new Color(0.75f, 0.58f, 0.35f);
                    targetFogDensity = .006f;
                    break;
            }
        }

        void ApplyAtmosphereLighting()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * 1.5f);
            RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * 1.5f);

            // Sun Rotation & Color by Time of Day
            if (!directionalSun) FindDirectionalSun();
            if (directionalSun)
            {
                // Sun rotates 360 degrees along X based on 24hr clock (Noon = 90 deg down, Midnight = -90 deg up)
                float sunAngle = ((TimeOfDayHours / 24f) * 360f) - 90f;
                directionalSun.transform.rotation = Quaternion.Euler(IsNight ? sunAngle + 180f : sunAngle, -30f, 0f);

                if (IsNight)
                {
                    // Moonlight
                    directionalSun.color = Color.Lerp(directionalSun.color, new Color(0.25f, 0.35f, 0.65f), Time.deltaTime * 2f);
                    directionalSun.intensity = Mathf.Lerp(directionalSun.intensity, .45f, Time.deltaTime * 2f);
                }
                else
                {
                    // Daylight
                    Color daySun = (CurrentWeather == WeatherType.Storm) ? new Color(0.4f, 0.45f, 0.55f) :
                                   (CurrentWeather == WeatherType.Sandstorm) ? new Color(0.9f, 0.7f, 0.4f) :
                                   new Color(1f, 0.95f, 0.85f);
                    directionalSun.color = Color.Lerp(directionalSun.color, daySun, Time.deltaTime * 2f);
                    directionalSun.intensity = Mathf.Lerp(directionalSun.intensity, CurrentWeather == WeatherType.Storm ? .85f : 1.65f, Time.deltaTime * 2f);
                }
            }
        }

        public static string GetWeatherIcon(WeatherType type)
        {
            return type switch
            {
                WeatherType.Clear => "☀️",
                WeatherType.Rain => "🌧",
                WeatherType.Storm => "⛈",
                WeatherType.Snow => "❄",
                WeatherType.DenseFog => "🌫",
                WeatherType.Sandstorm => "🌪",
                _ => "☀️"
            };
        }

        // GUI Rendering: Top Minimap Weather & Clock Badge
        public void DrawWeatherClockGUI(float minimapCenterX, float minimapCenterY, float minimapRadius)
        {
            float badgeW = 100f;
            float badgeH = 22f;
            float bx = minimapCenterX - badgeW * 0.5f;
            float by = minimapCenterY + minimapRadius + 4f;

            // Background pill
            var old = GUI.color;
            GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
            GUI.DrawTexture(new Rect(bx, by, badgeW, badgeH), Texture2D.whiteTexture);
            GUI.color = old;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = IsNight ? new Color(0.6f, 0.75f, 1f) : new Color(1f, 0.9f, 0.4f);

            string icon = GetWeatherIcon(CurrentWeather);
            string timeIcon = IsNight ? "🌙" : "☀️";
            GUI.Label(new Rect(bx, by + 2, badgeW, 18), $"{timeIcon} {FormattedTime}  ·  {icon}", style);
        }
    }
}
