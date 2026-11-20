using UnityEngine;

namespace Riftbound
{
    public static class RiverReaverCamp
    {
        public const string CaptainId = "westbank-reaver-captain";
        public const string CacheId = "river-reaver-lockbox";
        public static readonly Vector3 Position = new Vector3(-283, 15.46f, 78);

        public static void Ensure()
        {
            if (!WorldRegions.Expanded || GameObject.Find("River Reaver camp")) return;
            var player = Object.FindFirstObjectByType<PlayerController>();
            var root = new GameObject("River Reaver camp").transform;
            CreateEnemy(root, player, CaptainId, "Captain Selvek", "Undead", "Rogue", Position, true);
            CreateEnemy(root, player, "westbank-reaver-1", "River Reaver lookout", "Human", "Rogue", Position + new Vector3(-7, 0, -6), false);
            CreateEnemy(root, player, "westbank-reaver-2", "River Reaver guard", "Undead", "Warrior", Position + new Vector3(7, 0, -4), false);
            var prefab = Resources.Load<GameObject>("Props/TreasureChest");
            if (!prefab) return;
            var point = Ground(Position + Vector3.forward * 4);
            var instance = Object.Instantiate(prefab, point, Quaternion.identity, root);
            instance.name = "River Reaver lockbox";
            var chest = instance.GetComponent<LootChest>() ?? instance.AddComponent<LootChest>();
            chest.chestId = CacheId; chest.gold = 45; chest.experience = 75;
        }

        static Vector3 Ground(Vector3 point) { point.y = WorldRegions.GroundHeight(point) + .03f; return point; }

        internal static void CreateEnemy(Transform parent, PlayerController player, string id, string name, string race, string cls, Vector3 point, bool boss, bool ranged = false)
        {
            var root = new GameObject(name);
            root.SetActive(false);
            root.transform.SetParent(parent); root.transform.position = Ground(point);
            root.transform.rotation = Quaternion.Euler(0, 180, 0);
            var visual = CharacterVisualFactory.CreateVisualInstance(race, cls, root.transform, hostile: true);
            var enemy = root.AddComponent<EnemyController>();
            enemy.enemyId = id; enemy.player = player; enemy.isBoss = boss;
            enemy.ranged = ranged;
            if (ranged) enemy.attackRange = 11f;
            enemy.heavyAttacker = boss; enemy.campGuard = true; enemy.nativeDeathAnimation = true;
            enemy.animator = visual ? visual.GetComponentInChildren<Animator>() : null;
            enemy.attackDamage = boss ? 16 : 7; enemy.attackInterval = boss ? 2.4f : 2;
            enemy.movementMultiplier = boss ? .65f : .7f;
            var profile = CharacterVisualFactory.GetProfile(race);
            enemy.walkStrideSpeed = profile.walkStride; enemy.runStrideSpeed = profile.runStride;
            var motor = root.GetComponent<CharacterController>();
            motor.height = profile.controllerHeight; motor.radius = profile.controllerRadius; motor.center = profile.controllerCenter;
            motor.slopeLimit = 45; motor.stepOffset = .3f;
            var stats = root.GetComponent<Combatant>();
            stats.displayName = name; stats.ConfigureVitals(boss ? 260 : 90, boss ? 2 : 0); stats.Restore();
            root.SetActive(!player || !Application.isPlaying || !player.HasDefeated(id));
        }
    }
}
