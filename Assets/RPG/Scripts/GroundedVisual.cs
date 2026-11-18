using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Riftbound
{
    [DefaultExecutionOrder(100)]
    public class GroundedVisual : MonoBehaviour
    {
        public Animator animator;
        public float soleHeight = .025f;
        public float walkOffset, runOffset, backWalkOffset, backRunOffset;
        CharacterController motor;
        Combatant stats;
        PlayerController player;
        Animator boundAnimator;
        Transform leftFoot, rightFoot;
        readonly RaycastHit[] supportHits = new RaycastHit[32];
        bool calibrated, calibrationUnavailable, observer, serviceActor;
        Vector3 authoredPosition, soleInParent;
        float lastRootY;
        float currentCorrection, currentGait;
        Func<bool> observerMounted, observerAlive, observerAirborne;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void RegisterSceneBinding()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        static void SceneLoaded(Scene scene, LoadSceneMode mode) => BindSavedActors();

        static void BindSavedActors()
        {
            var runner = BindActors();
            if (runner) runner.StartCoroutine(BindActorsAfterStart());
        }

        static IEnumerator BindActorsAfterStart()
        {
            // Existing Start methods create service/encounter visuals after the scene hook.
            yield return null;
            BindActors();
        }

        static GroundedVisual BindActors()
        {
            var actors = new HashSet<GameObject>();
            foreach (var actor in FindObjectsByType<Combatant>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            foreach (var actor in FindObjectsByType<TownNPC>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            foreach (var actor in FindObjectsByType<Merchant>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            foreach (var actor in FindObjectsByType<QuestGiver>(FindObjectsSortMode.None)) actors.Add(actor.gameObject);
            GroundedVisual runner = null;
            foreach (var actor in actors)
            {
                var bound = EnsureFor(actor, actor.GetComponentInChildren<Animator>());
                if (bound && bound.isActiveAndEnabled) runner = bound;
            }
            return runner;
        }

        public static GroundedVisual EnsureFor(GameObject actor, Animator model)
        {
            if (!actor || !model || model.transform == actor.transform || !model.transform.IsChildOf(actor.transform)) return null;
            var grounding = actor.GetComponent<GroundedVisual>();
            if (!grounding) grounding = actor.AddComponent<GroundedVisual>();
            if (grounding.observer && grounding.animator == model) return grounding;
            grounding.BindAnimator(model);
            return grounding;
        }

        void Start()
        {
            CacheActorContext();
            if (animator && animator != boundAnimator) BindAnimator(animator);
        }

        void CacheActorContext()
        {
            motor = GetComponent<CharacterController>();
            stats = GetComponent<Combatant>();
            player = GetComponent<PlayerController>();
            serviceActor = !player && (GetComponent<TownNPC>() || GetComponent<Merchant>() || GetComponent<QuestGiver>());
        }

        public void BindAnimator(Animator newAnimator)
        {
            observer = false;
            observerMounted = observerAlive = observerAirborne = null;
            Bind(newAnimator);
        }

        public void BindObserver(Animator model, Func<bool> isMounted, Func<bool> isAlive, Func<bool> isAirborne)
        {
            observer = true;
            observerMounted = isMounted;
            observerAlive = isAlive;
            observerAirborne = isAirborne;
            Bind(model);
        }

        void Bind(Animator newAnimator)
        {
            CacheActorContext();
            if (newAnimator == boundAnimator && newAnimator) { animator = newAnimator; return; }
            if (boundAnimator && boundAnimator.transform != transform && boundAnimator.transform.IsChildOf(transform))
                boundAnimator.transform.localPosition = authoredPosition;
            animator = newAnimator;
            boundAnimator = newAnimator;
            leftFoot = null;
            rightFoot = null;
            calibrated = false;
            calibrationUnavailable = false;
            currentCorrection = currentGait = 0;
            lastRootY = transform.position.y;
            if (!animator || animator.transform == transform || !animator.transform.IsChildOf(transform)) return;
            authoredPosition = animator.transform.localPosition;
            if (animator.isHuman)
            {
                leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            }
            else foreach (var bone in animator.GetComponentsInChildren<Transform>())
            {
                if (bone.name.EndsWith(":LeftFoot", StringComparison.Ordinal) || bone.name == "LeftFoot" || bone.name == "foot.l") leftFoot = bone;
                if (bone.name.EndsWith(":RightFoot", StringComparison.Ordinal) || bone.name == "RightFoot" || bone.name == "foot.r") rightFoot = bone;
            }
            calibrationUnavailable = !leftFoot || !rightFoot;
        }

        void LateUpdate()
        {
            if (Time.deltaTime <= 0 || !animator || !animator.enabled || !animator.runtimeAnimatorController) return;
            if (animator != boundAnimator) BindAnimator(animator);
            if (animator.transform == transform || !animator.transform.IsChildOf(transform)) return;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            bool verticalStable = Mathf.Abs(transform.position.y - lastRootY) < .0001f;
            lastRootY = transform.position.y;
            if (observer)
            {
                if (observerMounted == null || observerAlive == null || observerAirborne == null ||
                    observerMounted() || !observerAlive() || observerAirborne()) return;
            }
            else
            {
                if ((player && player.Mounted) || (stats && !stats.Alive)) return;
                // A stationary service/patrol capsule may not call Move while idling.
                if (motor && !motor.isGrounded && !(serviceActor && verticalStable && state.IsName("Idle"))) return;
                // A peer without its explicit observer context must never borrow local state.
                if (GetComponent<Unity.Netcode.NetworkObject>()) return;
            }
            if (state.IsName("Jump") || state.IsName("Ride") || state.IsName("Death")) return;
            if (!TrySupport(out float surface)) return;
            if (!calibrated)
            {
                if (calibrationUnavailable || !state.IsName("Idle") || animator.IsInTransition(0)) return;
                Calibrate();
                if (!calibrated) return;
            }
            // Older serialized clearances described toe joints, not the actual mesh sole.
            float clearance = Mathf.Clamp(soleHeight, 0, .03f);
            float correction = surface + clearance - animator.transform.parent.TransformPoint(soleInParent).y;
            if (!Finite(correction) || Mathf.Abs(correction) > .8f) return;
            float gait = state.IsName("Walk") ? walkOffset : state.IsName("Run") ? runOffset :
                state.IsName("WalkBack") ? backWalkOffset : state.IsName("RunBack") ? backRunOffset : 0;
            currentCorrection = Mathf.Lerp(currentCorrection, correction, 1 - Mathf.Exp(-16 * Time.deltaTime));
            currentGait = Mathf.Lerp(currentGait, gait, 1 - Mathf.Exp(-16 * Time.deltaTime));
            var parent = animator.transform.parent;
            animator.transform.localPosition = authoredPosition + parent.InverseTransformVector(Vector3.up * currentCorrection) + Vector3.up * currentGait;
        }

        void Calibrate()
        {
            float lowest = float.PositiveInfinity;
            float ankle = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
            var mesh = new Mesh();
            try
            {
                foreach (var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!skin.sharedMesh) continue;
                    skin.BakeMesh(mesh);
                    foreach (var vertex in mesh.vertices)
                    {
                        Vector3 world = skin.transform.TransformPoint(vertex);
                        Vector3 l = world - leftFoot.position, r = world - rightFoot.position;
                        bool nearFoot = l.x * l.x + l.z * l.z < .0625f || r.x * r.x + r.z * r.z < .0625f;
                        if (nearFoot && world.y <= ankle + .08f && Finite(world.y)) lowest = Mathf.Min(lowest, world.y);
                    }
                }
            }
            finally { Destroy(mesh); }
            calibrationUnavailable = !Finite(lowest);
            if (calibrationUnavailable) return;
            var point = (leftFoot.position + rightFoot.position) * .5f;
            point.y = lowest;
            soleInParent = animator.transform.parent.InverseTransformPoint(point);
            calibrated = true;
        }

        bool TrySupport(out float y)
        {
            y = 0;
            int count = Physics.RaycastNonAlloc(transform.position + Vector3.up * .4f, Vector3.down, supportHits, 1.6f, ~0, QueryTriggerInteraction.Ignore);
            if (count == supportHits.Length) return false;
            float closest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = supportHits[i];
                var candidate = hit.collider.transform;
                if (candidate.IsChildOf(transform) || hit.normal.y < .45f || hit.point.y > transform.position.y + .35f ||
                    candidate.GetComponentInParent<CharacterController>() || candidate.GetComponentInParent<Combatant>() ||
                    candidate.GetComponentInParent<TownNPC>() || candidate.GetComponentInParent<Merchant>() || candidate.GetComponentInParent<QuestGiver>()) continue;
                if (hit.distance >= closest) continue;
                closest = hit.distance;
                y = hit.point.y;
            }
            return Finite(closest);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
