using System;
using Game4026.Enemy;
using Game4026.Interaction.Melee;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Game4026.Bridges
{
    /// <summary>
    /// Makes VR parries forgiving. Put next to an <see cref="EnemyWeapon"/>. While that attack's parry window is open,
    /// a zone a bit bigger than the weapon's collider is checked every physics step; a melee weapon the player is
    /// holding inside it parries — even held still (A's weapon only reports hits while swinging ≥ 1.2 m/s).
    /// The zone is a query, not a collider, so it never blocks the player's hits. Arrows and bows never count.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyWeapon))]
    public sealed class ParryAssist : MonoBehaviour
    {
        [Tooltip("Added to the weapon collider's radius / half size (m).")]
        [SerializeField, Min(0f)] private float radiusPadding = 0.13f;
        [Tooltip("Added to both ends of a capsule weapon (m), so the zone reaches past the blade tip.")]
        [SerializeField, Min(0f)] private float lengthPadding = 0.15f;
        [Tooltip("Dual wielders: only the hand swinging this step assists (left hand for mirrored moves).")]
        [SerializeField] private bool onlySwingingHand = true;
        [SerializeField] private LayerMask playerWeaponMask = ~0;

        private readonly Collider[] overlaps = new Collider[16];
        private EnemyWeapon weapon;
        private Collider[] shapes = new Collider[0];
        private bool dualWield;

        /// <summary>Which colliders count as the player's held melee weapon. Replaceable for tests.</summary>
        public Predicate<Collider> IsHeldPlayerWeapon { get; set; } = IsHeldMeleeWeapon;

        public float RadiusPadding { get => radiusPadding; set => radiusPadding = Mathf.Max(0f, value); }
        public float LengthPadding { get => lengthPadding; set => lengthPadding = Mathf.Max(0f, value); }

        private void Awake() => Cache();

        private void Cache()
        {
            if (weapon != null)
                return;
            weapon = GetComponent<EnemyWeapon>();
            shapes = FindShapes(weapon);
            dualWield = false;
            EnemyAttack attack = weapon.Attack;
            if (attack == null)
                return;
            foreach (EnemyWeapon other in attack.GetComponentsInChildren<EnemyWeapon>(true)) // once
                if (other.IsLeftHand != weapon.IsLeftHand)
                    dualWield = true;
        }

        private void FixedUpdate()
        {
            if (weapon != null && weapon.Attack != null)
                TryAssist(weapon.Attack.Now);
        }

        /// <summary>Checks the zone once; parries when a held player weapon is inside. Returns true on a parry.</summary>
        public bool TryAssist(float now)
        {
            Cache();
            EnemyAttack attack = weapon.Attack;
            if (attack == null || !attack.IsParryWindowOpen)
                return false;
            if (onlySwingingHand && dualWield && attack.CurrentStep != null && attack.CurrentStep.Mirror != weapon.IsLeftHand)
                return false;

            for (int s = 0; s < shapes.Length; s++)
            {
                Collider shape = shapes[s];
                if (shape == null || !shape.enabled || !shape.gameObject.activeInHierarchy)
                    continue;
                if (!ParryZone.Find(shape, radiusPadding, lengthPadding, overlaps, playerWeaponMask.value,
                        IsHeldPlayerWeapon, attack.transform, out Collider other, out Vector3 point))
                    continue;
                int before = HitImpacts.Count;
                if (!weapon.TryDeflect(point, now))
                    return false;
                if (HitImpacts.Count != before) // reaction raised by the deflect (EnemyHitFeedback)
                    other.GetComponentInParent<MeleeDamageBridge>()?.Rumble(HitImpacts.Last);
                return true;
            }
            return false;
        }

        /// <summary>The weapon's own collider, or (wings on a bone) the child colliders that belong to it and not to a weak point or another weapon.</summary>
        private static Collider[] FindShapes(EnemyWeapon weapon)
        {
            var own = weapon.GetComponent<Collider>();
            if (own != null)
                return new[] { own };
            var found = new System.Collections.Generic.List<Collider>();
            foreach (Collider c in weapon.GetComponentsInChildren<Collider>(true))
                if (ReferenceEquals(c.GetComponentInParent<Game4026.Contracts.IDamageable>(true), weapon))
                    found.Add(c);
            return found.ToArray();
        }

        /// <summary>Default filter: a collider of A's melee weapon that a hand is holding right now.</summary>
        public static bool IsHeldMeleeWeapon(Collider collider)
        {
            MeleeWeapon melee = collider.GetComponentInParent<MeleeWeapon>(); // only for colliders inside the zone
            if (melee == null)
                return false;
            XRGrabInteractable grab = melee.GetComponent<XRGrabInteractable>();
            return grab != null && grab.isSelected;
        }

        private void OnDrawGizmosSelected()
        {
            var owner = GetComponent<EnemyWeapon>();
            if (owner == null)
                return;
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
            foreach (Collider c in FindShapes(owner)) // editor only
                DrawZone(c);
        }

        private void DrawZone(Collider c) => ParryZone.DrawGizmo(c, radiusPadding, lengthPadding);
    }
}
