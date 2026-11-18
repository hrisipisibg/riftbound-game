using UnityEngine;

namespace Riftbound
{
    public class QuestGiver : MonoBehaviour
    {
        public string npcName = "Abe";
        public string questTitle = "The Forest Road";
        [TextArea] public string introduction = "These old bones cannot patrol the road anymore, but our supplies still need to get through. Raiders have been gathering beyond the village gate.\n\nClear them out, and I will see that you are paid.";
        [TextArea] public string progress = "The road is still dangerous. Keep your eyes open beyond the gate, and return when you have dealt with three raiders.";
        [TextArea] public string completion = "You did it! The merchants can travel safely again. A small purse, as promised — and the thanks of this old man.";
        public int rewardGold = 25;
        PlayerController player;
        TextMesh marker;
        Transform labels;
        void Start()
        {
            player = FindFirstObjectByType<PlayerController>();
            labels = new GameObject("Abe name and quest marker").transform;
            labels.SetParent(transform, false);
            MakeLabel(npcName, 2.35f, .045f, new Color(.5f, 1, .55f));
            marker = MakeLabel("!", 2.85f, .13f, new Color(1, .8f, .2f));
        }
        TextMesh MakeLabel(string value, float height, float size, Color color)
        {
            var go = new GameObject(value); go.transform.SetParent(labels, false); go.transform.localPosition = Vector3.up * height;
            var text = go.AddComponent<TextMesh>(); text.text = value; text.characterSize = size; text.fontSize = 48;
            text.anchor = TextAnchor.MiddleCenter; text.color = color; return text;
        }
        void LateUpdate()
        {
            if (!player) return;
            marker.text = player.QuestComplete ? "" : player.QuestAccepted ? "?" : "!";
            marker.color = player.QuestAccepted && !player.QuestReady ? Color.gray : new Color(1, .8f, .2f);
            if (Camera.main) labels.rotation = Camera.main.transform.rotation;
        }
    }
}
