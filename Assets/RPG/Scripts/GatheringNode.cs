using System;
using System.Collections;
using UnityEngine;

namespace Riftbound
{
    public enum GatheringType
    {
        Herbalism,
        Mining
    }

    public class GatheringNode : MonoBehaviour
    {
        public string nodeName = "Peacebloom";
        public GatheringType gatheringType = GatheringType.Herbalism;
        public int rewardXp = 25;
        public int rewardGold = 3;
        public string rewardItem = "Peacebloom Leaf";
        public float respawnDelay = 60f;

        public string ActionPrompt => gatheringType == GatheringType.Herbalism ? $"Gather {nodeName}" : $"Mine {nodeName}";

        bool gathered = false;
        Renderer[] renderers;
        Collider[] colliders;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
        }

        public static GatheringNode GetNearby(Vector3 pos, float radius = 3.2f)
        {
            GatheringNode nearest = null;
            float minDst = radius;
            foreach (var node in FindObjectsByType<GatheringNode>(FindObjectsSortMode.None))
            {
                if (node.gathered || !node.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(pos, node.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    nearest = node;
                }
            }
            return nearest;
        }

        public void StartGather(PlayerController player)
        {
            if (gathered || !player || !player.Stats.Alive) return;

            string verb = gatheringType == GatheringType.Herbalism ? "Gathering" : "Mining";
            CastBarSystem.StartCast($"{verb} {nodeName}...", 2.2f, () =>
            {
                FinishGather(player);
            }, true, true);
        }

        void FinishGather(PlayerController player)
        {
            if (gathered || !player) return;
            gathered = true;

            player.AddExperience(rewardXp);
            player.AddGoldDirect(rewardGold);
            CombatTextManager.Spawn(transform.position + Vector3.up * 1.2f, $"+{rewardXp} XP", new Color(0.4f, 0.85f, 1f), false);
            CombatTextManager.Spawn(transform.position + Vector3.up * 1.8f, $"+{rewardGold} Gold", new Color(1f, 0.85f, 0.25f), false);

            ChatSystem.LogSystem($"You gathered [{nodeName}]: received {rewardGold} gold and {rewardXp} experience.");
            if (ProfessionSystem.Instance != null)
            {
                ProfessionSystem.Instance.AddReagent(nodeName, 1);
            }
            AudioEffects.PlayLootCoin();

            StartCoroutine(RespawnRoutine());
        }

        IEnumerator RespawnRoutine()
        {
            SetVisible(false);
            yield return new WaitForSeconds(respawnDelay);
            gathered = false;
            SetVisible(true);
        }

        void SetVisible(bool vis)
        {
            if (renderers != null)
                foreach (var r in renderers) if (r) r.enabled = vis;
            if (colliders != null)
                foreach (var c in colliders) if (c) c.enabled = vis;
        }
    }
}
