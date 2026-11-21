namespace Game4026.Enemy
{
    /// <summary>
    /// Runs at chaseSpeed toward the player. Attacks when in range, off cooldown and an attack slot is free; without a
    /// slot it waits around WaitDistance and circles. Gives up beyond loseTargetRange or after LoseSightSeconds without
    /// line of sight.
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

            if (context.HasTarget && context.TrySelectAttack())
            {
                context.RequestState(EnemyStateId.Attack);
                return;
            }

            if (context.MustWaitForTurn && context.DistanceToTarget <= context.WaitDistance + 0.75f)
            {
                // Someone else's turn: keep a respectful distance and circle into the player's view.
                context.CircleTarget(deltaTime);
                return;
            }

            if (context.DistanceToTarget <= context.AttackHoldRange)
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
