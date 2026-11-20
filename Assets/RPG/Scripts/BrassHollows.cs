using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public sealed class BrassHollows : MonoBehaviour
    {
        public const string QuestTitle = "Cut the Siphon";
        public const string Engineer = "Tinker Fizzlebang";
        static readonly Vector3 EntranceSite = new(135f, 0f, 183f);
        static readonly Vector3 InteriorOrigin = new(642f, 36f, 163f);
        static readonly string[] EnemyIds = { "hollows-sentry", "hollows-lookout", "hollows-bruiser", "hollows-channeler", "hollows-veyl" };
        static readonly string[] EnemyNames = { "Choir sentry", "Choir lookout", "Choir bruiser", "Choir channeler", "Foreman Veyl" };
        static readonly Vector3[] EnemySpawns = {
            new(-1.5f, 0f, 18.0f), new(1.5f, 0f, 20.5f), new(3.6f, 0f, 27.5f), new(-3.2f, 0f, 32.5f), new(0f, 0f, 50.5f)
        };

        sealed class SteamVent
        {
            public Transform root;
            public Renderer plate;
            public LineRenderer ring;
            public Vector3 worldPos;
            public float radius;
            public float damage;
            public float idleDuration;
            public float warningDuration;
            public float recoveryDuration;
            public float startDelay;
            public bool requireVeylAlive;
            public bool warningShown;
            public int lastBurstCycle = -1;
            public string warningText;

            public float CycleLength => idleDuration + warningDuration + recoveryDuration;
        }

        static BrassHollows current;
        [SerializeField] List<Material> materials = new();
        readonly List<SteamVent> vents = new();
        Material shale, slate, oak, bronze, amber, cooled, danger, ringMat;
        Transform chamber, pump;
        Renderer pumpCore, ventCore;
        Light pumpLight;
        Mesh rockMesh;
        Transform serviceDoor;
        PlayerController entrant;
        float encounterTime, bellChamberTime, veylRecoveryUntil;
        int lastSyncedStage = -1;
        bool wasInside, taughtSteam, veylWasWarning, notifiedPumpReady;
        public static BrassHollows Current => current;
        [field: SerializeField] public Vector3 Entrance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => current = null;

        void OnEnable()
        {
            if (Application.isPlaying && transform.Find("Mine mouth"))
                current = this;
        }
        public static bool Inside(Vector3 point) =>
            point.x > InteriorOrigin.x - 13f && point.x < InteriorOrigin.x + 13f &&
            point.z > InteriorOrigin.z - 3f && point.z < InteriorOrigin.z + 61f &&
            point.y > InteriorOrigin.y - 2f && point.y < InteriorOrigin.y + 10f;

        internal Vector3 NavigationExit(PlayerController player) =>
            InteriorOrigin + (player.BrassHollowsStage >= 2 ? new Vector3(3f, 0f, 57.5f) : new Vector3(0f, 0f, 1.1f));

        public static void Ensure()
        {
            if (current || !WorldRegions.Expanded || Terrain.activeTerrains.Length == 0) return;
            current = new GameObject("The Brass Hollows").AddComponent<BrassHollows>();
            current.BuildEntrance();
        }

#if UNITY_EDITOR
        public static GameObject CreateScenePreview()
        {
            if (!WorldRegions.Expanded || Terrain.activeTerrains.Length == 0) return null;
            var preview = new GameObject("Frontier preview — Brass Hollows") { hideFlags = HideFlags.DontSave };
            preview.AddComponent<BrassHollows>().BuildEntrance();
            return preview;
        }
#endif

        void BuildEntrance()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (!lit) lit = Shader.Find("Standard");
            shale = Paint(lit, "Charcoal shale", new Color(.16f, .13f, .11f));
            slate = Paint(lit, "Copper-streaked slate", new Color(.27f, .21f, .17f));
            oak = Paint(lit, "Mine timber", new Color(.33f, .17f, .087f));
            bronze = Paint(lit, "Weathered brass", new Color(.61f, .36f, .13f));
            amber = Paint(lit, "Lantern amber", new Color(.95f, .58f, .16f), new Color(1f, .44f, .055f));
            cooled = Paint(lit, "Cold pump glass", new Color(.13f, .30f, .29f));
            danger = Paint(lit, "Steam warning", new Color(.8f, .20f, .055f), new Color(1f, .13f, .015f));
            var kit = OriginalLocationProps.Current;
            if (kit)
            {
                shale.SetTexture("_BaseMap", kit.stoneTexture);
                slate.SetTexture("_BaseMap", kit.stoneTexture);
                oak.SetTexture("_BaseMap", kit.woodTexture);
            }
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (!unlit) unlit = Shader.Find("Unlit/Color");
            ringMat = new Material(unlit) { name = "Steam warning ring" };
            ringMat.SetColor("_BaseColor", new Color(1f, .28f, .05f));
            ringMat.color = new Color(1f, .28f, .05f);
            materials.Add(ringMat);

            Entrance = Surface(EntranceSite);
            var mouth = new GameObject("Mine mouth").transform;
            mouth.SetParent(transform);
            mouth.SetPositionAndRotation(Entrance + Vector3.forward * 1.5f, Quaternion.Euler(0, -14f, 0));
            Part(mouth, "Dark shaft", new Vector3(0, 1.5f, 1.1f), new Vector3(3.2f, 3.3f, .65f), shale);
            PortalFrame(mouth, 0f, 1.72f, 3.0f);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int tier = 0; tier < 3; tier++)
                {
                    var rock = Rock(mouth, "Fractured rock face", new Vector3(side * (2.5f - tier * .34f), .4f + tier * .83f, .54f),
                        new Vector3(1.95f, 1.36f, 1.79f), tier % 2 == 0 ? slate : shale);
                    rock.localRotation = Quaternion.Euler(side * (12 + tier * 8), tier * 19, side * (18 - tier * 7));
                }
            }
            Rock(mouth, "Overhanging shale", new Vector3(0, 3.36f, .43f), new Vector3(4.15f, .80f, 1.72f), slate)
                .localRotation = Quaternion.Euler(5, 7, -7);
            Lantern(mouth, new Vector3(1.4f, 2.3f, -.42f));
            Vector3 marker = Surface(Vector3.Lerp(new Vector3(134, 0, 157), Entrance, .48f));
            Part(transform, "Mine waymark", marker + Vector3.up * .83f, new Vector3(.15f,1.66f,.15f), oak);
            Part(transform, "Brass trail flag", marker + new Vector3(.23f,1.30f,0), new Vector3(.42f,.18f,.06f), bronze);
            var cart = new GameObject("Broken ore cart").transform;
            cart.SetParent(transform);
            cart.SetPositionAndRotation(Surface(Entrance + new Vector3(-3.2f,0,-2.3f)), Quaternion.Euler(0,29,0));
            Part(cart, "Ore cart bed", new Vector3(0,.42f,0), new Vector3(1.12f,.15f,1.55f), oak);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(cart, "Cart trough", new Vector3(side*.53f,.64f,0), new Vector3(.15f,.43f,1.55f), oak);
                Part(cart, "Crushed wheel", new Vector3(side*.56f,.21f,-.47f), new Vector3(.19f,.40f,.43f), bronze);
            }
            for (int i = 0; i < 4; i++)
                Part(cart, "Spilled ore", new Vector3(-.35f+i*.22f,.59f,-.23f+i*.14f), new Vector3(.21f,.22f,.24f), slate)
                    .localRotation = Quaternion.Euler(28*i, 21*i, 12*i);
        }

        void BuildInterior(PlayerController player)
        {
            entrant = player;
            if (chamber)
            {
                RefreshPump(player.BrassHollowsStage >= 2);
                lastSyncedStage = player.BrassHollowsStage;
                return;
            }
            chamber = new GameObject("Brass Hollows underground passage").transform;
            chamber.SetParent(transform);
            chamber.position = InteriorOrigin;

            Section("Ore Landing", 0f, 17f, 5.5f, 5.0f);
            Section("Lower drive", 17f, 22f, 2.4f, 3.6f);
            Section("Pump Gallery", 22f, 36f, 6.5f, 5.5f);
            Section("Siphon tunnel", 36f, 42f, 2.6f, 3.6f);
            Section("Bell Chamber", 42f, 59f, 8f, 6.4f);
            Part(chamber, "Entrance rock cap", new Vector3(0,2.5f,-.55f), new Vector3(12.2f,5.5f,1.1f), shale, true);
            Part(chamber, "Far rock cap", new Vector3(0,3.2f,59.55f), new Vector3(17.2f,7f,1.1f), shale, true);
            foreach (float z in new[] { 1f, 17f, 22f, 36f, 42f, 56f }) PortalFrame(chamber, z, 2.25f, 3.25f);
            Lantern(chamber, new Vector3(-2.15f,2.8f,3.2f));
            Lantern(chamber, new Vector3(4.2f,2.65f,12.1f));
            Lantern(chamber, new Vector3(-5.3f,2.8f,27.8f));
            Lantern(chamber, new Vector3(6.1f,3.15f,51f));
            for (int i = 0; i < 12; i++)
                Part(chamber, "Trestle sleeper", new Vector3(1.9f,.022f,2.9f+i*.8f), new Vector3(1.4f,.045f,.17f), oak);
            foreach (float x in new[] { 1.46f, 2.34f })
                Part(chamber, "Ore cart rail", new Vector3(x,.05f,7.3f), new Vector3(.045f,.055f,9.3f), bronze);

            vents.Clear();
            // Teach the steam tell safely in Ore Landing before any enemy pulls, combine with the Pump Gallery fight, then flank Veyl.
            AddSteamVent("Ore Landing steam vent", new Vector3(0f, .025f, 6.0f), 1.65f, 8f, 2.2f, 2.0f, 1.8f, 0f, false, "⚠ STEAM VENT!");
            ventCore = AddSteamVent("Pressure vent", new Vector3(0f, .025f, 29.5f), 1.65f, 16f, 2.2f, 2.0f, 1.8f, 0f, false, "⚠ STEAM SURGE!").plate;
            AddSteamVent("Bell Chamber west vent", new Vector3(-3.6f, .025f, 49.5f), 1.75f, 20f, 2.2f, 2.0f, 2.3f, 0f, true, "⚠ VALVE OVERPRESSURE!");
            AddSteamVent("Bell Chamber east vent", new Vector3(3.6f, .025f, 49.5f), 1.75f, 20f, 2.2f, 2.0f, 2.3f, 3.25f, true, "⚠ VALVE OVERPRESSURE!");

            pump = new GameObject("Choir siphon shutoff").transform;
            pump.SetParent(chamber, false);
            pump.localPosition = new Vector3(0,0,55.5f);
            Part(pump, "Stone foundation", new Vector3(0,.20f,0), new Vector3(3.25f,.4f,1.9f), slate, true);
            Part(pump, "Siphon housing", new Vector3(0,1.20f,.15f), new Vector3(1.4f,2f,1.1f), bronze, true);
            pumpCore = Part(pump, "Resonance glass", new Vector3(0,1.43f,-.43f), new Vector3(.57f,1.13f,.18f), amber).GetComponent<Renderer>();
            for (int side = -1; side <= 1; side += 2)
            {
                Part(pump, "Pressure return riser", new Vector3(side*1.22f,1.08f,.34f), new Vector3(.24f,2.15f,.24f), bronze);
                Part(pump, "Pressure return header", new Vector3(side*.63f,2.10f,.34f), new Vector3(1.29f,.24f,.24f), bronze);
            }
            pumpLight = Lantern(pump, new Vector3(0,3.7f,-.8f));
            pumpLight.range = 11f;
            serviceDoor = Part(chamber, "Service hatch", new Vector3(3f,1.32f,58.96f), new Vector3(1.5f,2.65f,.18f), oak);
            Part(chamber, "Service hatch lintel", new Vector3(3f,2.79f,58.81f), new Vector3(1.8f,.20f,.24f), bronze);
            Lantern(chamber, new Vector3(3.85f,2.68f,58.6f));

            for (int i = 0; i < EnemyIds.Length; i++)
            {
                if (player.HasDefeated(EnemyIds[i])) continue;
                RiverReaverCamp.CreateEnemy(chamber, player, EnemyIds[i], EnemyNames[i],
                    i == 3 ? "Undead" : "Human", i == 3 ? "Mage" : i <= 1 ? "Rogue" : "Warrior",
                    InteriorOrigin + EnemySpawns[i] + Vector3.up * .06f, i == 4, i == 3);
            }
            RefreshPump(player.BrassHollowsStage >= 2);
            lastSyncedStage = player.BrassHollowsStage;
        }

        SteamVent AddSteamVent(string label, Vector3 localPos, float radius, float damage, float idle, float warn, float recovery, float startDelay, bool requireVeylAlive, string warningText)
        {
            var plateTransform = Part(chamber, label, localPos, new Vector3(2.6f, .05f, 2.6f), bronze);
            var ringGo = new GameObject("Steam warning ring");
            ringGo.transform.SetParent(plateTransform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.sharedMaterial = ringMat;
            ring.positionCount = 48;
            ring.startWidth = ring.endWidth = .15f;
            Vector3 worldPos = InteriorOrigin + localPos;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48f;
                ring.SetPosition(i, worldPos + new Vector3(Mathf.Sin(angle) * radius, .08f, Mathf.Cos(angle) * radius));
            }
            ring.enabled = false;
            var vent = new SteamVent
            {
                root = plateTransform,
                plate = plateTransform.GetComponent<Renderer>(),
                ring = ring,
                worldPos = worldPos,
                radius = radius,
                damage = damage,
                idleDuration = idle,
                warningDuration = warn,
                recoveryDuration = recovery,
                startDelay = startDelay,
                requireVeylAlive = requireVeylAlive,
                warningText = warningText
            };
            vents.Add(vent);
            return vent;
        }

        void Section(string label, float from, float to, float halfWidth, float ceiling)
        {
            float center = (from + to) / 2f, length = to - from;
            Part(chamber, label + " floor", new Vector3(0,-.45f,center), new Vector3(halfWidth*2+1,.9f,length), slate, true);
            Part(chamber, label + " ceiling", new Vector3(0,ceiling+.42f,center), new Vector3(halfWidth*2+1,.84f,length), shale, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(chamber, label + " wall", new Vector3(side*(halfWidth+.36f),ceiling*.5f,center),
                    new Vector3(.84f,ceiling+.5f,length+.03f), shale, true);
                for (int i = 0; i < Mathf.FloorToInt(length / 3f); i++)
                {
                    var slab = Rock(chamber, "Banded rock", new Vector3(side*(halfWidth-.18f),.58f+(i%3)*.61f,from+1.65f+i*3f),
                        new Vector3(.68f,1.05f,2.3f), i%2 == 0 ? slate : shale);
                    slab.localRotation = Quaternion.Euler(side*12, side*(i%3)*13, side*15);
                }
            }
            if (halfWidth > 4f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float span = halfWidth - 2.2f;
                    foreach (float z in new[] { from, to })
                        Part(chamber, "Chamber shoulder", new Vector3(side*(2.2f+span*.5f),ceiling*.5f,z),
                            new Vector3(span,ceiling,.65f), slate, true);
                }
            }
        }

        void PortalFrame(Transform parent, float z, float halfWidth, float height)
        {
            var kit = OriginalLocationProps.Current;
            var shellHeight = height + .34f;
            if (kit && OriginalLocationProps.Place(kit.arch, parent, new Vector3(0, shellHeight * .5f, z),
                new Vector3(halfWidth * 2.66f, shellHeight, .58f), slate)) return;
            for (int side = -1; side <= 1; side += 2)
            {
                Part(parent, "Braced timber upright", new Vector3(side*halfWidth,height*.5f,z),
                    new Vector3(.28f,height,.34f), oak).localRotation = Quaternion.Euler(0,0,-side*6f);
                Part(parent, "Riveted binding", new Vector3(side*(halfWidth-.13f),height-.44f,z),
                    new Vector3(.32f,.22f,.38f), bronze);
            }
            Part(parent, "Cut timber lintel", new Vector3(0,height,z), new Vector3(halfWidth*2+.24f,.29f,.43f), oak);
        }

        Light Lantern(Transform parent, Vector3 position)
        {
            Part(parent, "Lantern cage", position, new Vector3(.29f,.43f,.29f), bronze);
            Part(parent, "Lantern glow", position + Vector3.forward * .03f, new Vector3(.20f,.28f,.30f), amber);
            var lamp = new GameObject("Lantern light").AddComponent<Light>();
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = position + new Vector3(0,-.22f,.3f);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f,.62f,.31f);
            lamp.range = 8f;
            lamp.intensity = 3.4f;
            return lamp;
        }

        static Vector3 Surface(Vector3 point)
        {
            point.y = WorldRegions.GroundHeight(point) + .03f;
            return point;
        }

        Material Paint(Shader lit, string name, Color color, Color emission = default)
        {
            var material = new Material(lit) { color = color, name = name };
            material.SetFloat("_Smoothness", name.Contains("brass") ? .35f : .12f);
            if (emission != default)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
            materials.Add(material);
            return material;
        }

        static Transform Part(Transform parent, string name, Vector3 point, Vector3 scale, Material color, bool solid = false)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = point;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = color;
            part.GetComponent<Collider>().enabled = solid;
            return part.transform;
        }

        Transform Rock(Transform parent, string label, Vector3 point, Vector3 scale, Material material)
        {
            var kit = OriginalLocationProps.Current;
            var replacement = kit ? OriginalLocationProps.Place(kit.rock, parent, point, scale, material) : null;
            if (replacement)
            {
                replacement.name = label;
                return replacement;
            }
            if (!rockMesh)
            {
                Vector3[] corners = {
                    new(-.49f,-.22f,-.32f), new(-.39f,.25f,-.40f), new(.28f,.46f,-.27f),
                    new(.48f,.08f,-.34f), new(.38f,-.44f,-.30f),
                    new(-.39f,-.40f,.38f), new(-.48f,.15f,.41f), new(.25f,.36f,.46f),
                    new(.44f,.10f,.39f), new(.24f,-.35f,.46f)
                };
                int[] faces = { 0,1,2, 0,2,4, 2,3,4, 5,7,6, 5,9,7, 7,9,8,
                    0,6,1, 0,5,6, 1,6,7, 1,7,2, 2,7,8, 2,8,3, 3,8,9, 3,9,4, 4,9,5, 4,5,0 };
                var vertices = new Vector3[faces.Length];
                var indices = new int[faces.Length];
                for (int i = 0; i < faces.Length; i += 3)
                {
                    Vector3 a = corners[faces[i]], b = corners[faces[i+1]], c = corners[faces[i+2]];
                    if (Vector3.Dot(Vector3.Cross(b-a, c-a), a+b+c) < 0f) (b, c) = (c, b);
                    vertices[i] = a; vertices[i+1] = b; vertices[i+2] = c;
                    indices[i] = i; indices[i+1] = i+1; indices[i+2] = i+2;
                }
                rockMesh = new Mesh { name = "Brass Hollows faceted shale", vertices = vertices, triangles = indices };
                rockMesh.RecalculateNormals();
            }
            var rock = new GameObject(label);
            rock.transform.SetParent(parent, false);
            rock.transform.localPosition = point;
            rock.transform.localScale = scale;
            rock.AddComponent<MeshFilter>().sharedMesh = rockMesh;
            rock.AddComponent<MeshRenderer>().sharedMaterial = material;
            return rock.transform;
        }

        public bool IsVeylRecovering => Time.time < veylRecoveryUntil;

        public int ActiveVentWarningCount
        {
            get
            {
                int count = 0;
                foreach (var vent in vents) if (vent.ring && vent.ring.enabled) count++;
                return count;
            }
        }

        void Update()
        {
            if (!chamber || FrontendMenu.IsOpen) return;
            var player = entrant ? entrant : FindFirstObjectByType<PlayerController>();
            if (!player) return;

            if (player.BrassHollowsStage != lastSyncedStage)
            {
                lastSyncedStage = player.BrassHollowsStage;
                RefreshPump(player.BrassHollowsStage >= 2);
                ResetEncounterState(resetEnemies: false);
            }

            bool inside = Inside(player.transform.position);
            if (!inside || !player.Stats || !player.Stats.Alive || player.BrassHollowsStage != 1)
            {
                if (wasInside || encounterTime > 0f || bellChamberTime > 0f || veylWasWarning)
                    ResetEncounterState(resetEnemies: false);
                wasInside = false;
                return;
            }

            if (!wasInside)
            {
                wasInside = true;
                ResetEncounterState(resetEnemies: false);
            }

            float dt = Mathf.Min(Time.deltaTime, 0.2f);
            encounterTime += dt;

            var veyl = EnemyController.FindByKey(EnemyIds[4]);
            var veylStats = veyl ? veyl.GetComponent<Combatant>() : null;
            bool veylAlive = veyl && veyl.gameObject.activeInHierarchy && veylStats && veylStats.Alive && !player.HasDefeated(EnemyIds[4]);
            float localZ = player.transform.position.z - InteriorOrigin.z;

            if (veylAlive && localZ >= 38f && !veyl.IsEvading)
                bellChamberTime += dt;
            else if (!veylAlive || localZ < 36f || veyl.IsEvading)
                bellChamberTime = 0f;

            if (veylAlive)
            {
                if (veyl.IsWarningActive && !veylWasWarning)
                {
                    veylWasWarning = true;
                    veylRecoveryUntil = 0f;
                    player.Message = "Foreman Veyl winds up Crushing Blow — step clear of his ring!";
                }
                else if (!veyl.IsWarningActive && veylWasWarning)
                {
                    veylWasWarning = false;
                    if (!veyl.IsEvading)
                    {
                        veylRecoveryUntil = Time.time + 1.8f;
                        CombatTextManager.Spawn(veyl.transform.position + Vector3.up * 2.4f, "RECOVERY WINDOW!", new Color(.35f, .95f, .55f), true);
                        player.Message = "Veyl is off-balance after his slam — strike now!";
                    }
                }
            }
            else
            {
                veylWasWarning = false;
                veylRecoveryUntil = 0f;
            }

            if (!notifiedPumpReady && DefeatedCount(player) >= EnemyIds.Length)
            {
                notifiedPumpReady = true;
                player.Message = "All Choir defeated! Shut down the siphon at the far end of the Bell Chamber.";
            }

            foreach (var vent in vents)
            {
                if (!vent.plate || !vent.ring) continue;
                bool ventEnabled = !vent.requireVeylAlive || (veylAlive && bellChamberTime > 0f);
                if (!ventEnabled)
                {
                    vent.plate.sharedMaterial = cooled;
                    vent.ring.enabled = false;
                    vent.warningShown = false;
                    vent.lastBurstCycle = -1;
                    continue;
                }

                float timer = (vent.requireVeylAlive ? bellChamberTime : encounterTime) - vent.startDelay;
                if (timer < 0f)
                {
                    vent.plate.sharedMaterial = bronze;
                    vent.ring.enabled = false;
                    vent.warningShown = false;
                    continue;
                }

                float cycleLen = vent.CycleLength;
                int cycleIdx = Mathf.FloorToInt(timer / cycleLen);
                float phase = timer - cycleIdx * cycleLen;

                if (phase < vent.idleDuration)
                {
                    vent.plate.sharedMaterial = bronze;
                    vent.ring.enabled = false;
                    vent.warningShown = false;
                }
                else if (phase < vent.idleDuration + vent.warningDuration)
                {
                    vent.plate.sharedMaterial = danger;
                    vent.ring.enabled = true;
                    float pulse = .13f + .05f * Mathf.Sin(Time.time * 12f);
                    vent.ring.startWidth = vent.ring.endWidth = pulse;
                    if (!vent.warningShown)
                    {
                        vent.warningShown = true;
                        if (Vector3.Distance(player.transform.position, vent.worldPos) <= 16f)
                        {
                            GameSound.Play(SoundCue.Rune, vent.worldPos);
                            CombatTextManager.Spawn(vent.worldPos + Vector3.up * .45f, vent.warningText, new Color(1f, .55f, .12f), true);
                            if (!taughtSteam && !vent.requireVeylAlive)
                            {
                                taughtSteam = true;
                                player.Message = "Steam vent charging — step clear of the red ring before it erupts!";
                            }
                        }
                    }
                }
                else
                {
                    vent.plate.sharedMaterial = cooled;
                    vent.ring.enabled = false;
                    vent.warningShown = false;
                    if (cycleIdx != vent.lastBurstCycle)
                    {
                        vent.lastBurstCycle = cycleIdx;
                        TriggerSteamBurst(player, vent);
                    }
                }
            }
        }

        void TriggerSteamBurst(PlayerController player, SteamVent vent)
        {
            SpellFX.CreateExpandingRing(vent.worldPos, .35f, vent.radius, .35f, new Color(.95f, .72f, .42f, .9f), .2f);
            SpellFX.SpawnBurst(vent.worldPos + Vector3.up * .35f, new Color(.92f, .92f, .94f), new Color(.96f, .58f, .22f),
                24, 2.2f, 5.2f, .16f, .34f, .55f);
            if (Vector3.Distance(player.transform.position, vent.worldPos) <= 18f)
                GameSound.Play(SoundCue.Hit, vent.worldPos);
            Vector2 planar = new Vector2(player.transform.position.x - vent.worldPos.x, player.transform.position.z - vent.worldPos.z);
            float dy = Mathf.Abs(player.transform.position.y - vent.worldPos.y);
            if (player.Stats && player.Stats.Alive && planar.magnitude <= vent.radius && dy < 2.2f)
            {
                player.Stats.Damage(vent.damage);
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.8f, "SCALDED!", new Color(1f, .35f, .15f), true);
            }
        }

        void ResetEncounterState(bool resetEnemies)
        {
            encounterTime = 0f;
            bellChamberTime = 0f;
            veylWasWarning = false;
            veylRecoveryUntil = 0f;
            notifiedPumpReady = false;
            bool stopped = entrant && entrant.BrassHollowsStage >= 2;
            foreach (var vent in vents)
            {
                if (vent.plate) vent.plate.sharedMaterial = stopped ? cooled : bronze;
                if (vent.ring) vent.ring.enabled = false;
                vent.warningShown = false;
                vent.lastBurstCycle = -1;
            }
            if (!resetEnemies || !entrant) return;
            foreach (string id in EnemyIds)
            {
                if (entrant.HasDefeated(id)) continue;
                var enemy = EnemyController.FindByKey(id);
                if (enemy && enemy.gameObject.activeInHierarchy)
                    enemy.ResetForLoad();
            }
        }

        public int DefeatedCount(PlayerController player)
        {
            int count = 0;
            foreach (string id in EnemyIds) if (player.HasDefeated(id)) count++;
            return count;
        }

        public bool TryDestination(PlayerController player, out Vector3 position, out string caption)
        {
            position = Entrance; caption = "Enter the Brass Hollows";
            if (player.BrassHollowsStage == 1 && chamber && Inside(player.transform.position))
            {
                for (int i = 0; i < EnemyIds.Length; i++)
                {
                    if (player.HasDefeated(EnemyIds[i])) continue;
                    var enemy = EnemyController.FindByKey(EnemyIds[i]);
                    position = enemy ? enemy.transform.position : InteriorOrigin + EnemySpawns[i];
                    caption = "Defeat " + EnemyNames[i];
                    return true;
                }
                if (pump)
                {
                    position = pump.position;
                    caption = "Shut down the siphon";
                }
            }
            else if (player.BrassHollowsStage == 2)
            {
                if (Inside(player.transform.position))
                {
                    position = InteriorOrigin + new Vector3(3f, 0, 57.5f);
                    caption = "Take the service exit";
                    return true;
                }
                foreach (var npc in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                {
                    if (npc.npcName != Engineer) continue;
                    position = npc.transform.position;
                    caption = "Return to Fizzlebang";
                    return true;
                }
            }
            return player.BrassHollowsStage is 1 or 2;
        }

        public bool TryInteraction(PlayerController player, out PlayerController.InteractionCandidate candidate)
        {
            candidate = default;
            if (!player.CanUseBrassHollowsQuest) return false;
            Vector3 point = player.transform.position;
            if (!Inside(point))
            {
                if ((point - Entrance).sqrMagnitude > 3.1f * 3.1f) return false;
                return Near(point, Entrance, "Enter the Brass Hollows", () =>
                {
                    if (player.BrassHollowsStage == 0) { player.Message = "Speak to Tinker Fizzlebang in Rustwater about the sealed mine."; return; }
                    BuildInterior(player);
                    ResetEncounterState(resetEnemies: true);
                    if (player.TravelBrassHollows(InteriorOrigin + new Vector3(0,.12f,1.5f), Quaternion.identity))
                        ZoneBanner.Show("The Brass Hollows", "Ore Landing");
                }, out candidate);
            }

            if (Near(point, InteriorOrigin + new Vector3(0,0,1.1f), "Leave the Brass Hollows",
                () => Leave(player, requireClear: player.BrassHollowsStage < 2 && DefeatedCount(player) < EnemyIds.Length), out candidate)) return true;
            if (player.BrassHollowsStage >= 2)
                return Near(point, InteriorOrigin + new Vector3(3f,0,57.5f), "Take the service exit",
                    () => Leave(player, requireClear: false), out candidate);
            if (!pump) return false;
            return Near(point, pump.position, "Shut down the siphon", () =>
            {
                if (DefeatedCount(player) < EnemyIds.Length) { player.Message = "Clear all five Choir enemies before touching the siphon."; return; }
                player.ShutDownBrassSiphon();
                RefreshPump(true);
                lastSyncedStage = player.BrassHollowsStage;
            }, out candidate);
        }

        void RefreshPump(bool stopped)
        {
            if (pumpCore) pumpCore.sharedMaterial = stopped ? cooled : amber;
            foreach (var vent in vents)
            {
                if (vent.plate) vent.plate.sharedMaterial = stopped ? cooled : bronze;
                if (vent.ring) vent.ring.enabled = false;
            }
            if (pumpLight) pumpLight.intensity = stopped ? 1f : 3.4f;
            if (serviceDoor) serviceDoor.localPosition = new Vector3(stopped ? 4.48f : 3f, 1.32f, 58.96f);
        }

        void Leave(PlayerController player, bool requireClear = true)
        {
            if (player.TravelBrassHollows(Surface(Entrance - Vector3.forward * 2f) + Vector3.up * .15f,
                Quaternion.Euler(0,180f,0), savePosition: true, requireClear: requireClear))
            {
                ResetEncounterState(resetEnemies: false);
                ZoneBanner.Show("Redridge Pass", "Brass Hollows approach");
            }
        }

        static bool Near(Vector3 point, Vector3 goal, string name, System.Action action, out PlayerController.InteractionCandidate candidate)
        {
            float distance = Vector3.Distance(point, goal);
            candidate = new PlayerController.InteractionCandidate { text = name, execute = action, distance = distance };
            return distance <= 3.1f;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            foreach (var material in materials)
            {
                if (!material) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            if (rockMesh)
            {
                if (Application.isPlaying) Destroy(rockMesh);
                else DestroyImmediate(rockMesh);
            }
        }
    }

    public partial class PlayerController
    {
        public int BrassHollowsStage { get; private set; }
        public bool CanUseBrassHollowsQuest => WorldRegions.Expanded && !Network.CoopSessionConfig.IsMultiplayer;
        public bool BrassHollowsQuestActive => CanUseBrassHollowsQuest && BrassHollowsStage is 1 or 2;

        public string BrassHollowsObjective => BrassHollowsStage switch
        {
            1 => $"Defeat the Choir in the Brass Hollows ({(BrassHollows.Current ? BrassHollows.Current.DefeatedCount(this) : 0)}/5), then shut down the siphon.",
            2 => "Take the service exit and return the regulator to Tinker Fizzlebang in Rustwater.",
            3 => "The siphon is silent and the regulator has been returned.",
            _ => "Speak to Tinker Fizzlebang at Rustwater Scrapworks about the missing regulator."
        };

        public void HandleBrassHollowsQuest()
        {
            if (!CanUseBrassHollowsQuest || !ActiveTownNPC || ActiveTownNPC.npcName != BrassHollows.Engineer) return;
            if (BrassHollowsStage == 0)
            {
                BrassHollowsStage = 1;
                BrassHollows.Ensure();
                MinimapRadar.Instance?.FollowBrassHollows();
                PostBrassQuest("Cut the Siphon accepted. Find the Brass Hollows off Redridge Pass.");
            }
            else if (BrassHollowsStage == 2)
            {
                BrassHollowsStage = 3;
                AddGold(55);
                AddExperience(240);
                PostBrassQuest("Cut the Siphon complete. Fizzlebang paid 55 gold for the regulator.");
            }
        }

        public void ShutDownBrassSiphon()
        {
            if (!BrassHollowsQuestActive || BrassHollowsStage != 1) return;
            BrassHollowsStage = 2;
            PostBrassQuest("The siphon falls silent. Take the service exit and bring the regulator to Fizzlebang.");
        }

        void PostBrassQuest(string message)
        {
            AudioEffects.PlayClick();
            ChatSystem.LogQuest(message);
            if (SaveAdventure(false)) Message = message;
        }

        public bool TravelBrassHollows(Vector3 destination, Quaternion facing, bool savePosition = true, bool requireClear = true)
        {
            if (requireClear && Stats && Time.time - Stats.LastHit < 3f) { Message = "Get clear before using the mine passage."; return false; }
            if (Mounted) { Message = "Dismount before using the mine passage."; return false; }
            if (!motor || !motor.enabled) return false;
            ClearCombatInputBuffer();
            SetTarget(null);
            vertical = 0f;
            dodgeEnds = 0f;
            moveVelocity = Vector3.zero;
            motor.enabled = false;
            transform.SetPositionAndRotation(destination, facing);
            motor.enabled = true;
            if (orbit) orbit.FaceTravel(facing.eulerAngles.y);
            if (savePosition) SaveAdventure(false);
            return true;
        }
    }
}
