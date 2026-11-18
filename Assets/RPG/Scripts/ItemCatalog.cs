using UnityEngine;

namespace Riftbound
{
    public enum GearSlot { Weapon, Armor, Trinket }
    public class GearItem
    {
        public readonly string id, name, rarity;
        public readonly GearSlot slot;
        public readonly int damage, armor, health;
        public readonly int itemLevel;
        public readonly string subType;
        public readonly string flavor;
        public readonly Color color;

        public GearItem(string id, string name, GearSlot slot, string rarity, int damage, int armor, int health, int itemLevel = 1, string subType = "", string flavor = "")
        {
            this.id = id; this.name = name; this.slot = slot; this.rarity = rarity;
            this.damage = damage; this.armor = armor; this.health = health;
            this.itemLevel = itemLevel > 1 ? itemLevel : (rarity == "Legendary" ? 50 : rarity == "Epic" ? 35 : rarity == "Rare" ? 22 : rarity == "Uncommon" ? 12 : 5);
            this.subType = !string.IsNullOrEmpty(subType) ? subType : (slot == GearSlot.Weapon ? "Two-Handed" : slot == GearSlot.Armor ? "Chest Armor" : "Accessory");
            this.flavor = flavor;
            color = rarity == "Legendary" ? new Color(1.0f, 0.50f, 0.0f) :
                    rarity == "Epic" ? new Color(0.64f, 0.21f, 0.93f) :
                    rarity == "Rare" ? new Color(0.0f, 0.44f, 0.87f) :
                    rarity == "Uncommon" ? new Color(0.12f, 1.0f, 0.0f) :
                    new Color(0.85f, 0.86f, 0.82f);
        }

        public string Stats => slot == GearSlot.Weapon ? $"+{damage} weapon damage" : slot == GearSlot.Armor ? $"{armor} armor / +{health} maximum HP" : $"+{health} maximum HP";

        public string FormattedTooltip
        {
            get
            {
                var sb = new System.Text.StringBuilder();
                string hex = ColorUtility.ToHtmlStringRGB(color);
                sb.AppendLine($"<b><color=#{hex}>{name}</color></b>");
                sb.AppendLine($"<color=#ffd700>Item Level {itemLevel}</color>");
                sb.AppendLine("<color=#ffffff>Binds when picked up</color>");
                sb.AppendLine($"<color=#cccccc>{slot} · {subType}</color>");
                if (damage > 0) sb.AppendLine($"<color=#1eff00>+{damage} Weapon Damage</color>");
                if (armor > 0) sb.AppendLine($"<color=#ffffff>{armor} Armor</color>");
                if (health > 0) sb.AppendLine($"<color=#1eff00>+{health} Maximum Health</color>");
                sb.AppendLine("<color=#888888>Durability 60 / 60</color>");
                if (!string.IsNullOrEmpty(flavor))
                    sb.AppendLine($"<i><color=#ffd100>\"{flavor}\"</color></i>");
                return sb.ToString().TrimEnd();
            }
        }
    }

