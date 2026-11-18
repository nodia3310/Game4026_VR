using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    public sealed class EnemyAttackTests
    {
        private sealed class PlayerReceiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public int Hits;
            public float Total;
            public void ApplyPlayerDamage(float damage) { Hits++; Total += damage; }
        }

        private EnemyData data;
        private GameObject enemyGo;
        private GameObject playerGo;
        private EnemyAttack attack;
        private PlayerTarget target;
        private PlayerReceiver receiver;

        [SetUp]
        public void SetUp()
        {
            data = ScriptableObject.CreateInstance<EnemyData>(); // range 1.6 (+0.3 pad), angle 70, damage 10, cooldown 2
            enemyGo = new GameObject("Enemy");                   // at origin, facing +Z
            attack = enemyGo.AddComponent<EnemyAttack>();

            playerGo = new GameObject("Player");
            receiver = playerGo.AddComponent<PlayerReceiver>();
            target = playerGo.AddComponent<PlayerTarget>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(enemyGo);
            Object.DestroyImmediate(playerGo);
            Object.DestroyImmediate(data);
        }

        [Test]
        public void Resolve_PlayerInFrontAndInRange_DamagesOnce()
        {
            playerGo.transform.position = new Vector3(0f, 1.2f, 1.8f); // within 1.9 m, height ignored

            Assert.IsTrue(attack.Resolve(target, data, 0f));
            Assert.AreEqual(1, receiver.Hits);
            Assert.AreEqual(10f, receiver.Total, 1e-4f);
        }

        [Test]
        public void Resolve_PlayerStepsOutOfRange_Misses()
        {
            playerGo.transform.position = new Vector3(0f, 0f, 2.0f);

            Assert.IsFalse(attack.Resolve(target, data, 0f));
            Assert.AreEqual(0, receiver.Hits);
        }

        [Test]
        public void Resolve_PlayerOutsideCone_Misses()
        {
            playerGo.transform.position = new Vector3(1.2f, 0f, 1.0f); // ~50° off forward, cone half-angle 35°

            Assert.IsFalse(attack.Resolve(target, data, 0f));
        }

        [Test]
        public void Resolve_NoTarget_MissesWithoutError()
        {
            Assert.IsFalse(attack.Resolve(null, data, 0f));
        }

        [Test]
        public void Resolve_StartsCooldown_EvenOnMiss()
        {
            playerGo.transform.position = new Vector3(0f, 0f, 5f);
            attack.Resolve(target, data, 10f);

            Assert.IsFalse(attack.IsReady(11.9f));
            Assert.IsTrue(attack.IsReady(12f));
        }

        [Test]
        public void SpeedMultiplier_ShortensCooldownAndWindup()
        {
            attack.SpeedMultiplier = 2f;
            Assert.AreEqual(data.AttackWindup / 2f, attack.WindupFor(data), 1e-4f);

            attack.Resolve(target, data, 10f); // cooldown 2 s / 2 = 1 s
            Assert.IsFalse(attack.IsReady(10.9f));
            Assert.IsTrue(attack.IsReady(11f));
        }

        [Test]
        public void SpeedMultiplier_HasSafeMinimum_AndResets()
        {
            attack.SpeedMultiplier = 0f;
            Assert.AreEqual(0.1f, attack.SpeedMultiplier, 1e-5f);

            attack.ResetState();
            Assert.AreEqual(1f, attack.SpeedMultiplier);
        }

        [Test]
        public void ResetState_ClearsCooldown()
        {
            attack.Resolve(target, data, 10f);
            attack.ResetState();
            Assert.IsTrue(attack.IsReady(0f));
        }
    }
}
