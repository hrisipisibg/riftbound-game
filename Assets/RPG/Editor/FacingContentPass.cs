using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class FacingContentPass
    {
        public static void BatchPrepare()
        {
            try
            {
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                CharacterModelBuilder.BuildAll();
                if (!File.ReadAllText("CharacterModelBuildResult.txt").StartsWith("PASS"))
                    throw new InvalidOperationException("Character generation failed. See CharacterModelBuildResult.txt.");
                var report = new StringBuilder();
                StylizedRacialModelSystem.UpgradeAllSceneModelsAndMaterials(report);
                ExplorationSites.Ensure();
                RiverReaverCamp.Ensure();
                DressReaverCamp();
                foreach (string race in CharacterVisualFactory.AllianceRaces.Concat(CharacterVisualFactory.HordeRaces))
                    foreach (string cls in CharacterVisualFactory.AllClasses)
                    {
                        var prefab = Resources.Load<GameObject>($"Characters/{race}_{cls}");
                        if (!prefab || !prefab.GetComponentInChildren<Animator>()) throw new InvalidOperationException("Missing character: " + race + " " + cls);
                        if (race != "Undead") continue;
                        var animator = prefab.GetComponentInChildren<Animator>();
                        if (Quaternion.Angle(animator.transform.localRotation, Quaternion.Euler(0, 180, 0)) > .1f)
                            throw new InvalidOperationException("Undead visual is reversed: " + cls);
                        report.AppendLine("PASS corrected Undead " + cls);
                    }
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.SaveAssets();
                File.WriteAllText("FacingContentResult.txt", "PASS\n" + report);
                EditorApplication.Exit(0);
            }
            catch (Exception e) { File.WriteAllText("FacingContentResult.txt", "FAILED\n" + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                ExplorationSites.Ensure();
                RiverReaverCamp.Ensure();
                DressReaverCamp();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                Directory.CreateDirectory(".code-check");
                foreach (var enemy in GameObject.Find("River Reaver camp").GetComponentsInChildren<EnemyController>())
                    if (enemy.animator) { enemy.animator.Rebind(); enemy.animator.Play("Idle", 0, 0); enemy.animator.Update(0); }
                WorldPresentationSetup.RenderPreview("River-Reaver-Camp", RiverReaverCamp.Position + new Vector3(21, 18, -22), RiverReaverCamp.Position + Vector3.up, false);
                foreach (string cls in new[] { "Warrior", "Mage", "Rogue" })
                {
                    var model = CharacterVisualFactory.CreateVisualInstance("Undead", cls);
                    model.transform.position = new Vector3(0, -400, 0);
                    var animator = model.GetComponentInChildren<Animator>();
                    foreach (string state in new[] { "Idle", "Run", "Attack" })
                    {
                        animator.Play(state, 0, .3f); animator.Update(0);
                        CharacterArtSetup.RenderPose(model, ".code-check/Facing-" + cls + "-" + state + ".png");
                    }
                    UnityEngine.Object.DestroyImmediate(model);
                }
                PrototypeBuild.BuildWindows();
                EditorApplication.Exit(File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded") ? 0 : 1);
            }
            catch (Exception e) { File.WriteAllText("FacingBuildResult.txt", "FAILED\n" + e); Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void DressReaverCamp()
        {
            var camp = GameObject.Find("River Reaver camp");
            if (!camp || camp.transform.Find("Reaver provisions")) return;
            var parent = new GameObject("Reaver provisions").transform;
            parent.SetParent(camp.transform, false);
            AddProp("Stall_Cart_Empty", new Vector3(-6, 0, 5), 30, 1.6f, parent);
            AddProp("Crate_Wooden", new Vector3(6, 0, 5), 15, 1.1f, parent);
            AddProp("Crate_Wooden", new Vector3(7.2f, 0, 6), -15, .8f, parent);
            AddProp("Barrel", new Vector3(-3, 0, 7), 0, 1.1f, parent);
            AddProp("Barrel", new Vector3(-4.2f, 0, 7.5f), 0, .9f, parent);
            AddProp("Table_Large", new Vector3(0, 0, 7), 90, .8f, parent);
            AddProp("Cauldron", new Vector3(3, 0, 7), 0, 1, parent);
        }

        static void AddProp(string name, Vector3 offset, float yaw, float height, Transform parent)
        {
            var point = RiverReaverCamp.Position + offset;
            point.y = WorldRegions.GroundHeight(point);
            AdventureExpansion.Model("FantasyProps/FBX/" + name + ".fbx", point, yaw, height, parent, true);
        }
    }
}
