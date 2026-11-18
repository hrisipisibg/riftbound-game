using UnityEngine;

namespace Riftbound
{
    public class Landmark : MonoBehaviour
    {
        public string landmarkId, displayName;
        public void Activate(PlayerController player)
        {
            if (player && Vector3.Distance(player.transform.position, transform.position) < 3.5f) player.DiscoverLandmark(landmarkId);
        }
    }
}
