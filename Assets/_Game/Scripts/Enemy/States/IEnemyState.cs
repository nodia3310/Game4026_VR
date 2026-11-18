namespace Game4026.Enemy
{
    /// <summary>One enemy AI state. Implementations hold no engine references; they act through <see cref="IEnemyContext"/>.</summary>
    public interface IEnemyState
    {
        EnemyStateId Id { get; }
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
