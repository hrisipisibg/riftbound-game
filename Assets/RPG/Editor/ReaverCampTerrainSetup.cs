using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class ReaverCampTerrainSetup
    {
        const string Folder = "Assets/RPG/Terrain/ReaverCamp";
        public static void BatchInstall()
        {
            try
            {
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var terrain = WorldRegions.TerrainAt(RiverReaverCamp.Position);
                if (!terrain) throw new InvalidOperationException("Camp terrain is missing.");
                Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
                string path = Folder + "/Reaver approach.asset";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (!data)
                {
                    data = UnityEngine.Object.Instantiate(terrain.terrainData);
                    data.name = "Reaver approach"; AssetDatabase.CreateAsset(data, path);
                }
                terrain.terrainData = data;
                terrain.GetComponent<TerrainCollider>().terrainData = data;
                Shape(data, terrain.transform.position);
                terrain.Flush(); Physics.SyncTransforms();
                float grade = CheckApproach();
                var old = GameObject.Find("River Reaver camp");
                if (old) UnityEngine.Object.DestroyImmediate(old);
                RiverReaverCamp.Ensure(); FacingContentPass.DressReaverCamp();
                EditorUtility.SetDirty(data); AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                File.WriteAllText("ReaverCampTerrainResult.txt", $"PASS: separate terrain asset; maximum approach grade={grade:F1} degrees; camp elevation={RiverReaverCamp.Position.y:F2}m. Original terrain assets retained.");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("ReaverCampTerrainResult.txt", "FAILED\n" + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Vector2 Start => new Vector2(-285, 50);
        static Vector2 End => new Vector2(RiverReaverCamp.Position.x, RiverReaverCamp.Position.z);
        static float Weight(float outer, float inner, float distance) => Mathf.SmoothStep(0, 1, Mathf.Clamp01((outer - distance) / (outer - inner)));
        static float TrailDistance(Vector2 point, out float t)
        {
            var direction = End - Start;
            t = Mathf.Clamp01(Vector2.Dot(point - Start, direction) / direction.sqrMagnitude);
            return Vector2.Distance(point, Start + direction * t);
        }

        static void Shape(TerrainData data, Vector3 origin)
        {
            float startHeight = WorldRegions.GroundHeight(new Vector3(Start.x, 6, Start.y));
            int resolution = data.heightmapResolution;
            float step = data.size.x / (resolution - 1);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((End.x - 17 - origin.x) / step));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((Start.y - 9 - origin.z) / step));
            int width = Mathf.Min(resolution - x0, Mathf.CeilToInt(35 / step));
            int height = Mathf.Min(resolution - z0, Mathf.CeilToInt((End.y + 17 - origin.z) / step) - z0 + 1);
            var values = data.GetHeights(x0, z0, width, height);
            for (int z = 0; z < height; z++)
                for (int x = 0; x < width; x++)
                {
                    var point = new Vector2(origin.x + (x0 + x) * step, origin.z + (z0 + z) * step);
                    float old = origin.y + values[z, x] * data.size.y;
                    float clearing = Weight(15, 10, Vector2.Distance(point, End));
                    float distance = TrailDistance(point, out float t);
                    float ramp = Mathf.Lerp(startHeight, RiverReaverCamp.Position.y, Mathf.Clamp01(t / .65f));
                    float level = Mathf.Lerp(old, RiverReaverCamp.Position.y, clearing);
                    level = Mathf.Lerp(level, ramp, Weight(8, 4, distance));
                    values[z, x] = Mathf.Clamp01((level - origin.y) / data.size.y);
                }
            data.SetHeights(x0, z0, values);

            int dirt = Array.FindIndex(data.terrainLayers, l => l && (l.name.IndexOf("dirt", StringComparison.OrdinalIgnoreCase) >= 0 || l.name.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0));
            if (dirt < 0) return;
            float alphaStep = data.size.x / data.alphamapResolution;
            int ax = Mathf.FloorToInt(x0 * step / alphaStep), az = Mathf.FloorToInt(z0 * step / alphaStep);
            int aw = Mathf.Min(data.alphamapResolution - ax, Mathf.CeilToInt(width * step / alphaStep));
            int ah = Mathf.Min(data.alphamapResolution - az, Mathf.CeilToInt(height * step / alphaStep));
            var paint = data.GetAlphamaps(ax, az, aw, ah);
            for (int z = 0; z < ah; z++)
                for (int x = 0; x < aw; x++)
                {
                    var point = new Vector2(origin.x + (ax + x + .5f) * alphaStep, origin.z + (az + z + .5f) * alphaStep);
                    float amount = .8f * Mathf.Max(Weight(12, 8, Vector2.Distance(point, End)), Weight(4, 2.5f, TrailDistance(point, out _)));
                    for (int layer = 0; layer < data.alphamapLayers; layer++) paint[z, x, layer] *= 1 - amount;
                    paint[z, x, dirt] += amount;
                }
            data.SetAlphamaps(ax, az, paint);
        }

        public static float CheckApproach()
        {
            float worst = 0;
            var side = new Vector2(End.y - Start.y, Start.x - End.x).normalized;
            foreach (float offset in new[] { -1.5f, 0, 1.5f })
            {
                Vector3 previous = default;
                for (int i = 0; i <= 100; i++)
                {
                    var point = Vector2.Lerp(Start, End, i / 100f) + side * offset;
                    var sample = new Vector3(point.x, 0, point.y); sample.y = WorldRegions.GroundHeight(sample);
                    if (i > 0)
                    {
                        var delta = sample - previous;
                        worst = Mathf.Max(worst, Mathf.Atan2(Mathf.Abs(delta.y), new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg);
                    }
                    previous = sample;
                }
            }
            if (worst > 35) throw new InvalidOperationException($"Camp approach too steep: {worst:F1} degrees.");
            return worst;
        }
    }
}
