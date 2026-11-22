using System;
using Game4026.Contracts;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// The stand's giant hand. Only while it is stuck on the floor after a slam (<see cref="Vulnerable"/>) its colliders
    /// are on, and a weapon hit on it is passed to the boss's <see cref="EnemyHealth"/> (×damageMultiplier).
    /// The colliders are off the rest of the time so the flying hand never shoves the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PharaohHand : MonoBehaviour, IDamageable
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(0f)] private float damageMultiplier = 1.5f;
        [SerializeField] private Color idleColor = new Color(1f, 0.75f, 0.3f, 1f);
        [Tooltip("Colour while stuck on the floor and open to hits.")]
        [SerializeField] private Color vulnerableColor = new Color(1f, 0.95f, 0.7f, 1f);
        [SerializeField, Min(0f)] private float idleIntensity = 0.6f;
        [SerializeField, Min(0f)] private float vulnerableIntensity = 1.3f;

        private EnemyHealth health;
        private EnemyHitFeedback feedback;
        private Collider[] colliders = new Collider[0];
        private Renderer[] renderers = new Renderer[0];
        private MaterialPropertyBlock block;

        public bool Vulnerable { get; private set; }
        public float DamageMultiplier => damageMultiplier;

        /// <summary>(hand, damage passed to the boss)</summary>
        public event Action<PharaohHand, float> Struck;

        private void Awake()
        {
            health = GetComponentInParent<EnemyHealth>();
            feedback = GetComponentInParent<EnemyHitFeedback>();
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            SetVulnerable(false);
        }

        public void SetVulnerable(bool value)
        {
            Vulnerable = value;
            if (value)
                IgnorePlayerBody();
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = value;
            ApplyColor(value ? vulnerableColor * vulnerableIntensity : idleColor * idleIntensity);
        }

        public void TakeDamage(float damage, Vector3 hitPoint, bool isWeakPoint)
        {
            if (!(damage > 0f))
                return;
            if (!Vulnerable || health == null)
            {
                if (feedback != null)
                    feedback.Play(HitKind.Blocked, hitPoint, 0f);
                return;
            }
            float passed = damage * damageMultiplier;
            health.TakeDamage(passed, hitPoint, false);
            Struck?.Invoke(this, passed);
        }

        /// <summary>The stuck hand often lands where the player stands: never push or lift the player's body.</summary>
        private void IgnorePlayerBody()
        {
            PlayerTarget player = PlayerTarget.Current;
            if (player == null)
                return;
            CharacterController body = player.GetComponentInParent<CharacterController>();
            if (body == null)
                body = player.GetComponentInChildren<CharacterController>();
            if (body == null)
                return;
            for (int i = 0; i < colliders.Length; i++)
                Physics.IgnoreCollision(colliders[i], body, true);
        }

        private void ApplyColor(Color color)
        {
            if (renderers.Length == 0)
                return;
            if (block == null)
                block = new MaterialPropertyBlock();
            color.a = 1f;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColorId, color);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
