using UnityEngine;

namespace Riftbound
{
    public class WorldLabel : MonoBehaviour
    {
        MeshRenderer textRenderer;
        void Awake() { textRenderer = GetComponent<MeshRenderer>(); }
        void LateUpdate()
        {
            var camera = Camera.main; if (!camera) return;
            transform.rotation = camera.transform.rotation;
            if (textRenderer) textRenderer.enabled = (camera.transform.position - transform.position).sqrMagnitude < 900;
        }
    }
}
