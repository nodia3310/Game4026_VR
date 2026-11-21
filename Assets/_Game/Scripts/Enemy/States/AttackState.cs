namespace Game4026.Enemy
{
    /// <summary>
    /// Plays the move picked in Chase (telegraph → swing → hit check → recovery; combos chain steps), then Chase.
    /// Being staggered or killed cancels the whole move.
    /// </summary>
    public sealed class AttackState : IEnemyState
    {
        private readonly IEnemyContext context;
        private bool running;

        public AttackState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Attack;

        public void Enter()
        {
            running = true;
            context.StopMoving();
            context.BeginAttack();
        }

        public void Tick(float deltaTime)
        {
            if (context.TickAttack(deltaTime))
                return;
            running = false;
            context.RequestState(EnemyStateId.Chase);
        }

        public void Exit()
        {
            if (running)
                context.CancelAttack();
            running = false;
        }
    }
}
