using UnityEngine;

namespace Riftbound
{
    public class FlowingWater : MonoBehaviour
    {
        MaterialPropertyBlock properties;
        Renderer surface;
        void Awake() { properties = new MaterialPropertyBlock(); surface = GetComponent<Renderer>(); }
        void Update()
        {
            surface.GetPropertyBlock(properties);
            properties.SetVector("_BaseMap_ST", new Vector4(1, 1, Time.time * .025f, Time.time * .012f));
            surface.SetPropertyBlock(properties);
        }
    }
}
