using System;
using UnityEngine;

namespace Riftbound
{
    public class Mailbox : MonoBehaviour
    {
        public string mailboxName = "Capital City Mailbox";

        public static Mailbox GetNearby(Vector3 pos, float radius = 3.2f)
        {
            Mailbox nearest = null;
            float minDst = radius;
            foreach (var mb in FindObjectsByType<Mailbox>(FindObjectsSortMode.None))
            {
                if (!mb.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(pos, mb.transform.position);
                if (d < minDst)
                {
                    minDst = d;
                    nearest = mb;
                }
            }
            return nearest;
        }
    }
}
