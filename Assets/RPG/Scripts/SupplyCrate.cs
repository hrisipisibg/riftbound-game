using UnityEngine;

namespace Riftbound
{
    public class SupplyCrate : MonoBehaviour
    {
        public string supplyId;
        public bool Collected { get; private set; }
        TextMesh marker;
        void Start()
        {
            var label = new GameObject("Supply marker"); label.transform.SetParent(transform, false); label.transform.localPosition = Vector3.up * 1.8f;
            marker = label.AddComponent<TextMesh>(); marker.text = "SUPPLIES"; marker.anchor = TextAnchor.MiddleCenter; marker.fontSize = 48; marker.characterSize = .055f; marker.color = new Color(1, .8f, .2f);
            var player = FindFirstObjectByType<PlayerController>(); if (player) Refresh(player);
        }
        void LateUpdate() { if (marker && Camera.main) marker.transform.rotation = Camera.main.transform.rotation; }
        public void Refresh(PlayerController player)
        {
            Collected = player.HasSupply(supplyId);
            gameObject.SetActive(!Collected);
            if (marker && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
        }
        public void Collect(PlayerController player)
        {
            if (Collected || !player || !player.Stats.Alive || player.QuestIndex != 1 || !player.QuestAccepted || player.DialogueOpen || (player.orbit && player.orbit.Paused) || Vector3.Distance(transform.position, player.transform.position) >= 3) return;
            player.RecordSupply(supplyId); Refresh(player);
        }
    }
}
