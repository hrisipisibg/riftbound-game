using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class AbeSetup
    {
        const string ModelPath = "Assets/RPG/Art/Abe/Ch39_nonPBR@Old Man Idle.fbx";
        static AbeSetup() { EditorApplication.delayCall += FirstSetup; }
        static void FirstSetup()
        {
            if (Application.isBatchMode || File.Exists("AbeSetupResult.txt")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            { EditorApplication.delayCall += FirstSetup; return; }
            if (!EditorSceneManager.GetActiveScene().isDirty && EditorSceneManager.GetActiveScene().path == "Assets/RPG/Scenes/Outpost.unity") Setup();
        }
        [MenuItem("Riftbound/Set up Abe quest giver")]
        public static void Setup()
        {
            if (Application.isBatchMode) EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost.unity");
            if (EditorApplication.isPlaying) return;
            try
            {
                var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
                if (!player) throw new InvalidOperationException("Open the Outpost scene first.");
                var existing = UnityEngine.Object.FindFirstObjectByType<QuestGiver>();
                if (existing) { player.questGiver = existing.transform; Save(); return; }
                var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
                PrototypeImport.ConfigureAnimations(importer, ModelPath);
                const string textures = "Assets/RPG/Art/Abe/Textures";
                Directory.CreateDirectory(textures); importer.ExtractTextures(textures);
                AssetDatabase.Refresh(); importer.SaveAndReimport();
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var clip = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                var oldGiver = player.questGiver;
                var root = new GameObject("Abe - Outpost Quest Giver");
                if (oldGiver) { root.transform.SetParent(oldGiver.parent); root.transform.position = oldGiver.position; oldGiver.gameObject.SetActive(false); }
                else root.transform.position = new Vector3(-3, 0, 2);
                root.transform.rotation = Quaternion.Euler(0, 180, 0);
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(source);
                visual.transform.SetParent(root.transform, false);
                var renderers = visual.GetComponentsInChildren<Renderer>();
                var bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                visual.transform.localScale *= 1.85f / bounds.size.y;
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Abe clothing and skin" };
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "/Ch39_1001_Diffuse.png"));
                material.SetFloat("_Smoothness", .15f);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textures + "/Ch39_1001_Normal.png");
                if (normal) { material.SetTexture("_BumpMap", normal); material.EnableKeyword("_NORMALMAP"); }
                AssetDatabase.CreateAsset(material, "Assets/RPG/Materials/Abe.mat");
                foreach (var renderer in renderers) renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                var controller = AnimatorController.CreateAnimatorControllerAtPath("Assets/RPG/Animations/Abe.controller");
                var idle = controller.layers[0].stateMachine.AddState("Idle"); idle.motion = clip;
                controller.layers[0].stateMachine.defaultState = idle;
                var animator = visual.GetComponentInChildren<Animator>();
                animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
                animator.Rebind(); animator.Update(0);
                Physics.SyncTransforms();
                float ground = -.15f;
                if (Physics.Raycast(root.transform.position + Vector3.up, Vector3.down, out var hit, 4)) ground = hit.point.y;
                var left = animator.GetBoneTransform(HumanBodyBones.LeftToes) ?? animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                var right = animator.GetBoneTransform(HumanBodyBones.RightToes) ?? animator.GetBoneTransform(HumanBodyBones.RightFoot);
                if (left && right) visual.transform.position += Vector3.up * (ground + .08f - Mathf.Min(left.position.y, right.position.y));
                var collision = root.AddComponent<CapsuleCollider>(); collision.height = 1.85f; collision.radius = .32f; collision.center = Vector3.up * .8f;
                root.AddComponent<QuestGiver>(); player.questGiver = root.transform;
                if (!animator.avatar || !animator.avatar.isValid || !clip.isLooping || !material.GetTexture("_BaseMap"))
                    throw new InvalidOperationException("Abe avatar, idle loop or diffuse texture failed validation.");
                Save();
                File.WriteAllText("AbeSetupResult.txt", $"PASS: Abe installed; valid humanoid avatar; idle loops ({clip.length:F2}s); embedded diffuse texture assigned; original beacon disabled; player references Abe.");
            }
            catch (Exception exception) { File.WriteAllText("AbeSetupResult.txt", "FAILED: " + exception); Debug.LogException(exception); }
        }
        static void Save()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene()); AssetDatabase.SaveAssets();
        }
    }
}
