using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Engine-free pendulum scythe motion (the corridor the barrier opens onto).</summary>
    public static class PendulumMath
    {
        /// <summary>Swing angle in degrees at <paramref name="time"/>; <paramref name="phase01"/> offsets pendulums from each other.</summary>
        public static float Angle(float time, float period, float amplitude, float phase01)
        {
            if (period <= 0f)
                return 0f;
            return amplitude * Mathf.Sin((time / period + phase01) * 2f * Mathf.PI);
        }

        /// <summary>
        /// Blade centre of a pendulum hanging from <paramref name="pivot"/>, swinging sideways along <paramref name="swingDirection"/>
        /// (horizontal), <paramref name="angle"/> degrees from straight down.
        /// </summary>
        public static Vector3 Blade(Vector3 pivot, Vector3 swingDirection, float armLength, float angle)
        {
            swingDirection.y = 0f;
            swingDirection = swingDirection.sqrMagnitude > 1e-6f ? swingDirection.normalized : Vector3.right;
            float a = angle * Mathf.Deg2Rad;
            return pivot + (Vector3.down * Mathf.Cos(a) + swingDirection * Mathf.Sin(a)) * armLength;
        }
    }

    /// <summary>Engine-free layout and timing of the "thousand weapons" barrage.</summary>
    public static class BarragePlan
    {
        /// <summary>
        /// Slot <paramref name="index"/> of <paramref name="count"/>: a fan around <paramref name="center"/> (the stand), spread
        /// sideways over ±<paramref name="halfAngle"/>°, a little toward the player, alternating low / high so they do not overlap.
        /// </summary>
        public static Vector3 Slot(int index, int count, Vector3 center, Vector3 towardPlayer, float radius, float halfAngle,
            float lowHeight, float highHeight)
        {
            towardPlayer.y = 0f;
            towardPlayer = towardPlayer.sqrMagnitude > 1e-6f ? towardPlayer.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, towardPlayer);
            float t = count > 1 ? index / (float)(count - 1) : 0.5f;
            float a = Mathf.Lerp(-halfAngle, halfAngle, t) * Mathf.Deg2Rad;
            float height = index % 2 == 0 ? lowHeight : highHeight;
            return center + right * (Mathf.Sin(a) * radius) + towardPlayer * (Mathf.Cos(a) * radius * 0.35f) + Vector3.up * height;
        }

        /// <summary>
        /// Which slot fires as the <paramref name="launch"/>-th weapon: each wave of <paramref name="perWave"/> takes slots spread
        /// over the whole fan (left, middle, right), so every wave comes from several directions.
        /// </summary>
        public static int SlotForLaunch(int launch, int count, int perWave)
        {
            if (count <= 0)
                return 0;
            perWave = Mathf.Clamp(perWave, 1, count);
            int waves = Mathf.CeilToInt(count / (float)perWave);
            int wave = launch / perWave;
            int inWave = launch % perWave;
            int slot = wave + inWave * waves;
            return slot < count ? slot : Mathf.Min(launch, count - 1);
        }

        public static int WaveCount(int count, int perWave) => Mathf.CeilToInt(count / (float)Mathf.Max(1, perWave));
    }
}
