using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>Answers "is the player holding a melee weapon inside this zone?" (implemented by the bridges, which know A's weapons).</summary>
    public interface IParryProbe
    {
        /// <summary>
        /// Zone = <paramref name="shape"/>'s geometry (it may be disabled) grown by <paramref name="padding"/> metres.
        /// Returns true and the contact point when a held player melee weapon is inside.
        /// </summary>
        bool TryFind(Collider shape, float padding, out Vector3 point);
    }

    /// <summary>
    /// Forgiving parries for attacks that are not an <see cref="EnemyWeapon"/> (flying weapons):
    /// they ask <see cref="Current"/> each frame of their strike. Nothing happens while no probe is installed.
    /// </summary>
    public static class ParryProbe
    {
        public static IParryProbe Current { get; set; }

        public static bool TryFind(Collider shape, float padding, out Vector3 point)
        {
            point = default;
            return shape != null && Current != null && Current.TryFind(shape, padding, out point);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
