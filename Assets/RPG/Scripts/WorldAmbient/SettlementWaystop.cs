using System.Collections.Generic;
using UnityEngine;

namespace Riftbound
{
    public sealed class SettlementWaystop : MonoBehaviour
    {
        static readonly List<SettlementWaystop> active = new List<SettlementWaystop>();
        public static IReadOnlyList<SettlementWaystop> Active => active;
        public string displayName;
        [TextArea] public string notice;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);
    }
}
