using System;
using UnityEngine;

namespace Game4026.Interaction.Melee
{
    /// <summary>Pure calculations. Speeds are m/s, mass kg, energy J and estimated force N.</summary>
    public static class MeleeMotion
    {
        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        public static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);

        // Reconstruct the previous point in the CURRENT rig pose before measuring hand motion.
        // Rig translation and rotation therefore contribute no weapon speed.
        public static Vector3 RebasePoint(Vector3 previousPoint, Vector3 previousRigPosition,
            Quaternion previousRigRotation, Vector3 currentRigPosition, Quaternion currentRigRotation)
        {
            return currentRigPosition + currentRigRotation *
                (Quaternion.Inverse(previousRigRotation) * (previousPoint - previousRigPosition));
        }

        public static float KineticEnergy(float mass, float speed)
        {
            if (!IsFinite(mass) || !IsFinite(speed) || mass < 0f || speed < 0f)
                throw new ArgumentOutOfRangeException(nameof(mass));
            return 0.5f * mass * speed * speed;
        }

        // Assumes a complete stop over the configured contact interval; not a force sensor.
        public static float EstimatedStoppingForce(float mass, float speed, float contactSeconds)
        {
            if (!IsFinite(contactSeconds) || contactSeconds <= 0f)
                throw new ArgumentOutOfRangeException(nameof(contactSeconds));
            KineticEnergy(mass, speed); // Validate the common inputs.
            return mass * speed / contactSeconds;
        }
    }
}
