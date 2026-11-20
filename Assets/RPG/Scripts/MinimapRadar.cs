using UnityEngine;

namespace Riftbound
{
    public partial class MinimapRadar : MonoBehaviour
    {
        public static MinimapRadar Instance { get; private set; }

        public float radarRange = 75f;
        public bool rotateWithCamera = false;
        public bool showCompass = true;
        public bool showMinimap = true;

        // Tracking Filter Toggles
        public bool trackQuests = true;
        public bool trackEnemies = true;
        public bool trackHerbs = false;
        public bool trackMinerals = false;
        public bool trackLoot = true;
        public bool trackChests = false;
        public bool trackNPCs = false;

        enum QuestFocus { Automatic, Main, Western, Hollows, Faction }

        public Vector3? customWaypoint { get; private set; }
        public bool TrackingMainQuest => questFocus == QuestFocus.Main && !customWaypoint.HasValue;
        QuestFocus questFocus;
        float nextQuestUpdate;
        Vector3? questDestination;
        string questCaption;
        string waypointCaption;

        public bool TryGetNavigationTarget(out Vector3 position, out string caption)
        {
            position = customWaypoint ?? questDestination ?? default;
            caption = customWaypoint.HasValue ? waypointCaption ?? "Travel waypoint" : questCaption;
            return customWaypoint.HasValue || questDestination.HasValue;
        }

        public void TrackMainQuest() => FollowQuest(QuestFocus.Main);
        public void FollowWesternRoad() => FollowQuest(QuestFocus.Western);
        public void FollowBrassHollows() => FollowQuest(QuestFocus.Hollows);

        void FollowQuest(QuestFocus focus)
        {
            if (!TryGetQuestDestination(focus, out var target, out var caption)) return;
            questFocus = focus;
            questDestination = target;
            questCaption = caption;
            selectedQuest = null;
            customWaypoint = null;
            waypointCaption = null;
        }

        internal void StartFactionNavigation()
        {
            if (questFocus == QuestFocus.Automatic) questFocus = QuestFocus.Faction;
            RefreshQuestNavigation();
        }

        public static void SetWaypoint(Vector3 pos, string label = null)
        {
            var radar = Instance ?? FindFirstObjectByType<MinimapRadar>();
            if (!radar) return;
            radar.selectedQuest = null;
            radar.customWaypoint = pos;
            radar.waypointCaption = label;
            string name = string.IsNullOrEmpty(label) ? "Waypoint set" : $"Waypoint set: {label}";
            ChatSystem.LogSystem($"<color=#00e5ff>{name} ({pos.x:0.0}, {pos.z:0.0})</color>");
            AudioEffects.PlayClick();
        }

        public static void ClearWaypoint()
        {
            var radar = Instance ?? FindFirstObjectByType<MinimapRadar>();
            if (!radar || !radar.customWaypoint.HasValue) return;
            radar.selectedQuest = null;
            radar.customWaypoint = null;
            radar.waypointCaption = null;
            radar.RefreshQuestNavigation();
            ChatSystem.LogSystem("<color=#aaa>Waypoint cleared.</color>");
            AudioEffects.PlayClick();
        }

        PlayerController player;
        OrbitCamera orbitCam;

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

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (terrainTex) Destroy(terrainTex);
            if (mapDisc) Destroy(mapDisc);
        }

        void Update()
        {
            if (!player || FrontendMenu.IsOpen || Time.unscaledTime < nextQuestUpdate) return;
            nextQuestUpdate = Time.unscaledTime + .5f;
            RefreshQuestNavigation();
            RefreshQuestPins();
            if (showMinimap) RefreshMapPoints();
        }

        internal void RefreshQuestNavigation()
        {
            if (questFocus != QuestFocus.Automatic && TryGetQuestDestination(questFocus, out var target, out var caption))
            {
                questDestination = target;
                questCaption = caption;
                return;
            }

            questFocus = QuestFocus.Automatic;
            bool insideHollows = player && BrassHollows.Inside(player.transform.position);
            if ((insideHollows && TryGetQuestDestination(QuestFocus.Hollows, out target, out caption)) ||
                TryGetQuestDestination(QuestFocus.Western, out target, out caption) ||
                (!insideHollows && TryGetQuestDestination(QuestFocus.Hollows, out target, out caption)) ||
                TryGetQuestDestination(QuestFocus.Faction, out target, out caption) ||
                (player && player.QuestAccepted && TryGetQuestDestination(QuestFocus.Main, out target, out caption)))
            {
                questDestination = target;
                questCaption = caption;
            }
            else
            {
                questDestination = null;
                questCaption = null;
            }
        }

        bool TryGetQuestDestination(QuestFocus focus, out Vector3 target, out string caption)
        {
            target = default;
            caption = null;
            if (!player) return false;
            switch (focus)
            {
                case QuestFocus.Main:
                    if (player.QuestComplete) return false;
                    target = player.ObjectivePosition;
                    caption = player.QuestReady ? "Return to Abe" : player.QuestName;
                    return true;
                case QuestFocus.Western:
                    return player.TryGetWesternRoadDestination(out target, out caption);
                case QuestFocus.Hollows:
                    var cave = BrassHollows.Current;
                    return player.BrassHollowsQuestActive && cave && cave.TryDestination(player, out target, out caption);
                case QuestFocus.Faction:
                    var faction = FactionQuestSystem.Instance;
                    return faction && faction.TryGetActiveQuestDestination(player, out target, out caption);
                default:
                    return false;
            }
        }

    }
}
