using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class PetCompanionSystem : MonoBehaviour
    {
        public static PetCompanionSystem Instance { get; private set; }

        public string petName = "Frostfang";
        public float maxHealth = 120f;
        public float currentHealth = 120f;
        public float attackDamage = 16f;
        public float attackRange = 2.4f;
        public float attackCooldown = 1.6f;

        public enum PetState { Follow, Attack, Passive, Dead }
        public PetState CurrentState { get; private set; } = PetState.Follow;

        public GameObject petInstance;
        Animator petAnimator;
        string currentAnimState = "";
        string activePetResource = "Pets/Pet_Pug";
        Combatant currentTarget;
        PlayerController player;

        float nextAttackTime;
        float speed = 7.5f;

        GUIStyle frameTitleStyle, frameSubStyle, btnStyle, hpStyle;
        Texture2D frameBgTex, borderTex, hpFillTex;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            player = GetComponent<PlayerController>() ?? FindFirstObjectByType<PlayerController>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (petInstance)
            {
                if (Application.isPlaying) Destroy(petInstance);
                else DestroyImmediate(petInstance);
            }
        }

        public static string ResolvePetResourceKey(string petId)
        {
            switch (petId)
            {
                case "pet_whelp":
                case "pet_raptor":
                    return "Pets/Pet_Steed";
                case "pet_squirrel":
                    return "Pets/Pet_Pig";
                case "pet_phoenix":
                    return "Pets/Pet_Sheep";
                case "pet_wolf":
                default:
                    return "Pets/Pet_Pug";
            }
        }

        public GameObject SetCompanionFromBattlePet(string petId, string displayName)
        {
            if (!string.IsNullOrEmpty(displayName)) petName = displayName;
            string desiredResource = ResolvePetResourceKey(petId);
            if (petInstance != null && activePetResource == desiredResource)
            {
                petInstance.SetActive(true);
                return petInstance;
            }

            if (petInstance != null)
            {
                if (Application.isPlaying) Destroy(petInstance);
                else DestroyImmediate(petInstance);
                petInstance = null;
                petAnimator = null;
                currentAnimState = "";
            }

            activePetResource = desiredResource;
            SpawnPetCompanion();
            return petInstance;
        }

        public void DismissActiveCompanion()
        {
            if (petInstance != null)
            {
                if (Application.isPlaying) Destroy(petInstance);
                else DestroyImmediate(petInstance);
                petInstance = null;
                petAnimator = null;
                currentAnimState = "";
            }
        }

        public void SpawnPetCompanion()
        {
            if (petInstance != null) return;
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player) return;

            var petPrefab = Resources.Load<GameObject>(activePetResource) ?? Resources.Load<GameObject>("Pets/Pet_Pug");
            if (petPrefab != null)
            {
                petInstance = Instantiate(petPrefab);
                petInstance.name = $"Battle Companion {petName}";
                petInstance.transform.localScale = Vector3.one;
                petAnimator = petInstance.GetComponentInChildren<Animator>();
            }
            else
            {
                // Normalized compact companion fallback (~0.46m tall) if prefab is not yet built
                GameObject rawFbx = null;
#if UNITY_EDITOR
                rawFbx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/RPG/Art/Animals/Pug.fbx");
#endif
                if (rawFbx != null)
                {
                    petInstance = new GameObject($"Battle Companion {petName}");
                    var model = Instantiate(rawFbx, petInstance.transform);
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                    model.transform.localScale = Vector3.one * 0.142f; // Pug.fbx native height is 3.26m -> 0.46m
                    model.transform.localPosition = Vector3.zero;
                    petAnimator = model.GetComponentInChildren<Animator>();
#if UNITY_EDITOR
                    var ctrl = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/RPG/Animations/Animal Pug.controller");
                    if (petAnimator && ctrl) petAnimator.runtimeAnimatorController = ctrl;
#endif
                }
                else
                {
                    petInstance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    petInstance.name = $"Battle Companion {petName}";
                    petInstance.transform.localScale = new Vector3(0.32f, 0.24f, 0.45f);
                }
            }

            // Remove unwanted colliders so pet doesn't block player or camera raycasts
            foreach (var col in petInstance.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            foreach (var t in petInstance.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = 2; // Ignore Raycast

            Vector3 spawnPos = player.transform.position - player.transform.right * 1.65f - player.transform.forward * 0.6f;
            petInstance.transform.position = SnapToGround(spawnPos);
            petInstance.transform.rotation = player.transform.rotation;
            currentAnimState = "";
            SetAnimState("Idle");
            currentHealth = maxHealth;
            CurrentState = PetState.Follow;
        }

        static Vector3 SnapToGround(Vector3 pos)
        {
            if (Physics.Raycast(pos + Vector3.up * 2.5f, Vector3.down, out var hit, 8f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;
            return pos;
        }

        void Update()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            if (!player || !petInstance) return;

            // Handle hotkeys: Ctrl+1 or T for pet attack/toggle (P is Spellbook)
            var keys = Keyboard.current;
            if (keys != null && !FrontendMenu.IsOpen && !player.DialogueOpen && !player.ChatOpen)
            {
                if ((keys.ctrlKey.isPressed && keys.digit1Key.wasPressedThisFrame) || keys.tKey.wasPressedThisFrame)
                {
                    if (player.Target && player.Target.Alive)
                    {
                        CommandAttack(player.Target);
                    }
                    else
                    {
                        CommandFollow();
                    }
                }
            }

            // AI State Logic
            if (CurrentState == PetState.Follow)
            {
                Vector3 targetFollowPos = SnapToGround(player.transform.position - player.transform.right * 1.65f - player.transform.forward * 0.65f);
                Vector3 flatDelta = targetFollowPos - petInstance.transform.position;
                flatDelta.y = 0f;
                float dist = flatDelta.magnitude;

                if (dist > 18f)
                {
                    // Catch up / teleport if too far
                    petInstance.transform.position = targetFollowPos;
                }
                else if (dist > 0.65f)
                {
                    Vector3 dir = flatDelta.normalized;
                    float moveSpeed = (dist > 5f) ? speed * 1.5f : (dist < 1.8f ? speed * 0.55f : speed);
                    Vector3 nextPos = petInstance.transform.position + dir * moveSpeed * Time.deltaTime;
                    petInstance.transform.position = SnapToGround(nextPos);
                    if (dir.sqrMagnitude > 0.01f)
                        petInstance.transform.rotation = Quaternion.Slerp(petInstance.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 9f);

                    SetAnimState(dist < 1.8f ? "Walk" : "Run");
                }
                else
                {
                    petInstance.transform.position = SnapToGround(petInstance.transform.position);
                    SetAnimState("Idle");
                }
            }
            else if (CurrentState == PetState.Attack)
            {
                if (!currentTarget || !currentTarget.Alive)
                {
                    CurrentState = PetState.Follow;
                    return;
                }

                Vector3 targetPos = currentTarget.transform.position;
                Vector3 flatDelta = targetPos - petInstance.transform.position;
                flatDelta.y = 0f;
                float dist = flatDelta.magnitude;

                if (dist > attackRange)
                {
                    Vector3 dir = flatDelta.normalized;
                    Vector3 nextPos = petInstance.transform.position + dir * speed * 1.3f * Time.deltaTime;
                    petInstance.transform.position = SnapToGround(nextPos);
                    if (dir.sqrMagnitude > 0.01f)
                        petInstance.transform.rotation = Quaternion.Slerp(petInstance.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);

                    SetAnimState("Run");
                }
                else
                {
                    // Face target & attack
                    Vector3 dir = flatDelta.normalized;
                    if (dir.sqrMagnitude > 0.01f)
                        petInstance.transform.rotation = Quaternion.LookRotation(dir);
                    petInstance.transform.position = SnapToGround(petInstance.transform.position);

                    SetAnimState("Idle");

                    if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + attackCooldown;
                        DoPetBite();
                    }
                }
            }
        }

        void DoPetBite()
        {
            if (!currentTarget || !currentTarget.Alive) return;

            float dmg = attackDamage + UnityEngine.Random.Range(-2f, 3f);
            currentTarget.Damage(dmg);

            CombatTextManager.Spawn(currentTarget.transform.position + Vector3.up * 1.2f, $"{Mathf.RoundToInt(dmg)} (Pet)", new Color(0.35f, 0.95f, 0.45f), false);
            AudioEffects.PlayBladeHit();
            SetAnimState("Jump", forceRestart: true);
        }

        void SetAnimState(string state, bool forceRestart = false)
        {
            if (!petAnimator) return;
            if (!forceRestart && currentAnimState == state) return;
            if (petAnimator.HasState(0, Animator.StringToHash(state)))
            {
                petAnimator.CrossFadeInFixedTime(state, 0.14f, 0);
                currentAnimState = state;
            }
            else if (state == "Walk" && petAnimator.HasState(0, Animator.StringToHash("Run")))
            {
                if (!forceRestart && currentAnimState == "Run") return;
                petAnimator.CrossFadeInFixedTime("Run", 0.14f, 0);
                currentAnimState = "Run";
            }
        }

        public void CommandAttack(Combatant target)
        {
            if (!target || !target.Alive) return;
            currentTarget = target;
            CurrentState = PetState.Attack;
            ChatSystem.LogSystem($"{petName} commands: Attacking [{target.gameObject.name}]!");
            AudioEffects.PlayClick();
        }

        public void CommandFollow()
        {
            CurrentState = PetState.Follow;
            currentTarget = null;
            ChatSystem.LogSystem($"{petName} commands: Following master.");
            AudioEffects.PlayClick();
        }

        public void DrawPetFrameGUI(float screenW, float screenH)
        {
            if (!petInstance || FrontendMenu.IsOpen) return;
            InitStyles();

            float frameW = 205f;
            float frameH = 46f;
            float x = 24f;
            float y = 80f;

            // Frame Background & Border
            GUI.DrawTexture(new Rect(x - 2, y - 2, frameW + 4, frameH + 4), borderTex);
            GUI.DrawTexture(new Rect(x, y, frameW, frameH), frameBgTex);

            // Pet Name & Happiness Diet
            GUI.Label(new Rect(x + 10, y + 4, 130, 20), $"🐾 {petName}", frameTitleStyle);
            GUI.Label(new Rect(x + 130, y + 5, 70, 18), "🍖 Happy", frameSubStyle);

            // HP Bar
            float hpPct = Mathf.Clamp01(maxHealth > 0 ? currentHealth / maxHealth : 0);
            GUI.DrawTexture(new Rect(x + 10, y + 23, 120f * hpPct, 16), hpFillTex);
            GUI.Label(new Rect(x + 10, y + 23, 120, 16), $"{currentHealth} / {maxHealth}", hpStyle);

            // Command Button
            string cmdLabel = CurrentState == PetState.Attack ? "FOLLOW [T]" : "ATTACK [T]";
            if (GUI.Button(new Rect(x + 134, y + 22, 64, 18), cmdLabel, btnStyle))
            {
                if (CurrentState == PetState.Attack) CommandFollow();
                else if (player && player.Target && player.Target.Alive) CommandAttack(player.Target);
                else CommandFollow();
            }
        }

        void InitStyles()
        {
            if (frameTitleStyle != null) return;

            frameBgTex = new Texture2D(1, 1);
            frameBgTex.SetPixel(0, 0, new Color(0.08f, 0.10f, 0.14f, 0.94f));
            frameBgTex.Apply();

            borderTex = new Texture2D(1, 1);
            borderTex.SetPixel(0, 0, new Color(0.35f, 0.45f, 0.55f, 1f));
            borderTex.Apply();

            hpFillTex = new Texture2D(1, 1);
            hpFillTex.SetPixel(0, 0, new Color(0.18f, 0.72f, 0.25f, 1f));
            hpFillTex.Apply();

            frameTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            frameTitleStyle.normal.textColor = new Color(0.95f, 0.85f, 0.4f);

            frameSubStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleRight
            };
            frameSubStyle.normal.textColor = new Color(0.4f, 0.95f, 0.5f);

            hpStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            hpStyle.normal.textColor = Color.white;

            btnStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
