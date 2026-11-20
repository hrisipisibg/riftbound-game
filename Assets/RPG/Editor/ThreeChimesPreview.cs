using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class ThreeChimesPreview
    {
        const string Art = "Assets/RPG/Art/ThreeChimes/";
        const string Output = WorldProductionAudit.Evidence + "three-chimes/";

        public static void Run()
        {
            try
            {
                WorldProductionAudit.Open();
                Directory.CreateDirectory(Output);
                var report = new StringBuilder("Temporary alternative layout for Lantern Rest. No scene or terrain saved.\n");
                foreach (string file in Directory.GetFiles(Art, "*.fbx"))
                {
                    var model = Load(Path.GetFileNameWithoutExtension(file));
                    var filters = model.GetComponentsInChildren<MeshFilter>();
                    if (filters.Length != 1 || !filters[0].sharedMesh || !filters[0].sharedMesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                        throw new InvalidOperationException("Missing mesh or material UV: " + file);
                    report.AppendLine($"Imported {model.name}: {filters[0].sharedMesh.GetIndexCount(0) / 3} triangles");
                }

                var existing = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Where(c => c.enabled && !c.isTrigger && !(c is TerrainCollider)).ToArray();
                var relay = Create(report);
                foreach (var model in relay.Models)
                    SettlementPlacementPreview.ReportOverlap(model, existing, report);

                var player = Object.FindFirstObjectByType<PlayerController>().GetComponent<CharacterController>();
                foreach (bool mounted in new[] { false, true })
                foreach (bool reverse in new[] { false, true })
                    WorldTraversalVerification.Traverse(Approach, mounted, reverse, player, report, relay.Surface);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    SettlementPlacementPreview.Capture(Output + "lantern-rest-relay.png", new Vector3(-15, 20.5f, 291.5f),
                        new Vector3(3, 10.1f, 301));
                    SettlementPlacementPreview.Capture(Output + "lantern-rest-approach.png", new Vector3(-12, 12.85f, 299),
                        new Vector3(4, 10.7f, 301.5f));
                }
                File.WriteAllText(Output + "review.txt", report.ToString());
                EditorApplication.Exit(report.ToString().Contains("BLOCKED") ? 1 : 0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        internal static WorldTraversalVerification.Route Approach => new WorldTraversalVerification.Route("Three-Chimes entrance",
            new Vector3(-16.14f, 0, 300), new Vector3(-7f, 0, 300), new Vector3(3.3f, 0, 300));

        internal sealed class Placement
        {
            internal MeshCollider[] Models;
            internal MeshCollider[] Roads;

            internal Vector3 Surface(Vector3 point)
            {
                var surface = WorldTraversalVerification.Surface(point);
                foreach (var road in Roads)
                    if (road.Raycast(new Ray(surface + Vector3.up * 8, Vector3.down), out var hit, 16)
                        && hit.point.y > surface.y)
                        surface = hit.point;
                return surface;
            }
        }

        internal static Placement Create(StringBuilder report)
        {
            var surfaces = new RegionalSurfaces();
            var root = new GameObject("Three-Chimes Relay placement").transform;
            var models = new[]
            {
                Site("ThreeChimes_Relayhouse_Regional", root, surfaces, new Vector3(3.7f, 0, 306.2f), 180, report),
                Site("ThreeChimes_BellTower_Regional", root, surfaces, new Vector3(10f, 0, 304.2f), 180, report),
                Site("ThreeChimes_LoadingBay_Regional", root, surfaces, new Vector3(6.8f, 0, 295.6f), 0, report),
                Site("ThreeChimes_TrailSign_Regional", root, surfaces, new Vector3(-7f, 0, 302.6f), -25, report)
            };
            var roads = new MeshCollider[4];
            for (int i = 0; i < 3; i++)
                roads[i] = Road("RelayRoad_Straight4m_Regional", root, surfaces, new Vector3(-12.8f + i * 4f, 0, 300));
            roads[3] = Road("RelayRoad_Turnout6m_Regional", root, surfaces, new Vector3(-.8f, 0, 300));

            Physics.SyncTransforms();
            return new Placement { Models = models, Roads = roads };
        }

        static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Art + name + ".fbx");
            if (!prefab) throw new InvalidOperationException("Missing model: " + name);
            return prefab;
        }

        static MeshFilter Spawn(string name, Transform root, Vector3 point, float yaw)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(root);
            anchor.SetPositionAndRotation(point, Quaternion.Euler(0, yaw, 0));
            var model = (GameObject)PrefabUtility.InstantiatePrefab(Load(name), anchor);
            var mesh = model.GetComponentInChildren<MeshFilter>();
            return mesh;
        }

        static MeshCollider Site(string name, Transform root, RegionalSurfaces surfaces,
            Vector3 point, float yaw, StringBuilder report)
        {
            var mesh = Spawn(name, root, point, yaw);
            surfaces.Apply(mesh);
            var renderer = mesh.GetComponent<MeshRenderer>();
            var bounds = renderer.bounds;
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            foreach (float x in new[] { bounds.min.x, bounds.center.x, bounds.max.x })
            foreach (float z in new[] { bounds.min.z, bounds.center.z, bounds.max.z })
            {
                float y = WorldRegions.GroundHeight(new Vector3(x, 0, z));
                low = Mathf.Min(low, y);
                high = Mathf.Max(high, y);
            }
            var anchor = mesh.transform;
            while (anchor.parent != root) anchor = anchor.parent;
            anchor.position += Vector3.up * (high - bounds.min.y);
            if (high - low > .03f)
            {
                var footing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                footing.name = "Temporary stone footing";
                footing.transform.SetParent(anchor);
                footing.transform.SetPositionAndRotation(new Vector3(bounds.center.x, (low + high - .06f) / 2, bounds.center.z),
                    Quaternion.identity);
                footing.transform.localScale = new Vector3(bounds.size.x * .87f, high - low + .06f, bounds.size.z * .82f);
                footing.GetComponent<MeshRenderer>().sharedMaterial = surfaces.Stone;
            }
            report.AppendLine($"{name} x/z=({point.x:F2},{point.z:F2}) terrain={low:F2}..{high:F2}m");
            var collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
            return collider;
        }

        sealed class RegionalSurfaces
        {
            readonly Dictionary<string, Material> materials;
            readonly Dictionary<string, TerrainLayer> terrain = new Dictionary<string, TerrainLayer>();
            internal Material Stone => materials["TC_stone"];

            internal RegionalSurfaces()
            {
                var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(Art + "three_chimes_detail_atlas.png");
                if (!atlas) throw new InvalidOperationException("Three Chimes detail atlas is missing.");
                var fallback = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                fallback.SetTexture("_BaseMap", atlas);
                fallback.SetFloat("_Smoothness", .15f);
                materials = new Dictionary<string, Material>
                {
                    ["TC_atlas"] = fallback,
                    ["TC_stone"] = Village("RockTrim"),
                    ["TC_plaster"] = Village("Plaster"),
                    ["TC_oak"] = Village("WoodTrim_Wear")
                };
                AddTerrain("TC_dirt", "Earthen trail", fallback.shader);
                AddTerrain("TC_moss", "Painted meadow", fallback.shader);
            }

            void AddTerrain(string key, string name, Shader shader)
            {
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>("Assets/RPG/Terrain/" + name + ".terrainlayer");
                if (!layer || !layer.diffuseTexture) throw new InvalidOperationException("Missing terrain layer " + name);
                terrain.Add(key, layer);
                var material = new Material(shader);
                material.SetTexture("_BaseMap", layer.diffuseTexture);
                material.SetTexture("_BumpMap", layer.normalMapTexture);
                if (layer.normalMapTexture) material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", layer.normalScale);
                material.SetFloat("_Smoothness", layer.smoothness);
                materials.Add(key, material);
            }

            static Material Village(string name)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/RPG/Materials/Village_MI_" + name + ".mat");
                if (!material) throw new InvalidOperationException("Missing village surface " + name);
                return material;
            }

            internal Mesh Apply(MeshFilter filter)
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                var slots = renderer.sharedMaterials;
                var mesh = Object.Instantiate(filter.sharedMesh);
                var atlas = mesh.uv;
                var metres = mesh.uv2;
                var vertices = mesh.vertices;
                if (metres.Length != vertices.Length)
                    throw new InvalidOperationException("Regional UV channel missing on " + filter.name);
                for (int i = 0; i < slots.Length; i++)
                {
                    string key = slots[i].name;
                    if (!materials.TryGetValue(key, out var material))
                        throw new InvalidOperationException("Unexpected Three-Chimes material slot " + key);
                    slots[i] = material;
                    if (key == "TC_atlas") continue;
                    terrain.TryGetValue(key, out var layer);
                    foreach (int index in mesh.GetIndices(i))
                    {
                        if (!layer)
                        {
                            atlas[index] = metres[index];
                            continue;
                        }
                        Vector3 world = filter.transform.TransformPoint(vertices[index]);
                        var tile = WorldRegions.TerrainAt(world);
                        Vector3 local = tile ? world - tile.transform.position : world;
                        atlas[index] = new Vector2((local.x + layer.tileOffset.x) / layer.tileSize.x,
                            (local.z + layer.tileOffset.y) / layer.tileSize.y);
                    }
                }
                mesh.uv = atlas;
                mesh.RecalculateTangents();
                filter.sharedMesh = mesh;
                renderer.sharedMaterials = slots;
                return mesh;
            }
        }

        static MeshCollider Road(string name, Transform root, RegionalSurfaces surfaces, Vector3 start)
        {
            var mesh = Spawn(name, root, start, -90);
            var anchor = mesh.transform;
            while (anchor.parent != root) anchor = anchor.parent;
            if (mesh.GetComponent<MeshRenderer>().bounds.center.x < start.x)
                anchor.Rotate(0, 180, 0);
            var draped = surfaces.Apply(mesh);
            var vertices = draped.vertices;
            for (int i = 0; i < vertices.Length; i++)
            {
                var point = mesh.transform.TransformPoint(vertices[i]);
                float fade = Mathf.Min(Mathf.InverseLerp(-12.8f, -11.3f, point.x), Mathf.InverseLerp(5.2f, 4.1f, point.x));
                point.y += WorldRegions.GroundHeight(point) + Mathf.Lerp(-.045f, .04f, fade);
                vertices[i] = mesh.transform.InverseTransformPoint(point);
            }
            draped.vertices = vertices;
            draped.RecalculateNormals();
            draped.RecalculateTangents();
            draped.RecalculateBounds();
            mesh.sharedMesh = draped;
            var collision = mesh.gameObject.AddComponent<MeshCollider>();
            collision.sharedMesh = draped;
            return collision;
        }
    }
}
