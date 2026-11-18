using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class NPCInspect
    {
        [MenuItem("Riftbound/Inspect Downloaded NPCs")]
        public static void Inspect()
        {
            var sb = new StringBuilder();
            string[] fbxFiles = Directory.GetFiles("Assets/RPG/Art/NPCs", "*.fbx");
            foreach (var fbx in fbxFiles)
            {
                string path = fbx.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                var clips = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).ToArray();
                sb.AppendLine($"MODEL: {Path.GetFileName(path)} | Clips: {clips.Length}");
                foreach (var c in clips)
                {
                    sb.AppendLine($"  - Clip: '{c.name}' (Length: {c.length:F2}s, isLooping: {c.isLooping})");
                }
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go)
                {
                    var anim = go.GetComponentInChildren<Animator>();
                    var smr = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                    sb.AppendLine($"  - SkinnedMeshRenderers: {smr.Length}, Animator: {(anim != null)}");
                    if (smr.Length > 0)
                    {
                        foreach (var s in smr)
                        {
                            sb.AppendLine($"    * SMR: {s.name}, Bones: {s.bones?.Length ?? 0}, Materials: {s.sharedMaterials.Length}");
                            foreach (var m in s.sharedMaterials) if (m) sb.AppendLine($"      Mat: {m.name}");
                        }
                    }
                }
            }
            File.WriteAllText("NPCInspectResult.txt", sb.ToString());
            Debug.Log("NPC Inspect finished:\n" + sb.ToString());
        }
    }
}
