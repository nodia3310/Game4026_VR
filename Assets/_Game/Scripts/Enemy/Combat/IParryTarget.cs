using UnityEngine;

namespace Game4026.Enemy
{
    /// <summary>
    /// Something the player's melee weapon can deflect (an enemy blade or body, a projectile).
    /// Arrows must not deflect: they call <see cref="TakeUnparryableHit"/> instead of TakeDamage.
    /// </summary>
    public interface IParryTarget
    {
        /// <summary>A hit that can never parry or reflect (arrows). Returns true when it did anything (e.g. passed damage to the body).</summary>
        bool TakeUnparryableHit(float damage, Vector3 hitPoint);
    }
}
