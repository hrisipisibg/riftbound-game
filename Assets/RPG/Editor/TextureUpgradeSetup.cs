using System.IO;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class TextureUpgradeSetup
    {
        static TextureUpgradeSetup()
        {
            EditorApplication.delayCall += CheckAutoUpgrade;
        }

        static void CheckAutoUpgrade()
        {
            const string marker = "Assets/RPG/Textures/TerrainPBR/.upgraded";
            if (File.Exists(marker)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += CheckAutoUpgrade;
                return;
            }
            Apply();
            File.WriteAllText(marker, "PBR upgraded on " + System.DateTime.Now);
        }

        [MenuItem("Riftbound/Apply PBR textures to terrain and rebake rider")]
        public static void Apply()
        {
            const string pbrPath = "Assets/RPG/Textures/TerrainPBR/";
            
            // 1. Configure texture importers
            Configure(pbrPath + "Grass_Albedo.jpg", false);
            Configure(pbrPath + "Grass_Normal.exr", true);
            Configure(pbrPath + "Dirt_Albedo.jpg", false);
            Configure(pbrPath + "Dirt_Normal.exr", true);
            Configure(pbrPath + "Rock_Albedo.jpg", false);
            Configure(pbrPath + "Rock_Normal.exr", true);
            Configure(pbrPath + "Pebbles_Albedo.jpg", false);
            Configure(pbrPath + "Pebbles_Normal.exr", true);

            AssetDatabase.Refresh();

            // 2. Load textures
            var grassDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Grass_Albedo.jpg");
            var grassNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Grass_Normal.exr");
            var dirtDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Dirt_Albedo.jpg");
            var dirtNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Dirt_Normal.exr");
            var rockDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Rock_Albedo.jpg");
            var rockNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Rock_Normal.exr");
            var pebblesDiff = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Pebbles_Albedo.jpg");
            var pebblesNorm = AssetDatabase.LoadAssetAtPath<Texture2D>(pbrPath + "Pebbles_Normal.exr");

            // 3. Update Terrain Layers
            const string terrainFolder = "Assets/RPG/Terrain/";
            UpdateLayer(terrainFolder + "Painted meadow.terrainlayer", grassDiff, grassNorm, new Vector2(6, 6), .05f, .75f);
            UpdateLayer(terrainFolder + "Earthen trail.terrainlayer", dirtDiff, dirtNorm, new Vector2(5, 5), .08f, .85f);
            UpdateLayer(terrainFolder + "Weathered stone.terrainlayer", rockDiff, rockNorm, new Vector2(10, 10), .12f, 1.0f);

            // 4. Update Road ribbon material
            var roadMat = AssetDatabase.LoadAssetAtPath<Material>(terrainFolder + "Earthen road.mat");
            if (roadMat)
            {
                if (pebblesDiff) roadMat.SetTexture("_BaseMap", pebblesDiff);
                if (pebblesNorm) { roadMat.SetTexture("_BumpMap", pebblesNorm); roadMat.EnableKeyword("_NORMALMAP"); roadMat.SetFloat("_BumpScale", .7f); }
                roadMat.SetTextureScale("_BaseMap", new Vector2(1, 1));
                roadMat.SetFloat("_Smoothness", .1f);
                EditorUtility.SetDirty(roadMat);
            }

            // 5. Rebake Player Ride.anim with 22-degree natural leg flare
            PolishSetup.RebakeRide();

            AssetDatabase.SaveAssets();
            Debug.Log("PBR Terrain textures and natural riding pose applied successfully!");
        }

        static void Configure(string path, bool isNormal)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (!importer) return;
            importer.textureType = isNormal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !isNormal;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        static void UpdateLayer(string path, Texture2D diff, Texture2D norm, Vector2 tileSize, float smoothness, float normalScale)
        {
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (!layer) return;
            if (diff) layer.diffuseTexture = diff;
            if (norm) layer.normalMapTexture = norm;
            layer.tileSize = tileSize;
            layer.smoothness = smoothness;
            layer.normalScale = normalScale;
            EditorUtility.SetDirty(layer);
        }
    }
}
