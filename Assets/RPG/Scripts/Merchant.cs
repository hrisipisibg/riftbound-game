using UnityEngine;

namespace Riftbound
{
    public class Merchant : MonoBehaviour
    {
        public string npcName = "Quartermaster Rowan";
        Transform label;
        void Start()
        {
            label = new GameObject("Merchant name").transform; label.SetParent(transform, false); label.localPosition = Vector3.up * 2.5f;
            var text = label.gameObject.AddComponent<TextMesh>(); text.text = npcName + "\n<Supplies & Repairs>"; text.fontSize = 48; text.characterSize = .04f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.45f, .95f, .75f);
        }
        void LateUpdate() { if (label && Camera.main) label.rotation = Camera.main.transform.rotation; }
    }
}
