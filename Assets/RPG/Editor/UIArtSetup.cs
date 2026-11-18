using System;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class UIArtSetup
    {
        public static void PrepareGameView()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            var getGroup = sizesType.GetMethod("GetGroup");
            var group = getGroup.Invoke(sizes, new[] { Enum.Parse(getGroup.GetParameters()[0].ParameterType, "Standalone") });
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var mode = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(mode, "FixedResolution"), (object)1280, 720, "UI validation 1280x720" });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var viewType = assembly.GetType("UnityEditor.GameView");
            var view = EditorWindow.GetWindow(viewType);
            viewType.GetProperty("selectedSizeIndex").SetValue(view, count - 1);
            view.Repaint();
        }

        public static void Configure()
        {
            const string path = "Assets/RPG/Resources/UI/AbilityIcons.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Ability atlas is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = true; importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture || texture.width != texture.height || texture.width < 512)
                throw new InvalidOperationException("Ability atlas must contain sixteen equal square cells.");
        }

        public static void BatchBuild()
        {
            try
            {
                Configure(); PrototypeBuild.BuildWindows();
                EditorApplication.Exit(System.IO.File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded") ? 0 : 1);
            }
            catch (Exception e) { System.IO.File.WriteAllText("UIBuildResult.txt", e.ToString()); Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
