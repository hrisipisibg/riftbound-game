using UnityEngine;

namespace Riftbound
{
    public partial class PrototypeHUD
    {
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
                float x = width / 2 - 330, y = height / 2 - 250;
                Panel(new Rect(x, y, 660, 500));
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
                if (GUI.Button(new Rect(x + 24, y + 445, 610, 38), "FAREWELL  /  E OR ESC", questButton)) player.CloseShop();
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
            Mark(area, player.ObjectivePosition, "★ Quest Objective", new Color(1, .9f, .2f), 13);

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
            Vector2 a = MapPoint(area, from), b = MapPoint(area, to);
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - 1, Vector2.Distance(a, b), 2), new Color(1f, 0.82f, 0.25f, 0.85f));
            GUI.matrix = matrix;
        }
        void DrawExpandedAtlas(Rect panel)
        {
            var area = new Rect(panel.x, panel.y, panel.height, panel.height);
            Fill(area, new Color(.17f, .23f, .16f));
            PlotZone(area, new Vector3(-320,0,125),new Vector3(-140,0,365),new Color(.14f,.16f,.20f));
            PlotZone(area, new Vector3(-145,0,325),new Vector3(100,0,510),new Color(.16f,.30f,.22f));
            PlotZone(area, new Vector3(105,0,95),new Vector3(448,0,365),new Color(.34f,.25f,.15f));
            PlotZone(area, new Vector3(95,0,420),new Vector3(448,0,608),new Color(.40f,.46f,.48f));
            PlotZone(area, new Vector3(-320,0,-160),new Vector3(-135,0,125),new Color(.15f,.25f,.23f));
            var network = FindFirstObjectByType<WorldMapRoutes>();
            if (network && network.routes != null)
                foreach (var route in network.routes)
                    for (int i=1;i<route.points.Length;i++) MapLine(area,route.points[i-1],route.points[i]);
            MapLine(area,new Vector3(0,0,3),new Vector3(0,0,100));
            MapLine(area,new Vector3(0,0,51),new Vector3(25,0,64));
            MapLine(area,new Vector3(25,0,64),new Vector3(35,0,91));
            MapLine(area,new Vector3(0,0,50),new Vector3(-34,0,70));
            for(int i=1;i<=26;i++)
            {
                float z=-130+i*10;
                Vector2 from=MapPoint(area,new Vector3(-264+9*Mathf.Sin((z-10)*.025f),0,z-10));
                Vector2 to=MapPoint(area,new Vector3(-264+9*Mathf.Sin(z*.025f),0,z));
                var matrix=GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg,from);
                Fill(new Rect(from.x,from.y-1,Vector2.Distance(from,to),2),new Color(.22f,.55f,.78f));
                GUI.matrix=matrix;
            }
            MarkZoneTitle(area,new Vector3(5,0,28),"EMBERWATCH\nAlliance starting lands",new Color(.70f,.85f,.60f));
            MarkZoneTitle(area,WorldRegions.Cathedral,"DUSKWOOD\nCathedral & haunted forest",new Color(.75f,.70f,.85f));
            MarkZoneTitle(area,WorldRegions.Bloodrock,"BLOODROCK\nHorde frontier",new Color(1,.75f,.45f));
            MarkZoneTitle(area,WorldRegions.Greenhollow,"GREENHOLLOW\nNorthern valley",new Color(.55f,.90f,.70f));
            MarkZoneTitle(area,WorldRegions.Summit,"WYRMCREST\nMountain pass & summit",new Color(.85f,.93f,1));
            MarkZoneTitle(area,new Vector3(-245,0,40),"WILLOWMERE\nRiver & western woods",new Color(.65f,.85f,.8f));
            foreach(var node in FlightMaster.WorldFlightNodes) Mark(area,node.targetPos,"",new Color(1,.8f,.25f),5);
            foreach (var landmark in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
                Mark(area, landmark.transform.position, player.HasLandmark(landmark.landmarkId) ? "" : "Scout", new Color(.55f, 1, .8f), 4);
            if(player.questGiver) Mark(area,player.questGiver.position,"",new Color(.4f,1,.5f),6);
            if(!player.QuestComplete) Mark(area,player.ObjectivePosition,"",new Color(1,.9f,.2f),9);
            Mark(area,player.transform.position,"YOU",new Color(.2f,.95f,1),8);
            float x=panel.x+panel.height+20, y=panel.y+12, w=panel.width-panel.height-36;
            GUI.Label(new Rect(x,y,w,26),"768 × 768 m",title);
            GUI.Label(new Rect(x,y+36,w,42),"Choose a region to place a travel waypoint.",label);
            string[] names={"Emberwatch Outpost","Duskwood Cathedral","Camp Bloodrock","Greenhollow Valley","Wyrmcrest Summit","Willowmere Wilds","Highspire Keep","Gor'Kresh Stronghold"};
            Vector3[] positions={new Vector3(0,0,10),WorldRegions.Cathedral,WorldRegions.Bloodrock,WorldRegions.Greenhollow,WorldRegions.Summit,new Vector3(-285,6,50),WorldRegions.Highspire,WorldRegions.Stronghold};
            for(int i=0;i<names.Length;i++)
                if(GUI.Button(new Rect(x,y+92+i*39,w,33),names[i],questButton)) MinimapRadar.SetWaypoint(positions[i],names[i]);
            GUI.Label(new Rect(x,y+422,w,80),"Gold: roads / flight stops\nCyan: your position\nYellow: quest objective\nTravel along roads or call a flight taxi.",small);
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
            Vector2 a = MapPoint(area, from), b = MapPoint(area, to); var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg, a);
            Fill(new Rect(a.x, a.y - 2, Vector2.Distance(a, b), 4), new Color(.54f, .43f, .28f)); GUI.matrix = matrix;
        }
    }
}
