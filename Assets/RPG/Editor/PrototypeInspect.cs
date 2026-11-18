using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class PrototypeInspect
    {
        static PrototypeInspect() { EditorApplication.delayCall += Inspect; }
        [MenuItem("Riftbound/Extract Brute embedded textures")]
        public static void ExtractBruteTextures()
        {
            const string folder = "Assets/RPG/Art/Brute/Textures";
            Directory.CreateDirectory(folder);
            var importer = AssetImporter.GetAtPath("Assets/RPG/Art/Brute/Brute.fbx") as ModelImporter;
            bool extracted = importer != null && importer.ExtractTextures(folder);
            AssetDatabase.Refresh();
            File.WriteAllText("PrototypeTextureResult.txt", "Extracted: " + extracted + "\n" + string.Join("\n", Directory.GetFiles(folder)));
        }
        [MenuItem("Riftbound/Inspect imported assets")]
        public static void Inspect()
        {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) { EditorApplication.delayCall += Inspect; return; }
            var text = new StringBuilder();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!model) continue;
                var bounds = new Bounds(); bool first = true;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) { if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds); }
                text.AppendLine(path + " | size=" + bounds.size + " center=" + bounds.center);
                foreach (var r in model.GetComponentsInChildren<Renderer>())
                    foreach (var mat in r.sharedMaterials) if(mat) text.AppendLine(" material=" + mat.name + " texture=" + (mat.mainTexture ? mat.mainTexture.name : "none") + " color=" + mat.color);
                if (path.Contains("Brute/")) foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__")) text.AppendLine(" clip=" + clip.name + " length=" + clip.length + " human=" + clip.humanMotion);
            }
            File.WriteAllText("PrototypeAssetReport.txt", text.ToString());
        }
    }
}
