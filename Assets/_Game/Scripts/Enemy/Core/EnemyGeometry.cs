using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Ground-plane (XZ) distance and angle helpers. Height is ignored so a standing VR player's body
    /// height does not push them out of an enemy's attack range.
    /// </summary>
    public static class EnemyGeometry
    {
        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>True when target is within range and inside a cone of angleDegrees (full width) around forward.</summary>
        public static bool IsInAttackCone(Vector3 origin, Vector3 forward, Vector3 target, float range, float angleDegrees)
        {
            Vector3 toTarget = target - origin;
            toTarget.y = 0f;
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance > range * range)
                return false;
            if (sqrDistance < 1e-6f)
                return true; // standing on top of each other counts as a hit

            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                return false;

            return Vector3.Angle(forward, toTarget) <= angleDegrees * 0.5f;
        }
    }
}
