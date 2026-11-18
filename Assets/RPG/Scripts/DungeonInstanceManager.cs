using System;
using UnityEngine;

namespace Riftbound
{
    public class DungeonInstanceManager : MonoBehaviour
    {
        static DungeonInstanceManager _instance;
        public static DungeonInstanceManager Instance
        {
            get
            {
                if (!_instance) _instance = FindAnyObjectByType<DungeonInstanceManager>();
                return _instance;
            }
            private set => _instance = value;
        }

        public enum DungeonDifficulty
        {
            Normal,
            Heroic
        }

        public DungeonDifficulty CurrentDifficulty = DungeonDifficulty.Normal;

        // Dungeon coordinate center
        public static readonly Vector3 ScarletCryptsOrigin = new Vector3(-400f, -40f, 200f);
        public const float InstanceRadius = 130f;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
        }

        public static bool IsInsideDungeon(Vector3 pos)
        {
            return Vector3.Distance(pos, ScarletCryptsOrigin) < InstanceRadius;
        }

        public bool SetDifficulty(DungeonDifficulty diff, PlayerController player, out string reason)
        {
            if (player && IsInsideDungeon(player.transform.position))
            {
                reason = "Cannot change dungeon difficulty while inside an active dungeon instance!";
                return false;
            }

            CurrentDifficulty = diff;
            reason = $"Dungeon difficulty set to: {diff.ToString().ToUpperInvariant()}";
            string colorHex = diff == DungeonDifficulty.Heroic ? "#ff4444" : "#44ff44";
            ChatSystem.LogSystem($"<color={colorHex}>[Dungeon] Difficulty updated to {diff}.</color>");
            if (ZoneBanner.Instance)
            {
                ZoneBanner.Show("DUNGEON DIFFICULTY", diff.ToString().ToUpperInvariant());
            }
            return true;
        }

        public bool ResetAllInstances(PlayerController player, out string reason)
        {
            if (player && IsInsideDungeon(player.transform.position))
            {
                reason = "Cannot reset instances while inside an active dungeon!";
                ChatSystem.LogSystem("<color=#ff4444>[Dungeon] Cannot reset instances while inside!</color>");
                return false;
            }

            if (player)
            {
                player.ResetDungeonEncounters();
            }

            // Reactivate all dungeon entities in scene
            var allEnemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var enemy in allEnemies)
            {
                if (enemy && (enemy.enemyId.StartsWith("boss-") || enemy.enemyId.StartsWith("dungeon-") || enemy.enemyId.Contains("crypt")))
                {
                    enemy.gameObject.SetActive(true);
                    var stats = enemy.GetComponent<Combatant>();
                    if (stats) stats.Restore();
                }
            }

            // Refresh chests
            var allChests = FindObjectsByType<LootChest>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var chest in allChests)
            {
                if (chest && chest.chestId == "cathedral-altar-chest")
                {
                    chest.Refresh();
                }
            }

            AudioEffects.PlayLootCoin();
            reason = "Instances reset successfully. Encounters rejuvenated.";
            ChatSystem.LogSystem("<color=#ffd100>★ [Dungeon] All dungeon instances have been reset!</color>");
            if (ZoneBanner.Instance)
            {
                ZoneBanner.Show("INSTANCES RESET", "The Scarlet Crypts has been rejuvenated.");
            }

            return true;
        }

        public float GetHealthMultiplier()
        {
            return CurrentDifficulty == DungeonDifficulty.Heroic ? 2.5f : 1.0f;
        }

        public float GetDamageMultiplier()
        {
            return CurrentDifficulty == DungeonDifficulty.Heroic ? 1.5f : 1.0f;
        }
    }
}
