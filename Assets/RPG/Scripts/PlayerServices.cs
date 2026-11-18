using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class PlayerController
    {
        public bool MapOpen { get; set; }
        public bool ShopOpen => ActiveMerchant;
        public Merchant ActiveMerchant { get; private set; }
        public bool TemperedWeapon { get; private set; }
        public int WeaponBonus
        {
            get
            {
                int bonus = (Weapon != null ? Weapon.damage : 0) + (TemperedWeapon ? 4 : 0);
                if (GemSocketSystem.Instance != null)
                {
                    GemSocketSystem.Instance.GetTotalEquippedGemStats(out int gemAp, out _, out _, out _, out _);
                    bonus += gemAp / 2;
                }
                return bonus;
            }
        }
        readonly HashSet<string> openedChests = new HashSet<string>();
        public Merchant NearbyMerchant()
        {
            if (!Stats || !Stats.Alive) return null;
            Merchant result = null; float nearest = 3;
            foreach (var npc in FindObjectsByType<Merchant>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(transform.position, npc.transform.position);
                if (distance < nearest) { result = npc; nearest = distance; }
            }
            return result;
        }
        public bool CanTrade() => ActiveMerchant && Stats.Alive && Vector3.Distance(transform.position, ActiveMerchant.transform.position) < 3;
        public void CloseShop() { ActiveMerchant = null; }
        public bool InCombat()
        {
            if (Stats && Time.time - Stats.LastHit < 5) return true;
            foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                if (!enemy.trainingDummy && enemy.GetComponent<Combatant>().Alive && Vector3.Distance(transform.position, enemy.transform.position) < 12) return true;
            return false;
        }
        public struct InteractionCandidate
        {
            public string text;
            public float distance;
            public System.Action execute;
        }

        public bool TryGetBestInteraction(out InteractionCandidate best, out string outOfRangeHint)
        {
            best = default;
            outOfRangeHint = null;
            if (!Stats || !Stats.Alive) return false;
            Vector3 pos = transform.position;

            // Highest priority: revive downed nearby co-op ally within 4.5m so [E] never competes with NPC gossip
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                var lo = Network.RiftboundCoopPlayer.LocalOwner;
                Network.RiftboundCoopPlayer bestAlly = null;
                float bestAllyDist = 4.55f;
                foreach (var p in Network.RiftboundCoopPlayer.AllPlayers)
                {
                    if (!p || p == lo || p.Health.Value > 0) continue;
                    float d = Vector3.Distance(lo.transform.position, p.transform.position);
                    if (d <= bestAllyDist)
                    {
                        bestAllyDist = d;
                        bestAlly = p;
                    }
                }
                if (bestAlly != null)
                {
                    string allyName = bestAlly.PlayerName.Value.ToString();
                    ulong targetCid = bestAlly.OwnerClientId;
                    best = new InteractionCandidate
                    {
                        text = $"Revive {(string.IsNullOrWhiteSpace(allyName) ? "Ally" : allyName)}",
                        distance = bestAllyDist,
                        execute = () => lo.RequestReviveAlly(targetCid)
                    };
                    return true;
                }
            }

            bool hasCandidate = false;
            InteractionCandidate winner = default;
            void Consider(float dist, string label, System.Action action)
            {
                if (!hasCandidate || dist < winner.distance)
                {
                    hasCandidate = true;
                    winner = new InteractionCandidate { text = label, distance = dist, execute = action };
                }
            }

            if (LootDropSystem.Instance && LootDropSystem.Instance.GetNearbyCorpse(pos, 3.3f) is var corpse && corpse != null)
            {
                Consider(Vector3.Distance(pos, corpse.position), $"Loot {corpse.name}", () =>
                {
                    if (LootDropSystem.Instance.CurrentLooting != null)
                        LootDropSystem.Instance.LootAll(LootDropSystem.Instance.CurrentLooting);
                    else
                        LootDropSystem.Instance.LootAll(corpse);
                });
            }

            if (Mailbox.GetNearby(pos, 3.2f) is var mb && mb != null)
            {
                Consider(Vector3.Distance(pos, mb.transform.position), $"Open {mb.mailboxName}", () =>
                {
                    AuctionHouseSystem.MailboxOpen = !AuctionHouseSystem.MailboxOpen;
                    AuctionHouseSystem.AuctionOpen = false;
                    moveVelocity = Vector3.zero;
                });
            }

            if (FlightMaster.GetNearby(pos, 3.5f) is var flightMaster && flightMaster != null)
            {
                Consider(Vector3.Distance(pos, flightMaster.transform.position), $"Flight Master: {flightMaster.nodeName}", () =>
                {
                    if (InCombat()) { Message = "Cannot take flight while in combat."; return; }
                    FlightMaster.OpenTaxiWindow(flightMaster);
                });
            }

            if (GatheringNode.GetNearby(pos, 3.2f) is var gatherNode && gatherNode != null)
            {
                Consider(Vector3.Distance(pos, gatherNode.transform.position), gatherNode.ActionPrompt, () =>
                {
                    if (InCombat()) { Message = "Cannot gather while in combat."; return; }
                    gatherNode.StartGather(this);
                });
            }

            var merchant = NearbyMerchant();
            if (merchant)
            {
                Consider(Vector3.Distance(pos, merchant.transform.position), $"Trade with {merchant.npcName}", () =>
                {
                    if (InCombat()) { Message = "Leave combat before trading."; return; }
                    ActiveMerchant = merchant;
                    InventoryOpen = false;
                    moveVelocity = Vector3.zero;
                });
            }

            if (CanTalk())
            {
                var qGiver = questGiver.GetComponent<QuestGiver>();
                if (qGiver)
                {
                    string qLabel = QuestReady ? "Turn In Quest: Elder Abe" : (!QuestAccepted && !QuestComplete ? "Accept Quest: Elder Abe" : "Speak with Elder Abe");
                    Consider(Vector3.Distance(pos, questGiver.position), qLabel, () =>
                    {
                        ActiveQuestGiver = qGiver;
                        DialogueOpen = true;
                        dialogueOpenedAt = Time.time;
                        moveVelocity = Vector3.zero;
                    });
                }
            }

            var landmark = NearbyLandmark();
            if (landmark) Consider(Vector3.Distance(pos, landmark.transform.position), $"Discover {landmark.displayName}", () => landmark.Activate(this));

            var rune = NearbyRune();
            if (rune) Consider(Vector3.Distance(pos, rune.transform.position), "Activate Runestone", () => rune.Activate(this));

            var supply = NearbySupply();
            if (supply) Consider(Vector3.Distance(pos, supply.transform.position), "Recover Supplies", () => supply.Collect(this));

            var chest = NearbyChest();
            if (chest)
            {
                string chestPrompt = "Open Loot Cache";
                if (chest.chestId == RiverReaverCamp.CacheId && !HasDefeated(RiverReaverCamp.CaptainId))
                    chestPrompt = "Locked Cache (Defeat Captain Selvek)";
                else if (chest.requiresCaptainDefeated && !BossDefeated)
                    chestPrompt = "Locked Cache (Defeat Ashwood Captain)";
                else if (chest.requiresWardenDefeated && !HasDefeated("crypt-warden"))
                    chestPrompt = "Locked Reliquary (Defeat Crypt Warden)";
                Consider(Vector3.Distance(pos, chest.transform.position), chestPrompt, () => chest.Open(this));
            }

            var townNpc = TownNPC.FindNearby(pos, 3.1f);
            if (townNpc)
            {
                string npcLabel;
                if (townNpc.roleTag == "Auctioneer")
                    npcLabel = "Browse Auction House";
                else
                {
                    var fqs = FactionQuestSystem.Instance;
                    if (fqs != null && fqs.HasActiveQuestToComplete(townNpc.npcName, this))
                        npcLabel = $"Turn In Bounty: {townNpc.npcName}";
                    else if (fqs != null && fqs.HasAvailableQuest(townNpc.npcName, this))
                        npcLabel = $"Available Bounty: {townNpc.npcName}";
                    else
                        npcLabel = $"Speak with {townNpc.npcName}";
                }
                Consider(Vector3.Distance(pos, townNpc.transform.position), npcLabel, () =>
                {
                    if (townNpc.roleTag == "Auctioneer")
                    {
                        AuctionHouseSystem.AuctionOpen = !AuctionHouseSystem.AuctionOpen;
                        AuctionHouseSystem.MailboxOpen = false;
                        moveVelocity = Vector3.zero;
                    }
                    else
                    {
                        OpenTownNPCDialogue(townNpc);
                        townNpc.TriggerGreetingBark(this, explicitInteract: true);
                    }
                });
            }

            if (hasCandidate) { best = winner; return true; }

            // Nearby out-of-range feedback hint (3.1m..6.0m)
            float nearestHintDist = 6.0f;
            if (questGiver && questGiver.gameObject.activeInHierarchy)
            {
                float d = Vector3.Distance(pos, questGiver.position);
                if (d >= 4.0f && d < nearestHintDist) { nearestHintDist = d; outOfRangeHint = "Move closer to speak with Elder Abe."; }
            }
            var farNpc = TownNPC.FindNearby(pos, nearestHintDist);
            if (farNpc)
            {
                float d = Vector3.Distance(pos, farNpc.transform.position);
                if (d >= 3.1f && d < nearestHintDist) { nearestHintDist = d; outOfRangeHint = $"Move closer to {farNpc.npcName}."; }
            }
            foreach (var c in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
            {
                if (!c || HasOpenedChest(c.chestId)) continue;
                float d = Vector3.Distance(pos, c.transform.position);
                if (d >= 3.0f && d < nearestHintDist) { nearestHintDist = d; outOfRangeHint = "Move closer to inspect the cache."; }
            }
            return false;
        }

        void Interact()
        {
            if (Time.time < busyUntil || Blocking || Mounting)
            {
                if (Mounting) Message = "Finish or cancel mounting before interacting.";
                return;
            }
            if (TryGetBestInteraction(out var best, out string hint))
                best.execute?.Invoke();
            else if (!string.IsNullOrEmpty(hint))
                Message = hint;
        }
        public LootChest NearbyChest()
        {
            LootChest result = null; float nearest = 3;
            foreach (var chest in FindObjectsByType<LootChest>(FindObjectsSortMode.None))
            {
                float distance = Vector3.Distance(transform.position, chest.transform.position);
                if (!HasOpenedChest(chest.chestId) && distance < nearest) { result = chest; nearest = distance; }
            }
            return result;
        }
        public bool HasOpenedChest(string id) => openedChests.Contains(id);
        public bool OpenChest(LootChest chest)
        {
            if (chest && chest.chestId == RiverReaverCamp.CacheId && !HasDefeated(RiverReaverCamp.CaptainId))
            { Message = "Defeat Captain Selvek at the River Reaver camp to unlock this lockbox."; return false; }
            if (chest && chest.requiresCaptainDefeated && !BossDefeated) { Message = "Defeat the captain to unlock this cache."; return false; }
            if (chest && chest.requiresWardenDefeated && !HasDefeated("crypt-warden")) { Message = "Defeat the Crypt Warden to unlock the reliquary."; return false; }
            if (!chest || !Stats.Alive || FrontendMenu.IsOpen || DialogueOpen || ShopOpen || MapOpen || JournalOpen || (orbit && orbit.Paused) || Vector3.Distance(transform.position, chest.transform.position) >= 3 || string.IsNullOrEmpty(chest.chestId) || HasOpenedChest(chest.chestId)) return false;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                if (!Network.RiftboundCoopPlayer.LocalOwner.RequestOpenChestServerValidated(chest)) return false;
            }
            return ApplyAuthorizedChestOpen(chest);
        }

        public bool ApplyAuthorizedChestOpen(LootChest chest)
        {
            if (!chest || string.IsNullOrEmpty(chest.chestId) || !openedChests.Add(chest.chestId)) return false;
            Gold += chest.gold; Potions = Mathf.Min(10, Potions + 1); AddExperience(chest.experience);
            string item = ChestItem(chest.chestId);
            Message = $"Cache recovered: {chest.gold} gold, {chest.experience} XP." + (item.Length > 0 ? " New item: " + item + " [I]" : ""); SaveAdventure(false); return true;
        }
        public void SetAuthoritativeGold(int value) => Gold = Mathf.Max(0, value);
        public void BuyPotion()
        {
            if (!CanTrade() || InCombat() || Gold < 10 || Potions >= 10) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestTransaction("buy_potion");
                return;
            }
            Gold -= 10; Potions++; Message = "Purchased a health potion."; SaveAdventure(false);
        }
        public void TemperWeapon()
        {
            if (!CanTrade() || InCombat() || Gold < 45 || TemperedWeapon) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestTransaction("temper_weapon");
                return;
            }
            Gold -= 45; TemperedWeapon = true; Message = "Weapon tempered: +4 damage."; SaveAdventure(false);
        }
        public void RestAtMerchant()
        {
            if (!CanTrade() || InCombat() || Gold < 5 || Stats.Health >= Stats.maxHealth) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestTransaction("rest");
                return;
            }
            Gold -= 5; Stats.Heal(Stats.maxHealth); Message = "Rested at Emberwatch."; SaveAdventure(false);
        }
        public void ApplyAuthorizedTransaction(string txId, int newGold)
        {
            Gold = Mathf.Max(0, newGold);
            if (txId == "buy_potion") { Potions = Mathf.Min(10, Potions + 1); Message = "Purchased a health potion."; }
            else if (txId == "temper_weapon") { TemperedWeapon = true; Message = "Weapon tempered: +4 damage."; }
            else if (txId == "rest") { Stats.Heal(Stats.maxHealth); Message = "Rested at Emberwatch."; }
        }
    }
}
