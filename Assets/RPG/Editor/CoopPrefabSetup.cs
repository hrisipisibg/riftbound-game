using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Riftbound.Network;

namespace Riftbound.Editor
{
    public static class CoopPrefabSetup
    {
        const string PrefabPath = "Assets/RPG/Resources/CoopPlayerAvatar.prefab";
        const string OutpostScene = "Assets/RPG/Scenes/Outpost-Expanded.unity";

        public static void InstallMultiplayerServicesSdk()
        {
            AddRequest req = Client.Add("com.unity.services.multiplayer");
            while (!req.IsCompleted) System.Threading.Thread.Sleep(100);
            if (req.Status == StatusCode.Success)
            {
                Debug.Log($"[CoopPrefabSetup] Installed {req.Result.packageId}");
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError($"[CoopPrefabSetup] Failed to install com.unity.services.multiplayer: {req.Error?.message}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        [MenuItem("Riftbound/Multiplayer/Build Coop Avatar Prefab")]
        public static void BuildCoopAvatarPrefab()
        {
            Directory.CreateDirectory("Assets/RPG/Resources");

            var go = new GameObject("CoopPlayerAvatar");
            go.AddComponent<NetworkObject>();
            var nt = go.AddComponent<NetworkTransform>();
            nt.SyncPositionX = true;
            nt.SyncPositionY = true;
            nt.SyncPositionZ = true;
            nt.SyncRotAngleY = true;
            nt.SyncRotAngleX = false;
            nt.SyncRotAngleZ = false;
            nt.SyncScaleX = false;
            nt.SyncScaleY = false;
            nt.SyncScaleZ = false;
            go.AddComponent<RiftboundCoopPlayer>();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CoopPrefabSetup] Built " + PrefabPath);
        }

        public static void BuildCoopSmokeStandalone()
        {
            BuildCoopAvatarPrefab();

            string buildDir = "Builds/CoopSmoke";
            Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "RiftboundCoop.exe");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { OutpostScene },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[CoopPrefabSetup] Build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CoopPrefabSetup] Build succeeded: {exePath} ({report.summary.totalSize} bytes)");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Riftbound/Multiplayer/Build Standalone Co-op Release")]
        public static void BuildCoopReleaseStandalone()
        {
            BuildCoopAvatarPrefab();

            string buildDir = "Builds/CoopRelease";
            Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "Riftbound.exe");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { OutpostScene },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[CoopPrefabSetup] Release build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CoopPrefabSetup] Release build succeeded: {exePath} ({report.summary.totalSize} bytes)");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("Riftbound/Build Production Gameplay Standalone")]
        public static void BuildProductionGameplay()
        {
            BuildStandaloneTo("Builds/ProductionGameplay", "Production");
        }

        [MenuItem("Riftbound/Multiplayer/Build Online Validation")]
        public static void BuildOnlineValidation()
        {
            BuildStandaloneTo("Builds/OnlineValidation", "Overnight");
        }

        [MenuItem("Riftbound/Build Combat Refinement 01")]
        public static void BuildCombatRefinement01()
        {
            BuildStandaloneTo("Builds/RiftboundCombat01", "CombatRefinement01");
        }

        [MenuItem("Riftbound/Build Multiplayer Presentation 01")]
        public static void BuildMultiplayerPolish01()
        {
            BuildStandaloneTo("Builds/RiftboundPresentation01", "MultiplayerPresentation01");
        }

        [MenuItem("Riftbound/Build Multiplayer Presentation 02")]
        public static void BuildMultiplayerPolish02()
        {
            BuildStandaloneTo("Builds/RiftboundPresentation02", "MultiplayerPresentation02");
        }

        [MenuItem("Riftbound/Build Release Gate 1 Runtime 01")]
        public static void BuildReleaseGate1Runtime01()
        {
            BuildStandaloneTo("Builds/RiftboundReleaseGate1Runtime01", "ReleaseGate1Runtime01");
        }

        [MenuItem("Riftbound/Build Gameplay Gemini Combat 01")]
        public static void BuildGameplayGeminiCombat01()
        {
            BuildStandaloneTo("Builds/RiftboundCombatRevamp01", "GameplayGeminiCombat01");
        }

        [MenuItem("Riftbound/Build Gameplay Gemini Combat 02")]
        public static void BuildGameplayGeminiCombat02()
        {
            BuildStandaloneTo("Builds/RiftboundCombatRevamp02", "GameplayGeminiCombat02");
        }

        [MenuItem("Riftbound/Build Gameplay Gemini Combat 03")]
        public static void BuildGameplayGeminiCombat03()
        {
            BuildStandaloneTo("Builds/RiftboundCombatRevamp03", "GameplayGeminiCombat03");
        }

        [MenuItem("Riftbound/Build Gameplay Gemini Combat 04")]
        public static void BuildGameplayGeminiCombat04()
        {
            BuildStandaloneTo("Builds/RiftboundCombatRevamp04", "GameplayGeminiCombat04");
        }

        static void BuildStandaloneTo(string buildDir, string label)
        {
            Directory.CreateDirectory(buildDir);
            string exePath = Path.Combine(buildDir, "Riftbound.exe");

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { OutpostScene },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(opts);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"[CoopPrefabSetup] {label} build failed: " + report.summary.result);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[CoopPrefabSetup] {label} build succeeded: {exePath} ({report.summary.totalSize} bytes, warnings={report.summary.totalWarnings}, errors={report.summary.totalErrors})");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
