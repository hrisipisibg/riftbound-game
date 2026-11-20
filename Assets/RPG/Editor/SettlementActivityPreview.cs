using System;
using System.Linq;
using System.Text;
using Riftbound.WorldAmbient;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    internal static class SettlementActivityPreview
    {
        internal static CivilianRouteWalker Create(StringBuilder report, bool threeChimes = false)
        {
            var group = new GameObject("Settlement waystops");
            Waystop(group.transform, "Emberwatch Service Court", new Vector3(32.5f, 0, -22),
                "The north-road wagons load here at dawn. Leave the stone ramp clear for the teamsters.");
            Waystop(group.transform, threeChimes ? "Three-Chimes Relay" : "Lantern Rest", new Vector3(-.8f, 0, 300),
                threeChimes ? "One chime calls the loaders. Two warn of a late cart. Three tell the north-road riders to shelter here."
                    : "If the lantern goes out, wait under the canvas. Greenhollow's riders will relight it at dusk.");
            Waystop(group.transform, "Coilbreak Yard", new Vector3(207, 0, 187),
                "Keep the recovered copper dry. Report cracked valves to the tinker in Rustwater before taking them underground.");
            Waystop(group.transform, "Gor'Kresh Trailhead Shelter", new Vector3(367.5f, 0, 314),
                "The red pennant marks the last shelter inside the wall. Recheck your tack before climbing toward Wyrmcrest.");

            var porter = new GameObject("Emberwatch court porter");
            porter.transform.SetParent(group.transform);
            Vector3[] points =
            {
                new Vector3(34.6f, 0, -23.4f), new Vector3(37.3f, 0, -23.4f),
                new Vector3(37.3f, 0, -20), new Vector3(34.6f, 0, -20)
            };
            var markers = new Transform[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                var marker = new GameObject("Porter route stop " + (i + 1)).transform;
                marker.SetParent(group.transform);
                marker.position = Surface(points[i]);
                markers[i] = marker;
            }
            porter.transform.position = markers[0].position;
            var visual = CharacterVisualFactory.CreateVisualInstance("Human", "Rogue", porter.transform);
            var animator = visual ? visual.GetComponentInChildren<Animator>() : null;
            if (!animator || !animator.HasState(0, Animator.StringToHash("Walk")) ||
                !animator.HasState(0, Animator.StringToHash("Idle")))
                throw new InvalidOperationException("The court porter requires the existing Walk and Idle animation states.");
            animator.applyRootMotion = false;
            var walker = porter.AddComponent<CivilianRouteWalker>();
            var settings = new SerializedObject(walker);
            var waypoints = settings.FindProperty("waypoints");
            waypoints.arraySize = markers.Length;
            for (int i = 0; i < markers.Length; i++)
                waypoints.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
            settings.FindProperty("animator").objectReferenceValue = animator;
            settings.FindProperty("walkState").stringValue = "Walk";
            settings.FindProperty("idleState").stringValue = "Idle";
            settings.FindProperty("stopSeconds").floatValue = 2.2f;
            settings.ApplyModifiedPropertiesWithoutUndo();

            Physics.SyncTransforms();
            for (int i = 0; i < markers.Length; i++)
            {
                var from = markers[i].position;
                var to = markers[(i + 1) % markers.Length].position;
                for (int j = 0; j <= 8; j++)
                {
                    var feet = Vector3.Lerp(from, to, j / 8f);
                    if (Mathf.Abs(feet.y - WorldRegions.GroundHeight(feet)) > .16f ||
                        Physics.OverlapCapsule(feet + Vector3.up * .32f, feet + Vector3.up * 1.75f, .28f,
                            ~(1 << 2), QueryTriggerInteraction.Ignore).Any(c => !(c is TerrainCollider)))
                        throw new InvalidOperationException($"Porter route lacks supported clearance near {feet:F2}.");
                }
            }
            walker.RebuildRoute();
            if (!walker.HasValidRoute) throw new InvalidOperationException("Court porter route did not bind.");
            report.AppendLine("Four settlement notices and one decorative porter staged. Tested 36 porter clearance samples.");
            return walker;
        }

        static void Waystop(Transform root, string label, Vector3 point, string note)
        {
            var stop = new GameObject(label + " notice").AddComponent<SettlementWaystop>();
            stop.transform.SetParent(root);
            stop.transform.position = Surface(point);
            stop.displayName = label;
            stop.notice = note;
        }

        static Vector3 Surface(Vector3 point)
        {
            point.y = WorldRegions.GroundHeight(point);
            return point;
        }
    }
}
