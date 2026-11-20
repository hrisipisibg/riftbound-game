using System.Collections.Generic;
using UnityEngine;

namespace Riftbound.WorldAmbient
{
    /// <summary>
    /// Cosmetic, scene-authored waypoint walker for decorative civilian NPCs.
    /// Carries no quest, reward, save, or network authority behavior.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CivilianRouteWalker : MonoBehaviour
    {
        const float MinWaypointSeparationSqr = 0.0004f; // 2 cm minimum segment

        [Header("Scene-Authored Route")]
        [Tooltip("Ordered scene Transform waypoints (clear of bridges and player lanes). Uses each waypoint's explicit world position and height.")]
        [SerializeField] Transform[] waypoints = System.Array.Empty<Transform>();

        [Tooltip("When enabled, wraps from the final waypoint back to the first in a closed loop.")]
        [SerializeField] bool closedLoop = true;

        [Header("Motion & Facing")]
        [Tooltip("Walk speed in meters per second.")]
        [SerializeField] float walkSpeed = 1.6f;

        [Tooltip("Yaw turn speed in degrees per second while moving.")]
        [SerializeField] float turnSpeed = 360f;

        [Tooltip("Seconds to pause at each reached waypoint before advancing.")]
        [SerializeField] float stopSeconds = 1.5f;

        [Tooltip("Distance in meters within which a waypoint is considered reached.")]
        [SerializeField] float arrivalThreshold = 0.08f;

        [Tooltip("If enabled, smoothly rotates toward the horizontal travel direction while walking.")]
        [SerializeField] bool faceWaypointDirection = true;

        [Header("Optional Animator")]
        [Tooltip("Optional existing Animator on this decorative actor.")]
        [SerializeField] Animator animator;

        [Tooltip("Optional existing Animator Bool, Float, or Int locomotion parameter name. Left empty to leave Animator untouched.")]
        [SerializeField] string locomotionParameter = "";

        [Tooltip("Value written when locomotionParameter is a Float parameter and the actor is walking.")]
        [SerializeField] float locomotionFloatValue = 1f;

        [SerializeField] string walkState = "";
        [SerializeField] string idleState = "";
        int activeState;

        readonly List<Transform> validWaypoints = new List<Transform>();
        int targetIndex;
        float pauseRemaining;
        bool routeCompleted;
        bool hasStartedLoop;
        bool hasCachedParam;
        AnimatorControllerParameterType cachedParamType;
        int cachedParamHash;
        RuntimeAnimatorController cachedController;

        public bool HasValidRoute => validWaypoints.Count >= 2;
        public int ValidWaypointCount => validWaypoints.Count;
        public int CurrentWaypointIndex => targetIndex;
        public bool IsWalking { get; private set; }
        public bool IsPaused { get; private set; }
        public int CompletedLoops { get; private set; }

        void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            RebuildRoute();
        }

