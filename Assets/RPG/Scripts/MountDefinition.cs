using UnityEngine;

namespace Riftbound
{
    [CreateAssetMenu(menuName = "Riftbound/Mount")]
    public class MountDefinition : ScriptableObject
    {
        public string mountId, displayName;
        public MountVisual prefab;
        public float runSpeed = 8, walkSpeed = 3;
        public bool requiresExploration;
        public bool isFlying;
        public float flySpeed = 15f;
    }
}
