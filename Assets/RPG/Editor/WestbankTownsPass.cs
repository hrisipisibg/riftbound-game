using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Riftbound.Editor
{
    public static class WestbankTownsPass
    {
        const string Assets = "Assets/RPG/Terrain/WestbankTownArt";
        const string Output = ".code-check/westbank-towns";
        const float WaterLevel = 3.24f;
        const float ChannelStart = -124, ChannelEnd = 135;
        const string RootName = "Westbank crossing and new town buildings";
        static readonly string Materials = "Assets/RPG/Materials/";
        static WorldTraversalVerification.Route[] protectedRoutes;

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            try
            {
                var scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                if (GameObject.Find(RootName)) throw new InvalidOperationException("The building pass already exists in this scene.");
                Directory.CreateDirectory(Assets);
                AssetDatabase.Refresh();
                protectedRoutes = WorldProductionAudit.Routes();
                var report = new StringBuilder("Westbank crossing and town expansion\n");
                var root = Group(RootName, null);
                BuildRiver(root, report);
                BuildBridge(root);
                Physics.SyncTransforms();
                var buildings = BuildTowns(root, report);
                Physics.SyncTransforms();
                VerifyCrossing(report);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the updated scene.");
                AssetDatabase.SaveAssets();
                Render("westbank-bridge", new Vector3(-241, 22, 28), new Vector3(-256, 6, 52));
                Render("river-upstream", new Vector3(-258, 10, 89), new Vector3(-251, 5, 51));
                Render("westbank-from-road", new Vector3(-232, 10, 48), new Vector3(-266, 6, 50));
                foreach (var building in buildings)
                {
                    var eye = building.position + building.forward * 13f + building.right * 8f + Vector3.up * 5.4f;
                    Render(building.name.ToLowerInvariant().Replace(' ', '-'), eye, building.position + Vector3.up * 2.55f);
                }
                File.WriteAllText(Output + "/validation.txt", report.ToString());
            }
            catch (Exception error)
            {
                File.WriteAllText(Output + "/error.txt", error.ToString());
                Debug.LogException(error);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        static float RiverCenter(float z) => WorldExpansionLayout.RiverX(z);
        static float HalfWidth(float z) => 4.8f + .68f * Mathf.Sin(z * .082f) + .24f * Mathf.Cos(z * .247f);
        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));

        static void BuildRiver(Transform root, StringBuilder report)
        {
            var terrain = WorldRegions.TerrainAt(new Vector3(-256, 6, 50));
            if (!terrain || !AssetDatabase.GetAssetPath(terrain.terrainData).EndsWith("/Westbank crossing protected.asset", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The authored Westbank crossing terrain is required.");
            var data = terrain.terrainData;
            var origin = terrain.GetPosition();
            int resolution = data.heightmapResolution;
            float stepX = data.size.x / (resolution - 1), stepZ = data.size.z / (resolution - 1);
            var heights = data.GetHeights(0, 0, resolution, resolution);
            int edits = 0;
            for (int j = 0; j < resolution; j++)
            {
                float z = origin.z + j * stepZ;
                if (z <= ChannelStart || z >= ChannelEnd) continue;
                float endBlend = Smooth(ChannelStart, ChannelStart + 13, z) * (1 - Smooth(ChannelEnd - 12, ChannelEnd, z));
                float width = HalfWidth(z);
                for (int i = 0; i < resolution; i++)
                {
                    float x = origin.x + i * stepX;
                    float lateral = Mathf.Abs(x - RiverCenter(z));
                    if (lateral > width + 6.1f) continue;
                    float old = origin.y + heights[j, i] * data.size.y;
                    float channelFloor = 1.06f + .20f * Mathf.Cos(z * .135f) + .22f * Smooth(.9f, width, lateral);
                    float bank = Mathf.Lerp(channelFloor, WaterLevel, Smooth(width * .38f, width, lateral));
                    bank += 1.55f * Smooth(width, width + 3.6f, lateral);
                    float blend = endBlend * (1 - Smooth(width, width + 6.1f, lateral));
                    float next = Mathf.Lerp(old, Mathf.Min(old, bank), blend);
                    if (Mathf.Abs(next - old) < .012f) continue;
                    heights[j, i] = (next - origin.y) / data.size.y;
                    edits++;
                }
            }
            data.SetHeights(0, 0, heights);
            PaintRiverBanks(terrain);
            terrain.Flush();
            EditorUtility.SetDirty(data);

            var originalWater = GameObject.Find("Westbank flowing river");
            if (originalWater) originalWater.SetActive(false);
            var surface = Group("Flowing water and shoreline foam", root);
            var filter = surface.gameObject.AddComponent<MeshFilter>();
            var renderer = surface.gameObject.AddComponent<MeshRenderer>();
            filter.sharedMesh = MakeWaterMesh(terrain);
            var shader = Shader.Find("Riftbound/Westbank Water");
            if (!shader) throw new InvalidOperationException("Westbank water shader did not import.");
            const string waterPath = Assets + "/Westbank running water.mat";
            var water = AssetDatabase.LoadAssetAtPath<Material>(waterPath);
            if (!water)
            {
                water = new Material(shader) { name = "Westbank running water" };
                AssetDatabase.CreateAsset(water, waterPath);
            }
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            DressBanks(root, terrain);
            report.AppendLine($"Reshaped {edits} river height samples; retained {data.treeInstanceCount} terrain trees and {data.treePrototypes.Length} prototypes.");
        }

        static void PaintRiverBanks(Terrain terrain)
        {
            var data = terrain.terrainData;
            var origin = terrain.GetPosition();
            int w = data.alphamapWidth, h = data.alphamapHeight;
            var maps = data.GetAlphamaps(0, 0, w, h);
            if (data.terrainLayers.Length != 8 || data.terrainLayers[3].name != "Moss and needles")
                throw new InvalidOperationException("Apply the regional terrain art before adding the river banks.");
            for (int j = 0; j < h; j++)
            {
                float z = origin.z + j * data.size.z / (h - 1);
                if (z < ChannelStart || z > ChannelEnd) continue;
                for (int i = 0; i < w; i++)
                {
                    float x = origin.x + i * data.size.x / (w - 1);
                    float width = HalfWidth(z), edge = Mathf.Abs(x - RiverCenter(z));
                    if (edge > width + 11 || (Mathf.Abs(z - 50) < 3.8f && edge > width + 3f)) continue;
                    float weight = (1 - Smooth(width + .25f, width + 11f, edge)) * .88f;
                    float wetRock = 1 - Smooth(width - .35f, width + 1.8f, edge);
                    for (int k = 0; k < maps.GetLength(2); k++) maps[j, i, k] *= 1 - weight;
                    maps[j, i, 2] += weight * wetRock * .64f;
                    maps[j, i, 1] += weight * wetRock * .36f;
                    maps[j, i, 3] += weight * (1 - wetRock);
                }
            }
            data.SetAlphamaps(0, 0, maps);
        }

        static Mesh MakeWaterMesh(Terrain terrain)
        {
            const int count = 264;
            var points = new Vector3[count * 2];
            var uv = new Vector2[points.Length];
            var indices = new int[(count - 1) * 6];
            for (int i = 0; i < count; i++)
            {
                float progress = i / (count - 1f);
                float z = Mathf.Lerp(ChannelStart, ChannelEnd, progress);
                float center = RiverCenter(z);
                for (int side = 0; side < 2; side++)
                {
                    int sign = side == 0 ? -1 : 1;
                    float width = HalfWidth(z) * .72f;
                    while (width < HalfWidth(z) + 4f && Height(terrain, center + sign * width, z) < WaterLevel + .05f) width += .20f;
                    points[i * 2 + side] = new Vector3(center + sign * (width + .12f), WaterLevel, z);
                    uv[i * 2 + side] = new Vector2(side, progress);
                }
                if (i == count - 1) continue;
                int v = i * 2, t = i * 6;
                indices[t] = v; indices[t + 1] = v + 2; indices[t + 2] = v + 1;
                indices[t + 3] = v + 1; indices[t + 4] = v + 2; indices[t + 5] = v + 3;
            }
            var mesh = new Mesh { name = "Westbank meandering water", vertices = points, uv = uv, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return SaveMesh(mesh, Assets + "/Westbank meandering water.asset");
        }

        static void DressBanks(Transform root, Terrain terrain)
        {
            var group = Group("Shore rocks and reeds", root);
            var rock = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Prefabs/Nature/T_Rock_2.prefab");
            var grass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Prefabs/Nature/T_Grass_Large.prefab");
            var rng = new System.Random(785);
            for (int i = 0; i < 108; i++)
            {
                float z = Mathf.Lerp(-108, 128, (i + .3f) / 108);
                if (Mathf.Abs(z - 50) < 6) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = RiverCenter(z) + side * (HalfWidth(z) + (float)rng.NextDouble() * 2.6f);
                    float y = Height(terrain, x, z);
                    if (y < WaterLevel - .20f || y > WaterLevel + 2.6f) continue;
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(i % 3 == 0 ? rock : grass, group);
                    model.transform.SetPositionAndRotation(new Vector3(x, y - .07f, z), Quaternion.Euler(0, (float)rng.NextDouble() * 360, 0));
                    model.transform.localScale *= .43f + .5f * (float)rng.NextDouble();
                    foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                }
            }
        }

        static void BuildBridge(Transform root)
        {
            var deck = GameObject.Find("Westbank bridge deck");
            if (!deck || !deck.GetComponent<Collider>()) throw new InvalidOperationException("Westbank bridge collision is missing.");
            var bounds = deck.GetComponent<Collider>().bounds;
            foreach (var renderer in deck.transform.parent.GetComponentsInChildren<MeshRenderer>())
                if (renderer.name == deck.name || renderer.name == "Westbank bridge rail" || renderer.name == "Westbank bridge support") renderer.enabled = false;

            var bridge = Group("Westbank stone and timber bridge", root);
            bridge.position = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            var timber = Source("Dark timber");
            var rail = Tint(Source("Village_MI_WoodTrim"), "Bridge warm oak rails", "A58761");
            var boards = Tint(Source("Village_MI_WoodTrim_Wear"), "Bridge weathered oak", "BEB397");
            var stone = Tint(Source("Village_MI_RockTrim"), "Bridge grey limestone", "A6AAA1");
            var key = Tint(Source("Village_MI_UnevenBrick"), "Bridge cut masonry", "CEC4A9");
            const float half = 13f, width = 3.42f;
            for (int i = 0; i < 53; i++)
            {
                float x = Mathf.Lerp(-half + .25f, half - .25f, i / 52f);
                Part(bridge, "Deck plank", new Vector3(x, -.021f, 0), new Vector3(.48f, .10f, 6.70f), i % 7 == 0 ? rail : boards);
            }
            foreach (int side in new[] { -1, 1 })
            {
                float z = width * side;
                Part(bridge, "Timber fascia", new Vector3(0, -.27f, z), new Vector3(26.3f, .48f, .27f), timber);
                Part(bridge, "Capped handrail", new Vector3(0, 1.10f, z), new Vector3(26.25f, .16f, .25f), rail);
                Part(bridge, "Lower balustrade", new Vector3(0, .46f, z), new Vector3(26f, .14f, .16f), timber);
                for (int i = 0; i < 15; i++)
                {
                    float x = Mathf.Lerp(-12.8f, 12.8f, i / 14f);
                    Part(bridge, "Oak baluster", new Vector3(x, .52f, z), new Vector3(.21f, 1.09f, .24f), i % 2 == 0 ? timber : rail);
                }
                for (int i = 0; i < 23; i++)
                {
                    float x = Mathf.Lerp(-8.8f, 8.8f, i / 22f);
                    float rise = Mathf.Sqrt(Mathf.Max(0, 1f - x * x / 88f));
                    float bottom = -3.84f + 3.10f * rise;
                    float h = -0.48f - bottom;
                    Part(bridge, "Arch spandrel", new Vector3(x, bottom + h / 2, z), new Vector3(.81f, h, .53f), stone);
                    var voussoir = Part(bridge, "Arch face block", new Vector3(x, bottom + .03f, z + side * .32f), new Vector3(.84f, .30f, .22f), key);
                    float tangent = -3.10f * x / (88f * Mathf.Max(.23f, rise));
                    voussoir.localRotation = Quaternion.Euler(0, 0, Mathf.Atan(tangent) * Mathf.Rad2Deg);
                }
                foreach (int end in new[] { -1, 1 })
                {
                    float x = 10.8f * end;
                    Part(bridge, "Stone abutment", new Vector3(x, -2.35f, z), new Vector3(4.5f, 3.85f, .95f), stone);
                    Part(bridge, "End pier", new Vector3(12.8f * end, .42f, z), new Vector3(.59f, 1.26f, .68f), key);
                    Part(bridge, "End pier coping", new Vector3(12.8f * end, 1.11f, z), new Vector3(.77f, .22f, .87f), stone);
                }
            }
            foreach (int end in new[] { -1, 1 })
                Part(bridge, "Paved threshold", new Vector3(12.82f * end, -.069f, 0), new Vector3(.42f, .15f, 6.76f), stone);
        }

        static List<Transform> BuildTowns(Transform root, StringBuilder report)
        {
            var buildings = new List<Transform>();
            AddBuilding(buildings, root, report, "Willow Rest tollhouse", new Vector2(-287, 58), 14, 205, 6.5f, 5.3f, "3F6963", "8C7460", "tollhouse");
            AddBuilding(buildings, root, report, "Emberwatch smithy", new Vector2(0, 4), 26, 241, 7.3f, 6.1f, "954B36", "BAA68E", "smithy");
            AddBuilding(buildings, root, report, "Emberwatch coach house", new Vector2(0, 4), 37, 320, 8.25f, 6.0f, "675348", "B8AB8F", "coach house");
            AddBuilding(buildings, root, report, "Greenhollow ranger lodge", new Vector2(-40, 400), 24, 145, 8.25f, 6.3f, "4D6C52", "BABC95", "ranger lodge");
            AddBuilding(buildings, root, report, "Highspire wayfarers hall", new Vector2(-200, -60), 36, 224, 9.0f, 6.6f, "4F6579", "B6B2A3", "wayfarers hall");
            AddBuilding(buildings, root, report, "Bloodrock caravan workshop", new Vector2(280, 210), 29, 310, 7.8f, 6.4f, "7F453B", "AF7F5D", "caravan workshop");
            return buildings;
        }

        static void AddBuilding(List<Transform> buildings, Transform root, StringBuilder report, string name, Vector2 center, float radius,
            float bearing, float width, float depth, string roofColor, string wallColor, string use)
        {
            float footprint = new Vector2(width, depth).magnitude * .5f + .55f;
            if (!FindBuildingLot(center, radius, bearing, footprint, out var position, out var rotation))
                throw new InvalidOperationException("Could not place " + name + " clear of roads, structures and terrain transitions.");
            var house = Group(name, root);
            house.SetPositionAndRotation(position, rotation);
            var stone = Tint(Source("Village_MI_RockTrim"), "Rough foundations", "B6A88F");
            var plaster = Tint(Source("Village_MI_Plaster"), name + " plaster", wallColor);
            var roof = Tint(Source("Village_MI_RoundTiles"), name + " tile", roofColor);
            var trim = Source("Dark timber");
            var boards = Source("Village_MI_WoodTrim_Wear");
            var glass = Solid("Amber window glass", "BC9356", .25f);
            const float eave = 3.1f, door = 1.95f;
            float hw = width / 2, hd = depth / 2;
            Part(house, "Sunk limestone foundation", new Vector3(0, -.62f, 0), new Vector3(width + .18f, 1.12f, depth + .18f), stone);
            Part(house, "Floor", new Vector3(0, -.061f, 0), new Vector3(width, .14f, depth), boards, true);
            AddEntrance(house, hd, stone);
            Part(house, "Back wall", new Vector3(0, eave / 2, -hd), new Vector3(width, eave, .24f), plaster, true);
            Part(house, "Doorway lintel", new Vector3(0, 2.7f, hd), new Vector3(width, eave - 2.3f, .24f), plaster, true);

            foreach (int side in new[] { -1, 1 })
            {
                float x = side * hw;
                Part(house, "Side wall", new Vector3(x, eave / 2, 0), new Vector3(.24f, eave, depth), plaster, true);
                Part(house, "Front wing", new Vector3(side * (hw + door / 2) / 2f, 1.15f, hd), new Vector3(hw - door / 2f, 2.3f, .24f), plaster, true);
                float paneX = side * (hw + door / 2) / 2;
                AddWindow(house, paneX, hd, glass, trim);
                Part(house, "Door frame jamb", new Vector3(side * (door / 2 + .035f), 1.19f, hd + .13f), new Vector3(.15f, 2.38f, .19f), trim);
                foreach (int end in new[] { -1, 1 })
                    Part(house, "Corner timber", new Vector3(x, eave / 2f, end * hd), new Vector3(.29f, eave, .29f), trim);
                Part(house, "Side sill", new Vector3(x, .28f, 0), new Vector3(.31f, .19f, depth + .19f), stone);
                Part(house, "Side eave", new Vector3(x, eave - .07f, 0), new Vector3(.32f, .27f, depth + .63f), trim);

                const float pitch = 32f;
                float rise = Mathf.Tan(pitch * Mathf.Deg2Rad) * hw;
                var plane = Part(house, "Pitched tile roof", new Vector3(side * hw * .52f, eave + rise * .48f, 0),
                    new Vector3((hw + .56f) / Mathf.Cos(pitch * Mathf.Deg2Rad), .16f, depth + .88f), roof, true);
                plane.localRotation = Quaternion.Euler(0, 0, side * -pitch);
                foreach (int end in new[] { -1, 1 })
                {
                    var rake = Part(house, "Gable bargeboard", new Vector3(side * hw * .51f, eave + rise * .50f, end * (hd + .46f)),
                        new Vector3((hw + .58f) / Mathf.Cos(pitch * Mathf.Deg2Rad), .235f, .18f), trim);
                    rake.localRotation = plane.localRotation;
                }
            }

            float roofRise = Mathf.Tan(32f * Mathf.Deg2Rad) * hw;
            AddGables(house, hw, hd, eave, roofRise, plaster, name);
            Part(house, "Ridge beam", new Vector3(0, eave + roofRise + .055f, 0), new Vector3(.25f, .195f, depth + 1.0f), trim);
            Part(house, "Door trim", new Vector3(0, 2.37f, hd + .16f), new Vector3(2.19f, .17f, .21f), trim);
            GiveInterior(house, use, hw, hd, trim, stone);
            DressFacade(house, use, hw, hd, eave, roofRise, trim, roofColor);
            buildings.Add(house);
            report.AppendLine($"Built {name} at {position}; {width:F1}×{depth:F1}m with an open doorway and solid floor and walls.");
        }

        static void DressFacade(Transform house, string use, float hw, float hd, float eave, float rise, Material timber, string accentColor)
        {
            var painted = Solid(house.name + " painted trim", accentColor, .10f);
            var brass = Solid("Aged brass hardware", "A58651", .28f);
            var lamp = Solid("Lantern glass", "E9BE79", .32f);
            Part(house, "Gable tie beam", new Vector3(0, eave + .015f, hd + .132f), new Vector3(hw * 2, .19f, .21f), timber);
            Part(house, "King post", new Vector3(0, eave + rise * .48f, hd + .137f), new Vector3(.19f, rise * .90f, .215f), timber);
            foreach (int side in new[] { -1, 1 })
            {
                var brace = Part(house, "Gable diagonal brace", new Vector3(side * hw * .34f, eave + rise * .23f, hd + .15f),
                    new Vector3(hw * .62f, .16f, .19f), timber);
                brace.localRotation = Quaternion.Euler(0, 0, side * -27f);
                float x = side * (hw + .985f) / 2;
                Part(house, "Painted shutter", new Vector3(x + side * .64f, 1.56f, hd + .229f), new Vector3(.22f, 1.0f, .085f), painted);
            }
            Part(house, "Door lantern bracket", new Vector3(1.22f, 2.22f, hd + .34f), new Vector3(.14f, .14f, .41f), timber);
            Part(house, "Door lantern cage", new Vector3(1.22f, 2.08f, hd + .52f), new Vector3(.19f, .28f, .19f), brass);
            Part(house, "Door lantern panes", new Vector3(1.22f, 2.08f, hd + .52f), new Vector3(.202f, .175f, .202f), lamp);

            if (use.Contains("forge") || use.Contains("smithy") || use.Contains("workshop"))
            {
                Part(house, "Workshop sign rail", new Vector3(-1.25f, 2.55f, hd + .47f), new Vector3(.16f, .17f, .68f), timber);
                Part(house, "Workshop sign board", new Vector3(-1.25f, 2.25f, hd + .68f), new Vector3(.08f, .45f, .49f), painted);
                Part(house, "Sign anvil face", new Vector3(-1.25f, 2.27f, hd + .94f), new Vector3(.53f, .11f, .03f), brass);
                Part(house, "Sign anvil foot", new Vector3(-1.25f, 2.17f, hd + .937f), new Vector3(.26f, .12f, .03f), brass);
            }
            else
            {
                Part(house, "Painted inn pennant", new Vector3(-1.19f, 2.52f, hd + .235f), new Vector3(.59f, .71f, .047f), painted);
                var device = Part(house, "Pennant diamond", new Vector3(-1.19f, 2.53f, hd + .264f), new Vector3(.24f, .24f, .018f), brass);
                device.localRotation = Quaternion.Euler(0, 0, 45);
            }
        }

        static void AddEntrance(Transform house, float halfDepth, Material stone)
        {
            var landing = house.TransformPoint(new Vector3(0, 0, halfDepth + 1.6f));
            var terrain = WorldRegions.TerrainAt(landing);
            float height = terrain ? Height(terrain, landing.x, landing.z) : landing.y;
            float drop = Mathf.Clamp(house.position.y - height, 0, .90f);
            const int steps = 4;
            for (int i = 0; i < steps; i++)
            {
                float top = -.014f - drop * i / steps;
                Part(house, "Dressed stone step", new Vector3(0, top - .115f, halfDepth + .19f + i * .38f), new Vector3(2.22f, .23f, .42f), stone, true);
            }
        }

        static void AddWindow(Transform house, float x, float z, Material glass, Material trim)
        {
            Part(house, "Window casing", new Vector3(x, 1.56f, z + .135f), new Vector3(1.16f, 1.12f, .16f), trim);
            Part(house, "Golden window inset", new Vector3(x, 1.56f, z + .225f), new Vector3(.92f, .85f, .035f), glass);
            Part(house, "Window mullion", new Vector3(x, 1.56f, z + .26f), new Vector3(.08f, .87f, .06f), trim);
            Part(house, "Window transom", new Vector3(x, 1.56f, z + .26f), new Vector3(.95f, .07f, .06f), trim);
            Part(house, "Window shelf", new Vector3(x, 1.0f, z + .24f), new Vector3(1.31f, .13f, .39f), trim);
        }

        static void AddGables(Transform house, float hw, float hd, float eave, float rise, Material wall, string building)
        {
            var gables = Group("Timber gables", house);
            var mesh = new Mesh { name = building + " gable infill" };
            mesh.vertices = new[] { new Vector3(-hw, eave, hd), new Vector3(hw, eave, hd), new Vector3(0, eave + rise, hd),
                new Vector3(hw, eave, -hd), new Vector3(-hw, eave, -hd), new Vector3(0, eave + rise, -hd) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, new Vector2(.5f, 1), Vector2.zero, Vector2.right, new Vector2(.5f, 1) };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            gables.gameObject.AddComponent<MeshFilter>().sharedMesh = SaveMesh(mesh, $"{Assets}/{building} gables.asset");
            gables.gameObject.AddComponent<MeshRenderer>().sharedMaterial = wall;
        }

        static void GiveInterior(Transform house, string use, float hw, float hd, Material wood, Material stone)
        {
            if (use.Contains("smithy") || use.Contains("workshop"))
            {
                Part(house, "Stone hearth", new Vector3(hw - 1.10f, .8f, -hd + .74f), new Vector3(1.38f, 1.60f, 1.17f), stone, true);
                Part(house, "Stone chimney", new Vector3(hw - 1.1f, 2.87f, -hd + .74f), new Vector3(.78f, 4.75f, .82f), stone);
                Part(house, "Iron anvil", new Vector3(hw - 1.05f, 1.06f, -.55f), new Vector3(.78f, .24f, .43f), Tint(stone, "Forge metal", "4F5050"));
                Part(house, "Anvil stand", new Vector3(hw - 1.05f, .45f, -.55f), new Vector3(.48f, .91f, .49f), wood);
            }
            else
            {
                Part(house, "Service desk", new Vector3(0, .9f, -hd + 1.26f), new Vector3(2.86f, .15f, .91f), wood);
                Part(house, "Desk trestle", new Vector3(0, .44f, -hd + 1.26f), new Vector3(2.48f, .78f, .37f), wood);
                Part(house, "Storage shelf", new Vector3(-hw + .44f, 1.06f, -hd + 1.95f), new Vector3(.49f, .15f, 2.42f), wood);
            }
            foreach (int side in new[] { -1, 1 })
            {
                Part(house, "Wall bench", new Vector3(side * (hw - .56f), .48f, .88f), new Vector3(.52f, .13f, 1.51f), wood);
                Part(house, "Bench trestle", new Vector3(side * (hw - .56f), .22f, .88f), new Vector3(.32f, .44f, 1.20f), wood);
            }
        }

        static bool FindBuildingLot(Vector2 center, float radius, float bearing, float footprint, out Vector3 point, out Quaternion rotation)
        {
            for (int i = 0; i < 72; i++)
            {
                float angle = (bearing + i * 137.508f) * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + i / 13 * 5.5f);
                if (WorldExpansionLayout.RoadDistance(p.x, p.y) < footprint + 7f || IsNearProtectedRoute(p, footprint + 5f)) continue;
                if (p.y > ChannelStart && p.y < ChannelEnd && Mathf.Abs(p.x - RiverCenter(p.y)) < footprint + 11f) continue;
                var terrain = WorldRegions.TerrainAt(new Vector3(p.x, 0, p.y));
                if (!terrain) continue;
                float low = float.MaxValue, high = float.MinValue;
                for (int z = -1; z <= 1; z++) for (int x = -1; x <= 1; x++)
                {
                    float sample = Height(terrain, p.x + x * footprint * .62f, p.y + z * footprint * .62f);
                    low = Mathf.Min(low, sample); high = Mathf.Max(high, sample);
                }
                if (high - low > .79f || high < WaterLevel + .8f && Mathf.Abs(p.x - RiverCenter(p.y)) < 29f) continue;
                point = new Vector3(p.x, high + .035f, p.y);
                if (Physics.OverlapBox(point + Vector3.up * 2.3f, new Vector3(footprint + 1.0f, 3.0f, footprint + 1.0f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)
                    .Any(collider => !(collider is TerrainCollider))) continue;
                rotation = Quaternion.LookRotation(new Vector3(center.x - p.x, 0, center.y - p.y));
                return true;
            }
            point = default;
            rotation = default;
            return false;
        }

        static bool IsNearProtectedRoute(Vector2 point, float clearance)
        {
            foreach (var route in protectedRoutes)
                for (int i = 1; i < route.points.Length; i++)
                    if (WorldExpansionLayout.SegmentDistance(point, route.points[i - 1], route.points[i], out _) < clearance) return true;
            return false;
        }

        static void VerifyCrossing(StringBuilder report)
        {
            var mover = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            var controller = mover ? mover.GetComponent<CharacterController>() : null;
            var routes = WorldTraversalVerification.Routes.ToList();
            foreach (float z in new[] { 48.5f, 51.5f })
                routes.Add(new WorldTraversalVerification.Route("Bridge lane " + z, new Vector3(-278, 6, z), new Vector3(-235, 6, z)));
            var checks = new StringBuilder();
            foreach (var route in routes) foreach (bool mounted in new[] { false, true }) foreach (bool reverse in new[] { false, true })
                WorldTraversalVerification.Traverse(route, mounted, reverse, controller, checks);
            report.Append(checks);
            File.WriteAllText(Output + "/route-checks.txt", report.ToString());
            if (checks.ToString().Contains("BLOCKED")) throw new InvalidOperationException("A bridge or protected route was blocked. See the route report.");
        }

        static float Height(Terrain terrain, float x, float z) => terrain.GetPosition().y + terrain.SampleHeight(new Vector3(x, 0, z));

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!asset)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, asset);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static Material Source(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Materials + name + ".mat");
            if (!material) throw new IOException("Missing material " + name);
            return material;
        }

        static Material Tint(Material baseMaterial, string name, string color)
        {
            string path = $"{Assets}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) return existing;
            if (!ColorUtility.TryParseHtmlString("#" + color, out var tint)) throw new ArgumentException(color);
            var material = new Material(baseMaterial) { color = tint };
            material.SetFloat("_Smoothness", .16f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Material Solid(string name, string color, float smoothness)
        {
            string path = $"{Assets}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material) return material;
            if (!ColorUtility.TryParseHtmlString("#" + color, out var tint)) throw new ArgumentException(color);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = tint };
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            if (parent) group.SetParent(parent, false);
            return group;
        }

        static Transform Part(Transform parent, string name, Vector3 location, Vector3 size, Material material, bool solid = false)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            if (!solid) UnityEngine.Object.DestroyImmediate(part.GetComponent<BoxCollider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = location;
            part.transform.localScale = size;
            return part.transform;
        }

        static void Render(string name, Vector3 camera, Vector3 target)
        {
            var async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                WorldPresentationSetup.RenderPreview(name, camera, target, false);
                File.Copy($".code-check/{name}.png", $"{Output}/{name}.png", true);
            }
            finally { ShaderUtil.allowAsyncCompilation = async; }
        }
    }
}
