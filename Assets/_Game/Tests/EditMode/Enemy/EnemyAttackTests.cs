using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>EnemyAttack + AttackRunner: hit timing, hit area, combos, lunges, cancel, attack speed.</summary>
    public sealed class EnemyAttackTests
    {
        private sealed class PlayerReceiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public readonly List<float> Hits = new List<float>();
            public void ApplyPlayerDamage(float damage) => Hits.Add(damage);
        }

        private readonly List<Object> created = new List<Object>();
        private EnemyData data;
        private GameObject enemyGo;
        private GameObject playerGo;
        private EnemyAttack attack;
        private PlayerTarget target;
        private PlayerReceiver receiver;
        private float now;

        [SetUp]
        public void SetUp()
        {
            data = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyGo = new GameObject("Enemy"); // at origin, facing +Z, no NavMeshAgent / controller
            attack = enemyGo.AddComponent<EnemyAttack>();

            playerGo = new GameObject("Player");
            receiver = playerGo.AddComponent<PlayerReceiver>();
            target = playerGo.AddComponent<PlayerTarget>();
            now = 0f;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
            foreach (Object o in created)
                Object.DestroyImmediate(o);
            created.Clear();
        }

        private T Track<T>(T o) where T : Object { created.Add(o); return o; }

        private AttackDefinition Make(AttackType type = AttackType.Melee, float telegraph = 0f, float hitTime = 0.7f,
            float animSpeed = 1f, float recovery = 0.5f, float damage = 10f, float hitRange = 1.9f,
            float lungeDistance = 2f, AttackDefinition[] combo = null, float clipStart = 0f, float slamRadius = 2f) =>
            Track(AttackDefinition.Create(type, telegraph, hitTime, animSpeed, recovery, hitRange: hitRange,
                damage: damage, lungeDistance: lungeDistance, combo: combo, clipStart: clipStart, slamRadius: slamRadius));

        private void PlacePlayer(float x, float z) => playerGo.transform.position = new Vector3(x, 1.2f, z);

        private void Start(AttackDefinition move) => Assert.IsTrue(attack.Begin(data, now, move));

        /// <summary>Ticks in 10 ms steps; returns false if the move finished within the time.</summary>
        private bool Run(float seconds)
        {
            bool running = true;
            for (float t = 0f; t < seconds - 1e-5f; t += 0.01f)
            {
                now += 0.01f;
                running = attack.Tick(now, 0.01f, target);
                if (!running)
                    break;
            }
            return running;
        }

        // ---- Melee ----

        [Test]
        public void Melee_HitsAfterTelegraphPlusHitTimeOverAnimSpeed_Once()
        {
            PlacePlayer(0f, 1.5f);
            Start(Make(telegraph: 0.2f, hitTime: 0.6f, animSpeed: 1.5f)); // hit at 0.2 + 0.4 = 0.6 s

            Run(0.55f);
            Assert.AreEqual(0, receiver.Hits.Count, "No damage before the swing connects.");
            Run(0.1f);
            Assert.AreEqual(1, receiver.Hits.Count);
            Assert.AreEqual(10f, receiver.Hits[0], 1e-4f);
            Run(0.3f);
            Assert.AreEqual(1, receiver.Hits.Count, "One hit check per step.");
        }

        [Test]
        public void Melee_FinishesAfterRecovery()
        {
            PlacePlayer(0f, 1.5f);
            Start(Make(hitTime: 0.5f, recovery: 0.4f));

            Assert.IsTrue(Run(0.85f));
            Assert.IsFalse(Run(0.1f));
            Assert.IsFalse(attack.IsAttacking);
        }

        [Test]
        public void Melee_PlayerSteppedOutOfRange_Misses()
        {
            PlacePlayer(0f, 2.0f); // hit range 1.9
            bool? result = null;
            attack.AttackResolved += (_, __, hit) => result = hit;
            Start(Make());
            Run(1f);

            Assert.AreEqual(false, result);
            Assert.AreEqual(0, receiver.Hits.Count);
        }

        [Test]
        public void Melee_PlayerOutsideCone_Misses()
        {
            PlacePlayer(1.2f, 1.0f); // ~50° off forward, cone half-angle 35°
            Start(Make());
            Run(1f);
            Assert.AreEqual(0, receiver.Hits.Count);
        }

        [Test]
        public void Melee_NoTarget_RunsWithoutError()
        {
            Start(Make());
            for (int i = 0; i < 150; i++)
                attack.Tick(now += 0.01f, 0.01f, null);
            Assert.IsFalse(attack.IsAttacking);
        }

        [Test]
        public void AreaSlam_HitsAnyDirectionWithinRadius()
        {
            PlacePlayer(-1.5f, -1.0f); // behind and to the side, 1.8 m away
            Start(Make(AttackType.AreaSlam, slamRadius: 2f));
            Run(1f);
            Assert.AreEqual(1, receiver.Hits.Count);
        }

        // ---- Telegraph event / animation timing ----

        [Test]
        public void StepStarted_ReportsHoldAndHitTiming()
        {
            var timings = new List<AttackStepTiming>();
            int telegraphs = 0;
            attack.StepStarted += (_, t) => timings.Add(t);
            attack.AttackTelegraph += (_, __) => telegraphs++;

            // Pose at 70% of the hit time by default: 0.7 * 0.7 / 1 = 0.49 s.
            Start(Make(telegraph: 0.8f, hitTime: 0.7f, animSpeed: 1f));

            Assert.AreEqual(1, timings.Count);
            Assert.AreEqual(1, telegraphs);
            Assert.AreEqual(0.49f, timings[0].HoldStart, 1e-4f);
            Assert.AreEqual(0.8f, timings[0].HoldDuration, 1e-4f);
            Assert.AreEqual(1.5f, timings[0].HitDelay, 1e-4f);
            Assert.IsTrue(timings[0].IsLast);
        }

        [Test]
        public void RecoveryOverride_LongerThanMoveRecovery_IsUsed()
        {
            PlacePlayer(0f, 1.5f);
            attack.StepStarted += (a, _) => a.RecoveryOverride = 1.0f; // like EnemyAnimator letting the clip finish
            Start(Make(hitTime: 0.5f, recovery: 0.2f));

            Assert.IsTrue(Run(1.4f), "Still recovering: 0.5 + 1.0.");
            Assert.IsFalse(Run(0.2f));
        }

        // ---- Combo ----

        [Test]
        public void Combo_PlaysEveryStepInOrder_WithEachStepsDamage()
        {
            PlacePlayer(0f, 1.5f);
            AttackDefinition a = Make(hitTime: 0.3f, recovery: 0.05f, damage: 6f);
            AttackDefinition b = Make(hitTime: 0.3f, recovery: 0.05f, damage: 6f);
            AttackDefinition c = Make(hitTime: 0.3f, recovery: 0.05f, damage: 12f);
            AttackDefinition combo = Make(AttackType.Combo, telegraph: 0.4f, recovery: 0.6f, combo: new[] { a, null, b, c });

            var steps = new List<int>();
            attack.StepStarted += (_, t) => steps.Add(t.Index);
            Start(combo);
            bool running = Run(5f);

            Assert.IsFalse(running);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, steps, "Null steps are skipped.");
            CollectionAssert.AreEqual(new[] { 6f, 6f, 12f }, receiver.Hits);
        }

        [Test]
        public void Combo_TelegraphOnlyBeforeFirstStep_TotalTime()
        {
            PlacePlayer(0f, 1.5f);
            AttackDefinition step = Make(hitTime: 0.3f, recovery: 0.1f);
            AttackDefinition combo = Make(AttackType.Combo, telegraph: 0.4f, recovery: 0.5f, combo: new[] { step, step });
            // 0.4 + 0.3 hit, 0.1 gap, 0.3 hit, 0.5 recovery = 1.6 s
            Start(combo);
            Assert.IsTrue(Run(1.55f));
            Assert.IsFalse(Run(0.1f));
        }

        [Test]
        public void Combo_ClipStartTime_ShortensFollowUpSteps()
        {
            var timings = new List<AttackStepTiming>();
            attack.StepStarted += (_, t) => timings.Add(t);
            AttackDefinition followUp = Make(hitTime: 0.77f, animSpeed: 1.3f, clipStart: 0.25f);
            Start(Make(AttackType.Combo, combo: new[] { followUp }));

            Assert.AreEqual(0.25f, timings[0].ClipStartTime, 1e-4f);
            Assert.AreEqual(0.52f / 1.3f, timings[0].HitDelay, 1e-4f);
        }

        // ---- Lunge ----

        [Test]
        public void Lunge_MovesTowardPlayer_StopsShortOfThem_ThenHits()
        {
            PlacePlayer(0f, 4f);
            Start(Make(AttackType.Lunge, hitTime: 0.6f, lungeDistance: 5f));
            Run(0.7f);

            // Stops 0.9 m in front of the player (default lungeStopDistance).
            Assert.AreEqual(3.1f, enemyGo.transform.position.z, 0.02f);
            Assert.AreEqual(1, receiver.Hits.Count);
        }

        [Test]
        public void Lunge_LimitedByLungeDistance()
        {
            PlacePlayer(0f, 6f);
            Start(Make(AttackType.Lunge, hitTime: 0.6f, lungeDistance: 2f));
            Run(0.7f);

            Assert.AreEqual(2f, enemyGo.transform.position.z, 0.02f);
            Assert.AreEqual(0, receiver.Hits.Count, "Landed 4 m short: miss.");
        }

        [Test]
        public void Lunge_TurnsTowardPlayerAtLungeStart()
        {
            PlacePlayer(3f, 0f); // to the right
            Start(Make(AttackType.Lunge, hitTime: 0.4f, lungeDistance: 5f));
            Run(0.5f);

            Assert.AreEqual(2.1f, enemyGo.transform.position.x, 0.02f);
            Assert.AreEqual(1, receiver.Hits.Count);
        }

        // ---- Cancel / speed / reset ----

        [Test]
        public void Cancel_BeforeHit_NoDamage_RaisesCancelled()
        {
            PlacePlayer(0f, 1.5f);
            int cancelled = 0;
            attack.AttackCancelled += (_, __) => cancelled++;
            Start(Make(hitTime: 0.7f));
            Run(0.3f);

            attack.Cancel(now);
            Run(1f);

            Assert.AreEqual(1, cancelled);
            Assert.AreEqual(0, receiver.Hits.Count);
            Assert.IsFalse(attack.IsAttacking);
        }

        [Test]
        public void SpeedMultiplier_ShortensTelegraphSwingAndRecovery()
        {
            PlacePlayer(0f, 1.5f);
            attack.SpeedMultiplier = 2f;
            Start(Make(telegraph: 0.4f, hitTime: 0.6f, recovery: 0.4f)); // (0.4 + 0.6) / 2 = 0.5 hit, + 0.2

            Run(0.52f);
            Assert.AreEqual(1, receiver.Hits.Count);
            Assert.IsFalse(Run(0.2f));
        }

        [Test]
        public void SpeedMultiplier_HasSafeMinimum_AndResets()
        {
            attack.SpeedMultiplier = 0f;
            Assert.AreEqual(0.1f, attack.SpeedMultiplier, 1e-5f);

            attack.ResetState();
            Assert.AreEqual(1f, attack.SpeedMultiplier);
        }

        // ---- Selection through EnemyAttack ----

        [Test]
        public void TrySelect_UsesDataAttacks_AndStartsCooldown()
        {
            AttackDefinition melee = Make();
            data.SetAttacks(new[] { melee }, 0f);

            Assert.IsTrue(attack.TrySelect(data, 1f, now));
            Assert.IsTrue(attack.Begin(data, now));
            Assert.AreSame(melee, attack.Current);
            attack.Cancel(now);

            Assert.IsFalse(attack.TrySelect(data, 1f, now + 1.9f), "Own cooldown (2 s) from the start.");
            Assert.IsTrue(attack.TrySelect(data, 1f, now + 2.0f));
        }

        [Test]
        public void EmptyData_FallsBackToDefaultMelee()
        {
            Assert.AreEqual(1, data.Attacks.Length);
            Assert.AreEqual(AttackType.Melee, data.Attacks[0].Type);
            Assert.AreEqual(1.6f, attack.HoldRange(data), 1e-4f);
            Assert.IsTrue(attack.TrySelect(data, 1.5f, 0f));
            Assert.IsFalse(attack.TrySelect(data, 1.7f, 0f));
        }

        [Test]
        public void ResetState_StopsMoveAndClearsCooldowns()
        {
            data.SetAttacks(new[] { Make() }, 5f);
            attack.TrySelect(data, 1f, 0f);
            attack.Begin(data, 0f);
            attack.Cancel(0f);

            attack.ResetState();

            Assert.IsFalse(attack.IsAttacking);
            Assert.IsTrue(attack.TrySelect(data, 1f, 0f));
        }

        // ---- Parry ----

        private AttackDefinition Parryable(float window, float hitTime = 0.7f, AttackDefinition[] combo = null,
            AttackType type = AttackType.Melee) =>
            Track(AttackDefinition.Create(type, hitTime: hitTime, combo: combo, parryWindow: window));

        [Test]
        public void ParryWindow_OpensWindowSecondsBeforeHit_ClosesAtHit()
        {
            PlacePlayer(0f, 1.5f);
            var opened = new List<float>();
            attack.ParryWindowOpened += (_, t) => opened.Add(now);
            Start(Parryable(0.3f)); // hit 0.7 → window 0.4–0.7

            Run(0.35f);
            Assert.IsFalse(attack.IsParryWindowOpen);
            Run(0.1f);
            Assert.IsTrue(attack.IsParryWindowOpen);
            Assert.AreEqual(1, opened.Count);
            Assert.AreEqual(0.4f, opened[0], 0.015f);
            Run(0.3f);
            Assert.IsFalse(attack.IsParryWindowOpen, "Closed after the hit check.");
            Assert.AreEqual(1, receiver.Hits.Count);
        }

        [Test]
        public void TryParry_InsideWindow_CancelsMove_NoDamage_RaisesParried()
        {
            PlacePlayer(0f, 1.5f);
            AttackDefinition parried = null;
            int cancelled = 0, finished = 0;
            attack.Parried += (_, move, step) => parried = step;
            attack.AttackCancelled += (_, __) => cancelled++;
            attack.AttackFinished += (_, __) => finished++;
            AttackDefinition move = Parryable(0.35f);
            Start(move);
            Run(0.5f);

            Assert.IsTrue(attack.TryParry(now));
            Assert.AreSame(move, parried);
            Assert.IsFalse(attack.IsAttacking);
            Assert.IsFalse(Run(1f));
            Assert.AreEqual(0, receiver.Hits.Count);
            Assert.AreEqual(0, cancelled, "Parried, not cancelled.");
            Assert.AreEqual(0, finished);
        }

        [Test]
        public void TryParry_BeforeWindowOrWhenIdle_Fails()
        {
            Assert.IsFalse(attack.TryParry(now), "Not attacking.");
            PlacePlayer(0f, 1.5f);
            Start(Parryable(0.2f));
            Run(0.3f);
            Assert.IsFalse(attack.TryParry(now));
            Assert.IsTrue(attack.IsAttacking);
        }

        [Test]
        public void Unparryable_WindowZero_NeverOpens()
        {
            int opened = 0;
            attack.ParryWindowOpened += (_, __) => opened++;
            Start(Parryable(0f));
            for (int i = 0; i < 65; i++)
            {
                attack.Tick(now += 0.01f, 0.01f, target);
                Assert.IsFalse(attack.TryParry(now));
            }
            Assert.AreEqual(0, opened);
        }

        [Test]
        public void ParryWindow_ScalesWithSpeedMultiplier()
        {
            attack.SpeedMultiplier = 2f;
            Start(Parryable(0.4f)); // hit 0.35, window 0.2 → opens at 0.15
            Run(0.12f);
            Assert.IsFalse(attack.IsParryWindowOpen);
            Run(0.05f);
            Assert.IsTrue(attack.IsParryWindowOpen);
        }

        [Test]
        public void Combo_ParryOneStep_CancelsTheRest()
        {
            PlacePlayer(0f, 1.5f);
            AttackDefinition step = Parryable(0.3f, hitTime: 0.3f);
            AttackDefinition combo = Parryable(0.3f, combo: new[] { step, step, step }, type: AttackType.Combo);
            int windows = 0;
            attack.ParryWindowOpened += (_, __) => windows++;
            Start(combo);

            Run(0.32f);                       // step 1 hit (0.3)
            Assert.AreEqual(1, receiver.Hits.Count);
            Run(0.6f);                        // into step 2's window
            Assert.AreEqual(2, windows, "Every step has its own window.");
            Assert.IsTrue(attack.TryParry(now));
            Assert.IsFalse(Run(2f));
            Assert.AreEqual(1, receiver.Hits.Count, "Steps 2 and 3 never land.");
        }

        [Test]
        public void TryParry_StartsGlobalCooldown()
        {
            AttackDefinition move = Parryable(0.35f);
            data.SetAttacks(new[] { move }, 1f);
            Assert.IsTrue(attack.TrySelect(data, 1f, 0f));
            attack.Begin(data, 0f);
            Run(0.5f);
            attack.TryParry(now);

            Assert.IsFalse(attack.TrySelect(data, 1f, now + 0.9f));
        }

        // ---- Statue moves ----

        [Test]
        public void LeapSlam_JumpsToPlayer_ThenAreaHit()
        {
            PlacePlayer(0f, 5f);
            AttackDefinition leap = Track(AttackDefinition.Create(AttackType.AreaSlam, hitTime: 0.6f, lungeDistance: 7f,
                slamRadius: 1.8f, leap: true, stopDistance: 0.3f));
            Start(leap);
            Run(0.7f);

            Assert.AreEqual(4.7f, enemyGo.transform.position.z, 0.02f, "Lands 0.3 m short of the player.");
            Assert.AreEqual(1, receiver.Hits.Count);
        }

        [Test]
        public void AreaSlam_WithoutLeap_StaysInPlace()
        {
            PlacePlayer(0f, 5f);
            Start(Make(AttackType.AreaSlam, slamRadius: 1.8f));
            Run(1f);
            Assert.AreEqual(0f, enemyGo.transform.position.z, 1e-4f);
            Assert.AreEqual(0, receiver.Hits.Count, "Player 5 m away: outside the radius.");
        }

        [Test]
        public void Lunge_PerMoveStopDistance_OverridesDefault()
        {
            PlacePlayer(0f, 6f);
            Start(Track(AttackDefinition.Create(AttackType.Lunge, hitTime: 0.5f, lungeDistance: 9f, stopDistance: 0.6f)));
            Run(0.6f);
            Assert.AreEqual(5.4f, enemyGo.transform.position.z, 0.02f);
        }

        [Test]
        public void StatueMotion_AutoFollowsType_ExplicitWins()
        {
            Assert.AreEqual(StatueMotion.Hop, Make(AttackType.Lunge).Motion);
            Assert.AreEqual(StatueMotion.Slam, Make(AttackType.AreaSlam).Motion);
            Assert.AreEqual(StatueMotion.Shoot, Make(AttackType.Projectile).Motion);
            Assert.AreEqual(StatueMotion.Roll, Track(AttackDefinition.Create(AttackType.Lunge, motion: StatueMotion.Roll)).Motion);
        }

        [Test]
        public void Projectile_NeedsAPrefab_ToBeSelectable()
        {
            Assert.IsFalse(Make(AttackType.Projectile).IsSupported);
            var prefab = Track(new GameObject("BoltPrefab")).AddComponent<EnemyProjectile>();
            Assert.IsTrue(Track(AttackDefinition.Create(AttackType.Projectile, projectile: prefab)).IsSupported);
        }

        [Test]
        public void StatueLocomotion_HopAndArcHelpers()
        {
            Assert.AreEqual(0f, StatueLocomotion.HopHeight(1f, 0.2f), 1e-5f, "On the ground between hops.");
            Assert.AreEqual(0.2f, StatueLocomotion.HopHeight(0.5f, 0.2f), 1e-5f);
            Assert.AreEqual(0f, StatueLocomotion.ArcHeight(0f, 1.6f), 1e-5f);
            Assert.AreEqual(1.6f, StatueLocomotion.ArcHeight(0.5f, 1.6f), 1e-5f);
            Assert.AreEqual(0f, StatueLocomotion.ArcHeight(1f, 1.6f), 1e-5f);
        }
    }
}
