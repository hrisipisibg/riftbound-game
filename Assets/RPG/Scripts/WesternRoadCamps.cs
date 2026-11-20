using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    // Original camp props are assembled from a small shared material palette. The world positions
    // are selected once per scene against the authored terrain, so the quest and atlas share them.
    public sealed class WesternRoadCamps : MonoBehaviour
    {
        public const string QuestTitle = "Two Seals, One Load";
        public const string Rowan = "Quartermaster Rowan";
        public const string Witness = "Ferrymaster Sera";
        const string Lookout = "western-waybill-lookout";
        const string Raider = "western-waybill-raider";
        static WesternRoadCamps current;
        static readonly Vector3 LaybySite = new(-94, 0, -25);
        static readonly Vector3 RestSite = new(-219, 0, 29);

        [field: SerializeField] public Transform Layby { get; private set; }
        [field: SerializeField] public Transform Rest { get; private set; }
        [field: SerializeField] public Transform Waybill { get; private set; }
        [field: SerializeField] public TownNPC Ferrymaster { get; private set; }
        [SerializeField] List<Material> materials = new();
        [SerializeField] List<Mesh> meshes = new();
        Material wood, endgrain, iron, brass, blueCloth, creamCloth, redCloth, parchment, rope, grain, glow, unlitGlass;
        Light restLamp;
        Renderer lampGlass;
        bool spawnedGuards;

        public static WesternRoadCamps Current => current;

        void OnEnable()
        {
            if (Application.isPlaying && Layby && Rest && Waybill && Ferrymaster)
                current = this;
        }

#if UNITY_EDITOR
        public static GameObject CreateScenePreview()
        {
            var terrains = Terrain.activeTerrains;
            if (!WorldRegions.Expanded || terrains.Length == 0) return null;
            var root = new GameObject("Frontier preview — western camps") { hideFlags = HideFlags.DontSave };
            root.AddComponent<WesternRoadCamps>().Build(terrains);
            return root;
        }
#endif

        public static void Ensure(PlayerController player)
        {
            if (!WorldRegions.Expanded || !player) return;
            if (!current)
            {
                var terrains = Terrain.activeTerrains;
                if (terrains.Length == 0) return;
                current = new GameObject("Western road camps").AddComponent<WesternRoadCamps>();
                current.Build(terrains);
            }
            current.Refresh(player);
        }

        void Refresh(PlayerController player)
        {
            Waybill.gameObject.SetActive(player.WesternRoadStage <= 1);
            restLamp.enabled = player.WesternRoadStage == 4;
            lampGlass.sharedMaterial = restLamp.enabled ? glow : unlitGlass;
            if (player.WesternRoadStage != 1 || spawnedGuards || Network.CoopSessionConfig.IsMultiplayer) return;

            spawnedGuards = true;
            if (!player.HasDefeated(Lookout))
                RiverReaverCamp.CreateEnemy(Layby, player, Lookout, "Split-Seal lookout", "Human", "Rogue",
                    Layby.position + Layby.right * 4.5f + Layby.forward * 5.5f, false);
            if (!player.HasDefeated(Raider))
                RiverReaverCamp.CreateEnemy(Layby, player, Raider, "Split-Seal raider", "Human", "Warrior",
                    Layby.position - Layby.right * 5f + Layby.forward * 8.5f, false);
        }

        public static bool WaybillSecured(PlayerController player) => player.HasDefeated(Lookout) && player.HasDefeated(Raider);

        public static Merchant FindRowan()
        {
            foreach (var merchant in FindObjectsByType<Merchant>(FindObjectsSortMode.None))
                if (merchant.npcName == Rowan) return merchant;
            return null;
        }

        void Build(Terrain[] terrains)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (!shader) shader = Shader.Find("Standard");
            wood = Palette(shader, "Weathered oak", new Color(.23f, .132f, .076f));
            endgrain = Palette(shader, "Cut oak", new Color(.43f, .27f, .14f));
            iron = Palette(shader, "Blackened iron", new Color(.16f, .19f, .21f), .65f);
            brass = Palette(shader, "Dull brass", new Color(.58f, .36f, .13f), .45f);
            blueCloth = Palette(shader, "Indigo sailcloth", new Color(.11f, .28f, .39f));
            creamCloth = Palette(shader, "Unbleached canvas", new Color(.67f, .49f, .31f));
            redCloth = Palette(shader, "Dyed red canvas", new Color(.50f, .19f, .13f));
            parchment = Palette(shader, "Waybill parchment", new Color(.81f, .65f, .38f));
            rope = Palette(shader, "Hemp rope", new Color(.48f, .35f, .21f));
            grain = Palette(shader, "Spilled grain", new Color(.67f, .48f, .23f));
            glow = Palette(shader, "Amber lantern glass", new Color(.93f, .52f, .13f), .08f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(.74f, .28f, .04f));
            unlitGlass = Palette(shader, "Smoked lantern glass", new Color(.19f, .14f, .09f));

            Layby = NewSite("Split-Seal Layby", LaybySite, 26f, terrains);
            Rest = NewSite("Tallykeepers’ Rest", RestSite, -22f, terrains);
            BuildLayby();
            BuildRest();
            MarkApproach(Layby, new Vector3(-98, 0, -44), blueCloth);
            MarkApproach(Rest, new Vector3(-234, 0, 29), redCloth);
        }

        Transform NewSite(string label, Vector3 desired, float yaw, Terrain[] terrains)
        {
            // A small search north/east of the roads favors locally even ground without grading the scene.
            Vector3 best = desired;
            float bestScore = float.PositiveInfinity;
            for (int x = -2; x <= 2; x++)
            for (int z = -2; z <= 2; z++)
            {
                Vector3 point = desired + new Vector3(x * 2, 0, z * 2);
                if (!WorldRegions.TerrainAt(point, terrains)) continue;
                float height = WorldRegions.GroundHeight(point, terrains);
                float grade = Mathf.Max(
                    Mathf.Abs(WorldRegions.GroundHeight(point + Vector3.right * 3f, terrains) - height),
                    Mathf.Abs(WorldRegions.GroundHeight(point - Vector3.right * 3f, terrains) - height),
                    Mathf.Abs(WorldRegions.GroundHeight(point + Vector3.forward * 3f, terrains) - height),
                    Mathf.Abs(WorldRegions.GroundHeight(point - Vector3.forward * 3f, terrains) - height));
                float score = grade + .15f * (Mathf.Abs(x) + Mathf.Abs(z));
                if (score >= bestScore) continue;
                best = new Vector3(point.x, height, point.z);
                bestScore = score;
            }
            var site = new GameObject(label).transform;
            site.SetParent(transform);
            site.SetPositionAndRotation(best, Quaternion.Euler(0, yaw, 0));
            return site;
        }

        void BuildLayby()
        {
            var wagon = Group(Layby, "Broken grain wagon", -1.1f, 0, 22f);
            // Low cargo bed with alternating planks, offset axle, and one collapsed wheel.
            for (int i = 0; i < 7; i++)
                Box(wagon, "Cargo bed slat", new Vector3(-.72f + i * .24f, .64f, 0),
                    new Vector3(.225f, .12f, 2.45f), i % 3 == 0 ? endgrain : wood);
            foreach (int side in new[] { -1, 1 })
            {
                Box(wagon, "Sideboard", new Vector3(side * .84f, .91f, -.07f), new Vector3(.14f, .50f, 2.35f), wood);
                for (int i = 0; i < 3; i++)
                    Box(wagon, "Sideboard strap", new Vector3(side * .92f, .91f, -.86f + .8f * i),
                        new Vector3(.035f, .51f, .075f), iron);
            }
            Box(wagon, "Axle", new Vector3(0, .49f, -.48f), new Vector3(2.15f, .11f, .11f), iron);
            Wheel(wagon, new Vector3(-1.03f, .49f, -.48f), Quaternion.Euler(0, 90, 0));
            Wheel(wagon, new Vector3(1.40f, .33f, -.77f), Quaternion.Euler(64, 24, -18));
            Beam(wagon, "Cracked drawbar", new Vector3(.42f, .61f, .9f), new Vector3(.33f, .24f, 2.42f), .11f, wood);
            Beam(wagon, "Drawbar splinter", new Vector3(-.40f, .61f, .9f), new Vector3(-.52f, .35f, 1.77f), .093f, endgrain);
            Canvas(wagon, "Torn indigo wagon cloth", new[] {
                new Vector3(-.84f,1.08f,-1.12f), new Vector3(-.52f,1.19f,-1.08f), new Vector3(.25f,1.03f,-.95f),
                new Vector3(-1.16f,.37f,-.15f), new Vector3(-.73f,.52f,.01f), new Vector3(-.33f,.72f,-.26f)
            }, blueCloth);

            var supplies = Group(Layby, "Spilled shipment", 2.25f, -.9f, -14f);
            Crate(supplies, new Vector3(0, .34f, 0), .68f);
            Crate(supplies, new Vector3(.8f, .25f, -.43f), .50f);
            PlaceCargoArt(supplies, "mdvl_food_grain_sack_01", new Vector3(-.64f, .22f, -.22f), .44f);
            for (int i = 0; i < 9; i++)
            {
                float angle = i * 2.4f, radius = .21f * Mathf.Sqrt(i + 1);
                Box(supplies, "Grain scatter", new Vector3(Mathf.Cos(angle) * radius, .066f, .65f + Mathf.Sin(angle) * radius),
                    new Vector3(.14f, .05f, .09f), grain);
            }
            Waybill = new GameObject("Wagon waybill with two seals").transform;
            Waybill.SetParent(supplies, false);
            Waybill.localPosition = new Vector3(0, .70f, 0);
            Box(Waybill, "Folded waybill", Vector3.zero, new Vector3(.38f, .025f, .27f), parchment);
            Box(Waybill, "Red seal", new Vector3(.10f,.022f,-.06f), new Vector3(.06f,.018f,.06f), redCloth);
            Box(Waybill, "Blue seal", new Vector3(.18f,.022f,-.06f), new Vector3(.06f,.018f,.06f), blueCloth);
            Sign(Layby, "SPLIT-SEAL LAYBY", -.3f, -3.65f, blueCloth);
        }

        void BuildRest()
        {
            var awning = Group(Rest, "Ferry ledger shelter", .65f, .8f, 0);
            foreach (int side in new[] { -1, 1 })
            {
                foreach (int end in new[] { -1, 1 })
                {
                    float height = end == -1 ? 2.55f : 2.15f;
                    Box(awning, "Oak roof post", new Vector3(1.43f * side, height * .5f - .32f, end * .95f),
                        new Vector3(.14f, height + .64f, .14f), wood);
                    Box(awning, "Post lashing", new Vector3(1.43f * side, height - .18f, end * .95f),
                        new Vector3(.18f, .13f, .18f), rope);
                }
                Beam(awning, "Roof spar", new Vector3(1.43f * side,2.55f,-1.12f),
                    new Vector3(1.43f * side,2.15f,1.14f), .10f, endgrain);
            }
            for (int i = 0; i < 6; i++)
            {
                float x0 = -1.64f + i * (.547f), x1 = x0 + .547f;
                Canvas(awning, "Sewn awning strip", new[] {
                    new Vector3(x0,2.58f,-1.19f), new Vector3((x0+x1)/2f,2.57f,-1.19f), new Vector3(x1,2.58f,-1.19f),
                    new Vector3(x0,2.16f,1.19f), new Vector3((x0+x1)/2f,2.10f,1.19f), new Vector3(x1,2.16f,1.19f),
                }, i % 3 == 1 ? redCloth : creamCloth);
            }

            var table = Group(Rest, "Ferry ledger table", .95f, 1.05f, 0);
            Box(table, "Oak tabletop", new Vector3(0,.83f,0), new Vector3(1.48f,.15f,.69f), endgrain, true);
            foreach (int side in new[] { -1, 1 })
                foreach (int end in new[] { -1, 1 })
                    Box(table, "Table leg", new Vector3(side * .56f,.38f,end*.23f), new Vector3(.11f,.78f,.11f), wood);
            Box(table, "Tally ledger", new Vector3(.12f,.924f,0), new Vector3(.38f,.055f,.32f), wood);
            Box(table, "Open page", new Vector3(.12f,.957f,0), new Vector3(.35f,.025f,.29f), parchment);
            for (int i = 0; i < 4; i++)
                Box(table, "Ledger mark", new Vector3(.12f,.974f,-.09f+i*.042f), new Vector3(.23f,.004f,.008f), iron);
            Crate(table, new Vector3(-.99f,.26f,.22f), .52f);

            var bench = Group(Rest, "Shelter bench", 1.0f, -.86f, 0f);
            Box(bench, "Split oak seat", new Vector3(0,.49f,0), new Vector3(1.52f,.12f,.36f), endgrain, true);
            Box(bench, "Bench runner", new Vector3(0,.34f,0), new Vector3(1.36f,.09f,.10f), wood);
            foreach (int side in new[] { -1, 1 })
                Box(bench, "Bench foot", new Vector3(side*.55f,.21f,0), new Vector3(.14f,.43f,.33f), wood);

            var lanternPost = Group(Rest, "Lantern and ferry flag", -1.7f, -2.25f, 0);
            Box(lanternPost, "Lantern post", new Vector3(0,1.25f,0), new Vector3(.15f,2.85f,.15f), wood);
            Beam(lanternPost, "Lantern bracket", new Vector3(0,2.44f,0), new Vector3(.66f,2.44f,0), .08f, iron);
            Beam(lanternPost, "Chain", new Vector3(.56f,2.44f,0), new Vector3(.56f,2.26f,0), .027f, iron);
            lampGlass = Box(lanternPost, "Lantern glass", new Vector3(.56f,2.09f,0),
                new Vector3(.19f,.27f,.19f), unlitGlass).GetComponent<Renderer>();
            foreach (int x in new[] { -1, 1 })
                foreach (int z in new[] { -1, 1 })
                    Box(lanternPost, "Lantern cage", new Vector3(.56f + x*.105f,2.09f,z*.105f), new Vector3(.025f,.31f,.025f), brass);
            Box(lanternPost, "Lantern cap", new Vector3(.56f,2.25f,0), new Vector3(.26f,.055f,.26f), brass);
            Box(lanternPost, "Lantern base", new Vector3(.56f,1.93f,0), new Vector3(.24f,.06f,.24f), iron);
            var light = new GameObject("Restored ferry lamp");
            light.transform.SetParent(lanternPost, false);
            light.transform.localPosition = new Vector3(.56f, 2.05f, 0);
            restLamp = light.AddComponent<Light>();
            restLamp.type = LightType.Point;
            restLamp.color = new Color(1f,.68f,.32f);
            restLamp.range = 6f;
            restLamp.intensity = 2f;
            Sign(Rest, "TALLYKEEPERS’ REST", -.85f, -3.15f, redCloth);
            if (!Application.isPlaying) return;
            var actor = Group(Rest, Witness, -1.7f, .68f, 155f);
            CharacterVisualFactory.CreateVisualInstance("Human", "Rogue", actor);
            var capsule = actor.gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 1.8f; capsule.radius = .35f; capsule.center = Vector3.up * .9f;
            Ferrymaster = actor.gameObject.AddComponent<TownNPC>();
            Ferrymaster.npcName = Witness;
            Ferrymaster.roleTag = "Westbank ferry worker";
            Ferrymaster.greeting = "A ferry tally says who went across. The road office can write whatever it likes about the wagons that arrived.";
        }

        void MarkApproach(Transform site, Vector3 road, Material cloth)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector3 point = Vector3.Lerp(road, site.position, i == 0 ? .28f : .52f);
                Vector3 offset = site.InverseTransformPoint(point);
                var marker = Group(site, "Roadward trail pennant", offset.x, offset.z, i * 24f);
                Box(marker, "Marker stake", new Vector3(0, .61f, 0), new Vector3(.10f, 1.36f, .10f), wood);
                Beam(marker, "Pennant crosspiece", new Vector3(-.09f, 1.24f, 0), new Vector3(.32f, 1.24f, 0), .041f, endgrain);
                Canvas(marker, "Folded route cloth", new[] {
                    new Vector3(0, 1.23f, 0), new Vector3(.14f, 1.23f, .015f), new Vector3(.30f, 1.23f, 0),
                    new Vector3(0, .83f, 0), new Vector3(.14f, .92f, .035f), new Vector3(.30f, .86f, 0)
                }, cloth);
            }
        }

        void Sign(Transform site, string label, float x, float z, Material color)
        {
            var sign = Group(site, label, x, z, 0f);
            Box(sign, "Signpost", new Vector3(0,.73f,0), new Vector3(.12f,1.65f,.12f), wood);
            Box(sign, "Sign plank", new Vector3(0,1.48f,0), new Vector3(2.15f,.34f,.12f), wood);
            Box(sign, "Route color", new Vector3(0,1.31f,-.075f), new Vector3(2.15f,.075f,.045f), color);
            var text = new GameObject("Painted lettering").AddComponent<TextMesh>();
            text.transform.SetParent(sign, false);
            text.transform.localPosition = new Vector3(0,1.48f,-.067f);
            text.text = label; text.anchor = TextAnchor.MiddleCenter; text.fontSize = 48; text.characterSize = .033f;
            text.color = new Color(.93f,.85f,.64f);
        }

        void Wheel(Transform wagon, Vector3 point, Quaternion rotation)
        {
            var wheel = new GameObject("Iron-rimmed wagon wheel").transform;
            wheel.SetParent(wagon, false);
            wheel.localPosition = point;
            wheel.localRotation = rotation;
            for (int i = 0; i < 12; i++)
            {
                Vector3 a = Quaternion.Euler(0, 0, i * 30f) * Vector3.up * .48f;
                Vector3 b = Quaternion.Euler(0, 0, (i + 1) * 30f) * Vector3.up * .48f;
                Beam(wheel, "Rim segment", a, b, .074f, iron);
                if (i % 2 == 0) Beam(wheel, "Cut spoke", Vector3.zero, a, .052f, endgrain);
            }
            Box(wheel, "Iron hub", Vector3.zero, new Vector3(.14f,.14f,.24f), brass);
        }

        void Crate(Transform parent, Vector3 position, float size)
        {
            var crate = Box(parent, "Supply crate", position, Vector3.one * size, wood, true);
            var kit = OriginalLocationProps.Current;
            var model = kit ? OriginalLocationProps.Place(kit.crate, parent, position, Vector3.one * size) : null;
            if (model) model.name = "Timber supply crate";
            if (model || PlaceCargoArt(parent, "mdvl_props_crate_01", position, size))
            {
                crate.GetComponent<Renderer>().enabled = false;
                return;
            }
            foreach (int side in new[] {-1,1})
                Box(parent, "Crate lid cleat", position + new Vector3(side * size * .29f,size * .50f,0),
                    new Vector3(size*.11f,.045f,size+.04f), endgrain);
        }

        static bool PlaceCargoArt(Transform parent, string modelName, Vector3 position, float size)
        {
            var model = Resources.Load<GameObject>("FrontierProps/" + modelName);
            var material = Resources.Load<Material>("FrontierProps/Medieval-mdvl_palette-");
            if (!model || !material) return false;

            var visual = Instantiate(model, Vector3.zero, Quaternion.identity);
            var renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Release(visual);
                return false;
            }

            // Fit art to the existing cargo footprint. The original boxes still handle collisions.
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
                renderer.sharedMaterial = material;
            }
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            float span = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (span < .001f)
            {
                Release(visual);
                return false;
            }

            float scale = size / span;
            visual.name = modelName == "mdvl_props_crate_01" ? "Timber supply crate" : "Grain sack";
            visual.transform.SetParent(parent, false);
            visual.transform.localScale *= scale;
            var center = bounds.center;
            center.y = bounds.min.y;
            visual.transform.localPosition = position - Vector3.up * (size * .5f) - center * scale;
            return true;
        }

        Material Palette(Shader shader, string label, Color color, float metallic = 0)
        {
            var material = new Material(shader) { name = label, color = color };
            material.SetFloat("_Smoothness", .28f);
            material.SetFloat("_Metallic", metallic);
            materials.Add(material);
            return material;
        }

        static Transform Group(Transform parent, string label, float x, float z, float yaw)
        {
            var group = new GameObject(label).transform;
            group.SetParent(parent, false);
            Vector3 point = parent.TransformPoint(new Vector3(x, 0, z));
            point.y = WorldRegions.GroundHeight(point) + .03f;
            group.position = point;
            group.localRotation = Quaternion.Euler(0, yaw, 0);
            return group;
        }

        static Transform Box(Transform parent, string label, Vector3 point, Vector3 size, Material material, bool collidable = false)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = label;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = point;
            box.transform.localScale = size;
            box.GetComponent<Renderer>().sharedMaterial = material;
            box.GetComponent<Collider>().enabled = collidable;
            return box.transform;
        }

        static void Beam(Transform parent, string label, Vector3 a, Vector3 b, float width, Material material)
        {
            var beam = Box(parent, label, (a+b)*.5f, new Vector3(width, Vector3.Distance(a,b)+.018f, width), material);
            beam.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
        }

        void Canvas(Transform parent, string label, Vector3[] vertices, Material material)
        {
            var doubleSided = new Vector3[vertices.Length * 2];
            vertices.CopyTo(doubleSided, 0);
            vertices.CopyTo(doubleSided, vertices.Length);
            var mesh = new Mesh { name = label, vertices = doubleSided };
            mesh.triangles = new[] { 0,3,1, 1,3,4, 1,4,2, 2,4,5, 7,9,6, 10,9,7, 8,10,7, 11,10,8 };
            mesh.RecalculateNormals();
            meshes.Add(mesh);
            var canvas = new GameObject(label);
            canvas.transform.SetParent(parent, false);
            canvas.AddComponent<MeshFilter>().sharedMesh = mesh;
            canvas.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void OnDestroy()
        {
            if (current == this) current = null;
            foreach (var material in materials) Release(material);
            foreach (var mesh in meshes) Release(mesh);
        }

        static void Release(Object asset)
        {
            if (!asset) return;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }
    }

    public partial class PlayerController
    {
        public int WesternRoadStage { get; private set; }
        public bool CanUseWesternRoadQuest => WorldRegions.Expanded && !Network.CoopSessionConfig.IsMultiplayer;
        public bool WesternRoadActive => CanUseWesternRoadQuest && WesternRoadStage is >= 1 and <= 3;
        public string WesternRoadObjective => WesternRoadStage switch
        {
            1 => "Defeat the two raiders at Split-Seal Layby, then take the wagon waybill.",
            2 => "Ask Ferrymaster Sera at Tallykeepers’ Rest for her crossing tally.",
            3 => "Bring the waybill and the ferry tally to Quartermaster Rowan.",
            4 => "Rowan has both accounts. The ferry lantern is lit again.",
            _ => QuestIndex >= 2 ? "Speak to Quartermaster Rowan about the missing grain shipment." : "Finish Stolen Supplies with Abe to unlock this route."
        };

        public bool TryGetWesternRoadDestination(out Vector3 target, out string label)
        {
            target = default; label = null;
            var camps = WesternRoadCamps.Current;
            if (!WesternRoadActive || !camps) return false;
            switch (WesternRoadStage)
            {
                case 1:
                    target = WesternRoadCamps.WaybillSecured(this) ? camps.Waybill.position : camps.Layby.position;
                    label = WesternRoadCamps.WaybillSecured(this) ? "Recover the wagon waybill" : "Clear Split-Seal Layby";
                    return true;
                case 2:
                    target = camps.Ferrymaster.transform.position;
                    label = "Speak to Ferrymaster Sera";
                    return true;
                case 3:
                    var rowan = WesternRoadCamps.FindRowan();
                    if (!rowan) return false;
                    target = rowan.transform.position;
                    label = "Return to Rowan";
                    return true;
                default:
                    return false;
            }
        }

        public void HandleWesternRoadAtRowan()
        {
            if (!CanUseWesternRoadQuest || !CanTrade() || ActiveMerchant.npcName != WesternRoadCamps.Rowan) return;
            if (WesternRoadStage == 0 && QuestIndex >= 2)
            {
                AdvanceWesternRoad(1, "Two Seals, One Load accepted. Follow the highway west to the broken wagon.");
                MinimapRadar.Instance?.FollowWesternRoad();
            }
            else if (WesternRoadStage == 3)
            {
                WesternRoadStage = 4;
                AddGold(35);
                AddExperience(120);
                AdvanceWesternRoad(4, "Two Seals, One Load complete. Rowan paid 35 gold. The ferry lantern is lit again.");
            }
        }

        public void ReadFerryTally()
        {
            if (!CanUseWesternRoadQuest || WesternRoadStage != 2 || !ActiveTownNPC ||
                ActiveTownNPC.npcName != WesternRoadCamps.Witness) return;
            AdvanceWesternRoad(3, "Sera gave you a copy of her tally. Bring both accounts back to Rowan.");
        }

        void RecoverWesternWaybill()
        {
            if (!CanUseWesternRoadQuest || WesternRoadStage != 1) return;
            if (!WesternRoadCamps.WaybillSecured(this))
            {
                Message = "Defeat the two Split-Seal raiders before taking the waybill.";
                return;
            }
            AdvanceWesternRoad(2, "The waybill carries two seals for one load. Ask Sera what crossed the river.");
        }

        void AdvanceWesternRoad(int stage, string message)
        {
            WesternRoadStage = stage;
            WesternRoadCamps.Ensure(this);
            MinimapRadar.Instance?.RefreshQuestNavigation();
            AudioEffects.PlayClick();
            ChatSystem.LogQuest(message);
            if (SaveAdventure(false)) Message = message;
        }
    }
}
