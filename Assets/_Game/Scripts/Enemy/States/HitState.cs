namespace Game4026.Enemy
{
    /// <summary>Stagger: stops for the context's stagger duration (staggerDuration, longer after a parry), then Chase. Re-entering restarts the timer.</summary>
    public sealed class HitState : IEnemyState
    {
        private readonly IEnemyContext context;
        private float elapsed;

        public HitState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Hit;

        public void Enter()
        {
            elapsed = 0f;
            context.StopMoving();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed >= context.CurrentStaggerDuration)
                context.RequestState(EnemyStateId.Chase);
        }

        public void Exit() { }
    }
}
