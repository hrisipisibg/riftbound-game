using System;
using UnityEngine;

namespace Riftbound
{
    public class Combatant : MonoBehaviour
    {
        public string displayName = "Forest raider";
        public float maxHealth = 100;
        public bool isPlayer;
        public float flatDamageReduction;
        [System.NonSerialized] public float incomingDamageMultiplier = 1;
        [System.NonSerialized] public float invulnerableUntil;
        public float Health { get; private set; }
        public bool Alive => Health > 0;
        public event Action<Combatant> Died;
        public event Action<float> Damaged, Healed;
        public float LastHit { get; private set; } = -10;
        float lastImmuneTextAt = -10f;

        void Awake() { Health = maxHealth; gameObject.AddComponent<CombatFeedback>(); }
        public void Damage(float amount)
        {
            if (!Alive || amount <= 0) return;
            if (Time.time < invulnerableUntil)
            {
                CombatTextManager.Spawn(transform.position + Vector3.up * 1.9f, "DODGED!", new Color(0.45f, 0.92f, 1f), true);
                return;
            }
            if (incomingDamageMultiplier <= 0f)
            {
                if (Time.time >= lastImmuneTextAt + 0.6f)
                {
                    lastImmuneTextAt = Time.time;
                    CombatTextManager.Spawn(transform.position + Vector3.up * 1.9f, "IMMUNE", new Color(0.85f, 0.85f, 0.92f), false);
                }
                return;
            }
            amount = Mathf.Max(1, (amount - flatDamageReduction) * Mathf.Clamp01(incomingDamageMultiplier));
            ApplyHealthReduction(amount);
        }

        public void ApplyAuthoritativeNetHit(float netDamage, bool dodged)
        {
            if (!Alive) return;
            if (dodged)
            {
                CombatTextManager.Spawn(transform.position + Vector3.up * 1.9f, "DODGED!", new Color(0.45f, 0.92f, 1f), true);
                return;
            }
            if (netDamage <= 0f) return;
            ApplyHealthReduction(netDamage);
        }

        void ApplyHealthReduction(float amount)
        {
            float previous = Health;
            Health = Mathf.Max(0, Health - amount);
            LastHit = Time.time;
            if (isPlayer && previous > Health) OrbitCamera.Shake(0.11f, 0.1f);
            Damaged?.Invoke(previous - Health);
            if (previous > 0f && !Alive) Died?.Invoke(this);
        }
        public void Heal(float amount)
        {
            if (!Alive || amount <= 0) return;
            float previous = Health; Health = Mathf.Min(maxHealth, Health + amount);
            float actualHeal = Health - previous;
            if (actualHeal > 0)
            {
                CombatTextManager.SpawnHeal(transform.position, actualHeal);
                Healed?.Invoke(actualHeal);
            }
        }
        public void Restore() { Health = maxHealth; }
        public void ConfigureVitals(float maximum, float reduction)
        {
            maxHealth = Mathf.Max(1, maximum); flatDamageReduction = Mathf.Max(0, reduction);
            Health = Mathf.Min(Health, maxHealth);
        }
        public void SetReplicatedHealth(float hp, float maxHp)
        {
            bool wasAlive = Alive;
            maxHealth = Mathf.Max(1f, maxHp);
            Health = Mathf.Clamp(hp, 0f, maxHealth);
            if (wasAlive && !Alive) Died?.Invoke(this);
        }
    }
}
