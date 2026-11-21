namespace Game4026.Enemy
{
    /// <summary>How an <see cref="AttackDefinition"/> is executed (spec 4.5). Values are serialized; only append.</summary>
    public enum AttackType
    {
        Melee = 0,
        Lunge = 1,
        Combo = 2,
        Projectile = 3, // M4, not implemented yet (never selected)
        AreaSlam = 4,
        Charge = 5,     // M4 ChargeBehavior, not implemented yet (never selected)
    }
}
