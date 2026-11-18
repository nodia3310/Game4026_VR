namespace Game4026.Enemy
{
    /// <summary>
    /// What a state can read and do. <see cref="EnemyController"/> implements it with NavMesh, physics and
    /// <see cref="EnemyAttack"/>; tests implement it with plain fields.
    /// </summary>
    public interface IEnemyContext
    {
        EnemyData Data { get; }

        // Perception (CanSeeTarget / TimeSinceTargetSeen update on the perception interval, not every frame).
        bool HasTarget { get; }
        bool CanSeeTarget { get; }
        float DistanceToTarget { get; }
        float TimeSinceTargetSeen { get; }
        /// <summary>Chase gives up after this long without line of sight (spec: 5 s).</summary>
        float LoseSightSeconds { get; }

        // Movement
        bool HasPatrolPoints { get; }
        bool ReachedDestination { get; }
        void SetMoveSpeed(float speed);
        void StopMoving();
        void MoveToNextPatrolPoint();
        void ChaseTarget();
        void FaceTarget(float deltaTime);

        // Attack
        bool IsAttackReady { get; }
        /// <summary>Windup seconds, already scaled by attack speed.</summary>
        float AttackWindup { get; }
        float AttackRecovery { get; }
        void BeginAttack();
        /// <summary>Runs the hit check once and starts the cooldown. Returns true on a hit.</summary>
        bool ResolveAttack();
        void CancelAttack();

        // Lifecycle
        void SetInvulnerable(bool value);
        void OnDeath();
        void Despawn();

        void RequestState(EnemyStateId id);
    }
}
