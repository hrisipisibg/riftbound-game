using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class MinimapRadar
    {
        public enum QuestMark { Available, Objective, TurnIn }

        public readonly struct QuestPin
        {
            public readonly string id, caption;
            public readonly Vector3 position;
            public readonly QuestMark kind;

            public QuestPin(string id, string caption, Vector3 position, QuestMark kind)
            {
                this.id = id;
                this.caption = caption;
                this.position = position;
                this.kind = kind;
            }

            public string Icon => kind == QuestMark.Available ? "!" : kind == QuestMark.TurnIn ? "?" : "◆";
            public Color Ink => kind == QuestMark.Available ? new Color(1f, .84f, .37f) :
                kind == QuestMark.TurnIn ? new Color(.62f, 1f, .72f) : new Color(.75f, .86f, 1f);
        }

        readonly List<QuestPin> questPins = new();
        string selectedQuest;
        DungeonPortal cryptEntrance, cryptExit;
        public IReadOnlyList<QuestPin> QuestPins => questPins;

        public void SelectQuestPin(QuestPin pin)
        {
            SetWaypoint(pin.position, pin.caption);
            selectedQuest = pin.id;
        }

        public void RefreshQuestPins()
        {
            if (!player) return;
            questPins.Clear();
            void Add(string id, string caption, Vector3 position, QuestMark kind = QuestMark.Objective) =>
                questPins.Add(new QuestPin(id, caption, position, kind));

            if (!player.QuestComplete)
            {
                if (!player.QuestAccepted)
                    Add("abe", player.QuestName + " · Abe", player.ObjectivePosition, QuestMark.Available);
                else if (player.QuestReady)
                    Add("abe", player.QuestName + " · Return to Abe", player.ObjectivePosition, QuestMark.TurnIn);
                else if (player.QuestIndex == 1)
                {
                    foreach (var crate in FindObjectsByType<SupplyCrate>(FindObjectsSortMode.None))
                        if (!player.HasSupply(crate.supplyId))
                            Add("supply:" + crate.supplyId, "Stolen Supplies · Recover crate", crate.transform.position);
                }
                else
                {
                    foreach (var enemy in EnemyController.AllByKey.Values)
                        if (enemy && enemy.gameObject.activeInHierarchy &&
                            (player.QuestIndex == 0 ? enemy.IsRoadRaider : enemy.IsMainQuestCaptain) &&
                            enemy.GetComponent<Combatant>().Alive)
                            Add("enemy:" + enemy.NetworkKey, player.QuestName + " · " + enemy.GetComponent<Combatant>().displayName,
                                enemy.transform.position);
                }
            }

            if (player.CanUseWesternRoadQuest && player.WesternRoadStage == 0 && player.QuestIndex >= 2)
            {
                var rowan = WesternRoadCamps.FindRowan();
                if (rowan) Add("western", "Two Seals, One Load · Rowan", rowan.transform.position, QuestMark.Available);
            }
            if (TryGetQuestDestination(QuestFocus.Western, out var position, out var caption))
                Add("western", caption, position, player.WesternRoadStage == 3 ? QuestMark.TurnIn : QuestMark.Objective);
            if (TryGetQuestDestination(QuestFocus.Hollows, out position, out caption))
                Add("hollows", caption, position,
                    player.BrassHollowsStage == 2 && !BrassHollows.Inside(player.transform.position) ? QuestMark.TurnIn : QuestMark.Objective);

            var faction = FactionQuestSystem.Instance;
            if (TryGetQuestDestination(QuestFocus.Faction, out position, out caption))
                Add("faction:" + faction.GetActiveQuest().id, caption, position, faction.IsActiveQuestReady() ? QuestMark.TurnIn : QuestMark.Objective);
            foreach (var npc in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
            {
                if (player.CanUseBrassHollowsQuest && player.BrassHollowsStage == 0 && npc.npcName == BrassHollows.Engineer)
                    Add("hollows", "The Brass Hollows · Fizzlebang", npc.transform.position, QuestMark.Available);
                if (faction && faction.GetAvailableQuestForNPC(npc.npcName, player) is var quest && quest != null)
                    Add("faction:" + quest.id, quest.title + " · " + npc.npcName, npc.transform.position, QuestMark.Available);
            }

            if (!player.HasOpenedChest("crypt-reliquary"))
            {
                if (player.RunesFound < 3)
                {
                    foreach (var rune in FindObjectsByType<Runestone>(FindObjectsSortMode.None))
                        if (!player.HasRune(rune.runeId)) Add("rune:" + rune.runeId, "Western Runes · Activate stone", rune.transform.position);
                }
                else if (!player.HasDefeated("crypt-warden"))
                {
                    var warden = EnemyController.FindByKey("crypt-warden");
                    if (warden) Add("crypt", "Defeat the Crypt Warden", warden.transform.position);
                }
                else
                {
                    foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
                        if (chest.chestId == "crypt-reliquary") Add("crypt", "Open the Warden's reliquary", chest.transform.position);
                }
            }

            if (selectedQuest == null) return;
            foreach (var pin in questPins)
            {
                if (pin.id != selectedQuest) continue;
                customWaypoint = pin.position;
                waypointCaption = pin.caption;
                return;
            }
            selectedQuest = waypointCaption = null;
            customWaypoint = null;
        }

        internal static Vector3 AtlasPosition(Vector3 point)
        {
            if (BrassHollows.Inside(point) && BrassHollows.Current) return BrassHollows.Current.Entrance;
            if (MapFloor(point) == 1) return WorldRegions.Cathedral;
            return point;
        }

        static int MapFloor(Vector3 point) => BrassHollows.Inside(point) ? 2 :
            (WorldRegions.Expanded ? WorldRegions.Underground(point) : point.y < -20f) ? 1 : 0;

        Vector3 LocalPosition(Vector3 point, int floor)
        {
            int destinationFloor = MapFloor(point);
            if (floor == destinationFloor) return point;
            if (floor == 2 && BrassHollows.Current) return BrassHollows.Current.NavigationExit(player);
            if (floor == 1 && cryptExit) return cryptExit.transform.position;
            if (destinationFloor == 1 && cryptEntrance) return cryptEntrance.transform.position;
            return AtlasPosition(point);
        }
    }
}
