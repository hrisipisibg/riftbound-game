using UnityEngine;

namespace Riftbound
{
    public class LootChest : MonoBehaviour
    {
        public string chestId;
        public int gold = 15, experience = 30;
        public bool requiresCaptainDefeated;
        public bool requiresWardenDefeated;
        public Transform lid;
        Quaternion closed = Quaternion.identity;
        void Start() { if (lid) closed = lid.localRotation; Refresh(); }
        public void Open(PlayerController player) { if (player.OpenChest(this)) Refresh(); }
        public void Refresh()
        {
            var player = FindFirstObjectByType<PlayerController>();
            if (lid && player) lid.localRotation = closed * Quaternion.Euler(player.HasOpenedChest(chestId) ? 70 : 0, 0, 0);
        }
    }
}
