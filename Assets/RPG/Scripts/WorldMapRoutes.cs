using System;
using UnityEngine;

namespace Riftbound
{
    public class WorldMapRoutes : MonoBehaviour
    {
        [Serializable] public class Route
        {
            public string name;
            public Vector3[] points;
        }
        public Route[] routes;
    }
}
