using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static class EmberfordPresentation
    {
        [MenuItem("Riftbound/World/View the remodeled campaign")]
        public static void OpenForReview()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode ||
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                Vector3 target = Lookout(0, 8, 1.5f);
                view.LookAt(target, Quaternion.LookRotation(target - Lookout(0, -22, 16.6f)), 27f, false);
                view.Focus();
                view.Repaint();
            };
        }

        public static void BuildReview()
        {
            OriginalWorldRemodel.Run();
            Capture();
            OriginalWorldRemodel.VerifySavedWorld();
            GameplayCandidateBuild.Run();
        }

        public static void RemodelAndCapture()
        {
            OriginalWorldRemodel.Run();
            Capture();
            OriginalWorldRemodel.VerifySavedWorld();
        }

        public static void Capture()
        {
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            string directory = Path.GetFullPath("../original-world-frames");
            Directory.CreateDirectory(directory);
            var camera = new GameObject("Emberford review camera").AddComponent<Camera>();
            camera.fieldOfView = 49;
            camera.nearClipPlane = .35f;
            camera.farClipPlane = 400;
            camera.allowHDR = false;
            var target = RenderTexture.GetTemporary(1600, 900, 24);
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var original = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                Shot("Emberwatch Outpost", Lookout(10, -14, 11.5f), Lookout(0, 11, 2.3f));
                Shot("Highspire Keep", Lookout(-194, -37, 15.5f), Lookout(-201, -76, 6f));
                Shot("Frostpeak Anvil", Lookout(-85, -109, 13.6f), Lookout(-97, -86, 2.9f));
                Shot("Silverglen Sanctum", Lookout(-144, 59, 12.7f), Lookout(-151, 82, 3.3f));
                Shot("Bloodrock War Village", Lookout(277, 198, 10.85f), Lookout(280, 222, 3f));
                Shot("Deathknell Hollow", Lookout(-236, 165, 14.1f), Lookout(-240, 189, 3f));
                Shot("Rustwater Scrapworks", Lookout(227, 135, 10.9f), Lookout(242, 154, 2.9f));
                Shot("GorKresh Stronghold", Lookout(345, 282, 16.4f), Lookout(338, 313, 5.4f));
                Shot("Ashwood Supply Camp", Lookout(14, 56, 8.4f), Lookout(27, 67, 1.2f));
                Shot("Captain Ruins", Lookout(32, 77, 10.7f), Lookout(35, 94, 1.5f));
                Shot("Greenhollow Post", Lookout(-44, 388, 8.5f), Lookout(-44, 407, 1.5f));
                Shot("Westbank Crossing", Lookout(-276, 42.5f, 5.8f), Lookout(-253, 49, 2.3f));
                Shot("Scarlet Cathedral", Lookout(-215, 242, 9.4f), Lookout(-220, 260, 3.2f));
                Shot("Wyrmcrest Summit", Lookout(248, 491, 10f), Lookout(250, 511, 3.8f));
                void Shot(string name, Vector3 position, Vector3 subject)
                {
                    camera.transform.position = position;
                    camera.transform.LookAt(subject);
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    if (RenderPipeline.SupportsRenderRequest(camera, request))
                        RenderPipeline.SubmitRenderRequest(camera, request);
                    else camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
                    Debug.Log($"Captured {name} from the playable campaign scene.");
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = original;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        static Vector3 Lookout(float x, float z, float eye) =>
            new Vector3(x, WorldRegions.GroundHeight(new Vector3(x, 0, z)) + eye, z);
    }
}
