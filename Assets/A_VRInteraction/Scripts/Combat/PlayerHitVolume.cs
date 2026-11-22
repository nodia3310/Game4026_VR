using UnityEngine;

namespace Game4026.PlayerCombat
{
    internal readonly struct PlayerHitVolume
    {
        private readonly Vector3 head, chest, leftShoulder, rightShoulder, waist, hips;

        public Vector3 Chest => chest;

        public PlayerHitVolume(Vector3 eye, Quaternion look, Vector3 rigUp, Vector3 facing, Vector3 shoulderRight)
        {
            head = eye - rigUp * .035f - facing * .045f;
            chest = eye - rigUp * .25f - facing * .075f;
            float pitch = Vector3.Dot(look * Vector3.forward, rigUp);
            hips = chest - rigUp * .43f + facing * (.19f * pitch);
            waist = Vector3.Lerp(chest, hips, .53f);
            leftShoulder = chest - shoulderRight * .12f;
            rightShoulder = chest + shoulderRight * .12f;
        }

        public bool Overlaps(in AttackVolume attack) =>
            attack.Overlaps(head, .13f) ||
            attack.Overlaps(chest, .17f) ||
            attack.Overlaps(leftShoulder, .13f) ||
            attack.Overlaps(rightShoulder, .13f) ||
            attack.Overlaps(waist, .17f) ||
            attack.Overlaps(hips, .16f);

        public void Draw()
        {
            Gizmos.DrawWireSphere(head, .13f);
            Gizmos.DrawWireSphere(chest, .17f);
            Gizmos.DrawWireSphere(leftShoulder, .13f);
            Gizmos.DrawWireSphere(rightShoulder, .13f);
            Gizmos.DrawWireSphere(waist, .17f);
            Gizmos.DrawWireSphere(hips, .16f);
        }
    }

    /// <summary>A finite cylindrical sector, with the same horizontal range and angle as the enemy move.</summary>
    internal readonly struct AttackVolume
    {
        private readonly Vector3 origin;
        private readonly Vector2 forward, leftEdge, rightEdge;
        private readonly float range, halfAngleCos, minHeight, maxHeight;

        public AttackVolume(Transform enemy, float range, float angle, float minHeight, float maxHeight)
        {
            origin = enemy.position;
            Vector3 direction = enemy.forward;
            forward = new Vector2(direction.x, direction.z).normalized;
            if (forward == Vector2.zero) forward = Vector2.up;

            this.range = Mathf.Max(0f, range);
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
            float halfAngle = Mathf.Clamp(angle * .5f, 0f, 180f) * Mathf.Deg2Rad;
            halfAngleCos = Mathf.Cos(halfAngle);
            Vector2 side = new Vector2(forward.y, -forward.x) * Mathf.Sin(halfAngle);
            leftEdge = forward * halfAngleCos - side;
            rightEdge = forward * halfAngleCos + side;
        }

        public bool Overlaps(Vector3 sphereCenter, float radius)
        {
            Vector3 local = sphereCenter - origin;
            float vertical = Mathf.Max(0f, Mathf.Max(minHeight - local.y, local.y - maxHeight));
            if (vertical > radius) return false;

            var planar = new Vector2(local.x, local.z);
            float distance = planar.magnitude;
            float planarSqr;
            if (Vector2.Dot(planar, forward) >= distance * halfAngleCos)
            {
                float beyondRange = Mathf.Max(0f, distance - range);
                planarSqr = beyondRange * beyondRange;
            }
            else
            {
                planarSqr = Mathf.Min(DistanceToEdgeSquared(planar, leftEdge),
                    DistanceToEdgeSquared(planar, rightEdge));
            }
            return planarSqr + vertical * vertical <= radius * radius;
        }

        private float DistanceToEdgeSquared(Vector2 point, Vector2 edge)
        {
            float along = Mathf.Clamp(Vector2.Dot(point, edge), 0f, range);
            return (point - edge * along).sqrMagnitude;
        }
    }
}
