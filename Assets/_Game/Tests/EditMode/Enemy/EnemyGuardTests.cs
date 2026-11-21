using System.Collections.Generic;
using Game4026.Contracts;
using Game4026.Enemy;
using NUnit.Framework;
using UnityEngine;

namespace Game4026.Tests.Enemy
{
    /// <summary>Guard steps on the attack timeline (wing shield): held without damage, cut short into the counter.</summary>
    public sealed class EnemyGuardTests
    {
        private sealed class PlayerReceiver : MonoBehaviour, IPlayerDamageReceiver
        {
            public readonly List<float> Hits = new List<float>();
            public void ApplyPlayerDamage(float damage) => Hits.Add(damage);
        }

        private readonly List<Object> created = new List<Object>();
        private GameObject enemyGo, playerGo;
        private EnemyAttack attack;
        private PlayerTarget target;
        private PlayerReceiver receiver;
        private EnemyData data;

        [SetUp]
        public void SetUp()
        {
            data = Track(ScriptableObject.CreateInstance<EnemyData>());
            enemyGo = new GameObject("Enemy"); // at origin, facing +Z
            attack = enemyGo.AddComponent<EnemyAttack>();
            playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, 1.2f, 1f);
            receiver = playerGo.AddComponent<PlayerReceiver>();
            target = playerGo.AddComponent<PlayerTarget>();
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

        private AttackDefinition GuardThenCounter(out AttackDefinition counter)
        {
            AttackDefinition guard = Track(AttackDefinition.Create(AttackType.Guard, telegraph: 1f, hitTime: 0.3f, poseTime: 0.3f,
                recovery: 0.05f, parryWindow: 0f, damage: 0f, state: "Block", name: "Guard"));
            counter = Track(AttackDefinition.Create(AttackType.Melee, hitTime: 0.2f, damage: 10f, name: "Counter"));
            return Track(AttackDefinition.Create(AttackType.Combo, combo: new[] { guard, counter }));
        }

        [Test]
        public void GuardStep_IsHeldWithoutDamageOrWarning_ThenCounters()
        {
            AttackDefinition combo = GuardThenCounter(out AttackDefinition counter);
            int telegraphs = 0;
            attack.AttackTelegraph += (_, __) => telegraphs++;
            Assert.IsTrue(attack.Begin(data, 0f, combo));

            attack.Tick(0f, 0.5f, target);
            Assert.IsTrue(attack.IsGuarding);
            Assert.IsFalse(attack.IsParryWindowOpen, "A guard cannot be parried.");
            Assert.AreEqual(0, telegraphs, "Raising a guard is not an attack warning.");

            attack.Tick(0f, 0.85f, target); // past the guard's end (1 s hold + 0.3 s pose)
            Assert.IsFalse(attack.IsGuarding);
            Assert.AreEqual(0, receiver.Hits.Count, "The guard itself never hurts.");

            for (int i = 0; i < 40 && attack.CurrentStep != counter; i++)
                attack.Tick(0f, 0.02f, target);
            Assert.AreEqual(counter, attack.CurrentStep);
            for (int i = 0; i < 40 && receiver.Hits.Count == 0; i++)
                attack.Tick(0f, 0.02f, target);
            Assert.AreEqual(new[] { 10f }, receiver.Hits.ToArray());
        }

        [Test]
        public void EndGuard_CutsTheGuardShort_AndGoesStraightToTheCounter()
        {
            AttackDefinition combo = GuardThenCounter(out AttackDefinition counter);
            Assert.IsTrue(attack.Begin(data, 0f, combo));
            attack.Tick(0f, 0.2f, target);
            Assert.IsTrue(attack.EndGuard());
            Assert.IsFalse(attack.EndGuard(), "Only once.");

            attack.Tick(0f, 0.01f, target); // resolves the guard
            attack.Tick(0f, 0.06f, target); // its short recovery
            Assert.AreEqual(counter, attack.CurrentStep, "The counter starts about 1 s before the full guard would end.");
        }

        [Test]
        public void Covers_OnlyTheFrontArc()
        {
            Transform t = enemyGo.transform; // facing +Z
            Assert.IsTrue(EnemyGuard.Covers(t, new Vector3(0f, 1f, 1f), 220f));
            Assert.IsTrue(EnemyGuard.Covers(t, new Vector3(1f, 1f, -0.2f), 220f), "The wings reach a bit past the sides.");
            Assert.IsFalse(EnemyGuard.Covers(t, new Vector3(0f, 1f, -1f), 220f), "Not from behind.");
        }
    }
}
