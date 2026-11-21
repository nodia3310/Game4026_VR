using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Enemy damage receiver (spec 4.2). A's weapons reach it through
    /// <c>GetComponentInParent&lt;IDamageable&gt;()</c> on the enemy's body collider.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] private EnemyData data;

        private EnemyHealthModel model;

        /// <summary>Raised for every accepted hit.</summary>
        public event Action<EnemyHealth, EnemyDamageResult> Damaged;
        /// <summary>Raised exactly once per life; <see cref="ResetState"/> starts a new life.</summary>
        public event Action<EnemyHealth> Died;

        public EnemyData Data => data;
        public float MaxHealth => Model.MaxHealth;
        public float CurrentHealth => Model.CurrentHealth;
        public bool IsDead => Model.IsDead;

        /// <summary>Set by the spawn state: while true, all damage is ignored.</summary>
        public bool IsInvulnerable
        {
            get => Model.IsInvulnerable;
            set => Model.IsInvulnerable = value;
        }

        /// <summary>Runtime multiplier on all damage (parry counter window). Reset by <see cref="ResetState"/>.</summary>
        public float DamageMultiplier
        {
            get => Model.DamageMultiplier;
            set => Model.DamageMultiplier = value;
        }

        private EnemyHealthModel Model
        {
            get
            {
                if (model == null)
                    model = CreateModel();
                return model;
            }
        }

        private void Awake()
        {
            if (model == null)
                model = CreateModel();
        }

        /// <summary>Assigns data at runtime (spawner / tests) and resets to full health.</summary>
        public void Initialize(EnemyData enemyData)
        {
            data = enemyData;
            model = CreateModel();
        }

        /// <summary>Called by A's weapons and by <see cref="WeakPoint"/>.</summary>
        public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)
        {
            ApplyDamage(damage, hitPoint, isWeakPoint, Time.time);
        }

        /// <summary>Same as <see cref="TakeDamage"/> with an explicit timestamp (used by tests).</summary>
        public bool ApplyDamage(float damage, Vector3 hitPoint, bool isWeakPoint, float time)
        {
            if (!Model.TryApplyDamage(damage, hitPoint, isWeakPoint, time, out EnemyDamageResult result))
                return false;

            Damaged?.Invoke(this, result);
            if (result.Killed)
                Died?.Invoke(this);
            return true;
        }

        /// <summary>Restores full health and clears death state; call when an enemy is returned to or taken from the pool.</summary>
        public void ResetState()
        {
            Model.Reset();
        }

        private EnemyHealthModel CreateModel()
        {
            if (data != null)
                return new EnemyHealthModel(data);

            Debug.LogWarning($"{nameof(EnemyHealth)} on '{name}' has no {nameof(EnemyData)}; using default values.", this);
            var defaults = ScriptableObject.CreateInstance<EnemyData>();
            var fallback = new EnemyHealthModel(defaults);
            if (Application.isPlaying)
                Destroy(defaults);
            else
                DestroyImmediate(defaults);
            return fallback;
        }
    }
}
