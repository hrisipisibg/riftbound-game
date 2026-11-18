using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Riftbound
{
    public class FactionQuestSystem : MonoBehaviour
    {
        public static FactionQuestSystem Instance { get; private set; }

        [Serializable]
        public class QuestDef
        {
            public string id;
            public string giverNpc;
            public string title;
            public string faction; // "Horde", "Alliance", "Any"
            public string zone;
            [TextArea] public string description;
            public string targetType; // "defeat_enemy", "collect", "talk_npc"
            public string targetId;   // enemyId substring or item/crate id or target NPC
            public string targetName;
            public int requiredCount = 3;
            public int rewardGold = 35;
            public int rewardXP = 90;
            public string rewardItem = "";
            public string prerequisiteQuestId = "";
            public string nextQuestId = "";
            public Vector3 targetLocation;
        }

        [Serializable]
        public class FactionQuestSaveData
        {
            public string activeQuestId = "";
            public int currentProgress = 0;
            public List<string> completedQuests = new List<string>();
        }

        public string ActiveQuestId { get; private set; } = "";
        public int CurrentProgress { get; private set; } = 0;
        readonly HashSet<string> completedQuests = new HashSet<string>();

        static readonly Dictionary<string, QuestDef> catalog = new Dictionary<string, QuestDef>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            catalog.Clear();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                if (Application.isPlaying) Destroy(this); else DestroyImmediate(this);
                return;
            }
            Instance = this;
            BuildCatalog();
        }

        void Start()
        {
            LoadData();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureSystem(PlayerController player)
        {
            if (!player) return;
            var sys = player.GetComponent<FactionQuestSystem>();
            if (!sys) sys = player.gameObject.AddComponent<FactionQuestSystem>();
            Instance = sys;
        }

        void BuildCatalog()
        {
            catalog.Clear();
            Add(new QuestDef
            {
                id = "frontier_cartography", giverNpc = ExplorationSites.CartographerName,
                title = "Charting the Frontier", faction = "Any", zone = "Greenhollow Post",
                description = "Survey Westbank Overlook by the western river, Bloodrock Vista in the eastern frontier, and Wyrmcrest Lookout on the summit road. Return to Cartographer Elara at Greenhollow Post. Earlier discoveries count.",
                targetType = "discover_landmark", targetId = "frontier-vistas", targetName = "Frontier survey sites",
                requiredCount = 3, rewardGold = 75, rewardXP = 150, rewardItem = "travel-charm",
                targetLocation = new Vector3(-241, 6, 44), nextQuestId = "river_reaver_bounty"
            });
            Add(new QuestDef
            {
                id = "river_reaver_bounty", giverNpc = ExplorationSites.CartographerName,
                title = "Silence the River Reavers", faction = "Any", zone = "Willowmere Wilds",
                description = "Your survey exposed a River Reaver camp on the western bank, north of Willow Crossing. Captain Selvek and two guards threaten the road. Defeat the captain, then return to Elara at Greenhollow Post. His lockbox can be opened after his defeat.",
                targetType = "defeat_enemy", targetId = RiverReaverCamp.CaptainId, targetName = "Captain Selvek",
                requiredCount = 1, rewardGold = 60, rewardXP = 140, rewardItem = "riverwarden-signet",
                prerequisiteQuestId = "frontier_cartography", targetLocation = RiverReaverCamp.Position
            });

            // ==========================================
            // 1. EMBERWATCH OUTPOST (HUMAN / ALLIANCE STARTER VILLAGE)
            // ==========================================
            Add(new QuestDef
            {
                id = "emberwatch_raider_cull",
                giverNpc = "Captain Halford",
                title = "Defending Emberwatch Village",
                faction = "Alliance",
                zone = "Emberwatch Outpost",
                description = "Raiders and outrunners prowl just beyond our north gatehouse! Venture onto the Ashwood road, slay 3 Raiders threatening our village, and report back to the gate.",
                targetType = "defeat_enemy",
                targetId = "raider",
                targetName = "Ashwood Raiders",
                requiredCount = 3,
                rewardGold = 30,
                rewardXP = 85,
                prerequisiteQuestId = "",
                nextQuestId = "emberwatch_armory_check",
                targetLocation = new Vector3(0f, 1f, 46f)
            });

            Add(new QuestDef
            {
                id = "emberwatch_armory_check",
                giverNpc = "Captain Halford",
                title = "Steel from the Village Forge",
                faction = "Alliance",
                zone = "Emberwatch Outpost",
                description = "Well fought, recruit! Before you ride for the capital, visit Blacksmith Torvald at the Emberwatch Smithy to outfit your shield arm and collect our garrison dispatch.",
                targetType = "talk_npc",
                targetId = "Blacksmith Torvald",
                targetName = "Blacksmith Torvald",
                requiredCount = 1,
                rewardGold = 25,
                rewardXP = 70,
                rewardItem = "iron-buckler",
                prerequisiteQuestId = "emberwatch_raider_cull",
                nextQuestId = "emberwatch_to_highspire",
                targetLocation = new Vector3(9f, 0f, 8f)
            });

            Add(new QuestDef
            {
                id = "emberwatch_to_highspire",
                giverNpc = "Blacksmith Torvald",
                title = "Road to Highspire Keep",
                faction = "Alliance",
                zone = "Highspire Keep",
                description = "Your gear is tempered for the road! Follow the Cobblestone Highway southwest through the Imperial Gatehouse — or speak with Gryphon Master Ryan — to reach our grand capital, Highspire Keep. Deliver this seal to Grand Duke Ronald!",
                targetType = "talk_npc",
                targetId = "Grand Duke Ronald",
                targetName = "Grand Duke Ronald",
                requiredCount = 1,
                rewardGold = 50,
                rewardXP = 120,
                rewardItem = "alliance-war-banner",
                prerequisiteQuestId = "emberwatch_armory_check",
                nextQuestId = "highspire_royal_council",
                targetLocation = new Vector3(-200f, 12f, -60f)
            });

            // ==========================================
            // 2. CAMP BLOODROCK (ORC / HORDE STARTER VILLAGE)
            // ==========================================
            Add(new QuestDef
            {
                id = "bloodrock_cull",
                giverNpc = "Warlord Gorgash",
                title = "Bloodrock Vanguard",
                faction = "Horde",
                zone = "Camp Bloodrock",
                description = "The Redridge canyons outside our village palisades swarm with rogue deserters. Cull 3 Crag Orc Sentries beyond the gates and show them the strength of the Horde!",
                targetType = "defeat_enemy",
                targetId = "redridge-orc",
                targetName = "Crag Orc Sentries",
                requiredCount = 3,
                rewardGold = 35,
                rewardXP = 90,
                prerequisiteQuestId = "",
                nextQuestId = "bloodrock_munitions",
                targetLocation = new Vector3(304f, 18f, 214f)
            });

            Add(new QuestDef
            {
                id = "bloodrock_munitions",
                giverNpc = "Warlord Gorgash",
                title = "Canyon Munitions",
                faction = "Horde",
                zone = "Camp Bloodrock",
                description = "Our supply caravans from Rustwater were ambushed along the canyon road. Slay 3 Canyon Marauders outside the camp and reclaim our stolen provisions!",
                targetType = "defeat_enemy",
                targetId = "redridge",
                targetName = "Canyon Marauders",
                requiredCount = 3,
                rewardGold = 50,
                rewardXP = 120,
                prerequisiteQuestId = "bloodrock_cull",
                nextQuestId = "bloodrock_warlord",
                targetLocation = new Vector3(260f, 18f, 190f)
            });

            Add(new QuestDef
            {
                id = "bloodrock_warlord",
                giverNpc = "Warlord Gorgash",
                title = "Chieftain of the Crag",
                faction = "Horde",
                zone = "Camp Bloodrock",
                description = "Outcast Chieftain Karg mocks our clan from the eastern bluff outside the village. Slay the Outcast Chieftain and claim the Bloodrock Waraxe as your prize!",
                targetType = "defeat_enemy",
                targetId = "redridge-outcast-boss",
                targetName = "Outcast Chieftain Karg",
                requiredCount = 1,
                rewardGold = 80,
                rewardXP = 200,
                rewardItem = "bloodrock-waraxe",
                prerequisiteQuestId = "bloodrock_munitions",
                nextQuestId = "bloodrock_stronghold",
                targetLocation = new Vector3(306f, 18f, 226f)
            });

            Add(new QuestDef
            {
                id = "bloodrock_stronghold",
                giverNpc = "Warlord Gorgash",
                title = "March to Gor'Kresh",
                faction = "Horde",
                zone = "Gor'Kresh Stronghold",
                description = "You have proven your honor in battle, warrior! Follow the carved war-road northeast to our mighty capital fortress, Gor'Kresh Stronghold, and report to Overlord Mor'gok!",
                targetType = "talk_npc",
                targetId = "Overlord Mor'gok",
                targetName = "Overlord Mor'gok",
                requiredCount = 1,
                rewardGold = 55,
                rewardXP = 130,
                rewardItem = "horde-war-banner",
                prerequisiteQuestId = "bloodrock_warlord",
                nextQuestId = "gorkresh_war_forge",
                targetLocation = new Vector3(340f, 25f, 290f)
            });

            // ==========================================
            // 3. DEATHKNELL HOLLOW (UNDEAD STARTER VILLAGE)
            // ==========================================
            Add(new QuestDef
            {
                id = "deathknell_ghouls",
                giverNpc = "Deathstalker Morzan",
                title = "The Restless Dead",
                faction = "Horde",
                zone = "Deathknell Hollow",
                description = "Mindless ghouls claw through the outer cemetery beyond Deathknell's iron gates. Destroy 3 Wandering Ghouls so our apothecaries may work in peace.",
                targetType = "defeat_enemy",
                targetId = "deathknell-ghoul",
                targetName = "Wandering Ghouls",
                requiredCount = 3,
                rewardGold = 30,
                rewardXP = 85,
                prerequisiteQuestId = "",
                nextQuestId = "deathknell_cauldron",
                targetLocation = new Vector3(-216f, 12f, 188f)
            });

            Add(new QuestDef
            {
                id = "deathknell_cauldron",
                giverNpc = "Deathstalker Morzan",
                title = "Blight of Deathknell",
                faction = "Horde",
                zone = "Deathknell Hollow",
                description = "Purge 3 more shambling abominations around the haunted outskirts of Deathknell, and prove the will of the Forsaken.",
                targetType = "defeat_enemy",
                targetId = "ghoul",
                targetName = "Crypt Abominations",
                requiredCount = 3,
                rewardGold = 45,
                rewardXP = 110,
                rewardItem = "travel-charm",
                prerequisiteQuestId = "deathknell_ghouls",
                nextQuestId = "deathknell_shadow_rite",
                targetLocation = new Vector3(-220f, 12f, 198f)
            });

            Add(new QuestDef
            {
                id = "deathknell_shadow_rite",
                giverNpc = "Deathstalker Morzan",
                title = "Whispers in the Abbey",
                faction = "Horde",
                zone = "Deathknell Hollow",
                description = "Step inside the Deathknell Abbey square and receive the dark benediction from Shadow Priestess Alyssa before departing our hollow.",
                targetType = "talk_npc",
                targetId = "Shadow Priestess Alyssa",
                targetName = "Shadow Priestess Alyssa",
                requiredCount = 1,
                rewardGold = 25,
                rewardXP = 70,
                prerequisiteQuestId = "deathknell_cauldron",
                nextQuestId = "deathknell_to_gorkresh",
                targetLocation = new Vector3(-237f, 12f, 181f)
            });

            Add(new QuestDef
            {
                id = "deathknell_to_gorkresh",
                giverNpc = "Shadow Priestess Alyssa",
                title = "Emissary to Gor'Kresh Stronghold",
                faction = "Horde",
                zone = "Gor'Kresh Stronghold",
                description = "Our pact with the Warchief binds us in common cause. Journey east across the frontier into the Redridge Canyons and present our dark missive to Overlord Mor'gok in Gor'Kresh Stronghold!",
                targetType = "talk_npc",
                targetId = "Overlord Mor'gok",
                targetName = "Overlord Mor'gok",
                requiredCount = 1,
                rewardGold = 60,
                rewardXP = 140,
                rewardItem = "horde-war-banner",
                prerequisiteQuestId = "deathknell_shadow_rite",
                nextQuestId = "gorkresh_war_forge",
                targetLocation = new Vector3(340f, 25f, 290f)
            });

            // ==========================================
            // 4. RUSTWATER SCRAPWORKS (GOBLIN STARTER VILLAGE)
            // ==========================================
            Add(new QuestDef
            {
                id = "rustwater_scavengers",
                giverNpc = "Boss Griznak",
                title = "Scrap & Scavengers",
                faction = "Horde",
                zone = "Rustwater Scrapworks",
                description = "Rogue scavengers outside our trade town are looting high-grade brass! Knock some sense into 3 Rustwater Scavengers beyond the workshops.",
                targetType = "defeat_enemy",
                targetId = "rustwater-scavenger",
                targetName = "Rustwater Scavengers",
                requiredCount = 3,
                rewardGold = 35,
                rewardXP = 85,
                prerequisiteQuestId = "",
                nextQuestId = "rustwater_machinery",
                targetLocation = new Vector3(264f, 14f, 156f)
            });

            Add(new QuestDef
            {
                id = "rustwater_machinery",
                giverNpc = "Boss Griznak",
                title = "Turbine Protection",
                faction = "Horde",
                zone = "Rustwater Scrapworks",
                description = "Clear out 3 more trespassers lurking on the canyon outskirts. Time is money, and the Bilgewater Cartel pays on delivery!",
                targetType = "defeat_enemy",
                targetId = "scavenger",
                targetName = "Scrapyard Trespassers",
                requiredCount = 3,
                rewardGold = 50,
                rewardXP = 115,
                rewardItem = "copper-dagger",
                prerequisiteQuestId = "rustwater_scavengers",
                nextQuestId = "rustwater_blast_check",
                targetLocation = new Vector3(260f, 14f, 166f)
            });

            Add(new QuestDef
            {
                id = "rustwater_blast_check",
                giverNpc = "Boss Griznak",
                title = "Fizzlebang's Munitions Manifest",
                faction = "Horde",
                zone = "Rustwater Scrapworks",
                description = "Head over to the Tinkering Barn and pick up the signed munitions manifest from Chief Engineer Tinker Fizzlebang.",
                targetType = "talk_npc",
                targetId = "Tinker Fizzlebang",
                targetName = "Tinker Fizzlebang",
                requiredCount = 1,
                rewardGold = 30,
                rewardXP = 75,
                prerequisiteQuestId = "rustwater_machinery",
                nextQuestId = "rustwater_to_gorkresh",
                targetLocation = new Vector3(237f, 14f, 151f)
            });

            Add(new QuestDef
            {
                id = "rustwater_to_gorkresh",
                giverNpc = "Tinker Fizzlebang",
                title = "Bilgewater Contract: Gor'Kresh",
                faction = "Horde",
                zone = "Gor'Kresh Stronghold",
                description = "The Horde Warlord ordered three crates of boom-powder! Follow the canyon road northeast past Camp Bloodrock up to Gor'Kresh Stronghold and seal the deal with Overlord Mor'gok!",
                targetType = "talk_npc",
                targetId = "Overlord Mor'gok",
                targetName = "Overlord Mor'gok",
                requiredCount = 1,
                rewardGold = 60,
                rewardXP = 135,
                rewardItem = "horde-war-banner",
                prerequisiteQuestId = "rustwater_blast_check",
                nextQuestId = "gorkresh_war_forge",
                targetLocation = new Vector3(340f, 25f, 290f)
            });

            // ==========================================
            // 5. FROSTPEAK FOOTHILLS (DWARF STARTER VILLAGE)
            // ==========================================
            Add(new QuestDef
            {
                id = "frostpeak_troggs",
                giverNpc = "Thane Baelor Ironbeard",
                title = "Troggs of the Foothills",
                faction = "Alliance",
                zone = "Frostpeak Foothills",
                description = "Vile troggs have tunneled up outside our mountain hamlet! Slay 3 Frostpeak Trogg Scouts beyond the stone walls and defend Ironbeard's hearth.",
                targetType = "defeat_enemy",
                targetId = "frostpeak-trogg",
                targetName = "Frostpeak Troggs",
                requiredCount = 3,
                rewardGold = 30,
                rewardXP = 85,
                prerequisiteQuestId = "",
                nextQuestId = "frostpeak_forge",
                targetLocation = new Vector3(-72f, 16f, -98f)
            });

            Add(new QuestDef
            {
                id = "frostpeak_forge",
                giverNpc = "Thane Baelor Ironbeard",
                title = "Defenders of Iron",
                faction = "Alliance",
                zone = "Frostpeak Foothills",
                description = "Drive 3 more trogg tunnelers back underground along the snowy ridge. For Khaz Modan and the High King!",
                targetType = "defeat_enemy",
                targetId = "trogg",
                targetName = "Trogg Tunnelers",
                requiredCount = 3,
                rewardGold = 45,
                rewardXP = 110,
                rewardItem = "iron-buckler",
                prerequisiteQuestId = "frostpeak_troggs",
                nextQuestId = "frostpeak_armory_check",
                targetLocation = new Vector3(-76f, 16f, -106f)
            });

            Add(new QuestDef
            {
                id = "frostpeak_armory_check",
                giverNpc = "Thane Baelor Ironbeard",
                title = "Tempered at the Great Anvil",
                faction = "Alliance",
                zone = "Frostpeak Foothills",
                description = "Well struck, kin! Step over to the Frostpeak Smithy and speak with Master Armorer Gundrak Stonehammer to collect our clan's tribute for the Alliance capital.",
                targetType = "talk_npc",
                targetId = "Gundrak Stonehammer",
                targetName = "Gundrak Stonehammer",
                requiredCount = 1,
                rewardGold = 25,
                rewardXP = 70,
                prerequisiteQuestId = "frostpeak_forge",
                nextQuestId = "frostpeak_to_highspire",
                targetLocation = new Vector3(-98f, 16f, -91f)
            });

            Add(new QuestDef
            {
                id = "frostpeak_to_highspire",
                giverNpc = "Gundrak Stonehammer",
                title = "Envoy of Clan Ironbeard",
                faction = "Alliance",
                zone = "Highspire Keep",
                description = "Follow the Frostpeak mountain spur west onto the Cobblestone Highway and enter the grand gates of Highspire Keep. Present Clan Ironbeard's oath to Grand Duke Ronald!",
                targetType = "talk_npc",
                targetId = "Grand Duke Ronald",
                targetName = "Grand Duke Ronald",
                requiredCount = 1,
                rewardGold = 55,
                rewardXP = 130,
                rewardItem = "alliance-war-banner",
                prerequisiteQuestId = "frostpeak_armory_check",
                nextQuestId = "highspire_royal_council",
                targetLocation = new Vector3(-200f, 12f, -60f)
            });

            // ==========================================
            // 6. SILVERGLEN SANCTUM (HIGH ELF STARTER VILLAGE)
            // ==========================================
            Add(new QuestDef
            {
                id = "silverglen_sprites",
                giverNpc = "Sunwarden Thalorien",
                title = "Purification of the Grove",
                faction = "Alliance",
                zone = "Silverglen Sanctum",
                description = "Arcane instability has tainted the woodland mana flows outside our sanctum. Banish 3 Corrupted Mana Sprites beyond the Moonwell pavilions.",
                targetType = "defeat_enemy",
                targetId = "silverglen-mana-sprite",
                targetName = "Corrupted Mana Sprites",
                requiredCount = 3,
                rewardGold = 30,
                rewardXP = 85,
                prerequisiteQuestId = "",
                nextQuestId = "silverglen_attunement",
                targetLocation = new Vector3(-174f, 15f, 82f)
            });

            Add(new QuestDef
            {
                id = "silverglen_attunement",
                giverNpc = "Sunwarden Thalorien",
                title = "Radiance of the Moonwell",
                faction = "Alliance",
                zone = "Silverglen Sanctum",
                description = "Purge 3 more corrupted anomalies lingering near the outer glade. May the eternal light of Silvermoon guide your hand.",
                targetType = "defeat_enemy",
                targetId = "sprite",
                targetName = "Corrupted Anomalies",
                requiredCount = 3,
                rewardGold = 45,
                rewardXP = 110,
                rewardItem = "travel-charm",
                prerequisiteQuestId = "silverglen_sprites",
                nextQuestId = "silverglen_ley_counsel",
                targetLocation = new Vector3(-170f, 15f, 92f)
            });

            Add(new QuestDef
            {
                id = "silverglen_ley_counsel",
                giverNpc = "Sunwarden Thalorien",
                title = "The Magister's Counsel",
                faction = "Alliance",
                zone = "Silverglen Sanctum",
                description = "The grove breathes freely once more. Consult Arcane Scholar Magister Kaelen inside the Silverglen Athenaeum regarding the leyline readings.",
                targetType = "talk_npc",
                targetId = "Magister Kaelen",
                targetName = "Magister Kaelen",
                requiredCount = 1,
                rewardGold = 25,
                rewardXP = 70,
                prerequisiteQuestId = "silverglen_attunement",
                nextQuestId = "silverglen_to_highspire",
                targetLocation = new Vector3(-153f, 15f, 77f)
            });

            Add(new QuestDef
            {
                id = "silverglen_to_highspire",
                giverNpc = "Magister Kaelen",
                title = "Missive from the Moonwell",
                faction = "Alliance",
                zone = "Highspire Keep",
                description = "The ley disturbances trace back toward Duskwood, and the Alliance High Command must be warned. Travel south to our allied capital, Highspire Keep, and speak with Grand Duke Ronald!",
                targetType = "talk_npc",
                targetId = "Grand Duke Ronald",
                targetName = "Grand Duke Ronald",
                requiredCount = 1,
                rewardGold = 55,
                rewardXP = 130,
                rewardItem = "alliance-war-banner",
                prerequisiteQuestId = "silverglen_ley_counsel",
                nextQuestId = "highspire_royal_council",
                targetLocation = new Vector3(-200f, 12f, -60f)
            });

            // ==========================================
            // 7. GOR'KRESH STRONGHOLD (HORDE CAPITAL CAMPAIGN)
            // ==========================================
            Add(new QuestDef
            {
                id = "gorkresh_war_forge",
                giverNpc = "Overlord Mor'gok",
                title = "Irons of Gor'Kresh",
                faction = "Horde",
                zone = "Gor'Kresh Stronghold",
                description = "Welcome to the mighty fortress of Gor'Kresh Stronghold! Before marching into the volcanic badlands, inspect our capital's eastern district and speak with Blacksmith Garok at the Great War-Forge.",
                targetType = "talk_npc",
                targetId = "Blacksmith Garok",
                targetName = "Blacksmith Garok",
                requiredCount = 1,
                rewardGold = 40,
                rewardXP = 100,
                prerequisiteQuestId = "",
                nextQuestId = "gorkresh_caldera_threat",
                targetLocation = new Vector3(356f, 25f, 296f)
            });

            Add(new QuestDef
            {
                id = "gorkresh_caldera_threat",
                giverNpc = "Overlord Mor'gok",
                title = "Wrath of the Caldera",
                faction = "Horde",
                zone = "Redridge Volcanic Crater",
                description = "The molten rift in the sulfurous caldera threatens our western ramparts. Venture into the Redridge Volcanic Crater, defeat 3 fiery Elementals or Scorpions, and secure the perimeter!",
                targetType = "defeat_enemy",
                targetId = "cinder",
                targetName = "Caldera Elementals",
                requiredCount = 3,
                rewardGold = 75,
                rewardXP = 180,
                prerequisiteQuestId = "gorkresh_war_forge",
                nextQuestId = "gorkresh_wyrmcrest_scout",
                targetLocation = new Vector3(180f, 22f, 300f)
            });

            Add(new QuestDef
            {
                id = "gorkresh_wyrmcrest_scout",
                giverNpc = "Overlord Mor'gok",
                title = "Ascent of the Red Wyrm",
                faction = "Horde",
                zone = "Wyrmcrest Crags",
                description = "The shadow of the great dragon falls over Gor'Kresh. Scale the winding mountain switchbacks above our northern gatehouse and slay 3 Mountain Wyrmkin!",
                targetType = "defeat_enemy",
                targetId = "wyrm",
                targetName = "Mountain Wyrmkin",
                requiredCount = 3,
                rewardGold = 95,
                rewardXP = 220,
                prerequisiteQuestId = "gorkresh_caldera_threat",
                nextQuestId = "gorkresh_apex_clash",
                targetLocation = new Vector3(330f, 48f, 425f)
            });

            Add(new QuestDef
            {
                id = "gorkresh_apex_clash",
                giverNpc = "Overlord Mor'gok",
                title = "Dominion of the Skies",
                faction = "Horde",
                zone = "Wyrmcrest Summit",
                description = "Ascend to the highest summit roost of Mount Wyrmcrest and slay the Apex Drake! Bring back its fiery scale to Gor'Kresh as proof of Horde supremacy!",
                targetType = "defeat_enemy",
                targetId = "vermithrax-boss",
                targetName = "Apex Sovereign Drake",
                requiredCount = 1,
                rewardGold = 150,
                rewardXP = 450,
                rewardItem = "ember-drake-scale",
                prerequisiteQuestId = "gorkresh_wyrmcrest_scout",
                nextQuestId = "",
                targetLocation = new Vector3(250f, 75f, 510f)
            });

            // ==========================================
            // 8. HIGHSPIRE KEEP (ALLIANCE CAPITAL CAMPAIGN)
            // ==========================================
            Add(new QuestDef
            {
                id = "alliance_highspire_dispatch",
                giverNpc = "Abe",
                title = "The Highspire Dispatch",
                faction = "Alliance",
                zone = "Highspire Keep",
                description = "Our roads are safe for now, but Grand Duke Ronald must receive our scouting reports. Ride along the southern Cobblestone Highway to Highspire Keep and report to the Duke!",
                targetType = "talk_npc",
                targetId = "Grand Duke Ronald",
                targetName = "Grand Duke Ronald",
                requiredCount = 1,
                rewardGold = 45,
                rewardXP = 110,
                rewardItem = "alliance-war-banner",
                prerequisiteQuestId = "",
                nextQuestId = "highspire_royal_council",
                targetLocation = new Vector3(-200f, 12f, -60f)
            });

            Add(new QuestDef
            {
                id = "highspire_royal_council",
                giverNpc = "Grand Duke Ronald",
                title = "Council of Highspire Keep",
                faction = "Alliance",
                zone = "Highspire Keep",
                description = "Welcome to the royal citadel of Highspire Keep, champion! Walk across our cobbled courtyard to the Arcane Sanctum and consult Royal Arcanist Archmage Karen on our kingdom's defenses.",
                targetType = "talk_npc",
                targetId = "Archmage Karen",
                targetName = "Archmage Karen",
                requiredCount = 1,
                rewardGold = 40,
                rewardXP = 100,
                prerequisiteQuestId = "",
                nextQuestId = "highspire_foothills_convoy",
                targetLocation = new Vector3(-184f, 12f, -66f)
            });

            Add(new QuestDef
            {
                id = "highspire_foothills_convoy",
                giverNpc = "Grand Duke Ronald",
                title = "Dwarven Iron Alliance",
                faction = "Alliance",
                zone = "Frostpeak Foothills",
                description = "Our front-line paladins require forged dwarven weapons from our mountain allies. Follow the Frostpeak spur east into the snowy foothills and speak with Thane Baelor Ironbeard!",
                targetType = "talk_npc",
                targetId = "Thane Baelor Ironbeard",
                targetName = "Thane Baelor Ironbeard",
                requiredCount = 1,
                rewardGold = 55,
                rewardXP = 130,
                prerequisiteQuestId = "highspire_royal_council",
                nextQuestId = "highspire_duskwood_recon",
                targetLocation = new Vector3(-95f, 16f, -90f)
            });

            Add(new QuestDef
            {
                id = "highspire_duskwood_recon",
                giverNpc = "Grand Duke Ronald",
                title = "Shadows Over Duskwood",
                faction = "Alliance",
                zone = "Duskwood Cathedral",
                description = "Disturbing reports arrive from the western forest across the Willowmere River bridge. Cross into Duskwood, reach the ruined Cathedral grounds, and speak with Inquisitor Lucian!",
                targetType = "talk_npc",
                targetId = "Inquisitor Lucian",
                targetName = "Inquisitor Lucian",
                requiredCount = 1,
                rewardGold = 65,
                rewardXP = 150,
                prerequisiteQuestId = "highspire_foothills_convoy",
                nextQuestId = "duskwood_crypt_purge",
                targetLocation = new Vector3(-220f, 18f, 260f)
            });

            Add(new QuestDef
            {
                id = "duskwood_crypt_purge",
                giverNpc = "Inquisitor Lucian",
                title = "Purging the Scarlet Crypts",
                faction = "Alliance",
                zone = "The Scarlet Crypts",
                description = "Dark revenants claw through the catacombs beneath our ancient monastery. Step through the crimson portal into the Scarlet Crypts, purge 3 undead horrors, and purify the holy halls!",
                targetType = "defeat_enemy",
                targetId = "ghoul",
                targetName = "Crypt Horrors",
                requiredCount = 3,
                rewardGold = 85,
                rewardXP = 210,
                rewardItem = "scarlet-blade",
                prerequisiteQuestId = "highspire_duskwood_recon",
                nextQuestId = "alliance_wyrmcrest_summit",
                targetLocation = new Vector3(-400f, -40f, 200f)
            });

            Add(new QuestDef
            {
                id = "alliance_wyrmcrest_summit",
                giverNpc = "Inquisitor Lucian",
                title = "Apex of Mount Wyrmcrest",
                faction = "Alliance",
                zone = "Wyrmcrest Summit",
                description = "The supreme menace perches above the clouds. Climb the winding switchbacks of Mount Wyrmcrest and slay the Apex Sovereign Drake to secure everlasting peace for the realm!",
                targetType = "defeat_enemy",
                targetId = "vermithrax-boss",
                targetName = "Apex Sovereign Drake",
                requiredCount = 1,
                rewardGold = 150,
                rewardXP = 450,
                rewardItem = "ember-drake-scale",
                prerequisiteQuestId = "duskwood_crypt_purge",
                nextQuestId = "",
                targetLocation = new Vector3(250f, 75f, 510f)
            });
        }

        static void Add(QuestDef q) => catalog[q.id] = q;

        public static QuestDef GetQuest(string id)
        {
            if (catalog.Count == 0 && Instance) Instance.BuildCatalog();
            return catalog.TryGetValue(id, out var def) ? def : null;
        }

        public bool IsQuestCompleted(string id) => completedQuests.Contains(id);

        public QuestDef GetActiveQuest() => string.IsNullOrEmpty(ActiveQuestId) ? null : GetQuest(ActiveQuestId);

        public bool HasAvailableQuest(string npcName, PlayerController player)
        {
            if (catalog.Count == 0) BuildCatalog();
            if (!string.IsNullOrEmpty(ActiveQuestId)) return false;

            foreach (var kvp in catalog)
            {
                var q = kvp.Value;
                if (!string.Equals(q.giverNpc, npcName, StringComparison.OrdinalIgnoreCase)) continue;
                if (completedQuests.Contains(q.id)) continue;

                // Faction check
                bool isHordePlayer = player && (player.CharacterRace == "Brute" || player.CharacterRace == "Orc" || player.CharacterRace == "Goblin" || player.CharacterRace == "Undead");
                if (q.faction == "Horde" && !isHordePlayer) continue;
                if (q.faction == "Alliance" && isHordePlayer) continue;

                // Check explicit prerequisite quest if configured
                if (!string.IsNullOrEmpty(q.prerequisiteQuestId) && !completedQuests.Contains(q.prerequisiteQuestId))
                    continue;

                return true;
            }
            return false;
        }

        public QuestDef GetAvailableQuestForNPC(string npcName, PlayerController player)
        {
            if (catalog.Count == 0) BuildCatalog();
            if (!string.IsNullOrEmpty(ActiveQuestId)) return null;

            foreach (var kvp in catalog)
            {
                var q = kvp.Value;
                if (!string.Equals(q.giverNpc, npcName, StringComparison.OrdinalIgnoreCase)) continue;
                if (completedQuests.Contains(q.id)) continue;

                bool isHordePlayer = player && (player.CharacterRace == "Brute" || player.CharacterRace == "Orc" || player.CharacterRace == "Goblin" || player.CharacterRace == "Undead");
                if (q.faction == "Horde" && !isHordePlayer) continue;
                if (q.faction == "Alliance" && isHordePlayer) continue;

                if (!string.IsNullOrEmpty(q.prerequisiteQuestId) && !completedQuests.Contains(q.prerequisiteQuestId))
                    continue;

                return q;
            }
            return null;
        }

        public bool HasActiveQuestToComplete(string npcName, PlayerController player)
        {
            var q = GetActiveQuest();
            if (q == null) return false;

            // Talk quests can be turned in to targetId NPC, otherwise to original giver
            string turnInNpc = q.targetType == "talk_npc" ? q.targetId : q.giverNpc;
            if (!string.Equals(turnInNpc, npcName, StringComparison.OrdinalIgnoreCase)) return false;

            return IsActiveQuestReady();
        }

        public bool IsActiveQuestReady()
        {
            var q = GetActiveQuest();
            if (q == null) return false;
            return CurrentProgress >= q.requiredCount;
        }

        public void AcceptQuest(QuestDef quest, PlayerController player)
        {
            if (quest == null) return;
            ActiveQuestId = quest.id;
            CurrentProgress = 0;

            AudioEffects.PlayQuestAccept();
            ChatSystem.LogQuest($"<color=#ffaa20><b>[FACTION QUEST ACCEPTED]</b></color> {quest.title}");
            if (player)
            {
                player.Message = $"Accepted: {quest.title} ({quest.targetName})";
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2f, $"★ QUEST: {quest.title} ★", new Color(1f, 0.85f, 0.2f), true);
            }

            if (MinimapRadar.Instance && quest.targetLocation.sqrMagnitude > 1f)
            {
                MinimapRadar.SetWaypoint(quest.targetLocation, quest.title);
            }

            if (quest.targetType == "discover_landmark") UpdateSurvey(player, true);
            else if (player && quest.targetType == "defeat_enemy" && quest.requiredCount == 1 &&
                ExactBossTarget(quest.targetId) && player.HasDefeated(quest.targetId))
            {
                CurrentProgress = 1;
                TrackTurnIn(quest);
            }

            SaveData();
        }

        public void OnLandmarkDiscovered(PlayerController player)
        {
            if (GetActiveQuest()?.targetType != "discover_landmark") return;
            UpdateSurvey(player, true);
            SaveData();
        }

        void UpdateSurvey(PlayerController player, bool track)
        {
            if (!player) return;
            CurrentProgress = 0;
            string next = null;
            foreach (string id in ExplorationSites.SurveyIds)
                if (player.HasLandmark(id)) CurrentProgress++; else if (next == null) next = id;
            if (!track) return;
            if (next != null)
            {
                foreach (var landmark in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                    if (landmark.landmarkId == next) { MinimapRadar.SetWaypoint(landmark.transform.position, landmark.displayName); break; }
            }
            else
            {
                foreach (var npc in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                    if (npc.npcName == ExplorationSites.CartographerName) { MinimapRadar.SetWaypoint(npc.transform.position, "Return to " + npc.npcName); break; }
            }
        }

        public void CompleteQuest(PlayerController player)
        {
            var q = GetActiveQuest();
            if (q == null || !IsActiveQuestReady()) return;

            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestCompleteFactionQuestServerValidated(q.id);
                return;
            }

            int goldGain = q.rewardGold;
            if (player && player.CharacterRace == "Human") goldGain = Mathf.RoundToInt(goldGain * 1.25f);
            FinalizeQuestCompletion(player, q, goldGain, q.rewardXP, authoritativeGoldAlreadySet: false);
            SaveData();
        }

        public void ApplyAuthorizedComplete(PlayerController player, string questId, int newServerGold, int goldGain, int xpGain)
        {
            var q = GetQuest(questId);
            if (q == null || completedQuests.Contains(questId)) return;
            if (player) player.SetAuthoritativeGold(newServerGold);
            FinalizeQuestCompletion(player, q, goldGain, xpGain, authoritativeGoldAlreadySet: true);
        }

        void FinalizeQuestCompletion(PlayerController player, QuestDef q, int goldGain, int xpGain, bool authoritativeGoldAlreadySet)
        {
            completedQuests.Add(q.id);
            string completedTitle = q.title;
            string rewardItem = q.rewardItem;

            ActiveQuestId = "";
            CurrentProgress = 0;

            if (player)
            {
                if (!authoritativeGoldAlreadySet) player.AddGoldDirect(goldGain);
                player.AddExperience(xpGain);
                if (!string.IsNullOrEmpty(rewardItem))
                {
                    player.GrantItem(rewardItem, true);
                    ChatSystem.LogQuest($"<color=#70df80>Received Equipment Reward: [{ItemCatalog.Get(rewardItem)?.name ?? rewardItem}]!</color>");
                }
                if (authoritativeGoldAlreadySet && player.Stats)
                    player.Stats.SetReplicatedHealth(player.Stats.maxHealth, player.Stats.maxHealth);
                else if (player.Stats)
                    player.Stats.Heal(player.Stats.maxHealth);
                player.Message = $"Quest Complete: {completedTitle}! (+{goldGain} Gold, +{xpGain} XP)";
                CombatTextManager.Spawn(player.transform.position + Vector3.up * 2.5f, $"★ QUEST COMPLETE: {completedTitle} ★", new Color(0.4f, 1f, 0.5f), true);
            }

            AudioEffects.PlayQuestComplete();
            ZoneBanner.Show("QUEST COMPLETED", $"{completedTitle}\n+{goldGain} Gold  +{xpGain} XP");
            ChatSystem.LogQuest($"<color=#ffd700><b>[QUEST COMPLETE]</b></color> {completedTitle}! Rewards: {goldGain} Gold, {xpGain} XP.");

            if (MinimapRadar.Instance)
            {
                MinimapRadar.ClearWaypoint();
            }
        }

        public void OnEnemyDefeated(EnemyController enemy)
        {
            var q = GetActiveQuest();
            if (q == null || q.targetType != "defeat_enemy" || IsActiveQuestReady()) return;

            string eId = enemy.enemyId?.ToLowerInvariant() ?? "";
            string eName = enemy.GetComponent<Combatant>()?.displayName?.ToLowerInvariant() ?? "";
            string tId = q.targetId.ToLowerInvariant();

            bool exactTarget = ExactBossTarget(tId);
            bool matches = exactTarget ? eId == tId : eId.Contains(tId) || eName.Contains(tId);
            if (matches)
            {
                CurrentProgress = Mathf.Min(q.requiredCount, CurrentProgress + 1);
                string progressText = $"{q.title}: {CurrentProgress}/{q.requiredCount} {q.targetName}";
                ChatSystem.LogQuest($"<color=#ffb442>{progressText}</color>");

                var player = FindFirstObjectByType<PlayerController>();
                if (player)
                {
                    CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.8f, progressText, new Color(1f, 0.9f, 0.3f), false);
                    if (IsActiveQuestReady())
                    {
                        player.Message = $"Objective Complete! Return to {q.giverNpc} in {q.zone}.";
                        AudioEffects.PlayQuestComplete();
                        TrackTurnIn(q);
                    }
                    else
                    {
                        AudioEffects.PlayQuestAccept();
                    }
                }
                SaveData();
            }
        }

        static bool ExactBossTarget(string id) => id == "redridge-outcast-boss" || id == "vermithrax-boss" || id == RiverReaverCamp.CaptainId;

        static void TrackTurnIn(QuestDef quest)
        {
            foreach (var npc in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                if (npc.npcName == quest.giverNpc) { MinimapRadar.SetWaypoint(npc.transform.position, "Return to " + npc.npcName); break; }
        }

        public void OnTalkedToNPC(string npcName)
        {
            var q = GetActiveQuest();
            if (q == null || q.targetType != "talk_npc" || IsActiveQuestReady()) return;

            if (string.Equals(q.targetId, npcName, StringComparison.OrdinalIgnoreCase))
            {
                CurrentProgress = q.requiredCount;
                var player = FindFirstObjectByType<PlayerController>();
                if (player)
                {
                    player.Message = $"Objective Complete! Speak with {npcName} to conclude the mission.";
                    CombatTextManager.Spawn(player.transform.position + Vector3.up * 1.8f, $"{q.title}: Ready for Turn In!", new Color(0.4f, 1f, 0.5f), true);
                }
                SaveData();
            }
        }

        string SaveFilePath
        {
            get
            {
                if (!string.IsNullOrEmpty(PlayerController.SavePathOverride))
                    return Path.ChangeExtension(PlayerController.SavePathOverride, ".faction.json");
                var player = FindFirstObjectByType<PlayerController>();
                string id = player != null ? player.CharacterSaveId : "default";
                return Path.Combine(Application.persistentDataPath, $"faction-quests-{id}.json");
            }
        }

        public void SaveData()
        {
            if (Network.CoopSessionConfig.IsMultiplayer) return;
            try
            {
                var data = new FactionQuestSaveData
                {
                    activeQuestId = ActiveQuestId,
                    currentProgress = CurrentProgress,
                    completedQuests = new List<string>(completedQuests)
                };
                File.WriteAllText(SaveFilePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FactionQuestSystem] Failed to save quest progress: " + ex.Message);
            }
        }

        public void LoadData()
        {
            ActiveQuestId = "";
            CurrentProgress = 0;
            completedQuests.Clear();
            if (Network.CoopSessionConfig.IsMultiplayer) return;
            try
            {
                string path = SaveFilePath;
                if (File.Exists(path))
                {
                    var data = JsonUtility.FromJson<FactionQuestSaveData>(File.ReadAllText(path));
                    if (data != null)
                    {
                        ActiveQuestId = data.activeQuestId ?? "";
                        CurrentProgress = data.currentProgress;
                        completedQuests.Clear();
                        if (data.completedQuests != null)
                        {
                            foreach (var id in data.completedQuests) completedQuests.Add(id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FactionQuestSystem] Failed to load quest progress: " + ex.Message);
            }
        }
    }
}
