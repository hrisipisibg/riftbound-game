using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    [InitializeOnLoad]
    internal static class CC0WorldPreview
    {
        const string VerifyKey = "Riftbound.CC0World.Verifying";
        const string Evidence = ".code-check/cc0-world";

        static CC0WorldPreview()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(VerifyKey, false))
                    EditorApplication.update += WatchTownsfolk;
                if (state == PlayModeStateChange.EnteredEditMode)
                    EditorApplication.update -= WatchTownsfolk;
            };
        }

        public static void Verify()
        {
            SessionState.SetBool(VerifyKey, true);
            QuestRouteSmokeTest.Run();
        }

        static void WatchTownsfolk()
        {
            if (!EditorApplication.isPlaying || FrontendMenu.IsOpen) return;
            var npcs = Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None)
                .Where(npc => npc.npcName == "Emberwatch provisioner" || npc.npcName == "Greenhollow trail warden").ToArray();
            if (npcs.Length != 2) return;
            var report = new StringBuilder();
            foreach (var npc in npcs)
            {
                var rig = npc.GetComponentInChildren<Animator>();
                if (!rig || !rig.isHuman || !rig.avatar.isValid || !rig.GetBoneTransform(HumanBodyBones.Head)
                    .GetComponentsInChildren<MeshFilter>().Any(mesh => mesh.sharedMesh && mesh.sharedMesh.vertexCount > 50))
                    throw new InvalidOperationException(npc.npcName + " could not load the outfit avatar and head.");
                var state = rig.GetCurrentAnimatorStateInfo(0);
                if (!state.IsName("Idle") || state.normalizedTime < .10f) return;
                report.AppendLine($"PASS {npc.npcName} uses a valid Humanoid avatar and reached Idle at {state.normalizedTime:F2}.");
            }

            EditorApplication.update -= WatchTownsfolk;
            SessionState.SetBool(VerifyKey, false);
            File.WriteAllText(Evidence + "/townsfolk.txt", report.ToString());
            bool async = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var npc in npcs)
                {
                    string name = npc.npcName.StartsWith("Emberwatch") ? "riftbound-cc0-provisioner" : "riftbound-cc0-warden";
                    var camera = npc.transform.position + npc.transform.forward * 3.1f - npc.transform.right * 1.65f + Vector3.up * 1.65f;
                    WorldPresentationSetup.RenderPreview(name, camera, npc.transform.position + Vector3.up * 1.05f, false);
                    File.Copy($".code-check/{name}.png", $"{Evidence}/{name}.png", true);
                }
            }
            finally { ShaderUtil.allowAsyncCompilation = async; }
        }

        public static void ShowWestbank()
        {
            if (SceneManager.GetActiveScene().path != WorldRegions.ScenePath)
                UnityEditor.SceneManagement.EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            var sceneView = EditorWindow.GetWindow<SceneView>();
            var bridge = GameObject.Find("Westbank limestone arch");
            Selection.activeObject = bridge;
            sceneView.LookAtDirect(new Vector3(-255f, 5.6f, 49f), Quaternion.LookRotation(new Vector3(-17f, -12f, 23f)), 23f);
            sceneView.Focus();
        }
    }
}

