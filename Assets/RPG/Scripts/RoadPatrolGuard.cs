using UnityEngine;

namespace Riftbound
{
    public class RoadPatrolGuard : MonoBehaviour
    {
        public static RoadPatrolGuard Instance { get; private set; }

        static readonly Vector3[] Waypoints = new Vector3[]
        {
            new Vector3(0f, 0f, 15f),    // Emberwatch Gateway
            new Vector3(2f, 0f, 55f),    // Forest Road midpoint
            new Vector3(5f, 0f, 125f),   // Willow Crossing approach
            new Vector3(-22f, 0f, 154f)  // Greenhollow Riding Post
        };

        public Animator animator;
        public CharacterController motor;
        public Combatant combatant;

        int currentWaypointIndex = 0;
        bool forward = true;
        float waitTimer = 0f;
        Combatant currentEnemy;
        float attackCooldown = 0f;

        public static void Ensure()
        {
            if (Instance != null) return;
            if (FindFirstObjectByType<RoadPatrolGuard>() != null) return;

            var go = new GameObject("Alliance Highway Patrol");
            Instance = go.AddComponent<RoadPatrolGuard>();
            Instance.InitializeGuard();
        }

        void InitializeGuard()
        {
            Vector3 startPos = Waypoints[0];
            startPos.y = WorldRegions.GroundHeight(startPos) + 0.15f;
            transform.position = startPos;

            // Visual Model
            CharacterVisualFactory.CreateVisualInstance("Human", "Warrior", transform);
            animator = GetComponentInChildren<Animator>();

            // Movement and Physics
            motor = gameObject.AddComponent<CharacterController>();
            motor.height = 1.9f;
            motor.radius = 0.45f;
            motor.center = new Vector3(0f, 0.95f, 0f);

            // Combat stats
            combatant = gameObject.AddComponent<Combatant>();
            combatant.displayName = "Alliance Outrunner";
            combatant.maxHealth = 350f;
            combatant.flatDamageReduction = 3;
            combatant.isPlayer = true; // Friendly to player

            // NPC Gossip
            var npc = gameObject.AddComponent<TownNPC>();
            npc.npcName = "Alliance Outrunner";
            npc.roleTag = "Highway Patrol";
            npc.greeting = "The road between Emberwatch and Greenhollow is under our watch. Stay on the cobblestone path and watch out for Ashen scouts.";
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        void Update()
        {
            if (FrontendMenu.IsOpen || Time.deltaTime <= 0) return;

            // 1. Check for nearby enemies threatening the player
            if (currentEnemy == null || !currentEnemy.Alive)
            {
                currentEnemy = FindThreat();
            }

            if (currentEnemy != null && currentEnemy.Alive)
            {
                EngageEnemy();
                return;
            }

            // 2. Highway Patrol Route
            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
                PlayAnim("Idle");
                return;
            }

            Vector3 target = Waypoints[currentWaypointIndex];
            target.y = WorldRegions.GroundHeight(target);

            Vector3 dir = target - transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;

            if (dist < 1.8f)
            {
                // Reached waypoint: pause briefly
                waitTimer = 3.5f;
                if (forward)
                {
                    currentWaypointIndex++;
                    if (currentWaypointIndex >= Waypoints.Length)
                    {
                        currentWaypointIndex = Waypoints.Length - 2;
                        forward = false;
                    }
                }
                else
                {
                    currentWaypointIndex--;
                    if (currentWaypointIndex < 0)
                    {
                        currentWaypointIndex = 1;
                        forward = true;
                    }
                }
                PlayAnim("Idle");
                return;
            }

            // Move towards waypoint along ground
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 6f);
            Vector3 moveVec = dir.normalized * 2.8f;
            moveVec.y = -9.8f; // Gravity
            if (motor && motor.enabled) motor.Move(moveVec * Time.deltaTime);

            PlayAnim("Walk");
        }

        Combatant FindThreat()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (!player || !player.Stats || !player.Stats.Alive) return null;

            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
            {
                if (enemy != null && !enemy.trainingDummy)
                {
                    var c = enemy.GetComponent<Combatant>();
                    if (c != null && c.Alive)
                    {
                        float distToPlayer = Vector3.Distance(player.transform.position, enemy.transform.position);
                        float distToGuard = Vector3.Distance(transform.position, enemy.transform.position);
                        if (distToGuard < 14f && distToPlayer < 12f)
                            return c;
                    }
                }
            }
            return null;
        }

        void EngageEnemy()
        {
            Vector3 dir = currentEnemy.transform.position - transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;

            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir.normalized), Time.deltaTime * 8f);

            if (dist > 2.5f)
            {
                Vector3 moveVec = dir.normalized * 4.2f;
                moveVec.y = -9.8f;
                if (motor && motor.enabled) motor.Move(moveVec * Time.deltaTime);
                PlayAnim("Run");
            }
            else
            {
                PlayAnim("Attack");
                attackCooldown -= Time.deltaTime;
                if (attackCooldown <= 0f)
                {
                    attackCooldown = 1.3f;
                    currentEnemy.Damage(22f);
                    AudioEffects.PlayBladeHit();
                    CombatTextManager.Spawn(currentEnemy.transform.position + Vector3.up * 1.5f, "22", Color.white, false);
                }
            }
        }

        void PlayAnim(string anim)
        {
            if (!animator) return;
            animator.CrossFadeInFixedTime(anim, 0.2f, 0, 0);
        }
    }
}
