using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    public static class WalkDiagnostics
    {
        static WalkDiagnostics() { EditorApplication.delayCall += FirstCheck; }
        static void FirstCheck()
        {
            if (Application.isBatchMode || File.Exists("WalkAvatarComparison.txt")) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying)
            { EditorApplication.delayCall += FirstCheck; return; }
            if (EditorSceneManager.GetActiveScene().path == "Assets/RPG/Scenes/Outpost.unity")
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art/Brute" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.ToLowerInvariant().Contains("walk")) ((ModelImporter)AssetImporter.GetAtPath(path)).SaveAndReimport();
                }
                Inspect();
            }
        }
        [MenuItem("Riftbound/Inspect walking feet")]
        public static void Inspect()
        {
            var player = Object.FindFirstObjectByType<PlayerController>();
            if (!player) return;
            var controller = (AnimatorController)player.animator.runtimeAnimatorController;
            var state = controller.layers[0].stateMachine.states.First(s => s.state.name == "Walk").state;
            bool originalIK = state.iKOnFeet;
            var originalMotion = state.motion;
            var copy = Object.Instantiate(player.animator.gameObject);
            copy.hideFlags = HideFlags.HideAndDontSave;
            copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            copy.transform.localScale = player.animator.transform.lossyScale;
            var text = new StringBuilder();
            try
            {
                var animator = copy.GetComponent<Animator>(); animator.applyRootMotion = false;
                foreach (string file in new[] { "unarmed walk forward", "standing walk forward" })
                foreach (bool ik in new[] { true, false })
                {
                    state.motion = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/Brute/" + file + ".fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
                    state.iKOnFeet = ik;
                    animator.runtimeAnimatorController = null; animator.runtimeAnimatorController = controller;
                    animator.Rebind(); animator.Update(0);
                    float minDot = 1, maxDot = -1, minPitch = 90, maxPitch = -90, minToeY = float.MaxValue;
                    for (int i = 0; i < 60; i++)
                    {
                        animator.Play("Walk", 0, i / 60f); animator.Update(0);
                        foreach (bool left in new[] { true, false })
                        {
                            var foot = animator.GetBoneTransform(left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                            var toe = animator.GetBoneTransform(left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes);
                            var delta = toe.position - foot.position;
                            float dot = Vector3.Dot(delta.normalized, Vector3.forward);
                            float pitch = Mathf.Atan2(delta.y, new Vector2(delta.x, delta.z).magnitude) * Mathf.Rad2Deg;
                            minDot = Mathf.Min(minDot, dot); maxDot = Mathf.Max(maxDot, dot);
                            minPitch = Mathf.Min(minPitch, pitch); maxPitch = Mathf.Max(maxPitch, pitch); minToeY = Mathf.Min(minToeY, toe.position.y);
                        }
                    }
                    text.AppendLine($"{file}, Foot IK {ik}: foot forward dot {minDot:F3}..{maxDot:F3}; ankle-to-toe pitch {minPitch:F1}..{maxPitch:F1}; minimum toe Y {minToeY:F3}");
                }
                File.WriteAllText("WalkAvatarComparison.txt", text.ToString());
            }
            finally { state.iKOnFeet = originalIK; state.motion = originalMotion; Object.DestroyImmediate(copy); }
        }
    }
}
