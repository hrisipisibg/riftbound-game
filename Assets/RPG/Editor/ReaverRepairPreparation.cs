// Preparation tools do not apply changes to the live scene.
// Compile/run only in the isolated validation project's Editor folder until scene handoff.
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class ReaverRepairPreparation
    {
        const string SnapshotFolder = "Assets/WorldAuditSnapshot/";

        public static void VerifySnapshot()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch verification only.");
            var report = new StringBuilder("REAVER PREPARATION — ISOLATED DATA VERIFICATION ONLY\n");
            TerrainData candidate = null;
            try
            {
                var current = AssetDatabase.LoadAssetAtPath<TerrainData>(SnapshotFolder + "World tile 0-0.asset");
                var graded = AssetDatabase.LoadAssetAtPath<TerrainData>(SnapshotFolder + "Reaver approach.asset");
                if (!current || !graded) throw new InvalidOperationException("Current terrain snapshot inputs are missing.");
                if (current.treeInstanceCount != 813 || current.treePrototypes.Length != 15)
                    throw new InvalidOperationException("Unexpected vegetation baseline; refresh and review source before proceeding.");
                candidate = BuildCandidate(current, graded, report);
                VerifyPreservedData(current, candidate, report);
                ReportAffectedTrees(current, candidate, report);

                // A deliberately damaged copy must fail the same preservation gate.
                var trees = candidate.treeInstances;
                var damaged = (TreeInstance[])trees.Clone();
                damaged[0].position += new Vector3(.01f, 0, 0);
                candidate.SetTreeInstances(damaged, false);
                bool rejected = false;
                try { VerifyTrees(current, candidate); }
                catch (InvalidOperationException) { rejected = true; }
                if (!rejected) throw new InvalidOperationException("Preservation gate missed a moved tree.");
                candidate.SetTreeInstances(trees, false);
                VerifyTrees(current, candidate);
                report.AppendLine("PASS: preservation gate rejects an unintended horizontal tree move.");
                report.AppendLine("PASS: no scene loaded/saved, no terrain asset created/replaced, no actors changed. NOT RUN: scene assignment, collision, navigation, foot/mount gameplay.");
                Directory.CreateDirectory(".code-check");
                File.WriteAllText(".code-check/reaver-preparation-verification.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Directory.CreateDirectory(".code-check");
                File.WriteAllText(".code-check/reaver-preparation-verification.txt", report + "\nFAIL: " + e);
                Debug.LogException(e); EditorApplication.Exit(1);
            }
            finally { if (candidate) UnityEngine.Object.DestroyImmediate(candidate); }
        }

        public static TerrainData BuildCandidate(TerrainData current, TerrainData graded, StringBuilder report)
        {
            if (!current || !graded || current.size != graded.size || current.heightmapResolution != graded.heightmapResolution)
                throw new InvalidOperationException("Terrain grids must be compatible.");
            if (current.size.x != 384 || current.size.z != 384)
                throw new InvalidOperationException("Unexpected tile size; reviewed footprint is not applicable.");
            int n = current.heightmapResolution, edits = 0;
            var values = current.GetHeights(0, 0, n, n);
            var target = graded.GetHeights(0, 0, n, n);
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    if (!InsideFootprint(x, z, n)) continue;
                    if (Mathf.Abs(values[z, x] - target[z, x]) * current.size.y <= .01f) continue;
                    values[z, x] = target[z, x]; edits++;
                }
            if (edits == 0) throw new InvalidOperationException("No reviewed grading changes; do not create an unnecessary replacement.");
            var candidate = UnityEngine.Object.Instantiate(current);
            try { candidate.SetHeights(0, 0, values); }
            catch { UnityEngine.Object.DestroyImmediate(candidate); throw; }
            report.AppendLine($"Prepared in-memory candidate; height samples changed={edits}.");
            return candidate;
        }

        static bool InsideFootprint(int x, int z, int resolution)
        {
            float wx = -320 + x * 384f / (resolution - 1), wz = -160 + z * 384f / (resolution - 1);
            return wx >= -300 && wx <= -266 && wz >= 41 && wz <= 95;
        }

        static void ReportAffectedTrees(TerrainData current, TerrainData candidate, StringBuilder report)
        {
            int affected = 0, nearTrail = 0;
            var from = new Vector2(-285, 50); var to = new Vector2(-283, 78); var line = to - from;
            foreach (var tree in current.treeInstances)
            {
                float oldHeight = current.GetInterpolatedHeight(tree.position.x, tree.position.z);
                float newHeight = candidate.GetInterpolatedHeight(tree.position.x, tree.position.z);
                if (Mathf.Abs(oldHeight - newHeight) <= .1f) continue;
                affected++;
                var point = new Vector2(-320 + tree.position.x * current.size.x, -160 + tree.position.z * current.size.z);
                float t = Mathf.Clamp01(Vector2.Dot(point - from, line) / line.sqrMagnitude);
                float distance = Vector2.Distance(point, from + line * t);
                if (distance < 1.25f) nearTrail++;
                report.AppendLine(FormattableString.Invariant($"TREE REVIEW x={point.x:F2} z={point.y:F2} prototype={tree.prototypeIndex} terrainDelta={newHeight - oldHeight:F3}m trailCentreDistance={distance:F2}m widthScale={tree.widthScale:F2}"));
            }
            report.AppendLine($"Interpolated tree-grounding review: affected={affected}, centres within 1.25m of trail={nearTrail}. Not a collider/radius/gameplay verdict; prototype meshes require live-project review.");
        }

        public static void VerifyPreservedData(TerrainData current, TerrainData candidate, StringBuilder report, Rect? authorizedFootprint = null)
        {
            VerifyTrees(current, candidate);
            int n = current.heightmapResolution;
            var a = current.GetHeights(0, 0, n, n); var b = candidate.GetHeights(0, 0, n, n);
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    float wx = -320 + x * current.size.x / (n - 1), wz = -160 + z * current.size.z / (n - 1);
                    bool inside = authorizedFootprint.HasValue
                        ? wx >= authorizedFootprint.Value.xMin && wx <= authorizedFootprint.Value.xMax
                            && wz >= authorizedFootprint.Value.yMin && wz <= authorizedFootprint.Value.yMax
                        : InsideFootprint(x, z, n);
                    if (!inside && a[z, x] != b[z, x]) throw new InvalidOperationException("Height changed outside approved footprint.");
                }
            var layers = current.terrainLayers; var copies = candidate.terrainLayers;
            if (layers.Length != copies.Length) throw new InvalidOperationException("Terrain layers lost.");
            for (int i = 0; i < layers.Length; i++)
                if (layers[i] != copies[i]) throw new InvalidOperationException("Terrain layer reference changed.");
            if (current.alphamapWidth != candidate.alphamapWidth || current.alphamapHeight != candidate.alphamapHeight
                || current.alphamapLayers != candidate.alphamapLayers) throw new InvalidOperationException("Paint dimensions changed.");
            for (int z = 0; z < current.alphamapHeight; z += 32)
            {
                int rows = Math.Min(32, current.alphamapHeight - z);
                var paintA = current.GetAlphamaps(0, z, current.alphamapWidth, rows);
                var paintB = candidate.GetAlphamaps(0, z, current.alphamapWidth, rows);
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < current.alphamapWidth; x++)
                        for (int l = 0; l < current.alphamapLayers; l++)
                            if (paintA[y, x, l] != paintB[y, x, l]) throw new InvalidOperationException("Terrain paint changed.");
            }
            if (current.detailResolution != candidate.detailResolution || current.detailPrototypes.Length != candidate.detailPrototypes.Length)
                throw new InvalidOperationException("Detail layout changed.");
            for (int l = 0; l < current.detailPrototypes.Length; l++)
            {
                if (JsonUtility.ToJson(current.detailPrototypes[l]) != JsonUtility.ToJson(candidate.detailPrototypes[l]))
                    throw new InvalidOperationException("Detail prototype changed.");
                var detailsA = current.GetDetailLayer(0, 0, current.detailResolution, current.detailResolution, l);
                var detailsB = candidate.GetDetailLayer(0, 0, current.detailResolution, current.detailResolution, l);
                for (int z = 0; z < current.detailResolution; z++)
                    for (int x = 0; x < current.detailResolution; x++)
                        if (detailsA[z, x] != detailsB[z, x]) throw new InvalidOperationException("Detail density changed.");
            }
            if (current.holesResolution != candidate.holesResolution) throw new InvalidOperationException("Terrain holes grid changed.");
            var holesA = current.GetHoles(0, 0, current.holesResolution, current.holesResolution);
            var holesB = candidate.GetHoles(0, 0, candidate.holesResolution, candidate.holesResolution);
            for (int z = 0; z < current.holesResolution; z++)
                for (int x = 0; x < current.holesResolution; x++)
                    if (holesA[z, x] != holesB[z, x]) throw new InvalidOperationException("Terrain holes changed.");
            report.AppendLine($"PASS: {current.treeInstanceCount} trees, {layers.Length} layer references, every paint texel, detail prototype/density, terrain hole and outside-footprint height preserved.");
        }

        static void VerifyTrees(TerrainData a, TerrainData b)
        {
            if (a.treePrototypes.Length != b.treePrototypes.Length || a.treeInstanceCount != b.treeInstanceCount)
                throw new InvalidOperationException("Vegetation count changed.");
            var prototypesA = a.treePrototypes; var prototypesB = b.treePrototypes;
            for (int i = 0; i < prototypesA.Length; i++)
                if (prototypesA[i].prefab != prototypesB[i].prefab || prototypesA[i].bendFactor != prototypesB[i].bendFactor)
                    throw new InvalidOperationException("Tree prototype reference/settings changed.");
            var treesA = a.treeInstances; var treesB = b.treeInstances;
            for (int i = 0; i < treesA.Length; i++)
            {
                var x = treesA[i]; var y = treesB[i];
                if (x.position.x != y.position.x || x.position.z != y.position.z || x.prototypeIndex != y.prototypeIndex
                    || x.rotation != y.rotation || x.widthScale != y.widthScale || x.heightScale != y.heightScale
                    || !x.color.Equals(y.color) || !x.lightmapColor.Equals(y.lightmapColor))
                    throw new InvalidOperationException("Authored tree fields changed; only vertical re-grounding is allowed.");
            }
        }
    }
}
