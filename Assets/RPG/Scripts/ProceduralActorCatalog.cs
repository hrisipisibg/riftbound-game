using UnityEngine;

namespace Riftbound
{
    public sealed class ProceduralActorCatalog : ScriptableObject
    {
        public GameObject[] alliance;
        public GameObject[] horde;
        public GameObject brute, goblin, sentinel, wraith;
        public GameObject smith, scout, merchant;

        static ProceduralActorCatalog current;
        public static ProceduralActorCatalog Current =>
            current ? current : current = Resources.Load<ProceduralActorCatalog>("ProceduralActorCatalog");

        public GameObject ForClass(string race, string characterClass, bool hostile = false)
        {
            int index = characterClass switch { "Mage" => 1, "Rogue" => 2, "Paladin" => 3, _ => 0 };
            var faction = hostile || race == "Orc" || race == "Undead" || race == "Goblin" ? horde : alliance;
            return faction != null && index < faction.Length ? faction[index] : null;
        }
    }
}
