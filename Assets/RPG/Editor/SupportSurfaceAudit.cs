using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class SupportSurfaceAudit
    {
        const string Folder = ".coordination/support-surfaces/";
        [Serializable] class Report
        {
            public string evidence = "Native saved-scene physics inspection; NOT running gameplay or traversal acceptance";
            public string sceneHashBefore, sceneHashAfter;
            public string reaverTerrainPath;
            public int reaverTrees, reaverPrototypes;
            public bool reaverColliderMatches;
            public bool unsavedCandidatePreview;
            public List<ActorRow> actors = new List<ActorRow>();
            public List<string> errors = new List<string>();
        }
        [Serializable] class ActorRow
        {
            public string actor, objectId;
            public Vector3 root;
            public float terrainY;
            public bool hasMotor, motorGrounded;
            public Vector3 proposedRoot, afterMotorRoot;
            public List<HitRow> shortRay = new List<HitRow>(), aboveRay = new List<HitRow>();
            public List<SurfaceRow> surfaces = new List<SurfaceRow>();
        }
        [Serializable] class HitRow
        {
            public string surface, rejection;
            public float y, normalY;
        }
        [Serializable] class SurfaceRow
        {
            public string surface, objectId, collider;
            public bool closestPointSupported, shortRayStartsInside, hasRenderer;
            public Vector3 scale, colliderMin, colliderMax, rendererMin, rendererMax;
        }

        public static void Run() => Run(false, true);

        public static void RunCandidate() => Run(true, true);

        public static void RunBoth()
        {
            bool baseline = Run(false, false);
            bool candidate = Run(true, false);
            if (Application.isBatchMode) EditorApplication.Exit(baseline && candidate ? 0 : 1);
        }

        static bool Run(bool candidate, bool exit)
        {
            var report = new Report { unsavedCandidatePreview = candidate };
            try
            {
                if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Use an isolated batch outside Play Mode.");
                report.sceneHashBefore = Hash(WorldRegions.ScenePath);
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                Physics.SyncTransforms();
                var reaver = WorldRegions.TerrainAt(RiverReaverCamp.Position);
                if (!reaver || !reaver.terrainData) throw new InvalidOperationException("Reaver terrain reference is missing.");
                report.reaverTerrainPath = AssetDatabase.GetAssetPath(reaver.terrainData);
                report.reaverTrees = reaver.terrainData.treeInstanceCount;
                report.reaverPrototypes = reaver.terrainData.treePrototypes.Length;
                var terrainCollider = reaver.GetComponent<TerrainCollider>();
                report.reaverColliderMatches = terrainCollider && terrainCollider.terrainData == reaver.terrainData;
                var actors = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.name == "Ember Hatchling" || t.name == "Outrider Lyra" || t.name == "Vermithrax the Ember Sovereign")
                    .OrderBy(t => t.name).ThenBy(t => t.position.x).ToArray();
                if (candidate) PreviewSurfaces();
                foreach (var actor in actors)
                {
                    Vector3 proposed = actor.position;
                    var motor = actor.GetComponent<CharacterController>();
                    if (candidate)
                    {
                        var hits = Physics.RaycastAll(actor.position + Vector3.up * 100, Vector3.down, 300, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => !IsActorCollider(h.collider, actor) && h.normal.y >= .45f).OrderBy(h => h.distance).ToArray();
                        if (hits.Length == 0) throw new InvalidOperationException("Candidate has no surface: " + actor.name);
                        proposed.y = hits[0].point.y + .05f;
                        if (motor) motor.enabled = false;
                        actor.position = proposed;
                        if (motor) motor.enabled = true;
                        Physics.SyncTransforms();
                        if (motor) for (int i = 0; i < 30; i++) motor.Move(Vector3.down * .02f);
                    }
                    var row = Inspect(actor);
                    row.proposedRoot = proposed; row.afterMotorRoot = actor.position;
                    row.hasMotor = motor; row.motorGrounded = motor && motor.isGrounded;
                    report.actors.Add(row);
                    if (candidate && !row.shortRay.Any(h => h.rejection == "eligible")) report.errors.Add("Candidate still lacks support: " + actor.name);
                    if (candidate && motor && (!motor.isGrounded || Vector2.Distance(new Vector2(proposed.x, proposed.z), new Vector2(actor.position.x, actor.position.z)) > .1f))
                        report.errors.Add("Candidate motor is unsupported or displaced horizontally: " + actor.name);
                }
                if (report.actors.Count != 4) report.errors.Add("Expected exactly four scoped actors; found " + report.actors.Count);
                report.sceneHashAfter = Hash(WorldRegions.ScenePath);
                if (report.sceneHashBefore != report.sceneHashAfter) report.errors.Add("Saved scene changed during read-only audit.");
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Folder + (candidate ? "candidate.json" : "receipt.json"), JsonUtility.ToJson(report, true));
            Debug.Log($"SUPPORT_SURFACE_AUDIT candidate={candidate} actors={report.actors.Count} errors={report.errors.Count}");
            if (exit && Application.isBatchMode) EditorApplication.Exit(report.errors.Count == 0 ? 0 : 1);
            return report.errors.Count == 0;
        }

        static void PreviewSurfaces()
        {
            // Only the disposable in-memory scene changes; never save or edit the source asset.
            foreach (string name in new[] { "Summit Dais Platform", "Molten Roost Pit" })
            {
                var matches = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == name).ToArray();
                if (matches.Length != 1) throw new InvalidOperationException("Expected one surface: " + name);
                var source = matches[0].GetComponent<CapsuleCollider>();
                var mesh = matches[0].GetComponent<MeshFilter>();
                if (!source || !source.enabled || !mesh || !mesh.sharedMesh || matches[0].GetComponent<MeshCollider>())
                    throw new InvalidOperationException("Unexpected candidate source components: " + name);
                source.enabled = false;
                var replacement = matches[0].gameObject.AddComponent<MeshCollider>();
                replacement.sharedMesh = mesh.sharedMesh;
            }
            Physics.SyncTransforms();
        }

        static ActorRow Inspect(Transform actor)
        {
            var row = new ActorRow { actor = actor.name, objectId = GlobalObjectId.GetGlobalObjectIdSlow(actor.gameObject).ToString(),
                root = actor.position, terrainY = WorldRegions.GroundHeight(actor.position) };
            Vector3 origin = actor.position + Vector3.up * .4f;
            SampleRay(actor, origin, 1.6f, row.shortRay);
            SampleRay(actor, actor.position + Vector3.up * 100, 300, row.aboveRay);
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (!collider.enabled || collider.isTrigger || IsActorCollider(collider, actor)) continue;
                var bounds = collider.bounds;
                if (actor.position.x < bounds.min.x || actor.position.x > bounds.max.x ||
                    actor.position.z < bounds.min.z || actor.position.z > bounds.max.z ||
                    bounds.max.y < actor.position.y - 3 || bounds.min.y > actor.position.y + 50) continue;
                var renderer = collider.GetComponent<Renderer>();
                bool closestSupported = collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider ||
                    (collider is MeshCollider meshCollider && meshCollider.convex);
                row.surfaces.Add(new SurfaceRow { surface = collider.name, collider = collider.GetType().Name,
                    objectId = GlobalObjectId.GetGlobalObjectIdSlow(collider).ToString(), scale = collider.transform.lossyScale,
                    closestPointSupported = closestSupported,
                    shortRayStartsInside = closestSupported && (collider.ClosestPoint(origin) - origin).sqrMagnitude < .00000001f,
                    colliderMin = bounds.min, colliderMax = bounds.max, hasRenderer = renderer,
                    rendererMin = renderer ? renderer.bounds.min : Vector3.zero, rendererMax = renderer ? renderer.bounds.max : Vector3.zero });
            }
            return row;
        }

        static void SampleRay(Transform actor, Vector3 origin, float distance, List<HitRow> rows)
        {
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (IsActorCollider(hit.collider, actor)) continue;
                rows.Add(new HitRow { surface = hit.collider.name, y = hit.point.y, normalY = hit.normal.y,
                    rejection = hit.normal.y < .45f ? "steep" : hit.point.y > actor.position.y + .35f ? "above support limit" : "eligible" });
            }
        }

        static bool IsActorCollider(Collider collider, Transform actor) => collider.transform.IsChildOf(actor) ||
            collider.GetComponentInParent<CharacterController>() || collider.GetComponentInParent<Combatant>() ||
            collider.GetComponentInParent<TownNPC>() || collider.GetComponentInParent<Merchant>() || collider.GetComponentInParent<QuestGiver>();

        static string Hash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
