using UnityEngine;

namespace Riftbound
{
    public partial class PlayerController
    {
        internal bool CanUseVillageTour =>
            Application.productName == VillageTourMenu.Product && VillageTourMenu.IsOffline &&
            WorldRegions.Expanded && !FrontendMenu.IsOpen && Stats && Stats.Alive &&
            motor && motor.enabled && motor.isGrounded && !Mounted && !Mounting &&
            !Blocking && !ReturningHome && !FlightMaster.IsAnyTaxiInFlight &&
            !PrototypeHUD.MenuOpen && !(PartySystem.Instance && PartySystem.Instance.IsCompanionMenuOpen) &&
            !AnyBlockingWindowOpen() && Time.time >= busyUntil && Time.time >= dodgeEnds && !InCombat();

        internal static bool TryVillageTourLanding(CharacterController controller, Vector3 point, float heading,
            bool snapToGround, out Vector3 landing)
        {
            landing = point;
            if (!controller || !WorldRegions.TerrainAt(point)) return false;
            landing.y = snapToGround ? SampleSafeGroundY(point, .15f) : point.y + .025f;
            Quaternion facing = Quaternion.Euler(0, heading, 0);
            var center = landing + facing * controller.center;
            float capOffset = Mathf.Max(0, controller.height * .5f - controller.radius);
            var axis = Vector3.up * capOffset;
            foreach (var hit in Physics.OverlapCapsule(center - axis, center + axis, controller.radius * .96f,
                         Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(controller.transform)) return false;
            return true;
        }

        internal bool TryVillageTourTravel(Vector3 point, float heading, bool snapToGround)
        {
            if (!CanUseVillageTour || !TryVillageTourLanding(motor, point, heading, snapToGround, out var landing))
                return false;

            motor.enabled = false;
            transform.SetPositionAndRotation(landing, Quaternion.Euler(0, heading, 0));
            motor.enabled = true;
            Physics.SyncTransforms();
            moveVelocity = Vector3.zero;
            vertical = 0;
            jumpInput.Reset();
            ClearCombatInputBuffer();
            SetTarget(null);
            orbit.FaceTravel(heading);
            return true;
        }
    }
}
