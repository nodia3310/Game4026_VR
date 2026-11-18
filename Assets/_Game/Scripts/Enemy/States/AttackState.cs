namespace Game4026.Enemy
{
    /// <summary>
    /// Windup (faces the player, can be dodged) → one hit check → recovery → Chase.
    /// Being staggered or killed during the windup cancels the attack.
    /// </summary>
    public sealed class AttackState : IEnemyState
    {
        private readonly IEnemyContext context;
        private float elapsed;
        private bool resolved;

        public AttackState(IEnemyContext context) => this.context = context;

        public EnemyStateId Id => EnemyStateId.Attack;

        public void Enter()
        {
            elapsed = 0f;
            resolved = false;
            context.StopMoving();
            context.BeginAttack();
        }

        public void Tick(float deltaTime)
        {
            elapsed += deltaTime;

            if (!resolved)
            {
                context.FaceTarget(deltaTime);
                if (elapsed < context.Data.AttackWindup)
                    return;

                context.ResolveAttack();
                resolved = true;
                elapsed = 0f;
                return;
            }

            if (elapsed >= context.AttackRecovery)
                context.RequestState(EnemyStateId.Chase);
        }

        public void Exit()
        {
            if (!resolved)
                context.CancelAttack();
        }
    }
}
