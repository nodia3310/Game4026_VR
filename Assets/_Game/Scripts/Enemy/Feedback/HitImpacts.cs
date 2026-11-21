using System;
using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>What kind of contact produced a hit reaction.</summary>
    public enum HitKind
    {
        Normal = 0,
        WeakPoint,
        Stagger,
        Kill,
        /// <summary>The player's weapon deflected an attack inside its parry window.</summary>
        Parry,
        /// <summary>The player's weapon hit an enemy blade outside a parry window.</summary>
        Blocked,
        /// <summary>The player's weapon reflected an enemy bolt.</summary>
        Reflect,
        /// <summary>A heavy hit broke an enemy's guard (wing shield).</summary>
        GuardBreak,
    }

    /// <summary>One played hit reaction: what the player's side (haptics, camera, UI) should feel.</summary>
    public readonly struct HitImpact
    {
        public readonly HitKind Kind;
        public readonly Vector3 Point;
        public readonly float HitStop;
        /// <summary>Controller rumble 0..1.</summary>
        public readonly float HapticAmplitude;
        public readonly float HapticDuration;

        public HitImpact(HitKind kind, Vector3 point, float hitStop, float hapticAmplitude, float hapticDuration)
        {
            Kind = kind;
            Point = point;
            HitStop = hitStop;
            HapticAmplitude = hapticAmplitude;
            HapticDuration = hapticDuration;
        }
    }

    /// <summary>
    /// Global feed of hit reactions, raised synchronously inside the damage call. The weapon bridge compares
    /// <see cref="Count"/> before and after it applies a hit to know what that hit felt like (no allocation).
    /// </summary>
    public static class HitImpacts
    {
        public static event Action<HitImpact> Played;
        /// <summary>Increments for every reaction.</summary>
        public static int Count { get; private set; }
        public static HitImpact Last { get; private set; }

        internal static void Raise(in HitImpact impact)
        {
            Last = impact;
            Count++;
            Played?.Invoke(impact);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Played = null;
            Count = 0;
            Last = default;
        }
    }
}
