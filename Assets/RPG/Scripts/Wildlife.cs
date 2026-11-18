using UnityEngine;

namespace Riftbound
{
    public class Wildlife : MonoBehaviour
    {
        public Animator animator;
        public bool wander;
        Vector3 home, destination;
        float nextDestination;
        string state;
        void Start() { home = transform.position; destination = home; }
        void Update()
        {
            if (!animator || FrontendMenu.IsOpen || Time.deltaTime <= 0) return;
            if (wander && Time.time >= nextDestination)
            {
                nextDestination = Time.time + Random.Range(7, 12);
                Vector2 offset = Random.insideUnitCircle * 3;
                destination = home + new Vector3(offset.x, 0, offset.y);
            }
            Vector3 direction = destination - transform.position; direction.y = 0;
            bool moving = wander && direction.magnitude > .2f && !Physics.SphereCast(transform.position + Vector3.up * .5f, .25f, direction.normalized, out _, .7f, ~(1 << 2), QueryTriggerInteraction.Ignore);
            if (moving)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 3);
                transform.position += direction.normalized * (.55f * Time.deltaTime);
                if (Physics.Raycast(transform.position + Vector3.up * 2, Vector3.down, out var ground, 5, ~(1 << 2), QueryTriggerInteraction.Ignore))
                    transform.position = new Vector3(transform.position.x, ground.point.y, transform.position.z);
            }
            string next = moving ? "Walk" : "Idle";
            if (next != state) { animator.CrossFadeInFixedTime(next, .2f, 0, 0); state = next; }
        }
    }
}
