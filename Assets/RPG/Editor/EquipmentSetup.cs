using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class EquipmentSetup
    {
        public static void BatchBuild()
        {
            try
            {
                EditorSceneManager.OpenScene("Assets/RPG/Scenes/Outpost-Updated.unity"); Setup();
                PrototypeBuild.BuildWindows();
                if (!File.ReadAllText("PrototypeBuildResult.txt").StartsWith("Succeeded")) throw new Exception("Windows build failed.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { File.WriteAllText("EquipmentResult.txt", "FAILED: " + exception); Debug.LogException(exception); EditorApplication.Exit(1); }
        }
        [MenuItem("Riftbound/Set up equipment rewards")]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var scene = EditorSceneManager.GetActiveScene();
            const string backup = "Assets/RPG/Scenes/Backups/Outpost-before-equipment.unity";
            Directory.CreateDirectory("Assets/RPG/Scenes/Backups");
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(scene, backup, true);
            foreach (var chest in UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None))
            { chest.requiresCaptainDefeated = chest.chestId == "tower-cache"; EditorUtility.SetDirty(chest); }
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var stats = enemy.GetComponent<Combatant>();
                stats.flatDamageReduction = enemy.isBoss ? 3 : enemy.enemyId == "camp-guard-1" ? 2 : 0; EditorUtility.SetDirty(stats);
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Validate();
        }
        static void Validate()
        {
            var report = new StringBuilder();
            void Check(bool valid, string message) { report.AppendLine((valid ? "PASS " : "FAIL ") + message); if (!valid) throw new InvalidOperationException(message); }
            Check(ItemCatalog.All.Length == 11 && ItemCatalog.All.Select(i => i.id).Distinct().Count() == 11, "Eleven unique gear items");
            var tower = UnityEngine.Object.FindObjectsByType<LootChest>(FindObjectsSortMode.None).First(c => c.chestId == "tower-cache");
            Check(tower.requiresCaptainDefeated, "Tower reward requires captain defeat");
            var actor = new GameObject("Equipment checks") { hideFlags = HideFlags.HideAndDontSave };
            actor.transform.position = new Vector3(1000, 0, 1000);
            var trader = new GameObject("Equipment check trader") { hideFlags = HideFlags.HideAndDontSave };
            trader.transform.position = actor.transform.position + Vector3.forward;
            var victim = new GameObject("Armor checks") { hideFlags = HideFlags.HideAndDontSave };
            string previousSave = PlayerController.SavePathOverride;
            PlayerController.SavePathOverride = Path.GetFullPath("Library/EquipmentChecks/adventure.json");
            try
            {
                var player = actor.AddComponent<PlayerController>();
                typeof(PlayerController).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
                var stats = actor.GetComponent<Combatant>(); stats.Restore(); player.InventoryOpen = true;
                player.GrantItem("leather-vest"); player.EquipItem("leather-vest");
                Check(player.EquippedArmorId == "leather-vest" && stats.maxHealth == 160 && stats.flatDamageReduction == 2, "Armor equips and changes health/mitigation");
                Check(stats.Health == 100, "Equipping bonus health does not heal");
                player.GrantItem("boneplate"); player.EquipItem("boneplate"); stats.Restore(); player.EquipItem("leather-vest");
                Check(stats.Health == 160 && stats.maxHealth == 160, "Changing to weaker armor clamps health");
                var armorTest = victim.AddComponent<Combatant>(); armorTest.Restore(); armorTest.ConfigureVitals(100, 4); armorTest.Damage(10);
                Check(armorTest.Health == 94, "Armor reduces incoming damage");
                armorTest.Damage(1); Check(armorTest.Health == 93, "Positive hits retain a minimum of one damage");
                Check(!player.GrantItem("leather-vest"), "Unique loot cannot duplicate");
                var legacy = new PlayerController.AdventureSave { weaponUpgraded = true, bossDefeated = true, enemies = new[] { "road-raider-2", "camp-guard-1" }, chests = new[] { "village-cache", "camp-cache" } };
                typeof(PlayerController).GetMethod("RestoreEquipment", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, new object[] { legacy });
                Check(player.OwnsItem("captain-axe") && player.OwnsItem("boneplate") && player.OwnsItem("travel-charm"), "Legacy kills and caches grant earned items");
                Check(player.EquippedWeaponId == "emberwatch-axe" && player.EquippedTrinketId == "emberwatch-sigil", "Legacy quest gear migrates to equipped slots");
                var merchant = trader.AddComponent<Merchant>();
                typeof(PlayerController).GetProperty("ActiveMerchant").SetValue(player, merchant);
                typeof(PlayerController).GetProperty("BoneShards").SetValue(player, 3);
                typeof(PlayerController).GetProperty("Gold").SetValue(player, 10);
                player.SellShards(); player.SellShards();
                Check(player.Gold == 16 && player.BoneShards == 0, "Selling shards pays once");
                player.SaveAdventure(false);
                var saved = JsonUtility.FromJson<PlayerController.AdventureSave>(File.ReadAllText(PlayerController.SavePathOverride));
                Check(saved.weapon == "emberwatch-axe" && saved.items.Contains("captain-axe"), "Equipment persists in save file");
                File.WriteAllText("EquipmentResult.txt", report + "Isolated edit-mode checks only; gameplay tests left to user.\n");
            }
            finally
            {
                PlayerController.SavePathOverride = previousSave;
                UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(trader); UnityEngine.Object.DestroyImmediate(victim);
            }
        }
    }
}