    public static class ItemCatalog
    {
        public static readonly GearItem[] All = {
            new GearItem("starter-axe", "Brute Axe", GearSlot.Weapon, "Common", 0, 0, 0, 1, "Two-Hand Axe", "A simple woodcutter's axe, worn from years of frontier labor."),
            new GearItem("raider-cleaver", "Raider Cleaver", GearSlot.Weapon, "Uncommon", 5, 0, 0, 8, "One-Hand Axe", "Taken from an Ashwood river brigand."),
            new GearItem("captain-axe", "Captain's Greataxe", GearSlot.Weapon, "Rare", 10, 0, 0, 18, "Two-Hand Axe", "Pillaged from Captain Selvek's vanguard encampment."),
            new GearItem("emberwatch-axe", "Emberwatch Axe", GearSlot.Weapon, "Rare", 8, 0, 0, 16, "Two-Hand Axe", "Issued to the frontier watchmen of Emberwatch Outpost."),
            new GearItem("leather-vest", "Scout's Leather Vest", GearSlot.Armor, "Common", 0, 2, 10, 3, "Leather Chest", "Tough boiled leather, standard issue for border scouts."),
            new GearItem("boneplate", "Boneguard Plate", GearSlot.Armor, "Uncommon", 0, 4, 25, 14, "Plate Chest", "Reinforced with the calcified marrow of crypt ghouls."),
            new GearItem("watchtower-cuirass", "Watchtower Cuirass", GearSlot.Armor, "Rare", 0, 5, 30, 20, "Mail Chest", "Bears the embossed lion crest of the high watchtower."),
            new GearItem("travel-charm", "Traveler's Charm", GearSlot.Trinket, "Uncommon", 0, 0, 20, 10, "Trinket", "A polished river stone blessed for safe wandering."),
            new GearItem("riverwarden-signet", "Riverwarden's Signet", GearSlot.Trinket, "Rare", 0, 0, 30, 18, "Finger Ring", "Awarded for safeguarding the Willow Crossing ferry."),
            new GearItem("emberwatch-sigil", "Emberwatch Sigil", GearSlot.Trinket, "Rare", 0, 0, 35, 22, "Holy Relic", "Gleams with the fiery vigilance of the border guards."),
            new GearItem("warden-axe", "Warden's Executioner", GearSlot.Weapon, "Rare", 12, 0, 0, 24, "Two-Hand Axe", "Heavy runic blade wielded by the guardian of the Western Crypt."),
            new GearItem("crypt-signet", "Signet of the Crypt", GearSlot.Trinket, "Rare", 0, 0, 45, 25, "Finger Ring", "Cold to the touch; echoes with ancient dormant power."),
            new GearItem("scarlet-blade", "Inquisitor's Sunblade", GearSlot.Weapon, "Epic", 16, 0, 20, 36, "One-Hand Sword", "Blazes with the unyielding fanaticism of the Scarlet Crusade."),
            new GearItem("scarlet-aegis", "Aegis of the Scarlet Dawn", GearSlot.Armor, "Epic", 0, 8, 50, 38, "Heavy Shield", "Imbued with protective prayers from the Scarlet Cathedral altar."),
            new GearItem("duskwood-shadow-dagger", "Shadowfang Stiletto", GearSlot.Weapon, "Epic", 18, 0, 10, 35, "Dagger", "Coated in dark venom drawn from the mists of Duskwood."),
            new GearItem("cathedral-relic", "Censer of the Pure Light", GearSlot.Trinket, "Epic", 4, 3, 60, 40, "Holy Relic", "Burns sacred incense that purifies dark hexes."),
            new GearItem("bloodrock-waraxe", "Bloodrock Decapitator", GearSlot.Weapon, "Rare", 14, 0, 15, 26, "Two-Hand Axe", "Forged in the blood red canyons of the Ashen Horde."),
            new GearItem("ember-drake-scale", "Heart of the Ember Drake", GearSlot.Trinket, "Legendary", 8, 6, 120, 55, "Dragon Heart", "Pulsates with the eternal subterranean magma of Wyrmcrest summit."),
            new GearItem("wyrmtooth-edge", "Wyrmtooth Cleaver", GearSlot.Weapon, "Legendary", 22, 0, 40, 58, "Two-Hand Cleaver", "Honed from the obsidian fang of Vermithrax the Ember Sovereign."),
            new GearItem("horde-war-banner", "Bloodrock War Standard", GearSlot.Trinket, "Epic", 5, 4, 75, 42, "War Banner", "Instills savage battle fury in all warriors of the Horde."),
            // Blacksmithing Crafted Gear
            new GearItem("copper-dagger", "Sharpened Copper Dagger", GearSlot.Weapon, "Common", 8, 0, 0, 5, "Dagger", "Carefully ground copper edge, ideal for novice adventurers."),
            new GearItem("bronze-broadsword", "Tempered Bronze Broadsword", GearSlot.Weapon, "Uncommon", 16, 0, 15, 16, "Two-Hand Sword", "Alloyed bronze forged over an anvil in Emberwatch."),
            new GearItem("iron-buckler", "Heavy Iron Buckler", GearSlot.Armor, "Rare", 0, 8, 35, 24, "Shield", "Layered iron banding withstands ferocious monster strikes."),
            new GearItem("truesilver-blade", "Truesilver Executioner", GearSlot.Weapon, "Epic", 24, 0, 30, 42, "Two-Hand Sword", "Forged from gleaming truesilver veins deep within the earth."),
            // World Boss Gorgonax Epic Drops
            new GearItem("worldbreaker-maul", "Worldbreaker's War-Maul", GearSlot.Weapon, "Epic", 26, 0, 50, 48, "Two-Hand Mace", "Smashes the ground with the seismic force of a mountain."),
            new GearItem("crown-of-molten-ruin", "Crown of Molten Ruin", GearSlot.Armor, "Epic", 0, 12, 70, 46, "Plate Helm", "Encrusted with glowing magma embers from the sulfur rift."),
            new GearItem("cinderfury-signet", "Cinderfury Signet", GearSlot.Trinket, "Epic", 6, 4, 65, 45, "Finger Ring", "Radiates intense heat, empowering all melee and spell attacks.")
        };

        public static GearItem Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < All.Length; i++)
                if (All[i].id == id) return All[i];
            return null;
        }
    }
}
