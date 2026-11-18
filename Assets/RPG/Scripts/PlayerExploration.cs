using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public partial class PlayerController
    {
        readonly HashSet<string> landmarks = new HashSet<string>();
        public int LandmarksFound => landmarks.Count;
        public int GreenhollowLandmarksFound => (HasLandmark("willow-crossing") ? 1 : 0) +
            (HasLandmark("greenhollow-post") ? 1 : 0) + (HasLandmark("ancient-grove") ? 1 : 0);
        public bool HasLandmark(string id) => landmarks.Contains(id);
        static bool GreenhollowLandmark(string id) =>
            id == "willow-crossing" || id == "greenhollow-post" || id == "ancient-grove";
        static bool ValidLandmark(string id) =>
            GreenhollowLandmark(id) || id == "scarlet-crypts" || id == "redridge-frontier" ||
            id == "westbank-overlook" || id == "bloodrock-vista" || id == "wyrmcrest-lookout";
        public Landmark NearbyLandmark()
        {
            Landmark nearest = null; float distance = 3.5f;
            foreach (var landmark in FindObjectsByType<Landmark>(FindObjectsSortMode.None))
            {
                float current = Vector3.Distance(transform.position, landmark.transform.position);
                if (!HasLandmark(landmark.landmarkId) && current < distance) { distance = current; nearest = landmark; }
            }
            return nearest;
        }
        public void DiscoverLandmark(string id)
        {
            if (!Stats.Alive || FrontendMenu.IsOpen || MountWindowOpen || (orbit && orbit.Paused) || !ValidLandmark(id) || landmarks.Contains(id)) return;
            if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
            {
                Network.RiftboundCoopPlayer.LocalOwner.RequestDiscoverLandmark(id);
                return;
            }
            if (!landmarks.Add(id)) return;
            Gold += 15; AddExperience(35);
            ApplyDiscoveryPresentation(id, true);
        }

        public void ApplyAuthorizedDiscovery(string id, int newGold, int xpGranted)
        {
            if (!ValidLandmark(id) || !landmarks.Add(id)) return;
            SetAuthoritativeGold(newGold);
            AddExperience(xpGranted);
            ApplyDiscoveryPresentation(id, false);
        }

        void ApplyDiscoveryPresentation(string id, bool grantOfflineBonusXp)
        {
            if (id == "scarlet-crypts")
            {
                Message = "Discovered: Scarlet Crypts! Cathedral dungeon entrance in the mists of Duskwood. +15 gold / +60 XP.";
                if (grantOfflineBonusXp) AddExperience(25);
            }
            else if (id == "redridge-frontier")
            {
                Message = "Discovered: Redridge Frontier! Mountain pass to the Orc Badlands. +15 gold / +60 XP.";
                if (grantOfflineBonusXp) AddExperience(25);
            }
            else if (GreenhollowLandmark(id))
            {
                bool complete = GreenhollowLandmarksFound == 3;
                Message = complete ? "Greenhollow explored! Zebra mount unlocked. +15 gold / +85 XP. [V] Change mount / [Z] Summon" : $"Greenhollow landmark discovered: {GreenhollowLandmarksFound}/3. +15 gold / +35 XP.";
                if (grantOfflineBonusXp && complete) AddExperience(50);
            }
            else
            {
                string name = id == "westbank-overlook" ? "Westbank Overlook" :
                    id == "bloodrock-vista" ? "Bloodrock Vista" : "Wyrmcrest Lookout";
                Message = $"Discovered: {name}! +15 gold / +35 XP.";
            }
            string landmarkTitle = id == "scarlet-crypts" ? "Scarlet Crypts" :
                id == "redridge-frontier" ? "Redridge Frontier" :
                id == "willow-crossing" ? "Willow Crossing" :
                id == "greenhollow-post" ? "Greenhollow Post" :
                id == "ancient-grove" ? "Ancient Grove" :
                id == "westbank-overlook" ? "Westbank Overlook" :
                id == "bloodrock-vista" ? "Bloodrock Vista" :
                id == "wyrmcrest-lookout" ? "Wyrmcrest Lookout" : id;

            ZoneBanner.Show("DISCOVERED", landmarkTitle);
            AudioEffects.PlayZoneDiscover();
            if (FactionQuestSystem.Instance) FactionQuestSystem.Instance.OnLandmarkDiscovered(this);
            GameSound.Play(SoundCue.Rune, transform.position); SaveAdventure(false);
        }
    }
}
