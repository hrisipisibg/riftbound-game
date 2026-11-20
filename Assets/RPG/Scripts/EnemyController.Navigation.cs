using UnityEngine;

namespace Riftbound
{
    public partial class EnemyController
    {
        const int WorldCollision = ~(1 << 2);
        static readonly float[] DetourAngles = { 0f, 30f, -30f, 58f, -58f, 88f, -88f, 112f, -112f };
        readonly RaycastHit[] sightHits = new RaycastHit[12];
        readonly RaycastHit[] movementHits = new RaycastHit[12];
        Transform committedTarget;
        Vector3 steering, steeringGoal;
        float nextSteer, pursuitUntil, stuckReturnTime;
        int detourSide;

        void ResetNavigation()
        {
            steering = Vector3.zero;
            detourSide = 0;
            nextSteer = pursuitUntil = stuckReturnTime = 0f;
        }

        Vector3 SteerTowards(Vector3 goal, float speed, Transform target, bool returning)
        {
            if (Time.time < nextSteer && (steeringGoal - goal).sqrMagnitude < 1.5f) return steering;
            nextSteer = Time.time + (steering == Vector3.zero ? .1f : .16f);
            steeringGoal = goal;
            var offset = Vector3.ProjectOnPlane(goal - transform.position, Vector3.up);
            float distance = offset.magnitude;
            var direct = offset.normalized;
            float lookAhead = Mathf.Min(distance, Mathf.Clamp(speed * .27f, .75f, 2f));
            bool grounded = motor.isGrounded;
            float radius = motor.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            float step = Mathf.Max(.32f, motor.stepOffset * Mathf.Abs(transform.lossyScale.y));
            Vector3 origin = motor.bounds.center;
            origin.y = transform.position.y + Mathf.Max(step + radius, .7f);

            float best = float.NegativeInfinity;
            steering = Vector3.zero;
            int side = detourSide;
            foreach (float angle in DetourAngles)
            {
                var direction = Quaternion.Euler(0f, angle, 0f) * direct;
                if (!returning && (transform.position + direction * lookAhead - home).sqrMagnitude > 25f * 25f) continue;
                if (!CanStep(origin, direction, lookAhead, radius, step, grounded, target)) continue;
                int turn = angle > 0f ? 1 : angle < 0f ? -1 : 0;
                float score = Vector3.Dot(direction, direct) + (turn != 0 && turn == detourSide ? .21f : 0f);
                if (score <= best) continue;
                best = score;
                steering = direction;
                side = turn;
                if (turn == 0) break;
            }
            detourSide = side;
            return steering;
        }

        bool CanStep(Vector3 origin, Vector3 direction, float distance, float radius, float step, bool grounded, Transform target)
        {
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(.12f, radius * .75f), direction,
                movementHits, distance, WorldCollision, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = movementHits[i];
                if (hit.collider == motor || hit.collider.transform.IsChildOf(transform) ||
                    (target && hit.collider.transform.IsChildOf(target))) continue;
                if (Vector3.Angle(hit.normal, Vector3.up) > motor.slopeLimit) return false;
            }
            if (!grounded) return true;
            Vector3 sample = transform.position + direction * distance + Vector3.up * (step + .8f);
            return Physics.Raycast(sample, Vector3.down, out var floor, step + 2.05f, WorldCollision, QueryTriggerInteraction.Ignore) &&
                Vector3.Angle(floor.normal, Vector3.up) <= motor.slopeLimit &&
                floor.point.y <= transform.position.y + step + distance * .65f;
        }

        bool TryRecoverReturn(Vector3 travelled, Transform target)
        {
            stuckReturnTime = travelled.magnitude < Time.deltaTime * .12f ? stuckReturnTime + Time.deltaTime : 0f;
            if (stuckReturnTime < 5f || (target && (transform.position - target.position).sqrMagnitude < 20f * 20f)) return false;
            foreach (var peer in Network.RiftboundCoopPlayer.AllPlayers)
                if (peer && (peer.transform.position - transform.position).sqrMagnitude < 20f * 20f) return false;
            motor.enabled = false;
            transform.SetPositionAndRotation(home, homeRotation);
            motor.enabled = true;
            stats.Restore();
            evading = false;
            ResetNavigation();
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner)
                Network.RiftboundCoopPlayer.LocalOwner.BroadcastEnemyState(this);
            return true;
        }
    }
}
