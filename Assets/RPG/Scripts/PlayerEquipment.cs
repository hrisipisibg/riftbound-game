using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class PlayerController
    {
        readonly List<string> ownedItems = new List<string> { "starter-axe" };
        public IReadOnlyList<string> OwnedItems => ownedItems;
        public string EquippedWeaponId { get; private set; } = "starter-axe";
        public string EquippedArmorId { get; private set; } = "";
        public string EquippedTrinketId { get; private set; } = "";
        public int BoneShards { get; private set; }
        public void AddGoldDirect(int amount) { Gold += Mathf.Max(0, amount); SaveAdventure(false); }
        public void AddPotionsDirect(int count) { Potions = Mathf.Clamp(Potions + count, 0, 10); SaveAdventure(false); }
        public bool SpendGoldDirect(int amount) { if (Gold < amount) return false; Gold -= amount; SaveAdventure(false); return true; }
        public void AddBoneShardsDirect(int amount) { BoneShards += Mathf.Max(0, amount); SaveAdventure(false); }
        public GearItem Weapon => ItemCatalog.Get(EquippedWeaponId);
        public GearItem Armor => ItemCatalog.Get(EquippedArmorId);
        public GearItem Trinket => ItemCatalog.Get(EquippedTrinketId);
        public int ArmorRating => Armor != null ? Armor.armor : 0;
        public bool OwnsItem(string id) => ownedItems.Contains(id);
        public bool IsEquipped(GearItem item) => item != null && (EquippedWeaponId == item.id || EquippedArmorId == item.id || EquippedTrinketId == item.id);
        public bool GrantItem(string id, bool equipIfBetter = false)
        {
            var item = ItemCatalog.Get(id); if (item == null) return false;
            bool added = !ownedItems.Contains(id); if (added) ownedItems.Add(id);
            var previous = item.slot == GearSlot.Weapon ? Weapon : item.slot == GearSlot.Armor ? Armor : Trinket;
            if (equipIfBetter && (previous == null || GearValue(item) > GearValue(previous))) EquipInternal(item);
            return added;
        }
        static int GearValue(GearItem item) => item.damage * 10 + item.armor * 10 + item.health;
        public void EquipItem(string id)
        {
            var item = ItemCatalog.Get(id);
            if (item == null || !ownedItems.Contains(id) || (Stats && !Stats.Alive) || FrontendMenu.IsOpen || !InventoryOpen || ShopOpen || MapOpen || DialogueOpen || (orbit && orbit.Paused)) return;
            if (InCombat()) { Message = "Leave combat before changing equipment."; return; }
            EquipInternal(item); Message = "Equipped: " + item.name + "."; SaveAdventure(false);
        }
        public void EquipDirect(string id)
        {
            var item = ItemCatalog.Get(id);
            if (item == null) return;
            if (!ownedItems.Contains(id)) ownedItems.Add(id);
            EquipInternal(item);
            SaveAdventure(false);
        }
        void EquipInternal(GearItem item)
        {
            if (item.slot == GearSlot.Weapon) EquippedWeaponId = item.id;
            else if (item.slot == GearSlot.Armor) EquippedArmorId = item.id;
            else EquippedTrinketId = item.id;
            RefreshEquipmentStats();
        }
        public void RefreshEquipmentStats()
        {
            if (!Stats) return;
            int health = (Armor != null ? Armor.health : 0) + (Trinket != null ? Trinket.health : 0);
            int raceHpBonus = CharacterRace == "Brute" ? 25 : 0;
            int raceArmorBonus = CharacterRace == "Undead" ? 3 : 0;
            int gemHp = 0, gemArmor = 0;
            if (GemSocketSystem.Instance != null)
            {
                GemSocketSystem.Instance.GetTotalEquippedGemStats(out _, out _, out gemHp, out gemArmor, out _);
            }
            Stats.ConfigureVitals(150 + (Level - 1) * 20 + health + raceHpBonus + gemHp, ArmorRating + raceArmorBonus + gemArmor);
            if (!animator || Weapon == null) return;
            var block = new MaterialPropertyBlock();
            Color tint = Weapon.rarity == "Legendary" ? new Color(1.0f, 0.65f, 0.2f) :
                         Weapon.rarity == "Epic" ? new Color(0.85f, 0.45f, 1.0f) :
                         Weapon.rarity == "Rare" ? new Color(0.45f, 0.75f, 1.0f) :
                         Weapon.rarity == "Uncommon" ? new Color(0.45f, 1.0f, 0.5f) : Color.white;

            Transform handSlot = null;
            if (animator.isHuman) handSlot = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!handSlot)
            {
                foreach (var t in animator.GetComponentsInChildren<Transform>())
                {
                    string tName = t.name.ToLowerInvariant();
                    if (tName == "handslot.r" || tName.EndsWith(":righthand") || tName.Contains("hand.r"))
                    { handSlot = t; break; }
                }
            }

            if (handSlot)
            {
                foreach (var r in handSlot.GetComponentsInChildren<Renderer>())
                {
                    for (int i = 0; i < r.sharedMaterials.Length; i++)
                    {
                        r.GetPropertyBlock(block, i);
                        block.SetColor("_BaseColor", tint);
                        if (Weapon.rarity == "Epic" || Weapon.rarity == "Legendary")
                            block.SetColor("_EmissionColor", tint * 0.45f);
                        r.SetPropertyBlock(block, i);
                    }
                }
            }

            foreach (var renderer in animator.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null) continue;
                    string mName = materials[i].name.ToLowerInvariant();
                    if (mName.Contains("blinn") || mName.Contains("axe") || mName.Contains("weapon") || mName.Contains("blade") || mName.Contains("staff"))
                    {
                        renderer.GetPropertyBlock(block, i);
                        block.SetColor("_BaseColor", tint);
                        if (Weapon.rarity == "Epic" || Weapon.rarity == "Legendary")
                            block.SetColor("_EmissionColor", tint * 0.45f);
                        renderer.SetPropertyBlock(block, i);
                    }
                }
            }
        }
        string EnemyItem(EnemyController enemy)
        {
            string id = enemy.enemyId == "boss-vermithrax" ? "wyrmtooth-edge" :
                        enemy.enemyId == "boss-abbot-vael" ? "scarlet-aegis" :
                        enemy.enemyId == "boss-malakar" ? "scarlet-blade" :
                        enemy.enemyId == "crypt-warden" ? "warden-axe" :
                        enemy.IsMainQuestCaptain ? "captain-axe" :
                        enemy.enemyId == "road-raider-2" ? "raider-cleaver" :
                        enemy.enemyId == "camp-guard-1" ? "boneplate" : "";

            return id;
        }
        string ChestItem(string chestId)
        {
            string id = chestId == "crypt-reliquary" ? "crypt-signet" :
                        chestId == "village-cache" ? "leather-vest" :
                        chestId == "camp-cache" ? "travel-charm" :
                        chestId == "tower-cache" ? "watchtower-cuirass" :
                        chestId == "cathedral-altar-chest" ? "scarlet-blade" :
                        chestId == "bloodrock-chest" ? "bloodrock-waraxe" :
                        chestId == "wyrmcrest-hoard-chest" ? "ember-drake-scale" : "";
            return GrantItem(id) ? ItemCatalog.Get(id).name : "";
        }
        void RestoreEquipment(AdventureSave data)
        {
            ownedItems.Clear(); ownedItems.Add("starter-axe");
            if (data.items != null) foreach (string id in data.items) if (!string.IsNullOrEmpty(id) && !ownedItems.Contains(id)) ownedItems.Add(id);
            // Migration grants earned rewards without changing existing enemy/quest progress.
            if (!data.lootOnCorpses)
            {
                if (data.enemies != null && System.Array.IndexOf(data.enemies, "road-raider-2") >= 0) GrantItem("raider-cleaver");
                if (data.enemies != null && System.Array.IndexOf(data.enemies, "camp-guard-1") >= 0) GrantItem("boneplate");
                if (data.bossDefeated) GrantItem("captain-axe");
                if (data.enemies != null && System.Array.IndexOf(data.enemies, "crypt-warden") >= 0) GrantItem("warden-axe");
            }
            if (data.chests != null) foreach (string id in data.chests) ChestItem(id);
            if (data.weaponUpgraded) { GrantItem("emberwatch-axe"); GrantItem("emberwatch-sigil"); }
            EquippedWeaponId = ValidEquipment(data.weapon, GearSlot.Weapon) ? data.weapon : data.weaponUpgraded ? "emberwatch-axe" : "starter-axe";
            EquippedArmorId = ValidEquipment(data.armor, GearSlot.Armor) ? data.armor : "";
            EquippedTrinketId = ValidEquipment(data.trinket, GearSlot.Trinket) ? data.trinket : data.weaponUpgraded ? "emberwatch-sigil" : "";
            BoneShards = Mathf.Max(0, data.boneShards); RefreshEquipmentStats();
        }
        bool ValidEquipment(string id, GearSlot slot) => ownedItems.Contains(id ?? "") && ItemCatalog.Get(id) != null && ItemCatalog.Get(id).slot == slot;
        public void SellShards()
        {
            if (!CanTrade() || InCombat() || BoneShards <= 0) return;
            int gold = BoneShards * 2; BoneShards = 0; Gold += gold; Message = $"Sold bone shards for {gold} gold."; SaveAdventure(false);
        }
        public void BuyVest()
        {
            if (!CanTrade() || InCombat() || Gold < 25 || OwnsItem("leather-vest")) return;
            Gold -= 25; GrantItem("leather-vest"); Message = "Purchased Scout's Leather Vest. Open inventory [I] to equip it."; SaveAdventure(false);
        }
    }
}
