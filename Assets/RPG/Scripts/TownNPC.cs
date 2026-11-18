using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound
{
    public class TownNPC : MonoBehaviour
    {
        public string npcName = "Emberwatch Guard";
        public string roleTag = "Town Watch";
        [TextArea] public string greeting = "Keep your blade sharp, traveler. The Ashwood roads grow darker each dusk.";
        public float nameplateHeight = 2.22f;

        Transform labelTransform;
        TextMesh labelMesh;
        Transform barkTransform;
        TextMesh barkMesh;
        Transform questTransform;
        TextMesh questMesh;
        PlayerController player;
        Quaternion baseRotation;
        float barkUntil;
        float nextBarkChatAllowedAt;

        public static TownNPC FindNearby(Vector3 point, float maxDistance = 3.1f)
        {
            TownNPC best = null;
            float bestDist = maxDistance;
            foreach (var npc in FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
            {
                float d = Vector3.Distance(point, npc.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        void Start()
        {
            baseRotation = transform.rotation;
            player = FindFirstObjectByType<PlayerController>();

            labelTransform = new GameObject("NPC Nameplate").transform;
            labelTransform.SetParent(transform, false);
            labelTransform.localPosition = Vector3.up * nameplateHeight;
            labelMesh = labelTransform.gameObject.AddComponent<TextMesh>();
            labelMesh.text = string.IsNullOrEmpty(roleTag) ? npcName : $"{npcName}\n<{roleTag}>";
            labelMesh.fontSize = 46;
            labelMesh.characterSize = 0.036f;
            labelMesh.anchor = TextAnchor.MiddleCenter;
            labelMesh.alignment = TextAlignment.Center;
            labelMesh.color = new Color(0.42f, 0.96f, 0.48f);

            barkTransform = new GameObject("NPC Speech Bark").transform;
            barkTransform.SetParent(transform, false);
            barkTransform.localPosition = Vector3.up * (nameplateHeight + 0.46f);
            barkMesh = barkTransform.gameObject.AddComponent<TextMesh>();
            barkMesh.text = "";
            barkMesh.fontSize = 42;
            barkMesh.characterSize = 0.032f;
            barkMesh.anchor = TextAnchor.MiddleCenter;
            barkMesh.alignment = TextAlignment.Center;
            barkMesh.color = new Color(1f, 0.95f, 0.72f);
            barkTransform.gameObject.SetActive(false);

            questTransform = new GameObject("NPC Quest Indicator").transform;
            questTransform.SetParent(transform, false);
            questTransform.localPosition = Vector3.up * (nameplateHeight + 0.95f);
            questMesh = questTransform.gameObject.AddComponent<TextMesh>();
            questMesh.text = "";
            questMesh.fontSize = 58;
            questMesh.fontStyle = FontStyle.Bold;
            questMesh.characterSize = 0.052f;
            questMesh.anchor = TextAnchor.MiddleCenter;
            questMesh.alignment = TextAlignment.Center;
            questMesh.color = new Color(1f, 0.85f, 0.15f);
            questTransform.gameObject.SetActive(false);
        }

        public void TriggerGreetingBark(PlayerController viewer = null, bool explicitInteract = false)
        {
            var resolvedViewer = viewer ? viewer : player;
            if (!explicitInteract && resolvedViewer && (resolvedViewer.InCombat() || resolvedViewer.AnyBlockingWindowOpen() || !resolvedViewer.Stats || !resolvedViewer.Stats.Alive))
                return;

            var profile = Narrative.NarrativeCatalog.ResolveTownNPC(this);
            var sel = Narrative.NarrativeCatalog.SelectText(profile, "greeting", "", resolvedViewer);
            string line = !string.IsNullOrEmpty(sel.text) ? sel.text : greeting;
            if (string.IsNullOrEmpty(line)) return;

            barkUntil = Time.time + 4.5f;
            if (barkMesh) barkMesh.text = $"\"{line}\"";
            if (Time.time >= nextBarkChatAllowedAt)
            {
                nextBarkChatAllowedAt = Time.time + 10f;
                GameSound.Play(SoundCue.Guard, transform.position);
                if (ChatSystem.Instance)
                    ChatSystem.Instance.AddMessage(ChatSystem.Channel.Say, npcName, line, new Color(0.95f, 0.95f, 0.95f));
            }
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (!cam || FrontendMenu.IsOpen)
            {
                if (labelTransform) labelTransform.gameObject.SetActive(false);
                if (barkTransform) barkTransform.gameObject.SetActive(false);
                return;
            }

            float camDist = Vector3.Distance(cam.transform.position, transform.position);
            bool showName = camDist < 28f;
            if (labelTransform.gameObject.activeSelf != showName)
                labelTransform.gameObject.SetActive(showName);

            if (showName)
                labelTransform.rotation = cam.transform.rotation;

            if (!player) player = FindFirstObjectByType<PlayerController>();
            bool isTalkingToMe = player && player.DialogueOpen && player.ActiveTownNPC == this;
            bool pauseAmbient = !player || !player.Stats || !player.Stats.Alive || player.InCombat() || (player.AnyBlockingWindowOpen() && !isTalkingToMe);
            if (player && player.Stats && player.Stats.Alive)
            {
                float playerDist = Vector3.Distance(player.transform.position, transform.position);

                if (isTalkingToMe || (!pauseAmbient && Time.time < barkUntil && playerDist < 8f))
                {
                    if (playerDist < 8f)
                    {
                        Vector3 look = player.transform.position - transform.position;
                        look.y = 0f;
                        if (look.sqrMagnitude > 0.05f)
                            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), Time.deltaTime * 5f);
                    }
                }
                else if (!pauseAmbient)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation, Time.deltaTime * 3f);
                }
            }

            bool showBark = !isTalkingToMe && !pauseAmbient && Time.time < barkUntil && camDist < 22f;
            if (barkTransform.gameObject.activeSelf != showBark)
                barkTransform.gameObject.SetActive(showBark);
            if (showBark)
                barkTransform.rotation = cam.transform.rotation;

            UpdateQuestIndicator(cam, camDist);
        }

        void UpdateQuestIndicator(Camera cam, float camDist)
        {
            if (!questTransform) return;
            string icon = "";
            Color col = new Color(1f, 0.85f, 0.15f);

            if (GetComponent<QuestGiver>())
            {
                if (player)
                {
                    if (!player.QuestAccepted && !player.QuestComplete) icon = "!";
                    else if (player.QuestAccepted && !player.QuestReady) { icon = "?"; col = new Color(0.7f, 0.7f, 0.7f); }
                    else if (player.QuestAccepted && player.QuestReady) icon = "?";
                }
            }
            else if (player && player.GetComponent<FactionQuestSystem>() is var fqs && fqs != null)
            {
                var curQ = fqs.GetActiveQuest();
                if (fqs.HasActiveQuestToComplete(npcName, player) ||
                    (curQ != null && curQ.targetType == "talk_npc" && string.Equals(curQ.targetId, npcName, StringComparison.OrdinalIgnoreCase)))
                {
                    icon = "?";
                    col = new Color(1f, 0.85f, 0.15f);
                }
                else if (curQ != null && (string.Equals(curQ.giverNpc, npcName, StringComparison.OrdinalIgnoreCase) || string.Equals(curQ.targetId, npcName, StringComparison.OrdinalIgnoreCase)))
                {
                    icon = "?";
                    col = new Color(0.7f, 0.7f, 0.7f);
                }
                else if (fqs.HasAvailableQuest(npcName, player))
                {
                    icon = "!";
                    col = new Color(1f, 0.85f, 0.15f);
                }
            }
            else if (npcName.Contains("Lucian"))
            {
                if (player && !player.OwnsItem("scarlet-blade")) icon = "!";
            }
            else if (npcName.Contains("Gorgash"))
            {
                if (player && !player.OwnsItem("bloodrock-waraxe")) icon = "!";
            }
            else if (npcName.Contains("Sylvan"))
            {
                if (player && !player.OwnsItem("ember-drake-scale")) icon = "!";
            }

            bool show = !string.IsNullOrEmpty(icon) && camDist < 35f;
            if (questTransform.gameObject.activeSelf != show)
                questTransform.gameObject.SetActive(show);

            if (show)
            {
                float bob = Mathf.Sin(Time.time * 3.2f) * 0.08f;
                questTransform.localPosition = Vector3.up * (nameplateHeight + 0.95f + bob);
                questTransform.rotation = cam.transform.rotation;
                if (questMesh.text != icon) questMesh.text = icon;
                questMesh.color = col;
            }
        }
    }
}
