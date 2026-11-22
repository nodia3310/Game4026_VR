using System;
using Game4026.Contracts;
using Game4026.Enemy;
using UnityEngine;

namespace Game4026.Bridges
{
    /// <summary>
    /// Installs itself as <see cref="ParryProbe.Current"/>: flying weapons (<see cref="EnemyProjectile"/>) ask it whether the
    /// player holds a melee weapon in their (grown) shape. <see cref="DamageBridgeInstaller"/> adds it at start.
    /// The holding controller rumbles with the reaction the parry then raises.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeldWeaponParryProbe : MonoBehaviour, IParryProbe
    {
        [SerializeField] private LayerMask playerWeaponMask = ~0;
        [Tooltip("The weapon must be at least this much closer to the incoming weapon than the player's body is: " +
                 "it has to be in the way, not just held at the side.")]
        [SerializeField] private float frontMargin = 0.1f;

        private readonly Collider[] overlaps = new Collider[16];
        private MeleeDamageBridge pendingRumble;
        private int pendingFrame = -1;
        private Predicate<Collider> accept;
        private Vector3 zoneCenter;

        /// <summary>Which colliders count as the player's held melee weapon. Replaceable for tests.</summary>
        public Predicate<Collider> IsHeldPlayerWeapon { get; set; } = ParryAssist.IsHeldMeleeWeapon;

        private void OnEnable()
        {
            ParryProbe.Current = this;
            HitImpacts.Played += HandleImpact;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ParryProbe.Current, this))
                ParryProbe.Current = null;
            HitImpacts.Played -= HandleImpact;
            pendingRumble = null;
        }

        public float FrontMargin { get => frontMargin; set => frontMargin = value; }

        public bool TryFind(Collider shape, float padding, out Vector3 point)
        {
            point = default;
            if (shape == null)
                return false;
            zoneCenter = ParryZone.Center(shape);
            if (accept == null)
                accept = Accept; // cached delegate, no per-frame allocation
            // Only held player weapons pass the filter, so no enemy root needs excluding.
            if (!ParryZone.Find(shape, padding, padding, overlaps, playerWeaponMask.value, accept,
                    null, out Collider found, out point))
                return false;
            pendingRumble = found.GetComponentInParent<MeleeDamageBridge>(); // once per parry
            pendingFrame = Time.frameCount;
            return true;
        }

        private bool Accept(Collider c)
        {
            if (IsHeldPlayerWeapon == null || !IsHeldPlayerWeapon(c))
                return false;
            PlayerTarget player = PlayerTarget.Current;
            if (player == null)
                return true;
            float weapon = Vector3.Distance(c.ClosestPoint(zoneCenter), zoneCenter);
            float body = DistanceToSegment(zoneCenter, player.Body.position, player.Head.position);
            return weapon + frontMargin <= body;
        }

        public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len = ab.sqrMagnitude;
            float t = len > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }

        /// <summary>The parry's reaction (raised right after a successful query, same frame) rumbles the weapon's hand.</summary>
        private void HandleImpact(HitImpact impact)
        {
            if (pendingRumble == null)
                return;
            if (pendingFrame == Time.frameCount)
                pendingRumble.Rumble(impact);
            pendingRumble = null;
        }
    }
}
