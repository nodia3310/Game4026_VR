using System;

namespace Game4026.Enemy
{
    /// <summary>
    /// Holds one instance of every state (no allocation on transitions) and switches between them.
    /// Once Dead, only Spawn (pool reuse) can leave it.
    /// </summary>
    public sealed class EnemyStateMachine
    {
        private readonly IEnemyState[] states;

        public IEnemyState Current { get; private set; }
        public EnemyStateId CurrentId => Current != null ? Current.Id : EnemyStateId.None;

        /// <summary>(previous, next)</summary>
        public event Action<EnemyStateId, EnemyStateId> Changed;

        public EnemyStateMachine(params IEnemyState[] allStates)
        {
            states = new IEnemyState[Enum.GetValues(typeof(EnemyStateId)).Length];
            foreach (IEnemyState state in allStates)
                states[(int)state.Id] = state;
        }

        public static EnemyStateMachine CreateDefault(IEnemyContext context) => new EnemyStateMachine(
            new SpawnState(context),
            new IdleState(context),
            new PatrolState(context),
            new ChaseState(context),
            new AttackState(context),
            new HitState(context),
            new DeadState(context));

        public void ChangeState(EnemyStateId id)
        {
            if (CurrentId == EnemyStateId.Dead && id != EnemyStateId.Spawn)
                return;

            IEnemyState next = states[(int)id];
            if (next == null)
                throw new InvalidOperationException($"No state registered for {id}.");

            EnemyStateId previous = CurrentId;
            Current?.Exit();
            Current = next;
            next.Enter();
            Changed?.Invoke(previous, id);
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}
