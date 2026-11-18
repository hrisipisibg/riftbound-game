using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class GroundTextureSetup
    {
        const string Ground = "Assets/RPG/Art/Ground/";
        const string Materials = "Assets/RPG/Materials/";
        static GroundTextureSetup() { EditorApplication.delayCall += FirstSetup; }
        static void FirstSetup()
        {
            if (Application.isBatchMode || File.Exists("GroundTextureResult.txt")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            { EditorApplication.delayCall += FirstSetup; return; }
            if (EditorSceneManager.GetActiveScene().path == "Assets/RPG/Scenes/Outpost.unity") Apply();
        }
        [MenuItem("Riftbound/Apply ground textures")]
        public static void Apply()
        {
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost.unity");
            if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/RPG/Scenes/Outpost.unity") return;
            var scene = EditorSceneManager.GetActiveScene();
            bool hadUnsavedChanges = scene.isDirty;
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Ground.TrimEnd('/') }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    importer.textureShape = TextureImporterShape.Texture2D;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.filterMode = FilterMode.Trilinear; importer.anisoLevel = 8; importer.mipmapEnabled = true;
                    importer.textureType = path.Contains("normal_") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                    importer.sRGBTexture = path.Contains("basecolor");
                    importer.SaveAndReimport();
                }
                var bricks = AssetDatabase.LoadAssetAtPath<Texture2D>(Ground + "bricks_wall_14_basecolor_1k.png");
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Ground + "bricks_wall_14_normal_gl_1k.png");
                var occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(Ground + "bricks_wall_14_ambientocclusion_1k.png");
                var noise = AssetDatabase.LoadAssetAtPath<Texture2D>(Ground + "T_Noise_Terrain.png");
                if (!bricks || !normal || !noise) throw new InvalidOperationException("Ground texture files are missing.");
                var meadow = Material("Meadow");
                meadow.SetTexture("_BaseMap", noise); meadow.SetColor("_BaseColor", new Color(.45f, .63f, .28f));
                meadow.SetFloat("_Smoothness", .05f); meadow.SetTextureScale("_BaseMap", new Vector2(40, 45));
                AssetDatabase.SaveAssetIfDirty(meadow);
                var grass = AssetDatabase.LoadAssetAtPath<Material>(Materials + "Nature_Grass.mat");
                if (grass)
                {
                    Undo.RecordObject(grass, "Texture grass");
                    grass.SetTexture("_BaseMap", noise); grass.SetColor("_BaseColor", new Color(.4f, .62f, .18f));
                    grass.SetTextureScale("_BaseMap", Vector2.one * 2); grass.SetFloat("_Smoothness", .05f);
                    EditorUtility.SetDirty(grass); AssetDatabase.SaveAssetIfDirty(grass);
                }
                int paved = 0, meadows = 0;
                foreach (var root in scene.GetRootGameObjects())
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.name == "Meadow ground")
                    {
                        Undo.RecordObject(renderer, "Texture meadow ground"); renderer.sharedMaterial = meadow; meadows++; continue;
                    }
                    if (renderer.name != "Village square" && renderer.name != "Forest road" && renderer.name != "West lane" && renderer.name != "East lane") continue;
                    var material = Material("Ground " + renderer.name);
                    material.SetTexture("_BaseMap", bricks); material.SetColor("_BaseColor", new Color(.83f, .9f, 1));
                    material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", .65f); material.EnableKeyword("_NORMALMAP");
                    material.SetTexture("_OcclusionMap", occlusion); material.SetFloat("_OcclusionStrength", .65f); material.EnableKeyword("_OCCLUSIONMAP");
                    material.SetFloat("_Metallic", 0); material.SetFloat("_Smoothness", .12f);
                    // Unity cube top faces span UV 0..1; tile at one repeat per two world metres.
                    var size = renderer.transform.lossyScale;
                    var tile = new Vector2(Mathf.Abs(size.x) / 2, Mathf.Abs(size.z) / 2);
                    material.SetTextureScale("_BaseMap", tile);
                    Undo.RecordObject(renderer, "Texture paved ground"); renderer.sharedMaterial = material;
                    EditorUtility.SetDirty(renderer); AssetDatabase.SaveAssetIfDirty(material); paved++;
                }
                if (paved != 4 || meadows != 1) throw new InvalidOperationException($"Expected 4 paved objects and 1 meadow; found {paved} and {meadows}.");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!hadUnsavedChanges) EditorSceneManager.SaveScene(scene);
                File.WriteAllText("GroundTextureResult.txt", $"PASS: {paved} paved surfaces use brick basecolor, GL normal and occlusion; 1 meadow and grass material use green terrain noise.\nTexture tiling: 2 metres per repeat on paving; 4 metres on meadow.\nScene saved: {!hadUnsavedChanges}. Existing unsaved edits preserved.");
            }
            catch (Exception exception) { File.WriteAllText("GroundTextureResult.txt", "FAILED: " + exception); Debug.LogException(exception); }
        }
        static Material Material(string name)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            { material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; AssetDatabase.CreateAsset(material, path); }
            else Undo.RecordObject(material, "Update ground material");
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
