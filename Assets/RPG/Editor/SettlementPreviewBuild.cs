using System;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    public sealed class SettlementPreviewBuild : IProcessSceneWithReport
    {
        const string Product = "Riftbound Settlement Preview";
        static bool buildingPreview;

        public int callbackOrder => 0;

        public static void Run()
        {
            if (!Application.isBatchMode || PlayerSettings.productName != Product)
            {
                Debug.LogError($"Build the settlement preview from an isolated project named {Product}.");
                EditorApplication.Exit(1);
                return;
            }

            buildingPreview = true;
            try { GameplayCandidateBuild.Run(); }
            finally { buildingPreview = false; }
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!buildingPreview || report == null || scene.path != WorldRegions.ScenePath) return;

            var previous = SceneManager.GetActiveScene();
            if (scene != previous) SceneManager.SetActiveScene(scene);
            try
            {
                bool threeChimes = Array.IndexOf(Environment.GetCommandLineArgs(), "-includeThreeChimes") >= 0;
                var placement = SettlementPlacementPreview.Create(new StringBuilder(), threeChimes);
                if (threeChimes)
                {
                    var relay = ThreeChimesPreview.Create(new StringBuilder());
                    if (relay.Models.Length != 4 || relay.Roads.Length != 4)
                        throw new BuildFailedException("The Three-Chimes depot and approach were not fully staged.");
                    Debug.Log("Settlement preview: Three-Chimes Relay staged at Lantern Rest.");
                }
                foreach (var model in placement.Models)
                {
                    if (!model || model.gameObject.scene != scene || !model.sharedMesh)
                        throw new BuildFailedException("An expected settlement model was not included in the build scene.");
                }
                Debug.Log($"Settlement preview: staged {placement.Models.Length} models in {scene.name}.");
                bool trailheads = Array.IndexOf(Environment.GetCommandLineArgs(), "-includeSettlementTrailheads") >= 0;
                if (trailheads || Array.IndexOf(Environment.GetCommandLineArgs(), "-includeCoilbreakYard") >= 0)
                {
                    var yard = CoilbreakYardPreview.Create(new StringBuilder());
                    foreach (var part in yard)
                        if (!part || part.gameObject.scene != scene)
                            throw new BuildFailedException("An expected Coilbreak Yard placement was missing from the build scene.");
                    Debug.Log($"Settlement preview: staged {yard.Count} Coilbreak Yard placements in {scene.name}.");
                }
                if (trailheads)
                {
                    var shelter = WyrmcrestRefugePreview.Create(new StringBuilder());
                    if (!shelter || shelter.gameObject.scene != scene || shelter.childCount != 9)
                        throw new BuildFailedException("The trailhead shelter was not fully staged in the build scene.");
                    Debug.Log($"Settlement preview: staged the Gor'Kresh trailhead shelter in {scene.name}.");
                    if (Array.IndexOf(Environment.GetCommandLineArgs(), "-includeSettlementActivity") >= 0)
                        SettlementActivityPreview.Create(new StringBuilder(), threeChimes);
                }
            }
            finally
            {
                if (previous != scene && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
    }
}
