using UnityEngine;

namespace Riftbound.Editor
{
    internal static class TerrainLayout
    {
        internal readonly struct Road
        {
            public readonly Vector3 from, to;
            public Road(Vector3 from, Vector3 to) { this.from = from; this.to = to; }
        }
        internal static readonly Road[] Roads = {
            new Road(new Vector3(0, 0, 3), new Vector3(0, 0, 32)),
            new Road(new Vector3(0, 0, 32), new Vector3(0, 1.6f, 66)),
            new Road(new Vector3(0, .9f, 51), new Vector3(25, 2, 61)),
            new Road(new Vector3(27, 2, 69), new Vector3(35, 6, 87)),
            new Road(new Vector3(0, .85f, 50), new Vector3(-32, 3.5f, 61)),
            new Road(new Vector3(0, 1.6f, 66), new Vector3(0, 3.2f, 112)),
            new Road(new Vector3(0, 3.2f, 112), new Vector3(0, 3.2f, 136)),
            new Road(new Vector3(0, 3.2f, 136), new Vector3(0, 5.5f, 153)),
            new Road(new Vector3(0, 5.5f, 153), new Vector3(-26, 5.5f, 157)),
            new Road(new Vector3(1, 5.5f, 155), new Vector3(22, 10, 198)),
            new Road(new Vector3(31, 9.8f, 204), new Vector3(48, 9.0f, 212)),
            new Road(new Vector3(61, 9.0f, 217), new Vector3(84, 14.0f, 222)),
            new Road(new Vector3(-34, 5.5f, 168), new Vector3(-52, 12.5f, 204))
        };
        static float Hill(float x, float z, float cx, float cz, float sx, float sz) => Mathf.Exp(-Mathf.Pow((x - cx) / sx, 2) - Mathf.Pow((z - cz) / sz, 2));
        static float Blend(float outside, float fade) => 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(outside / fade));
        static float Box(float x, float z, float cx, float cz, float halfX, float halfZ, float fade) => Blend(Mathf.Max(0, Mathf.Max(Mathf.Abs(x - cx) - halfX, Mathf.Abs(z - cz) - halfZ)), fade);
        static float Disc(float x, float z, float cx, float cz, float radius, float fade) => Blend(Mathf.Max(0, Vector2.Distance(new Vector2(x, z), new Vector2(cx, cz)) - radius), fade);
        internal static float RoadDistance(float x, float z, Road road, out float t)
        {
            var a = new Vector2(road.from.x, road.from.z); var delta = new Vector2(road.to.x - road.from.x, road.to.z - road.from.z);
            t = Mathf.Clamp01(Vector2.Dot(new Vector2(x, z) - a, delta) / delta.sqrMagnitude);
            return Vector2.Distance(new Vector2(x, z), a + delta * t);
        }
        internal static float Height(float x, float z)
        {
            float height = .45f + Mathf.Max(0, z - 25) * .023f;
            height += 7 * Hill(x, z, -58, 60, 28, 42) + 9 * Hill(x, z, 57, 164, 27, 49) + 8 * Hill(x, z, -56, 202, 32, 32);
            height += 20 * Hill(x, z, -100, 75, 19, 130) + 22 * Hill(x, z, 100, 125, 20, 145) + 19 * Hill(x, z, 0, 250, 90, 22);
            height += .65f * Mathf.PerlinNoise(x * .035f + 43, z * .032f + 17);
            height = Mathf.Lerp(height, 0, Box(x, z, 0, 6, 29, 28, 10));
            height = Mathf.Lerp(height, 6, Box(x, z, 35, 91, 12, 11, 8));
            height = Mathf.Lerp(height, 2, Box(x, z, 25, 63.5f, 17, 12.5f, 5));
            height = Mathf.Lerp(height, 3.5f, Disc(x, z, -34, 68, 15, 6));
            height = Mathf.Lerp(height, 5.5f, Box(x, z, -25, 157, 16, 14, 6));
            height = Mathf.Lerp(height, 10, Disc(x, z, 22, 201, 11, 8));
            height = Mathf.Lerp(height, 0.87f, Box(x, z, -45, -20, 17, 17, 7));
            height = Mathf.Lerp(height, 9.0f, Box(x, z, 39, 176, 9, 9, 6));
            height = Mathf.Lerp(height, 7.5f, Disc(x, z, 10, 185, 9, 5));
            height = Mathf.Lerp(height, 9.0f, Box(x, z, 54, 214, 9, 8, 6));
            height = Mathf.Lerp(height, 14.0f, Box(x, z, 85, 234, 17, 16, 8));
            height = Mathf.Lerp(height, 12.5f, Box(x, z, -54, 211, 8, 9, 6));
            height = Mathf.Lerp(height, 3.2f, Box(x, z, 0, 124, 67, 12, 4));
            foreach (var road in Roads)
            {
                float distance = RoadDistance(x, z, road, out float t);
                float influence = Blend(Mathf.Max(0, distance - 3.5f), 5);
                height = Mathf.Lerp(height, Mathf.Lerp(road.from.y, road.to.y, t), influence);
            }
            height = Mathf.Lerp(height, 2, Box(x, z, 18, 72, 4, 3, 10));
            height = Mathf.Lerp(height, 2, Box(x, z, 33, 72, 4, 3, 10));
            return Mathf.Clamp(height, 0, 31);
        }
        internal static float Dirt(float x, float z)
        {
            float weight = 0;
            foreach (var road in Roads) weight = Mathf.Max(weight, 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.3f, 4, RoadDistance(x, z, road, out _))));
            float bank = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.5f, 3, Mathf.Abs(Mathf.Abs(z - 124) - 6)));
            return Mathf.Max(weight, bank * .55f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(63, 70, Mathf.Abs(x)))));
        }
    }
}
