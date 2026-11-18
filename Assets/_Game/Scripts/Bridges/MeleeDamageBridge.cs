using Game4026.Interaction.Melee;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Put next to A's <see cref="MeleeWeapon"/>. Turns each <see cref="MeleeWeapon.Hit"/> into damage on the
    /// hit collider's <c>IDamageable</c>. A's weapon already filters minimum speed and one hit per target per swing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeDamageBridge : MonoBehaviour
    {
        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private ImpactDamageCurve damage = ImpactDamageCurve.MeleeDefault;

        public ImpactDamageCurve Damage { get => damage; set => damage = value; }

        private void Awake()
        {
            if (weapon == null)
                weapon = GetComponent<MeleeWeapon>();
            if (weapon == null)
                Debug.LogWarning($"{nameof(MeleeDamageBridge)} on '{name}' has no {nameof(MeleeWeapon)}.", this);
        }

        private void OnEnable()
        {
            if (weapon != null)
                weapon.Hit += HandleHit;
        }

        private void OnDisable()
        {
            if (weapon != null)
                weapon.Hit -= HandleHit;
        }

        private void HandleHit(MeleeHit hit) => ApplyHit(hit);

        /// <summary>Applies one melee hit. Public so tests can feed a hand-built <see cref="MeleeHit"/>.</summary>
        public bool ApplyHit(MeleeHit hit) =>
            DamageDispatch.TryApply(hit.Collider, damage.Evaluate(hit.Speed), hit.Point);
    }
}
