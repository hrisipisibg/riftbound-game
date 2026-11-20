using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public enum AdventureGuideTab
    {
        Overview,
        Journal,
        Explore
    }

    public enum JournalEntryStatus
    {
        ReadyToTurnIn,
        Active,
        Available,
        Completed,
        Unavailable
    }

    public enum GuideActionKind
    {
        None,
        TrackMain,
        TrackWestern,
        TrackHollows,
        PinSite,
        OpenEncounters
    }

    public struct GuideDestination
    {
        public string label;
        public Vector3 position;
        public bool available;
    }

    public sealed class JournalChapterView
    {
        public int index;
        public string title;
        public string regionTag;
        public JournalEntryStatus status;
        public string statusLabel;
        public string summary;
        public string objective;
        public string availabilityNote;
        public GuideActionKind actionKind;
        public string actionLabel;
        public GuideDestination destination;
    }

    public sealed class ExploreCardView
    {
        public string id;
        public string title;
        public string subtitle;
        public string purpose;
        public string howToStart;
        public string namedDestination;
        public string practicalHint;
        public string availabilityBadge;
        public string liveObjectiveLine;
        public bool isSoloQuestRoute;
        public bool questUnavailableInCoop;
        public bool allowTrackWestern;
        public bool allowTrackHollows;
        public bool showEncountersButton;
        public readonly List<GuideDestination> liveStops = new List<GuideDestination>();
    }

    public static class AdventureGuideCatalog
    {
        static readonly string[] ChapterTitles =
        {
            "1. The Forest Road",
            "2. Stolen Supplies",
            "3. The Ashwood Captain",
            "4. The Western Crypt",
            "5. Scout Greenhollow",
            "6. The Scarlet Crypts",
            "7. Redridge Frontier",
            "8. Apex of Wyrmcrest",
            WesternRoadCamps.QuestTitle,
            BrassHollows.QuestTitle
        };

        static readonly string[] ChapterSummaries =
        {
            "Abe needs the forest road outside Emberwatch secured. Defeat three road raiders, then return to him for your reward.",
            "The raiders took Emberwatch's winter supplies to their camp. Recover three crates from Ashwood and bring news back to Abe.",
            "Challenge the captain at the northern watchtower. Accept Abe's mission first, defeat the captain, and report back to claim your Emberwatch gear.",
            "Ancient runestones mark a trail west of the forest road. Awaken all three to face the Crypt Warden, then open the reliquary at the rear of the ruins.",
            "Follow the north road beyond the watchtower and scout Willow Crossing, Greenhollow Post, and the Ancient Grove to earn gold, XP, and unlock the Frontier Zebra.",
            "Beneath the cathedral ruins in northwest Duskwood lie the Scarlet Crypts. Enter through the crimson portal, purge the corrupted zealots, and claim the Inquisitor's Sunblade from the grand altar.",
            "Northeast beyond the pass lies the Redridge Frontier and Camp Bloodrock, held by Warlord Gorgash and Armorer Thrak. Challenge the crag orcs and claim the Bloodrock Decapitator.",
            "Ascend to the caldera at Wyrmcrest Summit once you have a Winged Steed. Defeat Vermithrax the Ember Sovereign to claim the legendary Heart of the Ember Drake.",
            "A grain wagon never reached Emberwatch. Quartermaster Rowan needs its waybill, and Ferrymaster Sera keeps a record of what actually crossed Westbank.",
            "The Hollow Choir took Rustwater's regulator to an abandoned mine off Redridge Pass. Clear the mine and shut down the siphon."
        };

        public static List<JournalChapterView> BuildJournalEntries(PlayerController player)
        {
            var list = new List<JournalChapterView>(ChapterTitles.Length);
            if (!player) return list;

            bool expanded = WorldRegions.Expanded;
            bool isMultiplayer = Network.CoopSessionConfig.IsMultiplayer;

            for (int i = 0; i < ChapterTitles.Length; i++)
            {
                var entry = new JournalChapterView
                {
                    index = i,
                    title = ChapterTitles[i],
                    summary = ChapterSummaries[i],
                    availabilityNote = "",
                    actionKind = GuideActionKind.None,
                    actionLabel = ""
                };

                if (i <= 2)
                    PopulateAbeChapter(player, entry);
                else if (i == 3)
                    PopulateCryptChapter(player, entry);
                else if (i == 4)
                    PopulateGreenhollowChapter(player, entry);
                else if (i == 5)
                    PopulateScarletChapter(player, entry, expanded);
                else if (i == 6)
                    PopulateRedridgeChapter(player, entry, expanded);
                else if (i == 7)
                    PopulateWyrmcrestChapter(player, entry, expanded);
                else if (i == 8)
                    PopulateWesternRoadChapter(player, entry, expanded, isMultiplayer);
                else if (i == 9)
                    PopulateBrassHollowsChapter(player, entry, expanded, isMultiplayer);

                list.Add(entry);
            }

            return list;
        }

        static void PopulateAbeChapter(PlayerController player, JournalChapterView entry)
        {
            entry.regionTag = "Emberwatch · Main Campaign";
            int i = entry.index;

            if (player.QuestIndex > i)
            {
                entry.status = JournalEntryStatus.Completed;
                entry.statusLabel = "COMPLETED";
                entry.objective = "Chapter completed. Rewards claimed from Elder Abe.";
            }
            else if (player.QuestIndex == i)
            {
                if (player.QuestReady)
                {
                    entry.status = JournalEntryStatus.ReadyToTurnIn;
                    entry.statusLabel = "READY TO TURN IN";
                }
                else if (player.QuestAccepted)
                {
                    entry.status = JournalEntryStatus.Active;
                    entry.statusLabel = "ACTIVE";
                }
                else
                {
                    entry.status = JournalEntryStatus.Available;
                    entry.statusLabel = "AVAILABLE";
                }
                entry.objective = player.QuestObjective;
                entry.actionKind = GuideActionKind.TrackMain;
                entry.actionLabel = "TRACK MAIN QUEST";
            }
            else
            {
                entry.status = JournalEntryStatus.Unavailable;
                entry.statusLabel = "UNAVAILABLE";
                entry.objective = "Complete the earlier campaign chapters with Elder Abe first.";
                entry.availabilityNote = $"Requires Chapter {i} completion.";
            }
        }

        static void PopulateCryptChapter(PlayerController player, JournalChapterView entry)
        {
            entry.regionTag = "Amber Vale · Optional Ruins";
            bool reliquaryOpened = player.HasOpenedChest("crypt-reliquary");
            bool wardenDefeated = player.HasDefeated("crypt-warden");

            if (reliquaryOpened)
            {
                entry.status = JournalEntryStatus.Completed;
                entry.statusLabel = "COMPLETED";
            }
            else if (wardenDefeated || player.RunesFound > 0)
            {
                entry.status = wardenDefeated ? JournalEntryStatus.ReadyToTurnIn : JournalEntryStatus.Active;
                entry.statusLabel = wardenDefeated ? "RELIQUARY UNLOCKED" : "ACTIVE";
            }
            else
            {
                entry.status = JournalEntryStatus.Available;
                entry.statusLabel = "OPTIONAL";
            }

            entry.objective =
                $"Runestones Awakened: {player.RunesFound}/3\n" +
                $"Crypt Warden: {(wardenDefeated ? "DEFEATED" : player.RunesFound >= 3 ? "AWAKENED" : "DORMANT")}\n" +
                $"Reliquary Chest: {(reliquaryOpened ? "RECOVERED" : "SEALED")}\n" +
                "Recommended: Level 3+ with armor and potions.";

            if (!reliquaryOpened)
            {
                entry.destination = FindCryptDestination();
                if (entry.destination.available)
                {
                    entry.actionKind = GuideActionKind.PinSite;
                    entry.actionLabel = "PIN WESTERN CRYPT";
                }
            }
        }

        static void PopulateGreenhollowChapter(PlayerController player, JournalChapterView entry)
        {
            entry.regionTag = "Greenhollow Valley · Exploration";
            int found = WorldRegions.Expanded ? player.GreenhollowLandmarksFound : player.LandmarksFound;
            bool done = found >= 3;

            entry.status = done ? JournalEntryStatus.Completed : (found > 0 ? JournalEntryStatus.Active : JournalEntryStatus.Available);
            entry.statusLabel = done ? "COMPLETED" : (found > 0 ? "ACTIVE" : "OPTIONAL");

            entry.objective =
                $"Greenhollow Landmarks: {found}/3\n" +
                "Emberwatch Horse: AVAILABLE [Z]\n" +
                $"Frontier Zebra: {(done ? "UNLOCKED [V]" : "SCOUT ALL THREE LANDMARKS")}";

            if (!done)
            {
                Vector3 target = WorldRegions.Expanded ? WorldRegions.Greenhollow : new Vector3(-24f, 0f, 156f);
                entry.destination = new GuideDestination { label = "Greenhollow Valley", position = target, available = true };
                entry.actionKind = GuideActionKind.PinSite;
                entry.actionLabel = "PIN GREENHOLLOW";
            }
        }

        static void PopulateScarletChapter(PlayerController player, JournalChapterView entry, bool expanded)
        {
            entry.regionTag = "Duskwood Cathedral · Dungeon";
            bool done = player.OwnsItem("scarlet-blade");
            entry.status = done ? JournalEntryStatus.Completed : JournalEntryStatus.Available;
            entry.statusLabel = done ? "COMPLETED" : "DUNGEON";

            entry.objective =
                $"Cathedral Entrance: Duskwood Cathedral{(expanded ? " (Connected Frontier)" : "")}\n" +
                $"Inquisitor's Sunblade: {(done ? "ACQUIRED" : "UNCLAIMED")}\n" +
                "Dungeon Bosses: High Inquisitor Malakar & Dread Abbot Vael";

            entry.actionKind = GuideActionKind.OpenEncounters;
            entry.actionLabel = "VIEW IN ENCOUNTERS [Shift+J]";
            entry.destination = new GuideDestination
            {
                label = "Duskwood Cathedral",
                position = expanded ? WorldRegions.Cathedral : new Vector3(-55f, 0f, 215f),
                available = true
            };
        }

        static void PopulateRedridgeChapter(PlayerController player, JournalChapterView entry, bool expanded)
        {
            entry.regionTag = "Redridge Canyons · Frontier";
            bool done = player.OwnsItem("bloodrock-waraxe");
            entry.status = done ? JournalEntryStatus.Completed : JournalEntryStatus.Available;
            entry.statusLabel = done ? "COMPLETED" : "HORDE CAMP";

            var fqs = FactionQuestSystem.Instance;
            var fq = fqs != null ? fqs.GetActiveQuest() : null;
            string fqLine = fq != null ? $"\nActive Mission: {fq.title} ({fqs.CurrentProgress}/{fq.requiredCount})" : "";

            entry.objective =
                "Location: Camp Bloodrock\n" +
                $"Bloodrock Decapitator: {(done ? "ACQUIRED" : "UNCLAIMED")}\n" +
                "Territory: Horde Outpost" + fqLine;

            if (!done)
            {
                entry.destination = new GuideDestination
                {
                    label = "Camp Bloodrock",
                    position = expanded ? WorldRegions.Bloodrock : new Vector3(50f, 0f, 210f),
                    available = true
                };
                entry.actionKind = GuideActionKind.PinSite;
                entry.actionLabel = "PIN CAMP BLOODROCK";
            }
        }

        static void PopulateWyrmcrestChapter(PlayerController player, JournalChapterView entry, bool expanded)
        {
            entry.regionTag = "Wyrmcrest Summit · Apex Boss";
            bool done = player.OwnsItem("ember-drake-scale");
            entry.status = done ? JournalEntryStatus.Completed : JournalEntryStatus.Available;
            entry.statusLabel = done ? "COMPLETED" : "APEX BOSS";

            entry.objective =
                $"Mount: {(player.SelectedMount && player.SelectedMount.isFlying ? "WINGED STEED READY [Z]" : "SUMMON MOUNT [Z]")}\n" +
                $"Vermithrax: {(done ? "DEFEATED" : "ALIVE AT WYRMCREST SUMMIT (Lv 8)")}\n" +
                $"Legendary Heart: {(done ? "RECOVERED" : "SEALED IN HOARD")}";

            entry.actionKind = GuideActionKind.OpenEncounters;
            entry.actionLabel = "VIEW IN ENCOUNTERS [Shift+J]";
            entry.destination = new GuideDestination
            {
                label = "Wyrmcrest Summit",
                position = expanded ? WorldRegions.Summit : new Vector3(0f, 48f, 225f),
                available = true
            };
        }

        static void PopulateWesternRoadChapter(PlayerController player, JournalChapterView entry, bool expanded, bool isMultiplayer)
        {
            entry.regionTag = "Westbank Approach · Solo Frontier";
            if (!player.CanUseWesternRoadQuest)
            {
                entry.status = JournalEntryStatus.Unavailable;
                entry.statusLabel = "UNAVAILABLE";
                entry.availabilityNote = isMultiplayer
                    ? "Solo progression only (not shared in multiplayer co-op)."
                    : "Requires the Expanded Frontier region.";
                entry.objective = entry.availabilityNote;
                return;
            }

            if (player.WesternRoadStage >= 4)
            {
                entry.status = JournalEntryStatus.Completed;
                entry.statusLabel = "COMPLETED";
            }
            else if (player.WesternRoadStage == 3)
            {
                entry.status = JournalEntryStatus.ReadyToTurnIn;
                entry.statusLabel = "READY TO TURN IN";
            }
            else if (player.WesternRoadActive)
            {
                entry.status = JournalEntryStatus.Active;
                entry.statusLabel = "ACTIVE";
            }
            else if (player.QuestIndex >= 2)
            {
                entry.status = JournalEntryStatus.Available;
                entry.statusLabel = "AVAILABLE";
            }
            else
            {
                entry.status = JournalEntryStatus.Unavailable;
                entry.statusLabel = "UNAVAILABLE";
                entry.availabilityNote = "Complete Stolen Supplies with Elder Abe first.";
            }

            entry.objective = player.WesternRoadStage == 0 && player.QuestIndex < 2
                ? "Complete Chapter 2 (Stolen Supplies) with Elder Abe, then speak to Quartermaster Rowan in Emberwatch."
                : player.WesternRoadObjective;

            if (player.WesternRoadActive)
            {
                entry.actionKind = GuideActionKind.TrackWestern;
                entry.actionLabel = "TRACK WESTERN QUEST";
            }
            else if (entry.status == JournalEntryStatus.Available)
            {
                var rowan = WesternRoadCamps.FindRowan();
                if (rowan)
                {
                    entry.destination = new GuideDestination
                    {
                        label = WesternRoadCamps.Rowan,
                        position = rowan.transform.position,
                        available = true
                    };
                    entry.actionKind = GuideActionKind.PinSite;
                    entry.actionLabel = "PIN QUARTERMASTER ROWAN";
                }
            }
        }

        static void PopulateBrassHollowsChapter(PlayerController player, JournalChapterView entry, bool expanded, bool isMultiplayer)
        {
            entry.regionTag = "Redridge Pass · Solo Mine";
            if (!player.CanUseBrassHollowsQuest)
            {
                entry.status = JournalEntryStatus.Unavailable;
                entry.statusLabel = "UNAVAILABLE";
                entry.availabilityNote = isMultiplayer
                    ? "Solo progression only (not shared in multiplayer co-op)."
                    : "Requires the Expanded Frontier region.";
                entry.objective = entry.availabilityNote;
                return;
            }

            if (player.BrassHollowsStage >= 3)
            {
                entry.status = JournalEntryStatus.Completed;
                entry.statusLabel = "COMPLETED";
            }
            else if (player.BrassHollowsStage == 2)
            {
                entry.status = JournalEntryStatus.ReadyToTurnIn;
                entry.statusLabel = "READY TO TURN IN";
            }
            else if (player.BrassHollowsQuestActive)
            {
                entry.status = JournalEntryStatus.Active;
                entry.statusLabel = "ACTIVE";
            }
            else
            {
                entry.status = JournalEntryStatus.Available;
                entry.statusLabel = "AVAILABLE";
            }

            entry.objective = player.BrassHollowsObjective;

            if (player.BrassHollowsQuestActive)
            {
                entry.actionKind = GuideActionKind.TrackHollows;
                entry.actionLabel = "TRACK CAVE QUEST";
            }
            else if (entry.status == JournalEntryStatus.Available)
            {
                var fizz = FindTownNpc(BrassHollows.Engineer);
                Vector3 pos = fizz ? fizz.transform.position : WorldRegions.Rustwater;
                entry.destination = new GuideDestination
                {
                    label = $"{BrassHollows.Engineer} (Rustwater)",
                    position = pos,
                    available = fizz != null || expanded
                };
                if (entry.destination.available)
                {
                    entry.actionKind = GuideActionKind.PinSite;
                    entry.actionLabel = "PIN TINKER FIZZLEBANG";
                }
            }
        }

        public static int GetCurrentGoalChapterIndex(PlayerController player, IReadOnlyList<JournalChapterView> entries)
        {
            if (entries == null || entries.Count == 0) return 0;

            for (int i = 0; i < entries.Count; i++)
                if (entries[i].status == JournalEntryStatus.ReadyToTurnIn)
                    return i;

            for (int i = 0; i < entries.Count; i++)
                if (entries[i].status == JournalEntryStatus.Active && (i <= 2 || i == 8 || i == 9))
                    return i;

            if (player && !player.QuestComplete && player.QuestIndex >= 0 && player.QuestIndex <= 2)
                return player.QuestIndex;

            for (int i = 0; i < entries.Count; i++)
                if (entries[i].status == JournalEntryStatus.Active)
                    return i;

            for (int i = 0; i < entries.Count; i++)
                if (entries[i].status == JournalEntryStatus.Available)
                    return i;

            return 0;
        }

        public static List<int> GetRecommendedSideIndices(IReadOnlyList<JournalChapterView> entries, int currentGoalIndex)
        {
            var result = new List<int>(2);
            if (entries == null) return result;

            int[] priorityOrder = { 8, 9, 4, 3, 5, 6, 7 };
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (int idx in priorityOrder)
                {
                    if (idx < 0 || idx >= entries.Count || idx == currentGoalIndex) continue;
                    var s = entries[idx].status;
                    if (s == JournalEntryStatus.Completed || s == JournalEntryStatus.Unavailable) continue;
                    bool isUrgent = s == JournalEntryStatus.ReadyToTurnIn || s == JournalEntryStatus.Active;
                    if ((pass == 0 && isUrgent) || (pass == 1 && !isUrgent && !result.Contains(idx)))
                    {
                        result.Add(idx);
                        if (result.Count >= 2) return result;
                    }
                }
            }
            return result;
        }

        public static List<ExploreCardView> BuildExploreCards(PlayerController player)
        {
            bool expanded = WorldRegions.Expanded;
            bool isMultiplayer = Network.CoopSessionConfig.IsMultiplayer;
            var cards = new List<ExploreCardView>(4);

            // 1. Emberwatch to Greenhollow (North Road)
            var northCard = new ExploreCardView
            {
                id = "north-road",
                title = "Emberwatch to Greenhollow",
                subtitle = "North Road Corridor",
                purpose = "Travel north from Emberwatch through the watchtower corridor to reach Greenhollow Valley and survey the frontier landmarks.",
                howToStart = "Follow the main northern highway out of Emberwatch past Elder Abe's chapters, or speak with Cartographer Elara in Greenhollow Valley.",
                namedDestination = "Greenhollow Valley",
                practicalHint = "Watch for the roadside pull-off and bell tower along the northern stretch, and scout all three Greenhollow landmarks to unlock the Frontier Zebra.",
                availabilityBadge = expanded ? "CONNECTED FRONTIER" : "OPEN ROUTE"
            };
            var threeChimes = FindWaystop("Three-Chimes Relay");
            var lanternRest = FindWaystop("Lantern Rest");
            var serviceCourt = FindWaystop("Emberwatch Service Court");
            if (serviceCourt != null)
            {
                northCard.liveStops.Add(new GuideDestination { label = serviceCourt.displayName, position = serviceCourt.transform.position, available = true });
            }
            if (threeChimes != null)
            {
                northCard.namedDestination = "Three-Chimes Relay & Greenhollow Valley";
                northCard.liveStops.Add(new GuideDestination { label = threeChimes.displayName, position = threeChimes.transform.position, available = true });
            }
            else if (lanternRest != null)
            {
                northCard.namedDestination = "Lantern Rest & Greenhollow Valley";
                northCard.liveStops.Add(new GuideDestination { label = lanternRest.displayName, position = lanternRest.transform.position, available = true });
            }
            northCard.liveStops.Add(new GuideDestination
            {
                label = "Greenhollow Valley",
                position = expanded ? WorldRegions.Greenhollow : new Vector3(-24f, 0f, 156f),
                available = true
            });
            if (player)
            {
                int found = expanded ? player.GreenhollowLandmarksFound : player.LandmarksFound;
                northCard.liveObjectiveLine = $"Greenhollow Landmarks: {found}/3";
            }
            cards.Add(northCard);

            // 2. Missing Shipment / Westbank
            bool canWestern = player && player.CanUseWesternRoadQuest;
            var westernCamps = WesternRoadCamps.Current;
            var westernCard = new ExploreCardView
            {
                id = "western-road",
                title = "Missing Shipment / Westbank",
                subtitle = "The Missing Shipment",
                purpose = "Trace a missing grain wagon west across the river to compare its broken waybill against the ferry tally at Westbank.",
                howToStart = isMultiplayer
                    ? "Solo Expanded Frontier quest only (disabled while in a multiplayer co-op session)."
                    : "In solo play after finishing Stolen Supplies, ask Quartermaster Rowan in Emberwatch about the missing shipment.",
                namedDestination = "Split-Seal Layby -> Tallykeepers' Rest -> Westbank",
                practicalHint = "Clear or pull the Split-Seal raiders at the layby before recovering the waybill, then cross the Westbank bridge to compare seals with Ferrymaster Sera.",
                availabilityBadge = canWestern ? (player.WesternRoadStage >= 4 ? "COMPLETED" : player.WesternRoadActive ? "ACTIVE (SOLO)" : "SOLO QUEST") : "SOLO ONLY",
                isSoloQuestRoute = true,
                questUnavailableInCoop = isMultiplayer,
                allowTrackWestern = canWestern && player.WesternRoadActive,
                liveObjectiveLine = canWestern && player.WesternRoadStage > 0 ? player.WesternRoadObjective : ""
            };
            var rowan = WesternRoadCamps.FindRowan();
            if (rowan)
                westernCard.liveStops.Add(new GuideDestination { label = WesternRoadCamps.Rowan, position = rowan.transform.position, available = true });
            if (westernCamps && westernCamps.Layby)
                westernCard.liveStops.Add(new GuideDestination { label = "Split-Seal Layby", position = westernCamps.Layby.position, available = true });
            if (westernCamps && westernCamps.Rest)
                westernCard.liveStops.Add(new GuideDestination { label = "Tallykeepers' Rest", position = westernCamps.Rest.position, available = true });
            if (westernCamps && westernCamps.Ferrymaster)
                westernCard.liveStops.Add(new GuideDestination { label = WesternRoadCamps.Witness, position = westernCamps.Ferrymaster.transform.position, available = true });
            else if (expanded)
                westernCard.liveStops.Add(new GuideDestination { label = "Westbank Crossing", position = new Vector3(-285f, 6f, 50f), available = true });
            cards.Add(westernCard);

            // 3. Rustwater to Brass Hollows
            bool canHollows = player && player.CanUseBrassHollowsQuest;
            bool hollowsKnown = canHollows && player.BrassHollowsStage >= 1;
            var hollows = BrassHollows.Current;
            var hollowsCard = new ExploreCardView
            {
                id = "brass-hollows",
                title = "Rustwater to Brass Hollows",
                subtitle = "Into the Brass Hollows",
                purpose = "Follow the eastern pass from Rustwater Scrapworks to the Brass Hollows mine and shut down the Choir's siphon.",
                howToStart = isMultiplayer
                    ? "Solo Expanded Frontier quest only (disabled while in a multiplayer co-op session)."
                    : "In solo play, speak with Tinker Fizzlebang at Rustwater Scrapworks to accept Cut the Siphon.",
                namedDestination = hollows ? "Rustwater Scrapworks -> Brass Hollows entrance" : "Rustwater Scrapworks (Redridge Pass)",
                practicalHint = hollowsKnown
                    ? "Dismount before entering the mine, step out of floor vents when their red warning ring pulses, and strike Foreman Veyl during his recovery window after Crushing Blow."
                    : "Dismount before entering the mine passage, and speak with Tinker Fizzlebang in Rustwater before challenging the sealed quarry.",
                availabilityBadge = canHollows ? (player.BrassHollowsStage >= 3 ? "COMPLETED" : player.BrassHollowsQuestActive ? "ACTIVE (SOLO)" : "SOLO QUEST") : "SOLO ONLY",
                isSoloQuestRoute = true,
                questUnavailableInCoop = isMultiplayer,
                allowTrackHollows = canHollows && player.BrassHollowsQuestActive,
                liveObjectiveLine = canHollows && player.BrassHollowsStage > 0 ? player.BrassHollowsObjective : ""
            };
            var fizz = FindTownNpc(BrassHollows.Engineer);
            if (fizz)
                hollowsCard.liveStops.Add(new GuideDestination { label = $"{BrassHollows.Engineer} (Rustwater)", position = fizz.transform.position, available = true });
            else if (expanded)
                hollowsCard.liveStops.Add(new GuideDestination { label = "Rustwater Scrapworks", position = WorldRegions.Rustwater, available = true });
            var coilbreak = FindWaystop("Coilbreak Yard");
            if (coilbreak != null)
                hollowsCard.liveStops.Add(new GuideDestination { label = coilbreak.displayName, position = coilbreak.transform.position, available = true });
            if (hollows)
                hollowsCard.liveStops.Add(new GuideDestination { label = "Brass Hollows entrance", position = hollows.Entrance, available = true });
            cards.Add(hollowsCard);

            // 4. Gor'Kresh to Wyrmcrest
            var shelter = FindWaystop("Gor'Kresh Trailhead Shelter");
            var summitCard = new ExploreCardView
            {
                id = "wyrmcrest-climb",
                title = "Gor'Kresh to Wyrmcrest",
                subtitle = "Climb to Wyrmcrest",
                purpose = "Ascend from Gor'Kresh Stronghold through the mountain switchbacks to reach Wyrmcrest Summit.",
                howToStart = "Begin at the Gor'Kresh Stronghold trailhead or mount a Celestial Winged Steed [Z] for the high crag ascent.",
                namedDestination = shelter != null
                    ? "Gor'Kresh Stronghold -> Gor'Kresh Trailhead Shelter -> Wyrmcrest Summit"
                    : "Gor'Kresh Stronghold -> Wyrmcrest Summit",
                practicalHint = shelter != null
                    ? "Stop at the optional Gor'Kresh Trailhead Shelter inside the wall to check tack and potions before the steep switchbacks, and review Vermithrax in the Encounter Journal."
                    : "Check potions and mount readiness before the steep switchbacks, and review Vermithrax's tactics in the Encounter Journal [Shift+J].",
                availabilityBadge = "HIGHLAND ROUTE",
                showEncountersButton = true,
                liveObjectiveLine = player && player.OwnsItem("ember-drake-scale")
                    ? "Vermithrax defeated — Heart of the Ember Drake recovered."
                    : "Apex encounter details and loot live in Encounter Journal [Shift+J]."
            };
            summitCard.liveStops.Add(new GuideDestination
            {
                label = "Gor'Kresh Stronghold",
                position = expanded ? WorldRegions.Stronghold : new Vector3(85f, 0f, 235f),
                available = true
            });
            if (shelter != null)
                summitCard.liveStops.Add(new GuideDestination { label = shelter.displayName, position = shelter.transform.position, available = true });
            summitCard.liveStops.Add(new GuideDestination
            {
                label = "Wyrmcrest Summit",
                position = expanded ? WorldRegions.Summit : new Vector3(0f, 48f, 225f),
                available = true
            });
            cards.Add(summitCard);

            return cards;
        }

        static SettlementWaystop FindWaystop(string displayName)
        {
            var list = SettlementWaystop.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var stop = list[i];
                if (stop && string.Equals(stop.displayName, displayName, StringComparison.OrdinalIgnoreCase))
                    return stop;
            }
            return null;
        }

        static TownNPC FindTownNpc(string npcName)
        {
            foreach (var npc in UnityEngine.Object.FindObjectsByType<TownNPC>())
            {
                if (npc && string.Equals(npc.npcName, npcName, StringComparison.OrdinalIgnoreCase))
                    return npc;
            }
            return null;
        }

        static GuideDestination FindCryptDestination()
        {
            foreach (var chest in UnityEngine.Object.FindObjectsByType<LootChest>())
            {
                if (chest && chest.chestId == "crypt-reliquary")
                    return new GuideDestination { label = "Western Crypt", position = chest.transform.position, available = true };
            }
            foreach (var rune in UnityEngine.Object.FindObjectsByType<Runestone>())
            {
                if (rune)
                    return new GuideDestination { label = "Western Crypt", position = rune.transform.position, available = true };
            }
            return new GuideDestination
            {
                label = "Western Crypt",
                position = new Vector3(-34f, 0f, 70f),
                available = !WorldRegions.Expanded
            };
        }
    }
}
