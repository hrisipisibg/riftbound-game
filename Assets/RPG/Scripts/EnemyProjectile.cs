using UnityEngine;

namespace Riftbound
{
    public class EnemyProjectile : MonoBehaviour
    {
        PlayerController target;
        Transform remoteTarget;
        ulong remoteClientId;
        string sourceEnemyKey;
        bool authoritativeRemote;
        bool visualOnly;
        Vector3 direction;
        float damage, expires;
        Material material;

        public static void Launch(Vector3 origin, PlayerController player, float damage)
        {
            if (!player) return;
            var bolt = CreateBoltObject(origin, player.transform.position + Vector3.up - origin, damage);
            bolt.target = player;
        }

        public static void LaunchForRemotePlayer(Vector3 origin, ulong targetClientId, Transform remoteTransform, float damage, string enemyKey)
        {
            if (!remoteTransform) return;
            Vector3 aim = remoteTransform.position + Vector3.up - origin;
            var bolt = CreateBoltObject(origin, aim, damage);
            bolt.remoteTarget = remoteTransform;
            bolt.remoteClientId = targetClientId;
            bolt.sourceEnemyKey = enemyKey;
            bolt.authoritativeRemote = true;
            if (Network.RiftboundCoopPlayer.LocalOwner != null && Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsServer)
                Network.RiftboundCoopPlayer.LocalOwner.BroadcastVisualEnemyProjectile(origin, aim.normalized);
        }

        public static void LaunchVisualOnly(Vector3 origin, Vector3 normalizedDirection)
        {
            var bolt = CreateBoltObject(origin, normalizedDirection, 0f);
            bolt.visualOnly = true;
        }

        static EnemyProjectile CreateBoltObject(Vector3 origin, Vector3 aim, float dmg)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Ashwood shadow bolt";
            go.GetComponent<Collider>().enabled = false;
            Destroy(go.GetComponent<Collider>());

            var bolt = go.AddComponent<EnemyProjectile>();
            bolt.damage = dmg;
            bolt.direction = aim.sqrMagnitude > 0.001f ? aim.normalized : Vector3.forward;
            go.transform.position = origin + bolt.direction * .8f;
            go.transform.localScale = Vector3.one * .3f;
            bolt.expires = Time.time + 3;
            bolt.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            bolt.material.SetColor("_BaseColor", new Color(.8f, .25f, 1));
            go.GetComponent<Renderer>().sharedMaterial = bolt.material;

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.24f;
            trail.startWidth = 0.22f;
            trail.endWidth = 0.02f;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(0.85f, 0.3f, 1f, 0.9f);
            trail.endColor = new Color(0.4f, 0.08f, 0.65f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.75f, 0.25f, 1f);
            light.range = 4f;
            light.intensity = 1.8f;
            return bolt;
        }

        void Update()
        {
            if (Time.time >= expires) { Destroy(gameObject); return; }
            if (!visualOnly)
            {
                if (authoritativeRemote)
                {
                    if (!remoteTarget) { Destroy(gameObject); return; }
                }
                else if (!target || !target.Stats.Alive)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            Vector3 from = transform.position, step = direction * (7 * Time.deltaTime);
            Vector3 to = from + step;
            if (step.sqrMagnitude > 0)
            {
                bool wall = Physics.SphereCast(from, .12f, direction, out var hit, step.magnitude, ~(1 << 2), QueryTriggerInteraction.Ignore);
                if (wall && (hit.collider.GetComponentInParent<PlayerController>() != null || hit.collider.GetComponentInParent<Network.RiftboundCoopPlayer>() != null))
                    wall = false;

                bool targetHit = false;
                float hitDist = float.MaxValue;
                if (!visualOnly && authoritativeRemote)
                {
                    Vector3 center = remoteTarget.position + Vector3.up * 0.95f;
                    float t = Mathf.Clamp01(Vector3.Dot(center - from, step) / step.sqrMagnitude);
                    Vector3 closest = from + step * t;
                    float horizDist = Vector2.Distance(new Vector2(closest.x, closest.z), new Vector2(center.x, center.z));
                    float vertDist = Mathf.Abs(closest.y - center.y);
                    targetHit = horizDist <= 0.55f && vertDist <= 1.05f;
                    hitDist = Vector3.Distance(from, closest);
                }
                else if (!visualOnly && target)
                {
                    var cc = target.GetComponent<CharacterController>();
                    if (cc)
                    {
                        var bounds = cc.bounds;
                        Vector3 center = bounds.center;
                        float t = Mathf.Clamp01(Vector3.Dot(center - from, step) / step.sqrMagnitude);
                        targetHit = bounds.SqrDistance(from + step * t) < .12f * .12f;
                        hitDist = Vector3.Distance(from, from + step * t);
                    }
                }

                if (targetHit && (!wall || hit.distance >= hitDist))
                {
                    if (authoritativeRemote)
                    {
                        Network.RiftboundCoopPlayer.DispatchEnemyHitToPlayer(remoteClientId, damage, sourceEnemyKey);
                    }
                    else if (target)
                    {
                        target.Stats.Damage(damage);
                        if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null && Unity.Netcode.NetworkManager.Singleton && Unity.Netcode.NetworkManager.Singleton.IsServer)
                            Network.RiftboundCoopPlayer.LocalOwner.Health.Value = Mathf.Max(0, Mathf.RoundToInt(target.Stats.Health));
                        OrbitCamera.Shake(0.09f, 0.11f);
                    }
                    SpellFX.SpawnBurst(transform.position, new Color(0.85f, 0.3f, 1f), new Color(0.35f, 0.08f, 0.6f), 14, 2f, 4.5f, 0.09f, 0.18f, 0.32f);
                    Destroy(gameObject);
                    return;
                }
                if (wall)
                {
                    SpellFX.SpawnBurst(transform.position, new Color(0.8f, 0.3f, 1f), new Color(0.3f, 0.1f, 0.5f), 8, 1.5f, 3.5f, 0.07f, 0.14f, 0.25f);
                    Destroy(gameObject);
                    return;
                }
            }
            transform.position = to;
        }

        void OnDestroy() { if (material) Destroy(material); }
    }
}
