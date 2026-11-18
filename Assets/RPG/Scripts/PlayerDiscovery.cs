using System.Collections.Generic;
using UnityEngine;
namespace Riftbound
{
    public partial class PlayerController
    {
        readonly HashSet<string> discoveredRunes = new HashSet<string>();
        public bool JournalOpen { get; set; }
        public int RunesFound => discoveredRunes.Count;
        public bool HasRune(string id) => discoveredRunes.Contains(id);
        public bool HasDefeated(string id) => defeatedEnemies.Contains(id);
        public string CryptObjective => HasOpenedChest("crypt-reliquary") ? "CRYPT CLEARED / Reliquary recovered" : HasDefeated("crypt-warden") ? "CRYPT / Open the Warden's reliquary" : RunesFound >= 3 ? "CRYPT / Defeat the awakened Warden" : $"WESTERN RUNES / Activate stones: {RunesFound}/3";
        public Runestone NearbyRune()
        {
            Runestone nearest = null; float distance = 3;
            foreach (var rune in FindObjectsByType<Runestone>(FindObjectsSortMode.None))
            {
                float current = Vector3.Distance(transform.position, rune.transform.position);
                if (!HasRune(rune.runeId) && current < distance) { nearest = rune; distance = current; }
            }
            return nearest;
        }
        public void DiscoverRune(string id)
        {
            if (!Stats.Alive || FrontendMenu.IsOpen || DialogueOpen || ShopOpen || MapOpen || JournalOpen || (orbit && orbit.Paused) || string.IsNullOrEmpty(id) || !discoveredRunes.Add(id)) return;
            AddExperience(20); Message = RunesFound >= 3 ? "The western crypt awakens. Defeat its Warden!" : $"Runestone activated: {RunesFound}/3.";
            GameSound.Play(SoundCue.Rune, transform.position); RefreshAdventureWorld(); SaveAdventure(false);
        }
    }
}
