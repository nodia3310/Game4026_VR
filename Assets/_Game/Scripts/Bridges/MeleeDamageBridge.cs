using Game4026.Enemy;
using Game4026.Interaction.Melee;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace Game4026.Bridges
{
    /// <summary>
    /// Put next to A's <see cref="MeleeWeapon"/>. Turns each <see cref="MeleeWeapon.Hit"/> into damage on the
    /// hit collider's <c>IDamageable</c>. A's weapon already filters minimum speed and one hit per target per swing.
    /// Also rumbles the controller holding the weapon with the strength of the hit reaction it caused
    /// (<see cref="HitImpacts"/>: parry and kills hit hardest).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MeleeDamageBridge : MonoBehaviour
    {
        [SerializeField] private MeleeWeapon weapon;
        [SerializeField] private ImpactDamageCurve damage = ImpactDamageCurve.MeleeDefault;
        [Tooltip("Multiplies every rumble from this weapon (0 = no haptics).")]
        [SerializeField, Range(0f, 1f)] private float hapticScale = 1f;

        private XRGrabInteractable grab;

        public ImpactDamageCurve Damage { get => damage; set => damage = value; }
        /// <summary>Rumble of the last applied hit (0 when it caused no reaction). For tests and debugging.</summary>
        public float LastHapticAmplitude { get; private set; }

        private void Awake()
        {
            if (weapon == null)
                weapon = GetComponent<MeleeWeapon>();
            if (weapon == null)
                Debug.LogWarning($"{nameof(MeleeDamageBridge)} on '{name}' has no {nameof(MeleeWeapon)}.", this);
            else
                grab = weapon.GetComponent<XRGrabInteractable>();
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
        public bool ApplyHit(MeleeHit hit)
        {
            int before = HitImpacts.Count;
            bool applied = DamageDispatch.TryApply(hit.Collider, damage.Evaluate(hit.Speed), hit.Point);
            LastHapticAmplitude = 0f;
            if (HitImpacts.Count != before) // reactions are raised inside the damage call
            {
                HitImpact impact = HitImpacts.Last;
                LastHapticAmplitude = impact.HapticAmplitude * hapticScale;
                Rumble(LastHapticAmplitude, impact.HapticDuration);
            }
            return applied;
        }

        /// <summary>Rumbles the holding controller for a reaction raised outside <see cref="ApplyHit"/> (e.g. <see cref="ParryAssist"/>).</summary>
        public void Rumble(HitImpact impact)
        {
            LastHapticAmplitude = impact.HapticAmplitude * hapticScale;
            Rumble(LastHapticAmplitude, impact.HapticDuration);
        }

        private void Rumble(float amplitude, float duration)
        {
            if (grab == null || amplitude <= 0f || duration <= 0f)
                return;
            var holders = grab.interactorsSelecting; // live list, no allocation
            for (int i = 0; i < holders.Count; i++)
                if (holders[i] is XRBaseInputInteractor hand)
                    hand.SendHapticImpulse(amplitude, duration);
        }
    }
}
