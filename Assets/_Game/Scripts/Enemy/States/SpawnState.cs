namespace Game4026.Enemy
{
    /// <summary>Invulnerable and inactive for spawnDuration, then Patrol (with patrol points) or Idle.</summary>
    public sealed class SpawnState : IEnemyState
    {
        private readonly IEnemyContext context;
        private float elapsed;

        public SpawnState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Spawn;

        public void Enter()
        {
            elapsed = 0f;
            context.SetInvulnerable(true);
            context.StopMoving();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            if (elapsed >= context.Data.SpawnDuration)
                context.RequestState(EnemyStateRules.CalmState(context));
        }

        public void Exit() => context.SetInvulnerable(false);
    }
}
