#if UNITY_EDITOR
using System;
using UnityEngine;

namespace Riftbound.Editor
{
    public sealed class PlayerMovementFixtureDriver : MonoBehaviour
    {
        public static Action Step;
        void Update() => Step?.Invoke();
    }
}
#endif
