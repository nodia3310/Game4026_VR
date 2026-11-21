using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>Spec 4.4 transition table, driven through a fake context (EnemyData defaults).</summary>
    public sealed class EnemyStateMachineTests
    {
        private EnemyData data;
        private FakeEnemyContext ctx;

        [SetUp]
        public void SetUp()
        {
            // Defaults: spawn 1.5, detect 12, lose 18, stagger 0.5, despawn 1.5; fake attack range 1.6, move 1.2 s
            data = ScriptableObject.CreateInstance<EnemyData>();
            ctx = new FakeEnemyContext(data);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(data);

        private void SeePlayerAt(float distance)
        {
            ctx.HasTarget = true;
            ctx.CanSeeTarget = true;
            ctx.TimeSinceTargetSeen = 0f;
            ctx.DistanceToTarget = distance;
        }

        private void StartIn(EnemyStateId id) => ctx.Machine.ChangeState(id);

        // ---- Spawn ----

        [Test]
        public void Spawn_IsInvulnerable_ThenIdleWithoutPatrolPoints()
        {
            StartIn(EnemyStateId.Spawn);
            Assert.IsTrue(ctx.Invulnerable);

            ctx.Run(1.4f);
            Assert.AreEqual(EnemyStateId.Spawn, ctx.State);

            ctx.Run(0.2f);
            Assert.AreEqual(EnemyStateId.Idle, ctx.State);
            Assert.IsFalse(ctx.Invulnerable);
        }

        [Test]
        public void Spawn_ThenPatrolWithPatrolPoints()
        {
            ctx.HasPatrolPoints = true;
            StartIn(EnemyStateId.Spawn);
            ctx.Run(1.6f);

            Assert.AreEqual(EnemyStateId.Patrol, ctx.State);
            Assert.AreEqual(data.MoveSpeed, ctx.MoveSpeed);
            Assert.AreEqual(1, ctx.PatrolMoves);
        }

        [Test]
        public void Spawn_IgnoresPlayerUntilFinished()
        {
            SeePlayerAt(3f);
            StartIn(EnemyStateId.Spawn);
            ctx.Run(1f);
            Assert.AreEqual(EnemyStateId.Spawn, ctx.State);
        }

        // ---- Idle / Patrol → Chase ----

        [Test]
        public void Idle_SeesPlayerInRange_Chases()
        {
            StartIn(EnemyStateId.Idle);
            SeePlayerAt(10f);
            ctx.Run(0.02f);

            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
        }

        [Test]
        public void Idle_PlayerOutOfDetectionRange_StaysIdle()
        {
            StartIn(EnemyStateId.Idle);
            SeePlayerAt(13f);
            ctx.Run(0.1f);
            Assert.AreEqual(EnemyStateId.Idle, ctx.State);
        }

        [Test]
        public void Idle_PlayerInRangeWithoutLineOfSight_StaysIdle()
        {
            StartIn(EnemyStateId.Idle);
            SeePlayerAt(5f);
            ctx.CanSeeTarget = false;
            ctx.Run(0.1f);
            Assert.AreEqual(EnemyStateId.Idle, ctx.State);
        }

        [Test]
        public void Idle_NoPlayerTarget_StaysIdleWithoutErrors()
        {
            StartIn(EnemyStateId.Idle);
            ctx.HasTarget = false;
            ctx.Run(0.5f);
            Assert.AreEqual(EnemyStateId.Idle, ctx.State);
        }

        [Test]
        public void Patrol_AdvancesOnArrival_AndChasesWhenPlayerSeen()
        {
            ctx.HasPatrolPoints = true;
            StartIn(EnemyStateId.Patrol);
            ctx.ReachedDestination = true;
            ctx.Run(0.02f);
            Assert.AreEqual(2, ctx.PatrolMoves);

            ctx.ReachedDestination = false;
            SeePlayerAt(8f);
            ctx.Run(0.02f);
            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
            Assert.AreEqual(data.ChaseSpeed, ctx.MoveSpeed);
        }

        // ---- Chase ----

        [Test]
        public void Chase_MovesTowardPlayer()
        {
            SeePlayerAt(6f);
            StartIn(EnemyStateId.Chase);
            ctx.Run(0.1f);
            Assert.Greater(ctx.ChaseCalls, 0);
            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
        }

        [Test]
        public void Chase_InAttackRangeAndReady_Attacks()
        {
            SeePlayerAt(1.5f);
            StartIn(EnemyStateId.Chase);
            ctx.Run(0.02f);
            Assert.AreEqual(EnemyStateId.Attack, ctx.State);
        }

        [Test]
        public void Chase_InAttackRangeOnCooldown_HoldsAndFacesPlayer()
        {
            SeePlayerAt(1.5f);
            ctx.IsAttackReady = false;
            StartIn(EnemyStateId.Chase);
            ctx.Run(0.1f);

            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
            Assert.IsFalse(ctx.Moving);
            Assert.Greater(ctx.FaceCalls, 0);
        }

        [Test]
        public void Chase_BeyondLoseRange_ReturnsToCalmState()
        {
            SeePlayerAt(19f);
            StartIn(EnemyStateId.Chase);
            ctx.Run(0.02f);
            Assert.AreEqual(EnemyStateId.Idle, ctx.State);
        }

        [Test]
        public void Chase_NoSightForMoreThanFiveSeconds_ReturnsToPatrol()
        {
            ctx.HasPatrolPoints = true;
            SeePlayerAt(8f);
            StartIn(EnemyStateId.Chase);

            ctx.CanSeeTarget = false;
            ctx.TimeSinceTargetSeen = 4.9f;
            ctx.Run(0.02f);
            Assert.AreEqual(EnemyStateId.Chase, ctx.State);

            ctx.TimeSinceTargetSeen = 5.1f;
            ctx.Run(0.02f);
            Assert.AreEqual(EnemyStateId.Patrol, ctx.State);
        }

        [Test]
        public void Chase_TargetDisappears_ReturnsToIdle()
        {
            SeePlayerAt(5f);
            StartIn(EnemyStateId.Chase);
            ctx.HasTarget = false;
            ctx.Run(0.02f);
            Assert.AreEqual(EnemyStateId.Idle, ctx.State);
        }

        // ---- Attack ----

        [Test]
        public void Attack_StopsAndPlaysMoveUntilRunnerFinishes_ThenChase()
        {
            SeePlayerAt(1.5f);
            ctx.Moving = true;
            StartIn(EnemyStateId.Attack);
            Assert.AreEqual(1, ctx.AttackBegins);
            Assert.IsFalse(ctx.Moving);

            ctx.Run(1.0f);
            Assert.AreEqual(EnemyStateId.Attack, ctx.State, "Move still playing.");

            ctx.Run(0.3f);
            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
            Assert.AreEqual(1, ctx.AttackFinishes);
            Assert.AreEqual(0, ctx.AttackCancels, "A finished move is not cancelled.");
        }

        [Test]
        public void Attack_StaggeredMidMove_Cancels()
        {
            SeePlayerAt(1.5f);
            StartIn(EnemyStateId.Attack);
            ctx.Run(0.3f);

            ctx.Machine.ChangeState(EnemyStateId.Hit);

            Assert.AreEqual(1, ctx.AttackCancels);
            Assert.AreEqual(0, ctx.AttackFinishes);
        }

        [Test]
        public void Chase_OutsideAllMoveRangesButBeyondHoldRange_KeepsChasing()
        {
            SeePlayerAt(4f);
            ctx.AttackSelectRange = 1.6f;
            StartIn(EnemyStateId.Chase);
            ctx.Run(0.1f);

            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
            Assert.Greater(ctx.ChaseCalls, 0);
        }

        [Test]
        public void Chase_FarMoveAvailable_AttacksFromRange()
        {
            SeePlayerAt(4f);
            ctx.AttackSelectRange = 5f; // e.g. a lunge with range 3-5
            StartIn(EnemyStateId.Chase);
            ctx.Run(0.02f);

            Assert.AreEqual(EnemyStateId.Attack, ctx.State);
        }

        // ---- Hit ----

        [Test]
        public void Hit_StopsForStaggerDuration_ThenChase()
        {
            SeePlayerAt(5f);
            StartIn(EnemyStateId.Chase);
            ctx.Machine.ChangeState(EnemyStateId.Hit);
            Assert.IsFalse(ctx.Moving);

            ctx.Run(0.4f);
            Assert.AreEqual(EnemyStateId.Hit, ctx.State);

            ctx.Run(0.2f);
            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
        }

        [Test]
        public void Hit_ReStagger_RestartsTimer()
        {
            StartIn(EnemyStateId.Hit);
            ctx.Run(0.4f);
            ctx.Machine.ChangeState(EnemyStateId.Hit);
            ctx.Run(0.4f);
            Assert.AreEqual(EnemyStateId.Hit, ctx.State);
        }

        // ---- Dead ----

        [Test]
        public void Dead_IgnoresOtherStates_AndDespawnsOnceAfterDelay()
        {
            StartIn(EnemyStateId.Chase);
            ctx.Machine.ChangeState(EnemyStateId.Dead);
            Assert.AreEqual(1, ctx.DeathCalls);

            ctx.Machine.ChangeState(EnemyStateId.Hit);
            ctx.Machine.ChangeState(EnemyStateId.Chase);
            Assert.AreEqual(EnemyStateId.Dead, ctx.State);

            ctx.Run(1.4f);
            Assert.AreEqual(0, ctx.DespawnCalls);
            ctx.Run(2f);
            Assert.AreEqual(1, ctx.DespawnCalls);
        }

        [Test]
        public void Dead_CanRespawnThroughSpawn()
        {
            StartIn(EnemyStateId.Dead);
            ctx.Machine.ChangeState(EnemyStateId.Spawn);
            Assert.AreEqual(EnemyStateId.Spawn, ctx.State);
        }

        [Test]
        public void Changed_ReportsPreviousAndNext()
        {
            EnemyStateId from = EnemyStateId.None, to = EnemyStateId.None;
            ctx.Machine.Changed += (a, b) => { from = a; to = b; };

            StartIn(EnemyStateId.Idle);
            StartIn(EnemyStateId.Chase);

            Assert.AreEqual(EnemyStateId.Idle, from);
            Assert.AreEqual(EnemyStateId.Chase, to);
        }

        [Test]
        public void Hit_UsesContextStaggerDuration_ForParryStagger()
        {
            SeePlayerAt(5f); // out of attack range, so Chase does not attack right away
            ctx.StaggerOverride = 1.5f;
            StartIn(EnemyStateId.Hit);

            ctx.Run(1.4f);
            Assert.AreEqual(EnemyStateId.Hit, ctx.State);
            ctx.Run(0.2f);
            Assert.AreEqual(EnemyStateId.Chase, ctx.State);
        }
    }
}
