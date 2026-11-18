using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public class CalendarEventSystem : MonoBehaviour
    {
        public static CalendarEventSystem Instance { get; private set; }

        public enum EventType
        {
            WorldBossSpawn,
            HolidayFestival,
            DarkmoonFaire,
            PvPBonusWeekend,
            DungeonBonusWeek
        }

        [Serializable]
        public class WorldEvent
        {
            public string id;
            public string title;
            public EventType type;
            public string icon;
            public string location;
            public string description;
            public string activeBuffOrBonus;
            public DateTime startTime;
            public DateTime endTime;
            public bool isServerWide;

            public WorldEvent(string id, string title, EventType type, string icon, string location, string desc, string bonus, DateTime start, DateTime end, bool serverWide = true)
            {
                this.id = id;
                this.title = title;
                this.type = type;
                this.icon = icon;
                this.location = location;
                this.description = desc;
                this.activeBuffOrBonus = bonus;
                this.startTime = start;
                this.endTime = end;
                this.isServerWide = serverWide;
            }

            public bool IsActiveNow => DateTime.Now >= startTime && DateTime.Now <= endTime;
        }

        public List<WorldEvent> scheduledEvents = new List<WorldEvent>();
        public int selectedEventIndex = 0;
        public DateTime calendarViewDate = DateTime.Now;
        public bool IsWindowOpen { get; set; } = false;

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
            SeedCalendarEvents();
        }

        public void SeedCalendarEvents()
        {
            scheduledEvents.Clear();

            DateTime now = DateTime.Now;

            // 1. World Boss Incursion
            scheduledEvents.Add(new WorldEvent(
                "evt_gorgonax_invasion",
                "Invasion of Gorgonax",
                EventType.WorldBossSpawn,
                "🔥",
                "Ashwood Ridge Crater",
                "The earth trembles as Gorgonax the Worldbreaker emerges from molten fissures, demanding a 10-man raid vanguard to defend the realm.",
                "+25% Bonus Fire Resistance in Ashwood Ridge",
                now.AddDays(-1),
                now.AddDays(2)
            ));

            // 2. Darkmoon Carnival / Faire
            scheduledEvents.Add(new WorldEvent(
                "evt_darkmoon_faire",
                "Darkmoon Faire Carnival",
                EventType.DarkmoonFaire,
                "🎪",
                "Whispering Woods Outskirts",
                "Silas Darkmoon and his exotic troupe have pitched their tents! Play carnival games, trade Darkmoon Cards, and claim mystical fortune buffs.",
                "Sayge's Fortune: +10% to all Primary Attributes for 2 hours",
                now.AddDays(-2),
                now.AddDays(5)
            ));

            // 3. Midsummer Fire Festival
            scheduledEvents.Add(new WorldEvent(
                "evt_fire_festival",
                "Midsummer Fire Festival",
                EventType.HolidayFestival,
                "🎆",
                "Emberwatch & Camp Bloodrock",
                "Bonfires are lit across the realms! Honor the ancestral flames, toss burning torches, and claim Ribbon Pole stamina blessings.",
                "Ribbon Pole Dance: +10% Experience from monster kills",
                now.AddDays(-3),
                now.AddDays(7)
            ));

            // 4. Crossroads Battleground Bonus Weekend
            scheduledEvents.Add(new WorldEvent(
                "evt_pvp_bonus_weekend",
                "Crossroads Call to Arms",
                EventType.PvPBonusWeekend,
                "⚔",
                "Crossroads Battleground",
                "The war between the Silver Griffin and Ashen Pact reaches a boiling point. Earn doubled Honor points for arena and battleground matches.",
                "+100% Lifetime Honor and Marks of Honor",
                now.AddHours(-12),
                now.AddDays(3)
            ));

            // 5. Timewalking Dungeon Bonus
            scheduledEvents.Add(new WorldEvent(
                "evt_dungeon_bonus",
                "Cathedral Dungeon Timewalking",
                EventType.DungeonBonusWeek,
                "💀",
                "The Scarlet Crypts",
                "Subterranean bosses drop additional Emblems of Valor, Truesilver ingots, and rare transmog appearances.",
                "Guaranteed Epic Loot Drop from Final Boss",
                now.AddDays(-1),
                now.AddDays(4)
            ));
        }

        public void ToggleWindow()
        {
            IsWindowOpen = !IsWindowOpen;
            AudioEffects.PlayClick();
        }

        public List<WorldEvent> GetActiveEvents()
        {
            return scheduledEvents.FindAll(e => e.IsActiveNow);
        }

        public bool ClaimEventBuff(WorldEvent evt, out string reason)
        {
            if (evt == null || !evt.IsActiveNow)
            {
                reason = "This event is not currently active.";
                return false;
            }

            if (!player) player = FindFirstObjectByType<PlayerController>();

            ZoneBanner.Show("EVENT BLESSING GRANTED!", evt.title);
            ChatSystem.LogSystem($"<color=#ffd700><b>[Calendar]</b> You received {evt.activeBuffOrBonus} from {evt.title}!</color>");
            AudioEffects.PlayQuestComplete();

            reason = $"Received blessing: {evt.activeBuffOrBonus}";
            return true;
        }

        // GUI Rendering
        public void DrawCalendarGUI(float screenWidth, float screenHeight)
        {
            if (!IsWindowOpen) return;

            float winW = 640f;
            float winH = 480f;
            float winX = (screenWidth - winW) * 0.5f;
            float winY = (screenHeight - winH) * 0.5f;

            // Background & Gothic Border
            DrawRect(new Rect(winX - 3, winY - 3, winW + 6, winH + 6), new Color(0.85f, 0.70f, 0.28f, 0.95f)); // Gold trim
            DrawRect(new Rect(winX, winY, winW, winH), new Color(0.06f, 0.08f, 0.12f, 0.98f));

            // Header
            DrawRect(new Rect(winX, winY, winW, 38f), new Color(0.12f, 0.15f, 0.22f, 1f));
            var headStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headStyle.normal.textColor = new Color(1f, 0.85f, 0.35f);
            GUI.Label(new Rect(winX + 16, winY + 6, winW - 60, 26), $"📅 REALM CALENDAR & WORLD FESTIVALS ({DateTime.Now:MMMM yyyy})", headStyle);

            if (GUI.Button(new Rect(winX + winW - 32, winY + 6, 24, 24), "✕"))
            {
                IsWindowOpen = false;
                AudioEffects.PlayClick();
            }

            // Left Pane: Event List
            float listW = 260f;
            float listH = winH - 60f;
            DrawRect(new Rect(winX + 14, winY + 48, listW, listH), new Color(0.04f, 0.05f, 0.07f, 0.85f));

            var itemBtn = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(6, 6, 4, 4) };
            for (int i = 0; i < scheduledEvents.Count; i++)
            {
                var evt = scheduledEvents[i];
                bool isSel = (i == selectedEventIndex);
                if (isSel) GUI.color = new Color(1f, 0.85f, 0.3f);

                string activeTag = evt.IsActiveNow ? "<color=#40ff40>★ Active</color>" : "<color=#aaaaaa>Upcoming</color>";
                if (GUI.Button(new Rect(winX + 18, winY + 52 + i * 42, listW - 8, 38), $"{evt.icon}  {evt.title}\n{activeTag}", itemBtn))
                {
                    selectedEventIndex = i;
                    AudioEffects.PlayClick();
                }
                GUI.color = Color.white;
            }

            // Right Pane: Event Details & Blessing Claim
            float detX = winX + listW + 28;
            float detW = winW - listW - 42;
            if (selectedEventIndex >= 0 && selectedEventIndex < scheduledEvents.Count)
            {
                var sel = scheduledEvents[selectedEventIndex];

                var dTitle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
                dTitle.normal.textColor = new Color(1f, 0.85f, 0.35f);
                GUI.Label(new Rect(detX, winY + 52, detW, 24), $"{sel.icon} {sel.title}", dTitle);

                GUI.Label(new Rect(detX, winY + 80, detW, 20), $"Location: <b>{sel.location}</b>");
                GUI.Label(new Rect(detX, winY + 102, detW, 20), $"Duration: {sel.startTime:MMM dd} - {sel.endTime:MMM dd}");

                var descStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
                descStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);
                GUI.Label(new Rect(detX, winY + 130, detW, 70), sel.description, descStyle);

                // Active Buff / Celebration Reward Box
                DrawRect(new Rect(detX, winY + 215, detW, 70), new Color(0.12f, 0.14f, 0.18f, 0.95f));
                GUI.Label(new Rect(detX + 10, winY + 220, detW - 20, 20), "★ Event Blessing & Zone Buff:");

                var buffStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, wordWrap = true };
                buffStyle.normal.textColor = new Color(0.35f, 1f, 0.45f);
                GUI.Label(new Rect(detX + 10, winY + 242, detW - 20, 38), sel.activeBuffOrBonus, buffStyle);

                // Claim Blessing Button
                float botY = winY + winH - 48f;
                if (sel.IsActiveNow)
                {
                    GUI.color = new Color(0.4f, 1f, 0.4f);
                    if (GUI.Button(new Rect(detX, botY, 180, 36), "✨ Claim Blessing"))
                    {
                        ClaimEventBuff(sel, out _);
                    }
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(1f, 1f, 1f, 0.4f);
                    GUI.Button(new Rect(detX, botY, 180, 36), "Event Not Active Yet");
                    GUI.color = Color.white;
                }
            }
        }

        void DrawRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
