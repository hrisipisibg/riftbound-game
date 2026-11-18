using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class WorldProductionAudit
    {
        internal const string Evidence = ".code-check/world-production/";

        internal static void Open()
        {
            if (!Application.isBatchMode || !File.Exists(".coordination/world-validation-context.md"))
                throw new InvalidOperationException("Isolated world-production batch only.");
            Directory.CreateDirectory(Evidence);
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            Physics.SyncTransforms();
        }

        internal static WorldTraversalVerification.Route[] Routes()
        {
            var existing = WorldTraversalVerification.Routes;
            var north = WorldExpansionLayout.Routes.Single(r => r.name == "Greenhollow road").points;
            var arrival = SettlementRoutePass.ReadPath("Greenhollow arrival footway");
            if (arrival == null) throw new InvalidOperationException("Reviewed Greenhollow footway missing.");
            var fullNorth = new[] { Vector3.zero }.Concat(north.TakeWhile(p => p.z < 368)).Concat(arrival).ToArray();
            return existing.Concat(new[]
            {
                new WorldTraversalVerification.Route("Complete Emberwatch to Greenhollow", fullNorth),
                new WorldTraversalVerification.Route("Bridge south lane", new Vector3(-278,6,48.5f),new Vector3(-235,6,48.5f)),
                new WorldTraversalVerification.Route("Bridge north lane", new Vector3(-278,6,51.5f),new Vector3(-235,6,51.5f)),
                new WorldTraversalVerification.Route("Willow Rest interaction approach", new Vector3(-285,6,50),new Vector3(-288,7,51),new Vector3(-288.7f,7.1f,52))
            }).ToArray();
        }

        public static void Run()
        {
            try
            {
                Open();
                var report = new StringBuilder($"WORLD PRODUCTION AUDIT UTC={DateTime.UtcNow:O}\n");
                report.AppendLine("Read only; no scene or asset saved. Geometry/controller/camera-shape diagnostics, not normal input or rendered acceptance.");
                AuditAuthoring(report, "Highspire north footway", "Westbank approach");
                AuditAuthoring(report, "Greenhollow arrival footway", "Greenhollow road");
                AuditTrees(report);
                var player = Object.FindFirstObjectByType<PlayerController>();
                var source = player.GetComponent<CharacterController>();
                report.AppendLine($"SOURCE WALK height={source.height} radius={source.radius} slope={source.slopeLimit} step={source.stepOffset}");
                foreach (var route in Routes())
                {
                    foreach (bool mounted in new[] { false, true })
                    {
                        foreach (bool reverse in new[] { false, true })
                            WorldTraversalVerification.Traverse(route, mounted, reverse, source, report);
                        AuditCamera(route, mounted, report);
                    }
                }
                File.WriteAllText(Evidence + "audit.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence + "audit-error.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        public static void SyncReviewedMapRoutes()
        {
            try
            {
                Open();
                var maps=Object.FindObjectsByType<WorldMapRoutes>(FindObjectsSortMode.None);
                if(maps.Length!=1) throw new InvalidOperationException("Expected exactly one authored map-route container.");
                var map=maps[0];
                var report=new StringBuilder($"REVIEWED MAP ROUTE SYNC UTC={DateTime.UtcNow:O}\n");
                var changes=new Dictionary<WorldMapRoutes.Route,Vector3[]>();
                foreach(string name in new[] {"Westbank approach","Greenhollow road"})
                {
                    var current=map.routes.Single(r=>r.name==name);
                    var corrected=WorldExpansionLayout.Routes.Single(r=>r.name==name).points;
                    if(current.points.SequenceEqual(corrected)) throw new InvalidOperationException("Route already synchronized; inspect rather than reapply: "+name);
                    report.AppendLine(name+" BEFORE "+string.Join("; ",current.points.Select(p=>p.ToString("F2"))));
                    report.AppendLine(name+" AFTER "+string.Join("; ",corrected.Select(p=>p.ToString("F2"))));
                    changes.Add(current,(Vector3[])corrected.Clone());
                }
                File.Copy(WorldRegions.ScenePath,Evidence+"before-map-route-sync.unity",false);
                foreach(var change in changes) change.Key.points=change.Value;
                EditorUtility.SetDirty(map);
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                report.AppendLine("Only two existing route point arrays updated. Container/route names, all unrelated branches and persistent actors preserved. No terrain paint, heights, vegetation or runtime script changed.");
                File.WriteAllText(Evidence+"map-route-sync.txt",report.ToString());
                EditorApplication.Exit(0);
            }
            catch(Exception e)
            {
                Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence+"map-route-sync-error.txt",e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        public static void BuildVerifiedWorld()
        {
            BuildToFolder("Builds/WorldProduction01","build");
        }

        public static void BuildSettlementWorld()
        {
            BuildToFolder("Builds/WorldProduction02","build-02");
        }

        public static void BuildCombinedWorld()
        {
            BuildToFolder("Builds/RiftboundCombined01","build-combined");
        }

        public static void BuildCombinedWorld02()
        {
            BuildToFolder("Builds/RiftboundCombined02","build-combined-02");
        }

        static void BuildToFolder(string folder,string receipt)
        {
            try
            {
                Open();
                string output=folder+"/Riftbound.exe";
                if(Directory.Exists(folder)) throw new InvalidOperationException("Production output already exists; do not overwrite.");
                var terrain=WorldRegions.TerrainAt(RiverReaverCamp.Position);
                var original=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/RPG/Terrain/ReaverCamp/Reaver approach preserved final.asset");
                var report=new StringBuilder($"PRODUCTION BUILD PROTECTION UTC={DateTime.UtcNow:O}\n");
                if(terrain.terrainData.treeInstanceCount!=813 || terrain.terrainData.treePrototypes.Length!=15
                    || terrain.GetComponent<TerrainCollider>().terrainData!=terrain.terrainData)
                    throw new InvalidOperationException("Protected terrain reference/vegetation mismatch.");
                ReaverRepairPreparation.VerifyPreservedData(original,terrain.terrainData,report,new Rect(-274,45,37,10));
                float grade=ReaverCampTerrainSetup.CheckApproach();
                if(grade>35) throw new InvalidOperationException("Reaver grade regressed.");
                report.AppendLine($"PASS Terrain/Collider, 15 prototypes, grade={grade:F2}");
                Phase1WorldAudit.AuditCurrentScene();
                File.WriteAllText(Evidence+receipt+"-protection.txt",report.ToString());
                var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[] {WorldRegions.ScenePath},locationPathName=output,
                    target=BuildTarget.StandaloneWindows64,options=BuildOptions.None
                });
                File.WriteAllText(Evidence+receipt+"-result.txt",$"UTC={DateTime.UtcNow:O}\nPath={output}\nResult={build.summary.result}\nErrors={build.summary.totalErrors}\nWarnings={build.summary.totalWarnings}\nDuration={build.summary.totalTime}\nBytes={build.summary.totalSize}\n");
                EditorApplication.Exit(build.summary.result==UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
            }
            catch(Exception e)
            {
                Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence+receipt+"-error.txt",e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        public static void MatchHighspireFootwaySurface()
        {
            try
            {
                Open();
                const string folder="Assets/RPG/Terrain/WorldProduction";
                const string path=folder+"/Highspire footway cobblestone.mat";
                if(File.Exists(path)) throw new InvalidOperationException("Reviewed surface variant already exists; inspect rather than overwrite.");
                var renderer=GameObject.Find("Highspire north footway").GetComponent<MeshRenderer>();
                if(AssetDatabase.GetAssetPath(renderer.sharedMaterial)!="Assets/RPG/Materials/Road.mat")
                    throw new InvalidOperationException("Footway material changed since review.");
                var layer=AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RPG/Terrain/WorldExpansionLayers/Cobblestone highway.terrainlayer");
                var shader=Shader.Find("Universal Render Pipeline/Lit");
                if(!layer || !layer.diffuseTexture || !shader) throw new InvalidOperationException("Existing cobblestone surface inputs missing.");
                Directory.CreateDirectory(folder); AssetDatabase.Refresh();
                var material=new Material(shader) {name="Highspire footway cobblestone"};
                material.SetTexture("_BaseMap",layer.diffuseTexture); material.SetColor("_BaseColor",Color.white);
                material.SetTextureScale("_BaseMap",new Vector2(5/layer.tileSize.x,5/layer.tileSize.y));
                material.SetFloat("_Smoothness",layer.smoothness); material.SetFloat("_Metallic",layer.metallic);
                if(layer.normalMapTexture)
                {
                    material.SetTexture("_BumpMap",layer.normalMapTexture); material.SetFloat("_BumpScale",layer.normalScale);
                    material.EnableKeyword("_NORMALMAP");
                }
                AssetDatabase.CreateAsset(material,path); renderer.sharedMaterial=material;
                EditorUtility.SetDirty(renderer); AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                File.WriteAllText(Evidence+"footway-surface.txt",$"UTC={DateTime.UtcNow:O}\nChanged only Highspire footway material reference, new protected material={path}\nUses existing layer diffuse/normal, tileSize={layer.tileSize}, roughness/normal scale; no original material or terrain paint changed. Before GPU image showed plain dark strip; recapture required.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e)
            {
                Directory.CreateDirectory(Evidence); File.WriteAllText(Evidence+"footway-surface-error.txt",e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        static void AuditAuthoring(StringBuilder report, string footway, string routeName)
        {
            var path = SettlementRoutePass.ReadPath(footway);
            var authored = WorldExpansionLayout.Routes.Single(r => r.name == routeName).points;
            float maxDistance = path.Max(p => DistanceToRoute(p, authored));
            report.AppendLine($"AUTHORING {routeName}: footwayMaxHorizontalDeviation={maxDistance:F2}m; no terrain paint changed.");
            var maps=Object.FindObjectsByType<WorldMapRoutes>(FindObjectsSortMode.None);
            var configured=maps.Single().routes.Single(r=>r.name==routeName).points;
            report.AppendLine($"MAP DATA {routeName}: matchesAuthoring={configured.SequenceEqual(authored)}, pointCount={configured.Length}");
            foreach (var p in new[] { path[0], path[path.Length / 2], path[path.Length - 1] })
            {
                var terrain = WorldRegions.TerrainAt(p); var data = terrain.terrainData; var local = p - terrain.transform.position;
                int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
                var weights = data.GetAlphamaps(x, z, 1, 1); int largest = 0;
                for (int i = 1; i < data.alphamapLayers; i++) if (weights[0, 0, i] > weights[0, 0, largest]) largest = i;
                report.AppendLine($"  PAINT SAMPLE {p:F2}: dominant={data.terrainLayers[largest].name} weight={weights[0,0,largest]:F2}; surface mesh overlays do not erase old paint.");
            }
        }

        static void AuditTrees(StringBuilder report)
        {
            var before = AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/RPG/Terrain/WorldExpansion/World tile 0-0.asset");
            var terrain = WorldRegions.TerrainAt(RiverReaverCamp.Position); var current = terrain.terrainData;
            int affected = 0, trees = 0, shrubs = 0;
            var route = new[] { new Vector3(-285,0,50), RiverReaverCamp.Position };
            var instances = current.treeInstances;
            for (int i = 0; i < instances.Length; i++)
            {
                var tree = instances[i]; float height = current.GetInterpolatedHeight(tree.position.x, tree.position.z);
                float delta = height - before.GetInterpolatedHeight(tree.position.x, tree.position.z);
                if (Mathf.Abs(delta) <= .1f) continue;
                affected++;
                var prefab = current.treePrototypes[tree.prototypeIndex].prefab;
                bool shrub = prefab.name.IndexOf("Bush", StringComparison.OrdinalIgnoreCase) >= 0;
                if (shrub) shrubs++; else trees++;
                var p = terrain.transform.position + Vector3.Scale(tree.position, current.size);
                float gap = p.y - (terrain.transform.position.y + height);
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                float rootUp = Vector3.Dot(prefab.transform.up, Vector3.up);
                report.AppendLine($"TREE instance={i} prefab={prefab.name} kind={(shrub ? "shrub-intentional-lean-review" : "upright-tree-review")} x={p.x:F2} z={p.z:F2} rootUp={rootUp:F3} baseOffset={gap:F3}m corridorCentreDistance={DistanceToRoute(p,route):F2}m renderers={renderers.Length} prefabColliders={prefab.GetComponentsInChildren<Collider>(true).Length}; no proven defect from transform alone.");
            }
            report.AppendLine($"TREE TOTAL affected={affected}, tree={trees}, shrub={shrubs}; native rendering/collision still requires per-instance evidence. No instances or prefabs changed.");
            int logs = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r =>
                r.name.IndexOf("fallen", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("log_", StringComparison.OrdinalIgnoreCase) >= 0 || r.name.IndexOf("stump", StringComparison.OrdinalIgnoreCase) >= 0);
            report.AppendLine($"Intentional-log/stump renderer-name candidates={logs}; names are a review classification, not grounds for upright rotation.");
        }

        static void AuditCamera(WorldTraversalVerification.Route route, bool mounted, StringBuilder report)
        {
            int total = 0, compressed = 0, finalOverlap = 0; float shortest = 7;
            var examples = new List<string>();
            for (int segment = 1; segment < route.points.Length; segment++)
            {
                var a = route.points[segment - 1]; var b = route.points[segment];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 4));
                for (int i = 0; i < steps; i++)
                {
                    var point = WorldTraversalVerification.Surface(Vector3.Lerp(a,b,i/(float)steps));
                    for (int yaw = 0; yaw < 360; yaw += 45)
                    {
                        total++;
                        var pivot = point + Vector3.up * (mounted ? 2.4f : 1.6f);
                        var backward = Quaternion.Euler(23,yaw,0) * Vector3.back;
                        float distance = 7;
                        if (Physics.SphereCast(pivot,.2f,backward,out var hit,7,~(1<<2),QueryTriggerInteraction.Ignore))
                            distance = Mathf.Max(.6f,hit.distance-.15f);
                        shortest = Mathf.Min(shortest,distance);
                        if (distance < 1.5f)
                        {
                            compressed++;
                            if (examples.Count < 3) examples.Add($"at={point:F2} yaw={yaw} distance={distance:F2}");
                        }
                        if (Physics.CheckSphere(pivot+backward*distance,.2f,~(1<<2),QueryTriggerInteraction.Ignore)) finalOverlap++;
                    }
                }
            }
            report.AppendLine($"CAMERA SHAPE {route.name} mode={(mounted ? "mount" : "walk")} samples={total} compressedBelow1.5m={compressed} finalOverlap={finalOverlap} shortest={shortest:F2}m; 8 yaw samples/pitch23, geometry estimate, not actual OrbitCamera input/occlusion acceptance. {string.Join("; ",examples)}");
        }

        static float DistanceToRoute(Vector3 point, Vector3[] points)
        {
            var p = new Vector2(point.x, point.z); float closest = float.PositiveInfinity;
            for (int i = 1; i < points.Length; i++)
            {
                var a = new Vector2(points[i-1].x,points[i-1].z); var line = new Vector2(points[i].x,points[i].z)-a;
                float t = line.sqrMagnitude > 0 ? Mathf.Clamp01(Vector2.Dot(p-a,line)/line.sqrMagnitude) : 0;
                closest = Mathf.Min(closest,Vector2.Distance(p,a+line*t));
            }
            return closest;
        }
    }
}
