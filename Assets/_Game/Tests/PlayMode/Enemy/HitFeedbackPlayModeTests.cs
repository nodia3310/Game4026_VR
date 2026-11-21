using System.Collections;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace Game4026.Tests.Enemy.PlayMode
{
    /// <summary>Hit stop ("卡肉") on a real enemy: the AI clock, NavMesh and attack timeline slow down, then ease back.</summary>
    public sealed class HitFeedbackPlayModeTests
    {
        private PlayModeWorld world;

        [SetUp]
        public void SetUp() => world = new PlayModeWorld();

        [TearDown]
        public void TearDown() => world.Dispose();

        private EnemyController Spawn(EnemyData data, bool withFeedback)
        {
            EnemyController template = world.Template("Enemy", data);
            var model = new GameObject("Model");
            model.transform.SetParent(template.transform, false);
            if (withFeedback)
                template.gameObject.AddComponent<EnemyHitFeedback>();
            return world.Pool.Spawn(template, Vector3.zero, Quaternion.identity);
        }

        [UnityTest]
        public IEnumerator HitStop_SlowsAiClockAndAgent_ThenEasesBack()
        {
            EnemyController enemy = Spawn(world.Data("{\"spawnDuration\":0.1}"), false);
            yield return null;
            NavMeshAgent agent = enemy.GetComponent<NavMeshAgent>();
            float baseSpeed = agent.speed;
            int changes = 0;
            enemy.HitStopChanged += (_, __) => changes++;

            float localBefore = enemy.LocalTime;
            float realBefore = Time.time;
            enemy.HitStop(0.25f, 0.1f);
            Assert.IsTrue(enemy.IsHitStopped);
            Assert.AreEqual(0.1f, enemy.TimeScale, 1e-5f);
            Assert.AreEqual(baseSpeed * 0.1f, agent.speed, 1e-4f, "The agent slows with the enemy clock.");

            yield return new WaitForSeconds(0.1f);
            Assert.IsTrue(enemy.IsHitStopped);
            Assert.AreEqual(localBefore, enemy.LocalTime, 0.03f, "AI clock nearly still.");
            Assert.Greater(enemy.DeltaTime, 0f, "Slow motion, not a freeze.");
            Assert.Less(enemy.DeltaTime, Time.deltaTime * 0.5f);

            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(enemy.IsHitStopped);
            Assert.AreEqual(1f, enemy.TimeScale);
            Assert.AreEqual(baseSpeed, agent.speed, 1e-4f);
            Assert.AreEqual(2, changes);
            float lag = (Time.time - realBefore) - (enemy.LocalTime - localBefore);
            Assert.AreEqual(HitStopCurve.TimeLost(0.25f, 0.1f), lag, 0.03f, "The slow motion's lost time is missing from the AI clock.");
        }

        [UnityTest]
        public IEnumerator HitStop_DelaysTheAttackTimeline()
        {
            var playerGo = world.Track(new GameObject("Player"));
            playerGo.transform.position = new Vector3(0f, 1.6f, 1.2f);
            playerGo.AddComponent<PlayerTarget>();
            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"staggerThreshold\":1000}");
            AttackDefinition swing = world.Track(AttackDefinition.Create(hitTime: 0.6f, telegraph: 0f, cooldown: 10f));
            data.SetAttacks(new[] { swing }, 0f);
            EnemyController enemy = Spawn(data, false);

            float started = -1f, resolved = -1f;
            enemy.Attack.AttackStarted += (_, __) => started = Time.time;
            enemy.Attack.AttackResolved += (_, __, ___) => resolved = Time.time;

            float deadline = Time.time + 4f;
            while (started < 0f && Time.time < deadline)
                yield return null;
            Assert.Greater(started, 0f, "The enemy should swing.");
            yield return new WaitForSeconds(0.2f);
            enemy.HitStop(0.3f); // timeScale 0: a freeze that still eases out

            deadline = Time.time + 3f;
            while (resolved < 0f && Time.time < deadline)
                yield return null;
            Assert.AreEqual(0.6f + HitStopCurve.TimeLost(0.3f, 0f), resolved - started, 0.06f, "The hit lands later by the lost time.");
        }

        [UnityTest]
        public IEnumerator SlowMotionPoint_SlowsTheEnemyMidMove_AndTheHitStillLands()
        {
            var playerGo = world.Track(new GameObject("Player"));
            playerGo.transform.position = new Vector3(0f, 1.6f, 1.2f);
            playerGo.AddComponent<PlayerTarget>();
            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"staggerThreshold\":1000}");
            AttackDefinition jump = world.Track(AttackDefinition.Create(hitTime: 0.6f, telegraph: 0f, cooldown: 10f, slowAt: 0.3f));
            data.SetAttacks(new[] { jump }, 0f);
            EnemyController enemy = Spawn(data, false);

            float started = -1f, slowed = -1f, resolved = -1f;
            enemy.Attack.AttackStarted += (_, __) => started = Time.time;
            enemy.HitStopChanged += (_, on) => { if (on && slowed < 0f) slowed = Time.time; };
            enemy.Attack.AttackResolved += (_, __, ___) => resolved = Time.time;

            float deadline = Time.time + 5f;
            while (resolved < 0f && Time.time < deadline)
                yield return null;
            Assert.Greater(started, 0f, "The enemy should attack.");
            Assert.AreEqual(0.3f, slowed - started, 0.06f, "Slow motion starts at the clip's slow-motion point.");
            float expected = 0.6f + HitStopCurve.TimeLost(jump.SlowMotionDuration, jump.SlowMotionScale);
            Assert.AreEqual(expected, resolved - started, 0.08f, "The strike lands later by the slowed time.");
        }

        [UnityTest]
        public IEnumerator Damage_PlaysReaction_ByKind()
        {
            EnemyData data = world.Data("{\"spawnDuration\":0.1,\"maxHealth\":30,\"staggerThreshold\":1000}");
            EnemyController enemy = Spawn(data, true);
            yield return new WaitForSeconds(0.2f); // out of spawn invulnerability
            var feedback = enemy.GetComponent<EnemyHitFeedback>();
            HitImpact last = default;
            int played = 0;
            feedback.Played += (_, i) => { last = i; played++; };
            int globalBefore = HitImpacts.Count;

            enemy.Health.TakeDamage(10f, enemy.transform.position + Vector3.up, false);
            Assert.AreEqual(1, played);
            Assert.AreEqual(HitKind.Normal, last.Kind);
            Assert.IsTrue(enemy.IsHitStopped);
            Assert.IsTrue(feedback.IsShaking);
            Assert.AreEqual(globalBefore + 1, HitImpacts.Count);
            Assert.Greater(last.HapticAmplitude, 0f);

            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(enemy.IsHitStopped);
            Assert.IsFalse(feedback.IsShaking);
            Assert.Less(feedback.Visual.localPosition.magnitude, 1e-5f, "Shake leaves no offset behind.");

            enemy.Health.TakeDamage(50f, enemy.transform.position + Vector3.up, false);
            Assert.AreEqual(HitKind.Kill, last.Kind);
            Assert.Greater(last.HitStop, 0.1f, "Kills stick longest.");
        }
    }
}
