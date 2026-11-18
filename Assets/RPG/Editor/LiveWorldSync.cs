using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class LiveWorldSync
    {
        static LiveWorldSync()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        static void OnEditorReady()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Generating on every editor restart overwrites manual world and model edits.
            if (!File.Exists(WorldRegions.ScenePath))
                Debug.Log("[Riftbound] Expanded world is missing. Use Riftbound > Generate & Open Big Zone World to create it.");
        }

        [MenuItem("Riftbound/🗺️ Generate & Open Big Zone World", priority = -10)]
        public static void SyncAndOpenExpandedWorld()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Riftbound] Exit Play Mode before generating the world.");
                return;
            }

            if (!Application.isBatchMode && File.Exists(WorldRegions.ScenePath) &&
                !EditorUtility.DisplayDialog("Regenerate expanded world?", "This rebuilds the expanded scene and character assets. Manual edits to those generated assets will be replaced.", "Regenerate", "Cancel")) return;

            try
            {
                Debug.Log("<color=#ffb442>[Riftbound]</color> Synchronizing and generating Big Zone World (Outpost-Expanded.unity)...");
                EditorSceneManager.SaveOpenScenes();

                // 1. Rebuild all 24 Racial Character Prefabs + Unified Stylized Textures, then generate the expanded world
                WorldPresentationSetup.CreateLayers();
                CharacterModelBuilder.BuildAll();
                WorldExpansionSetup.Generate();
                WorldPresentationSetup.FinishScene();

                // 2. Open Outpost-Expanded.unity in the Editor
                if (File.Exists(WorldRegions.ScenePath))
                {
                    EditorSceneManager.OpenScene(WorldRegions.ScenePath, OpenSceneMode.Single);
                    Debug.Log($"<color=#70df80><b>[Riftbound]</b> Successfully opened {WorldRegions.ScenePath}! The big map with natural boundaries, gateways, and cross-zone questlines is now active.</color>");
                }

                // 3. Ensure Build Settings has Outpost-Expanded at index 0
                SceneQuickOpener.EnsureBuildSettings();
                SceneQuickOpener.FocusPlayerInSceneView();
            }
            catch (Exception ex)
            {
                Debug.LogError("[Riftbound] World sync failed: " + ex);
            }
        }
    }
}
