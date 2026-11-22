using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Moves of the pharaoh's spectral stand (the giant hand).</summary>
    public enum StandMove { Slam, Sweep, ComboSlam }

    /// <summary>
    /// Engine-free move choice per boss phase: phase 0 slam / sweep, later phases add the combo slam.
    /// Never two sweeps in a row (they are the hardest to read in VR).
    /// </summary>
    public static class StandMoveSelector
    {
        // Weights per phase: Slam, Sweep, ComboSlam.
        private static readonly float[][] Weights =
        {
            new[] { 0.6f, 0.4f, 0f },
            new[] { 0.35f, 0.3f, 0.35f },
            new[] { 0.25f, 0.3f, 0.45f },
        };

        public static StandMove Pick(int phase, float random01, StandMove? last)
        {
            float[] w = Weights[Mathf.Clamp(phase, 0, Weights.Length - 1)];
            float slam = w[0], sweep = last == StandMove.Sweep ? 0f : w[1], combo = w[2];
            float total = slam + sweep + combo;
            float r = Mathf.Clamp01(random01) * total;
            if (r < slam || total <= 0f)
                return StandMove.Slam;
            if (r < slam + sweep)
                return StandMove.Sweep;
            return StandMove.ComboSlam;
        }
    }

    /// <summary>Engine-free hit tests for the stand's moves (horizontal plane, floor height from the boss).</summary>
    public static class StandGeometry
    {
        /// <summary>The palm lands at <paramref name="center"/>: hits when the player's body is inside the radius.</summary>
        public static bool SlamHits(Vector3 center, float radius, Vector3 playerBody) =>
            EnemyGeometry.HorizontalDistance(center, playerBody) <= radius;

        /// <summary>
        /// The sweeping hand is at <paramref name="hand"/>: hits when the player is within reach of it
        /// and is not crouched under it (head above <paramref name="crouchHeight"/> over the floor).
        /// </summary>
        public static bool SweepHits(Vector3 hand, float reach, Vector3 playerBody, float headHeightAboveFloor, float crouchHeight) =>
            headHeightAboveFloor > crouchHeight && EnemyGeometry.HorizontalDistance(hand, playerBody) <= reach;

        /// <summary>
        /// Sweep lane through <paramref name="target"/>, perpendicular to the boss→target direction:
        /// the hand travels from <paramref name="start"/> to <paramref name="end"/>, <paramref name="halfLength"/> each side.
        /// </summary>
        public static void SweepLane(Vector3 boss, Vector3 target, float halfLength, bool fromLeft, out Vector3 start, out Vector3 end)
        {
            Vector3 forward = target - boss;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (fromLeft ? -1f : 1f);
            start = target + side * halfLength;
            end = target - side * halfLength;
        }
    }
}
