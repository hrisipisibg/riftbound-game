using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static partial class RiverTexturePolish
    {
        // Width is measured across the waterline. A short taper at each end lets the shore meet the surface.
        readonly struct Bank
        {
            internal readonly float along, y, width;
            internal Bank(float along, float y, float width) { this.along = along; this.y = y; this.width = width; }
        }

        static readonly Bank[] Westbank = {
            new(-156, 53.9f, .38f), new(-150, 52.9f, 2.1f), new(-144, 47.3f, 2.7f),
            new(-137, 36.6f, 2.45f), new(-132, 30.8f, 2.6f), new(-129, 17.4f, 2.65f),
            new(-126, 6.85f, 3f), new(-121, 3.66f, 4.3f), new(-107, 3.58f, 5.45f),
            new(-87, 3.52f, 5.85f), new(-66, 3.46f, 4.8f), new(-44, 3.4f, 6.6f),
            new(-14, 3.36f, 5.25f), new(14, 3.28f, 6.5f), new(39, 3.22f, 5.55f),
            new(50, 3.19f, 5.35f), new(65, 3.14f, 5.4f), new(93, 3.03f, 5.7f),
            new(115, 2.92f, 5.1f), new(137, 2.82f, 6.1f), new(156, 2.72f, 8.0f),
            new(168, 2.67f, 7.65f), new(178, 2.61f, 4.4f), new(186, 2.58f, 1.75f),
            new(192, 2.56f, .25f)
        };
        static readonly Bank[] Willow = {
            new(-111, 11.23f, .20f), new(-106, 11.16f, 1.65f), new(-94, 11.07f, 3.25f),
            new(-71, 10.94f, 4.8f), new(-47, 10.81f, 5.1f), new(-17, 10.74f, 4.75f),
            new(8, 10.57f, 4.3f), new(37, 10.41f, 5.7f), new(52, 10.31f, 3.2f),
            new(56, 10.27f, 1.6f), new(62, 10.26f, .2f)
        };

        static Bank Section(Bank[] course, float along)
        {
            int index = 1;
            while (index < course.Length - 1 && along > course[index].along) index++;
            var a = course[index - 1];
            var b = course[index];
            float t = Mathf.InverseLerp(a.along, b.along, along);
            return new Bank(along, Mathf.Lerp(a.y, b.y, t), Mathf.Lerp(a.width, b.width, t * t * (3 - 2 * t)));
        }

        static float Smooth(float a, float b, float value) =>
            Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, value));

        static float Centerline(float along, bool brook)
        {
            if (brook)
            {
                float protectBridge = Smooth(7, 23, Mathf.Abs(along + 17));
                return 368 + protectBridge * (2.1f * Mathf.Sin((along + 12) * .083f) + .85f * Mathf.Sin(along * .16f));
            }
            float bend = Smooth(11, 34, Mathf.Abs(along - 50));
            float pool = Smooth(136, 157, along) * (1 - Smooth(167, 190, along));
            return WorldExpansionLayout.RiverX(along) + bend * (.84f * Mathf.Sin(along * .072f)) - 1.4f * pool;
        }

        static float Fade(Bank[] course, float along) =>
            Smooth(course[0].along, course[0].along + 7f, along) *
            (1 - Smooth(course[^1].along - 7f, course[^1].along, along));

        static bool Candidate(float x, float z, bool brook) => brook
            ? x >= -112 && x <= 63 && Mathf.Abs(z - 368) < 21f
            : z >= -157 && z <= 193 && x > -312 && x < -220;

        static void GroundChannel(Terrain tile, TerrainLayer silt, bool brook)
        {
            var original = SourceTerrain(tile);
            string path = $"{SurfaceFolder}/Terrain/{(brook ? "Willow creek valley" : "Westbank river valley")}.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            if (!data)
            {
                data = Object.Instantiate(original);
                AssetDatabase.CreateAsset(data, path);
            }

            var origin = tile.transform.position;
            var size = data.size;
            int resolution = original.heightmapResolution;
            var heights = original.GetHeights(0, 0, resolution, resolution);
            for (int row = 0; row < resolution; row++)
            for (int column = 0; column < resolution; column++)
            {
                float x = origin.x + column * size.x / (resolution - 1);
                float z = origin.z + row * size.z / (resolution - 1);
                if (!Candidate(x, z, brook)) continue;
                float before = origin.y + heights[row, column] * size.y;
                float shaped = ShapeHeight(x, z, before, brook);
                heights[row, column] = (shaped - origin.y) / size.y;
            }
            data.SetHeights(0, 0, heights);
            ReseatTerrainInstances(data, original, origin, brook);

            int w = original.alphamapWidth;
            int h = original.alphamapHeight;
            var oldLayers = original.terrainLayers;
            var colors = original.GetAlphamaps(0, 0, w, h);
            data.terrainLayers = oldLayers.Append(silt).ToArray();
            var paint = new float[h, w, oldLayers.Length + 1];
            int gravel = Array.FindIndex(oldLayers, l => l.name == "Earthen trail");
            for (int row = 0; row < h; row++)
            for (int column = 0; column < w; column++)
            {
                float x = origin.x + column * size.x / (w - 1);
                float z = origin.z + row * size.z / (h - 1);
                float wet = 0, dry = 0;
                if (Candidate(x, z, brook))
                {
                    var course = brook ? Willow : Westbank;
                    float along = brook ? x : z;
                    float cross = brook ? z : x;
                    var bank = Section(course, along);
                    float offset = Mathf.Abs(cross - Centerline(along, brook));
                    float fade = Fade(course, along);
                    float noise = Mathf.PerlinNoise(x * .33f + 4, z * .25f);
                    wet = fade * (1 - Smooth(bank.width - .8f, bank.width + 2.3f + noise, offset)) * (.76f + .22f * noise);
                    dry = .56f * fade * (1 - Smooth(bank.width + 1.4f, bank.width + (brook ? 7f : 11f), offset));
                }
                for (int i = 0; i < oldLayers.Length; i++)
                    paint[row, column, i] = colors[row, column, i] * (1 - wet) * (1 - dry);
                if (gravel >= 0) paint[row, column, gravel] += dry * (1 - wet);
                paint[row, column, oldLayers.Length] = wet;
            }
            data.SetAlphamaps(0, 0, paint);
            data.name = brook ? "Willow creek valley" : "Westbank river valley";
            tile.terrainData = data;
            tile.GetComponent<TerrainCollider>().terrainData = data;
            EditorUtility.SetDirty(data);
            tile.Flush();
        }

        static Vector3 DryBankPosition(Vector3 position, float ground, bool brook)
        {
            var course = brook ? Willow : Westbank;
            float along = brook ? position.x : position.z;
            float cross = brook ? position.z : position.x;
            float center = Centerline(along, brook);
            var row = Section(course, along);
            if (Fade(course, along) < .65f || ground >= row.y - .12f ||
                Mathf.Abs(cross - center) >= row.width + .25f) return position;

            float bridge = brook ? -17f : 50f;
            float offset = along - bridge;
            if (Mathf.Abs(offset) < 6f)
                along = bridge + (offset < 0 ? -1 : 1) * 7.3f;
            var bank = Section(course, along);
            float edge = Centerline(along, brook) + (cross < center ? -1 : 1) * (bank.width + 1.75f);
            if (brook) { position.x = along; position.z = edge; }
            else { position.z = along; position.x = edge; }
            return position;
        }

        static void ReseatTerrainInstances(TerrainData data, TerrainData original, Vector3 origin, bool brook)
        {
            var instances = original.treeInstances;
            if (instances.Length == 0) return;
            var size = original.size;
            for (int index = 0; index < instances.Length; index++)
            {
                var entry = instances[index];
                var point = origin + Vector3.Scale(entry.position, size);
                if (!Candidate(point.x, point.z, brook)) continue;
                float y = origin.y + data.GetInterpolatedHeight(entry.position.x, entry.position.z);
                point = DryBankPosition(point, y, brook);
                float x = Mathf.Clamp01((point.x - origin.x) / size.x);
                float z = Mathf.Clamp01((point.z - origin.z) / size.z);
                entry.position = new Vector3(x, data.GetInterpolatedHeight(x, z) / size.y, z);
                instances[index] = entry;
            }
            data.SetTreeInstances(instances, false);
        }

        static TerrainData SourceTerrain(Terrain tile)
        {
            if (!AssetDatabase.GetAssetPath(tile.terrainData).StartsWith(SurfaceFolder, StringComparison.Ordinal))
                return tile.terrainData;
            string path = tile.name == "Expanded terrain 0-0"
                ? "Assets/RPG/Terrain/ReaverCamp/Westbank crossing protected.asset"
                : "Assets/RPG/Terrain/WorldExpansion/World tile 0-1.asset";
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            return data ? data : throw new InvalidOperationException($"Missing the original terrain for {tile.name}.");
        }

        static float ShapeHeight(float x, float z, float before, bool brook)
        {
            var course = brook ? Willow : Westbank;
            float along = brook ? x : z;
            float cross = brook ? z : x;
            var bank = Section(course, along);
            float fade = Smooth(course[0].along - 1f, course[0].along + 3f, along) *
                (1 - Smooth(course[^1].along - 2f, course[^1].along + 1f, along));
            float d = Mathf.Abs(cross - Centerline(along, brook));
            float bankWidth = bank.width;
            float read = Mathf.PerlinNoise(x * .21f + 3, z * .23f) - .5f;
            float deep = brook ? .68f : Mathf.Lerp(1.12f, 1.82f, Smooth(-126, -103, z));
            float bed = bank.y - deep * (1 - Smooth(bankWidth * .12f, bankWidth, d));
            bed += Smooth(bankWidth - .45f, bankWidth + .46f, d) * .14f + read * .12f;
            float shore = Mathf.Max(0, d - bankWidth);
            float bankSlope = brook ? .17f : .51f + .21f * Mathf.PerlinNoise(along * .065f, 18);
            bed += shore * bankSlope + (read * .64f) * Smooth(1.1f, 5f, shore);
            float proximity = 1 - Smooth(bankWidth + .65f, bankWidth + (brook ? 8.25f : 21f), d);
            if (d > bankWidth + 1.5f) bed = Mathf.Min(bed, before + .12f);

            // The road landings and rails are already measured against the original collision.
            // Inside the width of the channel, lower the buried transverse ridge just beyond the Westbank deck.
            float alongBridge = brook ? Mathf.Abs(x + 17) : Mathf.Abs(z - 50);
            float protect = (1 - Smooth(brook ? 3.5f : 4f, brook ? 7.3f : 8.5f, alongBridge)) *
                Smooth(brook ? 5.3f : 7.0f, brook ? 6.8f : 10.5f, d);
            float preserveDeathknell = !brook ? Smooth(142, 157, z) * Smooth(-266, -260, x) : 0;
            return Mathf.Lerp(before, bed, proximity * fade * (1 - protect) * (1 - preserveDeathknell));
        }

        static Mesh ReplaceWater(string objectName, bool brook, Material material)
        {
            var water = GameObject.Find(objectName);
            if (!water || !water.TryGetComponent<MeshFilter>(out var filter))
                throw new InvalidOperationException("Water object missing: " + objectName);
            var course = brook ? Willow : Westbank;
            float from = course[0].along;
            float to = course[^1].along;
            int count = Mathf.CeilToInt((to - from) / .52f) + 1;
            var points = new Vector3[count * 2];
            var uvs = new Vector2[points.Length];
            var colors = new Color[points.Length];
            var triangles = new int[(count - 1) * 6];
            float length = 0;
            Vector3 previous = default;
            for (int row = 0; row < count; row++)
            {
                float along = Mathf.Lerp(from, to, row / (count - 1f));
                var bank = Section(course, along);
                float cross = Centerline(along, brook);
                var center = brook ? new Vector3(along, bank.y, cross) : new Vector3(cross, bank.y, along);
                float drop = row == 0 ? 0 : Mathf.Abs(center.y - previous.y) / Mathf.Max(.05f, Mathf.Abs(along - (brook ? previous.x : previous.z)));
                if (row > 0) length += Vector3.Distance(previous, center);
                previous = center;
                float speed = Smooth(.21f, 1.88f, drop);
                var side = brook ? -Vector3.forward : Vector3.right;
                for (int j = 0; j < 2; j++)
                {
                    int index = row * 2 + j;
                    var position = center + side * ((j == 0 ? -1 : 1) * (bank.width + .28f));
                    points[index] = water.transform.InverseTransformPoint(position);
                    uvs[index] = new Vector2(j, length);
                    colors[index] = new Color(speed, Mathf.Clamp01(bank.width * .16f), .5f, Fade(course, along));
                }
                if (row == count - 1) continue;
                int a = row * 2, t = row * 6;
                triangles[t] = a; triangles[t + 1] = a + 2; triangles[t + 2] = a + 1;
                triangles[t + 3] = a + 1; triangles[t + 4] = a + 2; triangles[t + 5] = a + 3;
            }

            var mesh = new Mesh { name = brook ? "Willow Brook flowing surface" : "Westbank headwaters to lower pool" };
            mesh.vertices = points;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string path = $"{SurfaceFolder}/{(brook ? "Willow Brook" : "Westbank River")}.asset";
            var stored = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (stored)
            {
                EditorUtility.CopySerialized(mesh, stored);
                Object.DestroyImmediate(mesh);
                mesh = stored;
            }
            else AssetDatabase.CreateAsset(mesh, path);
            filter.sharedMesh = mesh;
            var renderer = water.GetComponent<MeshRenderer>();
            renderer.enabled = true;
            renderer.sharedMaterial = material;
            if (water.TryGetComponent<FlowingWater>(out var legacyUvAnimator)) legacyUvAnimator.enabled = false;
            return mesh;
        }
    }
}
