using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound.Narrative
{
    [Serializable]
    public class NarrativeCatalogDto
    {
        public int schemaVersion;
        public string catalogId;
        public NarrativeProfileDto[] profiles;
    }

    [Serializable]
    public class NarrativeProfileDto
    {
        public string profileId;
        public string actorKind;
        public string legacyName;
        public string enemyId;
        public string fallbackText;
        public NarrativeEntryDto[] entries;
    }

    [Serializable]
    public class NarrativeEntryDto
    {
        public string lineId;
        public string channel;
        public string scope;
        public string topicId;
        public int priority;
        public NarrativeConditionDto[] conditions;
        public string text;
    }

    [Serializable]
    public class NarrativeConditionDto
    {
        public string field;
        public string key;
        public bool boolValue;
        public int intValue;
    }

    public readonly struct NarrativeSelection
    {
        public readonly bool matched;
        public readonly string lineId;
        public readonly string text;
        public readonly bool usedFallback;

        public NarrativeSelection(bool matched, string lineId, string text, bool usedFallback)
        {
            this.matched = matched;
            this.lineId = lineId ?? "";
            this.text = text;
            this.usedFallback = usedFallback;
        }
    }

    /// <summary>
    /// Read-only consumer adapter for the SC02 narrative catalog contract (schemaVersion=1, riftbound.first-five.v1).
    /// Evaluates AND-only typed predicates against the interacting viewer's own state and fails closed on UNKNOWN context.
    /// </summary>
    public static class NarrativeCatalog
    {
        public const int ExpectedSchemaVersion = 1;
        public const string ExpectedCatalogId = "riftbound.first-five.v1";
        public const string ResourcePath = "Narrative/first-five.v1";

        enum TriState { Unknown, True, False }

        static readonly HashSet<string> AllowedActorKinds = new HashSet<string>(StringComparer.Ordinal)
        {
            "main_quest_giver", "merchant", "faction_quest_giver", "hostile_enemy"
        };

        static readonly HashSet<string> AllowedChannels = new HashSet<string>(StringComparer.Ordinal)
        {
            "greeting", "main_title", "main_body",
            "quest_offer", "quest_progress", "quest_ready", "quest_after",
            "public_topic", "encounter_bark"
        };

        static readonly HashSet<string> AllowedScopes = new HashSet<string>(StringComparer.Ordinal)
        {
            "public", "viewer", "host_encounter"
        };

        static readonly HashSet<string> AllowedFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "main.index", "main.accepted", "main.ready", "main.complete",
            "quest.available", "quest.active", "quest.ready", "quest.completed",
            "enemy.alive", "enemy.defeated"
        };

        static NarrativeCatalogDto activeCatalog;
        static bool initialized;
        static bool loadedFromResource;

        public static bool LoadedFromResource
        {
            get
            {
                EnsureLoaded();
                return loadedFromResource;
            }
        }

        public static void ReloadForTesting(string rawJson = null)
        {
            initialized = true;
            loadedFromResource = false;
            if (!string.IsNullOrEmpty(rawJson) && TryParseAndValidate(rawJson, out var parsed))
            {
                activeCatalog = parsed;
                loadedFromResource = true;
                return;
            }
            activeCatalog = CreateBuiltinFallbackCatalog();
        }

        static void EnsureLoaded()
        {
            if (initialized) return;
            initialized = true;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset && !string.IsNullOrEmpty(asset.text) && TryParseAndValidate(asset.text, out var parsed))
            {
                activeCatalog = parsed;
                loadedFromResource = true;
                return;
            }
            if (asset)
                Debug.LogWarning($"[NarrativeCatalog] Rejected '{ResourcePath}'; falling back to built-in v1 defaults.");
            activeCatalog = CreateBuiltinFallbackCatalog();
        }

        public static bool TryParseAndValidate(string json, out NarrativeCatalogDto dto)
        {
            dto = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            try
            {
                dto = JsonUtility.FromJson<NarrativeCatalogDto>(json);
            }
            catch
            {
                dto = null;
                return false;
            }
            if (!IsValidCatalog(dto))
            {
                dto = null;
                return false;
            }
            return true;
        }

        static bool IsValidCatalog(NarrativeCatalogDto dto)
        {
            if (dto == null || dto.schemaVersion != ExpectedSchemaVersion) return false;
            if (!string.Equals(dto.catalogId, ExpectedCatalogId, StringComparison.Ordinal)) return false;
            if (dto.profiles == null || dto.profiles.Length != 5) return false;

            foreach (var p in dto.profiles)
            {
                if (p == null || string.IsNullOrEmpty(p.profileId) || string.IsNullOrEmpty(p.legacyName) || string.IsNullOrEmpty(p.fallbackText))
                    return false;
                if (!AllowedActorKinds.Contains(p.actorKind ?? "")) return false;
                if (p.entries == null || p.entries.Length == 0) return false;
                foreach (var e in p.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.lineId) || string.IsNullOrEmpty(e.text)) return false;
                    if (!AllowedChannels.Contains(e.channel ?? "") || !AllowedScopes.Contains(e.scope ?? "")) return false;
                    if (e.priority < 0 || e.priority > 99) return false;
                    if (e.conditions != null)
                    {
                        foreach (var c in e.conditions)
                        {
                            if (c == null || !AllowedFields.Contains(c.field ?? "")) return false;
                            if (c.intValue < 0 || c.intValue > 3) return false;
                        }
                    }
                }
            }
            return true;
        }

        public static NarrativeProfileDto ResolveMainQuestGiver(QuestGiver giver)
            => giver ? FindProfileByKindAndName("main_quest_giver", giver.npcName) : null;

        public static NarrativeProfileDto ResolveMerchant(Merchant merchant)
            => merchant ? FindProfileByKindAndName("merchant", merchant.npcName) : null;

        public static NarrativeProfileDto ResolveTownNPC(TownNPC npc)
        {
            if (!npc) return null;
            if (npc.GetComponent<QuestGiver>() is QuestGiver qg)
                return FindProfileByKindAndName("main_quest_giver", qg.npcName);
            if (npc.GetComponent<Merchant>() is Merchant m)
                return FindProfileByKindAndName("merchant", m.npcName);
            return FindProfileByKindAndName("faction_quest_giver", npc.npcName);
        }

        public static NarrativeProfileDto ResolveHostileEnemy(EnemyController enemy)
        {
            if (!enemy || string.IsNullOrEmpty(enemy.enemyId)) return null;
            EnsureLoaded();
            if (activeCatalog?.profiles == null) return null;
            foreach (var p in activeCatalog.profiles)
            {
                if (string.Equals(p.actorKind, "hostile_enemy", StringComparison.Ordinal) &&
                    string.Equals(p.enemyId, enemy.enemyId, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        static NarrativeProfileDto FindProfileByKindAndName(string actorKind, string exactName)
        {
            if (string.IsNullOrEmpty(exactName)) return null;
            EnsureLoaded();
            if (activeCatalog?.profiles == null) return null;
            foreach (var p in activeCatalog.profiles)
            {
                if (string.Equals(p.actorKind, actorKind, StringComparison.Ordinal) &&
                    string.Equals(p.legacyName, exactName, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        public static NarrativeSelection SelectText(
            NarrativeProfileDto profile,
            string channel,
            string topicId,
            PlayerController viewer,
            EnemyController encounterActor = null)
        {
            if (profile == null || string.IsNullOrEmpty(channel))
                return new NarrativeSelection(false, "", null, false);

            NarrativeEntryDto best = null;
            string normalizedTopic = topicId ?? "";

            if (profile.entries != null)
            {
                foreach (var entry in profile.entries)
                {
                    if (entry == null || !string.Equals(entry.channel, channel, StringComparison.Ordinal))
                        continue;
                    if (!string.Equals(entry.topicId ?? "", normalizedTopic, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!EvaluateEntry(profile, entry, viewer, encounterActor))
                        continue;
                    if (IsBetterCandidate(entry, best))
                        best = entry;
                }
            }

            if (best != null)
                return new NarrativeSelection(true, best.lineId, best.text, false);

            if (string.Equals(channel, "greeting", StringComparison.Ordinal) ||
                string.Equals(channel, "public_topic", StringComparison.Ordinal))
                return new NarrativeSelection(false, "", profile.fallbackText, true);

            return new NarrativeSelection(false, "", null, false);
        }

        public static List<(string topicId, string label)> GetPublicTopics(NarrativeProfileDto profile, PlayerController viewer)
        {
            var topics = new List<(string topicId, string label)>();
            if (profile?.entries == null) return topics;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in profile.entries)
            {
                if (e == null || !string.Equals(e.channel, "public_topic", StringComparison.Ordinal)) continue;
                if (string.IsNullOrEmpty(e.topicId) || seen.Contains(e.topicId)) continue;
                if (!EvaluateEntry(profile, e, viewer, null)) continue;
                seen.Add(e.topicId);
                topics.Add((e.topicId, FormatTopicLabel(e.topicId)));
            }
            return topics;
        }

        static string FormatTopicLabel(string topicId)
        {
            switch (topicId)
            {
                case "roadward-truce": return "Ask about the roadward truce";
                case "road-work": return "Ask about clearing the road";
                case "supplies": return "Ask about garrison supplies";
                case "receipts": return "Ask about caravan receipts";
                case "road-crews": return "Ask about the road crews";
                case "fair-trade": return "Ask about fair trade";
                case "cartography": return "Ask about survey methods";
                case "emberwatch_road": return "Ask about the Ashwood road";
                case "raider_pressure": return "Ask about the raiders";
                case "outpost_ledgers": return "Ask about garrison rations";
                case "willowmere_trail": return "Ask about Willowmere & Westbank";
                case "westbank_survey": return "Ask about the Westbank survey";
                case "river_reavers": return "Ask about the River Reavers";
                default: return "Ask about the frontier";
            }
        }

        static bool IsBetterCandidate(NarrativeEntryDto candidate, NarrativeEntryDto current)
        {
            if (current == null) return true;
            if (candidate.priority != current.priority)
                return candidate.priority > current.priority;
            return string.Compare(candidate.lineId, current.lineId, StringComparison.Ordinal) < 0;
        }

        static bool EvaluateEntry(
            NarrativeProfileDto profile,
            NarrativeEntryDto entry,
            PlayerController viewer,
            EnemyController encounterActor)
        {
            if (string.Equals(entry.scope, "public", StringComparison.Ordinal))
                return entry.conditions == null || entry.conditions.Length == 0;

            if (string.Equals(entry.scope, "viewer", StringComparison.Ordinal))
            {
                if (!viewer) return false;
            }
            else if (string.Equals(entry.scope, "host_encounter", StringComparison.Ordinal))
            {
                if (!encounterActor) return false;
            }
            else
            {
                return false;
            }

            if (entry.conditions == null) return true;
            foreach (var c in entry.conditions)
            {
                if (c == null) return false;
                if (EvaluateCondition(profile, entry.scope, c, viewer, encounterActor) != TriState.True)
                    return false;
            }
            return true;
        }

        static TriState EvaluateCondition(
            NarrativeProfileDto profile,
            string scope,
            NarrativeConditionDto c,
            PlayerController viewer,
            EnemyController encounterActor)
        {
            bool isHostScope = string.Equals(scope, "host_encounter", StringComparison.Ordinal);
            if (isHostScope && !string.Equals(c.field, "enemy.alive", StringComparison.Ordinal))
                return TriState.Unknown;

            switch (c.field)
            {
                case "main.index":
                    if (!viewer) return TriState.Unknown;
                    return viewer.QuestIndex == c.intValue ? TriState.True : TriState.False;
                case "main.accepted":
                    if (!viewer) return TriState.Unknown;
                    return viewer.QuestAccepted == c.boolValue ? TriState.True : TriState.False;
                case "main.ready":
                    if (!viewer) return TriState.Unknown;
                    return viewer.QuestReady == c.boolValue ? TriState.True : TriState.False;
                case "main.complete":
                    if (!viewer) return TriState.Unknown;
                    return viewer.QuestComplete == c.boolValue ? TriState.True : TriState.False;

                case "quest.available":
                {
                    var fqs = GetViewerFactionQuests(viewer);
                    if (!fqs || string.IsNullOrEmpty(c.key)) return TriState.Unknown;
                    var avail = fqs.GetAvailableQuestForNPC(profile.legacyName, viewer);
                    bool match = avail != null && string.Equals(avail.id, c.key, StringComparison.OrdinalIgnoreCase);
                    return match == c.boolValue ? TriState.True : TriState.False;
                }
                case "quest.active":
                {
                    var fqs = GetViewerFactionQuests(viewer);
                    if (!fqs || string.IsNullOrEmpty(c.key)) return TriState.Unknown;
                    bool match = string.Equals(fqs.ActiveQuestId, c.key, StringComparison.OrdinalIgnoreCase);
                    return match == c.boolValue ? TriState.True : TriState.False;
                }
                case "quest.ready":
                {
                    var fqs = GetViewerFactionQuests(viewer);
                    if (!fqs || string.IsNullOrEmpty(c.key)) return TriState.Unknown;
                    bool match = string.Equals(fqs.ActiveQuestId, c.key, StringComparison.OrdinalIgnoreCase) && fqs.IsActiveQuestReady();
                    return match == c.boolValue ? TriState.True : TriState.False;
                }
                case "quest.completed":
                {
                    var fqs = GetViewerFactionQuests(viewer);
                    if (!fqs || string.IsNullOrEmpty(c.key)) return TriState.Unknown;
                    return fqs.IsQuestCompleted(c.key) == c.boolValue ? TriState.True : TriState.False;
                }

                case "enemy.alive":
                {
                    if (string.IsNullOrEmpty(c.key)) return TriState.Unknown;
                    if (!TryResolveEnemyAlive(c.key, encounterActor, out bool alive))
                        return TriState.Unknown;
                    return alive == c.boolValue ? TriState.True : TriState.False;
                }
                case "enemy.defeated":
                {
                    if (!viewer || string.IsNullOrEmpty(c.key)) return TriState.Unknown;
                    return viewer.HasDefeated(c.key) == c.boolValue ? TriState.True : TriState.False;
                }
                default:
                    return TriState.Unknown;
            }
        }

        static FactionQuestSystem GetViewerFactionQuests(PlayerController viewer)
        {
            if (!viewer) return null;
            return viewer.GetComponent<FactionQuestSystem>();
        }

        static bool TryResolveEnemyAlive(string enemyId, EnemyController hintActor, out bool alive)
        {
            alive = false;
            if (hintActor && string.Equals(hintActor.enemyId, enemyId, StringComparison.OrdinalIgnoreCase))
            {
                var c = hintActor.GetComponent<Combatant>();
                if (!c) return false;
                alive = hintActor.gameObject.activeInHierarchy && c.Alive;
                return true;
            }

            foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!enemy || !string.Equals(enemy.enemyId, enemyId, StringComparison.OrdinalIgnoreCase)) continue;
                var c = enemy.GetComponent<Combatant>();
                if (!c) return false;
                alive = enemy.gameObject.activeInHierarchy && c.Alive;
                return true;
            }
            return false;
        }

        static NarrativeConditionDto Cond(string field, bool b = false, int i = 0, string key = "")
            => new NarrativeConditionDto { field = field, key = key ?? "", boolValue = b, intValue = i };

        static NarrativeEntryDto Entry(string id, string channel, string scope, string topic, int priority, string text, params NarrativeConditionDto[] conds)
            => new NarrativeEntryDto { lineId = id, channel = channel, scope = scope, topicId = topic ?? "", priority = priority, conditions = conds ?? Array.Empty<NarrativeConditionDto>(), text = text };

        static NarrativeCatalogDto CreateBuiltinFallbackCatalog()
        {
            return new NarrativeCatalogDto
            {
                schemaVersion = ExpectedSchemaVersion,
                catalogId = ExpectedCatalogId,
                profiles = new[]
                {
                    new NarrativeProfileDto
                    {
                        profileId = "abe",
                        actorKind = "main_quest_giver",
                        legacyName = "Abe",
                        enemyId = "",
                        fallbackText = "Keep your lantern steady on the Ashwood road, traveler.",
                        entries = new[]
                        {
                            Entry("abe.greeting.complete", "greeting", "viewer", "", 70, "The wagons roll north again without a guard escort. Emberwatch owes you a quiet harvest.", Cond("main.complete", b: true)),
                            Entry("abe.greeting.ready", "greeting", "viewer", "", 60, "You made it back through the trees. Tell me what you found along the road.", Cond("main.ready", b: true)),
                            Entry("abe.greeting.active", "greeting", "viewer", "", 50, "Keep to the packed verge north of the gate. Three raiders can box in a careless scout.", Cond("main.accepted", b: true)),
                            Entry("abe.greeting.public", "greeting", "public", "", 10, "Wagons stack at the Emberwatch gate while the north road stays unpatrolled."),

                            Entry("abe.main0.title", "main_title", "viewer", "", 50, "The Forest Road", Cond("main.index", i: 0), Cond("main.complete", b: false)),
                            Entry("abe.main0.offer", "main_body", "viewer", "", 50, "The Ashwood road carries our flour and salves, and three road raiders have closed the north stretch past the gate. Clear those three from the road, then come back to me so the caravans can move.", Cond("main.index", i: 0), Cond("main.accepted", b: false), Cond("main.complete", b: false)),
                            Entry("abe.main0.progress", "main_body", "viewer", "", 55, "Still watching the north road? Clear out three forest raiders past the village gate and return before dusk.", Cond("main.index", i: 0), Cond("main.accepted", b: true), Cond("main.ready", b: false)),
                            Entry("abe.main0.ready", "main_body", "viewer", "", 60, "The roadrunners are scattered back into the treeline. Take the coin we set aside—though another caravan still needs its crates recovered.", Cond("main.index", i: 0), Cond("main.accepted", b: true), Cond("main.ready", b: true)),

                            Entry("abe.main1.title", "main_title", "viewer", "", 50, "Stolen Supplies", Cond("main.index", i: 1), Cond("main.complete", b: false)),
                            Entry("abe.main1.offer", "main_body", "viewer", "", 50, "Clearing the road exposed their camp east of the north trail in Ashwood. They hauled three supply crates into that clearing—bring those crates back and watch for their pair of guards.", Cond("main.index", i: 1), Cond("main.accepted", b: false), Cond("main.complete", b: false)),
                            Entry("abe.main1.progress", "main_body", "viewer", "", 55, "Look east off the north Ashwood road for the three marked supply crates. Two raiders still guard the clearing.", Cond("main.index", i: 1), Cond("main.accepted", b: true), Cond("main.ready", b: false)),
                            Entry("abe.main1.ready", "main_body", "viewer", "", 60, "Dry rations and bandages—none of it spoiled. Even better, their crates held a marked slip pointing straight to their captain.", Cond("main.index", i: 1), Cond("main.accepted", b: true), Cond("main.ready", b: true)),

                            Entry("abe.main2.title", "main_title", "viewer", "", 50, "The Ashwood Captain", Cond("main.index", i: 2), Cond("main.complete", b: false)),
                            Entry("abe.main2.offer", "main_body", "viewer", "", 50, "The slip names the Ashwood Captain holding the ruined watchtower northeast of their camp. Break his grip on that ridge, and dodge clear when his heavy strike winds up.", Cond("main.index", i: 2), Cond("main.accepted", b: false), Cond("main.complete", b: false)),
                            Entry("abe.main2.progress", "main_body", "viewer", "", 55, "The Ashwood Captain still holds the ruined watchtower northeast of camp. Step out of his red ground strike before pressing your attack.", Cond("main.index", i: 2), Cond("main.accepted", b: true), Cond("main.ready", b: false)),
                            Entry("abe.main2.ready", "main_body", "viewer", "", 60, "With the watchtower leader broken, the north road belongs to Emberwatch again. Take this axe and sigil with the village's gratitude.", Cond("main.index", i: 2), Cond("main.accepted", b: true), Cond("main.ready", b: true)),

                            Entry("abe.main3.title", "main_title", "viewer", "", 70, "Hero of Emberwatch", Cond("main.complete", b: true)),
                            Entry("abe.main3.complete", "main_body", "viewer", "", 70, "The north road stands open, and our storehouse roof can wait out the winter without fear of raiders. Keep traveling south or west toward Willowmere whenever you are ready.", Cond("main.complete", b: true)),

                            Entry("abe.topic.road", "public_topic", "public", "emberwatch_road", 20, "Every teamster bound for Highspire or Willowmere crosses our gate first. When raiders block the Ashwood treeline, the whole valley stalls."),
                            Entry("abe.topic.raiders", "public_topic", "public", "raider_pressure", 20, "They strike supply sleds rather than fortified walls—hit and vanish back toward the ruined watchtower northeast of the clearing.")
                        }
                    },
                    new NarrativeProfileDto
                    {
                        profileId = "rowan",
                        actorKind = "merchant",
                        legacyName = "Quartermaster Rowan",
                        enemyId = "",
                        fallbackText = "Dry powder, sharp steel, and ledger-checked rations for the road.",
                        entries = new[]
                        {
                            Entry("rowan.greeting.complete", "greeting", "viewer", "", 60, "Caravans from Highspire are moving on schedule again. Take what steel or salves you need.", Cond("main.complete", b: true)),
                            Entry("rowan.greeting.public", "greeting", "public", "", 10, "Keep your blade edge true and your pack light. Frontier roads charge interest on carelessness."),
                            Entry("rowan.topic.ledgers", "public_topic", "public", "outpost_ledgers", 20, "Garrison tallies stay tight until the north timber wagons clear Ashwood.")
                        }
                    },
                    new NarrativeProfileDto
                    {
                        profileId = "mira",
                        actorKind = "merchant",
                        legacyName = "Provisioner Mira",
                        enemyId = "",
                        fallbackText = "Warm broth, clean linen, and field remedies while the crossing stays muddy.",
                        entries = new[]
                        {
                            Entry("mira.greeting.defeated", "greeting", "viewer", "", 60, "Word reached Greenhollow that Selvek's banner fell on the Westbank. Even the pack mules walk easier tonight.", Cond("enemy.defeated", b: true, key: "westbank-reaver-captain")),
                            Entry("mira.greeting.public", "greeting", "public", "", 10, "Check your canteen before the river trail. Damp crossings drain warmth faster than bruises."),
                            Entry("mira.topic.trail", "public_topic", "public", "willowmere_trail", 20, "Wagons hauling salt from Willowmere watch both sides of the bridge now. Ask Elara if you need current landmarks marked.")
                        }
                    },
                    new NarrativeProfileDto
                    {
                        profileId = "elara",
                        actorKind = "faction_quest_giver",
                        legacyName = "Cartographer Elara",
                        enemyId = "",
                        fallbackText = "Keep an eye on the high ridge markers—every survey line shifts after storms.",
                        entries = new[]
                        {
                            Entry("elara.greeting.bounty_done", "greeting", "viewer", "", 80, "With Captain Selvek driven off the Westbank, Willowmere ferries can dock without archers on the bluff.", Cond("quest.completed", b: true, key: "river_reaver_bounty")),
                            Entry("elara.greeting.bounty_ready", "greeting", "viewer", "", 75, "You broke the Westbank camp. Let me close that survey ledger cleanly.", Cond("quest.ready", b: true, key: "river_reaver_bounty")),
                            Entry("elara.greeting.survey_ready", "greeting", "viewer", "", 70, "All three ridge bearings match my compass notes. Bring them here when you have a breath.", Cond("quest.ready", b: true, key: "frontier_cartography")),
                            Entry("elara.greeting.public", "greeting", "public", "", 10, "Greenhollow sits between the old road and the river bluffs. Good ground for bearings, bad ground for blind marches."),

                            Entry("elara.survey.offer", "quest_offer", "viewer", "frontier_cartography", 50, "My last caravan spotter lost his bearings between the western river bluffs and the summit ridge. Survey Westbank Overlook, Bloodrock Vista, and Wyrmcrest Lookout, then bring back clean sightlines.", Cond("quest.available", b: true, key: "frontier_cartography")),
                            Entry("elara.survey.progress", "quest_progress", "viewer", "frontier_cartography", 50, "Check all three high bearings: Westbank Overlook by the western river, Bloodrock Vista to the east, and Wyrmcrest Lookout along the summit road.", Cond("quest.active", b: true, key: "frontier_cartography"), Cond("quest.ready", b: false, key: "frontier_cartography")),
                            Entry("elara.survey.ready", "quest_ready", "viewer", "frontier_cartography", 55, "Three crisp survey marks—no guesswork left on the ridge map. Take the travel charm; wait until you see what the western bluff revealed.", Cond("quest.ready", b: true, key: "frontier_cartography")),
                            Entry("elara.survey.after", "quest_after", "viewer", "frontier_cartography", 45, "The three vista bearings sit inked in my folio now, though your Westbank notes showed smoke where no ferry should anchor.", Cond("quest.completed", b: true, key: "frontier_cartography")),

                            Entry("elara.reaver.offer", "quest_offer", "viewer", "river_reaver_bounty", 50, "Your Westbank survey caught River Reaver torches along the western bank north of Willow Crossing. Captain Selvek and two sentries hold the camp—defeat Selvek so travelers can cross safely, and his lockbox can finally be opened.", Cond("quest.available", b: true, key: "river_reaver_bounty")),
                            Entry("elara.reaver.progress", "quest_progress", "viewer", "river_reaver_bounty", 50, "Captain Selvek holds the River Reaver camp on the western bank north of Willow Crossing with two guards watching the slope. Watch the bluffline before engaging.", Cond("quest.active", b: true, key: "river_reaver_bounty"), Cond("quest.ready", b: false, key: "river_reaver_bounty")),
                            Entry("elara.reaver.ready", "quest_ready", "viewer", "river_reaver_bounty", 55, "That silences their signal fires on the western bank. Greenhollow owes your compass and your blade this Riverwarden Signet.", Cond("quest.ready", b: true, key: "river_reaver_bounty")),
                            Entry("elara.reaver.after", "quest_after", "viewer", "river_reaver_bounty", 60, "Even with Selvek defeated and the Westbank lockbox unsealed, I keep marking River Reaver driftwood along the bends—stay sharp near Willow Crossing.", Cond("quest.completed", b: true, key: "river_reaver_bounty")),

                            Entry("elara.topic.survey", "public_topic", "public", "westbank_survey", 20, "My ink marks elevation around the shattered Veyr anchors—none of the old lattice stones align straight north anymore."),
                            Entry("elara.topic.reavers", "public_topic", "public", "river_reavers", 20, "The River Reavers work the western crossing in small raiding cells, hitting freight skiffs whenever mist settles in Willowmere.")
                        }
                    },
                    new NarrativeProfileDto
                    {
                        profileId = "selvek",
                        actorKind = "hostile_enemy",
                        legacyName = "Captain Selvek",
                        enemyId = "westbank-reaver-captain",
                        fallbackText = "The Westbank crossing tolls are mine.",
                        entries = new[]
                        {
                            Entry("selvek.encounter.alive", "encounter_bark", "host_encounter", "aggro", 50, "Another Greenhollow scout? Nobody crosses Westbank without paying the Reavers' toll!", Cond("enemy.alive", b: true, key: "westbank-reaver-captain"))
                        }
                    }
                }
            };
        }
    }
}
