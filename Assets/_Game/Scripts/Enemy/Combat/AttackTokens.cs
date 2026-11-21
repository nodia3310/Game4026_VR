using System.Collections.Generic;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Attack slots for fighting a group: only <see cref="MaxTokens"/> worth of enemies may be attacking at once, and
    /// only enemies in front of the player (within <see cref="ViewHalfAngle"/> of where the head looks) may start one.
    /// The others wait at a distance and circle (see ChaseState), so a 1-vs-many fight reads as one attacker at a time
    /// and nothing hits the player from behind. Elites cost more slots. One player, so the ledger is global.
    /// Settings come from an <see cref="AttackCoordinator"/> in the scene, or these defaults. No allocation per frame.
    /// </summary>
    public static class AttackTokens
    {
        public const int DefaultMaxTokens = 2;
        public const float DefaultViewHalfAngle = 70f;

        private static readonly List<Object> Holders = new List<Object>(16);
        private static readonly List<int> Costs = new List<int>(16);

        /// <summary>Off = every enemy attacks whenever it can (the old behaviour).</summary>
        public static bool Enabled { get; set; } = true;
        public static int MaxTokens { get; set; } = DefaultMaxTokens;
        /// <summary>Half the horizontal view cone (degrees) in which enemies may start an attack. 180 = anywhere.</summary>
        public static float ViewHalfAngle { get; set; } = DefaultViewHalfAngle;
        /// <summary>Slots taken right now.</summary>
        public static int Used { get; private set; }
        public static int HolderCount => Holders.Count;

        public static bool IsHolder(Object owner) => Holders.Contains(owner);

        /// <summary>
        /// True when <paramref name="owner"/> holds a slot, or enough slots are free for <paramref name="cost"/>.
        /// A cost above the maximum is allowed when nobody else is attacking (an elite alone).
        /// </summary>
        public static bool HasRoom(Object owner, int cost)
        {
            if (!Enabled || IsHolder(owner))
                return true;
            cost = Mathf.Max(1, cost);
            return Used == 0 || Used + cost <= MaxTokens;
        }

        /// <summary>Takes slots if there is room (holding already counts). False = wait for a turn.</summary>
        public static bool TryTake(Object owner, int cost)
        {
            if (!Enabled || IsHolder(owner))
                return true;
            if (!HasRoom(owner, cost))
                return false;
            cost = Mathf.Max(1, cost);
            Holders.Add(owner);
            Costs.Add(cost);
            Used += cost;
            return true;
        }

        public static void Release(Object owner)
        {
            int i = Holders.IndexOf(owner);
            if (i < 0)
                return;
            Used -= Costs[i];
            Holders.RemoveAt(i);
            Costs.RemoveAt(i);
        }

        /// <summary>True when the enemy is inside the player's horizontal view cone (always true without a head direction).</summary>
        public static bool InView(Vector3 enemy, Vector3 head, Vector3 headForward, float halfAngle)
        {
            if (halfAngle >= 180f)
                return true;
            headForward.y = 0f;
            Vector3 to = enemy - head;
            to.y = 0f;
            if (headForward.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-4f)
                return true;
            return Vector3.Angle(headForward, to) <= halfAngle;
        }

        /// <summary>Signed horizontal angle from the head's forward to the enemy (+ = to the right).</summary>
        public static float AngleFromView(Vector3 enemy, Vector3 head, Vector3 headForward)
        {
            headForward.y = 0f;
            Vector3 to = enemy - head;
            to.y = 0f;
            if (headForward.sqrMagnitude < 1e-6f || to.sqrMagnitude < 1e-4f)
                return 0f;
            return Vector3.SignedAngle(headForward, to, Vector3.up);
        }

        /// <summary>Restores the defaults and frees every slot (tests, scene loads).</summary>
        public static void Reset()
        {
            Holders.Clear();
            Costs.Clear();
            Used = 0;
            Enabled = true;
            MaxTokens = DefaultMaxTokens;
            ViewHalfAngle = DefaultViewHalfAngle;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
