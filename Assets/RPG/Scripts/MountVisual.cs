using UnityEngine;

namespace Riftbound
{
    public class MountVisual : MonoBehaviour
    {
        public Animator animator;
        public Transform saddle;
        public float walkStride = 1.5f, runStride = 4;
        [HideInInspector] public int artVersion;
        string state;
        Animator boundAnimator;
        RuntimeAnimatorController boundController;
        public void Animate(float speed, bool airborne, bool walking = false)
        {
            if (!animator || !animator.runtimeAnimatorController)
            {
                state = null;
                boundAnimator = null;
                boundController = null;
                return;
            }
            if (animator != boundAnimator || animator.runtimeAnimatorController != boundController)
            {
                state = null;
                boundAnimator = animator;
                boundController = animator.runtimeAnimatorController;
            }
            if (float.IsNaN(speed) || float.IsInfinity(speed)) speed = 0;
            speed = Mathf.Max(0, speed);
            string next = airborne ? "Jump" : speed < .15f ? "Idle" : walking ? "Walk" : "Run";
            if (!animator.HasState(0, Animator.StringToHash(next))) next = "Idle";
            if (!animator.HasState(0, Animator.StringToHash(next))) { state = null; animator.speed = 1; return; }
            if (state != next) { animator.CrossFadeInFixedTime(next, .15f, 0, 0); state = next; }
            float stride = next == "Run" ? runStride : walkStride;
            float cadence = stride > .1f && !float.IsInfinity(stride) ? speed / stride : 1;
            animator.speed = next == "Idle" || next == "Jump" ? 1 : Mathf.Clamp(cadence, .65f, 2.25f);
        }
    }
}
