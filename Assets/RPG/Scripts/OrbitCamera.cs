using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class OrbitCamera : MonoBehaviour
    {
        public Transform target;
        public float distance = 7;
        public float mouseSensitivity = 1;
        float yaw, pitch = 23;
        float viewHeight = 1.6f;
        public bool Paused { get; private set; }
        public float Yaw => yaw;
        public float Pitch => pitch;
        public void FaceTravel(float heading) { yaw = heading; pitch = 18f; }
        public bool IsSteering { get; private set; }
        public bool IsFreeLooking { get; private set; }
        public bool BothMouseButtonsPressed { get; private set; }
        PlayerController player;
        static OrbitCamera activeInstance;
        float shakeIntensity, shakeRemaining, shakeDuration;
        void Awake() { activeInstance = this; }
        public static void Shake(float intensity, float duration)
        {
            if (!activeInstance || activeInstance.Paused) return;
            activeInstance.shakeIntensity = Mathf.Max(activeInstance.shakeIntensity, intensity);
            activeInstance.shakeDuration = Mathf.Max(0.01f, duration);
            activeInstance.shakeRemaining = Mathf.Max(activeInstance.shakeRemaining, duration);
        }
        void Start()
        {
            if (target)
            {
                player = target.GetComponent<PlayerController>();
                yaw = target.eulerAngles.y;
            }
        }
        void Update()
        {
            if (FrontendMenu.IsOpen) return;
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (PartySystem.Instance && PartySystem.Instance.IsCompanionMenuOpen) { PartySystem.Instance.CloseCompanionMenu(); return; }
                if (PrototypeHUD.MenuOpen) { PrototypeHUD.MenuOpen = false; return; }
                if (player && player.DialogueOpen) { player.CloseDialogue(); return; }
                if (FlightMaster.IsAnyTaxiMapOpen) { FlightMaster.CloseActiveTaxiWindow(); return; }
                if (player && player.ShopOpen) { player.CloseShop(); return; }
                if (player && player.MapOpen) { player.MapOpen = false; return; }
                if (player && player.JournalOpen) { player.JournalOpen = false; return; }
                if (player && player.EncounterJournalOpen) { player.EncounterJournalOpen = false; return; }
                if (player && player.InventoryOpen) { player.InventoryOpen = false; return; }
                if (player && player.TalentsOpen) { player.TalentsOpen = false; return; }
                if (player && player.SpellbookOpen) { player.SpellbookOpen = false; return; }
                if (player && player.AchievementOpen) { player.AchievementOpen = false; return; }
                if (player && player.ReputationOpen) { player.ReputationOpen = false; return; }
                if (ProfessionSystem.IsOpen) { ProfessionSystem.IsOpen = false; return; }
                if (AuctionHouseSystem.AuctionOpen) { AuctionHouseSystem.AuctionOpen = false; return; }
                if (GuildSystem.Instance && GuildSystem.Instance.IsOpen) { GuildSystem.Instance.IsOpen = false; return; }
                if (MailboxSystem.Instance && MailboxSystem.Instance.IsOpen) { MailboxSystem.Instance.IsOpen = false; return; }
                if (CharacterSheetSystem.Instance && CharacterSheetSystem.Instance.IsOpen) { CharacterSheetSystem.Instance.IsOpen = false; return; }
                if (MacroSystem.Instance && MacroSystem.Instance.IsOpen) { MacroSystem.Instance.IsOpen = false; return; }
                if (GlyphSystem.Instance && GlyphSystem.Instance.IsWindowOpen) { GlyphSystem.Instance.IsWindowOpen = false; return; }
                if (GemSocketSystem.Instance && GemSocketSystem.Instance.IsWindowOpen) { GemSocketSystem.Instance.IsWindowOpen = false; return; }
                if (EquipmentSetSystem.Instance && EquipmentSetSystem.Instance.IsWindowOpen) { EquipmentSetSystem.Instance.IsWindowOpen = false; return; }
                if (WorldPvPSystem.Instance && WorldPvPSystem.Instance.IsOverviewOpen) { WorldPvPSystem.Instance.IsOverviewOpen = false; return; }
                if (BarberShopSystem.Instance && BarberShopSystem.Instance.IsWindowOpen) { BarberShopSystem.Instance.CloseBarber(); return; }
                if (PetBattleSystem.Instance && PetBattleSystem.Instance.IsBattleActive) { PetBattleSystem.Instance.ForfeitBattle(); return; }
                if (PetBattleSystem.Instance && PetBattleSystem.Instance.IsJournalOpen) { PetBattleSystem.Instance.IsJournalOpen = false; return; }
                if (CalendarEventSystem.Instance && CalendarEventSystem.Instance.IsWindowOpen) { CalendarEventSystem.Instance.IsWindowOpen = false; return; }
                if (FishingSystem.Instance && FishingSystem.Instance.IsWindowOpen) { FishingSystem.Instance.IsWindowOpen = false; return; }
                if (TransmogSystem.Instance && TransmogSystem.Instance.IsWindowOpen) { TransmogSystem.Instance.IsWindowOpen = false; return; }
                if (ArchaeologySystem.Instance && ArchaeologySystem.Instance.IsWindowOpen) { ArchaeologySystem.Instance.IsWindowOpen = false; return; }
                if (DungeonFinderSystem.Instance && DungeonFinderSystem.Instance.IsWindowOpen) { DungeonFinderSystem.Instance.IsWindowOpen = false; return; }
                if (SettingsSystem.Instance && SettingsSystem.Instance.IsMenuOpen) { SettingsSystem.Instance.CloseMenu(); return; }
                if (BattlegroundSystem.Instance && BattlegroundSystem.Instance.IsWindowOpen) { BattlegroundSystem.Instance.IsWindowOpen = false; return; }

                // Deselect target if a unit is currently targeted
                if (player && player.Target != null) { player.SetTarget(null); return; }

                if (SettingsSystem.Instance) { SettingsSystem.Instance.ToggleMenu(); } else { SetPaused(!Paused); }
            }
        }
        int inputFrame = -1;

        public void RefreshInput()
        {
            var mouse = Mouse.current;
            bool blocked = !target || FrontendMenu.IsOpen || Paused || FlightMaster.IsAnyTaxiMapOpen ||
                (player && player.AnyBlockingWindowOpen()) || PrototypeHUD.MenuOpen ||
                (MailboxSystem.Instance && MailboxSystem.Instance.IsOpen) ||
                (BattlegroundSystem.Instance && BattlegroundSystem.Instance.IsWindowOpen) ||
                (MacroSystem.Instance && MacroSystem.Instance.IsOpen) ||
                (PartySystem.Instance && PartySystem.Instance.IsCompanionMenuOpen);
            if (mouse != null)
                blocked |= PrototypeHUD.QuestPointerTakesMouse(player, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame);
            if (!blocked && mouse != null && player)
                blocked = MinimapRadar.TakesMouse(player, mouse.position.ReadValue());
            if (blocked || mouse == null)
            {
                IsSteering = IsFreeLooking = BothMouseButtonsPressed = false;
                return;
            }
            if (inputFrame == Time.frameCount) return;
            inputFrame = Time.frameCount;
            bool rightDown = mouse.rightButton.isPressed, leftDown = mouse.leftButton.isPressed;
            IsSteering = rightDown;
            IsFreeLooking = leftDown && !rightDown;
            BothMouseButtonsPressed = rightDown && leftDown;
            if (rightDown || leftDown)
            {
                bool flying = player && player.Mounted && player.SelectedMount && player.SelectedMount.isFlying;
                yaw += mouse.delta.ReadValue().x * .22f * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - mouse.delta.ReadValue().y * .16f * mouseSensitivity, flying ? -65f : -15f, 75f);
            }
            distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * .008f, 2.5f, 15f);
        }

        void LateUpdate()
        {
            RefreshInput();
            if (!target || FrontendMenu.IsOpen) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            float targetHeight = player ? (player.Mounted ? 2.4f : player.EyeHeight) : 1.6f;
            viewHeight = Mathf.Lerp(viewHeight, targetHeight, 1 - Mathf.Exp(-6 * Time.deltaTime));
            var pivot = target.position + Vector3.up * viewHeight;
            var backwards = rotation * Vector3.back;
            float actual = distance;
            if (Physics.SphereCast(pivot, .2f, backwards, out var hit, distance, ~ (1 << 2), QueryTriggerInteraction.Ignore))
                actual = Mathf.Max(.6f, hit.distance - .15f);
            Vector3 shakeOffset = Vector3.zero;
            if (shakeRemaining > 0f)
            {
                shakeRemaining -= Time.unscaledDeltaTime;
                float falloff = Mathf.Clamp01(shakeRemaining / shakeDuration);
                float mag = shakeIntensity * falloff;
                shakeOffset = (transform.right * Random.Range(-1f, 1f) + transform.up * Random.Range(-1f, 1f)) * mag;
                if (shakeRemaining <= 0f) shakeIntensity = 0f;
            }
            transform.SetPositionAndRotation(pivot + backwards * actual + shakeOffset, rotation);
        }
        void OnDisable() { Time.timeScale = 1; }
        public void SetPaused(bool paused) { Paused = paused; Time.timeScale = paused ? 0 : 1; }
    }
}
