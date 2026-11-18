using System.Collections;
using UnityEngine;

namespace Riftbound
{
    public class DungeonPortal : MonoBehaviour
    {
        public Vector3 destination;
        public string destinationZoneName = "The Scarlet Crypts";
        public string returnZoneName = "Duskwood Forest";
        public bool isExitPortal = false;

        float lastTeleportTime;

        void OnTriggerEnter(Collider other)
        {
            if (Time.time < lastTeleportTime + 2.5f) return;

            var player = other.GetComponent<PlayerController>() ?? other.GetComponentInParent<PlayerController>();
            if (player && player.Stats && player.Stats.Alive)
            {
                lastTeleportTime = Time.time;
                Teleport(player);
            }
        }

        public void Teleport(PlayerController player)
        {
            Vector3 safeDest = isExitPortal
                ? PlayerController.ClampAboveGround(destination, 0.35f)
                : destination;

            var cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;

            player.transform.position = safeDest;

            if (cc) cc.enabled = true;

            GameSound.Play(SoundCue.Rune, destination);

            if (isExitPortal)
            {
                player.Message = $"Returned to {returnZoneName}.";
            }
            else
            {
                player.Message = $"<color=#ff4444>★ ENTERED: {destinationZoneName.ToUpperInvariant()} (DUNGEON)</color>";
                player.DiscoverLandmark("scarlet-crypts");
            }
        }
    }
}
