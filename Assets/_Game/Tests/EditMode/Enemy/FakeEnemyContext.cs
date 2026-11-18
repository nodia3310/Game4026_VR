using Game4026.Enemy;

namespace Game4026.Tests.Enemy
{
    /// <summary>Plain-field <see cref="IEnemyContext"/> for driving states without NavMesh, physics or Play Mode.</summary>
    public sealed class FakeEnemyContext : IEnemyContext
    {
        public EnemyStateMachine Machine;

        public EnemyData Data { get; set; }
        public bool HasTarget { get; set; }
        public bool CanSeeTarget { get; set; }
        public float DistanceToTarget { get; set; } = float.PositiveInfinity;
        public float TimeSinceTargetSeen { get; set; }
        public float LoseSightSeconds { get; set; } = 5f;
        public bool HasPatrolPoints { get; set; }
        public bool ReachedDestination { get; set; }
        public bool IsAttackReady { get; set; } = true;
        /// <summary>Negative = use Data.AttackWindup.</summary>
        public float AttackWindupOverride = -1f;
        public float AttackWindup => AttackWindupOverride >= 0f ? AttackWindupOverride : Data.AttackWindup;
        public float AttackRecovery { get; set; } = 0.5f;
        public bool AttackHits { get; set; } = true;

        public bool Invulnerable;
        public bool Moving;
        public float MoveSpeed;
        public int PatrolMoves, ChaseCalls, FaceCalls;
        public int AttackBegins, AttackResolves, AttackCancels;
        public int DeathCalls, DespawnCalls;

        public FakeEnemyContext(EnemyData data)
        {
            Data = data;
            Machine = EnemyStateMachine.CreateDefault(this);
        }

        public EnemyStateId State => Machine.CurrentId;

        /// <summary>Advances the machine in small steps so timers cross their thresholds the way a frame loop would.</summary>
        public void Run(float seconds, float step = 0.02f)
        {
            for (float t = 0f; t < seconds - 1e-5f; t += step)
                Machine.Tick(step);
        }

        public void SetMoveSpeed(float speed) => MoveSpeed = speed;
        public void StopMoving() => Moving = false;
        public void MoveToNextPatrolPoint() { PatrolMoves++; Moving = true; }
        public void ChaseTarget() { ChaseCalls++; Moving = true; }
        public void FaceTarget(float deltaTime) => FaceCalls++;
        public void BeginAttack() => AttackBegins++;
        // Like EnemyAttack.Resolve: the hit check starts the cooldown.
        public bool ResolveAttack() { AttackResolves++; IsAttackReady = false; return AttackHits; }
        public void CancelAttack() => AttackCancels++;
        public void SetInvulnerable(bool value) => Invulnerable = value;
        public void OnDeath() => DeathCalls++;
        public void Despawn() => DespawnCalls++;
        public void RequestState(EnemyStateId id) => Machine.ChangeState(id);
    }
}
