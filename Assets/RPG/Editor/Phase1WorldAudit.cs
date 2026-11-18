using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class Phase1WorldAudit
    {
        const int DetailLimit = 40;

        [MenuItem("Riftbound/World/Audit Current Scene (Read Only)")]
        public static void AuditCurrentScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("World audit requires Edit Mode; no scene was changed.");
                return;
            }
            WriteReport();
        }

        public static void Run()
        {
            // The interactive command never switches scenes. Batch callers opt into this explicitly.
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use Audit Current Scene in the editor; Run is batch-only.");
            EditorSceneManager.OpenScene(WorldRegions.ScenePath, OpenSceneMode.Single);
            WriteReport();
            EditorApplication.Exit(0);
        }

        static void WriteReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== READ-ONLY WORLD AUDIT ===");
            sb.AppendLine($"UTC={DateTime.UtcNow:O} scene={EditorSceneManager.GetActiveScene().path}");
            sb.AppendLine("Scope: active loaded terrains/renderers/colliders. Candidates require review, not automatic fixes.");
            sb.AppendLine("AABB grounding is terrain-relative: raised floors, bridges, intentional burial and foliage can produce false positives.");

            var terrains = Terrain.activeTerrains;
            sb.AppendLine($"Active terrains={terrains.Length}");
            var checkedProtos = new HashSet<GameObject>();
            foreach (var t in terrains)
            {
                if (!t.terrainData) { sb.AppendLine($"MISSING terrain data: {t.name}"); continue; }
                var tc = t.GetComponent<TerrainCollider>();
                sb.AppendLine($"TERRAIN {t.name} data={AssetDatabase.GetAssetPath(t.terrainData)} origin={t.transform.position} size={t.terrainData.size} instances={t.terrainData.treeInstanceCount} colliderMatches={(tc && tc.terrainData == t.terrainData)}");
                var protos = t.terrainData.treePrototypes;
                for (int i = 0; i < protos.Length; i++)
                {
                    var p = protos[i].prefab;
                    if (!p) { sb.AppendLine($"MISSING tree prototype: {t.name}[{i}]"); continue; }
                    if (!checkedProtos.Add(p)) continue;
                    var mr = p.GetComponentInChildren<MeshRenderer>();
                    var b = mr ? mr.bounds : default;
                    bool tilted = Vector3.Dot(p.transform.up, Vector3.up) < .98f;
                    sb.AppendLine($"  Proto '{p.name}' path={AssetDatabase.GetAssetPath(p)} rootRot={p.transform.eulerAngles} rootScale={p.transform.localScale} rootTiltReview={tilted} childUp={(mr ? Vector3.Dot(mr.transform.up, Vector3.up).ToString("F2") : "n/a")} boundsSize={b.size}");
                }
            }

            int candidates = 0, checkedProps = 0, missingTerrain = 0, elevated = 0, buried = 0;
            var categories = new Dictionary<string, int>();
            var propLines = new List<string>();
            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            foreach (var mr in renderers)
            {
                string path = GetPath(mr.transform);
                string n = mr.name.ToLowerInvariant();
                if (path.Contains("mixamorig:") || path.Contains("RacialModelVisual") || n.Contains("lantern_wall")) continue;
                var b = mr.bounds;
                if (b.center.y < -10f) continue;
                string category = Category(n);
                if (category == null) continue;
                checkedProps++;
                var terrain = FindTerrain(b.center, terrains);
                if (!terrain) { missingTerrain++; continue; }
                float groundY = terrain.SampleHeight(b.center) + terrain.transform.position.y;
                float minDelta = b.min.y - groundY;
                if (minDelta > 0.55f || minDelta < -1.5f)
                {
                    candidates++;
                    if (minDelta > .55f) elevated++; else buried++;
                    categories.TryGetValue(category, out int count);
                    categories[category] = count + 1;
                    AddDetail(propLines, $"  CANDIDATE [{category}]: '{path}' pos={mr.transform.position} terrainDelta={minDelta:F2}");
                }
            }
            sb.AppendLine($"Grounding: checked renderers={checkedProps}, missing terrain={missingTerrain}, candidates={candidates} (elevated={elevated}, buried={buried}), details={propLines.Count}, omitted={candidates - propLines.Count}");
            foreach (var pair in categories) sb.AppendLine($"  CATEGORY {pair.Key}: {pair.Value}");
            foreach (var l in propLines) sb.AppendLine(l);
            AuditSeams(sb, terrains);
            foreach (var route in WorldExpansionLayout.Routes) AuditRoute(sb, route.name, route.points, terrains);
            AuditRoute(sb, "Westbank to Reaver camp", new[] { new Vector3(-285, 6, 50), RiverReaverCamp.Position }, terrains);
            Physics.SyncTransforms();
            AuditSurfaceRoute(sb, "Highspire highway", WorldExpansionLayout.Routes[0].points, terrains);
            AuditSurfaceRoute(sb, "Westbank approach", WorldExpansionLayout.Routes[11].points, terrains);
            AuditSurfaceRoute(sb, "Greenhollow road", WorldExpansionLayout.Routes[5].points, terrains);
            AuditSurfaceRoute(sb, "Westbank to Reaver camp", new[] { new Vector3(-285, 6, 50), RiverReaverCamp.Position }, terrains);
            var campTerrain = FindTerrain(RiverReaverCamp.Position, terrains);
            sb.AppendLine($"REAVER assignedData={(campTerrain ? AssetDatabase.GetAssetPath(campTerrain.terrainData) : "MISSING TERRAIN")} expectedFolder=Assets/RPG/Terrain/ReaverCamp/");
            sb.AppendLine("NOT VERIFIED: rendered orientation, controller traversal, navigation, walkability and terrain tree-instance collision. Surface/clearance samples are heuristics, not a gameplay pass. No scene/assets saved.");

            Directory.CreateDirectory(".code-check");
            File.WriteAllText(".code-check/phase1-world-audit.txt", sb.ToString());
            Debug.Log("Read-only world audit written to .code-check/phase1-world-audit.txt. Review candidates; this is not a traversal pass.");
        }

        static void AddDetail(List<string> lines, string value)
        {
            if (lines.Count < DetailLimit) lines.Add(value);
        }

        static string Category(string name)
        {
            if (name.Contains("fallen") || name.Contains("stump") || name.Contains("log_")) return "intentional-log-review";
            if (name.Contains("tree") || name.Contains("bush") || name.Contains("pine") || name.Contains("birch")) return "vegetation";
            if (name.Contains("rock") || name.Contains("boulder")) return "rock-intentional-burial-review";
            if (name.Contains("crate") || name.Contains("barrel") || name.Contains("chest") || name.Contains("tent") || name.Contains("cart") || name.Contains("wagon") || name.Contains("fence") || name.Contains("bench") || name.Contains("table")) return "ground-prop-or-supported-furniture";
            return null;
        }

        static Terrain FindTerrain(Vector3 p, Terrain[] terrains)
        {
            foreach (var t in terrains)
            {
                if (!t || !t.terrainData) continue;
                var o = t.transform.position; var s = t.terrainData.size;
                if (p.x >= o.x && p.x <= o.x + s.x && p.z >= o.z && p.z <= o.z + s.z) return t;
            }
            return null;
        }

        static void AuditSeams(StringBuilder sb, Terrain[] terrains)
        {
            int seams = 0; float worst = 0;
            for (int i = 0; i < terrains.Length; i++)
                for (int j = i + 1; j < terrains.Length; j++)
                {
                    var a = terrains[i]; var b = terrains[j];
                    if (!a.terrainData || !b.terrainData) continue;
                    var ao = a.transform.position; var bo = b.transform.position;
                    var size = a.terrainData.size; var bs = b.terrainData.size;
                    bool xEdge = Mathf.Abs(ao.x + size.x - bo.x) < .01f || Mathf.Abs(bo.x + bs.x - ao.x) < .01f;
                    bool zEdge = Mathf.Abs(ao.z + size.z - bo.z) < .01f || Mathf.Abs(bo.z + bs.z - ao.z) < .01f;
                    float lo = xEdge ? Mathf.Max(ao.z, bo.z) : Mathf.Max(ao.x, bo.x);
                    float hi = xEdge ? Mathf.Min(ao.z + size.z, bo.z + bs.z) : Mathf.Min(ao.x + size.x, bo.x + bs.x);
                    if ((!xEdge && !zEdge) || hi <= lo) continue;
                    float edge = xEdge ? Mathf.Max(ao.x, bo.x) : Mathf.Max(ao.z, bo.z);
                    float maxDelta = 0;
                    for (int k = 0; k <= 128; k++)
                    {
                        float along = Mathf.Lerp(lo, hi, k / 128f);
                        var p = xEdge ? new Vector3(edge, 0, along) : new Vector3(along, 0, edge);
                        maxDelta = Mathf.Max(maxDelta, Mathf.Abs(a.SampleHeight(p) + ao.y - b.SampleHeight(p) - bo.y));
                    }
                    seams++; worst = Mathf.Max(worst, maxDelta);
                    sb.AppendLine($"SEAM {a.name}/{b.name}: sampledMaxDelta={maxDelta:F4}m");
                }
            sb.AppendLine($"SEAMS checked={seams}, worstSampledDelta={worst:F4}m; 129 samples per shared edge, not exhaustive.");
        }

        static void AuditRoute(StringBuilder sb, string name, Vector3[] points, Terrain[] terrains, bool checkObstacles = true)
        {
            float length = 0, grade = 0; int missing = 0, samples = 0, walkObstacles = 0, mountObstacles = 0;
            Vector3 worstAt = default;
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                float run = new Vector2(b.x - a.x, b.z - a.z).magnitude;
                length += run;
                int steps = Mathf.Max(1, Mathf.CeilToInt(run / 2f));
                Vector3? previous = null;
                for (int k = 0; k <= steps; k++)
                {
                    Vector3 p = Vector3.Lerp(a, b, k / (float)steps);
                    samples++;
                    var t = FindTerrain(p, terrains);
                    if (!t) { missing++; previous = null; continue; }
                    p.y = t.SampleHeight(p) + t.transform.position.y;
                    if (previous.HasValue)
                    {
                        Vector3 d = p - previous.Value;
                        float value = Mathf.Atan2(Mathf.Abs(d.y), new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                        if (value > grade) { grade = value; worstAt = p; }
                    }
                    previous = p;
                    if (checkObstacles && HasStaticObstacle(p, .45f, 1.9f)) walkObstacles++;
                    if (checkObstacles && HasStaticObstacle(p, .62f, 3.6f)) mountObstacles++;
                }
            }
            string overlaps = checkObstacles ? $"{walkObstacles}/{mountObstacles}" : "NOT RUN (terrain-only snapshot)";
            sb.AppendLine($"ROUTE {name}: horizontalLength={length:F1}m samples={samples} missingTerrain={missing} maxTerrainGrade={grade:F1}deg at={worstAt} staticOverlapSamples(walk/mount)={overlaps}");
            sb.AppendLine("  Geometry candidates only: terrain samples ignore bridge/mesh-road height; walk capsule is estimated, mount uses current .62m radius/3.6m height; excludes triggers/dynamic or nonstatic objects and does not sweep between samples.");
        }

        // Run only in the isolated validation checkout after copying terrain binaries into this new folder.
        // Their metadata is not copied: imported snapshot assets must have their own GUIDs.
        public static void AuditTerrainSnapshot()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Terrain snapshot audit is batch-only.");
            const string folder = "Assets/WorldAuditSnapshot/";
            var objects = new List<GameObject>(); var terrains = new List<Terrain>();
            var sb = new StringBuilder("TERRAIN SNAPSHOT AUDIT: source hashes/assignments in world-audit-scene-references.json and snapshot manifest.\n");
            sb.AppendLine("No source scene loaded/saved; no scene colliders or rendered-tree verification. Heights/grades only.");
            try
            {
                for (int z = 0; z < 2; z++)
                    for (int x = 0; x < 2; x++)
                    {
                        string path = folder + $"World tile {x}-{z}.asset";
                        var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                        if (!data) throw new InvalidOperationException("Missing snapshot: " + path);
                        var go = Terrain.CreateTerrainGameObject(data); objects.Add(go);
                        go.name = $"Snapshot {x}-{z}";
                        go.transform.position = new Vector3(-320 + x * 384, -8, -160 + z * 384);
                        terrains.Add(go.GetComponent<Terrain>());
                    }
                var tiles = terrains.ToArray();
                AuditSeams(sb, tiles);
                foreach (var route in WorldExpansionLayout.Routes) AuditRoute(sb, route.name, route.points, tiles, false);
                var approach = new[] { new Vector3(-285, 6, 50), RiverReaverCamp.Position };
                AuditRoute(sb, "Reaver CURRENT assigned tile", approach, tiles, false);
                var protectedData = AssetDatabase.LoadAssetAtPath<TerrainData>(folder + "Reaver approach.asset");
                if (protectedData)
                {
                    var go = Terrain.CreateTerrainGameObject(protectedData); objects.Add(go);
                    go.transform.position = new Vector3(-320, -8, -160);
                    AuditRoute(sb, "Reaver UNASSIGNED protected asset (comparison only)", approach, new[] { go.GetComponent<Terrain>() }, false);
                    CompareProtectedTile(sb, tiles[0].terrainData, protectedData);
                }
                Directory.CreateDirectory(".code-check");
                File.WriteAllText(".code-check/world-audit-terrain-snapshot.txt", sb.ToString());
            }
            finally { foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go); }
            EditorApplication.Exit(0);
        }

        static void CompareProtectedTile(StringBuilder sb, TerrainData current, TerrainData proposed)
        {
            sb.AppendLine($"REAVER COMPARE sizeEqual={current.size == proposed.size} resolution={current.heightmapResolution}/{proposed.heightmapResolution} treeInstances={current.treeInstanceCount}/{proposed.treeInstanceCount} treePrototypes={current.treePrototypes.Length}/{proposed.treePrototypes.Length} layers={current.terrainLayers.Length}/{proposed.terrainLayers.Length}");
            if (current.heightmapResolution != proposed.heightmapResolution || current.size != proposed.size)
            {
                sb.AppendLine("REAVER COMPARE incompatible height grids; no direct replacement recommendation.");
                return;
            }
            int n = current.heightmapResolution, changed = 0, outside = 0;
            float maxDelta = 0;
            var a = current.GetHeights(0, 0, n, n); var b = proposed.GetHeights(0, 0, n, n);
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float delta = Mathf.Abs(a[z, x] - b[z, x]) * current.size.y;
                    maxDelta = Mathf.Max(maxDelta, delta);
                    if (delta <= .01f) continue;
                    changed++;
                    var p = new Vector2(-320 + x * current.size.x / (n - 1), -160 + z * current.size.z / (n - 1));
                    if (p.x < -300 || p.x > -266 || p.y < 41 || p.y > 95) outside++;
                }
            sb.AppendLine($"REAVER COMPARE heightSamplesChangedOver1cm={changed} outsideExpectedClearingBounds={outside} maxHeightDelta={maxDelta:F3}m");
            sb.AppendLine("Comparison is not replacement approval: alphamaps/details/tree transforms/material settings and manual edits still require review. Expected clearing bounds are a conservative footprint from the existing authoring tool.");
        }

        static void AuditSurfaceRoute(StringBuilder sb, string name, Vector3[] points, Terrain[] terrains)
        {
            int samples = 0, missing = 0, supported = 0, walkBlocked = 0, mountBlocked = 0;
            float maxGrade = 0; Vector3 worstAt = default; var details = new List<string>();
            for (int i = 1; i < points.Length; i++)
            {
                Vector3 a = points[i - 1], b = points[i];
                Vector3 delta = b - a; delta.y = 0;
                Vector3 side = Vector3.Cross(Vector3.up, delta.normalized);
                int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude));
                foreach (float offset in new[] { -.7f, 0f, .7f })
                {
                    Vector3? previous = null;
                    for (int k = 0; k <= steps; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / (float)steps) + side * offset;
                        samples++;
                        if (!TrySurface(p, terrains, out Vector3 ground, out Collider support))
                        { missing++; previous = null; continue; }
                        if (support) supported++;
                        if (previous.HasValue)
                        {
                            var d = ground - previous.Value;
                            float grade = Mathf.Atan2(Mathf.Abs(d.y), new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
                            if (grade > maxGrade) { maxGrade = grade; worstAt = ground; }
                        }
                        previous = ground;
                        string walkHit = ClearanceObstacle(ground, .45f, 1.9f, support);
                        string mountHit = ClearanceObstacle(ground, .62f, 3.6f, support);
                        if (walkHit != null) walkBlocked++;
                        if (mountHit != null) mountBlocked++;
                        if (walkHit != null || mountHit != null) AddDetail(details, $"  SURFACE CANDIDATE at={ground} side={offset:F1} walk={walkHit ?? "none"} mount={mountHit ?? "none"}");
                    }
                }
            }
            sb.AppendLine($"SURFACE ROUTE {name}: samples={samples} missing={missing} meshSupportSamples={supported} maxSampledGrade={maxGrade:F1}deg at={worstAt} overlapSamples(walk/mount)={walkBlocked}/{mountBlocked} examples={details.Count}");
            foreach (string detail in details) sb.AppendLine(detail);
            sb.AppendLine("  Samples at <=1m and side offsets +/-0.7m; mesh support is limited to named bridge/road/ground surfaces within 3m of authored height. Terrain fallback can include riverbanks. No swept controller/camera or NavMesh check.");
        }

        static bool TrySurface(Vector3 reference, Terrain[] terrains, out Vector3 ground, out Collider support)
        {
            ground = reference; support = null;
            var terrain = FindTerrain(reference, terrains);
            bool found = terrain != null;
            if (found) ground.y = terrain.SampleHeight(reference) + terrain.transform.position.y;
            // Restrict the vertical search to road height so a roof cannot become the sampled ground.
            foreach (var hit in Physics.RaycastAll(reference + Vector3.up * 3f, Vector3.down, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                var c = hit.collider;
                if (c is TerrainCollider || c.attachedRigidbody || c is CharacterController || hit.normal.y < .65f) continue;
                string leaf = c.name.ToLowerInvariant();
                if (leaf.Contains("rail") || leaf.Contains("fence") || leaf.Contains("roof") || leaf.Contains("wall")) continue;
                string path = GetPath(c.transform).ToLowerInvariant();
                if (!path.Contains("bridge") && !path.Contains("road") && !path.Contains("paving") && !path.Contains("ground")) continue;
                if (found && hit.point.y <= ground.y + .02f) continue;
                found = true; ground = hit.point; support = c;
            }
            return found;
        }

        static string ClearanceObstacle(Vector3 ground, float radius, float height, Collider support)
        {
            foreach (var c in Physics.OverlapCapsule(ground + Vector3.up * (radius + .12f), ground + Vector3.up * (height - radius + .12f), radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c == support || c is TerrainCollider || c is CharacterController || c.attachedRigidbody || c.GetComponentInParent<Combatant>()) continue;
                return GetPath(c.transform);
            }
            return null;
        }

        static bool HasStaticObstacle(Vector3 p, float radius, float height)
        {
            var bottom = p + Vector3.up * (radius + .1f);
            var top = p + Vector3.up * (height - radius + .1f);
            foreach (var c in Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore))
                if (!(c is TerrainCollider) && !c.attachedRigidbody && c.gameObject.isStatic) return true;
            return false;
        }

        public static void VerifyFixtures()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Fixture verification is batch-only.");
            var lines = new List<string>(); int total = 0;
            for (int i = 0; i < 65; i++) { total++; AddDetail(lines, i.ToString()); }
            if (total != 65 || lines.Count != 40) throw new Exception("Audit truncation changed the total.");
            if (Category("fallen_tree_log") != "intentional-log-review" || Category("bush_large") != "vegetation"
                || Category("iron chest band") != "ground-prop-or-supported-furniture") throw new Exception("Candidate classification failed.");
            if (FindTerrain(Vector3.zero, new Terrain[0]) != null) throw new Exception("Missing terrain must remain unknown.");
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32, 10, 32) };
            var go = Terrain.CreateTerrainGameObject(data);
            try
            {
                var terrain = go.GetComponent<Terrain>();
                var sb = new StringBuilder();
                AuditRoute(sb, "flat fixture", new[] { Vector3.zero, new Vector3(8, 0, 0) }, new[] { terrain });
                if (!sb.ToString().Contains("missingTerrain=0 maxTerrainGrade=0.0deg")) throw new Exception("Flat route coverage/grade failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(data); }
            VerifyBridgeFixture();
            Directory.CreateDirectory(".code-check");
            File.WriteAllText(".code-check/world-audit-fixtures.txt", "PASS: uncapped total, bounded details, deliberate-log/vegetation classification, missing terrain, flat-route coverage/grade, bridge support above terrain, nonstatic overhead obstacle, walk versus mount headroom. Synthetic data only.\n");
            EditorApplication.Exit(0);
        }

        static void VerifyBridgeFixture()
        {
            var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32, 10, 32) };
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                terrainGo.transform.position = new Vector3(5000, 0, 5000);
                deck.name = "Audit Bridge Deck";
                deck.transform.position = new Vector3(5004, 1, 5004);
                deck.transform.localScale = new Vector3(4, .4f, 6);
                beam.name = "Audit Overhead Beam";
                beam.transform.position = new Vector3(5004, 4, 5004);
                beam.transform.localScale = new Vector3(4, 1, 1);
                Physics.SyncTransforms();
                if (!TrySurface(new Vector3(5004, 1, 5004), new[] { terrainGo.GetComponent<Terrain>() }, out Vector3 ground, out Collider support)
                    || support != deck.GetComponent<Collider>() || Mathf.Abs(ground.y - 1.2f) > .01f)
                    throw new Exception("Bridge deck must override riverbed/terrain sampling.");
                if (ClearanceObstacle(ground, .45f, 1.9f, support) != null)
                    throw new Exception("Walk capsule falsely blocked by safe overhead beam/support.");
                if (ClearanceObstacle(ground, .62f, 3.6f, support) == null)
                    throw new Exception("Mount capsule missed nonstatic overhead beam.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(beam); UnityEngine.Object.DestroyImmediate(deck);
                UnityEngine.Object.DestroyImmediate(terrainGo); UnityEngine.Object.DestroyImmediate(data);
            }
        }

        static string GetPath(Transform t)
        {
            var stack = new List<string>();
            while (t != null) { stack.Add(t.name); t = t.parent; }
            stack.Reverse();
            return string.Join("/", stack);
        }

        public static void ReviewReadOnly()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Review is batch-only.");
            try
            {
                PreviewReaverGrading();
                EditorSceneManager.OpenScene(WorldRegions.ScenePath, OpenSceneMode.Single);
                WorldPresentationSetup.RenderPreview("WorldAudit-Westbank", new Vector3(-237, 22, 27), new Vector3(-262, 6, 50), false);
                WorldPresentationSetup.RenderPreview("WorldAudit-Reaver", new Vector3(-262, 27, 47), new Vector3(-284, 13, 72), false);
                WorldPresentationSetup.RenderPreview("WorldAudit-Greenhollow", new Vector3(-14, 27, 368), new Vector3(-40, 13, 400), false);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(".code-check");
                File.WriteAllText(".code-check/world-audit-review-error.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        static void PreviewReaverGrading()
        {
            const string currentPath = "Assets/RPG/Terrain/WorldExpansion/World tile 0-0.asset";
            const string gradedPath = "Assets/RPG/Terrain/ReaverCamp/Reaver approach.asset";
            var current = AssetDatabase.LoadAssetAtPath<TerrainData>(currentPath);
            var graded = AssetDatabase.LoadAssetAtPath<TerrainData>(gradedPath);
            if (!current || !graded || current.heightmapResolution != graded.heightmapResolution || current.size != graded.size)
                throw new InvalidOperationException("Current and graded terrain grids must be present and compatible.");
            var preview = UnityEngine.Object.Instantiate(current);
            GameObject terrainGo = null;
            try
            {
                int n = current.heightmapResolution, changed = 0, treesAffected = 0, treesResnapped = 0;
                var original = current.GetHeights(0, 0, n, n);
                var proposed = graded.GetHeights(0, 0, n, n);
                var heights = (float[,])original.Clone();
                var patch = new StringBuilder("xIndex,zIndex,worldX,worldZ,currentNormalized,proposedNormalized\n");
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        float wx = -320 + x * current.size.x / (n - 1), wz = -160 + z * current.size.z / (n - 1);
                        if (wx < -300 || wx > -266 || wz < 41 || wz > 95) continue;
                        if (Mathf.Abs(original[z, x] - proposed[z, x]) * current.size.y <= .01f) continue;
                        heights[z, x] = proposed[z, x]; changed++;
                        patch.AppendLine(FormattableString.Invariant($"{x},{z},{wx:F3},{wz:F3},{original[z, x]:R},{proposed[z, x]:R}"));
                    }
                preview.SetHeights(0, 0, heights);
                var beforeTrees = current.treeInstances; var afterTrees = preview.treeInstances;
                if (beforeTrees.Length != afterTrees.Length || current.treePrototypes.Length != preview.treePrototypes.Length)
                    throw new Exception("Preview lost vegetation.");
                for (int i = 0; i < beforeTrees.Length; i++)
                {
                    var a = beforeTrees[i]; var b = afterTrees[i];
                    // SetHeights may re-ground tree Y and update internal tree metadata.
                    // Compare the authored fields explicitly, rather than ValueType.Equals.
                    if (a.position.x != b.position.x || a.position.z != b.position.z || a.prototypeIndex != b.prototypeIndex
                        || a.rotation != b.rotation || a.widthScale != b.widthScale || a.heightScale != b.heightScale
                        || !a.color.Equals(b.color) || !a.lightmapColor.Equals(b.lightmapColor))
                        throw new Exception($"Preview changed authored tree fields at index {i}: before={JsonUtility.ToJson(a)} after={JsonUtility.ToJson(b)}");
                    if (a.position.y != b.position.y) treesResnapped++;
                    int x = Mathf.Clamp(Mathf.RoundToInt(beforeTrees[i].position.x * (n - 1)), 0, n - 1);
                    int z = Mathf.Clamp(Mathf.RoundToInt(beforeTrees[i].position.z * (n - 1)), 0, n - 1);
                    if (Mathf.Abs(heights[z, x] - original[z, x]) * current.size.y > .1f) treesAffected++;
                }
                int outsideChanged = 0;
                var result = preview.GetHeights(0, 0, n, n);
                var sourceAfter = current.GetHeights(0, 0, n, n);
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        if (sourceAfter[z, x] != original[z, x]) throw new Exception("Source height data changed.");
                        float wx = -320 + x * current.size.x / (n - 1), wz = -160 + z * current.size.z / (n - 1);
                        if ((wx < -300 || wx > -266 || wz < 41 || wz > 95) && result[z, x] != original[z, x]) outsideChanged++;
                    }
                if (outsideChanged != 0) throw new Exception("Preview altered heights outside the local footprint.");
                var sb = new StringBuilder($"PREVIEW ONLY: in-memory current-tile clone, no asset/scene saved.\nChanged samples={changed}; outside-footprint changes={outsideChanged}; tree instances preserved={afterTrees.Length}; tree prototypes preserved={preview.treePrototypes.Length}; trees requiring grounding review (>0.1m nearest-grid height change)={treesAffected}; Unity re-grounded tree Y count={treesResnapped}\nAuthored tree X/Z, prototype, rotation, width/height scale and colors compared explicitly.\n");
                terrainGo = Terrain.CreateTerrainGameObject(preview);
                terrainGo.transform.position = new Vector3(-320, -8, -160);
                AuditRoute(sb, "Reaver LOCAL REPAIR PREVIEW", new[] { new Vector3(-285, 6, 50), RiverReaverCamp.Position }, new[] { terrainGo.GetComponent<Terrain>() }, false);
                sb.AppendLine("Layer/detail/paint content not edited. No scene assignment, collider/actor/nav changes or gameplay verification. Patch is a review artifact, not an apply command; source hashes must be rechecked before any approved edit.");
                Directory.CreateDirectory(".code-check");
                File.WriteAllText(".code-check/reaver-local-grading-preview.txt", sb.ToString());
                File.WriteAllText(".code-check/reaver-local-grading-preview.csv", patch.ToString());
            }
            finally
            {
                if (terrainGo) UnityEngine.Object.DestroyImmediate(terrainGo);
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }
    }
}
