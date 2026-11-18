using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Riftbound
{
    public class LivingWorldEvents : MonoBehaviour
    {
        public static LivingWorldEvents Instance { get; private set; }
        public static bool IsAuthoritative => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;
        readonly HashSet<string> completedEventIds = new HashSet<string>();

        public enum EventState { Idle, Active, Cooldown }

        // --- Event 1: Supply Caravan ---
        public EventState CaravanState { get; private set; } = EventState.Idle;
        float caravanCooldownUntil;
        GameObject caravanRoot, caravanCart;
        Combatant caravanStats;
        readonly List<EnemyController> caravanRaiders = new List<EnemyController>();
        bool caravanAmbushSpawned;
        float caravanProgress;
        float caravanLeaveTimer;
        Vector3[] caravanRoute;

        // --- Event 2: Roadside Ambush ---
        public EventState AmbushState { get; private set; } = EventState.Idle;
        float ambushCooldownUntil;
        GameObject ambushRoot;
        Combatant ambushScout;
        readonly List<EnemyController> ambushRaiders = new List<EnemyController>();
        float ambushLeaveTimer;
        static readonly Vector3 AmbushSite = new Vector3(-24f, 0f, 215f);

        // --- Ambient Traveller ---
        GameObject travellerRoot;
        Animator travellerAnim;
        Vector3[] travellerRoute;
        int travellerIndex;
        float travellerWaitUntil;

        PlayerController player;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            caravanRoute = new[]
            {
                GroundPos(new Vector3(14f, 0f, 22f)),
                GroundPos(new Vector3(-45f, 0f, 30f)),
                GroundPos(new Vector3(-110f, 0f, 38f)),
                GroundPos(new Vector3(-175f, 0f, 46f))
            };
            travellerRoute = new[]
            {
                GroundPos(new Vector3(-6f, 0f, 18f)),
                GroundPos(new Vector3(-70f, 0f, 32f)),
                GroundPos(new Vector3(-155f, 0f, 44f)),
                GroundPos(new Vector3(-70f, 0f, 32f))
            };
            SpawnAmbientTraveller();
        }

        static Vector3 GroundPos(Vector3 p)
        {
            p.y = WorldRegions.GroundHeight(p);
            return p;
        }

        void SpawnAmbientTraveller()
        {
            if (travellerRoot) return;
            travellerRoot = new GameObject("AmbientTraveller_RoadPorter");
            travellerRoot.transform.position = travellerRoute[0];
            var vis = CharacterVisualFactory.CreateVisualInstance("Human", "Warrior", travellerRoot.transform);
            travellerAnim = vis ? vis.GetComponentInChildren<Animator>() : null;
        }

        void Update()
        {
            if (!IsAuthoritative || FrontendMenu.IsOpen || Time.timeScale <= 0f) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || !player.Stats || !player.Stats.Alive) return;

            UpdateAmbientTraveller();
            UpdateCaravanEvent();
            UpdateAmbushEvent();
        }

        void UpdateAmbientTraveller()
        {
            if (!travellerRoot || travellerRoute == null || travellerRoute.Length == 0) return;
            if (Time.time < travellerWaitUntil)
            {
                if (travellerAnim) travellerAnim.CrossFadeInFixedTime("Idle", 0.2f, 0, 0);
                return;
            }
            Vector3 target = GroundPos(travellerRoute[travellerIndex]);
            Vector3 diff = target - travellerRoot.transform.position;
            diff.y = 0f;
            if (diff.magnitude < 1.2f)
            {
                travellerIndex = (travellerIndex + 1) % travellerRoute.Length;
                travellerWaitUntil = Time.time + 4.5f;
                return;
            }
            Vector3 dir = diff.normalized;
            travellerRoot.transform.rotation = Quaternion.Slerp(travellerRoot.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
            Vector3 next = travellerRoot.transform.position + dir * (2.6f * Time.deltaTime);
            next.y = WorldRegions.GroundHeight(next);
            travellerRoot.transform.position = next;
            if (travellerAnim) travellerAnim.CrossFadeInFixedTime("Walk", 0.2f, 0, 0);
        }

        float nextCaravanBroadcastAt;

        float DistanceToAnyActivePlayer(Vector3 point)
        {
            if (Network.CoopSessionConfig.IsMultiplayer && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && Network.RiftboundCoopPlayer.AllPlayers.Count > 0)
            {
                float best = float.MaxValue;
                foreach (var cp in Network.RiftboundCoopPlayer.AllPlayers)
                {
                    if (!cp || !cp.IsSpawned || cp.Health.Value <= 0) continue;
                    float d = Vector3.Distance(cp.transform.position, point);
                    if (d < best) best = d;
                }
                if (best < float.MaxValue) return best;
            }
            return player ? Vector3.Distance(player.transform.position, point) : float.MaxValue;
        }

        public void SendEventSnapshotToClient(Network.RiftboundCoopPlayer targetPlayer, RpcParams rpcParams)
        {
            if (!IsAuthoritative || !targetPlayer) return;
            if (CaravanState == EventState.Active && caravanCart)
                targetPlayer.SendEventSnapshotRpc("supply_caravan_westbank", true, caravanCart.transform.position, rpcParams);
            if (AmbushState == EventState.Active)
                targetPlayer.SendEventSnapshotRpc("roadside_ambush_greenhollow", true, GroundPos(AmbushSite), rpcParams);
        }

        public void ApplyClientEventState(string eventId, bool active, Vector3 pos)
        {
            if (eventId == "supply_caravan_westbank")
            {
                if (!active)
                {
                    CleanupCaravan();
                    CaravanState = EventState.Cooldown;
                    return;
                }
                if (CaravanState != EventState.Active)
                {
                    CleanupCaravan();
                    CaravanState = EventState.Active;
                    caravanRoot = new GameObject("WorldEvent_SupplyCaravan_ClientProxy");
                    caravanCart = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    caravanCart.name = "SupplyWagon";
                    caravanCart.transform.SetParent(caravanRoot.transform, false);
                    caravanCart.transform.localScale = new Vector3(1.8f, 1.2f, 2.8f);
                    caravanCart.GetComponent<Renderer>().sharedMaterial = SpellFX.CreateUnlitMat(new Color(0.45f, 0.3f, 0.16f));
                    ZoneBanner.Show("WORLD EVENT: SUPPLY CARAVAN", "Escort the Emberwatch wagon safely toward Westbank!");
                }
                if (caravanCart) caravanCart.transform.position = pos;
            }
            else if (eventId == "roadside_ambush_greenhollow")
            {
                if (!active)
                {
                    CleanupAmbush();
                    AmbushState = EventState.Cooldown;
                    return;
                }
                if (AmbushState != EventState.Active)
                {
                    CleanupAmbush();
                    AmbushState = EventState.Active;
                    ambushRoot = new GameObject("WorldEvent_RoadsideAmbush_ClientProxy");
                    var scoutGo = new GameObject("PinnedScout");
                    scoutGo.transform.SetParent(ambushRoot.transform, false);
                    scoutGo.transform.position = pos;
                    CharacterVisualFactory.CreateVisualInstance("Human", "Rogue", scoutGo.transform);
                    ZoneBanner.Show("WORLD EVENT: ROADSIDE AMBUSH", "Defeat the raiders attacking the Frontier Scout!");
                }
            }
        }

        public void TriggerCaravanEvent()
        {
            if (CaravanState == EventState.Active) return;
            CleanupCaravan();
            CaravanState = EventState.Active;
            caravanAmbushSpawned = false;
            caravanProgress = 0f;
            caravanLeaveTimer = 0f;

            caravanRoot = new GameObject("WorldEvent_SupplyCaravan");
            caravanCart = GameObject.CreatePrimitive(PrimitiveType.Cube);
            caravanCart.name = "SupplyWagon";
            caravanCart.transform.SetParent(caravanRoot.transform, false);
            caravanCart.transform.position = caravanRoute[0] + Vector3.up * 0.7f;
            caravanCart.transform.localScale = new Vector3(1.8f, 1.2f, 2.8f);
            caravanCart.GetComponent<Renderer>().sharedMaterial = SpellFX.CreateUnlitMat(new Color(0.45f, 0.3f, 0.16f));

            caravanStats = caravanCart.AddComponent<Combatant>();
            caravanStats.displayName = "Emberwatch Supply Caravan";
            caravanStats.ConfigureVitals(220f, 1f);
            caravanStats.Restore();

            Network.RiftboundCoopPlayer.BroadcastWorldEventState("supply_caravan_westbank", true, caravanCart.transform.position);
            ZoneBanner.Show("WORLD EVENT: SUPPLY CARAVAN", "Escort the Emberwatch wagon safely toward Westbank!");
            ChatSystem.LogSystem("World Event started: Escort the Supply Caravan!");
        }

        void UpdateCaravanEvent()
        {
            if (CaravanState == EventState.Cooldown && Time.time >= caravanCooldownUntil)
                CaravanState = EventState.Idle;

            if (CaravanState == EventState.Idle)
            {
                if (DistanceToAnyActivePlayer(caravanRoute[0]) < 32f)
                    TriggerCaravanEvent();
                return;
            }

            if (CaravanState != EventState.Active) return;

            if (!caravanCart || !caravanStats || !caravanStats.Alive)
            {
                ChatSystem.LogSystem("The Supply Caravan was destroyed!");
                EndCaravan(false);
                return;
            }

            if (DistanceToAnyActivePlayer(caravanCart.transform.position) > 90f)
            {
                caravanLeaveTimer += Time.deltaTime;
                if (caravanLeaveTimer > 15f) { EndCaravan(false); return; }
            }
            else caravanLeaveTimer = 0f;

            if (!caravanAmbushSpawned && caravanProgress >= 0.42f)
            {
                caravanAmbushSpawned = true;
                SpawnEventRaider(caravanRoot.transform, caravanCart.transform.position + new Vector3(-6f, 0f, 4f), "caravan-raider-1", "Highway Raider", caravanRaiders);
                SpawnEventRaider(caravanRoot.transform, caravanCart.transform.position + new Vector3(-4f, 0f, -5f), "caravan-raider-2", "Roadside Cutthroat", caravanRaiders);
                CombatTextManager.Spawn(caravanCart.transform.position + Vector3.up * 2.4f, "AMBUSH!", new Color(1f, 0.35f, 0.15f), true);
            }

            bool raidersAlive = false;
            foreach (var r in caravanRaiders)
                if (r && r.GetComponent<Combatant>()?.Alive == true) { raidersAlive = true; break; }

            if (!raidersAlive)
            {
                caravanProgress += Time.deltaTime * 0.032f;
                Vector3 pos = SamplePolyline(caravanRoute, Mathf.Clamp01(caravanProgress));
                Vector3 look = SamplePolyline(caravanRoute, Mathf.Clamp01(caravanProgress + 0.02f)) - pos;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f) caravanCart.transform.rotation = Quaternion.LookRotation(look);
                caravanCart.transform.position = pos + Vector3.up * 0.7f;
                if (Time.time >= nextCaravanBroadcastAt)
                {
                    nextCaravanBroadcastAt = Time.time + 0.45f;
                    Network.RiftboundCoopPlayer.BroadcastWorldEventState("supply_caravan_westbank", true, caravanCart.transform.position);
                }
            }

            if (caravanProgress >= 1f)
            {
                if (Network.CoopSessionConfig.IsMultiplayer && NetworkManager.Singleton && NetworkManager.Singleton.IsServer)
                {
                    Network.RiftboundCoopPlayer.BroadcastWorldEventReward("supply_caravan_westbank", caravanCart.transform.position, 60, 35);
                    SpellFX.CreateLevelUpBurst(caravanCart.transform.position);
                    EndCaravan(true);
                    return;
                }
                bool firstTime = completedEventIds.Add("supply_caravan_westbank");
                int xp = firstTime ? 60 : 30;
                int gold = firstTime ? 35 : 18;
                player.AddExperience(xp);
                player.AddGold(gold);
                SpellFX.CreateLevelUpBurst(caravanCart.transform.position);
                ZoneBanner.Show("CARAVAN ESCORTED!", firstTime ? $"First-Clear Bonus: +{xp} XP  +{gold} Gold" : $"Repeatable Bounty: +{xp} XP  +{gold} Gold");
                ChatSystem.LogSystem($"Supply Caravan reached Westbank! Earned {xp} XP and {gold} Gold.");
                EndCaravan(true);
            }
        }

        void EndCaravan(bool success)
        {
            CleanupCaravan();
            CaravanState = EventState.Cooldown;
            caravanCooldownUntil = Time.time + 120f;
            Network.RiftboundCoopPlayer.BroadcastWorldEventState("supply_caravan_westbank", false, Vector3.zero);
        }

        void CleanupCaravan()
        {
            caravanRaiders.Clear();
            if (caravanRoot) Destroy(caravanRoot);
            caravanRoot = null;
            caravanCart = null;
            caravanStats = null;
        }

        public void TriggerAmbushEvent()
        {
            if (AmbushState == EventState.Active) return;
            CleanupAmbush();
            AmbushState = EventState.Active;
            ambushLeaveTimer = 0f;

            Vector3 site = GroundPos(AmbushSite);
            ambushRoot = new GameObject("WorldEvent_RoadsideAmbush");

            var scoutGo = new GameObject("PinnedScout");
            scoutGo.transform.SetParent(ambushRoot.transform, false);
            scoutGo.transform.position = site;
            CharacterVisualFactory.CreateVisualInstance("Human", "Rogue", scoutGo.transform);
            ambushScout = scoutGo.AddComponent<Combatant>();
            ambushScout.displayName = "Frontier Scout";
            ambushScout.ConfigureVitals(180f, 1f);
            ambushScout.Restore();

            SpawnEventRaider(ambushRoot.transform, site + new Vector3(4f, 0f, 3f), "ambush-raider-1", "Redridge Ambusher", ambushRaiders);
            SpawnEventRaider(ambushRoot.transform, site + new Vector3(-4f, 0f, 3f), "ambush-raider-2", "Redridge Stalker", ambushRaiders);

            Network.RiftboundCoopPlayer.BroadcastWorldEventState("roadside_ambush_greenhollow", true, site);
            CombatTextManager.Spawn(site + Vector3.up * 2.4f, "HELP! AMBUSH!", new Color(1f, 0.75f, 0.2f), true);
            ZoneBanner.Show("WORLD EVENT: ROADSIDE AMBUSH", "Defeat the raiders attacking the Frontier Scout!");
        }

        void UpdateAmbushEvent()
        {
            if (AmbushState == EventState.Cooldown && Time.time >= ambushCooldownUntil)
                AmbushState = EventState.Idle;

            if (AmbushState == EventState.Idle)
            {
                if (DistanceToAnyActivePlayer(GroundPos(AmbushSite)) < 36f)
                    TriggerAmbushEvent();
                return;
            }

            if (AmbushState != EventState.Active) return;

            if (DistanceToAnyActivePlayer(GroundPos(AmbushSite)) > 85f)
            {
                ambushLeaveTimer += Time.deltaTime;
                if (ambushLeaveTimer > 15f) { EndAmbush(); return; }
            }
            else ambushLeaveTimer = 0f;

            bool anyAlive = false;
            foreach (var r in ambushRaiders)
                if (r && r.GetComponent<Combatant>()?.Alive == true) { anyAlive = true; break; }

            if (!anyAlive)
            {
                if (Network.CoopSessionConfig.IsMultiplayer && NetworkManager.Singleton && NetworkManager.Singleton.IsServer)
                {
                    Network.RiftboundCoopPlayer.BroadcastWorldEventReward("roadside_ambush_greenhollow", GroundPos(AmbushSite), 55, 30);
                    SpellFX.CreateConsecration(GroundPos(AmbushSite), 3f);
                    EndAmbush();
                    return;
                }
                bool firstTime = completedEventIds.Add("roadside_ambush_greenhollow");
                int xp = firstTime ? 55 : 28;
                int gold = firstTime ? 30 : 15;
                player.AddExperience(xp);
                player.AddGold(gold);
                SpellFX.CreateConsecration(GroundPos(AmbushSite), 3f);
                ZoneBanner.Show("AMBUSH REPELLED!", firstTime ? $"First-Clear Bonus: +{xp} XP  +{gold} Gold" : $"Repeatable Bounty: +{xp} XP  +{gold} Gold");
                ChatSystem.LogSystem($"Roadside Ambush repelled! Earned {xp} XP and {gold} Gold.");
                EndAmbush();
            }
        }

        void EndAmbush()
        {
            CleanupAmbush();
            AmbushState = EventState.Cooldown;
            ambushCooldownUntil = Time.time + 120f;
            Network.RiftboundCoopPlayer.BroadcastWorldEventState("roadside_ambush_greenhollow", false, Vector3.zero);
        }

        void CleanupAmbush()
        {
            ambushRaiders.Clear();
            if (ambushRoot) Destroy(ambushRoot);
            ambushRoot = null;
            ambushScout = null;
        }

        void SpawnEventRaider(Transform parent, Vector3 xzPos, string id, string displayName, List<EnemyController> list)
        {
            var go = new GameObject(displayName);
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            go.transform.position = GroundPos(xzPos);
            var vis = CharacterVisualFactory.CreateVisualInstance("Orc", "Warrior", go.transform);
            var ec = go.AddComponent<EnemyController>();
            ec.enemyId = id;
            ec.player = player;
            ec.animator = vis ? vis.GetComponentInChildren<Animator>() : null;
            ec.attackDamage = 8f;
            ec.nativeDeathAnimation = true;
            var cc = go.GetComponent<CharacterController>();
            cc.height = 1.9f; cc.radius = 0.42f; cc.center = Vector3.up * 0.95f;
            var stats = go.GetComponent<Combatant>();
            stats.displayName = displayName;
            stats.ConfigureVitals(85f, 0f);
            stats.Restore();
            go.SetActive(true);
            list.Add(ec);
        }

        static Vector3 SamplePolyline(Vector3[] pts, float t)
        {
            if (pts == null || pts.Length == 0) return Vector3.zero;
            if (pts.Length == 1) return pts[0];
            float scaled = t * (pts.Length - 1);
            int idx = Mathf.Min(pts.Length - 2, Mathf.FloorToInt(scaled));
            float frac = scaled - idx;
            Vector3 p = Vector3.Lerp(pts[idx], pts[idx + 1], frac);
            p.y = WorldRegions.GroundHeight(p);
            return p;
        }
    }
}
