using UnityEngine;

namespace Riftbound
{
    public sealed class OriginalLocationProps : ScriptableObject
    {
        public GameObject crate, arch, rock;
        public Texture2D stoneTexture, woodTexture;

        static OriginalLocationProps current;
        public static OriginalLocationProps Current =>
            current ? current : current = Resources.Load<OriginalLocationProps>("OriginalLocationProps");

        // Dynamic quest sites use their authored collision and anchor positions. This swaps the visual shell.
        public static Transform Place(GameObject prefab, Transform parent, Vector3 center, Vector3 size, Material finish = null)
        {
            if (!prefab) return null;
            var anchor = new GameObject(prefab.name).transform;
            anchor.SetParent(parent, false);
            var visual = Instantiate(prefab, anchor, false);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var lod = visual.GetComponent<LODGroup>();
            var renderers = lod ? lod.GetLODs()[0].renderers : visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                if (Application.isPlaying) Destroy(anchor.gameObject);
                else DestroyImmediate(anchor.gameObject);
                return null;
            }

            var bounds = new Bounds();
            bool found = false;
            foreach (var renderer in renderers)
            {
                var local = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    var p = local.center + Vector3.Scale(local.extents, new Vector3(
                        (i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    p = anchor.InverseTransformPoint(renderer.transform.TransformPoint(p));
                    if (found) bounds.Encapsulate(p);
                    else { bounds = new Bounds(p, Vector3.zero); found = true; }
                }
            }

            if (finish)
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int index = 0; index < materials.Length; index++) materials[index] = finish;
                    renderer.sharedMaterials = materials;
                }
            visual.transform.localScale = new Vector3(size.x / Mathf.Max(.014f, bounds.size.x),
                size.y / Mathf.Max(.014f, bounds.size.y), size.z / Mathf.Max(.014f, bounds.size.z));
            visual.transform.localPosition = -Vector3.Scale(visual.transform.localScale, bounds.center);
            anchor.localPosition = center;
            return anchor;
        }
    }
}
