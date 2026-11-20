using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class GameplayCandidateBuild
    {
        public static void Run()
        {
            try
            {
                if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Use an isolated build batch outside Play Mode.");
                string name = "RiftboundGameplayRefinement01";
                var args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-gameplayBuildName") name = args[i + 1];
                if (string.IsNullOrWhiteSpace(name) || name != Path.GetFileName(name) || name.Contains("..") || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException("Use a plain unique build-folder name.");
                string folder = Path.Combine("Builds", name);
                if (Directory.Exists(folder) || File.Exists(folder)) throw new InvalidOperationException("Build output exists; preserve it and choose another name.");
                string sceneHash = Hash(WorldRegions.ScenePath);
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var terrain = WorldRegions.TerrainAt(RiverReaverCamp.Position);
                var collider = terrain ? terrain.GetComponent<TerrainCollider>() : null;
                if (!terrain || !terrain.terrainData || !collider || collider.terrainData != terrain.terrainData ||
                    terrain.terrainData.treeInstanceCount != 813 || terrain.terrainData.treePrototypes.Length != 15)
                    throw new InvalidOperationException("Protected Reaver terrain/collider/vegetation mismatch.");
                string terrainPath = AssetDatabase.GetAssetPath(terrain.terrainData);
                string terrainHash = Hash(terrainPath);
                var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { WorldRegions.ScenePath }, locationPathName = Path.Combine(folder, "Riftbound.exe"),
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
                });
                Directory.CreateDirectory(".code-check/gameplay-build");
                File.WriteAllText(".code-check/gameplay-build/" + name + ".txt",
                    $"Unity={Application.unityVersion}\nPath={folder}\nResult={result.summary.result}\nErrors={result.summary.totalErrors}\nWarnings={result.summary.totalWarnings}\nSceneSHA256={sceneHash}\nTerrain={terrainPath}\nTerrainSHA256={terrainHash}\nDuration={result.summary.totalTime}\n");
                if (Hash(WorldRegions.ScenePath) != sceneHash || Hash(terrainPath) != terrainHash)
                    throw new InvalidOperationException("Scene or protected terrain changed during build.");
                EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