        void OnEnable()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            RebuildRoute();
        }

        void OnDisable()
        {
            IsWalking = false;
            IsPaused = false;
            ApplyAnimatorLocomotion(false);
        }

        void OnValidate()
        {
            walkSpeed = Mathf.Max(0f, walkSpeed);
            turnSpeed = Mathf.Max(0f, turnSpeed);
            stopSeconds = Mathf.Max(0f, stopSeconds);
            arrivalThreshold = Mathf.Max(0.01f, arrivalThreshold);
            hasCachedParam = false;
        }

        /// <summary>
        /// Re-validates the authored transform list and resets traversal state.
        /// Guards null, empty, non-scene, and consecutive/closed-loop duplicate waypoints.
        /// </summary>
        public void RebuildRoute()
        {
            validWaypoints.Clear();
            IsWalking = false;
            IsPaused = false;
            pauseRemaining = 0f;
            routeCompleted = false;
            CompletedLoops = 0;
            targetIndex = 0;

            if (waypoints != null)
            {
                for (int i = 0; i < waypoints.Length; i++)
                {
                    Transform wp = waypoints[i];
                    if (!wp || !wp.gameObject.scene.IsValid()) continue;
                    if (validWaypoints.Count > 0 &&
                        (wp.position - validWaypoints[validWaypoints.Count - 1].position).sqrMagnitude <= MinWaypointSeparationSqr)
                    {
                        continue;
                    }
                    validWaypoints.Add(wp);
                }

                // Avoid a redundant zero-length closing hop if the author repeated the first waypoint at the end.
                if (closedLoop && validWaypoints.Count >= 2 &&
                    (validWaypoints[validWaypoints.Count - 1].position - validWaypoints[0].position).sqrMagnitude <= MinWaypointSeparationSqr)
                {
                    validWaypoints.RemoveAt(validWaypoints.Count - 1);
                }
            }

            if (validWaypoints.Count < 2)
            {
                ApplyAnimatorLocomotion(false);
                return;
            }

            // If placed right on waypoint 0 at startup, begin walking toward waypoint 1.
            float threshSq = arrivalThreshold * arrivalThreshold;
            hasStartedLoop = (transform.position - validWaypoints[0].position).sqrMagnitude <= threshSq;
            if (hasStartedLoop)
                targetIndex = 1;

            ApplyAnimatorLocomotion(false);
        }

        void Update()
        {
            if (!HasValidRoute || routeCompleted)
            {
                IsWalking = false;
                IsPaused = false;
                ApplyAnimatorLocomotion(false);
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (IsPaused)
            {
                pauseRemaining -= dt;
                if (pauseRemaining > 0f)
                {
                    IsWalking = false;
                    ApplyAnimatorLocomotion(false);
                    return;
                }
                IsPaused = false;
                AdvanceWaypoint();
                if (routeCompleted)
                {
                    IsWalking = false;
                    ApplyAnimatorLocomotion(false);
                    return;
                }
            }

            Transform target = validWaypoints[targetIndex];
            if (!target)
            {
                RebuildRoute();
                return;
            }

            Vector3 destination = target.position;
            Vector3 toTarget = destination - transform.position;
            float threshold = Mathf.Max(0.01f, arrivalThreshold);

            if (toTarget.magnitude <= threshold)
            {
                transform.position = destination;
                IsWalking = false;
                if (closedLoop && targetIndex == 0)
                {
                    if (hasStartedLoop) CompletedLoops++;
                    hasStartedLoop = true;
                }
                if (stopSeconds > 0f)
                {
                    IsPaused = true;
                    pauseRemaining = stopSeconds;
                }
                else
                {
                    AdvanceWaypoint();
                }
                ApplyAnimatorLocomotion(false);
                return;
            }

            if (faceWaypointDirection && turnSpeed > 0f)
            {
                Vector3 planar = new Vector3(toTarget.x, 0f, toTarget.z);
                if (planar.sqrMagnitude > 1e-6f)
                {
                    Quaternion desired = Quaternion.LookRotation(planar.normalized, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desired, turnSpeed * dt);
                }
            }

            float speed = Mathf.Max(0f, walkSpeed);
            if (speed <= 0f)
            {
                IsWalking = false;
                ApplyAnimatorLocomotion(false);
                return;
            }

            transform.position = Vector3.MoveTowards(transform.position, destination, speed * dt);
            IsWalking = true;
            ApplyAnimatorLocomotion(true);
        }

        void AdvanceWaypoint()
        {
            int next = targetIndex + 1;
            if (next >= validWaypoints.Count)
            {
                if (closedLoop)
                {
                    targetIndex = 0;
                    CompletedLoops++;
                }
                else
                {
                    routeCompleted = true;
                }
            }
            else
            {
                targetIndex = next;
            }
        }

        void ApplyAnimatorLocomotion(bool walking)
        {
            if (!animator || !animator.isActiveAndEnabled)
                return;
            var ctrl = animator.runtimeAnimatorController;
            if (!ctrl) return;
            if (string.IsNullOrWhiteSpace(locomotionParameter))
            {
                string state = walking ? walkState : idleState;
                if (string.IsNullOrWhiteSpace(state)) return;
                int hash = Animator.StringToHash(state);
                if (hash != activeState && animator.HasState(0, hash))
                {
                    animator.CrossFadeInFixedTime(hash, .14f, 0);
                    activeState = hash;
                }
                return;
            }

            if (!hasCachedParam || cachedController != ctrl)
            {
                cachedController = ctrl;
                hasCachedParam = false;
                foreach (var p in animator.parameters)
                {
                    if (p.name == locomotionParameter)
                    {
                        cachedParamType = p.type;
                        cachedParamHash = p.nameHash;
                        hasCachedParam = true;
                        break;
                    }
                }
            }

            if (!hasCachedParam) return;

            switch (cachedParamType)
            {
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(cachedParamHash, walking);
                    break;
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(cachedParamHash, walking ? locomotionFloatValue : 0f);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(cachedParamHash, walking ? 1 : 0);
                    break;
            }
        }
    }
}
