using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class SettlementSiteSurvey
    {
        public static void Run()
        {
            WorldProductionAudit.Open();
            var report = new StringBuilder("Draft siting survey. Samples at 1 m; no scene or terrain writes.\n");
            var terrains = Terrain.activeTerrains;
            Survey(report, terrains, "Emberwatch east court", new Vector2(25, -3), 8, 6);
            Survey(report, terrains, "Lantern Rest", new Vector2(2, 300), 7, 5);
            Survey(report, terrains, "Coilbreak Yard", new Vector2(211, 188), 8, 6);
            Survey(report, terrains, "Wyrmcrest switchback edge", new Vector2(378, 373), 6, 4);
            Survey(report, terrains, "Wyrmcrest lower bench", new Vector2(345, 350), 6, 4);
            Survey(report, terrains, "Wyrmcrest approach bench", new Vector2(337, 323), 6, 4);
            foreach (int z in new[] { 314, 322, 330, 338 })
            foreach (int x in new[] { 330, 336, 342, 365, 371 })
                Survey(report, terrains, "Wyrmcrest refuge grid", new Vector2(x, z), 3, 3);
            File.WriteAllText(WorldProductionAudit.Evidence + "settlement-sites.txt", report.ToString());
            EditorApplication.Exit(0);
        }

        static void Survey(StringBuilder report, Terrain[] terrains, string name, Vector2 centre, int halfWidth, int halfDepth)
        {
            float low = float.PositiveInfinity;
            float high = float.NegativeInfinity;
            float steepest = 0f;
            int samples = 0;
            int missing = 0;
            for (int z = -halfDepth; z <= halfDepth; z++)
            for (int x = -halfWidth; x <= halfWidth; x++)
            {
                var point = new Vector3(centre.x + x, 0f, centre.y + z);
                var terrain = WorldRegions.TerrainAt(point, terrains);
                if (!terrain)
                {
                    missing++;
                    continue;
                }

                samples++;
                float height = WorldRegions.GroundHeight(point, terrains);
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
                var local = point - terrain.transform.position;
                var data = terrain.terrainData;
                steepest = Mathf.Max(steepest, data.GetSteepness(local.x / data.size.x, local.z / data.size.z));
            }

            int trees = 0;
            foreach (var terrain in terrains)
            {
                var data = terrain.terrainData;
                foreach (var tree in data.treeInstances)
                {
                    var point = terrain.transform.position + Vector3.Scale(tree.position, data.size);
                    if (Mathf.Abs(point.x - centre.x) <= halfWidth && Mathf.Abs(point.z - centre.y) <= halfDepth)
                        trees++;
                }
            }

            report.AppendLine(FormattableString.Invariant(
                $"{name} at {centre}: footprint {halfWidth * 2}m x {halfDepth * 2}m, samples={samples}, missing={missing}, ground={low:F2}..{high:F2}m, maxSlope={steepest:F1}°, terrainTreeRoots={trees}"));
        }
    }
}
