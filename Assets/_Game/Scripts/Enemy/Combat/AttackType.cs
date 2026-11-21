namespace Game4026.Enemy
{
    /// <summary>How an <see cref="AttackDefinition"/> is executed (spec 4.5). Values are serialized; only append.</summary>
    public enum AttackType
    {
        Melee = 0,
        Lunge = 1,
        Combo = 2,
        Projectile = 3,
        AreaSlam = 4,
        Charge = 5,     // M4 ChargeBehavior, not implemented yet (never selected)
    }

    /// <summary>Procedural body motion of a statue enemy for one attack step (see StatueLocomotion).</summary>
    public enum StatueMotion
    {
        Auto = 0,
        Hop = 1,   // tilt back, then hop forward (headbutt)
        Roll = 2,  // spin up, then roll toward the player
        Slam = 3,  // crouch, jump up, slam down
        Shoot = 4, // lean back, recoil when firing
    }
}
