using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Riftbound.Networking;

namespace Riftbound.Editor
{
    public static class Phase4NetworkSetup
    {
        static AddRequest addRequest;
        const string PrefabPath = "Assets/RPG/Prefabs/NetworkPlayerAvatar.prefab";
        const string ScenePath = "Assets/RPG/Scenes/NetworkTest.unity";

        public static void InstallNetcode()
        {
            Debug.Log("Installing com.unity.netcode.gameobjects via UPM...");
            addRequest = Client.Add("com.unity.netcode.gameobjects");
            EditorApplication.update += PollAdd;
        }

        static void PollAdd()
        {
            if (addRequest == null || !addRequest.IsCompleted) return;
            EditorApplication.update -= PollAdd;

            Directory.CreateDirectory(".code-check");
            if (addRequest.Status == StatusCode.Success)
            {
                string msg = $"PASS: Installed {addRequest.Result.packageId} (version {addRequest.Result.version})";
                File.WriteAllText(".code-check/phase4-upm-result.txt", msg + "\n");
                Debug.Log(msg);
                EditorApplication.Exit(0);
            }
            else
            {
                string err = $"FAIL: {addRequest.Error?.message}";
                File.WriteAllText(".code-check/phase4-upm-result.txt", err + "\n");
                Debug.LogError(err);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildTestSceneAndPrefab()
        {
            Directory.CreateDirectory("Assets/RPG/Prefabs");
            Directory.CreateDirectory("Assets/RPG/Scenes");

            // 1. Create NetworkPlayerAvatar Prefab
            var avatarGo = new GameObject("NetworkPlayerAvatar");
            var cc = avatarGo.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.42f;
            cc.center = Vector3.up * 0.95f;

            avatarGo.AddComponent<NetworkObject>();
            avatarGo.AddComponent<NetworkTransform>();
            avatarGo.AddComponent<RiftboundNetworkPlayer>();

            var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            capsule.name = "BodyVisual";
            var capCol = capsule.GetComponent<Collider>();
            if (capCol) Object.DestroyImmediate(capCol);
            capsule.transform.SetParent(avatarGo.transform, false);
            capsule.transform.localPosition = Vector3.up * 1.0f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(avatarGo, PrefabPath);
            Object.DestroyImmediate(avatarGo);

            // 2. Create Additive NetworkTest Scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "ArenaGround";
            ground.transform.localScale = new Vector3(6f, 1f, 6f);
            var gMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            gMat.SetColor("_BaseColor", new Color(0.24f, 0.34f, 0.22f));
            ground.GetComponent<Renderer>().sharedMaterial = gMat;

            var nmGo = new GameObject("NetworkManager");
            var nm = nmGo.AddComponent<NetworkManager>();
            var utp = nmGo.AddComponent<UnityTransport>();
            nmGo.AddComponent<LanSessionManager>();

            nm.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = utp,
                PlayerPrefab = prefab,
                ConnectionApproval = true
            };
            nm.AddNetworkPrefab(prefab);

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Ensure NetworkTest.unity is in EditorBuildSettings without removing existing scenes
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            AssetDatabase.SaveAssets();
            Directory.CreateDirectory(".code-check");
            File.WriteAllText(".code-check/phase4-scene-result.txt", $"PASS: Created {PrefabPath} and {ScenePath}\n");
            Debug.Log($"Phase4NetworkSetup complete: {PrefabPath}, {ScenePath}");
            EditorApplication.Exit(0);
        }

        public static void BuildNetworkTestExecutable()
        {
            Directory.CreateDirectory("Builds/NetworkTest");
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/NetworkTest/RiftboundNetTest.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(options);
            Directory.CreateDirectory(".code-check");
            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                File.WriteAllText(".code-check/phase4-build-result.txt", $"PASS: Built Builds/NetworkTest/RiftboundNetTest.exe ({report.summary.totalSize} bytes)\n");
                Debug.Log("NetworkTest executable build succeeded.");
                EditorApplication.Exit(0);
            }
            else
            {
                File.WriteAllText(".code-check/phase4-build-result.txt", $"FAIL: {report.summary.result} errors={report.summary.totalErrors}\n");
                Debug.LogError($"NetworkTest executable build failed: {report.summary.totalErrors} errors");
                EditorApplication.Exit(1);
            }
        }

        static int verifyStage;
        static double verifyTimeout;

        public static void VerifyHostInTestScene()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            verifyStage = 0;
            verifyTimeout = EditorApplication.timeSinceStartup + 25.0;
            EditorApplication.update += TickVerify;
            EditorApplication.isPlaying = true;
        }

        static void TickVerify()
        {
            if (EditorApplication.timeSinceStartup > verifyTimeout)
            {
                EditorApplication.update -= TickVerify;
                File.WriteAllText(".code-check/phase4-verify-result.txt", "FAIL: Timeout waiting for NetworkTest PlayMode verification.\n");
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(1);
                return;
            }

            if (!EditorApplication.isPlaying) return;

            if (verifyStage == 0)
            {
                if (LanSessionManager.Instance == null || NetworkManager.Singleton == null) return;
                LanSessionManager.Instance.StartHostLan();
                verifyStage = 1;
                return;
            }

            if (verifyStage == 1)
            {
                var nm = NetworkManager.Singleton;
                if (!nm || !nm.IsHost) return;
                var players = Object.FindObjectsByType<RiftboundNetworkPlayer>(FindObjectsSortMode.None);
                if (players.Length == 0 || !players[0].IsOwner) return;

                string ok = $"PASS: Host started, ConnectedClients={nm.ConnectedClientsList.Count}, LocalOwnerSpawned={players[0].name}";
                LanSessionManager.Instance.DisconnectLan();
                EditorApplication.update -= TickVerify;
                File.WriteAllText(".code-check/phase4-verify-result.txt", ok + "\n");
                Debug.Log(ok);
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(0);
            }
        }
    }
}
