using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Riftbound
{
    public partial class PlayerController
    {
        public int QuestIndex { get; private set; }
        public int Supplies => collectedSupplies.Count;
        public bool BossDefeated { get; private set; }
        public int Experience { get; private set; }
        public int Potions { get; private set; } = 2;
        public bool WeaponUpgraded { get; private set; }
        public bool InventoryOpen { get; set; }
        public static string SavePathOverride;
        public string CharacterSaveId { get; set; } = "";
        public string CharacterName { get; set; } = "Brute";
        public string CharacterFaction { get; set; } = "Emberwatch";
        public string CharacterRace { get; set; } = "Brute";
        public string CharacterClass { get; set; } = "Warrior";
        public string SavePath => string.IsNullOrEmpty(SavePathOverride) ? Path.Combine(Application.persistentDataPath, string.IsNullOrEmpty(CharacterSaveId) ? "adventure-v1.json" : "adventure-" + CharacterSaveId + ".json") : SavePathOverride;
        readonly HashSet<string> defeatedEnemies = new HashSet<string>();
        readonly HashSet<string> collectedSupplies = new HashSet<string>();
        static readonly int[] LevelThresholds =
        {
            0, 100, 240, 420, 650, 900, 1160, 1430, 1710, 2000,
            2300, 2610, 2930, 3260, 3600, 3950, 4310, 4680, 5060, 5450,
            5850, 6260, 6680, 7110, 7550
        };
        public static int MaxLevel => LevelThresholds.Length;
        public int Level
        {
            get
            {
                int index = Array.BinarySearch(LevelThresholds, Experience);
                return index >= 0 ? index + 1 : ~index;
            }
        }
        public int LevelFloor => LevelThresholds[Level - 1];
        public int NextLevelExperience => LevelThresholds[Mathf.Min(Level, MaxLevel - 1)];
        public string QuestName => QuestIndex == 0 ? "The Forest Road" : QuestIndex == 1 ? "Stolen Supplies" : QuestIndex == 2 ? "The Ashwood Captain" : "Hero of Emberwatch";
        public int QuestGold => QuestIndex == 0 ? 25 : QuestIndex == 1 ? 35 : 60;
        public int QuestExperience => QuestIndex == 0 ? 80 : QuestIndex == 1 ? 100 : 180;
        public bool QuestReady => QuestAccepted && (QuestIndex == 0 ? Kills >= 3 : QuestIndex == 1 ? Supplies >= 3 : QuestIndex == 2 && BossDefeated);
        public string QuestObjective => QuestComplete ? "Emberwatch is safe. Adventure complete!" : !QuestAccepted ? "Speak to Abe at the village gate. [E]" : QuestReady ? "Return to Abe for your reward." : QuestIndex == 0 ? $"Defeat forest raiders: {Mathf.Min(Kills, 3)} / 3\nFollow the road north." : QuestIndex == 1 ? $"Recover supply crates: {Supplies} / 3\nAshwood camp, east of the road. [E]" : "Defeat the Ashwood Captain: 0 / 1\nThe ruined watchtower, northeast of camp.";
        public string QuestDescription => QuestComplete ? "You broke the raiders' hold on the forest. The village will remember your name." : !QuestAccepted ? QuestIndex == 0 ? "Raiders have cut off our forest road. Defeat three of them and return to me. Watch your health, and use Second Wind when you need it." : QuestIndex == 1 ? "The raiders stole our winter supplies. Follow the road north, then turn east into Ashwood camp. Recover the three marked crates. Be careful: two guards remain." : "Those supplies carried a map to the captain's hideout. He waits at the ruined watchtower northeast of camp. His heavy strike takes time to prepare. Move out of the red circle!" : QuestReady ? QuestIndex == 2 ? "The captain has fallen! Take this axe as a symbol of Emberwatch's gratitude. You have earned it." : "Well done! These supplies and a safe road will keep our village alive. Rest here before your next task." : QuestObjective;
        public Vector3 ObjectivePosition
        {
            get
            {
                if (!QuestAccepted || QuestReady || QuestComplete) return questGiver ? questGiver.position : spawn;
                Vector3 result = QuestIndex == 2 ? new Vector3(35, 0, 90) : new Vector3(0, 0, 45);
                float nearest = float.MaxValue;
                if (QuestIndex == 1)
                {
                    foreach (var supply in FindObjectsByType<SupplyCrate>(FindObjectsSortMode.None))
                        if (!supply.Collected && Vector3.Distance(transform.position, supply.transform.position) < nearest)
                        { result = supply.transform.position; nearest = Vector3.Distance(transform.position, result); }
                }
                else foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    if ((QuestIndex == 2 ? enemy.IsMainQuestCaptain : enemy.IsRoadRaider) && enemy.GetComponent<Combatant>().Alive && Vector3.Distance(transform.position, enemy.transform.position) < nearest)
                    { result = enemy.transform.position; nearest = Vector3.Distance(transform.position, result); }
                return result;
            }
        }
        public SupplyCrate NearbySupply()
        {
            if (!Stats || !Stats.Alive || QuestIndex != 1 || !QuestAccepted || DialogueOpen) return null;
            SupplyCrate result = null; float nearest = 3;
            foreach (var supply in FindObjectsByType<SupplyCrate>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(transform.position, supply.transform.position);
                if (!supply.Collected && distance < nearest) { result = supply; nearest = distance; }
            }
            return result;
        }
        public void RecordSupply(string id)
        {
            if (QuestIndex != 1 || !QuestAccepted || string.IsNullOrEmpty(id) || !collectedSupplies.Add(id)) return;
            Message = $"Supplies recovered: {Supplies}/3.";
            SaveAdventure(false);
        }
        public bool HasSupply(string id) => collectedSupplies.Contains(id);
        public void ResetDungeonEncounters()
        {
            defeatedEnemies.RemoveWhere(id => id.StartsWith("boss-") || id.StartsWith("dungeon-") || id.Contains("crypt"));
            openedChests.Remove("cathedral-altar-chest");
        }
        public void RecordEnemyDefeat(EnemyController enemy)
        {
            if (!enemy || enemy.trainingDummy || string.IsNullOrEmpty(enemy.enemyId) || !defeatedEnemies.Add(enemy.enemyId)) return;
            if (enemy.IsMainQuestCaptain) BossDefeated = true;
            else if (enemy.IsRoadRaider) Kills++;
            if (FactionQuestSystem.Instance) FactionQuestSystem.Instance.OnEnemyDefeated(enemy);
            int goldGain = enemy.isBoss ? 20 : enemy.campGuard ? 5 : 3;
            int expGain = enemy.isBoss ? 120 : enemy.campGuard ? 40 : 30;
            string enemyName = enemy.GetComponent<Combatant>()?.displayName ?? "enemy";
            ChatSystem.LogCombat($"Defeated {enemyName}! +{expGain} XP.");
            string item = EnemyItem(enemy);
            int boneCount = enemy.isBoss ? 5 : enemy.campGuard ? 2 : 1;
            LootDropSystem.SpawnLoot(enemy.transform.position, enemyName, goldGain, boneCount, item);
            AddExperience(expGain);
            Message = enemy.IsMainQuestCaptain ? "Captain defeated! Return to Abe. [E] Loot the remains." : "+XP. [E] Loot the remains. " + QuestObjective;
            SaveAdventure(false);
        }
        public void AddExperience(int amount)
        {
            int previousLevel = Level;
            int finalAmount = amount;
            if (RestedXPSystem.Instance != null && amount > 0)
                finalAmount += RestedXPSystem.Instance.ApplyRestedBonus(amount);
            Experience = Mathf.Max(0, Experience + Mathf.Max(0, finalAmount));
            RefreshEquipmentStats();
            if (Level > previousLevel)
            {
                if (Stats != null) Stats.Heal(Stats.maxHealth);
                SpellFX.CreateLevelUpBurst(transform.position);
                bool unlockedMilestone = MilestoneTalentsAvailable &&
                    Level / MilestoneTalents.LevelsPerTier > previousLevel / MilestoneTalents.LevelsPerTier;
                string reward = unlockedMilestone ? "New milestone talent available [N]" : "Health and damage increased";
                ZoneBanner.Show($"LEVEL {Level}!", reward);
                Message = $"Level {Level}! {reward}.";
                AudioEffects.PlayLevelUp();
                ChatSystem.LogSystem($"Congratulations! You have reached Level {Level}!");
            }
        }
        public void AddGold(int amount) { if (amount > 0) Gold += amount; }
        public void UsePotion()
        {
            if (!Stats.Alive || FrontendMenu.IsOpen || DialogueOpen || ShopOpen || MapOpen || JournalOpen || (orbit && orbit.Paused) || Potions <= 0 || Stats.Health >= Stats.maxHealth) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestUsePotion();
                return;
            }
            Potions--; Stats.Heal(70); Message = "Health potion: restored 70 HP.";
            SaveAdventure(false);
        }

        public void ApplyAuthorizedPotionUse(int remainingPotions, int newHealth)
        {
            Potions = Mathf.Max(0, remainingPotions);
            Stats.SetReplicatedHealth(newHealth, Stats.maxHealth);
            Message = "Health potion: restored 70 HP.";
        }
        public void RefreshAdventureWorld()
        {
            WesternRoadCamps.Ensure(this);
            BrassHollows.Ensure();
            CharacterVisualFactory.ApplyToPlayer(this, CharacterRace, CharacterClass);
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                bool active = !defeatedEnemies.Contains(enemy.enemyId) && (!enemy.IsMainQuestCaptain || (QuestIndex == 2 && QuestAccepted)) && (!enemy.requiresRunes || RunesFound >= 3);
                // Preserve an enemy's death animation until the next load.
                if (active || enemy.GetComponent<Combatant>().Alive || !Application.isPlaying) enemy.gameObject.SetActive(active);
            }
            foreach (var supply in FindObjectsByType<SupplyCrate>(FindObjectsInactive.Include, FindObjectsSortMode.None)) supply.Refresh(this);
            foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None)) chest.Refresh();
        }
        [Serializable] public class AdventureSave
        {
            public int version = 1, questIndex, kills, gold, experience, potions;
            public int westernRoadStage;
            public int brassHollowsStage;
            public bool accepted, bossDefeated, weaponUpgraded, temperedWeapon;
            public string[] enemies, supplies, chests;
            public string[] items;
            public string[] runes;
            public string[] landmarks;
            public string mount;
            public string weapon, armor, trinket;
            public int boneShards;
            public string characterRace;
            public string characterClass;
            public bool lootOnCorpses;
            public LootDropSystem.SavedCorpse[] corpses;
            public int[] talentRanksA;
            public int[] talentRanksB;
            public string[] milestoneChoices;
            public string[] milestoneBindings;
            public bool hasPosition;
            public int worldRevision;
            public float posX, posY, posZ;
        }
        public bool SaveAdventure(bool notify = true)
        {
            if (Network.CoopSessionConfig.IsMultiplayer) return false;
            try
            {
                var data = new AdventureSave { questIndex = QuestIndex, accepted = QuestAccepted, kills = Kills, gold = Gold, experience = Experience, potions = Potions, bossDefeated = BossDefeated, weaponUpgraded = WeaponUpgraded, temperedWeapon = TemperedWeapon, enemies = new List<string>(defeatedEnemies).ToArray(), supplies = new List<string>(collectedSupplies).ToArray(), chests = new List<string>(openedChests).ToArray(), characterRace = CharacterRace, characterClass = CharacterClass };
                var (tA, tB) = TalentSystem.ExportRanks(CharacterClass);
                data.talentRanksA = tA;
                data.talentRanksB = tB;
                data.milestoneChoices = milestoneChoices;
                data.milestoneBindings = milestoneBindings;
                data.westernRoadStage = WesternRoadStage;
                data.brassHollowsStage = BrassHollowsStage;
                data.lootOnCorpses = true;
                data.worldRevision = WorldRegions.Expanded ? 1 : 0;
                data.corpses = LootDropSystem.Instance ? LootDropSystem.Instance.ExportCorpses() : null;
                data.items = ownedItems.ToArray(); data.weapon = EquippedWeaponId; data.armor = EquippedArmorId; data.trinket = EquippedTrinketId; data.boneShards = BoneShards;
                data.runes = new List<string>(discoveredRunes).ToArray();
                data.landmarks = new List<string>(landmarks).ToArray(); data.mount = SelectedMountId;
                if (!FrontendMenu.IsOpen)
                {
                    data.hasPosition = true;
                    // Reload at the trailhead while retaining cave kills and quest progress.
                    Vector3 savePosition = BrassHollows.Inside(transform.position) && BrassHollows.Current
                        ? BrassHollows.Current.Entrance - Vector3.forward * 2f : transform.position;
                    data.posX = savePosition.x;
                    data.posY = savePosition.y;
                    data.posZ = savePosition.z;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
                File.WriteAllText(SavePath + ".tmp", JsonUtility.ToJson(data, true));
                if (File.Exists(SavePath)) File.Replace(SavePath + ".tmp", SavePath, SavePath + ".bak");
                else File.Move(SavePath + ".tmp", SavePath);
                if (notify) Message = $"Progress saved in {ZoneManager.GetZoneName(transform.position)}.";
                return true;
            }
            catch (Exception exception) { Message = "Save failed. Your current session is still playable."; Debug.LogWarning("Adventure save: " + exception.Message); return false; }
        }
        public Vector3 GetRacialSpawnPosition()
        {
            Vector3 raw;
            switch (CharacterRace)
            {
                case "Human":
                    raw = new Vector3(0f, 0.5f, 10f);
                    break;
                case "Dwarf":
                    raw = new Vector3(20f, 0.5f, -15f);
                    break;
                case "Elf":
                case "High Elf":
                    raw = new Vector3(-25f, 0.5f, 15f);
                    break;
                case "Undead":
                    raw = new Vector3(-28f, 5.7f, 160f);
                    break;
                case "Goblin":
                    raw = new Vector3(38f, 9.1f, 175f);
                    break;
                case "Orc":
                case "Brute":
                    raw = new Vector3(50f, 9.2f, 210f);
                    break;
                default:
                    raw = CharacterFaction == "Horde" ? new Vector3(50f, 9.2f, 210f) : new Vector3(0f, 0.5f, 10f);
                    break;
            }
            if (WorldRegions.Expanded)
            {
                switch (CharacterRace)
                {
                    case "Dwarf": raw = WorldRegions.Frostpeak; break;
                    case "Elf": case "High Elf": raw = WorldRegions.Silverglen; break;
                    case "Undead": raw = WorldRegions.Deathknell; break;
                    case "Goblin": raw = WorldRegions.Rustwater; break;
                    case "Orc": case "Brute": raw = WorldRegions.Bloodrock + new Vector3(-5, 0, -5); break;
                }
            }
            return ClampAboveGround(raw, 0.30f);
        }

        public static string GetStartingZoneName(string race)
        {
            switch (race)
            {
                case "Human": return "Emberwatch Outpost";
                case "Dwarf": return "Frostpeak Foothills (Dwarven Haven)";
                case "Elf":
                case "High Elf": return "Silverglen Sanctum (Elven Grove)";
                case "Undead": return "Deathknell Hollow (Forsaken Crypts)";
                case "Goblin": return "Rustwater Scrapworks (Cartel Basin)";
                case "Orc":
                case "Brute":
                default: return "Redridge Canyons (Camp Bloodrock)";
            }
        }

        void SyncFactionSpawnAndHearth(Vector3 racialSpawn)
        {
            spawn = racialSpawn;
            if (RestedXPSystem.Instance != null)
            {
                RestedXPSystem.Instance.HearthHomePosition = racialSpawn;
                RestedXPSystem.Instance.HearthHomeLocationName = GetStartingZoneName(CharacterRace);
            }
        }

        public void LoadAdventure()
        {
            WesternRoadStage = 0;
            BrassHollowsStage = 0;
            RestoreMilestoneTalents(null, null);
            if (!File.Exists(SavePath))
            {
                Vector3 racialSpawn = GetRacialSpawnPosition();
                SyncFactionSpawnAndHearth(racialSpawn);
                if (motor)
                {
                    motor.enabled = false;
                    transform.position = racialSpawn;
                    motor.enabled = true;
                }
                else transform.position = racialSpawn;

                string zone = GetStartingZoneName(CharacterRace);
                Message = $"Welcome, {CharacterName}. Your adventure begins in {zone}.";
                ZoneBanner.Show(zone, $"{CharacterFaction} territory");
                return;
            }
            try
            {
                var data = JsonUtility.FromJson<AdventureSave>(File.ReadAllText(SavePath));
                if (data == null || data.version != 1) return;
                WesternRoadStage = Mathf.Clamp(data.westernRoadStage, 0, 4);
                BrassHollowsStage = Mathf.Clamp(data.brassHollowsStage, 0, 3);
                QuestIndex = Mathf.Clamp(data.questIndex, 0, 3); QuestComplete = QuestIndex >= 3; QuestAccepted = !QuestComplete && data.accepted;
                Kills = Mathf.Max(0, data.kills); Gold = Mathf.Max(0, data.gold); Experience = Mathf.Max(0, data.experience); Potions = Mathf.Max(0, data.potions);
                BossDefeated = data.bossDefeated; WeaponUpgraded = data.weaponUpgraded;
                TemperedWeapon = data.temperedWeapon; openedChests.Clear();
                if (data.chests != null) foreach (string id in data.chests) openedChests.Add(id);
                defeatedEnemies.Clear(); collectedSupplies.Clear();
                if (data.enemies != null) foreach (string id in data.enemies) defeatedEnemies.Add(id);
                if (data.enemies != null)
                {
                    Kills = 0;
                    foreach (var id in defeatedEnemies)
                        if (id != null && id.StartsWith("road-raider-", StringComparison.Ordinal)) Kills++;
                    BossDefeated = defeatedEnemies.Contains("ashwood-captain");
                    data.bossDefeated = BossDefeated;
                }
                if (data.supplies != null) foreach (string id in data.supplies) collectedSupplies.Add(id);
                discoveredRunes.Clear(); if (data.runes != null) foreach (string id in data.runes) discoveredRunes.Add(id);
                landmarks.Clear(); if (data.landmarks != null) foreach (string id in data.landmarks) if (ValidLandmark(id)) landmarks.Add(id);
                if (!string.IsNullOrEmpty(data.characterRace)) CharacterRace = data.characterRace;
                if (!string.IsNullOrEmpty(data.characterClass)) CharacterClass = data.characterClass;
                if (CharacterRace == "Orc" || CharacterRace == "Brute" || CharacterRace == "Undead" || CharacterRace == "Goblin")
                    CharacterFaction = "Horde";

                Vector3 racialSpawn = GetRacialSpawnPosition();
                SyncFactionSpawnAndHearth(racialSpawn);

                Vector3 targetPos;
                if (data.hasPosition)
                {
                    var savedPosition = new Vector3(data.posX, data.posY, data.posZ);
                    if (data.worldRevision < 1) savedPosition = WorldRegions.LegacyPosition(savedPosition, CharacterRace);
                    targetPos = ClampAboveGround(savedPosition, 0.30f);
                }
                else if (CharacterFaction == "Horde" && Vector2.Distance(new Vector2(transform.position.x, transform.position.z), new Vector2(0f, -6f)) < 25f)
                {
                    targetPos = racialSpawn;
                }
                else
                {
                    targetPos = ClampAboveGround(transform.position, 0.30f);
                }

                if (motor)
                {
                    motor.enabled = false;
                    transform.position = targetPos;
                    motor.enabled = true;
                }
                else transform.position = targetPos;

                RestoreMilestoneTalents(data.milestoneChoices, data.milestoneBindings);
                TalentSystem.ImportRanks(this, data.talentRanksA, data.talentRanksB);
                RestoreMountSelection(data.mount);
                RestoreEquipment(data); Stats.Restore();
                foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (!HasDefeated(enemy.enemyId)) enemy.ResetForLoad();
                    else enemy.gameObject.SetActive(false);
                RefreshAdventureWorld();
                if (WorldRegions.Expanded && data.worldRevision < 1 && data.corpses != null)
                    foreach (var corpse in data.corpses)
                        if (corpse != null) corpse.position = ClampAboveGround(WorldRegions.LegacyPosition(corpse.position, CharacterRace));
                if (LootDropSystem.Instance) LootDropSystem.Instance.RestoreCorpses(data.corpses);
                string activeZone = ZoneManager.GetZoneName(transform.position);
                Message = $"Welcome back to {activeZone}. " + QuestObjective;
            }
            catch (Exception exception) { Message = "Could not load the save. Starting a fresh session."; Debug.LogWarning("Adventure load: " + exception.Message); }
        }
        void OnApplicationQuit() { if (Stats && !FrontendMenu.IsOpen) SaveAdventure(false); }
    }
}
