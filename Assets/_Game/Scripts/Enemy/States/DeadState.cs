namespace Game4026.Enemy
{
    /// <summary>Stops everything, waits despawnDuration (for the death effect), then despawns once.</summary>
    public sealed class DeadState : IEnemyState
    {
        private readonly IEnemyContext context;
        private float elapsed;
        private bool despawned;

        public DeadState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Dead;

        public void Enter()
        {
            elapsed = 0f;
            despawned = false;
            context.StopMoving();
            context.OnDeath();
        }

        public void Tick(float deltaTime)
        {
            if (despawned)
                return;

            elapsed += deltaTime;
            if (elapsed >= context.Data.DespawnDuration)
            {
                despawned = true;
                context.Despawn();
            }
        }

        public void Exit() { }
    }
}
