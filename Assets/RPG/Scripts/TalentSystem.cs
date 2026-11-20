using System;
using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    [Serializable]
    public class TalentNode
    {
        public string name;
        public string description;
        public int maxRank;
        public int currentRank;

        public TalentNode(string name, string desc, int maxRank)
        {
            this.name = name;
            this.description = desc;
            this.maxRank = maxRank;
            this.currentRank = 0;
        }
    }

    [Serializable]
    public class TalentTree
    {
        public string specName;
        public string roleTag;
        public string description;
        public List<TalentNode> talents = new List<TalentNode>();

        public int PointsSpent
        {
            get
            {
                int sum = 0;
                foreach (var t in talents) sum += t.currentRank;
                return sum;
            }
        }

        public TalentTree(string specName, string roleTag, string desc)
        {
            this.specName = specName;
            this.roleTag = roleTag;
            this.description = desc;
        }
    }

    public static class TalentSystem
    {
        public static int ActiveSpecIndex = 0; // 0 = Spec A (DPS), 1 = Spec B (Tank / Control / Healer)

        static readonly Dictionary<string, (TalentTree specA, TalentTree specB)> ClassTrees =
            new Dictionary<string, (TalentTree, TalentTree)>();

        public static string GetActiveSpecName(string characterClass)
        {
            var (specA, specB) = GetTrees(characterClass);
            var active = ActiveSpecIndex == 0 ? specA : specB;
            return active != null ? active.specName : "Specialization";
        }

        public static string GetActiveSpecTag(string characterClass)
        {
            var (specA, specB) = GetTrees(characterClass);
            var active = ActiveSpecIndex == 0 ? specA : specB;
            return active != null ? active.roleTag : "Adventurer";
        }

        public static (TalentTree specA, TalentTree specB) GetTrees(string characterClass)
        {
            if (string.IsNullOrEmpty(characterClass)) characterClass = "Warrior";
            if (!ClassTrees.ContainsKey(characterClass))
            {
                ClassTrees[characterClass] = BuildTreesForClass(characterClass);
            }
            return ClassTrees[characterClass];
        }

        static (TalentTree, TalentTree) BuildTreesForClass(string cls)
        {
            TalentTree specA, specB;
            switch (cls)
            {
                case "Mage":
                    specA = new TalentTree("Fire", "Burst Spell DPS", "Command devastating flames and critical infernos.");
                    specA.talents.Add(new TalentNode("Ignite", "Fireball burns targets for +15% / +30% / +45% bonus damage over 4s.", 3));
                    specA.talents.Add(new TalentNode("Pyroblast", "Fireball travels 30% faster with +10% / +20% / +30% spell critical strike chance.", 3));
                    specA.talents.Add(new TalentNode("Combustion", "Fireball hits explode in a 5m blazing blast, hitting all nearby enemies.", 1));

                    specB = new TalentTree("Frost", "Control & Defense", "Freeze enemies in their tracks with chilling frost barrier magic.");
                    specB.talents.Add(new TalentNode("Permafrost", "Frost Nova roots enemies for an additional +1.5s / +3.0s / +4.5s.", 3));
                    specB.talents.Add(new TalentNode("Ice Barrier", "Conjures a passive crystalline frost shield absorbing 25 / 50 / 75 incoming damage.", 3));
                    specB.talents.Add(new TalentNode("Blizzard", "Frost Nova area is doubled to 12m and deals +40 damage to all frozen targets.", 1));
                    break;

                case "Rogue":
                    specA = new TalentTree("Assassination", "Poison & Bleed Finisher", "Lethal venom and devastating execution strikes.");
                    specA.talents.Add(new TalentNode("Lethal Poisons", "Sinister Strike poisons enemies for +8 / +16 / +24 nature damage over 6s.", 3));
                    specA.talents.Add(new TalentNode("Cold Blood", "Increases melee critical strike chance by +10% / +20% / +30%.", 3));
                    specA.talents.Add(new TalentNode("Deathmark", "Eviscerate deals +75% massive finisher damage on poisoned enemies.", 1));

                    specB = new TalentTree("Combat", "Fast Brawler & Cleave", "High-speed steel, rapid Energy regeneration, and multi-target cleaves.");
                    specB.talents.Add(new TalentNode("Adrenaline", "Energy regenerates at 24 / 28 / 32 per second (Base is 20/sec).", 3));
                    specB.talents.Add(new TalentNode("Lightning Reflexes", "+10% / +18% / +25% Movement speed and 15% passive chance to dodge.", 3));
                    specB.talents.Add(new TalentNode("Blade Flurry", "Sinister Strike cleaves all enemies in front of you.", 1));
                    break;

                case "Paladin":
                    specA = new TalentTree("Retribution", "Holy Crusader DPS", "Channelling radiant wrath to smite the wicked.");
                    specA.talents.Add(new TalentNode("Crusader's Wrath", "Crusader Strike deals +15% / +30% / +45% damage and grants 2 Holy Power.", 3));
                    specA.talents.Add(new TalentNode("Sanctity", "Consecration ground area is increased by +40% and ticks for +40% damage.", 3));
                    specA.talents.Add(new TalentNode("Templar's Verdict", "Finisher that strikes target with heavenly light for 75 holy damage.", 1));

                    specB = new TalentTree("Holy", "Healer & Aegis", "Sacred bastion of healing light and divine invulnerability.");
                    specB.talents.Add(new TalentNode("Blessed Light", "Holy Light healing increased by +30% / +60% / +90%.", 3));
                    specB.talents.Add(new TalentNode("Aegis of Faith", "+20 / +40 / +60 Armor Rating, and taking damage generates 1 Holy Power.", 3));
                    specB.talents.Add(new TalentNode("Divine Shield", "Casting Holy Light grants 3 seconds of complete invulnerability.", 1));
                    break;

                case "Warrior":
                default:
                    specA = new TalentTree("Arms", "Melee Cleave & Juggernaut", "Unstoppable broadsword power and sweeping weapon cleaves.");
                    specA.talents.Add(new TalentNode("Deep Wounds", "Weapon strikes cause bleed dealing +12% / +24% / +36% bonus physical damage.", 3));
                    specA.talents.Add(new TalentNode("Sweeping Strikes", "Whirlwind cooldown is reduced by 1s / 2s / 3s and deals +30% damage.", 3));
                    specA.talents.Add(new TalentNode("Mortal Strike", "Devastating execution blow; deals double damage against foes below 35% HP.", 1));

                    specB = new TalentTree("Protection", "Shield Tank & Resilience", "Impenetrable defense, shield slams, and towering health pools.");
                    specB.talents.Add(new TalentNode("Toughness", "+15% / +30% / +45% Armor and +25 / +50 / +75 Max Health.", 3));
                    specB.talents.Add(new TalentNode("Shield Slam", "Strike generates +15 / +30 / +45 extra Rage and staggers light enemies.", 3));
                    specB.talents.Add(new TalentNode("Last Stand", "When health falls below 25%, instantly heal 40% Max HP (60s CD).", 1));
                    break;
            }
            return (specA, specB);
        }

        public static int GetTotalTalentPoints(PlayerController player)
        {
            if (!player) return 0;
            return Mathf.Clamp(player.Level - 1, 0, 4);
        }

        public static int GetSpentTalentPoints(PlayerController player)
        {
            var (specA, specB) = GetTrees(player.CharacterClass);
            return specA.PointsSpent + specB.PointsSpent;
        }

        public static int GetUnspentTalentPoints(PlayerController player)
        {
            return Mathf.Max(0, GetTotalTalentPoints(player) - GetSpentTalentPoints(player));
        }

        public static bool AllocatePoint(PlayerController player, TalentTree tree, int talentIndex)
        {
            if (GetUnspentTalentPoints(player) <= 0) return false;
            if (talentIndex < 0 || talentIndex >= tree.talents.Count) return false;
            var node = tree.talents[talentIndex];
            if (node.currentRank >= node.maxRank) return false;

            // Tier requirement: Talent 2 (Capstone) requires at least 3 points in this tree
            if (talentIndex == 2 && tree.PointsSpent < 3) return false;

            node.currentRank++;
            GameSound.Play(SoundCue.Rune, player.transform.position);
            ApplyTalentStats(player);
            return true;
        }

        public static void ResetTalents(PlayerController player)
        {
            var (specA, specB) = GetTrees(player.CharacterClass);
            foreach (var t in specA.talents) t.currentRank = 0;
            foreach (var t in specB.talents) t.currentRank = 0;
            GameSound.Play(SoundCue.Heal, player.transform.position);
            ApplyTalentStats(player);
        }

        public static (int[] ranksA, int[] ranksB) ExportRanks(string characterClass)
        {
            var (specA, specB) = GetTrees(characterClass);
            int[] a = new int[specA.talents.Count];
            for (int i = 0; i < a.Length; i++) a[i] = specA.talents[i].currentRank;
            int[] b = new int[specB.talents.Count];
            for (int i = 0; i < b.Length; i++) b[i] = specB.talents[i].currentRank;
            return (a, b);
        }

        public static void ImportRanks(PlayerController player, int[] ranksA, int[] ranksB)
        {
            if (!player) return;
            var (specA, specB) = GetTrees(player.CharacterClass);
            if (ranksA != null)
            {
                for (int i = 0; i < specA.talents.Count && i < ranksA.Length; i++)
                    specA.talents[i].currentRank = Mathf.Clamp(ranksA[i], 0, specA.talents[i].maxRank);
            }
            if (ranksB != null)
            {
                for (int i = 0; i < specB.talents.Count && i < ranksB.Length; i++)
                    specB.talents[i].currentRank = Mathf.Clamp(ranksB[i], 0, specB.talents[i].maxRank);
            }
            ApplyTalentStats(player);
        }

        public static void ApplyTalentStats(PlayerController player)
        {
            if (player) player.RefreshEquipmentStats();
        }

        public static (int health, int armor) GetSpecializationVitals(PlayerController player)
        {
            var (specA, specB) = GetTrees(player.CharacterClass);

            int health = 0;
            int armor = 0;

            if (player.CharacterClass == "Warrior")
            {
                int toughnessRank = specB.talents[0].currentRank;
                health += toughnessRank * 25;
                if (ActiveSpecIndex == 1) // Protection Tank
                {
                    health += 40;
                    armor += 8;
                }
            }
            else if (player.CharacterClass == "Paladin")
            {
                int aegisRank = specB.talents[1].currentRank;
                health += aegisRank * 15;
                if (ActiveSpecIndex == 1) // Holy Healer
                {
                    armor += 5;
                }
            }

            return (health, armor);
        }

        // Gameplay query helpers
        public static float GetDamageMultiplier(PlayerController player)
        {
            var (specA, _) = GetTrees(player.CharacterClass);
            float bonus = 0f;
            if (ActiveSpecIndex == 0) bonus += 0.20f; // Active Primary DPS Spec bonus

            if (player.CharacterClass == "Warrior")
            {
                int rank = specA.talents[0].currentRank;
                return 1f + bonus + (rank * 0.12f);
            }
            if (player.CharacterClass == "Mage")
            {
                int rank = specA.talents[0].currentRank;
                return 1f + bonus + (rank * 0.15f);
            }
            if (player.CharacterClass == "Paladin")
            {
                int rank = specA.talents[0].currentRank;
                return 1f + (rank * 0.15f);
            }
            return 1f;
        }

        public static float GetWhirlwindCooldownReduction(PlayerController player)
        {
            if (player.CharacterClass != "Warrior") return 0f;
            var (specA, _) = GetTrees(player.CharacterClass);
            return specA.talents[1].currentRank * 1.0f; // 1s per rank (up to 3s off)
        }

        public static float GetFrostNovaDurationBonus(PlayerController player)
        {
            if (player.CharacterClass != "Mage") return 0f;
            var (_, specB) = GetTrees(player.CharacterClass);
            return specB.talents[0].currentRank * 1.5f; // +1.5s per rank
        }

        public static float GetEnergyRegenBonus(PlayerController player)
        {
            if (player.CharacterClass != "Rogue") return 0f;
            var (_, specB) = GetTrees(player.CharacterClass);
            return specB.talents[0].currentRank * 4.0f; // +4 Energy/sec per rank
        }
    }

    public static class MilestoneTalents
    {
        public const int Tiers = 5;
        public const int ChoicesPerTier = 3;
        public const int LevelsPerTier = 5;

        public sealed class Talent
        {
            public readonly string Id, Name, Description;
            public readonly int Tier, Icon;
            public readonly float Cooldown, Range;
            public int Level => (Tier + 1) * LevelsPerTier;
            public bool Active => Cooldown > 0f;

            public Talent(int icon, string id, string name, string description, float cooldown = 0f, float range = 0f)
            {
                Icon = icon;
                Tier = icon / ChoicesPerTier;
                Id = id;
                Name = name;
                Description = description;
                Cooldown = cooldown;
                Range = range;
            }
        }

        public static readonly System.Collections.Generic.IReadOnlyList<Talent> All = new[]
        {
            new Talent(0, "ironhide", "Ironhide", "Gain 5 armor."),
            new Talent(1, "field-dressing", "Field Dressing", "Restore 20% of your maximum health.", 32f),
            new Talent(2, "trailburst", "Trailburst", "Run 65% faster for 4 seconds. Dismount before casting.", 25f),
            new Talent(3, "oakheart", "Oakheart", "Gain 35 maximum health."),
            new Talent(4, "crescent-throw", "Crescent Throw", "Send a blade of force at a visible enemy for 35 base damage.", 14f, 18f),
            new Talent(5, "fleetfoot", "Fleetfoot", "Move 8% faster on foot."),
            new Talent(6, "rally", "Rallying Horn", "Restore 35% of your maximum class resource.", 28f),
            new Talent(7, "dawnwave", "Dawnwave", "Strike visible enemies within 6 m for 28 base damage each.", 21f, 6f),
            new Talent(8, "bulwark", "Bulwark", "Gain 9 armor."),
            new Talent(9, "keen-eye", "Keen Eye", "Gain 8 percentage points of critical hit chance on weapon strikes and talents."),
            new Talent(10, "borrowed-time", "Borrowed Time", "Finish the cooldowns of your class abilities on slots 2 and 3.", 48f),
            new Talent(11, "last-edge", "Last Edge", "Deal 20% more damage below 35% health."),
            new Talent(12, "enduring-might", "Enduring Might", "Deal 12% more damage."),
            new Talent(13, "lifebloom", "Lifebloom", "Restore 45% of your maximum health.", 55f),
            new Talent(14, "thunderfist", "Thunderfist", "Strike a visible enemy within melee range for 76 base damage.", 26f, 3.5f),
        };

        public static Talent Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var talent in All)
                if (talent.Id == id) return talent;
            return null;
        }
    }

    public partial class PlayerController
    {
        readonly string[] milestoneChoices = new string[MilestoneTalents.Tiers];
        readonly string[] milestoneBindings = new string[2];
        readonly System.Collections.Generic.Dictionary<string, float> milestoneCooldowns = new();

        public bool MilestoneTalentsAvailable => !Network.CoopSessionConfig.IsMultiplayer;
        public string ChosenMilestone(int tier) => milestoneChoices[tier];
        public bool HasMilestone(string id)
        {
            var talent = MilestoneTalents.Find(id);
            return MilestoneTalentsAvailable && talent != null && Level >= talent.Level &&
                milestoneChoices[talent.Tier] == id;
        }

        int MilestoneArmor => (HasMilestone("ironhide") ? 5 : 0) + (HasMilestone("bulwark") ? 9 : 0);
        int MilestoneHealth => HasMilestone("oakheart") ? 35 : 0;
        float MilestoneDamageMultiplier => (HasMilestone("enduring-might") ? 1.12f : 1f) *
            (HasMilestone("last-edge") && Stats && Stats.Health < Stats.maxHealth * .35f ? 1.2f : 1f);

        public bool ChooseMilestone(MilestoneTalents.Talent talent)
        {
            if (talent == null || !MilestoneTalentsAvailable || Level < talent.Level || InCombat() || HasMilestone(talent.Id))
                return false;
            string previous = milestoneChoices[talent.Tier];
            milestoneChoices[talent.Tier] = talent.Id;
            if (!string.IsNullOrEmpty(previous))
                for (int i = 0; i < milestoneBindings.Length; i++)
                    if (milestoneBindings[i] == previous) milestoneBindings[i] = talent.Active ? talent.Id : null;
            if (talent.Active && System.Array.IndexOf(milestoneBindings, talent.Id) < 0)
            {
                int freeSlot = System.Array.FindIndex(milestoneBindings, string.IsNullOrEmpty);
                if (freeSlot >= 0) milestoneBindings[freeSlot] = talent.Id;
            }
            RefreshEquipmentStats();
            SaveAdventure(false);
            return true;
        }

        public MilestoneTalents.Talent MilestoneInSlot(int slot)
        {
            string id = milestoneBindings[slot];
            return HasMilestone(id) ? MilestoneTalents.Find(id) : null;
        }

        public bool BindMilestone(string id, int slot)
        {
            var talent = MilestoneTalents.Find(id);
            if (talent == null || !talent.Active || !HasMilestone(id) || InCombat()) return false;
            milestoneBindings[1 - slot] = milestoneBindings[1 - slot] == id ? null : milestoneBindings[1 - slot];
            milestoneBindings[slot] = id;
            SaveAdventure(false);
            return true;
        }

        void RestoreMilestoneTalents(string[] choices, string[] bindings)
        {
            System.Array.Clear(milestoneChoices, 0, milestoneChoices.Length);
            System.Array.Clear(milestoneBindings, 0, milestoneBindings.Length);
            milestoneCooldowns.Clear();
            for (int i = 0; choices != null && i < choices.Length && i < milestoneChoices.Length; i++)
            {
                var talent = MilestoneTalents.Find(choices[i]);
                if (talent != null && talent.Tier == i && Level >= talent.Level) milestoneChoices[i] = talent.Id;
            }
            for (int i = 0; bindings != null && i < bindings.Length && i < milestoneBindings.Length; i++)
            {
                var talent = MilestoneTalents.Find(bindings[i]);
                if (talent != null && talent.Active && HasMilestone(talent.Id) && System.Array.IndexOf(milestoneBindings, talent.Id) < 0)
                    milestoneBindings[i] = talent.Id;
            }
        }

        public float MilestoneCooldown(string id) => milestoneCooldowns.TryGetValue(id, out float ready)
            ? Mathf.Max(0f, ready - Time.time) : 0f;

        public void CastMilestoneSlot(int slot)
        {
            var talent = MilestoneInSlot(slot);
            if (talent == null || !Stats || !Stats.Alive || FrontendMenu.IsOpen || AnyBlockingWindowOpen() ||
                PrototypeHUD.MenuOpen || (orbit && orbit.Paused) || Blocking || Time.time < busyUntil) return;
            if (MilestoneCooldown(talent.Id) > 0f) return;
            switch (talent.Id)
            {
                case "field-dressing":
                case "lifebloom":
                    if (Stats.Health >= Stats.maxHealth) { Message = "Health is already full."; return; }
                    Stats.Heal(Stats.maxHealth * (talent.Id == "lifebloom" ? .45f : .20f));
                    SpellFX.CreateArcaneBurst(transform.position);
                    break;
                case "trailburst":
                    if (Mounted) { Message = "Dismount to use Trailburst."; return; }
                    sprintUntil = Mathf.Max(sprintUntil, Time.time + 4f);
                    break;
                case "crescent-throw":
                case "thunderfist":
                    if (!CanHitWithMilestone(Target, talent.Range)) { Message = $"Select a visible enemy within {talent.Range:0.#} m."; return; }
                    Hit(Target, talent.Id == "thunderfist" ? 76f : 35f);
                    break;
                case "rally":
                    if (ClassResource >= MaxClassResource) { Message = $"{ClassResourceName} is already full."; return; }
                    ClassResource = Mathf.Min(MaxClassResource, ClassResource + Mathf.Ceil(MaxClassResource * .35f));
                    break;
                case "dawnwave":
                    foreach (var enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                    {
                        var combatant = enemy.GetComponent<Combatant>();
                        if (CanHitWithMilestone(combatant, talent.Range)) Hit(combatant, 28f);
                    }
                    SpellFX.CreateConsecration(transform.position, talent.Range);
                    break;
                case "borrowed-time":
                    if (Time.time >= CleaveReady && Time.time >= HealReady) { Message = "Slots 2 and 3 are ready."; return; }
                    CleaveReady = HealReady = Time.time;
                    break;
                default:
                    return;
            }
            milestoneCooldowns[talent.Id] = Time.time + talent.Cooldown;
            AudioEffects.PlaySpellCast();
            Message = talent.Name + "!";
        }

        bool CanHitWithMilestone(Combatant target, float range)
        {
            if (!target || !target.Alive || !target.GetComponent<EnemyController>() ||
                (target.transform.position - transform.position).sqrMagnitude > range * range) return false;
            Vector3 eye = transform.position + Vector3.up * 1.2f;
            Vector3 aim = target.transform.position + Vector3.up * 1.1f;
            return !Physics.Linecast(eye, aim, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore) ||
                hit.transform.IsChildOf(target.transform) || hit.transform.IsChildOf(transform);
        }
    }
}
