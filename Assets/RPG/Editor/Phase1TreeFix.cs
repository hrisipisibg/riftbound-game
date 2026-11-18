using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class Phase1TreeFix
    {
        const string UprightMeshFolder = "Assets/RPG/Prefabs/Nature/UprightMeshes";

        public static void Run()
        {
            Directory.CreateDirectory(UprightMeshFolder);
            AssetDatabase.Refresh();

            int fixedPrefabs = 0;
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/RPG/Prefabs/Nature" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string fileName = Path.GetFileNameWithoutExtension(path);
                if (!fileName.StartsWith("T_")) continue;

                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var mf = root.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh) continue;

                    float upDot = Vector3.Dot(root.transform.up.normalized, Vector3.up);
                    if (upDot > 0.98f && Vector3.Distance(root.transform.localScale, Vector3.one) < 0.01f)
                        continue;

                    if (!fileName.Contains("Tree") && !fileName.Contains("Rock"))
                        continue;

                    string meshPath = $"{UprightMeshFolder}/{fileName}_Upright.asset";
                    Mesh upright = BakeMesh(mf.sharedMesh, root.transform.localRotation, root.transform.localScale);
                    var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (existing)
                    {
                        EditorUtility.CopySerialized(upright, existing);
                        UnityEngine.Object.DestroyImmediate(upright);
                        upright = existing;
                    }
                    else
                    {
                        AssetDatabase.CreateAsset(upright, meshPath);
                    }

                    mf.sharedMesh = upright;
                    root.transform.localRotation = Quaternion.identity;
                    root.transform.localScale = Vector3.one;

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    fixedPrefabs++;
                    Debug.Log($"Fixed terrain prototype prefab: {path}");
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();

            // Ground floating/buried scene trees in Outpost-Expanded.unity
            EditorSceneManager.OpenScene(WorldRegions.ScenePath, OpenSceneMode.Single);
            var terrains = Terrain.activeTerrains;
            int groundedSceneTrees = 0;

            var renderers = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude);
            foreach (var mr in renderers)
            {
                string n = mr.name.ToLowerInvariant();
                if (!mr.transform.parent || !mr.transform.parent.name.StartsWith("Expanded ")) continue;
                bool isTree = (n.Contains("tree") || n.Contains("pine") || n.Contains("oak") || n.Contains("birch"))
                    && !n.Contains("log") && !n.Contains("stump") && !n.Contains("fallen");
                if (!isTree) continue;

                var b = mr.bounds;
                float groundY = WorldRegions.GroundHeight(b.center, terrains);
                float minDelta = b.min.y - groundY;
                if (minDelta > 0.65f || minDelta < -1.8f)
                {
                    Transform target = mr.transform.parent;
                    target.position += Vector3.up * (-minDelta - 0.12f);
                    groundedSceneTrees++;
                }
            }

            foreach (var t in terrains) t.Flush();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Directory.CreateDirectory(".code-check");
            File.WriteAllText(".code-check/phase1-fix-result.txt",
                $"Fixed terrain tree/rock prefabs: {fixedPrefabs}\nGrounded floating/buried scene trees: {groundedSceneTrees}\n");
            Debug.Log($"Phase1TreeFix complete: prefabs={fixedPrefabs}, groundedTrees={groundedSceneTrees}");
            EditorApplication.Exit(0);
        }

        static Mesh BakeMesh(Mesh source, Quaternion rot, Vector3 scale)
        {
            var m = UnityEngine.Object.Instantiate(source);
            m.name = source.name + "_Upright";
            Matrix4x4 mtx = Matrix4x4.TRS(Vector3.zero, rot, scale);

            var verts = m.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] = mtx.MultiplyPoint3x4(verts[i]);
            m.vertices = verts;

            if (m.normals != null && m.normals.Length == verts.Length)
            {
                var norms = m.normals;
                for (int i = 0; i < norms.Length; i++) norms[i] = (rot * norms[i]).normalized;
                m.normals = norms;
            }
            else m.RecalculateNormals();

            if (m.tangents != null && m.tangents.Length == verts.Length)
            {
                var tans = m.tangents;
                for (int i = 0; i < tans.Length; i++)
                {
                    Vector3 t = rot * new Vector3(tans[i].x, tans[i].y, tans[i].z);
                    tans[i] = new Vector4(t.x, t.y, t.z, tans[i].w);
                }
                m.tangents = tans;
            }

            m.RecalculateBounds();
            return m;
        }
    }
}
