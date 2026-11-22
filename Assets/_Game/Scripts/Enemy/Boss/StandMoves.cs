using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Moves of the pharaoh's spectral stand (the giant hand).</summary>
    public enum StandMove { Slam, Sweep, ComboSlam, HeadSlash, WeaponThrow, Barrage, Clap }

    /// <summary>
    /// Engine-free move choice per boss phase: phase 0 slam / sweep / head slash / weapon throw, later phases add the combo slam.
    /// Never the same side attack (sweep or head slash) twice in a row (they are the hardest to read in VR).
    /// </summary>
    public static class StandMoveSelector
    {
        // Weights per phase, in this order: Slam, Sweep, HeadSlash, WeaponThrow, Clap, ComboSlam.
        private static readonly float[][] Weights =
        {
            new[] { 0.3f, 0.15f, 0.15f, 0.2f, 0.2f, 0f },
            new[] { 0.2f, 0.12f, 0.12f, 0.16f, 0.15f, 0.25f },
            new[] { 0.15f, 0.12f, 0.12f, 0.16f, 0.15f, 0.3f },
        };

        public static StandMove Pick(int phase, float random01, StandMove? last)
        {
            float[] w = Weights[Mathf.Clamp(phase, 0, Weights.Length - 1)];
            float slam = w[0];
            float sweep = last == StandMove.Sweep ? 0f : w[1];
            float head = last == StandMove.HeadSlash ? 0f : w[2];
            float throwing = w[3];
            float clap = last == StandMove.Clap ? 0f : w[4];
            float combo = w[5];
            float total = slam + sweep + head + throwing + clap + combo;
            float r = Mathf.Clamp01(random01) * total;
            if (r < slam || total <= 0f)
                return StandMove.Slam;
            if (r < slam + sweep)
                return StandMove.Sweep;
            if (r < slam + sweep + head)
                return StandMove.HeadSlash;
            if (r < slam + sweep + head + throwing)
                return StandMove.WeaponThrow;
            if (r < slam + sweep + head + throwing + clap)
                return StandMove.Clap;
            return StandMove.ComboSlam;
        }

        /// <summary>Weapons thrown in one WeaponThrow move: 1 in phase 0, then 2, then 3.</summary>
        public static int ThrowCount(int phase) => Mathf.Clamp(phase + 1, 1, 3);
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
        /// Head-height slash: the hand (moving along the lane <paramref name="laneDirection"/>) hits a standing player whose body is
        /// within <paramref name="alongHalf"/> of it along the lane and <paramref name="acrossHalf"/> across it.
        /// The lane is too wide to step out of: crouching under <paramref name="crouchHeight"/> is the way out.
        /// </summary>
        public static bool LaneHits(Vector3 hand, Vector3 laneDirection, Vector3 playerBody, float alongHalf, float acrossHalf,
            float headHeightAboveFloor, float crouchHeight)
        {
            if (headHeightAboveFloor <= crouchHeight)
                return false;
            Vector3 d = playerBody - hand;
            d.y = 0f;
            laneDirection.y = 0f;
            if (laneDirection.sqrMagnitude < 1e-6f)
                return d.magnitude <= Mathf.Max(alongHalf, acrossHalf);
            laneDirection.Normalize();
            float along = Vector3.Dot(d, laneDirection);
            float across = (d - laneDirection * along).magnitude;
            return Mathf.Abs(along) <= alongHalf && across <= acrossHalf;
        }

        /// <summary>
        /// Clap: both hands meet at <paramref name="target"/>, closing sideways across the boss→player line
        /// (<paramref name="towardBoss"/>). Hits when the player is within <paramref name="depthHalf"/> of the target along that
        /// line and between the hands (<paramref name="widthHalf"/>): one step back or forward gets out.
        /// </summary>
        public static bool ClapHits(Vector3 target, Vector3 towardBoss, Vector3 playerBody, float depthHalf, float widthHalf)
        {
            Vector3 d = playerBody - target;
            d.y = 0f;
            towardBoss.y = 0f;
            if (towardBoss.sqrMagnitude < 1e-6f)
                return d.magnitude <= depthHalf;
            towardBoss.Normalize();
            float along = Vector3.Dot(d, towardBoss);
            float across = (d - towardBoss * along).magnitude;
            return Mathf.Abs(along) <= depthHalf && across <= widthHalf;
        }

        /// <summary>
        /// Sweep lane through <paramref name="target"/>, perpendicular to the boss→target direction, turned
        /// <paramref name="angleTowardBoss"/> degrees so the start is in front of the player (between them and the boss)
        /// and the hand is seen coming. The hand travels from <paramref name="start"/> to <paramref name="end"/>.
        /// </summary>
        public static void SweepLane(Vector3 boss, Vector3 target, float halfLength, bool fromLeft, out Vector3 start, out Vector3 end,
            float angleTowardBoss = 0f)
        {
            Vector3 forward = target - boss;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f)
                forward = Vector3.forward;
            forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, forward) * (fromLeft ? -1f : 1f);
            float a = Mathf.Clamp(angleTowardBoss, 0f, 85f) * Mathf.Deg2Rad;
            Vector3 startDir = side * Mathf.Cos(a) - forward * Mathf.Sin(a);
            start = target + startDir * halfLength;
            end = target - startDir * halfLength;
        }
    }
}
