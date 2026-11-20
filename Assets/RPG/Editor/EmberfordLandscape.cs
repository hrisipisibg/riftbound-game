using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static class EmberfordLandscape
    {
        const string Folder = "Assets/RPG/ProceduralKit/Landscape";
        readonly struct Site
        {
            public readonly Rect core;
            public readonly Vector2 anchor, grade, swatch;
            public readonly float elevation, feather;
            public Site(Rect core, Vector2 anchor, Vector2 grade, Vector2 swatch, float elevation, float feather)
            {
                this.core = core; this.anchor = anchor; this.grade = grade;
                this.swatch = swatch; this.elevation = elevation; this.feather = feather;
            }

            public float Coverage(float x, float z)
            {
                float dx = Mathf.Max(0, Mathf.Max(core.xMin - x, x - core.xMax));
                float dz = Mathf.Max(0, Mathf.Max(core.yMin - z, z - core.yMax));
                // Keep the original Emberwatch civic sites on the west side of the grading.
                float fadeX = core.xMin - x > 0 && anchor.y < 0 ? 6f : feather;
                return 1f - Mathf.SmoothStep(0f, 1f, new Vector2(dx / fadeX, dz / feather).magnitude);
            }

            public float Elevation(float x, float z) =>
                elevation + Vector2.Dot(new Vector2(x, z) - anchor, grade);
        }

        static readonly Site[] Sites =
        {
            new Site(Rect.MinMaxRect(51, -57, 89, -9), new Vector2(71, -41),
                new Vector2(.025f, .018f), new Vector2(56, -45), 1.14f, 22f),
            new Site(Rect.MinMaxRect(63, 100, 79, 119), new Vector2(69, 105),
                new Vector2(.03f, .08f), new Vector2(53, 107), 5.28f, 14f),
            new Site(Rect.MinMaxRect(134, 170, 163, 194), new Vector2(149, 175),
                new Vector2(.025f, .018f), new Vector2(158, 161), 11.85f, 15f)
        };

        public static void Shape(Terrain[] terrains)
        {
            var sources = terrains.Select(t => new { tile = t, source = Original(t) }).ToArray();
            var palette = Sites.Select(site =>
            {
                Vector3 point = new Vector3(site.swatch.x, 0, site.swatch.y);
                var tile = WorldRegions.TerrainAt(point, terrains);
                if (!tile) throw new InvalidOperationException($"No grading swatch terrain at {point}.");
                var terrain = sources.Single(row => row.tile == tile).source;
                var origin = tile.transform.position;
                int x = Mathf.Clamp(Mathf.RoundToInt((point.x - origin.x) / terrain.size.x * (terrain.alphamapWidth - 1)), 0, terrain.alphamapWidth - 1);
                int z = Mathf.Clamp(Mathf.RoundToInt((point.z - origin.z) / terrain.size.z * (terrain.alphamapHeight - 1)), 0, terrain.alphamapHeight - 1);
                var alpha = terrain.GetAlphamaps(x, z, 1, 1);
                return (layers: terrain.terrainLayers, weights: alpha);
            }).ToArray();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/RPG/ProceduralKit", "Landscape");

            foreach (var (tile, source) in sources.Select(row => (row.tile, row.source)))
            {
                var origin = tile.transform.position;
                var size = source.size;
                if (!Sites.Any(site => site.core.xMax + site.feather > origin.x &&
                    site.core.xMin - site.feather < origin.x + size.x &&
                    site.core.yMax + site.feather > origin.z && site.core.yMin - site.feather < origin.z + size.z)) continue;
                string path = $"{Folder}/Emberford {source.name}.asset";
                var data = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                bool create = !data;
                if (create) data = Object.Instantiate(source);
                data.terrainLayers = source.terrainLayers;

                int resolution = source.heightmapResolution;
                var heights = source.GetHeights(0, 0, resolution, resolution);
                for (int iz = 0; iz < resolution; iz++)
                for (int ix = 0; ix < resolution; ix++)
                {
                    float x = origin.x + ix * size.x / (resolution - 1);
                    float z = origin.z + iz * size.z / (resolution - 1);
                    float y = origin.y + heights[iz, ix] * size.y;
                    foreach (var site in Sites)
                    {
                        float coverage = site.Coverage(x, z);
                        if (coverage > 0) y = Mathf.Lerp(y, site.Elevation(x, z), coverage);
                    }
                    heights[iz, ix] = (y - origin.y) / size.y;
                }
                data.SetHeights(0, 0, heights);

                int width = data.alphamapWidth;
                int height = data.alphamapHeight;
                var colors = source.GetAlphamaps(0, 0, width, height);
                var layers = source.terrainLayers;
                var swatches = palette.Select(p => layers.Select(l =>
                {
                    int i = Array.IndexOf(p.layers, l);
                    return i >= 0 ? p.weights[0, 0, i] : 0f;
                }).ToArray()).ToArray();
                for (int iz = 0; iz < height; iz++)
                for (int ix = 0; ix < width; ix++)
                {
                    float x = origin.x + ix * size.x / (width - 1);
                    float z = origin.z + iz * size.z / (height - 1);
                    for (int index = 0; index < Sites.Length; index++)
                    {
                        float blend = Sites[index].Coverage(x, z) * (.78f + .20f * Mathf.PerlinNoise(x * .085f, z * .085f));
                        if (blend <= 0) continue;
                        for (int layer = 0; layer < layers.Length; layer++)
                            colors[iz, ix, layer] = Mathf.Lerp(colors[iz, ix, layer], swatches[index][layer], blend);
                    }
                }
                data.SetAlphamaps(0, 0, colors);

                if (create) AssetDatabase.CreateAsset(data, path);
                data.name = "Emberford " + source.name;
                tile.terrainData = data;
                tile.GetComponent<TerrainCollider>().terrainData = data;
                EditorUtility.SetDirty(data);
            }
            Physics.SyncTransforms();
        }

        static TerrainData Original(Terrain tile)
        {
            var data = tile.terrainData;
            if (!AssetDatabase.GetAssetPath(data).StartsWith(Folder, StringComparison.Ordinal)) return data;
            string name = data.name.Substring("Emberford ".Length);
            return AssetDatabase.FindAssets("t:TerrainData", new[] { "Assets/RPG/Terrain" })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<TerrainData>)
                .Single(candidate => candidate && candidate.name == name);
        }
    }
}
