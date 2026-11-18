using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class ReaverRepairApplication
    {
        const string Source = "Assets/RPG/Terrain/WorldExpansion/World tile 0-0.asset";
        const string Graded = "Assets/RPG/Terrain/ReaverCamp/Reaver approach.asset";
        const string Target = "Assets/RPG/Terrain/ReaverCamp/Reaver approach preserved.asset";

        static string Hash(string path)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
        }

        public static void ApplyAndVerify()
        {
            TerrainData candidate = null;
            try
            {
                if (!Application.isBatchMode) throw new InvalidOperationException("Coordinated batch operation only.");
                if (!File.ReadAllText(".coordination/scene-status.md").Contains("**SCENE RELEASED**"))
                    throw new InvalidOperationException("Explicit scene release is required.");
                if (File.Exists(Target) || File.Exists(Target + ".meta")) throw new InvalidOperationException("Destination exists; refusing replacement.");
                string sourceHash = Hash(Source), gradedHash = Hash(Graded), sceneHash = Hash(WorldRegions.ScenePath);
                if (!File.ReadAllText(".code-check/world-audit-live-before.json").Contains(sourceHash)
                    || !File.ReadAllText(".code-check/world-audit-live-before.json").Contains(gradedHash)
                    || !File.ReadAllText(".code-check/world-audit-live-before.json").Contains(sceneHash))
                    throw new InvalidOperationException("Reviewed inputs changed; regenerate/review preview first.");
                // Scene loading unloads unpersisted TerrainData, so create the candidate afterwards.
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var source = AssetDatabase.LoadAssetAtPath<TerrainData>(Source);
                var graded = AssetDatabase.LoadAssetAtPath<TerrainData>(Graded);
                if (source.treeInstanceCount != 813 || source.treePrototypes.Length != 15)
                    throw new InvalidOperationException("Vegetation baseline changed.");
                var report = new StringBuilder($"Localized Reaver repair UTC={DateTime.UtcNow:O}\n");
                candidate = ReaverRepairPreparation.BuildCandidate(source, graded, report);
                ReaverRepairPreparation.VerifyPreservedData(source, candidate, report);
                var terrain = WorldRegions.TerrainAt(RiverReaverCamp.Position);
                if (!terrain || terrain.terrainData != source || terrain.transform.position != new Vector3(-320, -8, -160))
                    throw new InvalidOperationException("Unexpected camp terrain assignment/origin.");
                var collider = terrain.GetComponent<TerrainCollider>();
                if (!collider || collider.terrainData != source) throw new InvalidOperationException("Terrain collider mismatch.");
                string backup = ".code-check/reaver-before-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + ".unity";
                File.Copy(WorldRegions.ScenePath, backup, false);
                candidate.name = "Reaver approach preserved";
                AssetDatabase.CreateAsset(candidate, Target);
                terrain.terrainData = candidate; collider.terrainData = candidate;
                terrain.Flush(); Physics.SyncTransforms();
                float grade = ReaverCampTerrainSetup.CheckApproach();
                if (!AssetDatabase.GetAssetPath(WorldRegions.TerrainAt(RiverReaverCamp.Position).terrainData).Contains("/ReaverCamp/"))
                    throw new InvalidOperationException("Existing separate-terrain assertion failed.");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                AssetDatabase.ImportAsset(Target, ImportAssetOptions.ForceSynchronousImport);
                candidate = AssetDatabase.LoadAssetAtPath<TerrainData>(Target);
                ReaverRepairPreparation.VerifyPreservedData(source, candidate, report);
                if (Hash(Source) != sourceHash || Hash(Graded) != gradedHash) throw new Exception("Original terrain bytes changed.");
                report.AppendLine($"PASS saved terrain/collider assignment={Target}; full three-lane approach grade={grade:F2}deg; backup={backup}");
                report.AppendLine("PASS existing QuestRouteSmokeTest separate-terrain expression, original terrain hashes and serialized candidate preservation. Full quest suite not run here.");
                VerifyControllerLanes(report);
                File.WriteAllText(".code-check/reaver-repair-applied.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(".code-check");
                File.WriteAllText(".code-check/reaver-repair-application-error.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
        }

        static void VerifyControllerLanes(StringBuilder report)
        {
            foreach (bool mounted in new[] { false, true })
                foreach (float offset in new[] { -1.5f, 0f, 1.5f })
                {
                    var from = new Vector3(-285, 0, 50); var end = RiverReaverCamp.Position;
                    var direction = end - from; direction.y = 0; direction.Normalize();
                    var side = Vector3.Cross(Vector3.up, direction) * offset;
                    from += side; end += side;
                    var go = new GameObject("Temporary Reaver controller probe");
                    try
                    {
                        go.transform.position = new Vector3(from.x, WorldRegions.GroundHeight(from) + .15f, from.z);
                        var cc = go.AddComponent<CharacterController>();
                        cc.height = mounted ? 3.6f : 1.9f; cc.radius = mounted ? .62f : .42f;
                        cc.center = Vector3.up * cc.height * .5f; cc.slopeLimit = 45; cc.stepOffset = .3f;
                        float originalDistance = Vector2.Distance(new Vector2(from.x, from.z), new Vector2(end.x, end.z));
                        for (int i = 0; i < 600; i++)
                        {
                            Vector3 delta = end - go.transform.position; delta.y = 0;
                            if (delta.magnitude < .4f) break;
                            cc.Move(delta.normalized * .08f + Vector3.down * .15f);
                        }
                        float remaining = Vector2.Distance(new Vector2(go.transform.position.x, go.transform.position.z), new Vector2(end.x, end.z));
                        report.AppendLine($"CONTROLLER {(mounted ? "mount-sized" : "walk-sized")} lane={offset:F1}: {(remaining < .4f ? "REACHED" : "BLOCKED")} remaining={remaining:F2}m travelled={originalDistance - remaining:F2}m at={go.transform.position}");
                    }
                    finally { UnityEngine.Object.DestroyImmediate(go); }
                }
            report.AppendLine("Controller probes use real scene collision, estimated walk and current mounted dimensions, 45deg slope and .3m step. They do not exercise player input, animations, mount box sweep, camera or network behavior.");
        }

        public static void BuildTestRelease()
        {
            const string path = "Builds/CoopReaverRepair/Riftbound.exe";
            if (File.Exists(path)) throw new InvalidOperationException("Test build already exists; preserve it.");
            EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            Phase1WorldAudit.AuditCurrentScene();
            WorldPresentationSetup.RenderPreview("WorldAudit-Reaver-Repaired", new Vector3(-262, 27, 47), new Vector3(-284, 13, 72), false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { WorldRegions.ScenePath }, locationPathName = path,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            File.WriteAllText(".code-check/reaver-test-build.txt", $"{result.summary.result}; errors={result.summary.totalErrors}; warnings={result.summary.totalWarnings}; path={path}; UTC={DateTime.UtcNow:O}\n");
            EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void InspectSavedCandidate()
        {
            var a = AssetDatabase.LoadAssetAtPath<TerrainData>(Source);
            var b = AssetDatabase.LoadAssetAtPath<TerrainData>(Target);
            var report = new StringBuilder($"Source paint={a.alphamapWidth}x{a.alphamapHeight}x{a.alphamapLayers}; candidate={b.alphamapWidth}x{b.alphamapHeight}x{b.alphamapLayers}\n");
            int changed = 0; float worst = 0;
            for (int z = 0; z < a.alphamapHeight; z += 32)
            {
                int rows = Math.Min(32, a.alphamapHeight - z);
                var x = a.GetAlphamaps(0, z, a.alphamapWidth, rows);
                var y = b.GetAlphamaps(0, z, b.alphamapWidth, rows);
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < a.alphamapWidth; col++)
                        for (int l = 0; l < a.alphamapLayers; l++)
                        {
                            float delta = Mathf.Abs(x[row, col, l] - y[row, col, l]);
                            if (delta == 0) continue;
                            if (changed < 8) report.AppendLine($"DIFF ({col},{row + z},{l}): {x[row, col, l]:R} -> {y[row, col, l]:R}");
                            changed++; worst = Mathf.Max(worst, delta);
                        }
            }
            report.AppendLine($"Changed weights={changed}; maxDelta={worst:R}");
            foreach (var texture in a.alphamapTextures) report.AppendLine($"SOURCE texture path={AssetDatabase.GetAssetPath(texture)} name={texture.name}");
            foreach (var texture in b.alphamapTextures) report.AppendLine($"CANDIDATE texture path={AssetDatabase.GetAssetPath(texture)} name={texture.name}");
            File.WriteAllText(".code-check/reaver-paint-diagnosis.txt", report.ToString());
            EditorApplication.Exit(0);
        }

        public static void ApplySerializedCopy()
        {
            const string safeTarget = "Assets/RPG/Terrain/ReaverCamp/Reaver approach preserved final.asset";
            TerrainData preview = null;
            try
            {
                if (!Application.isBatchMode || !File.ReadAllText(".coordination/scene-status.md").Contains("**SCENE RELEASED**"))
                    throw new InvalidOperationException("Coordinated batch scene release required.");
                if (File.Exists(safeTarget) || File.Exists(safeTarget + ".meta")) throw new InvalidOperationException("Final destination exists.");
                string originalHash = Hash(Source), gradedHash = Hash(Graded);
                if (!File.ReadAllText(".code-check/world-audit-live-before.json").Contains(originalHash)
                    || !File.ReadAllText(".code-check/world-audit-live-before.json").Contains(gradedHash))
                    throw new InvalidOperationException("Original inputs changed.");
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var terrain = WorldRegions.TerrainAt(RiverReaverCamp.Position);
                var collider = terrain ? terrain.GetComponent<TerrainCollider>() : null;
                if (!terrain || !collider || terrain.transform.position != new Vector3(-320, -8, -160)
                    || collider.terrainData != terrain.terrainData || AssetDatabase.GetAssetPath(terrain.terrainData) != Target)
                    throw new InvalidOperationException("Unexpected recovery scene assignment.");
                var source = AssetDatabase.LoadAssetAtPath<TerrainData>(Source);
                var graded = AssetDatabase.LoadAssetAtPath<TerrainData>(Graded);
                if (source.treeInstanceCount != 813 || source.treePrototypes.Length != 15) throw new Exception("Vegetation baseline changed.");
                var report = new StringBuilder($"Serialized-copy Reaver repair UTC={DateTime.UtcNow:O}\n");
                preview = ReaverRepairPreparation.BuildCandidate(source, graded, report);
                // CopyAsset preserves embedded splat textures on disk; CreateAsset(Instantiate(...)) did not.
                if (!AssetDatabase.CopyAsset(Source, safeTarget)) throw new Exception("Serialized terrain copy failed.");
                var saved = AssetDatabase.LoadAssetAtPath<TerrainData>(safeTarget);
                int n = preview.heightmapResolution;
                saved.SetHeights(0, 0, preview.GetHeights(0, 0, n, n));
                EditorUtility.SetDirty(saved); AssetDatabase.SaveAssetIfDirty(saved);
                AssetDatabase.ImportAsset(safeTarget, ImportAssetOptions.ForceSynchronousImport);
                saved = AssetDatabase.LoadAssetAtPath<TerrainData>(safeTarget);
                ReaverRepairPreparation.VerifyPreservedData(source, saved, report);
                terrain.terrainData = saved; collider.terrainData = saved;
                terrain.Flush(); Physics.SyncTransforms();
                float grade = ReaverCampTerrainSetup.CheckApproach();
                if (!AssetDatabase.GetAssetPath(WorldRegions.TerrainAt(RiverReaverCamp.Position).terrainData).Contains("/ReaverCamp/"))
                    throw new Exception("Existing separate-terrain assertion failed.");
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                if (Hash(Source) != originalHash || Hash(Graded) != gradedHash) throw new Exception("Original terrain bytes changed.");
                report.AppendLine($"PASS assigned Terrain + TerrainCollider={safeTarget}; three-lane maximum grade={grade:F2}deg; existing separate-terrain assertion passed; original assets unchanged.");
                VerifyControllerLanes(report);
                report.AppendLine("First unsatisfactory clone retained unassigned for diagnosis; source assets unchanged.");
                File.WriteAllText(".code-check/reaver-repair-applied.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                File.WriteAllText(".code-check/reaver-serialized-copy-error.txt", e.ToString());
                Debug.LogException(e); EditorApplication.Exit(1);
            }
            finally { if (preview) UnityEngine.Object.DestroyImmediate(preview); }
        }
    }
}
