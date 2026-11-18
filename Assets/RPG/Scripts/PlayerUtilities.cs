using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public partial class PlayerController
    {
        public bool Blocking { get; private set; }
        public float GuardReady { get; private set; }
        public float GuardEnds { get; private set; }
        public float HearthReady { get; private set; }
        public bool ReturningHome { get; private set; }
        public float ReturnProgress => ReturningHome ? Mathf.Clamp01((Time.time - returnStarted) / 5) : 0;
        float returnStarted, returnHitTime;
        Vector3 returnPosition;
        bool UpdateUtilities(Keyboard keys)
        {
            if (!Stats.Alive) { StopGuard(); ReturningHome = false; return false; }
            bool uiBlocked = AnyBlockingWindowOpen();
            if (Blocking && (!keys.qKey.isPressed || Time.time >= GuardEnds || !motor.isGrounded || uiBlocked)) StopGuard();
            bool isSteering = orbit && orbit.IsSteering;
            bool isMoving = keys.wKey.isPressed || keys.sKey.isPressed || keys.aKey.isPressed || keys.dKey.isPressed;
            if (!Blocking && keys.qKey.isPressed && !isSteering && !isMoving && !uiBlocked && Time.time >= GuardReady && Time.time >= busyUntil && motor.isGrounded && !ReturningHome)
            {
                Blocking = true; GuardEnds = Time.time + 3; Stats.incomingDamageMultiplier = .5f;
                moveVelocity = Vector3.zero; if (animator) animator.speed = 1; Play("Block", true);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                {
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cancel", 0f);
                    Network.RiftboundCoopPlayer.LocalOwner.SyncDefenseState(true, false);
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Block", 0.25f);
                }
            }
            if (keys.hKey.wasPressedThisFrame && !ReturningHome && !Blocking && !uiBlocked && Time.time >= busyUntil)
            {
                if (Time.time < HearthReady) Message = "Hearthstone is still recharging.";
                else if (!motor.isGrounded || InCombat()) Message = "Hearthstone requires safe ground, outside combat.";
                else
                {
                    ReturningHome = true; returnStarted = Time.time; returnPosition = transform.position; returnHitTime = Stats.LastHit; moveVelocity = Vector3.zero;
                    CastBarSystem.StartCast("Hearthstone...", 5.0f, null, true, true);
                }
            }
            if (!ReturningHome) return false;
            bool cancel = keys.wKey.isPressed || keys.aKey.isPressed || keys.sKey.isPressed || keys.dKey.isPressed || keys.upArrowKey.isPressed || keys.downArrowKey.isPressed || keys.leftArrowKey.isPressed || keys.rightArrowKey.isPressed || keys.spaceKey.wasPressedThisFrame || keys.eKey.wasPressedThisFrame || keys.mKey.wasPressedThisFrame || keys.iKey.wasPressedThisFrame || keys.qKey.isPressed || Stats.LastHit > returnHitTime || InCombat() || Vector3.Distance(transform.position, returnPosition) > .2f;
            cancel |= keys.jKey.wasPressedThisFrame || keys.digit1Key.wasPressedThisFrame || keys.digit2Key.wasPressedThisFrame || keys.digit3Key.wasPressedThisFrame || keys.digit4Key.wasPressedThisFrame;
            if (cancel) { ReturningHome = false; CastBarSystem.CancelCast("Hearthstone interrupted."); Message = "Hearthstone interrupted."; return false; }
            Play("Idle"); if (animator) animator.speed = 1; GravityOnly();
            if (Time.time - returnStarted >= 5)
            {
                ReturningHome = false; HearthReady = Time.time + 60;
                Vector3 dest = (RestedXPSystem.Instance != null && RestedXPSystem.Instance.HearthHomePosition != Vector3.zero) ? RestedXPSystem.Instance.HearthHomePosition : spawn;
                dest = ClampAboveGround(dest, 0.30f);
                string destName = (RestedXPSystem.Instance != null && !string.IsNullOrEmpty(RestedXPSystem.Instance.HearthHomeLocationName)) ? RestedXPSystem.Instance.HearthHomeLocationName : "Sanctuary";
                motor.enabled = false; transform.position = dest; motor.enabled = true; vertical = 0; moveVelocity = Vector3.zero; Target = null;
                CastBarSystem.CancelCast();
                Message = $"Returned to {destName}."; GameSound.Play(SoundCue.Heal, transform.position); SaveAdventure(false);
            }
            return true;
        }
        void StopGuard()
        {
            bool wasBlocking = Blocking;
            if (Blocking) GuardReady = Time.time + 6;
            Blocking = false; if (Stats) Stats.incomingDamageMultiplier = 1;
            if (wasBlocking && Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.SyncDefenseState(false, false);
                Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cancel", 0f);
            }
        }
        public void ToggleGuardPublic()
        {
            if (Blocking) StopGuard();
            else if (Time.time >= GuardReady && motor && motor.isGrounded && !DialogueOpen && !ShopOpen && !MapOpen && !JournalOpen && !InventoryOpen && !ReturningHome)
            {
                Blocking = true; GuardEnds = Time.time + 3; Stats.incomingDamageMultiplier = .5f;
                moveVelocity = Vector3.zero; if (animator) animator.speed = 1; Play("Block", true);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                {
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cancel", 0f);
                    Network.RiftboundCoopPlayer.LocalOwner.SyncDefenseState(true, false);
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Block", 0.25f);
                }
            }
        }
        public void TriggerHearthstonePublic()
        {
            if (ReturningHome) return;
            if (Time.time < HearthReady) { Message = "Hearthstone is still recharging."; return; }
            if (!motor || !motor.isGrounded || InCombat()) { Message = "Hearthstone requires safe ground, outside combat."; return; }
            ReturningHome = true; returnStarted = Time.time; returnPosition = transform.position; returnHitTime = Stats.LastHit; moveVelocity = Vector3.zero;
            CastBarSystem.StartCast("Hearthstone...", 5.0f, null, true, true);
        }
    }
}
