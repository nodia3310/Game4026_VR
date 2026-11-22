using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Interaction
{
    [DisallowMultipleComponent]
    public sealed class PlayerHealth : MonoBehaviour, IPlayerDamageReceiver
    {
        [SerializeField, Min(1f)] private float maxHealth = 100f;

        public float MaxHealth => maxHealth;
        public float CurrentHealth { get; private set; }

        public event Action<float> Damaged;
        public event Action HealthReset;

        private void Awake() => ResetHealth();

        public void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            HealthReset?.Invoke();
        }

        public void ApplyPlayerDamage(float damage)
        {
            if (!(damage > 0f) || float.IsInfinity(damage)) return;
            float previousHealth = CurrentHealth;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (CurrentHealth < previousHealth)
                Damaged?.Invoke(previousHealth - CurrentHealth);
        }
    }
}
