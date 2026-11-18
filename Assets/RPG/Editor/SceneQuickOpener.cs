using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class SceneQuickOpener
    {
        static string OutpostScenePath => System.IO.File.Exists(WorldRegions.ScenePath) ? WorldRegions.ScenePath : "Assets/RPG/Scenes/Outpost-Updated.unity";
        const string DirectPlayPrefKey = "Riftbound.DirectPlayInEditor";

        static SceneQuickOpener()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        static void OnEditorReady()
        {
            // If Editor is opened with an untitled or empty scene, automatically load Outpost-Updated
            var currentScene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(currentScene.name) || currentScene.name == "SampleScene" || currentScene.path == "")
            {
                Debug.Log($"<color=#ffb442>[Riftbound]</color> Current scene is empty or untitled. Auto-loading: {OutpostScenePath}");
                OpenOutpostScene();
            }
        }

        [MenuItem("Riftbound/🌟 Open & Focus Outpost Scene %#o", priority = 0)]
        public static void OpenOutpostScene()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogWarning("[Riftbound] Exit Play Mode before switching editor scenes.");
                return;
            }

            if (SceneManager.GetActiveScene().path != OutpostScenePath)
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(OutpostScenePath, OpenSceneMode.Single);
                    Debug.Log($"<color=#70df80>[Riftbound]</color> Successfully loaded {OutpostScenePath}");
                }
            }

            FocusPlayerInSceneView();
            EnsureBuildSettings();
        }

        [MenuItem("Riftbound/🎮 Direct Play in Editor (Toggle)", priority = 1)]
        public static void ToggleDirectPlay()
        {
            bool current = EditorPrefs.GetBool(DirectPlayPrefKey, false);
            EditorPrefs.SetBool(DirectPlayPrefKey, !current);
            Debug.Log($"<color=#ffb442>[Riftbound]</color> Direct Play in Editor set to: {!current}. {( !current ? "(Full frontend menu shown on Play)" : "(Menu is bypassed on Play - instant testing)" )}");
        }

        [MenuItem("Riftbound/🎮 Direct Play in Editor (Toggle)", validate = true)]
        public static bool ValidateToggleDirectPlay()
        {
            Menu.SetChecked("Riftbound/🎮 Direct Play in Editor (Toggle)", EditorPrefs.GetBool(DirectPlayPrefKey, false));
            return true;
        }

        [MenuItem("Riftbound/⚙️ Ensure Outpost-Updated in Build Settings", priority = 2)]
        public static void EnsureBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;
            bool found = false;
            for (int i = 0; i < scenes.Length; i++)
            {
                if (scenes[i].path == OutpostScenePath)
                {
                    found = true;
                    if (i != 0)
                    {
                        var temp = scenes[0];
                        scenes[0] = scenes[i];
                        scenes[i] = temp;
                        scenes[0].enabled = true;
                        EditorBuildSettings.scenes = scenes;
                        Debug.Log($"<color=#70df80>[Riftbound]</color> Moved {OutpostScenePath} to Index 0 in Build Settings.");
                    }
                    break;
                }
            }

            if (!found)
            {
                var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
                newScenes[0] = new EditorBuildSettingsScene(OutpostScenePath, true);
                for (int i = 0; i < scenes.Length; i++)
                {
                    newScenes[i + 1] = scenes[i];
                }
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"<color=#70df80>[Riftbound]</color> Added {OutpostScenePath} as Scene 0 in Build Settings.");
            }
        }

        [MenuItem("Riftbound/🎯 Focus Camera on Player", priority = 3)]
        public static void FocusPlayerInSceneView()
        {
            var player = GameObject.FindWithTag("Player") ?? GameObject.Find("Player") ?? GameObject.Find("Brute - Player") ?? (UnityEngine.Object.FindFirstObjectByType<PlayerController>()?.gameObject);
            if (player != null)
            {
                if (!player.CompareTag("Player"))
                {
                    try { player.tag = "Player"; } catch { }
                }
                Selection.activeGameObject = player;
                if (SceneView.lastActiveSceneView != null)
                {
                    SceneView.lastActiveSceneView.FrameSelected();
                    Debug.Log("<color=#70df80>[Riftbound]</color> Scene view camera framed on Player (" + player.name + ")!");
                }
            }
            else
            {
                Debug.LogWarning("[Riftbound] Player GameObject not found in current scene. Ensure Outpost-Updated.unity is open.");
            }
        }
    }
}
