namespace Game4026.Enemy
{
    /// <summary>Stands still until the player is seen within detection range.</summary>
    public sealed class IdleState : IEnemyState
    {
        private readonly IEnemyContext context;

        public IdleState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Idle;

        public void Enter() => context.StopMoving();

        public void Tick(float deltaTime)
        {
            if (EnemyStateRules.ShouldStartChase(context))
                context.RequestState(EnemyStateId.Chase);
        }

        public void Exit() { }
    }
}
