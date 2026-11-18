using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class CC0WorldRebuild
    {
        const string Source = "Assets/RPG/ThirdParty/CC0World";
        const string Art = "Assets/RPG/Terrain/CC0WorldArt";
        const string Evidence = ".code-check/cc0-world";
        const string RootLabel = "CC0 world rebuild";
        const string OldTowns = "Westbank crossing and new town buildings";
        static readonly Dictionary<string, GameObject> Models = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        static WorldTraversalVerification.Route[] routes;

        [MenuItem("Riftbound/World/Rebuild CC0 scenery")]
        public static void Run()
        {
            Directory.CreateDirectory(Evidence);
            try
            {
                var scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                if (GameObject.Find(RootLabel)) throw new InvalidOperationException("The CC0 scenery is already authored in the scene.");
                Directory.CreateDirectory(Art);
                AssetDatabase.Refresh();
                Models.Clear();
                Materials.Clear();
                routes = WorldProductionAudit.Routes();
                var legacy = GameObject.Find(OldTowns).transform;
                var root = new GameObject(RootLabel).transform;
                var report = new StringBuilder("Riftbound CC0 map rebuild\n");
                ShapeBridgeBanks();
                BuildBridge(root, legacy);
                BuildTowns(root, legacy, report);
                BuildRegionalScenery(root, report);
                Physics.SyncTransforms();
                Validate(report);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the rebuilt world.");
                AssetDatabase.SaveAssets();
                Render();
                File.WriteAllText(Evidence + "/validation.txt", report.ToString());
            }
            catch (Exception error)
            {
                File.WriteAllText(Evidence + "/error.txt", error.ToString());
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        static void ShapeBridgeBanks()
        {
            var terrain = WorldRegions.TerrainAt(new Vector3(-256, 6, 50));
            var original = terrain.terrainData;
            const string path = "Assets/RPG/Terrain/ReaverCamp/Westbank CC0 banks.asset";
            if (!AssetDatabase.LoadAssetAtPath<TerrainData>(path) &&
                !AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), path))
                throw new IOException("Could not branch the Westbank terrain.");
            var banks = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            var origin = terrain.GetPosition();
            int resolution = banks.heightmapResolution;
            var samples = original.GetHeights(0, 0, resolution, resolution);
            for (int j = 0; j < resolution; j++)
            {
                float z = origin.z + j * banks.size.z / (resolution - 1);
                float acrossRoad = Mathf.Abs(z - 50);
                if (acrossRoad <= 4.0f || acrossRoad >= 17.0f) continue;
                float drop = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(4, 6, acrossRoad))
                    * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(9, 17, acrossRoad)));
                for (int i = 0; i < resolution; i++)
                {
                    float x = origin.x + i * banks.size.x / (resolution - 1);
                    float alongDeck = Mathf.Abs(x + 255.459f);
                    if (alongDeck > 14) continue;
                    float weight = drop * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(9.5f, 14, alongDeck)));
                    float old = origin.y + samples[j, i] * banks.size.y;
                    float cap = Mathf.Lerp(4.05f, 5.25f, Mathf.InverseLerp(4.5f, 11.5f, alongDeck));
                    samples[j, i] = (Mathf.Lerp(old, Mathf.Min(old, cap), weight) - origin.y) / banks.size.y;
                }
            }
            banks.SetHeights(0, 0, samples);
            terrain.terrainData = banks;
            terrain.GetComponent<TerrainCollider>().terrainData = banks;
            terrain.Flush();
            EditorUtility.SetDirty(banks);
        }

        static void BuildBridge(Transform root, Transform legacy)
        {
            var deck = GameObject.Find("Westbank bridge deck").GetComponent<Collider>().bounds;
            var oldBridge = legacy.Find("Westbank stone and timber bridge");
            oldBridge.gameObject.SetActive(false);
            var bridge = Group(root, "Westbank limestone arch");
            bridge.position = new Vector3(deck.center.x, deck.max.y - .015f, deck.center.z);
            var arch = Model(bridge, "Bridge", "Stone Bridge", Vector3.zero, new Vector3(deck.size.x / 8f, 1.15f, deck.size.z / 2f), 0, "DEDAD0");
            var stone = MaterialFor("Village", "MI_RockTrim");
            var timber = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Dark timber.mat");
            foreach (float side in new[] { -1f, 1f })
            {
                float z = side * (deck.extents.z + .30f);
                for (int i = 0; i < 16; i++)
                {
                    float x = Mathf.Lerp(-12f, 12f, i / 15f);
                    Block(bridge, "Cut stone parapet", new Vector3(x, .42f, z), new Vector3(1.59f, .72f, .48f), stone, true);
                }
                Block(bridge, "Oak coping beam", new Vector3(0, .84f, z), new Vector3(25.7f, .17f, .59f), timber);
                for (int i = 0; i < 5; i++)
                {
                    float x = Mathf.Lerp(-12.7f, 12.7f, i / 4f);
                    Block(bridge, "Parapet pier", new Vector3(x, .49f, z), new Vector3(.60f, .98f, .65f), stone);
                    Block(bridge, "Pier coping", new Vector3(x, 1.02f, z), new Vector3(.76f, .13f, .77f), timber);
                }
            }
            var landing = Group(root, "Westbank landings");
            for (int i = 0; i < 4; i++)
            {
                float x = i < 2 ? -270.1f : -240.8f;
                float z = i % 2 == 0 ? 46.5f : 53.5f;
                Place(landing, "Medieval", "mdvl_castle_brazier_01", x, z, .83f, 0);
            }
            if (!arch.GetComponentInChildren<MeshRenderer>()) throw new InvalidOperationException("The bridge mesh did not import.");
        }

        static void BuildTowns(Transform root, Transform legacy, StringBuilder report)
        {
            var town = Group(root, "Workshop streets");
            Reface(town, legacy, "Emberwatch smithy", 7.3f, 6.1f, "B67154", "F2DAB9", true);
            Reface(town, legacy, "Emberwatch coach house", 8.25f, 6.0f, "B07A57", "E6D6B5", false);
            Reface(town, legacy, "Willow Rest tollhouse", 6.5f, 5.3f, "6B9791", "D6C19F", false);
            Reface(town, legacy, "Greenhollow ranger lodge", 8.25f, 6.3f, "718267", "DDD3AA", false);
            Reface(town, legacy, "Highspire wayfarers hall", 9f, 6.6f, "778CA4", "D2D6D6", true);
            Reface(town, legacy, "Bloodrock caravan workshop", 7.8f, 6.4f, "995E47", "C89978", true);

            Market(town, legacy.Find("Emberwatch smithy"), false);
            Market(town, legacy.Find("Greenhollow ranger lodge"), true);
            Market(town, legacy.Find("Bloodrock caravan workshop"), false);
            Market(town, legacy.Find("Willow Rest tollhouse"), true);
            Market(town, legacy.Find("Highspire wayfarers hall"), false);
            AddCottage(town, "Emberwatch orchard house", new Vector2(-20, -20), 15, 220);
            AddCottage(town, "Emberwatch provisioner", new Vector2(28, -20), 14, 331);
            AddCottage(town, "Greenhollow forester's cottage", new Vector2(-11, 427), 15, 28);
            AddCottage(town, "Willow Rest ferry cottage", new Vector2(-279, 17), 13, 235);

            var smithy = legacy.Find("Emberwatch smithy");
            var ranger = legacy.Find("Greenhollow ranger lodge");
            var citizenA = smithy.TransformPoint(new Vector3(-5.25f, 0, 6.25f));
            var citizenB = ranger.TransformPoint(new Vector3(4.85f, 0, 4.05f));
            Citizen(town, "Male_Peasant", "Emberwatch provisioner", citizenA, smithy.rotation, "I keep a dry stock of oats and lamp oil for the road.");
            Citizen(town, "Male_Ranger", "Greenhollow trail warden", citizenB, ranger.rotation, "Stay below the pines if you cross the frontier after dark.");
            report.AppendLine("Rebuilt six workshop exteriors, added four cottages and five market yards, and dressed two ambient townsfolk.");
        }

        static void Reface(Transform root, Transform legacy, string name, float width, float depth, string roofTint, string plasterTint, bool stone)
        {
            var existing = legacy.Find(name);
            var house = Group(root, name + " modular exterior");
            house.SetPositionAndRotation(existing.position, existing.rotation);
            foreach (var renderer in existing.GetComponentsInChildren<MeshRenderer>())
            {
                string n = renderer.name;
                if (n == "Back wall" || n == "Side wall" || n == "Front wing" || n == "Doorway lintel" ||
                    n == "Pitched tile roof" || n == "Ridge beam" || n == "Gable bargeboard" ||
                    n == "Corner timber" || n == "Side sill" || n == "Side eave" || n.Contains("Gable"))
                    renderer.enabled = false;
            }

            float hw = width / 2f, hd = depth / 2f;
            SideWall(house, new Vector3(-hw, 0, 0), 90, depth, stone, plasterTint);
            SideWall(house, new Vector3(hw, 0, 0), -90, depth, stone, plasterTint);
            SideWall(house, new Vector3(0, 0, -hd), 0, width, stone, plasterTint);
            // Keep the existing open doorway and collision at the center of the frontage.
            float wing = (width - 1.96f) / 2f;
            foreach (float side in new[] { -1f, 1f })
                Village(house, "Wall_Plaster_WoodGrid", new Vector3(side * (.98f + wing / 2f), 0, hd + .04f),
                    new Vector3(wing / 2f, 1f, 1f), 180, roofTint, plasterTint);
            Block(house, "Doorway header", new Vector3(0, 2.72f, hd),
                new Vector3(2.08f, .82f, .29f), MaterialFor("Village", "MI_WoodTrim"));

            float rise = Mathf.Tan(32 * Mathf.Deg2Rad) * hw;
            Village(house, "Roof_RoundTiles_8x10", new Vector3(0, 3.1f, 0),
                new Vector3((width + .9f) / 9.952f, (rise + .16f) / 6f, (depth + .9f) / 11.858f), 0, roofTint, plasterTint);
            foreach (float side in new[] { -1f, 1f })
                Village(house, "Roof_Front_Brick8", new Vector3(0, 3.04f, side * hd),
                    new Vector3(width / 8.511f, rise / 5.314f, .75f), side > 0 ? 180 : 0, roofTint, plasterTint);
            Village(house, "Prop_Chimney2", new Vector3(-hw * .52f, 3.0f + rise * .40f, -hd * .51f),
                new Vector3(.82f, .89f, .82f), 0, roofTint, plasterTint);
        }

        static void SideWall(Transform house, Vector3 pos, float angle, float width, bool stone, string tint)
        {
            var rotation = Quaternion.Euler(0, angle, 0);
            for (int i = 0; i < 3; i++)
            {
                bool window = i == 1 && !stone;
                string mesh = window ? "Wall_Plaster_Window_Wide_Flat" : stone ? "Wall_UnevenBrick_Straight" : "Wall_Plaster_WoodGrid";
                var center = pos + rotation * Vector3.right * ((i - 1) * width / 3);
                var scale = new Vector3(width / 6f, 1, 1);
                Village(house, mesh, center, scale, angle, "", tint);
                if (window) Village(house, "Window_Wide_Flat1", center, scale, angle, "", tint);
            }
        }

        static void Market(Transform parent, Transform building, bool cool)
        {
            var yard = Group(parent, building.name + " market yard");
            yard.SetPositionAndRotation(building.position, building.rotation);
            LocalProp(yard, "mdvl_village_stall_01", -3.35f, 5.4f, .91f, 175, cool);
            LocalProp(yard, "mdvl_props_barrel_01", -4.3f, 4.6f, 1f, 45, cool);
            LocalProp(yard, "mdvl_food_grain_sack_01", -4.1f, 5.25f, 1.13f, 25, cool);
            LocalProp(yard, "mdvl_props_crate_01", -3.9f, 5.95f, 1.04f, 67, cool);
            LocalProp(yard, "mdvl_food_produce_crate_01", -3.9f, 5.95f, 1.04f, 67, cool, .56f);
            LocalProp(yard, "mdvl_village_well_01", 5.75f, 5.7f, 1.02f, 10, cool);
            LocalProp(yard, "mdvl_props_public_bench_01", 6.1f, 3.45f, 1.1f, 16, cool);
            for (int i = 0; i < 4; i++)
            {
                var world = yard.TransformPoint(new Vector3(1.05f, 0, 3.2f + .85f * i));
                Place(parent, "Nature", "RockPath_Square_Small_1", world.x, world.z, .85f, 20 + i * 31);
            }
        }

        static void LocalProp(Transform yard, string model, float x, float z, float scale, float angle, bool cool, float lift = 0)
        {
            var world = yard.TransformPoint(new Vector3(x, 0, z));
            var prop = Place(yard, "Medieval", model, world.x, world.z, scale, yard.eulerAngles.y + angle, cool ? "D0DBD5" : "", lift);
            if (model.Contains("well")) prop.gameObject.AddComponent<BoxCollider>().size = new Vector3(1.30f, 1.0f, 1.2f);
        }

        static void AddCottage(Transform parent, string label, Vector2 center, float radius, float bearing)
        {
            for (int i = 0; i < 42; i++)
            {
                float a = (bearing + 137.508f * i) * Mathf.Deg2Rad;
                Vector2 xy = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (radius + i / 12 * 4.5f);
                if (!Clear(xy.x, xy.y, 6.5f, .9f)) continue;
                var home = Place(parent, "Medieval", "mdvl_village_cottage_01", xy.x, xy.y, .91f, 90 - a * Mathf.Rad2Deg, "E3D6C6");
                home.name = label;
                var collider = home.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0, 1.5f, 0);
                collider.size = new Vector3(5.9f, 3.1f, 5.7f);
                Physics.SyncTransforms();
                return;
            }
            throw new InvalidOperationException("No clear cottage lot for " + label);
        }

        static void BuildRegionalScenery(Transform root, StringBuilder report)
        {
            Grove(root, report, "Silverglen sanctuary canopy", -151, 83, 42, 30, 24, 301, "A5C7AB",
                new[] { "TwistedTree_1", "CommonTree_3", "CommonTree_4", "TwistedTree_3" });
            Grove(root, report, "Frostpeak alpine line", -97, -107, 30, 29, 20, 302, "B9C9C5",
                new[] { "Pine_1", "Pine_3", "Pine_5", "Rock_Medium_2" });
            Grove(root, report, "Greenhollow woodland edge", -41, 413, 32, 29, 18, 303, "ACC6A4",
                new[] { "CommonTree_1", "CommonTree_4", "Pine_3", "Pine_5" });
            Grove(root, report, "Bloodrock weathered outcrops", 288, 215, 31, 27, 17, 304, "BC987F",
                new[] { "Rock_Medium_1", "Rock_Medium_2", "Rock_Medium_3", "DeadTree_2" });
            Grove(root, report, "Wyrmcrest crags", 246, 473, 42, 35, 15, 305, "BEC7D4",
                new[] { "Rock_Medium_1", "Rock_Medium_2", "Pine_5", "DeadTree_2" });

            var bank = Group(root, "Westbank riparian margins");
            var rand = new System.Random(903);
            for (int i = 0; i < 46; i++)
            {
                float z = Mathf.Lerp(-12, 101, i / 45f);
                if (Mathf.Abs(z - 50) < 10) continue;
                foreach (int sign in new[] { -1, 1 })
                {
                    float x = WorldExpansionLayout.RiverX(z) + sign * (6.1f + (float)rand.NextDouble() * 4.5f);
                    if (!Clear(x, z, 1.12f, 2.1f) || Ground(x, z) < 3.35f) continue;
                    string model = i % 5 == 0 ? "Rock_Medium_2" : i % 3 == 0 ? "Fern_1" : "Bush_Common_Flowers";
                    Place(bank, "Nature", model, x, z, model == "Fern_1" ? .21f : .48f + .24f * (float)rand.NextDouble(),
                        (float)rand.NextDouble() * 360f, "B4C5A4", -.07f);
                }
            }
            report.AppendLine($"Westbank received {bank.childCount} new bank-side plants and rocks.");
        }

        static void Grove(Transform root, StringBuilder report, string label, float cx, float cz,
            float width, float depth, int count, int seed, string tint, string[] models)
        {
            var area = Group(root, label);
            var random = new System.Random(seed);
            var positions = new List<Vector2>();
            for (int i = 0; i < count * 27 && positions.Count < count; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radial = Mathf.Lerp(.26f, 1f, Mathf.Sqrt((float)random.NextDouble()));
                var p = new Vector2(cx + Mathf.Cos(angle) * width * radial, cz + Mathf.Sin(angle) * depth * radial);
                if (!Clear(p.x, p.y, 2.35f, 3.6f) || positions.Any(previous => (previous - p).sqrMagnitude < 23f)) continue;
                string model = models[positions.Count % models.Length];
                float size = Mathf.Lerp(.60f, 1.02f, (float)random.NextDouble());
                var point = Place(area, "Nature", model, p.x, p.y, size, (float)random.NextDouble() * 360f, tint, -.12f);
                // Trunk and rock collision remains compact to leave woodland paths open.
                var bounds = BoundsOf(point);
                var obstacle = point.gameObject.AddComponent<CapsuleCollider>();
                obstacle.radius = model.StartsWith("Rock") ? .60f : .26f;
                obstacle.height = Mathf.Min(3.1f, bounds.size.y / size * .50f);
                obstacle.center = Vector3.up * (obstacle.height * .5f);
                positions.Add(p);
                if (positions.Count % 3 == 0)
                {
                    var fern = p + new Vector2(2.1f, -.9f);
                    if (Clear(fern.x, fern.y, .8f, 2.0f))
                        Place(area, "Nature", "Fern_1", fern.x, fern.y, .22f, i * 37, tint, -.08f);
                }
            }
            report.AppendLine($"{label}: {positions.Count} primary trees and rocks, {area.childCount - positions.Count} understory groups.");
        }

        static void Citizen(Transform parent, string outfit, string label, Vector3 pos, Quaternion rotation, string greeting)
        {
            string bodyPath = $"{Source}/Outfits/{outfit}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(bodyPath);
            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }
            var actor = Group(parent, label);
            actor.SetPositionAndRotation(new Vector3(pos.x, Ground(pos.x, pos.z) + .08f, pos.z), rotation);
            var body = Model(actor, "Outfits", outfit, Vector3.zero, Vector3.one);
            var animator = body.GetComponentInChildren<Animator>();
            if (!animator || !animator.isHuman || !animator.avatar.isValid)
                throw new InvalidOperationException("Humanoid avatar failed for " + outfit);
            animator.runtimeAnimatorController = TownsfolkController();
            animator.applyRootMotion = false;
            var bone = animator.GetBoneTransform(HumanBodyBones.Head);
            if (!bone) throw new InvalidOperationException("No head bone in " + outfit);
            AddHead(body, bone);
            var npc = actor.gameObject.AddComponent<TownNPC>();
            npc.npcName = label;
            npc.roleTag = outfit.Contains("Ranger") ? "Frontier Watch" : "Townsfolk";
            npc.greeting = greeting;
        }

        static AnimatorController TownsfolkController()
        {
            const string path = Art + "/Townsfolk idle.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller) return controller;
            var clip = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/UniversalAnimations/UAL1_Standard.fbx")
                .OfType<AnimationClip>().First(animation => animation.name == "Armature|Idle_Loop" && animation.humanMotion);
            controller = AnimatorController.CreateAnimatorControllerAtPathWithClip(path, clip);
            var idle = controller.layers[0].stateMachine.defaultState;
            idle.name = "Idle";
            EditorUtility.SetDirty(idle);
            return controller;
        }

        public static void FinalizeTownsfolk()
        {
            var scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            var smithy = GameObject.Find(OldTowns).transform.Find("Emberwatch smithy");
            var provisioner = Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None)
                .Single(npc => npc.npcName == "Emberwatch provisioner");
            Vector3 pos = smithy.TransformPoint(new Vector3(-5.25f, 0, 6.25f));
            provisioner.transform.position = new Vector3(pos.x, Ground(pos.x, pos.z) + .08f, pos.z);
            var warden = Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None)
                .Single(npc => npc.npcName == "Greenhollow trail warden");
            foreach (var npc in new[] { provisioner, warden })
                npc.GetComponentInChildren<Animator>().runtimeAnimatorController = TownsfolkController();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Render();
            EditorApplication.delayCall += CC0WorldPreview.Verify;
        }

        static void AddHead(Transform body, Transform head)
        {
            const string model = "Superhero_Male_FullBody";
            string path = $"{Source}/Outfits/{model}.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var reference = Model(body, "Outfits", model, Vector3.zero, Vector3.one);
            Vector3 localHead = body.InverseTransformPoint(head.position);
            try
            {
                foreach (var skin in reference.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var sharedMesh = skin.sharedMesh;
                    var sourceVertices = sharedMesh.vertices;
                    var transformedVertices = sourceVertices.Select(skin.transform.TransformPoint).ToArray();
                    var modelPoints = transformedVertices.Select(body.InverseTransformPoint).ToArray();
                    for (int i = 0; i < sharedMesh.subMeshCount; i++)
                    {
                        string name = skin.sharedMaterials[i].name;
                        var indices = sharedMesh.GetTriangles(i);
                        var triangles = new List<int>();
                        for (int a = 0; a < indices.Length; a += 3)
                        {
                            if (modelPoints[indices[a]].y < localHead.y - .085f ||
                                modelPoints[indices[a + 1]].y < localHead.y - .085f ||
                                modelPoints[indices[a + 2]].y < localHead.y - .085f) continue;
                            triangles.Add(indices[a]);
                            triangles.Add(indices[a + 1]);
                            triangles.Add(indices[a + 2]);
                        }
                        if (triangles.Count == 0) continue;
                        var piece = Group(head, "Townsperson " + name);
                        string meshPath = $"{Art}/{name} head.asset";
                        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                        if (!mesh)
                        {
                            var remap = new Dictionary<int, int>();
                            var verts = new List<Vector3>();
                            var uv = new List<Vector2>();
                            var sourceUV = sharedMesh.uv;
                            for (int k = 0; k < triangles.Count; k++)
                            {
                                int index = triangles[k];
                                if (!remap.TryGetValue(index, out int mapped))
                                {
                                    mapped = verts.Count;
                                    remap[index] = mapped;
                                    verts.Add(head.InverseTransformPoint(transformedVertices[index]));
                                    uv.Add(sourceUV[index]);
                                }
                                triangles[k] = mapped;
                            }

                            mesh = new Mesh { name = name + " head", vertices = verts.ToArray(), uv = uv.ToArray(), triangles = triangles.ToArray() };
                            mesh.RecalculateNormals();
                            mesh.RecalculateBounds();
                            AssetDatabase.CreateAsset(mesh, meshPath);
                        }
                        piece.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                        piece.gameObject.AddComponent<MeshRenderer>().sharedMaterial = skin.sharedMaterials[i];
                    }
                }
            }
            finally { Object.DestroyImmediate(reference.gameObject); }
            if (!head.GetComponentInChildren<MeshFilter>()) throw new InvalidOperationException("Compatible character head could not be assembled.");
        }

        static void Validate(StringBuilder report)
        {
            var player = Object.FindAnyObjectByType<PlayerController>().GetComponent<CharacterController>();
            var results = new StringBuilder();
            foreach (var route in routes)
            foreach (bool mounted in new[] { false, true })
            foreach (bool reverse in new[] { false, true })
                WorldTraversalVerification.Traverse(route, mounted, reverse, player, results);
            File.WriteAllText(Evidence + "/traversal.txt", results.ToString());
            if (results.ToString().Contains("BLOCKED")) throw new InvalidOperationException("Protected road traversal was blocked.");
            report.Append(results);
        }

        public static void RenderBatch()
        {
            Render();
            EditorApplication.Exit(0);
        }

        public static void Render()
        {
            Directory.CreateDirectory(Evidence);
            if (EditorSceneManager.GetActiveScene().path != WorldRegions.ScenePath)
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            var old = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                Shot("westbank", new Vector3(-243, 18, 28), new Vector3(-256, 5.7f, 51));
                Shot("westbank-road", new Vector3(-236, 10, 54), new Vector3(-260, 6.5f, 49));
                var smithy = GameObject.Find("Emberwatch smithy modular exterior").transform;
                Shot("emberwatch", smithy.position + smithy.forward * 14f + smithy.right * 7f + Vector3.up * 4.4f,
                    smithy.position + Vector3.up * 2.65f);
                Shot("silverglen", new Vector3(-171, 23, 65), new Vector3(-146, 17, 95));
                Shot("frostpeak", new Vector3(-123, 28, -80), new Vector3(-100, 15, -107));
                Shot("bloodrock", new Vector3(276, 29, 206), new Vector3(295, 19, 190));
                Shot("greenhollow", new Vector3(-27, 23, 412), new Vector3(-10, 17, 427));
                Shot("wayfarers", new Vector3(-205, 27, -71), new Vector3(-226, 15, -85));
            }
            finally { ShaderUtil.allowAsyncCompilation = old; }
        }

        static void Shot(string label, Vector3 camera, Vector3 target)
        {
            string name = "riftbound-cc0-" + label;
            WorldPresentationSetup.RenderPreview(name, camera, target, false);
            File.Copy($".code-check/{name}.png", $"{Evidence}/{name}.png", true);
        }

        static Transform Place(Transform parent, string pack, string mesh, float x, float z, float scale,
            float yaw, string tint = "", float offset = 0)
        {
            var site = Group(parent, mesh);
            site.SetPositionAndRotation(new Vector3(x, Ground(x, z) + offset, z), Quaternion.Euler(0, yaw, 0));
            site.localScale = Vector3.one * scale;
            var model = Model(site, pack, mesh, Vector3.zero, Vector3.one, 0, tint);
            if (pack == "Medieval")
            {
                var bounds = BoundsOf(model);
                var center = site.InverseTransformPoint(bounds.center);
                model.localPosition = new Vector3(-center.x, 0, -center.z);
            }
            return site;
        }

        static bool Clear(float x, float z, float radius, float maxGrade)
        {
            var pos = new Vector2(x, z);
            if (WorldExpansionLayout.RoadDistance(x, z) < radius + 1.4f) return false;
            foreach (var route in routes)
            for (int i = 1; i < route.points.Length; i++)
                if (WorldExpansionLayout.SegmentDistance(pos, route.points[i - 1], route.points[i], out _) < radius + 1.8f) return false;
            var terrain = WorldRegions.TerrainAt(new Vector3(x, 0, z));
            if (!terrain) return false;
            float y = Ground(x, z);
            foreach (var offset in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down })
                if (Mathf.Abs(Ground(x + offset.x * radius, z + offset.y * radius) - y) > maxGrade) return false;
            return !Physics.OverlapBox(new Vector3(x, y + 1.5f, z), new Vector3(radius, 1.55f, radius), Quaternion.identity,
                ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider));
        }

        static float Ground(float x, float z)
        {
            var p = new Vector3(x, 0, z);
            var terrain = WorldRegions.TerrainAt(p);
            if (!terrain) throw new InvalidOperationException($"No terrain at ({x:F1}, {z:F1}).");
            return terrain.GetPosition().y + terrain.SampleHeight(p);
        }

        static Transform Village(Transform parent, string mesh, Vector3 position, Vector3 scale,
            float yaw, string roof, string plaster)
            => Model(parent, "Village", mesh, position, scale, yaw, roof, plaster);

        static Transform Model(Transform parent, string pack, string name, Vector3 pos, Vector3 scale,
            float yaw = 0, string tint = "", string plaster = "")
        {
            string key = pack + "/" + name;
            if (!Models.TryGetValue(key, out var asset))
            {
                asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Source}/{key}.{(pack == "Bridge" ? "obj" : "fbx")}");
                if (!asset) throw new FileNotFoundException(key);
                Models[key] = asset;
            }
            // Some FBX roots carry a centimeters-to-meters and Z-up conversion.
            var placement = Group(parent, name);
            placement.localPosition = pos;
            placement.localRotation = Quaternion.Euler(0, yaw, 0);
            placement.localScale = scale;
            var node = (GameObject)PrefabUtility.InstantiatePrefab(asset, placement);
            foreach (var renderer in node.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
                {
                    string materialName = material.name;
                    string color = materialName == "MI_Plaster" ? plaster : tint;
                    if (pack == "Village" && materialName != "MI_RoundTiles" && materialName != "MI_Plaster") color = "";
                    return MaterialFor(pack, materialName, color);
                }).ToArray();
            }
            return placement;
        }

        static Material MaterialFor(string pack, string material, string tint = "")
        {
            string key = $"{pack}-{material}-{tint}";
            if (Materials.TryGetValue(key, out var cached)) return cached;
            string path = $"{Art}/{key}.mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!result)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
                AssetDatabase.CreateAsset(result, path);
            }
            ColorUtility.TryParseHtmlString("#" + (tint == "" ? "FFFFFF" : tint), out var color);
            result.SetFloat("_Smoothness", .17f);
            string baseTexture = "", normal = "";
            bool foliage = false;
            switch (pack)
            {
                case "Village":
                    string kind = material.Replace("MI_", "").Replace("_Wear", "");
                    baseTexture = "T_" + kind + "_BaseColor";
                    normal = "T_" + kind + "_Normal";
                    if (kind == "WindowGlass")
                    {
                        ColorUtility.TryParseHtmlString("#74513C", out color);
                        result.SetColor("_EmissionColor", color * .32f);
                        result.EnableKeyword("_EMISSION");
                    }
                    break;
                case "Nature":
                    foliage = material.StartsWith("Leaves") || material == "Flowers" || material.Contains("Grass");
                    baseTexture = material == "Leaves_Pine" ? "Leaf_Pine_C" :
                        material == "Leaves_NormalTree" || material == "Leaves_TwistedTree" ? material + "_C" :
                        material == "Rocks" || material == "PathRocks" ? material + "_Diffuse" : material;
                    if (material.StartsWith("Bark")) normal = material + "_Normal";
                    break;
                case "Medieval": baseTexture = "mdvl_palette"; break;
                case "Bridge":
                    baseTexture = "stone_bridge_2_001_d";
                    normal = "stone_bridge_2_001_n";
                    color *= 2.8f; color.a = 1;
                    break;
                case "Outfits":
                    switch (material)
                    {
                        case "MI_Peasant": baseTexture = "T_Peasant_BaseColor"; normal = "T_Peasant_Normal"; break;
                        case "MI_Ranger": baseTexture = "T_Ranger_BaseColor"; normal = "T_Ranger_Normal"; break;
                        case "MI_Regular_Male": baseTexture = "T_Regular_Male_Dark_BaseColor"; normal = "T_Regular_Male_Normal"; break;
                        case "MI_Superhero_Male": baseTexture = "T_Superhero_Male_Dark"; break;
                        case "MI_Eyes": baseTexture = "T_Eye_Brown"; break;
                        case "MI_Hair_1": baseTexture = "T_Hair_1_BaseColor"; foliage = true; break;
                    }
                    break;
            }
            string texturePath = $"{Source}/{pack}/{baseTexture}.png";
            if (pack == "Medieval" && AssetImporter.GetAtPath(texturePath) is TextureImporter palette &&
                (palette.mipmapEnabled || palette.filterMode != FilterMode.Point))
            {
                palette.mipmapEnabled = false;
                palette.filterMode = FilterMode.Point;
                palette.textureCompression = TextureImporterCompression.Uncompressed;
                palette.SaveAndReimport();
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if (!texture && material != "MI_WindowGlass") throw new InvalidOperationException($"Missing material texture for {pack}/{material} ({baseTexture}).");
            result.SetTexture("_BaseMap", texture);
            result.SetColor("_BaseColor", color);
            string normalPath = $"{Source}/{pack}/{normal}.png";
            if (AssetImporter.GetAtPath(normalPath) is TextureImporter normalImporter)
            {
                if (normalImporter.textureType != TextureImporterType.NormalMap)
                {
                    normalImporter.textureType = TextureImporterType.NormalMap;
                    normalImporter.SaveAndReimport();
                }
                result.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                result.SetFloat("_BumpScale", .64f);
                result.EnableKeyword("_NORMALMAP");
            }
            if (foliage)
            {
                result.SetFloat("_Cull", 0);
                result.SetFloat("_AlphaClip", 1);
                result.SetFloat("_Cutoff", .39f);
                result.EnableKeyword("_ALPHATEST_ON");
                result.SetOverrideTag("RenderType", "TransparentCutout");
                result.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                result.SetFloat("_Smoothness", 0);
            }
            EditorUtility.SetDirty(result);
            Materials[key] = result;
            return result;
        }

        static Bounds BoundsOf(Transform node)
        {
            var renderers = node.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        static Transform Group(Transform parent, string name)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            return node;
        }

        static void Block(Transform parent, string label, Vector3 position, Vector3 scale, Material material, bool solid = false)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = label;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(block.GetComponent<BoxCollider>());
        }
    }
}






