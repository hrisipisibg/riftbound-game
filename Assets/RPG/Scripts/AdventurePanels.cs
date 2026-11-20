using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
        Texture2D atlasPaper, atlasRelief;
        Vector2 atlasRegionScroll;
        bool atlasPaperLoaded;
        Vector3[] atlasRiverBanks;

        void DrawAdventurePanels(float width, float height)
        {
            if (!player.MapOpen && !player.InventoryOpen && !player.DialogueOpen && !player.ShopOpen && !player.JournalOpen)
            {
                bool nearCrypt = Vector3.Distance(player.transform.position, new Vector3(-34, player.transform.position.y, 70)) < 55f;
                if (nearCrypt && player.RunesFound > 0 && !player.HasOpenedChest("crypt-reliquary"))
                {
                    Panel(new Rect(width - 256, 356, 236, 52));
                    GUI.Label(new Rect(width - 244, 364, 212, 38), player.CryptObjective, small);
                }
                if (player.Blocking)
                    GUI.Label(new Rect(width / 2 - 120, height - 104, 240, 24), "Guarding", small);
                if (player.Mounted)
                {
                    string travelHint = player.IsFlying ? "Z Dismount   Space Ascend   X Descend" : "Z Dismount   / Walk / run";
                    GUI.Label(new Rect(24, height - 54, 370, 24), travelHint, small);
                }
            }
            if (player.Mounting)
            {
                Panel(new Rect(width / 2 - 160, height / 2 + 84, 320, 65));
                GUI.Label(new Rect(width / 2 - 144, height / 2 + 94, 290, 24), "Summoning mount / stand still", small);
                Bar(new Rect(width / 2 - 144, height / 2 + 130, 288, 9), player.MountProgress, new Color(.85f, .65f, .3f));
            }
            if (player.ReturningHome)
            {
                Panel(new Rect(width / 2 - 180, height / 2 + 85, 360, 70));
                GUI.Label(new Rect(width / 2 - 163, height / 2 + 94, 330, 25), "Returning to Emberwatch / move to cancel", small);
                Bar(new Rect(width / 2 - 163, height / 2 + 131, 326, 9), player.ReturnProgress, new Color(.3f, .7f, 1));
            }
            if (player.ShopOpen)
            {
                bool westernQuest = player.CanUseWesternRoadQuest && player.ActiveMerchant.npcName == WesternRoadCamps.Rowan &&
                    player.QuestIndex >= 2;
                float shopHeight = westernQuest ? 570f : 500f;
                float x = width / 2 - 330, y = height / 2 - shopHeight / 2;
                Panel(new Rect(x, y, 660, shopHeight));
                GUI.Label(new Rect(x + 24, y + 20, 600, 36), player.ActiveMerchant.npcName.ToUpperInvariant(), title);
                var merchantProfile = Narrative.NarrativeCatalog.ResolveMerchant(player.ActiveMerchant);
                var merchantGreeting = Narrative.NarrativeCatalog.SelectText(merchantProfile, "greeting", "", player);
                string headerCopy = !string.IsNullOrEmpty(merchantGreeting.text) ? merchantGreeting.text : "Supplies for the forest road.";
                GUI.Label(new Rect(x + 24, y + 65, 600, 55), $"{headerCopy}\nYour purse: {player.Gold} gold.   Potions: {player.Potions}/10   /   Weapon bonus: +{player.WeaponBonus}", label);
                GUI.Label(new Rect(x + 24, y + 143, 400, 30), "Health potion / restores 70 HP", label);
                GUI.enabled = player.Gold >= 10 && player.Potions < 10 && !player.InCombat();
                if (GUI.Button(new Rect(x + 455, y + 137, 180, 36), "BUY / 10 GOLD", questButton)) player.BuyPotion();
                GUI.enabled = true;
                GUI.Label(new Rect(x + 24, y + 201, 400, 45), player.TemperedWeapon ? "Weapon tempered / +4 damage" : "Temper axe / permanent +4 damage", label);
                GUI.enabled = player.Gold >= 45 && !player.TemperedWeapon && !player.InCombat();
                if (GUI.Button(new Rect(x + 455, y + 195, 180, 36), "UPGRADE / 45 GOLD", questButton)) player.TemperWeapon();
                GUI.enabled = true;
                GUI.Label(new Rect(x + 24, y + 259, 400, 30), "Leather vest / 2 armor, +10 max HP", label);
                GUI.enabled = player.Gold >= 25 && !player.OwnsItem("leather-vest") && !player.InCombat();
                if (GUI.Button(new Rect(x + 455, y + 253, 180, 36), player.OwnsItem("leather-vest") ? "OWNED" : "BUY VEST / 25 GOLD", questButton)) player.BuyVest();
                GUI.enabled = true;
                GUI.Label(new Rect(x + 24, y + 317, 400, 30), "Rest / restore all health", label);
                GUI.enabled = player.Gold >= 5 && player.Stats.Health < player.Stats.maxHealth && !player.InCombat();
                if (GUI.Button(new Rect(x + 455, y + 311, 180, 36), "REST / 5 GOLD", questButton)) player.RestAtMerchant();
                GUI.enabled = true;
                GUI.Label(new Rect(x + 24, y + 375, 400, 30), $"Bone shards: {player.BoneShards} / 2 gold each", label);
                GUI.enabled = player.BoneShards > 0 && !player.InCombat();
                if (GUI.Button(new Rect(x + 455, y + 369, 180, 36), "SELL ALL SHARDS", questButton)) player.SellShards();
                GUI.enabled = true;
                if (westernQuest)
                {
                    GUI.Label(new Rect(x + 24, y + 420, 610, 26), WesternRoadCamps.QuestTitle, title);
                    string roadText = player.WesternRoadStage switch
                    {
                        0 => "The grain wagon never arrived. Find its waybill at the layby on the Highspire road.",
                        3 => "One wagon and two seals. Let’s pay the ferry crew for the load they actually carried.",
                        _ => player.WesternRoadObjective
                    };
                    GUI.Label(new Rect(x + 24, y + 450, 420, 53), roadText, small);
                    if ((player.WesternRoadStage == 0 || player.WesternRoadStage == 3) &&
                        GUI.Button(new Rect(x + 452, y + 453, 183, 38), player.WesternRoadStage == 0 ? "Accept quest" : "Turn in quest", questButton))
                        player.HandleWesternRoadAtRowan();
                }
                if (GUI.Button(new Rect(x + 24, y + shopHeight - 55, 610, 38), "FAREWELL  /  E OR ESC", questButton)) player.CloseShop();
            }
            if (player.JournalOpen) { DrawQuestJournal(width, height); return; }
            if (!player.MapOpen) return;

            var area = new Rect(width / 2 - 430, height / 2 - 265, 860, 520);
            Fill(new Rect(area.x - 14, area.y - 58, area.width + 28, area.height + 104), new Color(.02f, .035f, .05f, .97f));
            GUI.Label(new Rect(area.x + 8, area.y - 48, 600, 36), WorldRegions.Expanded ? "RIFTBOUND — THE CONNECTED FRONTIER" : "WORLD ATLAS OF AZEBOTH", title);
            GUI.Label(new Rect(area.xMax - 240, area.y - 42, 230, 24), $"Coords: ({Mathf.RoundToInt(player.transform.position.x)}, {Mathf.RoundToInt(player.transform.position.z)})  Alt: {Mathf.RoundToInt(player.transform.position.y)}m", small);

            // Ocean / Map Base
            Fill(area, new Color(.08f, .14f, .18f));
            if (WorldRegions.Expanded)
            {
                DrawExpandedAtlas(area);
                if (GUI.Button(new Rect(area.xMax - 130, area.yMax + 8, 130, 32), "CLOSE [M]", questButton)) player.MapOpen = false;
                return;
            }

            // --- 6 Distinct Styled Zones ---
            // 1. Highspire Keep (Alliance Capital)
            PlotZone(area, new Vector3(-80, 0, -35), new Vector3(-25, 0, 15), new Color(.11f, .18f, .30f));
            // 2. Amber Vale (Alliance Starter Plains)
            PlotZone(area, new Vector3(-25, 0, -35), new Vector3(35, 0, 75), new Color(.16f, .26f, .14f));
            // 3. Duskwood & The Scarlet Crypts (Haunted Forest & Cathedral)
            PlotZone(area, new Vector3(-80, 0, 80), new Vector3(-15, 0, 245), new Color(.11f, .10f, .19f));
            // 4. Redridge Canyons (Horde Desert Starter)
            PlotZone(area, new Vector3(15, 0, 100), new Vector3(80, 0, 215), new Color(.34f, .18f, .11f));
            // 5. Gor'Kresh Stronghold (Horde Capital)
            PlotZone(area, new Vector3(45, 0, 215), new Vector3(85, 0, 248), new Color(.30f, .07f, .07f));
            // 6. Mount Wyrmcrest / Ironfang Peaks (Mountain Summit & Dragon Roost)
            PlotZone(area, new Vector3(-25, 0, 195), new Vector3(25, 0, 248), new Color(.24f, .30f, .38f));

            // Cobblestone Ground Roads
            MapLine(area, new Vector3(-45, 0, -20), new Vector3(0, 0, 10)); // Highspire to Emberwatch
            MapLine(area, new Vector3(0, 0, 10), new Vector3(25, 0, 64));   // Emberwatch to Ashwood
            MapLine(area, new Vector3(25, 0, 64), new Vector3(35, 0, 95));  // Ashwood to Watchtower
            MapLine(area, new Vector3(0, 0, 10), new Vector3(0, 0, 150));   // North highway
            MapLine(area, new Vector3(0, 0, 150), new Vector3(-24, 0, 156)); // Highway to Greenhollow
            MapLine(area, new Vector3(-24, 0, 156), new Vector3(-55, 0, 215)); // Greenhollow to Cathedral
            MapLine(area, new Vector3(35, 0, 95), new Vector3(50, 0, 210));  // Watchtower to Camp Bloodrock
            MapLine(area, new Vector3(50, 0, 210), new Vector3(85, 0, 235)); // Camp Bloodrock to Gor'Kresh
            MapLine(area, new Vector3(0, 0, 150), new Vector3(0, 0, 215));  // Mountain pass trail

            // Flight Taxi Network Spline Routes (Golden aerial paths)
            FlightLine(area, new Vector3(-45, 0, -20), new Vector3(2, 0, 10));   // Highspire <-> Emberwatch
            FlightLine(area, new Vector3(2, 0, 10), new Vector3(-50, 0, 205));  // Emberwatch <-> Duskwood
            FlightLine(area, new Vector3(2, 0, 10), new Vector3(50, 0, 210));   // Emberwatch <-> Bloodrock
            FlightLine(area, new Vector3(-50, 0, 205), new Vector3(0, 0, 215)); // Duskwood <-> Wyrmcrest
            FlightLine(area, new Vector3(50, 0, 210), new Vector3(85, 0, 235)); // Bloodrock <-> Gor'Kresh
            FlightLine(area, new Vector3(50, 0, 210), new Vector3(0, 0, 215));  // Bloodrock <-> Wyrmcrest

            // Zone Title Watermarks
            MarkZoneTitle(area, new Vector3(-55, 0, -5), "HIGHSPIRE KEEP\n[ALLIANCE CAPITAL]", new Color(.4f, .7f, 1f));
            MarkZoneTitle(area, new Vector3(0, 0, 35), "AMBER VALE\n[Alliance Plains (Lv 1-6)]", new Color(.5f, .9f, .5f));
            MarkZoneTitle(area, new Vector3(-50, 0, 175), "DUSKWOOD FORESTS\n[Contested Grounds (Lv 4-8)]", new Color(.7f, .5f, .9f));
            MarkZoneTitle(area, new Vector3(52, 0, 160), "REDRIDGE CANYONS\n[Horde Territory (Lv 1-6)]", new Color(1f, .6f, .3f));
            MarkZoneTitle(area, new Vector3(68, 0, 235), "GOR'KRESH STRONGHOLD\n[HORDE CAPITAL]", new Color(1f, .3f, .3f));
            MarkZoneTitle(area, new Vector3(0, 0, 238), "IRONFANG PEAKS\n[Mount Wyrmcrest (Lv 8+ APEX)]", new Color(.8f, .9f, 1f));

            // Flight Master Hubs (Flight Taxi Nodes)
            MarkFlightNode(area, new Vector3(-45, 0, -20), "✈ Gryphon Master Darien");
            MarkFlightNode(area, new Vector3(2, 0, 10), "✈ Emberwatch Flight Master");
            MarkFlightNode(area, new Vector3(-50, 0, 205), "✈ Duskwood Skywatcher");
            MarkFlightNode(area, new Vector3(50, 0, 210), "✈ Bloodrock Outrider");
            MarkFlightNode(area, new Vector3(85, 0, 235), "✈ Wind Rider Vark");
            MarkFlightNode(area, new Vector3(0, 0, 215), "✈ Wyrmcrest Summit Roost");

            // Landmarks & Settlements
            Mark(area, new Vector3(-45, 0, -15), "🏰 Highspire Keep [Alliance Capital]", new Color(.45f, .75f, 1f), 11);
            Mark(area, new Vector3(85, 0, 235), "⛩ Gor'Kresh Stronghold [Horde Capital]", new Color(1f, .3f, .3f), 11);
            Mark(area, new Vector3(0, 0, 10), "Emberwatch [Human Starter]", Color.white, 8);
            Mark(area, new Vector3(20, 0, -15), "Frostpeak [Dwarf Starter]", new Color(.85f, .75f, .6f), 8);
            Mark(area, new Vector3(-25, 0, 15), "Silverglen [High Elf Starter]", new Color(.45f, .9f, .95f), 8);
            Mark(area, new Vector3(50, 0, 210), "Camp Bloodrock [Orc Starter]", new Color(1f, .7f, .3f), 8);
            Mark(area, new Vector3(38, 0, 175), "Rustwater [Goblin Starter]", new Color(1f, .85f, .2f), 8);
            Mark(area, new Vector3(-28, 0, 160), "Deathknell [Undead Starter]", new Color(.75f, .5f, .95f), 8);
            Mark(area, new Vector3(25, 0, 64), "Ashwood Camp", Color.white, 7);
            Mark(area, new Vector3(35, 0, 95), "Watchtower Bastion", Color.white, 7);
            Mark(area, new Vector3(-24, 0, 156), "Greenhollow Hamlet", Color.white, 8);
            Mark(area, new Vector3(-34, 0, 70), "Western Crypt", new Color(.6f, .85f, 1f), 9);
            Mark(area, new Vector3(-55, 0, 215), "Ω SCARLET CRYPTS [CATHEDRAL DUNGEON]", new Color(1f, .25f, .25f), 12);
            Mark(area, new Vector3(0, 0, 225), "🐉 Vermithrax Apex Roost", new Color(1f, .5f, .1f), 12);
            Mark(area, new Vector3(10, 0, 185), "☠ GORGONAX [WORLD BOSS]", new Color(1f, .4f, .1f), 13);

            foreach (var landmark in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                if (landmark.landmarkId != "scarlet-crypts" && landmark.landmarkId != "redridge-frontier")
                    Mark(area, landmark.transform.position, player.HasLandmark(landmark.landmarkId) ? "" : "Scout", new Color(.55f, 1, .8f), 8);

            foreach (var rune in FindObjectsByType<Runestone>(FindObjectsSortMode.None))
                Mark(area, rune.transform.position, player.HasRune(rune.runeId) ? "" : "✦ Rune", new Color(.3f, 1, .9f), 7);

            foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
                if (!player.HasOpenedChest(chest.chestId)) Mark(area, chest.transform.position, "", new Color(.95f, .72f, .2f), 7);

            // Enemies & Bosses
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (enemy.GetComponent<Combatant>().Alive)
                    Mark(area, enemy.transform.position, enemy.trainingDummy ? "Dummy" : (enemy.isBoss ? "★ BOSS" : ""), enemy.trainingDummy ? new Color(.4f, 1, .7f) : new Color(1, .3f, .2f), enemy.isBoss ? 11 : 6);

            if (player.questGiver) Mark(area, player.questGiver.position, "Abe", new Color(.4f, 1, .5f), 8);
            DrawAtlasDestination(area);

            // Player Blip (Glowing pulsing cyan circle)
            Vector2 pPos = MapPoint(area, player.transform.position);
            Fill(new Rect(pPos.x - 7, pPos.y - 7, 14, 14), new Color(0f, 0.4f, 0.8f, 0.5f));
            Fill(new Rect(pPos.x - 4, pPos.y - 4, 8, 8), new Color(.2f, .9f, 1f));
            GUI.Label(new Rect(pPos.x + 9, pPos.y - 12, 120, 25), "YOU", small);

            // Bottom Legend
            GUI.Label(new Rect(area.x, area.yMax + 10, 700, 28), "BLUE: Alliance Capital · RED: Horde Capital · PURPLE: Duskwood & Cathedral · GOLD: ✈ Flight Taxi · CYAN: You", small);
            if (GUI.Button(new Rect(area.xMax - 130, area.yMax + 8, 130, 32), "CLOSE [M]", questButton)) player.MapOpen = false;
        }

        void MarkZoneTitle(Rect area, Vector3 worldPos, string text, Color color)
        {
            Vector2 pt = MapPoint(area, worldPos);
            var prevColor = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(new Rect(pt.x - 90, pt.y - 18, 180, 36), text, small);
            GUI.contentColor = prevColor;
        }

        void MarkFlightNode(Rect area, Vector3 worldPos, string name)
        {
            Vector2 pt = MapPoint(area, worldPos);
            Fill(new Rect(pt.x - 4, pt.y - 4, 8, 8), new Color(1f, 0.85f, 0.25f));
            GUI.Label(new Rect(pt.x + 7, pt.y - 10, 160, 20), name, small);
        }

        void FlightLine(Rect area, Vector3 from, Vector3 to)
        {
            DrawAtlasSegment(MapPoint(area, from), MapPoint(area, to), 2f, new Color(1f, 0.82f, 0.25f, 0.85f));
        }
        void DrawExpandedAtlas(Rect panel)
        {
            var area = new Rect(panel.x, panel.y, panel.height, panel.height);
            if (Event.current.type == EventType.Repaint)
            {
                if (!atlasPaperLoaded)
                {
                    atlasPaper = Resources.Load<Texture2D>("WorldAtlasPaper");
                    atlasPaperLoaded = true;
                }
                if (!atlasRelief) atlasRelief = CreateAtlasRelief();
                Fill(area, new Color(.89f, .78f, .58f));
                if (atlasPaper) GUI.DrawTexture(area, atlasPaper);
                if (atlasRelief) GUI.DrawTexture(area, atlasRelief);
            }
            DrawAtlasRiver(area);
            var network = FindFirstObjectByType<WorldMapRoutes>();
            if (network && network.routes != null)
                foreach (var route in network.routes)
                    for (int i=1;i<route.points.Length;i++) MapLine(area,route.points[i-1],route.points[i]);
            MapLine(area,new Vector3(0,0,3),new Vector3(0,0,100));
            MapLine(area,new Vector3(0,0,51),new Vector3(25,0,64));
            MapLine(area,new Vector3(25,0,64),new Vector3(35,0,91));
            MapLine(area,new Vector3(0,0,50),new Vector3(-34,0,70));
            DrawAtlasLabel(area, new Vector3(5, 0, 28), "Emberwatch");
            DrawAtlasLabel(area, WorldRegions.Cathedral, "Duskwood");
            DrawAtlasLabel(area, WorldRegions.Bloodrock, "Bloodrock");
            DrawAtlasLabel(area, WorldRegions.Greenhollow, "Greenhollow");
            DrawAtlasLabel(area, WorldRegions.Summit, "Wyrmcrest");
            DrawAtlasLabel(area, new Vector3(-245, 0, 40), "Willowmere");
            DrawAtlasLabel(area, WorldRegions.Highspire, "Highspire");
            DrawAtlasLabel(area, WorldRegions.Stronghold, "Gor’Kresh");
            var westernCamps = WesternRoadCamps.Current;
            if (westernCamps)
            {
                Mark(area, westernCamps.Layby.position, "", new Color(.66f, .38f, .10f), 10f);
                Mark(area, westernCamps.Rest.position, "", new Color(.66f, .38f, .10f), 10f);
                DrawAtlasLabel(area, westernCamps.Layby.position, "Split-Seal Layby");
                DrawAtlasLabel(area, westernCamps.Rest.position, "Tallykeepers’ Rest");
            }
            var hollows = BrassHollows.Current;
            if (hollows)
            {
                Mark(area, hollows.Entrance, "", new Color(.39f, .21f, .57f), 10f);
                DrawAtlasLabel(area, hollows.Entrance, "Brass Hollows");
            }
            foreach (var stop in SettlementWaystop.Active)
                Mark(area, stop.transform.position, "", new Color(.32f, .49f, .25f), 9);
            foreach(var node in FlightMaster.WorldFlightNodes) Mark(area,node.targetPos,"",new Color(1,.8f,.25f),5);
            foreach (var landmark in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                if (!player.HasLandmark(landmark.landmarkId))
                    Mark(area, landmark.transform.position, "", new Color(.15f, .37f, .29f), 4);
            DrawAtlasDestination(area);
            bool inHollows = hollows && BrassHollows.Inside(player.transform.position);
            Vector3 playerPin = inHollows ? hollows.Entrance :
                WorldRegions.Underground(player.transform.position) ? WorldRegions.Cathedral : player.transform.position;
            Mark(area, playerPin, "", new Color(.09f,.18f,.22f), 12);
            Mark(area, playerPin, "", new Color(.25f,.93f,1), 8);
            DrawAtlasLabel(area, playerPin, inHollows || WorldRegions.Underground(player.transform.position) ? "You · Below ground" : "You");
            DrawAtlasQuestMarkers(area);
            GUI.Label(new Rect(area.x + 8, area.y + 6, 70, 24), "▲ N", new GUIStyle(small)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(.16f, .12f, .08f) }
            });
            float x=panel.x+panel.height+20, y=panel.y+12, w=panel.width-panel.height-36;
            if (GUI.Button(new Rect(x, y, w * .5f - 3, 27), "Quests", questButton)) atlasPlaces = false;
            if (GUI.Button(new Rect(x + w * .5f + 3, y, w * .5f - 3, 27), "Places", questButton)) atlasPlaces = true;
            y += 35f;
            if (!atlasPlaces)
            {
                DrawAtlasQuestList(new Rect(x, y, w, panel.yMax - y - 9));
                return;
            }
            GUI.Label(new Rect(x,y,w,29),"FRONTIER SITES",title);
            GUI.Label(new Rect(x,y+34,w,24),"Choose a site to follow its waypoint.",small);
            if (westernCamps)
            {
                DrawSiteWaypoint(new Rect(x,y+62,w,30),"Split-Seal Layby",westernCamps.Layby.position);
                DrawSiteWaypoint(new Rect(x,y+96,w,30),"Tallykeepers’ Rest",westernCamps.Rest.position);
            }
            if (hollows) DrawSiteWaypoint(new Rect(x,y+130,w,30),"Brass Hollows entrance",hollows.Entrance);
            GUI.Label(new Rect(x,y+170,w,24),"WAYPOINTS",small);
            string[] names={"Emberwatch Outpost","Ashwood Supply Camp","Ruined Watchtower","Westbank Crossing","Duskwood Cathedral","Camp Bloodrock","Greenhollow Valley","Wyrmcrest Summit","Willowmere Wilds","Highspire Keep","Gor'Kresh Stronghold"};
            Vector3[] positions={new Vector3(0,0,10),new Vector3(24,2,61),new Vector3(35,6,86),new Vector3(-255,6,50),WorldRegions.Cathedral,WorldRegions.Bloodrock,WorldRegions.Greenhollow,WorldRegions.Summit,new Vector3(-285,6,50),WorldRegions.Highspire,WorldRegions.Stronghold};
            var waystops = SettlementWaystop.Active;
            atlasRegionScroll = GUI.BeginScrollView(new Rect(x,y+198,w,180), atlasRegionScroll, new Rect(0,0,w-19,(names.Length + waystops.Count)*35));
            for (int i = 0; i < waystops.Count; i++)
                DrawSiteWaypoint(new Rect(0, i * 35, w - 19, 30), waystops[i].displayName, waystops[i].transform.position);
            for(int i=0;i<names.Length;i++)
                if(GUI.Button(new Rect(0,(i + waystops.Count)*35,w-19,30),names[i],questButton)) MinimapRadar.SetWaypoint(positions[i],names[i]);
            GUI.EndScrollView();
            var radar = MinimapRadar.Instance;
            if (radar && radar.TryGetNavigationTarget(out _, out var caption))
            {
                bool pinned = radar.customWaypoint.HasValue;
                GUI.Label(new Rect(x,y+384,w,24), pinned ? "MAP PIN" : "QUEST DESTINATION", small);
                GUI.Label(new Rect(x,y+406,w,30), caption, label);
                if (pinned && GUI.Button(new Rect(x,y+438,w,27), "CLEAR PIN", questButton))
                    MinimapRadar.ClearWaypoint();
            }
        }

        bool atlasPlaces;
        Vector2 atlasQuestScroll;
        readonly System.Collections.Generic.List<Vector2> atlasQuestPoints = new();

        void DrawAtlasQuestMarkers(Rect area)
        {
            var radar = MinimapRadar.Instance;
            if (!radar) return;
            atlasQuestPoints.Clear();
            foreach (var pin in radar.QuestPins)
            {
                Vector2 point = MapPoint(area, MinimapRadar.AtlasPosition(pin.position));
                bool stacked = false;
                foreach (var placed in atlasQuestPoints)
                    if ((placed - point).sqrMagnitude < 18f * 18f) { stacked = true; break; }
                if (stacked) continue;
                atlasQuestPoints.Add(point);
                var hit = new Rect(Mathf.Clamp(point.x - 10, area.x, area.xMax - 20),
                    Mathf.Clamp(point.y - 10, area.y, area.yMax - 20), 20, 20);
                if (radar.DrawQuestMapPin(hit, pin)) radar.SelectQuestPin(pin);
            }
        }

        void DrawAtlasQuestList(Rect panel)
        {
            var radar = MinimapRadar.Instance;
            if (!radar) return;
            GUI.Label(new Rect(panel.x, panel.y, panel.width, 24), "! Available   ◆ Objective   ? Turn in", small);
            if (radar.QuestPins.Count == 0)
                GUI.Label(new Rect(panel.x, panel.y + 31, panel.width, 32), "No visible quests at this stage.", small);
            float listWidth = panel.width - 17, rowHeight = 44;
            var rows = new Rect(panel.x, panel.y + 30, panel.width, panel.height - 81);
            atlasQuestScroll = GUI.BeginScrollView(rows, atlasQuestScroll,
                new Rect(0, 0, listWidth, rowHeight * radar.QuestPins.Count));
            for (int i = 0; i < radar.QuestPins.Count; i++)
            {
                var pin = radar.QuestPins[i];
                var row = new Rect(0, i * rowHeight, listWidth - 2, rowHeight - 3);
                var zone = ZoneManager.GetZoneName(pin.position);
                if (GUI.Button(row, GUIContent.none, questButton)) radar.SelectQuestPin(pin);
                var textRect = new Rect(row.x + 8, row.y + 2, row.width - 15, 20);
                Color saved = GUI.contentColor;
                GUI.contentColor = pin.Ink;
                GUI.Label(textRect, pin.Icon + "  " + pin.caption, small);
                GUI.contentColor = saved;
                GUI.Label(new Rect(textRect.x + 15, textRect.y + 18, textRect.width - 15, 17), zone, small);
            }
            GUI.EndScrollView();
            if (radar.TryGetNavigationTarget(out _, out var destination))
                GUI.Label(new Rect(panel.x, rows.yMax + 2, panel.width - 6, 22), destination, small);
            if (radar.customWaypoint.HasValue && GUI.Button(new Rect(panel.x, rows.yMax + 23, panel.width, 25), "Clear pin", questButton))
                MinimapRadar.ClearWaypoint();
        }

        void DrawSiteWaypoint(Rect button, string name, Vector3 point)
        {
            if (!GUI.Button(button, name, questButton)) return;
            MinimapRadar.SetWaypoint(point, name);
            player.MapOpen = false;
        }

        void DrawAtlasRiver(Rect area)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (atlasRiverBanks == null)
            {
                var river = GameObject.Find("Westbank flowing river");
                var filter = river ? river.GetComponent<MeshFilter>() : null;
                if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return;
                // The authored mesh stores a left/right bank pair at each point of the river.
                atlasRiverBanks = filter.sharedMesh.vertices;
                for (int i = 0; i < atlasRiverBanks.Length; i++)
                    atlasRiverBanks[i] = filter.transform.TransformPoint(atlasRiverBanks[i]);
            }
            for (int i = 2; i + 1 < atlasRiverBanks.Length; i += 2)
            {
                Vector2 from = MapPoint(area, (atlasRiverBanks[i - 2] + atlasRiverBanks[i - 1]) * .5f);
                Vector2 to = MapPoint(area, (atlasRiverBanks[i] + atlasRiverBanks[i + 1]) * .5f);
                float width = Vector2.Distance(MapPoint(area, atlasRiverBanks[i]), MapPoint(area, atlasRiverBanks[i + 1]));
                DrawAtlasSegment(from, to, width, new Color(.21f, .47f, .53f));
            }
        }

        void DrawAtlasLabel(Rect area, Vector3 position, string text)
        {
            Vector2 point = MapPoint(area, position);
            var ink = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            ink.normal.textColor = new Color(.16f, .11f, .07f);
            float width = ink.CalcSize(new GUIContent(text)).x + 12;
            var caption = new Rect(Mathf.Clamp(point.x - width / 2, area.x + 2, area.xMax - width - 2),
                Mathf.Clamp(point.y - 25, area.y + 2, area.yMax - 22), width, 19);
            Fill(caption, new Color(1f, .93f, .76f, .90f));
            GUI.Label(caption, text, ink);
        }

        void DrawAtlasDestination(Rect area)
        {
            var radar = MinimapRadar.Instance;
            if (!radar || !radar.TryGetNavigationTarget(out var position, out var caption)) return;
            if (BrassHollows.Current && BrassHollows.Inside(position)) position = BrassHollows.Current.Entrance;
            if (WorldRegions.Expanded && WorldRegions.Underground(position)) position = WorldRegions.Cathedral;
            Vector2 point = MapPoint(area, position);
            var matrix = GUI.matrix;
            GUI.matrix = matrix * Matrix4x4.TRS(point, Quaternion.Euler(0f, 0f, 45f), Vector3.one);
            Fill(new Rect(-7, -7, 14, 14), new Color(.20f, .13f, .07f));
            Fill(new Rect(-5, -5, 10, 10), new Color(1f, .79f, .28f));
            GUI.matrix = matrix;
            if (!WorldRegions.Expanded) Mark(area, position, caption, Color.white, 0);
        }

        static Texture2D CreateAtlasRelief()
        {
            var terrains = Terrain.activeTerrains;
            if (terrains.Length == 0) return null;
            const int size = 256;
            Rect bounds = WorldRegions.Bounds;
            var elevations = new float[size * size];
            var pixels = new Color32[elevations.Length];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var position = new Vector3(bounds.xMin + (x + .5f) * bounds.width / size, 0,
                    bounds.yMin + (y + .5f) * bounds.height / size);
                elevations[y * size + x] = WorldRegions.GroundHeight(position, terrains);
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float height = elevations[i];
                float east = elevations[y * size + Mathf.Min(x + 1, size - 1)] - height;
                float north = elevations[Mathf.Min(y + 1, size - 1) * size + x] - height;
                float slope = Mathf.Sqrt(east * east + north * north);
                float light = Mathf.Clamp((north - east) * .05f, -.24f, .24f);
                var ink = Color.Lerp(new Color(.42f, .53f, .29f), new Color(.64f, .45f, .31f),
                    Mathf.Clamp01((height - 12f) / 40f + slope * .07f));
                ink = Color.Lerp(ink, new Color(.94f, .93f, .84f), Mathf.SmoothStep(0, 1, Mathf.InverseLerp(45, 72, height)));
                float contour = Mathf.Abs(height / 8f - Mathf.Round(height / 8f));
                if (height > 4f && slope > .45f && contour < .07f) light -= .10f;
                ink += new Color(light, light, light, 0);
                ink.a = .72f;
                pixels[i] = ink;
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        Vector2 MapPoint(Rect area, Vector3 position)
        {
            var bounds = WorldRegions.Expanded ? WorldRegions.Bounds : new Rect(-85,-40,175,295);
            return new Vector2(area.x+Mathf.InverseLerp(bounds.xMin,bounds.xMax,position.x)*area.width,area.yMax-Mathf.InverseLerp(bounds.yMin,bounds.yMax,position.z)*area.height);
        }
        void PlotZone(Rect area, Vector3 from, Vector3 to, Color color)
        {
            Vector2 a = MapPoint(area, from), b = MapPoint(area, to); Fill(new Rect(a.x, b.y, b.x - a.x, a.y - b.y), color);
        }
        void Mark(Rect area, Vector3 position, string text, Color color, float size)
        {
            Vector2 point = MapPoint(area, position); if (size > 0) Fill(new Rect(point.x - size / 2, point.y - size / 2, size, size), color);
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(point.x + 9, point.y - 12, 190, 28), text, small);
        }
        void MapLine(Rect area, Vector3 from, Vector3 to)
        {
            DrawAtlasSegment(MapPoint(area, from), MapPoint(area, to), 4f, new Color(.54f, .43f, .28f));
        }

        void DrawAtlasSegment(Vector2 from, Vector2 to, float width, Color color)
        {
            if (Event.current.type != EventType.Repaint) return;
            var delta = to - from;
            if (delta.sqrMagnitude < .001f) return;
            var matrix = GUI.matrix;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUI.matrix = matrix * Matrix4x4.TRS(from, Quaternion.Euler(0f, 0f, angle), Vector3.one);
            Fill(new Rect(-.4f, -width * .5f, delta.magnitude + .8f, width), color);
            GUI.matrix = matrix;
        }
    }
}
