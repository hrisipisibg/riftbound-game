using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class PrototypeBuild
    {
        [MenuItem("Riftbound/Build Windows demo")]
        public static void BuildWindows()
        {
            if (EditorApplication.isPlaying) return;
            try
            {
                Directory.CreateDirectory("Builds/Windows");
                PlayerSettings.productName = "Riftbound Prototype";
                PlayerSettings.defaultScreenWidth = 1280;
                PlayerSettings.defaultScreenHeight = 720;
                PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
                string scenePath = File.Exists(WorldRegions.ScenePath) ? WorldRegions.ScenePath : File.Exists("Assets/RPG/Scenes/Outpost-Updated.unity") ? "Assets/RPG/Scenes/Outpost-Updated.unity" : "Assets/RPG/Scenes/Outpost.unity";
                var active = EditorSceneManager.GetActiveScene();
                if (active.path == scenePath && active.isDirty) EditorSceneManager.SaveScene(active);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = "Builds/Windows/Riftbound.exe",
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                File.WriteAllText("PrototypeBuildResult.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nWarnings: " + report.summary.totalWarnings + "\nTime: " + report.summary.totalTime + "\nBytes: " + report.summary.totalSize);
                Debug.Log("Riftbound Windows build: " + report.summary.result);
            }
            catch (Exception exception)
            {
                File.WriteAllText("PrototypeBuildResult.txt", exception.ToString()); Debug.LogException(exception);
            }
        }
    }
}
