using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public partial class PlayerController
    {
        public MountDefinition[] mounts;
        public bool Mounted => mountInstance;
        bool RidingFlyingMount => Mounted && SelectedMount && SelectedMount.isFlying;
        public bool IsFlying => RidingFlyingMount && motor && !motor.isGrounded;
        public bool Mounting { get; private set; }
        public float MountProgress => Mounting ? Mathf.Clamp01((Time.time - mountStarted) / 1.5f) : 0;
        public string SelectedMountId { get; private set; } = "horse";
        public MountDefinition SelectedMount
        {
            get
            {
                if (mounts == null) return null;
                foreach (var mount in mounts) if (mount && mount.mountId == SelectedMountId) return mount;
                return mounts.Length > 0 ? mounts[0] : null;
            }
        }
        MountVisual mountInstance;
        Transform riderHips;
        Renderer weaponRenderer;
        bool weaponWasEnabled;
        Vector3 riderPosition, normalCenter, mountPosition;
        float normalHeight, normalRadius, mountStarted, mountHitTime;
        public bool MountUnlocked(MountDefinition mount) => mount && (!mount.requiresExploration || GreenhollowLandmarksFound == 3);
        bool MountWindowOpen => AnyBlockingWindowOpen();
        bool UpdateMounts(Keyboard keys)
        {
            if (Mounted)
            {
                // Dismount with Z or when using combat/utility abilities (1, 2, 3, 4, Q, H); E remains reserved for contextual interact
                bool action = keys.digit1Key.wasPressedThisFrame || keys.digit2Key.wasPressedThisFrame || keys.digit3Key.wasPressedThisFrame || keys.digit4Key.wasPressedThisFrame || keys.qKey.wasPressedThisFrame || keys.hKey.wasPressedThisFrame;
                bool allowAutoDismount = !SettingsSystem.Instance || SettingsSystem.Instance.autoDismount;
                if (!Stats.Alive || keys.zKey.wasPressedThisFrame || (allowAutoDismount && !MountWindowOpen && action)) { Dismount(); return false; }
                if (MountWindowOpen) mountInstance.Animate(0, false, false);
                return false;
            }
            if (keys.zKey.wasPressedThisFrame && Mounting) { Mounting = false; CastBarSystem.CancelCast(); Message = "Mount summon cancelled."; return false; }
            if (keys.vKey.wasPressedThisFrame && !Mounting && !MountWindowOpen && !ReturningHome && !Blocking) CycleMount();
            if (keys.zKey.wasPressedThisFrame && !Mounting && !MountWindowOpen) BeginMount();
            if (!Mounting) return false;
            bool move = keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed || keys.upArrowKey.isPressed || keys.downArrowKey.isPressed || keys.leftArrowKey.isPressed || keys.rightArrowKey.isPressed;
            bool actionKey = keys.spaceKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame || keys.qKey.wasPressedThisFrame || keys.hKey.wasPressedThisFrame || keys.iKey.wasPressedThisFrame || keys.jKey.wasPressedThisFrame || keys.mKey.wasPressedThisFrame || keys.digit1Key.wasPressedThisFrame || keys.digit2Key.wasPressedThisFrame || keys.digit3Key.wasPressedThisFrame || keys.digit4Key.wasPressedThisFrame;
            if (!Stats.Alive || move || actionKey || MountWindowOpen || Stats.LastHit > mountHitTime || InCombat() || Vector3.Distance(transform.position, mountPosition) > .2f)
            { Mounting = false; CastBarSystem.CancelCast("Mount summon interrupted."); Message = "Mount summon interrupted."; return false; }
            moveVelocity = Vector3.zero; Play("Idle"); if (animator) animator.speed = 1; GravityOnly();
            if (Time.time - mountStarted >= 1.5f) { CastBarSystem.CancelCast(); FinishMount(); }
            return true;
        }
        public void CycleMount()
        {
            if (Mounted || Mounting || mounts == null || mounts.Length == 0) return;
            int index = System.Array.FindIndex(mounts, m => m && m.mountId == SelectedMountId);
            for (int i = 1; i <= mounts.Length; i++)
            {
                var mount = mounts[(Mathf.Max(0, index) + i) % mounts.Length];
                if (!MountUnlocked(mount)) continue;
                SelectedMountId = mount.mountId;
                Message = mount.mountId == "horse" && GreenhollowLandmarksFound < 3 ? "Emberwatch Horse selected. Scout all three Greenhollow landmarks to unlock the zebra." : "Mount selected: " + mount.displayName + ". [Z] Summon";
                SaveAdventure(false); return;
            }
        }
        public void BeginMount()
        {
            if (Mounted || Mounting || !Stats.Alive || FrontendMenu.IsOpen || MountWindowOpen || ReturningHome || Blocking || (orbit && orbit.Paused) || Time.time < busyUntil) return;
            if (!SelectedMount || !SelectedMount.prefab || !MountUnlocked(SelectedMount)) { Message = "This mount is not available yet."; return; }
            if (!animator || !animator.HasState(0, Animator.StringToHash("Ride"))) { Message = "Riding animation is unavailable."; return; }
            if (!motor.isGrounded || InCombat()) { Message = "Summon a mount on safe ground, outside combat."; return; }
            if (!MountClearance()) { Message = "Find open space before summoning your mount."; return; }
            Mounting = true; mountStarted = Time.time; mountHitTime = Stats.LastHit; mountPosition = transform.position;
            CastBarSystem.StartCast($"Summoning {SelectedMount.displayName}...", 1.5f, null, true, true);
            moveVelocity = Vector3.zero; Message = "Summoning " + SelectedMount.displayName + "...";
        }
        bool MountClearance() => !Physics.CheckCapsule(transform.position + Vector3.up * .68f, transform.position + Vector3.up * 2.95f, .62f, ~(1 << 2), QueryTriggerInteraction.Ignore)
            && !Physics.CheckBox(transform.position + Vector3.up * 1.3f, new Vector3(.58f, .5f, 1.25f), transform.rotation, ~(1 << 2), QueryTriggerInteraction.Ignore);
        bool MountPathClear(Vector3 velocity) => velocity.sqrMagnitude < .001f || !Physics.BoxCast(transform.position + Vector3.up * 1.3f, new Vector3(.58f, .5f, 1.25f), velocity.normalized, out _, transform.rotation, velocity.magnitude * Time.deltaTime + .04f, ~(1 << 2), QueryTriggerInteraction.Ignore);
        void FinishMount()
        {
            Mounting = false;
            if (!MountClearance() || !motor.isGrounded || InCombat()) { Message = "Cannot mount here."; return; }
            normalHeight = motor.height; normalRadius = motor.radius; normalCenter = motor.center; riderPosition = animator.transform.localPosition;
            if (animator.isHuman) riderHips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!riderHips)
            {
                foreach (var bone in animator.GetComponentsInChildren<Transform>())
                {
                    string bName = bone.name.ToLowerInvariant();
                    if (bone.name.EndsWith(":Hips") || bName == "hips" || bName.Contains("hips") || bName.Contains("pelvis"))
                    { riderHips = bone; break; }
                }
            }
            if (!riderHips) { Message = "Riding pose is unavailable."; return; }
            mountInstance = Instantiate(SelectedMount.prefab, transform); mountInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            motor.height = 3.6f; motor.center = Vector3.up * 1.8f; motor.radius = .62f;
            foreach (var renderer in animator.GetComponentsInChildren<Renderer>()) if (renderer.name.ToLowerInvariant().Contains("axe")) { weaponRenderer = renderer; weaponWasEnabled = renderer.enabled; renderer.enabled = false; break; }
            animator.speed = 1; Play("Ride", true); moveVelocity = Vector3.zero;
            SpellFX.CreateArcaneBurst(transform.position);
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                Network.RiftboundCoopPlayer.LocalOwner.RequestSetMount(true, SelectedMountId);
            Message = SelectedMount.displayName + " / [Z] Dismount. Combat actions also dismount.";
        }
        public void ToggleMount()
        {
            if (Mounted) Dismount();
            else BeginMount();
        }

        public void Dismount()
        {
            Mounting = false; if (!Mounted) return;
            Destroy(mountInstance.gameObject); mountInstance = null;
            transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
            jumpInput.Reset();
            if (motor)
            {
                motor.radius = normalRadius; motor.height = normalHeight; motor.center = normalCenter;
            }
            if (animator) animator.transform.localPosition = riderPosition;
            if (weaponRenderer) weaponRenderer.enabled = weaponWasEnabled;
            weaponRenderer = null; riderHips = null; moveVelocity = Vector3.zero; if (animator) { animator.speed = 1; Play("Idle", true); }
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                Network.RiftboundCoopPlayer.LocalOwner.RequestSetMount(false, SelectedMountId);
            SpellFX.SpawnBurst(transform.position + Vector3.up * 0.4f, new Color(0.88f, 0.82f, 0.72f), new Color(0.6f, 0.55f, 0.48f), 12, 1.5f, 3.5f, 0.12f, 0.25f, 0.4f);
        }
        public void MountForFlightTaxi()
        {
            Mounting = false;
            if (Mounted || mounts == null || mounts.Length == 0 || !animator) return;
            MountDefinition taxiDef = null;
            foreach (var m in mounts) if (m && m.isFlying && m.prefab) { taxiDef = m; break; }
            if (!taxiDef) taxiDef = SelectedMount;
            if (!taxiDef || !taxiDef.prefab) return;

            normalHeight = motor ? motor.height : 1.8f;
            normalRadius = motor ? motor.radius : 0.38f;
            normalCenter = motor ? motor.center : Vector3.up * 0.9f;
            riderPosition = animator.transform.localPosition;
            if (animator.isHuman) riderHips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!riderHips)
            {
                foreach (var bone in animator.GetComponentsInChildren<Transform>())
                {
                    string bName = bone.name.ToLowerInvariant();
                    if (bone.name.EndsWith(":Hips") || bName == "hips" || bName.Contains("hips") || bName.Contains("pelvis"))
                    { riderHips = bone; break; }
                }
            }
            mountInstance = Instantiate(taxiDef.prefab, transform);
            mountInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var renderer in animator.GetComponentsInChildren<Renderer>())
                if (renderer.name.ToLowerInvariant().Contains("axe")) { weaponRenderer = renderer; weaponWasEnabled = renderer.enabled; renderer.enabled = false; break; }
            animator.speed = 1f;
            if (animator.HasState(0, Animator.StringToHash("Ride"))) Play("Ride", true);
            moveVelocity = Vector3.zero;
            vertical = 0f;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                Network.RiftboundCoopPlayer.LocalOwner.RequestSetMount(true, taxiDef.mountId);
        }
        public void AnimateFlightTaxi()
        {
            if (mountInstance) mountInstance.Animate(14f, true, false);
        }
        public void EndFlightTaxiMount(bool keepMounted)
        {
            vertical = 0f;
            moveVelocity = Vector3.zero;
            if (!keepMounted && Mounted) Dismount();
        }
        void PositionRider()
        {
            if (!Mounted || !riderHips || !mountInstance || !mountInstance.saddle) return;
            if (motor && motor.enabled && motor.isGrounded && Physics.Raycast(transform.position + Vector3.up * .5f, Vector3.down, out var ground, 1.2f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                mountInstance.transform.localPosition = Vector3.up * (ground.point.y - transform.position.y + .02f);
            Vector3 hipWorldOffset = riderHips.position - animator.transform.position;
            animator.transform.position = mountInstance.saddle.position - hipWorldOffset;
        }
        void RestoreMountSelection(string id)
        {
            SelectedMountId = "horse";
            if (mounts == null) return;
            foreach (var mount in mounts) if (mount && mount.mountId == id && MountUnlocked(mount)) { SelectedMountId = id; return; }
        }
        public void ToggleMountPublic()
        {
            if (Mounted) Dismount();
            else if (!Mounting) BeginMount();
        }
    }
}
