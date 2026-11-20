using UnityEngine;

namespace Riftbound
{
    public class FlowingWater : MonoBehaviour
    {
        MaterialPropertyBlock properties;
        Renderer surface;
        void OnEnable() { properties = new MaterialPropertyBlock(); surface = GetComponent<Renderer>(); }
        void Update()
        {
            if (!surface || properties == null) return;
            surface.GetPropertyBlock(properties);
            properties.SetVector("_BaseMap_ST", new Vector4(1, 1, Time.time * .025f, Time.time * .012f));
            surface.SetPropertyBlock(properties);
        }
    }
}
