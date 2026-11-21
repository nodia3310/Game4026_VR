using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Speed of an enemy's clock during a hit stop: drops to the slow speed at once, holds it for the first
    /// <see cref="HoldFraction"/> of the stop, then ramps linearly back to 1 so the enemy eases out of the slow motion
    /// instead of snapping back. Closed form, so the AI clock can be advanced exactly by any frame length.
    /// </summary>
    public static class HitStopCurve
    {
        public const float HoldFraction = 0.6f;

        /// <summary>Clock speed <paramref name="elapsed"/> real seconds into a stop of <paramref name="length"/>.</summary>
        public static float ScaleAt(float elapsed, float length, float slowScale)
        {
            if (length <= 0f || elapsed >= length)
                return 1f;
            float u = Mathf.Max(0f, elapsed) / length;
            if (u <= HoldFraction)
                return slowScale;
            return Mathf.Lerp(slowScale, 1f, (u - HoldFraction) / (1f - HoldFraction));
        }

        /// <summary>Enemy-clock seconds that pass between two real times of the stop.</summary>
        public static float LocalTime(float fromElapsed, float toElapsed, float length, float slowScale)
        {
            if (length <= 0f)
                return toElapsed - fromElapsed;
            return length * (Integral(toElapsed / length, slowScale) - Integral(fromElapsed / length, slowScale));
        }

        /// <summary>Real seconds a whole stop removes from the enemy clock.</summary>
        public static float TimeLost(float length, float slowScale) => length - LocalTime(0f, length, length, slowScale);

        // Integral of the scale over normalised time 0..u (beyond 1 the clock runs at 1).
        private static float Integral(float u, float s)
        {
            if (u <= 0f)
                return 0f;
            const float h = HoldFraction;
            if (u <= h)
                return s * u;
            float r = Mathf.Min(u, 1f) - h;
            float value = s * h + s * r + (1f - s) * r * r / (2f * (1f - h));
            if (u > 1f)
                value += u - 1f;
            return value;
        }
    }
}
