using System;
using UnityEngine;

namespace Riftbound
{
    public class RestedXPSystem : MonoBehaviour
    {
        public static RestedXPSystem Instance { get; private set; }

        public bool IsInRestZone { get; private set; } = false;
        public float RestedXPPool { get; set; } = 0f;
        public float MaxRestedXPPool => (player != null ? player.NextLevelExperience : 1000) * 1.5f;

        // Hearthstone Home Location
        public Vector3 HearthHomePosition { get; set; } = new Vector3(0f, 0.5f, 0f);
        public string HearthHomeLocationName { get; set; } = "Emberwatch Outpost";

        PlayerController player;
        float restAccumulator = 0f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
        }

        void Start()
        {
            if (player)
            {
                HearthHomePosition = player.transform.position;
            }
        }

        void Update()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || FrontendMenu.IsOpen) return;

            // Rest Zone Detection:
            // Safe sanctuary zones: Emberwatch Outpost, Camp Bloodrock tavern, or near any Innkeeper
            string currentZone = ZoneManager.GetZoneName(player.transform.position);
            bool wasResting = IsInRestZone;

            // Check proximity to Innkeeper or Emberwatch Outpost
            bool nearInnkeeper = false;
            var npcs = FindObjectsByType<TownNPC>(FindObjectsSortMode.None);
            foreach (var npc in npcs)
            {
                if (npc != null && (npc.roleTag.IndexOf("Innkeeper", StringComparison.OrdinalIgnoreCase) >= 0 || npc.npcName.IndexOf("Innkeeper", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (Vector3.Distance(player.transform.position, npc.transform.position) < 18f)
                    {
                        nearInnkeeper = true;
                        break;
                    }
                }
            }

            // Check proximity to campfires or forges across the world
            bool nearCampfire = false;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l != null && (l.name.IndexOf("Campfire", StringComparison.OrdinalIgnoreCase) >= 0 || l.name.IndexOf("Forge", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    if (Vector3.Distance(player.transform.position, l.transform.position) < 8f)
                    {
                        nearCampfire = true;
                        break;
                    }
                }
            }

            IsInRestZone = (currentZone == "Emberwatch Outpost") || (currentZone == "Camp Bloodrock") || nearInnkeeper || nearCampfire;

            if (IsInRestZone && !wasResting)
            {
                ChatSystem.LogZone("You have entered a resting area (Zzz). Double XP enabled!");
            }

            // Out-of-combat health recovery when resting by campfire or tavern
            if (IsInRestZone && !player.InCombat() && player.Stats && player.Stats.Alive && player.Stats.Health < player.Stats.maxHealth)
            {
                player.Stats.Heal(player.Stats.maxHealth * 0.08f * Time.deltaTime);
            }

            // Accumulate rested XP while resting
            if (IsInRestZone)
            {
                restAccumulator += Time.deltaTime;
                if (restAccumulator >= 3f) // Tick every 3 seconds
                {
                    restAccumulator = 0f;
                    float gainRate = (player.NextLevelExperience * 0.05f) / 10f; // Rapid prototype rate
                    RestedXPPool = Mathf.Min(RestedXPPool + gainRate, MaxRestedXPPool);
                }
            }
        }

        /// <summary>
        /// Consumes rested XP on enemy kill or quest turn-in, granting a 200% bonus.
        /// Returns the actual bonus awarded.
        /// </summary>
        public int ApplyRestedBonus(int baseExp)
        {
            if (RestedXPPool <= 0f) return 0;

            int bonus = Mathf.Min(Mathf.RoundToInt(RestedXPPool), baseExp);
            RestedXPPool = Mathf.Max(0f, RestedXPPool - bonus);

            if (bonus > 0)
            {
                ChatSystem.LogCombat($"Rested XP Bonus: +{bonus} EXP! (Remaining: {Mathf.RoundToInt(RestedXPPool)})");
            }
            return bonus;
        }

        public void BindHearthstone(string locationName, Vector3 position)
        {
            HearthHomeLocationName = locationName;
            HearthHomePosition = position;
            ZoneBanner.Show("HEARTHSTONE BOUND", $"Your home is now set to {locationName}.");
            ChatSystem.LogSystem($"You have bound your Hearthstone to {locationName}!");
            AudioEffects.PlayQuestAccept();
        }

        // GUI Indicator: Rest icon "Zzz" on Player Unit Frame
        public void DrawRestedIconGUI(Rect playerFrameRect)
        {
            if (!IsInRestZone) return;

            float pulse = 0.8f + Mathf.PingPong(Time.time * 2f, 0.4f);
            var zStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            zStyle.normal.textColor = new Color(0.95f, 0.95f, 0.3f, pulse);

            // Draw "Zzz" badge over player portrait
            GUI.Label(new Rect(playerFrameRect.x + 35f, playerFrameRect.y - 12f, 35f, 20f), "Zzz", zStyle);
        }
    }
}
