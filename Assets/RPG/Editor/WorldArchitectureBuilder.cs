using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class WorldArchitectureBuilder
    {
        const string Root = "Assets/RPG/";
        const string NpcPrefabDir = "Assets/RPG/Prefabs/NPCs/";
        const string PropPrefabDir = "Assets/RPG/Prefabs/Props/";

        static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public enum ArchStyle
        {
            Tudor,
            DwarfStone,
            Elven,
            GothicBrick,
            HordeTimber,
            GoblinTimber
        }

        public static void BuildAllSettlementsAndCapitals(Transform worldRoot, StringBuilder report)
        {
            MaterialCache.Clear();
            var archRoot = new GameObject("22 - Modular Settlements & Grand Capitals").transform;
            archRoot.SetParent(worldRoot, false);

            // 1. Strip old primitive slabs/cylinders from relocated capitals & starter zones
            StripAllLegacyPlaceholders();

            // 2. Enhance Human Starter Village: Emberwatch Outpost (0, 0, 10)
            BuildEmberwatchVillageEnhancements(archRoot, report);

            // 3. Build Dwarf Starter Mini-Village: Frostpeak Foothills (-95, 16, -90)
            BuildFrostpeakVillage(archRoot, report);

            // 4. Build High Elf Starter Mini-Village: Silverglen Sanctum (-150, 15, 75)
            BuildSilverglenVillage(archRoot, report);

            // 5. Build Orc Starter Mini-Village: Camp Bloodrock (280, 18, 210)
            BuildBloodrockVillage(archRoot, report);

            // 6. Build Undead Starter Mini-Village: Deathknell Hollow (-240, 12, 180)
            BuildDeathknellVillage(archRoot, report);

            // 7. Build Goblin Starter Mini-Village: Rustwater Scrapworks (240, 14, 150)
            BuildRustwaterVillage(archRoot, report);

            // 8. Build Duskwood Scarlet Cathedral Exterior (-220, 18, 260)
            BuildDuskwoodCathedralExterior(archRoot, report);

            // 9. Build Grand Alliance Capital City: Highspire Keep (-200, 12, -60)
            BuildHighspireCapitalCity(archRoot, report);

            // 10. Build Grand Horde Capital Fortress: Gor'Kresh Stronghold (340, 25, 290)
            BuildGorkreshCapitalCity(archRoot, report);

            // 11. Upgrade All Gathering Nodes, Dungeon Interior, Boss Arenas & Remaining Primitive Props to 3D Meshes
            UpgradeGatheringNodesAndBossArenas(archRoot, report);
        }

        public static void BuildModularZoneGateways(Transform parent, StringBuilder report)
        {
            var root = new GameObject("Zone Transition Gateways and Landmarks").transform;
            root.SetParent(parent, false);

            // 1. Highspire Imperial Gateway (Cobblestone Highway at (-70, -35))
            BuildGatehouse("Highspire Imperial Gatehouse", Grounded(-70f, -35f), 62f, root, false, 9.5f);
            PlaceSign("HIGHSPIRE CITADEL\nSeat of the Grand Alliance", new Vector3(-62f, 0f, -27f), root);

            // 2. Redridge Canyon Pass Gateway (Horde Frontier Threshold at (105, 150))
            BuildGatehouse("Redridge Frontier Gatehouse", Grounded(105f, 150f), 48f, root, true, 9.5f);
            PlaceSign("REDRIDGE CANYON\nHorde Frontier & Ashen Crags", new Vector3(96f, 0f, 142f), root);

            // 3. Duskwood Mist Gate (Gothic Border Arch at (-145, 135))
            BuildGatehouse("Duskwood Mist Gatehouse", Grounded(-145f, 135f), -35f, root, false, 9.0f, true);
            PlaceSign("DUSKWOOD BORDER\nCursed Woods & Cathedral Grounds", new Vector3(-138f, 0f, 127f), root);

            // 4. Wyrmcrest Alpine Ascent Gate (Dragon Pass at (352, 330))
            BuildTower("Wyrmcrest Watch Spire West", Grounded(344f, 332f), new Vector3(356f, 28f, 332f), root, true, 0.92f);
            BuildTower("Wyrmcrest Watch Spire East", Grounded(360f, 326f), new Vector3(346f, 28f, 330f), root, true, 0.92f);
            PlaceSign("WYRMCREST PASS\nDanger: Apex Drake Nesting Grounds", new Vector3(347f, 0f, 322f), root);

            report?.AppendLine("GATEWAYS: Built 4 modular 3D architectural border gatehouses (Highspire, Redridge, Duskwood, Wyrmcrest).");
        }

        // =========================================================================
        // 1. ALLIANCE CAPITAL CITY: HIGHSPIRE KEEP (-200, 12, -60)
        // =========================================================================
        static void BuildHighspireCapitalCity(Transform parent, StringBuilder report)
        {
            var city = new GameObject("Highspire Keep - Grand Alliance Capital").transform;
            city.SetParent(parent, false);
            Vector3 center = Grounded(-200f, -60f);

            // A. Grand Royal Citadel Keep (South end, facing North into plaza)
            BuildGrandKeep("Highspire Royal Citadel", Grounded(-200f, -90f), center, city);

            // B. Arcane Sanctum & Royal Library (East-South district, facing West)
            BuildSanctumHall("Highspire Arcane Sanctum", Grounded(-170f, -68f), new Vector3(-200f, 12f, -68f), city, ArchStyle.Elven);
            PlaceProp("FantasyProps/FBX/Table_Large.fbx", Grounded(-178f, -69f), 90f, 1.0f, city, true);
            PlaceProp("FantasyProps/FBX/Book_Stack_1.fbx", Grounded(-178f, -69f) + Vector3.up * 0.85f, 25f, 1.1f, city, false);
            PlaceProp("FantasyProps/FBX/CandleStick.fbx", Grounded(-178f, -68.4f) + Vector3.up * 0.85f, 0f, 1.1f, city, false);

            // C. Royal Exchange & Auction House Hall (East-North district, facing West)
            BuildInn("Highspire Royal Exchange", Grounded(-170f, -46f), new Vector3(-200f, 12f, -46f), city, ArchStyle.Tudor);
            PlaceProp("FantasyProps/FBX/Stall_Cart_Empty.fbx", Grounded(-180f, -41f), -75f, 1.0f, city, true);
            PlaceProp("FantasyProps/FBX/Chest_Wood.fbx", Grounded(-179f, -50f), 90f, 1.1f, city, true);

            // D. Lion's Pride Tavern & Inn (West-South district, facing East)
            BuildInn("Lion's Pride Royal Inn", Grounded(-230f, -68f), new Vector3(-200f, 12f, -68f), city, ArchStyle.Tudor);
            SpawnExtraTownNPC("Prefab_WoW_TavernMaiden.prefab", city, "Innkeeper Evelyn", "Highspire Innkeeper",
                "Welcome to the Lion's Pride Inn! Rest beneath the banners of Highspire and bind your Hearthstone here.",
                Grounded(-221f, -67.5f), 90f);

            // E. Royal Armory & Great Smithy (West-North district, facing East)
            BuildSmithy("Highspire Royal Armory", Grounded(-230f, -46f), new Vector3(-200f, 12f, -46f), city, false);
            SpawnExtraTownNPC("Prefab_WoW_Blacksmith.prefab", city, "Royal Armorer Gareth", "Alliance Weaponsmith",
                "Every blade in the Grand Duke's vanguard is quenched right here in Highspire steel!",
                Grounded(-221f, -45.5f), 85f);

            // F. Northern Noble Townhouses flanking the Avenue
            BuildHouse("Noble Townhouse East", Grounded(-182f, -29f), new Vector3(-196f, 12f, -45f), city, ArchStyle.Tudor, 2);
            BuildHouse("Noble Townhouse West", Grounded(-218f, -29f), new Vector3(-204f, 12f, -45f), city, ArchStyle.Tudor, 2);

            // G. Central Royal Courtyard Fountain / Well, Market Carts, Benches & Street Lanterns
            PlacePrefabProp("Prefab_TownWell.prefab", Grounded(-200f, -56f), 0f, 1.25f, city);
            PlacePrefabProp("Prefab_Medieval_Bench.prefab", Grounded(-194.5f, -56f), -90f, 1.0f, city);
            PlacePrefabProp("Prefab_Medieval_Bench.prefab", Grounded(-205.5f, -56f), 90f, 1.0f, city);
            PlaceProp("Village/FBX/Prop_Wagon.fbx", Grounded(-188f, -36f), -35f, 1.0f, city, true);
            PlaceProp("FantasyProps/FBX/FarmCrate_Apple.fbx", Grounded(-181f, -42.5f), 15f, 1.0f, city, false);
            PlaceProp("FantasyProps/FBX/FarmCrate_Carrot.fbx", Grounded(-181f, -43.5f), -20f, 1.0f, city, false);

            // Street Lamp Posts along the Capital Plaza
            foreach (var lp in new[] { new Vector2(-192f, -72f), new Vector2(-208f, -72f), new Vector2(-192f, -42f), new Vector2(-208f, -42f) })
                BuildStreetLamp("Royal Street Lamp", Grounded(lp.x, lp.y), city, new Color(1f, 0.82f, 0.45f));

            // H. Fortified Stone Curtain Walls, Corner Spires & Walkable City Gatehouses
            // East-North Highway Gatehouse (faces incoming Cobblestone Highway toward (-135, -55))
            BuildGatehouse("Highspire East Highway Gate", Grounded(-160f, -57f), -95f, city, false, 9.5f);
            // North-West River Gatehouse (faces Westbank approach toward (-225, 0))
            BuildGatehouse("Highspire North Gate", Grounded(-204f, -20f), 165f, city, false, 9.5f);

            // Corner Bastion Towers
            BuildTower("Highspire Bastion SE", Grounded(-160f, -96f), center, city, false, 1.1f);
            BuildTower("Highspire Bastion SW", Grounded(-240f, -96f), center, city, false, 1.1f);
            BuildTower("Highspire Bastion NW", Grounded(-240f, -22f), center, city, false, 1.1f);
            BuildTower("Highspire Bastion NE", Grounded(-160f, -22f), center, city, false, 1.1f);

            // Curtain Wall runs connecting towers cleanly (leaving wide gate openings)
            BuildCurtainWall(Grounded(-240f, -94f), Grounded(-240f, -24f), city, false); // West wall
            BuildCurtainWall(Grounded(-238f, -96f), Grounded(-214f, -96f), city, false); // South-West wall
            BuildCurtainWall(Grounded(-186f, -96f), Grounded(-162f, -96f), city, false); // South-East wall
            BuildCurtainWall(Grounded(-160f, -94f), Grounded(-160f, -66f), city, false); // East-South wall
            BuildCurtainWall(Grounded(-160f, -48f), Grounded(-160f, -24f), city, false); // East-North wall
            BuildCurtainWall(Grounded(-238f, -21f), Grounded(-213f, -21f), city, false); // North-West wall
            BuildCurtainWall(Grounded(-194f, -21f), Grounded(-162f, -21f), city, false); // North-East wall

            // I. Reposition & Ground Existing Highspire NPCs, Mailbox & Flight Master cleanly onto the Plaza
            RepositionTownNPC("Grand Duke Ronald", Grounded(-200f, -75f), 0f);
            RepositionTownNPC("Archmage Karen", Grounded(-180f, -67.5f), -90f);
            RepositionTownNPC("Auctioneer Fitch", Grounded(-180f, -46f), -90f);
            RepositionTownNPC("Captain Brian", Grounded(-207f, -60f), 110f);
            RepositionTownNPC("Highspire Sentry Donald", Grounded(-164f, -52.5f), 90f);
            RepositionTownNPC("Highspire Sentry Kenneth", Grounded(-164f, -61.5f), 90f);
            RepositionNamedObject("Highspire Royal Mailbox", Grounded(-181.5f, -49.5f) + Vector3.up * 0.55f);
            RepositionFlightMaster("Highspire", Grounded(-169f, -31f));

            report?.AppendLine("CAPITAL [Highspire Keep]: Built walled 80x80m Alliance Capital with Royal Citadel, Arcane Sanctum, Royal Exchange, Lion's Pride Inn, Armory, Townhouses, 2 Gatehouses, and 4 Corner Spires.");
        }

        // =========================================================================
        // 2. HORDE CAPITAL FORTRESS: GOR'KRESH STRONGHOLD (340, 25, 290)
        // =========================================================================
        static void BuildGorkreshCapitalCity(Transform parent, StringBuilder report)
        {
            var city = new GameObject("Gor'Kresh Stronghold - Grand Horde Capital").transform;
            city.SetParent(parent, false);
            Vector3 center = Grounded(340f, 290f);

            // A. Warchief's Great War-Hall (North-West Citadel backing, facing South-East into War Plaza)
            BuildHordeWarHall("Gor'Kresh Great War-Hall", Grounded(334f, 319f), center, city);

            // B. Lodge of the Ancestral Spirits (West wing, facing East near Shaman Kelsey)
            BuildSanctumHall("Ancestral Spirit Lodge", Grounded(310f, 296f), new Vector3(340f, 25f, 296f), city, ArchStyle.HordeTimber);
            PlaceProp("FantasyProps/FBX/Cauldron.fbx", Grounded(318f, 293f), 0f, 1.3f, city, true);
            AddPointLight("Spirit Brazier Glow", Grounded(318f, 293f) + Vector3.up * 1.4f, new Color(1f, 0.45f, 0.12f), 11f, 2.8f, city);

            // C. Gor'Kresh Great War-Forge (East wing, facing West near Blacksmith Garok)
            BuildSmithy("Gor'Kresh Great War-Forge", Grounded(370f, 298f), new Vector3(340f, 25f, 298f), city, true);

            // D. Bilgewater Cartel Exchange & Auction Hall (East-South wing, facing West near Auctioneer Drazzle)
            BuildInn("Bilgewater Exchange Hall", Grounded(370f, 276f), new Vector3(340f, 25f, 276f), city, ArchStyle.GoblinTimber);
            PlaceProp("FantasyProps/FBX/Stall_Cart_Empty.fbx", Grounded(360f, 272f), 105f, 1.05f, city, true);
            PlaceProp("Village/FBX/Prop_Wagon.fbx", Grounded(359f, 282f), 155f, 1.0f, city, true);

            // E. Bloodfury Tavern & Barracks (West-South wing, facing East)
            BuildInn("Bloodfury War-Tavern", Grounded(310f, 274f), new Vector3(340f, 25f, 274f), city, ArchStyle.HordeTimber);
            SpawnExtraTownNPC("Prefab_WoW_TavernMaiden.prefab", city, "Innkeeper Borga", "Gor'Kresh Barkeep",
                "Drink up, warrior! Bind your Hearthstone to Gor'Kresh and feast before marching on Mount Wyrmcrest!",
                Grounded(319f, 274.5f), 90f);

            // F. Horde Clan Longhouses
            BuildHouse("Warsong Clan Lodge", Grounded(356f, 259f), center, city, ArchStyle.HordeTimber, 2);
            BuildHouse("Frostwolf Clan Lodge", Grounded(322f, 259f), center, city, ArchStyle.HordeTimber, 2);

            // G. Central Warchief War-Hearth, Weapon Racks & War Drums/Crates
            BuildBonfireHearth("Warchief Ancestral Bonfire", Grounded(340f, 288f), city, new Color(1f, 0.42f, 0.10f), 2.2f);
            PlacePrefabProp("Prefab_WeaponRack_Loaded.prefab", Grounded(333f, 304f), 180f, 1.15f, city);
            PlacePrefabProp("Prefab_WeaponRack_Loaded.prefab", Grounded(347f, 304f), 180f, 1.15f, city);
            PlacePrefabProp("Prefab_Medieval_Barrel.prefab", Grounded(346f, 286f), 20f, 1.1f, city);
            PlacePrefabProp("Prefab_Medieval_Crate.prefab", Grounded(347.2f, 287f), -15f, 1.1f, city);

            foreach (var lp in new[] { new Vector2(332f, 298f), new Vector2(348f, 298f), new Vector2(332f, 276f), new Vector2(348f, 276f) })
                BuildStreetLamp("Horde Torch Post", Grounded(lp.x, lp.y), city, new Color(1f, 0.48f, 0.14f));

            // H. Fortress Timber & Stone Ramparts, Watchtowers, and Walkable Gates
            // South-West War Gate (faces Camp Bloodrock road toward (310, 255))
            BuildGatehouse("Gor'Kresh South War-Gate", Grounded(326f, 252f), 32f, city, true, 10f);
            // North-East Dragon Ascent Gate (faces Wyrmcrest switchbacks toward (365, 370))
            BuildGatehouse("Gor'Kresh Dragon-Pass Gate", Grounded(354f, 326f), -160f, city, true, 10f);

            BuildTower("Gor'Kresh Bastion SW", Grounded(302f, 252f), center, city, true, 1.1f);
            BuildTower("Gor'Kresh Bastion SE", Grounded(378f, 252f), center, city, true, 1.1f);
            BuildTower("Gor'Kresh Bastion NW", Grounded(302f, 326f), center, city, true, 1.1f);
            BuildTower("Gor'Kresh Bastion NE", Grounded(378f, 326f), center, city, true, 1.1f);

            BuildCurtainWall(Grounded(302f, 255f), Grounded(302f, 323f), city, true); // West palisade
            BuildCurtainWall(Grounded(378f, 255f), Grounded(378f, 323f), city, true); // East palisade
            BuildCurtainWall(Grounded(305f, 252f), Grounded(317f, 252f), city, true); // South-West run
            BuildCurtainWall(Grounded(336f, 252f), Grounded(375f, 252f), city, true); // South-East run
            BuildCurtainWall(Grounded(305f, 326f), Grounded(344f, 326f), city, true); // North-West run
            BuildCurtainWall(Grounded(364f, 326f), Grounded(375f, 326f), city, true); // North-East run

            // I. Reposition & Ground Gor'Kresh NPCs, Mailbox & Flight Master
            RepositionTownNPC("Overlord Mor'gok", Grounded(337f, 304f), 175f);
            RepositionTownNPC("Shaman Kelsey", Grounded(320f, 296f), 90f);
            RepositionTownNPC("Blacksmith Garok", Grounded(359f, 298f), -90f);
            RepositionTownNPC("Auctioneer Drazzle", Grounded(359f, 276f), -90f);
            RepositionTownNPC("Korgath the Brute", Grounded(333f, 281f), 45f);
            RepositionNamedObject("Gor'Kresh War Mailbox", Grounded(358f, 279.5f) + Vector3.up * 0.55f);
            RepositionFlightMaster("Gor'Kresh", Grounded(348f, 314f));

            report?.AppendLine("CAPITAL [Gor'Kresh Stronghold]: Built 80x80m Horde Capital Fortress with Great War-Hall, Spirit Lodge, War-Forge, Bilgewater Exchange, War-Tavern, Clan Lodges, 2 Gatehouses, and 4 Watchtowers.");
        }

        // =========================================================================
        // 3. HUMAN STARTER MINI-VILLAGE: EMBERWATCH OUTPOST (0, 0, 10)
        // =========================================================================
        static void BuildEmberwatchVillageEnhancements(Transform parent, StringBuilder report)
        {
            var village = new GameObject("Emberwatch Village Architecture").transform;
            village.SetParent(parent, false);

            // Replace legacy primitive cube Gate posts / Crimson banners / Gate crossbeam in 02 - Emberwatch Outpost with 3D timber/stone gateway
            var outpostRoot = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "02 - Emberwatch Outpost");
            if (outpostRoot)
            {
                foreach (Transform ch in outpostRoot)
                {
                    if (ch.name == "Gate post" || ch.name == "Crimson banner" || ch.name == "Gate crossbeam" || ch.name == "Village sign" || ch.GetComponent<TextMesh>())
                        ch.gameObject.SetActive(false);
                }
            }

            // Build real 3D Timber & Stone North Village Gateway Arch at (0, 0, 18) with wide 8.5m road clearance
            var gateArch = new GameObject("Emberwatch North Gateway Arch").transform;
            gateArch.SetParent(village, false);
            gateArch.position = Grounded(0f, 18f);
            foreach (int side in new[] { -1, 1 })
            {
                var pillar = PlaceProp("Village/FBX/Corner_ExteriorWide_Brick.fbx", Vector3.zero, 0f, 1f, gateArch, true);
                if (pillar) { FitBox(pillar, new Vector3(0.95f, 4.8f, 0.95f)); pillar.transform.localPosition = new Vector3(side * 4.4f, 0f, 0f); }
                var post = PlaceProp("Village/FBX/Prop_Support.fbx", Vector3.zero, 0f, 1f, gateArch, false);
                if (post) { FitBox(post, new Vector3(0.55f, 5.1f, 0.55f)); post.transform.localPosition = new Vector3(side * 3.9f, 0f, 0f); }
                var lantern = PlaceProp("FantasyProps/FBX/Lantern_Wall.fbx", new Vector3(side * 3.55f, 3.2f, -0.35f), 0f, 1.15f, gateArch, false);
                if (lantern) FitHeight(lantern, 0.55f);
            }
            var archTop = PlaceProp("Village/FBX/Wall_Arch.fbx", Vector3.zero, 0f, 1f, gateArch, false);
            if (archTop) { FitBox(archTop, new Vector3(9.4f, 2.2f, 1.1f)); archTop.transform.localPosition = new Vector3(0f, 4.2f, 0f); }
            var archRoof = PlaceProp("Village/FBX/Roof_RoundTiles_4x8.fbx", Vector3.zero, 0f, 1f, gateArch, false);
            if (archRoof)
            {
                FitBox(archRoof, new Vector3(2.2f, 1.6f, 10.2f));
                archRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                archRoof.transform.localPosition = new Vector3(0f, 5.9f, 0f);
            }

            // Keep existing 4 Tudor houses at (±16, 8) and (±12, 24) & Abe/Rowan/Crates untouched!
            // Add South Village Town Hall / Abbey facing North into the square
            BuildSanctumHall("Emberwatch Abbey & Town Hall", Grounded(0f, -17f), Grounded(0f, 4f), village, ArchStyle.Tudor);

            // Add Twin Watchtowers flanking the North Gate
            BuildTower("Emberwatch Watchtower East", Grounded(17f, 19f), Grounded(0f, 14f), village, false, 0.9f);
            BuildTower("Emberwatch Watchtower West", Grounded(-17f, 19f), Grounded(0f, 14f), village, false, 0.9f);

            // Market Stalls & Farm Produce in Village Square (clear of center road & Abe)
            PlaceProp("FantasyProps/FBX/Stall_Cart_Empty.fbx", Grounded(-9.5f, 1.5f), 75f, 1.0f, village, true);
            PlaceProp("FantasyProps/FBX/FarmCrate_Apple.fbx", Grounded(-8.5f, 0.2f), 15f, 1.0f, village, false);
            PlaceProp("FantasyProps/FBX/FarmCrate_Carrot.fbx", Grounded(-8.2f, 2.8f), -25f, 1.0f, village, false);

            // Wooden Village Fences framing the green
            PlaceFenceLine("Village/FBX/Prop_WoodenFence_Single.fbx", Grounded(-19f, -4f), Grounded(-19f, 15f), village, 3.0f);
            PlaceFenceLine("Village/FBX/Prop_WoodenFence_Single.fbx", Grounded(19f, -4f), Grounded(19f, 15f), village, 3.0f);
            BuildStreetLamp("Emberwatch Square Lamp W", Grounded(-5.5f, 13f), village, new Color(1f, 0.82f, 0.45f));
            BuildStreetLamp("Emberwatch Square Lamp E", Grounded(5.5f, 13f), village, new Color(1f, 0.82f, 0.45f));

            report?.AppendLine("STARTER VILLAGE [Emberwatch Outpost]: Replaced primitive gate cubes with 3D Gateway Arch, added Abbey Town Hall, Twin Watchtowers, Market Stalls, Street Lamps, and Village Fences.");
        }

        // =========================================================================
        // 4. DWARF STARTER MINI-VILLAGE: FROSTPEAK FOOTHILLS (-95, 16, -90)
        // =========================================================================
        static void BuildFrostpeakVillage(Transform parent, StringBuilder report)
        {
            var village = new GameObject("Frostpeak Anvil - Dwarven Starter Village").transform;
            village.SetParent(parent, false);
            Vector3 center = Grounded(-95f, -90f);

            // 4 Real Dwarven Stone & Brick Buildings + Watchtower around cobbled square
            BuildInn("Thunderbrew Mountain Mead-Hall", Grounded(-95f, -73f), center, village, ArchStyle.DwarfStone);
            BuildSmithy("Ironbeard Great Smithy", Grounded(-112f, -90f), center, village, false);
            BuildHouse("Miner's Stone Cottage South", Grounded(-95f, -107f), center, village, ArchStyle.DwarfStone, 2);
            BuildHouse("Mountaineer's Lodge East", Grounded(-78f, -88f), center, village, ArchStyle.DwarfStone, 2);
            BuildTower("Frostpeak Stone Watchtower", Grounded(-111f, -75f), center, village, false, 0.95f);

            PlacePrefabProp("Prefab_TownWell.prefab", Grounded(-89f, -95f), 25f, 1.1f, village);
            PlaceProp("Village/FBX/Prop_Wagon.fbx", Grounded(-104f, -101f), 40f, 1.0f, village, true);
            PlacePrefabProp("Prefab_WeaponRack_Loaded.prefab", Grounded(-105f, -87f), 90f, 1.1f, village);
            BuildStreetLamp("Frostpeak Brazier Lamp 1", Grounded(-90f, -84f), village, new Color(1f, 0.72f, 0.32f));
            BuildStreetLamp("Frostpeak Brazier Lamp 2", Grounded(-101f, -95f), village, new Color(1f, 0.72f, 0.32f));

            SpawnExtraTownNPC("Prefab_WoW_TavernMaiden.prefab", village, "Innkeeper Olga", "Thunderbrew Hostess",
                "Warm your frozen bones by the hearth, kin! Bind your Hearthstone to Thunderbrew Mead-Hall anytime.",
                Grounded(-95f, -80f), 180f);

            // Position Dwarf NPCs cleanly in plaza & smithy porch (Player spawns at (-95, 16, -90))
            RepositionTownNPC("Thane Baelor Ironbeard", Grounded(-95f, -84.5f), 180f);
            RepositionTownNPC("Gundrak Stonehammer", Grounded(-104.5f, -90f), 90f);

            // Move Trogg Scouts outside the village to an eastern snowy ridge hunting camp & ensure 4 enemies
            ArrangeStarterEnemyCamp("16 - Dwarf Starter Frostpeak Foothills", "frostpeak-trogg",
                new[] { new Vector2(-68f, -96f), new Vector2(-72f, -104f), new Vector2(-64f, -103f), new Vector2(-75f, -111f) });

            report?.AppendLine("STARTER VILLAGE [Frostpeak Foothills]: Built Dwarven Mini-Village (Mead-Hall, Great Smithy, 2 Stone Cottages, Watchtower, Well, Innkeeper Olga) & outer Trogg camp.");
        }

        // =========================================================================
        // 5. HIGH ELF STARTER MINI-VILLAGE: SILVERGLEN SANCTUM (-150, 15, 75)
        // =========================================================================
        static void BuildSilverglenVillage(Transform parent, StringBuilder report)
        {
            var village = new GameObject("Silverglen Sanctum - High Elf Starter Village").transform;
            village.SetParent(parent, false);
            Vector3 center = Grounded(-150f, 75f);

            // Shift Moonwell slightly north (-150, 81) so player spawn (-150, 75) is 100% open cobbled plaza!
            RepositionNamedObject("Moonwell Stone Basin", Grounded(-150f, 81f) + Vector3.up * 0.25f);
            RepositionNamedObject("Moonstone Obelisk 1", Grounded(-145.5f, 85.5f) + Vector3.up * 2.1f);
            RepositionNamedObject("Moonstone Obelisk 2", Grounded(-154.5f, 85.5f) + Vector3.up * 2.1f);
            RepositionNamedObject("Moonstone Obelisk 3", Grounded(-145.5f, 76.5f) + Vector3.up * 2.1f);
            RepositionNamedObject("Moonstone Obelisk 4", Grounded(-154.5f, 76.5f) + Vector3.up * 2.1f);

            // 4 Graceful Highborne Plaster & Spire Buildings around the Moonwell Terrace
            BuildSanctumHall("Sanctum of the Eternal Sun", Grounded(-150f, 94f), center, village, ArchStyle.Elven);
            BuildInn("Silverglen Spire Inn & Lounge", Grounded(-133f, 78f), center, village, ArchStyle.Elven);
            BuildHouse("Silverglen Athenaeum", Grounded(-166f, 78f), center, village, ArchStyle.Elven, 2);
            BuildHouse("Farstrider Ranger Lodge", Grounded(-150f, 59f), center, village, ArchStyle.Elven, 2);
            BuildTower("Silverglen Arcane Spire", Grounded(-165f, 91f), center, village, false, 1.0f);

            // Scholar desks & ornate iron fences
            PlaceProp("FantasyProps/FBX/Table_Large.fbx", Grounded(-160.5f, 75f), 90f, 1.0f, village, true);
            PlaceProp("FantasyProps/FBX/Book_Stack_1.fbx", Grounded(-160.5f, 75f) + Vector3.up * 0.85f, 30f, 1.1f, village, false);
            PlacePrefabProp("Prefab_Medieval_Bench.prefab", Grounded(-143.5f, 81f), -90f, 1.0f, village);
            PlacePrefabProp("Prefab_Medieval_Bench.prefab", Grounded(-156.5f, 81f), 90f, 1.0f, village);
            PlaceFenceLine("Village/FBX/Prop_MetalFence_Ornament.fbx", Grounded(-168f, 64f), Grounded(-158f, 64f), village, 2.8f);
            PlaceFenceLine("Village/FBX/Prop_MetalFence_Ornament.fbx", Grounded(-142f, 64f), Grounded(-132f, 64f), village, 2.8f);

            BuildStreetLamp("Silverglen Arcane Lantern 1", Grounded(-144f, 71f), village, new Color(0.45f, 0.85f, 1f));
            BuildStreetLamp("Silverglen Arcane Lantern 2", Grounded(-156f, 71f), village, new Color(0.45f, 0.85f, 1f));

            SpawnExtraTownNPC("Prefab_WoW_TavernMaiden.prefab", village, "Innkeeper Lyrielle", "Sanctum Steward",
                "Anaria shola, traveler. Rest beneath the Moonwell's light and make Silverglen your home.",
                Grounded(-140.5f, 78f), -90f);

            RepositionTownNPC("Sunwarden Thalorien", Grounded(-148.5f, 71.5f), 0f);
            RepositionTownNPC("Magister Kaelen", Grounded(-159.5f, 78f), 90f);

            ArrangeStarterEnemyCamp("17 - High Elf Starter Silverglen Sanctum", "silverglen-mana-sprite",
                new[] { new Vector2(-176f, 82f), new Vector2(-180f, 90f), new Vector2(-172f, 94f), new Vector2(-183f, 76f) });

            report?.AppendLine("STARTER VILLAGE [Silverglen Sanctum]: Built High Elf Mini-Village (Sun Sanctum, Athenaeum, Spire Inn, Farstrider Lodge, Arcane Spire, Moonwell Plaza) & outer Sprite glade.");
        }

        // =========================================================================
        // 6. ORC STARTER MINI-VILLAGE: CAMP BLOODROCK (280, 18, 210)
        // =========================================================================
        static void BuildBloodrockVillage(Transform parent, StringBuilder report)
        {
            var village = new GameObject("Camp Bloodrock - Orc Starter War-Village").transform;
            village.SetParent(parent, false);
            Vector3 center = Grounded(280f, 210f);

            // Fortified Horde War-Village (Orc player spawns at (275, 18, 205))
            BuildInn("Bloodrock Chieftain's Longhouse", Grounded(280f, 227f), center, village, ArchStyle.HordeTimber);
            BuildSmithy("Thrak's Canyon War-Forge", Grounded(262f, 214f), center, village, true);
            BuildHouse("Bloodrock Grunt Barracks", Grounded(297f, 210f), center, village, ArchStyle.HordeTimber, 2);
            BuildHouse("Outrunner Supply Hut", Grounded(265f, 196f), center, village, ArchStyle.HordeTimber, 1);
            BuildGatehouse("Bloodrock West Palisade Gate", Grounded(258f, 203f), 60f, village, true, 8.5f);
            BuildTower("Bloodrock East Watchtower", Grounded(296f, 224f), center, village, true, 0.95f);

            BuildBonfireHearth("Bloodrock Village Bonfire", Grounded(282f, 211f), village, new Color(1f, 0.48f, 0.12f), 1.5f);
            PlacePrefabProp("Prefab_WeaponRack_Loaded.prefab", Grounded(274f, 220f), 180f, 1.1f, village);
            PlaceProp("Village/FBX/Prop_Wagon.fbx", Grounded(289f, 199f), -45f, 1.0f, village, true);
            PlacePrefabProp("Prefab_Medieval_Barrel.prefab", Grounded(286f, 216f), 0f, 1.05f, village);
            PlaceFenceLine("Village/FBX/Prop_WoodenFence_Single.fbx", Grounded(271f, 192f), Grounded(292f, 192f), village, 3.0f);

            SpawnExtraTownNPC("Prefab_WoW_TavernMaiden.prefab", village, "Innkeeper Gashna", "Bloodrock Ration Master",
                "Hot boar ribs and strong cactus brew! Bind your Hearthstone to Camp Bloodrock, warrior!",
                Grounded(284.5f, 219.5f), 180f);

            RepositionTownNPC("Warlord Gorgash", Grounded(278.5f, 218.5f), 180f);
            RepositionTownNPC("Thrak", Grounded(269.5f, 214f), 90f);
            RepositionTownNPC("Scout Grimjaw", Grounded(264f, 203.5f), -120f);
            RepositionFlightMaster("Bloodrock", Grounded(290f, 203f));

            // Arrange 3 Crag Orc marauders + 1 Outcast Chieftain Boss on the eastern canyon bluff outside the village!
            ArrangeStarterEnemyCamp("07 - Redridge Frontier Landmark", "redridge-orc-sentry",
                new[] { new Vector2(306f, 208f), new Vector2(310f, 216f), new Vector2(304f, 221f), new Vector2(312f, 228f) }, true);

            report?.AppendLine("STARTER VILLAGE [Camp Bloodrock]: Built Orc War-Village (Chieftain's Longhouse, Thrak's War-Forge, Barracks, Palisade Gatehouse, Watchtower, Bonfire) & Outcast bluff camp.");
        }

        // =========================================================================
        // 7. UNDEAD STARTER MINI-VILLAGE: DEATHKNELL HOLLOW (-240, 12, 180)
        // =========================================================================
        static void BuildDeathknellVillage(Transform parent, StringBuilder report)
        {
            var village = new GameObject("Deathknell Hollow - Forsaken Starter Village").transform;
            village.SetParent(parent, false);
            Vector3 center = Grounded(-240f, 180f);

            // Gothic Dark Brick Forsaken Hamlet (Undead spawns at (-240, 12, 180))
            BuildSanctumHall("Deathknell Forgotten Shadow Abbey", Grounded(-240f, 198f), center, village, ArchStyle.GothicBrick);
            BuildInn("Gallows' End Tavern", Grounded(-258f, 180f), center, village, ArchStyle.GothicBrick);
            BuildHouse("Royal Apothecary Lab", Grounded(-223f, 180f), center, village, ArchStyle.GothicBrick, 2);
            BuildHouse("Deathstalker Watch House", Grounded(-242f, 163f), center, village, ArchStyle.GothicBrick, 2);
            BuildTower("Deathknell Bell Tower", Grounded(-256f, 195f), center, village, false, 0.95f);

            // Reposition Forsaken Blight Cauldron & Gravestones cleanly beside the Apothecary & Abbey
            RepositionNamedObject("Forsaken Blight Cauldron", Grounded(-230f, 183f) + Vector3.up * 0.45f);
            PlaceProp("FantasyProps/FBX/Cauldron.fbx", Grounded(-230f, 183f), 0f, 1.35f, village, true);
            PlaceProp("FantasyProps/FBX/Table_Large.fbx", Grounded(-229.5f, 177.5f), 90f, 1.0f, village, true);
            PlacePrefabProp("Prefab_Medieval_Barrel.prefab", Grounded(-228.5f, 185f), 15f, 1.0f, village);

            // Wrought-iron cemetery fences
            PlaceFenceLine("Village/FBX/Prop_MetalFence_Simple.fbx", Grounded(-232f, 164f), Grounded(-220f, 164f), village, 2.8f);
            PlaceFenceLine("Village/FBX/Prop_MetalFence_Simple.fbx", Grounded(-256f, 164f), Grounded(-248f, 164f), village, 2.8f);
            BuildStreetLamp("Deathknell Blight Lantern 1", Grounded(-235f, 186f), village, new Color(0.35f, 1f, 0.45f));
            BuildStreetLamp("Deathknell Blight Lantern 2", Grounded(-246f, 186f), village, new Color(0.35f, 1f, 0.45f));

            SpawnExtraTownNPC("Prefab_WoW_Arcanist.prefab", village, "Innkeeper Cain", "Gallows' End Keeper",
                "Even the dead require a roof over their skulls. Make Gallows' End your Hearthstone bind point, dark one.",
                Grounded(-250.5f, 180f), 90f);

            RepositionTownNPC("Deathstalker Morzan", Grounded(-240f, 174f), 0f);
            RepositionTownNPC("Shadow Priestess Alyssa", Grounded(-239.5f, 189.5f), 180f);

            // Reposition gravestones safely into a side cemetery plot
            for (int g = 1; g <= 4; g++)
                RepositionNamedObject($"Weathered Gravestone {g}", Grounded(-230f + g * 2.1f, 167f) + Vector3.up * 0.55f);

            // Move Wandering Ghouls outside the iron gates to the northeast haunted graveyard
            ArrangeStarterEnemyCamp("18 - Undead Starter Deathknell Hollow", "deathknell-ghoul",
                new[] { new Vector2(-214f, 188f), new Vector2(-210f, 196f), new Vector2(-218f, 199f), new Vector2(-212f, 181f) });

            report?.AppendLine("STARTER VILLAGE [Deathknell Hollow]: Built Gothic Forsaken Mini-Village (Shadow Abbey, Gallows' End Tavern, Apothecary Lab, Bell Tower, Cemetery) & outer Ghoul graveyard.");
        }

        // =========================================================================
        // 8. GOBLIN STARTER MINI-VILLAGE: RUSTWATER SCRAPWORKS (240, 14, 150)
        // =========================================================================
        static void BuildRustwaterVillage(Transform parent, StringBuilder report)
        {
            var village = new GameObject("Rustwater Port - Goblin Starter Town").transform;
            village.SetParent(parent, false);
            Vector3 center = Grounded(240f, 150f);

            // Bustling Bilgewater Trade & Engineering Town (Goblin spawns at (240, 14, 150))
            BuildInn("Bilgewater Profit & Trade Hall", Grounded(240f, 133f), center, village, ArchStyle.GoblinTimber);
            BuildSmithy("Fizzlebang's Tinkering Barn", Grounded(222f, 150f), center, village, true);
            BuildHouse("Cartel Counting House", Grounded(257f, 148f), center, village, ArchStyle.GoblinTimber, 2);
            BuildHouse("Boom-Powder Depot Shack", Grounded(240f, 167f), center, village, ArchStyle.GoblinTimber, 1);
            BuildTower("Rustwater Rig Watchtower", Grounded(224f, 135f), center, village, true, 0.9f);

            // Market stalls, cargo wagons & crates
            PlaceProp("FantasyProps/FBX/Stall_Cart_Empty.fbx", Grounded(248f, 143f), -60f, 1.05f, village, true);
            PlaceProp("FantasyProps/FBX/Stall_Cart_Empty.fbx", Grounded(248f, 156f), -110f, 1.05f, village, true);
            PlaceProp("Village/FBX/Prop_Wagon.fbx", Grounded(231f, 161f), 35f, 1.0f, village, true);
            PlacePrefabProp("Prefab_Medieval_Crate.prefab", Grounded(233f, 158f), 15f, 1.15f, village);
            PlacePrefabProp("Prefab_Medieval_Barrel.prefab", Grounded(234.3f, 158.5f), 0f, 1.05f, village);

            // Keep & reposition TNT Barrels neatly beside the Boom-Powder Depot
            for (int t = 1; t <= 3; t++)
                RepositionNamedObject($"TNT Barrel {t}", Grounded(236f + t * 1.1f, 162.5f) + Vector3.up * 0.5f);

            BuildStreetLamp("Rustwater Oil Lamp 1", Grounded(234f, 144f), village, new Color(1f, 0.85f, 0.25f));
            BuildStreetLamp("Rustwater Oil Lamp 2", Grounded(246f, 144f), village, new Color(1f, 0.85f, 0.25f));

            SpawnExtraTownNPC("Prefab_WoW_TavernMaiden.prefab", village, "Innkeeper Slinky", "Cartel Hospitality",
                "Best beds in the basin — at unbeatable Bilgewater rates! Bind your Hearthstone free of charge!",
                Grounded(243.5f, 140.5f), 0f);

            RepositionTownNPC("Boss Griznak", Grounded(238.5f, 143.5f), 0f);
            RepositionTownNPC("Tinker Fizzlebang", Grounded(229.5f, 150f), 90f);

            // Move Scavengers outside the town to an eastern canyon scrapyard
            ArrangeStarterEnemyCamp("19 - Goblin Starter Rustwater Scrapworks", "rustwater-scavenger",
                new[] { new Vector2(266f, 148f), new Vector2(270f, 156f), new Vector2(264f, 162f), new Vector2(272f, 142f) });

            report?.AppendLine("STARTER VILLAGE [Rustwater Scrapworks]: Built Goblin Trade Town (Profit Hall, Tinkering Barn, Counting House, Depot, Market Stalls, Innkeeper Slinky) & outer Scavenger camp.");
        }

        // =========================================================================
        // 9. DUSKWOOD SCARLET CATHEDRAL EXTERIOR (-220, 18, 260)
        // =========================================================================
        static void BuildDuskwoodCathedralExterior(Transform parent, StringBuilder report)
        {
            var cat = new GameObject("Scarlet Cathedral Ruined Exterior").transform;
            cat.SetParent(parent, false);

            // Frame the glowing Crimson Dungeon Portal at (-220, 18, 260) with an imposing Gothic Cathedral Nave & Twin Belfries!
            BuildSanctumHall("Scarlet Monastery Nave Backing", Grounded(-220f, 268f), Grounded(-220f, 250f), cat, ArchStyle.GothicBrick);
            BuildTower("Cathedral Belfry West", Grounded(-228f, 261f), Grounded(-220f, 248f), cat, false, 1.15f);
            BuildTower("Cathedral Belfry East", Grounded(-212f, 261f), Grounded(-220f, 248f), cat, false, 1.15f);

            // Walkable stone arch framing the Crimson Portal at (-220, 18, 260)
            RepositionNamedObject("Cathedral Crimson Portal", Grounded(-220f, 260f) + Vector3.up * 2.4f);
            var arch = PlaceProp("Village/FBX/Wall_Arch.fbx", Grounded(-220f, 260f), 0f, 1f, cat, false);
            if (arch) FitBox(arch, new Vector3(6.8f, 6.2f, 1.2f));

            PlaceFenceLine("Village/FBX/Prop_MetalFence_Ornament.fbx", Grounded(-232f, 250f), Grounded(-224f, 250f), cat, 2.8f);
            PlaceFenceLine("Village/FBX/Prop_MetalFence_Ornament.fbx", Grounded(-216f, 250f), Grounded(-208f, 250f), cat, 2.8f);
            RepositionTownNPC("Inquisitor Lucian", Grounded(-216.5f, 254.5f), -155f);
            RepositionFlightMaster("Duskwood", Grounded(-208f, 246f));

            report?.AppendLine("LANDMARK [Duskwood Cathedral]: Built Gothic Cathedral Façade & Twin Belfries around Scarlet Crypts Dungeon Portal.");
        }

        // =========================================================================
        // MODULAR ARCHITECTURAL ARCHETYPE BUILDERS (REAL 3D FBX + URP MATERIALS)
        // =========================================================================
        public static GameObject BuildHouse(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent, ArchStyle style, int stories = 2)
        {
            var house = new GameObject(name).transform;
            house.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var backingMat = style == ArchStyle.GothicBrick || style == ArchStyle.DwarfStone
                ? LoadMat("Village_MI_UnevenBrick.mat")
                : style == ArchStyle.HordeTimber || style == ArchStyle.GoblinTimber
                    ? LoadMat("Dark timber.mat")
                    : LoadMat("Village_MI_Plaster.mat");
            var woodMat = LoadMat("Village_MI_WoodTrim.mat") ?? LoadMat("Dark timber.mat");

            float totalH = stories * 2.5f;
            StructuralBox("Foundation Plinth", new Vector3(0f, 0.15f, 0f), new Vector3(6.5f, 0.5f, 6.5f), stone, house, false);

            for (int level = 0; level < stories; level++)
            {
                for (int side = 0; side < 4; side++)
                {
                    for (int sec = 0; sec < 2; sec++)
                    {
                        bool isDoor = side == 0 && sec == 0 && level == 0;
                        string wallFbx = ChooseWallFbx(style, level, isDoor);
                        var piece = PlaceProp("Village/FBX/" + wallFbx, Vector3.zero, 0f, 1f, house, false);
                        if (!piece) continue;
                        FitBox(piece, new Vector3(3.02f, 2.52f, 0.30f));
                        var rot = Quaternion.Euler(0f, side * 90f, 0f);
                        piece.transform.localPosition = rot * new Vector3(sec == 0 ? -1.5f : 1.5f, 0.35f + level * 2.5f, -3.0f);
                        piece.transform.localRotation = rot;
                    }
                }
            }

            // Opaque interior core so light never leaks through modular seams + solid wall collision
            StructuralBox("Building Core & Collider", new Vector3(0f, 0.35f + totalH * 0.5f, 0f), new Vector3(5.8f, totalH, 5.8f), backingMat, house, true);

            // Corner Exterior Trim Columns & Upper Windows
            string cornerFbx = (style == ArchStyle.HordeTimber || style == ArchStyle.GoblinTimber)
                ? "Corner_ExteriorWide_Wood.fbx" : "Corner_ExteriorWide_Brick.fbx";
            for (int side = 0; side < 4; side++)
            {
                var face = new GameObject("Facade Detail " + side).transform;
                face.SetParent(house, false);
                var corner = PlaceProp("Village/FBX/" + cornerFbx, Vector3.zero, 0f, 1f, face, false);
                if (corner)
                {
                    FitBox(corner, new Vector3(0.55f, totalH + 0.1f, 0.55f));
                    corner.transform.localPosition = new Vector3(-3.05f, 0.35f, -3.05f);
                }

                if (stories >= 2)
                {
                    string winFbx = style == ArchStyle.Elven || style == ArchStyle.GothicBrick ? "Window_Thin_Round1.fbx" : "Window_Wide_Round1.fbx";
                    for (int w = 0; w < 2; w++)
                    {
                        var pane = PlaceProp("Village/FBX/" + winFbx, Vector3.zero, 0f, 1f, face, false);
                        if (pane)
                        {
                            FitBox(pane, new Vector3(1.05f, 1.25f, 0.2f));
                            pane.transform.localPosition = new Vector3(w == 0 ? -1.5f : 1.5f, 3.25f, -3.18f);
                        }
                    }
                }
                face.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
            }

            // Front Door, Frame & Porch Lantern
            var door = PlaceProp("Village/FBX/Door_1_Round.fbx", Vector3.zero, 0f, 1f, house, false);
            if (door) { FitBox(door, new Vector3(1.05f, 2.15f, 0.16f)); door.transform.localPosition = new Vector3(-1.5f, 0.35f, -3.18f); }
            var frame = PlaceProp("Village/FBX/DoorFrame_Round_Brick.fbx", Vector3.zero, 0f, 1f, house, false);
            if (frame) { FitBox(frame, new Vector3(1.35f, 2.35f, 0.28f)); frame.transform.localPosition = new Vector3(-1.5f, 0.35f, -3.16f); }

            var lantern = PlaceProp("FantasyProps/FBX/Lantern_Wall.fbx", new Vector3(-0.35f, 2.15f, -3.32f), 180f, 1.1f, house, false);
            if (lantern) FitHeight(lantern, 0.55f);

            // Roof & Chimney (rotated 90 deg so tiled slope faces front/back and stepped gable encloses sides)
            StructuralBox("House Gable Attic Lower", new Vector3(0f, 0.35f + totalH + 0.6f, 0f), new Vector3(5.6f, 1.25f, 3.8f), backingMat, house, false);
            StructuralBox("House Gable Attic Ridge", new Vector3(0f, 0.35f + totalH + 1.45f, 0f), new Vector3(5.4f, 1.0f, 1.8f), backingMat, house, false);
            string roofFbx = style == ArchStyle.HordeTimber ? "Roof_Log.fbx"
                : style == ArchStyle.GoblinTimber ? "Roof_Wooden_2x1.fbx"
                : "Roof_RoundTiles_6x8.fbx";
            var roof = PlaceProp("Village/FBX/" + roofFbx, Vector3.zero, 0f, 1f, house, false);
            if (roof)
            {
                FitBox(roof, new Vector3(7.4f, 2.65f, 7.3f));
                roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                roof.transform.localPosition = new Vector3(0f, 0.3f + totalH, 0f);
            }

            if (style != ArchStyle.Elven)
            {
                var chimney = PlaceProp("Village/FBX/Prop_Chimney.fbx", new Vector3(2.0f, 0.3f + totalH + 1.1f, 1.2f), 0f, 1f, house, false);
                if (chimney) FitHeight(chimney, 1.9f);
            }

            if (style == ArchStyle.GothicBrick)
            {
                var vine = PlaceProp("Village/FBX/Prop_Vine1.fbx", new Vector3(1.4f, 1.2f, -3.22f), 0f, 1.1f, house, false);
                if (vine) FitHeight(vine, 2.8f);
            }

            // Timber belt trim
            StructuralBox("Timber Eave Belt", new Vector3(0f, 0.35f + 2.5f, 0f), new Vector3(6.35f, 0.18f, 6.35f), woodMat, house, false);

            house.position = worldPos;
            house.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);
            return house.gameObject;
        }

        public static GameObject BuildInn(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent, ArchStyle style)
        {
            var inn = new GameObject(name).transform;
            inn.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var backingMat = style == ArchStyle.HordeTimber || style == ArchStyle.GoblinTimber
                ? LoadMat("Dark timber.mat")
                : style == ArchStyle.GothicBrick || style == ArchStyle.DwarfStone
                    ? LoadMat("Village_MI_UnevenBrick.mat")
                    : LoadMat("Village_MI_Plaster.mat");

            // Wide 12m x 7m 2-Story Tavern Hall
            StructuralBox("Inn Foundation", new Vector3(0f, 0.15f, 0f), new Vector3(12.4f, 0.5f, 7.4f), stone, inn, false);
            StructuralBox("Inn Core & Collider", new Vector3(0f, 2.85f, 0f), new Vector3(11.6f, 5.1f, 6.6f), backingMat, inn, true);

            // Front & Back 4-bay walls (12m wide)
            for (int level = 0; level < 2; level++)
            {
                for (int fb = -1; fb <= 1; fb += 2)
                {
                    float z = fb * 3.5f;
                    float yaw = fb < 0 ? 0f : 180f;
                    for (int bay = 0; bay < 4; bay++)
                    {
                        bool isMainDoor = fb < 0 && level == 0 && (bay == 1 || bay == 2);
                        string wallFbx = ChooseWallFbx(style, level, isMainDoor && bay == 1);
                        if (fb < 0 && level == 0 && bay == 2) wallFbx = "Wall_Arch.fbx";
                        var piece = PlaceProp("Village/FBX/" + wallFbx, Vector3.zero, 0f, 1f, inn, false);
                        if (!piece) continue;
                        FitBox(piece, new Vector3(3.02f, 2.52f, 0.32f));
                        piece.transform.localPosition = new Vector3(-4.5f + bay * 3.0f, 0.35f + level * 2.5f, z);
                        piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }

                // Left & Right 2-bay side walls (7m deep)
                for (int lr = -1; lr <= 1; lr += 2)
                {
                    float x = lr * 6.0f;
                    float yaw = lr < 0 ? 90f : -90f;
                    for (int bay = 0; bay < 2; bay++)
                    {
                        string wallFbx = ChooseWallFbx(style, level, false);
                        var piece = PlaceProp("Village/FBX/" + wallFbx, Vector3.zero, 0f, 1f, inn, false);
                        if (!piece) continue;
                        FitBox(piece, new Vector3(3.52f, 2.52f, 0.32f));
                        piece.transform.localPosition = new Vector3(x, 0.35f + level * 2.5f, -1.75f + bay * 3.5f);
                        piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                    }
                }
            }

            // Double Entrance Doors, Upper Balcony & Porch Canopy
            var door = PlaceProp("Village/FBX/Door_2_Round.fbx", Vector3.zero, 0f, 1f, inn, false);
            if (door) { FitBox(door, new Vector3(1.25f, 2.2f, 0.18f)); door.transform.localPosition = new Vector3(-1.5f, 0.35f, -3.65f); }

            var balcony = PlaceProp("Village/FBX/Balcony_Cross_Straight.fbx", Vector3.zero, 0f, 1f, inn, false);
            if (balcony) { FitBox(balcony, new Vector3(5.6f, 1.25f, 1.5f)); balcony.transform.localPosition = new Vector3(0f, 2.85f, -4.15f); }

            // Grand Inn Roof + Dormers + Twin Chimneys (rotated 90 deg so tiled slope faces front/back)
            StructuralBox("Inn Gable Attic Lower", new Vector3(0f, 5.95f, 0f), new Vector3(11.4f, 1.5f, 4.4f), backingMat, inn, false);
            StructuralBox("Inn Gable Attic Ridge", new Vector3(0f, 7.05f, 0f), new Vector3(11.2f, 1.2f, 2.1f), backingMat, inn, false);
            string roofFbx = style == ArchStyle.HordeTimber ? "Roof_Log.fbx"
                : style == ArchStyle.GoblinTimber ? "Roof_Wooden_2x1.fbx"
                : "Roof_RoundTiles_8x12.fbx";
            var roof = PlaceProp("Village/FBX/" + roofFbx, Vector3.zero, 0f, 1f, inn, false);
            if (roof)
            {
                FitBox(roof, new Vector3(8.6f, 3.2f, 13.4f));
                roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                roof.transform.localPosition = new Vector3(0f, 5.3f, 0f);
            }
            if (style != ArchStyle.HordeTimber)
            {
                foreach (float dx in new[] { -3.2f, 3.2f })
                {
                    var dormer = PlaceProp("Village/FBX/Roof_Dormer_RoundTile.fbx", Vector3.zero, 0f, 1f, inn, false);
                    if (dormer) { FitBox(dormer, new Vector3(2.0f, 1.7f, 2.2f)); dormer.transform.localPosition = new Vector3(dx, 5.9f, -2.9f); }
                }
            }
            foreach (float cx in new[] { -4.4f, 4.4f })
            {
                var chim = PlaceProp("Village/FBX/Prop_Chimney2.fbx", new Vector3(cx, 6.8f, 1.4f), 0f, 1f, inn, false);
                if (chim) FitHeight(chim, 2.2f);
            }

            // Outdoor Tavern Patio Furniture (offset to sides so front door at x=0..-1.5 is wide open)
            PlaceProp("FantasyProps/FBX/Table_Large.fbx", new Vector3(3.8f, 0.25f, -5.2f), 0f, 1.0f, inn, true);
            PlaceProp("FantasyProps/FBX/Bench.fbx", new Vector3(3.8f, 0.25f, -4.3f), 0f, 1.0f, inn, false);
            PlaceProp("FantasyProps/FBX/Bench.fbx", new Vector3(3.8f, 0.25f, -6.1f), 180f, 1.0f, inn, false);
            PlaceProp("FantasyProps/FBX/Barrel.fbx", new Vector3(-4.6f, 0.25f, -4.3f), 15f, 1.05f, inn, true);
            PlaceProp("FantasyProps/FBX/Barrel_Apples.fbx", new Vector3(-3.6f, 0.25f, -4.4f), -25f, 1.0f, inn, true);

            AddPointLight("Tavern Porch Warmth", new Vector3(0f, 3.1f, -4.6f), new Color(1f, 0.76f, 0.36f), 13f, 2.6f, inn);

            inn.position = worldPos;
            inn.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);
            return inn.gameObject;
        }

        public static GameObject BuildSmithy(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent, bool hordeStyle)
        {
            var smithy = new GameObject(name).transform;
            smithy.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var brick = LoadMat("Village_MI_UnevenBrick.mat");
            var wood = LoadMat("Dark timber.mat");
            var embers = LoadMat("Camp embers.mat");

            // Foundation covering both enclosed workshop and covered front forge bay
            StructuralBox("Smithy Stone Pad", new Vector3(0f, 0.12f, -1.2f), new Vector3(9.4f, 0.45f, 8.8f), stone, smithy, false);

            // Enclosed rear stone forge hall (z in [-0.5, +3.0])
            StructuralBox("Forge Hall Core", new Vector3(0f, 2.1f, 1.2f), new Vector3(8.6f, 3.8f, 3.8f), brick, smithy, true);
            for (int bay = 0; bay < 3; bay++)
            {
                var frontWall = PlaceProp("Village/FBX/" + (bay == 1 ? "Wall_Arch.fbx" : "Wall_UnevenBrick_Straight.fbx"), Vector3.zero, 0f, 1f, smithy, false);
                if (frontWall) { FitBox(frontWall, new Vector3(3.0f, 3.6f, 0.35f)); frontWall.transform.localPosition = new Vector3(-3.0f + bay * 3.0f, 0.3f, -0.75f); }
                var backWall = PlaceProp("Village/FBX/Wall_UnevenBrick_Straight.fbx", Vector3.zero, 180f, 1f, smithy, false);
                if (backWall) { FitBox(backWall, new Vector3(3.0f, 3.6f, 0.35f)); backWall.transform.localPosition = new Vector3(-3.0f + bay * 3.0f, 0.3f, 3.15f); }
            }

            StructuralBox("Forge Gable Attic", new Vector3(0f, 4.45f, 1.2f), new Vector3(8.4f, 1.3f, 2.4f), brick, smithy, false);
            var mainRoof = PlaceProp("Village/FBX/" + (hordeStyle ? "Roof_Log.fbx" : "Roof_RoundTiles_6x8.fbx"), Vector3.zero, 0f, 1f, smithy, false);
            if (mainRoof)
            {
                FitBox(mainRoof, new Vector3(5.0f, 2.4f, 9.8f));
                mainRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                mainRoof.transform.localPosition = new Vector3(0f, 3.9f, 1.2f);
            }

            // Covered Open-Air Forge Awning in Front (z in [-5.0, -0.8])
            var awning = PlaceProp("Village/FBX/Roof_Wooden_2x1.fbx", Vector3.zero, 0f, 1f, smithy, false);
            if (awning) { FitBox(awning, new Vector3(9.2f, 1.3f, 4.4f)); awning.transform.localPosition = new Vector3(0f, 3.2f, -2.8f); }
            foreach (float px in new[] { -4.1f, 4.1f })
            {
                var post = PlaceProp("Village/FBX/Prop_Support.fbx", Vector3.zero, 0f, 1f, smithy, false);
                if (post) { FitBox(post, new Vector3(0.35f, 3.1f, 0.35f)); post.transform.localPosition = new Vector3(px, 0.3f, -4.6f); }
            }

            // Tall Brick Smokestack & Glowing Molten Hearth
            var stack = PlaceProp("Village/FBX/Prop_Chimney2.fbx", new Vector3(-2.6f, 4.8f, 0.4f), 0f, 1f, smithy, false);
            if (stack) FitHeight(stack, 3.4f);
            StructuralBox("Forge Stone Hearth", new Vector3(-2.6f, 0.85f, -1.8f), new Vector3(2.2f, 1.2f, 1.6f), brick, smithy, true);
            StructuralBox("Glowing Forge Coals", new Vector3(-2.6f, 1.52f, -1.8f), new Vector3(1.5f, 0.22f, 1.1f), embers, smithy, false);
            AddPointLight("Forge Fire Glow", new Vector3(-2.6f, 2.1f, -2.2f), new Color(1f, 0.48f, 0.12f), 13f, 3.2f, smithy);

            // Real Anvil, Loaded Weapon Rack, Quench Bucket & Crates
            if (hordeStyle)
                PlaceProp("FantasyProps/FBX/Anvil_Log.fbx", new Vector3(0.4f, 0.3f, -2.8f), 20f, 1.25f, smithy, true);
            else
                PlacePrefabPropLocal("Prefab_Medieval_Anvil.prefab", new Vector3(0.4f, 0.3f, -2.8f), 15f, 1.2f, smithy);

            PlacePrefabPropLocal("Prefab_WeaponRack_Loaded.prefab", new Vector3(3.1f, 0.3f, -1.6f), 0f, 1.1f, smithy);
            PlaceProp("FantasyProps/FBX/Bucket_Wooden_1.fbx", new Vector3(-0.7f, 0.3f, -2.6f), 0f, 1.15f, smithy, false);
            PlaceProp("FantasyProps/FBX/Crate_Wooden.fbx", new Vector3(3.3f, 0.3f, -3.5f), -25f, 1.05f, smithy, true);

            smithy.position = worldPos;
            smithy.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);
            return smithy.gameObject;
        }

        public static GameObject BuildSanctumHall(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent, ArchStyle style)
        {
            var hall = new GameObject(name).transform;
            hall.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var coreMat = style == ArchStyle.GothicBrick ? LoadMat("Village_MI_UnevenBrick.mat")
                : style == ArchStyle.HordeTimber ? LoadMat("Dark timber.mat")
                : LoadMat("Village_MI_Plaster.mat");

            // Stepped Sanctuary Platform + Exterior Stairs
            StructuralBox("Sanctum Dais", new Vector3(0f, 0.35f, 0f), new Vector3(11.2f, 0.8f, 10.2f), stone, hall, true);
            var stairs = PlaceProp("Village/FBX/Stairs_Exterior_Straight.fbx", Vector3.zero, 0f, 1f, hall, false);
            if (stairs) { FitBox(stairs, new Vector3(3.8f, 0.85f, 2.2f)); stairs.transform.localPosition = new Vector3(0f, 0f, -5.8f); }

            float h = 6.2f;
            StructuralBox("Sanctum Vault Core", new Vector3(0f, 0.75f + h * 0.5f, 0.4f), new Vector3(9.4f, h, 8.2f), coreMat, hall, true);

            // Tall Lancet Window Walls & Grand Arched Portico
            string lancetWall = style == ArchStyle.GothicBrick ? "Wall_UnevenBrick_Window_Thin_Round.fbx" : "Wall_Plaster_Window_Thin_Round.fbx";
            for (int bay = -1; bay <= 1; bay++)
            {
                string frontPiece = bay == 0 ? "Wall_Arch.fbx" : lancetWall;
                var fw = PlaceProp("Village/FBX/" + frontPiece, Vector3.zero, 0f, 1f, hall, false);
                if (fw) { FitBox(fw, new Vector3(3.2f, h, 0.38f)); fw.transform.localPosition = new Vector3(bay * 3.2f, 0.75f, -3.75f); }
                var bw = PlaceProp("Village/FBX/" + lancetWall, Vector3.zero, 180f, 1f, hall, false);
                if (bw) { FitBox(bw, new Vector3(3.2f, h, 0.38f)); bw.transform.localPosition = new Vector3(bay * 3.2f, 0.75f, 4.55f); }
            }
            for (int lr = -1; lr <= 1; lr += 2)
            {
                for (int sb = 0; sb < 2; sb++)
                {
                    var sw = PlaceProp("Village/FBX/" + lancetWall, Vector3.zero, lr < 0 ? 90f : -90f, 1f, hall, false);
                    if (sw) { FitBox(sw, new Vector3(4.1f, h, 0.38f)); sw.transform.localPosition = new Vector3(lr * 4.8f, 0.75f, -1.6f + sb * 4.1f); }
                }
            }

            // Vaulted Roof + Central Belfry Spire
            StructuralBox("Sanctum Gable Attic Lower", new Vector3(0f, 0.75f + h + 0.8f, 0.4f), new Vector3(9.2f, 1.6f, 5.2f), coreMat, hall, false);
            StructuralBox("Sanctum Gable Attic Ridge", new Vector3(0f, 0.75f + h + 1.9f, 0.4f), new Vector3(9.0f, 1.2f, 2.5f), coreMat, hall, false);
            string roofFbx = style == ArchStyle.HordeTimber ? "Roof_Log.fbx" : "Roof_RoundTiles_8x10.fbx";
            var roof = PlaceProp("Village/FBX/" + roofFbx, Vector3.zero, 0f, 1f, hall, false);
            if (roof)
            {
                FitBox(roof, new Vector3(9.8f, 3.2f, 11.0f));
                roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                roof.transform.localPosition = new Vector3(0f, 0.7f + h, 0.4f);
            }

            if (style != ArchStyle.HordeTimber)
            {
                StructuralBox("Belfry Drum", new Vector3(0f, 0.7f + h + 2.6f, 1.2f), new Vector3(3.2f, 3.0f, 3.2f), coreMat, hall, false);
                var spire = PlaceProp("Village/FBX/Roof_Tower_RoundTiles.fbx", Vector3.zero, 0f, 1f, hall, false);
                if (spire) { FitBox(spire, new Vector3(4.2f, 4.4f, 4.2f)); spire.transform.localPosition = new Vector3(0f, 0.7f + h + 4.0f, 1.2f); }
            }

            if (style == ArchStyle.GothicBrick)
            {
                var vine = PlaceProp("Village/FBX/Prop_Vine4.fbx", new Vector3(-3.0f, 2.2f, -3.98f), 0f, 1f, hall, false);
                if (vine) FitHeight(vine, 3.6f);
            }

            hall.position = worldPos;
            hall.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);
            return hall.gameObject;
        }

        public static GameObject BuildTower(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent, bool hordeStyle, float heightScale = 1f)
        {
            var tower = new GameObject(name).transform;
            tower.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var brick = LoadMat("Village_MI_UnevenBrick.mat");
            var wood = LoadMat("Dark timber.mat");

            float tierH = 2.8f * heightScale;
            float totalShaftH = tierH * 3f;

            StructuralBox("Tower Plinth", new Vector3(0f, 0.25f, 0f), new Vector3(5.2f, 0.7f, 5.2f), stone, tower, false);
            StructuralBox("Tower Shaft Collider", new Vector3(0f, 0.5f + totalShaftH * 0.5f, 0f), new Vector3(4.3f, totalShaftH, 4.3f), hordeStyle ? wood : brick, tower, true);

            string cornerFbx = hordeStyle ? "Corner_ExteriorWide_Wood.fbx" : "Corner_ExteriorWide_Brick.fbx";
            for (int level = 0; level < 3; level++)
            {
                string wallFbx = level == 2 ? "Wall_UnevenBrick_Window_Thin_Round.fbx" : "Wall_UnevenBrick_Straight.fbx";
                for (int side = 0; side < 4; side++)
                {
                    var rot = Quaternion.Euler(0f, side * 90f, 0f);
                    var wall = PlaceProp("Village/FBX/" + wallFbx, Vector3.zero, 0f, 1f, tower, false);
                    if (wall)
                    {
                        FitBox(wall, new Vector3(4.4f, tierH + 0.05f, 0.35f));
                        wall.transform.localPosition = rot * new Vector3(0f, 0.5f + level * tierH, -2.18f);
                        wall.transform.localRotation = rot;
                    }
                    var corner = PlaceProp("Village/FBX/" + cornerFbx, Vector3.zero, 0f, 1f, tower, false);
                    if (corner)
                    {
                        FitBox(corner, new Vector3(0.65f, tierH + 0.08f, 0.65f));
                        corner.transform.localPosition = rot * new Vector3(-2.22f, 0.5f + level * tierH, -2.22f);
                        corner.transform.localRotation = rot;
                    }
                }
            }

            // Crown with real Conical/Pitched 3D Tower Roof!
            string roofFbx = hordeStyle ? "Roof_Log.fbx" : "Roof_Tower_RoundTiles.fbx";
            var roof = PlaceProp("Village/FBX/" + roofFbx, Vector3.zero, 0f, 1f, tower, false);
            if (roof)
            {
                FitBox(roof, new Vector3(5.8f, 4.4f * heightScale, 5.8f));
                roof.transform.localPosition = new Vector3(0f, 0.45f + totalShaftH, 0f);
            }

            var lantern = PlaceProp("FantasyProps/FBX/Lantern_Wall.fbx", new Vector3(0f, 3.2f, -2.45f), 180f, 1.2f, tower, false);
            if (lantern) FitHeight(lantern, 0.65f);

            tower.position = worldPos;
            tower.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);
            return tower.gameObject;
        }

        public static GameObject BuildGrandKeep(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent)
        {
            var keep = new GameObject(name).transform;
            keep.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var plaster = LoadMat("Village_MI_Plaster.mat");
            var brick = LoadMat("Village_MI_Brick.mat") ?? stone;
            var allianceBlue = LoadMat("Mat_Banner_AllianceBlue.mat") ?? LoadMat("Beacon gold.mat");

            // Raised Castle Terrace + Grand Royal Staircase
            StructuralBox("Royal Keep Terrace", new Vector3(0f, 0.55f, 0f), new Vector3(24.5f, 1.3f, 16.5f), stone, keep, true);
            var stairs = PlaceProp("Village/FBX/Stairs_Exterior_Straight.fbx", Vector3.zero, 0f, 1f, keep, false);
            if (stairs) { FitBox(stairs, new Vector3(6.4f, 1.35f, 3.4f)); stairs.transform.localPosition = new Vector3(0f, 0f, -9.6f); }

            // 3-Story Central Royal Great Hall (13m x 12m, 9.6m wall height + 4.2m pitched roof)
            StructuralBox("Central Great Hall Core", new Vector3(0f, 5.9f, 0.5f), new Vector3(13.0f, 9.5f, 11.6f), plaster, keep, true);
            for (int level = 0; level < 3; level++)
            {
                for (int bay = 0; bay < 4; bay++)
                {
                    string fbx = level == 0 && (bay == 1 || bay == 2) ? "Wall_Arch.fbx"
                        : level == 2 ? "Wall_Plaster_Window_Thin_Round.fbx" : "Wall_Plaster_Window_Wide_Round.fbx";
                    var fw = PlaceProp("Village/FBX/" + fbx, Vector3.zero, 0f, 1f, keep, false);
                    if (fw) { FitBox(fw, new Vector3(3.3f, 3.2f, 0.4f)); fw.transform.localPosition = new Vector3(-4.95f + bay * 3.3f, 1.15f + level * 3.15f, -5.35f); }
                    var bw = PlaceProp("Village/FBX/Wall_UnevenBrick_Window_Thin_Round.fbx", Vector3.zero, 180f, 1f, keep, false);
                    if (bw) { FitBox(bw, new Vector3(3.3f, 3.2f, 0.4f)); bw.transform.localPosition = new Vector3(-4.95f + bay * 3.3f, 1.15f + level * 3.15f, 6.35f); }
                }
            }

            StructuralBox("Keep Gable Attic Lower", new Vector3(0f, 11.5f, 0.5f), new Vector3(12.8f, 2.0f, 7.2f), plaster, keep, false);
            StructuralBox("Keep Gable Attic Ridge", new Vector3(0f, 13.0f, 0.5f), new Vector3(12.6f, 1.6f, 3.4f), plaster, keep, false);
            var keepRoof = PlaceProp("Village/FBX/Roof_RoundTiles_8x14.fbx", Vector3.zero, 0f, 1f, keep, false);
            if (keepRoof)
            {
                FitBox(keepRoof, new Vector3(13.2f, 4.2f, 14.6f));
                keepRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                keepRoof.transform.localPosition = new Vector3(0f, 10.6f, 0.5f);
            }

            var royalBalcony = PlaceProp("Village/FBX/Balcony_Cross_Straight.fbx", Vector3.zero, 0f, 1f, keep, false);
            if (royalBalcony) { FitBox(royalBalcony, new Vector3(6.8f, 1.4f, 1.8f)); royalBalcony.transform.localPosition = new Vector3(0f, 4.3f, -6.2f); }

            // Flanking East & West Castle Wings
            foreach (int side in new[] { -1, 1 })
            {
                StructuralBox("Keep Wing " + side, new Vector3(side * 9.0f, 4.3f, 0.8f), new Vector3(5.6f, 6.3f, 9.6f), brick, keep, true);
                StructuralBox("Keep Wing Attic " + side, new Vector3(side * 9.0f, 8.1f, 0.8f), new Vector3(5.4f, 1.5f, 5.6f), brick, keep, false);
                var wingRoof = PlaceProp("Village/FBX/Roof_RoundTiles_6x10.fbx", Vector3.zero, 0f, 1f, keep, false);
                if (wingRoof)
                {
                    FitBox(wingRoof, new Vector3(10.6f, 2.8f, 6.6f));
                    wingRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    wingRoof.transform.localPosition = new Vector3(side * 9.0f, 7.4f, 0.8f);
                }

                // Royal Alliance Banners flanking the grand portico
                StructuralBox("Alliance Royal Banner", new Vector3(side * 4.2f, 6.2f, -5.65f), new Vector3(1.5f, 4.2f, 0.12f), allianceBlue, keep, false);
            }

            AddPointLight("Royal Portico Light", new Vector3(0f, 4.8f, -6.5f), new Color(1f, 0.86f, 0.52f), 18f, 3.4f, keep);

            keep.position = worldPos;
            keep.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);

            // 4 Soaring Corner Citadel Spires in world space anchored to the Keep corners
            foreach (var c in new[] { new Vector3(-11.2f, 0f, -6.5f), new Vector3(11.2f, 0f, -6.5f), new Vector3(-11.2f, 0f, 6.8f), new Vector3(11.2f, 0f, 6.8f) })
            {
                Vector3 wp = keep.TransformPoint(c);
                BuildTower("Citadel Keep Spire", wp, faceTarget, keep, false, 1.32f);
            }

            return keep.gameObject;
        }

        public static GameObject BuildHordeWarHall(string name, Vector3 worldPos, Vector3 faceTarget, Transform parent)
        {
            var hall = new GameObject(name).transform;
            hall.SetParent(parent, false);

            var stone = LoadMat("Foundation.mat");
            var wood = LoadMat("Dark timber.mat");
            var brick = LoadMat("Village_MI_UnevenBrick.mat");
            var crimson = LoadMat("Faction crimson.mat");

            // Heavy Stepped Stone & Timber Fortress Dais
            StructuralBox("War-Hall Dais", new Vector3(0f, 0.55f, 0f), new Vector3(23.5f, 1.3f, 15.5f), stone, hall, true);
            var stairs = PlaceProp("Village/FBX/Stairs_Exterior_Straight.fbx", Vector3.zero, 0f, 1f, hall, false);
            if (stairs) { FitBox(stairs, new Vector3(6.2f, 1.35f, 3.2f)); stairs.transform.localPosition = new Vector3(0f, 0f, -9.1f); }

            // Monumental Timber & Stone Great Hall
            StructuralBox("War-Hall Core", new Vector3(0f, 5.1f, 0.5f), new Vector3(15.2f, 7.8f, 11.4f), wood, hall, true);
            for (int level = 0; level < 2; level++)
            {
                for (int bay = 0; bay < 4; bay++)
                {
                    string fbx = level == 0 && (bay == 1 || bay == 2) ? "Wall_Arch.fbx" : "Wall_UnevenBrick_Straight.fbx";
                    var fw = PlaceProp("Village/FBX/" + fbx, Vector3.zero, 0f, 1f, hall, false);
                    if (fw) { FitBox(fw, new Vector3(3.8f, 3.9f, 0.42f)); fw.transform.localPosition = new Vector3(-5.7f + bay * 3.8f, 1.15f + level * 3.85f, -5.25f); }
                }
            }

            // Tiered Double Log Roofs (Classic Horde Great-Hall silhouette!)
            StructuralBox("War-Hall Lower Attic", new Vector3(0f, 9.8f, 0.5f), new Vector3(14.8f, 2.2f, 7.0f), wood, hall, false);
            StructuralBox("War-Hall Upper Attic", new Vector3(0f, 12.0f, 0.5f), new Vector3(10.4f, 1.8f, 4.4f), wood, hall, false);
            var lowerRoof = PlaceProp("Village/FBX/Roof_Log.fbx", Vector3.zero, 0f, 1f, hall, false);
            if (lowerRoof)
            {
                FitBox(lowerRoof, new Vector3(13.2f, 3.4f, 17.4f));
                lowerRoof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                lowerRoof.transform.localPosition = new Vector3(0f, 8.8f, 0.5f);
            }
            var upperCrown = PlaceProp("Village/FBX/Roof_Log.fbx", Vector3.zero, 0f, 1f, hall, false);
            if (upperCrown)
            {
                FitBox(upperCrown, new Vector3(8.6f, 3.0f, 11.2f));
                upperCrown.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                upperCrown.transform.localPosition = new Vector3(0f, 11.2f, 0.5f);
            }

            // Heavy Timber Buttresses & Crimson Horde Banners
            foreach (float bx in new[] { -6.8f, -3.6f, 3.6f, 6.8f })
            {
                var rib = PlaceProp("Village/FBX/Prop_Support.fbx", Vector3.zero, 0f, 1f, hall, false);
                if (rib) { FitBox(rib, new Vector3(0.65f, 8.2f, 0.65f)); rib.transform.localPosition = new Vector3(bx, 1.15f, -5.55f); }
            }
            foreach (int side in new[] { -1, 1 })
                StructuralBox("Crimson War Banner", new Vector3(side * 4.4f, 5.8f, -5.7f), new Vector3(1.7f, 4.4f, 0.14f), crimson, hall, false);

            AddPointLight("War-Hall Hearth Glow", new Vector3(0f, 4.6f, -6.2f), new Color(1f, 0.44f, 0.12f), 18f, 3.6f, hall);

            hall.position = worldPos;
            hall.rotation = Quaternion.Euler(0f, FaceToward(worldPos, faceTarget), 0f);

            foreach (var c in new[] { new Vector3(-10.2f, 0f, -5.2f), new Vector3(10.2f, 0f, -5.2f) })
            {
                Vector3 wp = hall.TransformPoint(c);
                BuildTower("War-Hall Bastion Tower", wp, faceTarget, hall, true, 1.2f);
            }

            return hall.gameObject;
        }

        public static GameObject BuildGatehouse(string name, Vector3 worldPos, float yaw, Transform parent, bool hordeStyle, float clearSpan = 9.0f, bool gothic = false)
        {
            var gate = new GameObject(name).transform;
            gate.SetParent(parent, false);
            gate.position = worldPos;
            gate.rotation = Quaternion.Euler(0f, yaw, 0f);

            float towerX = clearSpan * 0.5f + 2.5f;
            Vector3 lookFwd = worldPos + gate.rotation * Vector3.back * 12f;

            // Left & Right Flanking Guard Towers (leaving full clearSpan m unobstructed road in center!)
            BuildTower(name + " Tower L", GroundedVec(gate.TransformPoint(new Vector3(-towerX, 0f, 0f))), lookFwd, gate, hordeStyle, 1.0f);
            BuildTower(name + " Tower R", GroundedVec(gate.TransformPoint(new Vector3(towerX, 0f, 0f))), lookFwd, gate, hordeStyle, 1.0f);

            // Overhead Arched Bridge Gallery (starts at y = 5.2m so tall mounts & flying mounts pass easily underneath!)
            var bridge = new GameObject("Gatehouse Overhead Gallery").transform;
            bridge.SetParent(gate, false);

            var arch = PlaceProp("Village/FBX/Wall_Arch.fbx", Vector3.zero, 0f, 1f, bridge, false);
            if (arch) { FitBox(arch, new Vector3(clearSpan + 1.0f, 3.0f, 2.8f)); arch.transform.localPosition = new Vector3(0f, 5.2f, 0f); }

            string galleryWall = gothic ? "Wall_UnevenBrick_Window_Thin_Round.fbx" : "Wall_Plaster_Window_Thin_Round.fbx";
            for (int fb = -1; fb <= 1; fb += 2)
            {
                var gw = PlaceProp("Village/FBX/" + (hordeStyle ? "Wall_UnevenBrick_Straight.fbx" : galleryWall), Vector3.zero, fb < 0 ? 0f : 180f, 1f, bridge, false);
                if (gw) { FitBox(gw, new Vector3(clearSpan + 1.0f, 2.4f, 0.35f)); gw.transform.localPosition = new Vector3(0f, 6.8f, fb * 1.4f); }
            }

            string roofFbx = hordeStyle ? "Roof_Log.fbx" : "Roof_RoundTiles_4x8.fbx";
            var roof = PlaceProp("Village/FBX/" + roofFbx, Vector3.zero, 0f, 1f, bridge, false);
            if (roof)
            {
                FitBox(roof, new Vector3(3.8f, 2.2f, clearSpan + 1.6f));
                roof.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                roof.transform.localPosition = new Vector3(0f, 9.1f, 0f);
            }

            var bannerMat = hordeStyle ? LoadMat("Faction crimson.mat") : LoadMat("Mat_Banner_AllianceBlue.mat");
            if (bannerMat)
            {
                StructuralBox("Gate Banner Front", new Vector3(0f, 6.6f, -1.65f), new Vector3(1.5f, 2.8f, 0.1f), bannerMat, bridge, false);
                StructuralBox("Gate Banner Back", new Vector3(0f, 6.6f, 1.65f), new Vector3(1.5f, 2.8f, 0.1f), bannerMat, bridge, false);
            }

            AddPointLight("Gatehouse Passage Light", new Vector3(0f, 5.4f, 0f),
                hordeStyle ? new Color(1f, 0.5f, 0.16f) : new Color(1f, 0.85f, 0.5f), 14f, 2.5f, bridge);

            return gate.gameObject;
        }

        public static void BuildCurtainWall(Vector3 fromWorld, Vector3 toWorld, Transform parent, bool hordeStyle)
        {
            float dist = Vector2.Distance(new Vector2(fromWorld.x, fromWorld.z), new Vector2(toWorld.x, toWorld.z));
            int segments = Mathf.Max(1, Mathf.RoundToInt(dist / 4.2f));
            var wallGroup = new GameObject(hordeStyle ? "Fortress Palisade Run" : "Stone Curtain Wall").transform;
            wallGroup.SetParent(parent, false);

            var mat = hordeStyle ? LoadMat("Dark timber.mat") : LoadMat("Village_MI_UnevenBrick.mat");
            float segLen = dist / segments;
            float yaw = Mathf.Atan2(toWorld.x - fromWorld.x, toWorld.z - fromWorld.z) * Mathf.Rad2Deg + 90f;

            for (int i = 0; i < segments; i++)
            {
                float t = (i + 0.5f) / segments;
                Vector3 p = Vector3.Lerp(fromWorld, toWorld, t);
                p.y = WorldExpansionLayout.Height(p.x, p.z);

                var seg = new GameObject("Wall Segment " + i).transform;
                seg.SetParent(wallGroup, false);
                seg.position = p;
                seg.rotation = Quaternion.Euler(0f, yaw, 0f);

                StructuralBox("Wall Core Collider", new Vector3(0f, 1.8f, 0f), new Vector3(segLen + 0.15f, 3.8f, 0.95f), mat, seg, true);
                var faceA = PlaceProp("Village/FBX/" + (hordeStyle ? "Prop_WoodenFence_Single.fbx" : "Wall_UnevenBrick_Straight.fbx"), Vector3.zero, 0f, 1f, seg, false);
                if (faceA) { FitBox(faceA, new Vector3(segLen + 0.1f, 3.8f, 0.45f)); faceA.transform.localPosition = new Vector3(0f, -0.1f, -0.35f); }
                var faceB = PlaceProp("Village/FBX/" + (hordeStyle ? "Prop_WoodenFence_Single.fbx" : "Wall_UnevenBrick_Straight.fbx"), Vector3.zero, 180f, 1f, seg, false);
                if (faceB) { FitBox(faceB, new Vector3(segLen + 0.1f, 3.8f, 0.45f)); faceB.transform.localPosition = new Vector3(0f, -0.1f, 0.35f); }

                var pilaster = PlaceProp("Village/FBX/" + (hordeStyle ? "Corner_ExteriorWide_Wood.fbx" : "Corner_ExteriorWide_Brick.fbx"), Vector3.zero, 0f, 1f, seg, false);
                if (pilaster) { FitBox(pilaster, new Vector3(0.55f, 4.1f, 1.15f)); pilaster.transform.localPosition = new Vector3(-segLen * 0.5f, -0.1f, 0f); }
            }
        }

        static void PlaceFenceLine(string relativeFbx, Vector3 fromWorld, Vector3 toWorld, Transform parent, float segWidth)
        {
            float dist = Vector2.Distance(new Vector2(fromWorld.x, fromWorld.z), new Vector2(toWorld.x, toWorld.z));
            int count = Mathf.Max(1, Mathf.RoundToInt(dist / segWidth));
            float actualW = dist / count;
            float yaw = Mathf.Atan2(toWorld.x - fromWorld.x, toWorld.z - fromWorld.z) * Mathf.Rad2Deg + 90f;

            for (int i = 0; i < count; i++)
            {
                Vector3 p = Vector3.Lerp(fromWorld, toWorld, (i + 0.5f) / count);
                p.y = WorldExpansionLayout.Height(p.x, p.z);
                var f = PlaceProp(relativeFbx, p, yaw, 1f, parent, true);
                if (f) FitBox(f, new Vector3(actualW, 1.45f, 0.25f));
            }
        }

        static void BuildStreetLamp(string name, Vector3 worldPos, Transform parent, Color lightColor)
        {
            var lamp = new GameObject(name).transform;
            lamp.SetParent(parent, false);
            lamp.position = worldPos;

            var post = PlaceProp("Village/FBX/Prop_Support.fbx", Vector3.zero, 0f, 1f, lamp, false);
            if (post) FitBox(post, new Vector3(0.24f, 3.1f, 0.24f));
            var lantern = PlaceProp("FantasyProps/FBX/Lantern_Wall.fbx", new Vector3(0f, 2.75f, 0.22f), 0f, 1.15f, lamp, false);
            if (lantern) FitHeight(lantern, 0.6f);
            AddPointLight("Street Lamp Glow", new Vector3(0f, 2.85f, 0.3f), lightColor, 11f, 2.3f, lamp);
        }

        static void BuildBonfireHearth(string name, Vector3 worldPos, Transform parent, Color glow, float scale)
        {
            var hearth = new GameObject(name).transform;
            hearth.SetParent(parent, false);
            hearth.position = worldPos;

            var rock = LoadMat("Nature_Rock.mat");
            var embers = LoadMat("Camp embers.mat");
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 0.25f;
                var stone = PlaceProp("Nature/Rock_" + (i % 4 + 1) + ".fbx", new Vector3(Mathf.Sin(a) * 1.1f * scale, 0f, Mathf.Cos(a) * 1.1f * scale), i * 45f, 1f, hearth, false);
                if (stone) FitHeight(stone, 0.55f * scale);
            }
            StructuralBox("Ember Bed", new Vector3(0f, 0.18f, 0f), new Vector3(1.5f * scale, 0.3f, 1.5f * scale), embers ?? rock, hearth, true);
            AddPointLight("Bonfire Light", new Vector3(0f, 1.4f * scale, 0f), glow, 16f, 3.6f, hearth);
        }

        // =========================================================================
        // CLEANUP, GROUNDING & STARTER ENEMY / NPC HELPERS
        // =========================================================================
        static void StripAllLegacyPlaceholders()
        {
            string[] placeholderRoots =
            {
                "08 - Highspire Keep Alliance Capital",
                "09 - Gor'Kresh Stronghold Horde Capital",
                "16 - Dwarf Starter Frostpeak Foothills",
                "17 - High Elf Starter Silverglen Sanctum",
                "18 - Undead Starter Deathknell Hollow",
                "19 - Goblin Starter Rustwater Scrapworks",
                "07 - Redridge Frontier Landmark",
                "06 - Scarlet Crypts Landmark"
            };

            string[] stripExactPrefixes =
            {
                "Highspire Courtyard Plaza", "Bastion Tower", "Highspire Gate Header", "Royal Alliance Banner", "Lion Crest Pedestal",
                "Gor'Kresh War Ground", "Horde War Tower", "Horde Gate Log Header", "Horde War Banner", "Warchief Great Hearth",
                "Dwarven Forge Platform", "Great Dwarven Furnace", "Mountain Great Anvil", "Dwarven Ale Cask",
                "Moonstone Dais",
                "Deathknell Crypt Platform", "Mausoleum Pillar", "Mausoleum Gate Header",
                "Rustwater Workshop Deck", "Steam Boiler Engine", "Lantern Post",
                "Horde Palisade Spike", "Frontier Campfire",
                "Cathedral Arch Left Pillar", "Cathedral Arch Right Pillar", "Cathedral Arch Header"
            };

            foreach (string groupName in placeholderRoots)
            {
                var group = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == groupName);
                if (!group) continue;

                // Ensure group root itself sits cleanly at terrain height
                Vector3 gp = group.position;
                gp.y = WorldExpansionLayout.Height(gp.x, gp.z);
                group.position = gp;

                var toDestroy = new List<GameObject>();
                foreach (Transform child in group)
                {
                    if (child.GetComponentInChildren<TownNPC>(true) ||
                        child.GetComponentInChildren<EnemyController>(true) ||
                        child.GetComponentInChildren<Mailbox>(true) ||
                        child.GetComponentInChildren<DungeonPortal>(true) ||
                        child.GetComponentInChildren<FlightMaster>(true) ||
                        child.GetComponentInChildren<LootChest>(true))
                        continue;

                    if (stripExactPrefixes.Any(prefix => child.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        toDestroy.Add(child.gameObject);
                }
                foreach (var go in toDestroy)
                    Object.DestroyImmediate(go);
            }
        }

        static void ArrangeStarterEnemyCamp(string groupName, string idPrefix, Vector2[] campPositions, bool includeOutcastBoss = false)
        {
            var group = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == groupName);
            if (!group) return;

            var existing = group.GetComponentsInChildren<EnemyController>(true).ToList();
            if (existing.Count == 0) return;

            var template = existing[0];
            while (existing.Count < campPositions.Length)
            {
                var clone = Object.Instantiate(template.gameObject, group);
                clone.name = template.gameObject.name + " " + (existing.Count + 1);
                var ec = clone.GetComponent<EnemyController>();
                if (ec) ec.enemyId = $"{idPrefix}-{existing.Count + 1}";
                existing.Add(ec);
            }

            for (int i = 0; i < campPositions.Length && i < existing.Count; i++)
            {
                var ec = existing[i];
                if (!ec) continue;
                Vector3 p = Grounded(campPositions[i].x, campPositions[i].y);
                ec.transform.position = p;
                ec.transform.rotation = Quaternion.Euler(0f, (i * 95f) % 360f, 0f);
                EditorUtility.SetDirty(ec);
            }

            // For Camp Bloodrock's 3rd quest ("Chieftain of the Crag"), ensure an actual Outcast Chieftain mini-boss exists!
            if (includeOutcastBoss && existing.Count >= 4)
            {
                var bossEc = existing[3];
                bossEc.enemyId = "redridge-outcast-boss";
                bossEc.isBoss = true;
                bossEc.gameObject.name = "Outcast Chieftain Karg";
                bossEc.transform.localScale = template.transform.localScale * 1.18f;
                var comb = bossEc.GetComponent<Combatant>();
                if (comb)
                {
                    comb.displayName = "Outcast Chieftain Karg";
                    comb.maxHealth = 165f;
                    EditorUtility.SetDirty(comb);
                }
                EditorUtility.SetDirty(bossEc);
            }
        }

        static void RepositionTownNPC(string npcName, Vector3 groundedWorldPos, float yaw)
        {
            var npc = Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(n => string.Equals(n.npcName, npcName, StringComparison.OrdinalIgnoreCase));
            if (!npc) return;
            npc.transform.position = groundedWorldPos + Vector3.up * 0.05f;
            npc.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            EditorUtility.SetDirty(npc.transform);
        }

        static void RepositionNamedObject(string objName, Vector3 worldPos)
        {
            var tr = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == objName);
            if (!tr) return;
            tr.position = worldPos;
            EditorUtility.SetDirty(tr);
        }

        static void RepositionFlightMaster(string nodeKeyword, Vector3 groundedPos)
        {
            var fm = Object.FindObjectsByType<FlightMaster>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(f => f.nodeName != null && f.nodeName.IndexOf(nodeKeyword, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!fm) return;
            Vector3 p = groundedPos + Vector3.up * 0.08f;
            fm.transform.position = p;
            fm.flightStationPos = p;
            EditorUtility.SetDirty(fm);
        }

        static void SpawnExtraTownNPC(string prefabFile, Transform parent, string npcName, string roleTag, string greeting, Vector3 groundedPos, float yaw)
        {
            if (Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Any(n => string.Equals(n.npcName, npcName, StringComparison.OrdinalIgnoreCase)))
                return;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabDir + prefabFile);
            if (!prefab) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "NPC - " + npcName;
            go.transform.position = groundedPos + Vector3.up * 0.05f;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var npc = go.GetComponent<TownNPC>() ?? go.AddComponent<TownNPC>();
            npc.npcName = npcName;
            npc.roleTag = roleTag;
            npc.greeting = greeting;
            npc.nameplateHeight = 2.18f;
            EditorUtility.SetDirty(npc);
        }

        // =========================================================================
        // 11. UPGRADE GATHERING NODES, DUNGEON INTERIOR, BOSS ARENAS & PROPS
        // =========================================================================
        static void UpgradeGatheringNodesAndBossArenas(Transform archRoot, StringBuilder report)
        {
            StylizedRacialModelSystem.EnsureUnifiedAssetsReady(false);
            var crystalMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/RPG/Art/RacialMeshes/Mesh_ArcaneCrystal.asset");
            var stoneMat = LoadMat("Foundation.mat") ?? LoadMat("Village_MI_UnevenBrick.mat");
            var brickMat = LoadMat("Village_MI_UnevenBrick.mat") ?? stoneMat;
            var plasterMat = LoadMat("Village_MI_Plaster.mat") ?? stoneMat;
            var woodMat = LoadMat("Village_MI_WoodTrim.mat") ?? LoadMat("Dark timber.mat");
            var goldMat = LoadMat("Beacon gold.mat") ?? LoadMat("FantasyProps_MI_Trim_Metal.mat");
            var crystalMat = LoadMat("Runestone glow.mat") ?? LoadMat("Mat_Portal_Arcane.mat");
            var flameMat = LoadMat("Camp embers.mat") ?? LoadMat("Mat_Horde_WarFlame.mat");
            var flowerMat = LoadMat("Nature_Flowers.mat");
            var leafMat = LoadMat("Nature_Bush_Leaves.mat");
            var rockMat = LoadMat("Nature_Rock.mat");

            // A. Upgrade All 18 World Gathering Nodes (Herbs & Ore Veins) from Primitive Spheres/Cubes to 3D Meshes
            int upgradedNodes = 0;
            foreach (var gn in Object.FindObjectsByType<GatheringNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Vector3 p = gn.transform.position;
                p.y = WorldExpansionLayout.Height(p.x, p.z);
                gn.transform.position = p;

                // Strip primitive cube/sphere/cylinder MeshRenderer/MeshFilter on root AND children while preserving colliders and lights
                var oldVisual = gn.transform.Find("UpgradedNode3D");
                if (oldVisual) Object.DestroyImmediate(oldVisual.gameObject);

                var rootMr = gn.GetComponent<MeshRenderer>();
                if (rootMr) Object.DestroyImmediate(rootMr);
                var rootMf = gn.GetComponent<MeshFilter>();
                if (rootMf) Object.DestroyImmediate(rootMf);

                foreach (Transform ch in gn.transform)
                {
                    if (ch.name.Contains("Herb") || ch.name.Contains("Mineral") || ch.name.Contains("Ore"))
                    {
                        var mr = ch.GetComponent<MeshRenderer>();
                        if (mr) Object.DestroyImmediate(mr);
                        var mf = ch.GetComponent<MeshFilter>();
                        if (mf) Object.DestroyImmediate(mf);
                    }
                }

                var visRoot = new GameObject("UpgradedNode3D").transform;
                visRoot.SetParent(gn.transform, false);
                Vector3 gs = gn.transform.localScale;
                visRoot.localScale = new Vector3(
                    1f / Mathf.Max(0.1f, gs.x),
                    1f / Mathf.Max(0.1f, gs.y),
                    1f / Mathf.Max(0.1f, gs.z));

                if (gn.gatheringType == GatheringType.Herbalism)
                {
                    var f1 = PlaceProp("Nature/Flower_1_Clump.fbx", Vector3.zero, 0f, 1.35f, visRoot, false);
                    var f2 = PlaceProp("Nature/Flower_2_Clump.fbx", new Vector3(0.18f, 0f, -0.14f), 65f, 1.25f, visRoot, false);
                    var gr = PlaceProp("Nature/Grass_Common_Short.fbx", new Vector3(-0.12f, 0f, 0.12f), 140f, 1.1f, visRoot, false);
                    foreach (var r in visRoot.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials;
                        for (int m = 0; m < mats.Length; m++)
                            mats[m] = (m == 0 && leafMat) ? leafMat : (flowerMat ?? mats[m]);
                        r.sharedMaterials = mats;
                    }
                }
                else
                {
                    var rock = PlaceProp("Nature/Rock_2.fbx", Vector3.zero, (upgradedNodes * 47f) % 360f, 0.85f, visRoot, false);
                    if (rock)
                    {
                        FitBox(rock, new Vector3(1.25f, 0.82f, 1.15f));
                        if (rockMat) foreach (var r in rock.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = rockMat;
                    }
                    if (crystalMesh)
                    {
                        var vein = new GameObject("OreCrystalCluster");
                        vein.transform.SetParent(visRoot, false);
                        vein.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                        vein.transform.localScale = new Vector3(1.1f, 0.95f, 1.1f);
                        vein.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
                        var mr = vein.AddComponent<MeshRenderer>();
                        mr.sharedMaterial = gn.nodeName != null && (gn.nodeName.Contains("Gold") || gn.nodeName.Contains("Copper"))
                            ? (goldMat ?? crystalMat)
                            : (crystalMat ?? goldMat);
                    }
                }
                EditorUtility.SetDirty(gn);
                upgradedNodes++;
            }

            // B. Upgrade Silverglen Moonwell & Obelisks, Deathknell Cauldron & Gravestones, and Rustwater TNT Barrels
            var moonwell = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Moonwell Stone Basin");
            if (moonwell)
            {
                var mr = moonwell.GetComponent<MeshRenderer>();
                if (mr && stoneMat) mr.sharedMaterial = stoneMat;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI * 0.25f;
                    var rimRock = PlaceProp("Nature/Rock_" + (i % 4 + 1) + ".fbx",
                        moonwell.position + new Vector3(Mathf.Sin(a) * 3.1f, -0.25f, Mathf.Cos(a) * 3.1f), i * 45f, 1f, archRoot, false);
                    if (rimRock) FitHeight(rimRock, 0.75f);
                }
            }
            for (int ob = 1; ob <= 4; ob++)
            {
                var obTr = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == $"Moonstone Obelisk {ob}");
                if (obTr && crystalMesh)
                {
                    var mf = obTr.GetComponent<MeshFilter>();
                    var mr = obTr.GetComponent<MeshRenderer>();
                    if (mf) mf.sharedMesh = crystalMesh;
                    if (mr && crystalMat) mr.sharedMaterial = crystalMat;
                    obTr.position = Grounded(obTr.position.x, obTr.position.z);
                    obTr.localScale = new Vector3(1.8f, 3.4f, 1.8f);
                }
            }

            var oldCauldron = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Forsaken Blight Cauldron");
            if (oldCauldron)
            {
                var mr = oldCauldron.GetComponent<MeshRenderer>();
                if (mr) mr.enabled = false;
            }
            for (int g = 1; g <= 4; g++)
            {
                var grave = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.name == $"Weathered Gravestone {g}");
                if (grave)
                {
                    var mr = grave.GetComponent<MeshRenderer>();
                    if (mr && stoneMat) mr.sharedMaterial = stoneMat;
                    grave.position = Grounded(grave.position.x, grave.position.z) + Vector3.up * 0.55f;
                    var stoneTop = PlaceProp("Nature/Rock_3.fbx", grave.position - Vector3.up * 0.55f, g * 35f, 1f, archRoot, false);
                    if (stoneTop) FitBox(stoneTop, new Vector3(0.85f, 1.15f, 0.32f));
                }
            }
            for (int t = 1; t <= 3; t++)
            {
                var tnt = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(tr => tr.name == $"TNT Barrel {t}");
                if (tnt)
                {
                    var mr = tnt.GetComponent<MeshRenderer>();
                    if (mr) mr.enabled = false;
                    Vector3 bp = Grounded(tnt.position.x, tnt.position.z);
                    tnt.position = bp + Vector3.up * 0.45f;
                    PlacePrefabProp("Prefab_Medieval_Barrel.prefab", bp, t * 40f, 1.05f, archRoot);
                }
            }

            // C. Upgrade 12 - The Scarlet Crypts Dungeon Interior (-400, -40, 200)
            var dungeon = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "12 - The Scarlet Crypts Dungeon");
            if (dungeon)
            {
                foreach (Transform ch in dungeon)
                {
                    var mr = ch.GetComponent<MeshRenderer>();
                    if (!mr) continue;
                    if (ch.name.Contains("Floor") || ch.name.Contains("Dais"))
                        mr.sharedMaterial = stoneMat;
                    else if (ch.name.Contains("Wall") || ch.name.Contains("Column"))
                        mr.sharedMaterial = brickMat;
                    else if (ch.name.Contains("Altar Table"))
                    {
                        mr.enabled = false;
                        PlaceProp("FantasyProps/FBX/Table_Large.fbx", ch.localPosition - Vector3.up * 0.5f, 0f, 1.35f, dungeon, false);
                        PlaceProp("FantasyProps/FBX/CandleStick.fbx", ch.localPosition + new Vector3(-1.1f, 0.55f, 0f), 0f, 1.2f, dungeon, false);
                        PlaceProp("FantasyProps/FBX/CandleStick.fbx", ch.localPosition + new Vector3(1.1f, 0.55f, 0f), 0f, 1.2f, dungeon, false);
                        PlaceProp("FantasyProps/FBX/Book_Stack_1.fbx", ch.localPosition + new Vector3(0f, 0.55f, 0f), 15f, 1.2f, dungeon, false);
                    }
                    else if (ch.name.Contains("Column "))
                    {
                        // Wrap cylinder columns in 3D Gothic stone piers
                        var pier = PlaceProp("Village/FBX/Corner_ExteriorWide_Brick.fbx", Vector3.zero, 0f, 1f, dungeon, false);
                        if (pier) { FitBox(pier, new Vector3(1.45f, 11.0f, 1.45f)); pier.transform.localPosition = new Vector3(ch.localPosition.x, 0f, ch.localPosition.z); }
                    }
                }
            }

            // D. Upgrade 13 - Wyrmcrest Summit Roost (385, 86, 420)
            var roost = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "13 - Wyrmcrest Summit Roost");
            if (roost)
            {
                Vector3 rp = roost.position;
                rp.y = WorldExpansionLayout.Height(rp.x, rp.z);
                roost.position = rp;

                foreach (Transform ch in roost)
                {
                    var mr = ch.GetComponent<MeshRenderer>();
                    if (ch.name == "Summit Dais Platform")
                    {
                        if (mr && stoneMat) mr.sharedMaterial = stoneMat;
                    }
                    else if (ch.name.StartsWith("Summit Obsidian Pillar"))
                    {
                        if (mr) mr.enabled = false;
                        Vector3 wp = Grounded(ch.position.x, ch.position.z);
                        var pier = PlaceProp("Village/FBX/Corner_ExteriorWide_Brick.fbx", wp, 0f, 1f, archRoot, true);
                        if (pier) FitBox(pier, new Vector3(1.5f, 6.2f, 1.5f));
                        var brazier = PlaceProp("FantasyProps/FBX/Cauldron.fbx", wp + Vector3.up * 6.1f, 0f, 1.2f, archRoot, false);
                        if (brazier && crystalMesh)
                        {
                            var flame = new GameObject("RoostBrazierFlame");
                            flame.transform.SetParent(brazier.transform, false);
                            flame.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                            flame.AddComponent<MeshFilter>().sharedMesh = crystalMesh;
                            flame.AddComponent<MeshRenderer>().sharedMaterial = flameMat;
                        }
                    }
                    else if (ch.name.StartsWith("Dragon Brazier Flame"))
                    {
                        if (mr) mr.enabled = false;
                    }
                    else if (ch.GetComponent<TownNPC>() || ch.GetComponent<EnemyController>() || ch.GetComponent<LootChest>())
                    {
                        Vector3 cp = ch.position;
                        cp.y = WorldExpansionLayout.Height(cp.x, cp.z) + 0.05f;
                        ch.position = cp;
                    }
                }
            }

            // E. Upgrade 20 - Redridge Volcanic Crater World Boss (330, 18, 125)
            var crater = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "20 - Redridge Volcanic Crater World Boss");
            if (crater)
            {
                Vector3 cp = crater.position;
                cp.y = WorldExpansionLayout.Height(cp.x, cp.z);
                crater.position = cp;

                int rockIdx = 0;
                foreach (Transform ch in crater)
                {
                    var mr = ch.GetComponent<MeshRenderer>();
                    if (ch.name.StartsWith("Volcanic Obsidian Crag"))
                    {
                        if (mr) mr.enabled = false;
                        Vector3 wp = Grounded(ch.position.x, ch.position.z);
                        var boulder = PlaceProp("Nature/Rock_" + ((rockIdx++ % 4) + 1) + ".fbx", wp, rockIdx * 52f, 1f, archRoot, true);
                        if (boulder) FitBox(boulder, new Vector3(3.6f, 4.8f, 3.4f));
                    }
                    else if (ch.name == "Molten Magma Pool")
                    {
                        ch.position = Grounded(ch.position.x, ch.position.z) + Vector3.up * 0.06f;
                        if (mr && flameMat) mr.sharedMaterial = flameMat;
                    }
                    else if (ch.GetComponent<EnemyController>() || ch.GetComponent<LootChest>())
                    {
                        Vector3 ep = ch.position;
                        ep.y = WorldExpansionLayout.Height(ep.x, ep.z) + 0.05f;
                        ch.position = ep;
                    }
                }
            }

            // F. Upgrade all LootChest objects that still use a raw Cube mesh to 3D FantasyProps/FBX/Chest_Gold.fbx
            foreach (var lc in Object.FindObjectsByType<LootChest>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lc.transform.position.y > -15f)
                {
                    Vector3 lp = lc.transform.position;
                    lp.y = WorldExpansionLayout.Height(lp.x, lp.z) + 0.04f;
                    lc.transform.position = lp;
                }

                var mr = lc.GetComponent<MeshRenderer>();
                var mf = lc.GetComponent<MeshFilter>();
                if (mr && mf && mf.sharedMesh && mf.sharedMesh.name == "Cube")
                {
                    mr.enabled = false;
                    var oldVis = lc.transform.Find("UpgradedChest3D");
                    if (oldVis) Object.DestroyImmediate(oldVis.gameObject);

                    var vis = PlaceProp("FantasyProps/FBX/Chest_Gold.fbx", Vector3.zero, 0f, 1f, lc.transform, false);
                    if (vis)
                    {
                        vis.name = "UpgradedChest3D";
                        vis.transform.localScale = new Vector3(
                            1f / Mathf.Max(0.1f, lc.transform.localScale.x),
                            1f / Mathf.Max(0.1f, lc.transform.localScale.y),
                            1f / Mathf.Max(0.1f, lc.transform.localScale.z)) * 1.15f;
                        vis.transform.localPosition = new Vector3(0f, -0.42f, 0f);
                    }
                }
            }

            // G. Fix any scene BirchTree/MapleTree/NormalTree/PineTree bark/leaf submesh swaps & re-material any Medieval Town Props
            var birchBark = LoadMat("Nature_BirchTree_Bark.mat");
            var birchLeaves = LoadMat("Nature_BirchTree_Leaves.mat");
            var normalBark = LoadMat("Nature_NormalTree_Bark.mat");
            var normalLeaves = LoadMat("Nature_NormalTree_Leaves.mat");
            var pineBark = LoadMat("Nature_PineTree_Bark.mat");
            var pineLeaves = LoadMat("Nature_PineTree_Leaves.mat");
            var furnitureMat = LoadMat("FantasyProps_MI_Trim_Furniture.mat");

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string rn = r.name;
                string pn = r.transform.parent ? r.transform.parent.name : "";
                string treeKey = rn.StartsWith("BirchTree") || pn.StartsWith("BirchTree") ? "BirchTree"
                    : rn.StartsWith("PineTree") || pn.StartsWith("PineTree") ? "PineTree"
                    : (rn.Contains("Tree_") || pn.Contains("Tree_")) && !rn.StartsWith("DeadTree") ? "NormalTree" : "";
                if (treeKey != "")
                {
                    string fbxName = rn.Contains("_") ? rn.Split(' ')[0] : pn.Split(' ')[0];
                    var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/Nature/" + fbxName + ".fbx");
                    var fbxR = fbx ? fbx.GetComponentInChildren<Renderer>() : null;
                    var srcMats = fbxR ? fbxR.sharedMaterials : null;
                    var mats = r.sharedMaterials;
                    Material barkM = treeKey == "BirchTree" ? birchBark : treeKey == "PineTree" ? pineBark : normalBark;
                    Material leafM = treeKey == "BirchTree" ? birchLeaves : treeKey == "PineTree" ? pineLeaves : normalLeaves;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material refM = (srcMats != null && i < srcMats.Length && srcMats[i]) ? srcMats[i] : mats[i];
                        string mn = refM ? refM.name.ToLowerInvariant() : "";
                        bool isBark = mn.Contains("bark") || mn.Contains("trunk") || mn.Contains("wood");
                        bool isLeaf = mn.Contains("leaf") || mn.Contains("leav") || mn.Contains("green") || mn.Contains("foliage");
                        if (!isBark && !isLeaf && refM && refM.HasProperty("_Color"))
                        {
                            Color c = refM.color;
                            if (c.g > c.b + 0.12f || (c.r > 0.6f && c.g > 0.4f && c.b < 0.3f)) isLeaf = true;
                            else isBark = true;
                        }
                        mats[i] = isBark ? (barkM ?? mats[i]) : isLeaf ? (leafM ?? mats[i]) : mats[i];
                    }
                    r.sharedMaterials = mats;
                }
                else if ((rn.Contains("Bench") || pn.Contains("Bench")) && furnitureMat)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = furnitureMat;
                    r.sharedMaterials = mats;
                }
            }

            report?.AppendLine($"NODES & ARENAS: Upgraded {upgradedNodes} gathering nodes to 3D flower/ore/crystal meshes, upgraded Scarlet Crypts interior, Wyrmcrest Summit, Volcanic Crater, and all LootChests.");
        }

        // =========================================================================
        // LOW-LEVEL MESH, MATERIAL & GEOMETRY UTILITIES
        // =========================================================================
        static string ChooseWallFbx(ArchStyle style, int level, bool door)
        {
            if (door)
                return (style == ArchStyle.DwarfStone || style == ArchStyle.GothicBrick || style == ArchStyle.HordeTimber)
                    ? "Wall_UnevenBrick_Door_Round.fbx"
                    : "Wall_Plaster_Door_Round.fbx";

            switch (style)
            {
                case ArchStyle.DwarfStone:
                    return level == 0 ? "Wall_UnevenBrick_Straight.fbx" : "Wall_UnevenBrick_Window_Wide_Round.fbx";
                case ArchStyle.GothicBrick:
                    return level == 0 ? "Wall_UnevenBrick_Straight.fbx" : "Wall_UnevenBrick_Window_Thin_Round.fbx";
                case ArchStyle.Elven:
                    return level == 0 ? "Wall_Plaster_Straight.fbx" : "Wall_Plaster_Window_Thin_Round.fbx";
                case ArchStyle.HordeTimber:
                case ArchStyle.GoblinTimber:
                    return level == 0 ? "Wall_UnevenBrick_Straight.fbx" : "Wall_Plaster_WoodGrid.fbx";
                default:
                    return level == 0 ? "Wall_UnevenBrick_Straight.fbx" : "Wall_Plaster_WoodGrid.fbx";
            }
        }

        static GameObject PlaceProp(string relativeArtPath, Vector3 localPos, float yaw, float scale, Transform parent, bool addCollider)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Art/" + relativeArtPath);
            if (!asset) return null;

            var holder = new GameObject(asset.name);
            holder.transform.SetParent(parent, false);
            holder.transform.localPosition = localPos;
            holder.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            holder.transform.localScale = Vector3.one * scale;

            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            model.transform.SetParent(holder.transform, false);

            foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                    if (mats[i]) mats[i] = ResolveImportedMaterial(mats[i], relativeArtPath);
                r.sharedMaterials = mats;
            }

            if (addCollider)
            {
                var b = BoundsOf(holder);
                var bc = holder.AddComponent<BoxCollider>();
                bc.center = holder.transform.InverseTransformPoint(b.center);
                bc.size = b.size / Mathf.Max(0.01f, scale);
            }
            return holder;
        }

        static GameObject PlacePrefabProp(string prefabFile, Vector3 worldPos, float yaw, float scale, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PropPrefabDir + prefabFile);
            if (!asset) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            inst.transform.position = worldPos;
            inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.localScale *= scale;
            return inst;
        }

        static GameObject PlacePrefabPropLocal(string prefabFile, Vector3 localPos, float yaw, float scale, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(PropPrefabDir + prefabFile);
            if (!asset) return null;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            inst.transform.localPosition = localPos;
            inst.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            inst.transform.localScale *= scale;
            return inst;
        }

        internal static Material ResolveImportedMaterial(Material source, string relativePath)
        {
            string family = relativePath.Split('/')[0];
            string key = family + "_" + source.name.Replace('/', '_');
            if (MaterialCache.TryGetValue(key, out var cached) && cached) return cached;

            var persisted = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + key + ".mat");
            if (persisted)
            {
                MaterialCache[key] = persisted;
                return persisted;
            }

            // Fallback mapping to persisted family materials
            string lower = source.name.ToLowerInvariant();
            Material fallback = lower.Contains("wood") ? LoadMat("Village_MI_WoodTrim.mat")
                : lower.Contains("tile") || lower.Contains("roof") ? LoadMat("Village_MI_RoundTiles.mat")
                : lower.Contains("uneven") ? LoadMat("Village_MI_UnevenBrick.mat")
                : lower.Contains("brick") ? LoadMat("Village_MI_Brick.mat")
                : lower.Contains("metal") ? LoadMat("FantasyProps_MI_Trim_Metal.mat")
                : lower.Contains("furniture") ? LoadMat("FantasyProps_MI_Trim_Furniture.mat")
                : LoadMat("Village_MI_Plaster.mat");

            if (fallback) MaterialCache[key] = fallback;
            return fallback ? fallback : source;
        }

        static Material LoadMat(string fileName)
        {
            if (MaterialCache.TryGetValue(fileName, out var m) && m) return m;
            m = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + fileName);
            if (m) MaterialCache[fileName] = m;
            return m;
        }

        static GameObject StructuralBox(string name, Vector3 localPos, Vector3 size, Material mat, Transform parent, bool keepCollider)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            if (mat) go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static void AddPointLight(string name, Vector3 localPos, Color color, float range, float intensity, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var lt = go.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = color;
            lt.range = range;
            lt.intensity = intensity;
        }

        static void PlaceSign(string text, Vector3 point, Transform parent)
        {
            point.y = WorldExpansionLayout.Height(point.x, point.z);
            var post = PlaceProp("Village/FBX/Prop_Support.fbx", point, 0f, 1f, parent, false);
            if (post) FitBox(post, new Vector3(0.22f, 2.5f, 0.22f));

            var label = new GameObject("Sign - " + text.Split('\n')[0]);
            label.transform.SetParent(parent, false);
            label.transform.position = point + Vector3.up * 2.85f;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = 0.045f;
            mesh.fontSize = 36;
            mesh.color = new Color(0.95f, 0.84f, 0.52f);
            label.AddComponent<WorldLabel>();
        }

        static Vector3 Grounded(float x, float z) => new Vector3(x, WorldExpansionLayout.Height(x, z), z);
        static Vector3 GroundedVec(Vector3 v) => new Vector3(v.x, WorldExpansionLayout.Height(v.x, v.z), v.z);

        static float FaceToward(Vector3 fromPos, Vector3 toTarget)
        {
            Vector3 d = toTarget - fromPos;
            d.y = 0f;
            if (d.sqrMagnitude < 0.001f) return 0f;
            // Local -Z is building front; rotate so local -Z points along +d (i.e. local +Z points along -d)
            return Quaternion.LookRotation(-d.normalized, Vector3.up).eulerAngles.y;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void FitHeight(GameObject go, float targetHeight)
        {
            float h = BoundsOf(go).size.y;
            if (h > 0.001f) go.transform.localScale *= targetHeight / h;
        }

        static void FitBox(GameObject go, Vector3 targetSize)
        {
            Quaternion savedRot = go.transform.rotation;
            go.transform.rotation = Quaternion.identity;

            var b = BoundsOf(go);
            var s = go.transform.localScale;
            go.transform.localScale = Vector3.Scale(s, new Vector3(
                targetSize.x / Mathf.Max(0.001f, b.size.x),
                targetSize.y / Mathf.Max(0.001f, b.size.y),
                targetSize.z / Mathf.Max(0.001f, b.size.z)));
            b = BoundsOf(go);
            var offset = go.transform.position + Vector3.up * b.extents.y - b.center;
            foreach (Transform child in go.transform)
                child.position += offset;

            go.transform.rotation = savedRot;
        }
    }
}
