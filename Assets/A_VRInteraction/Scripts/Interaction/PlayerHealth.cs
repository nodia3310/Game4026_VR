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

        private void Awake() => CurrentHealth = MaxHealth;

        public void ApplyPlayerDamage(float damage)
        {
            if (!(damage > 0f) || float.IsInfinity(damage)) return;
            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
        }
    }
}
