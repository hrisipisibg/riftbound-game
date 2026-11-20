using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    [RequireComponent(typeof(CharacterController), typeof(Combatant))]
    public partial class PlayerController : MonoBehaviour
    {
        public Animator animator;
        public float attackDuration = 1.2f;
        public float cleaveDuration = 1.22f;
        public float walkSpeed = 1.5f;
        public float runSpeed = 5.5f;
        public float backwardMultiplier = .65f;
        public float walkStrideSpeed = .987f;
        public float runStrideSpeed = 2.415f;
        public float backRunStrideSpeed = 2.415f;
        public float backWalkStrideSpeed = .987f;
        public float walkPhase, runPhase, backWalkPhase, backRunPhase;
        public bool Walking { get; private set; }
        public OrbitCamera orbit;
        public Combatant Target { get; private set; }
        public float CleaveReady { get; private set; }
        public float HealReady { get; private set; }
        public int Kills { get; private set; }
        public bool QuestAccepted { get; private set; }
        public bool QuestComplete { get; private set; }
        public bool DialogueOpen { get; private set; }
        public QuestGiver ActiveQuestGiver { get; private set; }
        public TownNPC ActiveTownNPC { get; private set; }
        public int Gold { get; private set; }
        public Transform questGiver;
        public string Message { get; set; } = "Welcome to Emberwatch Outpost. Speak to Abe near the village gate.";
        public Combatant Stats { get; private set; }
        CharacterController motor;
        Vector3 spawn;
        float vertical, attackReady, busyUntil;
        string currentAnimation;
        LineRenderer targetRing;
        float sprintUntil;
        TrailRenderer sprintTrail;
        float dodgeReadyTime, dodgeEnds;
        Vector3 dodgeDir;
        public float DodgeCooldownRemaining => Mathf.Max(0f, dodgeReadyTime - Time.time);
        int bufferedAbilitySlot;
        float bufferedAbilityExpiresAt;
        int combatActionEpoch;
        public int BufferedAbilitySlot => Time.time < bufferedAbilityExpiresAt ? bufferedAbilitySlot : 0;
        public void ClearCombatInputBuffer() { bufferedAbilitySlot = 0; bufferedAbilityExpiresAt = 0f; }
        Vector3 moveVelocity;
        readonly JumpInputBuffer jumpInput = new JumpInputBuffer();
        readonly RaycastHit[] combatSightHits = new RaycastHit[16];
        Quaternion visualBaseRotation = Quaternion.identity;
        bool hasVisualBaseRotation;
        float visualYawOffset;

        public void OnVisualApplied(Animator anim)
        {
            if (anim)
            {
                visualBaseRotation = anim.transform.localRotation;
                hasVisualBaseRotation = true;
            }
            else
            {
                visualBaseRotation = Quaternion.identity;
                hasVisualBaseRotation = false;
            }
            visualYawOffset = 0f;
        }

        public bool AnyBlockingWindowOpen() =>
            DialogueOpen || ShopOpen || MapOpen || JournalOpen || EncounterJournalOpen || InventoryOpen || TalentsOpen || ChatOpen ||
            ProfessionSystem.IsOpen || AuctionHouseSystem.AuctionOpen || AuctionHouseSystem.MailboxOpen || SpellbookOpen || AchievementOpen || ReputationOpen ||
            FlightMaster.IsAnyTaxiMapOpen ||
            (GuildSystem.Instance && GuildSystem.Instance.IsOpen) ||
            (CharacterSheetSystem.Instance && CharacterSheetSystem.Instance.IsOpen) ||
            (SettingsSystem.Instance && SettingsSystem.Instance.IsMenuOpen) ||
            (DungeonFinderSystem.Instance && DungeonFinderSystem.Instance.IsWindowOpen) ||
            (ArchaeologySystem.Instance && ArchaeologySystem.Instance.IsWindowOpen) ||
            (TransmogSystem.Instance && TransmogSystem.Instance.IsWindowOpen) ||
            (FishingSystem.Instance && FishingSystem.Instance.IsWindowOpen) ||
            (CalendarEventSystem.Instance && CalendarEventSystem.Instance.IsWindowOpen) ||
            (BarberShopSystem.Instance && BarberShopSystem.Instance.IsWindowOpen) ||
            (WorldPvPSystem.Instance && WorldPvPSystem.Instance.IsOverviewOpen) ||
            (EquipmentSetSystem.Instance && EquipmentSetSystem.Instance.IsWindowOpen) ||
            (GlyphSystem.Instance && GlyphSystem.Instance.IsWindowOpen) ||
            (GemSocketSystem.Instance && GemSocketSystem.Instance.IsWindowOpen) ||
            (PetBattleSystem.Instance && (PetBattleSystem.Instance.IsJournalOpen || PetBattleSystem.Instance.IsBattleActive)) ||
            Network.CoopWorldManager.IsGroupMenuOpen;
        public float ClassResource { get; private set; } = 100f;
        public float MaxClassResource => CharacterClass == "Paladin" ? 5f : 100f;
        public string ClassResourceName => CharacterClass == "Warrior" ? "Rage" : CharacterClass == "Mage" ? "Mana" : CharacterClass == "Rogue" ? "Energy" : "Holy Power";
        public float EyeHeight
        {
            get
            {
                switch (CharacterRace)
                {
                    case "Dwarf": return 1.25f;
                    case "Goblin": return 1.15f;
                    case "Orc":
                    case "Brute": return 1.85f;
                    default: return 1.6f;
                }
            }
        }

        void Awake()
        {
            motor = GetComponent<CharacterController>();
            Stats = GetComponent<Combatant>();
            Stats.Damaged += OnDamaged;
            spawn = transform.position;
        }
        void Start()
        {
            if (!AudioEffects.Instance && !GetComponent<AudioEffects>()) gameObject.AddComponent<AudioEffects>();
            if (!ChatSystem.Instance && !GetComponent<ChatSystem>()) gameObject.AddComponent<ChatSystem>();
            if (!ZoneManager.Instance && !GetComponent<ZoneManager>()) gameObject.AddComponent<ZoneManager>();
            if (!ZoneBanner.Instance && !GetComponent<ZoneBanner>()) gameObject.AddComponent<ZoneBanner>();
            if (!MinimapRadar.Instance && !GetComponent<MinimapRadar>()) gameObject.AddComponent<MinimapRadar>();
            if (!CombatTextManager.Instance && !GetComponent<CombatTextManager>()) gameObject.AddComponent<CombatTextManager>();
            if (!LootDropSystem.Instance && !GetComponent<LootDropSystem>()) gameObject.AddComponent<LootDropSystem>();
            if (!CastBarSystem.Instance && !GetComponent<CastBarSystem>()) gameObject.AddComponent<CastBarSystem>();
            if (!BuffSystem.Instance && !GetComponent<BuffSystem>()) gameObject.AddComponent<BuffSystem>();
            if (!PartySystem.Instance && !GetComponent<PartySystem>()) gameObject.AddComponent<PartySystem>();
            if (!DungeonInstanceManager.Instance && !GetComponent<DungeonInstanceManager>()) gameObject.AddComponent<DungeonInstanceManager>();
            if (!DuelSystem.Instance && !GetComponent<DuelSystem>()) gameObject.AddComponent<DuelSystem>();
            if (!LootRollSystem.Instance && !GetComponent<LootRollSystem>()) gameObject.AddComponent<LootRollSystem>();
            if (!MailboxSystem.Instance && !GetComponent<MailboxSystem>()) gameObject.AddComponent<MailboxSystem>();
            if (!BattlegroundSystem.Instance && !GetComponent<BattlegroundSystem>()) gameObject.AddComponent<BattlegroundSystem>();
            if (!GuildSystem.Instance && !GetComponent<GuildSystem>()) gameObject.AddComponent<GuildSystem>();
            if (!CharacterSheetSystem.Instance && !GetComponent<CharacterSheetSystem>()) gameObject.AddComponent<CharacterSheetSystem>();
            if (!AchievementSystem.Instance && !GetComponent<AchievementSystem>()) gameObject.AddComponent<AchievementSystem>();
            if (!ReputationSystem.Instance && !GetComponent<ReputationSystem>()) gameObject.AddComponent<ReputationSystem>();
            if (!EncounterJournal.Instance && !GetComponent<EncounterJournal>()) gameObject.AddComponent<EncounterJournal>();
            FactionQuestSystem.EnsureSystem(this);
            // Loading a character can publish zone notifications and use these services.
            var frontend = GetComponent<FrontendMenu>() ?? gameObject.AddComponent<FrontendMenu>();
            frontend.Initialize(this);
            targetRing = new GameObject("Selected target ring").AddComponent<LineRenderer>();
            targetRing.useWorldSpace = true; targetRing.loop = true; targetRing.positionCount = 48;
            targetRing.startWidth = .055f; targetRing.endWidth = .055f;
            targetRing.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            targetRing.material.SetColor("_BaseColor", new Color(1, .6f, .15f));
            targetRing.enabled = false;
        }
        void LateUpdate()
        {
            PositionRider();
            if (!targetRing) return;
            targetRing.enabled = Target && Target.Alive;
            if (!targetRing.enabled) return;
            for (int i = 0; i < targetRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2 / targetRing.positionCount;
                targetRing.SetPosition(i, Target.transform.position + new Vector3(Mathf.Sin(angle) * .7f, .03f, Mathf.Cos(angle) * .7f));
            }
        }
        void OnDestroy()
        {
            if (Stats) Stats.Damaged -= OnDamaged;
            if (!targetRing) return;
            Destroy(targetRing.material); Destroy(targetRing.gameObject);
        }
        public bool TalentsOpen { get; set; }
        public bool SpellbookOpen { get; set; }
        public bool AchievementOpen { get; set; }
        public bool ReputationOpen { get; set; }
        public bool EncounterJournalOpen { get; set; }
        public bool ChatOpen { get; set; }

        void Update()
        {
            UpdateClassResource();
            if (FlightMaster.IsAnyTaxiInFlight)
            {
                jumpInput.Reset();
                moveVelocity = Vector3.zero;
                vertical = 0f;
                return;
            }
            var keys = Keyboard.current;
            if (keys == null || FrontendMenu.IsOpen || (orbit && orbit.Paused))
            {
                jumpInput.Reset();
                ClearCombatInputBuffer();
                return;
            }
            if (orbit) orbit.RefreshInput();
            if (AnyBlockingWindowOpen() || PrototypeHUD.MenuOpen)
            {
                ClearCombatInputBuffer();
                jumpInput.Reset();
            }
            if (PrototypeHUD.MenuOpen || (PartySystem.Instance && PartySystem.Instance.IsCompanionMenuOpen))
            {
                if (animator) animator.speed = 1;
                Play(Mounted ? "Ride" : "Idle");
                HoldMenuPosition();
                return;
            }
            if (UpdateMounts(keys)) return;
            if (UpdateUtilities(keys)) return;
            if (keys.mKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil) { MapOpen = !MapOpen; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; }
            if (keys.jKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking)
            {
                if (keys.shiftKey.isPressed)
                {
                    EncounterJournalOpen = !EncounterJournalOpen;
                    MapOpen = false; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; SpellbookOpen = false; AchievementOpen = false; ReputationOpen = false;
                }
                else
                {
                    JournalOpen = !JournalOpen;
                    MapOpen = false; InventoryOpen = false; EncounterJournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; SpellbookOpen = false; AchievementOpen = false; ReputationOpen = false;
                }
            }
            if (keys.nKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking) { TalentsOpen = !TalentsOpen; MapOpen = false; InventoryOpen = false; JournalOpen = false; ProfessionSystem.IsOpen = false; SpellbookOpen = false; }
            if (keys.kKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking) { ProfessionSystem.IsOpen = !ProfessionSystem.IsOpen; MapOpen = false; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; SpellbookOpen = false; }
            if (keys.pKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking) { SpellbookOpen = !SpellbookOpen; MapOpen = false; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; AchievementOpen = false; }
            if (keys.yKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking) { AchievementOpen = !AchievementOpen; MapOpen = false; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; SpellbookOpen = false; ReputationOpen = false; }
            if (keys.uKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking) { ReputationOpen = !ReputationOpen; MapOpen = false; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; SpellbookOpen = false; AchievementOpen = false; }
            if (keys.gKey.wasPressedThisFrame && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen && Time.time >= busyUntil && !Blocking)
            {
                var gSys = GuildSystem.Instance ?? GetComponent<GuildSystem>();
                if (gSys)
                {
                    gSys.IsOpen = !gSys.IsOpen;
                    MapOpen = false; InventoryOpen = false; JournalOpen = false; TalentsOpen = false; ProfessionSystem.IsOpen = false; SpellbookOpen = false; AchievementOpen = false; ReputationOpen = false;
                }
            }
            if (ShopOpen || MapOpen || JournalOpen || EncounterJournalOpen || TalentsOpen || ChatOpen || ProfessionSystem.IsOpen || SpellbookOpen || AchievementOpen || ReputationOpen || FlightMaster.IsAnyTaxiMapOpen || (GuildSystem.Instance && GuildSystem.Instance.IsOpen))
            {
                Play(Mounted ? "Ride" : "Idle"); if (animator) animator.speed = 1;
                HoldMenuPosition();
                if (keys.escapeKey.wasPressedThisFrame)
                {
                    if (ShopOpen) CloseShop();
                    MapOpen = false; JournalOpen = false; EncounterJournalOpen = false;
                    TalentsOpen = false; SpellbookOpen = false; AchievementOpen = false;
                    ReputationOpen = false; ProfessionSystem.IsOpen = false;
                    if (GuildSystem.Instance) GuildSystem.Instance.IsOpen = false;
                    return;
                }
                if (ShopOpen && (!CanTrade() || keys.eKey.wasPressedThisFrame)) CloseShop();
                return;
            }
            if (DialogueOpen)
            {
                Play(Mounted ? "Ride" : "Idle"); if (animator) animator.speed = 1;
                HoldMenuPosition();
                bool withinRange = (ActiveQuestGiver != null && CanTalk()) || 
                                   (ActiveTownNPC != null && Vector3.Distance(transform.position, ActiveTownNPC.transform.position) < 4.5f);
                if (keys.escapeKey.wasPressedThisFrame || !withinRange || Stats.LastHit > dialogueOpenedAt || (Time.time > dialogueOpenedAt + 0.35f && keys.eKey.wasPressedThisFrame)) CloseDialogue();
                return;
            }
            if (!Stats.Alive)
            {
                GravityOnly();
                if (keys.rKey.wasPressedThisFrame) Respawn();
                return;
            }
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (InventoryOpen) InventoryOpen = false;
                else if (CharacterSheetSystem.Instance && CharacterSheetSystem.Instance.IsOpen) CharacterSheetSystem.Instance.IsOpen = false;
                else if (PrototypeHUD.MenuOpen) PrototypeHUD.MenuOpen = false;
                else if (Target != null) Target = null;
                else PrototypeHUD.MenuOpen = true;
            }
            if (keys.tabKey.wasPressedThisFrame) SelectTarget();
            if (keys.iKey.wasPressedThisFrame || keys.bKey.wasPressedThisFrame) InventoryOpen = !InventoryOpen;
            if (keys.f5Key.wasPressedThisFrame) SaveAdventure();
            if (keys.digit4Key.wasPressedThisFrame) UsePotion();
            if (Target && (!Target.Alive || Vector3.Distance(transform.position, Target.transform.position) > 28)) Target = null;

            var mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame) &&
                !MinimapRadar.TakesMouse(this, mouse.position.ReadValue()) &&
                !PrototypeHUD.QuestPointerTakesMouse(this, mouse.position.ReadValue(), mouse.leftButton.wasPressedThisFrame) &&
                !DialogueOpen && !ShopOpen && !MapOpen && !JournalOpen && !InventoryOpen && Camera.main)
            {
                var ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out var hit, 40f, ~(1 << 2), QueryTriggerInteraction.Collide))
                {
                    var lootTarget = hit.collider.GetComponentInParent<LootClickTarget>();
                    if (lootTarget != null && lootTarget.corpse != null && LootDropSystem.Instance)
                    {
                        if (Vector3.Distance(transform.position, lootTarget.corpse.position) <= 4.5f)
                        {
                            LootDropSystem.Instance.LootAll(lootTarget.corpse);
                            return;
                        }
                        else
                        {
                            Message = "Move closer to loot the remains.";
                        }
                    }
                    var combatant = hit.collider.GetComponentInParent<Combatant>();
                    if (combatant && !combatant.isPlayer && combatant.Alive && mouse.leftButton.wasPressedThisFrame) Target = combatant;
                }
            }

            if (keys.cKey.wasPressedThisFrame && !DialogueOpen && !ShopOpen && !ChatOpen)
            {
                var csSys = CharacterSheetSystem.Instance ?? GetComponent<CharacterSheetSystem>();
                if (csSys) csSys.ToggleSheet();
            }
            bool isFlyingMount = RidingFlyingMount;
            // Some overlays, including inventory and the character sheet, open later in this update.
            if (isFlyingMount && (AnyBlockingWindowOpen() || PrototypeHUD.MenuOpen))
            {
                HoldMenuPosition();
                return;
            }
            if ((keys.slashKey != null && keys.slashKey.wasPressedThisFrame) || (keys.numpadDivideKey != null && keys.numpadDivideKey.wasPressedThisFrame)) Walking = !Walking;
            bool running = !Walking;

            bool rightMouseSteering = orbit && orbit.IsSteering;
            bool bothMiceDown = orbit && orbit.BothMouseButtonsPressed;

            bool qPressed = keys.qKey != null && keys.qKey.isPressed;
            bool forwardInput = keys.wKey.isPressed || keys.upArrowKey.isPressed || bothMiceDown;
            bool backInput = keys.sKey.isPressed || keys.downArrowKey.isPressed;
            bool leftInput = keys.aKey.isPressed || keys.leftArrowKey.isPressed || (rightMouseSteering && qPressed);
            bool rightInput = keys.dKey.isPressed || keys.rightArrowKey.isPressed;

            bool turnLeft = keys.leftArrowKey.isPressed || (Mounted && leftInput);
            bool turnRight = keys.rightArrowKey.isPressed || (Mounted && rightInput);

            // In WoW: Right-mouse turns the character directly to camera heading
            if (rightMouseSteering)
            {
                if (isFlyingMount)
                {
                    // 3D Flight Banking & Pitch Tilt
                    float flightPitch = Mathf.Clamp(-orbit.Pitch * 0.75f, -55f, 55f);
                    transform.rotation = Quaternion.Euler(flightPitch, orbit.Yaw, 0f);
                }
                else
                {
                    var camForward = orbit.transform.forward; camForward.y = 0;
                    if (camForward.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(camForward);
                }
            }
            else
            {
                if (turnLeft && !turnRight) transform.Rotate(0, -220f * Time.deltaTime, 0);
                else if (turnRight && !turnLeft) transform.Rotate(0, 220f * Time.deltaTime, 0);
                if (isFlyingMount && transform.eulerAngles.x != 0)
                {
                    // Level out pitch smoothly when not right-mouse steering
                    var euler = transform.eulerAngles;
                    euler.x = Mathf.LerpAngle(euler.x, 0f, 8f * Time.deltaTime);
                    transform.eulerAngles = euler;
                }
            }

            Vector3 moveDirection = Vector3.zero;
            if (isFlyingMount && rightMouseSteering)
            {
                // Full 3D Flight Vector in direction of camera gaze
                if (forwardInput) moveDirection += orbit.transform.forward;
                if (backInput) moveDirection -= orbit.transform.forward;
                if (leftInput) moveDirection -= orbit.transform.right;
                if (rightInput) moveDirection += orbit.transform.right;
            }
            else
            {
                Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                if (!Mounted && orbit && !rightMouseSteering && !turnLeft && !turnRight)
                {
                    forward = Quaternion.Euler(0f, orbit.Yaw, 0f) * Vector3.forward;
                    right = Vector3.Cross(Vector3.up, forward);
                }
                if (forwardInput) moveDirection += forward;
                if (backInput) moveDirection -= forward;
                if ((!Mounted && keys.aKey.isPressed) || (rightMouseSteering && leftInput)) moveDirection -= right;
                if ((!Mounted && keys.dKey.isPressed) || (rightMouseSteering && rightInput)) moveDirection += right;
            }

            if (moveDirection.sqrMagnitude > 1f) moveDirection.Normalize();

            bool isBackpedaling = backInput && !forwardInput;
            bool diagonalBackpedal = isBackpedaling && (keys.aKey.isPressed || keys.dKey.isPressed ||
                (rightMouseSteering && (leftInput || rightInput)));
            float speed = Mounted && SelectedMount ? (running ? SelectedMount.runSpeed : SelectedMount.walkSpeed) : running ? runSpeed : walkSpeed;
            if (CharacterRace == "Human") speed *= 1.10f;
            if (Time.time < sprintUntil) speed *= 1.65f;
            speed *= WorldShrineSystem.GetSpeedMultiplier();
            if (!Mounted && HasMilestone("fleetfoot")) speed *= 1.08f;
            UpdateMotionTrail(Time.time < sprintUntil || Time.time < dodgeEnds || Mounted);
            if (isBackpedaling && !Mounted) speed *= (diagonalBackpedal ? 0.78f : backwardMultiplier);

            if (isFlyingMount)
            {
                jumpInput.Reset();
                if (running) speed = SelectedMount.flySpeed;
                if (keys.leftShiftKey.isPressed) speed *= 1.5f;

                float verticalFly = 0f;
                if (keys.spaceKey.isPressed) verticalFly = 7f;
                else if (keys.xKey.isPressed) verticalFly = -7f;
                else if (!motor.isGrounded) verticalFly = 0f;
                else verticalFly = -2f;

                vertical = verticalFly;
                jumpInput.Reset();
            }
            else
            {
                bool grounded = motor.isGrounded && vertical <= 0;
                if (grounded) vertical = -2f;
                if (jumpInput.TryJump(Time.time, grounded, keys.spaceKey.wasPressedThisFrame, !Blocking))
                    vertical = 6.4f;
                vertical += Physics.gravity.y * 2.1f * Time.deltaTime;
            }

            bool actionInProgress = Time.time < busyUntil;
            bool dodging = Time.time < dodgeEnds;
            // Combat recovery gates the next ability, while guard still holds the player in place.
            var desiredVelocity = Blocking ? Vector3.zero : moveDirection * speed;
            if (!Mounted && !actionInProgress && !Blocking && !dodging && !rightMouseSteering && moveDirection.sqrMagnitude > .01f)
            {
                var facing = isBackpedaling ? -moveDirection : moveDirection;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(facing), 680f * Time.deltaTime);
            }

            if (dodging) moveVelocity = dodgeDir * 15.5f;
            else if (Mounted || Blocking) moveVelocity = desiredVelocity;
            else
            {
                if (Time.time - Time.deltaTime < dodgeEnds) moveVelocity = Vector3.zero;
                float acceleration = desiredVelocity.sqrMagnitude > .001f ? 62f : 88f;
                moveVelocity = Vector3.MoveTowards(moveVelocity, desiredVelocity, acceleration * Time.deltaTime);
            }
            if (Mounted && !MountPathClear(moveVelocity)) moveVelocity = Vector3.zero;
            if (motor && motor.enabled)
            {
                var collision = motor.Move((moveVelocity + Vector3.up * vertical) * Time.deltaTime);
                if ((collision & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
            }
            EnsureAboveTerrain(isFlyingMount);
            if (transform.position.y < -10 && !WorldRegions.Underground(transform.position)) Respawn();

            var planarVelocity = motor.velocity; planarVelocity.y = 0;
            float actualSpeed = planarVelocity.magnitude;
            bool turningInPlace = !rightMouseSteering && (turnLeft ^ turnRight) && actualSpeed <= 0.2f;
            bool holdCombatPose = Blocking || (actionInProgress && desiredVelocity.sqrMagnitude < .04f &&
                actualSpeed <= .2f && motor.isGrounded);

            if (animator && !hasVisualBaseRotation)
            {
                visualBaseRotation = animator.transform.localRotation;
                hasVisualBaseRotation = true;
            }

            // Visual strafe/diagonal alignment: turn the child mesh smoothly into strafe/backpedal motion while keeping actor transform.forward authoritative for camera/combat
            float desiredVisualYaw = 0f;
            if (animator && !Mounted && !holdCombatPose && motor.isGrounded && actualSpeed > 0.2f)
            {
                float signedTravelAngle = Vector3.SignedAngle(transform.forward, planarVelocity.normalized, Vector3.up);
                if (Mathf.Abs(signedTravelAngle) <= 105f) desiredVisualYaw = Mathf.Clamp(signedTravelAngle, -52f, 52f);
                else desiredVisualYaw = Mathf.Clamp(Vector3.SignedAngle(-transform.forward, planarVelocity.normalized, Vector3.up), -32f, 32f);
            }
            if (animator)
            {
                float lerpRate = holdCombatPose ? 28f : 14f;
                visualYawOffset = Mathf.Lerp(visualYawOffset, desiredVisualYaw, 1f - Mathf.Exp(-lerpRate * Time.deltaTime));
                if (hasVisualBaseRotation)
                    animator.transform.localRotation = visualBaseRotation * Quaternion.Euler(0f, visualYawOffset, 0f);
            }

            if (Mounted)
            {
                Play("Ride"); if (animator) animator.speed = 1;
                mountInstance.Animate(actualSpeed, !motor.isGrounded, Walking);
            }
            else if (!holdCombatPose && !dodging)
            {
                bool airborne = !motor.isGrounded;
                bool movingBack = isBackpedaling || Vector3.Dot(planarVelocity, transform.forward) < -.15f;
                bool animationRun = running;
                string movementState = animationRun ? (movingBack ? "RunBack" : "Run") : (movingBack ? "WalkBack" : "Walk");
                if (airborne)
                    Play("Jump");
                else if (actualSpeed > .2f)
                    Play(movementState);
                else if (turningInPlace)
                    Play("Walk");
                else
                    Play("Idle");

                if (animator)
                {
                    float stride = animationRun ? (movingBack ? backRunStrideSpeed : runStrideSpeed) : (movingBack ? backWalkStrideSpeed : walkStrideSpeed);
                    float playback = !airborne && actualSpeed > .2f ? Mathf.Clamp(actualSpeed / Mathf.Max(.1f, stride), .65f, 2.25f) : (turningInPlace && !airborne ? 0.75f : 1f);
                    animator.speed = playback;
                }
            }
            TickCombatInputBuffer();

            if (keys.digit1Key.wasPressedThisFrame) RequestPrimaryAttackInput();
            if (keys.digit2Key.wasPressedThisFrame) RequestCombatAbility(2);
            if (keys.digit3Key.wasPressedThisFrame) RequestCombatAbility(3);
            if (keys.digit5Key.wasPressedThisFrame) CastMilestoneSlot(0);
            if (keys.digit6Key.wasPressedThisFrame) CastMilestoneSlot(1);
            if (keys.fKey.wasPressedThisFrame && !Blocking && !InventoryOpen) CastInterruptPublic();
            if (keys.leftAltKey.wasPressedThisFrame && !Mounted && !InventoryOpen) PerformDodgeRoll(moveDirection);
            if (keys.eKey.wasPressedThisFrame) Interact();
        }

        public void RequestPrimaryAttackInput() => RequestCombatAbility(1);

        bool CanUseCombatInput() => Stats && Stats.Alive && motor && motor.enabled && !Mounted &&
            !Blocking && !PrototypeHUD.MenuOpen && !FrontendMenu.IsOpen && !(orbit && orbit.Paused) &&
            !AnyBlockingWindowOpen();

        bool HasAbilityFooting(int slot) => motor.isGrounded || slot == 3 || CharacterClass == "Mage" ||
            (slot == 2 && (CharacterClass == "Paladin" || CharacterClass == "Rogue"));

        float CombatAbilityReadyAt(int slot)
        {
            float cooldown = slot == 3 ? HealReady : slot == 2 ? Mathf.Max(attackReady, CleaveReady) : attackReady;
            return Mathf.Max(cooldown, Mathf.Max(busyUntil, dodgeEnds));
        }

        void RequestCombatAbility(int slot)
        {
            if (!CanUseCombatInput() || !HasAbilityFooting(slot))
            {
                ClearCombatInputBuffer();
                return;
            }
            float wait = CombatAbilityReadyAt(slot) - Time.time;
            if (wait <= 0f)
            {
                ClearCombatInputBuffer();
                UseCombatAbility(slot);
            }
            else if (wait <= .35f)
            {
                bufferedAbilitySlot = slot;
                bufferedAbilityExpiresAt = CombatAbilityReadyAt(slot) + .16f;
            }
            else if (slot != 1 && (slot == 2 ? CleaveReady : HealReady) > Time.time)
                Message = $"Ability recharging ({Mathf.CeilToInt(wait)}s).";
        }

        void TickCombatInputBuffer()
        {
            int slot = bufferedAbilitySlot;
            if (slot == 0) return;
            if (!CanUseCombatInput() || Time.time >= bufferedAbilityExpiresAt ||
                (slot == 1 && (!Target || !Target.Alive)))
            {
                ClearCombatInputBuffer();
                return;
            }
            if (Time.time < CombatAbilityReadyAt(slot) || !HasAbilityFooting(slot)) return;
            ClearCombatInputBuffer();
            UseCombatAbility(slot);
        }

        void UseCombatAbility(int slot)
        {
            if (slot == 1) CastAbility1();
            else if (slot == 2) CastAbility2();
            else CastAbility3();
        }

        void PerformDodgeRoll(Vector3 inputDir)
        {
            if (Stats && !Stats.Alive) return;
            if (Time.time < dodgeReadyTime || !motor || !motor.isGrounded || Blocking || AnyBlockingWindowOpen()) return;
            ClearCombatInputBuffer();
            bool wasMidAction = Time.time < busyUntil;
            combatActionEpoch++;
            busyUntil = Mathf.Min(busyUntil, Time.time);
            dodgeReadyTime = Time.time + 3.5f;
            dodgeEnds = Time.time + 0.32f;
            dodgeDir = inputDir.sqrMagnitude > 0.05f ? inputDir.normalized : transform.forward;
            if (animator && animator.HasState(0, Animator.StringToHash("Dodge"))) Play("Dodge", true);
            if (Stats) Stats.invulnerableUntil = Time.time + 0.38f;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                if (wasMidAction) Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cancel", 0f);
                Network.RiftboundCoopPlayer.LocalOwner.SyncDefenseState(Blocking, true);
                Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Dodge", 0.32f);
            }
            SpellFX.CreateArcaneBurst(transform.position);
            CombatTextManager.Spawn(transform.position + Vector3.up * 1.8f, "DODGE!", new Color(0.55f, 0.95f, 1f), false);
        }
        float interruptReadyTime;
        public float InterruptCooldownRemaining => Mathf.Max(0f, interruptReadyTime - Time.time);
        public string InterruptSpellName => CharacterClass == "Warrior" ? "Pummel" : CharacterClass == "Rogue" ? "Kick" : CharacterClass == "Mage" ? "Counterspell" : "Rebuke";
        public string InterruptIcon => CharacterClass == "Warrior" ? "🛡" : CharacterClass == "Rogue" ? "🦶" : CharacterClass == "Mage" ? "✦" : "🔨";

        public void CastInterruptPublic()
        {
            if (Stats && !Stats.Alive) return;
            if (InterruptCooldownRemaining > 0f)
            {
                Message = $"{InterruptSpellName} is on cooldown ({Mathf.CeilToInt(InterruptCooldownRemaining)}s).";
                return;
            }

            if (!Target || !Target.Alive)
            {
                Message = "No target selected [Tab].";
                return;
            }

            float maxRange = CharacterClass == "Mage" ? 25f : 4.5f;
            if (Vector3.Distance(transform.position, Target.transform.position) > maxRange)
            {
                Message = $"Target out of range for {InterruptSpellName} ({maxRange}m).";
                return;
            }

            if (CastBarSystem.IsTargetCasting(Target))
            {
                bool success = CastBarSystem.InterruptTarget(Target, InterruptSpellName);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(Target, "interrupt");
                if (success)
                {
                    interruptReadyTime = Time.time + 12f;
                    if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                        Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Attack", 0.35f);
                    CombatTextManager.Spawn(Target.transform.position + Vector3.up * 1.5f, $"★ {InterruptSpellName.ToUpper()}! ★", Color.red, true);
                    SpellFX.CreateExpandingRing(Target.transform.position, 0.3f, 2.2f, 0.25f, new Color(0.4f, 0.85f, 1f, 0.9f), 0.14f);
                    OrbitCamera.Shake(0.12f, 0.12f);
                    AudioEffects.PlayBladeHit();
                    ChatSystem.LogCombat($"<color=#ff4040>Your [{InterruptSpellName}] INTERRUPTED {Target.displayName}!</color>");
                    Message = $"Interrupted {Target.displayName}!";
                }
                else
                {
                    Message = $"{Target.displayName}'s cast cannot be interrupted! [Shielded]";
                }
            }
            else
            {
                Message = $"{Target.displayName} is not casting.";
            }
        }
        public void CastAbility1Public() => RequestCombatAbility(1);
        public void CastAbility2Public() => RequestCombatAbility(2);
        public void CastAbility3Public() => RequestCombatAbility(3);
        public void UsePotionPublic() { UsePotion(); }
        public void ToggleMapPublic()
        {
            if (Stats && Stats.Alive && !DialogueOpen && !ShopOpen && !ChatOpen)
            {
                MapOpen = !MapOpen;
                InventoryOpen = false;
                JournalOpen = false;
                TalentsOpen = false;
            }
        }
        float dialogueOpenedAt;
        public bool CanTalk() => Stats && Stats.Alive && questGiver && questGiver.gameObject.activeInHierarchy && Vector3.Distance(transform.position, questGiver.position) < 4;
        public void CloseDialogue() { DialogueOpen = false; ActiveQuestGiver = null; ActiveTownNPC = null; PrototypeHUD.ResetGossipState(); }
        public void OpenTownNPCDialogue(TownNPC npc)
        {
            if (!npc) return;
            if (Stats && !Stats.Alive) return;
            (GetComponent<FactionQuestSystem>() ?? FactionQuestSystem.Instance)?.OnTalkedToNPC(npc.npcName);
            PrototypeHUD.ResetGossipState();
            ActiveTownNPC = npc;
            ActiveQuestGiver = null;
            DialogueOpen = true;
            dialogueOpenedAt = Time.time;
            moveVelocity = Vector3.zero;
        }
        public void AcceptQuest()
        {
            if (!DialogueOpen || !CanTalk() || QuestAccepted || !ActiveQuestGiver) return;
            if (QuestComplete) return;
            QuestAccepted = true; Message = QuestObjective;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                Network.RiftboundCoopPlayer.LocalOwner.RequestAcceptQuest();
            AudioEffects.PlayQuestAccept();
            ChatSystem.LogQuest($"Quest Accepted: {QuestName}");
            CloseDialogue();
            RefreshAdventureWorld(); SaveAdventure(false);
        }
        public void CompleteQuest()
        {
            if (!DialogueOpen || !CanTalk() || !QuestReady || QuestComplete || !ActiveQuestGiver) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                if (!Network.RiftboundCoopPlayer.LocalOwner.RequestCompleteQuestServerValidated(QuestIndex)) return;
            }
            ApplyAuthorizedCompleteQuest(QuestIndex);
        }

        public void ApplyAuthorizedCompleteQuest(int expectedIndex)
        {
            if (QuestComplete || QuestIndex != expectedIndex) return;
            int goldGain = QuestGold;
            int questExperience = QuestExperience;
            string completedQuest = QuestName;
            if (CharacterRace == "Human") goldGain = Mathf.RoundToInt(goldGain * 1.25f);
            Gold += goldGain; AddExperience(questExperience); Potions = Mathf.Min(10, Potions + 1);
            QuestIndex++; QuestAccepted = false; QuestComplete = QuestIndex >= 3;
            if (QuestComplete) { WeaponUpgraded = true; GrantItem("emberwatch-axe", true); GrantItem("emberwatch-sigil", true); }
            Stats.Heal(Stats.maxHealth);
            AudioEffects.PlayQuestComplete();
            ChatSystem.LogQuest($"Quest Completed: {completedQuest}! Rewards: {goldGain} Gold, {questExperience} XP.");
            Message = QuestComplete ? "Emberwatch is safe! Rewards: Emberwatch Axe and Sigil. [I]" : "Quest complete! Speak to Abe for your next mission.";
            CloseDialogue();
            RefreshAdventureWorld(); SaveAdventure(false);
        }

        void UpdateClassResource()
        {
            if (CharacterClass == "Warrior")
            {
                if (Time.time - Mathf.Max(Stats.LastHit, attackReady) > 4f)
                    ClassResource = Mathf.Max(0, ClassResource - 4f * Time.deltaTime);
            }
            else if (CharacterClass == "Mage")
            {
                ClassResource = Mathf.Min(MaxClassResource, ClassResource + 8f * Time.deltaTime);
            }
            else if (CharacterClass == "Rogue")
            {
                float regenRate = 20f + TalentSystem.GetEnergyRegenBonus(this);
                ClassResource = Mathf.Min(MaxClassResource, ClassResource + regenRate * Time.deltaTime);
            }
        }

        void CastAbility1()
        {
            if (Blocking || InventoryOpen || AnyBlockingWindowOpen() || Time.time < attackReady) return;
            if (CharacterClass == "Mage")
            {
                if (ClassResource < 15) { Message = "Not enough Mana!"; return; }
                if (!Target || !Target.Alive) SelectTarget();
                if (!Target || !Target.Alive || Vector3.Distance(transform.position, Target.transform.position) > 28f)
                {
                    Message = "Target is out of range (28m). TAB selects the nearest target.";
                    return;
                }
                if (!CanSeeCombatTarget(Target)) { Message = "Something blocks your line of sight."; return; }
                ClassResource -= 15;
                var dir = Target.transform.position - transform.position; dir.y = 0;
                if (dir.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(dir);
                visualYawOffset = 0f;
                if (animator && hasVisualBaseRotation) animator.transform.localRotation = visualBaseRotation;
                attackReady = Time.time + 1.25f;
                busyUntil = Time.time + 0.6f;
                Play("Attack", true);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cast", 0.60f);
                AudioEffects.PlaySpellCast();
                float dmg = (36 + (Level - 1) * 4 + WeaponBonus) * MilestoneDamageMultiplier;
                if (CharacterRace == "Brute") dmg *= 1.15f;
                FireballSpell.Cast(this, Target, dmg);
                Message = "Fireball launched!";
            }
            else
            {
                if (!Target || !Target.Alive) SelectTarget();
                if (!Target || !Target.Alive || Vector3.Distance(transform.position, Target.transform.position) > 3.2f)
                {
                    Message = "Move closer to an enemy. TAB selects the nearest target.";
                    return;
                }
                if (!CanSeeCombatTarget(Target)) { Message = "Something blocks your line of sight."; return; }
                if (CharacterClass == "Rogue")
                {
                    if (ClassResource < 40) { Message = "Not enough Energy!"; return; }
                    ClassResource -= 40;
                    StartCoroutine(Attack(false, 0.55f, 20f, "Sinister Strike!"));
                }
                else if (CharacterClass == "Paladin")
                {
                    ClassResource = Mathf.Min(MaxClassResource, ClassResource + 1);
                    StartCoroutine(Attack(false, 1.1f, 26f, "Crusader Strike! (+1 Holy Power)"));
                }
                else // Warrior
                {
                    ClassResource = Mathf.Min(MaxClassResource, ClassResource + 15);
                    StartCoroutine(Attack(false, attackDuration, 24f, "Strike! (+15 Rage)"));
                }
            }
        }

        void CastAbility2()
        {
            if (Blocking || InventoryOpen || AnyBlockingWindowOpen() || Time.time < attackReady) return;
            if (Time.time < CleaveReady)
            {
                Message = $"Ability recharging ({Mathf.CeilToInt(CleaveReady - Time.time)}s).";
                return;
            }
            if (CharacterClass == "Mage")
            {
                if (ClassResource < 25) { Message = "Not enough Mana!"; return; }
                ClassResource -= 25;
                CleaveReady = Time.time + 6f;
                busyUntil = Time.time + 0.5f;
                visualYawOffset = 0f;
                if (animator && hasVisualBaseRotation) animator.transform.localRotation = visualBaseRotation;
                Play("Cleave", true);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cast", 0.50f);
                AudioEffects.PlaySpellCast();
                SpellFX.CreateFrostNova(transform.position, 6.5f);
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (Vector3.Distance(transform.position, enemy.transform.position) < 6.5f)
                    {
                        var eCombatant = enemy.GetComponent<Combatant>();
                        if (!eCombatant || !eCombatant.Alive || !CanSeeCombatTarget(eCombatant)) continue;
                        Hit(eCombatant, 32f, "frostnova");
                        BuffSystem.ApplyTargetDebuff(eCombatant, "frostbite", "Frostbite", "Movement slowed by 60%.", "❄", new Color(0.3f, 0.8f, 1f), 5f, 0f);
                    }
                }
                Message = "Frost Nova: Freezing blast!";
            }
            else if (CharacterClass == "Rogue")
            {
                if (ClassResource < 35) { Message = "Not enough Energy!"; return; }
                if (!Target || !Target.Alive || Vector3.Distance(transform.position, Target.transform.position) > 3.5f)
                {
                    Message = "Eviscerate requires a target in melee range.";
                    return;
                }
                if (!CanSeeCombatTarget(Target)) { Message = "Something blocks your line of sight."; return; }
                ClassResource -= 35;
                CleaveReady = Time.time + 4f;
                attackReady = Time.time + 0.6f;
                busyUntil = Time.time + 0.5f;
                var evisDir = Target.transform.position - transform.position; evisDir.y = 0;
                if (evisDir.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(evisDir);
                visualYawOffset = 0f;
                if (animator && hasVisualBaseRotation) animator.transform.localRotation = visualBaseRotation;
                Play("Cleave", true);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cleave", 0.50f);
                AudioEffects.PlaySpellCast();
                Hit(Target, 52, "eviscerate");
                Message = "Eviscerate! Critical finisher!";
            }
            else if (CharacterClass == "Paladin")
            {
                if (ClassResource < 2) { Message = "Requires 2 Holy Power!"; return; }
                ClassResource -= 2;
                CleaveReady = Time.time + 6f;
                busyUntil = Time.time + 0.5f;
                visualYawOffset = 0f;
                if (animator && hasVisualBaseRotation) animator.transform.localRotation = visualBaseRotation;
                Play("Cleave", true);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim("Cast", 0.50f);
                AudioEffects.PlaySpellCast();
                SpellFX.CreateConsecration(transform.position, 5.5f);
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (Vector3.Distance(transform.position, enemy.transform.position) < 5.5f)
                    {
                        var eCombatant = enemy.GetComponent<Combatant>();
                        if (!eCombatant || !eCombatant.Alive || !CanSeeCombatTarget(eCombatant)) continue;
                        Hit(eCombatant, 40f, "consecration");
                        BuffSystem.ApplyTargetDebuff(eCombatant, "consecration", "Consecrated", "Sacred flame scorches the target.", "☼", new Color(1f, 0.85f, 0.35f), 5f, 0f);
                    }
                }
                Message = "Consecration: Holy ground!";
            }
            else // Warrior
            {
                if (ClassResource < 20) { Message = "Requires 20 Rage for Whirlwind!"; return; }
                ClassResource -= 20;
                StartCoroutine(Attack(true, cleaveDuration, 38f, "Whirlwind!"));
            }
        }

        void CastAbility3()
        {
            if (Blocking || InventoryOpen || AnyBlockingWindowOpen() || (Stats && !Stats.Alive)) return;
            if (Time.time < HealReady)
            {
                Message = $"Ability recharging ({Mathf.CeilToInt(HealReady - Time.time)}s).";
                return;
            }
            if (CharacterClass == "Mage")
            {
                if (ClassResource < 20) { Message = "Not enough Mana!"; return; }
                ClassResource -= 20;
                HealReady = Time.time + 10f;
                motor.Move(transform.forward * 7.5f);
                Stats.Heal(40);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.RequestAbilityHeal(40, "arcane_ward");
                AudioEffects.PlaySpellCast();
                SpellFX.CreateArcaneBurst(transform.position);
                Message = "Blink & Arcane Ward: Teleported & shielded 40 HP!";
            }
            else if (CharacterClass == "Rogue")
            {
                if (ClassResource < 20) { Message = "Not enough Energy!"; return; }
                ClassResource -= 20;
                HealReady = Time.time + 10f;
                sprintUntil = Time.time + 5f;
                attackReady = Time.time;
                AudioEffects.PlaySpellCast();
                Message = "Sprint: +65% speed for 5 seconds!";
            }
            else if (CharacterClass == "Paladin")
            {
                if (Stats && Stats.Health >= Stats.maxHealth) { Message = "Health is already full."; return; }
                if (ClassResource < 3) { Message = "Requires 3 Holy Power!"; return; }
                ClassResource -= 3;
                HealReady = Time.time + 12f;
                Stats.Heal(60);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.RequestAbilityHeal(60, "holy_light");
                AudioEffects.PlaySpellCast();
                SpellFX.CreateConsecration(transform.position, 2f);
                Message = "Holy Light: restored 60 health.";
            }
            else // Warrior
            {
                if (Stats && Stats.Health >= Stats.maxHealth) { Message = "Health is already full."; return; }
                HealReady = Time.time + 12f;
                Stats.Heal(45);
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                    Network.RiftboundCoopPlayer.LocalOwner.RequestAbilityHeal(45, "second_wind");
                AudioEffects.PlaySpellCast();
                Message = "Second Wind: restored 45 health.";
            }
        }

        IEnumerator Attack(bool cleave, float duration = -1f, float damage = 24f, string message = "")
        {
            if (duration < 0) duration = cleave ? cleaveDuration : attackDuration;
            if (!Target || !Target.Alive) SelectTarget();
            var victim = Target;
            if (!cleave && (!Target || !Target.Alive || Vector3.Distance(transform.position, Target.transform.position) > 3.2f))
            { Message = "Move closer to an enemy. TAB selects the nearest target."; yield break; }
            if (Target && Target.Alive)
            {
                var direction = Target.transform.position - transform.position; direction.y = 0;
                if (direction.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(direction);
            }
            visualYawOffset = 0f;
            if (animator && hasVisualBaseRotation) animator.transform.localRotation = visualBaseRotation;
            attackReady = Time.time + duration + .08f;
            if (cleave) CleaveReady = Time.time + Mathf.Max(1.5f, 5f - TalentSystem.GetWhirlwindCooldownReduction(this));
            busyUntil = Time.time + duration * .78f;
            int actionEpoch = combatActionEpoch;
            if (animator) animator.speed = 1;
            string swingAnim = cleave ? "Cleave" : "Attack";
            Play(swingAnim, true);
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                Network.RiftboundCoopPlayer.LocalOwner.TriggerActionAnim(swingAnim, duration * 0.78f);
            float windupEnd = Time.time + duration * .45f;
            while (Time.time < windupEnd)
            {
                if (!Stats || !Stats.Alive || Blocking || actionEpoch != combatActionEpoch || Time.time < dodgeEnds) yield break;
                if (!cleave && victim && victim.Alive && (!orbit || !orbit.IsSteering))
                {
                    Vector3 aim = victim.transform.position - transform.position; aim.y = 0f;
                    if (aim.sqrMagnitude > 0.01f && aim.sqrMagnitude < 16f)
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(aim), 240f * Time.deltaTime);
                }
                yield return null;
            }
            if (!Stats.Alive || Blocking || actionEpoch != combatActionEpoch || Time.time < dodgeEnds) yield break;
            if (cleave)
            {
                SpellFX.CreateCleaveArc(transform.position, 3.8f, CharacterClass);
                OrbitCamera.Shake(0.16f, 0.16f);
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    var ec = enemy.GetComponent<Combatant>();
                    if (ec && ec.Alive && Vector3.Distance(transform.position, enemy.transform.position) < 3.8f) Hit(ec, damage, "whirlwind");
                }
                Message = string.IsNullOrEmpty(message) ? "Whirlwind!" : message;
            }
            else if (victim && victim.Alive && Vector3.Distance(transform.position, victim.transform.position) < 3.5f &&
                Vector3.Dot(transform.forward, (victim.transform.position - transform.position).normalized) > .1f &&
                CanSeeCombatTarget(victim))
            {
                SpellFX.CreateSlashArc(transform.position, transform.forward, CharacterClass);
                Hit(victim, damage, "melee");
                if (!string.IsNullOrEmpty(message)) Message = message;
            }
            else
            {
                Message = !victim || !victim.Alive ? "Target defeated." :
                    !CanSeeCombatTarget(victim) ? "Something blocks your strike." : "Target moved out of melee range.";
            }
        }

        void UpdateMotionTrail(bool active)
        {
            if (!active && !sprintTrail) return;
            if (!sprintTrail)
            {
                var go = new GameObject("MotionTrail");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                sprintTrail = go.AddComponent<TrailRenderer>();
                sprintTrail.time = 0.28f;
                sprintTrail.startWidth = 0.28f;
                sprintTrail.endWidth = 0.02f;
                sprintTrail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
                sprintTrail.startColor = new Color(0.75f, 0.92f, 1f, 0.45f);
                sprintTrail.endColor = new Color(0.75f, 0.92f, 1f, 0f);
            }
            sprintTrail.emitting = active;
        }
        bool CanSeeCombatTarget(Combatant enemy)
        {
            if (!enemy) return false;
            Vector3 origin = transform.position + Vector3.up * Mathf.Clamp(EyeHeight * .78f, .8f, 1.4f);
            Vector3 offset = enemy.transform.position + Vector3.up * 1.1f - origin;
            if (offset.sqrMagnitude < .01f) return true;
            int count = Physics.RaycastNonAlloc(origin, offset.normalized, combatSightHits, offset.magnitude,
                ~(1 << 2), QueryTriggerInteraction.Ignore);
            var hits = count == combatSightHits.Length
                ? Physics.RaycastAll(origin, offset.normalized, offset.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore)
                : combatSightHits;
            if (hits != combatSightHits) count = hits.Length;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i].collider;
                if (!hit || hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(enemy.transform) ||
                    hit.GetComponentInParent<Combatant>()) continue;
                return false;
            }
            return true;
        }

        void Hit(Combatant enemy, float damage, string abilityId = "melee")
        {
            if (!enemy || !enemy.Alive) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(enemy, abilityId);
                return;
            }
            if (!CanSeeCombatTarget(enemy)) return;

            float finalDmg = damage + (Level - 1) * 3 + WeaponBonus;
            if (CharacterRace == "Brute") finalDmg *= 1.15f;
            finalDmg *= TalentSystem.GetDamageMultiplier(this);
            finalDmg *= WorldShrineSystem.GetDamageMultiplier() * MilestoneDamageMultiplier;

            float critChance = (CharacterClass == "Rogue" ? 0.28f : 0.16f) + WorldShrineSystem.GetCritBonus() +
                (HasMilestone("keen-eye") ? .08f : 0f);
            bool isCrit = UnityEngine.Random.value < critChance;
            if (isCrit) finalDmg *= 1.8f;

            float healthBeforeHit = enemy.Health;
            enemy.Damage(finalDmg);
            float damageDealt = healthBeforeHit - enemy.Health;
            if (damageDealt <= 0f) return;
            ApplyConfirmedHitPresentation(enemy, damageDealt, isCrit);
            if (CharacterRace == "Undead" && Stats && Stats.Alive) Stats.Heal(6);
        }

        public void ApplyConfirmedHitPresentation(Combatant enemy, float confirmedDamage, bool isCrit)
        {
            if (!enemy) return;
            if (CharacterClass == "Mage") GameSound.Play(SoundCue.Rune, enemy.transform.position + Vector3.up * 1.1f);
            else AudioEffects.PlayBladeHit();
            SpellFX.CreateHitSparks(enemy.transform.position + Vector3.up * 1.1f, isCrit);
            if (isCrit) OrbitCamera.Shake(0.13f, 0.14f);
            else if (confirmedDamage >= 35f) OrbitCamera.Shake(0.08f, 0.08f);
            CombatTextManager.SpawnDamage(enemy.transform.position, confirmedDamage, isCrit);
            string critMsg = isCrit ? " [CRITICAL!]" : "";
            ChatSystem.LogCombat($"Your strike hits {enemy.displayName} for {Mathf.RoundToInt(confirmedDamage)} damage{critMsg}.");
            if (CharacterClass == "Warrior")
                BuffSystem.ApplyTargetDebuff(enemy, "rend", "Rend", "Suffering 6 Physical bleed damage every second.", "🩸", new Color(0.85f, 0.2f, 0.2f), 5f, 6f, 1f);
            else if (CharacterClass == "Rogue")
                BuffSystem.ApplyTargetDebuff(enemy, "poison", "Deadly Poison", "Inflicted with 5 Nature poison damage every second.", "🧪", new Color(0.3f, 0.85f, 0.3f), 6f, 5f, 1f);
            else if (CharacterClass == "Paladin")
                BuffSystem.ApplyTargetDebuff(enemy, "judgement", "Judgement", "Weakened against Holy power for 6 seconds.", "✝", new Color(1f, 0.85f, 0.3f), 6f, 4f, 1.5f);
            else if (CharacterClass == "Mage")
                BuffSystem.ApplyTargetDebuff(enemy, "ignite", "Ignite", "Burning for 8 Fire damage every second.", "🔥", new Color(1f, 0.45f, 0.1f), 4f, 8f, 1f);
        }
        public void SelectTarget()
        {
            Camera cam = Camera.main;
            Vector3 camFwd = cam ? cam.transform.forward : transform.forward;
            camFwd.y = 0f;
            if (camFwd.sqrMagnitude > 0.001f) camFwd.Normalize();

            var candidates = FindObjectsByType<EnemyController>(FindObjectsSortMode.None)
                .Select(e => e.GetComponent<Combatant>())
                .Where(s => s != null && s.Alive && Vector3.Distance(transform.position, s.transform.position) < 28f && CanSeeCombatTarget(s))
                .ToList();

            if (candidates.Count == 0)
            {
                Target = null;
                return;
            }

            var targets = candidates.OrderBy(s =>
            {
                Vector3 toEnemy = s.transform.position - transform.position;
                float dist = toEnemy.magnitude;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude > 0.001f) toEnemy.Normalize();
                float dot = Vector3.Dot(camFwd, toEnemy);
                int inConeTier = dot > 0.15f ? 0 : 1;
                return inConeTier * 1000f - dot * 10f + dist;
            }).ToArray();

            int index = System.Array.IndexOf(targets, Target);
            Target = targets[(index + 1) % targets.Length];
        }
        public void SetTarget(Combatant newTarget)
        {
            Target = newTarget;
        }
        public static float SampleSafeGroundY(Vector3 worldPos, float clearance = 0.15f)
        {
            if (WorldRegions.Underground(worldPos) || (!WorldRegions.Expanded && (worldPos.x < -200f || worldPos.y >= 40f)))
                return worldPos.y;

            float groundY = worldPos.y;
            bool hasTerrainSample = false;

            var terrain = WorldRegions.TerrainAt(worldPos);
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 tp = terrain.transform.position;
                Vector3 ts = terrain.terrainData.size;
                if (worldPos.x >= tp.x && worldPos.x <= tp.x + ts.x &&
                    worldPos.z >= tp.z && worldPos.z <= tp.z + ts.z)
                {
                    groundY = terrain.SampleHeight(worldPos) + tp.y;
                    hasTerrainSample = true;
                }
            }

            float rayTop = (hasTerrainSample ? Mathf.Max(worldPos.y, groundY) : worldPos.y) + 12f;
            var hits = Physics.RaycastAll(new Vector3(worldPos.x, rayTop, worldPos.z), Vector3.down, 24f, ~0, QueryTriggerInteraction.Ignore);
            float bestDeckY = float.NegativeInfinity;
            foreach (var h in hits)
            {
                if (h.collider is CharacterController || h.collider is CapsuleCollider) continue;
                var tr = h.collider.transform;
                if (tr.GetComponentInParent<PlayerController>() ||
                    tr.GetComponentInParent<EnemyController>() ||
                    tr.GetComponentInParent<TownNPC>() ||
                    tr.GetComponentInParent<FlightMaster>() ||
                    tr.GetComponentInParent<QuestGiver>() ||
                    tr.GetComponentInParent<Merchant>() ||
                    tr.GetComponentInParent<Wildlife>() ||
                    tr.GetComponentInParent<GatheringNode>())
                    continue;

                if (hasTerrainSample && h.point.y > groundY + 1.8f) continue;
                if (h.point.y > bestDeckY) bestDeckY = h.point.y;
            }

            if (!float.IsNegativeInfinity(bestDeckY))
                groundY = hasTerrainSample ? Mathf.Max(groundY, bestDeckY) : bestDeckY;

            return groundY + clearance;
        }

        public static Vector3 ClampAboveGround(Vector3 worldPos, float clearance = 0.15f)
        {
            if (WorldRegions.Underground(worldPos) || (!WorldRegions.Expanded && (worldPos.x < -200f || worldPos.y >= 40f)))
                return worldPos;

            float minSafeY = SampleSafeGroundY(worldPos, clearance);
            if (worldPos.y < minSafeY || worldPos.y > minSafeY + 2.5f)
                worldPos.y = minSafeY;
            return worldPos;
        }

        void EnsureAboveTerrain(bool isFlyingMount)
        {
            if (isFlyingMount || WorldRegions.Underground(transform.position) || (!WorldRegions.Expanded && (transform.position.x < -200f || transform.position.y >= 40f))) return;
            var terrain = WorldRegions.TerrainAt(transform.position);
            if (!terrain || !terrain.terrainData) return;

            Vector3 p = transform.position;
            Vector3 tp = terrain.transform.position;
            Vector3 ts = terrain.terrainData.size;
            if (p.x < tp.x || p.x > tp.x + ts.x || p.z < tp.z || p.z > tp.z + ts.z) return;

            float ty = terrain.SampleHeight(p) + tp.y;
            if (p.y < ty - 0.70f)
            {
                float safeY = SampleSafeGroundY(p, 0.25f);
                bool wasEnabled = motor && motor.enabled;
                if (wasEnabled) motor.enabled = false;
                transform.position = new Vector3(p.x, safeY, p.z);
                if (wasEnabled) motor.enabled = true;
                if (vertical < 0f) vertical = 0f;
            }
        }

        void Respawn()
        {
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestRespawn();
                return;
            }
            ApplyAuthorizedRespawn(ClampAboveGround(spawn, 0.25f));
        }

        public void ApplyAuthorizedRespawn(Vector3 safeSpawn, int serverHp = -1)
        {
            Dismount();
            CloseDialogue();
            CloseShop(); MapOpen = false; JournalOpen = false; EncounterJournalOpen = false; InventoryOpen = false; TalentsOpen = false; SpellbookOpen = false; AchievementOpen = false; ReputationOpen = false; ChatOpen = false; ProfessionSystem.IsOpen = false; AuctionHouseSystem.AuctionOpen = false; AuctionHouseSystem.MailboxOpen = false; if (GuildSystem.Instance) GuildSystem.Instance.IsOpen = false; if (CharacterSheetSystem.Instance) CharacterSheetSystem.Instance.IsOpen = false;
            StopGuard(); ReturningHome = false;
            StopAllCoroutines(); motor.enabled = false; transform.position = safeSpawn; motor.enabled = true;
            vertical = 0; moveVelocity = Vector3.zero;
            jumpInput.Reset(); ClearCombatInputBuffer();
            visualYawOffset = 0f;
            if (animator && hasVisualBaseRotation) animator.transform.localRotation = visualBaseRotation;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            if (serverHp >= 0) Stats.SetReplicatedHealth(serverHp, Stats.maxHealth);
            else Stats.Restore();
            Target = null; busyUntil = 0;
            if (animator) animator.speed = 1;
            Play("Idle", true);
            Message = "Recovered at the outpost. Your mission progress is kept for this session.";
        }
        public void Play(string state, bool force = false)
        {
            if (!animator || (!force && currentAnimation == state)) return;
            if (!force && Locomotion(currentAnimation) && Locomotion(state))
                animator.CrossFadeInFixedTime(state, .16f, 0, 0);
            else 
                animator.CrossFadeInFixedTime(state, .14f, 0, 0);
            currentAnimation = state;
        }
        static bool Locomotion(string state) => state == "Walk" || state == "WalkBack" || state == "Run" || state == "RunBack";
        float GaitPhase(string state) => state == "Walk" ? walkPhase : state == "Run" ? runPhase : state == "WalkBack" ? backWalkPhase : backRunPhase;

        void HoldMenuPosition()
        {
            moveVelocity = Vector3.zero;
            if (RidingFlyingMount && Stats && Stats.Alive)
            {
                vertical = 0f;
                Play("Ride");
                mountInstance.Animate(0f, IsFlying, false);
                return;
            }
            GravityOnly();
        }

        void GravityOnly()
        {
            jumpInput.Reset(); ClearCombatInputBuffer();
            if (!motor || !motor.enabled) return;
            if (motor.isGrounded && vertical < 0) vertical = -2;
            vertical += Physics.gravity.y * 1.8f * Time.deltaTime;
            motor.Move(Vector3.up * vertical * Time.deltaTime);
            EnsureAboveTerrain(false);
        }
        void OnDamaged(float amount)
        {
            Dismount();
            CloseDialogue(); CloseShop(); MapOpen = false; JournalOpen = false; ProfessionSystem.IsOpen = false; AuctionHouseSystem.AuctionOpen = false;
            if (Blocking)
                CombatTextManager.SpawnBlock(transform.position);
            else
                CombatTextManager.Spawn(transform.position, $"-{Mathf.RoundToInt(amount)}", new Color(1.0f, 0.25f, 0.25f), false);
            ChatSystem.LogCombat($"You take {Mathf.RoundToInt(amount)} damage.");
            if (!Stats.Alive)
            {
                StopGuard(); ReturningHome = false;
                StopAllCoroutines(); moveVelocity = Vector3.zero; if (animator) animator.speed = 1; Play("Death", true);
                ChatSystem.LogSystem("You have died! Press [R] to respawn at Emberwatch Outpost.");
            }
            else if (!Blocking && motor.isGrounded)
            {
                if (Time.time >= busyUntil)
                {
                    if (animator) animator.speed = 1;
                    Play("Hit", true);
                    attackReady = Mathf.Max(attackReady, Time.time + .22f);
                }
            }
        }
    }
}
