using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor.SceneManagement;
using UnityEngine;
using static Riftbound.Editor.OriginalSiteLayout;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    internal static partial class OriginalWorldRemodel
    {
        public static void VerifySavedWorld()
        {
            scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
            Physics.SyncTransforms();
            var results = new StringBuilder("Original location checks in the saved campaign scene\n");
            foreach (var layout in Sites)
            {
                var root = Find(layout.root);
                int checkCount = 0;
                foreach (var (name, role) in layout.buildings)
                foreach (var original in root.Cast<Transform>().Where(t => t.name == name))
                {
                    if (role == Role.Wall && original.Cast<Transform>().Any(t => t.name.StartsWith("Wall Segment")))
                    {
                        foreach (var segment in original.Cast<Transform>().Where(t => t.name.StartsWith("Wall Segment")))
                            CheckReplacement(segment);
                    }
                    else CheckReplacement(original);
                    if (role == Role.Gate) CheckGate(original);
                    checkCount++;
                }
                results.AppendLine($"PASS {layout.label} | {checkCount} named building and prop anchors use visible kit replacements.");
            }

            var map = Object.FindAnyObjectByType<WorldMapRoutes>();
            if (!map || map.routes == null || !map.routes.Any() || scene.GetRootGameObjects().Any(g => g.name == OldShowcase))
                throw new InvalidOperationException("Campaign route data or original location layout is missing.");
            results.AppendLine($"PASS {map.routes.Length} authored route records are present. The temporary satellite districts are absent.");

            foreach (var crate in Object.FindObjectsByType<SupplyCrate>().Where(c => c.supplyId.StartsWith("camp-supply-")))
            {
                CheckReplacement(crate.transform);
                if (!crate.GetComponent<Collider>() || !crate.GetComponent<Collider>().enabled)
                    throw new InvalidOperationException("An Ashwood quest crate lost its interaction collision.");
            }

            var player = Object.FindAnyObjectByType<PlayerController>();
            var motor = player ? player.GetComponent<CharacterController>() : null;
            VerifyRoute(new WorldTraversalVerification.Route("Emberwatch north gate", new Vector3(0, 0, 13), new Vector3(0, 0, 25)));
            VerifyRoute(new WorldTraversalVerification.Route("Westbank crossing", new Vector3(-278,6,50), new Vector3(-235,6,50)));
            VerifyRoute(new WorldTraversalVerification.Route("Willow crossing", new Vector3(-17,0,360), new Vector3(-17,0,376)), DeckSurface);
            results.AppendLine("PASS Ashwood remains accessible and the original quest supplies have interaction collision.");
            string result = results.ToString();
            File.WriteAllText("../original-world-access-checks.txt", result);
            Debug.Log(result);

            void VerifyRoute(WorldTraversalVerification.Route route, Func<Vector3, Vector3> surface = null)
            {
                foreach (bool mounted in new[] { false, true })
                {
                    var trace = new StringBuilder();
                    WorldTraversalVerification.Traverse(route, mounted, false, motor, trace, surface);
                    string line = trace.ToString();
                    if (!line.StartsWith("REACHED", StringComparison.Ordinal))
                        throw new InvalidOperationException(line.TrimEnd());
                    results.Append(line);
                }
            }
        }

        static void CheckReplacement(Transform original)
        {
            var swap = original.Find(Remodel);
            if (!swap || !OriginalArt(swap).Any())
                throw new InvalidOperationException($"The saved scene has no visible replacement at {original.name}.");
            if (OriginalArt(original).Any(art => !art.transform.IsChildOf(swap)))
                throw new InvalidOperationException($"The old geometry is still visible at {original.name}.");
        }

        static void CheckGate(Transform original)
        {
            var shell = original.Find(Remodel);
            for (int i = -4; i <= 4; i++)
            {
                Vector3 point = original.position + original.forward * i;
                point.y = Mathf.Max(point.y, WorldRegions.GroundHeight(point));
                var overlaps = Physics.OverlapCapsule(point + Vector3.up * .65f, point + Vector3.up * 2.65f, .46f,
                    ~0, QueryTriggerInteraction.Ignore);
                if (overlaps.Any(c => c.transform.IsChildOf(shell)))
                    throw new InvalidOperationException($"{original.name} blocks the original road at {point}.");
            }
        }

        static Vector3 DeckSurface(Vector3 point)
        {
            point.y = WorldRegions.GroundHeight(point);
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * 8, Vector3.down, 9,
                ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider.name == "Willow crossing deck" || hit.collider.name == "Ashwater_Bridge_LOD2")
                    point.y = Mathf.Max(point.y, hit.point.y);
            return point;
        }
    }
}
