using UnityEngine;

namespace Riftbound
{
    public class TargetSelectionRing : MonoBehaviour
    {
        PlayerController player;
        LineRenderer outerRing;
        LineRenderer innerRing;
        const int Segments = 40;

        void Awake()
        {
            outerRing = CreateRing("TargetRingOuter", 0.11f, new Color(0.95f, 0.22f, 0.16f, 0.9f));
            innerRing = CreateRing("TargetRingInner", 0.06f, new Color(1f, 0.78f, 0.25f, 0.75f));
            outerRing.enabled = false;
            innerRing.enabled = false;
        }

        LineRenderer CreateRing(string goName, float width, Color color)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = true;
            lr.positionCount = Segments;
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = width;
            lr.endWidth = width;
            lr.startColor = color;
            lr.endColor = color;
            return lr;
        }

        void LateUpdate()
        {
            if (!player) player = FindFirstObjectByType<PlayerController>();
            var target = player ? player.Target : null;
            if (!target || !target.Alive || FrontendMenu.IsOpen)
            {
                outerRing.enabled = false;
                innerRing.enabled = false;
                return;
            }

            outerRing.enabled = true;
            bool isCasting = CastBarSystem.IsTargetCasting(target);
            innerRing.enabled = isCasting;

            Vector3 pos = target.transform.position;
            float groundY = Mathf.Max(pos.y, WorldRegions.GroundHeight(pos)) + 0.08f;
            float pulse = 1f + Mathf.Sin(Time.time * 4.5f) * 0.06f;
            float baseRadius = 1.15f * pulse;

            Color outerCol = isCasting
                ? new Color(1f, 0.65f, 0.15f, 0.95f)
                : new Color(0.95f, 0.22f, 0.16f, 0.85f);
            outerRing.startColor = outerCol;
            outerRing.endColor = outerCol;

            float rotOffset = Time.time * 0.8f;
            for (int i = 0; i < Segments; i++)
            {
                float a = (i / (float)Segments) * Mathf.PI * 2f + rotOffset;
                outerRing.SetPosition(i, new Vector3(pos.x + Mathf.Cos(a) * baseRadius, groundY, pos.z + Mathf.Sin(a) * baseRadius));
                if (isCasting)
                {
                    float r2 = baseRadius * (0.68f + Mathf.Sin(Time.time * 8f) * 0.06f);
                    float a2 = -a;
                    innerRing.SetPosition(i, new Vector3(pos.x + Mathf.Cos(a2) * r2, groundY + 0.03f, pos.z + Mathf.Sin(a2) * r2));
                }
            }
        }
    }
}
