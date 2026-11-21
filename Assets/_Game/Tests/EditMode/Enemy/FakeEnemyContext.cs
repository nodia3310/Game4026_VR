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
        /// <summary>A move is off cooldown (cleared when one finishes, like a cooldown starting).</summary>
        public bool IsAttackReady { get; set; } = true;
        /// <summary>Moves can be picked within this distance.</summary>
        public float AttackSelectRange = 1.6f;
        public float AttackHoldRange { get; set; } = 1.6f;
        /// <summary>How long a move plays before the runner reports it finished.</summary>
        public float AttackDuration = 1.2f;
        public float AttackElapsed;

        /// <summary>Negative = Data.StaggerDuration.</summary>
        public float StaggerOverride = -1f;
        public float CurrentStaggerDuration => StaggerOverride >= 0f ? StaggerOverride : Data.StaggerDuration;

        public bool Invulnerable;
        public bool Moving;
        public float MoveSpeed;
        public int PatrolMoves, ChaseCalls, FaceCalls;
        public int AttackBegins, AttackTicks, AttackFinishes, AttackCancels, AttackSelects;
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
        public bool TrySelectAttack()
        {
            AttackSelects++;
            return IsAttackReady && DistanceToTarget <= AttackSelectRange;
        }
        public void BeginAttack() { AttackBegins++; AttackElapsed = 0f; }
        public bool TickAttack(float deltaTime)
        {
            AttackTicks++;
            AttackElapsed += deltaTime;
            if (AttackElapsed < AttackDuration - 1e-5f)
                return true;
            AttackFinishes++;
            IsAttackReady = false;
            return false;
        }
        public void CancelAttack() => AttackCancels++;
        public void SetInvulnerable(bool value) => Invulnerable = value;
        public void OnDeath() => DeathCalls++;
        public void Despawn() => DespawnCalls++;
        public void RequestState(EnemyStateId id) => Machine.ChangeState(id);
    }
}
