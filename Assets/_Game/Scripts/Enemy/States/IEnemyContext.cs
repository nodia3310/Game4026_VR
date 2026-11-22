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
        /// <summary>Encounter enemies know where the player is: they chase at any distance and never lose the target.</summary>
        bool IsAlerted { get; }

        // Movement
        bool HasPatrolPoints { get; }
        bool ReachedDestination { get; }
        void SetMoveSpeed(float speed);
        void StopMoving();
        void MoveToNextPatrolPoint();
        void ChaseTarget();
        void FaceTarget(float deltaTime);

        // Attack
        /// <summary>Chase asks this (throttled): true when a move is in range and off cooldown; it is then started by <see cref="BeginAttack"/>.</summary>
        bool TrySelectAttack();
        /// <summary>While no move is ready, Chase holds position (facing the player) inside this distance.</summary>
        float AttackHoldRange { get; }
        void BeginAttack();
        /// <summary>No attack slot for this enemy right now (others are attacking, or it is outside the player's view).</summary>
        bool MustWaitForTurn { get; }
        /// <summary>Waiting distance from the player while <see cref="MustWaitForTurn"/>.</summary>
        float WaitDistance { get; }
        /// <summary>Walk around the player at <see cref="WaitDistance"/>, facing them, drifting toward their view.</summary>
        void CircleTarget(float deltaTime);
        /// <summary>One frame of the move (telegraph, swing, hit checks, recovery). False once it has finished.</summary>
        bool TickAttack(float deltaTime);
        void CancelAttack();

        // Stagger
        /// <summary>How long the current Hit state lasts (staggerDuration, or longer after a parry).</summary>
        float CurrentStaggerDuration { get; }

        // Lifecycle
        void SetInvulnerable(bool value);
        void OnDeath();
        void Despawn();

        void RequestState(EnemyStateId id);
    }
}
