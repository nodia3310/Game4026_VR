namespace Game4026.Enemy
{
    /// <summary>Transition conditions shared by several states (spec 4.4 table).</summary>
    public static class EnemyStateRules
    {
        public static bool ShouldStartChase(IEnemyContext c) =>
            c.HasTarget && c.CanSeeTarget && c.DistanceToTarget <= c.Data.DetectionRange;

        public static bool ShouldLoseTarget(IEnemyContext c) =>
            !c.HasTarget || c.DistanceToTarget > c.Data.LoseTargetRange || c.TimeSinceTargetSeen > c.LoseSightSeconds;

        public static bool CanStartAttack(IEnemyContext c) =>
            c.HasTarget && c.DistanceToTarget <= c.Data.AttackRange && c.IsAttackReady;

        public static EnemyStateId CalmState(IEnemyContext c) =>
            c.HasPatrolPoints ? EnemyStateId.Patrol : EnemyStateId.Idle;
    }
}
