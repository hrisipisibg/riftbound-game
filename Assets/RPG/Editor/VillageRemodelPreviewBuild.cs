using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound.Editor
{
    public sealed class VillageRemodelPreviewBuild : IProcessSceneWithReport
    {
        const string Product = "Riftbound Settlement Preview";
        static bool buildingPreview;

        public int callbackOrder => 0;

        public static void Run()
        {
            if (!Application.isBatchMode ||
                (PlayerSettings.productName != Product && PlayerSettings.productName != VillageTourMenu.Product))
            {
                Debug.LogError($"Build from an isolated project named {Product} or {VillageTourMenu.Product}.");
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
            StageRemodels(scene);
        }

        internal static void StageRemodels(Scene scene)
        {
            Stage(scene, "HighspireDistrict/Highspire East Ward", new Vector3(-138, 12.30f, -49), 10);
            Stage(scene, "EmberwatchCivicSquare/Emberwatch Civic Square", new Vector3(28, 0, -29), 8);
            Stage(scene, "VillageLandmarks/Emberwatch Supply Lane", new Vector3(43, 0, -40), 4);
            Stage(scene, "VillageLandmarks/Greenhollow Rest Stop", new Vector3(-33, 0, 377), 3);
            Stage(scene, "GorKreshForgeCourt/GorKresh Forge Court", new Vector3(340, 0, 269), 5);
            var rustwater = Stage(scene, "RustwaterSalvageYard/Rustwater Salvage Yard", new Vector3(224, 0, 171), 5);
            var axles = rustwater.GetComponentsInChildren<AmbientAxle>();
            if (axles.Length != 1 || axles[0].GetComponentInChildren<Collider>() ||
                !axles[0].transform.parent.GetComponent<BoxCollider>())
                throw new BuildFailedException("Rustwater's sorting drum must have one visual axle and separate fixed collision.");

            var cloister = Stage(scene, "DeathknellCloister/Deathknell Cloister", new Vector3(-242.5f, 0, 156.8f), 6);
            if (cloister.GetComponentsInChildren<Light>().Length != 2)
                throw new BuildFailedException("Deathknell's cloister requires both approach lanterns.");

            Stage(scene, "SilverglenArrivalCourt/Silverglen Arrival Court", new Vector3(-130, 0, 55), 11);
            Stage(scene, "FrostpeakTrailStation/Frostpeak Trail Station", new Vector3(-112, 0, -109), 6);
            var landing = Stage(scene, "WestbankFerryLanding/Westbank Ferry Landing", new Vector3(-270, 0, 41), 4);
            if (landing.GetComponentsInChildren<MeshFilter>().Length != 5 || !landing.GetComponentInChildren<LineRenderer>())
                throw new BuildFailedException("Westbank's moored skiff is missing from the landing.");
            Stage(scene, "BloodrockLookout/Bloodrock Trail Lookout", new Vector3(294, 0, 184.5f), 5);

            if (PlayerSettings.productName == VillageTourMenu.Product)
            {
                var tour = new GameObject("Village Tour");
                SceneManager.MoveGameObjectToScene(tour, scene);
                tour.AddComponent<VillageTourMenu>();
                Debug.Log("Staged the offline village tour.");
            }
        }

        static GameObject Stage(Scene scene, string model, Vector3 position, int meshCount)
        {
            var path = "Assets/RPG/Art/" + model + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!prefab) throw new BuildFailedException($"Missing village prefab {path}.");

            var instance = scene.GetRootGameObjects().FirstOrDefault(root =>
                PrefabUtility.GetCorrespondingObjectFromSource(root) == prefab ||
                root.name == prefab.name && (root.transform.position - position).sqrMagnitude < .0001f);
            if (!instance) instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.position = position;
            var colliders = instance.GetComponentsInChildren<MeshCollider>();
            if (colliders.Length != meshCount)
                throw new BuildFailedException($"{prefab.name}: expected {meshCount} mesh colliders, got {colliders.Length}.");
            foreach (var collider in colliders)
                if (!collider.sharedMesh || !collider.GetComponent<MeshRenderer>().sharedMaterial)
                    throw new BuildFailedException($"{prefab.name} has a missing mesh or material.");
            Debug.Log($"Village preview staged {prefab.name} with {colliders.Length} model colliders.");
            return instance;
        }
    }
}
