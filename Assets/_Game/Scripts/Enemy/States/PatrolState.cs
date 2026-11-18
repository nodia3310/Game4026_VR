namespace Game4026.Enemy
{
    /// <summary>Walks between patrol points at moveSpeed until the player is seen within detection range.</summary>
    public sealed class PatrolState : IEnemyState
    {
        private readonly IEnemyContext context;

        public PatrolState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Patrol;

        public void Enter()
        {
            context.SetMoveSpeed(context.Data.MoveSpeed);
            context.MoveToNextPatrolPoint();
        }

        public void Tick(float deltaTime)
        {
            if (EnemyStateRules.ShouldStartChase(context))
            {
                context.RequestState(EnemyStateId.Chase);
                return;
            }

            if (context.ReachedDestination)
                context.MoveToNextPatrolPoint();
        }

        public void Exit() { }
    }
}
