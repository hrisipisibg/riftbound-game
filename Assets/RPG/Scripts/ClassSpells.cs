using System.Collections;
using UnityEngine;

namespace Riftbound
{
    public class FireballSpell : MonoBehaviour
    {
        Combatant target;
        PlayerController caster;
        float damage;
        Vector3 targetLastPos;
        float speed = 22f;
        float life = 3.5f;
        ParticleSystem trailParticles;

        public static void Cast(PlayerController player, Combatant enemy, float dmg)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fireball";
            var col = go.GetComponent<Collider>();
            if (col) Destroy(col);

            go.transform.position = player.transform.position + Vector3.up * 1.4f + player.transform.forward * 0.8f;
            go.transform.localScale = Vector3.one * 0.42f;

            var r = go.GetComponent<Renderer>();
            var mat = SpellFX.CreateUnlitMat(new Color(1f, 0.55f, 0.1f, 1f));
            r.sharedMaterial = mat;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.12f);
            light.range = 6f;
            light.intensity = 3.2f;

            var spell = go.AddComponent<FireballSpell>();
            spell.caster = player;
            spell.target = enemy;
            spell.damage = dmg;
            spell.targetLastPos = enemy ? enemy.transform.position + Vector3.up : go.transform.position + player.transform.forward * 20f;
            spell.trailParticles = SpellFX.AttachFireTrail(go.transform);
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0) { DetachTrail(); Destroy(gameObject); return; }

            Vector3 dest = (target && target.Alive) ? target.transform.position + Vector3.up * 1.2f : targetLastPos;
            Vector3 diff = dest - transform.position;
            float step = speed * Time.deltaTime;

            transform.Rotate(Vector3.one * (360f * Time.deltaTime));

            if (diff.magnitude <= step + 0.3f)
            {
                Impact();
                return;
            }

            transform.position += diff.normalized * step;
        }

        void DetachTrail()
        {
            if (!trailParticles) return;
            trailParticles.transform.SetParent(null, true);
            trailParticles.Stop(false, ParticleSystemStopBehavior.StopEmitting);
            Destroy(trailParticles.gameObject, 1.2f);
            trailParticles = null;
        }

        void Impact()
        {
            if (target && target.Alive)
            {
                if (Network.CoopSessionConfig.IsMultiplayer && Network.RiftboundCoopPlayer.LocalOwner != null)
                {
                    Network.RiftboundCoopPlayer.LocalOwner.RequestAbilityAttack(target, "fireball");
                }
                else
                {
                    target.Damage(damage);
                    AudioEffects.PlayBladeHit();
                    SpellFX.CreateHitSparks(target.transform.position + Vector3.up * 1.1f, false);
                    CombatTextManager.SpawnDamage(target.transform.position, damage, false);
                }
            }
            DetachTrail();
            OrbitCamera.Shake(0.18f, 0.2f);
            SpawnExplosion(transform.position);
            Destroy(gameObject);
        }

        void SpawnExplosion(Vector3 pos)
        {
            SpellFX.SpawnBurst(pos, new Color(1f, 0.55f, 0.1f), new Color(1f, 0.15f, 0.02f), 28, 3.5f, 7.5f, 0.25f, 0.45f, 0.45f);
            SpellFX.CreateExpandingRing(pos, 0.4f, 2.8f, 0.28f, new Color(1f, 0.65f, 0.15f, 0.95f), 0.14f);

            var burst = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            burst.name = "FireballBurst";
            var c = burst.GetComponent<Collider>();
            if (c) Destroy(c);
            burst.transform.position = pos;
            burst.transform.localScale = Vector3.one * 1.1f;

            var r = burst.GetComponent<Renderer>();
            r.sharedMaterial = SpellFX.CreateUnlitMat(new Color(1f, 0.78f, 0.25f, 0.85f));
            var fx = burst.AddComponent<ScaleFadeFX>();
            fx.Init(1.1f, 2.2f, 0.18f);
        }
    }

    public class ScaleFadeFX : MonoBehaviour
    {
        float startScale, endScale, duration, elapsed;
        public void Init(float start, float end, float dur)
        {
            startScale = start;
            endScale = end;
            duration = Mathf.Max(0.05f, dur);
            Destroy(gameObject, duration + 0.02f);
        }
        void Update()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, t);
        }
    }

    public class ExpandingRingFX : MonoBehaviour
    {
        LineRenderer lr;
        float startRadius, endRadius, duration, elapsed, baseWidth;
        Color baseColor;
        int segments = 36;

        public void Init(float startR, float endR, float dur, Color color, float width)
        {
            startRadius = startR;
            endRadius = endR;
            duration = Mathf.Max(0.05f, dur);
            baseColor = color;
            baseWidth = width;

            lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = color;
            lr.endColor = color;
            lr.startWidth = width;
            lr.endWidth = width;
            UpdateRing(0f);
            Destroy(gameObject, duration + 0.02f);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            UpdateRing(Mathf.Clamp01(elapsed / duration));
        }

        void UpdateRing(float t)
        {
            float eased = 1f - (1f - t) * (1f - t);
            float r = Mathf.Lerp(startRadius, endRadius, eased);
            for (int i = 0; i < segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
            var c = baseColor;
            c.a *= (1f - t);
            lr.startColor = c;
            lr.endColor = c;
            float w = baseWidth * (1f - t * 0.65f);
            lr.startWidth = w;
            lr.endWidth = w;
        }
    }

    public class CleaveArcFX : MonoBehaviour
    {
        LineRenderer lr;
        float radius, duration, elapsed;
        Color baseColor;
        int points = 18;

        public void Init(float r, float dur, Color color)
        {
            radius = r;
            duration = Mathf.Max(0.05f, dur);
            baseColor = color;

            lr = gameObject.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = points;
            lr.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            lr.startWidth = 0.28f;
            lr.endWidth = 0.08f;
            UpdateArc(0f);
            Destroy(gameObject, duration + 0.02f);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            UpdateArc(Mathf.Clamp01(elapsed / duration));
        }

        void UpdateArc(float t)
        {
            float r = Mathf.Lerp(radius * 0.45f, radius, t);
            float sweep = Mathf.Lerp(-120f, 120f, t);
            for (int i = 0; i < points; i++)
            {
                float frac = i / (float)(points - 1);
                float deg = sweep - 85f + frac * 170f;
                float rad = deg * Mathf.Deg2Rad;
                lr.SetPosition(i, new Vector3(Mathf.Sin(rad) * r, 0.95f + Mathf.Sin(frac * Mathf.PI) * 0.25f, Mathf.Cos(rad) * r));
            }
            var c = baseColor;
            c.a *= (1f - t);
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g, c.b, c.a * 0.2f);
        }
    }

    public static class SpellFX
    {
        static Material cachedParticleMat;

        public static Material CreateUnlitMat(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            return mat;
        }

        static Material GetParticleMaterial()
        {
            if (cachedParticleMat) return cachedParticleMat;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                      ?? Shader.Find("Particles/Standard Unlit")
                      ?? Shader.Find("Sprites/Default")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");
            cachedParticleMat = new Material(shader);
            cachedParticleMat.SetColor("_BaseColor", Color.white);
            cachedParticleMat.SetColor("_Color", Color.white);
            return cachedParticleMat;
        }

        public static ParticleSystem AttachFireTrail(Transform parent)
        {
            var go = new GameObject("FireTrail");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 2f;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.3f, 0.05f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;

            var emission = ps.emission;
            emission.rateOverTime = 45f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.14f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = GetParticleMaterial();
            return ps;
        }

        public static void SpawnBurst(Vector3 pos, Color colA, Color colB, int count, float minSpeed, float maxSpeed, float minSize, float maxSize, float lifetime)
        {
            var go = new GameObject("VFXBurst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.65f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = new ParticleSystem.MinMaxGradient(colA, colB);
            main.gravityModifier = 0.35f;
            main.maxParticles = count + 8;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var pr = go.GetComponent<ParticleSystemRenderer>();
            pr.sharedMaterial = GetParticleMaterial();

            ps.Play();
            Object.Destroy(go, lifetime + 0.4f);
        }

        public static void CreateExpandingRing(Vector3 center, float startR, float endR, float duration, Color color, float width = 0.18f)
        {
            var go = new GameObject("ExpandingRingFX");
            go.transform.position = center + Vector3.up * 0.08f;
            go.AddComponent<ExpandingRingFX>().Init(startR, endR, duration, color, width);
        }

        public static void CreateFrostNova(Vector3 center, float radius)
        {
            OrbitCamera.Shake(0.14f, 0.16f);
            CreateExpandingRing(center, 0.4f, radius, 0.45f, new Color(0.35f, 0.9f, 1f, 0.95f), 0.24f);
            CreateExpandingRing(center + Vector3.up * 0.18f, 0.3f, radius * 0.78f, 0.38f, new Color(0.75f, 0.96f, 1f, 0.8f), 0.12f);
            SpawnBurst(center + Vector3.up * 0.4f, new Color(0.55f, 0.95f, 1f), new Color(0.2f, 0.65f, 1f), 32, 3f, 7f, 0.15f, 0.35f, 0.55f);
        }

        public static void CreateConsecration(Vector3 center, float radius)
        {
            CreateExpandingRing(center, 0.3f, radius, 0.55f, new Color(1f, 0.88f, 0.3f, 0.95f), 0.22f);
            SpawnBurst(center + Vector3.up * 0.35f, new Color(1f, 0.92f, 0.45f), new Color(1f, 0.7f, 0.15f), 26, 1.5f, 4.5f, 0.14f, 0.3f, 0.75f);

            var lightGo = new GameObject("HolyFlash");
            lightGo.transform.position = center + Vector3.up * 1.1f;
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.86f, 0.38f);
            light.range = radius + 3f;
            light.intensity = 3.5f;
            Object.Destroy(lightGo, 0.45f);
        }

        public static void CreateHitSparks(Vector3 pos, bool isCrit)
        {
            if (isCrit)
            {
                SpawnBurst(pos, new Color(1f, 0.92f, 0.35f), new Color(1f, 0.32f, 0.1f), 20, 3.2f, 6.8f, 0.12f, 0.26f, 0.36f);
                CreateExpandingRing(pos - Vector3.up * 0.6f, 0.2f, 1.8f, 0.22f, new Color(1f, 0.78f, 0.2f, 0.85f), 0.12f);
            }
            else
            {
                SpawnBurst(pos, new Color(1f, 0.95f, 0.75f), new Color(1f, 0.65f, 0.25f), 10, 2f, 4.5f, 0.08f, 0.18f, 0.25f);
            }
        }

        public static void CreateCleaveArc(Vector3 center, float radius, string characterClass)
        {
            Color col = characterClass switch
            {
                "Rogue" => new Color(0.4f, 1f, 0.65f, 0.95f),
                "Paladin" => new Color(1f, 0.88f, 0.3f, 0.95f),
                _ => new Color(1f, 0.38f, 0.22f, 0.95f)
            };
            var go = new GameObject("CleaveArcFX");
            go.transform.position = center;
            go.AddComponent<CleaveArcFX>().Init(radius, 0.24f, col);
        }

        public static void CreateArcaneBurst(Vector3 pos)
        {
            CreateExpandingRing(pos, 0.3f, 3.2f, 0.35f, new Color(0.65f, 0.45f, 1f, 0.95f), 0.2f);
            SpawnBurst(pos + Vector3.up * 0.8f, new Color(0.75f, 0.6f, 1f), new Color(0.35f, 0.85f, 1f), 22, 2.5f, 5.5f, 0.12f, 0.28f, 0.45f);
        }

        public static void CreateLevelUpBurst(Vector3 pos)
        {
            OrbitCamera.Shake(0.15f, 0.25f);
            CreateExpandingRing(pos, 0.4f, 4.8f, 0.75f, new Color(1f, 0.9f, 0.35f, 1f), 0.26f);
            SpawnBurst(pos + Vector3.up * 0.5f, new Color(1f, 0.96f, 0.55f), new Color(1f, 0.75f, 0.15f), 42, 2.5f, 7f, 0.15f, 0.35f, 0.95f);

            var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.name = "LevelUpPillar";
            var col = pillar.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            pillar.transform.position = pos + Vector3.up * 3.5f;
            pillar.transform.localScale = new Vector3(1.1f, 3.5f, 1.1f);
            pillar.GetComponent<Renderer>().sharedMaterial = CreateUnlitMat(new Color(1f, 0.92f, 0.45f, 0.45f));

            var light = pillar.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.9f, 0.4f);
            light.range = 8f;
            light.intensity = 4f;

            Object.Destroy(pillar, 0.85f);
        }

        public static void CreateSlashArc(Vector3 pos, Vector3 forward, string characterClass)
        {
            Color col = characterClass switch
            {
                "Rogue" => new Color(0.45f, 1f, 0.7f, 0.9f),
                "Paladin" => new Color(1f, 0.88f, 0.35f, 0.9f),
                "Mage" => new Color(0.45f, 0.8f, 1f, 0.9f),
                _ => new Color(1f, 0.55f, 0.28f, 0.9f)
            };
            var go = new GameObject("SlashArcFX");
            go.transform.position = pos;
            if (forward.sqrMagnitude > 0.001f)
                go.transform.rotation = Quaternion.LookRotation(forward) * Quaternion.Euler(0f, 0f, Random.Range(-28f, 28f));
            go.AddComponent<CleaveArcFX>().Init(2.15f, 0.16f, col);
        }

        public static void CreateHeavySlam(Vector3 center, float radius)
        {
            OrbitCamera.Shake(0.24f, 0.25f);
            CreateExpandingRing(center, 0.4f, radius, 0.35f, new Color(1f, 0.25f, 0.1f, 0.95f), 0.28f);
            CreateExpandingRing(center + Vector3.up * 0.15f, 0.2f, radius * 0.65f, 0.28f, new Color(1f, 0.72f, 0.2f, 0.85f), 0.16f);
            SpawnBurst(center + Vector3.up * 0.35f, new Color(1f, 0.45f, 0.12f), new Color(0.9f, 0.15f, 0.05f), 30, 3f, 7.5f, 0.15f, 0.36f, 0.5f);
        }

        public static void CreateSoulOrb(Vector3 startPos, Transform target)
        {
            if (!target) return;
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            orb.name = "SoulOrbFX";
            var col = orb.GetComponent<Collider>();
            if (col) Object.Destroy(col);
            orb.transform.position = startPos;
            orb.transform.localScale = Vector3.one * 0.24f;
            orb.GetComponent<Renderer>().sharedMaterial = CreateUnlitMat(new Color(1f, 0.86f, 0.32f, 1f));

            var trail = orb.AddComponent<TrailRenderer>();
            trail.time = 0.28f;
            trail.startWidth = 0.16f;
            trail.endWidth = 0.02f;
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = new Color(1f, 0.85f, 0.3f, 0.9f);
            trail.endColor = new Color(0.45f, 0.85f, 1f, 0f);

            orb.AddComponent<SoulOrbFX>().Init(startPos, target);
        }
    }

    public class SoulOrbFX : MonoBehaviour
    {
        Transform target;
        Vector3 startPos;
        float elapsed;
        float duration = 0.65f;

        public void Init(Vector3 start, Transform followTarget)
        {
            startPos = start;
            target = followTarget;
        }

        void Update()
        {
            if (!target) { Destroy(gameObject); return; }
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float ease = t * t;
            Vector3 dest = target.position + Vector3.up * 1.15f;
            Vector3 pos = Vector3.Lerp(startPos, dest, ease);
            pos.y += Mathf.Sin(t * Mathf.PI) * 1.4f;
            transform.position = pos;

            if (t >= 1f)
            {
                SpellFX.SpawnBurst(dest, new Color(1f, 0.92f, 0.45f), new Color(0.55f, 0.9f, 1f), 10, 1.5f, 3.5f, 0.08f, 0.16f, 0.3f);
                Destroy(gameObject);
            }
        }
    }
}
