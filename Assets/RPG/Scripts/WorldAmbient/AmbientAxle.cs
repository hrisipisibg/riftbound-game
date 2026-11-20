using UnityEngine;

namespace Riftbound
{
    [DisallowMultipleComponent]
    public sealed class AmbientAxle : MonoBehaviour
    {
        [SerializeField] float degreesPerSecond = 28f;

        void Update()
        {
            transform.Rotate(Vector3.right, Time.deltaTime * degreesPerSecond, Space.Self);
        }
    }
}
