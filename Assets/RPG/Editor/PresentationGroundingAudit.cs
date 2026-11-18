using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class PresentationGroundingAudit
    {
        [Serializable] public class Report
        {
            public string evidence = "Saved actors, disposable Idle visual poses and native collider candidates; NOT gameplay/online or final sole-grounding verification";
            public string scene;
            public List<Row> actors = new List<Row>();
            public List<string> errors = new List<string>();
            public string[] limitations = { "Runtime-only actors/companions/peers are absent outside Play Mode.",
                "Foot-region mesh minimum is a geometric estimate, not a verified sole contact marker.",
                "Support candidates exclude actors/triggers and high surfaces; visual or route checks must confirm support semantics." };
        }
        [Serializable] public class Row
        {
            public string actor, kind, animator, support, status;
            public Vector3 root;
            public bool hasFeet, hasSupport, hasMotor, hasFootGeometry;
            public float supportY, capsuleBottom, footBoneY, footRegionMeshY, estimatedSoleGap;
        }

        [MenuItem("Riftbound/Presentation/Read-only saved actor grounding audit")]
        public static void Run()
        {
            var report = new Report { scene = WorldRegions.ScenePath };
            Scene preview = default;
            try
            {
                if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Isolated batch outside Play Mode only; opens the saved scene without saving it.");
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                Physics.SyncTransforms();
                preview = EditorSceneManager.NewPreviewScene();
                var actors = new HashSet<GameObject>();
                foreach (var x in Object.FindObjectsByType<Combatant>(FindObjectsSortMode.None)) actors.Add(x.gameObject);
                foreach (var x in Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None)) actors.Add(x.gameObject);
                foreach (var x in Object.FindObjectsByType<Merchant>(FindObjectsSortMode.None)) actors.Add(x.gameObject);
                foreach (var x in Object.FindObjectsByType<QuestGiver>(FindObjectsSortMode.None)) actors.Add(x.gameObject);
                foreach (var actor in actors.OrderBy(x => Hierarchy(x.transform), StringComparer.Ordinal))
                    Inspect(actor, preview, report);
            }
            catch (Exception e) { report.errors.Add(e.ToString()); }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                Directory.CreateDirectory(".code-check/presentation");
                File.WriteAllText(".code-check/presentation/grounding-audit.json", JsonUtility.ToJson(report, true));
            }
            Debug.Log($"PRESENTATION_GROUNDING_AUDIT actors={report.actors.Count} errors={report.errors.Count}");
            if (Application.isBatchMode) EditorApplication.Exit(report.errors.Count == 0 ? 0 : 1);
        }

        static string Hierarchy(Transform t) => t.parent ? Hierarchy(t.parent) + "/" + t.name : t.name;

        static void Inspect(GameObject actor, Scene preview, Report report)
        {
            GameObject copy = null;
            var row = new Row { actor = Hierarchy(actor.transform), root = actor.transform.position,
                kind = actor.GetComponent<PlayerController>() ? "player" : actor.GetComponent<EnemyController>() ? "enemy" :
                    actor.GetComponent<TownNPC>() ? "town_npc" : actor.GetComponent<Merchant>() ? "merchant" :
                    actor.GetComponent<QuestGiver>() ? "quest_giver" : "other", status = "review" };
            report.actors.Add(row);
            try
            {
                var motor = actor.GetComponent<CharacterController>();
                row.hasMotor = motor;
                if (motor) row.capsuleBottom = actor.transform.TransformPoint(motor.center - Vector3.up * motor.height * .5f).y;
                foreach (var hit in Physics.RaycastAll(row.root + Vector3.up * .4f, Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    var t = hit.collider.transform;
                    if (t.IsChildOf(actor.transform) || t.GetComponentInParent<Combatant>() || t.GetComponentInParent<TownNPC>() ||
                        t.GetComponentInParent<Merchant>() || t.GetComponentInParent<QuestGiver>() || hit.normal.y < .45f || hit.point.y > row.root.y + .35f) continue;
                    row.hasSupport = true; row.supportY = hit.point.y; row.support = Hierarchy(t); break;
                }
                var source = actor.GetComponentInChildren<Animator>();
                if (!source) { row.status = "no_animator"; return; }
                row.animator = Hierarchy(source.transform);
                copy = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(copy, preview);
                copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
                copy.transform.localScale = source.transform.lossyScale;
                var animator = copy.GetComponent<Animator>();
                animator.applyRootMotion = false; animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                if (!animator.HasState(0, Animator.StringToHash("Idle"))) { row.status = "no_idle_pose"; return; }
                animator.Play("Idle", 0, 0); animator.Update(0);
                Transform left = null, right = null;
                if (animator.isHuman)
                {
                    left = animator.GetBoneTransform(HumanBodyBones.LeftFoot); right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                }
                else foreach (var bone in animator.GetComponentsInChildren<Transform>())
                {
                    if (bone.name == "foot.l" || bone.name.EndsWith(":LeftFoot", StringComparison.Ordinal)) left = bone;
                    if (bone.name == "foot.r" || bone.name.EndsWith(":RightFoot", StringComparison.Ordinal)) right = bone;
                }
                row.hasFeet = left && right;
                if (!row.hasFeet) { row.status = "foot_binding_unavailable"; return; }
                row.footBoneY = Mathf.Min(left.position.y, right.position.y);
                row.hasFootGeometry = TryEstimateSole(animator, left, right, out float low);
                if (row.hasFootGeometry) row.footRegionMeshY = low;
                if (row.hasFootGeometry && row.hasSupport)
                {
                    row.estimatedSoleGap = low - row.supportY;
                    row.status = Mathf.Abs(row.estimatedSoleGap) > .06f ? "contact_candidate_review" : "within_6cm_geometric_screen_not_visual_pass";
                }
                else row.status = row.hasSupport ? "foot_geometry_unavailable" : "support_unavailable";
            }
            catch (Exception e) { report.errors.Add(row.actor + ": " + e.Message); }
            finally { if (copy) Object.DestroyImmediate(copy); }
        }

        internal static bool TryEstimateSole(Animator animator, Transform left, Transform right, out float low)
        {
            low = float.PositiveInfinity;
            if (!left || !right) return false;
            float footBoneY = Mathf.Min(left.position.y, right.position.y);
            foreach (var skinned in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var mesh = new Mesh();
                try
                {
                    skinned.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 world = skinned.transform.TransformPoint(vertex);
                        bool nearFoot = HorizontalSquared(world - left.position) < .25f * .25f || HorizontalSquared(world - right.position) < .25f * .25f;
                        if (nearFoot && world.y <= footBoneY + .08f) low = Mathf.Min(low, world.y);
                    }
                }
                finally { Object.DestroyImmediate(mesh); }
            }
            return !float.IsPositiveInfinity(low);
        }

        static float HorizontalSquared(Vector3 delta) => delta.x * delta.x + delta.z * delta.z;
    }
}
