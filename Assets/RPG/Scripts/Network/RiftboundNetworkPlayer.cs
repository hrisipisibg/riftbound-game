using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Riftbound.Networking
{
    [RequireComponent(typeof(CharacterController))]
    public class RiftboundNetworkPlayer : NetworkBehaviour
    {
        public NetworkVariable<FixedString64Bytes> PlayerName = new NetworkVariable<FixedString64Bytes>(
            "Adventurer", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        public NetworkVariable<float> Health = new NetworkVariable<float>(
            100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public float moveSpeed = 7f;
        CharacterController controller;
        float verticalVel;
        TextMesh overheadLabel;

        Vector2 serverMoveInput;
        Vector2 lastSentInput;
        float nextInputSendTime;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            var labelGo = new GameObject("NameLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 2.25f, 0f);
            overheadLabel = labelGo.AddComponent<TextMesh>();
            overheadLabel.alignment = TextAlignment.Center;
            overheadLabel.anchor = TextAnchor.MiddleCenter;
            overheadLabel.fontSize = 24;
            overheadLabel.characterSize = 0.08f;
            overheadLabel.color = new Color(0.95f, 0.88f, 0.55f);
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                string chosen = PlayerPrefs.GetString("Riftbound_SelectedName", $"Hero_{OwnerClientId}");
                PlayerName.Value = new FixedString64Bytes(string.IsNullOrWhiteSpace(chosen) ? $"Hero_{OwnerClientId}" : chosen);
            }

            UpdateLabel(PlayerName.Value.ToString());
            PlayerName.OnValueChanged += (_, next) => UpdateLabel(next.ToString());
            Health.OnValueChanged += (_, __) => UpdateLabel(PlayerName.Value.ToString());

            var mr = GetComponentInChildren<MeshRenderer>();
            if (mr)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.SetColor("_BaseColor", IsOwner ? new Color(0.25f, 0.72f, 0.95f) : new Color(0.92f, 0.45f, 0.25f));
                mr.sharedMaterial = mat;
            }
        }

        void UpdateLabel(string text)
        {
            if (overheadLabel) overheadLabel.text = $"{text} [{Mathf.CeilToInt(Health.Value)} HP]";
        }

        [Rpc(SendTo.Server)]
        public void SubmitMoveInputServerRpc(Vector2 input)
        {
            serverMoveInput = Vector2.ClampMagnitude(input, 1f);
        }

        void Update()
        {
            if (overheadLabel && Camera.main)
                overheadLabel.transform.rotation = Camera.main.transform.rotation;

            // Server-authoritative movement validation (NetworkTransform replicates to clients)
            if (IsServer)
            {
                Vector3 move = new Vector3(serverMoveInput.x, 0f, serverMoveInput.y);
                if (move.sqrMagnitude > 1f) move.Normalize();

                if (controller.isGrounded && verticalVel < 0f) verticalVel = -2f;
                verticalVel += Physics.gravity.y * Time.deltaTime;

                if (move.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(move), Time.deltaTime * 10f);

                controller.Move((move * moveSpeed + Vector3.up * verticalVel) * Time.deltaTime);
            }

            if (!IsOwner) return;

            var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
                Vector2 input = Vector2.ClampMagnitude(new Vector2(x, z), 1f);
                if ((input - lastSentInput).sqrMagnitude > 0.001f || (input.sqrMagnitude > 0.001f && Time.time >= nextInputSendTime))
                {
                    lastSentInput = input;
                    nextInputSendTime = Time.time + 0.05f;
                    SubmitMoveInputServerRpc(input);
                }
            }

            if (Camera.main)
            {
                Vector3 camTarget = transform.position + new Vector3(0f, 5.5f, -7.5f);
                Camera.main.transform.position = Vector3.Lerp(Camera.main.transform.position, camTarget, Time.deltaTime * 8f);
                Camera.main.transform.LookAt(transform.position + Vector3.up * 1.2f);
            }
        }
    }
}
