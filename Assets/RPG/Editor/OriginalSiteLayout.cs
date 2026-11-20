using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Riftbound.Editor
{
    internal static class OriginalSiteLayout
    {
        internal enum Role { Cottage, Inn, Forge, Hall, Tower, Citadel, Gate, Wall, Fence, Broken, Lamp, Cargo, Barrel, Hut, Rock }
        internal enum Region { Emberwatch, Highspire, Frostpeak, Silverglen, Bloodrock, Deathknell, Rustwater, Gorkresh, Duskwood, Ashwood, Greenhollow, Westbank, Wyrmcrest }

        internal sealed class Site
        {
            internal readonly string label, root;
            internal readonly Region region;
            internal readonly (string name, Role role)[] buildings;

            internal Site(string label, string root, Region region, params (string, Role)[] buildings)
            {
                this.label = label;
                this.root = root;
                this.region = region;
                this.buildings = buildings;
            }
        }

        internal static readonly Site[] Sites =
        {
            new("Emberwatch town center", "02 - Emberwatch Outpost", Region.Emberwatch,
                ("Modular village house", Role.Cottage), ("Prop_Wagon", Role.Cargo)),
            new("Emberwatch north gate", "Emberwatch Village Architecture", Region.Emberwatch,
                ("Emberwatch Abbey & Town Hall", Role.Hall), ("Emberwatch North Gateway Arch", Role.Gate),
                ("Emberwatch Watchtower East", Role.Tower), ("Emberwatch Watchtower West", Role.Tower)),
            new("Highspire Keep", "Highspire Keep - Grand Alliance Capital", Region.Highspire,
                ("Highspire Royal Citadel", Role.Citadel), ("Highspire Arcane Sanctum", Role.Hall),
                ("Highspire Royal Exchange", Role.Inn), ("Lion's Pride Royal Inn", Role.Inn),
                ("Highspire Royal Armory", Role.Forge), ("Noble Townhouse East", Role.Cottage),
                ("Noble Townhouse West", Role.Cottage), ("Highspire East Highway Gate", Role.Gate),
                ("Highspire North Gate", Role.Gate), ("Highspire Bastion SE", Role.Tower),
                ("Highspire Bastion SW", Role.Tower), ("Highspire Bastion NW", Role.Tower),
                ("Highspire Bastion NE", Role.Tower), ("Stone Curtain Wall", Role.Wall)),
            new("Frostpeak Anvil", "Frostpeak Anvil - Dwarven Starter Village", Region.Frostpeak,
                ("Thunderbrew Mountain Mead-Hall", Role.Inn), ("Ironbeard Great Smithy", Role.Forge),
                ("Miner's Stone Cottage South", Role.Cottage), ("Mountaineer's Lodge East", Role.Cottage),
                ("Frostpeak Stone Watchtower", Role.Tower)),
            new("Silverglen Sanctum", "Silverglen Sanctum - High Elf Starter Village", Region.Silverglen,
                ("Sanctum of the Eternal Sun", Role.Hall), ("Silverglen Spire Inn & Lounge", Role.Inn),
                ("Silverglen Athenaeum", Role.Hall), ("Farstrider Ranger Lodge", Role.Cottage),
                ("Silverglen Arcane Spire", Role.Tower)),
            new("Bloodrock war village", "Camp Bloodrock - Orc Starter War-Village", Region.Bloodrock,
                ("Bloodrock Chieftain's Longhouse", Role.Inn), ("Thrak's Canyon War-Forge", Role.Forge),
                ("Bloodrock Grunt Barracks", Role.Hut), ("Outrunner Supply Hut", Role.Hut),
                ("Bloodrock West Palisade Gate", Role.Gate), ("Bloodrock East Watchtower", Role.Tower)),
            new("Deathknell Hollow", "Deathknell Hollow - Forsaken Starter Village", Region.Deathknell,
                ("Deathknell Forgotten Shadow Abbey", Role.Hall), ("Gallows' End Tavern", Role.Inn),
                ("Royal Apothecary Lab", Role.Cottage), ("Deathstalker Watch House", Role.Cottage),
                ("Deathknell Bell Tower", Role.Tower)),
            new("Rustwater Scrapworks", "Rustwater Port - Goblin Starter Town", Region.Rustwater,
                ("Bilgewater Profit & Trade Hall", Role.Inn), ("Fizzlebang's Tinkering Barn", Role.Forge),
                ("Cartel Counting House", Role.Cottage), ("Boom-Powder Depot Shack", Role.Hut),
                ("Rustwater Rig Watchtower", Role.Tower)),
            new("Gor'Kresh Stronghold", "Gor'Kresh Stronghold - Grand Horde Capital", Region.Gorkresh,
                ("Gor'Kresh Great War-Hall", Role.Citadel), ("Ancestral Spirit Lodge", Role.Hall),
                ("Gor'Kresh Great War-Forge", Role.Forge), ("Bilgewater Exchange Hall", Role.Inn),
                ("Bloodfury War-Tavern", Role.Inn), ("Warsong Clan Lodge", Role.Hut),
                ("Frostwolf Clan Lodge", Role.Hut), ("Gor'Kresh South War-Gate", Role.Gate),
                ("Gor'Kresh Dragon-Pass Gate", Role.Gate), ("Gor'Kresh Bastion SW", Role.Tower),
                ("Gor'Kresh Bastion SE", Role.Tower), ("Gor'Kresh Bastion NW", Role.Tower),
                ("Gor'Kresh Bastion NE", Role.Tower), ("Fortress Palisade Run", Role.Wall)),
            new("Duskwood Cathedral exterior", "Scarlet Cathedral Ruined Exterior", Region.Duskwood,
                ("Scarlet Monastery Nave Backing", Role.Hall), ("Cathedral Belfry West", Role.Tower),
                ("Cathedral Belfry East", Role.Tower), ("Wall_Arch", Role.Broken)),
            new("Ashwood supply camp", "Ashwood supply camp", Region.Ashwood,
                ("Stolen supplies 1", Role.Cargo), ("Stolen supplies 2", Role.Cargo), ("Stolen supplies 3", Role.Cargo),
                ("Prop_Wagon", Role.Cargo)),
            new("Ashwood ruined watchtower", "Captain's ruined watchtower", Region.Ashwood,
                ("Wall_Arch", Role.Broken)),
            new("Western Crypt courtyard", "07 - Western Crypt", Region.Duskwood,
                ("Wall_Arch", Role.Broken)),
            new("Highway and canyon gateways", "Zone Transition Gateways and Landmarks", Region.Highspire,
                ("Highspire Imperial Gatehouse", Role.Gate), ("Redridge Frontier Gatehouse", Role.Gate),
                ("Duskwood Mist Gatehouse", Role.Gate), ("Wyrmcrest Watch Spire East", Role.Tower),
                ("Wyrmcrest Watch Spire West", Role.Tower))
        };

        internal static Role? PropRole(string name)
        {
            if (name.Contains("Lamp") || name.Contains("Lantern") || name.EndsWith("Torch Post")) return Role.Lamp;
            if (name.StartsWith("Prop_WoodenFence")) return Role.Fence;
            if (name.StartsWith("Prop_MetalFence")) return Role.Wall;
            if (name.StartsWith("Stall_Cart") || name.StartsWith("FarmCrate") || name == "Crate_Wooden") return Role.Cargo;
            if (name.StartsWith("Barrel_") || name.StartsWith("TNT Barrel")) return Role.Barrel;
            return null;
        }

        internal static string Asset(Role role, Region region) => role switch
        {
            Role.Cottage => "Timber_Cottage", Role.Inn => "Wayfarer_Inn", Role.Forge => "Emberford_Smithy",
            Role.Hall => "Bell_Chapel", Role.Tower => Tower(region), Role.Wall => IsPalisade(region) ? "Palisade" : "Stone_Wall",
            Role.Fence => "Palisade", Role.Broken => "Broken_Arch", Role.Lamp => "Street_Lantern",
            Role.Cargo => "Supply_Crate", Role.Barrel => "Barrel", Role.Hut => "Briar_Camp_Hut", Role.Rock => "Moss_Rock",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };

        internal static string Tower(Region region) =>
            IsPalisade(region) || region == Region.Rustwater || region == Region.Emberwatch ? "Briar_Watchtower" : "Highspire_Masonry_Tower";

        internal static bool IsPalisade(Region region) =>
            region == Region.Bloodrock || region == Region.Gorkresh || region == Region.Ashwood;

        internal static Region GatewayRegion(string gate, Region fallback)
        {
            if (gate.StartsWith("Redridge")) return Region.Bloodrock;
            if (gate.StartsWith("Duskwood")) return Region.Duskwood;
            if (gate.StartsWith("Wyrmcrest")) return Region.Wyrmcrest;
            return fallback;
        }

        internal static (string stone, string timber, string roof, string cloth, string plaster, string metal) Palette(Region region) => region switch
        {
            Region.Highspire => ("b7b5a0", "674831", "3b506f", "385c88", "d3ba88", "b99e60"),
            Region.Frostpeak => ("8c99a4", "655144", "355e68", "a06355", "b1a99a", "b06e48"),
            Region.Silverglen => ("c9c7b1", "6e7b69", "307775", "50ae9c", "ded7ac", "c2a166"),
            Region.Bloodrock => ("a6694e", "48332c", "853e31", "a43127", "ae7654", "a17251"),
            Region.Deathknell => ("798081", "463b48", "4b466c", "568452", "99918a", "7a8559"),
            Region.Rustwater => ("827567", "4d4636", "656647", "ae7a38", "b09a76", "859669"),
            Region.Gorkresh => ("826353", "53342e", "713326", "a5322a", "b28659", "9b6846"),
            Region.Duskwood => ("6a6879", "49414b", "493c53", "82374e", "95888b", "7f6a74"),
            Region.Ashwood => ("857868", "624031", "71413b", "863833", "b19a79", "8d7060"),
            Region.Greenhollow => ("869568", "695439", "54765f", "887843", "c7b18b", "918956"),
            Region.Westbank => ("7c908a", "6a5140", "436e78", "477679", "b8ab85", "85928b"),
            Region.Wyrmcrest => ("6a6369", "4b393a", "713c46", "a4533b", "928288", "99624e"),
            _ => ("9b9a84", "63432e", "415b71", "307c78", "c6a77b", "a98759")
        };
    }
}
