using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static class EmberfordWorldIntegration
    {
        const string Kit = "Assets/RPG/ProceduralKit/";
        const string CatalogPath = "Assets/RPG/Resources/ProceduralActorCatalog.asset";
        const string World = "Emberford - Ashwood district, approach, and camps";
        const string RoadAsset = Kit + "Emberford Trails.asset";
        static Scene scene;
        static Transform root;
        static Terrain[] terrains;
        static Mesh trails;
        static int propCount;

        [MenuItem("Riftbound/Procedural Kit/Install Emberford in the campaign")]
        public static void Run()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            RepairNestedLODGroups();
            scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            var previous = scene.GetRootGameObjects().FirstOrDefault(g => g.name == World);
            if (previous) Object.DestroyImmediate(previous);

            Terrain[] tiles = Terrain.activeTerrains;
            if (tiles.Length == 0) throw new InvalidOperationException("The expanded world has no terrain.");
            terrains = tiles;
            EmberfordLandscape.Shape(terrains);
            RefreshSurfaceMaterials();
            VillageRemodelPreviewBuild.StageRemodels(scene);
            var catalog = WriteCatalog();

            root = new GameObject(World).transform;
            propCount = 0;
            BuildDistrict();
            BuildRuinAndCamp();
            RebuildWestbank();
            Mesh oldTrails = AssetDatabase.LoadAssetAtPath<Mesh>(RoadAsset);
            if (oldTrails)
            {
                EditorUtility.CopySerialized(trails, oldTrails);
                Object.DestroyImmediate(trails);
                trails = oldTrails;
            }
            else AssetDatabase.CreateAsset(trails, RoadAsset);
            var roads = root.Find("Emberford stone-dust lanes");
            roads.GetComponent<MeshFilter>().sharedMesh = trails;
            EditorUtility.SetDirty(trails);

            var counts = InstallActors(catalog);
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player && player.animator && player.animator.gameObject != player.gameObject)
                Install(player.animator.gameObject, player.animator, catalog.alliance[0], .97f);
            VerifyBridge();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the playable campaign scene.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Emberford campaign installed {propCount} prop and architecture instances, {counts.enemies} enemy visuals and {counts.civilians} town visuals. Saved {scene.path}.");
        }

        static ProceduralActorCatalog WriteCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ProceduralActorCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<ProceduralActorCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            string[] classes = { "Knight", "Mage", "Rogue", "Paladin" };
            catalog.alliance = classes.Select(c => Prefab("Emberwatch_" + c)).ToArray();
            catalog.horde = classes.Select(c => Prefab("Briar_" + c)).ToArray();
            catalog.brute = Prefab("Briar_Brute");
            catalog.goblin = Prefab("Cutpurse_Goblin");
            catalog.sentinel = Prefab("Quarry_Sentinel");
            catalog.wraith = Prefab("Gloam_Wraith");
            catalog.smith = Prefab("Town_Blacksmith");
            catalog.scout = Prefab("Town_Scout");
            catalog.merchant = Prefab("Town_Merchant");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static GameObject Prefab(string key)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "Prefabs/" + key + ".prefab");
            if (!prefab) throw new InvalidOperationException($"Missing Emberford prefab {key}.");
            return prefab;
        }

        static float GroundHeight(float x, float z) => WorldRegions.GroundHeight(new Vector3(x, 0, z), terrains);
        static Vector3 Ground(Vector2 p, float offset = 0) => new Vector3(p.x, GroundHeight(p.x, p.y) + offset, p.y);

        static Transform Group(string name, Vector2 centre)
        {
            var group = new GameObject(name).transform;
            group.SetParent(root, false);
            group.position = Ground(centre);
            return group;
        }

        static GameObject Add(Transform group, string key, float x, float z, float yaw = 0, float scale = 1,
            float footing = 0, bool foundation = false)
        {
            var local = group.TransformPoint(x, 0, z);
            var point = new Vector2(local.x, local.z);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(key), scene);
            instance.transform.SetParent(group, false);
            instance.transform.SetPositionAndRotation(Ground(point, footing), Quaternion.Euler(0, yaw, 0));
            instance.transform.localScale = Vector3.one * scale;
            if (foundation)
            {
                var ground = instance.transform.position;
                var support = instance.GetComponentsInChildren<MeshCollider>().FirstOrDefault();
                if (support)
                {
                    float xExtent = Mathf.Max(1, support.bounds.extents.x * .72f);
                    float zExtent = Mathf.Max(1, support.bounds.extents.z * .72f);
                    float lowest = ground.y;
                    float highest = ground.y;
                    foreach (float side in new[] { -1f, 1f })
                    foreach (float end in new[] { -1f, 1f })
                    {
                        float y = GroundHeight(ground.x + xExtent * side, ground.z + zExtent * end);
                        lowest = Mathf.Min(lowest, y);
                        highest = Mathf.Max(highest, y);
                    }
                    // Sink gently into the meadow, or fill a visible downhill foundation with stone.
                    instance.transform.position = new Vector3(ground.x, highest - .08f, ground.z);
                    if (highest - lowest > .14f)
                    {
                        var foot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        foot.name = instance.name + " stone foundation";
                        foot.transform.SetParent(group, false);
                        foot.transform.position = new Vector3(ground.x, (highest + lowest) * .5f - .16f, ground.z);
                        foot.transform.localScale = new Vector3(xExtent * 2.25f, highest - lowest + .24f, zExtent * 2.25f);
                        foot.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/Limestone.mat");
                    }
                }
            }
            propCount++;
            return instance;
        }

        static void BuildDistrict()
        {
            var district = Group("Emberford - market and smithy", new Vector2(72, -42));
            Add(district, "Wayfarer_Inn", -12, -4, 90, 1.08f, foundation: true);
            Add(district, "Emberford_Smithy", 12, -4, -90, 1.07f, foundation: true);
            Add(district, "Bell_Chapel", 0, 17, 180, 1.05f, foundation: true);
            Add(district, "Timber_Cottage", -13, 10, 102, 1.04f, foundation: true);
            Add(district, "Timber_Cottage", 14, 11, -107, .92f, foundation: true);
            Add(district, "Timber_Cottage", -9, 22, 130, .9f, foundation: true);
            Add(district, "Timber_Cottage", 10, 23, -135, .86f, foundation: true);
            Add(district, "Stone_Arch", 0, -11, 0, 1.1f);
            Town("Town_Blacksmith", "Mara Bellows", "Emberford Smith", 7, -1, -85,
                "The forge is lit. If you go north, follow the old watch chapel trail off the Ashwood road.");
            Town("Town_Merchant", "Orren Reed", "Innkeeper", -6, 0, 85,
                "The Wayfarer Inn takes travellers dry or drenched. The footway west leads to Emberwatch.");
            Town("Town_Scout", "Tamsin Vail", "Emberford Watch", -2, -8, 185,
                "The Briar Watch built a stockade north of the Redridge road. The trail branches off between the rock walls.");

            void Town(string key, string name, string role, float x, float z, float yaw, string greeting)
            {
                var npc = Add(district, key, x, z, yaw).GetComponent<TownNPC>();
                npc.npcName = name;
                npc.roleTag = role;
                npc.greeting = greeting;
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Add(district, "Street_Lantern", side * 3.1f, -9, 0, 1.05f);
                Add(district, "Street_Lantern", side * 4.1f, 7, 0, 1.05f);
                Add(district, "Supply_Crate", side * 8, -3, side * 16);
                Add(district, "Barrel", side * 9.1f, -2.5f);
                Add(district, "Stone_Wall", side * 7, -10.8f, 0, 1.15f);
                Add(district, "Stone_Wall", side * 12, -10.8f, 0, 1.15f);
                Add(district, "Fir_Tree", side * 18, 7, side * 17, 1.25f);
                Add(district, "Fir_Tree", side * 14, 27, 30, 1.4f);
                Add(district, "Fir_Tree", side * 20, -9, 125, 1.12f);
                Add(district, "Moss_Rock", side * 16, 18, 130, .85f);
            }
            for (int z = -2; z <= 2; z++)
            for (int x = -2; x <= 2; x++)
                Add(district, "Paving_Tile", x * .93f, 4.1f + z * .93f, ((x + z + 8) % 4) * 90,
                    .92f, -.015f);

            PlaceExisting("Assets/RPG/Prefabs/Props/Prefab_TownWell.prefab", district, 3.8f, 4.8f, 24, .82f);
            foreach (float side in new[] { -1f, 1f })
            {
                for (int i = 0; i < 5; i++)
                {
                    float x = side * (7.7f + i * 1.1f);
                    PlaceExisting("Assets/RPG/Prefabs/Nature/T_Flower_1_Clump.prefab", district, x, 5.9f + i * .53f, i * 31, .86f);
                    PlaceExisting("Assets/RPG/Prefabs/Nature/T_Flower_3_Clump.prefab", district, x, 5.7f + i * .61f, 30 * i, .59f);
                }
                PlaceExisting("Assets/RPG/Prefabs/Nature/T_Bush.prefab", district, side * 4.6f, 12f, side * 30, .75f);
                PlaceExisting("Assets/RPG/Prefabs/Nature/T_Bush_Large.prefab", district, side * 6.9f, 18f, side * 40, .83f);
            }
            for (int i = 0; i < 12; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                Add(district, "Moss_Rock", side * (16.5f + (i % 3) * .75f), -7 + 2.7f * i,
                    41 * i, .44f + (i % 4) * .15f);
            }
            MeshRoads(new[] {
                (2.7f, new[] { new Vector2(45,-35), new Vector2(51,-39), new Vector2(59,-48),
                    new Vector2(66,-53), new Vector2(72,-53), new Vector2(72,-41), new Vector2(72,-28) }),
                (1.05f, new[] { new Vector2(72,-43), new Vector2(65,-43), new Vector2(62,-46) }),
                (1.05f, new[] { new Vector2(72,-43), new Vector2(79,-43), new Vector2(82,-46) }),
                (1.5f, new[] { new Vector2(45,100), new Vector2(55,102), new Vector2(63,103), new Vector2(70,105) }),
                (1.4f, new[] { new Vector2(139,159), new Vector2(144,164), new Vector2(149,169), new Vector2(149,181) })
            });
        }

        static void BuildRuinAndCamp()
        {
            var ruin = Group("Ashwood - broken watch chapel", new Vector2(70, 105));
            Add(ruin, "Broken_Arch", 0, 0, 70);
            Add(ruin, "Ruined_Wall", 3.9f, 3.7f, 24, 1.2f);
            Add(ruin, "Ruined_Wall", -3.2f, 5, -12, 1.4f);
            Add(ruin, "Ruined_Wall", 6.1f, 7.5f, 91, 1.6f);
            Add(ruin, "Collapsed_Shed", 1.3f, 9.2f, -11, 1.13f);
            Add(ruin, "Broken_Window", 4.6f, 5.8f, -32, 1, -.37f);
            Add(ruin, "Broken_Door", -4.6f, 7.6f, 33, .85f, -.2f);
            for (int i = 0; i < 10; i++)
            {
                float angle = i * 2.4f;
                float distance = 2.2f + i * .65f;
                Add(ruin, i % 3 == 0 ? "Moss_Rock" : "Rubble", Mathf.Cos(angle) * distance,
                    4 + Mathf.Sin(angle) * distance, i * 37, .58f + (i % 4) * .11f);
            }
            var camp = Group("Briar Watch - palisade stockade", new Vector2(149, 175));
            Add(camp, "Briar_Watchtower", -7.8f, 5.1f, -12, 1.2f, foundation: true);
            Add(camp, "Briar_Camp_Hut", -1.2f, 10, 13, 1.12f, foundation: true);
            Add(camp, "Briar_Camp_Hut", 7.5f, 4.5f, -55, .95f, foundation: true);
            Add(camp, "Briar_Camp_Hut", 6.2f, 12, -158, .89f, foundation: true);
            foreach (float side in new[] { -1f, 1f })
            {
                Add(camp, "Briar_Banner", side * 2.7f, -2.2f, 0);
                for (int i = 0; i < 4; i++)
                    Add(camp, "Palisade", side * 10, i * 4.4f, 90, .96f);
                Add(camp, "Palisade", side * 6.1f, -2.4f, 0, 1.03f);
                Add(camp, "Palisade", side * 4.5f, 14.5f, 0, 1.03f);
                Add(camp, "Supply_Crate", side * 6, 6.2f);
                Add(camp, "Barrel", side * 7, 6.2f);
            }
            SpawnGarrison(camp);
        }

        static void SpawnGarrison(Transform camp)
        {
            Spawn("camp-guard-1", "briar-stockade-vanguard", "Briar Stockade Vanguard", -4.5f, 7.6f, 120, 130);
            Spawn("tower-goblin-1", "briar-stockade-cutpurse", "Briar Stockade Cutpurse", 4.8f, 10.5f, 195, 80);
            Spawn("camp-guard-2", "briar-stockade-lookout", "Briar Stockade Lookout", 4.6f, 1.5f, 240, 95);

            void Spawn(string templateId, string id, string title, float x, float z, float yaw, float health)
            {
                var template = Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include)
                    .Single(enemy => enemy.enemyId == templateId);
                var instance = Object.Instantiate(template.gameObject, camp, false);
                if (instance.GetComponent<ProceduralActorVisual>() is { } previous)
                {
                    if (previous.StagedDisplay) Object.DestroyImmediate(previous.StagedDisplay.gameObject);
                    Object.DestroyImmediate(previous);
                }
                instance.name = title;
                instance.SetActive(true);
                var position = camp.TransformPoint(x, 0, z);
                instance.transform.SetPositionAndRotation(Ground(new Vector2(position.x, position.z), .04f),
                    Quaternion.Euler(0, yaw, 0));
                instance.GetComponent<EnemyController>().enemyId = id;
                var stats = instance.GetComponent<Combatant>();
                stats.displayName = title;
                stats.maxHealth = health;
            }
        }

        static void PlaceExisting(string path, Transform group, float x, float z, float yaw, float scale)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new InvalidOperationException($"Missing landscaping prefab {path}.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetParent(group, false);
            var position = group.TransformPoint(x, 0, z);
            instance.transform.SetPositionAndRotation(Ground(new Vector2(position.x, position.z)), Quaternion.Euler(0, yaw, 0));
            instance.transform.localScale *= scale;
            propCount++;
        }

        static void RepairNestedLODGroups()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Kit + "Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponentsInChildren<LODGroup>(true).Length < 2) continue;
                var prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (var extra in prefab.GetComponentsInChildren<LODGroup>(true))
                        if (extra.gameObject != prefab) Object.DestroyImmediate(extra);
                    if (!PrefabUtility.SaveAsPrefabAsset(prefab, path))
                        throw new InvalidOperationException($"Could not repair model LOD groups in {path}.");
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }

        static void RefreshSurfaceMaterials()
        {
            Material trail = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/Trail.mat");
            trail.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "Textures/trail_albedo.png"));
            trail.SetColor("_BaseColor", new Color(.62f, .55f, .42f));
            EditorUtility.SetDirty(trail);

            Material river = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/River.mat");
            river.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Kit + "Textures/river_albedo.png"));
            river.SetColor("_BaseColor", new Color(.17f, .42f, .45f));
            river.SetFloat("_Smoothness", .28f);
            river.SetFloat("_SpecularHighlights", 0);
            river.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            EditorUtility.SetDirty(river);
            GameObject.Find("Westbank flowing river").GetComponent<MeshRenderer>().sharedMaterial = river;
        }

        static void MeshRoads((float halfWidth, Vector2[] points)[] routes)
        {
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var triangles = new List<int>();
            foreach (var (halfWidth, points) in routes)
            {
                var rows = new List<Vector2> { points[0] };
                for (int i = 1; i < points.Length; i++)
                {
                    int segments = Mathf.CeilToInt(Vector2.Distance(points[i - 1], points[i]) / .65f);
                    for (int step = 1; step <= segments; step++)
                        rows.Add(Vector2.Lerp(points[i - 1], points[i], (float)step / segments));
                }
                int first = vertices.Count;
                float length = 0;
                const int slices = 7;
                for (int i = 0; i < rows.Count; i++)
                {
                    var point = rows[i];
                    var ahead = rows[Mathf.Min(i + 1, rows.Count - 1)] - rows[Mathf.Max(i - 1, 0)];
                    var right = new Vector2(ahead.y, -ahead.x).normalized;
                    if (i > 0) length += Vector2.Distance(rows[i - 1], point);
                    for (int x = 0; x < slices; x++)
                    {
                        float t = x / (slices - 1f);
                        var edge = right * (Mathf.Lerp(-halfWidth, halfWidth, t) *
                            (.97f + .06f * Mathf.Sin(length * 1.17f + x)));
                        vertices.Add(Ground(point + edge, x == 0 || x == slices - 1 ? .018f : .064f));
                        uv.Add(new Vector2(t * halfWidth, length * .44f));
                        if (i == 0 || x == 0) continue;
                        int a = first + i * slices + x - 1;
                        triangles.AddRange(new[] { a - slices, a, a - slices + 1, a - slices + 1, a, a + 1 });
                    }
                }
            }
            trails = new Mesh { name = "Emberford stone-dust lanes" };
            trails.SetVertices(vertices);
            trails.SetUVs(0, uv);
            trails.SetTriangles(triangles, 0);
            trails.RecalculateNormals();
            trails.RecalculateBounds();
            var road = new GameObject(trails.name, typeof(MeshFilter), typeof(MeshRenderer));
            road.transform.SetParent(root, false);
            road.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Kit + "Materials/Trail.mat");
        }

        static void RebuildWestbank()
        {
            var deck = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).Single(t => t.name == "Westbank bridge deck");
            var group = Group("Westbank - ashwood bridge and approaches", new Vector2(deck.position.x, deck.position.z));
            var bridge = Add(group, "Ashwater_Bridge", 0, 0, 90);
            bridge.transform.position = new Vector3(deck.position.x, deck.position.y + deck.lossyScale.y * .5f - .13f, deck.position.z);
            bridge.transform.localScale = new Vector3(2.32f, .83f, 2.42f);
            foreach (var part in deck.parent.GetComponentsInChildren<Transform>(true))
            {
                if (!part.name.StartsWith("Westbank bridge", StringComparison.Ordinal)) continue;
                var renderer = part.GetComponent<MeshRenderer>();
                if (renderer) renderer.enabled = false;
                if (part == deck) continue; // Low-profile backup surface spans the joints in the timber deck.
                var collider = part.GetComponent<Collider>();
                if (collider) collider.enabled = false;
            }
            foreach (float side in new[] { -1f, 1f })
            {
                Add(group, "Street_Lantern", side * 13.8f, -3.8f, side * 90);
                Add(group, "Street_Lantern", side * 13.8f, 3.8f, side * 90);
                Add(group, "Moss_Rock", side * 10.6f, -6.8f, side * 110, 1.2f);
                Add(group, "Moss_Rock", side * 12.6f, 7.3f, side * 95, .85f);
                Add(group, "Stone_Wall", side * 15.4f, -4.5f, 0, .8f);
                Add(group, "Rubble", side * 13.5f, 5.9f, 120, .8f);
            }
        }

        static void VerifyBridge()
        {
            Physics.SyncTransforms();
            var bridge = root.Find("Westbank - ashwood bridge and approaches").GetComponentInChildren<MeshCollider>();
            if (!bridge || !bridge.sharedMesh) throw new InvalidOperationException("Westbank bridge is missing its deck mesh.");
            var centre = bridge.transform.position;
            for (int step = -11; step <= 11; step++)
            {
                var sample = new Vector3(centre.x + step, centre.y + 3f, centre.z);
                var ray = new Ray(sample, Vector3.down);
                if (!bridge.Raycast(ray, out _, 4f))
                    throw new InvalidOperationException($"Westbank has no new deck surface at {sample}.");
            }
            Debug.Log("Westbank bridge passed 23 deck collision samples across the new crossing.");
        }

        static (int enemies, int civilians) InstallActors(ProceduralActorCatalog catalog)
        {
            int enemies = 0, civilians = 0;
            foreach (var actor in Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include))
            {
                if (actor.trainingDummy) continue;
                string label = (actor.enemyId + " " + actor.name).ToLowerInvariant();
                if (Contains(label, "wolf", "boar", "bear", "dragon", "wyrm", "vermithrax", "spider", " rat", "beast", "raptor")) continue;
                var driver = actor.animator ? actor.animator : actor.GetComponentInChildren<Animator>(true);
                var model = Contains(label, "goblin", "cutpurse", "sapper") ? catalog.goblin :
                    Contains(label, "skeleton", "wraith", "specter", "ghost", "lich", "spirit", "ghoul", "hexer", "sprite") ? catalog.wraith :
                    Contains(label, "construct", "golem", "sentinel", "elemental", "guardian") ? catalog.sentinel :
                    actor.isBoss || actor.heavyAttacker || Contains(label, "brute", "ogre", "orc", "vanguard", "trogg") ? catalog.brute :
                    actor.ranged ? catalog.horde[1] : catalog.horde[0];
                float kitHeight = model == catalog.brute ? 2.75f : model == catalog.goblin ? 1.49f :
                    model == catalog.sentinel ? 2.54f : model == catalog.wraith ? 2.28f : 2.04f;
                var capsule = actor.GetComponent<CharacterController>();
                Install(actor.gameObject, driver, model, capsule ? capsule.height / kitHeight : 1);
                enemies++;
            }

            var seen = new HashSet<GameObject>();
            foreach (var npc in Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Include))
            {
                if (npc.transform.IsChildOf(root) || npc.GetComponent<EnemyController>()) continue;
                string label = (npc.npcName + " " + npc.roleTag).ToLowerInvariant();
                var model = Contains(label, "smith", "hammer", "tinker") ? catalog.smith :
                    Contains(label, "sentry", "guard", "captain", "rider", "scout", "outrider", "watch") ? catalog.scout :
                    catalog.merchant;
                Install(npc.gameObject, npc.GetComponentInChildren<Animator>(true), model, .93f);
                seen.Add(npc.gameObject);
                civilians++;
            }
            foreach (var npc in Object.FindObjectsByType<Merchant>(FindObjectsInactive.Include))
            {
                if (!seen.Add(npc.gameObject)) continue;
                Install(npc.gameObject, npc.GetComponentInChildren<Animator>(true), catalog.merchant, .94f);
                civilians++;
            }
            foreach (var narrator in Object.FindObjectsByType<QuestGiver>(FindObjectsInactive.Include))
            {
                if (!seen.Add(narrator.gameObject)) continue;
                Install(narrator.gameObject, narrator.GetComponentInChildren<Animator>(true), catalog.smith, .98f);
                civilians++;
            }
            return (enemies, civilians);
        }

        static bool Contains(string label, params string[] words) => words.Any(word => label.Contains(word));

        static void Install(GameObject owner, Animator animator, GameObject model, float scale)
        {
            var replacement = owner.GetComponent<ProceduralActorVisual>() ?? owner.AddComponent<ProceduralActorVisual>();
            if (replacement.StagedDisplay)
            {
                animator = replacement.Driver;
                Object.DestroyImmediate(replacement.StagedDisplay.gameObject);
            }
            replacement.Configure(animator, model, Mathf.Clamp(scale, .55f, 2.15f));
            var display = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
            display.name = "Emberford Body";
            foreach (var duplicate in display.GetComponentsInChildren<TownNPC>(true))
                Object.DestroyImmediate(duplicate);
            replacement.StageDisplay(display.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(replacement);
        }
    }
}
