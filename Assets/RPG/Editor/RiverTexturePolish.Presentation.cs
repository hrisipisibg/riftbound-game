using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static partial class RiverTexturePolish
    {
        static (Transform transform, float before, float offset)[] RecordBankDressing()
        {
            var candidates = Object.FindObjectsByType<Transform>();
            return candidates.Where(t =>
            {
                string name = t.name;
                var p = t.position;
                return (Candidate(p.x, p.z, true) || Candidate(p.x, p.z, false)) &&
                    (name.Contains("Tree", StringComparison.OrdinalIgnoreCase) ||
                     name.Contains("Bush", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Rock_", StringComparison.Ordinal)) &&
                    Mathf.Abs(t.position.y - WorldRegions.GroundHeight(t.position)) < 3.2f &&
                    t.GetComponentInChildren<MeshRenderer>();
            }).Select(t => (t, t.position.y, t.position.y - WorldRegions.GroundHeight(t.position))).ToArray();
        }

        static void ReseatDressing((Transform transform, float before, float offset)[] props)
        {
            foreach (var prop in props)
            {
                var position = prop.transform.position;
                bool brook = position.z > 220;
                position = DryBankPosition(position, WorldRegions.GroundHeight(position), brook);
                position.y = WorldRegions.GroundHeight(position) + prop.offset;
                prop.transform.position = position;
                PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
            }
        }

        static void DressBanks()
        {
            var river = GameObject.Find("Westbank River and Willow Rest").transform;
            var previous = river.Find("Grounded waterline stone shelves");
            if (previous) Object.DestroyImmediate(previous.gameObject);
            var shelf = new GameObject("Grounded waterline stone shelves").transform;
            shelf.SetParent(river, false);
            var rock = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + "/Prefabs/Moss_Rock.prefab");
            var finish = SurfaceMaterial("bedrock", Vector2.one);
            var scene = river.gameObject.scene;
            // Stone groups frame the cascade, round the repaired cut, and continue into the lower pool.
            foreach (var (z, side, size) in new[] {
                (-151f, -1f, 1.48f), (-145f, 1f, 2.03f), (-139f, -1f, 1.35f), (-132f, 1f, 1.61f),
                (-122f, -1f, 1.31f), (-120f, 1f, 1.38f), (-117f, -1f, .96f),
                (40.5f, 1f, 1.04f), (56.3f, -1f, .79f), (58.7f, 1f, 1.25f), (62.3f, -1f, 1.11f),
                (135f, -1f, 1.1f), (143f, 1f, 1.32f), (156f, -1f, 1.6f),
                (171f, 1f, 1.3f), (178f, -1f, 1.43f), (184f, 1f, 1.6f)
            })
            {
                var bank = Section(Westbank, z);
                float x = Centerline(z, false) + side * (bank.width + size * .44f);
                var p = new Vector3(x, 0, z);
                p.y = WorldRegions.GroundHeight(p) - .12f;
                var boulder = (GameObject)PrefabUtility.InstantiatePrefab(rock, scene);
                boulder.transform.SetParent(shelf, false);
                boulder.transform.SetPositionAndRotation(p, Quaternion.Euler(0, z * 11, 0));
                boulder.transform.localScale = new Vector3(size * 1.34f, size, size * .87f);
                foreach (var renderer in boulder.GetComponentsInChildren<MeshRenderer>())
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Select((m, i) => m &&
                        m.name.Contains("Grass", StringComparison.OrdinalIgnoreCase) ? m : finish).ToArray();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                foreach (var collider in boulder.GetComponentsInChildren<Collider>())
                    collider.enabled = false;
            }
        }

        static float CheckRiver(string name, Mesh mesh, bool brook)
        {
            var waterObject = GameObject.Find(name);
            if (!waterObject || !waterObject.GetComponent<MeshRenderer>().enabled)
                throw new InvalidOperationException(name + " must have an enabled renderer.");
            var transform = waterObject.transform;
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            var colors = mesh.colors;
            if (vertices.Length < 600 || vertices.Length % 2 != 0 || mesh.vertexCount != colors.Length ||
                colors[0].a > .001f || colors[^1].a > .001f)
                throw new InvalidOperationException(name + " has invalid bank rows or a hard-cut endpoint.");
            float clearance = float.PositiveInfinity;
            int checkedRows = 0;
            for (int i = 0; i < vertices.Length; i += 2)
            {
                var center = transform.TransformPoint((vertices[i] + vertices[i + 1]) * .5f);
                if (i > 0 && uv[i].y <= uv[i - 2].y) throw new InvalidOperationException(name + " has backwards flow UVs.");
                if (colors[i].a < .78f) continue;
                float delta = center.y - WorldRegions.GroundHeight(center);
                if (delta < .10f)
                    throw new InvalidOperationException($"{name} is blocked by the terrain at {center:F2}, clearance {delta:F2} m.");
                clearance = Mathf.Min(clearance, delta);
                checkedRows++;
            }
            if (checkedRows < (brook ? 275 : 550)) throw new InvalidOperationException(name + " is missing its continuous channel.");
            return clearance;
        }

        public static void RunAndCapture()
        {
            Run();
            Capture();
        }

        public static void CheckCrossingsAndReaverRoute()
        {
            OriginalWorldRemodel.VerifySavedWorld();
            ReaverApproachChecks.Run();
        }

        public static void Capture()
        {
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            string folder = Path.GetFullPath("../riftbound-river-texture-frames");
            Directory.CreateDirectory(folder);
            var camera = new GameObject("River and surfaces review camera").AddComponent<Camera>();
            camera.nearClipPlane = .24f;
            camera.farClipPlane = 445;
            camera.fieldOfView = 50;
            camera.allowHDR = false;
            var target = RenderTexture.GetTemporary(1600, 900, 24);
            camera.targetTexture = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            var oldActive = RenderTexture.active;
            try
            {
                Shot("Westbank Crossing", new Vector3(-273, 11.0f, 39), new Vector3(-254, 4.55f, 52));
                Shot("Westbank Channel Past the Bridge", new Vector3(-276, 16.0f, 49), new Vector3(-256, 3.4f, 73));
                Shot("Westbank Spring Cascade", new Vector3(-264, 42f, -74), new Vector3(-262, 29.5f, -128));
                Shot("Willowmere Lower Pool", new Vector3(-275, 16.2f, 147), new Vector3(-273, 2.65f, 173));
                Shot("Willow Brook at Greenhollow", new Vector3(-25, 18.6f, 355), new Vector3(-18, 10.9f, 369));
                Shot("Emberwatch Courtyard", new Vector3(10, 11.9f, -14), new Vector3(0, 2.4f, 11));
                Shot("Redridge Grounds", new Vector3(283, 28.4f, 195), new Vector3(281, 20.6f, 220));
                Shot("Highspire Plaza", new Vector3(-197, 26, -37), new Vector3(-201, 17, -77));
                void Shot(string name, Vector3 at, Vector3 point)
                {
                    camera.fieldOfView = name == "Westbank Spring Cascade" ? 56 : 50;
                    camera.transform.position = at;
                    camera.transform.LookAt(point);
                    var request = new RenderPipeline.StandardRequest { destination = target };
                    if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                    else camera.Render();
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
                    Debug.Log($"Captured {name} from {WorldRegions.ScenePath}.");
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        [MenuItem("Riftbound/World/View the restored Westbank river")]
        public static void OpenWestbank()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode ||
                    !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var view = SceneView.lastActiveSceneView ?? EditorWindow.GetWindow<SceneView>();
                var point = new Vector3(-255, 5, 50);
                view.LookAt(point, Quaternion.LookRotation(point - new Vector3(-273, 14f, 37f)), 24f, false);
                view.Focus();
                view.Repaint();
            };
        }
    }
}
