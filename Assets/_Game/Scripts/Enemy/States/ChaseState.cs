namespace Game4026.Enemy
{
    /// <summary>
    /// Runs at chaseSpeed toward the player. Attacks when in range and off cooldown; gives up beyond
    /// loseTargetRange or after LoseSightSeconds without line of sight.
    /// </summary>
    public sealed class ChaseState : IEnemyState
    {
        private readonly IEnemyContext context;

        public ChaseState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Chase;

        public void Enter() => context.SetMoveSpeed(context.Data.ChaseSpeed);

        public void Tick(float deltaTime)
        {
            if (EnemyStateRules.ShouldLoseTarget(context))
            {
                context.RequestState(EnemyStateRules.CalmState(context));
                return;
            }

            if (EnemyStateRules.CanStartAttack(context))
            {
                context.RequestState(EnemyStateId.Attack);
                return;
            }

            if (context.DistanceToTarget <= context.Data.AttackRange)
            {
                // In range but on cooldown: hold position and keep facing the player.
                context.StopMoving();
                context.FaceTarget(deltaTime);
                return;
            }

            context.ChaseTarget();
        }

        public void Exit() { }
    }
}
